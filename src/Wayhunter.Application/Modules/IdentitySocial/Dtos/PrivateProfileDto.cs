
namespace Wayhunter.Application.Modules.IdentitySocial.Dtos;

public sealed record PrivateProfileDto(
    string UserId,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    string Email,
    string? PhoneNumber
);
