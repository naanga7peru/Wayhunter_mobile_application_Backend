$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\Admin\Desktop\WAYHUNTER\WAYHUNTER_M5_PRODUCTION'
$baseUrl = 'http://127.0.0.1:5055'

if (Get-NetTCPConnection -LocalPort 5055 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 5055 is already in use. This launcher will not stop an existing process.'
}

function Reveal-SecureString([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

$keyPath = Join-Path $env:LOCALAPPDATA 'Wayhunter\demo-voucher-protection-key.dpapi'
if (-not (Test-Path -LiteralPath $keyPath)) { throw 'Existing DPAPI voucher key is missing.' }
$voucherKey = Reveal-SecureString (ConvertTo-SecureString (Get-Content -LiteralPath $keyPath -Raw))
$keyBytes = [Convert]::FromBase64String($voucherKey)
if ($keyBytes.Length -ne 32) { throw 'Existing DPAPI voucher key is invalid.' }
[Array]::Clear($keyBytes, 0, $keyBytes.Length)

$signingBytes = New-Object byte[] 64
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $rng.GetBytes($signingBytes)
    $jwtSecret = [Convert]::ToBase64String($signingBytes)
}
finally {
    $rng.Dispose()
    [Array]::Clear($signingBytes, 0, $signingBytes.Length)
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $baseUrl
$env:ConnectionStrings__WayhunterSharedDb = 'Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Demo_Final;Integrated Security=true;TrustServerCertificate=true'
$env:Identity__EnableSharedAuthentication = 'true'
$env:Identity__SessionLifetimeMinutes = '60'
$env:Authentication__Jwt__Issuer = 'wayhunter-demo-local'
$env:Authentication__Jwt__Audience = 'wayhunter-flutter-demo'
$env:Authentication__Jwt__SigningSecret = $jwtSecret
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

Write-Host 'API config present: Wayhunter_Demo_Final, shared authentication, 60-minute sessions, JWT key, and DPAPI voucher key.'
Write-Host 'Starting only the Wayhunter API at http://127.0.0.1:5055. Keep this window open.'
Set-Location $repo
try {
    & dotnet run --no-build --no-restore --no-launch-profile --project src/Wayhunter.Api/Wayhunter.Api.csproj -c Release -- --urls $baseUrl
}
finally {
    @('ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','ConnectionStrings__WayhunterSharedDb','Identity__EnableSharedAuthentication','Identity__SessionLifetimeMinutes','Authentication__Jwt__Issuer','Authentication__Jwt__Audience','Authentication__Jwt__SigningSecret','CoinsWallet__VoucherProtectionKey','CoinsWallet__EnableSqlVoucherRuntime','Advertisements__EnableSqlPersistence','Games__EnableSqlPersistence','TripSafety__EnableSqlPersistence','TripSafety__EnableSqlTripMemories','TripSafety__EnableNotificationOutboxWorker','TripSafety__EnableHazardExpiryWorker','TravelPlanning__EnableSqlPersistence','Advertisements__ImageUpload__Enabled','Advertisements__ImageUpload__LifecycleEnabled','Advertisements__Cloudinary__Enabled') |
        ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }
    $voucherKey = $null
    $jwtSecret = $null
}
