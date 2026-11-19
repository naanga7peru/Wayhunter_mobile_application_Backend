using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

public interface ISampleVoucherRequestStore
{
    Task<IReadOnlyList<SampleVoucherOfferResponse>>
        GetAvailableOffersAsync(
            string ownerUserId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default);

    Task<SampleVoucherRequestResponse> RequestAsync(
        string ownerUserId,
        CreateSampleVoucherRequest request,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherRequestResponse?> GetOwnedRequestAsync(
        string ownerUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherRequestResponse> CancelAsync(
        string ownerUserId,
        Guid requestId,
        string reason,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken = default);

    Task<MyIssuedSampleVoucherResponse?> GetOwnedVoucherAsync(
        string ownerUserId,
        Guid requestId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
