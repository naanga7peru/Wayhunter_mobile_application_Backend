# M7-MS07-T01/T03 privacy and retention assessment

**Status** PROPOSED. M1, DB Coordinator and formal governance approval are PENDING. M7 proposes the rules, M1 reviews consent and personal data and authenticated access, the DB Coordinator reviews storage and cleanup safety, and the Team Leader coordinates formal sign-off. This assessment authorizes no ingestion, cleanup or erasure execution. [Owner decisions](M7-MS07_T01_T03_PRIVACY_OWNER_DECISIONS.md)

## Verified context

Review baseline `abda0a96693fef0fbaf82bcc4f11dafefda97f22`. Fetched develop `ac7e790572b45b57594800a33cd991da977253ae` added two M3 evidence documents since `77d1f57`, with no overlap with the M7 proposal, source, shared schema or identity policies. No merge or rebase was used for this assessment. The inventory follows the exact three-table candidate in the [final SQL artifacts](M7-MS07_T01_T03_FINAL_SQL_REVIEW_ARTIFACTS.md) and [physical review package](M7-MS07_T01_T03_PHYSICAL_SCHEMA_REVIEW_PACKAGE.md).

At the fetched baseline, `src/Wayhunter.Domain/Modules/IdentitySocial/Entities/IdentityConsent.cs` records consent by subject and purpose key, with UTC grant and revocation and an active interval ending at revocation. The M1/M3 GPS review requires separate explicit consent for sharing and stops future sharing on revoke. The code in `src/Wayhunter.Api/Authorization/AdvertisementAdministratorPolicy.cs` protects existing Advertisement Admin operations and requires a trusted authenticated Admin role. Report and export scopes still need approval. [GPS consent evidence](M1_M3_AUTH_GPS_CONTRACT_REVIEW.md)

In the local integration harness at `d5e0c88f97895c206f8b76f6e40c17ea40836a47`, T01's `IAdvertisementEventStore` accepts a stable scoped retry key, T03's `AdvertisementAuditTraceService` gates committed facts by principal and scope, and T02's `AdvertisementReportingReconciliation` compares complete same-scope, same-checkpoint inputs by convention. T04's `AdvertisementDistrictEligibilityPolicy` requires current trusted district authorization. The harness uses synthetic values and supplies no production privacy, SQL or access-control evidence.

## Column inventory and proposed access

Classification describes the system context. **Pseudonymous** means a stable or correlatable reference whose association can disclose an individual's activity if combined with likely auxiliary data. **Operational** covers controls or metadata with no direct subject identifier. **Non-personal in isolation** covers a business reference or code that can contribute to identification in context. Treat rows and small cohorts as potentially personal wherever linkage is plausible. No proposed field directly names a user. Classification and each column's access, retention and minimization treatment await **M1 approval**.

Access codes in each row are **W** controlled writer or replay verifier, **G** scoped reporting routine reading facts for aggregates, **A** separately authorized audit routine and its scoped output, and **C** coordinator-approved cleanup routine for expiry/key checks only. Calls are mediated through the approved service identity and scoped authorization. Business users receive no direct table access. W inserts immutable facts and receipts, and inserts or increments buckets. C may delete eligible rows only after separate approval. Facts and receipts receive no routine UPDATE permissions. Verification and cleanup operators receive no unrestricted SQL reads. No cleanup grant is implied for receipts.

### AdvertisementAcceptedEvents

