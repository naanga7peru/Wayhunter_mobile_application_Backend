# M7-MS07 T01 and T03 SQL persistence review proposal

**Status** PROPOSED, awaiting approval. **Scope** M7 HomeAdsAnalytics, ADM-05 and ADM-15. **Review baseline** `8760966805a449e95c4741433384dac148f7e7c8` on `wip/m7-ms07-event-contract-harness`. Campaign PR #273 is pending.

## Existing contracts and evidence

**SOURCE** The M7 acceptance criteria require replay-safe impression and click counts, tested rate limits, dashboard reconciliation, and traceability to Campaign and source event records.

**CODE_VERIFIED locally** `AdvertisementRepository` uses `WayhunterSharedDbContext` for Advertisement reads and writes. `AdvertisementConfiguration` maps `Advertisements` with an application-assigned GUID, status and placement checks, a schedule check and index, and lifetime `bigint` impression and click counters. Those counters carry neither retry identity nor reporting time. T01's `IAdvertisementEventStore` defines atomic acceptance and consistent totals. T03's immutable fact and audit reader model authorized committed history. T02's comparator reconciles complete per-ad rows and validates matching UTC windows. The combined harness maps between the two event enums, uses a locked test store, and exercises reporting and attribution with synthetic IDs. SQL concurrency and authoritative Campaign resolution require separate evidence. [Harness evidence](M7-MS07_EVENT_CONTRACT_INTEGRATION_READINESS.md)

Existing persistence patterns include explicit EF execution strategy and transaction boundaries in `EfTripMembershipRevocationRepository`, and SQL locking reads in `SqlGameRepository`. They inform the proposal. The query plans, lock behavior and failure classification below require M7-specific verification.

## Proposed logical storage

Names and types below are review candidates. M7 owns the module mappings and adapter proposal. The DB Coordinator owns shared schema coordination, migration approval and isolated SQL access.

| Entity / proposed table | Fields | Keys and constraints |
| --- | --- | --- |
| Accepted event / `AdvertisementAcceptedEvents` | `SourceEventId uniqueidentifier`, `AdvertisementId uniqueidentifier`, `EventType int`, `CampaignId uniqueidentifier NULL`, `AcceptedAtUtc datetimeoffset(7)` | PK `SourceEventId`, application-assigned on the server. Required Advertisement FK to `Advertisements.Id` with restricted deletion. Check nonzero GUIDs, event type in `(1, 2)`, UTC offset zero and non-default timestamp. Campaign FK or historical reference subject to approval. |
| Retry receipt / `AdvertisementEventRetryKeys` | `AdvertisementId uniqueidentifier`, `EventType int`, `RetryEventId uniqueidentifier`, `SourceEventId uniqueidentifier`, `AcceptedAtUtc datetimeoffset(7)` | Composite PK `(AdvertisementId, EventType, RetryEventId)`. Unique alternate key on `SourceEventId`. Required Advertisement FK with restricted deletion. Same type, nonzero GUID and UTC checks. No nullable key fields. |
| Quota bucket / `AdvertisementEventQuotaWindows` | `PartitionKey binary(32)` candidate opaque server-derived key, `WindowStartUtc datetimeoffset(7)`, `WindowEndUtc datetimeoffset(7)`, `AcceptedCount bigint`, `AcceptedLimit bigint` | PK `(PartitionKey, WindowStartUtc)`. Check UTC offsets, ordered window, positive limit and `0 <= AcceptedCount <= AcceptedLimit`. Window end and limit are fixed for an existing bucket. Partition construction and configuration rollover need review. |

Use one accepted row per committed event for T01 and T03. The retry receipt stores the caller key outside the audit fact and links it to the stable source ID. Proposed FK direction is from the fact to the receipt's unique source ID, with restricted delete, allowing the receipt to survive fact expiry. The transaction inserts both together and derives their ad, event type and timestamp from the same validated values. The coordinator should decide whether to enforce that equality with a wider alternate key and FK, or with a restricted write boundary and integrity checks. An ad must be retained while referenced by either row.

Codes `1` and `2` represent Impression and Click. Adopt one shared enum or an explicit validated mapping before EF persistence. Use explicit `IEntityTypeConfiguration` mappings, named constraints and `ValueGeneratedNever` for the application-assigned source ID. Choose clustering separately after measuring random GUID insert behavior and reporting index overhead. Restrict routine writer permissions to inserting facts and receipts and updating quota. Grant controlled retention permissions separately.

