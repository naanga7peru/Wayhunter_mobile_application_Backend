$ErrorActionPreference = 'Stop'

function Reveal-SecureString([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

try {
    $catalog = 'Wayhunter_Demo_Final'
    $repo = 'C:\Users\Admin\Desktop\WAYHUNTER\WAYHUNTER_M5_PRODUCTION'
    $password = Read-Host 'Demo account password (minimum 8 characters; input hidden)' -AsSecureString

    $env:WAYHUNTER_DEMO_SEED_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Demo_Final;Integrated Security=true;TrustServerCertificate=true'
    $env:WAYHUNTER_DEMO_SEED_DATABASE = $catalog
    $env:WAYHUNTER_DEMO_SEED_CONFIRM = 'SEED_DEMO_DATABASE:Wayhunter_Demo_Final'
    $env:WAYHUNTER_DEMO_ACCOUNT_PASSWORD = Reveal-SecureString $password

    $preflight = "SET NOCOUNT ON; IF DB_NAME()<>N'Wayhunter_Demo_Final' THROW 51000,'Wrong catalog.',1; IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name=N'Wayhunter_Demo_Final' AND state_desc=N'ONLINE' AND is_read_only=0) THROW 51001,'Target not writable.',1; IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory)<>8 OR (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory)<>N'20261123175746_M5GameDraftCreateIdempotency' THROW 51002,'Unexpected migration history.',1; SELECT N'TARGET_PREFLIGHT_OK';"
    $preflightResult = & sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d $catalog -b -h -1 -W -Q $preflight
    if ($LASTEXITCODE -ne 0 -or ($preflightResult -join '') -notmatch 'TARGET_PREFLIGHT_OK') { throw 'Database preflight failed; runner not started.' }

    Set-Location $repo
    Write-Host 'Starting seed pass 1 of 2.'
    dotnet run --project tools/Wayhunter.DemoSeedRunner/Wayhunter.DemoSeedRunner.csproj -c Release -- --seed --skip-voucher
    if ($LASTEXITCODE -ne 0) { throw 'Seed pass 1 failed; database was not dropped or recreated.' }
    Write-Host 'Starting seed pass 2 of 2.'
    dotnet run --project tools/Wayhunter.DemoSeedRunner/Wayhunter.DemoSeedRunner.csproj -c Release -- --seed --skip-voucher
    if ($LASTEXITCODE -ne 0) { throw 'Seed pass 2 failed; database was not dropped or recreated.' }
    Write-Host 'Both seed passes completed successfully. Return to Codex for read-only SQL verification.'
}
catch {
    Write-Host ('Seed stopped: ' + $_.Exception.GetType().Name + '. Secret values omitted; no database was dropped or recreated.')
}
finally {
    @('WAYHUNTER_DEMO_SEED_CONNECTION','WAYHUNTER_DEMO_SEED_DATABASE','WAYHUNTER_DEMO_SEED_CONFIRM','WAYHUNTER_DEMO_ACCOUNT_PASSWORD') |
        ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }
}

Read-Host 'Return to Codex after completion, then press Enter to close this window' | Out-Null
