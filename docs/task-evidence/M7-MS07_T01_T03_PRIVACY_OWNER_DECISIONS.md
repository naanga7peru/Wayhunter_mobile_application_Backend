# M7-MS07-T01/T03 privacy owner decisions

**Status** PENDING M1, DB Coordinator and formal governance sign-off. Kaviyan confirmed the review responsibilities. Conditional architecture/physical design acceptance does not confer privacy, retention, access or cleanup approval. [Assessment](M7-MS07_T01_T03_PRIVACY_RETENTION_ASSESSMENT.md)

| Decision | Recommended candidate and approval question | Responsible review | Status |
| --- | --- | --- | --- |
| Processing basis and disclosures | Approve distinct purposes and basis for measurement, dedup/quota, location targeting, historical analytics, access audit and export. Which notices, settings and withdrawal actions are required? | M7 proposes, M1 reviews, Team Leader coordinates designated governance sign-off | PENDING |
| Classification and allowed linkage | Review all 21 columns and related logs, backups, Campaign data and delivery linkage. Limit joins, access and sparse cohort disclosure. Which direct or inferred identity risks require stricter handling? | M1 and data owners, M7 | PENDING |
| Location consent lifecycle | Verify purpose-specific source and revocation freshness. Denied/revoked/unavailable/unknown excludes targeted delivery. Decide approved untargeted fallback and delayed click processing. | M1, M7 and Campaign owner | PENDING |
| Previously accepted events | Retain only under approved independent historical basis and purpose. Decide restriction, erasure and downstream export effects after revocation. | M1, M7 and formal governance | PENDING |
| Authenticated replay and reporting access | Approve trusted principal/session checks, admission binding, scope before disclosure, current revocation checks, anti-enumeration responses, distinct historical drilldown and CSV/PDF scopes. | M1 and M7 | PENDING |
| Fact retention and archived visibility | Compare 365 days to shorter online duration with restricted archives or approved aggregates. Approve dates, backup limits, coverage bounds and historical parity conditions. | M7 proposes, M1/data owners review, DB Coordinator verifies feasibility | PENDING |
| Receipt lifetime and replay safeguards | Compare initially unbounded receipts with durable minimal tombstones or non-reissuable time-bound admission. Approve minimum replay fields, binding version and immutable source recovery. Require a review date and growth budget before acceptance. | M1, M7, Campaign owner for delivery identity, DB Coordinator | PENDING |
| Quota partition and expiry | Keep ad/type scope without personal identifiers. Compare 48-hour grace with measured shorter retention. Approve independent attempt-limit data and shorter handling for any personal signals. | M7 proposes, M1 and DB Coordinator review | PENDING |
| Erasure workflow and exceptions | Approve intake identity verification, safe linkage trace, governed removal/restriction, downstream copies, recordings of approved exceptions and communications. Define who may authorize denial or deferral. | M1 and data owners, DB Coordinator for DB effects, Team Leader coordinates formal governance sign-off | PENDING |
| Cleanup and recovery | Approve expiry routines, account grants, lock order, cutoff rechecks, audit and restart, backups and disaster recovery. Validate no replay reacceptance after retention or restore. | DB Coordinator and M7, M1 for residual data | PENDING |
| Report completeness and export minimization | Approve coverage metadata, scoping, range limits, suppression thresholds, labels, and permitted CSV/PDF fields across complete, empty, partial, unavailable and unknown states. | M7, M1 and data owners | PENDING |
| Governance authorization | Record named approvers, reviewed SHA, effective rules, exceptions, review dates and escalation/monitoring owners before implementation. | Team Leader coordinates, with M1, DB Coordinator and designated governance/data owners | PENDING |

## Review checklist

**M1**

- [ ] Confirm approved processing purposes, basis, notice and consent requirements. Confirm separate authenticated access and current grant checks.
- [ ] Sign the column classification and linkage inventory, authorize disclosure and receipt behavior, and set revocation, erasure and export policy.
- [ ] Approve separate access audit handling, rare cohort and differencing controls, and backup and artifact retention.

**DB Coordinator**

- [ ] Approve storage shape, retention limits and monitoring. Test independent-connection replay and cleanup safety only after isolated execution approval.
- [ ] Review lock ordering, active-write cutoff, FK consequences, tombstone or receipt continuity and disaster/restore behavior.
- [ ] Approve controlled cleanup authority, accounts, isolation provisioning, procedural audit and explicit stop conditions.

**Team Leader**

- [ ] Coordinate decisions from M7, M1, DB Coordinator, Campaign owner and designated privacy/data governance approvers.
- [ ] Record sign-off and decision scope against the review commit before any implementation or cleanup.
- [ ] Resolve open approval ownership for processing basis and exceptions, follow-up review dates and go-live gates.

This documentation does not approve SQL execution, cleanup, production ingestion or any final governance decision.
