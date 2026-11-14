# WAYHUNTER — M6 Wallet & Ledger Schema Draft

**Task:** M6-MS02-T01  
**Owner:** Kaviyan — M6  
**Status:** Draft — Awaiting Schema Review

## 1. Proposed Data Models

| Model | Purpose |
|---|---|
| CoinWalletSchemaDraft | Wallet owner, posted Coins, reserved Coins and lifetime earned |
| CoinLedgerEntrySchemaDraft | Append-only signed Coins ledger entries |
| CoinReservationSchemaDraft | Pending Coins reservations associated with a request |
| CoinRuleVersionSchemaDraft | Versioned voucher Coins cost and LKR reference |

## 2. Approved Business Rules

- Coins are stored as integer values.
- LKR voucher reference is stored as decimal, never floating point.
- Minimum spendable Coins before sample redemption is 10,100.
- A voucher request reserves 10,000 Coins.
- Admin approval debits exactly once and consumes the reservation.
- Issuance does not debit again.
- Voucher reference is LKR 100, not actual cash payout.

## 3. Proposed Database Relationships

- Wallet belongs to an identity user.
- Ledger entry references a wallet.
- Reservation references a wallet and redemption request.
- Ledger entry references an applicable rule version.

Foreign keys, uniqueness rules, relationship cardinalities and indexes require cross-module schema approval.

## 4. Identity and Immutability

- Wallet, ledger entry, reservation and rule version use stable GUID identifiers in this draft.
- UserId uses the existing string-based user identity convention.
- SourceRecordId identifies the authoritative source event.
- Ledger entry source and identity must not be edited after posting.
- Corrections must use compensating entries, not overwrite or delete existing ledger entries.

## 5. Implementation Boundaries

This task includes C# draft models and unit tests only.

It does not include:
- EF Core configurations or DbContext changes
- SQL tables, indexes or constraints
- EF Core migrations
- Shared database updates
- Actual transactional ledger operations

## 6. Required Review

- Confirm canonical ERD names and field types.
- Confirm M1 user identity key type.
- Confirm cross-module event and redemption request identifiers.
- Review wallet and reservation lifecycle fields.
- Approve uniqueness and transactional constraints in M6-MS02-T02.
- Obtain seven-module schema approval and Team Leader migration authorization.

**Migration Status: Blocked — Awaiting Shared Database Migration Approval**

**Task Status: Draft — Pending Peer Review**
