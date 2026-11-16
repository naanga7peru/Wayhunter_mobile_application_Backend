
using System;

namespace Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

public sealed class HunterCaptureSessionRow
{
    public Guid Id { get; set; }

    public Guid HunterId { get; set; }

    public Guid HunterSubmissionId { get; set; }

    public DateTimeOffset IssuedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public bool IsConsumed { get; set; }

    public bool IsRevoked { get; set; }
}
