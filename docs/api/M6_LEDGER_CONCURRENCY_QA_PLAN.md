# WAYHUNTER — M6 Ledger & Redemption QA Plan

**Task ID:** M6-MS01-T07  
**Owner:** Kaviyan — M6 Coins & Wallet  
**Status:** Draft — Pending Peer Review

## 1. Objective

Prepare concurrency, idempotency and rollback test cases for Coins wallet and academic demo voucher redemption.

## 2. Required QA Scenarios

| ID | Scenario | Expected Result |
|---|---|---|
| QA-01 | Same reward event received twice | Coins credited exactly once |
| QA-02 | Two distinct eligible reward sources | Each eligible source credited once |
| QA-03 | Same voucher request submitted twice | Only one reservation for that request |
| QA-04 | Concurrent requests with insufficient available balance | No overspending |
| QA-05 | Admin Approve and Reject happen concurrently | Only one final state succeeds |
| QA-06 | Admin Approve retried | Exactly one permanent Coins debit |
| QA-07 | Voucher issuance retried | No additional Coins debit |
| QA-08 | Ledger transaction fails during commit | All related transaction changes roll back |
| QA-09 | Two requests compete for one available sample voucher | No duplicate voucher allocation |
| QA-10 | Pending request rejected or cancelled | Reservation released exactly once |
| QA-11 | Voucher issuance fails after approval | Debit preserved, voucher remains unissued, controlled retry permitted |
| QA-12 | Spendable Coins below 10,100 | Sample voucher request rejected |

## 3. Approved Financial Rules

- Active currency: Coins only.
- 10,000 Coins = LKR 100 sample voucher reference.
- Request requires at least 10,100 spendable Coins.
- Request holds 10,000 Coins without permanent debit.
- Admin approval finalizes the debit exactly once.
- Rejection or cancellation releases the hold exactly once.
- Issuance and fulfillment do not debit again.
- No real cash payout.
- No automatic refund policy is approved.

## 4. Current Automated Test Scope

CoinLedgerQaContractTests checks existing domain policies for reward key construction, wallet balance invariants, threshold eligibility and voucher approval rules.

These tests do not prove database-level idempotency, concurrent transaction safety or rollback.

## 5. Pending Integration Tests

QA-01, QA-03 through QA-11 require implementation of the authoritative transactional ledger, redemption workflow and/or sample voucher inventory.

These cases must be tested against actual repository/database behavior when the implementation becomes available.

**Status: Blocked — Awaiting Shared Database Migration and Ledger Implementation**, where applicable.

## 6. Database Rule

Individual module developers must not create EF Core migrations, modify shared ModelSnapshot files or update the shared SQL Server database.

Shared database migrations require approved schema review and the appointed DB Coordinator.

## 7. Acceptance Evidence

- QA scenario matrix
- C# domain policy tests
- Build and unit test results
- Peer-reviewed PR targeting develop

**Task Status: Draft — Pending Review.**
