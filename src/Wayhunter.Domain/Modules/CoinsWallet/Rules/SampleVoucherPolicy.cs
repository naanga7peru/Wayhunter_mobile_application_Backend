
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

/// <summary>
/// M6 sample voucher policy.
/// The 10,000-Coin debit occurs once when an Admin
/// approves the request. Issuance and controlled retry
/// must never create another debit.
///
/// Policy validation does not perform SQL transactions.
/// </summary>
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

    /// <summary>
    /// Identifies the single action that finalizes a debit.
    ///
    /// The transaction layer must atomically consume the
    /// reservation, append the debit, and record approval.
    /// Issue and RetryIssue are fulfillment-only actions.
    /// </summary>
    public static bool IsFinalDebitTrigger(
        SampleVoucherAction action)
    {
        ValidateAction(action);

        return action == SampleVoucherAction.AdminApprove;
    }

    /// <summary>
    /// Determines whether issuance may begin.
    /// Approval and its committed debit are prerequisites.
    /// A failed issue remains approved/unissued so a
    /// controlled retry can reuse the same debit.
    /// </summary>
    public static bool CanIssue(
        bool adminApproved,
        bool approvalDebitCommitted)
    {
        return adminApproved && approvalDebitCommitted;
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
        {
            throw new ArgumentOutOfRangeException(
                nameof(action));
        }
    }
}
