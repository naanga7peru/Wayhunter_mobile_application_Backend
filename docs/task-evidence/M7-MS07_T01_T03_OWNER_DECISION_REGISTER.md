# M7-MS07-T01/T03 owner decision register

**Decision basis** Physical schema design conditionally accepted at `239bc059bebd6dfdb2e51bc4d7e8030dc0bd3819`. Architectural acceptance and conditional design acceptance leave the following implementation decisions open. [Final SQL review artifacts](M7-MS07_T01_T03_FINAL_SQL_REVIEW_ARTIFACTS.md)

| Decision | Recommended candidate for review | Required approver | Status |
| --- | --- | --- | --- |
| Admission authentication and retry identity | Server authenticates and authorizes before accepting or disclosing replay. Scope identity to ad/type/retry GUID. Require server-minted RetryEventId and authenticated admission credentials on every path, including unattributed v1; changing current T01 caller-generated IDs requires approval. | M1 and M7 | PENDING |
| Binding versions and conflict | Domain-separated SHA-256 over canonical RFC-order GUIDs, big-endian version and event type, and validated stable delivery GUID in v2. Pin version on the receipt. Reject altered signed tuples before lookup; 409 remains reserved unless a reachable conflict is approved. | M1 and M7, Campaign owner for v2 | PENDING |
| Original receipt and external behavior | Preserve current internal Duplicate semantics. Candidate stable HTTP 200 only after confirmed commit, and reviewed 401/403/410/429/503 responses. 409 is reserved because it is unreachable under current signed v1/v2 assumptions. Approve exposure of source ID/time, any attribution fields and retry guidance. | M1 and M7 | PENDING |
| Quota and attempt limits | Nonpersonal ad/type SHA-256 partition, 60-second UTC buckets, candidate 120 impressions and 30 clicks, persisted configuration per bucket and boundary activation. Ten-minute candidate write age. Define separate attempt controls and review capacity. | M1, M7 and DB Coordinator, privacy owner for any expanded scope | PENDING |
| Cardinality and authoritative attribution | Decide actual ad relation and what proves the Campaign shown. Prefer a stable server-issued delivery reference where a current link is ambiguous. Specify format, caller binding, validity and whether eligibility is rechecked. | Campaign owner and M7, M1 for proof authentication | PENDING |
| Historical Campaign principal | Nullable only for approved unattributed flows. Retain approved unique principal for receipt FK, restrict hard delete, and preserve snapshot through archive or unlink. | Campaign owner, M7 and DB Coordinator | PENDING |
| Fact retention and coverage | Candidate 365 days online, with explicit archive or incomplete coverage semantics and scoped historical audit. | Privacy/data owners, M7 and DB Coordinator | PENDING |
| Receipt and tombstones | Unbounded retention declined by Shayanthan. Evaluate immutable server-issued admission and finite markers only with proven non-reissuance and expiry safety. | M1, M7, Campaign owner, DB Coordinator and formal Privacy/Data Governance | BLOCKED FOR DESIGN DECISION |
| Quota retention and cleanup | M1 conditionally accepts 48 hours after window end. Recheck cutoff under the same key lock. No deletion authority until separately approved. | M1, privacy/data owners and DB Coordinator | PENDING final approval |
| Final physical schema | Sign off exact principal binding, types, keys, CHECKs, indexes, composite null handling, and the retained Campaign FK gate. | DB Coordinator and M7 | PENDING |
| Controlled writer and isolation | Approve static EXECUTE-only routines, indexed protected seeks, guarded increment, ordered locks, bounded retry and primary commit verification. Approve report scope and consistent snapshot. | DB Coordinator and M7, M1 for authorization | PENDING |
| Shared migration ownership and deployment | Assign owner, order schema and routine rollout, review rollback and restore strategy, and require verification before any intake activation. | DB Coordinator and M7 | PENDING |
| Isolated SQL environment and cleanup | Approve disposable target and provisioner, verified server and per-run DB allowlists, opt-in, marker, account grants, rollback and guarded cleanup authority. | DB Coordinator | PENDING |

No implementation, migrations, data mutation or SQL execution is authorized by this register. Record approval with the reviewed commit, approver and exact decision scope before taking dependent implementation steps.

## Shayanthan review decisions

