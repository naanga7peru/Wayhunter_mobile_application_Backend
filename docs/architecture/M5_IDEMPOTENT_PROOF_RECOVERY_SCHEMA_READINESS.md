# M5 - Idempotent Proof Recovery Schema Readiness

Status: DRAFT - DB Coordinator and Capture Owner sign-off pending
Branch: task/m5-idempotent-proof-recovery

## Verified isolated work
- Operation binding, validation and recovery state guards created.
- Proposed operation persistence model and EF configuration created.
- Latest full backend unit tests: 2921/2921 passed, 0 failed, 0 skipped.
- New SQL fencing input-guard tests: 5/5 passed.
- Actual SQL fencing, concurrency and crash-recovery integration verification remains pending.
- No shared migrations or production DI changes.

## Proposed schema
- Table: GameProofSubmissionOperations
- Primary key: SubmissionOperationId.
- Composite attempt ownership FK: AttemptId, GameId, AuthenticatedPlayerUserId.
- Existing InitialSharedSchema migration defines AK_GameAttempts_Id_Game_Player (Id, GameId, PlayerUserId), with PlayerUserId nvarchar(256). Source-level FK compatibility verified; physical database deployment remains unverified.
- Unique indexes: Provider + CanonicalMediaId and Provider + CaptureSessionId.
- Recovery state, reconciliation index, rowversion and check constraints.

## Pending decisions
- DB Coordinator: physical schema, provider namespace normalization, existing duplicate cleanup and migration ordering.
- DB Coordinator: global uniqueness across existing GameProofEvidence and new operation records.
- Capture Owner: trusted provider idempotency, single-use enforcement and outcome reconciliation.
- M5 Owner: durable atomic claims, recovery transitions, ambiguous commit handling and concurrency tests.
- Team Leader: final production wiring and activation approval.

## Safety boundary
- Proposed EF configuration is not registered in WayhunterSharedDbContext.
- No SQL migration or live database update is authorized.
- Unknown provider or SQL commit outcomes must never trigger consumption of a different capture.
- Production readiness is not claimed.
