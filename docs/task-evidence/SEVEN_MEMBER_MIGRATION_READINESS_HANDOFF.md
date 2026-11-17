# Seven-member migration readiness handoff

Date: 2026-11-17

## Repository state

- Active local branch: `integration/seven-member-migration`
- Base/tracking ref: `origin/develop`
- Base commit: `5c73ec5bdde056ca67a3b6ec5c6e8bbc55f9736a`
- Preserved review branch: `review/pr-141` at `10300f619f9eaa55bb964fb6fda462f5a063cb0f`
- Push/PR/commit: not performed
- EF migration generation/application: not performed
- Database commands or manual DDL: not performed

## Evidence classification

- `SOURCE`: `AGENTS.md`, `docs/second-brain/ACTIVE_RULES.md`, and
  `docs/second-brain/CONTEXT.md` were read before edits. The separate vault
  Master BRD was not exposed by the current MCP inventory, so no unavailable
  BRD detail was treated as approved.
- `CODE_VERIFIED`: the shared EF metadata, DI registration, authentication
  integration, build, and test results listed below were executed locally.
- `PROPOSED`: the new M5 persistence rows/configurations are local migration
  readiness code. They are not an applied schema and do not prove SQL runtime
  completion.
- `DECISION_REQUIRED`: voucher debit timing, existing-data migration strategy,
  M4 direct identity FK strategy, and incomplete runtime SQL stores remain open.

## Integrated changes

### M1 / PR #141 review

Selected the authentication and shared-persistence implementation plus its
direct tests from `review/pr-141`. The stale PR evidence note was not copied.
The integration keeps authentication disabled/fail-closed by default and uses
`WayhunterSharedDbContext` when explicitly enabled.

### Shared EF model

- One authoritative `WayhunterSharedDbContext` registration helper is reused by
  M1, M2 SQL opt-in, and M7 SQL opt-in.
- M7 `AdvertisementRepository` now depends on the shared context; the duplicate
  `HomeAdsAnalyticsDbContext` was removed.
- M6 voucher owner and reward recipient IDs now have retention-safe `NoAction`
  foreign keys to the canonical M1 user.
- The shared model has no duplicate physical table mappings and no automatic
  seed rows.

### M5 Games readiness

Added persistence rows, configurations, and shared DbSets for:

- Games, steps, hints, attempts, progress, and proof evidence
- Pending Admin completion results and completion-evidence links
- One-winner-per-game records
- Creation approval, moderation, proof review, and result-decision audit rows

The model includes canonical 256-character string user FKs, `NoAction` delete
behavior, 720-hour approval expiry, unique winner/attempt/evidence constraints,
rowversion concurrency tokens, and unique audit correlation IDs. No M5 SQL
repository or migration was invented.

### Test-harness correction

The full run initially exposed three startup-validation tests whose intended
`OptionsValidationException` was masked by Windows Event Log access denial.
Those test factories now clear external logging providers. Production logging
and the original validation type/message assertions are unchanged.

## Changed files

### API/runtime

- `src/Wayhunter.Api/Program.cs`
- `src/Wayhunter.Api/Controllers/Mobile/IdentitySocial/SessionLogoutController.cs`
- `src/Wayhunter.Api/Extensions/HomeAdsAnalyticsServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/IdentityPersistenceServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/SessionAuthenticationServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/SharedPersistenceServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/TravelPlanningSqlServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Services/IdentitySocial/DisabledIdentityAuthenticationHandler.cs`
- `src/Wayhunter.Api/Services/IdentitySocial/SessionBearerAuthenticationHandler.cs`
- `src/Wayhunter.Api/Services/IdentitySocial/SqlAuthenticatedSessionReader.cs`
- `src/Wayhunter.Api/Services/IdentitySocial/SqlIdentitySessionRevocationStore.cs`

### Infrastructure/model

- `src/Wayhunter.Infrastructure/Persistence/Configurations/CoinsWallet/CoinVoucherOutboxConfigurations.cs`
- `src/Wayhunter.Infrastructure/Persistence/Configurations/Games/GameAuditPersistenceConfigurations.cs`
- `src/Wayhunter.Infrastructure/Persistence/Configurations/Games/GamePersistenceConfigurations.cs`
- `src/Wayhunter.Infrastructure/Persistence/DbContexts/HomeAdsAnalyticsDbContext.cs` (removed)
- `src/Wayhunter.Infrastructure/Persistence/DbContexts/WayhunterSharedDbContext.cs`
- `src/Wayhunter.Infrastructure/Persistence/Models/Games/GameAuditPersistenceModels.cs`
- `src/Wayhunter.Infrastructure/Persistence/Models/Games/GamePersistenceModels.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/HomeAdsAnalytics/AdvertisementRepository.cs`

