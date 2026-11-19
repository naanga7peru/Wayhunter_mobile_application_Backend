# M5 - Proof Consistency and Replay Protection Readiness

Status: DRAFT - Coordinator review required
Branch: task/m5-proof-consistency-replay-hardening

## Verified baseline
- PR #239 merged: SQL proof submission repository and 8 SQL integration tests.
- Latest verification: 9 M5 SQL tests passed; full backend 2994 passed, 0 failed, 36 skipped.
- SQL transaction rolls back on verifier rejection or exception.

## Production blockers
- B1: Capture consumption and SQL commit are not atomic; an approved recovery contract is needed.
- B2: MediaId lacks database-level global uniqueness across Games.
- B3: CaptureSessionId is not durably stored with Game evidence.
- B4: M4 Hunter sessions are bound to HunterSubmission, not M5 Game proof.

## Decisions requiring approval
- M5 and DB Coordinator: media replay keys, uniqueness, and schema design.
- Capture provider owner: trusted single-use binding and failure recovery.
- Team Leader and relevant owners: integration approval.

## Safe next work
- Add replay and failure regression tests.
- Keep production DI and shared migrations unchanged.

## Acceptance boundary
- Isolated SQL tests do not prove production camera or cross-system consistency.
- No production integration is authorized by this document.
