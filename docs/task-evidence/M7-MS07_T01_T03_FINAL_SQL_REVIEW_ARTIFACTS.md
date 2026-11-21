# M7-MS07-T01/T03 final SQL review artifacts

**Status** CONDITIONAL DESIGN ACCEPTANCE, FINAL SIGN-OFF PENDING. Review only. Do not execute these definitions. Based on Kaviyan's review of `239bc059bebd6dfdb2e51bc4d7e8030dc0bd3819`. Preserve the accepted three tables, replay uniqueness, immutable attribution, atomic quota/fact acceptance, and fact-based reporting. [Physical candidate](M7-MS07_T01_T03_PHYSICAL_SCHEMA_REVIEW_PACKAGE.md) · [Owner decisions](M7-MS07_T01_T03_OWNER_DECISION_REGISTER.md) · [Repository compatibility](M7-MS07_T01_T03_LATEST_DEVELOP_COMPATIBILITY.md)

## Three-table definition

`dbo` is the proposed new object schema. Existing source names `Advertisements` without an explicit qualifier. The FK examples below use `dbo.Advertisements` only if the coordinator verifies it in the disposable target. Validate identifier bindings, existing PK and types before approval. Keep all constraints trusted. The statements are illustrative Markdown only, with no deployment or cleanup script. [SQL Server CREATE TABLE reference](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql?view=sql-server-ver17)

