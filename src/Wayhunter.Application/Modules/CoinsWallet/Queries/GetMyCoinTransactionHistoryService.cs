using System.Security.Claims;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Queries;

public sealed class GetMyCoinTransactionHistoryService
{
    public const int MaximumPageSize = 100;

    private readonly ICoinTransactionHistoryReader _reader;

    public GetMyCoinTransactionHistoryService(
        ICoinTransactionHistoryReader reader)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));
    }

    public Task<CoinTransactionHistoryResponse> GetMineAsync(
        ClaimsPrincipal? currentUser,
        int page,
        int pageSize,
        CoinTransactionKind? kind,
        CoinRewardType? rewardType,
        CancellationToken cancellationToken = default)
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

        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page));

        if (pageSize < 1 || pageSize > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        return _reader.GetAsync(
            new CoinTransactionHistoryQuery(
                ownerId.Trim(),
                page,
                pageSize,
                kind,
                rewardType),
            cancellationToken);
    }
}
