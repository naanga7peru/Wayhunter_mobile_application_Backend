# M7-MS07-T01/T03 physical schema review package

**Decision record** Kaviyan reviewed `c20ed555914fe7dcad0e9d3f2306c7b8e0d5127c`. **ARCHITECTURE ACCEPTED — FINAL PHYSICAL SCHEMA APPROVAL PENDING CORRECTIONS.** This package proposes those corrections. It authorizes no migration, historical deletion or database execution.

## 1. Executive decision summary

Preserve the accepted three-table architecture, composite retry uniqueness, immutable historical attribution, atomic event and quota acceptance, rollback, receipt-based ambiguous-commit recovery, and reporting from committed accepted facts. The proposal below adds versioned payload binding and stable response fields to the receipt, enforces fact/receipt consistency using a non-null composite FK, and chooses a quota design that cannot reset capacity on configuration rollover. These physical choices remain **PROPOSED**.

**CODE_VERIFIED baseline:** The existing EF mapping uses an application-assigned Advertisement GUID and named SQL constraints. T01 accepts ad/type/retry ID, T03 separates source ID and historical attribution, T02 compares complete per-ad snapshots, and T04 requires an independently authorized district context. The combined harness exercises those contracts with synthetic IDs and an in-memory lock. The local Campaign design at `10c8812` has a GUID `Id`, polymorphic `ContentId`, optional district text, schedule and lifecycle. It establishes no SQL Campaign-to-Advertisement relationship. [Original architecture proposal](M7-MS07_T01_T03_SQL_PERSISTENCE_REVIEW_PROPOSAL.md)

## 2. Physical tables, constraints and FK graph

Proposed schema namespace is `dbo`. Confirm the existing Advertisement schema and any Campaign principal before mapping. All columns are explicit, with no SQL defaults or identity generators. `Z` below is the all-zero GUID. For every column designated UTC, its named CHECK requires `DATEPART(TZOFFSET, column) = 0` and `column > CONVERT(datetimeoffset(7), '0001-01-01T00:00:00+00:00')`. Each GUID except `CampaignBindingId` must be nonzero when present. Constraints apply with validation and remain trusted.

| Table | Proposed columns |
| --- | --- |
| `AdvertisementAcceptedEvents` | `SourceEventId uniqueidentifier NOT NULL`, `AdvertisementId uniqueidentifier NOT NULL`, `EventType int NOT NULL`, `AcceptedAtUtc datetimeoffset(7) NOT NULL`, `CampaignId uniqueidentifier NULL`, `CampaignBindingId uniqueidentifier NOT NULL` |
| `AdvertisementEventRetryKeys` | `AdvertisementId uniqueidentifier NOT NULL`, `EventType int NOT NULL`, `RetryEventId uniqueidentifier NOT NULL`, `SourceEventId uniqueidentifier NOT NULL`, `AcceptedAtUtc datetimeoffset(7) NOT NULL`, `CampaignId uniqueidentifier NULL`, `CampaignBindingId uniqueidentifier NOT NULL`, `BindingVersion smallint NOT NULL`, `BindingDigest binary(32) NOT NULL` |
| `AdvertisementEventQuotaWindows` | `PartitionKey binary(32) NOT NULL`, `WindowStartUtc datetimeoffset(7) NOT NULL`, `WindowEndUtc datetimeoffset(7) NOT NULL`, `PolicyVersion int NOT NULL`, `AcceptedCount bigint NOT NULL`, `AcceptedLimit bigint NOT NULL` |