```sql
-- REVIEW ONLY. DO NOT EXECUTE.
-- dbo.Advertisements is conditional on verified target schema resolution.
CREATE TABLE dbo.AdvertisementEventRetryKeys (
    AdvertisementId uniqueidentifier NOT NULL,
    EventType int NOT NULL,
    RetryEventId uniqueidentifier NOT NULL,
    SourceEventId uniqueidentifier NOT NULL,
    AcceptedAtUtc datetimeoffset(7) NOT NULL,
    CampaignId uniqueidentifier NULL,
    CampaignBindingId uniqueidentifier NOT NULL,
    BindingVersion smallint NOT NULL,
    BindingDigest binary(32) NOT NULL,
    CONSTRAINT PK_AdRetryKeys PRIMARY KEY CLUSTERED (AdvertisementId, EventType, RetryEventId),
    CONSTRAINT AK_AdRetryKeys_Source UNIQUE NONCLUSTERED (SourceEventId),
    CONSTRAINT AK_AdRetryKeys_FactBinding UNIQUE NONCLUSTERED
        (SourceEventId, AdvertisementId, EventType, AcceptedAtUtc, CampaignBindingId),
    CONSTRAINT FK_AdReceipts_Advertisement FOREIGN KEY (AdvertisementId)
        REFERENCES dbo.Advertisements (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT CK_AdReceipts_Ids CHECK (
        AdvertisementId <> '00000000-0000-0000-0000-000000000000' AND
        RetryEventId <> '00000000-0000-0000-0000-000000000000' AND
        SourceEventId <> '00000000-0000-0000-0000-000000000000' AND
        (CampaignId IS NULL OR CampaignId <> '00000000-0000-0000-0000-000000000000')),
    CONSTRAINT CK_AdReceipts_Type CHECK (EventType IN (1, 2)),
    CONSTRAINT CK_AdReceipts_Utc CHECK (DATEPART(TZOFFSET, AcceptedAtUtc) = 0 AND
        AcceptedAtUtc > CONVERT(datetimeoffset(7), '0001-01-01T00:00:00+00:00')),
    CONSTRAINT CK_AdReceipts_CampaignBinding CHECK (
        CampaignBindingId = ISNULL(CampaignId, CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000'))),
    CONSTRAINT CK_AdReceipts_BindingVersion CHECK (BindingVersion IN (1, 2))
);

CREATE TABLE dbo.AdvertisementEventQuotaWindows (
    PartitionKey binary(32) NOT NULL,
    WindowStartUtc datetimeoffset(7) NOT NULL,
    WindowEndUtc datetimeoffset(7) NOT NULL,
    PolicyVersion int NOT NULL,
    AcceptedCount bigint NOT NULL,
    AcceptedLimit bigint NOT NULL,
    CONSTRAINT PK_AdQuotaWindows PRIMARY KEY CLUSTERED (PartitionKey, WindowStartUtc),
    CONSTRAINT CK_AdQuota_Utc CHECK (
        DATEPART(TZOFFSET, WindowStartUtc) = 0 AND
        WindowStartUtc > CONVERT(datetimeoffset(7), '0001-01-01T00:00:00+00:00') AND
        DATEPART(TZOFFSET, WindowEndUtc) = 0 AND
        WindowEndUtc > CONVERT(datetimeoffset(7), '0001-01-01T00:00:00+00:00')),
    CONSTRAINT CK_AdQuota_Window CHECK (WindowEndUtc > WindowStartUtc),
    CONSTRAINT CK_AdQuota_Version CHECK (PolicyVersion > 0),
    CONSTRAINT CK_AdQuota_Count CHECK (
        AcceptedLimit > 0 AND AcceptedCount >= 0 AND AcceptedCount <= AcceptedLimit)
);

CREATE TABLE dbo.AdvertisementAcceptedEvents (
    SourceEventId uniqueidentifier NOT NULL,
    AdvertisementId uniqueidentifier NOT NULL,
    EventType int NOT NULL,
    AcceptedAtUtc datetimeoffset(7) NOT NULL,
    CampaignId uniqueidentifier NULL,
    CampaignBindingId uniqueidentifier NOT NULL,
    CONSTRAINT PK_AdAcceptedEvents PRIMARY KEY NONCLUSTERED (SourceEventId),
    CONSTRAINT FK_AdFacts_Advertisement FOREIGN KEY (AdvertisementId)
        REFERENCES dbo.Advertisements (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_AdFacts_Receipt FOREIGN KEY
        (SourceEventId, AdvertisementId, EventType, AcceptedAtUtc, CampaignBindingId)
        REFERENCES dbo.AdvertisementEventRetryKeys
        (SourceEventId, AdvertisementId, EventType, AcceptedAtUtc, CampaignBindingId)
        ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT CK_AdFacts_Ids CHECK (
        AdvertisementId <> '00000000-0000-0000-0000-000000000000' AND
        SourceEventId <> '00000000-0000-0000-0000-000000000000' AND
        (CampaignId IS NULL OR CampaignId <> '00000000-0000-0000-0000-000000000000')),
    CONSTRAINT CK_AdFacts_Type CHECK (EventType IN (1, 2)),
    CONSTRAINT CK_AdFacts_Utc CHECK (DATEPART(TZOFFSET, AcceptedAtUtc) = 0 AND
        AcceptedAtUtc > CONVERT(datetimeoffset(7), '0001-01-01T00:00:00+00:00')),
    CONSTRAINT CK_AdFacts_CampaignBinding CHECK (
        CampaignBindingId = ISNULL(CampaignId, CONVERT(uniqueidentifier, '00000000-0000-0000-0000-000000000000')))
);

CREATE UNIQUE CLUSTERED INDEX CX_AdFacts_Time_Source
    ON dbo.AdvertisementAcceptedEvents (AcceptedAtUtc, SourceEventId);
CREATE NONCLUSTERED INDEX IX_AdFacts_Ad_Time_Source
    ON dbo.AdvertisementAcceptedEvents (AdvertisementId, AcceptedAtUtc, SourceEventId)
    INCLUDE (EventType, CampaignId);
CREATE NONCLUSTERED INDEX IX_AdFacts_Campaign_Time_Source
    ON dbo.AdvertisementAcceptedEvents (CampaignId, AcceptedAtUtc, SourceEventId)
    INCLUDE (AdvertisementId, EventType) WHERE CampaignId IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_AdQuota_End
    ON dbo.AdvertisementEventQuotaWindows (WindowEndUtc, PartitionKey, WindowStartUtc);
```

**BLOCKED FOR OWNER DECISION** The Campaign principal is absent from fetched develop and PR #273 supplies no persisted mapping. After approval, add the nullable receipt FK with checked existing rows and the filtered supporting index. Use the exact approved schema, table and unique key. Until that decision, disable sponsored intake. Any independently approved unattributed-only rollout needs a database CHECK requiring null attribution on receipts, version 1 only, and a controlled writer that admits only enumerated unattributed flows.

