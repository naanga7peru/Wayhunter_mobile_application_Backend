
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Application.Modules.IdentitySocial.Dtos;

public sealed record RegistrationIdentityResponseDto(
    string UserId,
    string DisplayName,
    bool RequiresOtpVerification)
{
    public static RegistrationIdentityResponseDto FromPendingAccount(
        UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.Status !=
            UserAccountStatus.PendingVerification)
        {
            throw new InvalidOperationException(
                "Registration response requires a pending account.");
        }

        return new RegistrationIdentityResponseDto(
            account.UserId,
            account.DisplayName,
            RequiresOtpVerification: true);
    }
}
