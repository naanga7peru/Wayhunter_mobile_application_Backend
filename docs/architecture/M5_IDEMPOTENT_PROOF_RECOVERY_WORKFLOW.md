# M5 - Idempotent Proof Recovery Workflow

Status: DRAFT - Capture Owner and DB Coordinator sign-off required

## 1. Operation registration
- Resolve authenticated player from trusted server identity.
- Validate Game, Attempt, Step, CaptureSessionId and canonical provider/media identity.
- Persist a stable SubmissionOperationId with the complete immutable binding.
- Same operation ID with matching binding must reuse its stored outcome.
- Same operation ID with conflicting binding must be rejected.

## 2. Initial processing
- Atomically claim a durable Pending operation before contacting the capture provider.
- Persist the in-flight state and processing lease.
- Never treat an in-memory claim as sufficient concurrency protection.
- Call only an approved trusted provider supporting operation-bound idempotency.

## 3. Unknown provider outcome
- Timeout, crash or expired in-flight lease requires reconciliation.
- Never blindly repeat capture consumption after an unknown outcome.
- Query the trusted provider using the same operation and immutable binding.
- Do not consume a replacement capture to resolve an ambiguous result.

## 4. Confirmed consumption
- Persist provider confirmation for the original operation.
- Commit evidence, progress and Completed state consistently in SQL.
- If SQL commit outcome is unknown, query durable operation and evidence state before deciding the next action.
- Duplicate completed requests must return the original committed outcome.

## 5. Rejection and manual recovery
- Reject conflicting operation, capture or canonical media reuse.
- Definitive trusted rejection may transition to Rejected.
- Unresolvable outcomes require ManualReviewRequired and authorized review.
- Do not automatically reissue consumption while ownership or outcome is uncertain.

## 6. Remaining approval and verification
- DB Coordinator: physical schema, global uniqueness, relationships and migration ordering.
- Capture Owner: provider idempotency, outcome lookup and single-use guarantees.
- M5 Owner: atomic claim store, reconciliation implementation and concurrency tests.
- Team Leader: final production wiring approval.

## Safety boundary
- Proposed design only; no shared migration, database update or production DI activation.
- Isolated metadata and validation tests do not demonstrate end-to-end recovery.

## Current isolated implementation status
- Isolated SQL operation registration, pending claim, reconciliation claim and claim-token fencing methods are implemented but not SQL integration-tested.
- Latest full backend unit tests: 2921 passed, 0 failed, 0 skipped.
- Provider reconciliation, durable provider outcome transitions, atomic evidence/progress completion, ambiguous commit recovery and concurrency verification remain incomplete.
- Proposed recovery schema is not registered in production DbContext, migrated or activated through production DI.
- DB Coordinator, Capture Owner and Team Leader approvals remain pending.
