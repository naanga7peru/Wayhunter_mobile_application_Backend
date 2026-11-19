using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

public interface ISampleVoucherAdminStore
{
    Task CreateOfferAsync(
        string adminUserId,
        CreateSampleVoucherOfferRequest request,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherInventoryResponse> AddInventoryAsync(
        string adminUserId,
        AddSampleVoucherInventoryRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherAdminQueueResponse> GetQueueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherRequestResponse> ApproveAsync(
        string adminUserId,
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        DateTimeOffset decidedAtUtc,
        string ruleVersion,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherRequestResponse> RejectAsync(
        string adminUserId,
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherIssuanceResponse> IssueAsync(
        string adminUserId,
        Guid requestId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<SampleVoucherIssuanceResponse> ConsumeAsync(
        string adminUserId,
        Guid requestId,
        ConsumeSampleVoucherRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> ExpireDueAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default);
}
