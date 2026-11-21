# M4 backend continuation tracker

Baseline: M4 `0fe18bc1a47de9b03e2d95cff1fb0dd7ade05b55` with reviewed develop `588867ffd6b39686092de9f84d15cc34e110c3b7`. The approved local merge is b758010f78e7972660af4b620d0181d0981abd8c.

| Official tasks | Status | Evidence or remaining decision |
| --- | --- | --- |
| M4-MS01-T03, M4-MS01-T04 | Partial | Existing submission, evidence and review EF models and DTOs. Owned Draft retrieval is implemented. Draft creation/update and required initial field rules need approval. |
| M4-MS02-T03, M4-MS02-T05, M4-MS02-T06, M4-MS02-T07 | Partial | Capture-to-Draft linkage exists. Authoritative district data source, GPS freshness and accuracy policy, and Draft initialization contract are needed. |
| M4-MS03-T05 | Implemented during this continuation for runtime wiring | Reused authorized start and consume handlers. Registered the consume handler and covered all four SQL and auth startup combinations. HTTP capture contract and actual SQL execution remain pending. |
| M4-MS03-T06, M4-MS03-T07, M4-MS04-T03, M4-MS04-T04, M4-MS04-T05, M4-MS04-T07 | Blocked by approval | Existing evidence validation and EF mapping. Need approved private upload and content verification contract, evidence binding and retention rules. Client camera UI is outside backend scope. |
| M4-MS05-T01, M4-MS05-T02, M4-MS05-T03, M4-MS05-T05, M4-MS05-T07 | Partial | 200 m candidate rules, query and in-memory reader exist. Durable approved-place lookup, approved media/name matching and curated restricted-zone data remain pending. Proximity alone cannot reject. |
| M4-MS06-T01, M4-MS06-T02, M4-MS06-T05, M4-MS06-T07 | Partial | Owner-only My Hunts list and detail now read from the existing submission mapping through the shared scoped reader. Evidence readiness checks requested record IDs, submission binding, metadata and capture order. Submit writes, approved routes and atomic queue/idempotency contract remain pending. |
| M4-MS07-T03, M4-MS07-T04, M4-MS07-T06, M4-MS07-T07, M4-MS07-T08 | Partial | Existing review DTO and status transition/revocation store. Admin permission mapping, review/audit workflow, publication and durable dispatch to the existing M6 +200 wallet path remain pending. |
| M4-MS08-T01, M4-MS08-T04, M4-MS08-T05, M4-MS08-T07 | Missing | Approved-place query, audited edits/deactivation and reference-preserving duplicate merge need approved contracts. |
| M4-MS09-T01, M4-MS09-T02, M4-MS09-T06, M4-MS09-T07 | Missing | Leaderboard and profile Hunt aggregation need approved read contract and tie/reversal policy. |
| M4-MS10-T02, M4-MS10-T03, M4-MS10-T04, M4-MS10-T05, M4-MS10-T07 | Partial | Focused unit/startup verification completed. Endpoint acceptance and approved isolated SQL verification are pending. M4-MS10-T01 physical-device QA is outside backend scope. |

## Verification and gates

- Focused run: 433 M4 unit tests and 22 selected startup tests passed, zero failures and skips. Separate M1 bearer, session authorization, claims and deletion-guard run: 30 passed, zero failures and skips.
- Full unit regression: 3,247 passed, zero failures and skips. The initial build reported two existing TravelPlanning xUnit2031 warnings.
- Program startup exercises the existing guard for SQL enabled/auth disabled. Scoped DI verifies both capture interfaces resolve the same store in one scope and different stores across scopes. Production defaults keep M4 SQL off.
- M5 PR #256 durable recovery needs capture owner, M5 owner and DB Coordinator approval for the binding, receipt and transaction contract.
- Existing M4 SQL integration fixtures use schema initialization. An approved isolated database and setup are required before any run.
- Commit, push and PR creation require separate approval.

Continuation verification: 440 M4 unit tests, 3,254 full unit tests, and 22 selected startup tests passed with zero failures or skips. Existing TravelPlanning xUnit2031 warnings remain.

See [the consolidated decision document](M4_BACKEND_CONTRACT_DECISIONS.md). The continuation owner-read and evidence-readiness changes are uncommitted.
## Current delivery checklist

Branch: feature/m4-final-backend-integration. HEAD: b758010f78e7972660af4b620d0181d0981abd8c. Continuation files are uncommitted.

