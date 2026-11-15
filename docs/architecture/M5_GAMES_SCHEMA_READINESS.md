# WAYHUNTER — M5 Games Schema Readiness Proposal

**Date:** 2026-11-15
**Owner:** M5 — Thuvarathan
**Repository:** `naanga7peru/Wayhunter_mobile_application_Backend`
**Reference branch/commit reviewed:** `develop` / `7b83a0b` (local checkpoint)
**Document status:** **DRAFT — FOR LEADER + 7-MEMBER SCHEMA REVIEW; NOT APPROVED**
**Migration status:** **HOLD — no M5 EF migration generated, applied, or tested against SQL**

## 1. Scope and rules

This is a proposed SQL Server / EF Core schema for Games, steps, clues, attempts, proof, admin decisions, a single verified winner, creator/player reward sources, and expiry/archive. It is **not** a claim that these SQL tables, constraints, or EF mappings already exist.

- Continue independent Backend code and unit tests only. No `dotnet ef migrations add`, `dotnet ef database update`, manual SQL DDL, or database seed changes.
- Only the Team Leader-appointed DB Coordinator may create/apply the approved **shared** migration after explicit approval by **all seven members**.
- A merged PR is not full database acceptance. Migration-dependent acceptance stays **BLOCKED** until shared-schema and integration verification.
- Only one Admin-verified winner per Game; the Game creator cannot win their own Game.
- Game expiry is exactly `ApprovedAtUtc + 720 hours` (30 × 24 hours); remove expired Games from discovery/play, **not** physically from the database. Preserve creator My Games history and audit/evidence according to retention policy.
- Reward rules: approved Game creator **+100 Coins** once; approved eligible player winner **+100 Coins** once. Pending/rejected/failed attempts earn **0**. M6 owns the actual immutable wallet ledger and credit integration.
- Player proof is camera-only. A client-provided flag by itself is **not** sufficient proof of real camera origin.

## 2. Existing code inventory (verified on `develop`)

| Existing code | Existing representation | Database work |
|---|---|---|
| `Game.cs` | Draft, PendingApproval, Published, Rejected, Disabled, Expired; creator ID; approval time; expiry; winner ID; moderation fields | Proposed `Games` mapping and status/expiry constraints |
| `GameStep.cs` | `Guid Id`, `Guid GameId`, `int StepNumber` | `GameSteps` mapping and ordered-stage uniqueness |
| `GameHint.cs` | `Guid Id`, `Guid GameStepId`, `TextHint`, `PhotoMediaId` | `GameHints` and media-reference rules |
| `GameAttempt.cs` | `Guid Id`, `Guid GameId`, `string PlayerUserId`, `StartedAtUtc` | `GameAttempts`, deadline and active-attempt constraints |
| `GameProgress.cs` | Attempt ID, step progress, rejected evidence count, state | `GameAttemptProgress` mapping |
| `GameEvidence.cs` | Attempt ID, step number, media ID, submitted UTC time | `GameProofEvidence`; step FK strategy needs resolution |
| `InMemoryGameSubmissionStore` | Pending queue, approvals/reward outbox, moderation audit prototypes | Durable SQL transactions, concurrency, outbox and audit pending |

**Current implementation gap:** The `Persistence/Configurations/Games` folder contains no approved Games EF mappings as of this review. No M5 SQL migration is verified. Existing in-memory tests passing do **not** prove SQL constraints or actual wallet credit.

## 3. Proposed tables and key columns (all require approval)

SQL types below are suggested, not final DDL. `UserId` is presently a **string** in M1's `UserAccount`, while Games/attempt IDs use `Guid`; M1 must confirm canonical UserId SQL length and FK target.

