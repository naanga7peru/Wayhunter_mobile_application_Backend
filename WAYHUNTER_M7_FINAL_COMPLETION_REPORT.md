# WAYHUNTER M7 HomeAdsAnalytics Backend Completion Report

**Date:** 2026-11-22

**Worktree:** `C:\Users\Admin\Desktop\WAYHUNTER\WAYHUNTER_M7_COMPLETION`

**Branch:** `task/m7-homeads-final-completion`

**Baseline:** `e3b0e9336e9f19003811f01649491c27fa3730a5` (`develop`, same commit at task start)
**Working state:** Local changes only; no commit, push, or merge performed.

## Scope and evidence

The repository was clean at the start. The local active rules and context, M7 scope, M7 campaign foundation evidence, advertisement API and SQL readiness notes, source code, and existing unit and integration tests were inspected. The M7 acceptance brief and draft Master BRD are referenced in the separate Second Brain but were not available locally; the configured `wayhunter_vault` MCP server was absent from the tool inventory for this session. Requirements below are therefore identified as local code/evidence or pending owner/DB decisions, not as inferred BRD approvals.

The repository tip already contained M7 campaign foundation PR #273 and prior advertisement API, RBAC, persistence mapping, image upload, and shared EF model work. No implementation was copied from unmerged branches. GitHub search found open M7 PR #279, a documentation-only SQL analytics persistence proposal. It has significant design overlap with event tracking and reporting but no code overlap with this report's active-ad query change. Open PR #288 is an M1/M2/M5 implementation and does not overlap this change.

## Existing functionality

- Advertisement domain fields, UTC schedule window, approved `HomeBanner` and `SponsoredPoi` placements, `Draft`/`Active`/`Paused`/`Archived` statuses, enable/disable/archive behavior, and aggregate impression/click counters.
- Mobile authenticated active-ad read route, optional placement filter, server-owned current time, and rejection of unapproved query parameters. District targeting and client-selected time remain unsupported.
- Admin advertisement list/detail, draft creation, update, status change, centralized Admin authorization policy, and controller-side defense-in-depth role check.
- Advertisement request and URL validation; EF Core entity mapping, approved placement/status/window constraints, and shared-context repository registration behind runtime configuration gates.
- Demo seed factory for one home banner and one sponsored POI. It prepares only missing deterministic records and does not write to a database itself.
- Admin image-upload route with file validation, feature gates, and an unavailable provider by default. Real provider activation and durable SQL reconciliation remain unverified.
- Campaign domain lifecycle foundation merged in #273. Campaign API, persistence, budget safeguards and approved cross-module eligibility/attribution are not complete.

## Newly implemented code

Active advertisement selection now has a required repository query contract. `AdvertisementService` delegates its active, placement, and validated internal query paths to that contract. The production EF repository applies enabled, active-status, half-open UTC schedule (`StartAtUtc <= nowUtc < EndAtUtc`) and optional placement filters in the database query, projects only the existing response DTO, and forwards cancellation. The predicates use EF-supported equality and comparison expressions and remain in the `IQueryable` through `ToListAsync`; no client-side filtering is used by this repository. It rejects a non-UTC query timestamp and unsupported placements. There is no interface fallback that loads all advertisements for client-side filtering. The API contract defines no result ordering, so no ordering behavior was added.

`StartAtUtc`, `EndAtUtc`, `IsEnabled`, `Placement`, and `Status` are non-nullable in the EF mapping and shared migration. The optional `DestinationUrl` is not used in filtering. Regression coverage includes exact start inclusion, exact end exclusion, future and expired windows, paused/disabled state, and a placement mismatch.

No new HTTP route, query parameter, placement, status, table, migration, analytics event, or provider integration was added. District filtering still fails closed.

## Complete M7 API endpoint inventory

| Method | Route | Access | Behavior |
|---|---|---|---|
| GET | `/api/mobile/advertisements/active` | Authenticated user | Active ads by server time; optional `placement=HomeBanner` or `SponsoredPoi`. |
| GET | `/api/admin/advertisements` | Admin policy | List advertisements. |
| GET | `/api/admin/advertisements/{advertisementId:guid}` | Admin policy | Read one advertisement. |
| POST | `/api/admin/advertisements` | Admin policy | Create a draft advertisement. |
| PUT | `/api/admin/advertisements/{advertisementId:guid}` | Admin policy | Update advertisement details. |
| PATCH | `/api/admin/advertisements/{advertisementId:guid}/status` | Admin policy | Activate, pause, or archive under domain lifecycle rules. |
| POST | `/api/admin/advertisements/{advertisementId:guid}/image` | Admin policy plus feature/provider gates | Multipart image replacement; returns unavailable while gates/dependencies are not ready. |

There are no implemented Campaign management, impression/click intake, analytics dashboard, or report endpoints in the inspected API controllers. Existing entity counters are not evidence of trusted event collection or deduplication.

## Files changed