```sql
-- BLOCKED. Placeholder principal names intentionally require substitution.
ALTER TABLE dbo.AdvertisementEventRetryKeys WITH CHECK
ADD CONSTRAINT FK_AdReceipts_Campaign FOREIGN KEY (CampaignId)
    REFERENCES [ApprovedCampaignSchema].[ApprovedCampaignPrincipal] ([ApprovedUniqueGuidKey])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
CREATE NONCLUSTERED INDEX IX_AdReceipts_Campaign
    ON dbo.AdvertisementEventRetryKeys (CampaignId, SourceEventId)
    WHERE CampaignId IS NOT NULL;
```

The non-null binding tuple prevents a nullable composite FK from skipping review-critical checks. A receipt survives fact expiry, and its retention preserves replay response fields and restricts deletion of referenced principals. Quota rows have independent retention. The controlled transaction enforces creation of both rows on acceptance. EF review would map physical names/types explicitly, use `ValueGeneratedNever`, a required composite alternate key and `DeleteBehavior.NoAction`. Immutable rows need no rowversion. Quota updates use the guarded predicate. SQL lock and seek behavior remains unverified.

## Procedure contracts and atomic write

**PROPOSED** The trusted application authenticates the caller, checks admission/report scope, validates server-issued delivery proof where approved, and applies separate attempt limits. Procedure execution permission authenticates the service identity. It does not attest that any caller-supplied principal, digest, quota value or Campaign ID is trusted. No mobile input supplies a trusted Campaign or policy parameter. The routine receives typed, server-validated inputs and verifies canonical bindings and policy selection against approved authorities.

Candidate acceptance inputs are `@AdvertisementId uniqueidentifier`, `@EventType int`, `@RetryEventId uniqueidentifier`, `@CandidateSourceEventId uniqueidentifier`, `@ReceivedAtUtc datetimeoffset(7)`, `@BindingVersion smallint`, `@BindingDigest varbinary(max)` with exact length 32, and an optional validated stable delivery GUID for approved version 2. Delivery resolution and policy configuration source remain gates. Public response shape remains pending. Procedure outcomes return the internal disposition, then authorized receipt fields only for a matching accepted or duplicate result. Verify original source ID, timestamp and attribution using the receipt itself.

```text
REVIEW PSEUDOCODE — AcceptAdvertisementEvent
Require no ambient or nested caller transaction. XACT_ABORT ON; READ COMMITTED.
Validate IDs, typed codes, digest length, UTC receipt time and trusted context.
Keep receipt datetime, candidate source GUID and canonical identity immutable per attempt.

TRY:
  BEGIN TRANSACTION
  Seek retry PK (ad, type, retry ID) with UPDLOCK,HOLDLOCK, including missing key.
  If receipt exists:
      Validate payload against its pinned binding version and digest.
      On mismatch, end transaction with no writes and return Conflict.
      On match, end transaction with no writes and return Duplicate plus original receipt.
  If absent:
      Enforce version, current admission and exact canonical digest.
      Lock Advertisement and permitted attribution records in deterministic key order.
      Require enabled ad, Active (1), and StartAtUtc <= ReceivedAtUtc < EndAtUtc.
      Resolve authoritative delivered Campaign or an approved null; otherwise Unavailable.
      Resolve immutable policy effective at receipt time and derive partition and window.
      Lock quota PK with UPDLOCK,HOLDLOCK, including a missing key range.
      Recheck trusted operation age using DB UTC under the quota lock.
      Insert zero-count quota row if absent, using effective version and limit.
      If present, preserve its stored limit, version and boundaries; flag discrepancies.
      Guarded increment of the single quota row. Capture row count immediately.
      If zero rows updated, rollback and return RateLimited.
      INSERT receipt, then INSERT fact using identical source/ad/type/time/binding values.
      COMMIT. Return Accepted with stable receipt fields.
CATCH:
  Roll back if XACT_STATE() is nonzero, then THROW the original failure.
  Do not infer an accepted response from successful intermediate statements.
```

