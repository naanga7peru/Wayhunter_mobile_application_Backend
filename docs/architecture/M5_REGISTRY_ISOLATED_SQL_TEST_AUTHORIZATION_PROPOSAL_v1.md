# WAYHUNTER M5 — Global Media Claim Registry: Isolated SQL Test Schema & Authorization Proposal v1

**Prepared for:** Kaviyan — Team Leader / DB Coordinator
**Owner:** Thuvarathan — M5 Games
**Date:** 2026-11-21
**Context:** Backend PR #256, `task/m5-idempotent-proof-recovery`
**Architecture baseline:** `docs/architecture/M5_GLOBAL_MEDIA_CLAIM_REGISTRY_FINAL_DESIGN_REVIEW_v3.md`, SHA `411e5c6fcd8559312e732c7039355ca1a5ab5b91`
**Status:** **REQUEST FOR SEPARATE ISOLATED TEST-SCHEMA / SQL EXECUTION AUTHORIZATION — NOT APPROVED OR EXECUTED**.

> This file is a proposed physical test shape and execution plan. It authorizes no migration, no DDL, no registry SQL execution, no production activation, no history mutation, and no PR merge. The physical table, canonicalization and index strategy remain subject to DB Coordinator + M4 decisions. Previously accepted M5 operation-store SQL results do **not** grant registry SQL test permission.

## 1. Required independent approvals

Before **any** registry SQL test or test-only DDL:

- [ ] DB Coordinator names and explicitly authorizes the isolated SQL Server instance, exact disposable database, login, test window and cleanup rules.
- [ ] DB Coordinator approves the exact test-only schema, dependency alternate keys, check/index/collation changes, controlled-writer objects and allowed DDL/transaction operations.
- [ ] M4 approves authoritative provider namespace, canonical media ID normalization and equivalence test vectors; Game-purpose capture compatibility remains **HOLD** until separately resolved. No real provider consumption in this registry test suite.
- [ ] DB Coordinator verifies whether existing `GameAttempts`, `GameProofEvidence`, and proposed `GameProofSubmissionOperations` physical schemas and principal keys are present in the **isolated test** snapshot.
- [ ] DB Coordinator verifies reserved key lengths, index limits, FK text collation compatibility and the implementation of case/Unicode equality; do not infer canonicalization from SQL collation.
- [ ] Reviewers separately approve any test service-principal privileges, immutable-guard trigger/procedure prototypes, and any operation/claim transaction refactor under test.
- [ ] Final cutover H0-H3 gates remain disabled for production, irrespective of isolated test results.

## 2. Proposed test environment and isolation (not yet authorized)

| Parameter | Proposal; DB Coordinator must confirm |
|---|---|
| SQL instance | `(localdb)\\MSSQLLocalDB` on an authorized developer machine, or a coordinator-provided disposable SQL Server |
| Database | `Wayhunter_M5_Registry_IsolatedTest` (new, dedicated; **not** shared/production; name is only a request) |
| Schema | Existing approved `InitialSharedSchema` copied/created in a **disposable** test database only after permission, with registry-related test-only delta |
| Connection | Separate least-privilege test identity; explicit opt-in, never application production secrets |
| Tests | Integration test filter for M5 registry only, run sequentially for setup/teardown; concurrency tests use 2+ independent SQL connections |
| External M4 | Deterministic fake provider, **no real camera, upload, consume or API activation** |
| Test cleanup | Prove no residual app rows, claim/operation/evidence/receipt/leases and no modified shared DB; keep audit of test-only DDL created/dropped |
| Logging | Sanitized IDs/hashes and counts only; no raw media URLs, personal data or secrets |

**Explicit prohibition:** do not assume earlier permission for `Wayhunter_M5_IntegrationTest` includes this new registry table. Do not use `EnsureCreatedAsync`, `dotnet ef database update`, automatic migrations or test setup SQL before written DDL authorization.

## 3. Exact candidate registry physical shape (proposed, not implemented)

All 16 columns from accepted Design v3 are retained unchanged. Only candidate SQL Server types, collation and nullability are specified:

