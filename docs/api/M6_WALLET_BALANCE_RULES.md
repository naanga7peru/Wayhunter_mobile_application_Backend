# WAYHUNTER — M6 Wallet Balance & Accounting Definitions

**Task ID:** M6-MS01-T02  
**Owner:** Kaviyan — M6 Coins & Wallet  
**Status:** Draft — Pending Peer Review  
**Source:** Latest Approved Rules — 2026-11-11

## 1. Wallet Balance Definitions

Wayhunter uses Coins as the only reward currency.

| Field | Description |
|---|---|
| PostedCoins | Current balance after successfully committed ledger credits and debits |
| ReservedCoins | Coins temporarily reserved for pending redemption requests |
| SpendableCoins | PostedCoins minus ReservedCoins |
| LifetimeEarnedCoins | Total eligible Coins awarded throughout the user's lifetime; not reduced by redemption |
| TotalCoins | Ambiguous legacy label; use an explicitly defined field instead |

## 2. Balance Calculation

SpendableCoins = PostedCoins - ReservedCoins

Rules:

1. All values must use non-negative integer Coins.
2. ReservedCoins cannot exceed PostedCoins.
3. M6 Backend owns authoritative balance calculations.
4. Flutter must not independently create or modify authoritative balances.
5. LifetimeEarnedCoins tracks earning progress and is separate from spendable balance.
6. Duplicate reward or redemption processing must not change balances twice.

## 3. Reward Examples

- Verified account: +100 Coins.
- Approved Hunter submission: +200 Coins.
- Approved Game creator: +100 Coins.
- Verified single Game winner: +100 Coins.

Only eligible approved sources generate credits.

## 4. Redemption Example

The user requires at least 10,100 SpendableCoins before requesting the 10,000-Coin / Rs.100 sample voucher.

| State | PostedCoins | ReservedCoins | SpendableCoins |
|---|---:|---:|---:|
| Before Request | 10,100 | 0 | 10,100 |
| Request Pending | 10,100 | 10,000 | 100 |
| Admin Approved | 100 | 0 | 100 |
| Voucher Issued | 100 | 0 | 100 |

On rejection or eligible cancellation of the Pending request, release the reservation once without a permanent debit.

Admin approval finalizes the debit exactly once. Issuing the voucher afterward does not cause another debit.

If issuance fails, preserve the approved debit and the unissued voucher state for controlled retry.

## 5. Level Progress

- Use LifetimeEarnedCoins for level progression.
- The documented initial level-bar baseline is 2,000 earned Coins.
- Spending Coins must not reduce LifetimeEarnedCoins.
- Final level numbering and progression formula require confirmation before implementation.

## 6. Backend Responsibility

M6 owns CoinWallets, CoinLedgerEntries, CoinReservations and authoritative calculations.

Other modules must not directly modify Wallet balances.

All balance-changing operations require audit records, idempotency and concurrency-safe processing.

## 7. Future Validation Tests

- Posted 500, Reserved 100 → Spendable 400.
- Posted 10,100, Reserved 10,000 → Spendable 100.
- Request with 10,000 SpendableCoins → Rejected.
- Request with 10,100 SpendableCoins → Eligible.
- Duplicate reservation → No second hold.
- Duplicate Admin approval → No second debit.
- Rejected request → Reservation released once.
- Voucher issued after approval → No additional debit.
- Earning 2,000 Coins followed by spending → LifetimeEarnedCoins unchanged.
- Negative balance and overflow attempts → Rejected.

## 8. Implementation Status

Documentation only. Database operations, ledger services, Flutter integration and behavioral tests are not yet implemented.

**Task Status:** Draft — Pending Review.
