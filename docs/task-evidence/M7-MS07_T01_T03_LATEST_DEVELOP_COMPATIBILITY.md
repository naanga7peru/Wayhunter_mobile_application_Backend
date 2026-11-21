# M7-MS07-T01/T03 latest develop compatibility

**Verification** Read-only Git and source inspection, plus a normal develop-to-review synchronization. No database connection used.

| Ref or event | Exact commit |
| --- | --- |
| Original shared baseline | `13553911febb44343bf51227c08db470712bf804` |
| Coordinator-reviewed correction tip | `239bc059bebd6dfdb2e51bc4d7e8030dc0bd3819` |
| Fetched develop, merge of PR #275 | `77d1f57ee717e012efc2d7dca1ad9710de28020b` |
| Fetched Campaign PR #273 head | `10c8812acc6eec534e0e52af20a1124183da29b1` |
| Normal merge of develop into the review branch | `0feb3e99168f7e9d32072bacd07d6ed255128e9b` |

## Advertisement principal

The following paths were inspected at fetched develop. Shorthand paths in this table are relative to the repository root.

| Evidence path | Finding |
| --- | --- |
| `src/Wayhunter.Domain/Modules/HomeAdsAnalytics/Entities/Advertisement.cs` | Application entity carries GUID identity, schedule, enabled and lifecycle state, placement and lifetime counters. |
| `src/Wayhunter.Infrastructure/Persistence/Configurations/HomeAdsAnalytics/AdvertisementConfiguration.cs` | `Advertisements` with no explicit schema. `PK_Advertisements(Id)`, `uniqueidentifier NOT NULL`, application-assigned via `ValueGeneratedNever`. Required `Title nvarchar(200)`, `ImageUrl nvarchar(2048)`, optional `DestinationUrl nvarchar(2048)`. Required `StartAtUtc` and `EndAtUtc` as `datetimeoffset`, `IsEnabled bit`, `Placement int`, `Status int`, and `ImpressionCount` and `ClickCount` as `bigint` with default 0. Schedule, placement and status CHECKs. `IX_Advertisements_Enabled_Window` over enabled and schedule. |
| `src/Wayhunter.Infrastructure/Persistence/DbContexts/WayhunterSharedDbContext.cs` | `DbSet<Advertisement> Advertisements` and explicit registration of `AdvertisementConfiguration`. No explicit default schema and no Campaign DbSet or configuration found. |
| `src/Wayhunter.Infrastructure/Persistence/Migrations/20261117093023_InitialSharedSchema.cs` and `20261117093023_InitialSharedSchema.Designer.cs` | Existing migration defines the Advertisement PK and matching columns and checks. The Advertisement table creation has no explicit schema. The migration has no inbound FK to Advertisements or associated cascade behavior. Proposed M7 FKs use NO ACTION after target validation. |
| `src/Wayhunter.Infrastructure/Persistence/Migrations/WayhunterSharedDbContextModelSnapshot.cs` | Snapshot maps Advertisement to `ToTable("Advertisements", null, ...)` with matching key and checks. No Campaign principal. |
| `src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql` | Checked-in script contains unqualified `CREATE TABLE [Advertisements]`. Actual target schema is unverified. Before generating a future qualified FK, coordinator must confirm schema, object and principal key in the approved disposable environment. |

SQL Server's `datetimeoffset` mapping is unqualified for old Advertisement dates. The new proposal explicitly uses `datetimeoffset(7)` and UTC checks on event and quota columns. The FK joins GUIDs, so timestamp precision on the Advertisement table is independent of the proposed FK. No database name, real database schema resolution or deployed migration state was inferred.

## Campaign and migration compatibility

The fetched PR adds `src/Wayhunter.Domain/Modules/HomeAdsAnalytics/Entities/Campaign.cs`, the two Campaign enums, `CampaignDomainTests.cs` under the M7 unit tests, and its evidence document. The domain has GUID `Id`, GUID `ContentId`, sponsor, district and UTC lifecycle properties. No EF entity registration, migration, snapshot or unique Campaign SQL key exists in that PR delta. The PR tip is outside develop ancestry and no Campaign principal was found on develop. The proposed receipt FK remains blocked, including its qualified target, unique key, cardinality and historical delete behavior.

The baseline-to-develop migration, snapshot, shared DbContext and Advertisement mapping diffs were empty at `77d1f57`. New develop changes came through M3 readiness and TripSafety code/tests. The normal merge into the feature branch completed without conflicts. Task scope is the three new M7 review documents plus the physical package and original proposal. The feature delta contains Markdown only relative to develop. No merges into develop and no edits to unrelated code, migrations or snapshots were made. No .NET build or database test was run for this documentation revision. Prior harness evidence remains application-only evidence.