| Column | Candidate type | Null? | Ownership |
|---|---|---|---|
| `MediaClaimId` | `uniqueidentifier` | No | PK, immutable |
| `MediaProviderNamespace` | `nvarchar(64) COLLATE Latin1_General_100_BIN2` | No | M4-authoritative, immutable |
| `CanonicalMediaId` | `nvarchar(256) COLLATE Latin1_General_100_BIN2` | No | M4-authoritative, immutable |
| `SubmissionOperationId` | `uniqueidentifier` | Yes | Non-null for new; null for verified legacy only |
| `EvidenceId` | `uniqueidentifier` | Yes | One-time evidence link |
| `GameId` | `uniqueidentifier` | No | Immutable |
| `AttemptId` | `uniqueidentifier` | No | Immutable |
| `StepNumber` | `int` | No | Immutable; >= 1 |
| `AuthenticatedPlayerUserId` | `nvarchar(256)` | No | Trusted M1 player; immutable |
| `ClaimOrigin` | `tinyint` | No | `1 NewOperation`, `2 VerifiedLegacyEvidence` |
| `ClaimState` | `tinyint` | No | `0 Reserved`, `1 ProviderConsumed`, `2 EvidenceCommitted`, `3 CancelledUnusedTombstone`, `4 LegacyIndexed` |
| `ProviderConsumedAtUtc` | `datetimeoffset(7)` | Yes | Set only alongside confirmed operation receipt in TX-B |
| `EvidenceCommittedAtUtc` | `datetimeoffset(7)` | Yes | Set atomically in TX-C |
| `CreatedAtUtc` | `datetimeoffset(7)` | No | Immutable |
| `UpdatedAtUtc` | `datetimeoffset(7)` | No | Monotonic on real transitions |
| `RowVersion` | `rowversion` | No | SQL-generated |

### 3.1 Test-only PK, unique/nonunique indexes

| Name | Exact candidate columns / filter |
|---|---|
| `PK_GameProofMediaClaims` | PRIMARY KEY (`MediaClaimId`) |
| `UX_GameProofMediaClaims_Provider_Canonical` | UNIQUE **NONFILTERED** (`MediaProviderNamespace`, `CanonicalMediaId`); tombstones remain unique |
| `UX_GameProofMediaClaims_Operation` | UNIQUE (`SubmissionOperationId`) FILTER `IS NOT NULL` |
| `UX_GameProofMediaClaims_Evidence` | UNIQUE (`EvidenceId`) FILTER `IS NOT NULL` |
| `IX_GameProofMediaClaims_Attempt` | (`AttemptId`, `StepNumber`, `ClaimState`) |
| `IX_GameProofMediaClaims_Reconciliation` | (`ClaimState`, `UpdatedAtUtc`) |

**Principal/foreign key candidates** (all `ON DELETE NO ACTION`, no direct runtime bypass):

1. (`AttemptId`, `GameId`, `AuthenticatedPlayerUserId`) → `GameAttempts(Id, GameId, PlayerUserId)`; existing `AK_GameAttempts_Id_Game_Player` must be **physically confirmed**.
2. (`SubmissionOperationId`, `GameId`, `AttemptId`, `AuthenticatedPlayerUserId`, `StepNumber`, `MediaProviderNamespace`, `CanonicalMediaId`) → `GameProofSubmissionOperations` same ordered seven columns, backed by candidate `AK_GameProofOperations_ImmutableClaimOwner`.
3. (`EvidenceId`, `GameId`, `AttemptId`, `StepNumber`) → `GameProofEvidence(Id, GameId, AttemptId, StepNumber)`, backed by candidate `AK_GameProofEvidence_Id_Game_Attempt_Step`.

A composite FK is not checked when its optional leading child value is null; CHECK constraints below must control legacy nullability. Operation/evidence FK principal-key and collation differences **must be reconciled before any executable setup**. The 7-column text-heavy candidate requires physical index-size verification. Do not approve a smaller key without equivalent database-enforced ownership semantics.

### 3.2 Complete CHECK expression candidates

These are **review text**, not authorized executable SQL. The test-only setup must contain all five, not abbreviated variants.

