
namespace Wayhunter.Application.Modules.IdentitySocial.Services;

public sealed record OtpResendDecision(
    bool Allowed,
    TimeSpan RetryAfter);

public static class OtpResendCooldownPolicy
{
    public static OtpResendDecision Check(
        DateTimeOffset nowUtc,
        DateTimeOffset? lastOtpSentAtUtc,
        TimeSpan cooldown)
    {
        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC time is required.",
                nameof(nowUtc));
        }

        if (cooldown <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cooldown));
        }

        if (lastOtpSentAtUtc is null)
        {
            return new OtpResendDecision(
                true,
                TimeSpan.Zero);
        }

        var lastSent = lastOtpSentAtUtc.Value;

        if (lastSent == default ||
            lastSent.Offset != TimeSpan.Zero ||
            lastSent > nowUtc)
        {
            throw new ArgumentException(
                "Invalid last OTP sent time.",
                nameof(lastOtpSentAtUtc));
        }

        var elapsed = nowUtc - lastSent;
        var remaining = cooldown - elapsed;

        if (remaining <= TimeSpan.Zero)
        {
            return new OtpResendDecision(
                true,
                TimeSpan.Zero);
        }

        return new OtpResendDecision(
            false,
            remaining);
    }
}