| Work area and task IDs | Status | Evidence and next action |
| --- | --- | --- |
| Draft creation, MS01-T03/T04, MS02-T05 | PARTIALLY VERIFIED | HunterDraftCreationService uses active M1 account, trusted location, storage field limits and the existing submission mapping. In-memory create/ownership test passed. Internal name/description replacement now uses an atomic owner-and-Draft condition. Approve provider, public route, and client retry contract. Real SQL verification remains pending. |
| GPS and district, MS02-T03/T06/T07 | PARTIALLY VERIFIED | Draft checks finite coordinate ranges, positive accuracy and non-future capture time. Approve district source, freshness and accuracy limits. |
| Capture HTTP, MS03-T05 | BLOCKED BY EXTERNAL APPROVAL | Existing handlers and scoped startup checks pass. Approve exact start route and HTTP outcomes. |
| Trusted evidence, MS03-T06/T07, MS04-T03/T04/T05/T07 | PARTIALLY VERIFIED | Existing readiness and order tests pass. Approve provider receipt, binding, limits and retention. |
| Duplicate lookup, MS05-T01/T02/T03/T05/T07 | PARTIALLY VERIFIED | Existing 200 m screening covered by unit suite. Approve authoritative catalogue and restricted-zone source. |
| Submit Hunt, MS06-T01/T02/T05/T07 | PARTIALLY VERIFIED | Owner reader and evidence precheck available. Approve trusted evidence/location verification and queue/idempotency contract. |
| Admin review, MS07-T03/T04/T07/T08 | BLOCKED BY EXTERNAL APPROVAL | Existing transition infrastructure remains. Approve review permissions, audit transaction and publication rules. |
| M6 dispatch, MS07-T06/T08 | BLOCKED BY EXTERNAL APPROVAL | Existing wallet grant path preserved. Approve durable handoff from committed review. |
| Published places, MS08-T01/T04/T05/T07 | BLOCKED BY EXTERNAL APPROVAL | Approve catalogue mapping and publication/deactivation semantics. |
| Leaderboard, MS09-T01/T02/T07 | BLOCKED BY EXTERNAL APPROVAL | Approve qualifying source/reversal and ranking visibility. |
| Profile counts, MS09-T06/T07 | PARTIALLY VERIFIED | CountMyApprovedHuntsAsync queries approved rows for the authenticated owner. Confirm M1 integration and visibility before public exposure. |
| M5 recovery, MS03-T05 cross-module | BLOCKED BY EXTERNAL APPROVAL | Capture and M5 owners and DB Coordinator must approve operation binding and durable receipt. |
| HTTP security, MS03-T05, MS06-T01/T05, ADM-04 | BLOCKED BY EXTERNAL APPROVAL | Selected M1 startup and full unit regression passed. No approved M4 route has been exposed. |
| Real SQL, MS10-T02/T03/T04/T07 | BLOCKED BY EXTERNAL APPROVAL | Isolated target, initialization and cleanup require DB Coordinator authorization. |
| Regression, MS10-T02/T03/T04/T05/T07 | PARTIALLY VERIFIED | Fresh run: 3,255 unit and 22 selected startup tests passed, zero failed or skipped. Real SQL and full integration suite were excluded. |
| Delivery, MS10-T07 | PARTIALLY VERIFIED | Reviewed merge preserved. Proposed local commit after approval: feat(m4): add trusted draft creation and owner Hunt statistics. Proposed PR base: develop. Release pending approved contracts and verification. |

Draft input is a server-verified location object. No provider or HTTP binder is registered for that object. Next operation after district approval: connect the authoritative location verifier to the Draft creation entry point and add provider failure and district mismatch tests.

Latest execution: 3,255 unit and 22 selected startup tests passed. New Draft update authorization and invalid-input assertions ran without SQL. Review the consolidated proposals in [M4_FINAL_APPROVAL_REQUEST.md](M4_FINAL_APPROVAL_REQUEST.md).

## Isolated HTTP and provider candidates

HEAD b758010f78e7972660af4b620d0181d0981abd8c on feature/m4-final-backend-integration. Checkpoints 1–9 retain their prior completed status. For checkpoints 10–25, use the sixteen rows above in order. Draft, GEO, capture HTTP, evidence, profile statistics and HTTP security now also have candidate evidence. Seven routes live only in the test assembly. Twelve routes remain proposals. Zero operational M4 routes.

