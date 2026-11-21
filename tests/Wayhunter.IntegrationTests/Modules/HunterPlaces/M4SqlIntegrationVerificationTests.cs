
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

// =====================================================
// M4 SQL Integration Test Attribute
// =====================================================

public sealed class M4SqlIntegrationFactAttribute : FactAttribute
{
    public M4SqlIntegrationFactAttribute()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = "M4 isolated SQL test database is not configured.";
        }
    }
}

// =====================================================
// M4 SQL Integration Verification Tests
// =====================================================

public sealed class M4SqlIntegrationVerificationTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M4_IntegrationTest";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    private static bool _schemaInitialized;

    // =================================================
    // Validate Dedicated Test Database
    // =================================================

    private static string GetTestConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "M4 test database connection is missing.");
        }

        var builder =
            new SqlConnectionStringBuilder(connectionString);

        if (!string.Equals(
                builder.InitialCatalog,
                AllowedDatabaseName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the dedicated M4 test database is allowed.");
        }

        return connectionString;
    }

    // =================================================
    // Shared Production DbContext Factory
    // =================================================

    private static WayhunterSharedDbContext CreateContext(
        DbCommandInterceptor? interceptor = null)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(GetTestConnectionString());

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new WayhunterSharedDbContext(
            optionsBuilder.Options);
    }

    // =================================================
    // M4 Isolated Test Schema Context
    // =================================================

    // This context contains only M1 UserAccount and
    // M4 Hunter entities. It does not create other
    // module tables in the test database.

    private sealed class M4TestSchemaContext : DbContext
    {
        public M4TestSchemaContext(
            DbContextOptions<M4TestSchemaContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(
                new UserAccountConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterSubmissionConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterEvidenceConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterReviewDecisionConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterCaptureSessionRowConfiguration());
        }
    }

    // =================================================
    // Prepare Empty Test Database Schema
    // =================================================

    private static async Task EnsureM4TestSchemaAsync()
    {
        await SchemaGate.WaitAsync();

        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            var options =
                new DbContextOptionsBuilder<M4TestSchemaContext>()
                    .UseSqlServer(GetTestConnectionString())
                    .Options;

            await using var db =
                new M4TestSchemaContext(options);

            // Creates tables directly from this limited
            // test model. Does not generate or apply
            // EF Core migrations.
            await db.Database.EnsureCreatedAsync();

            _schemaInitialized = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    // =================================================
    // Test Data Seeder
    // =================================================

    private static async Task<(
        Guid HunterId,
        string UserId,
        Guid SubmissionId,
        Guid SessionId)> SeedAsync()
    {
        await EnsureM4TestSchemaAsync();

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

            Description = "M4 SQL integration test",

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

    // =================================================
    // Cleanup Test Data
    // =================================================

    private static async Task CleanupAsync(
        Guid submissionId,
        string userId)
    {
        await using var db = CreateContext();

        await db.HunterCaptureSessions
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterReviewDecisions
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterEvidence
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterSubmissions
            .Where(x => x.Id == submissionId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    // =================================================
    // TEST 01 - Concurrent Session Consumption
    // =================================================

    [M4SqlIntegrationFact]
    public async Task VerifiedSubmit_RollsBackOnRevocationFailureAndQueuesOnlyOnce()
    {
        var seed = await SeedAsync();
        try
        {
            await using var db = CreateContext();
            var draft = await db.HunterSubmissions.AsNoTracking().SingleAsync(row => row.Id == seed.SubmissionId);
            var now = DateTimeOffset.UtcNow;
            var video = new HunterEvidence
            {
                HunterSubmissionId = draft.Id, EvidenceType = HunterEvidenceType.Guided360Video,
                PrivateMediaReference = "test-video", CapturedAtUtc = now.AddMinutes(-4),
                UploadedAtUtc = now.AddMinutes(-3)
            };
            var photo = new HunterEvidence
            {
                HunterSubmissionId = draft.Id, EvidenceType = HunterEvidenceType.LivePhoto,
                PrivateMediaReference = "test-photo", CapturedAtUtc = now.AddMinutes(-2),
                UploadedAtUtc = now.AddMinutes(-1)
            };
            db.HunterEvidence.AddRange(video, photo);
            await db.SaveChangesAsync();
            var request = new HunterSubmitRequestDto
            {
                PlaceName = draft.PlaceName, Description = draft.Description, ClaimedDistrict = draft.District,
                Latitude = draft.Latitude, Longitude = draft.Longitude,
                Guided360VideoEvidenceId = video.Id, LivePhotoEvidenceId = photo.Id
            };
            var verified = new HunterVerifiedSubmitInputs("Verified Hunter", draft.District, video, photo);
            var interceptor = new FailHunterRevocationInterceptor();
            await using (var failingDb = CreateContext(interceptor))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    new SqlHunterSubmissionStatusTransitionStore(failingDb)
                        .TrySubmitVerifiedDraftAsync(draft.Id, seed.UserId, request, verified, now));
            }

            Assert.True(interceptor.RevocationBlocked);
            Assert.True(await db.HunterSubmissions.AnyAsync(row => row.Id == draft.Id &&
                row.Status == HunterSubmissionStatus.Draft && row.SubmittedAtUtc == null));
            Assert.True(await db.HunterCaptureSessions.AnyAsync(row => row.Id == seed.SessionId && !row.IsRevoked));

            var store = new SqlHunterSubmissionStatusTransitionStore(db);
            Assert.False(await store.TrySubmitVerifiedDraftAsync(draft.Id, Guid.NewGuid().ToString("D"), request, verified, now));
            Assert.True(await store.TrySubmitVerifiedDraftAsync(draft.Id, seed.UserId, request, verified, now));
            Assert.False(await store.TrySubmitVerifiedDraftAsync(draft.Id, seed.UserId, request, verified, now));
            var queued = await new SqlHunterCaptureSubmissionAccessReader(db).FindPendingReviewCandidateAsync(draft.Id);
            Assert.NotNull(queued);
            Assert.Equal(now, queued.SubmittedAtUtc);
            Assert.True(await db.HunterCaptureSessions.AnyAsync(row => row.Id == seed.SessionId && row.IsRevoked));
        }
        finally
        {
            await CleanupAsync(seed.SubmissionId, seed.UserId);
        }
    }

    [M4SqlIntegrationFact]
    public async Task ConcurrentReview_RecordsOneDecisionAndRevokesCaptureWithReplay()
    {
        var seed = await SeedAsync();
        try
        {
            await using var db = CreateContext();
            var sessions = new SqlHunterCaptureSessionStore(db);
            var now = DateTimeOffset.UtcNow;
            Assert.True(await sessions.IsEligibleForEvidenceAsync(seed.SessionId, seed.HunterId, seed.SubmissionId, now));
            Assert.False(await sessions.IsEligibleForEvidenceAsync(seed.SessionId, Guid.NewGuid(), seed.SubmissionId, now));

            var transitions = new SqlHunterSubmissionStatusTransitionStore(db);
            Assert.True(await transitions.TryTransitionAndRevokeCaptureSessionsAsync(
                seed.SubmissionId, HunterSubmissionStatus.Draft, HunterSubmissionStatus.PendingReview));
            Assert.False(await sessions.IsEligibleForEvidenceAsync(seed.SessionId, seed.HunterId, seed.SubmissionId, now));

            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            async Task<bool> ApproveAsync(Guid id, string? reason = null)
            {
                await using var scopeDb = CreateContext();
                return await new SqlHunterSubmissionStatusTransitionStore(scopeDb).TryRecordReviewAsync(
                    id, seed.UserId, new HunterReviewRequestDto
                    {
                        HunterSubmissionId = seed.SubmissionId,
                        ResultingStatus = HunterSubmissionStatus.Approved,
                        Reason = reason
                    }, now);
            }

            var outcomes = await Task.WhenAll(ApproveAsync(first), ApproveAsync(second));
            Assert.Single(outcomes, outcome => outcome);
            var winner = outcomes[0] ? first : second;
            Assert.True(await ApproveAsync(winner));
            Assert.False(await ApproveAsync(winner, "Different review reason"));

            var audit = await db.HunterReviewDecisions.AsNoTracking()
                .SingleAsync(row => row.HunterSubmissionId == seed.SubmissionId);
            Assert.Equal(winner, audit.Id);
            Assert.Equal(seed.UserId, audit.ReviewerUserId);
            Assert.Equal(HunterSubmissionStatus.Approved, audit.ResultingStatus);
            Assert.True(await db.HunterCaptureSessions.AnyAsync(row => row.Id == seed.SessionId && row.IsRevoked));
        }
        finally
        {
            await CleanupAsync(seed.SubmissionId, seed.UserId);
        }
    }

    [M4SqlIntegrationFact]
    public async Task DraftDetailsUpdate_RequiresOwnerAndDraftAndPreservesCaptureBinding()
    {
        var seed = await SeedAsync();
        try
        {
            await using var db = CreateContext();
            var reader = new SqlHunterCaptureSubmissionAccessReader(db);
            var before = await db.HunterSubmissions.AsNoTracking().SingleAsync(row => row.Id == seed.SubmissionId);

            Assert.False(await reader.UpdateDraftDetailsAsync(
                Guid.NewGuid().ToString("D"), seed.SubmissionId, "Foreign edit", null));
            Assert.True(await reader.UpdateDraftDetailsAsync(
                seed.UserId, seed.SubmissionId, "Updated draft", "Updated description"));
            Assert.True(await reader.UpdateDraftDetailsAsync(
                seed.UserId, seed.SubmissionId, "Updated draft", "Updated description"));

            var after = await db.HunterSubmissions.AsNoTracking().SingleAsync(row => row.Id == seed.SubmissionId);
            Assert.Equal("Updated draft", after.PlaceName);
            Assert.Equal("Updated description", after.Description);
            Assert.Equal(before.UserId, after.UserId);
            Assert.Equal(before.District, after.District);
            Assert.Equal(before.Latitude, after.Latitude);
            Assert.Equal(before.Longitude, after.Longitude);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.SubmittedAtUtc, after.SubmittedAtUtc);
            Assert.True(await db.HunterCaptureSessions.AnyAsync(row => row.Id == seed.SessionId && !row.IsRevoked));

            await db.HunterSubmissions.Where(row => row.Id == seed.SubmissionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, HunterSubmissionStatus.PendingReview));
            Assert.False(await reader.UpdateDraftDetailsAsync(
                seed.UserId, seed.SubmissionId, "Late edit", null));
        }
        finally
        {
            await CleanupAsync(seed.SubmissionId, seed.UserId);
        }
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

            // Two independent requests.
            var results = await Task.WhenAll(
                ConsumeAsync(),
                ConsumeAsync());

            Assert.Single(results, result => result);

            await using var verifyDb = CreateContext();

            var consumed = await verifyDb.HunterCaptureSessions
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

    // =================================================
    // TEST 02 - Status Transition and Revocation
    // =================================================

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

    // =================================================
    // TEST 03 - Rollback on Revocation Failure
    // =================================================

    [M4SqlIntegrationFact]
    public async Task RevocationFailure_ShouldRollbackStatusTransition()
    {
        var seed = await SeedAsync();

        try
        {
            var interceptor =
                new FailHunterRevocationInterceptor();

            await using (var db = CreateContext(interceptor))
            {
                var store =
                    new SqlHunterSubmissionStatusTransitionStore(db);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store
                        .TryTransitionAndRevokeCaptureSessionsAsync(
                            seed.SubmissionId,
                            HunterSubmissionStatus.Draft,
                            HunterSubmissionStatus.PendingReview));
            }

            Assert.True(interceptor.StatusUpdateObserved);

            Assert.True(interceptor.RevocationBlocked);

            // Verify using a fresh context.
            await using var verifyDb = CreateContext();

            var submission = await verifyDb.HunterSubmissions
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.SubmissionId);

            var session = await verifyDb.HunterCaptureSessions
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.SessionId);

            // The status update must be rolled back.
            Assert.Equal(
                HunterSubmissionStatus.Draft,
                submission.Status);

            // The session must remain unchanged.
            Assert.False(session.IsRevoked);

            Assert.False(session.IsConsumed);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }

    // =================================================
    // Failure Injection Interceptor
    // =================================================

    private sealed class FailHunterRevocationInterceptor
        : DbCommandInterceptor
    {
        public bool StatusUpdateObserved { get; private set; }

        public bool RevocationBlocked { get; private set; }

        public override ValueTask<InterceptionResult<int>>
            NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;

            var isUpdate = sql.Contains(
                "UPDATE",
                StringComparison.OrdinalIgnoreCase);

            if (isUpdate &&
                sql.Contains(
                    "Hunter_Submissions",
                    StringComparison.OrdinalIgnoreCase) &&
                sql.Contains(
                    "Status",
                    StringComparison.OrdinalIgnoreCase))
            {
                StatusUpdateObserved = true;
            }

            if (StatusUpdateObserved &&
                isUpdate &&
                sql.Contains(
                    "Hunter_CaptureSessions",
                    StringComparison.OrdinalIgnoreCase) &&
                sql.Contains(
                    "IsRevoked",
                    StringComparison.OrdinalIgnoreCase))
            {
                RevocationBlocked = true;

                throw new InvalidOperationException(
                    "Injected M4 capture-session revocation failure.");
            }

            return new ValueTask<InterceptionResult<int>>(result);
        }
    }
}
