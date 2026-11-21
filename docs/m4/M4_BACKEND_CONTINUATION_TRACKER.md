# M4 backend continuation tracker

Baseline: M4 `0fe18bc1a47de9b03e2d95cff1fb0dd7ade05b55` with reviewed develop `588867ffd6b39686092de9f84d15cc34e110c3b7`. Integration is pending review on the existing M4 branch.

| Official tasks | Status | Evidence or remaining decision |
| --- | --- | --- |
| M4-MS01-T03, M4-MS01-T04 | Partial | Existing submission, evidence and review EF models and DTOs. Draft write contract and required fields need confirmation before adding a workflow. |
| M4-MS02-T03, M4-MS02-T05, M4-MS02-T06, M4-MS02-T07 | Partial | Capture-to-Draft linkage exists. Authoritative district data source, GPS freshness and accuracy policy, and Draft initialization contract are needed. |
| M4-MS03-T05 | Implemented during this continuation for runtime wiring | Reused authorized start and consume handlers. Registered the consume handler and covered all four SQL and auth startup combinations. HTTP capture contract and actual SQL execution remain pending. |
| M4-MS03-T06, M4-MS03-T07, M4-MS04-T03, M4-MS04-T04, M4-MS04-T05, M4-MS04-T07 | Blocked by approval | Existing evidence validation and EF mapping. Need approved private upload and content verification contract, evidence binding and retention rules. Client camera UI is outside backend scope. |
| M4-MS05-T01, M4-MS05-T02, M4-MS05-T03, M4-MS05-T05, M4-MS05-T07 | Partial | 200 m candidate rules, query and in-memory reader exist. Durable approved-place lookup, approved media/name matching and curated restricted-zone data remain pending. Proximity alone cannot reject. |
| M4-MS06-T01, M4-MS06-T02, M4-MS06-T05, M4-MS06-T07 | Partial | Existing submit and owner-detail contracts and validators. Need approved routes, atomic queue/write and idempotency contract, and owner status refresh. |
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