| Table | Keys, relationships and named CHECK constraints |
| --- | --- |
| Accepted events | `PK_AdAcceptedEvents` nonclustered on `SourceEventId`. `FK_AdFacts_Advertisement` on `AdvertisementId` references `Advertisements(Id)`. `FK_AdFacts_Receipt` on `(SourceEventId, AdvertisementId, EventType, AcceptedAtUtc, CampaignBindingId)` references the receipt alternate key in that order. `CK_AdFacts_Ids` rejects zero source and ad IDs and zero non-null Campaign IDs. `CK_AdFacts_Type` requires `EventType IN (1,2)`. `CK_AdFacts_Utc` applies the UTC rule above. `CK_AdFacts_CampaignBinding` requires `CampaignBindingId = ISNULL(CampaignId,Z)`. |
| Retry keys | `PK_AdRetryKeys` clustered on `(AdvertisementId, EventType, RetryEventId)`. `AK_AdRetryKeys_Source` unique nonclustered on `SourceEventId`. `AK_AdRetryKeys_FactBinding` unique nonclustered on `(SourceEventId, AdvertisementId, EventType, AcceptedAtUtc, CampaignBindingId)`. `FK_AdReceipts_Advertisement` references `Advertisements(Id)`. Candidate `FK_AdReceipts_Campaign` uses nullable `CampaignId` and targets the retained approved Campaign principal, pending its actual schema. `CK_AdReceipts_Ids`, `CK_AdReceipts_Type`, `CK_AdReceipts_Utc`, and `CK_AdReceipts_CampaignBinding` mirror the fact checks and also reject zero `RetryEventId`. `CK_AdReceipts_BindingVersion` requires `BindingVersion IN (1,2)`. SQL's fixed length column stores exactly 32 bytes. The controlled writer validates digest length before binding the parameter to avoid implicit padding or truncation. |
| Quota windows | `PK_AdQuotaWindows` clustered on `(PartitionKey, WindowStartUtc)`. `CK_AdQuota_Utc` applies the UTC rule to both times. `CK_AdQuota_Window` requires `WindowEndUtc > WindowStartUtc`. `CK_AdQuota_Count` requires `AcceptedLimit > 0 AND AcceptedCount >= 0 AND AcceptedCount <= AcceptedLimit`. `CK_AdQuota_Version` requires `PolicyVersion > 0`. Window alignment and the version-effective policy are checked by the controlled write transaction. |

**FK graph** Fact → Advertisement, fact → receipt (required), receipt → Advertisement, receipt → retained Campaign principal (optional and blocked on owner approval). Every FK uses `ON DELETE NO ACTION ON UPDATE NO ACTION`. SQL composite FKs skip checks when a component is NULL. The non-null binding GUID closes that gap. A CHECK maps NULL attribution to `Z` on both rows, and a real Campaign can never use `Z`. The Campaign reference is therefore enforced transitively when its FK is approved. Receipt rows can outlive facts with the original replay identity and attribution. Quota buckets have no FK from retained facts or receipts, so quota cleanup cannot alter them. Reverse existence of a fact for a newly committed receipt is enforced by atomic controlled writes. The deliberate receipt-only state is reserved for approved fact expiry.

| Proposed index | Definition and purpose |
| --- | --- |
| `CX_AdFacts_Time_Source` | Unique clustered `(AcceptedAtUtc, SourceEventId)` for time scans and bounded retention. Source PK still guarantees global source identity. |
| `IX_AdFacts_Ad_Time_Source` | Nonclustered `(AdvertisementId, AcceptedAtUtc, SourceEventId) INCLUDE (EventType, CampaignId)`. |
| `IX_AdFacts_Campaign_Time_Source` | Nonclustered `(CampaignId, AcceptedAtUtc, SourceEventId) INCLUDE (AdvertisementId, EventType) WHERE CampaignId IS NOT NULL`. |
| `IX_AdReceipts_Campaign` | Nonclustered `(CampaignId, SourceEventId) WHERE CampaignId IS NOT NULL`, conditional on approval of the Campaign FK and principal. |
| `IX_AdQuota_End` | Nonclustered `(WindowEndUtc, PartitionKey, WindowStartUtc)` for bounded cleanup discovery. |

