
namespace Wayhunter.Application.Modules.IdentitySocial.Dtos;

public sealed record PublicProfileDto(
    string UserId,
    string DisplayName,
    string? AvatarUrl,
    string? Bio
);