| Column and purpose | Class and linkage / impact | Readers and writer | Retention | Minimization and M1 status |
| --- | --- | --- | --- | --- |
| SourceEventId, stable server identity for trace | Pseudonymous event reference. Joining receipts, delivery systems or support records can reveal activity. | W/G/A, C for keyed expiry. W inserts. | 365 days candidate | Never include in dashboards or routine CSV/PDF. Scope audit and any caller receipt disclosure. PENDING |
| AdvertisementId, ad FK and report dimension | Non-personal business key in isolation. Ad ownership or low-volume delivery data can identify a person. | W/G/A; C for keyed expiry. W inserts. | 365 days candidate | Use for grouping and scope. Export an approved label or dimension only. PENDING |
| EventType, impression or click | Non-personal code in isolation. Joined with time and delivery it reveals viewing or engagement. | W/G/A; C for keyed expiry. W inserts. | 365 days candidate | Restrict to two enum codes and aggregate separately. PENDING |
| AcceptedAtUtc, server receipt time for accepted facts | Operational timestamp with strong linkage risk to logs and sparse delivery. | W/G/A/C. W inserts. | 365 days candidate | Keep exact time internally for reconciliation and bounded audit. Use approved coarser exports and avoid time joins. PENDING |
| CampaignId, optional immutable Campaign FK context | Non-personal business key in isolation. Sponsor or delivery joins can expose personal or cohort information. | W/G/A; C for eligibility checks only where required. W inserts. | 365 days candidate | Nullable only for approved unattributed flow. Enforce historical scope, omit from routine exports unless specifically allowed. PENDING |
| CampaignBindingId, FK consistency across nullable attribution | Operational duplicate encoding of CampaignId with null sentinel, subject to the same linkage. | W only and internal constraints. W inserts. | 365 days candidate | Keep inside storage. Never emit the sentinel or the duplicate field. PENDING |

### AdvertisementEventRetryKeys

| Column and purpose | Class and linkage / impact | Readers and writer | Retention | Minimization and M1 status |
| --- | --- | --- | --- | --- |
| AdvertisementId, composite key and ad reference | Non-personal in isolation. Combines with retry identity and delivery history. | W; C for fact expiry under its receipt key. W inserts. | Finite policy BLOCKED | Use for exact lookup and scope. Exclude receipts from reporting. PENDING |
| EventType, independent click and impression retry scope | Non-personal code in isolation. Reveals engagement when linked. | W; C for fact expiry under its receipt key. W inserts. | Finite policy BLOCKED | Constrain to two codes. Omit from routine logs. PENDING |
| RetryEventId, caller-supplied opaque key | Pseudonymous. Can correlate client traces or other records. A GUID is not proof of anonymity. | W; C for fact expiry locking only. W inserts. | Finite policy BLOCKED | Require opaque generated keys, no subject or device ID substitution and no logging/export. PENDING |
| SourceEventId, commit witness and stable replay ID | Pseudonymous join to accepted history and support records, including after fact expiry. | W; C for fact lookup only. W inserts. | Finite policy BLOCKED | Keep exact for recovery. Candidate external disclosure only after scoped authorization. PENDING |
| AcceptedAtUtc, original receipt time | Operational with activity and timing linkage. | W; C where needed for expiry verification. W inserts. | Finite policy BLOCKED | Preserve minimum replay semantics and restrict fine time exposure. PENDING |
| CampaignId, stable historical attribution | Non-personal business key in isolation. Sparse Campaigns or delivery joins may identify people. | W only and internal FK checks. W inserts. | Finite policy BLOCKED | Restore from receipt. Keep internal unless explicit disclosure approval. PENDING |
| CampaignBindingId, normalized attribution FK component | Operational duplicate with same Campaign linkage. | W and internal constraints. W inserts. | Finite policy BLOCKED | Internal only, never rendered or logged. PENDING |
| BindingVersion, canonical identity format | Operational configuration identifier. Reveals protocol generation when joined, with little direct subject linkage. | W only. W inserts. | Finite policy BLOCKED | Small enumerated version. No telemetry tie to user. PENDING |
| BindingDigest, same-key payload commitment | Pseudonymous linkable fingerprint. In v2 includes a stable delivery reference in the hashed input. SHA-256 does not anonymize known or enumerable inputs. | W only. W inserts. | Finite policy BLOCKED | Store digest and version only, no raw token or delivery ID. No exports, logs or open digest lookup. PENDING |

### AdvertisementEventQuotaWindows