| Decision | Recommended candidate for review | Required approver | Status |
| --- | --- | --- | --- |
| Caller-account binding | Separate receipt-only algorithm version, key ID and HMAC-SHA-256 digest over the canonical M1 account and retry tuple. Verify ownership before payload equality or response. Preserve retry identity across refreshed sessions. | M1, M7, DB Coordinator and privacy/data owners | PENDING |
| Binding key custody and retention | Version rotation for new writes, historical verification lifetime, fail-closed behavior for missing keys and no duplicate reacceptance. Update privacy and capacity impact before adoption. | M1, privacy/data owners, DB Coordinator and M7 | PENDING |
| Replay, conflict and unavailable disclosure | Candidate 200 after confirmed commit, generic 403 before lookup, same 410 for expired credentials, and 503 for unresolved commit. Current signed v1/v2 does not make 409 reachable. Use the revised security matrix in the M1 review. | M1 and M7 | PENDING |
| Attempt and accepted quota | Attempt budget applies to retries before receipt access. A matching authorized retry consumes no accepted quota. Preserve open bucket version. Use 120/30 for load tests only. | M1, M7 and DB Coordinator | PENDING |
| Sponsored proof | Caller-bound, server-issued proof of actual delivery. Keep sponsored intake disabled until the proof and attribution relationship are approved. | Campaign owner, M1 and M7 | PENDING |

Shayanthan confirmed the authentication requirements. This review leaves production HTTP semantics, privacy governance and final SQL approval pending. [Full M1 review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md)

## Review decision provenance

Kaviyan acknowledged Shayanthan's M1 API review, with joint sign-off and final SQL approval pending. Shayanthan's separate privacy review declined unbounded receipt retention. The [privacy assessment](M7-MS07_T01_T03_PRIVACY_RETENTION_ASSESSMENT.md) records the decision, outstanding processing basis and consent rules, and the bounded admission blocker. This is not formal governance or implementation approval.

## Latest bounded admission decisions

| Decision | Required approval | Status |
| --- | --- | --- |
| HMAC canonical M1 identity, framing, dedicated key and version lifecycle | M1, M7, DB Coordinator and privacy/data owners | PENDING |
| Server-minted key, signed deadlines, globally non-reused allocation and restore/compromise fences | M1, M7 and DB Coordinator, Campaign owner for delivery contract | BLOCKED FOR DESIGN DECISION |
| Candidate 10-minute admission, 24-hour replay and generic 403/no-disclosure or expiry behavior | M1 and M7, with governance and coordinator review of timing and enforcement | PENDING |
| FK-compatible finite receipt lifecycle, archive/holds, key retirement and cleanup bounds | Privacy/Data Governance, M1, M7 and DB Coordinator, Team Leader coordinates | BLOCKED FOR DESIGN DECISION |

See the [Latest M1 API review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md) for candidate timelines, tradeoffs and tests, all NOT EXECUTED.

## Latest M1 deadline and storage candidates

| Decision | Required approval | Status |
| --- | --- | --- |
| 10-minute first acceptance and 24-hour replay from trusted UTC issuance | M1/M7 and DB Coordinator for time and transaction proof | PENDING |
| 365-day fact and 366-day receipt storage candidates measured from `AcceptedAtUtc`; receipt cleanup only after fact disposition and all FK/archive/recovery dependencies end | Privacy/Data Governance, M1, M7 and DB Coordinator | PENDING; guaranteed day-366 deletion BLOCKED because delayed cleanup, holds, recovery, restoration or remaining references may outlast the nominal one-day margin |
| Option A one-way clearing or option B retention of caller HMAC | M1, Privacy/Data Governance and DB Coordinator | PENDING |

[Deadline and FK lifecycle review](M7-MS07_T01_T03_BOUNDED_REPLAY_FK_REVIEW.md) records all boundary tests and the non-reissuance blocker. Campaign proof remains pending.

## Joint review decisions and provenance