Candidate build: 3,261 unit and 25 selected integration tests passed, zero failed or skipped. Existing two TravelPlanning xUnit2031 warnings. Production discovery, anonymous denial, owner isolation, conflicting capture ID and GEO unavailable/invalid coordinates were tested. Capture success, Draft update SQL, actual provider authentication, cursor pagination and real SQL remain unverified. No candidate provider registered in production.

Next action: approve API-01/02/04 and the owner statistics part of API-07. Confirm pagination/error envelopes and connect the approved GEO provider before activation. Continue MEDIA-01, DATA-01, ADMIN-01 and PUB-01 before production submit/review.

## Latest continuation

HEAD b758010f78e7972660af4b620d0181d0981abd8c. Checkpoints 1–9 retain prior scope. Checkpoints 10–25 map to these work areas.

| Checkpoint | Status | Latest evidence and next action |
| --- | --- | --- |
| 10 Draft | Partially completed | Test-host create returns 201 and stores an owned Draft in EF InMemory. Draft update relational verification prepared, pending SQL-01. |
| 11 GEO | Partially completed | Verified test double and unavailable outcomes tested. Approved real data required. |
| 12 Capture HTTP | Partially completed | Owned start returns 201 and foreign start 404 using existing handlers and in-memory store. API-02 and SQL acceptance pending. |
| 13 Evidence | Partially completed | Existing receipt binding tests preserved. MEDIA-01 needs approved provider and atomic evidence/session write. |
| 14 Duplicate lookup | Partially completed | Existing 200 m screening remains. PUB-01/GEO-01 must identify authoritative catalogue/zone source before durable queries. |
| 15 Submit | Partially completed | Existing ownership and evidence prechecks remain. MEDIA-01/GEO-01/DATA-01 required to implement verified atomic submission. |
| 16 Admin | Partially completed | Test-only pending queue and details with explicit permission. ADMIN-01 and MEDIA-01 needed for decisions/audit and protected evidence. |
| 17 M6 | Blocked by external approval | Existing +200 handler tests covered by full unit suite. REWARD-01 durable handoff pending. |
| 18 Discovery | Partially completed | Test-only reads require Approved, Published and active source rows. PUB-01 catalogue and moderation required. |
| 19 Leaderboard | Partially completed | Provisional distinct Hunter earnings, reversals, tie ranking and pagination tested. RANK-01 must approve source, display names and visibility. |
| 20 Profile counts | Partially completed | Existing owner-count and route tests pass. API-07/M1 activation needed. |
| 21 M5 | Blocked by external approval | PR #256 open at 0d16cefd. M5-01 durable contract and schema pending. |
| 22 HTTP security | Partially completed | Anonymous, foreign owner and test-only Admin denial verified. Production bearer and permission activation pending. |
| 23 SQL | Blocked by external approval | Added Draft-update fixture for owner/state, retries and preserved capture binding. Compiled, not run. SQL-01 pending. |
| 24 Testing | Partially completed | 3,263 unit and 29 selected integration passed, zero failed/skipped. Remaining integration suites excluded. |
| 25 Delivery | Partially completed | 0 operational routes, 12 isolated candidates, 7 proposals. Next action: approve API-01/02/04/05/06/07, GEO-01, then media and atomic write paths. |

Remaining seven proposals: upload authorization, evidence completion, Submit Hunt, protected Admin evidence read, Admin decision write, moderation edit and deactivation.

## Latest continuation: 19 isolated routes

Checkpoints 1–9 retain prior completion. Latest work areas 10–25 are below. HEAD remains b758010f78e7972660af4b620d0181d0981abd8c.