### Integration tests

- `tests/Wayhunter.IntegrationTests/CrossModuleAuthorizationHttpTests.cs`
- `tests/Wayhunter.IntegrationTests/ProgramAuthenticationStartupTests.cs`
- `tests/Wayhunter.IntegrationTests/SessionBearerHttpIntegrationTests.cs`
- `tests/Wayhunter.IntegrationTests/SessionLogoutHttpIntegrationTests.cs`
- `tests/Wayhunter.IntegrationTests/ValidJwtCrossModuleHttpTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/HomeAdsAnalytics/AdvertisementRuntimeDiGuardTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/AiConfigurationStartupTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/MapsConfigurationStartupTests.cs`

### Unit/model tests

- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/AdvertisementConfigurationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/HomeAdsAnalyticsServiceRegistrationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/IdentityEfModelTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/IdentityEfSchemaCoverageTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/IdentityPersistenceRegistrationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/SessionAuthenticationRegistrationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/SessionBearerAuthenticationHandlerTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/SessionLogoutControllerTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/WayhunterSharedDbContextModelTests.cs`
- `tests/Wayhunter.UnitTests/Modules/SharedPersistence/M5GamesAuditSqlModelTests.cs`
- `tests/Wayhunter.UnitTests/Modules/SharedPersistence/M5GamesSqlModelTests.cs`
- `tests/Wayhunter.UnitTests/Modules/SharedPersistence/SevenModuleMigrationReadinessTests.cs`

## Verification results

- `dotnet build Wayhunter.slnx`: **passed**, 0 warnings, 0 errors.
- `dotnet test Wayhunter.slnx`: unit **2,594 passed, 0 failed, 0 skipped**;
  integration **53 passed, 0 failed, 3 skipped** (56 total).
- Focused relational model tests: 47 passed, then updated M6 ownership subset
  27 passed; these are included in the full unit total.
- `dotnet ef migrations has-pending-model-changes`: **no pending changes**.
- `git diff --check`: **passed**

The three skipped tests require the isolated SQL database connection variable
`WAYHUNTER_M4_TEST_SQL_CONNECTION` and restrict the database name to
`Wayhunter_M4_IntegrationTest`. No database was contacted. No disposable SQL
connection was configured, so actual FK/unique enforcement and M4
concurrency/rollback or migration apply/repeat/rollback/reapply were not tested.

## Remaining migration blockers

1. **Voucher timing — owner decision required.** Active rules require debit on
   Admin approval, while current M6 policy/aggregate code debits on successful
   issue. No voucher behavior was changed.
2. **Database state — owner decision required.** Confirm whether the target is
   fresh or contains data. No initial/baseline or data-preserving migration
   strategy can be selected safely before that answer.
3. **M4 identity FK.** M1 stores `UserId` as string/256; capture sessions store
   `HunterId` as `Guid`. The approved runtime mapping validates string GUIDs,
   but the direct physical FK strategy remains undecided.
4. **M5 runtime persistence.** EF mappings exist locally, but SQL repositories,
   transactional Admin decision/winner flows, proof step FK strategy, and an
   active-attempt status/index contract are not yet implemented/approved.
5. **M6 runtime persistence.** Shared EF mappings exist, but the application
   still has an in-memory wallet prototype rather than an authoritative shared
   SQL store and transaction boundary.
6. **M3/M4 runtime composition.** Several shared mappings and SQL repositories
   exist, but the default API still uses partial in-memory service graphs; the
   three isolated M4 SQL tests were not run without the dedicated database.
7. **Migration ownership.** No migration files, design-time migration factory,
   seed plan, production connection, or deployment order is approved. Appoint
   the coordinator only after the preceding decisions and seven-member review.

## Risk note

Passing metadata and in-memory HTTP tests proves model/DI consistency only. It
does not prove generated migration DDL against an existing database, SQL
transactions under concurrency, or production secret/configuration readiness.
