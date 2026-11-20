# M5 - Idempotent Proof Recovery Schema Readiness

Status: DRAFT - isolated SQL tests passed; final integration approvals pending
Branch: task/m5-idempotent-proof-recovery

## Verified isolated work
- Operation binding, validation and recovery state guards created.
- Proposed operation persistence model and EF configuration created.
- After merging origin/develop at f9a5dd2: full backend build passed; unit tests 3057/3057 passed, 0 failed, 0 skipped; selected integration tests 191 total, 121 passed, 70 skipped, 0 failed (12 M5 SQL recovery tests excluded).
- New SQL fencing input-guard tests: 5/5 passed.
- Isolated SQL fencing and concurrency tests: 12/12 passed; provider crash recovery and atomic completion remain pending.
- No shared migrations or production DI changes.

## Proposed schema
- Table: GameProofSubmissionOperations
- Primary key: SubmissionOperationId.
- Composite attempt ownership FK: AttemptId, GameId, AuthenticatedPlayerUserId.
- Existing InitialSharedSchema migration defines AK_GameAttempts_Id_Game_Player (Id, GameId, PlayerUserId), with PlayerUserId nvarchar(256). Composite FK physically verified in the authorized isolated M5 LocalDB; shared and production deployment remain unverified.
- Unique indexes: Provider + CanonicalMediaId and Provider + CaptureSessionId.
- Recovery state, reconciliation index, rowversion and check constraints.

## Pending decisions
- DB Coordinator: physical schema, provider namespace normalization, existing duplicate cleanup and migration ordering.
- DB Coordinator: global uniqueness across existing GameProofEvidence and new operation records.
- Capture Owner: trusted provider idempotency, single-use enforcement and outcome reconciliation.
- M5 Owner: durable atomic claims, recovery transitions, ambiguous commit handling and concurrency tests.
- Team Leader: final production wiring and activation approval.

## M4-M5 integration findings and isolated SQL verification
- Latest backend build succeeded without reported warnings or errors.
- Twelve M5 recovery integration tests passed against the DB Coordinator-authorized isolated LocalDB; production approval remains pending.
- Cross-game canonical-media replay rejection passed in isolated SQL recovery-operation tests; global uniqueness across existing GameProofEvidence remains unverified.
- Existing GameProofEvidence.MediaId has no unique index; the new operation-table unique index does not guarantee cross-table global uniqueness.
- Existing M4 capture SQL store provides conditional single-use consumption, but no confirmed SubmissionOperationId-bound durable outcome lookup.
- No concrete IGameProofCaptureRecoveryProvider implementation was found in the reviewed source search.
- Proposed M1 string UserId to M4 Guid HunterId mapping requires explicit approval and trusted authenticated caller verification.
- M4 authorized capture handlers exist, but source searches found no API invocation or production DI registration.
- DB Coordinator and M4 Capture Owner decisions remain blockers; no shared migration or production wiring is authorized.

## Safety boundary
- Proposed EF configuration is not registered in WayhunterSharedDbContext.
- DB Coordinator-authorized test-only SQL table creation completed in isolated M5 LocalDB. Shared migrations, production database updates and production DI activation remain unauthorized.
- Unknown provider or SQL commit outcomes must never trigger consumption of a different capture.
- Production readiness is not claimed.

## Isolated SQL verification - 2026-11-20
- Authorized target: (localdb)\MSSQLLocalDB / Wayhunter_M5_IntegrationTest.
- Test-only schema: 20 columns, 4 indexes, 3 CHECK constraints and 1 composite FK.
- Real M5 SQL recovery tests: 12 passed, 0 failed.
- Release build: all 6 projects passed.
- Release unit tests: 3057 passed, 0 failed.
- Release integration tests: 167 passed, 36 skipped, 0 failed.
- Final SQL cleanup audit: no application-table rows reported.
- Shared migration, production DI and production database updates remain unauthorized.
- PR #256: DO NOT MERGE until remaining contracts and approvals are resolved.
