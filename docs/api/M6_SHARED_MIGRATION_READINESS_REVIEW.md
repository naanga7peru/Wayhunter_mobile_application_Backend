	
# WAYHUNTER — M6 Shared Migration Readiness Review

Date: 2026-11-15
Owner: M6 — Kaviyan
Status: DRAFT — Review Required
Migration: HOLD

## 1. Scope

M6 manages:
- Coins Wallet
- Immutable Ledger
- Coin Reservations
- Reward Idempotency
- Reward Outbox
- Reward Consumer Deliveries
- Sample Voucher Offers
- Voucher Requests and Issuances

This document proposes SQL schema and constraints.
It does not authorize database migrations.

## 2. Existing Implementation

- MS01 Coins policy and rules: PRs merged
- MS02 Ledger models and prototype tests: PRs merged
- MS03-T01..T07: PRs merged
- Actual durable SQL Wallet store: Pending
- M6 EF Core configurations: Pending
- M6 DbContext registration: Pending
- SQL migration: Not generated

Merged code is not evidence of SQL migration acceptance.

## 3. Proposed Tables

### CoinWallets

- Id: UNIQUEIDENTIFIER PK
- UserId: NVARCHAR, length requires M1 approval
- PostedCoins: BIGINT NOT NULL
- ReservedCoins: BIGINT NOT NULL
- LifetimeEarnedCoins: BIGINT NOT NULL
- RowVersion: ROWVERSION

Constraints:
- UNIQUE(UserId)
- PostedCoins >= 0
- ReservedCoins >= 0
- ReservedCoins <= PostedCoins
- LifetimeEarnedCoins >= 0

### CoinLedgerEntries

- Id: UNIQUEIDENTIFIER PK
- WalletId: UNIQUEIDENTIFIER FK
- DeltaCoins: BIGINT NOT NULL
- RewardType: INT NULL
- SourceRecordId: NVARCHAR, length to approve
- RuleVersion: NVARCHAR, length to approve
- CorrelationId: UNIQUEIDENTIFIER
- RecordedAtUtc: UTC timestamp
- CorrectionOfEntryId: UNIQUEIDENTIFIER NULL

Rules:
- DeltaCoins must not be zero
- Ledger entries are append-only
- Corrections use new compensating entries
- Existing entries must not be updated or deleted

### CoinReservations

- Id: UNIQUEIDENTIFIER PK
- WalletId: UNIQUEIDENTIFIER FK
- RequestId: UNIQUEIDENTIFIER UNIQUE
- ReservedCoins: BIGINT
- Status: Pending / Consumed / Released
- CreatedAtUtc: UTC timestamp
- ResolvedAtUtc: UTC timestamp NULL

Rules:
- One reservation per redemption request
- Pending hold reduces spendable Coins
- Rejected/cancelled pending request releases once
- Consumed reservation cannot be released again

### CoinRuleVersions

- Id: UNIQUEIDENTIFIER PK
- VersionCode: NVARCHAR UNIQUE
- VoucherCostCoins: BIGINT
- VoucherReferenceLkr: DECIMAL(18,2)

### SampleVoucherOffers

- Id: UNIQUEIDENTIFIER PK
- OfferCode: NVARCHAR UNIQUE
- CostCoins: BIGINT
- ReferenceValueLkr: DECIMAL(18,2)
- IsActive: BIT
- ValidFromUtc / ValidUntilUtc: UTC timestamps

### SampleVoucherRedemptionRequests

- Id: UNIQUEIDENTIFIER PK
- WalletId: UNIQUEIDENTIFIER FK
- OfferId: UNIQUEIDENTIFIER FK
- OwnerUserId: NVARCHAR
- Status: Pending / Approved / Rejected / Cancelled
- AdminDecisionId: UNIQUEIDENTIFIER NULL
- RequestedAtUtc / DecidedAtUtc

### SampleVoucherInventory

- Id: UNIQUEIDENTIFIER PK
- OfferId: UNIQUEIDENTIFIER FK
- SampleCode: NVARCHAR UNIQUE
- AssignedRequestId: UNIQUEIDENTIFIER NULL

### SampleVoucherIssuances

