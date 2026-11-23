# WAYHUNTER — M5 Global Media-Claim Registry: Detailed Design v3

**Owner:** Thuvarathan — M5 Games
**Reviewers:** Kaviyan — Team Leader / DB Coordinator; M4 Capture Owner for provider identity/compatibility
**Date:** 2026-11-21
**PR:** [#256](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/256) — `task/m5-idempotent-proof-recovery`
**Predecessor:** `docs/architecture/M5_GLOBAL_MEDIA_CLAIM_REGISTRY_FINAL_DESIGN_REVIEW_v2.md`, commit `d4c776c620619218212469f6c4acf4b0a6e04c87`
**Decision received:** **ARCHITECTURE ACCEPTED — PHYSICAL SCHEMA APPROVAL PENDING CORRECTIONS**
**Status of this document:** **DESIGN v3 PROPOSAL FOR REVIEW, NOT IMPLEMENTED OR APPROVED PHYSICAL DDL**
**M4 dependency:** **HOLD — capture compatibility, trusted provider namespace and canonicalization unconfirmed.**

**Authorization boundary:** This is documentation only. Code-only domain models, validators, unregistered/test-only EF metadata and fake-provider tests are permitted. No registry SQL execution (without separate test authorization), shared migration, production DDL, historical DML/backfill, provider adapter/worker activation or PR #256 merge is authorized.

---

## 1. Revision summary and factual baseline

| DB Coordinator v3 correction | Resolution proposed here | Sections |
|---|---|---|
| Atomic operation registration + registry reservation | One transaction, one SQL connection, all-or-nothing commit, fresh-connection reconciliation on unknown commit | 5.1, 6 |
| Consistent consumed receipt and claim transitions | M4 commit separately; M5 updates operation receipt + registry state together; M5 evidence/progress finalization is another atomic transaction | 5.2–5.4 |
| Media-key-first versus existing Game locks | **Game row first**, then Attempt/Progress, Operation, Registry, Evidence; unique key handles cross-Game collision; no media-key-first `sp_getapplock` | 6 |
| Composite operation/evidence FKs | Complete principal/foreign-key shapes, including nullable legacy handling, with supporting unique keys | 3.2–3.4 |
| SQL immutability, permissions, complete checks | Explicit candidate CHECK SQL and restricted-writer/trigger proposal, including evidence-link one-time transition and delete denial | 4 |
| Unresolved historical provenance fence | Fail-closed global cutover gate until verified classification or explicitly approved safe disjoint partition; no inferred namespace | 7 |
| M4 final provider decision | Named **PENDING / HOLD** fields; do not invent namespace, equivalence algorithm or reuse of HunterSubmission capture sessions | 2, 10 |

### Verified source facts (not new v3 implementation)

- Existing `dbo.GameProofEvidence` uses `GameProofEvidenceRow` with `Id`, `GameId`, `AttemptId`, `StepNumber`, raw `MediaId` (max 256) and `SubmittedAtUtc`. It has no provider namespace or global media-key unique index.
- `SqlGameProofSubmissionRepository` locks `dbo.Games` with `UPDLOCK, HOLDLOCK` before proof writes; `SqlGameProofReviewRepository` and `SqlGameAttemptRepository` also acquire a Game lock early in their SQL transaction. Their old write path is **not** registry-aware.
- The *isolated proposed* `GameProofSubmissionOperationRow` and `SqlGameProofSubmissionOperationStore` currently register operations separately from any registry. `TryRecordConfirmedConsumptionAsync` updates operation receipt/state but not a registry row. These methods must be **refactored after authorization** to satisfy the proposed v3 boundaries; do not claim atomic registry integration today.
- The proposed M5 `IGameProofCaptureRecoveryProvider` offers `VerifyAndConsumeOnceAsync` and `GetOperationOutcomeAsync`, but no approved production M4 Game-proof provider exists. Existing M4 capture session is tied to `HunterSubmissionId`; Game-proof compatibility remains on HOLD.
- Prior isolated M5 SQL tests are accepted evidence **for their actual scope only**. They do not exercise this proposed registry or prove M4 receipt durability.

---

## 2. Trusted key and M4-owned input contract — final confirmation pending

The authoritative global uniqueness key is **non-filtered UNIQUE (`MediaProviderNamespace`, `CanonicalMediaId`)**.

| Field | Proposed SQL representation | Required trusted provenance |
|---|---|---|
| `MediaProviderNamespace` | `nvarchar(64)` with candidate `Latin1_General_100_BIN2` | M4-approved server-side provider identity, namespace/alias/version mapping; never directly accepted from Flutter |
| `CanonicalMediaId` | `nvarchar(256)` with same candidate BIN2 collation | M4-approved immutable asset identifier, canonicalization algorithm and equivalence policy; never a raw URL without trusted validation |
| `AuthenticatedPlayerUserId` | `nvarchar(256)` | M1 authenticated account/session + GameAttempt owner, not client-provided identifier |
| `CaptureSessionId` | Existing operation record `nvarchar(256)` | M4-owned validated purpose-scoped capture session; M4 may require a new Game-proof session provider |

**Not decided by M5:** exact namespace string(s), case/Unicode normalization, path/URL equivalence, whether aliases collapse, canonicalization version, asset replacement/deletion rules, Game-purpose capture compatibility, HunterSubmissionId mapping and trusted M1-to-M4 account identity. Only M4 + DB Coordinator can settle these. The SQL collation choice is a **candidate**, not proof that provider canonicalization is settled.

No mixed raw-versus-canonical comparisons and no fabricated legacy namespace. If M4 cannot prove that legacy records and new provider keys are comparable, the production cutover remains blocked (§7).

---

## 3. Exact proposed SQL shape, keys and foreign-key plan

### 3.1 `dbo.GameProofMediaClaims` — 16 proposed columns

All columns and lengths below are **proposals for approval**. Times are server UTC. `ProviderConsumedAtUtc` means M5 durably recorded the provider's confirmed consumption; it is not necessarily the original M4 event timestamp.

| Column | SQL type | Nullable | Rule |
|---|---|---|---|
| `MediaClaimId` | `uniqueidentifier` | No | Server-assigned permanent PK |
| `MediaProviderNamespace` | `nvarchar(64) COLLATE Latin1_General_100_BIN2` | No | Immutable trusted provider identity |
| `CanonicalMediaId` | `nvarchar(256) COLLATE Latin1_General_100_BIN2` | No | Immutable canonical asset identity |
| `SubmissionOperationId` | `uniqueidentifier` | Yes | Non-null for new operations; null for verified legacy indexing |
| `EvidenceId` | `uniqueidentifier` | Yes | Null before evidence commit; one-time link, or required for verified legacy |
| `GameId` | `uniqueidentifier` | No | Immutable Game owner |
| `AttemptId` | `uniqueidentifier` | No | Immutable attempt owner |
| `StepNumber` | `int` | No | Immutable positive step |
| `AuthenticatedPlayerUserId` | `nvarchar(256)` | No | Immutable trusted M1 player |
| `ClaimOrigin` | `tinyint` | No | `1=NewOperation`, `2=VerifiedLegacyEvidence` |
| `ClaimState` | `tinyint` | No | `0=Reserved`, `1=ProviderConsumed`, `2=EvidenceCommitted`, `3=CancelledUnusedTombstone`, `4=LegacyIndexed` |
| `ProviderConsumedAtUtc` | `datetimeoffset(7)` | Yes | Recorded with trusted receipt on M5 operation, state `1` or `2` |
| `EvidenceCommittedAtUtc` | `datetimeoffset(7)` | Yes | Recorded atomically with evidence/progress, state `2` |
| `CreatedAtUtc` | `datetimeoffset(7)` | No | Immutable creation instant |
| `UpdatedAtUtc` | `datetimeoffset(7)` | No | Updated on every state mutation; must not precede creation |
| `RowVersion` | `rowversion` | No | DB-generated concurrency token |

**No receipt duplication:** `ProviderReceiptId` remains on the M5 operation, and the M4 receipt is authoritative in the M4-owned provider database. In the same M5 transaction that sets registry `ProviderConsumed`, persist the operation `ProviderReceiptId`, `ProviderConsumptionConfirmedAtUtc`, and state. Registry `ProviderConsumed` without an operation receipt is an invalid **cross-table state** to detect/deny by controlled writing and reconciliation; a CHECK constraint on one table cannot validate another table.

### 3.2 Primary and alternate keys / indexes

```text
PK_GameProofMediaClaims
  PRIMARY KEY (MediaClaimId)

UX_GameProofMediaClaims_Provider_Canonical
  UNIQUE NONFILTERED (MediaProviderNamespace, CanonicalMediaId)

UX_GameProofMediaClaims_Operation
  UNIQUE FILTERED (SubmissionOperationId) WHERE SubmissionOperationId IS NOT NULL

UX_GameProofMediaClaims_Evidence
  UNIQUE FILTERED (EvidenceId) WHERE EvidenceId IS NOT NULL

IX_GameProofMediaClaims_Attempt
  (AttemptId, StepNumber, ClaimState)

IX_GameProofMediaClaims_Reconciliation
  (ClaimState, UpdatedAtUtc)
```

The global provider/canonical index is **never filtered by state**, including terminal tombstones and legacy-indexed claims. Index maintenance/retention cannot free media for replay.

### 3.3 Preferred composite foreign keys and exact principal keys

| Child columns → parent principal | Supporting principal key | Reason |
|---|---|---|
| Claim `(AttemptId, GameId, AuthenticatedPlayerUserId)` → `GameAttempts(Id, GameId, PlayerUserId)` | Existing `AK_GameAttempts_Id_Game_Player` (verify physical migration before approval) | Claim must belong to the exact authenticated attempt/player |
| Claim `(SubmissionOperationId, GameId, AttemptId, AuthenticatedPlayerUserId, StepNumber, MediaProviderNamespace, CanonicalMediaId)` → `GameProofSubmissionOperations` with identical columns | **New candidate alternate key** `AK_GameProofOperations_ImmutableClaimOwner` over the 7 listed operation columns | Locks a new claim to the precise operation, player, step and provider asset pair |
| Claim `(EvidenceId, GameId, AttemptId, StepNumber)` → `GameProofEvidence(Id, GameId, AttemptId, StepNumber)` | **New candidate alternate key** `AK_GameProofEvidence_Id_Game_Attempt_Step` | A linked evidence row cannot belong to another Game/Attempt/Step |

All FKs: **NO ACTION** delete/update; no cascade of audit or proof claims. A composite SQL Server FK with nullable `SubmissionOperationId` or `EvidenceId` is deliberately non-enforcing while that column is null: the `CHECK` constraints below restrict which origin/state may use null. Legacy `SubmissionOperationId=NULL` is allowed only for trusted historical indexing; it never masquerades as a real M5 operation.

**Required preflight:** SQL Server index-key-width and collation compatibility, existing physical alternate-key presence, EF Core principal/foreign-key metadata, M1 `PlayerUserId` datatype/length and physical migration snapshot verification. Candidate combined operation key is large: verify against SQL Server nonclustered index byte-size limits and actual data lengths before approval; if the DB Coordinator rejects it, use a smaller unique `(SubmissionOperationId, MediaProviderNamespace, CanonicalMediaId)` principal key **plus database-enforced, trusted ownership checks** rather than an unenforced application assumption. No migration or index creation is approved by this proposal.

### 3.4 Evidence writer binding: no silent bypass

`GameProofEvidence` currently has no `MediaClaimId` or provider/canonical columns. v3 deliberately does **not** pretend a new FK from every existing evidence row to the registry can be deployed without classifying history. Preferred initial release enforcement is:

1. New-operation claims reference the immutable operation by the 7-column composite FK.
2. Once evidence is inserted in a controlled finalization transaction, the claim's composite `EvidenceId` FK proves the same Game/Attempt/Step; operation `EvidenceId` and claim `EvidenceId` are also made equal in that transaction.
3. Only the reviewed registry-aware SQL writer may insert new evidence after cutover; deny the production application role direct table DML, and remove/disable all legacy proof-write DI routes. A reviewed INSERT trigger or additional `GameProofEvidence` claim linkage can provide defense-in-depth after DB approval, **but neither exists now**.
4. Same `EvidenceId` may not be attached to two claims (filtered unique index). Historical rows remain untouched and are linked only after verified provenance classification and separate backfill approval.

This is a **controlled-writer design**, not an assertion that a composite FK alone prevents privileged direct SQL writes. The DB Coordinator must approve the final privilege/trigger or extended evidence-FK design before activation.

---

## 4. Complete SQL CHECK candidates and database immutability controls

All expressions below are candidate contents of separately named SQL Server CHECK constraints for the proposed registry. They are **illustrative definitions, NOT commands to execute**. The M4-approved canonical format may require more restrictions; do not invent them here.

```sql
-- CK_GameProofMediaClaims_IdsAndStep
[MediaClaimId] <> '00000000-0000-0000-0000-000000000000'
AND [GameId] <> '00000000-0000-0000-0000-000000000000'
AND [AttemptId] <> '00000000-0000-0000-0000-000000000000'
AND [StepNumber] >= 1
AND ([SubmissionOperationId] IS NULL OR
     [SubmissionOperationId] <> '00000000-0000-0000-0000-000000000000')
AND ([EvidenceId] IS NULL OR
     [EvidenceId] <> '00000000-0000-0000-0000-000000000000')

-- CK_GameProofMediaClaims_IdentityNonblank
-- Key values use candidate BIN2 collation (actual provider normalization still pending).
DATALENGTH([MediaProviderNamespace]) > 0
AND DATALENGTH([CanonicalMediaId]) > 0
AND DATALENGTH([AuthenticatedPlayerUserId]) > 0
AND LEN(LTRIM(RTRIM([MediaProviderNamespace]))) > 0
AND LEN(LTRIM(RTRIM([CanonicalMediaId]))) > 0
AND LEN(LTRIM(RTRIM([AuthenticatedPlayerUserId]))) > 0

-- CK_GameProofMediaClaims_OriginState
[ClaimOrigin] IN (1, 2)
AND [ClaimState] IN (0, 1, 2, 3, 4)
AND (
  ([ClaimOrigin] = 1 AND [SubmissionOperationId] IS NOT NULL
     AND [ClaimState] IN (0, 1, 2, 3))
  OR
  ([ClaimOrigin] = 2 AND [SubmissionOperationId] IS NULL
     AND [ClaimState] = 4 AND [EvidenceId] IS NOT NULL)
)

-- CK_GameProofMediaClaims_StateFields
(
  ([ClaimState] = 0 AND [EvidenceId] IS NULL
     AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL)
  OR
  ([ClaimState] = 1 AND [EvidenceId] IS NULL
     AND [ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] IS NULL)
  OR
  ([ClaimState] = 2 AND [EvidenceId] IS NOT NULL
     AND [ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] IS NOT NULL)
  OR
  ([ClaimState] = 3 AND [EvidenceId] IS NULL
     AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL)
  OR
  ([ClaimState] = 4 AND [EvidenceId] IS NOT NULL
     AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL)
)

-- CK_GameProofMediaClaims_TimeOrder
[UpdatedAtUtc] >= [CreatedAtUtc]
AND ([ProviderConsumedAtUtc] IS NULL
     OR [ProviderConsumedAtUtc] >= [CreatedAtUtc])
AND ([EvidenceCommittedAtUtc] IS NULL
     OR ([ProviderConsumedAtUtc] IS NOT NULL
       AND [EvidenceCommittedAtUtc] >= [ProviderConsumedAtUtc]))
AND ([EvidenceCommittedAtUtc] IS NULL
     OR [UpdatedAtUtc] >= [EvidenceCommittedAtUtc])
```

**Invariant limitation:** SQL CHECKs are row-local and cannot enforce immutable *changes*, forbidden state regressions, operation receipt matching, or the historical-source fact that a canonical ID came from M4. Therefore the physical design requires **both**:

- **Minimum privileges:** application identity gets `EXECUTE` only on vetted registration/confirmation/finalization/reconciliation procedures; no direct `INSERT`, `UPDATE`, `DELETE` to registry, operation or Game evidence tables; separate restricted migration/audit roles; prohibit privileged production bypass in runtime connection strings. DDL owners remain DB Coordinator-controlled.
- **Database immutable update/delete guard:** approved `AFTER UPDATE, DELETE` trigger or equally authoritative guarded stored procedures with direct DML denied. It must reject `DELETE` and any update to `MediaClaimId`, provider namespace, canonical media ID, GameId, AttemptId, StepNumber, authenticated player, origin, SubmissionOperationId or CreatedAtUtc; it must permit `EvidenceId` only from `NULL` to the first verified ID on the `1 -> 2` transition; it must reject every state regression, `4 -> *`, `3 -> *`, and any `2 -> *`, and block timestamps from being cleared or rewritten after confirmation. Compare immutable strings using a byte-equivalent representation as appropriate to M4 policy, not case-insensitive SQL equality. `RowVersion` is DB-generated; `UpdatedAtUtc` moves forward only. Guard must correctly handle multi-row UPDATE/DELETE statements.

**Authorized state transitions:** `0→0` exact retry (no write), `0→1` consumed, `0→3` definitely unconsumed terminal tombstone, `1→1` exact retry, `1→2` evidence commit, `2→2` exact read only, `3→3` and `4→4` read only. **No state reversion or automatic release**, even after a lease expires, game deletion, rejection or capture revocation.

**Cross-table consistency checks in vetted procedures:** update operation receipt and registry `1` together; set operation `Completed` plus `EvidenceId`, registry `2` plus the same `EvidenceId`, evidence insert and progress mutation together. Abort if the operation and registry are inconsistent. Validate `ProviderReceiptId` nonempty and bound to the exact M4 `SubmissionOperationId` before `0→1`; `Unknown` must never take `0→1`.

---

## 5. Atomic operations and ambiguous-commit recovery

### 5.1 TX-A — Operation registration + registry reservation (one M5 SQL transaction)

**Preflight outside transaction:** trusted M1 user, Game/Attempt/Step eligibility, M4-approved canonical identity source; fail closed if source ambiguous. Inputs must satisfy existing binding DTO lengths. M4 is **not** invoked to consume during this transaction.

1. Open **one M5 SQL connection**, begin one SQL transaction, use approved isolation (`SERIALIZABLE` candidate) and §6 lock sequence.
2. Lock `Games` (`UPDLOCK,HOLDLOCK`), verify current Game/Attempt ownership and eligibility, lock Attempt/Progress.
3. Lock/insert `GameProofSubmissionOperations` for the stable `SubmissionOperationId`. Exact existing binding → idempotent; any mismatch → reject. Register operation and claim as **one unit of work**, not by calling the current independent `RegisterOrReadAsync` and then a second commit.
4. Lock/read the registry by the exact canonical pair. If owned by another operation or legacy evidence → conflict. If existing exact operation claim → return it only after checking the entire binding. If a registered operation lacks its claim (legacy partial state) → **fail closed**, diagnose separately.
5. Insert the registry row `ClaimOrigin=1`, `ClaimState=0`, same operation identity and owner. The candidate composite FKs and **nonfiltered unique** provider/media index are the database's final authorities. Neither partial insert may commit alone.
6. `COMMIT`. Only after a successful commit or fresh-connection verification that both rows exist with matching immutable fields may the orchestrator consider invoking M4. The caller must not let an ambient EF transaction conflict with this transaction boundary.

**Injected failure:** operation INSERT succeeds but registry INSERT fails → **ROLLBACK both**. Unique conflict (including competing Games) → do not call M4. If COMMIT acknowledgement fails, dispose transaction, open a **new SQL connection**, read operation + registry using a consistent authoritative snapshot/transaction and verify matching binding/state; if missing, contradictory or inaccessible, mark outcome **unresolved / manual review** and do not consume. A missing row after an ambiguous COMMIT is **not** alone proof that consumption may be retried.

### 5.2 M4 TX-P — Provider owns atomic consumption and durable outcome receipt

After TX-A durable confirmation, M5 calls the approved purpose-scoped `VerifyAndConsumeOnceAsync(binding, nowUtc)`. M4 verifies actual capture purpose, owner, media ID, expiry, revocation, duplicate operation, and HunterSubmission/Game compatibility. Within **M4's own transaction** it consumes once and writes an immutable `SubmissionOperationId`-bound durable `ProviderReceiptId`. The exact same operation retry returns the original receipt; different binding or second operation rejects. M4 exposes `GetOperationOutcomeAsync(binding)` after a lost response. **`Unknown` → lookup only; never blindly consume again.**

**No cross-module transaction:** M5 TX-A, M4 TX-P, M5 TX-B, and M5 TX-C are distinct; no distributed atomicity claim.

### 5.3 TX-B — Confirmed M4 receipt persisted to M5 operation **and** registry

1. Read trusted `Consumed` outcome and immutable receipt from M4, verify operation identity and M4 purpose/binding. Maintain existing M5 claim-token/lease fencing.
2. Begin a **new single M5 SQL transaction**, acquire §6 Game → Attempt/Progress → Operation → Registry locks. Recheck exact binding, current claim token and unexpired lease under SQL server UTC, registry `Reserved`, and no existing conflicting receipt/evidence.
3. **Same transaction**: set M5 operation to `CaptureConsumed` (`3`), persist `ProviderReceiptId`, `ProviderConsumptionConfirmedAtUtc`, clear/fence lease consistently, and set registry `ProviderConsumed` (`1`) with `ProviderConsumedAtUtc` and `UpdatedAtUtc`. Keep the registry key/owner unchanged. If a retry sees existing `Consumed` + the same receipt and registry `1/2`, return the original result; a different receipt or only one persisted side fails closed.
4. `COMMIT`. If acknowledgement lost, on a **fresh connection** verify **both** operation + registry + receipt. If both durably agree, treat TX-B as confirmed; if neither/one/unknown, freeze and reconcile M4's authoritative outcome. Never call M4 consumption again merely because TX-B failed/appears absent.

**Required code change after approval:** the existing `TryRecordConfirmedConsumptionAsync` updates only the operation table using its own transaction. It **does not** implement TX-B until refactored with a registry-aware SQL unit of work. This document does not authorize or claim that refactor is deployed.

### 5.4 TX-C — Evidence + progress + operation completion + registry link (one M5 SQL transaction)

1. Begin M5 SQL transaction; lock Game first, Attempt/Progress next, then exact Operation and Registry using §6. Confirm M4 durable consumed receipt is already reflected by consistent TX-B state. Check Game/attempt/time/stage rules; reject stale lease/claim and unauthorized attempts.
2. If operation `Completed`, return the prior **persisted** EvidenceId/progress result only if registry `EvidenceCommitted` and evidence owner/step all agree. If any mismatch → fail closed.
3. Insert exactly one `GameProofEvidence` row for the current operation, preserving expected raw media information only under an **approved** canonical association policy; never assert old `MediaId` itself is canonical. Apply any accepted proof stage/progress transition and final-stage PendingAdmin result (if authorized by separately approved M5 completion rules). **Do not grant a winner/reward here.**
4. In the **same M5 SQL transaction**, set operation `Completed` with the exact new EvidenceId and registry `EvidenceCommitted` (`2`), `EvidenceId`, `EvidenceCommittedAtUtc`, `UpdatedAtUtc`. Validate operation + claim + evidence composite FKs and the one-evidence-per-operation rule. All writes commit or roll back together; progress must not advance separately.
5. On an ambiguous COMMIT: fresh connection read operation, registry, evidence and current stage/progress (including review/decision records where applicable). Return success **only** if a coherent single committed outcome exists; otherwise freeze/supervise reconciliation. M4 remains consumed; **never replay its consumption**.

**Gap:** Current `SqlGameProofSubmissionRepository` and `SqlGameProofReviewRepository` have separate write semantics and final-stage limitations. TX-C is a proposed unified operation-bound path, **not existing production behavior**. It must be implemented/tested under approved code and SQL integration gates before any activation.

### 5.5 Recovery decision table

| Durable observed state | Safe action |
|---|---|
| Neither operation nor registry after *known rollback* | Re-register only after proving no M4 invocation; new attempt requires complete eligibility review |
| Operation + registry both `Reserved`, provider never invoked/known not consumed | Continue controlled state machine under the original operation only |
| Provider `Unknown`, registry `Reserved`, operation `ProviderOutcomeUnknown` | **Lookup M4 only**, no blind consume; preserve claim/tombstone |
| M4 `Consumed` receipt, M5 TX-B unknown | Verify M5 op+claim; replay **TX-B persistence only** with current fencing if proven safe; never replay M4 consume |
| M5 op `CaptureConsumed` and registry `ProviderConsumed` | Attempt TX-C once under current lease and locks; no provider call |
| M5 operation `Completed`, registry `EvidenceCommitted`, evidence/progress consistent | Return exact existing outcome without a new write |
| Registry `1/2` but operation missing/receipt absent or evidence mismatch | **Manual review**, no automatic repair/reassignment |
| Rejected definite not-consumed operation + registry `Reserved` | Approved `0→3` permanent tombstone; never release to another operation |
| Unresolved historical identity/provenance | **Cutover blocked**, never invent provider namespace |

---

## 6. Lock order and concurrency ownership — replaces v2 media-first proposal

**Verified existing pattern:** SQL Game proof submission, review and attempt repositories obtain a `Games` row `WITH (UPDLOCK, HOLDLOCK)` early in transactions. A new *media-key-first* application lock before acquiring Game locks could invert ordering and create deadlocks against those writers.

**v3 canonical M5 lock sequence for TX-A/TX-B/TX-C and any coordinated legacy Game writer:**

```text
1. dbo.Games [Id = GameId]                 UPDLOCK, HOLDLOCK
2. dbo.GameAttempts [Id = AttemptId]       UPDLOCK, HOLDLOCK
3. dbo.GameAttemptProgress [AttemptId]     UPDLOCK, HOLDLOCK, if present
4. dbo.GameProofSubmissionOperations       UPDLOCK, HOLDLOCK on operation ID
5. dbo.GameProofMediaClaims                UPDLOCK, HOLDLOCK on pair/claim ID
6. dbo.GameProofEvidence and stage/result records (insert/update as required)
```

- **Do not** acquire `sp_getapplock` on media key ahead of Game. The nonfiltered UNIQUE `(MediaProviderNamespace, CanonicalMediaId)` resolves cross-Game races atomically; a second transaction gets a unique-key violation/conflict, not a second owner.
- Within a transaction, ensure principal rows exist before FK-dependent registry/evidence inserts. The lock sequence applies to locking **existing rows** and to the subsequent writes; a non-existent new operation cannot be locked before insertion. Atomicity and unique/FK constraints remain essential.
- **Different Games, same media:** each may hold its own Game lock, but the registry unique index arbitrates the shared provider key. Handle deadlocks by rolling back that M5 transaction and using the **same durable operation ID/binding** only after authoritative reconciliation; never generate a new provider consume call on retry.
- Any path that must lock multiple Games acquires Game IDs in a canonical total order (binary Guid order approved/tested), then downstream locks. M4 provider transaction uses its own capture-session/receipt order and is never held open during an M5 SQL transaction.
- If legacy proof writers cannot be converted to this ordering and registry enforcement, **disable them before cutover**. No mixed old/new write paths are allowed.
- Candidate isolation: `SERIALIZABLE` for TX-A and tested locking semantics for TX-B/TX-C (SERIALIZABLE or tightly validated row locks). SQL Server deadlock victim and optimistic concurrency failures are retryable **only for M5 local DB operations**, under preserved operation ID and fresh confirmation of provider outcome.

**Sign-off requirement:** DB Coordinator approves SQL statement-level lock order, contention/index plan, service-principal privileges and concurrency results from separately authorized isolated SQL tests.

---

## 7. Historical evidence audit, cutover/fencing and safe rollback

### 7.1 Approved now: read-only classification

Existing `GameProofEvidence.MediaId` is a raw ID without trusted provider namespace. **Never** assume a provider, rewrite history, infer past consumption or silently deduplicate. With a separately authorized read-only connection/snapshot, categorize:

| Category | Condition | Allowed disposition now |
|---|---|---|
| A | Provider + canonical identity and owner independently verified; unique pair | Future backfill candidate **only**, no current write |
| B | Multiple historical records map to one trusted pair | Secure collision report, DB/M4 manual decision |
| C | Missing trusted provider namespace | Unresolved provenance exception |
| D | Alias/equivalence/case ambiguity | M4 source-of-truth clarification |
| E | Invalid, blank, deleted media or ambiguous ownership | Audit exception, do not delete |
| F | Verified legacy pair conflicts with proposed/new operation | Collision report, no automatic winner |

For the read-only audit, collect **counts, case IDs, source-attribution status and affected Game/Attempt/Evidence IDs in a restricted report**, not public asset URLs. Prefer an approved read-only snapshot to avoid interfering with live writes. Read-only SQL templates from v2 remain proposal examples; actual execution requires authorized credentials and scope. Never run backfill DML merely because the audit is approved.

### 7.2 Exact pre-activation legacy fencing rule

**Gate H0 (default):** `RegistryProofWritesEnabled = false` for all Game proof write callers. The new registry may be prepared in code-only form but **must not serve as a production uniqueness authority** while any legacy evidence may share an unverified namespace/asset domain.

**Gate H1 (required before enabling):** DB Coordinator and M4 sign a deterministic inventory of all historical evidence: each record either (a) mapped by trusted immutable provenance to a unique key and indexed after *separate* backfill approval, or (b) classified into an explicitly authorized, **provably disjoint** legacy storage/identity domain that cannot collide with any approved new provider key. A merely *different guessed namespace string* is **not** evidence of disjointness. Verified duplicate pairs require documented manual resolution; unique-index installation must not arbitrarily choose an owner.

**Gate H2 (cutover):** Exclusive controlled migration window or equivalent write fence; zero unclassified collidable historical records; no bypassing old writers; compare expected/audited counts; registry keys populated and verified under separately approved SQL/backfill procedure; privileges/guards enabled; schema, backup and rollback plans tested; authorized M4 provider contract active only after separate integration approval.

**Gate H3 (activation):** Team Leader/DB Coordinator explicitly authorize each production feature/worker/DI flag **after** SQL, M4 and M5 end-to-end evidence. If H1 cannot be satisfied, **keep the entire Game proof mutation path disabled** and document the block; do not weaken uniqueness to meet a demo deadline.

**Fail-closed historic-unknown rule:** No new registry reservation, provider consume, evidence commit or media-key reuse against unresolved potentially overlapping legacy identity. A query that returns no comparable historical canonical key does **not** establish absence of collision. A historical duplicate cannot be fixed automatically with `DELETE`, `UPDATE`, a different namespace, or a tombstone release.

### 7.3 Future backfill / migration order (requires separate approval)

1. Freeze/reconcile all proof writers and establish read-only audit baseline; approve M4 namespace/equivalence policy.
2. Review final FK/index widths/collations, controlled-writer SQL API, privileges/immutability protections, backups and rollback plan; stage in an **authorized** isolated copy first.
3. Prepare separately approved shared schema migration: proposed operation principal keys and additional evidence principal key; registry physical table and indexes; permissions/guards. Do not alter `InitialSharedSchema` or snapshots without DB Coordinator ownership and explicit authorization.
4. Dry-run provenance mapping and duplicate exception resolution. Backfill **only verified** historical rows with `ClaimOrigin=2`, `ClaimState=4`, `SubmissionOperationId=NULL`, true original `EvidenceId`; no fabricated receipt or operation. Unresolved cases must prevent H1/H2 activation.
5. Validate unique keys, FKs, exact row-count reconciliation, concurrent writes, crash recovery and legacy bypass protections on separately authorized isolated SQL.
6. Cut over atomically from old writer routes to registry-aware paths under an operational fence; keep new route disabled until approval.

**Rollback policy:** Before activation, revert deployment/flags without deleting the authoritative historical evidence or attempting to reuse reserved keys. After any registry claims exist, never drop/recreate the registry or free tombstones as a casual rollback; preserve unique ownership and audit, disable new proof writes, restore from approved backup/forward-fix plan under DB Coordinator direction. A schema rollback that destroys claims is **not acceptable** as an ordinary feature revert.

---

## 8. Concurrency and crash-recovery test matrix (plan only)

**SQL tests below are NOT currently authorized.** Only code-only tests/fake providers/metadata proposals are permitted now. SQL test execution needs explicit separate approval, including isolated target database and allowed test DDL.

| ID | Test scenario | Required invariant |
|---|---|---|
| A01 | Register operation and claim, one TX | Either both persist or neither persists |
| A02 | Force exception after operation INSERT before registry INSERT | Both rolled back; M4 never called |
| A03 | Same operation/full binding retry | Same claim; no second media reservation |
| A04 | Same operation with changed player/Game/Attempt/Step/capture/key | Conflict, original immutable ownership retained |
| A05 | Two independent connections, different Games, same canonical key | One claim owner only; loser rejected by nonfiltered unique key |
| A06 | Lost TX-A COMMIT acknowledgement | Fresh read of op+claim required before provider invocation |
| A07 | Operation exists with missing/mismatched registry | Fail closed; no silent registry repair |
| A08 | Existing Game-level submission/review runs alongside claim registration | No lock-order inversion; deadlock handling keeps original operation |
| B01 | M4 confirms consumed receipt | M5 TX-B sets op receipt/state and registry `1` in same commit |
| B02 | Inject TX-B failure between operation and registry UPDATE | Both roll back; M4 remains consumed; no second consume |
| B03 | Lost TX-B COMMIT acknowledgement | Fresh read of op+registry+receipt; no M4 reconsume |
| B04 | Provider `Unknown` / timeout / lost response | Lookup only; claim remains reserved; no progress/reward |
| B05 | M4 one capture, two competing operations | One bound durable receipt, other rejects (M4-owned tests) |
| C01 | Consumed receipt + valid proof finalization | One evidence + one progress transition + op `Completed` + claim `2`, atomic |
| C02 | Crash after evidence INSERT before progress/registry UPDATE | TX-C rolls back as one unit; no duplicate evidence on recovery |
| C03 | TX-C COMMIT acknowledgement lost | Fresh coherent op+claim+evidence/progress snapshot, or manual HOLD |
| C04 | Two finalizers, stale claim token/expired lease | Only current fenced holder advances; never double progress |
| C05 | Same operation exact retry after completion | Return same EvidenceId and receipt; no second provider call |
| C06 | Cross-attempt or wrong-step evidence ID | Composite FK/owner check rejects |
| C07 | Final step proof | PendingAdmin result only under authorized policy; no premature coins/winner |
| D01 | SQL UPDATE attempts change media/owner/operation | Immutability protection rejects and preserves rows |
| D02 | SQL UPDATE attempts change EvidenceId after first link | Reject; immutable one-time association |
| D03 | DELETE or `2→0`, `3→0`, `4→0` transition | Reject, tombstone retained |
| D04 | Direct legacy evidence INSERT bypasses registry after cutover | Denied by service permissions/approved guard |
| H01 | Historical evidence has raw ID only, no trusted provider | Classified unresolved; H1 gate false, writes disabled |
| H02 | Two historical rows verified to same provider/key | Manual collision resolution; no arbitrary backfill winner |
| H03 | Proved distinct namespaces / provider aliases | Behavior exactly matches approved M4 canonicalization |
| H04 | Rollback after live claims exist | Production writes halted, claim ownership and tombstones preserved |
| P01 | 2+ independent DB connections high-contention run | No two owners, no duplicated stage advances; bounded deadlock recovery |

Run metadata tests for all FKs/alternate keys/collations/nullability/checks; test SQL under the approved DbContext/migration snapshot, not only an independent hand-built table. Capture SQL exceptions and complete test logs while avoiding raw media identifiers and secrets in shared evidence.

---

## 9. Work allowed now versus held

**Code-only now (authorized):** propose unregistered EF entity/configuration and metadata tests; immutable binding/claim-state validators; fake-provider outcome/receipt/unknown regression tests; audit classification rules and sanitized report template; documentation. Do **not** wire unreviewed registry SQL into active `WayhunterSharedDbContext`, the runtime API, background workers or production DI. Do not execute even isolated registry SQL until specific authorization is given.

**Held:** M4 provider/session compatibility and canonicalization; DB Coordinator physical tables/keys/indexes/CHECK/guards/permissions; historical backfill; shared migration; registry SQL execution; full recovery adapter activation; PR #256 merge. Existing accepted isolated SQL results remain valid evidence for the earlier, narrower operation-store tests; no new SQL result is claimed here.

---

## 10. Requested final decisions / owner checklist

### M4 Capture Owner — required before final schema/activation

1. Can the existing `HunterSubmissionId`-bound M4 Draft capture lifecycle legally/technically serve Game/Attempt/Step? If not, approve a separate Game-purpose capture/session contract; no M5 adapter until then.
2. Provide authoritative provider namespace strings, aliases, Unicode/case normalization, canonical asset ID algorithm/version, equivalence/test vectors, trusted source and legacy lookup limits.
3. Approve stable provider receipt uniqueness, exact `SubmissionOperationId`/Game/Attempt/Step/player/capture/media binding, SQL transaction ownership and `GetOperationOutcomeAsync` semantics after crash.

### Kaviyan / DB Coordinator — review and correct v3

1. Approve/modify the 16 columns, candidate BIN2 collation, CHECK expressions, terminal tombstones, minimum-privilege writer and immutable-update/delete guard.
2. Confirm composite operation/evidence FKs/principal alternate keys and index byte sizes; approve smaller-key alternative with **DB-enforced** owner checks if required.
3. Approve Game-first lock order and TX-A/TX-B/TX-C ownership, provider-independent COMMIT reconciliation, service-permission and legacy-writer fencing.
4. Approve explicit H0–H3 historical cutover gates, exception/disjointness rules, migration order, rollback and privacy/retention controls.
5. Specify whether any **separate** isolated registry SQL test/DDL authorization may be considered; no authorization is inferred from architecture acceptance.

### M5 Owner

- Keep PR #256 **REVIEW ONLY / DO NOT MERGE**. Submit this v3 GitHub document with exact file path and commit SHA; proceed only with authorized code-only proposals/tests. Do not activate the M4 provider or claim registry, generate migrations or mutate historical data.

---

## 11. Repository evidence links

- [M5 proof submission SQL — current Game lock](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameProofSubmissionRepository.cs)
- [M5 proof review SQL — current Game lock](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameProofReviewRepository.cs)
- [M5 SQL attempt — current Game lock](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameAttemptRepository.cs)
- [Existing M5 proof operation configuration — proposed/unregistered](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Configurations/Games/GameProofSubmissionOperationRowConfiguration.cs)
- [Existing M5 operation registration](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameProofSubmissionOperationStore.Register.cs)
- [Existing M5 operation confirmation](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameProofSubmissionOperationStore.Confirmation.cs)
- [Existing M4 Hunter capture model](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/blob/task/m5-idempotent-proof-recovery/src/Wayhunter.Infrastructure/Persistence/Models/HunterPlaces/HunterCaptureSessionRow.cs)

**This is a review proposal. It does not assert final schema approval, production integration, historical data modification or successful new registry SQL tests.**
