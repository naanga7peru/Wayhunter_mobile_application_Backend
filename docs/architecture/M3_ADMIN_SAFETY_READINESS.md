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
| 403 | No current verified Admin assignment or exact monitoring grant |
| 404 | Invalid path shape, or the authorized trip/session pair does not exist |
| 503 | Required dependency unavailable, or projection or read failed security revalidation |

## Security hardening

- The endpoint uses the same M1 trusted-role resolver for the entry policy and the checks before audit and response release. One bearer header is required. The role resolver rechecks account, session and server assignment. Missing or unavailable role resolution denies access.
- The exact actor/trip/session grant is checked before and after each read, and the second pass must match the original grant. Each pass re-reads trip and session state, and evaluates membership and live GPS access against authoritative readers. A changed grant or role denies the response. A changed trip/session or effective summary withholds it.
- Each requested projection must be freshly observed during its reader call using the same trusted UTC timebase. Future, stale, wrong-target and malformed data fail closed. The staging limit is 256 entries per granted section. The application copies and validates granted data before producing aggregates and discards ungranted sections. The response contract remains unchanged.
- The first audit records the prepared result. The service then performs a fresh authorization and projection pass before returning data. A failed final check records a separate `WithholdTripSafety` outcome. Audit failure at either stage withholds the response. An initial `Success` records a prepared authorized read, which may subsequently be withheld.

## Approval matrix

| Owner | Decision | Status and evidence |
|---|---|---|
| M1 / Admin | Trusted Admin role contract, active verified account, non-revoked session and server assignment; deny missing or invalid assignment | **APPROVED** for Phase A by the [M1–M7 role checklist](../task-evidence/M1_M7_ADMIN_ROLE_SQL_APPROVAL_CHECKLIST.md), section 2 |
| M1 / Admin | Production role resolver wiring, exact trip/session and section grants, grant revocation/version, and monitoring denial policy | **DECISION_REQUIRED** |
| M3 / M2 | Authoritative, route-matched hazard read projection and route revision/invalidation | **DECISION_REQUIRED** |
| M3 | Separation and Safe Check-in read projections, and recipient visibility | **DECISION_REQUIRED** |
| M3 | Atomic consent, session and membership snapshots for monitoring; production limits and freshness contract | **DECISION_REQUIRED** |
| DB Coordinator / Privacy | Durable audit persistence, reader roles, retention and deletion policy | **DECISION_REQUIRED** |
| DB Coordinator / M1 / M3 | Transaction boundary, concurrency, idempotency and failure strategy | **DECISION_REQUIRED** |

### Owner approval requests

- **M1 / Admin** — Please confirm production use of the Phase A resolver and approve the exact trip/session and section grant source, change/version semantics and denied-access handling.
- **M3 / M2** — Please confirm the authoritative route and hazard, separation, and Safe Check-in projections. Specify how each read proves scope and freshness, and how consent, membership and session changes invalidate a read.
- **DB Coordinator / Privacy** — Please review the audit fields, authorized readers and retention policy, and choose a transaction and concurrency strategy that prevents release across revoked permissions.

## Audit persistence proposal for DB Coordinator review

The existing `IAdminSafetyAuditSink` receives trusted actor ID, bounded action and outcome, exact trip/session target and UTC timestamp. For durable review, propose an append-only event identifier and a server-generated opaque attempt identifier to correlate a prepared read with a later withheld outcome. Use bounded action and outcome codes and the approved M1 actor identifier format. Review target linking and deletion semantics because a denial can name a missing or untrusted target. Exclude response payloads, participant IDs, GPS, contacts, evidence references, authorization headers, tokens and free text.

Use separate least-privilege write and review roles, restrict access to approved security/privacy staff, and track audit access and exports. Approve retention duration, deletion or de-identification, backup handling and legal holds before enabling persistence. Review indexes, uniqueness for retries, and an acknowledgement contract that distinguishes committed writes from uncertain failures. Required audit writes must be acknowledged before release. A failed or uncertain write withholds the summary. Review alerting for sink failures without logging sensitive request data.

Prefer an approved consistent snapshot or revision fence covering the Admin role, monitoring grant, trip, session, membership, consent and projections, with an auditable release decision. An independently read role or projection may require a coordinated version across sources. The current checks narrow race windows but cannot make separate source reads and the audit write atomic. Revocation after the final check, change-and-reversal between reads, an adapter relabeling cached data, and a change while the HTTP response is being sent remain possible. A prepared record is not proof of delivery. Audit schema and durable storage remain proposed. No SQL migration or DDL is authorized.


## DECISION_REQUIRED before activation

- M1 and Admin owners must approve registration of the existing trusted Admin role resolver and the server-controlled monitoring grant source, including per-trip and per-section permissions, revocation and observability.
- M2/M3 must approve the authoritative trip-specific route adapter and a matched hazard read projection. M3/product owners must approve a separation read projection and any additional participant safety fields. The existing Safe Check-in store provides a write contract. A read projection and visibility policy require approval.
- The DB Coordinator and privacy owners must approve audit storage, access, retention, failure handling and the consistency requirements for grants, membership, consent, status and audit. Current reads and audit are separate operations. Define bounded projection sizes and operational limits before activation.
- Any Admin mutation requires existing owner-approved permission and audit contracts. Existing hazard moderation ownership and participant alert transitions remain in their current paths.

The repository host registers the endpoint and policy while leaving the role resolver, monitoring grant source, projections and audit sink unwired. Tests supply controlled implementations. No production monitoring, FCM, alerts or workers were activated. No SQL DDL ran. InitialSharedSchema, the EF model snapshot and migrations match develop.

## Verification

Base develop `0ba69f7`. `dotnet restore` and `dotnet build --no-restore` passed with zero errors and two existing M2 `xUnit2031` warnings.

| Run | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Full unit | 3238 | 0 | 0 |
| Full integration | 197 | 0 | 85 opt-in SQL |
| Focused Admin Safety unit | 5 | 0 | 0 |
| Focused Admin Safety HTTP | 23 | 0 | 0 |

Full command `dotnet test --no-restore`. The separate focused run used `--no-restore --no-build --filter FullyQualifiedName~M3AdminSafety`. SQL opt-ins were cleared for the test process. Git diff checks passed. The tests cover changed and removed grants, role and consent revocation, membership removal, session finish and trip cancellation, stale/invalid/oversized projections, wrong scope, unavailable dependencies, audit failures and ordinary-user denial.