The composite retry key spans all windows and ad and Campaign lifecycle changes. Including the quota bucket, timestamp or Campaign ID in uniqueness would let the same request count twice after a rollover or relink. Event type and ad belong in the key so the same opaque GUID can represent a click and an impression, or events on different ads, independently.

### Attribution and indexes

Resolve ad eligibility and, where approved, Campaign attribution from server-owned data in the acceptance transaction. The accepted row stores the selected ID as an immutable snapshot. A null Campaign represents an approved unattributed acceptance. If a flow requires attribution and the authoritative choice is missing or ambiguous, fail closed. M7 and the Campaign owner must approve the relationship, cardinality, and whether the reference reflects delivery or a consistent lookup at acceptance. A delayed click may require a server-owned delivery reference. PR #273 does not establish a persisted Advertisement–Campaign relationship.

Keep historical Campaign references resolvable for authorized reporting after unlinking or archival. If a live Campaign FK is approved, use restrictive delete behavior and define a retained historical record. Avoid substituting a current Campaign join for the stored ID. Candidate covering indexes for query-plan review:

- Ad drilldown `(AdvertisementId, AcceptedAtUtc, SourceEventId) INCLUDE (EventType, CampaignId)`.
- Attributed Campaign drilldown `(CampaignId, AcceptedAtUtc, SourceEventId) INCLUDE (AdvertisementId, EventType)`, filtered to non-null Campaigns if supported by actual query plans.
- Receipt retention `(AcceptedAtUtc, SourceEventId)` and quota cleanup `(WindowEndUtc)` if measured cleanup queries justify them.

## Proposed acceptance transaction

Timestamp candidate preserves the T01 contract. Use normalized server receipt time as `AcceptedAtUtc` for successful facts and derive the fixed UTC bucket from that same time. Hold the original timestamp and candidate source ID across internal transaction retries. A later client retry supplies the same composite key and can have a new receipt time. The original fact wins. The timestamp expresses when the accepted request reached the server. Commit visibility comes from the database snapshot. The coordinator must approve this semantic and the maximum age of a write attempt.

The following is pseudocode. The write candidate uses locking reads with `UPDLOCK, HOLDLOCK` on indexed retry and quota keys, including a missing-key range. Read authoritative ad and Campaign/delivery rows under protection that keeps the eligibility decision valid through commit. Acquire locks in retry key → Advertisement → approved attribution rows in stable key order → quota bucket order. Retention and other writers must participate in a compatible order. Validate this under the target SQL Server settings and compare with a `SERIALIZABLE` transaction and guarded quota update before choosing the design. [SQL Server locking guidance](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver16) · [Table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver16)

```text
ACCEPT(adId, type, retryId, authenticated server context):
    validate identifiers and type and authorize the request
    capture receiptUtc and candidateSourceId once for this operation
    enforce the approved attempt-limit policy outside accepted-event quota

    run the complete write unit through an approved EF execution strategy:
        open a fresh DbContext and short SQL transaction
        lock and read receipt by exact (adId, type, retryId)
        if found:
            end transaction with no writes
            return Duplicate using the stored SourceEventId internally

        verify trusted replay admission deadline and resolve eligible ad
        resolve approved historical Campaign attribution or approved null
        if ad or required attribution unavailable:
            end transaction with no writes and return AdvertisementUnavailable

        derive quota partition and fixed UTC window from trusted inputs
        reject expired write attempt before touching the bucket
        lock exact quota key, including missing range
        if absent: insert bucket at count zero with the approved effective limit
        increment once with predicate AcceptedCount < AcceptedLimit
        if affected rows = 0:
            roll back and return RateLimited

        insert immutable retry receipt and accepted fact using candidateSourceId
        commit transaction
        return Accepted

    on known pre-commit failure:
        roll back the complete unit and discard the context
        retry bounded transient errors only with the same key and operation inputs

    on duplicate-key error:
        roll back the complete unit and dispose the context
        classify Duplicate only for a confirmed committed matching retry receipt
        handle quota-key creation contention with a bounded whole-unit retry
        propagate unrelated constraint failures

    on ambiguous commit:
        dispose the context and look up the exact receipt from the primary
        if the receipt matches candidateSourceId: confirm this operation committed
        if the receipt names another source ID: return Duplicate for the committed winner
        if absent and outcome can be safely resolved: retry the complete unit with the same key
        if still indeterminate: surface a retryable failure and preserve the original key
```

