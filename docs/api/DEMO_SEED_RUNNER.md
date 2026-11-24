# Explicit demo seed runner

Build and execute the standalone tool only when a human authorizes seeding the named demo database. The runner does not create databases or apply migrations. It refuses the Official and production catalogs and permits only `(localdb)\MSSQLLocalDB` with a catalog prefixed `Wayhunter_Demo_`. It checks that the connection catalog exactly matches the separately supplied catalog authorization, the database is ONLINE/read-write, and all required tables exist before its first write.

PowerShell invocation:

```powershell
$env:WAYHUNTER_DEMO_SEED_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Demo_20261130;Integrated Security=true;TrustServerCertificate=true'
$env:WAYHUNTER_DEMO_SEED_DATABASE = 'Wayhunter_Demo_20261130'
$env:WAYHUNTER_DEMO_SEED_CONFIRM = 'SEED_DEMO_DATABASE:Wayhunter_Demo_20261130'
$env:WAYHUNTER_DEMO_ACCOUNT_PASSWORD = '<operator supplied password, minimum 8 characters>'
$env:WAYHUNTER_DEMO_VOUCHER_CODE = '<operator supplied academic sample code>'
$env:WAYHUNTER_DEMO_VOUCHER_IMAGE_REFERENCE = '<private opaque image reference; no URL>'
$env:WAYHUNTER_DEMO_VOUCHER_PROTECTION_KEY = '<Base64 key containing 32 random bytes>'
dotnet run --project tools/Wayhunter.DemoSeedRunner/Wayhunter.DemoSeedRunner.csproj -c Release -- --seed
```

Supply values from protected environment/secret storage; never put them in source control or command history. The confirmation string must exactly match the catalog. `--help` is read-only. Do not use the command against the authorized M5 test database or Official.

For a new final demo catalog, the future migration procedure is separate from seeding. Confirm the exact catalog, take/verify a backup when it already exists, review the migration list, and obtain explicit authorization before running `database update` (which may create the named database if absent):

```powershell
$env:ConnectionStrings__WayhunterSharedDb = 'Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Demo_20261130;Integrated Security=true;TrustServerCertificate=true'
dotnet ef migrations list --project src/Wayhunter.Infrastructure --startup-project src/Wayhunter.Api --configuration Release
dotnet ef database update --project src/Wayhunter.Infrastructure --startup-project src/Wayhunter.Api --configuration Release
```

Those commands are documented only and have not been executed. The current model has no pending migration changes. Migration history in this worktree is the eight previously recorded migrations, including `20261123175746_M5GameDraftCreateIdempotency`.

The runner uses stable account, trip, stop, session, check-in, hazard, memory, Hunter, Game, wallet reward, voucher offer/inventory and advertisement IDs. It uses ASP.NET Identity password hashing, the trusted Admin role factory, current Game domain validation, M6 reward grant store, voucher admin store/protector, Trip Memory store and Safe Check-in store. Existing seed records are preserved; mismatched IDs/passwords or inconsistent ownership cause fail-closed errors. It never produces OTP delivery, media verification, provider receipts or notification-delivery success.

Wallet grants are limited to the approved +100 verified-account welcome reward for each demo account. The runner does not mint coins to meet the 10,100 voucher redemption threshold; earning/approval workflows must produce those balances.

The seed timestamp is fixed for deterministic seed data. The five trip plans are dated from 2026-11-25. For later academic runs, review/update the seed dataset and tests as a separate source change rather than silently mutating existing rows.
