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
| Receipt lifetime and replay safeguards | M1 declined unbounded receipts. Bounded lifecycle is blocked for design decision until non-reissuable admission or equivalent safety is proven. Approve minimum replay fields, marker expiry and cleanup. | M1, M7, Campaign owner, DB Coordinator and formal governance | BLOCKED FOR DESIGN DECISION |
| Quota partition and expiry | Keep ad/type scope without personal identifiers. M1 finds 48-hour cleanup conditionally acceptable. Approve safe cutoff, actual write bounds and cleanup before use. | M7, M1, DB Coordinator and governance | PENDING final approval |
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

## Latest M1 ownership and expiry gates

- [ ] Approve purpose-specific HMAC and verification-key retirement, signed admission identity, privacy classification and generic foreign-caller behavior.
- [ ] Approve separate first-admission and replay deadlines and a finite FK-compatible receipt lifecycle covering archival, holds and restore. Prove eventual deletion cannot revive a key with new proof. This stays BLOCKED FOR DESIGN DECISION.
- [ ] Review the [Latest M1 API review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md) candidate durations, evidence requirements, consent withdrawal and erasure tests before formal sign-off. No SQL tests have been executed.

## Latest deadline and FK approvals

- [ ] Review 365-day facts and 366-day receipts as pending candidates and approve the separate 10-minute first-acceptance and 24-hour disclosure boundaries.
- [ ] Decide whether post-replay digest clearing, key retirement and archived-copy handling satisfy minimization and erasure obligations. Approve any controlled mutation exception before use.
- [ ] Confirm the non-reissuance and FK/cleanup evidence in the [Deadline and FK lifecycle review](M7-MS07_T01_T03_BOUNDED_REPLAY_FK_REVIEW.md). All SQL and API cases remain NOT EXECUTED.
