using Wayhunter.Application.Modules.TripSafety.Notifications;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed record NotificationLease(Guid Token, Guid EventId, string UserId, int Attempt,
    DateTimeOffset ExpiresAtUtc, NotificationCategory Category, NotificationTemplate Template, int TemplateVersion);

public enum NotificationDispatchOutcome { NoWork, Delivered, Suppressed, Retry, DeadLettered, LeaseLost }
public sealed record NotificationAttemptResult(NotificationDeliveryState State, string Outcome, DateTimeOffset? RetryAtUtc);

public interface INotificationDeliveryStore
{
    // Atomically increment attempts, assign a unique lease, and recover expired leases.
    Task<NotificationLease?> ClaimAsync(DateTimeOffset now, int maxAttempts, TimeSpan leaseDuration, CancellationToken ct);
    // Persist attempt audit and outcome together, only for the current unexpired lease.
    Task<bool> FinishAsync(NotificationLease lease, NotificationAttemptResult result, DateTimeOffset now, CancellationToken ct);
}

public interface INotificationPushEligibility
{
    // Recheck category preference, current recipient and target permissions, job
    // state, and device opt-in. Unknown state throws or returns false.
    Task<bool> CanPushAsync(NotificationLease lease, CancellationToken ct);
}

public interface INotificationPushProvider
{
    // Resolve only active opt-in devices for this recipient. Keep tokens out of
    // messages and logs. For invalid tokens use conditional owner/version revocation.
    // The approved adapter must deduplicate the same (event, recipient, device) on retry.
    Task<NotificationPushResult> SendAsync(Guid eventId, string recipientId, NotificationContent content, CancellationToken ct);
}

public sealed class UnavailableNotificationPushProvider : INotificationPushProvider
{
    public Task<NotificationPushResult> SendAsync(Guid eventId, string recipientId, NotificationContent content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new NotificationUnavailableException();
    }
}

public sealed class NotificationDispatchService(INotificationDeliveryStore store, INotificationPushEligibility eligibility,
    INotificationPushProvider provider, TimeProvider clock, M3OutboxWorkerSettings settings)
{
    public async Task<NotificationDispatchOutcome> ProcessNextAsync(CancellationToken ct)
    {
        settings.Validate();
        var lease = await store.ClaimAsync(clock.GetUtcNow(), settings.MaxAttempts,
            TimeSpan.FromSeconds(settings.LeaseSeconds), ct);
        if (lease is null)
            return NotificationDispatchOutcome.NoWork;

        var now = clock.GetUtcNow();
        if (lease.ExpiresAtUtc <= now)
            return NotificationDispatchOutcome.LeaseLost;

        var content = Enum.IsDefined(lease.Template) ? NotificationTemplates.Get(lease.Template) : null;
        if (content is null || !NotificationInputs.IsCanonicalUser(lease.UserId) ||
            content.Category != lease.Category || content.Version != lease.TemplateVersion)
            return await FinishAsync(lease, new(NotificationDeliveryState.DeadLettered, "InvalidEnvelope", null),
                NotificationDispatchOutcome.DeadLettered, ct);

        using var deadline = new CancellationTokenSource(lease.ExpiresAtUtc - now, clock);
        using var send = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, ct);
        NotificationPushResult result;
        var denied = false;
        try
        {
            denied = !await eligibility.CanPushAsync(lease, send.Token);
            send.Token.ThrowIfCancellationRequested();
            if (clock.GetUtcNow() >= lease.ExpiresAtUtc)
                return NotificationDispatchOutcome.LeaseLost;

            result = denied ? NotificationPushResult.NoActiveDevice :
                await provider.SendAsync(lease.EventId, lease.UserId, content, send.Token);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            result = NotificationPushResult.TemporaryFailure;
        }

        ct.ThrowIfCancellationRequested();
        return result switch
        {
            NotificationPushResult.Delivered => await FinishAsync(lease,
                new(NotificationDeliveryState.Delivered, "Delivered", null), NotificationDispatchOutcome.Delivered, ct),
            NotificationPushResult.NoActiveDevice or NotificationPushResult.InvalidDevice => await FinishAsync(lease,
                new(NotificationDeliveryState.Suppressed,
                    denied ? "PushDenied" : result == NotificationPushResult.InvalidDevice ? "InvalidDevice" : "NoActiveDevice", null),
                NotificationDispatchOutcome.Suppressed, ct),
            _ when lease.Attempt >= settings.MaxAttempts => await FinishAsync(lease,
                new(NotificationDeliveryState.DeadLettered, "AttemptLimit", null), NotificationDispatchOutcome.DeadLettered, ct),
            _ => await FinishAsync(lease,
                new(NotificationDeliveryState.Pending, "RetryScheduled", clock.GetUtcNow().Add(
                    M3OutboxRetryPolicy.CalculateDelay(lease.Attempt, settings.BaseRetrySeconds, settings.MaxRetrySeconds))),
                NotificationDispatchOutcome.Retry, ct)
        };
    }

    private async Task<NotificationDispatchOutcome> FinishAsync(NotificationLease lease, NotificationAttemptResult result,
        NotificationDispatchOutcome outcome, CancellationToken ct) =>
        await store.FinishAsync(lease, result, clock.GetUtcNow(), ct) ? outcome : NotificationDispatchOutcome.LeaseLost;
}