```sql
-- CK_GameProofMediaClaims_IdsAndStep
[MediaClaimId] <> '00000000-0000-0000-0000-000000000000'
AND [GameId] <> '00000000-0000-0000-0000-000000000000'
AND [AttemptId] <> '00000000-0000-0000-0000-000000000000'
AND [StepNumber] >= 1
AND ([SubmissionOperationId] IS NULL OR [SubmissionOperationId] <> '00000000-0000-0000-0000-000000000000')
AND ([EvidenceId] IS NULL OR [EvidenceId] <> '00000000-0000-0000-0000-000000000000')

-- CK_GameProofMediaClaims_IdentityNonblank
DATALENGTH([MediaProviderNamespace]) > 0
AND DATALENGTH([CanonicalMediaId]) > 0
AND DATALENGTH([AuthenticatedPlayerUserId]) > 0
AND LEN(LTRIM(RTRIM([MediaProviderNamespace]))) > 0
AND LEN(LTRIM(RTRIM([CanonicalMediaId]))) > 0
AND LEN(LTRIM(RTRIM([AuthenticatedPlayerUserId]))) > 0

-- CK_GameProofMediaClaims_OriginState
[ClaimOrigin] IN (1, 2) AND [ClaimState] IN (0, 1, 2, 3, 4)
AND (([ClaimOrigin] = 1 AND [SubmissionOperationId] IS NOT NULL AND [ClaimState] IN (0, 1, 2, 3))
  OR ([ClaimOrigin] = 2 AND [SubmissionOperationId] IS NULL AND [ClaimState] = 4 AND [EvidenceId] IS NOT NULL))

-- CK_GameProofMediaClaims_StateFields
(([ClaimState] = 0 AND [EvidenceId] IS NULL AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL)
OR ([ClaimState] = 1 AND [EvidenceId] IS NULL AND [ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] IS NULL)
OR ([ClaimState] = 2 AND [EvidenceId] IS NOT NULL AND [ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] IS NOT NULL)
OR ([ClaimState] = 3 AND [EvidenceId] IS NULL AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL)
OR ([ClaimState] = 4 AND [EvidenceId] IS NOT NULL AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL))

-- CK_GameProofMediaClaims_TimeOrder
[UpdatedAtUtc] >= [CreatedAtUtc]
AND ([ProviderConsumedAtUtc] IS NULL OR [ProviderConsumedAtUtc] >= [CreatedAtUtc])
AND ([EvidenceCommittedAtUtc] IS NULL OR ([ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] >= [ProviderConsumedAtUtc]))
AND ([EvidenceCommittedAtUtc] IS NULL OR [UpdatedAtUtc] >= [EvidenceCommittedAtUtc])
```

**Important limitations:** No CHECK can prove provider provenance, receipt ↔ operation consistency, immutable updates across old/new values, or absence of legacy collisions. Those require approved controlled-writer procedures/privileges/guards and M4 authoritative receipts.

## 4. Required test-only transaction and controlled-writer proposals

**TX-A (M5):** one connection/one serializable transaction; acquire Game `UPDLOCK,HOLDLOCK`, Attempt/Progress, Operation, Registry; register operation and reserve media as one atomic commit. Any ambiguous COMMIT result requires an independent authoritative read of **both** rows before considering M4 invocation.

**TX-P (M4):** separate provider transaction is **not part of this registry test grant**. Use fake-provider outcomes; M4 owner later tests durable operation-bound consume/receipt in M4's own isolated scope.

**TX-B (M5):** after **confirmed** fake M4 consumed/receipt response, hold the same Game-first lock order, validate claim token/lease, and update M5 operation receipt/state + registry `ProviderConsumed` in one atomic commit. Unknown provider outcomes never authorize TX-B.

**TX-C (M5):** acquire Game → Attempt/Progress → Operation → Registry → Evidence; evidence insert, progress/decision, operation completed and registry evidence link in one atomic commit. Final-stage result remains pending Admin; never award Coins here.

**Test-only immutability plan:** DB Coordinator must separately approve a least-privileged procedure executor with **no direct application role DML** for registry, operations and evidence, plus a multi-row-safe immutable `UPDATE/DELETE` guard or equally authoritative enforced procedure surface. Confirm that terminal tombstones (`3`, `4`) and completed claims (`2`) cannot be deleted or released. Test direct DML denial and privileged trigger behavior separately; never claim pure C# validators enforce SQL immutability.

**Cutover H0-H3:** These isolated tests do **not** activate H1/H2/H3, backfill old evidence, or allow new production proof writes when unresolved history exists. No actual history data may be copied into tests without scoped, privacy-reviewed read-only authorization.

## 5. Exact proposed test matrix and entry/exit criteria

### 5.1 Metadata/preflight (no DB execution until authorized)