```sql
-- REVIEW ONLY. Illustration inside the protected bucket transaction.
UPDATE dbo.AdvertisementEventQuotaWindows
SET AcceptedCount = AcceptedCount + CONVERT(bigint, 1)
WHERE PartitionKey = @PartitionKey AND WindowStartUtc = @WindowStartUtc
  AND AcceptedCount < AcceptedLimit;
SET @UpdatedRows = @@ROWCOUNT;
```

The canonical identity and partition bytes, version policy and numeric candidates are fixed in the physical package and owner register. Arrival-time UTC is the candidate report timestamp. End-of-window enforcement for fresh keys checks database time under the bucket lock. An old request with an expired write age is rejected before creating a bucket. End the transaction before returning Unavailable, invalid admission, or Duplicate. Lock order is retry key → ad → attribution → quota → inserted rows. Reporting uses read-only routines with an approved transaction-level consistent snapshot and effective data scope. Both totals and trace use committed facts and the same filters. Receipt-only rows and legacy counters stay outside rollups.

**Failure classification** A fresh primary session verifies the exact retry key and binding after rollback or an ambiguous commit. Confirm the original outcome only when the stable source ID and digest agree. Another committed source with matching binding is Duplicate. A differing binding is Conflict. Absence must be decisive after waiting for the key to settle before retrying the whole transaction. Unverifiable outcomes surface a retryable failure and preserve the original key. Classify 2601/2627 by the implicated key and persisted evidence. A quota PK race can retry the unit. Unrelated unique failures propagate. A deadlock victim (1205) can retry with bounded jitter and unchanged operation identity, candidate maximum three attempts. Keep one retry owner between the EF execution strategy and the adapter. An in-memory lock establishes no SQL guarantee. [EF Core transaction recovery](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) · [SQL Server locking hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17)

## Controlled identities and append-only protection

```sql
-- REVIEW ONLY. Role and procedure names are candidates, not live identities.
-- Apply only after the reviewed routines exist and ownership is verified.
CREATE ROLE [M7AdEventWriter];
CREATE ROLE [M7AdEventReporting];
CREATE ROLE [M7AdEventRetention];

GRANT EXECUTE ON OBJECT::dbo.AcceptAdvertisementEvent TO [M7AdEventWriter];
GRANT EXECUTE ON OBJECT::dbo.VerifyAdvertisementEventCommit TO [M7AdEventWriter];
GRANT EXECUTE ON OBJECT::dbo.ReadAdvertisementEventTotals TO [M7AdEventReporting];
GRANT EXECUTE ON OBJECT::dbo.ReadAdvertisementEventTrace TO [M7AdEventReporting];
-- Retention grants remain gated on separate cleanup authorization.
GRANT EXECUTE ON OBJECT::dbo.ExpireAdvertisementFactBatch TO [M7AdEventRetention];
GRANT EXECUTE ON OBJECT::dbo.ExpireAdvertisementQuotaBatch TO [M7AdEventRetention];

DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.AdvertisementAcceptedEvents
    TO [M7AdEventWriter], [M7AdEventReporting], [M7AdEventRetention];
DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.AdvertisementEventRetryKeys
    TO [M7AdEventWriter], [M7AdEventReporting], [M7AdEventRetention];
DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.AdvertisementEventQuotaWindows
    TO [M7AdEventWriter], [M7AdEventReporting], [M7AdEventRetention];
```