| Proposed table | Primary key | Important proposed columns | Relationship / purpose |
|---|---|---|---|
| `Games` | `Id UNIQUEIDENTIFIER` | `CreatorUserId NVARCHAR(128)`, `Title NVARCHAR(150)`, `Description NVARCHAR(2000) NULL`, `DurationMinutes INT`, `Status`, `SubmittedAtUtc`, `ApprovedAtUtc`, `ExpiresAtUtc`, `WinnerUserId NULL`, `IsFlaggedForReview BIT`, reason fields, `LastModeratedAtUtc`, `ArchivedAtUtc NULL`, `RowVersion ROWVERSION` | Game creator → M1 user, approval-anchored expiry, optimistic concurrency; fields absent from domain need implementation before mapping |
| `GameSteps` | `Id UNIQUEIDENTIFIER` | `GameId UNIQUEIDENTIFIER`, `StepNumber INT` | Many steps → one Game; steps are ordered starting at 1 |
| `GameHints` | `Id UNIQUEIDENTIFIER` | `GameStepId UNIQUEIDENTIFIER`, `TextHint NVARCHAR(2000) NULL`, `PhotoMediaId NVARCHAR(256) NULL` | Clue text/photo metadata for one step; media lives outside DB; lengths require media-contract review |
| `GameAttempts` | `Id UNIQUEIDENTIFIER` | `GameId UNIQUEIDENTIFIER`, `PlayerUserId NVARCHAR(128)`, `StartedAtUtc`, `DeadlineUtc`, `Status`, `RowVersion` | Each attempt belongs to a Game and player; deadline is server-owned; new fields are proposed |
| `GameAttemptProgress` | `AttemptId UNIQUEIDENTIFIER` | `TotalSteps INT`, `CurrentStepNumber INT`, `RejectedEvidenceCount INT`, `Status` | One progress row per attempt; mirror `GameProgress` states |
| `GameProofEvidence` | `Id UNIQUEIDENTIFIER` | `AttemptId UNIQUEIDENTIFIER`, `GameStepId NULL`, `StepNumber INT`, `MediaId NVARCHAR(256)`, `SubmittedAtUtc`, trusted capture-session reference NULL, verification status | Many proofs/retakes per attempt-step; do not store binary photos in SQL; `GameStepId` is proposed, not in current entity |
| `GameProofReviewDecisions` | `DecisionId UNIQUEIDENTIFIER` | `EvidenceId UNIQUEIDENTIFIER`, `ReviewerUserId NVARCHAR(128)`, `Outcome`, `Reason`, `OccurredAtUtc`, `CorrelationId` | Immutable audit of stage proof review; exactly-once stage advancement needs transaction |
| `GameCreationApprovals` | `ApprovalRecordId NVARCHAR(128)` | `GameId UNIQUEIDENTIFIER`, `AdminUserId NVARCHAR(128)`, `ApprovedAtUtc`, `RewardEventId UNIQUEIDENTIFIER`, `CorrelationId UNIQUEIDENTIFIER`, `RewardRuleVersion` | Exactly one creation approval per Game; mirrors existing approval command |
| `GameModerationDecisions` | `DecisionId UNIQUEIDENTIFIER` | `GameId UNIQUEIDENTIFIER`, `AdminUserId NVARCHAR(128)`, `Action`, `Reason NVARCHAR(1000)`, `OccurredAtUtc`, `ResultingStatus`, `CorrelationId` | Immutable reject/flag/disable audit; preserves rejected reason |
| `GameResultDecisions` | `DecisionId UNIQUEIDENTIFIER` | `GameId UNIQUEIDENTIFIER`, `AttemptId UNIQUEIDENTIFIER`, `AdminUserId NVARCHAR(128)`, `Outcome`, `Reason`, `OccurredAtUtc`, `CorrelationId` | Separate player completion approval/rejection audit; result flow not yet implemented |
| `GameWinners` | `GameId UNIQUEIDENTIFIER` | `AttemptId UNIQUEIDENTIFIER`, `PlayerUserId NVARCHAR(128)`, `ApprovedAtUtc`, `ApprovedByUserId`, `ResultDecisionId` | **One row at most per Game**: winner approval must be atomic |
| `GameRewardOutbox` | `EventId UNIQUEIDENTIFIER` | `RecipientUserId NVARCHAR(128)`, `SourceType`, `SourceId`, `RewardType`, `CoinsAmount INT`, `RuleVersion`, `CreatedAtUtc`, `DeliveryState`, `CorrelationId` | Proposed idempotent source event for M6; final ownership/table placement needs M6 approval |

**Pending Admin queue:** Prefer a query over `Games.Status = PendingApproval` plus a durable creator-name snapshot / submission record **if approved**, rather than introduce a duplicate queue table without review. The in-memory queue is **not** SQL persistence.

## 4. Proposed relationships and constraints

