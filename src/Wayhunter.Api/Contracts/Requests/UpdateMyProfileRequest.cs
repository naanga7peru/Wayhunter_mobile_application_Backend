using System.ComponentModel.DataAnnotations;

namespace Wayhunter.Api.Contracts.Requests;

public sealed record UpdateMyProfileRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string DisplayName,
    [StringLength(500)]
    string? Bio);
