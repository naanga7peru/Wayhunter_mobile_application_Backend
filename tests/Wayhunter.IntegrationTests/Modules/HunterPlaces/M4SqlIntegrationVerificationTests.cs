
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

// Skip SQL tests unless an isolated test DB is configured.
public sealed class M4SqlIntegrationFactAttribute : FactAttribute
{
    public M4SqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION")))
        {
            Skip = "M4 isolated SQL test database is not configured.";
        }
    }
}

public sealed class M4SqlIntegrationVerificationTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "M4 SQL test connection is missing.");
        }

        var builder = new SqlConnectionStringBuilder(
            connectionString);

        // Never run these data-writing tests on the
        // application or shared development database.
        if (!builder.InitialCatalog.StartsWith(
                "Wayhunter_M4_IntegrationTest",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only an isolated Wayhunter_M4_IntegrationTest database is allowed.");
        }

        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static async Task<(
        Guid HunterId,
        string UserId,
        Guid SubmissionId,
        Guid SessionId)> SeedAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var user = UserAccount.RegisterPending(
            "M4 SQL Test Hunter",
            $"m4-sql-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            now);

        user.ActivateAfterVerifiedOtp(now);

        var submission = new HunterSubmission
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            UserNameSnapshot = "M4 SQL Test Hunter",
            PlaceName = "M4 SQL Test Place",
            District = "Jaffna",
            Latitude = 9.66845,
            Longitude = 80.0074,
            Status = HunterSubmissionStatus.Draft,
            CreatedAtUtc = now
        };

        var session = new HunterCaptureSessionRow
        {
            Id = Guid.NewGuid(),
            HunterId = Guid.Parse(user.UserId),
            HunterSubmissionId = submission.Id,
            IssuedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(10),
            IsConsumed = false,
            IsRevoked = false
        };

        await using var db = CreateContext();

        db.Users.Add(user);
        db.HunterSubmissions.Add(submission);
        db.HunterCaptureSessions.Add(session);

        await db.SaveChangesAsync();

        return (
            Guid.Parse(user.UserId),
            user.UserId,
            submission.Id,
            session.Id);
    }

    private static async Task CleanupAsync(
        Guid submissionId,
        string userId)
    {
        await using var db = CreateContext();

        await db.HunterCaptureSessions
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterSubmissions
            .Where(x => x.Id == submissionId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    [M4SqlIntegrationFact]
    public async Task ConcurrentConsume_ExactlyOne_ShouldSucceed()
    {
        var seed = await SeedAsync();

        try
        {
            async Task<bool> ConsumeAsync()
            {
                await using var db = CreateContext();

                var store =
                    new SqlHunterCaptureSessionStore(db);

                return await store.TryConsumeAsync(
                    seed.SessionId,
                    seed.HunterId,
                    seed.SubmissionId,
                    DateTimeOffset.UtcNow);
            }

            // Independent DbContexts simulate two requests.
            var results = await Task.WhenAll(
                ConsumeAsync(),
                ConsumeAsync());


            Assert.Single(results, x => x);


            await using var verifyDb = CreateContext();

            var consumed = await verifyDb
                .HunterCaptureSessions
                .AsNoTracking()
                .Where(x => x.Id == seed.SessionId)
                .Select(x => x.IsConsumed)
                .SingleAsync();

            Assert.True(consumed);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }

    [M4SqlIntegrationFact]
    public async Task StatusTransition_ShouldRevokeUnusedSessions()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var db = CreateContext())
            {
                var store =
                    new SqlHunterSubmissionStatusTransitionStore(db);

                var changed = await store
                    .TryTransitionAndRevokeCaptureSessionsAsync(
                        seed.SubmissionId,
                        HunterSubmissionStatus.Draft,
                        HunterSubmissionStatus.PendingReview);

                Assert.True(changed);
            }

            await using var verifyDb = CreateContext();

            var status = await verifyDb.HunterSubmissions
                .AsNoTracking()
                .Where(x => x.Id == seed.SubmissionId)
                .Select(x => x.Status)
                .SingleAsync();

            var revoked = await verifyDb.HunterCaptureSessions
                .AsNoTracking()
                .Where(x => x.Id == seed.SessionId)
                .Select(x => x.IsRevoked)
                .SingleAsync();

            Assert.Equal(
                HunterSubmissionStatus.PendingReview,
                status);

            Assert.True(revoked);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }
}