| Column and purpose | Class and linkage / impact | Readers and writer | Retention | Minimization and M1 status |
| --- | --- | --- | --- | --- |
| PartitionKey, hashed ad/type scope | Operational, linkable to enumerated known ad/type combinations. No user scope in candidate. | W/C. W inserts. | 48 hours after window end candidate | Domain-separate from retry digest. Prohibit IP, user/device identifiers or wider joins without approval. PENDING |
| WindowStartUtc, bucket identity | Operational time. Can correlate sparse delivery and acceptance activity. | W/C. W inserts. | 48 hours after end candidate | Fixed UTC bucket rather than per-request detail. Keep out of reports. PENDING |
| WindowEndUtc, boundary and expiry | Operational time. Reveals matching activity interval when joined. | W/C. W inserts. | 48 hours after end candidate | Store only required bound. Keep cutoff checks internal. PENDING |
| PolicyVersion, effective rate configuration | Operational. Indicates configured behavior, with no direct user linkage. | W/C when checking expiry compatibility. W inserts. | 48 hours after end candidate | Version number only, no policy secrets. PENDING |
| AcceptedCount, consumption of accepted quota | Operational aggregate. A low bucket may reveal single activity when paired with delivery observations. | W/C for controlled maintenance. W increments. | 48 hours after end candidate | Do not use for dashboard counts or expose per-bucket values. PENDING |
| AcceptedLimit, stored capacity | Operational configuration, with no direct user identifier. | W only. W inserts. | 48 hours after end candidate | Preserve immutable capacity per bucket. Keep it internal. PENDING |

The proposed fact, receipt and quota schemas exclude raw GPS, district observations, IP addresses, OTPs, auth tokens, session identifiers and raw request payloads. Keep those out of trace/export and diagnostic logs. Any admission proof, consent records and authentication material stay in their approved owner boundaries. Minimize DMV traces, backups, caches and observability copies as well as live tables. Restrict joins to approved purposes, review small cohorts and repeated filters for differencing, and log report access with purpose and scope in a separately approved protected audit trail.

## Consent, processing purpose and authenticated access

Each purpose needs an approved processing basis, notice where required and defined revocation behavior. Decisions below are **PENDING**. Authentication proves identity under the approved session model. It confers neither consent nor analytics, district or export permission.

| Processing activity | Candidate boundary and pending decision |
| --- | --- |
| Impression/click measurement and deduplicated reporting | Assess purpose for both collection and downstream analytics, distinguish essential operational dedup from optional analytics, establish applicable basis, disclosure and user controls. Use only coarse facts. M1 and governance must approve. |
| Location-enabled targeting | Require purpose-specific current authorized location source before deriving a district. Trip GPS consent does not carry over. Unknown, denied, revoked or unavailable yields no targeted delivery and no fallback inference. Location-free untargeted behavior needs separate approval. |
| Measurement after a location change | Generic analytics has no inherent district input. Whether a previously delivered impression or delayed click remains admissible after revocation requires M1 and Campaign owner approval. Fail closed for flows requiring a currently valid grant until defined. |
| Historical accepted event after revocation | Stop future processing that depended on the revoked permission and invalidate any district cache. Candidate preserve already committed facts only while an independently approved historical basis and retention period apply. Until decided, do not imply automatic retention, automatic erasure or further export. Route for privacy review and restrict access where necessary. |
| Abuse prevention and ambiguous-commit recovery | Assess the separate operational purpose, least data and retention for receipts and quotas. An ad/type partition excludes personal identifiers. Any user/device/IP partition needs separate review. |
| Drilldown, access audit and CSV/PDF | Approve distinct aggregate, export and source-record scopes, campaign/district restrictions, access audit retention, artifact handling, disclosure thresholds and restrictions on onward use. Treat export as a separate disclosure decision. |

A trusted application must validate current token and session through M1's approved mechanism, reject untrusted or stale claims and derive ad/purpose and report scope server-side. Validate current role/permission and historical data scope again for exports and drilldown. Match authorization, UTC window, filters and snapshot before reading or rendering. An ingestion caller cannot access another caller's receipt from a guessed GUID. Replay authentication and receipt disclosure must bind to approved admission context, with a generic unauthorized or conflict response that gives no original record details. Audit the decision and avoid recording raw credentials or retry inputs. Proposed SQL roles and routines are separate from user authentication.

## Retention comparison and capacity scenarios

