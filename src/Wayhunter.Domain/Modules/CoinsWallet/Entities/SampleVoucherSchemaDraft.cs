
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;

namespace Wayhunter.Domain.Modules.CoinsWallet.Entities;

// PROPOSED SCHEMA ONLY.
// Not mapped to EF Core or SQL Server.

public enum SampleVoucherRequestStatusDraft
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum SampleVoucherIssueStatusDraft
{
    Unissued = 1,
    Failed = 2,
    Issued = 3
}

internal static class VoucherDraftValidation
{
    public static void RequireUtc(
        DateTimeOffset value,
        string field)
    {
        if (value == default ||
            value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC timestamp required.",
                field);
        }
    }
}

// Proposed table: SampleVoucherOffers
public sealed record SampleVoucherOfferSchemaDraft
{
    public Guid Id { get; }
    public string OfferCode { get; }
    public long CostCoins { get; }
    public decimal ReferenceValueLkr { get; }
    public bool IsActive { get; }
    public DateTimeOffset ValidFromUtc { get; }
    public DateTimeOffset ValidUntilUtc { get; }

    public SampleVoucherOfferSchemaDraft(
        Guid id,
        string offerCode,
        long costCoins,
        decimal referenceValueLkr,
        bool isActive,
        DateTimeOffset validFromUtc,
        DateTimeOffset validUntilUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Offer ID required.");

        if (string.IsNullOrWhiteSpace(offerCode))
            throw new ArgumentException(
                "Offer code required.");

        if (costCoins != SampleVoucherPolicy.VoucherCostCoins)
            throw new ArgumentOutOfRangeException(
                nameof(costCoins));

        if (referenceValueLkr !=
            SampleVoucherPolicy.ReferenceValueLkr)
            throw new ArgumentOutOfRangeException(
                nameof(referenceValueLkr));

        VoucherDraftValidation.RequireUtc(
            validFromUtc, nameof(validFromUtc));

        VoucherDraftValidation.RequireUtc(
            validUntilUtc, nameof(validUntilUtc));

        if (validUntilUtc <= validFromUtc)
            throw new ArgumentException(
                "Voucher validity range is invalid.");

        Id = id;
        OfferCode = offerCode.Trim();
        CostCoins = costCoins;
        ReferenceValueLkr = referenceValueLkr;
        IsActive = isActive;
        ValidFromUtc = validFromUtc;
        ValidUntilUtc = validUntilUtc;
    }
}

// Proposed table: SampleVoucherRedemptionRequests
public sealed record SampleVoucherRequestSchemaDraft
{
    public Guid Id { get; }
    public Guid WalletId { get; }
    public Guid OfferId { get; }
    public string OwnerUserId { get; }
    public long CostCoins { get; }
    public SampleVoucherRequestStatusDraft Status { get; }
    public DateTimeOffset RequestedAtUtc { get; }
    public Guid? AdminDecisionId { get; }
    public DateTimeOffset? DecidedAtUtc { get; }

