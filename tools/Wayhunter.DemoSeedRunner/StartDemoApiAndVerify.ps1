$ErrorActionPreference = 'Stop'

function Reveal-SecureString([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

$repo = 'C:\Users\Admin\Desktop\WAYHUNTER\WAYHUNTER_M5_PRODUCTION'
$catalog = 'Wayhunter_Demo_Final'
$baseUrl = 'http://127.0.0.1:5055'
$apiWindow = $null
$accessToken = $null
$accountPassword = $null
$signingSecret = $null
$voucherKey = $null
$rngBytes = $null

try {
    $keyPath = Join-Path $env:LOCALAPPDATA 'Wayhunter\demo-voucher-protection-key.dpapi'
    if (-not (Test-Path -LiteralPath $keyPath)) { throw 'Existing DPAPI voucher key is missing.' }
    $voucherKey = Reveal-SecureString (ConvertTo-SecureString (Get-Content -LiteralPath $keyPath -Raw))
    if ([Convert]::FromBase64String($voucherKey).Length -ne 32) { throw 'Existing DPAPI voucher key is invalid.' }

    $preflight = "SET NOCOUNT ON; IF DB_NAME()<>N'Wayhunter_Demo_Final' THROW 51000,'Wrong catalog.',1; IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name=N'Wayhunter_Demo_Final' AND state_desc=N'ONLINE' AND is_read_only=0) THROW 51001,'Target not writable.',1; IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory)<>8 OR (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory)<>N'20261123175746_M5GameDraftCreateIdempotency' THROW 51002,'Unexpected migration history.',1; SELECT N'TARGET_PREFLIGHT_OK';"
    $check = & sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d $catalog -b -h -1 -W -Q $preflight
    if ($LASTEXITCODE -ne 0 -or ($check -join '') -notmatch 'TARGET_PREFLIGHT_OK') { throw 'Database preflight failed.' }

    $rngBytes = New-Object byte[] 64
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($rngBytes)
    $signingSecret = [Convert]::ToBase64String($rngBytes)
    [Array]::Clear($rngBytes, 0, $rngBytes.Length)

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://127.0.0.1:5055'
    $env:ConnectionStrings__WayhunterSharedDb = 'Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Demo_Final;Integrated Security=true;TrustServerCertificate=true'
    $env:Identity__EnableSharedAuthentication = 'true'
    $env:Identity__SessionLifetimeMinutes = '60'
    $env:Authentication__Jwt__Issuer = 'wayhunter-demo-local'
    $env:Authentication__Jwt__Audience = 'wayhunter-flutter-demo'
    $env:Authentication__Jwt__SigningSecret = $signingSecret
    $env:CoinsWallet__VoucherProtectionKey = $voucherKey
    $env:CoinsWallet__EnableSqlVoucherRuntime = 'true'
    $env:Advertisements__EnableSqlPersistence = 'true'
    $env:Games__EnableSqlPersistence = 'true'
    $env:TripSafety__EnableSqlPersistence = 'true'
    $env:TripSafety__EnableSqlTripMemories = 'true'
    $env:TripSafety__EnableNotificationOutboxWorker = 'false'
    $env:TripSafety__EnableHazardExpiryWorker = 'false'
    $env:TravelPlanning__EnableSqlPersistence = 'true'
    $env:Advertisements__ImageUpload__Enabled = 'false'
    $env:Advertisements__ImageUpload__LifecycleEnabled = 'false'
    $env:Advertisements__Cloudinary__Enabled = 'false'

    Set-Location $repo
    $apiWindow = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'RemoteSigned', '-NoExit', '-Command',
        'dotnet run --no-build --no-restore --no-launch-profile --project src/Wayhunter.Api/Wayhunter.Api.csproj -c Release -- --urls http://127.0.0.1:5055'
    ) -WorkingDirectory $repo -PassThru

    $health = $null
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 2
            break
        } catch { Start-Sleep -Seconds 1 }
    }
    if ($null -eq $health) { throw 'API did not become healthy within 60 seconds; check the API PowerShell window.' }
    Write-Host "HEALTH_OK service=$($health.service) status=$($health.status)"

    $swagger = Invoke-RestMethod -Uri "$baseUrl/swagger/v1/swagger.json" -TimeoutSec 10
    $operationCount = 0
    foreach ($path in $swagger.paths.PSObject.Properties) {
        foreach ($method in $path.Value.PSObject.Properties) {
            if ($method.Name -in @('get','post','put','patch','delete','head','options','trace')) { $operationCount++ }
        }
    }
    Write-Host "SWAGGER_OK operations=$operationCount"

    $secure = Read-Host 'Demo traveller password for authenticated login verification (input hidden)' -AsSecureString
    $accountPassword = Reveal-SecureString $secure
    $body = @{ email = 'traveller1@demo.wayhunter.test'; password = $accountPassword } | ConvertTo-Json -Compress
    $login = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/mobile/identity/login" -ContentType 'application/json' -Body $body -TimeoutSec 15
    $accessToken = $login.accessToken
    if ([string]::IsNullOrWhiteSpace($accessToken)) { throw 'Login response did not contain an access token.' }
    Write-Host "LOGIN_OK userId=$($login.userId)"

    $headers = @{ Authorization = "Bearer $accessToken" }
    $homeResponse = Invoke-RestMethod -Uri "$baseUrl/api/mobile/home" -Headers $headers -TimeoutSec 15
    Write-Host "HOME_OK trips=$(@($homeResponse.upcomingTrips).Count) games=$(@($homeResponse.availableGames).Count) hunterPlaces=$(@($homeResponse.approvedHunterPlaces).Count) banners=$(@($homeResponse.homeBanners).Count) sponsoredPois=$(@($homeResponse.sponsoredPois).Count)"
    Write-Host "API_BASE_URL=http://<reachable-host>:5055"
    Write-Host 'API remains open in its separate PowerShell window. Close that window to stop it.'
}
catch {
    Write-Host ('API verification stopped: ' + $_.Exception.GetType().Name + '. Secret values omitted.')
    if ($null -ne $apiWindow -and (Get-Process -Id $apiWindow.Id -ErrorAction SilentlyContinue)) {
        Write-Host 'Check the separate API PowerShell window for startup diagnostics; close it if startup failed.'
    }
}
finally {
    if ($null -ne $accessToken) { $accessToken = $null }
    if ($null -ne $accountPassword) { $accountPassword = $null }
    @('ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','ConnectionStrings__WayhunterSharedDb','Identity__EnableSharedAuthentication','Identity__SessionLifetimeMinutes','Authentication__Jwt__Issuer','Authentication__Jwt__Audience','Authentication__Jwt__SigningSecret','CoinsWallet__VoucherProtectionKey','CoinsWallet__EnableSqlVoucherRuntime','Advertisements__EnableSqlPersistence','Games__EnableSqlPersistence','TripSafety__EnableSqlPersistence','TripSafety__EnableSqlTripMemories','TripSafety__EnableNotificationOutboxWorker','TripSafety__EnableHazardExpiryWorker','TravelPlanning__EnableSqlPersistence','Advertisements__ImageUpload__Enabled','Advertisements__ImageUpload__LifecycleEnabled','Advertisements__Cloudinary__Enabled') |
        ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }
}
