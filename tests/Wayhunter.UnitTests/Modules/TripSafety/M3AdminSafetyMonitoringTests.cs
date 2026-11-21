using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3AdminSafetyMonitoringTests
{
    [Fact]
    public async Task AuditCapturesTrustedActorTargetTimeAndOutcome()
    {
        var fixture = await Fixture.CreateAsync();
        var success = await fixture.Service.ReadAsync(fixture.Actor, fixture.Target, default);
        Assert.Equal(AdminSafetyOutcome.Success, success.Outcome);
        var wrong = fixture.Target with { SessionId = Guid.NewGuid() };
        var missing = await fixture.Service.ReadAsync(fixture.Actor, wrong, default);
        Assert.Equal(AdminSafetyOutcome.NotFound, missing.Outcome);
        Assert.Collection(fixture.Audit.Records,
            record => Assert.Equal(new(fixture.Actor, "ReadTripSafety", fixture.Target, fixture.Now, AdminSafetyOutcome.Success), record),
            record => Assert.Equal(new(fixture.Actor, "ReadTripSafety", wrong, fixture.Now, AdminSafetyOutcome.NotFound), record));
    }

    [Fact]
    public async Task ChangedOrWrongGrantCannotReleaseData()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Source.RevokeAfterFirstRead = true;
        Assert.Equal(new(AdminSafetyOutcome.Denied), await fixture.Service.ReadAsync(fixture.Actor, fixture.Target, default));
        fixture.Source.RevokeAfterFirstRead = false;
        fixture.Source.GrantTargetOverride = fixture.Target with { TripPlanId = Guid.NewGuid() };
        Assert.Equal(new(AdminSafetyOutcome.Denied), await fixture.Service.ReadAsync(fixture.Actor, fixture.Target, default));
        Assert.All(fixture.Audit.Records, record => Assert.Equal(AdminSafetyOutcome.Denied, record.Outcome));
    }

    [Fact]
    public async Task AuditFailureAndMissingDependenciesSuppressResponse()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Audit.Fail = true;
        Assert.Equal(new(AdminSafetyOutcome.Unavailable), await fixture.Service.ReadAsync(fixture.Actor, fixture.Target, default));
        var disabled = new AdminSafetyMonitoringService(new InMemoryTripMembershipRevocationRepository(),
            new InMemoryLiveTripSessionRepository(), new InMemoryTripSharingConsentRepository(), TimeProvider.System);
        Assert.Equal(new(AdminSafetyOutcome.Unavailable), await disabled.ReadAsync(fixture.Actor, fixture.Target, default));
    }

    [Fact]
    public async Task UnavailableScopeIsAuditedWithoutReturningData()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Source.Unavailable = true;
        Assert.Equal(new(AdminSafetyOutcome.Unavailable), await fixture.Service.ReadAsync(fixture.Actor, fixture.Target, default));
        Assert.Equal(AdminSafetyOutcome.Unavailable, Assert.Single(fixture.Audit.Records).Outcome);
    }

    [Fact]
    public async Task FinalRoleDenialWithFailedAuditStillWithholdsSummary()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Audit.FailAfterFirstRecord = true;
        var checks = 0;
        var result = await fixture.Service.ReadAsync(fixture.Actor, fixture.Target, default,
            () => Task.FromResult(++checks == 1));
        Assert.Equal(new(AdminSafetyOutcome.Unavailable), result);
        Assert.Equal(AdminSafetyOutcome.Success, Assert.Single(fixture.Audit.Records).Outcome);
        Assert.Equal(2, checks);
    }

    private sealed class Scope : IAdminSafetyScopeReader
    {
        private int _reads;
        public bool RevokeAfterFirstRead { get; set; }
        public bool Unavailable { get; set; }
        public AdminSafetyTarget? GrantTargetOverride { get; set; }
        public Task<AdminSafetyGrant?> ReadGrantAsync(string actor, AdminSafetyTarget target, CancellationToken ct) =>
            Unavailable ? throw new InvalidOperationException("Scope source unavailable.") :
            Task.FromResult<AdminSafetyGrant?>(RevokeAfterFirstRead && ++_reads > 1
                ? null : new(GrantTargetOverride ?? target, actor, AdminSafetySections.None));
    }

    private sealed class AuditSink : IAdminSafetyAuditSink
    {
        public List<AdminSafetyAuditRecord> Records { get; } = [];
        public bool Fail { get; set; }
        public bool FailAfterFirstRecord { get; set; }
        public Task RecordAsync(AdminSafetyAuditRecord record, CancellationToken ct)
        {
            if (Fail || (FailAfterFirstRecord && Records.Count > 0))
                throw new InvalidOperationException("Test audit store unavailable.");
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Fixture(string Actor, AdminSafetyTarget Target, DateTimeOffset Now, Scope Source,
        AuditSink Audit, AdminSafetyMonitoringService Service)
    {
        public static async Task<Fixture> CreateAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var actor = Guid.NewGuid().ToString("D");
            var trip = new TripPlan(actor, "Test trip", new(2026, 11, 21), new(2026, 11, 22), 0, "Car", 2);
            var members = new InMemoryTripMembershipRevocationRepository();
            await members.AddTripAsync(trip);
            var sessions = new InMemoryLiveTripSessionRepository();
            var session = LiveTripSession.Start(Guid.NewGuid(), trip.Id, now);
            await sessions.AddAsync(session);
            var scope = new Scope();
            var audit = new AuditSink();
            var service = new AdminSafetyMonitoringService(members, sessions, new InMemoryTripSharingConsentRepository(),
                new Clock(now), scope, audit: audit);
            return new(actor, new(trip.Id, session.SessionId), now, scope, audit, service);
        }
    }
}