Existing PK and alternate-key indexes cover exact retry lookups and source verification. Review insert costs, FK checks, seek plans, lock escalation and index maintenance in the target provider. [SQL Server primary and foreign keys](https://learn.microsoft.com/en-us/sql/relational-databases/tables/primary-and-foreign-key-constraints?view=sql-server-ver16)

## 3. Payload binding and replay response

**PROPOSED binding v1** applies only to an enumerated, owner-approved unattributed flow. Its identity fields are AdvertisementId, EventType and RetryEventId. **PROPOSED binding v2** adds a required validated, server-issued stable delivery GUID. No delivery contract is approved. A sponsored or ambiguous flow stays blocked until the owner approves how delivery is proven. Validate delivery authenticity and caller binding before using its ID. For an existing receipt, proof identity may be authenticated after its admission window expires, subject to caller authorization and the approved proof verification lifetime. A missing receipt needs currently valid proof wherever required.

For each version hash these canonical bytes with SHA-256: ASCII `Wayhunter.M7.AdEventRetry`, one zero delimiter, version as unsigned 16-bit big-endian, ad GUID as 16 RFC network-order bytes, type as unsigned 32-bit big-endian, retry GUID in the same GUID encoding, then exactly 16 network-order delivery GUID bytes for v2. Parse GUIDs into typed values first, reject zero, and use fixed byte order rather than the default mixed-endian .NET GUID array. Parse event type into canonical codes 1 and 2. Delivery token serialization, client timestamps, Campaign state, district hints, receipt time and quota version are excluded. Keep digest and binding version in the receipt and compare against that version under the retry-key lock. The server selects admissible versions for new requests. A receipt pins the version for comparison and a different semantic request, such as supplying delivery B for an existing delivery A, conflicts even if both are valid. Reject unsupported fields for a version. Do not downgrade a bound request or silently accept a new meaning for a key.

Campaign changes, key case differences in textual GUIDs and quota rollover do not create a new retry key. A valid caller retry with the same canonical identity retrieves the stored source ID, UTC time and nullable Campaign ID, even if the fact has expired. Source ID, time and attribution are immutable. The future HTTP response is **PROPOSED pending M1 and M7 approval**. The current T01 enum has no conflict or exact-response replay contract.

| Scenario | Candidate internal disposition | Candidate HTTP response, all pending |
| --- | --- | --- |
| First accepted request | Accepted, with stored source ID, UTC acceptance time and Campaign ID | 200 with authorized stable receipt fields in a fixed versioned response. Candidate public body contains source ID, UTC time and an accepted status. Keep Campaign ID internal unless M1 approves disclosure. |
| Identical later retry | Current T01 returns Duplicate. Future adapter can also load the original receipt | 200 with the same stable accepted body if exact-response replay is approved. Alternative is a distinct Duplicate response. |
| HTTP response lost | Same-key retry follows identical replay | Same candidate 200 body built from the stored receipt. |
| SQL commit ambiguous | Fresh primary verification under exact retry-key lock. Matching digest and candidate source ID confirms Accepted. Matching digest and another source ID indicates Duplicate. Binding mismatch is Conflict. | Apply the corresponding response. Unresolved verification returns proposed 503 and instructs the caller to retain the original key. |
| Reused key, conflicting payload | Conflict after caller authorization. No writes or quota reservation. | 409 with a generic conflict response and no receipt or attribution fields. |
| Quota rejection | RateLimited with no receipt or fact. | 429 with an approved retry interval derived from the bucket boundary. |
| Unavailable ad or required attribution | AdvertisementUnavailable with no receipt or fact. | Generic 404 candidate, subject to M1 anti-disclosure rules. |

Exact replay refers to approved stable status and body fields, with canonical UTC representation. Transport metadata such as request IDs may differ. Do not read mutable Campaign state or fall back to a current Campaign link to construct a replay.

## 4. Quota configuration proposal

**PROPOSED starting candidate for review and load tests:** an Advertisement/event-type partition, 60-second UTC windows, 120 accepted impressions and 30 accepted clicks per partition/window. Numbers are candidates only. A separate request-attempt limiter needs M1 approval before intake. The accepted-event quota cannot stop abuse that repeatedly submits fresh keys.

Use SHA-256 over ASCII `Wayhunter.M7.AdQuotaPartition.v1`, a zero byte, the ad GUID in the above canonical order and the 32-bit canonical event type. No personal identifiers or secret keys enter this partition. Its hash is a stable internal identifier, with no key rotation needed. Treat it as operational data and keep it out of responses and ordinary logs. If M1 later approves an HMAC partition or another input dimension, schedule key and derivation rotation at a common UTC boundary, retain previous selection for legitimate in-flight operations and prove no dual bucket for one effective partition/window. Do not add user, device or IP inputs without separate approval.

Use half-open windows aligned to whole multiples of 60 seconds since the Unix epoch. Capture server UTC receipt time once, hold it across internal retries, and store `AcceptedAtUtc` as UTC receipt time for successful facts. The persisted policy version, end and limit are fixed for the bucket. Policy version is deliberately outside the PK and derivation. Publish immutable effective-time policies and activate changed limits at the next boundary. An existing bucket's persisted values win. Alert on configuration disagreement. A duration or partition change needs a common boundary, coordinated cutover and draining or pinning of old in-flight writes. Candidate write age is ten minutes after receipt, verified against database UTC under the bucket lock before creation or update.

**Approval question for M1, M7 and DB Coordinator:** approve the candidate partition, limits, windows, age and cutover, or choose separate placement scoping, different measured limits and windows, or an independently reviewed identity-aware policy. At capacity a serialized conditional increment succeeds for only the final available slot. Later distinct keys see zero rows updated and consume no accepted quota.

## 5. Transaction and controlled writers

**PROPOSED** SQL Server READ COMMITTED transaction with explicit `UPDLOCK,HOLDLOCK` exact-seek protection on the retry and quota PKs, including missing-key ranges. Hold appropriate locks on authoritative Advertisement and conditional attribution keys through commit. Verify target database settings, seek plans and absent-row protection on SQL Server. Readers can use a separately approved transaction-level snapshot for same-population reconciliation. Statement-level versioning cannot prove consistency across two reads. [SQL Server locking guidance](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver16) · [Table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver16)

```text
Validate typed input and authenticate/authorize. Resolve stable identity and capture
one server receipt time and candidate source GUID for this operation.
Apply approved attempt limits outside the accepted quota transaction.
Open a fresh DbContext and begin the whole unit under one bounded execution strategy.

1. Seek and protect retry PK (ad, type, retry GUID), including an absent key.
   Existing: validate identity using pinned version. End without writes.
   Match -> Duplicate plus stored internal receipt. Mismatch -> Conflict.
2. Lock Advertisement and approved attribution rows in fixed key order.
   Check eligibility and required trusted delivery admission, or return Unavailable.
   Capture one immutable Campaign ID or approved null.
3. Derive partition and bucket. Seek and protect quota PK including absent key.
   Recheck operation age against database UTC now that the lock is held.
   If missing, insert one zero-count bucket with the effective policy metadata.
   UPDATE exactly that key SET AcceptedCount = AcceptedCount + 1
     WHERE AcceptedCount < AcceptedLimit.
   Zero affected rows -> rollback, RateLimited.
4. Insert receipt, then fact using the same source GUID, ad/type/time and attribution.
   Commit once. Return Accepted only after commit or positive verification.

Any failure before commit rolls back all writes and discards the context.
Deadlock victim 1205: bounded whole-unit retry with jitter (candidate max 3 attempts),
using the same key, identity, receipt time and candidate source GUID.
2601/2627: rollback, identify the actual constraint or verify on a fresh primary context.
A confirmed matching retry receipt resolves replay. A differing digest resolves conflict.
A quota PK collision may retry the whole unit. An unrelated failure propagates.
Ambiguous commit: lock/read exact receipt on fresh primary connection, check digest
and source GUID. Resolve only after the original transaction has settled.
Decisive absence permits bounded same-operation retry. Indeterminate -> retryable error.
```

One lock order applies to acceptance paths: retry → Advertisement → attribution rows sorted by key → quota → inserts. Same-key contenders wait on the protected range or retry the whole unit after rollback. For distinct keys, the guarded quota update and unique bucket key arbitrate the last slot and bucket creation, even when retry keys differ. Quota rollback precedes conflict classification. Unique indexes and FK checks are independent of in-process locking. No external I/O or caller round trips inside the transaction. Use EF execution-strategy verification for uncertain commits with one retry owner. [EF Core commit verification](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)

Prefer a reviewed stored transaction routine exposed through EXECUTE-only application credentials, with direct writes forbidden to application, report and audit readers. The module owns its permissions for fact/receipt INSERT and quota INSERT/guarded UPDATE. Keep deployment DDL and approved retention DELETE behind separate identities. Ordinary roles receive no UPDATE or DELETE for historical rows. A reviewed EF-owned transaction with tightly scoped table grants is an alternative, subject to evidence that a writer cannot commit orphaned receipts, facts or quota. FK direction alone cannot enforce that atomic writer invariant. Reject ad/Campaign deletion while referenced.

## 6. Retention and cleanup

All durations below are candidates. **No historical mutation or deletion is authorized.**

| Item | Candidate and cleanup invariant |
| --- | --- |
| Accepted facts and audit visibility | 365 days online from `AcceptedAtUtc`, pending storage and privacy signoff. Archive or delete only after approved coverage policy. Return unavailable or explicit incomplete coverage when historical rows cannot be read. Maintain reporting and audit parity for available slices. |
| Retry receipts | UNBOUNDED DECLINED BY M1. Do not delete receipts for caller-chosen keys or release the contract until an approved finite lifecycle prevents reacceptance. Preserve original identity while lawfully held. |
| Tombstones or finite retry expiry | No separate table proposed. Any finite marker may bridge only a governed, verified recovery window. Before its deletion, prove the tuple remains permanently inadmissible across all routes and versions, including under a fresh proof or after restore. Unbounded receipts and indefinite substitutes are outside the current proposal. See the privacy and retention assessment. |
| Quota buckets | Eligible for cleanup 48 hours after window end, exceeding the ten-minute candidate write age. Use database UTC, verify age after acquiring the same quota key lock, and keep old policy versions available for the maximum admitted age plus recovery margin. |
| Cleanup recovery | Discover candidates in small batches without holding discovery locks across subsequent operations. Fact expiry locks retry receipt first by its PK, then rechecks and locks the dependent fact, and deletes only the fact through the approved procedure. Quota cleanup locks quota keys in stable order in its own transaction and never seeks earlier lock classes. If a writer holds the key, cleanup waits or retries. A waiter rechecks state and age after acquiring the lock. Rollback or retry batches only after confirming run ownership and rereading committed rows. Repeated cleanup is idempotent. |

Report T01 → T03 → T02 → Admin from the same accepted facts, scope, ad/Campaign filters and half-open UTC window at a consistent approved snapshot. Use checked 64-bit independent counts and retain the legacy counter cutover policy. T04 remains an eligibility boundary for future serving. No district, GPS, raw request, OTP, token or user/device detail enters these tables.

## 7. Campaign attribution — BLOCKED FOR OWNER DECISION

The Campaign domain's `ContentId` does not establish persisted ad identity. Decide whether an ad belongs to one Campaign, whether one Campaign has many ads, or whether ads and Campaigns need many-to-many assignments. Even a unique current assignment may differ from what was shown before a later click. Recommend a trusted delivery reference for flows where actual delivery cannot otherwise be proved. Choose its issuer, lifetime, caller binding, how it proves the ad and Campaign actually served, and whether current eligibility is also required at acceptance.

Capture the chosen attribution once in the same acceptance transaction. Null means an owner-approved unattributed flow. Missing or ambiguous required attribution fails closed. Unlink/archive affects future eligibility only. Hard deletion of referenced principals is restricted by the proposed FK. If the Campaign owner needs physical deletion, review a durable historical principal design and its privacy retention first. No implicit FK absence, invented table name, or overwrite of history is authorized.

## 8. EF design, migration order and deployment review

Separate persistence row types and future `IEntityTypeConfiguration` classes map exact types, named CHECKs, indexes and no-action relationships. Use `ValueGeneratedNever` on server-assigned GUIDs, enum conversion to int, precision seven for datetimeoffset, fixed-length binary fields, and a required alternate key for the composite FK. Do not use rowversion on immutable rows. Proposed quota updates use the guarded SQL predicate under the protected key instead of tracked read/modify/write or a concurrency token. Use no-tracking historical queries. No registrations or mappings are added by this package.

The DB Coordinator assigns sole migration ownership after physical approval. Verify the Advertisement principal and Campaign decision first. Then review migration order as receipt and quota tables, fact table, indexes and dependent FKs, then controlled routines and role grants. If Campaign is unresolved, a separately approved unattributed-only schema must explicitly enforce its restrictions and leave the sponsored flow disabled. Verify provider/version, plans, grant boundaries, rollback and backup restoration on disposable SQL Server before runtime enablement. Deploy schema and controlled writers before verified readers, leave ingestion disabled until approved, and keep a forward-fix plan. After writes exist, prefer disabling intake and preserving accepted records and retry keys. Dropping tables as rollback would lose history and replay protection and needs separate explicit approval.

## 9. Isolated SQL protocol and matrix

**Every SQL test is NOT EXECUTED.** Future execution requires an approved disposable database on an approved isolated server. Require explicit opt-in and exact matches against server/instance and database allowlists, with a unique run ID recorded in the provisioned database. Verify actual connected server, DB name and ID, and ownership marker before any write or cleanup. Reject shared, production, development, system and unknown databases. A separate provisioner owns create/drop rights. Exercise production-style least-privilege writer, audit and reader accounts. Use independent physical connections and DbContexts, deterministic command barriers, fault injection, and a third connection for committed-row verification. Check the target isolation options, constraint metadata and index/locking evidence.

| Test | Persisted assertions | Status |
| --- | --- | --- |
| Same-key race and first writer failure | One receipt, fact and increment when both contend, including takeover after rollback. Same GUID across types or ads remains independent. | NOT EXECUTED |
| Binding versions and conflicts | Canonical encoding vectors, same identity replay, different delivery conflict, unauthorized replay and invalid admission proof. No extra writes or quota. | NOT EXECUTED |
| Last slot and missing bucket | One bucket row and exactly the permitted winners. Exercise version rollover and late first writes without splitting capacity. | NOT EXECUTED |
| Rollback and constraints | Fail after quota or receipt insert. Assert unchanged committed state. Verify source/ad/type/time/Campaign mismatch, UTC/type checks and unauthorized writes fail. | NOT EXECUTED |
| Lost acknowledgement | Inject before/after commit failure and failed verification. Confirm original stable receipt, single fact and increment, and correct indeterminate outcome. | NOT EXECUTED |
| Relink, archive and drilldown | Capture approved historical attribution under concurrent change. Verify authorized reads, denied mixed scope, and restrictive principal deletion. | NOT EXECUTED |
| Retention and cleanup race | Expire a fact while retaining its receipt, retry unchanged, race quota cleanup with a delayed writer, and reject creation after the admitted age. | NOT EXECUTED |
| Reconciliation and provider behavior | Verify snapshot parity, legacy exclusion, late commits, deadlock strategy, locks/seek plans, and privacy-safe projection. | NOT EXECUTED |

Record only synthetic IDs and sanitized diagnostics. In finally/recovery, validate the run marker and allowlists again, close all run connections, then let the provisioner clean only that run's database. A missing or mismatched marker blocks automated cleanup. Record partial provisioning and hand the exact run identity to the coordinator for recovery. Never delete by prefix alone.

## 10. Approval matrix

| Decision | Recommended proposal | Alternatives | Required owner | Approval status | Evidence |
| --- | --- | --- | --- | --- | --- |
| Architecture | Preserve three tables and atomic accepted fact/receipt/quota transaction with unique retries and immutable historical attribution | Accepted baseline retained | DB Coordinator | ACCEPTED architecture | Coordinator review of `c20ed55` |
| Request identity | v1 ad/type/retry key and v2 stable authenticated delivery ID digest | Persist versioned typed identity directly | M1, M7 and Campaign owner | PENDING | Sections 3 and 7 |
| Receipt response | Restore stable fields and propose exact authorized HTTP replay | Explicit Duplicate response or narrower body | M1 and M7 | PENDING | Section 3 response matrix |
| Quota and attempt policy | Ad/type SHA-256 partition with boundary-pinned policy and separate attempt controls | Measured alternative limits or approved additional scope | M1, M7, DB Coordinator and privacy owner for scope | PENDING | Section 4 |
| DDL and controlled writers | Named constraints, non-null composite FK, restricted routines and reviewed indexes | EF-owned writer with equivalent guarantees, alternate clustering | DB Coordinator and M7 | PENDING | Sections 2, 5 and 8 |
| Isolation and recovery | Protected seeks, one lock order and bounded transaction retry with commit witness | Serializable transaction and guarded update, subject to measured plans | DB Coordinator | PENDING | Sections 5 and 9 |
| Campaign identity | Trusted delivery attribution and retained principal | Unambiguous approved authoritative lookup, explicit unattributed scope | Campaign owner, M7 and DB Coordinator | BLOCKED FOR OWNER DECISION | Section 7 |
| Retention and coverage | 365-day fact candidate PENDING, unbounded receipts declined, 48-hour quota grace conditionally acceptable to M1 | Non-reissuable bounded admission or verified finite tombstone bridge | M1, privacy/data owners, DB Coordinator and M7 | BLOCKED for receipt design, governance PENDING | Privacy assessment |
| Test, migration and deployment ownership | Disposable SQL protocol and coordinator-owned approval sequence | Another approved isolated test provisioner | DB Coordinator and M7 | PENDING | Sections 8 and 9 |

The existing harness results are prior evidence for application contract behavior. This revision changes documentation only. No new .NET or SQL tests were run.

## Final implementation review handoff

Kaviyan conditionally accepted this design at commit 239bc059bebd6dfdb2e51bc4d7e8030dc0bd3819. Final sign-off remains pending. The [final SQL review artifacts](M7-MS07_T01_T03_FINAL_SQL_REVIEW_ARTIFACTS.md) provide the exact candidate definitions, procedure boundaries and disposable test protocol. The [owner register](M7-MS07_T01_T03_OWNER_DECISION_REGISTER.md) and [develop compatibility evidence](M7-MS07_T01_T03_LATEST_DEVELOP_COMPATIBILITY.md) track the remaining approvals and source-verified prerequisites. The architecture and prior verification remain as recorded above.

## M1 API contract review addendum

Shayanthan confirmed trusted M1 bearer validation, a current active and verified account, and a valid unexpired, non-revoked session. This addendum extends the conditional physical review candidate. The [M1 review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md) supersedes earlier generic response candidates and adds the future acceptance matrix. Final approval remains pending.

Propose receipt-only `CallerBindingAlgorithmVersion smallint NOT NULL`, `CallerBindingKeyId uniqueidentifier NOT NULL`, and `CallerBindingDigest binary(32) NOT NULL` using dedicated-key HMAC-SHA-256 scoped to the canonical account and retry tuple. Keep both separate from payload `BindingVersion` and `BindingDigest`. Do not alter the composite unique retry key or add account fields to the fact. Verify ownership under the retry-key lock before comparing payload or revealing any receipt outcome, including commit verification. Keep existing receipt ownership across refreshed sessions. A foreign or unverifiable caller receives a generic failure with no key-existence signal.

Accepted and authorized identical replay are candidate HTTP 200 with the same stable accepted body. T01 retains internal Duplicate. Authorized payload mismatch is candidate generic 409. An invalid session maps to 401, and attempt or accepted-event quota exhaustion maps to 429 with approved Retry-After. Unavailable ad or proof uses a generic failure. Attempt limits apply before receipt lookup and include retries. The candidate 120/30 values are load-test values only. Preserve existing bucket versions and keep sponsored intake disabled pending caller-bound delivery proof and Campaign attribution approval.

## Current M1 privacy retention gate

Shayanthan's separate M1 privacy review declined unbounded retry receipts, left the 365-day accepted-fact candidate PENDING, and found cleanup of quota windows 48 hours after end CONDITIONALLY ACCEPTABLE. No cleanup or ingestion is authorized. The [updated assessment](M7-MS07_T01_T03_PRIVACY_RETENTION_ASSESSMENT.md) is the current source for bounded retention analysis and acceptance boundaries. Earlier physical and SQL examples remain review artifacts. A caller-chosen key with a fresh delivery proof can be reaccepted after receipt deletion. Require a proven non-reissuable, authenticated admission identity or another design with equivalent deduplication before opening intake. Keep bounded retention BLOCKED FOR DESIGN DECISION. The three-table architecture, immutable attribution, and atomic write direction are unchanged.

## Latest bounded replay dependency

[Latest M1 API review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md) records separate first-admission, replay and finite storage windows. Keep the fact-to-receipt composite FK and verify no live dependency before authorized receipt deletion. The revised candidate depends on proven non-reissuance under restore and rotation. The 365-day fact duration remains pending and 48-hour quota cleanup remains conditionally acceptable. Bounded receipt retention is BLOCKED FOR DESIGN DECISION. Approval of both the issuer contract and the physical storage lifecycle is required.

## Latest receipt lifecycle gate

[Deadline and FK lifecycle review](M7-MS07_T01_T03_BOUNDED_REPLAY_FK_REVIEW.md) keeps the required composite NO ACTION FK. The 365-day fact and 366-day receipt candidates are PENDING. Review post-replay clearing as an explicit nullable-pair and controlled maintenance alternative. The proposed NOT NULL and immutable receipt policy remains the baseline until approved. Bounded retention remains BLOCKED FOR DESIGN DECISION.
