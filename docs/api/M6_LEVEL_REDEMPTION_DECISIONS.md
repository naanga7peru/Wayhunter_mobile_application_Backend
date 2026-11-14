# WAYHUNTER — M6 Level & Redemption Decisions

**Task:** M6-MS01-T04  
**Owner:** Kaviyan — M6  
**Status:** Draft — Pending Peer Review

## Confirmed Rules

- The active reward currency is Coins only.
- Initial level progress uses LifetimeEarnedCoins with a 2,000-Coin baseline.
- Spending Coins does not reduce LifetimeEarnedCoins.
- A sample Rs.100 voucher costs 10,000 Coins.
- A user requires at least 10,100 SpendableCoins before requesting the voucher.
- Pending requests reserve Coins; this task does not implement reservations or debits.

## Pending Decision

The complete level numbering and subsequent level progression formula are not yet approved.

Do not implement arbitrary additional level thresholds.

## Implementation Scope

`CoinLevelRedemptionRules` implements the confirmed initial progress and redemption eligibility checks.

Database migrations, ledger transactions, voucher issuance and live API connections are outside this task.

## Acceptance Evidence

- C# rules implementation
- Unit tests for 1,999 / 2,000 / 2,001 earned Coins
- Unit tests for 10,000 / 10,099 / 10,100 spendable Coins
- Successful build and tests
- Reviewed Pull Request into develop
