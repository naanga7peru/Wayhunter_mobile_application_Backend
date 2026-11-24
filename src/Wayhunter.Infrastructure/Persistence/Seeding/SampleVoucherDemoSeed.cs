using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Infrastructure.Persistence.Seeding;

/// <summary>
/// Deterministic academic voucher seed requests. This factory never persists
/// data and deliberately accepts the voucher code and protected image
/// reference from the operator so neither secret is committed to source.
/// </summary>
public static class SampleVoucherDemoSeed
{
    public static readonly Guid OfferId =
        Guid.Parse("a6000000-0000-4000-8000-000000000001");

    public static readonly Guid InventoryId =
        Guid.Parse("a6000000-0000-4000-8000-000000000002");

    private static readonly DateTimeOffset ValidFromUtc =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ValidUntilUtc =
        new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static CreateSampleVoucherOfferRequest CreateOfferRequest() =>
        new(OfferId, "ACADEMIC-DEMO-10000", ValidFromUtc, ValidUntilUtc);

    public static AddSampleVoucherInventoryRequest CreateInventoryRequest(
        string voucherCode,
        string protectedSampleImageReference) =>
        new(
            InventoryId,
            OfferId,
            RequireValue(voucherCode, nameof(voucherCode)),
            RequireValue(protectedSampleImageReference,
                nameof(protectedSampleImageReference)),
            ValidUntilUtc);

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", parameterName)
            : value.Trim();
}