| Candidate | Purpose and shorter-retention risk | Longer-retention risk and alternatives | Approval |
| --- | --- | --- | --- |
| Facts, 365 days online | Year-scale trend comparison and historical review. Shorter retention breaks audit/reconciliation for older windows unless a verified archive preserves evidence. | Fine-grained time and Campaign linkage remain searchable. Compare 90 or 180 days plus restricted archive or independently approved non-identifying aggregates. Aggregate archives alone cannot support source drilldown. | M1, DB Coordinator, M7 and governance, PENDING |
| Receipts, unbounded option DECLINED BY M1 | A retained key prevents retries from counting again and preserves replay/commit identity. Time-only deletion is unsafe for opaque keys. | Unbounded growth retains correlatable metadata and pins principal records indefinitely. M1 declined this option. Evaluate only bounded candidates with a verified non-reissuance gate. Implementation remains blocked. | M1, DB Coordinator, M7 and governance, PENDING |
| Quota windows, 48 hours after UTC end, conditionally acceptable to M1 | Covers the proposed ten-minute write lifetime and an operational recovery margin. Shortening it could let delayed writers recreate a bucket and exceed quota unless guarded. | Retains small time buckets longer than needed and creates avoidable churn. Compare shorter measured grace after proving transaction deadlines, clock and cleanup behavior. | DB Coordinator, M1 and M7, PENDING |

**Illustrative estimates, not measured data.** Assume 160 bytes per stored fact, 240 bytes per receipt, and 128 bytes per quota row, including rough row/index overhead. Assume each accepted event creates one fact and receipt, no compression, constant daily volume, no historical purge for receipts, and two completed days of buckets plus any current partial day. Decimal MB/GB. These estimates exclude logs, backups, archives, replication, free space and page fragmentation. Benchmark actual indexes, write rate, row sizes and backup growth on an approved disposable target before capacity approval.

| Scenario | Assumptions | 365-day facts | Receipt growth each year | Two completed days of buckets |
| --- | --- | ---: | ---: | ---: |
| Small prototype | 10,000 accepted/day and 1,000 bucket rows/day | 3.65M rows, 584 MB | 3.65M rows, 876 MB | 2,000 rows, 0.256 MB |
| Larger workload | 1,000,000 accepted/day and 100,000 bucket rows/day | 365M rows, 58.4 GB | 365M rows, 87.6 GB | 200,000 rows, 25.6 MB |

Receipts accumulate linearly under these assumptions. Rejected or duplicate attempts contribute no new fact, receipt or committed quota increment. Include a separate capacity and retention check for approved telemetry and archives. Before cleanup, validate target identity, restrict execution to approved routines, verify the clock and write cutoff under the quota lock, and acquire receipt then fact locks in acceptance-compatible order. Recheck conditions after waits, use bounded batches, audit outcomes and preserve safe restart after failure. Never purge a receipt still referenced by a fact or grant deletion authority through this assessment.

## Erasure and historical deletion decision matrix

All handling is **PROPOSED/PENDING**. No exception, refusal, data modification or historical deletion is approved.

| Request or case | Candidate treatment | Evidence, decision and impact |
| --- | --- | --- |
| Identifiable user's request | Verify requester using M1's approved process, record purpose and scope in the protected request case, discover approved linkages, and prevent further disclosure while reviewed where appropriate. | Avoid collecting new crosswalks or copying personal identifiers into M7. M1 and governance decide scope and response. |
| Event with no demonstrable link | Determine whether identification is reasonably possible using approved data available to the team. Preserve only if the approved classification and retention rules allow. | A GUID alone does not prove anonymization. Explain coverage and inability to isolate a row through the approved process. |
| Indirectly linked facts | Use minimum authorized evidence to identify affected scope, assess downstream copies and consider deletion, restriction or unlinking under approved procedure. | An append-only event cannot be edited by routine writers. Any approved erasure is a governed operation. Reassess report completeness and cached/exported results. |
| Retry receipts needed for deduplication | Evaluate whether a restricted tombstone or trustworthy non-reissuable admission policy can retain safety with less data. | If removing a receipt would permit reacceptance, do not choose blind expiry. Escalate the design and proposed response to M1, DB Coordinator and governance for a scoped decision. |
| Historical Campaign attribution | Preserve attribution consistency during routine operations. Review linkability and any approved archive, retained principal or erasure path. | Never update an old fact to the current Campaign. Restricted FKs prevent accidental cascading removal. |
| Audit records | Review separate audit purpose and retention and whether a minimized protected record must remain. | Do not propagate identifiers into the review log. Restrict access and account for backup and export copies in the decision. |
| Proposed legal or operational exception | Require formal approval for its basis, necessary fields, access restrictions, deadline and review owner. | No assumed exception. Document decision and limits in the approved record. |
| Denial or deferred deletion | Use only with a recorded approved exception, explanation and review date. Route communication through the authorized owner. | Minimize disclosure, review continued necessity and apply any approved restriction while pending. |
| Totals and historical completeness | Track approved coverage impact of removal or suppression. Rebuild permitted rollups or mark affected slices incomplete. | A verified zero is reserved for a complete authorized slice. Preserve a non-identifying governance/coverage record where approved. |