- `src/Wayhunter.Application/Modules/HomeAdsAnalytics/Interfaces/IAdvertisementRepository.cs`
- `src/Wayhunter.Application/Modules/HomeAdsAnalytics/Services/AdvertisementService.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/HomeAdsAnalytics/AdvertisementRepository.cs`
- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/AdvertisementServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/AdvertisementRepositoryPersistenceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/ActiveAdvertisementsControllerTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/AdminAdvertisementsControllerTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/HomeAdsAnalytics/AdvertisementAdminPositiveHttpTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/HomeAdsAnalytics/M1M7RealBearerAdvertisementHttpTests.cs`
- `tests/Wayhunter.IntegrationTests/AdvertisementAuthorizationHttpTests.cs`
- `WAYHUNTER_M7_FINAL_COMPLETION_REPORT.md`

## Build and test results

- `dotnet restore Wayhunter.slnx`: completed from available package cache. NuGet vulnerability metadata lookup could not reach `api.nuget.org` and emitted NU1900 warnings.
- `dotnet build Wayhunter.slnx --no-restore`: passed, 0 errors. NU1900 vulnerability metadata warnings remained; build also reports existing unrelated unit analyzer warnings when a full compile occurs.
- Full unit suite: **3,258 passed, 0 failed, 0 skipped**.
- Non-SQL integration suite: **174 passed, 0 failed, 0 skipped**. Filter excluded `Sql`, `LocalDb`, `ValidJwtCrossModuleHttpTests`, and `ProgramAuthenticationStartupTests` classes to prevent shared/live SQL execution or mixed SQL-backed flows. `Logging__EventLog__LogLevel__Default=None` was set only for the test process because the default Windows Event Log provider is not writable in this environment.
- Focused M7/auth HTTP integration selection: **27 passed, 0 failed**; includes the new mobile active-listing HTTP regression. Counts overlap the broader integration run and are not additive.
- Focused M7 unit selection: **434 passed, 0 failed, 0 skipped**; included in the full unit suite count.
- An initial focused integration attempt without the Event Log override failed because the test host's exception logger tried to write to the protected `.NET Runtime` event source; rerunning with the documented override passed.
- `git diff --check`: passed at the time of report preparation.
- SQL Server, live database, and Cloudinary/provider tests were not run.

## PR overlaps

- **#273 — campaign domain foundation:** already merged into develop at the task baseline. Its local evidence marks the campaign domain slice implemented and API/persistence/eligibility decisions pending.
- **#279 — M7-MS07-T01/T03 SQL persistence proposal:** open and documentation-only. The proposal requires design/schema/privacy/DB approval before event ingestion or dashboard implementation. This change does not implement its event model and does not duplicate its files.
- **#288:** open M1/M2/M5 implementation; no detected HomeAdsAnalytics overlap.
- Local origin refs also contain prior M7 API/RBAC, advertisement persistence, runtime integration, and final SQL readiness branches. The current develop baseline includes their merged code; this task did not cherry-pick, merge, or modify other branches/worktrees.

## SQL and provider blockers

- Local `docs/api/M7_MS02_T02_ADS_SCHEMA_MIGRATION_READINESS.md` identifies the M7 advertisement schema review as draft and migration execution as **HOLD — NOT APPROVED**. A shared initial migration and model snapshot exist in source; their presence does not prove deployment or owner sign-off. No migration or live SQL operation was performed.
- Shared SQL registration remains configuration-gated. This batch's repository test uses EF InMemory and does not establish SQL translation, query plans, schema compatibility, migration status, restart persistence, or live concurrency behavior.
- The analytics proposal remains pending physical schema, retention, privacy, authentication/replay identity, attribution, quota, and isolated SQL acceptance decisions. No fake production analytics events were created.
- The image storage provider remains unavailable by default. No Cloudinary credentials were used and no provider was activated.
- Second Brain M7 task acceptance details and the draft Master BRD were not available from the session's local files or tool inventory; confirm current decisions before expanding public contracts or persistence.

## Pending M7 tasks

Based on local M7 evidence, remaining or partial backend work includes:

- M7-MS02-T02: coordinator-approved advertisement schema/migration and isolated SQL restart verification.
- M7-MS02-T03: complete trusted Admin source and production role storage verification; district behavior still requires an approved canonical targeting contract.
- M7-MS02-T04: provider-backed image lifecycle acceptance and durable reconciliation only after the provider and SQL gates are approved.
- M7-MS02-T05: AdMob readiness remains blocked on D16 approval/access and consent verification.
- M7-MS03-T02: Campaign API after persistence, authorization, and relationship contracts are agreed.
- M7-MS03-T04: budget caps, concurrency, and duplicate reward-source policy remain decision-dependent.
- M7-MS04 dashboard and M7-MS05 reports: authoritative cross-module reads, eligible statuses, filters, and privacy/access decisions are pending.
- M7-MS06 sponsored places: M2/M4/M5 eligibility and placement contracts are pending.
- M7-MS07-T01/T02/T03 analytics: event persistence, independent deduplication/replay protection, rate limits, trustworthy attribution, reporting parity, retention, and audit references are pending design and SQL approval.
- M7-MS09-T03/T05 SQL coordination and shared interfaces remain partial/pending; client integration, fresh-clone verification, privacy QA, cross-module UAT, and handover are not established by backend tests here.

## Recommended next safe implementation batch

Complete an isolated SQL readiness package review with the DB coordinator for the existing advertisement schema and shared migration; then, only after explicit approval, run the advertisement repository/migration/restart suite against a dedicated disposable SQL Server database with identity allowlisting and no provider activation. For analytics, review and resolve PR #279's remaining contract, privacy, retention, and physical-schema decisions before implementing ingestion. In parallel, obtain the approved Second Brain M7 task matrix and current Master BRD so remaining M7 tasks can be mapped to accepted criteria without inventing API or reporting behavior.
