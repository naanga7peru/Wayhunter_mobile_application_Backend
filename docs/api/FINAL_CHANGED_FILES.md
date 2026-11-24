# Changed files

## Post-PR #314 final runtime-hardening delta

Target branch: `task/final-demo-runtime-hardening`; base `develop` merge commit `1db9999`. No migrations or generated SQL artifacts are part of this delta.

- Modified `src/Wayhunter.Api/appsettings.Development.json`: Development session lifetime only; no secrets.
- Modified `tests/Wayhunter.UnitTests/Modules/DemoSeedRunner/DemoSeedRunnerSafetyTests.cs`: skip-voucher safety coverage.
- Modified `tools/Wayhunter.DemoSeedRunner/DemoSeedOptions.cs`, `DemoSeedRunnerService.cs`, and `Program.cs`: explicit `--skip-voucher` mode.
- Modified `tools/Wayhunter.DemoSeedRunner/StartDemoApiAndVerify.ps1` and `VerifyDemoLoginHome.ps1`: loopback/no-launch-profile startup settings, session lifetime, PowerShell 5.1 request-body handling, and `$homeResponse` naming.
- Added `tools/Wayhunter.DemoSeedRunner/InvokeDemoSeedSecurely.ps1`, `StartDemoApi.ps1`: secure prompting, explicit demo catalog targeting, DPAPI key reuse, and standalone loopback API launch that refuses an occupied port.
- Updated `docs/api/FINAL_BACKEND_DEMO_HANDOFF.md` and this inventory with actual demo verification and Flutter connection guidance.

Secret review: these files contain environment-variable names and runtime code that prompts for credentials or loads the current-user DPAPI key; they contain no password, signing key, voucher code, voucher protection key, bearer token, or actual provider credential. The Development appsettings change is only a 60-minute session lifetime. Scripts were inspected for target strings; demo writes target only `Wayhunter_Demo_Final`.

## Merged PR #314 inventory

The following inventory was prepared for the merged `task/m5-production-game-writes` branch; it is historical and included in merge commit `1db9999` on `develop`.

The merged PR inventory below is preserved; the final runtime-hardening delta above is intended for the separate branch and PR.

- `config/wayhunter.server.example.json`
- `docs/api/DEMO_SEED_RUNNER.md`
- `docs/api/FINAL_BACKEND_DEMO_HANDOFF.md`
- `docs/api/FINAL_CHANGED_FILES.md`
- `docs/api/wayhunter-swagger-endpoint-inventory.json`
- `src/Wayhunter.Api/Controllers/Admin/Games/GameReviewController.cs`
- `src/Wayhunter.Api/Controllers/Admin/Games/GameReviewControllerFeatureProvider.cs`
- `src/Wayhunter.Api/Controllers/Mobile/CoinsWallet/SampleVoucherRequestsController.cs`
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApplicationAdapter.cs`
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftsController.cs`
- `src/Wayhunter.Api/Controllers/Mobile/HomeAdsAnalytics/HomeDashboardController.cs`
- `src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/TripSafetySqlServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Program.cs`
- `src/Wayhunter.Api/Services/IdentitySocial/SessionBearerAuthenticationHandler.cs`
- `src/Wayhunter.Application/Modules/CoinsWallet/Commands/SampleVoucherRequestService.cs`
- `src/Wayhunter.Application/Modules/Games/Interfaces/IGameDraftCreateOperationStore.cs`
- `src/Wayhunter.Application/Modules/Games/Services/GameService.cs`
- `src/Wayhunter.Application/Modules/Games/Services/VerifiedGameDraftService.cs`
- `src/Wayhunter.Domain/Modules/Games/Entities/Game.cs`
- `src/Wayhunter.Domain/Modules/IdentitySocial/Entities/UserAccount.cs`
- `src/Wayhunter.Domain/Modules/TravelPlanning/Entities/TripPlan.cs`
- `src/Wayhunter.Domain/Modules/TravelPlanning/Entities/TripStop.cs`
- `src/Wayhunter.Infrastructure/Persistence/Configurations/Games/GameDraftCreateOperationRowConfiguration.cs`
- `src/Wayhunter.Infrastructure/Persistence/DbContexts/WayhunterSharedDbContext.cs`
- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261123175746_M5GameDraftCreateIdempotency.cs`
- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261123175746_M5GameDraftCreateIdempotency.Designer.cs`
- `src/Wayhunter.Infrastructure/Persistence/Migrations/WayhunterSharedDbContextModelSnapshot.cs`
- `src/Wayhunter.Infrastructure/Persistence/Models/Games/GameDraftCreateOperationRow.cs`
- `src/Wayhunter.Infrastructure/Persistence/Models/IdentitySocial/UserRoleAssignmentRow.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/InMemoryGameSubmissionStore.Repository.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameAdminReviewStore.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameRepository.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameSubmissionStore.cs`
- `src/Wayhunter.Infrastructure/Persistence/Seeding/SampleVoucherDemoSeed.cs`
- `tests/Wayhunter.IntegrationTests/Modules/CoinsWallet/M6SqlVoucherHttpE2ETests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/CoinsWallet/SqlCoinRewardConcurrencyTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftDevelopmentApiHttpTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/Games/M5SqlGameDraftAdminHttpE2ETests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/HomeAdsAnalytics/M7SqlAdvertisementHttpE2ETests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/M2SqlTripPlanHttpTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3GpsConsentSqlTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3HazardSqlPersistenceTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3InvitationDecisionSqlTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3LiveTripSessionSqlTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3LocalDbProfile.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3LocalDbProfileTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3MembershipRevocationSqlConcurrencyTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3MembershipRevocationSqlRollbackTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3SafeCheckInSqlTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TripSafety/M3SqlExecutionStrategyTests.cs`
- `tests/Wayhunter.UnitTests/Modules/CoinsWallet/SampleVoucherDemoSeedTests.cs`
- `tests/Wayhunter.UnitTests/Modules/DemoSeedRunner/DemoSeedRunnerSafetyTests.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/GameDraftUpdateServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/GamesServiceCollectionExtensionsTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/HomeDashboardControllerTests.cs`
- `tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj`
- `tools/Wayhunter.DemoSeedRunner/DemoSeedOptions.cs`
- `tools/Wayhunter.DemoSeedRunner/DemoSeedRunnerService.cs`
- `tools/Wayhunter.DemoSeedRunner/Program.cs`
- `tools/Wayhunter.DemoSeedRunner/Wayhunter.DemoSeedRunner.csproj`
- `Wayhunter.slnx`
