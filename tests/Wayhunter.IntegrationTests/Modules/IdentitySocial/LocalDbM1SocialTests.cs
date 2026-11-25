using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Contracts;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    [LocalDbM1Fact]
    public Task VerifyRemainingM1Operations()
    {
        var catalog = Environment.GetEnvironmentVariable("WAYHUNTER_M1_CATALOG");
        Assert.True(catalog is not null && Regex.IsMatch(catalog, @"\AWayhunter_M1_Test_[a-f0-9]{32}\z"),
            "Use a freshly created, dedicated M1 test catalog.");
        return RunIsolatedAsync(catalog!, migrateFreshCatalog: true,
            (client, db, capture, clock, _) =>
            {
                clock.Offset = TimeSpan.Zero;
                return VerifySocialAsync(client, db, capture);
            });
    }

    private async Task VerifySocialAsync(HttpClient client, WayhunterSharedDbContext db, Capture capture)
    {
        const string profile = "/" + IdentityApiContract.MyProfileRoute;
        const string blocks = "/api/mobile/identity/blocks/";
        var owner = await CreateUserAsync("Owner");
        var other = await CreateUserAsync("Other");
        var target = blocks + other.Id;
        var self = blocks + owner.Id;
        var missing = blocks + Guid.NewGuid().ToString("D");
        const string malformed = blocks + "invalid";
        var reverse = blocks + owner.Id;
        var outcomes = new List<bool>();
        bool profilePrivacy = false;

        var update = new
        {
            displayName = "  Updated Owner  ",
            bio = "M1 integration profile",
            userId = other.Id,
            email = other.Email,
            avatarUrl = "https://example.invalid/avatar"
        };

        SetBearer(null);
        var anonymousUpdate = await PutAsync(update);
        var anonymousBlock = await SendAsync(HttpMethod.Post, target);
        var anonymousUnblock = await SendAsync(HttpMethod.Delete, target);

        SetBearer(owner.Token);
        var blank = await PutAsync(new { displayName = " " });
        var longName = await PutAsync(new { displayName = new string('a', 201) });
        var longBio = await PutAsync(new { displayName = "Valid", bio = new string('b', 501) });
        var invalidUnchanged = await db.Users.AnyAsync(x => x.UserId == owner.Id && x.DisplayName == "Owner");
        var changed = await PutAsync(update, inspectOwner: true);
        var ownerRow = await db.Users.AsNoTracking().SingleAsync(x => x.UserId == owner.Id);
        var otherRow = await db.Users.AsNoTracking().SingleAsync(x => x.UserId == other.Id);
        var ownerProfile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner.Id);
        var otherHasOwnerBio = await db.UserProfiles.AnyAsync(x => x.UserId == other.Id && x.Bio == update.bio);
        Check("PUT profile", [401, 400, 400, 400, 200],
            [anonymousUpdate, blank, longName, longBio, changed],
            invalidUnchanged && ownerRow.DisplayName == "Updated Owner" && ownerRow.NormalizedEmail == owner.Email &&
            otherRow.DisplayName == "Other" && otherRow.NormalizedEmail == other.Email &&
            ownerProfile?.Bio == update.bio && ownerProfile.AvatarUrl is null && !otherHasOwnerBio);

        var invalidBlock = await SendAsync(HttpMethod.Post, malformed);
        var selfBlock = await SendAsync(HttpMethod.Post, self);
        var missingBlock = await SendAsync(HttpMethod.Post, missing);
        var noInvalidRows = !await db.BlockedUserRelationships.AnyAsync();
        var blocked = await SendAsync(HttpMethod.Post, target);
        var repeatedBlock = await SendAsync(HttpMethod.Post, target);
        var edges = await db.BlockedUserRelationships.AsNoTracking().ToListAsync();
        Check("POST block", [401, 400, 400, 404, 204, 204],
            [anonymousBlock, invalidBlock, selfBlock, missingBlock, blocked, repeatedBlock],
            noInvalidRows && edges.Count == 1 && edges[0].BlockerUserId == owner.Id &&
            edges[0].BlockedUserId == other.Id && edges[0].CreatedAtUtc != default);

        SetBearer(other.Token);
        var attemptedReverse = await SendAsync(HttpMethod.Delete, reverse);
        var ownerEdgeRetained = await db.BlockedUserRelationships.AnyAsync(
            x => x.BlockerUserId == owner.Id && x.BlockedUserId == other.Id);
        SetBearer(owner.Token);
        var invalidUnblock = await SendAsync(HttpMethod.Delete, malformed);
        var selfUnblock = await SendAsync(HttpMethod.Delete, self);
        var missingUnblock = await SendAsync(HttpMethod.Delete, missing);
        var invalidRetained = await db.BlockedUserRelationships.CountAsync() == 1;
        var unblocked = await SendAsync(HttpMethod.Delete, target);
        var repeatedUnblock = await SendAsync(HttpMethod.Delete, target);
        Check("DELETE unblock", [401, 204, 400, 400, 404, 204, 204],
            [anonymousUnblock, attemptedReverse, invalidUnblock, selfUnblock, missingUnblock, unblocked, repeatedUnblock],
            ownerEdgeRetained && invalidRetained && !await db.BlockedUserRelationships.AnyAsync());

        Assert.True(outcomes.Count == 3 && outcomes.All(result => result), "See sanitized M1 case results.");

        void SetBearer(string? token) => client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

        async Task<int> SendAsync(HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request);
            return (int)response.StatusCode;
        }

        async Task<int> PutAsync(object payload, bool inspectOwner = false)
        {
            using var response = await client.PutAsJsonAsync(profile, payload);
            if (inspectOwner && response.IsSuccessStatusCode)
            {
                using var body = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
                var json = body.RootElement;
                var fields = json.EnumerateObject().Select(property => property.Name).ToHashSet();
                profilePrivacy = fields.SetEquals(["userId", "displayName", "avatarUrl", "bio", "email", "phoneNumber"]) &&
                    json.GetProperty("userId").GetString() == owner.Id &&
                    json.GetProperty("email").GetString() == owner.Email &&
                    json.GetProperty("displayName").GetString() == "Updated Owner" &&
                    json.GetProperty("bio").GetString() == update.bio;
                output.WriteLine($"PUT profile response privacy: {(profilePrivacy ? "PASS" : "FAIL")}.");
            }
            return (int)response.StatusCode;
        }

        void Check(string operation, int[] expected, int[] actual, bool databaseEvidence)
        {
            bool passed = expected.SequenceEqual(actual) && databaseEvidence &&
                (operation != "PUT profile" || profilePrivacy);
            outcomes.Add(passed);
            output.WriteLine($"{operation}: {(passed ? "PASS" : "FAIL")}, expected {string.Join("/", expected)}, actual {string.Join("/", actual)}, SQL evidence {databaseEvidence}.");
        }

        async Task<(string Id, string Email, string Token)> CreateUserAsync(string displayName)
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            var password = Guid.NewGuid().ToString("N") + "A!";
            using var register = await client.PostAsJsonAsync("/" + IdentityApiContract.RegisterRoute,
                new { displayName, email, password, confirmPassword = password });
            Assert.Equal(201, (int)register.StatusCode);
            using var verify = await client.PostAsJsonAsync("/" + IdentityApiContract.VerifyOtpRoute,
                new { email, code = capture.Codes.Last() });
            Assert.Equal(204, (int)verify.StatusCode);
            using var login = await client.PostAsJsonAsync("/" + IdentityApiContract.LoginRoute, new { email, password });
            Assert.Equal(200, (int)login.StatusCode);
            using var body = (await login.Content.ReadFromJsonAsync<JsonDocument>())!;
            return (body.RootElement.GetProperty("userId").GetString()!, email, body.RootElement.GetProperty("accessToken").GetString()!);
        }
    }
}

public sealed class LocalDbM1FactAttribute : FactAttribute
{
    public LocalDbM1FactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_LOCAL_M1_SOCIAL") != "1")
            Skip = "Opt in with WAYHUNTER_LOCAL_M1_SOCIAL=1 and a fresh dedicated LocalDB catalog.";
    }
}
