
# M4-MS07 — Isolated SQL Integration & Trusted Runtime Readiness

**Module:** M4 — Hunter Places
**Task:** M4-MS07
**Branch:** `task/m4-ms07-isolated-sql-runtime-readiness`
**Verification date:** 2026-11-19
**Status:** Readiness review only — not production approved

---

## 1. Scope

This task documents source-level readiness for:

1. M4 isolated SQL integration verification.
2. M1–M4 trusted identity mapping and runtime wiring.
3. M4–M6 approved Hunter reward integration.
4. Backend build and test verification.
5. Remaining approvals and production activation blockers.

This task does not authorize database migrations, live SQL integration testing, or production activation.

## 2. M4 Isolated SQL Integration Readiness

### 2.1 Test implementation

Test file:

`tests/Wayhunter.IntegrationTests/Modules/HunterPlaces/M4SqlIntegrationVerificationTests.cs`

The isolated SQL integration tests cover:

1. Concurrent capture-session consumption — exactly one successful consumption.
2. Hunter Place status transition — revocation of unused capture sessions.
3. Capture-session revocation failure — rollback of the status transition.

### 2.2 SQL test configuration

Required environment variable:

`WAYHUNTER_M4_TEST_SQL_CONNECTION`

Expected dedicated test database:

`Wayhunter_M4_IntegrationTest`

The SQL test implementation checks the connection string's initial catalog.

The tests use `EnsureCreatedAsync` for test database initialization.

### 2.3 Verification limitations

- The SQL connection environment variable was not configured during the fresh verification.
- Dedicated M4 SQL integration tests were not executed.
- DB Coordinator approval is still pending.
- Database-name validation alone does not prove SQL Server instance isolation.
- Actual SQL Server schema constraints and runtime behavior remain unverified.

**Decision:** SQL integration readiness reviewed at source level only. Live execution is pending approval.

## 3. M1–M4 Trusted Identity Readiness

### 3.1 Reviewed contracts and services

- `TrustedIdentityAccessContract`
- `AuthenticatedSessionAuthorizationService`
- `SessionBearerAuthenticationHandler`
- `TrustedHunterCaptureSessionIdentityResolver`
- `ITrustedIdentityAccountReader`

### 3.2 Identity mapping

Current source-level identity representations:

- M1 `UserAccount.UserId`: GUID-formatted string.
- M4 `HunterCaptureSessionRow.HunterId`: `Guid`.

Proposed mapping:

M1 authenticated user identity → validated GUID → M4 Hunter identity.

The resolver validates GUID identity representation and depends on a trusted account reader.

### 3.3 Pending decisions

- M1 owner must approve the logical identity mapping.
- The trusted identity claim contract must be confirmed.
- Invalid and inactive account handling must be approved.
- A concrete trusted account reader implementation and DI registration were not confirmed during the source review.
- M4 authenticated API runtime wiring remains unverified.
- M1–M4 database relationship and foreign-key strategy require approval.

**Decision:** Identity integration is not production approved.

## 4. M4–M6 Approved Hunter Reward Readiness

### 4.1 Reviewed components

- `GrantApprovedHunterCoinsHandler`
- `SqlHunterApprovedRewardReader`
- `SqlCommittedCoinRewardSource`
- `SqlCoinRewardGrantStore`
- `CoinWalletPersistenceConfigurations`
- `CoinsWalletServiceCollectionExtensions`
- `Program.cs`

### 4.2 Reward contract

The reviewed implementation defines:

- Reward type: `HunterApproved`.
- Reward amount: 200 coins.
- Stable source identity: Hunter submission ID.
- Eligibility: Committed approved Hunter submission and review.

The direct grant handler validates approval/review information and UTC timestamps before requesting a reward grant.

### 4.3 Duplicate prevention and transaction behavior

The reviewed SQL reward store includes:

- Serializable transaction handling.
- SQL application locking using `sp_getapplock`.
- Duplicate ledger-entry checks.
- Atomic wallet and ledger updates.
- Handling for SQL unique-constraint violations.