Use reviewed same-owner static routines and table ownership chaining, verify it with each actual role, and avoid dynamic SQL, cross-database chaining and elevated memberships. Writer routines need protected reads of Advertisement, approved attribution and receipts, insert on receipts and facts, and insert/update on quota. Reporting routines read authorized accepted facts and expose no retry GUID, binding digest or quota data. Only separately approved retention routines delete eligible facts and quota rows. They never update facts or receipts or delete retained receipts. None receives schema change privileges, unrestricted table DML or a direct role assignment in this review. Deployment and test provisioning identities are separate. [SQL Server permissions](https://learn.microsoft.com/en-us/sql/t-sql/statements/grant-object-permissions-transact-sql)

## Disposable SQL provisioning handoff

All configuration below is **PENDING**. The coordinator supplies each value through an approved protected channel. These are review keys only. There is no connection string or executable fixture.

| Review key | Coordinator must supply or approve |
| --- | --- |
| Target and ownership | Isolated SQL instance and edition/version, TLS policy, approved test-only owner and provisioner, and authority to create and remove the disposable database. |
| Target allowlists | Exact expected connected server/instance identity and network endpoint, database-name allowlist for the active run, expected database ID after creation, and expected Advertisement schema and key. Never infer ownership from a name prefix. |
| Run identity | Naming convention candidate `Wayhunter_M7SqlReview_<run-guid>`, generated run GUID, coordinator-written ownership marker and recorded DB name/ID. Compare all identities against the approved provisioning manifest before writes and cleanup. |
| Explicit execution opt-in | Proposed `WAYHUNTER_M7_ISOLATED_SQL_TESTS=1` plus the run manifest. Missing or inconsistent configuration prevents execution. Metadata, connection and secret handling need approval. |
| Accounts | Separate provisioner, setup/migration runner, restricted writer, reporting, retention and verification identities. Approve grants and secret delivery. Runtime test identities have no create/drop/alter privileges. |
| Provider settings | Confirm database compatibility level, locking and row-versioning options, candidate write isolation and report snapshot setting, timeouts, pool/retry behavior and actual index plans. |
| Fixture and concurrency | Build fixtures only after all connected target guards pass. Use synthetic ads, approved synthetic Campaign relationship if available, independently opened SQL connections and DbContexts, command barriers, and a third observer for committed rows. |
| Cleanup and recovery | Provisioner rechecks manifest, connected server/database identity and run marker, closes run-owned sessions, and cleans only the marked disposable DB under approved authority. A failed guard halts automated cleanup and records the run for coordinator recovery. Review rollback for partial setup before execution. |
| Evidence | Capture target version and scoped database settings, synthetic IDs, sanitized outcomes, persisted row/quota counts, constraint and plan evidence, and cleanup status. Use no credentials, tokens, personal data or raw connection strings in logs. |

| Future isolated SQL scenario | Evidence required | Status |
| --- | --- | --- |
| Same-key replay race, including winner rollback | One committed receipt, fact and increment. Replays return the original receipt. Distinct type/ad scopes remain independent. | NOT EXECUTED |
| Last slot and absent bucket races | Exactly the available slots accepted, one bucket, and no overshoot at rollover or policy change. | NOT EXECUTED |
| Failure between quota, receipt and fact | Rollback leaves no partial writes. Same-key retry succeeds once. | NOT EXECUTED |
| Ambiguous commit and HTTP acknowledgement loss | Recover original stable receipt without duplicate increments. Cover confirmed commit, decisive absence and still-unknown outcome. | NOT EXECUTED |
| Conflicting payload and binding versions | Canonical vectors, mismatching delivery ID and rejected version switch. No quota or fact change. | NOT EXECUTED |
| Concurrent Campaign change and archive | Verify original attribution survives relink and replay. Verify history and FK restrictions with a separately approved principal. | NOT EXECUTED |
| Cleanup and retention race | Fact expiry leaves receipt, delayed writers cannot recreate an expired bucket, and retained receipts prevent reacceptance. | NOT EXECUTED |
| Procedure permissions, SQL constraints and report parity | Reject unauthorized direct writes and reads, enforce all constraints, test scoped snapshots and deadlock handling, reconcile counts without legacy totals. | NOT EXECUTED |

Create receipt and quota definitions before dependent facts, then trusted indexes and FKs, then controlled routines and grants after coordinator review. Review deployment, restore and forward-fix plans before migration ownership is assigned. After durable acceptances exist, disabling intake preserves history and replay protection. Removing stored facts or receipts requires separate authorization.

## M1 admission review dependency

The DDL and procedure sketches above are the pre-M1 physical baseline. The [M1 API contract review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md) proposes two additional receipt-only caller-binding fields and requires ownership verification ahead of payload comparison and receipt disclosure. Reconcile those changes, including commit verification and attempt limiting, before approving any executable definition. No SQL definition, migration or test execution is authorized by this addendum.
