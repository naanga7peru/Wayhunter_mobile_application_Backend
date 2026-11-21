# M3 Admin Safety readiness

## Source and scope

M3_SCOPE assigns ADM-08, ADM-09 and ADM-16 to M3. Active Rules require route-matched hazards within 1 km, photo capture time and expiry 24 hours after server report time. The checked-in M3 consent, SignalR, Safe Check-in, notification and hazard/separation documents provide the backend context. The separate Master BRD and milestone vault were unavailable in the workspace.

## Implemented and verified

The read-only endpoint reuses the M1 session bearer handler, trusted identity contract and TrustedAdministratorRoleResolutionService. The policy follows the Admin Notifications pattern of requiring authenticated identity and a server-owned Admin authorization decision, with a separate policy for monitoring. The role resolver verifies the bearer, persisted account and session, and server role assignment. Monitoring independently requires a grant for the authenticated actor and exact trip/session, checks live trip sessions and canonical membership, and rechecks the grant before returning data.

Trip and session status come from the existing M3 repositories. Participant and Safe Check-in counts validate active membership or trip ownership. Check-ins use the existing M3 record shape and check the trip, session and time bounds. Safe Check-in is independent of GPS consent. Separation pair summaries require active session access and current GPS consent for both members through the same reader used by SignalR. Reversed pairs count once. Hazard summaries accept only route-matched, unexpired reported or verified entries from the scoped projection. Live hazard and separation sections are unavailable for inactive or cancelled trips. The endpoint returns counts and hazard status/time summaries without participant identifiers, coordinates, GPS trails, contacts, private media references, credentials or notification tokens.

The audit contract records authenticated Admin read attempts with actor, action, target, UTC time and outcome. The response is withheld unless the audit sink accepts the record. Authentication and role failures are rejected in middleware before monitoring. Existing moderation, alert transitions, notification dispatch and Safe Check-in writes are unchanged. This branch adds no Admin mutation endpoint.

## Proposed API contract

`GET /api/admin/safety/trips/{tripPlanId}/sessions/{sessionId}` uses GUID path segments and returns `Cache-Control: no-store` on success. The response fields are `target`, `tripStatus`, `sessionStatus`, `activeParticipantCount`, `hazards`, `activeSeparationCount` and `checkedInParticipantCount`. Hazard items contain `status`, `photoCapturedAtUtc` and `expiresAtUtc`. Null sections indicate unavailable or ungranted data. Zero or an empty array represents a returned section with no qualifying entries.

| Status | Meaning |
|---|---|
| 200 | Audited, authorized summary |
| 400 | Empty trip or session GUID |
| 401 | Missing or invalid authenticated session |
| 403 | No verified Admin assignment or exact monitoring grant |
| 404 | Invalid path shape, or the authorized trip/session pair does not exist |
| 503 | Required monitoring or audit dependency unavailable |

## DECISION_REQUIRED before activation

- M1 and Admin owners must approve registration of the existing trusted Admin role resolver and the server-controlled monitoring grant source, including per-trip and per-section permissions, revocation and observability.
- M2/M3 must approve the authoritative trip-specific route adapter and a matched hazard read projection. M3/product owners must approve a separation read projection and any additional participant safety fields. The existing Safe Check-in store provides a write contract. A read projection and visibility policy require approval.
- The DB Coordinator and privacy owners must approve audit storage, access, retention, failure handling and the consistency requirements for grants, membership, consent, status and audit. Current reads and audit are separate operations. Define bounded projection sizes and operational limits before activation.
- Any Admin mutation requires existing owner-approved permission and audit contracts. Existing hazard moderation ownership and participant alert transitions remain in their current paths.

The repository host registers the endpoint and policy while leaving the role resolver, monitoring grant source, projections and audit sink unwired. Tests supply controlled implementations. No production monitoring, FCM, alerts or workers were activated. No SQL DDL ran. InitialSharedSchema, the EF model snapshot and migrations match develop.

## Verification

Base develop `4e5d713`. Restore and build passed. The first build reported 0 errors and two existing M2 `xUnit2031` warnings. The incremental rebuild following the HTTP test correction reported 0 errors and 0 warnings.

| Run | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Full unit | 3236 | 0 | 0 |
| Full integration | 185 | 0 | 85 opt-in SQL |
| Focused Admin Safety unit | 3 | 0 | 0 |
| Focused Admin Safety HTTP | 11 | 0 | 0 |

Full command `dotnet test --no-restore`. The separate focused run used `--no-restore --no-build --filter FullyQualifiedName~M3AdminSafety`. SQL opt-ins were cleared for the test process. Git diff checks passed.