## Replay, archive and retention invariants

- On first acceptance persist the unique `(AdvertisementId, EventType, RetryEventId)`, pinned binding version and digest, and immutable `SourceEventId`, `AcceptedAtUtc` and nullable Campaign attribution in the receipt in the same commit as fact and quota. Do not count verified replays or conflicts. Always check the exact retained key before current Campaign or quota.
- After archival or fact expiry, the retained receipt still restores original fields and resolves matching Duplicate versus conflict. No fact is reconstructed for reporting. Mutable current Campaign or consent state cannot rewrite the acceptance record. Authorization for disclosure is evaluated anew.
- Receipt expiry is **blocked** for opaque retry GUIDs. For a future minimal durable tombstone, retain the composite key or a reviewed collision-safe equivalent and pinned binding commitment to retain conflict detection. Keep the stable source, time and attribution fields if exact authorized replay or historical recovery remains required. A tombstone lacking those fields must produce an approved replay/expired-receipt disposition with no fresh acceptance and no invented response. It cannot satisfy exact-response restoration.
- A fully bounded key lifetime requires a trusted non-reissuable admission binding to that retry scope, a server-enforced expiry for every path, version and replacement proof, and explicit handling of in-flight and ambiguous commits. Proof expiry alone is insufficient if a new valid proof can reuse the old opaque key. Retain markers through any overlap, application retries, proposal recovery grace and backup/restore reconciliation. Never declare a key free merely because a read or archive is missing.
- Ambiguous commit recovery checks the authoritative committed receipt or approved marker after the key lock settles. Confirm only a matching identity and source. If history has moved to a tombstone that cannot prove the exact outcome, return the approved indeterminate disposition without a fresh increment. Unknown coverage or inaccessible storage blocks new acceptance for affected keys until recovery. Keep backups and disaster recovery aligned with replay markers so rollback of retention state cannot re-enable accepted keys.

## Historical reporting completeness and disclosure

Coverage is computed for the same authorized population, ad/Campaign/district filters, half-open UTC window, retention coverage and checkpoint used for totals and trace. No district dimension goes live without M1 and M7 approval. Render status and coverage bounds in both formats, enforce max dates/rows and coarse aggregation, and authorize CSV/PDF independently. Minimize detail for small cohorts and protect exports from recovery caches, links, long storage and unauthorized sharing. Do not expose precise timestamps, internal source IDs or Campaign details by default. Drilldown needs an explicit historical scope at every read, a bounded window, audit logging and stable pagination. Proposed export gates and labels remain subject to approval.

| State | Candidate behavior |
| --- | --- |
| Complete | Authorized source and counts cover the entire request at one verified snapshot. Report totals with approved filters and scope. |
| Empty but complete | Verified complete population contains zero accepted events. Report genuine zero counts. |
| Partially retained | Known gaps due to retention, erasure or archive availability. Label verified subranges and partial counts only if approved. Suppress whole-window parity claims. |
| Unavailable | Reader, archive or dependency cannot serve the authorized slice. Display no verified totals. Access denial is a separate result and reveals no coverage. |
| Coverage unknown | No trustworthy completeness metadata, inconsistent checkpoint or unverifiable archive boundary. Suppress authoritative totals and drilldown. |

Previously recorded application tests are prior evidence only. This assessment adds no code, migration, SQL procedure, cleanup or production verification.

## Shayanthan’s privacy and consent review

**Decision provenance** This section records the separate review from Shayanthan (M1), as relayed in the task authorization. M1 declined unbounded retry-receipt retention, left 365-day fact retention PENDING, and found 48-hour quota cleanup CONDITIONALLY ACCEPTABLE. Kaviyan acknowledged the M1 contract review with joint sign-off and final SQL approval pending. Formal Privacy/Data Governance approval remains pending for each processing purpose, lawful basis, retention, disclosure and erasure policy.

