using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3SafeCheckInSqlFactAttribute : FactAttribute
{
    public M3SafeCheckInSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M3_SAFECHECKIN_TEST_SQL_CONNECTION")))
            Skip = "Dedicated M3 safe check-in SQL Server test connection is unavailable.";
    }
}

public sealed class M3SafeCheckInSqlTests
{
    [M3SafeCheckInSqlFact]
    public async Task ConcurrentCheckIns_CommitOneRecordAndOneOwnerEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var store = new EfTripSafeCheckInStore(fixture);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 8).Select(async _ =>
        {
            await start.Task;
            return await store.RecordAsync(fixture.CheckIn(fixture.Member.UserId));
        }).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(requests);
        var created = Assert.Single(results, result => result.Created).CheckIn;
        Assert.All(results, result => Assert.Equal(created, result.CheckIn));
        Assert.False((await store.RecordAsync(fixture.CheckIn(fixture.Member.UserId))).Created);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(created.CheckInId, (await db.TripSafeCheckIns.SingleAsync()).CheckInId);
        var notification = await db.TripSafeCheckInOutbox.SingleAsync();
        Assert.Equal(created.CheckInId, notification.CheckInId);
        Assert.Equal(fixture.Owner.UserId, notification.RecipientUserId);
    }

    [M3SafeCheckInSqlFact]
    public async Task InvalidActor_InactiveMembership_CrossTripAndIneligibleState_AreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var store = new EfTripSafeCheckInStore(fixture);
        var ownerCheckIn = fixture.CheckIn(fixture.Owner.UserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.RecordAsync(ownerCheckIn with { UserId = "spoofed" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.RecordAsync(ownerCheckIn with { UserId = fixture.Outsider.UserId }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.RecordAsync(ownerCheckIn with { SessionId = fixture.OtherSession.SessionId }));

        await using var db = fixture.CreateDbContext();
        var membership = await db.TripMembers.SingleAsync();
        membership.Remove(fixture.Clock.GetUtcNow());
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.RecordAsync(fixture.CheckIn(fixture.Member.UserId)));

        var session = await db.LiveTripSessions.FindAsync(fixture.Session.SessionId);
        session!.Pause();
        await db.SaveChangesAsync();
        Assert.True((await store.RecordAsync(ownerCheckIn)).Created);

        session.Finish(fixture.Clock.GetUtcNow());
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordAsync(ownerCheckIn));
        var trip = await db.Trips.FindAsync(fixture.Trip.Id);
        trip!.Cancel();
        await db.SaveChangesAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordAsync(ownerCheckIn));
        Assert.Contains("Cancelled", failure.Message);
        Assert.Equal(1, await db.TripSafeCheckInOutbox.CountAsync());
    }

    [M3SafeCheckInSqlFact]
    public async Task CompetingFinishHoldingSessionLock_PreventsNewCheckIn()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var transition = fixture.CreateTransitionContext();
        await using var transaction = await transition.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var session = await transition.LiveTripSessions.FindAsync(fixture.Session.SessionId);
        session!.Finish(fixture.Clock.GetUtcNow());
        await transition.SaveChangesAsync();

        var store = new EfTripSafeCheckInStore(fixture);
        var checkIn = store.RecordAsync(fixture.CheckIn(fixture.Member.UserId));
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => checkIn);
        Assert.Empty(await transition.TripSafeCheckIns.ToListAsync());
        Assert.Empty(await transition.TripSafeCheckInOutbox.ToListAsync());
    }

    [M3SafeCheckInSqlFact]
    public async Task CompetingRemovalHoldingMembershipLock_PreventsNewCheckIn()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var transition = fixture.CreateTransitionContext();
        await using var transaction = await transition.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var member = await transition.TripMembers.SingleAsync();
        member.Remove(fixture.Clock.GetUtcNow());
        await transition.SaveChangesAsync();

        var store = new EfTripSafeCheckInStore(fixture);
        var checkIn = store.RecordAsync(fixture.CheckIn(fixture.Member.UserId));
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => checkIn);
        Assert.Empty(await transition.TripSafeCheckIns.ToListAsync());
        Assert.Empty(await transition.TripSafeCheckInOutbox.ToListAsync());
    }

    [M3SafeCheckInSqlFact]
    public async Task CheckInCommitsBeforeCompetingFinish_AndKeepsItsHistoricalEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var pause = new PauseBeforeCommit();
        var store = new EfTripSafeCheckInStore(fixture.CreateWriteFactory(pause));
        var write = store.RecordAsync(fixture.CheckIn(fixture.Member.UserId));
        await using var transition = fixture.CreateTransitionContext();
        Task<int>? finish = null;
        try
        {
            await pause.Committing.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var finishAt = fixture.Clock.GetUtcNow();
            finish = transition.LiveTripSessions.Where(x => x.SessionId == fixture.Session.SessionId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.Status, LiveTripStatus.Finished)
                    .SetProperty(x => x.FinishedAtUtc, finishAt));
        }
        finally
        {
            pause.Release.TrySetResult();
            Assert.True((await write).Created);
        }

        Assert.Equal(1, await finish!);
        Assert.Single(await transition.TripSafeCheckIns.ToListAsync());
        Assert.Single(await transition.TripSafeCheckInOutbox.ToListAsync());
    }

    [M3SafeCheckInSqlFact]
    public async Task OutboxInsertFailure_RollsBackCheckIn()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE [TripSafety_SafeCheckInOutbox]
            ADD CONSTRAINT [RejectSafeCheckInOutboxInsert] CHECK ([DeliveryAttempts] < 0)
            """);

        var store = new EfTripSafeCheckInStore(fixture);
        await Assert.ThrowsAsync<TripSafetyCheckInUnavailableException>(() =>
            store.RecordAsync(fixture.CheckIn(fixture.Member.UserId)));
        Assert.Empty(await db.TripSafeCheckIns.ToListAsync());
        Assert.Empty(await db.TripSafeCheckInOutbox.ToListAsync());
    }

    [M3SafeCheckInSqlFact]
    public async Task ParallelWorkers_LeaseBeforeSend_AndFenceStaleAcknowledgement()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.EnqueueAsync();
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var started = new TaskCompletionSource<TripSafeCheckInOwnerNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSender = new Sender(async (message, _) =>
        {
            started.SetResult(message);
            await release.Task;
        });
        var secondSender = new Sender((_, _) => Task.CompletedTask);
        var first = fixture.Dispatcher(firstContext, firstSender);
        var second = fixture.Dispatcher(secondContext, secondSender);
        var inFlight = first.ProcessNextAsync();

        try
        {
            var original = await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TripSafeCheckInDispatchOutcome.NoWork, await second.ProcessNextAsync());
            fixture.Clock.Advance(TimeSpan.FromMinutes(3));
            Assert.Equal(TripSafeCheckInDispatchOutcome.Delivered, await second.ProcessNextAsync());
            Assert.Equal(original, Assert.Single(secondSender.Deliveries));
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.Equal(TripSafeCheckInDispatchOutcome.LeaseLost, await inFlight);
        var row = await secondContext.TripSafeCheckInOutbox.SingleAsync();
        Assert.True(row.IsDelivered);
        Assert.Equal(2, row.DeliveryAttempts);
        Assert.Null(row.LeaseToken);
    }

    [M3SafeCheckInSqlFact]
    public async Task Failure_RetriesWithStableIdAndDeadLettersAtAttemptLimit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.EnqueueAsync();
        await using var db = fixture.CreateDbContext();
        var sender = new Sender((_, _) => throw new InvalidOperationException("private provider error"));
        var dispatcher = fixture.Dispatcher(db, sender, new M3OutboxWorkerSettings { MaxAttempts = 2 });
        Assert.Equal(TripSafeCheckInDispatchOutcome.RetryScheduled, await dispatcher.ProcessNextAsync());

        var pending = await db.TripSafeCheckInOutbox.AsNoTracking().SingleAsync();
        Assert.Equal(fixture.Clock.GetUtcNow().AddSeconds(30), pending.NextAttemptAtUtc);
        Assert.Equal(TripSafeCheckInDispatchOutcome.NoWork, await dispatcher.ProcessNextAsync());
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(TripSafeCheckInDispatchOutcome.DeadLettered, await dispatcher.ProcessNextAsync());
        var terminal = await db.TripSafeCheckInOutbox.AsNoTracking().SingleAsync();
        Assert.NotNull(terminal.DeadLetteredAtUtc);
        Assert.Null(terminal.NextAttemptAtUtc);
        Assert.Null(terminal.LeaseToken);
        Assert.Equal("DeliveryFailed", terminal.LastError);
        Assert.Equal(new[] { terminal.Id, terminal.Id }, sender.Deliveries.Select(x => x.Id));
    }

    [M3SafeCheckInSqlFact]
    public async Task RecipientChange_NeverSendsToFormerOwnerOrRedirectsEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.EnqueueAsync();
        await using var db = fixture.CreateDbContext();
        // Current production ownership is immutable. Simulate a changed authoritative row.
        await db.Trips.Where(x => x.Id == fixture.Trip.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.OwnerUserId, fixture.Outsider.UserId));
        var sender = new Sender((_, _) => Task.CompletedTask);

        Assert.Equal(TripSafeCheckInDispatchOutcome.DeadLettered, await fixture.Dispatcher(db, sender).ProcessNextAsync());
        Assert.Empty(sender.Deliveries);
        Assert.Equal("RecipientNotAuthorized", (await db.TripSafeCheckInOutbox.SingleAsync()).LastError);
    }

    [M3SafeCheckInSqlFact]
    public async Task CancelledFinalLease_ExpiresAndDeadLettersWithoutSecondSend()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.EnqueueAsync();
        await using var db = fixture.CreateDbContext();
        using var cancellation = new CancellationTokenSource();
        var sender = new Sender((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        var dispatcher = fixture.Dispatcher(db, sender, new M3OutboxWorkerSettings { MaxAttempts = 1 });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.ProcessNextAsync(cancellation.Token));

        var leased = await db.TripSafeCheckInOutbox.AsNoTracking().SingleAsync();
        Assert.NotNull(leased.LeaseToken);
        fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal(TripSafeCheckInDispatchOutcome.NoWork, await dispatcher.ProcessNextAsync());
        var exhausted = await db.TripSafeCheckInOutbox.AsNoTracking().SingleAsync();
        Assert.NotNull(exhausted.DeadLetteredAtUtc);
        Assert.Null(exhausted.LeaseToken);
        Assert.Single(sender.Deliveries);
    }

    private sealed class Sender(Func<TripSafeCheckInOwnerNotification, CancellationToken, Task> send)
        : ITripSafeCheckInOwnerNotificationSender
    {
        public List<TripSafeCheckInOwnerNotification> Deliveries { get; } = [];

        public Task SendAsync(TripSafeCheckInOwnerNotification notification, CancellationToken cancellationToken)
        {
            Deliveries.Add(notification);
            return send(notification, cancellationToken);
        }
    }

    private sealed class PauseBeforeCommit : DbTransactionInterceptor
    {
        public TaskCompletionSource Committing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Committing.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class ContextFactory(DbContextOptions<WayhunterSharedDbContext> options)
        : IDbContextFactory<WayhunterSharedDbContext>
    {
        public WayhunterSharedDbContext CreateDbContext() => new(options);
    }

    private sealed class TestClock : TimeProvider
    {
        private long _ticks = DateTimeOffset.UtcNow.Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    }

    private sealed class Fixture : IDbContextFactory<WayhunterSharedDbContext>, IAsyncDisposable
    {
        private const string CatalogPrefix = "Wayhunter_M3_SafeCheckInIntegrationTest";
        private readonly string _connection;
        private bool _created;
        private readonly DbContextOptions<TestSchemaContext> _testOptions;
        private readonly DbContextOptions<WayhunterSharedDbContext> _sharedOptions;

        public TestClock Clock { get; } = new();
        public UserAccount Owner { get; private set; } = null!;
        public UserAccount Member { get; private set; } = null!;
        public UserAccount Outsider { get; private set; } = null!;
        public TripPlan Trip { get; private set; } = null!;
        public LiveTripSession Session { get; private set; } = null!;
        public LiveTripSession OtherSession { get; private set; } = null!;

        private Fixture(string connection)
        {
            _connection = connection;
            _testOptions = new DbContextOptionsBuilder<TestSchemaContext>().UseSqlServer(connection).Options;
            _sharedOptions = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(8, TimeSpan.FromSeconds(3), [1205])).Options;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var configured = new SqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("WAYHUNTER_M3_SAFECHECKIN_TEST_SQL_CONNECTION"));
            if (!string.Equals(configured.InitialCatalog, CatalogPrefix, StringComparison.Ordinal) ||
                !string.Equals(configured.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
                !configured.IntegratedSecurity || !string.IsNullOrEmpty(configured.AttachDBFilename))
                throw new InvalidOperationException("Only the dedicated M3 safe check-in LocalDB test profile is allowed.");

            configured.InitialCatalog = $"{CatalogPrefix}_{Guid.NewGuid():N}";
            var fixture = new Fixture(configured.ConnectionString);
            try
            {
                await using var schema = new TestSchemaContext(fixture._testOptions);
                fixture._created = await schema.Database.EnsureCreatedAsync();
                if (!fixture._created)
                    throw new InvalidOperationException("Expected a fresh isolated test database.");
                await fixture.SeedAsync();
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public WayhunterSharedDbContext CreateDbContext() => new(_sharedOptions);

        public WayhunterSharedDbContext CreateTransitionContext() => new(
            new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseSqlServer(_connection).Options);

        public IDbContextFactory<WayhunterSharedDbContext> CreateWriteFactory(IInterceptor interceptor) =>
            new ContextFactory(new DbContextOptionsBuilder<WayhunterSharedDbContext>(_sharedOptions)
                .AddInterceptors(interceptor).Options);

        public TripSafeCheckIn CheckIn(string userId) =>
            new(Guid.NewGuid(), Trip.Id, Session.SessionId, userId, Clock.GetUtcNow());

        public Task<TripSafeCheckInWriteResult> EnqueueAsync() =>
            new EfTripSafeCheckInStore(this).RecordAsync(CheckIn(Member.UserId));

        public TripSafeCheckInOutboxDispatcher Dispatcher(WayhunterSharedDbContext context, Sender sender,
            M3OutboxWorkerSettings? settings = null) => new(context, sender, Clock, settings ?? new());

        private async Task SeedAsync()
        {
            var now = Clock.GetUtcNow();
            Owner = Account("Owner", now);
            Member = Account("Member", now);
            Outsider = Account("Outsider", now);
            var date = DateOnly.FromDateTime(now.UtcDateTime);
            Trip = new TripPlan(Owner.UserId, "Check-in trip", date, date.AddDays(1), 100m, "Car", 2);
            var otherTrip = new TripPlan(Outsider.UserId, "Other trip", date, date.AddDays(1), 100m, "Car", 2);
            Session = LiveTripSession.Start(Guid.NewGuid(), Trip.Id, now.AddMinutes(-5));
            OtherSession = LiveTripSession.Start(Guid.NewGuid(), otherTrip.Id, now.AddMinutes(-5));
            var invite = TripInvitation.Create(Guid.NewGuid(), Trip.Id, Owner.UserId, Member.UserId, now.AddMinutes(-15));
            invite.Accept(now.AddMinutes(-10));
            var member = TripMember.FromAcceptedInvitation(Guid.NewGuid(), invite);

            await using var db = CreateDbContext();
            db.AddRange(Owner, Member, Outsider, Trip, otherTrip, Session, OtherSession, invite, member);
            await db.SaveChangesAsync();
        }

        private static UserAccount Account(string name, DateTimeOffset now)
        {
            var account = UserAccount.RegisterPending(name, $"{name}@example.invalid", "test-hash", now.AddHours(-1));
            account.ActivateAfterVerifiedOtp(now.AddMinutes(-30));
            return account;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_created)
                return;

            await using var schema = new TestSchemaContext(_testOptions);
            await schema.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestSchemaContext(DbContextOptions<TestSchemaContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.ApplyConfiguration(new UserAccountConfiguration());
            model.ApplyConfiguration(new TripPlanConfiguration());
            model.ApplyConfiguration(new TripStopConfiguration());
            model.ApplyConfiguration(new TripInvitationConfiguration());
            model.ApplyConfiguration(new TripMemberConfiguration());
            model.ApplyConfiguration(new LiveTripSessionConfiguration());
            model.ApplyConfiguration(new TripSafeCheckInConfiguration());
            model.ApplyConfiguration(new TripSafeCheckInOutboxConfiguration());
        }
    }
}
