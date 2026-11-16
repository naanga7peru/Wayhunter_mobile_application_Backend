\# WAYHUNTER — M7 Advertisement SQL Schema \& Migration Readiness



\*\*Document ID:\*\* M7-MS02-T02

\*\*Module Owner:\*\* Thashvini — M7 Home, Ads \& Analytics

\*\*Date:\*\* 2026-11-15

\*\*Status:\*\* DRAFT — SCHEMA REVIEW REQUIRED

\*\*Migration Status:\*\* HOLD — NOT APPROVED



\---



\## 1. Purpose



This document records the proposed migration readiness of the WAYHUNTER Advertisement module.



It is intended for review by the Team Leader, DB Coordinator and all seven module owners before the shared SQL Server migration.



No migration has been created or applied by M7.



\## 2. Current Backend Implementation



The following M7 components have been implemented in the Backend repository:



\- Advertisement domain entity and lifecycle rules

\- AdvertisementPlacement enum

\- AdvertisementStatus enum

\- Impression and Click counters

\- HomeAdsAnalyticsDbContext

\- Advertisement EF Core Configuration

\- Advertisement read repository

\- Advertisement DTO and application service

\- Advertisement URL and request validators

\- Unit tests for domain rules, configuration and service behavior



\*\*Existing task:\*\* M7-MS02-T02 — Advertisement SQL Persistence.



The C# persistence foundation has been merged through PR #66, but actual SQL Server persistence and restart verification are not yet complete.



Additional M7-MS02-T03 application validation work is in progress on a separate task branch and must not be treated as merged or accepted until reviewed.



\## 3. Advertisement Database Schema



\*\*Proposed SQL table name:\*\* `Advertisements`



| Column | Data Type / Configuration | Purpose |

|---|---|---|

| Id | uniqueidentifier, PK, application-generated | Unique Advertisement ID |

| Title | nvarchar(200), required | Advertisement title |

| ImageUrl | nvarchar(2048), required | Image reference |

| DestinationUrl | nvarchar(2048), nullable | Advertisement destination |

| StartAtUtc | datetimeoffset, required | Start of validity |

| EndAtUtc | datetimeoffset, required | End of validity |

| IsEnabled | bit, required | Enabled/disabled flag |

| Placement | int, required | Advertisement placement |

| Status | int, required | Lifecycle status |

| ImpressionCount | bigint, required, default 0 | Recorded impressions |

| ClickCount | bigint, required, default 0 | Recorded clicks |



These definitions reflect the current Advertisement entity and EF Core configuration.



\### 3.1 Advertisement Placement



\- `Unspecified = 0` — invalid for new advertisements

\- `HomeBanner = 1`

\- `SponsoredPoi = 2`



\### 3.2 Advertisement Lifecycle



\- `Draft = 0`

\- `Active = 1`

\- `Paused = 2`

\- `Archived = 3`



Archived advertisements cannot be reactivated through the current domain behavior.



The `IsEnabled` and `Status` properties must remain consistent when write operations are implemented.



\## 4. Existing SQL Constraints and Index



\### Primary Key



`PK\_Advertisements` — Advertisement ID.



The final generated primary-key constraint name must be verified from the shared migration.



\### Valid Date Window



Existing SQL check constraint:



`CK\_Advertisements\_ValidWindow`



Condition:



`\[EndAtUtc] > \[StartAtUtc]`



\### Active Advertisement Index



Existing index:



`IX\_Advertisements\_Enabled\_Window`



Columns:



`IsEnabled, StartAtUtc, EndAtUtc`



\### Counter Defaults



\- ImpressionCount: `0`

\- ClickCount: `0`



\*\*Important:\*\* Current EF configuration does not yet define SQL check constraints limiting Placement or Status to their allowed enum values. Those rules are currently enforced primarily in application/domain validation. Any additional database constraints require schema review.



\## 5. DbContext Strategy



Existing context:



`HomeAdsAnalyticsDbContext`



Existing DbSet:



`DbSet<Advertisement> Advertisements`



Existing configuration:



`AdvertisementConfiguration`



\### Shared Migration Decision Required



The DB Coordinator must confirm whether the project will use:



\*\*Option A:\*\* A consolidated application DbContext.



\*\*Option B:\*\* Multiple module DbContexts with coordinated migration ownership.



M7 currently uses a module-specific DbContext.



M7 will not independently generate migrations until the shared approach is approved.



The final plan must ensure that modules do not generate competing migration histories or conflicting database objects.



\## 6. Campaign and District Relationships



The current Advertisement entity does not yet include persisted CampaignId, SponsorId or district-targeting relationships.



These relationships are \*\*PROPOSED / NOT APPROVED\*\*, not implemented database columns.



