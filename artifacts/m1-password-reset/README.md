# M1 password-reset migration review package

## Review basis

- Latest fetched `origin/develop`: `a708a3a` (`Merge pull request #268 ...`), fetched 2026-11-21.
- Requested proposal commit: `fad5e19cf5f5d036fef122b84a869cdb0860f674`. Its only file change is the M1 proposal document. This isolated branch is based on latest `origin/develop` and includes that proposal as commit `a08d65e` (cherry-pick).
- Migration ID reserved for this candidate: `20261121100000_AddIdentityPasswordResetCredentials`.
- Scope: one additive M1 table only.

## Candidate files

- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261121100000_AddIdentityPasswordResetCredentials.cs` — `Up` creates only `IdentityPasswordResetCredentials`; `Down` drops only that table.
- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261121100000_AddIdentityPasswordResetCredentials.Designer.cs` — target model copied from the last committed snapshot plus only the reset entity and `Users` relationship.
- `src/Wayhunter.Infrastructure/Persistence/Migrations/WayhunterSharedDbContextModelSnapshot.cs` — candidate current snapshot, same isolated reset-table delta.
- `artifacts/m1-password-reset/20261121100000_AddIdentityPasswordResetCredentials.from-InitialSharedSchema.sql` — candidate SQL Server script from `InitialSharedSchema` through this migration.

**Generation limitation:** The migration operations, designer and snapshot were assembled locally from the checked-in EF mapping and last snapshot; they were not emitted or validated by `dotnet ef`. Builds stop at Roslyn/MSBuild with `Access to the path '\\.\pipe\LOCAL\dotnet_…' is denied`, including with shared compilation disabled and elevated execution. The SQL script is likewise a hand-assembled EF-shaped candidate, not a generated script. Regenerate all three EF artifacts with EF tooling and compare before approval. Do not treat this package as ready to apply.

## Exact intended operations

`Up` creates `IdentityPasswordResetCredentials` with `CredentialId` PK, `UserId` FK to `Users.UserId` (`NO ACTION`), the mapping's expiry check, then the unique `TokenHash` index and (`UserId`, `ExpiresAtUtc`) index. `Down` drops only `IdentityPasswordResetCredentials`; it does not drop or alter `Users` or any existing object.

`InitialSharedSchema`, `20261117093023_InitialSharedSchema.Designer.cs`, and `InitialSharedSchema.idempotent.sql` are preserved unchanged.

## Unrelated pending EF model drift

The checked-in snapshot has none of the reset entity. The latest context model also contains unrelated mappings absent from that snapshot:

- M3: `TripSafety_SafeCheckIns`, `TripSafety_SafeCheckInOutbox`, `TripSafety_TripMemories`, and `TripSafety_TripMemoryMedia`.
- M8: `TripSafety_NotificationEvents`, `TripSafety_NotificationRecipients`, `TripSafety_NotificationPreferences`, `TripSafety_NotificationDevices`, `TripSafety_NotificationTokenBindings`, `TripSafety_NotificationJobs`, `TripSafety_NotificationJobRecipients`, `TripSafety_NotificationAudienceReceipts`, `TripSafety_NotificationOperations`, and `TripSafety_NotificationDeliveryAudit`.
- M3/M8 relationship configuration also introduces a composite principal key on existing `TripSafety_LiveTripSessions` (`SessionId`, `TripPlanId`) to support those mappings.

Those model changes are deliberately absent from both the candidate migration target model and snapshot. The latest branch has additional M7 advertisement upload configuration source files, but they are not registered in `WayhunterSharedDbContext` and do not currently contribute entity mappings to its EF model.

This is a source-level inventory, not an EF `IMigrationsModelDiffer` run. Compare it against the regenerated EF pending model diff after the build/tooling issue is resolved.

## Local disposable SQL Server target and initialization

- Server/instance target: `(localdb)\\MSSQLLocalDB` (Windows SQL Server LocalDB named instance).
- Database target: `Wayhunter_M1_Verify` only.
- Read-only pre-execution check supplied by the DB Coordinator: `20261117093023_InitialSharedSchema` is present; `20261121100000_AddIdentityPasswordResetCredentials` is absent; reset-table lookup returned `NULL`; migration execution approval is pending.
- A local `sqllocaldb info` probe listed `MSSQLLocalDB`, but querying its details failed here with “Unexpected error occurred while trying to access the LocalDB instance registry configuration.” No instance was started and no database was created by this work.

After DB Coordinator review, use a disposable local profile only:

1. Start the named LocalDB instance: `sqllocaldb start MSSQLLocalDB`.
2. Confirm `Wayhunter_M1_Verify` does not already exist, then create it with Windows integrated authentication, e.g. `sqlcmd -S "(localdb)\\MSSQLLocalDB" -E -Q "CREATE DATABASE [Wayhunter_M1_Verify]"`.
3. Initialize only the baseline: run `dotnet ef database update 20261117093023_InitialSharedSchema --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --connection "Server=(localdb)\\MSSQLLocalDB;Database=Wayhunter_M1_Verify;Integrated Security=True;TrustServerCertificate=True"`.
4. Inspect the migration history and baseline schema. For a later approved verification run, apply the reviewed script in this package to that database. This package did not execute any of these commands or SQL.

## Verification state

- `InitialSharedSchema` and its designer: unchanged.
- SQL execution/database update: not run.
- Read-only target check: baseline present, reset migration absent, reset table absent, approval pending (per DB Coordinator).
- Migration generation: EF tooling attempted; blocked before generation by Roslyn/MSBuild named-pipe access failure.
- Tests: none run.
- Database instance inspection: LocalDB name found; registry query failed as noted above.
- Remaining approval/decision: DB Coordinator must approve the exact scope and obtain genuine EF-generated artifacts before any migration or database update.
