
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

/// <summary>
/// M6 sample voucher policy.
/// Final debit occurs only with successful
/// voucher issuance/fulfillment.
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
    /// Identifies fulfillment-stage actions that may
    /// finalize a debit.
    ///
    /// Approval alone never debits Coins.
    /// Issue and RetryIssue still require:
    /// - An approved request
    /// - An active 10,000-Coin reservation
    /// - Successful issuance
    /// - A durable exactly-once transaction guard
    ///
    /// Returning true does NOT authorize an
    /// unconditional second debit.
    /// </summary>
    public static bool IsFinalDebitTrigger(
        SampleVoucherAction action)
    {
        ValidateAction(action);

        return action is
            SampleVoucherAction.Issue or
            SampleVoucherAction.RetryIssue;
    }

    /// <summary>
    /// Determines whether issuance may begin.
    /// A committed debit is NOT a prerequisite.
    /// Debit and successful issuance must be
    /// finalized atomically by the transaction layer.
    /// </summary>
    public static bool CanIssue(
        bool adminApproved,
        bool reservationHeld)
    {
        return adminApproved && reservationHeld;
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
