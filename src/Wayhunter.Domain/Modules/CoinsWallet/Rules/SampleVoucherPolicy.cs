
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

public static class SampleVoucherPolicy
{
    public const decimal ReferenceValueLkr = 100m;

    public const bool SupportsRealCashPayout = false;

    public static long VoucherCostCoins =>
        CoinLevelRedemptionRules.SampleVoucherCostCoins;

    public static bool RequiresReservation(
        SampleVoucherAction action)
    {
        ValidateAction(action);
        return action == SampleVoucherAction.Request;
    }

    public static bool IsFinalDebitTrigger(
        SampleVoucherAction action)
    {
        ValidateAction(action);
        return action == SampleVoucherAction.AdminApprove;
    }

    public static bool CanIssue(
        bool adminApproved,
        bool debitCommitted)
    {
        return adminApproved && debitCommitted;
    }

    public static bool CanDisplayVoucher(
        bool successfullyIssued,
        bool codeReady,
        bool qrReady)
    {
        return successfullyIssued
            && codeReady
            && qrReady;
    }

    private static void ValidateAction(
        SampleVoucherAction action)
    {
        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(
                nameof(action));
    }
}