    public SampleVoucherRequestSchemaDraft(
        Guid id,
        Guid walletId,
        Guid offerId,
        string ownerUserId,
        long costCoins,
        SampleVoucherRequestStatusDraft status,
        DateTimeOffset requestedAtUtc,
        Guid? adminDecisionId = null,
        DateTimeOffset? decidedAtUtc = null)
    {
        if (id == Guid.Empty ||
            walletId == Guid.Empty ||
            offerId == Guid.Empty)
            throw new ArgumentException(
                "Request, wallet and offer IDs required.");

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Owner user ID required.");

        if (costCoins != SampleVoucherPolicy.VoucherCostCoins)
            throw new ArgumentOutOfRangeException(
                nameof(costCoins));

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(
                nameof(status));

        VoucherDraftValidation.RequireUtc(
            requestedAtUtc, nameof(requestedAtUtc));

        bool reviewed = status is
            SampleVoucherRequestStatusDraft.Approved or
            SampleVoucherRequestStatusDraft.Rejected;

        if (reviewed &&
            (adminDecisionId is null ||
             adminDecisionId == Guid.Empty ||
             decidedAtUtc is null))
        {
            throw new ArgumentException(
                "Admin decision and time required.");
        }

        if (status == SampleVoucherRequestStatusDraft.Pending &&
            (adminDecisionId is not null ||
             decidedAtUtc is not null))
        {
            throw new ArgumentException(
                "Pending request cannot have a decision.");
        }

        if (adminDecisionId == Guid.Empty)
            throw new ArgumentException(
                "Invalid Admin decision ID.");

        if (decidedAtUtc.HasValue)
        {
            VoucherDraftValidation.RequireUtc(
                decidedAtUtc.Value,
                nameof(decidedAtUtc));

            if (decidedAtUtc.Value < requestedAtUtc)
                throw new ArgumentException(
                    "Decision predates request.");
        }

        Id = id;
        WalletId = walletId;
        OfferId = offerId;
        OwnerUserId = ownerUserId.Trim();
        CostCoins = costCoins;
        Status = status;
        RequestedAtUtc = requestedAtUtc;
        AdminDecisionId = adminDecisionId;
        DecidedAtUtc = decidedAtUtc;
    }
}

// Proposed table: SampleVoucherInventory
public sealed record SampleVoucherInventorySchemaDraft
{
    public Guid Id { get; }
    public Guid OfferId { get; }
    public string CodeHash { get; }
    public string EncryptedCodeReference { get; }
    public Guid? AssignedRequestId { get; }

    public SampleVoucherInventorySchemaDraft(
        Guid id,
        Guid offerId,
        string codeHash,
        string encryptedCodeReference,
        Guid? assignedRequestId = null)
    {
        if (id == Guid.Empty || offerId == Guid.Empty)
            throw new ArgumentException(
                "Inventory and offer IDs required.");

        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException(
                "Unique code hash required.");

        if (string.IsNullOrWhiteSpace(encryptedCodeReference))
            throw new ArgumentException(
                "Encrypted code reference required.");

        if (assignedRequestId == Guid.Empty)
            throw new ArgumentException(
                "Invalid assigned request ID.");

        Id = id;
        OfferId = offerId;
        CodeHash = codeHash.Trim();
        EncryptedCodeReference =
            encryptedCodeReference.Trim();
        AssignedRequestId = assignedRequestId;
    }
}

// Proposed table: SampleVoucherIssuances
public sealed record SampleVoucherIssuanceSchemaDraft
{
    public Guid Id { get; }
    public Guid RequestId { get; }
    public Guid InventoryId { get; }
    public SampleVoucherIssueStatusDraft Status { get; }
    public string? QrPayload { get; }
    public string? ImageReference { get; }
    public DateTimeOffset? IssuedAtUtc { get; }

    public SampleVoucherIssuanceSchemaDraft(
        Guid id,
        Guid requestId,
        Guid inventoryId,
        SampleVoucherIssueStatusDraft status,
        string? qrPayload,
        string? imageReference,
        DateTimeOffset? issuedAtUtc)
    {
        if (id == Guid.Empty ||
            requestId == Guid.Empty ||
            inventoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Issuance IDs required.");
        }

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(
                nameof(status));

        if (status == SampleVoucherIssueStatusDraft.Issued)
        {
            if (string.IsNullOrWhiteSpace(qrPayload) ||
                string.IsNullOrWhiteSpace(imageReference) ||
                issuedAtUtc is null)
            {
                throw new ArgumentException(
                    "Issued voucher needs QR, image and time.");
            }
        }
        else if (issuedAtUtc is not null)
        {
            throw new ArgumentException(
                "Unissued voucher cannot have issue time.");
        }

        if (issuedAtUtc.HasValue)
            VoucherDraftValidation.RequireUtc(
                issuedAtUtc.Value, nameof(issuedAtUtc));

        Id = id;
        RequestId = requestId;
        InventoryId = inventoryId;
        Status = status;
        QrPayload = qrPayload;
        ImageReference = imageReference;
        IssuedAtUtc = issuedAtUtc;
    }
}
