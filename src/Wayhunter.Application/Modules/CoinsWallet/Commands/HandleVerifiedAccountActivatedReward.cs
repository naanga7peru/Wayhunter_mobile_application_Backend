using Wayhunter.Domain.Modules.IdentitySocial.Events;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

/// <summary>
/// Trusted M1-to-M6 integration point. M1 must call this only after
/// persisting the first successful OTP activation and its event.
/// No public reward-grant endpoint is exposed.
/// </summary>
public sealed class HandleVerifiedAccountActivatedReward
{
    private readonly GrantVerifiedWelcomeCoinsHandler _handler;

    public HandleVerifiedAccountActivatedReward(
        GrantVerifiedWelcomeCoinsHandler handler)
    {
        _handler = handler ??
            throw new ArgumentNullException(nameof(handler));
    }

    public Task<bool> HandleAsync(
        VerifiedAccountActivatedEvent activation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activation);

        return _handler.HandleAsync(
            activation.UserId,
            activation.CorrelationId,
            cancellationToken);
    }
}
