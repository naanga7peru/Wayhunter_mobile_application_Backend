using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Wayhunter.Infrastructure.Persistence.Seeding;

namespace Wayhunter.DemoSeedRunner;

public sealed class DemoSeedRunnerService(DemoSeedOptions options)
{
    private static readonly DateTimeOffset SeedTime = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private readonly DbContextOptions<WayhunterSharedDbContext> _dbOptions =
        new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(options.ConnectionString, sql => sql.EnableRetryOnFailure(3))
            .Options;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await VerifyTargetAsync(cancellationToken);
        var users = await SeedAccountsAndProfilesAsync(cancellationToken);
        var trips = await SeedTripsAsync(users, cancellationToken);
        await SeedSafetyAsync(users, trips, cancellationToken);
        await SeedHunterExamplesAsync(users, cancellationToken);
        await SeedGameDraftsAsync(users, cancellationToken);
        await SeedWalletRewardsAsync(users, cancellationToken);
        if (options.SeedVoucher)
            await SeedVoucherAsync(users[^1], cancellationToken);
        await SeedAdvertisementsAsync(cancellationToken);
        var voucherSummary = options.SeedVoucher ? "1 voucher offer/inventory" : "voucher offer/inventory skipped by explicit option";
        Console.WriteLine($"Seed complete for {options.Catalog}: 10 accounts, 5 trips, 2 Hunter drafts, 2 Game drafts, 10 welcome rewards, {voucherSummary}, 2 advertisements.");
        Console.WriteLine("No OTP, media receipt, notification delivery or external provider success was asserted.");
    }

    private async Task VerifyTargetAsync(CancellationToken ct)
    {
        await using var db = new WayhunterSharedDbContext(_dbOptions);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT DB_NAME(), state_desc, is_read_only FROM sys.databases WHERE name = DB_NAME();";
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) ||
                !string.Equals(reader.GetString(0), options.Catalog, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(reader.GetString(1), "ONLINE", StringComparison.Ordinal) ||
                reader.GetBoolean(2))
                throw new InvalidOperationException("Target database did not pass exact catalog/ONLINE/read-write checks.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
        var requiredTables = new[]
        {
            "Users", "UserProfiles", "UserRoleAssignments", "Trips", "TripStops",
            "TripSafety_LiveTripSessions", "TripSafety_SafeCheckIns", "TripSafety_SafeCheckInOutbox",
            "TripSafety_HazardReports", "TripSafety_TripMemories", "Hunter_Submissions",
            "Games", "GameSteps", "GameHints", "CoinWallets", "CoinLedgerEntries",
            "SampleVoucherOffers", "SampleVoucherInventory", "Advertisements"
        };
        await using var schema = new WayhunterSharedDbContext(_dbOptions);
        await schema.Database.OpenConnectionAsync(ct);
        await using var schemaCommand = schema.Database.GetDbConnection().CreateCommand();
        schemaCommand.CommandText = string.Join(" UNION ALL ", requiredTables.Select((_, i) =>
            $"SELECT N'{requiredTables[i]}' AS TableName WHERE OBJECT_ID(N'dbo.{requiredTables[i]}', N'U') IS NULL"));
        await using var missing = await schemaCommand.ExecuteReaderAsync(ct);
        var missingTables = new List<string>();
        while (await missing.ReadAsync(ct)) missingTables.Add(missing.GetString(0));
        if (missingTables.Count > 0)
            throw new InvalidOperationException("Seed target schema is incomplete; apply reviewed migrations separately first. Missing: " + string.Join(", ", missingTables));
    }

    private async Task<IReadOnlyList<UserAccount>> SeedAccountsAndProfilesAsync(CancellationToken ct)
    {
        var hasher = new PasswordHasher<object>();
        var result = new List<UserAccount>(DemoUsers.All.Count);
        await using var db = new WayhunterSharedDbContext(_dbOptions);
        foreach (var seed in DemoUsers.All)
        {
            var account = await db.Users.SingleOrDefaultAsync(x => x.UserId == seed.Id, ct);
            var byEmail = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == seed.Email, ct);
            if (account is not null && !string.Equals(account.NormalizedEmail, seed.Email, StringComparison.Ordinal))
                throw new InvalidOperationException($"Stable demo user ID {seed.Id} is already assigned to another account.");
            if (byEmail is not null && !string.Equals(byEmail.UserId, seed.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"Demo email {seed.Email} exists with an unexpected user ID.");

            if (account is null)
            {
                var hash = hasher.HashPassword(new object(), options.Password);
                account = UserAccount.RegisterPendingWithId(seed.Id, seed.Name, seed.Email, hash, SeedTime);
                account.ActivateAfterVerifiedOtp(SeedTime.AddMinutes(1));
                db.Users.Add(account);
            }
            else if (account.Status != Wayhunter.Domain.Modules.IdentitySocial.Enums.UserAccountStatus.Active ||
                     !account.VerifiedAtUtc.HasValue)
            {
                throw new InvalidOperationException($"Existing demo identity {seed.Email} is not active; runner will not alter account state.");
            }
            else if (hasher.VerifyHashedPassword(new object(), account.PasswordHash, options.Password) ==
                     PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException($"Configured demo password does not match existing seed identity {seed.Email}; runner will not rotate it.");
            }

            if (!await db.UserProfiles.AnyAsync(x => x.UserId == seed.Id, ct))
            {
                var profile = UserProfile.CreateForUser(account, SeedTime.AddMinutes(2));
                profile.UpdatePublicDetails(null, seed.Bio, SeedTime.AddMinutes(3));
                db.UserProfiles.Add(profile);
            }
            if (seed.IsAdmin && !await db.UserRoleAssignments.AnyAsync(x =>
                    x.UserId == seed.Id && x.Role == "Admin" && x.Status == "Active", ct))
                db.UserRoleAssignments.Add(UserRoleAssignmentRow.CreateTrusted(seed.Id, SeedTime.AddMinutes(4), null,
                    DemoSeedIds.AdminRoleAssignment));

            await db.SaveChangesAsync(ct);
            result.Add(account);
        }
        return result;
    }

    private async Task<IReadOnlyList<TripPlan>> SeedTripsAsync(
        IReadOnlyList<UserAccount> users, CancellationToken ct)
    {
        var trips = new List<TripPlan>(5);
        await using var db = new WayhunterSharedDbContext(_dbOptions);
        for (var i = 0; i < 5; i++)
        {
            var id = DemoSeedIds.Trip(i);
            var existing = await db.Trips.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (existing is null)
            {
                var start = new DateOnly(2026, 11, 25).AddDays(i * 2);
                var trip = new TripPlan(id, users[i].UserId, $"Demo Island Journey {i + 1}",
                    start, start.AddDays(2), 25000 + i * 5000, "Car", 2);
                trip.AddStopWithId(DemoSeedIds.Stop(i, 0), "Demo starting point", $"DEMO-PLACE-{i + 1}-A",
                    6.9271 + i * .01, 79.8612 + i * .01);
                trip.AddStopWithId(DemoSeedIds.Stop(i, 1), "Demo destination", $"DEMO-PLACE-{i + 1}-B",
                    7.2906 + i * .01, 80.6337 + i * .01);
                db.Trips.Add(trip);
                await db.SaveChangesAsync(ct);
                existing = trip;
            }
            else if (!string.Equals(existing.OwnerUserId, users[i].UserId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Stable demo trip ID {id} belongs to another account.");
            trips.Add(existing);
        }
        return trips;
    }

    private async Task SeedSafetyAsync(IReadOnlyList<UserAccount> users,
        IReadOnlyList<TripPlan> trips, CancellationToken ct)
    {
        var trip = trips[0];
        var sessionId = DemoSeedIds.Session;
        await using (var db = new WayhunterSharedDbContext(_dbOptions))
        {
            var existingSession = await db.LiveTripSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId, ct);
            if (existingSession is not null && existingSession.TripPlanId != trip.Id)
                throw new InvalidOperationException("Stable demo session ID belongs to another trip.");
            if (existingSession is null)
                db.LiveTripSessions.Add(LiveTripSession.Start(sessionId, trip.Id, SeedTime.AddHours(1)));
            var existingHazard = await db.HazardReports.SingleOrDefaultAsync(x => x.Id == DemoSeedIds.Hazard, ct);
            if (existingHazard is not null && existingHazard.ReporterUserId != users[0].UserId)
                throw new InvalidOperationException("Stable demo hazard ID belongs to another reporter.");
            if (existingHazard is null)
                db.HazardReports.Add(HazardReport.Report(DemoSeedIds.Hazard, users[0].UserId,
                    SeedTime.AddHours(2), SeedTime.AddHours(1).AddMinutes(45)));
            await db.SaveChangesAsync(ct);
        }

        var factory = new SeedContextFactory(_dbOptions);
        var checkInStore = new EfTripSafeCheckInStore(factory);
        await checkInStore.RecordAsync(new TripSafeCheckIn(DemoSeedIds.CheckIn, trip.Id,
            sessionId, users[0].UserId, SeedTime.AddHours(2)), ct);

        var memoryStore = new EfTripMemoryStore(factory, TimeProvider.System);
        await memoryStore.CreateAsync(new TripMemory(DemoSeedIds.Memory, trip.Id,
            trip.Stops.FirstOrDefault()?.Id, users[0].UserId, DemoSeedIds.MemoryOperation,
            "Text-only demo journey note.", TripMemoryVisibility.Private,
            SeedTime.AddHours(2), SeedTime.AddHours(2)), ct);
    }

    private async Task SeedHunterExamplesAsync(IReadOnlyList<UserAccount> users, CancellationToken ct)
    {
        await using var db = new WayhunterSharedDbContext(_dbOptions);
        for (var i = 0; i < 2; i++)
        {
            var id = DemoSeedIds.HunterSubmission(i);
            var existing = await db.HunterSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (existing is not null)
            {
                if (existing.UserId != users[5 + i].UserId)
                    throw new InvalidOperationException("Stable demo Hunter ID belongs to another account.");
                continue;
            }
            db.HunterSubmissions.Add(new HunterSubmission
            {
                Id = id, UserId = users[5 + i].UserId, UserNameSnapshot = users[5 + i].DisplayName,
                PlaceName = $"Demo place proposal {i + 1}", Description = "Synthetic draft; no verification evidence attached.",
                District = "Demo District", Latitude = 6.9271 + i * .02, Longitude = 79.8612 + i * .02,
                Status = HunterSubmissionStatus.Draft, CreatedAtUtc = SeedTime
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedGameDraftsAsync(IReadOnlyList<UserAccount> users, CancellationToken ct)
    {
        for (var i = 0; i < 2; i++)
        {
            var id = DemoSeedIds.Game(i);
            await using var db = new WayhunterSharedDbContext(_dbOptions);
            var existing = await db.Games.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (existing is not null)
            {
                if (existing.CreatorUserId != users[7 + i].UserId)
                    throw new InvalidOperationException("Stable demo Game ID belongs to another creator.");
                continue;
            }
            var game = Game.CreateDraft(id, users[7 + i].UserId, $"Demo clue trail {i + 1}",
                "Synthetic academic demo draft.", 60);
            var step = new GameStep(DemoSeedIds.GameStep(i), id, 1);
            var hint = new GameHint(DemoSeedIds.GameHint(i), step.Id, "Look for the marked public landmark.", null);
            game.ReplaceDraftStages(users[7 + i].UserId, [step], [hint]);
            await new SqlGameRepository(db).AddAsync(game, ct);
        }
    }

    private async Task SeedWalletRewardsAsync(IReadOnlyList<UserAccount> users, CancellationToken ct)
    {
        foreach (var user in users)
        {
            await using var db = new WayhunterSharedDbContext(_dbOptions);
            await new SqlCoinRewardGrantStore(db).TryGrantOnceAsync(new CoinRewardGrantRequest(
                DemoSeedIds.WelcomeReward(user.UserId), CoinRewardType.WelcomeVerified,
                "demo-seed-welcome-v1:" + user.UserId, user.UserId, 100,
                user.VerifiedAtUtc!.Value, DemoSeedIds.WelcomeCorrelation(user.UserId),
                "2026-11-11"), ct);
        }
    }

    private async Task SeedVoucherAsync(UserAccount admin, CancellationToken ct)
    {
        await using var db = new WayhunterSharedDbContext(_dbOptions);
        var store = new SqlSampleVoucherAdminStore(db,
            new AesVoucherSecretProtector(options.VoucherProtectionKey));
        await store.CreateOfferAsync(admin.UserId, SampleVoucherDemoSeed.CreateOfferRequest(), ct);
        await store.AddInventoryAsync(admin.UserId,
            SampleVoucherDemoSeed.CreateInventoryRequest(options.VoucherCode,
                options.VoucherImageReference), SeedTime.AddHours(3), ct);
    }

    private async Task SeedAdvertisementsAsync(CancellationToken ct)
    {
        await using var db = new WayhunterSharedDbContext(_dbOptions);
        var records = AdvertisementDemoSeed.CreateDemoRecords();
        var ids = records.Select(x => x.Id).ToArray();
        var existing = await db.Advertisements.Where(x => ids.Contains(x.Id))
            .Select(x => x.Id).ToListAsync(ct);
        var missing = AdvertisementDemoSeed.GetMissingRecords(existing);
        foreach (var advertisement in missing)
            advertisement.Enable();
        db.Advertisements.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    private sealed class SeedContextFactory(DbContextOptions<WayhunterSharedDbContext> options)
        : IDbContextFactory<WayhunterSharedDbContext>
    {
        public WayhunterSharedDbContext CreateDbContext() => new(options);
    }
}

public sealed record DemoUser(string Id, string Name, string Email, string Bio, bool IsAdmin);

public static class DemoUsers
{
    public static readonly IReadOnlyList<DemoUser> All =
    [
        new("d1000000-0000-4000-8000-000000000001", "Demo Traveller One", "traveller1@demo.wayhunter.test", "Synthetic traveller profile.", false),
        new("d1000000-0000-4000-8000-000000000002", "Demo Traveller Two", "traveller2@demo.wayhunter.test", "Synthetic traveller profile.", false),
        new("d1000000-0000-4000-8000-000000000003", "Demo Traveller Three", "traveller3@demo.wayhunter.test", "Synthetic traveller profile.", false),
        new("d1000000-0000-4000-8000-000000000004", "Demo Traveller Four", "traveller4@demo.wayhunter.test", "Synthetic traveller profile.", false),
        new("d1000000-0000-4000-8000-000000000005", "Demo Traveller Five", "traveller5@demo.wayhunter.test", "Synthetic traveller profile.", false),
        new("d2000000-0000-4000-8000-000000000001", "Demo Hunter One", "hunter1@demo.wayhunter.test", "Synthetic Hunter profile.", false),
        new("d2000000-0000-4000-8000-000000000002", "Demo Hunter Two", "hunter2@demo.wayhunter.test", "Synthetic Hunter profile.", false),
        new("d3000000-0000-4000-8000-000000000001", "Demo Game Creator One", "creator1@demo.wayhunter.test", "Synthetic creator profile.", false),
        new("d3000000-0000-4000-8000-000000000002", "Demo Game Creator Two", "creator2@demo.wayhunter.test", "Synthetic creator profile.", false),
        new("d9000000-0000-4000-8000-000000000001", "Demo Admin", "admin@demo.wayhunter.test", "Synthetic Admin account.", true)
    ];
}

public static class DemoSeedIds
{
    public static Guid Trip(int n) => Guid.Parse($"b2000000-0000-4000-8000-{n + 1:000000000000}");
    public static Guid Stop(int trip, int stop) => Guid.Parse($"b2100000-0000-4000-8000-{trip * 2 + stop + 1:000000000000}");
    public static Guid Session => Guid.Parse("b3000000-0000-4000-8000-000000000001");
    public static Guid AdminRoleAssignment => Guid.Parse("b1000000-0000-4000-8000-000000000001");
    public static Guid CheckIn => Guid.Parse("b3100000-0000-4000-8000-000000000001");
    public static Guid CheckInOutbox => Guid.Parse("b3110000-0000-4000-8000-000000000001");
    public static Guid Hazard => Guid.Parse("b4000000-0000-4000-8000-000000000001");
    public static Guid Memory => Guid.Parse("b5000000-0000-4000-8000-000000000001");
    public static Guid MemoryOperation => Guid.Parse("b5010000-0000-4000-8000-000000000001");
    public static Guid HunterSubmission(int n) => Guid.Parse($"b6000000-0000-4000-8000-{n + 1:000000000000}");
    public static Guid Game(int n) => Guid.Parse($"b7000000-0000-4000-8000-{n + 1:000000000000}");
    public static Guid GameStep(int n) => Guid.Parse($"b7100000-0000-4000-8000-{n + 1:000000000000}");
    public static Guid GameHint(int n) => Guid.Parse($"b7200000-0000-4000-8000-{n + 1:000000000000}");
    public static Guid WelcomeReward(string userId) => StableId("wayhunter-demo-welcome-v1:" + Guid.Parse(userId).ToString("D"));
    public static Guid WelcomeCorrelation(string userId) => StableId("wayhunter-demo-welcome-correlation-v1:" + Guid.Parse(userId).ToString("D"));
    private static Guid StableId(string value) => new(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
