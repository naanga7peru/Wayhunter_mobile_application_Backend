# Wayhunter backend pending approvals and unfinished work

## Approval-dependent activation

| Item | Required decision / owner | Evidence and current state |
| --- | --- | --- |
| M3 Admin safety summary and monitoring scopes/projections | M3, M1, Project Lead and data owners | Runtime safety summary is blocked; scoped fixture behavior is not production API readiness. |
| M3 durable audit, live media and push | M3, M1, storage/push owners, DB Coordinator | Local deterministic providers do not prove live delivery or retention. |
| M4 trusted private media and +200 Coins handoff | M4, M6, storage owner, DB Coordinator, Project Lead | Media verifier disabled; approval-to-reward handoff not activated. |
| M5 trusted capture/proof recovery and +100 creator/winner reward handoff | M5, M6, capture provider, DB Coordinator, Project Lead | Missing gameplay HTTP contracts and reward handoff remain distinct implementation/activation dependencies. |
| M6 live cross-module reward delivery | M4/M5/M6, DB Coordinator, Project Lead | No live reward worker enabled. Lost acknowledgement and stock-race/expiry scenarios unverified. |
| M7 Admin identity | M1, M7, DB Coordinator, Project Lead | Trusted role production wiring/storage approval remains fail closed. |
| M7 private image storage/lifecycle | M7, M1, private storage owner, DB Coordinator, Project Lead | Current endpoint returns 503 while flags/provider unavailable. Positive upload not exercised. |
| M7 campaign attribution and sponsored delivery | Campaign owner, M1, M7, governance | Delivery proof and attribution decisions pending. Sponsored intake stays disabled. |
| M7 privacy, consent, retention, replay and reporting exports | M1, M7, DB Coordinator, designated privacy/governance approvers | See `docs/task-evidence/M7-MS07_T01_T03_PRIVACY_OWNER_DECISIONS.md`; bounded replay marked blocked for design decision. |

## Unfinished implementation and testing

M7 has no HTTP endpoints for campaign management, impression/click ingestion, analytics reports, event deduplication or rate-limit management. Those require approved contracts and implementation. M2 live external provider checks and unresolved route geometry requirements are unfinished verification/implementation tasks, not all approval blockers. M4/M5 gameplay HTTP gaps and M6 failure-mode tests also need implementation/testing follow-up.

## Recommended sequence

1. Resolve M1 administrator role storage and trusted authentication wiring with DB Coordinator.
2. Approve M7 privacy, consent, replay, retention, report scopes and campaign attribution contracts with M1, M7, campaign owner and governance.
3. Approve private media ownership, verification and lifecycle recovery with module and storage owners.
4. Implement absent campaign, analytics and gameplay contracts from approved specs, then add isolated SQL integration coverage.
5. Wire M4/M5 post-commit reward events to M6 outbox after owner and DB coordination sign-off.
6. Run approved live-provider checks and targeted failure/recovery testing. Keep production activation separate from LocalDB acceptance.


## Resolved verification issue

The earlier NU1301 restore/build blocker is resolved per user-supplied verification. NuGet connectivity, restore, Infrastructure/API builds and EF model consistency passed. This item is no longer an active blocker. The remaining table entries are product approvals or implementation/testing dependencies.
