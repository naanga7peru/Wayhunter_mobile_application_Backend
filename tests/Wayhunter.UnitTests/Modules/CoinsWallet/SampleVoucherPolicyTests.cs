
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class SampleVoucherPolicyTests
{
    [Fact]
    public void VoucherReference_IsCorrect()
    {
        Assert.Equal(100m,
            SampleVoucherPolicy.ReferenceValueLkr);

        Assert.Equal(10000,
            SampleVoucherPolicy.VoucherCostCoins);

        Assert.False(
            SampleVoucherPolicy.SupportsRealCashPayout);
    }

    [Theory]
    [InlineData(SampleVoucherAction.Request, true)]
    [InlineData(SampleVoucherAction.AdminApprove, false)]
    [InlineData(SampleVoucherAction.Issue, false)]
    [InlineData(SampleVoucherAction.RetryIssue, false)]
    public void Reservation_OnlyOnRequest(
        SampleVoucherAction action,
        bool expected)
    {
        Assert.Equal(expected,
            SampleVoucherPolicy.RequiresReservation(action));
    }

    [Theory]
    [InlineData(SampleVoucherAction.Request, false)]
    [InlineData(SampleVoucherAction.RejectOrCancel, false)]
    [InlineData(SampleVoucherAction.AdminApprove, true)]
    [InlineData(SampleVoucherAction.Issue, false)]
    [InlineData(SampleVoucherAction.RetryIssue, false)]
    public void DebitTrigger_OnlyOnAdminApproval(
        SampleVoucherAction action,
        bool expected)
    {
        Assert.Equal(expected,
            SampleVoucherPolicy.IsFinalDebitTrigger(action));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void Issuance_RequiresApprovalAndDebit(
        bool approved,
        bool debitCommitted,
        bool expected)
    {
        Assert.Equal(expected,
            SampleVoucherPolicy.CanIssue(
                approved, debitCommitted));
    }

    [Fact]
    public void VoucherWithoutQr_CannotBeDisplayed()
    {
        Assert.False(
            SampleVoucherPolicy.CanDisplayVoucher(
                true, true, false));
    }

    [Fact]
    public void IssuedVoucherWithCodeAndQr_CanDisplay()
    {
        Assert.True(
            SampleVoucherPolicy.CanDisplayVoucher(
                true, true, true));
    }

    [Fact]
    public void InvalidAction_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SampleVoucherPolicy.IsFinalDebitTrigger(
                (SampleVoucherAction)999));
    }
}