| ID | Test | Pass criterion |
|---|---|---|
| M01 | All 16 columns, nullability, times, `rowversion` | Exact v3 metadata; no implicit drift |
| M02 | PK + 5 indexes, unique/nonfiltered provider pair | State-agnostic pair uniqueness preserved |
| M03 | Three composite FKs + 3 principal keys | Same ordered columns/types/collations; NO ACTION |
| M04 | Candidate key-byte-width inspection | All fixed/max variable-size constraints supported by actual SQL Server version |
| M05 | All five CHECK expressions + approved immutable guard | Rejection behavior matches v3, not just code-level validation |
| M06 | Test user denied direct writes; approved controlled writer only | No bypass through normal application principal |

### 5.2 Registration / confirmation / finalization

| ID | Test | Pass criterion |
|---|---|---|
| A01-A02 | TX-A normal registration and exception after first INSERT | Both op+claim exist or both absent; M4 fake calls zero on failure |
| A03-A04 | Same-operation exact retry / altered immutable binding | Exact retry returns original; conflict rejected |
| A05 | Two Games, same media pair with 2 independent SQL connections | One owner only; loser receives deterministic conflict |
| A06-A07 | Lost TX-A COMMIT ack / mismatched op and claim | Consistent fresh read or fail closed; no blind provider consume |
| A08 | Game-level submission/review competes with registration | Game-first ordering preserved; no unbounded deadlock/retry |
| B01-B02 | Confirmed receipt / exception between TX-B updates | Op receipt+registry state atomic; M4 fake consumed once |
| B03-B04 | Lost TX-B COMMIT ack / M4 fake Unknown | Reconcile only; no reconsume/progress/reward |
| B05 | Two operation IDs for same capture (M4-owned case) | M4 proof deferred until separate provider contract/SQL approval |
| C01-C02 | TX-C full commit / injected exception mid-finalize | One evidence+progress+operation+claim, or none of TX-C |
| C03-C05 | Lost TX-C COMMIT ack / stale lease / exact retry | Coherent return or HOLD; no duplicate evidence/progress |
| C06-C07 | Wrong attempt/step evidence / final step | FK and eligibility reject; no auto-winner/reward |

### 5.3 Concurrency, security and legacy gates

| ID | Test | Pass criterion |
|---|---|---|
| D01-D03 | Immutable fields updated, evidence rebind, DELETE/state regression | DB rejects and preserves claims/tombstones |
| D04 | Old raw `GameProofEvidence` writer tries bypass post-cutover | Denied by approved service permission/guard test fixture |
| H01-H04 | Unknown historical namespace, duplicate canonical key, aliases, rollback | Gate remains disabled until M4 + DB Coordinator clearance |
| P01 | Multi-connection contention / timeout / deadlock victim | One authoritative owner, original operation ID reused only after safe reconciliation |

**Fault injection:** Simulate database commit-ack loss at the client boundary; a simulated exception alone is not proof of an actual SQL COMMIT ambiguity. Read via a new physical connection after untrusted SQL results. A fake provider must return `Unknown`, delayed `Consumed`, stable receipt, duplicate reject and mismatched retry scenarios; tests must assert provider consume-call counts.

**Exit criteria:** Metadata checks pass, no new SQL exceptions or incomplete transactions, no double ownership, no double stage advance, receipt and registry reconcile, direct bypass denied, tombstones immutable, sanitized cleanup audit reports 0 remaining application/test rows, full build/unit regressions pass. Retain results with target/database/time/test IDs and commit SHA. Any skipped/unexecuted case is reported explicitly rather than counted as passed.

## 6. Authorization decision requested from Kaviyan

Please reply in writing with:

1. APPROVED / CORRECTIONS REQUIRED / HOLD for the **test-only registry schema proposal** above; approval of the previous design v3 does not answer this.
2. Exact database + SQL instance + principal, allowed setup and teardown DDL, whether `ALTER` of **test-only** operation/evidence alternate keys/collations and controlled-writer objects is permitted, and whether explicit test `EnsureCreated` is prohibited or separately approved.
3. Whether the full matrix or a named subset may run first, and which tests require M4 final canonicalization decisions before safe execution.
4. How to handle failed/collision tests without touching shared databases and how to preserve minimal sanitized test evidence.
5. Required acceptance evidence and whether a second explicit test execution authorization is needed after test-schema review.

**Until written authorization: no SQL execution, no shared migration/DDL, no provider activation, no historical DML, and PR #256 DO NOT MERGE.**
