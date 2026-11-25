# M3 Phase 4.3 functional testing

Branch `task/m3-api-functional-testing` starts from M2 commit `5a62d0e8df4b58ef5781c09db6d8dc87ef1efef2`. Scope is Trip Safety and the ADM-08, ADM-09, and ADM-16 responsibilities. See [M3 ownership](../members/M3_SCOPE.md) and [Active Rules](../second-brain/ACTIVE_RULES.md).

## Results

**36 audited HTTP operations exercised. 35 PASS, 0 FAIL, 1 BLOCKED.** The 35 passes include 21 passes through the guarded runtime profile and 14 passes with simulated providers and real SQL stores. All 36 operations returned 401 for missing authentication and invalid JWT. In the profile with unavailable adapters, all 15 affected operations returned the expected 503.

| Group | Operations | Result and actual HTTP evidence |
| --- | ---: | --- |
| Invitations and membership | 6 | PASS. Create 201. List pending, accept, reject, cancel, and remove 200. Unauthorized invitation creation 403. SQL verified decisions and membership removal. |
| Live sessions and GPS consent | 6 | PASS. Start, pause, resume, recover, finish, and consent update 200. Member start and outsider consent modification 403. SQL verified session transitions. |
| Safe check-in | 1 | PASS. Member and retry 200, outsider 403. One SQL check-in and one outbox row. |
| Hazards | 4 | PASS with deterministic route geometry. Report 201. List, verify, and resolve 200. Invalid coordinates 400 and unauthorized access 403. SQL verified resolution and the response excluded the private photo reference. |
| Trip memories | 4 | PASS. Create 201, list and read 200, delete 204, another member's private read 404. SQL verified soft deletion. |
| Memory media | 2 | Simulated-provider PASS. Upload grant and completion 200, with SQL attachment verified. Both returned 503 with the unavailable media adapter. |
| Mobile notifications | 8 | Simulated-provider PASS. Inbox, preference read, device register, and reminder schedule 200. Mark read, preference write, device revoke, and reminder cancel 204. Cross-user read marking 404. SQL verified changes. All eight returned 503 with unavailable adapters. |
| Admin notifications | 4 | Simulated-provider PASS. Preview, schedule, and summary 200, cancel 204. Regular-user preview 403. SQL verified preview receipt, intended recipient, and cancellation. All four returned 503 with unavailable adapters. |
| Admin Safety summary | 1 | BLOCKED in the runtime host. Regular user 403 and verified Admin 503. Scoped 200 and denial/privacy behavior passed in controlled fixtures only. |

SignalR `/hubs/trip-location` passed separately from the HTTP count. A real-JWT WebSocket handshake succeeded. Join was denied before GPS consent, join and publish succeeded after consent, and publishing was denied after revocation. The separate regression exercised fan-out, reconnect, membership removal, and trip finish.

## Isolation and verification

- The API runtime factory confirmed `IsLocalDB = 1`, server version `17.0.4025.3`, and the intended empty catalog on `(localdb)\WayhunterTest2026` before migrations. All nine existing migrations were applied and verified. The final simulated-provider catalog was `Wayhunter_M3_Functional_dc6c4ae32864400da5f85e3587761a43`. The latest fail-closed catalog was `Wayhunter_M3_Functional_298d0bb89d28454fae9c0863caeda7f2`.
- Both isolated profiles passed. 646 M3 unit tests and 27 focused Admin Safety and SignalR integration regressions passed. A follow-up rerun also passed five focused Admin Safety unit cases. The API build reported zero warnings and errors.
- Application workers were disabled and OTP and reset emails stayed in memory. Maps route geometry and the private media provider were simulated. The notification SQL workflow and registry used a restricted test audience and synthetic device-token protection. Live map routing, media storage or deletion, token protection and push delivery remain unverified. Admin Safety fixture projections and audit acknowledgements do not establish runtime SQL projection or durable audit behavior.
- No production M3 code or migration changed. The existing Admin Safety test now checks the actual monitoring dependencies rather than assuming the shared M1 role resolver is absent. No production deployment, external email, live push, or shared SQL work occurred.

## Admin Safety pending approvals

`GET /api/admin/safety/trips/{tripPlanId}/sessions/{sessionId}` returns 503 for a verified Admin because `AdminSafetyMonitoringService` returns `Unavailable` as soon as it encounters a missing `IAdminSafetyAuditSink`. Runtime DI also lacks `IAdminSafetyScopeReader` and `IAdminSafetyProjectionReader`. Shared SQL identity can resolve the Admin role independently. There is no runtime activation setting that supplies these three adapters. See [monitoring service](../../src/Wayhunter.Application/Modules/TripSafety/Services/AdminSafetyMonitoringService.cs), [runtime DI registration](../../src/Wayhunter.Api/Extensions/AdminSafetyServiceCollectionExtensions.cs), and [Admin Safety readiness](../architecture/M3_ADMIN_SAFETY_READINESS.md#approval-matrix).

| Responsible owners | Pending decision |
| --- | --- |
| M1 and Admin owners | Approve the authoritative grant source for the actor, exact trip, session, and allowed sections. Define revision, expiry, revocation, and denial handling. |
| M2, M3, and product owners | Approve the authoritative route-matched hazard, separation, and Safe Check-in projections, allowed visibility, freshness and limits, and invalidation on consent, membership, and session changes. |
| DB Coordinator and Privacy, with M1 and M3 | Approve durable audit schema and access, retention and deletion policy, success or uncertain-write acknowledgement, and transaction or revision fencing for summary release. Approve any required M3 schema migration separately. |

The project decision is to collect remaining approvals after functional testing across all member modules. M2 approval does not cover M3 monitoring. See [SQL coordination evidence](../task-evidence/M3_POST_MERGE_SQL_COORDINATION.md) and the [audit persistence proposal](../architecture/M3_ADMIN_SAFETY_READINESS.md#audit-persistence-proposal-for-db-coordinator-review).

## Review stack

Target `task/m2-api-functional-testing` to keep this PR scoped to M3. The base is [PR #317](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/317), which targets `task/m1-api-functional-testing`. That branch depends on `task/swagger-auth-readiness` and the shared database configuration branch reviewed in [PR #316](https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/316). Review the stack before retargeting to develop.