The retry receipt is the commit witness. An authoritative read must wait for or serialize with unresolved writes before treating absence as decisive. SQL errors 2601 and 2627 alone do not identify a retry-key conflict. Check the relevant constraint or confirm the exact winning receipt in a fresh context. Verify that any bounded retry for deadlock or transient failure replays the entire transaction. Use the EF execution strategy to own retries of that unit, with a deliberate commit-verification path, and avoid overlapping retries around individual writes. [EF Core commit verification](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)

### Scenario outcomes

| Scenario | Required transaction or recovery behavior |
| --- | --- |
| First impression | Lock missing key at type 1, check eligibility, reserve one quota slot, insert one receipt and one impression fact, commit and return Accepted. |
| First click | Same operation with type 2. The same retry GUID from an impression is an independent key. The quota partition definition is a separate decision. |
| Duplicate retry | Read the committed receipt before current eligibility and quota. Return Duplicate with the existing source identity available internally. |
| Retry after quota rollover | Find the same receipt regardless of the new bucket. The new bucket is untouched. |
| Rate-limited event | Guarded increment affects zero rows. Roll back and return RateLimited. Receipt and fact counts stay unchanged. |
| Concurrent same retry key | A acquires the retry key range. B waits or retries after the SQL conflict. If A commits, B reads its receipt. If A rolls back, B can accept once. The uniqueness constraint still rejects competing inserts. |
| Distinct keys competing for last slot | Both pass their key checks. A increments the protected bucket and commits. B observes capacity exhausted and rolls back. If A rolls back, B can claim the slot. |
| Failure after quota before event write | Inject the failure after the increment. Rollback restores the prior quota and leaves zero facts and receipts for the key. A later retry may accept once. |
| Commit succeeds and HTTP response is lost | A subsequent request with the same key returns Duplicate and preserves original fact and quota. If SQL commit acknowledgement itself was lost, verify the original receipt through a fresh primary connection. |
| Replay after Campaign changes | The committed receipt wins before relationship resolution. A fresh eligible event resolves the approved relationship independently. The original fact remains attributable to its original Campaign. |

## Reporting, retention and privacy

- **T01 → T03 → T02 → Admin dashboard.** The acceptance transaction publishes one committed fact by making it visible to database readers. T03 reads that fact for authorized drilldown by source ID and orders by `(AcceptedAtUtc, SourceEventId)`. T02 compares independently grouped per-ad rows from the same accepted population and uses separate 64-bit impression and click totals. Use `COUNT_BIG` and checked totals. Display views as impressions.
- Resolve the same effective authorization scope, half-open UTC window `[fromUtc, toUtc)`, ad and Campaign filters, and fact population for both reads. Use stored Campaign IDs for historical filters, with explicit null handling. T02 only verifies per-ad rows and equal windows. Enforce matching scope, Campaign filters, completeness and snapshot in the adapters. To claim per-Campaign parity, compare separate Campaign-filtered slices or review an explicit extension to the grouping key.
- Prefer a short, approved transaction-level SQL snapshot for initial reads of source and dashboard data. Verify SQL database settings with the coordinator. Statement-level snapshots on separate queries can see different commits. Apply authorization to every historical fact within the same effective snapshot. An unauthorized or failed read cannot be represented as an empty, verified report. Reconcile the complete slice before paging. Stable paging across requests needs a reviewed checkpoint or bounded read mechanism.
- If a materialized dashboard is approved later, apply each source ID once and advance its checkpoint atomically with the projection. A checkpoint must identify a complete committed population, including out-of-order receipt timestamps and late commits. A wall-clock maximum, a GUID or a bare allocated sequence alone does not establish that population. Fail closed on mismatched checkpoints or scope.
- Keep legacy lifetime counters outside accepted-event rollups. Approve a cutover and coverage start, label legacy totals separately if displayed, and mark earlier or pruned windows as uncovered. Do not fabricate source events from historical counts. Any approved archive must preserve completeness for the windows offered by the report.
- Store only the coarse fact fields listed above. Keep raw location, IP, user/device identifiers, session tokens and request payloads out of analytics facts, retry receipts and drilldown. Scope any server-derived quota identifier to its approved purpose, version and lifetime, and treat it as sensitive. Audit access and restrict exports. T04 district reporting needs separate privacy approval.
- Approve separate lifetimes for facts, receipts and quota buckets. Keep receipts until no supported retry can recreate the event, and at least as long as the referencing fact. A caller GUID gives no trusted age. Until admission can prove expiry, retain receipts or equivalent durable tombstones. If an expiring server-issued admission proof is approved, validate it for new keys and preserve duplicate recognition for retained keys. Clean up quota only after in-flight writes and any authorized internal retries cannot enter the old window. Coordinate cleanup and writes with indexed locks and test the boundary. Reads after fact expiry must not silently turn archived or unavailable history into zero.

