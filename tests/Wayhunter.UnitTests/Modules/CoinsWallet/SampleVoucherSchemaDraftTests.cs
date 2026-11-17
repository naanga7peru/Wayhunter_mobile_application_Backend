
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class SampleVoucherSchemaDraftTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static SampleVoucherOfferSchemaDraft Offer()
    {
        return new SampleVoucherOfferSchemaDraft(
            Guid.NewGuid(),
            "SAMPLE-100",
            10000,
            100m,
            true,
            Now,
            Now.AddDays(30));
    }

    [Fact]
    public void ValidOffer_UsesApprovedCoinsAndLkr()
    {
        var offer = Offer();

        Assert.Equal(10000, offer.CostCoins);
        Assert.Equal(100m, offer.ReferenceValueLkr);
        Assert.True(offer.IsActive);
    }

    [Fact]
    public void IncorrectVoucherCost_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SampleVoucherOfferSchemaDraft(
                Guid.NewGuid(),
                "SAMPLE-100",
                9000,
                100m,
                true,
                Now,
                Now.AddDays(30)));
    }

    [Fact]
    public void InvalidOfferDateRange_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new SampleVoucherOfferSchemaDraft(
                Guid.NewGuid(),
                "SAMPLE-100",
                10000,
                100m,
                true,
                Now,
                Now));
    }

    [Fact]
    public void PendingRequest_HasNoAdminDecision()
    {
        var request = new SampleVoucherRequestSchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-001",
            10000,
            SampleVoucherRequestStatusDraft.Pending,
            Now);

        Assert.Equal(
            SampleVoucherRequestStatusDraft.Pending,
            request.Status);

        Assert.Null(request.AdminDecisionId);
        Assert.Null(request.DecidedAtUtc);
    }

    [Fact]
    public void ApprovedRequest_RequiresAdminDecision()
    {
        Assert.Throws<ArgumentException>(() =>
            new SampleVoucherRequestSchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "user-001",
                10000,
                SampleVoucherRequestStatusDraft.Approved,
                Now));
    }

    [Fact]
    public void ApprovedRequest_WithAudit_IsValid()
    {
        var request = new SampleVoucherRequestSchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-001",
            10000,
            SampleVoucherRequestStatusDraft.Approved,
            Now,
            Guid.NewGuid(),
            Now.AddMinutes(5));

        Assert.Equal(
            SampleVoucherRequestStatusDraft.Approved,
            request.Status);

        Assert.NotNull(request.AdminDecisionId);
    }

    [Fact]
    public void InventoryItem_HasProtectedCodeReferences()
    {
        var item = new SampleVoucherInventorySchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sha256-demo-code-001",
            "secret-store:vouchers/demo-001");

        Assert.Equal(
            "sha256-demo-code-001",
            item.CodeHash);
        Assert.Equal(
            "secret-store:vouchers/demo-001",
            item.EncryptedCodeReference);
        Assert.Null(item.AssignedRequestId);
    }

    [Fact]
    public void Inventory_WithoutCode_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new SampleVoucherInventorySchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "",
                "secret-store:vouchers/demo-001"));

        Assert.Throws<ArgumentException>(() =>
            new SampleVoucherInventorySchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "sha256-demo-code-001",
                ""));
    }

    [Fact]
    public void IssuedVoucher_MustHaveQrAndImage()
    {
        Assert.Throws<ArgumentException>(() =>
            new SampleVoucherIssuanceSchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                SampleVoucherIssueStatusDraft.Issued,
                null,
                null,
                Now));
    }

    [Fact]
    public void SuccessfulIssuance_HasRequiredData()
    {
        var issue = new SampleVoucherIssuanceSchemaDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SampleVoucherIssueStatusDraft.Issued,
            "SAMPLE-QR-DATA",
            "sample-image-reference",
            Now);

        Assert.Equal(
            SampleVoucherIssueStatusDraft.Issued,
            issue.Status);

        Assert.NotNull(issue.QrPayload);
        Assert.NotNull(issue.ImageReference);
        Assert.Equal(Now, issue.IssuedAtUtc);
    }

    [Fact]
    public void NonUtcIssuanceTime_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new SampleVoucherIssuanceSchemaDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                SampleVoucherIssueStatusDraft.Issued,
                "QR",
                "image",
                Now.ToOffset(TimeSpan.FromHours(5.5))));
    }
}
