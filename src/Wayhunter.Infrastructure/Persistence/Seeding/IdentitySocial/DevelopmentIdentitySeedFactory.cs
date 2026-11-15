
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Seeding.IdentitySocial;

public static class DevelopmentIdentitySeedFactory
{
    // Only for development/test sample data.
    // Does not write to a database.
    // Never connect this factory to production startup.

    private const string DisabledPasswordHash =
        "!DEV_ONLY_INVALID_PASSWORD_HASH!";

    public static IReadOnlyList<UserAccount> Create(
        bool isDevelopment,
        DateTimeOffset createdAtUtc)
    {
        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                "Development seed users cannot be created in production.");
        }

        // Active sample account
        var activeUser = UserAccount.RegisterPending(
            "Demo Active Traveler",
            "active@wayhunter.invalid",
            DisabledPasswordHash,
            createdAtUtc);

        activeUser.ActivateAfterVerifiedOtp(
            createdAtUtc.AddMinutes(1));

        // Unverified sample account
        var unverifiedUser = UserAccount.RegisterPending(
            "Demo Unverified Traveler",
            "unverified@wayhunter.invalid",
            DisabledPasswordHash,
            createdAtUtc);

        // Suspended sample account
        var suspendedUser = UserAccount.RegisterPending(
            "Demo Suspended Traveler",
            "suspended@wayhunter.invalid",
            DisabledPasswordHash,
            createdAtUtc);

        suspendedUser.ActivateAfterVerifiedOtp(
            createdAtUtc.AddMinutes(1));

        suspendedUser.Suspend();

        return Array.AsReadOnly(new[]
        {
            activeUser,
            unverifiedUser,
            suspendedUser
        });
    }
}