| Checkpoint | Status | Latest evidence and next action |
| --- | --- | --- |
| 10 Draft | Partial | Isolated create/read verified. SQL details update, retry and activation remain. |
| 11 GPS | Partial | GEO outcomes tested. Approve authoritative thresholds and datasets. |
| 12 Capture | Partial | Existing start, consume and revocation preserved. Production activation and SQL acceptance remain. |
| 13 Evidence | Partial | Authorization and completion application orchestrator, receipt binding and repeated-completion denial tested. MEDIA-01 must approve atomic relational association and signer. |
| 14 Duplicate lookup | Partial | Existing 200 m rules now exercised against visible catalogue snapshot; inactive/unpublished rows excluded. PUB-01 and GEO-01 needed for authoritative data. |
| 15 Submit | Partial | Trusted snapshot preflight, evidence matching, district consistency, duplicate screening and replay tests. DATA-01 must approve durable transaction/payload identity. |
| 16 Admin | Partial | Pending reads, permission, protected evidence and decision candidates tested. ADMIN-01 and MEDIA-01 needed for real policy, audit and atomic review. |
| 17 M6 | Blocked | Existing SqlCommittedCoinRewardSource already reconciles committed Hunter approval and review. REWARD-01 approval, atomic review and authorized durable runtime acceptance needed. |
| 18 Published places | Partial | Isolated discovery and moderation routes. PUB-01 authoritative moderation/audit storage pending. |
| 19 Leaderboard | Partial | Prior ranking tests pass. RANK-01 ledger selection, reversal and public names pending. |
| 20 Profile | Partial | Owner stats preserved. API-07 and M1 exposure approval pending. |
| 21 M5 | Blocked | PR #256 open at 0d16cefd. No durable provider activated. |
| 22 HTTP security | Partial | Test-only permission and owner gates exercised. Production bearer, ADM-04 roles and deployment acceptance pending. |
| 23 SQL | Blocked | Existing fixtures compiled. SQL-01 approvals required before any fixture execution. |
| 24 Regression | Partial | 3,263 unit and 32 selected integration passed, zero failures and skips. Remaining integration and real SQL excluded. |
| 25 Delivery | Partial | Nineteen isolated HTTP candidate routes and zero operational M4 routes. Approve contracts and commission real adapters before deployment. |

Continuation remains uncommitted. Build has zero errors and two existing TravelPlanning xUnit2031 warnings. Next action: approve MEDIA-01/DATA-01/ADMIN-01/PUB-01 and build relational adapters, then execute only authorized integration tests.

## Actual relational continuation

HEAD b758010f78e7972660af4b620d0181d0981abd8c. No checkpoint was promoted to completed without real SQL and activation evidence. Checkpoints 1–9 remain complete, 10–16 and 18–20 and 22 and 24–25 remain partial, 17/21/23 remain externally blocked.

- Checkpoint 12: existing capture SQL start/consume transactions now run through the shared EF execution strategy. Start retains a stable server-generated session ID within an execution-strategy retry. Consume remains conditional and cannot prove a successful uncertain outcome without M5-01 receipt support.
- Checkpoint 13: real read-only SQL session eligibility checks owner, Draft, issue/expiry and unused/unrevoked state. An atomic association adapter must repeat the checks and bind the trusted media receipt before consumption.
- Checkpoint 16: SQL transition store now supports one PendingReview decision with audit and unused-session revocation in one transaction. Supports matching decision-ID retry. Production caller and ADM-04 permission mapping are pending.
- Checkpoint 23: compiled concurrent review and evidence eligibility integration case. No real SQL executed.
- Checkpoint 24: 3,263 unit and 32 selected integration tests passed, no failures or skips. Two existing xUnit2031 warnings. M6 safe unit tests are included. Full integration and isolated SQL not executed.
- Checkpoint 25: 19 test-only candidates and zero operational routes. No new controllers. Next step: approve ADM-04 caller and SQL-01, then run the existing M4 SQL filter and review the transaction evidence.

## Relational Submit Hunt and review follow-up

- Checkpoint 15 remains partial. TrySubmitVerifiedDraftAsync locks the owned Draft, compares canonical location and place fields with the saved Draft, re-reads the evidence records, and writes PendingReview, SubmittedAtUtc and the owner snapshot with unused-session revocation. Existing PendingReview query is the review queue. The write primitive has no HTTP caller; DATA-01 durable operation binding and MEDIA-01 authoritative receipt-to-capture association remain prerequisites.
- Checkpoint 16 remains partial. Review retries now acquire an update/range lock on the audit decision key before deciding whether to replay or insert. Allowed transitions stay PendingReview to Approved, Held or Rejected. ADM-04 caller and relational verification remain pending.
- Checkpoint 17: Existing M6 committed source now has a cross-module EF InMemory verification for Approved submission plus Approved review, exclusion of Held/Rejected/missing review, +200, stored beneficiary, stable source/event IDs and existing-outbox exclusion. This does not exercise durable delivery.
- Checkpoint 23: VerifiedSubmit_RollsBackOnRevocationFailureAndQueuesOnlyOnce compiled. SQL execution still blocked. Capture, Draft update and review audit fixtures are preserved.
- Checkpoint 24: 3,264 unit and 32 selected integration tests passed. Zero failures or skips in those runs, two existing TravelPlanning xUnit2031 warnings. Real SQL and other integration fixtures were excluded.
- Checkpoint 25: No change to the 19 isolated candidates or zero operational routes. HEAD b758010f78e7972660af4b620d0181d0981abd8c. All continuation work remains uncommitted.