| Item | M1 review decision |
| --- | --- |
| Accepted-event facts | 365 days — PENDING governance approval. |
| Retry receipts | UNBOUNDED RETENTION DECLINED BY M1. |
| Quota windows | 48-hour cleanup after window end — CONDITIONALLY ACCEPTABLE, not final. |
| Historical deletion | NOT AUTHORIZED. |

- Require a purpose-specific pre-acceptance consent or other governance-approved processing-basis check wherever required. Repeat it at the point of acceptance under the approved freshness and revocation rules. Login does not grant consent. Withdrawal stops new collection and use that relied on the withdrawn permission, invalidates derived district cache, and blocks future targeted delivery. Delayed events and previously accepted facts require a separately approved historical basis and disclosure policy. No automatic erasure or indefinite retention follows.
- Apply M1-confirmed active verified account and valid session requirements to ingestion and replay. Account deactivation or authorization revocation blocks future intake and receipt disclosure. It does not recycle the composite key. Handle erasure through the governed matrix above. Record only approved exceptions and deadlines, with controlled access and explicit review.
- Recommend aggregate-only dashboard and CSV/PDF reporting with scoped, allowlisted dimensions, minimum cohort sizes and differencing checks. Raw audit is a separate narrowly scoped exception requiring explicit authorization and access audit. Receipts are restricted to controlled acceptance, verification and approved cleanup. Caller binding, full payload digest, raw identifiers and credentials do not enter facts or exports.
- Apply expiry and access rules across online facts, archives, stored or downloaded exports, caches and backups. Approve archive visibility, export lifetime and downstream deletion responsibilities. Backups age under a separate approved schedule. Before any restore reopens ingestion or reporting, reconcile approved erasure, suppression, expiry and replay state. Never restore stale authorization or reopen an expired identity. Authorized archival or deletion can create gaps even when totals remain available. Mark affected slices incomplete and reserve verified zero for proven complete coverage.

| Proposed receipt column | Classification, linkage and controls | Access and retention |
| --- | --- | --- |
| CallerBindingAlgorithmVersion | Operational algorithm/framing version. Keep internal and separate from payload version. | Controlled writer and verifier only. Finite receipt lifecycle PENDING. |
| CallerBindingKeyId | Operational opaque key selector. May reveal verification-key cohorts if joined. Contains no key material. | Controlled writer and verifier only. Key custody and retirement PENDING. |
| CallerBindingDigest | Pseudonymous personal data. A stable keyed account reference permits correlation and remains identifiable with approved secret and identity access. Keep separate from the payload digest. Never log, export or copy to facts. | Controlled writer and verifier only, with narrowly scoped key custody and version lookup. Same unapproved finite receipt lifecycle, including after archival. M1/privacy approval PENDING. |

The earlier 21-column inventory describes the three-table physical baseline. These three proposed additions extend the inventory to 24 columns across the three tables. A 2-byte algorithm version, 16-byte key ID and 32-byte digest add 50 raw bytes per receipt before page/index overhead. Re-estimate capacity after approval. The earlier unbounded-growth scenarios quantify the declined option only.

## Bounded receipt retention — BLOCKED FOR DESIGN DECISION

Under the current contract the client controls RetryEventId. Once all durable memory of a composite key disappears, a matching submission is indistinguishable from a first use. A fresh delivery proof can make that old key look newly eligible. Proof expiry, a finite archive, quota rollover and an arbitrary receipt lifetime cannot establish replay safety. Keep production implementation and receipt cleanup blocked while the admission contract remains unresolved.

**M7 proposal for review:** Keep the same composite PK, but consider trusted issuance of RetryEventId as part of an authenticated, server-issued admission identity for every intake path. Bind the permitted GUID, AdvertisementId, EventType, stable M1 account and stable approved delivery/payload identity to an immutable server-issued validity interval and version. Enforce expiry on the missing-key path after taking the retry-key lock, using trusted DB UTC and explicit skew and retry bounds. Renewal may restore access to the same identity without extending its original acceptance deadline. A replacement identity for a genuinely new event must use a new, demonstrably non-reused server-issued key. The issuing service must refuse any caller-supplied replacement key and prove it cannot sign the expired tuple again with a new deadline, including across versions and disaster recovery. Do not accept a legacy caller-chosen ID for any post-retention path. Merely signing a caller-supplied GUID or expiring only the delivery proof does not meet this condition.

