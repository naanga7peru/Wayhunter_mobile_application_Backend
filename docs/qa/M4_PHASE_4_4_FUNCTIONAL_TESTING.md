# M4 Phase 4.4 functional testing

Scope is Hunter & Places and ADM-04/06. The branch starts at M3 commit `a0cb9f4aa3be6152152a8d9264efec51f7eec9c8`. All 12 audited operations are present. Identity registration remains in M1. The audit contains no standalone M4 Hunter registration, profile, or verification endpoint.

## Results

All 12 routes rejected missing authentication and invalid JWTs with HTTP 401. Result for the existing approval scope is 10 PASS, 0 FAIL, 2 BLOCKED for live use, and 0 NOT TESTED. With a deterministic verified-media fixture, all 12 exercised their positive LocalDB paths. Live evidence intake and new submission remain blocked with the runtime verifier disabled. Admin review and approved discovery were verified against the SQL records produced with test media.

Paths below use `M = /api/mobile/hunter` and `A = /api/admin/hunter`.

| HTTP operation | Expected and observed | Result |
| --- | --- | --- |
| GET A/submissions | 200 Admin, 403 traveler | PASS. Pending rows were listed and removed after decision. |
| POST A/submissions/{submissionId}/approve | 200 valid, 400 empty ID, 403 traveler, 409 missing or stale | PASS with test media. Concurrent approval produced 200 and 409 with one decision. |
| POST A/submissions/{submissionId}/reject | 200 valid, 400 missing reason, 403 traveler, 409 missing or repeat | PASS with test media. SQL persisted the rejection and reviewer. |
| GET M/places | 200 | PASS. The authenticated listing excluded pending and rejected submissions and contained only the approved place. |
| GET M/submissions | 200 | PASS. An unrelated user received an empty list. |
| POST M/submissions | 201 valid or identical duplicate, 400 invalid, 409 changed payload for same key, 503 injected commit failure | PASS. Concurrent identical requests returned one draft and one request key. The failed commit left neither row. |
| DELETE M/submissions/{submissionId} | 204 eligible draft, 404 foreign, 409 capture session exists | PASS. SQL confirmed deletion of the eligible draft and retention of the captured draft. |
| GET M/submissions/{submissionId} | 200 owner, 404 foreign | PASS. Returned persisted owner-scoped fields without private evidence or owner snapshot. |
| PUT M/submissions/{submissionId} | 204 valid, 400 invalid, 404 foreign | PASS. SQL confirmed the edit. |
| POST M/submissions/{submissionId}/capture-sessions | 201 owner, 404 foreign | PASS. SQL recorded the trusted owner and exact submission. |
| POST M/submissions/{submissionId}/evidence | 201 valid, 200 replay, 400 invalid/foreign receipt, 409 photo before video, 503 unavailable provider | Simulated PASS, live BLOCKED. SQL stored two verified rows and consumed exactly the two bound sessions. Unavailable verification left no evidence or consumed session. |
| POST M/submissions/{submissionId}/submit | 200 valid/replay, 404 foreign, 409 incomplete with test verifier, 503 incomplete with disabled verifier | Simulated PASS, live BLOCKED. SQL confirmed PendingReview, server submission time, and revocation of an unused capture session. |

## Defect and verification

The guarded runtime initially returned 503 for valid draft creation. The four M4 write repositories started explicit serializable transactions outside the configured EF SQL Server retry strategy. Draft creation, update, deletion, evidence binding, submission transition, and Admin review now invoke their existing transaction bodies inside that strategy. The M4 helper clears tracked state before a retry. No endpoint, schema, or authorization contract changed.

Review confirmed that the original serializable isolation, SQL lock hints, conditional updates, idempotency checks, and commit boundaries remain in the transaction bodies. The LocalDB cases verified concurrent draft replay, one winning review decision, and draft rollback before commit. The tests did not inject a lost commit acknowledgement, so response recovery for that failure mode remains unverified.

The actual runtime factory verified `IsLocalDB = 1`, an empty intended catalog, and SQL version `17.0.4025.3` on `(localdb)\WayhunterTest2026`. Each profile applied all nine existing migrations to a fresh catalog. The default profile used `Wayhunter_M4_Functional_b55444c29ba5482f904fcbb5fab7b19d`; the simulated-media profile used `Wayhunter_M4_Functional_ab5bc690dda6439288cb6a5cbe7bfd21`. Both passed. Workers were disabled and OTP and reset email captured in memory. API build passed with zero warnings and errors. All 459 M4 unit tests and 31 focused HTTP and cross-module JWT regressions passed.

## Remaining decisions

- **M4 and media/storage owners, with the Project Lead** must approve and enable a trusted private-media verifier. It must prove asset ownership and upload operation, bind verified facts to the intended capture session, and define capture attestation, access, retention, cleanup, and outage behavior. Runtime uses `DisabledHunterMediaReceiptVerifier`. The deterministic fixture did not exercise live capture, upload, signature verification or storage. [Media policy and decision register](../../WAYHUNTER_M4_DRAFT_EVIDENCE_FINAL_DECISIONS.md) · [Runtime registration](../../src/Wayhunter.Api/Program.cs)
- **M6 owner and Project Lead**, with DB coordination as needed, own the separately approved activation of the durable exactly-once +200 Coins boundary. This run verified M4 review decisions without activating a reward writer or worker. [SQL and reward readiness](../m4/M4_MS07_SQL_RUNTIME_READINESS_EVIDENCE.md)
- No schema change or production activation is part of this phase. The earlier design proposals are approval context. The route table records the current source and observed behavior.

## Review stack

The PR targets `task/m3-api-functional-testing` at `a0cb9f4aa3be6152152a8d9264efec51f7eec9c8` to keep the diff scoped to M4. The stack runs through [M3 PR #318](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/318), [M2 PR #317](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/317), the M1 and Swagger branches, and [shared database configuration PR #316](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/316).