- **Shayanthan, M1 IdentitySocial:** DIRECTION ACCEPTED FOR CONTINUED DESIGN REVIEW. Final joint sign-off remains BLOCKED on restore safety and exact HTTP response contract.
- **Kaviyan, Team Leader / DB Coordinator:** DOCUMENTATION CORRECTIONS ACCEPTED. FINAL DESIGN AND SQL APPROVAL PENDING.
- **Restore/reconciliation/fencing:** BLOCKED FOR DESIGN DECISION. Globally unique allocation does not prove a pre-restore committed acceptance remains represented after restore.
- **Exact expiry and unresolved-commit HTTP behavior:** PENDING M1/M7. DB Coordinator must approve logical linearization, physical commit visibility requirement, settlement bound and recovery behavior.
- **HTTP 409 conclusion:** ACCEPTED UNDER CURRENT SIGNED V1/V2 ASSUMPTIONS. It remains reserved and unreachable after validated tuple checks; the T01 prototype audit found no payload-digest conflict path. Full future/legacy entry-path inventory and final contract approval remain required.
- **Option A caller-binding clearing:** M1 SUPPORTS FOR REVIEW; Privacy/Data Governance and DB Coordinator approval PENDING.
- **Campaign delivery proof and attribution:** PENDING Campaign owner, M1 and M7 approval. Sponsored intake remains disabled.


### Latest M1 conditional review status

- M1 design direction — **ACCEPTED FOR CONTINUED REVIEW**; final M1 sign-off remains on hold.
- HTTP 409 conclusion — **ACCEPTED UNDER CURRENT SIGNED V1/V2 ASSUMPTIONS**; T01 prototype audit also found no payload field or event-ingestion HTTP route in the inspected commit. Full future/legacy path inventory remains required.
- Restore safety evidence — **BLOCKED**; allocator uniqueness does not prove a pre-restore committed receipt is represented.
- Issuer/non-reissuance contract — **PENDING APPROVAL**.
- HTTP exact precedence and response semantics — **PENDING APPROVAL**.
- DB Coordinator locking/settlement and physical SQL — **PENDING**.
- Privacy and retention — **PENDING**.
- Campaign attribution and sponsored delivery proof — **PENDING**.
- Security and isolated SQL/API tests — **NOT EXECUTED**.
The bounded replay review contains 46 cases, all SQL/API NOT EXECUTED. This register records owner direction, not final schema approval or implementation authorization.

## Latest DB Coordinator technical directions — recorded, not implementation approval

Kaviyan's latest review accepts these directions for continued design: restore-independent monotonic issuer/cohort fencing is the mandatory preferred safety foundation; authoritative reconciliation is required for committed acceptance history; the final guarded logical acceptance decision must be strictly before T0+10m; receipt, fact and accepted quota commit atomically; HTTP 200 follows authoritative commit confirmation; physical COMMIT visibility may follow the guarded decision only within a separately validated finite settlement period; ambiguous commits are reauthorized and verified with the original credential/key and never blindly reaccepted; primary SQL UTC is the candidate trusted deadline clock with issuer synchronization and health checks; preserve the composite NO ACTION fact-to-receipt FK and fact-first cleanup; Option A HMAC clearing is conditionally supported; and the 46-case matrix is accepted as future verification scope only. HTTP 409 remains reserved and unreachable under fully signed v1/v2 assumptions, conditional on entry-path verification.

| Approval or proof item | Status |
| --- | --- |
| Implemented/proven restore-independent fencing authority and enforcement at every replica and ingress | BLOCKED — none verified |
| Authoritative reconciliation after restore/failover, including R1 accepted at T0+2m then removed by a pre-commit snapshot before its credential expires | BLOCKED — no approved authority/evidence |
| Numeric transaction settlement maximum and logical-decision versus physical-COMMIT boundary | PENDING — no numeric bound approved |
| Maximum issuer/database clock uncertainty and synchronization/health thresholds | PENDING |
| Final M1/M7 admission credential, expiry precedence and HTTP contract | PENDING |
| Campaign DeliveryId and attribution contract | PENDING; sponsored intake disabled |
| Privacy/Data Governance, retention and Option A clearing approval | PENDING |
| Physical SQL, controlled maintenance writer and migration ownership | PENDING DB Coordinator approval |
| Disposable database provisioning and test execution authorization | PENDING; no SQL/API tests run |
| 46-case acceptance scope | ACCEPTED FOR FUTURE VERIFICATION; NOT EXECUTED |

These are design directions only. They do not prove recovery safety, approve physical implementation, or authorize tests or cleanup.