Items requiring review:



\- Advertisement-to-Campaign relationship

\- Sponsor ownership and sponsor metadata

\- District-specific advertisement targeting

\- Advertisement-to-sponsored-location relationship

\- Campaign start/end scheduling consistency

\- Delete and archive behavior across relationships

\- Foreign key and unique constraint decisions



The M7-MS03 Campaign milestone will define the related campaign model.



Do not add these relationships to the shared migration without explicit schema approval.



\## 7. Demo Advertisement Seed Plan



Seed data is required for repeatable academic demonstrations and testing.



\### Proposed Seed Records



| Record | Placement | Initial Status |

|---|---|---|

| Demo Home Banner | HomeBanner | Draft |

| Demo Sponsored POI | SponsoredPoi | Draft |



\### Seed Rules



1\. Use deterministic Advertisement IDs for repeatable seeds.

2\. Use sample HTTPS URLs with no personal data.

3\. Insert initial records as Draft and disabled.

4\. Validate start/end time windows.

5\. Avoid duplicate inserts when seed logic is run again.

6\. Do not overwrite real administrator-managed advertisements.

7\. Do not automatically credit Coins or rewards from seed advertisements.

8\. Document image availability and placeholder behavior.

9\. Review demo records and activation rules before insertion.



\*\*Status:\*\* Seed strategy proposed. Actual SQL/EF seed implementation is pending shared database approval.



\## 8. Required Security and Validation



The following controls are required before production-like API acceptance:



\- Admin-only authorization for advertisement create/edit/status/upload operations

\- JWT authentication and Admin RBAC integration with M1

\- HTTPS advertisement image and destination URL validation

\- Approved URL-host and media-upload policies

\- Valid date window checking

\- Valid placement and lifecycle status checking

\- Sri Lankan district validation

\- No unsafe public write endpoints

\- Secure Cloudinary upload integration when approved



The URL and request validators are currently application-layer foundations. They do not, by themselves, establish complete production security.



\## 9. Migration-Dependent Acceptance Tests



After the shared migration is approved and applied, M7 must verify:



\- Advertisements table is generated correctly

\- Primary key and SQL constraints exist

\- Placement and Status are persisted correctly

\- Image URL references are persisted correctly

\- ImpressionCount and ClickCount persist correctly

\- Records survive application restart

\- Seed process is repeatable without duplicates

\- Active advertisement reads work against actual SQL Server

\- Invalid date windows are rejected

\- Persistence does not conflict with other module schemas



These checks cannot be marked passed based only on unit tests or a successful build.



\## 10. Outstanding Tasks



| Task ID | Pending Work | Status |

|---|---|---|

| M7-MS02-T02 | Shared SQL migration, seed and persistence verification | BLOCKED |

| M7-MS02-T03 | API authorization, actual endpoint integration and district targeting | IN PROGRESS / DEPENDENCIES |

| M7-MS02-T04 | Admin Ads flow and secure upload | PENDING |

| M7-MS02-T06 | Fresh-clone end-to-end regression | PENDING |

| M7-MS03-T01 | Campaign and relationship schema | PENDING |



Other M7 milestone tasks continue according to the team tracker.



\## 11. Shared Migration Coordination



The shared migration process must be coordinated by the Team Leader-appointed DB Coordinator.



Required sequence:



1\. Each of the seven members submits schema proposals.

2\. Resolve the M2/M3 TripPlanId contract mismatch.

3\. Review all table names, keys, relationships and constraints.

4\. Finalize DbContext and migration ownership.

5\. Obtain explicit schema approval from all seven members.

6\. DB Coordinator prepares the shared migration.

7\. Team reviews and merges the migration PR.

8\. Apply the migration only to the authorized database.

9\. Run persistence, seed and restart checks.

10\. Record test evidence and final acceptance.



M7 must not run independent EF migration generation or database-update commands before authorization.



\## 12. M7 Open Decisions for Team Leader



\- \[ ] Approve DbContext and shared migration strategy

\- \[ ] Confirm campaign and sponsor relationship design

\- \[ ] Confirm district targeting persistence model

\- \[ ] Approve demo advertisement seed policy

\- \[ ] Confirm SQL constraint policy for lifecycle status and placement

\- \[ ] Assign DB Coordinator and migration ownership

\- \[ ] Approve shared migration after all module reviews

\- \[ ] Complete actual SQL persistence verification



\## 13. Approval Record



\*\*M7 Owner:\*\* Thashvini — Submitted for review, not yet approved



\*\*Team Leader:\*\* Pending



\*\*DB Coordinator:\*\* Pending



\*\*All seven module schema approvals:\*\* Pending