1. `Games.CreatorUserId` → M1 `UserAccount.UserId` (logical relationship until M1 table name, `UserId` size, deletion/retention rules are approved).
2. `GameSteps.GameId` → `Games.Id` (FK); `UNIQUE(GameId, StepNumber)`; `CHECK(StepNumber >= 1)`. No gaps and every step has a clue are application/transaction checks, not enforced by that unique index alone.
3. `GameHints.GameStepId` → `GameSteps.Id` (FK); at least one of `TextHint` or `PhotoMediaId` must be nonblank. Decide whether multiple hints per step are allowed before adding a unique index.
4. `GameAttempts.GameId` → `Games.Id` (FK); `PlayerUserId` → M1 user (after contract sign-off). Use a **filtered unique index** for at most one active attempt per `(GameId, PlayerUserId)`, with exact active statuses agreed first.
5. `GameAttemptProgress.AttemptId` → `GameAttempts.Id` (PK/FK); `CurrentStepNumber` between 1 and `TotalSteps`; `RejectedEvidenceCount` nonnegative.
6. `GameProofEvidence.AttemptId` → `GameAttempts.Id` (FK). **Blocker:** existing `GameEvidence` records only `StepNumber`, not `GameStepId`; decide whether to add `GameStepId` + FK and validate that it belongs to the same Game as the Attempt. Multiple retakes must remain possible; do **not** add unconditional `UNIQUE(AttemptId, StepNumber)`.
7. `GameProofReviewDecisions.EvidenceId` → `GameProofEvidence.Id` (FK); `DecisionId` unique; stage progression and rejection counting must be concurrency-safe and idempotent.
8. `GameCreationApprovals.GameId` → `Games.Id` with **unique GameId**; `ApprovalRecordId` and `RewardEventId` unique. A retried identical command returns its original result; a conflicting approval fails.
9. `GameModerationDecisions.GameId` → `Games.Id`; unique `DecisionId`, mandatory nonblank reason; record admin identity, reason, time and correlation ID.
10. `GameResultDecisions.AttemptId` → `GameAttempts.Id`; approval must check completed evidence, no self-win, unexpired/eligible Game and no existing winner.
11. `GameWinners.GameId` is **PK/FK** → `Games.Id`, enforcing at most one winner in SQL. Add `UNIQUE(AttemptId)`; perform game-state check, winner insert, result approval and reward-source creation within **one SQL transaction**. An in-memory `lock` does not protect multiple API instances.
12. `GameRewardOutbox.EventId` unique; propose `UNIQUE(SourceType, SourceId, RewardType)` and an M6 ledger idempotency key. Game creator reward source must be distinct from winner reward source; both amounts 100. Only M6 ledger determines actual credited Coins.
13. `ExpiresAtUtc` must equal `ApprovedAtUtc + 720 hours` for approved Games; application computes server-side and stores UTC. Public reads and attempt start/resume/completion must check expiry even if the archive worker has not run.
14. Archive by status/timestamp, **not physical deletion**. Evidence, approvals, audit and winners remain available subject to team-approved retention/privacy rules. Avoid cascading deletion of audit/ledger rows.
15. Admin/user FKs, media FK strategy, indexes, `rowversion` location, enum storage (numeric/string), and delete behavior remain subject to shared 7-member schema approval.

## 5. Suggested query indexes (proposal)

- `Games(Status, ExpiresAtUtc, ApprovedAtUtc)` for public eligible discovery; query also filters `IsFlaggedForReview = 0` and `WinnerUserId IS NULL` (if winner state stays on `Games`).
- `Games(CreatorUserId, SubmittedAtUtc)` for My Games / Admin creator review.
- `GameSteps(GameId, StepNumber)` unique; `GameHints(GameStepId)` for authorized clue lookup.
- `GameAttempts(GameId, PlayerUserId, Status)` for active/resume queries; filtered uniqueness requires status contract.
- `GameProofEvidence(AttemptId, StepNumber, SubmittedAtUtc)` for current stage evidence/history.
- `GameModerationDecisions(GameId, OccurredAtUtc)` and `GameResultDecisions(GameId, OccurredAtUtc)` for audit history.
- `GameRewardOutbox(DeliveryState, CreatedAtUtc)` for delivery retries; never issue duplicate ledger credit.

## 6. Cross-module contract decisions needed

