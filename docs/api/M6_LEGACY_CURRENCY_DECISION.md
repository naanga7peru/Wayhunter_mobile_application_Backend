# WAYHUNTER — M6 Legacy Currency Decision

**Task ID:** M6-MS01-T06  
**Owner:** Kaviyan — M6 Coins & Wallet  
**Status:** Draft — Pending Peer Review

## 1. Active Currency

Wayhunter uses **Coins** as its only active reward currency.

Legacy HP, RC and XP are not supported by the current reward system.

## 2. Legacy Conversion Rules

- No HP-to-Coins conversion without approved policy.
- No RC-to-Coins conversion without approved policy.
- No XP-to-Coins conversion without approved policy.
- Do not automatically import legacy balances.
- Do not delete historical records without approved retention rules.

## 3. Legacy Migration Decision

If historical HP, RC or XP balances exist, the D13 migration decision must be reviewed and approved before implementation.

**Status: Blocked — Awaiting Legacy Data Migration Approval**

## 4. Shared Database Migration Rules

- Individual members must not generate or apply EF Core migrations.
- All seven module schemas require review and approval.
- Only the appointed DB Coordinator may create shared migration files.
- Migration changes require a separate task branch and PR.
- Updating the shared SQL Server database requires Team Leader approval.

## 5. Implementation

The `CoinCurrencyPolicy` class validates the supported Coins currency.

It does not perform currency conversions, SQL database changes or historical data migrations.

## 6. Acceptance Evidence

- Currency restriction code
- Unit tests
- Successful build and test output
- Peer-reviewed PR to `develop`

**T06 Status:** Implementation pending verification and peer review.