- Id: UNIQUEIDENTIFIER PK
- RequestId: UNIQUEIDENTIFIER UNIQUE
- InventoryId: UNIQUEIDENTIFIER UNIQUE
- Status: Unissued / Failed / Issued
- QrPayload: NVARCHAR NULL
- ImageReference: NVARCHAR NULL
- IssuedAtUtc: UTC timestamp NULL

### CoinRewardOutbox

- EventId: UNIQUEIDENTIFIER PK
- RewardType: INT
- SourceRecordId: NVARCHAR
- RecipientUserId: NVARCHAR
- Status: Pending / Processing / Delivered
- AttemptNumber: INT
- NextDueAtUtc: UTC timestamp
- LeaseUntilUtc: UTC timestamp NULL
- ClaimToken: NVARCHAR NULL

### CoinRewardConsumerDeliveries

- Id: UNIQUEIDENTIFIER PK
- EventId: UNIQUEIDENTIFIER FK
- ConsumerName: NVARCHAR
- DeliveryState: INT
- AttemptCount: INT
- DeliveredAtUtc: UTC timestamp NULL

Constraint:
- UNIQUE(EventId, ConsumerName)

## 4. Reward Idempotency

Proposed uniqueness:
- UNIQUE(RewardType, SourceRecordId)

Welcome:
- One verified welcome reward per eligible user

Hunter:
- One reward per approved Hunter submission

Game Creator:
- One reward per approved Game

Game Winner:
- One winner reward per GameId across all recipients

Approval and reward source records must be trusted.
Client-provided approval flags are not sufficient.

## 5. Approved Coins Rules

- Welcome: +100
- Hunter approved: +200
- Game creator approved: +100
- Game winner approved: +100

Sample voucher:
- Spendable balance required: >= 10,100 Coins
- Pending reservation: 10,000 Coins
- Voucher reference value: LKR 100
- Actual cash payout: Out of scope

## 6. Voucher Debit Decision Conflict

Earlier repository documents:
- Debit on Admin approval

Later Product Owner clarification:
- Reserve at request
- Admin approval does not finalize debit
- Debit only when sample voucher is issued/fulfilled

Required:
- Reconfirm latest owner decision
- Update BRD, rules, tests and ERD consistently
- Obtain recorded approval before SQL implementation

Status: DECISION RECONCILIATION REQUIRED

## 7. Transaction Strategy

Reward:
1. Verify authoritative approval
2. Check unique reward source
3. Append immutable ledger entry
4. Update wallet projection
5. Insert reward outbox entry
6. Commit atomically

Voucher request:
1. Verify spendable balance >= 10,100
2. Reserve 10,000 Coins
3. Create pending request
4. Commit atomically

Voucher fulfillment:
1. Verify approved request and active reservation
2. Verify unique sample voucher inventory
3. Finalize issuance and one debit atomically
4. Consume reservation
5. Record audit entry
6. Commit

Final debit timing is subject to decision reconciliation.

## 8. Cross-Module Contracts

M1:
- UserId format, SQL length, FK target, deletion policy

M4:
- Trusted Hunter owner, approved submission source ID

M5:
- GameId, attempt ownership, unique verified winner

M6:
- Ledger ownership and unique reward source key
- Outbox/consumer transaction and retry ownership

DB Coordinator:
- Shared DbContext and migration assembly
- FK order, naming, index strategy, rollback procedure

## 9. Migration Approval Register

M1: NOT RECORDED
M2: NOT RECORDED
M3: NOT RECORDED
M4: NOT RECORDED
M5: NOT RECORDED
M6: PENDING OWNER SIGN-OFF
M7: NOT RECORDED

Team Leader migration authorization: NOT RECORDED
DB Coordinator: NOT RECORDED

Do not infer approval from GitHub PR merges.

## 10. Migration Gate

- Final schema reviewed
- Conflicting voucher policy reconciled
- M1/M4/M5 contracts approved
- All seven members explicitly signed off
- DB Coordinator appointed
- Migration PR reviewed
- Test SQL database backup confirmed
- Migration and rollback verified
- SQL concurrency and duplicate reward tests passed

Until all required gates are satisfied:
MIGRATION STATUS = HOLD
