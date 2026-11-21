# WAYHUNTER M5 — Global Media-Claim Registry: Detailed Design for Final DB Review (v2)

**Owner:** Thuvarathan (M5 Games)
**Review recipient:** Kaviyan — Team Leader / DB Coordinator; M4 Capture Owner for canonical media and provider contract
**Date:** 2026-11-21
**Context:** M5 PR [#256](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/256), branch `task/m5-idempotent-proof-recovery`; [backend repository](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend)
**Status:** **DETAILED DESIGN PROPOSAL — FINAL PHYSICAL SCHEMA APPROVAL PENDING**
**Current authorization:** Central media-claim registry **conditionally approved in principle only**. Historical audit preparation **READ-ONLY**. Code-only models, validators, metadata/fake-provider tests and test plans may proceed. **No migration, DDL, data mutation, provider activation, recovery worker activation or PR merge is authorized.**
**Related M4 decision:** **HOLD — FINAL CAPTURE COMPATIBILITY APPROVAL PENDING.**

---

## 1. Verified source baseline versus proposed changes

### Existing facts in the inspected M5 branch

1. `GameProofEvidenceRow` maps to `dbo.GameProofEvidence` and contains `Id` (`Guid`), `AttemptId`, `GameId`, `StepNumber`, `MediaId` (max 256), `SubmittedAtUtc`. The index `(AttemptId, StepNumber, SubmittedAtUtc)` is **not** a global media uniqueness constraint. `MediaProviderNamespace` and `CanonicalMediaId` do **not** exist on historical evidence rows.
2. `SqlGameProofSubmissionRepository` performs an application-side `AnyAsync(x => x.MediaId == cleanMediaId)` check; this is **not** a cross-table, concurrent uniqueness guarantee.
3. **Proposed isolated only** `GameProofSubmissionOperationRow` contains `SubmissionOperationId`, `AuthenticatedPlayerUserId`, `GameId`, `AttemptId`, `StepNumber`, `CaptureSessionId`, `MediaProviderNamespace`, `CanonicalMediaId`, state/claim/lease/reconciliation timestamps and `ProviderReceiptId`. Its EF configuration proposes unique `(MediaProviderNamespace, CanonicalMediaId)` and `(MediaProviderNamespace, CaptureSessionId)` **within that table**, not across the evidence table. This configuration is **not registered in production shared DbContext or migration**.
4. `IGameProofCaptureRecoveryProvider` is an M5 **proposed interface**, not a production M4 adapter. Current M4 Hunter capture sessions reference a `HunterSubmissionId`; their lifecycle may not be compatible with Game/Attempt/Step proof. No M4 purpose reuse is approved.
5. M5 isolated LocalDB recovery and receipt tests establish **limited isolated behavior**; they do **not** validate this proposed registry, a deployed schema, real provider outcome lookup or global historical backfill.

**Problem:** two different persistence tables can independently contain the same raw/canonical media without a shared durable SQL uniqueness authority. An ordinary pre-insert read is vulnerable to concurrent races, and raw `MediaId` cannot be safely converted into a provider-aware key without trusted provenance.

### Scope boundaries

- M5 owns Game proof operation claims, verified capture response orchestration, evidence/progress/completion transaction and this proposed M5 registry.
- M4 owns (if its contract is approved) capture issuance/validation, trusted provider namespaces, canonicalization and **atomic single-use provider consumption + durable receipt/outcome lookup within M4's own transaction**.
- DB Coordinator owns physical shared schema, FK/index/constraint approval, legacy backfill strategy and migration sequencing.
- M1 owns trusted account/identity mapping. Neither media-key membership nor claims from the client establish M1 ownership.

---

## 2. Canonical identity contract (M4 confirmation is a blocker)

**Authoritative key:** `(MediaProviderNamespace, CanonicalMediaId)`.

The following are **proposed requirements**, **not confirmed M4 provider values**:

| Field | Proposed type / comparison | Source and rule |
|---|---|---|
| `MediaProviderNamespace` | `nvarchar(64)`, binary (`Latin1_General_100_BIN2`) **candidate** | Must come from approved server-side M4/provider configuration, never an untrusted Flutter field. M4 must confirm provider namespace and normalization (case, aliases, trim, Unicode, versioning). |
| `CanonicalMediaId` | `nvarchar(256)`, binary (`Latin1_General_100_BIN2`) **candidate** | Must come from trusted M4/provider asset metadata; enforce agreed stable canonical format. M4 must confirm whether different raw representations are equivalent. |
| Pair equality | Exact persisted normalized bytes/values under **one approved** SQL collation | M4 and DB Coordinator must agree how EF, SQL, provider callbacks and legacy audit interpret equality. Do not case-fold or trim IDs as an unapproved assumption. |
| Legacy `GameProofEvidence.MediaId` | Current `nvarchar(256)` raw value with **no provider namespace** | Cannot automatically produce a trusted pair. Unresolved records stay in the exception report, not silently entered into an assumed provider. |

**M4 owner must explicitly return:** provider namespace(s); asset identity source; normalization/equivalence algorithm and version; media immutability; single-use scope; whether a Game-specific capture session is required; durable provider receipt and lookup semantics. Until then **new registry activation and final schema approval remain blocked**. Never relabel an M4 Draft HunterSubmission as Game proof without authorized compatibility verification.

---

## 3. Exact proposed `dbo.GameProofMediaClaims` column contract

**Logical design only — not DDL, no EF registration or migration authorized.** Types/lengths below are concrete *candidates for approval*; collation is expressly provisional pending M4 decision.

| Column | Proposed SQL Server type | Null? | Purpose / immutability |
|---|---|---:|---|
| `MediaClaimId` | `uniqueidentifier` | No | PK, server-created, never changed |
| `MediaProviderNamespace` | `nvarchar(64) COLLATE Latin1_General_100_BIN2` | No | Immutable trusted provider namespace |
| `CanonicalMediaId` | `nvarchar(256) COLLATE Latin1_General_100_BIN2` | No | Immutable provider-canonical key |
| `SubmissionOperationId` | `uniqueidentifier` | Yes | Immutable M5 operation owner; **required for new** claims, absent for verified legacy indexing |
| `EvidenceId` | `uniqueidentifier` | Yes | Null until proof commit; then permanently bound. Required for verified legacy indexing |
| `GameId` | `uniqueidentifier` | No | Immutable Game scope from trusted GameAttempt and/or operation |
| `AttemptId` | `uniqueidentifier` | No | Immutable attempt scope |
| `StepNumber` | `int` | No | Immutable step scope, >= 1 |
| `AuthenticatedPlayerUserId` | `nvarchar(256)` | No | Trusted M1/attempt player; not client asserted |
| `ClaimOrigin` | `tinyint` | No | `1=NewOperation`, `2=VerifiedLegacyEvidence` |
| `ClaimState` | `tinyint` | No | `0=Reserved`, `1=ProviderConsumed`, `2=EvidenceCommitted`, `3=CancelledUnusedTombstone`, `4=LegacyIndexed` |
| `ProviderConsumedAtUtc` | `datetimeoffset(7)` | Yes | Written only after trusted M4 confirmed consumption; legacy does not imply confirmed consumption |
| `EvidenceCommittedAtUtc` | `datetimeoffset(7)` | Yes | Set in same M5 SQL transaction as evidence/progress commit; null for legacy index |
| `CreatedAtUtc` | `datetimeoffset(7)` | No | SQL server UTC, immutable |
| `UpdatedAtUtc` | `datetimeoffset(7)` | No | SQL server UTC; monotonically updated on state transitions |
| `RowVersion` | `rowversion` | No | SQL concurrency token; DB generated |

**Deliberately not included:** raw image bytes, client-selected provider identity, untrusted URL, redundant reward balances, an independent copy of `ProviderReceiptId`, or a second media-key string. The stable receipt remains attached to the M5 operation **only after** M4 confirms it; M4 owns the authoritative original provider receipt.

**Rationale for preserving both `SubmissionOperationId` and `EvidenceId`:** a single immutable media claim can begin as a durable operation, then link to one evidence record, without allowing other operations to claim the same pair. Legacy verified evidence can be indexed without falsifying a nonexistent operation.

---

## 4. Proposed primary key, foreign keys and indexes

### Primary/uniqueness

| Logical name | Candidate shape | Why |
|---|---|---|
| `PK_GameProofMediaClaims` | PK `(MediaClaimId)` | Permanent claim identity |
| `UX_GameProofMediaClaims_Provider_Canonical` | **UNIQUE** `(MediaProviderNamespace, CanonicalMediaId)`; **not filtered by state** | Single authoritative global key, including reserved/consumed/cancelled/history |
| `UX_GameProofMediaClaims_Operation` | **UNIQUE FILTERED** `(SubmissionOperationId) WHERE SubmissionOperationId IS NOT NULL` | At most one media claim per M5 submission operation |
| `UX_GameProofMediaClaims_Evidence` | **UNIQUE FILTERED** `(EvidenceId) WHERE EvidenceId IS NOT NULL` | At most one claim linked to each evidence row |
| `IX_GameProofMediaClaims_Attempt` | `(AttemptId, StepNumber, ClaimState)` | Verified read and reconciliation path |
| `IX_GameProofMediaClaims_State` | `(ClaimState, UpdatedAtUtc)` | Supervised recovery/exception inventory, **not authorization for automatic recycling** |

**Existing M5 operation indexes remain defense-in-depth**, including unique provider/media and provider/capture. Separate indexes on operation and evidence tables *do not replace* the registry unique key.

### Proposed foreign keys / required principal keys

1. `(AttemptId, GameId, AuthenticatedPlayerUserId)` → `GameAttempts(Id, GameId, PlayerUserId)` using the already-described `AK_GameAttempts_Id_Game_Player`; use `NO ACTION`, no cascading deletion.
2. `SubmissionOperationId` → `GameProofSubmissionOperations.SubmissionOperationId` (nullable FK) **only once the operation table is approved/deployed**. To enforce matching media identity at the DB boundary, **preferred stronger option**: create an additional unique principal key `(SubmissionOperationId, MediaProviderNamespace, CanonicalMediaId)` in that operation table and have the claim's corresponding three columns reference it. This extra key/relationship itself requires DB Coordinator index-budget and EF model approval; alternatively enforce matching within approved stored procedures and a guarded immutable update policy. A simple FK on `SubmissionOperationId` does **not** prove media-key equality.
3. `EvidenceId` → `GameProofEvidence.Id` (nullable FK) plus an approved scoped verification. **Preferred stronger option:** add unique principal key `(Id, GameId, AttemptId, StepNumber)` in `GameProofEvidence`, and link registry `(EvidenceId, GameId, AttemptId, StepNumber)` to it. Legacy backfill must resolve trusted attempt-owner first. A simple `EvidenceId` FK does not establish matching game/attempt/step.
4. M4 `CaptureSessionId` and `HunterSubmissionId` stay in the M5 operation/provider-binding design, **not guessed new FK columns in this media registry**. Their physical linkage is pending M4 identity and compatibility decisions.

**SQL Server FK design caveat:** composite nullable FKs are not checked when the optional operation/evidence component is null; state/origin CHECK constraints below must prevent invalid null ownership. New candidate principal keys and existing FK compatibility need isolated database verification before sign-off. Keep delete behavior `NO ACTION` to preserve evidence and audit history.

### Proposed CHECK constraints

- `CK_GameProofMediaClaims_Identity`: `StepNumber >= 1`, provider/key/nonblank player after agreed normalization; disallow all-empty/whitespace input.
- `CK_GameProofMediaClaims_Origin`: `ClaimOrigin IN (1,2)`.
- `CK_GameProofMediaClaims_State`: `ClaimState IN (0,1,2,3,4)`.
- `CK_GameProofMediaClaims_NewOrLegacy`:
  - `NewOperation (1)` **requires** nonnull `SubmissionOperationId` and `ClaimState IN (0,1,2,3)`;
  - `VerifiedLegacyEvidence (2)` requires `SubmissionOperationId IS NULL`, `EvidenceId IS NOT NULL`, `ClaimState=4`.
- `CK_GameProofMediaClaims_StateBindings`:
  - `Reserved (0)`: evidence/consumption/commit timestamps all null;
  - `ProviderConsumed (1)`: `ProviderConsumedAtUtc` nonnull, evidence/commit null;
  - `EvidenceCommitted (2)`: consumption time, evidence and commit time all nonnull;
  - `CancelledUnusedTombstone (3)`: no evidence or consumption/commit timestamps;
  - `LegacyIndexed (4)`: evidence nonnull; provider-consumption and new-commit timestamps null, because past provider consumption is **not inferred**.
- `CK_GameProofMediaClaims_Time`: `UpdatedAtUtc >= CreatedAtUtc`; where set, `ProviderConsumedAtUtc >= CreatedAtUtc` and `EvidenceCommittedAtUtc >= ProviderConsumedAtUtc`.

**DB-level immutability limitation:** `CHECK` constraints alone cannot stop changing a persisted provider/key or owner to a different valid value. Final physical design must include **controlled update permissions/stored procedures and/or a reviewed database trigger** that rejects changes to key, Game/Attempt/Step/player, origin and operation owner, and only permits `EvidenceId` to transition from `NULL` to its first matching value. The unique key alone does not ensure ownership immutability. Do not enable until this is established and tested.

---

## 5. Claim lifecycle, retries, release and retention

| Current state | Event | Allowed next state | Required source and behavior |
|---|---|---|---|
| none | first approved operation registration | `Reserved` | In same M5 SQL transaction: immutable operation + registry; unique provider/key collision fails closed |
| `Reserved` | same operation, exact same binding | `Reserved` | Idempotent read of existing claim, not a second insert or new consumption |
| `Reserved` | conflicting operation/key/attempt/step/player/capture | none (reject) | Explicit conflict; do not consume M4 capture |
| `Reserved` | M4 outcome `Unknown` or SQL commit ambiguous | `Reserved` (no change) | M5 operation owns `ProviderOutcomeUnknown`; call M4 outcome lookup only, **never blind consume retry** |
| `Reserved` | M4 `Consumed` with stable bound receipt | `ProviderConsumed` | M5 validates operation, receipt, claim/lease fencing; M4 consumption already durably recorded in **M4 transaction** |
| `Reserved` | M4 terminal definite not-consumed rejection / cancellation | `CancelledUnusedTombstone` **only if proven not consumed** | Never automatically delete/reassign the provider/key; record failed operation outcome with reason |
| `ProviderConsumed` | M5 evidence/progress commit succeeds | `EvidenceCommitted` | One M5 SQL transaction updates evidence, progress, operation and registry; same receipt and binding |
| `ProviderConsumed` | M5 crash/ambiguous commit | `ProviderConsumed` until verified otherwise | Read M5 op/claim/evidence; if unknown, keep nonterminal and reconcile, no second provider consume |
| `EvidenceCommitted` | identical retry | `EvidenceCommitted` | Return originally persisted `EvidenceId`, stage result and receipt; no duplicate stage advance |
| `CancelledUnusedTombstone` | any new operation attempts same media | **reject** | Permanent uniqueness tombstone; any administrative exception requires separate approved migration/policy, not runtime release |
| `LegacyIndexed` | any new operation attempts same verified historical media | **reject** | Historical proof linked, but M4 consumption proof is not implied |

**Release policy:** Only temporary *processing leases* may expire and be re-claimed under the existing M5 fencing rules. **A media identity claim is not automatically released on lease expiry, failed verification, rejected proof, game deletion, photo retake or player cancellation.** This strict tombstone policy intentionally sacrifices automatic reuse to prevent replay; DB Coordinator can separately approve explicit audited exceptions after source verification and privacy/retention review. No destructive routine cleanup is proposed.

**ClaimState is not a substitute for M5 `GameProofSubmissionOperationState` or M4 authoritative capture status.** Each has a different owner. Provider `Unknown` never grants M5 progress.

---

## 6. Transaction ownership, lock order and crash boundaries

### A. M5 registration (one M5 SQL transaction, no provider call)

1. Authenticate user via M1 trusted identity and validate active Game/Attempt/Step ownership.
2. Obtain **trusted** provider namespace and canonical media identity from the approved M4/provider contract; reject missing/ambiguous source.
3. Normalize strictly according to **M4-approved** canonicalization; never invent provider namespace.
4. Begin a **single M5 SQL transaction**. Acquire an agreed stable media-key lock **first**, e.g., transaction-owned `sp_getapplock` keyed by a SHA-256 encoding of the exact canonical pair (optional deadlock reduction; **not a substitute** for the unique index). Acquire attempt/progress row lock, immutable operation row lock/insert, then registry unique-key insert or exact-match read, consistently in this order.
5. If operation exists, confirm the *entire* original binding. If registry is missing for an existing operation, **fail closed and escalate**, not silently create a second owner. If media exists under another operation/evidence, reject. Both operation and registry persist or roll back together.
6. Commit. If COMMIT acknowledgement is lost, read the authoritative operation **and registry** on a fresh connection before deciding; missing/ambiguous state is **not proof of rollback**. Provider must not be called until durable registration/ownership is confirmed.

**Proposed isolation:** `SERIALIZABLE` for claim registration pending measured testing, plus database UNIQUE index and deterministic lock order. Concurrency must be verified with independent SQL connections, not only in-memory fakes.

### B. M4 provider single-use operation (separate M4-owned transaction)

1. M5 invokes proposed `IGameProofCaptureRecoveryProvider.VerifyAndConsumeOnceAsync(binding, ...)` **only after** durable registration and only if M4 has approved a Game-safe capture session type.
2. M4 verifies its own issuance purpose, HunterSubmission/Game purpose compatibility, authenticated player-to-capture identity, expiry, revocation, media ownership/identity, single-use and exact immutable operation binding.
3. **Within its own M4 SQL transaction**, atomically record consumption and stable `ProviderReceiptId` / operation outcome. Duplicate exact-operation calls must return the **same durable receipt**; conflicting operations reject.
4. M4 exposes `GetOperationOutcomeAsync(binding)` for fresh, stable lookup after a dropped response or process crash. `Unknown` is not `Consumed`; never blindly repeat consumption.
5. M4's table/receipt schema, trigger and provider adapter remain **unapproved**. The existing HunterSubmission-bound `TryConsumeAsync` does **not** currently satisfy this contract.

**Crucial:** No distributed transaction or single SQL transaction spanning M4 and M5 is claimed. Orchestration uses durable operation IDs, lookup and reconciliation to bridge independently committed transactions.

### C. M5 consumption confirmation, then evidence/progress finalization

1. With trusted M4 `Consumed` outcome + stable receipt, M5 uses existing claim-token/lease fencing (`TryRecordConfirmedConsumptionAsync` is a current isolated implementation). Store the receipt and move the registry to `ProviderConsumed` under an approved transaction/commit recovery design. Confirm claimed operation and media pair have not changed.
2. **Separate finalization transaction:** acquire the same consistent canonical-key lock, then lock attempt/progress and operation, then registry; ensure game is still eligible, step matches, claim is `ProviderConsumed`, M4 receipt is durable, and no evidence was already committed for that operation.
3. Insert `GameProofEvidence` with an approved provider/canonical association strategy, apply stage progress and, **when applicable**, completion/result update, link `EvidenceId` in registry and operation, and set registry `EvidenceCommitted`, operation `Completed` in the **same M5 SQL transaction**. Owner must approve final-stage/decision boundary (existing SQL review implementation explicitly defers final-stage completion).
4. On conflict or uncertain SQL commit, read operation + registry + evidence + attempt progress to verify a single consistent committed result; **do not re-consume M4**. If inconsistent or incomplete, hold for supervised reconciliation.

**Required cutover rule:** Every writer of Game proof evidence (including legacy `SqlGameProofSubmissionRepository`, a future API path, recovery worker and any Admin/testing integration) must use the same registry authority. Enforce fail-closed DI/write routing before activation. A mixed period where a writer bypasses the registry **invalidates the global uniqueness guarantee**.

### Lock order and deadlock validation

All M5 registration/finalization paths must document the **same** stable order: normalized key lock → authoritative attempt/progress → operation → registry/evidence writes (principal rows inserted before FK dependents). Exact SQL statement ordering must be approved and tested: operation and registry may need specific FK-safe insert ordering; unique constraint remains the final authority even when app locks cannot be acquired. Retry deadlocks only with identical durable operation and exact binding, never with a new media or blind provider consume. M4 has its **own** capture-session/receipt lock order and does not hold its transaction during M5 SQL activity.

---

## 7. Historical READ-ONLY audit, ambiguity classification and backfill

### 7.1 Approved current activity: read-only inventory only

Perform on an **authorized read-only connection or database snapshot**; no DDL or DML, no table locks held for mutation. Preserve privacy: prefer aggregated counts and controlled incident IDs, do not export raw proof media URLs/IDs to shared chat. Do not assume default provider or delete/deduplicate history.

Suggested **read-only** query templates for DB Coordinator authorization/review:

```sql
-- Read-only inventory of raw legacy M5 evidence.
SELECT
    COUNT_BIG(*) AS TotalEvidenceRows,
    SUM(CASE WHEN MediaId IS NULL OR LEN(LTRIM(RTRIM(MediaId))) = 0 THEN 1 ELSE 0 END) AS BlankMediaIdRows,
    COUNT(DISTINCT AttemptId) AS DistinctAttempts
FROM dbo.GameProofEvidence;

-- Raw-string duplicate candidates only: NOT provider-canonical duplicate proof.
SELECT TOP (100)
    COUNT_BIG(*) AS RawMediaIdOccurrences,
    COUNT(DISTINCT AttemptId) AS AttemptCount,
    COUNT(DISTINCT GameId) AS GameCount
FROM dbo.GameProofEvidence
WHERE MediaId IS NOT NULL
  AND LEN(LTRIM(RTRIM(MediaId))) > 0
GROUP BY MediaId COLLATE Latin1_General_100_BIN2
HAVING COUNT_BIG(*) > 1
ORDER BY RawMediaIdOccurrences DESC;
```

**Do not treat duplicate-count alone as a complete audit:** the second query hides raw IDs to avoid unsolicited exposure. Authorized internal review may separately correlate evidence row IDs with trusted provider asset metadata, consent, attempt ownership and M4 classification. SQL collation here is a **candidate for identifying byte-sensitive raw duplicates**, not approval of M4 canonicalization.

### 7.2 Required classification output (no writes)

| Category | Meaning | Proposed action |
|---|---|---|
| A — Verified unique | Trusted provider namespace + canonical ID, no other historical or proposed operation owner | Candidate for **future approved** `LegacyIndexed` backfill |
| B — Verified collision | Two or more legacy evidence rows map to same trustworthy canonical pair | Report full evidence IDs securely; policy/manual remediation decision required, no silent winner |
| C — Missing provider | Only legacy raw `MediaId`; no trustworthy namespace | **Unresolved exception**; do not infer namespace or insert claim |
| D — Ambiguous alias/equivalence | Provider gives multiple possible canonical IDs, changing normalization or IDs shared across providers | M4 source confirmation; remain blocked |
| E — Invalid/missing media | Blank, missing, deleted asset or unknown ownership | Preserve historical evidence/audit; exception and privacy review |
| F — Cross-table conflict | Verified historical pair matches isolated/new operation pair | Report both IDs; no automatic claim override |

**Additional discrepancy checks:** orphan evidence-to-attempt references, wrong Game/Step, rejected-versus-accepted evidence, timestamps, account deletion, capture ownership and legacy retention. Existing FK constraints help but do not replace source-of-truth verification.

### 7.3 Future backfill and duplicate resolution — approval gates

1. Produce a reviewed **read-only** count, category report, authorized provenance mapping and deterministic canonicalization version, with no mass data exfiltration.
2. M4 signs off real provider source and equivalence rules. DB Coordinator chooses whether historical unknowns can be resolved, migrated in batches or must remain fail-closed with explicit access restrictions.
3. Prepare **separate** staged migration/backfill plan: isolated copy → deterministic candidate row mapping → manual resolution of collisions → dry-run counts → FK/index verification → transaction/backups/rollback plan → explicit 7-member/DB approvals where required.
4. Apply **no** backfill `INSERT`, `UPDATE`, `DELETE`, migration or unique-index build until separately authorized. Never rewrite old `MediaId` or invent operation/capture IDs; `LegacyIndexed` records use `SubmissionOperationId=NULL` and retain true `EvidenceId`.
5. Before production claim activation, resolve or safely fence all unclassified legacy conflicts under an approved policy and ensure **zero bypass writers**. A registry containing only new claims while old evidence remains unclassified must **not** be advertised as protecting global historical uniqueness.

**Rollback:** an unapproved or partially migrated registry must never be silently wiped to permit media reuse; preserve audit/collision results and follow a separately approved rollback plan that maintains claim integrity. Owner decides whether activation stays blocked when legacy ambiguity cannot be resolved by submission day.

---

## 8. Concurrency, failure and crash-recovery acceptance matrix

All SQL executions require a *separate explicit isolated test authorization*; this table itself is a test plan. Fake-provider/validator/metadata tests may be written code-only now.

| ID | Scenario | Required outcome | Owner / verification |
|---|---|---|---|
| REG-01 | One valid operation registers first key | One operation + one `Reserved` claim, atomic | M5 / isolated SQL |
| REG-02 | Same operation and exact immutable binding retries | Original claim returned, no new row | M5 / isolated SQL |
| REG-03 | Same operation with changed player/Game/Attempt/Step/capture/media | Conflict, no reassignment | M5 / unit + SQL |
| REG-04 | Different operations race for same provider/canonical pair | Exactly one durable owner; other rejected | M5 / **separate connections** |
| REG-05 | Different games/attempts use same pair | Global rejection regardless of game | M5 / SQL |
| REG-06 | Same canonical ID across genuinely separate providers | Distinct only under **M4-approved** namespace semantics | M4 + M5 / SQL |
| REG-07 | Alias/case/Unicode/whitespace variations | Exactly the equality rules formally approved by M4 | M4 + DB / tests |
| REG-08 | Operation insert succeeds then registry insert fails | Transaction rolls back both (or verified durable pair); no orphan registration | M5 / injected failure |
| REG-09 | Registration COMMIT response lost | Fresh read verifies both operation and claim; `Unknown` stays held | M5 / fault injection |
| REG-10 | Old proof writer attempts evidence without claim | Fail closed; no bypass write | M5 / DI+SQL tests |
| REG-11 | Historical evidence pair matches new operation | New claim rejected after verified backfill; never dual ownership | DB + M5 / isolated |
| REG-12 | Historical raw `MediaId` missing provider | Report as unresolved; no guessed canonical key | M4 + DB / read-only audit |
| REG-13 | Legacy provider-backed duplicates | Explicit conflict inventory; no automatic delete/rewrite | DB / read-only audit |
| M4-01 | Same capture exact operation repeated | Same durable receipt, one consumption | M4 / isolated provider SQL |
| M4-02 | Two operations race for one capture | Exactly one owner, competing operation rejected | M4 / SQL concurrency |
| M4-03 | M4 consumed, HTTP/response lost | Lookup returns original `Consumed` receipt; no second consumption | M4 / crash injection |
| M4-04 | Unknown/unavailable provider lookup | M5 remains nonterminal, **no blind retry** | M4 + M5 / fake + SQL |
| M4-05 | M4 HunterSubmission lifecycle incompatible with Game proof | Reject and require approved separate Game session type | M4 / contract + tests |
| COM-01 | Consumed receipt confirmed, SQL finalization crashes | Claim and operation recover; one eventual evidence/progress result | M5 / SQL crash |
| COM-02 | Two finalizers with stale/different lease token | Only current holder may mutate; stale completion rejected | M5 / SQL fencing |
| COM-03 | Duplicate finalization exact operation | Same evidence ID/unchanged progress; no duplicate step advance | M5 / SQL |
| COM-04 | Finalization COMMIT acknowledgement lost | Fresh read proves consistent claim+op+evidence+progress or holds for review | M5 / SQL fault injection |
| COM-05 | Wrong stage or attempt/Game mismatch | Reject; no evidence/progress mutation | M5 / SQL |
| COM-06 | Expired/disabled Game or attempt deadline edge | No unauthorized finalization or credit; follow approved policy | M5 / SQL |
| COM-07 | Final stage becomes pending Admin result | Atomic stage result according to separate final-stage policy; no premature winner/reward | M5/M6 / SQL |
| OWN-01 | Key/owner columns modified after registration | Immutable update prohibited by DB guard | DB + M5 / SQL metadata & write tests |
| OWN-02 | Evidence linked to different attempt/step | Composite FK or approved transaction guard rejects | DB + M5 / SQL |
| OWN-03 | Cancelled/rejected/revoked proof attempts to free media | Tombstone/claim remains; replay rejected | M5 / SQL |
| PERF-01 | Different keys, high parallel claim rate | No incorrect collisions; bounded deadlocks/acceptable lock waits | DB + M5 / load tests |

**Minimum acceptance evidence:** SQL schema inspection (actual PK/FK/index/collations/CHECKs), independent-connection races, deterministic crash injection, provider receipt durability, historical-audit counts, backup/rollback plan, comprehensive `dotnet test` logs and zero unauthorized runtime wiring. A green unit test does **not** prove provider or shared production correctness.

---

## 9. Proposed staged implementation plan and approvals

### Permitted immediately — code-only, no shared DbContext/migration changes

- `GameProofMediaClaim` **proposed model** and validator for lengths, binding, origin/state transitions, tombstone behavior and consistency.
- Code-only EF configuration proposal placed outside active shared model registration, with metadata tests using a **test-only** model builder; no runtime use or migration generation.
- Fake-provider tests for exact retry, conflicting binding and `Unknown`→lookup-only behavior.
- Read-only legacy audit plan and report template; if running an actual audit, use an explicitly authorized read-only SQL connection.
- Draft one coordinator-reviewed interface between `IGameProofSubmissionOperationStore` and registry claim state; do not change production DI.

### Held until specific approval

- M4 confirms namespace/canonicalization and Game-purpose capture compatibility or supplies a separate Game-proof provider/session contract.
- DB Coordinator approves final physical columns/types, composite keys/FKs, indexes, collation, state/immutability guards, backfill SQL and migration sequence.
- Team/DB Coordinator separately authorizes shared EF migration, controlled historical write/backfill, SQL test environment and production activation.
- M5 submits full isolated SQL/test evidence and integration risk assessment for a **new** final go/no-go decision.

### Explicit stop conditions

- Historical namespace unverified; canonical IDs ambiguous; provider returns `Unknown` without durable reconciliation; any legacy writer bypasses the registry; wrong-op receipt; M4 HunterSubmission lifecycle incompatible; no atomic claim+operation or evidence+progress transaction; physical schema unsigned; unauthorized SQL execution.

**Until all gates close: PR #256 stays OPEN / REVIEW ONLY — DO NOT MERGE. No production recovery worker or provider adapter, no shared migration, no historical DML/DDL.**

---

## 10. Sign-off questions (please respond separately)

**M4 Capture Owner**

1. Exact approved `MediaProviderNamespace` (format, normalization, aliases, versioning) and the trusted source of it?
2. Exact `CanonicalMediaId` algorithm, equivalence/collation (case, Unicode, filename/URL variants), immutable asset identity and legacy lookup mechanism?
3. Can existing HunterSubmission-linked capture sessions serve a Game/Attempt/Step proof **without violating HunterSubmission state rules**? If not, approve separate purpose-scoped Game capture sessions/provider contract.
4. Can M4 durably bind `SubmissionOperationId` + identity/Game/Attempt/Step/CaptureSession/HunterSubmission/media, consume once, return stable receipt and look up an exact outcome after a crash in M4-owned SQL transaction?

**DB Coordinator / Kaviyan**

1. Approve/modify the exact **16-column** proposed registry shape, state numbers and `BIN2` candidate collation; any additional FK columns or auditing requirements?
2. Approve preferred composite FK links versus guarded transaction alternatives; verify that SQL Server keys and existing migrations support them.
3. Which DB mechanism will enforce immutable owner/media keys and one-time `EvidenceId` linkage (guarded proc, role privileges, trigger)?
4. Approve `CancelledUnusedTombstone` permanent key retention (no automatic media release) and the retention/privacy exception procedure?
5. Confirm lock order, isolation, registration transaction, separately committed M4 provider receipt and M5 evidence/progress completion boundary.
6. Approve read-only legacy inventory fields, provenance sources and exception categories. Specify required manual duplicate-resolution and backfill gate; **no write authorization implied**.
7. Authorize an isolated SQL test plan **separately** after final schema/contract decisions; confirm migration order, PR #256 review status and production fail-closed activation plan.

**M5 Owner**

- No adapter or migration until sign-off. Implement only the approved code-only model/validator/metadata/fake-provider-test package and continue coordinating with M4 for canonicalization and final compatibility.

---

## 11. Source references (inspected; proposal distinct from current code)

- [`GamePersistenceModels.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Models/Games/GamePersistenceModels.cs)
- [`GamePersistenceConfigurations.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Configurations/Games/GamePersistenceConfigurations.cs)
- [`SqlGameProofSubmissionRepository.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameProofSubmissionRepository.cs)
- [`GameProofSubmissionOperationRowConfiguration.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Configurations/Games/GameProofSubmissionOperationRowConfiguration.cs)
- [`GameProofSubmissionOperationBinding.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Application/Modules/Games/Dtos/GameProofSubmissionOperationBinding.cs)
- [`SqlGameProofSubmissionOperationStore.Register.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameProofSubmissionOperationStore.Register.cs)
- [`IGameProofCaptureRecoveryProvider.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Application/Modules/Games/Interfaces/IGameProofCaptureRecoveryProvider.cs)
- [`HunterCaptureSessionRowConfiguration.cs`](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Configurations/HunterPlaces/HunterCaptureSessionRowConfiguration.cs)

**This document proposes an architecture; it records no approved deployment, verified registry SQL behavior, or new code commit.**
