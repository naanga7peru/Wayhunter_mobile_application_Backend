# WAYHUNTER — M6 Coins Reward & Accounting Rules

**Task ID:** M6-MS01-T01  
**Owner:** Kaviyan — M6 Coins & Wallet  
**Status:** Draft — Pending Peer Review  
**Source:** Latest Approved Rules — 2026-11-11

## 1. Supported Currency

Wayhunter uses Coins as its only reward currency. Legacy HP, RC, and XP currencies are not part of the active reward model.

## 2. Approved Earning Rules

| Reward Source | Coins | Eligibility |
|---|---:|---|
| Welcome Reward | +100 | First eligible verified user account |
| Hunter Approval | +200 | Admin approves an eligible Hunter submission |
| Game Creator Approval | +100 | Admin approves and publishes an eligible Game |
| Game Winner Approval | +100 | Admin verifies the single eligible Game winner |

## 3. Accounting Rules

1. Each approved reward source may credit Coins only once.
2. Pending or rejected submissions earn zero Coins.
3. M6 owns the authoritative wallet balances and ledger.
4. M1, M4, and M5 must not modify Wallet balances directly.
5. Every reward must reference a trusted source record and recipient.
6. Duplicate reward delivery must never create additional Coins.
7. Reward amounts and eligibility must be validated by the Backend.
8. Lifetime Earned Coins must remain distinguishable from Spendable Coins.

## 4. Module Dependencies

- M1 Authentication — Verified account reward.
- M4 Hunter — Approved place reward.
- M5 Games — Approved creator and verified winner rewards.
- M3 Notifications — Reward notification consumer.
- M7 Profile/Navigation — Wallet navigation and shared UI integration.

## 5. Proposed Integration Contract

Reward event details will be finalized under M6-MS01-T03.

Required concepts:
- Reward Type
- Source ID
- Recipient User ID
- Approved Reward Amount
- Approval Timestamp
- Idempotency Key

Final field names and transport are pending cross-module agreement.

## 6. Test Cases to Implement Later

- A verified user receives 100 Coins once.
- Repeated verification does not duplicate Coins.
- One eligible Hunter approval grants 200 Coins once.
- Game creator approval grants 100 Coins once.
- One verified Game winner receives 100 Coins once.
- Pending/rejected records grant zero Coins.
- Duplicate reward events are processed idempotently.

## 7. Implementation Status

This task documents the approved reward rules. Backend reward processing, database persistence, Flutter API integration, and the above behavioral tests are not yet implemented.

**Task Status:** Draft — Pending Review.
