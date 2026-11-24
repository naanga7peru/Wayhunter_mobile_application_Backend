$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:5055'

function Reveal-SecureString([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Send-JsonRequest([string]$Method, [string]$Path, [string]$JsonBody, [string]$BearerToken) {
    $request = [Net.HttpWebRequest]::Create("$baseUrl$Path")
    $request.Method = $Method
    $request.Accept = 'application/json'
    $request.Timeout = 15000
    if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
        $request.Headers['Authorization'] = "Bearer $BearerToken"
    }
    # Windows PowerShell 5.1 coerces a null argument to an empty string for a
    # [string] parameter. Gate on verbs that accept JSON and require non-empty
    # content so GET/HEAD never open a request stream or send a body.
    $bodyVerb = $Method.ToUpperInvariant() -in @('POST', 'PUT', 'PATCH')
    if ($bodyVerb -and -not [string]::IsNullOrWhiteSpace($JsonBody)) {
        $request.ContentType = 'application/json'
        $payload = [Text.Encoding]::UTF8.GetBytes($JsonBody)
        $request.ContentLength = $payload.Length
        $stream = $request.GetRequestStream()
        try { $stream.Write($payload, 0, $payload.Length) }
        finally { $stream.Dispose(); [Array]::Clear($payload, 0, $payload.Length) }
    }

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $response = $null
    try { $response = $request.GetResponse() }
    catch {
        # Windows PowerShell 5.1 can wrap HttpWebRequest.GetResponse() errors
        # in MethodInvocationException. Unwrap only to recognize HTTP errors;
        # never print the request, bearer token, or response body.
        $requestError = $_.Exception
        while ($null -ne $requestError.InnerException -and
            $requestError -isnot [Net.WebException]) {
            $requestError = $requestError.InnerException
        }
        if ($requestError -is [Net.WebException]) {
            $response = $requestError.Response
        }
        else {
            throw
        }
    }
    $timer.Stop()
    if ($null -eq $response) {
        return [pscustomobject]@{ Status = 0; ElapsedMs = $timer.ElapsedMilliseconds; ContentType = ''; Body = '' }
    }
    try {
        $reader = New-Object IO.StreamReader($response.GetResponseStream())
        try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{
            Status = [int]$response.StatusCode
            ElapsedMs = $timer.ElapsedMilliseconds
            ContentType = $response.ContentType
            Body = $body
        }
    }
    finally { $response.Close() }
}

try {
    $securePassword = Read-Host 'Demo traveller password (input hidden)' -AsSecureString
    $password = Reveal-SecureString $securePassword
    $loginJson = @{ email = 'traveller1@demo.wayhunter.test'; password = $password } | ConvertTo-Json -Compress
    $login = Send-JsonRequest 'POST' '/api/mobile/identity/login' $loginJson ''
    $loginObject = $null
    if ($login.Body) { try { $loginObject = $login.Body | ConvertFrom-Json } catch { } }
    $errorCode = ''
    if ($login.Status -ne 200 -and $null -ne $loginObject) {
        if ($loginObject.errorCode) { $errorCode = [string]$loginObject.errorCode }
        elseif ($loginObject.code) { $errorCode = [string]$loginObject.code }
    }
    Write-Host "LOGIN method=POST path=/api/mobile/identity/login status=$($login.Status) elapsed_ms=$($login.ElapsedMs) content_type=$($login.ContentType) error_code=$errorCode"

    $token = $null
    if ($login.Status -eq 200 -and $null -ne $loginObject) { $token = $loginObject.accessToken }
    if ([string]::IsNullOrWhiteSpace($token)) {
        Write-Host 'LOGIN_RESULT=FAILED token omitted; response body omitted.'
        return
    }
    Write-Host "LOGIN_RESULT=OK account_id=$($loginObject.userId) token=omitted"

    $homeResponse = Send-JsonRequest 'GET' '/api/mobile/home' $null $token
    $homeObject = $null
    if ($homeResponse.Body) { try { $homeObject = $homeResponse.Body | ConvertFrom-Json } catch { } }
    $homeCode = ''
    if ($homeResponse.Status -ne 200 -and $null -ne $homeObject) {
        if ($homeObject.errorCode) { $homeCode = [string]$homeObject.errorCode }
        elseif ($homeObject.code) { $homeCode = [string]$homeObject.code }
    }
    Write-Host "HOME method=GET path=/api/mobile/home status=$($homeResponse.Status) elapsed_ms=$($homeResponse.ElapsedMs) content_type=$($homeResponse.ContentType) error_code=$homeCode"
    if ($homeResponse.Status -eq 200 -and $null -ne $homeObject) {
        $wallet = $homeObject.wallet
        if ($null -eq $wallet) {
            $walletSummary = 'wallet=unavailable'
        }
        else {
            $walletSummary = "wallet_posted=$($wallet.postedCoins) wallet_reserved=$($wallet.reservedCoins) wallet_spendable=$($wallet.spendableCoins) wallet_lifetime=$($wallet.lifetimeEarnedCoins)"
        }
        Write-Host "HOME_RESULT=OK profile_present=$($null -ne $homeObject.profile) profile_display_name_present=$(-not [string]::IsNullOrWhiteSpace($homeObject.profile.displayName)) $walletSummary upcoming_trips=$(@($homeObject.upcomingTrips).Count) games=$(@($homeObject.availableGames).Count) hunter_places=$(@($homeObject.approvedHunterPlaces).Count) banners=$(@($homeObject.homeBanners).Count) sponsored_pois=$(@($homeObject.sponsoredPois).Count)"
    }
    else { Write-Host 'HOME_RESULT=FAILED response body omitted.' }

    $anonymous = Send-JsonRequest 'GET' '/api/mobile/home' $null ''
    Write-Host "ANONYMOUS_HOME method=GET path=/api/mobile/home status=$($anonymous.Status)"
}
catch {
    $failure = $_.Exception
    $root = $failure.GetBaseException()
    $line = $_.InvocationInfo.ScriptLineNumber
    $message = [string]$root.Message
    $message = $message -replace '(?i)Bearer\s+[^\s"'']+', 'Bearer [redacted]'
    $message = $message -replace '(?i)(password|token|secret|authorization)\s*[:=]\s*[^\s,;]+', '$1=[redacted]'
    Write-Host "HTTP_VERIFICATION_EXCEPTION line=$line outer=$($failure.GetType().Name) inner=$($root.GetType().Name) message=$message"
}
finally {
    $password = $null
    $loginJson = $null
    $token = $null
}

Read-Host 'Verification complete; return to Codex and press Enter to close this window' | Out-Null