| Scenario | Boundary required for candidate safety |
| --- | --- |
| Same retry key after receipt expiry | An expired identity cannot enter fresh acceptance. If it cannot be authenticated or the original response is gone, use approved generic expiry or indeterminate behavior. Never recreate the fact. |
| Same retry key and different delivery proof | The authority must refuse to bind a second delivery identity to the old key. While retained, the owned receipt detects payload conflict. Once absent, mismatched or expired proof cannot admit it. |
| Lost acknowledgement | Within approved replay coverage, retain complete receipt fields and verify the exact locked key on the primary. A missing receipt or marker with uncertain history cannot be treated as fresh. After coverage ends, the approved expiry response must avoid claiming either acceptance or failure. |
| Delayed retry | Recheck the original admission deadline under the retry-key lock for new acceptance. Bound in-flight and recovery time. No expiry extension through repeated attempts or a policy change. |
| Account deactivation | Fail before receipt disclosure or acceptance. Restoration of the account does not renew old admission or authorize disclosure without current checks. |
| Historical fact archival | Preserve the receipt for all active fact FK references and the approved recovery horizon. An archive must preserve its integrity and access restrictions. Reporting visibility is a separate gate. |
| Concurrent cleanup | Cleanup and admission must serialize on the retry key and recheck deadline, dependencies and settled commits after locks. Retire markers only after all versions, writers, in-flight work, restore paths and required replay coverage are fenced. |
| Key-version rollover | Re-verification and issuer rotation preserve the original expiry and key identity. Missing verification material fails closed. Disable legacy bypass. Approve verification-key lifetime and recovery before changing versions. |
| Multiple sessions | Reauthentication to the same active account preserves ownership and original identity. A new session grants no new acceptance period. |

**Finite tombstone alternative:** After governed fact expiry and FK review, a minimal marker could bridge only the approved uncertainty and retry horizon. Keep the composite key or a collision-safe equivalent, minimal versioned ownership and payload commitments where required for conflict checks, and the verified deadline. The key and commitments are pseudonymous linkable data. Restrict access to the resolver and authorized cleanup. A marker that omits source ID, time and attribution cannot restore the original accepted body. Keep the complete receipt where exact replay is required. Set the finite marker cutoff only after proving durable non-reissuance, authoritative deadline, bounded settle/recovery time, approved skew, archive/FK release and restore handling. Eventual marker deletion must leave the tuple permanently inadmissible. An indefinite marker merely shifts the rejected retention problem, and a marker with finite expiry alone is unsafe. No duration is approved or proposed arbitrarily.

**Decision gate:** M1 and M7 must agree on whether changing issuance is feasible and on exact replay/expiry responses. Campaign must approve proof format and attribution. DB Coordinator must verify FK, transaction, cleanup and restore constraints. Formal governance must approve the purpose and maximum retained data at each stage. Until those conditions are proven, bounded receipt retention is BLOCKED FOR DESIGN DECISION.

## Latest bounded replay and HMAC assessment

[Latest M1 API review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md) refines the finite candidate. Proposed replay disclosure expires independently of stored receipt and fact lifetime. A 10-minute first admission and 24-hour replay are review candidates, with no duration approved. The retained HMAC is pseudonymous personal data. Key loss, compromise, backup restore, account deactivation, consent withdrawal and erasure must never reopen intake or disclosure. Hold and archival dependencies require a governed finite retention decision. Unbounded receipts remain declined. The 365-day fact proposal is pending and 48-hour quota cleanup is conditionally acceptable. No historical deletion is authorized.

## Latest M1 storage and digest candidates

[Deadline and FK lifecycle review](M7-MS07_T01_T03_BOUNDED_REPLAY_FK_REVIEW.md) records 366-day receipt storage as PENDING alongside 365-day facts and conditionally acceptable 48-hour quota cleanup. Review early clearing of caller digest and key ID after replay and required recovery close, while retaining the FK identity fields. This is pseudonymous data. Governance must approve any maintenance exception, erasure workflow, backup aging and audit scope. No historical deletion is authorized.
