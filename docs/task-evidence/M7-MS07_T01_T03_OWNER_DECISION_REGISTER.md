# M7-MS07-T01/T03 owner decision register

**Decision basis** Physical schema design conditionally accepted at `239bc059bebd6dfdb2e51bc4d7e8030dc0bd3819`. Architectural acceptance and conditional design acceptance leave the following implementation decisions open. [Final SQL review artifacts](M7-MS07_T01_T03_FINAL_SQL_REVIEW_ARTIFACTS.md)

| Decision | Recommended candidate for review | Required approver | Status |
| --- | --- | --- | --- |
| Admission authentication and retry identity | Server authenticates and authorizes before accepting or disclosing replay. Scope identity to ad/type/retry GUID. Permit v1 only for an explicitly approved unattributed flow. | M1 and M7 | PENDING |
| Binding versions and conflict | Domain-separated SHA-256 over canonical RFC-order GUIDs, big-endian version and event type, and validated stable delivery GUID in v2. Pin version on the receipt. Reject conflicting reuse without a write. | M1 and M7, Campaign owner for v2 | PENDING |
| Original receipt and external behavior | Preserve current internal Duplicate semantics. Candidate exact stable HTTP 200 replay after authorization, generic 409 conflict, 404 unavailable, 429 at quota and 503 for unresolved commit. Approve exposure of source ID/time, any attribution fields and retry guidance. | M1 and M7 | PENDING |
| Quota and attempt limits | Nonpersonal ad/type SHA-256 partition, 60-second UTC buckets, candidate 120 impressions and 30 clicks, persisted configuration per bucket and boundary activation. Ten-minute candidate write age. Define separate attempt controls and review capacity. | M1, M7 and DB Coordinator, privacy owner for any expanded scope | PENDING |
| Cardinality and authoritative attribution | Decide actual ad relation and what proves the Campaign shown. Prefer a stable server-issued delivery reference where a current link is ambiguous. Specify format, caller binding, validity and whether eligibility is rechecked. | Campaign owner and M7, M1 for proof authentication | PENDING |
| Historical Campaign principal | Nullable only for approved unattributed flows. Retain approved unique principal for receipt FK, restrict hard delete, and preserve snapshot through archive or unlink. | Campaign owner, M7 and DB Coordinator | PENDING |
| Fact retention and coverage | Candidate 365 days online, with explicit archive or incomplete coverage semantics and scoped historical audit. | Privacy/data owners, M7 and DB Coordinator | PENDING |
| Receipt and tombstones | Unbounded initial receipts and no separate tombstones until trustworthy future admission can prevent reuse after expiry. Review retained attribution and principal lifetimes. | Privacy/data owners, M7 and DB Coordinator | PENDING |
| Quota retention and cleanup | Candidate 48-hour grace after window end. Recheck under the same key lock. No deletion authority until separately approved. | Privacy/data owners and DB Coordinator | PENDING |
| Final physical schema | Sign off exact principal binding, types, keys, CHECKs, indexes, composite null handling, and the retained Campaign FK gate. | DB Coordinator and M7 | PENDING |
| Controlled writer and isolation | Approve static EXECUTE-only routines, indexed protected seeks, guarded increment, ordered locks, bounded retry and primary commit verification. Approve report scope and consistent snapshot. | DB Coordinator and M7, M1 for authorization | PENDING |
| Shared migration ownership and deployment | Assign owner, order schema and routine rollout, review rollback and restore strategy, and require verification before any intake activation. | DB Coordinator and M7 | PENDING |
| Isolated SQL environment and cleanup | Approve disposable target and provisioner, verified server and per-run DB allowlists, opt-in, marker, account grants, rollback and guarded cleanup authority. | DB Coordinator | PENDING |

No implementation, migrations, data mutation or SQL execution is authorized by this register. Record approval with the reviewed commit, approver and exact decision scope before taking dependent implementation steps.
