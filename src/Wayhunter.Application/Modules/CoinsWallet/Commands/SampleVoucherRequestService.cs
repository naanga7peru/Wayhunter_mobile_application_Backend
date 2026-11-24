using System.Security.Claims;
using System.Linq;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

public sealed class SampleVoucherRequestService
{
    private readonly ISampleVoucherRequestStore _store;
    private readonly TimeProvider _timeProvider;

    public SampleVoucherRequestService(
        ISampleVoucherRequestStore store,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<IReadOnlyList<SampleVoucherOfferResponse>>
        GetOffersAsync(
            ClaimsPrincipal? currentUser,
            CancellationToken cancellationToken = default)
    {
        string ownerId = RequireOwner(currentUser);
        return _store.GetAvailableOffersAsync(
            ownerId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<SampleVoucherOfferResponse?> GetOfferAsync(
        ClaimsPrincipal? currentUser,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        if (offerId == Guid.Empty)
            throw new ArgumentException("Offer ID is required.");

        var offers = await GetOffersAsync(currentUser, cancellationToken);
        return offers.SingleOrDefault(x => x.OfferId == offerId);
    }

    public Task<SampleVoucherRequestResponse> RequestAsync(
        ClaimsPrincipal? currentUser,
        CreateSampleVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestId == Guid.Empty ||
            request.OfferId == Guid.Empty)
        {
            throw new ArgumentException(
                "Request and offer IDs are required.");
        }

        return _store.RequestAsync(
            RequireOwner(currentUser),
            request,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<SampleVoucherRequestResponse?> GetAsync(
        ClaimsPrincipal? currentUser,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request ID is required.");

        return _store.GetOwnedRequestAsync(
            RequireOwner(currentUser),
            requestId,
            cancellationToken);
    }

    public Task<SampleVoucherRequestResponse> CancelAsync(
        ClaimsPrincipal? currentUser,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request ID is required.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.");

        return _store.CancelAsync(
            RequireOwner(currentUser),
            requestId,
            reason.Trim(),
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<MyIssuedSampleVoucherResponse?> GetVoucherAsync(
        ClaimsPrincipal? currentUser,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request ID is required.");

        return _store.GetOwnedVoucherAsync(
            RequireOwner(currentUser),
            requestId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static string RequireOwner(
        ClaimsPrincipal? currentUser)
    {
        if (currentUser?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException(
                "Authentication required.");

        string? ownerId = currentUser
            .FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? currentUser.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new UnauthorizedAccessException(
                "Authenticated user ID required.");

        return ownerId.Trim();
    }
}