| Dependency / owner | Exact question to approve | Status |
|---|---|---|
| M1 — Shayanthan | Canonical user table name, `UserId` string size/format, FKs for creator/player/admin, user deletion vs retained game history | **TO VERIFY** |
| M6 — Kaviyan | Who owns reward outbox, unique creator/winner source keys, transaction boundary, ledger idempotency, failure/retry compensation | **TO VERIFY** |
| M4 — Neela | If clues/proofs use shared media metadata, agree asset ownership, access and retention boundaries; no unrelated Places schema dependency assumed | **TO VERIFY** |
| M7 — Thashvini / DB Coordinator | Shared DbContext conventions, EF mappings locations, migrations assembly, SQL/seed order and naming | **TO VERIFY** |
| M5 — Thuvarathan | Proof `StepNumber` versus FK `GameStepId`; active attempt statuses; result decision structure; archive/retention; transactional single-winner design | **TO VERIFY** |
| All seven members + Team Leader | Explicit approval of merged schema and migration execution plan | **NOT APPROVED** |

## 7. Migration-dependent task checkpoints

The following are **not to be marked fully accepted** until approved SQL schema and integration tests are available, even if prototype code or PRs have been merged:

| Task ID | SQL-dependent acceptance outstanding | Status |
|---|---|---|
| `M5-MS02-T01` | Games/stages/clues EF relationships and shared migration | **BLOCKED** |
| `M5-MS03-T01` | Pending submission + Admin queue persistence | **PARTIAL / SQL BLOCKED** |
| `M5-MS03-T04` | Durable publication / reward event transaction | **PARTIAL / SQL BLOCKED** |
| `M5-MS03-T05` | Durable moderation audit / decisions | **PARTIAL / SQL BLOCKED** |
| `M5-MS03-T07` | Persisted credit and duplicate prevention against M6 ledger; in-memory QA PR #71 merged | **PARTIAL / SQL BLOCKED** |
| `M5-MS04-T04` | Attempt persistence, active-attempt uniqueness | **BLOCKED** |
| `M5-MS04-T05` | Transactional start/resume and deadline verification | **PARTIAL / SQL BLOCKED** |
| `M5-MS06-T03` | Private proof asset/decision references | **BLOCKED** |
| `M5-MS06-T04` / `T05` | Durable proof dedup and exactly-once stage advancement | **BLOCKED** |
| `M5-MS07-T03` / `T04` / `T08` | One winner per Game, atomic winner decision, M6 reward credit | **BLOCKED** |
| `M5-MS08-T06` / `T09` | Soft archive retention and expiry enforcement across SQL reads/starts | **BLOCKED** |
| `M5-MS10-T01` / `T04` | Database-backed end-to-end reward / concurrency evidence | **BLOCKED** |

Other Backend API and unit-test tasks may continue independently, but **do not infer completion or approval** from this document.

## 8. Current local migration evidence

- Backend latest local sync checkpoint: `develop` at `7b83a0b` (verified by developer before writing this proposal).
- `dotnet restore Wayhunter.slnx`: **passed** on local machine.
- `dotnet test Wayhunter.slnx`: **1214 passed, 0 failed, 0 skipped** on local machine; this is **not** SQL migration acceptance.
- M5 EF configuration files: **not yet present/approved** at review checkpoint.
- M5 `dotnet ef migrations add`: **NOT RUN**.
- M5 `dotnet ef database update`: **NOT RUN**.
- SQL schema applied / seed data / SQL concurrency test: **NOT VERIFIED / BLOCKED**.
- Local old NuGet-fix stash: do not pop or commit unless separately reviewed; newer `develop` already restores successfully.

## 9. Proposed review/approval sequence

1. M5 owner submits this document and draft mapping/relationship proposal; mark as **review requested**, not accepted.
2. M1 validates ID types and user FKs; M6 validates credit/outbox and transactional reward ownership.
3. M5 resolves proof-stage FK, winner atomicity and archive/retention open items.
4. DB Coordinator aligns shared DbContext/EF configuration and migration sequencing with the other six modules.
5. All **seven** members explicitly approve schema decisions; Team Leader confirms sign-off.
6. **Only then** appointed DB Coordinator creates shared migration PR; reviewers inspect DDL, uniqueness, delete rules and seed plan.
7. After approved migration and integration tests, update task acceptance records individually. No auto-approval from a merge.

**Review request:** Team Leader and members, please comment on table ownership, key/FK types, the listed constraints and open decisions. No migration authorization is requested by this proposal.