\*\*Shared Migration Authorization:\*\* NOT GRANTED



\---



\*\*Final Status:\*\* M7 advertisement schema documentation prepared for review. Migration remains on hold until explicit approval and centralized coordination.

---

## 14. Final M7 Advertisement SQL Migration Readiness Update

**Updated:** 2026-11-16

**Status:** EF SCHEMA PREPARATION VERIFIED

**Migration Execution:** HOLD - NOT AUTHORIZED

### 14.1 Approved DbContext Ownership

- WayhunterSharedDbContext is the authoritative EF model for the consolidated shared SQL migration.
- HomeAdsAnalyticsDbContext remains temporarily for module compatibility and isolated EF tests.
- M7 must not generate an independent migration history.
- Shared migration generation and execution remain under DB Coordinator coordination.

### 14.2 Approved Advertisement SQL Schema

Table: Advertisements

Primary Key:
- PK_Advertisements
- Column: Id
- SQL type: uniqueidentifier
- Application-generated GUID

CHECK Constraints:
- CK_Advertisements_ValidWindow
  SQL: [EndAtUtc] > [StartAtUtc]

- CK_Advertisements_Placement
  SQL: [Placement] IN (1, 2)

- CK_Advertisements_Status
  SQL: [Status] IN (0, 1, 2, 3)

Index:
- IX_Advertisements_Enabled_Window
- Columns: IsEnabled, StartAtUtc, EndAtUtc

Counter Columns:
- ImpressionCount: bigint, default 0
- ClickCount: bigint, default 0

Other Columns:
- Title: nvarchar(200), required
- ImageUrl: nvarchar(2048), required
- DestinationUrl: nvarchar(2048), nullable
- StartAtUtc: datetimeoffset, required
- EndAtUtc: datetimeoffset, required
- IsEnabled: bit, required
- Placement: int, required
- Status: int, required

These mappings have been prepared in AdvertisementConfiguration.cs.
EF metadata tests have passed. Actual SQL Server schema
creation has not been verified.

### 14.3 Deterministic Demo Advertisement Seed Preparation

Implementation:
src/Wayhunter.Infrastructure/Persistence/Seeding/AdvertisementDemoSeed.cs

Demo Home Banner:
- ID: a1000000-0000-4000-8000-000000000001
- Placement: HomeBanner
- Initial Status: Draft
- IsEnabled: false

Demo Sponsored POI:
- ID: a1000000-0000-4000-8000-000000000002
- Placement: SponsoredPoi
- Initial Status: Draft
- IsEnabled: false

Both records:
- Use sample HTTPS URLs
- Have valid UTC date windows
- Start with zero impressions and clicks
- Use fixed IDs for repeatability

Duplicate Prevention:
- GetMissingRecords checks supplied existing advertisement IDs.
- Existing IDs are excluded from the prepared records.
- Existing administrator-managed records are not modified.
- No database insert or update is performed.
- Database concurrency safety is not yet verified.

The sample image URLs are placeholders.
Actual image availability is not guaranteed.

### 14.4 Test Evidence

AdvertisementConfigurationTests:
- Total: 13
- Passed: 13
- Failed: 0

AdvertisementDemoSeedTests:
- Total: 8
- Passed: 8
- Failed: 0

Full Backend Regression:
- Total: 2427
- Passed: 2424
- Failed: 0
- Skipped: 3

The three skipped tests require an isolated M4 SQL
test database that is not configured.

Restore: PASSED
Build: PASSED
Full test run: PASSED
Git diff whitespace check: PASSED

The test logs also contain expected-host-validation
failure messages for invalid AI provider configuration
and missing Maps provider configuration. These messages
did not cause the full test run to fail.

### 14.5 Future Relationships

The following are outside the current approved
Advertisement SQL schema:

- CampaignId
- SponsorId
- District targeting relationships
- Sponsored-location relationships
- Related foreign keys and unique constraints

These require separate schema review before inclusion
in the consolidated migration.

### 14.6 Remaining Work

- Shared migration generation by the authorized DB Coordinator
- Review of generated SQL schema and constraints
- Authorized database migration application
- SQL persistence and restart verification
- Seed execution and concurrency-safe duplicate prevention
- Actual image URL and endpoint integration verification
- Final database acceptance evidence

M7 must not independently run:
- dotnet ef migrations add
- dotnet ef database update
- Manual SQL DDL or schema modification

### 14.7 Approval and Execution Status

M7 Advertisement schema preparation:
COMPLETED FOR PR REVIEW

Team Leader-approved design decisions:
RECORDED

Consolidated SQL migration execution:
NOT AUTHORIZED

Actual SQL Server persistence:
NOT VERIFIED