## Isolated SQL Server test requirements

All rows below are **NOT EXECUTED**. Use an approved disposable SQL Server database and independent connections and DbContexts, with synchronization at SQL command or transaction boundaries. Gather results from persisted rows using a third context. The in-memory harness establishes application contract behavior only.

| Test | Required persisted evidence |
| --- | --- |
| Same key race and independent keys | Parallel connections create one receipt, fact and increment for identical keys. Same GUID across types and ads creates independent receipts. Include a first-writer rollback variant. |
| Last slot and bucket creation | Race distinct keys against one remaining slot and against an absent bucket. Verify exactly one winner per available slot and no orphan quota increments. Include retries across UTC rollover. |
| Rollback between quota and fact | Inject failure after quota increment and after receipt insert. Verify pre-write state is restored and a same-key retry commits once. |
| Lost response and ambiguous commit | Exercise response loss after a successful commit and SQL commit-acknowledgement failure. Probe with the original key and verify the committed source ID, Campaign and quota. Also test no-commit recovery and failed verification. |
| Attribution changes | Change an approved fixture relation concurrently with write acceptance. Verify isolation yields one authorized snapshot. Replay after relink and archive, and verify the old fact still traces to its stored Campaign. |
| Reconciliation | Include both UTC boundaries, two ads, a null Campaign, distinct Campaigns and a late commit. Compare source, trace and dashboard at one scope and checkpoint. Exclude failed and duplicate attempts and legacy counters. Reject partial and unauthorized snapshots. |
| Drilldown authorization | Verify permitted scoped reads, denied and revoked access, including mixed historical Campaign scope. Assert denied responses disclose neither facts nor totals. |
| Retention boundaries | Archive facts while retaining receipts. Verify retries remain duplicates. Exercise cleanup concurrent with in-flight attempts and approved expiry or tombstone rules. Mark incomplete reporting windows as unavailable. |
| Provider failure and plan checks | Verify bounded deadlock retry, named constraint classification, retention lock order, target isolation settings, index use and contention under expected quota partitions. |

## Approval checklist

- [ ] **DB Coordinator and M7** approve table and relationship shape, named keys, check constraints, append-only protection, index and clustering choices, and retention dependencies.
- [ ] **DB Coordinator** approves transaction isolation, lock order, guarded quota update and bucket creation, execution strategy, commit witness, retry limits and provider test plan.
- [ ] **M1 and M7** approve intake authentication, admin reporting and historical drilldown scope, trusted attempt and quota partitions, limits, windows and release-change behavior.
- [ ] **Campaign owner and M7** approve authoritative link selection and nullable attribution policy, delivery versus acceptance semantics, and preservation of historical references after #273 review.
- [ ] **Privacy and data owners** approve receipt and fact lifetimes, expiry or tombstone semantics, disclosure, access audit and cleanup policy.
- [ ] **M7 and DB Coordinator** approve reporting timestamp semantics, snapshot or checkpoint protocol and the legacy cutover and coverage policy.
- [ ] **DB Coordinator** assigns migration ownership and approves an isolated SQL Server target, deployment ordering and rollback review before implementation. Coordinate any shared DbContext changes through the normal reviewed workflow.

After approval, implement the T01/T03 EF mapping and atomic store in a reviewed, isolated persistence change and execute the SQL matrix before connecting ingestion. Use the accepted facts for T02 reads after the report scope and cutover decisions are approved.
