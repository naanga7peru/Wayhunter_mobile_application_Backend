# WAYHUNTER — M6 Sample Voucher Fulfillment Policy

**Task:** M6-MS01-T05  
**Owner:** Kaviyan — M6  
**Status:** Draft — Pending Review

## 1. Voucher Reference

- Voucher cost: 10,000 Coins
- Reference value: LKR 100
- Minimum spendable balance before request: 10,100 Coins
- Voucher type: Academic demonstration sample
- Actual cash payout: Out of scope
- Merchant settlement: Out of scope

## 2. Approved Workflow

1. User submits an eligible redemption request.
2. Pending request reserves 10,000 Coins.
3. Rejected or cancelled pending request releases its reservation exactly once.
4. Authorized Admin approval finalizes the 10,000-Coin debit exactly once.
5. The reservation is consumed when approval is finalized.
6. The approved voucher is then issued using a unique sample code, image and QR.
7. Voucher issuance and retries must not debit Coins again.
8. Failed issuance must preserve the approved debit and unissued state for controlled retry.

## 3. Safety Boundaries

- Never treat a sample voucher as actual cash.
- No automatic refund policy is approved.
- Do not expose a voucher code or QR as usable before successful issuance.
- Duplicate approvals and retries must not cause duplicate debits.
- No real merchant payout or payment gateway is implemented.
- OCR and intake barcode scanning are optional.

## 4. Implementation Scope

This task implements policy constants and pure validation rules.

It does not implement:
- Database persistence
- Ledger debit transactions
- Actual voucher issuance
- HTTP endpoints
- EF Core migrations
- External payment APIs

## 5. Later Implementation

Ledger transactions, reservation state management, idempotent approval and issuance will be implemented in their respective M6 milestones.

Shared migrations must be performed only by the appointed DB Coordinator after the required approvals.

## 6. Acceptance Evidence

- Policy code
- Unit tests
- Successful build
- Successful test run
- Reviewed Pull Request to develop

**Task Status:** Draft — Pending Peer Review.