The EF persistence configuration defines a unique index over:

`(WalletId, RewardType, SourceRecordId)`

These findings are based on source-code review.

The actual SQL Server index and transaction behavior have not been live-verified.

### 4.4 Reconciliation limitation

The committed reward source uses committed approved submissions and reviews to identify reward candidates.

Its eligibility checks are not fully equivalent to all direct-handler validation checks.

The reconciliation validation contract requires M6 owner review before production activation.

### 4.5 Runtime feature flags

Reviewed runtime gates:

- `Identity:EnableSharedAuthentication`
- `CoinsWallet:EnableSqlRewardWriter`
- `CoinsWallet:EnableSqlRewardOutboxStore`
- `CoinsWallet:EnableRewardOutboxWorker`

SQL reward writer registration depends on shared authentication and reward-writer configuration.

The outbox worker requires additional feature flags.

Actual production configuration values were not verified.

**Decision:** Reward producer, SQL writer, outbox store, and worker must not be activated through this readiness task.

## 5. Fresh Backend Verification — 2026-11-19

### 5.1 Backend build

Command:

`dotnet build Wayhunter.slnx --no-restore`

Result:

**PASSED — 3.2 seconds**

### 5.2 Unit tests

Command:

`dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --no-restore --logger "console;verbosity=normal"`

Results:

- Total: 2,892
- Passed: 2,892
- Failed: 0
- Skipped: 0
- Duration: approximately 7.5 seconds

**Result: PASSED**

### 5.3 Integration tests

Command:

`dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-build --no-restore --logger "console;verbosity=normal"`

Results:

- Total: 178
- Passed: 108
- Failed: 0
- Skipped: 70
- Duration: approximately 7.4 seconds

**Result: PASSED — with 70 skipped tests**

### 5.4 Combined test summary

| Metric | Result |
|---|---:|
| Total tests | 3,070 |
| Passed | 3,000 |
| Failed | 0 |
| Skipped | 70 |

### 5.5 SQL safety verification

Command:

`[bool]$env:WAYHUNTER_M4_TEST_SQL_CONNECTION`

Output:

`False`

The dedicated M4 SQL connection was not configured.

The three dedicated M4 SQL integration tests remain unexecuted pending DB Coordinator approval.

No live SQL verification is claimed.

## 6. Outstanding Blockers and Approvals

### DB Coordinator

- Approve dedicated SQL integration test execution.
- Confirm SQL Server instance and database isolation.
- Approve the applicable migration and schema verification strategy.
- Confirm actual SQL constraints and indexes.

### M1 Owner

- Approve the trusted M1–M4 identity mapping.
- Confirm authenticated identity claims.
- Confirm trusted account reader ownership.
- Approve inactive and invalid account behavior.

### M4 Owner

- Approve capture-session identity integration.
- Confirm authenticated M4 API runtime behavior.
- Review SQL test evidence after authorized execution.

### M6 Owner

- Approve reward eligibility and idempotency contracts.
- Review reconciliation validation consistency.
- Confirm reward producer and outbox activation requirements.

### Runtime / Integration Team

- Verify concrete dependency injection registrations.
- Verify feature flag configuration.
- Validate end-to-end runtime wiring after approvals.

## 7. Release Readiness Decision

**Current status: SOURCE-LEVEL READINESS VERIFIED — LIVE SQL AND PRODUCTION ACTIVATION PENDING.**

Verified:

- Backend build passed.
- 2,892 unit tests passed.
- 108 integration tests passed.
- Zero test failures.
- SQL test connection was not configured.
- Source-level identity and reward integration reviewed.

Not verified:

- Dedicated M4 SQL integration tests.
- Actual SQL Server schema and constraints.
- Production trusted identity wiring.
- Production reward writer and outbox behavior.
- End-to-end runtime integration.

No SQL migration or `InitialSharedSchema` modification is authorized by this document.

No production feature flag activation is authorized by this document.

**Final decision:** M4-MS07 readiness evidence may be submitted for review. Live SQL execution and production integration remain blocked until the relevant owners and DB Coordinator approve them.
