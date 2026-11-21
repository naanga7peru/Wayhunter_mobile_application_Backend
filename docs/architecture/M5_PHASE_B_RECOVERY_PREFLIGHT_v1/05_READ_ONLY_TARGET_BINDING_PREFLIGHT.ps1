# M5 REVISED PHASE B -- READ-ONLY preflight ONLY. No SQL mutation. No execution permission.
# Dot-source from repository root AFTER committing the reviewed files:
# . "docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1\05_READ_ONLY_TARGET_BINDING_PREFLIGHT.ps1"
# On any mismatch: THROW/STOP. This script NEVER calls the baseline wrapper.
$verifiedPipe = $null
$verifiedServerName = $null
$verifiedInstanceName = $null
$ErrorActionPreference = 'Stop'
function Stop-Gate([string]$message) { throw "M5 PREFLIGHT STOP: $message" }
function Check([bool]$ok,[string]$message) { if (-not $ok) { Stop-Gate $message } }
$repo = (git rev-parse --show-toplevel).Trim()
Check ($LASTEXITCODE -eq 0 -and [string]::Equals((Get-Location).Path,(Resolve-Path $repo).Path,[StringComparison]::OrdinalIgnoreCase)) 'Must run from repository root'
$branch = (git branch --show-current).Trim()
Check ($branch -ceq 'task/m5-idempotent-proof-recovery') 'Wrong branch'
$dirty = @(git status --porcelain)
Check ($LASTEXITCODE -eq 0 -and $dirty.Count -eq 0) 'Working tree not clean; commit/review before preflight'
$commit = (git rev-parse HEAD).Trim()
Check ($LASTEXITCODE -eq 0) 'Cannot determine commit'
$operator = (& whoami).Trim()
Check ($LASTEXITCODE -eq 0 -and $operator -ieq 'UNICOMTIC\ut011148') 'Wrong Windows operator'
$pack = 'docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1'
$baseline = 'src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql'
$wrapper = "$pack\01B_BASELINE_RECOVERY_CANDIDATE.sql"
$manifest = Get-Content "$pack\SHA256_MANIFEST.json" -Raw | ConvertFrom-Json
foreach ($f in $manifest.PSObject.Properties.Name) {
  $file = Join-Path $pack $f
  Check (Test-Path -LiteralPath $file) "Missing manifest payload: $f"
  $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
  Check ($actual -ceq $manifest.$f) "SHA-256 mismatch: $f"
}
Check ((Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash -ceq '74243E7CE8C5C79A79D1B455C7EE0C263E1894CD570A4EF3FC45FEDE3DAE6724') 'Immutable baseline SHA-256 mismatch'
$blob = (git hash-object -- $baseline).Trim()
Check ($LASTEXITCODE -eq 0 -and $blob -ceq '90a193505e8efe0e680ec2c94cae4d2186b3d87e') 'Immutable baseline Git blob differs'
$directives = @(Select-String -LiteralPath $baseline -Pattern '^\s*(USE\b|:CONNECT\b|:r\b|SET\s+QUOTED_IDENTIFIER\s+OFF\b)')
Check ($directives.Count -eq 0) 'Baseline contains unapproved context-switch or SET directives'
# sqlLocalDB INFO is read-only; DO NOT run sqllocaldb start/stop/delete.
function Get-LocalDbInfo {
  $lines = @(& sqllocaldb info MSSQLLocalDB 2>&1)
  Check ($LASTEXITCODE -eq 0) 'sqllocaldb info MSSQLLocalDB failed'
  $s = $lines -join "`n"
  $vals = @{}
  foreach ($name in @('Name','Owner','State','Instance pipe name')) {
    $matches = @($lines | Where-Object { $_ -match "^\s*$([regex]::Escape($name))\s*:" })
    Check ($matches.Count -eq 1) "Missing/ambiguous sqllocaldb info field: $name (localized output? report for review)"
    $vals[$name] = ($matches[0] -split ':',2)[1].Trim()
  }
  Check ($vals['Name'] -ceq 'MSSQLLocalDB') 'Logical LocalDB instance name mismatch'
  Check ($vals['Owner'] -ieq 'UNICOMTIC\ut011148') 'LocalDB owner mismatch'
  Check ($vals['State'] -ceq 'Running') 'LocalDB is not already running; do not auto-start'
  $pipe = $vals['Instance pipe name']
  Check ($pipe -cmatch '^np:\\\\.\\pipe\\LOCALDB#[0-9A-Fa-f]{8}\\tsql\\query$') 'Invalid/ambiguous named pipe shape'
  return $pipe
}
$pipe1 = Get-LocalDbInfo
$instanceFromPipe = 'LOCALDB#' + ([regex]::Match($pipe1,'LOCALDB#([0-9A-Fa-f]{8})')).Groups[1].Value.ToUpperInvariant()
$expectedServer = 'UNICOMTIC48\' + $instanceFromPipe
function Read-Binding([string]$target) {
  $output = @(& sqlcmd -S $target -d 'Wayhunter_M5_Registry_IsolatedTest' -E -I -b -V 11 -W -h -1 -w 2048 -i "$pack\04_READ_ONLY_BINDING_ROW.sql" 2>&1)
  Check ($LASTEXITCODE -eq 0) "Read-only identity query failed for target $target : $($output -join ' ')"
  $lines = @($output | ForEach-Object { "$_".Trim() } | Where-Object { $_ -ne '' })
  Check ($lines.Count -eq 1 -and $lines[0].StartsWith('M5BIND|')) "Unexpected binding response from $target; STOP"
  $fields = $lines[0].Split([char]'|')
  Check ($fields.Length -eq 18) "Wrong binding field count for $target"
  Check ($fields[1] -ceq $expectedServer) 'SERVERPROPERTY ServerName mismatch'
  Check ($fields[2] -ceq $expectedServer) '@@SERVERNAME mismatch'
  Check ($fields[3] -ceq $instanceFromPipe) 'Actual instance does not correspond to LocalDB named pipe'
  Check ($fields[4] -ceq 'UNICOMTIC48') 'Wrong machine'
  Check ($fields[5] -ceq '1') 'Not LocalDB'
  Check ($fields[6] -ieq 'UNICOMTIC\ut011148') 'Wrong SQL operator'
  Check ($fields[7] -ceq 'Wayhunter_M5_Registry_IsolatedTest') 'Wrong database name'
  Check ($fields[8] -ceq '7') 'Wrong database ID'
  Check ($fields[9] -ceq '2026-11-21 14:56:24.330') 'Wrong creation timestamp (datetime2(3) representation)'
  Check ($fields[10] -ceq '1') 'QUOTED_IDENTIFIER is not ON'
  Check ($fields[11] -ceq '0') 'Migration history nonempty'
  Check ($fields[12] -ceq '1') 'Manifest count mismatch'
  Check ($fields[13] -ieq 'F42D6B20-B250-4F93-AB07-0478C2FA8E85') 'Fixture RunId mismatch'
  Check ($fields[14] -ieq 'UNICOMTIC\ut011148') 'Fixture CreatedBy mismatch'
  Check ($fields[15] -ceq 'Wayhunter_M5_Registry_IsolatedTest') 'Fixture DB identity mismatch'
  Check ($fields[16] -ceq '1') 'No privilege to inspect all SQL sessions'
  Check ($fields[17] -ceq '0') 'Competing SQL session detected'
  return $lines[0]
}
$aliasTarget = '(localdb)\MSSQLLocalDB'
$aliasBinding = Read-Binding $aliasTarget
$pipeBinding = Read-Binding $pipe1
Check ($aliasBinding -ceq $pipeBinding) 'Logical alias and physical named pipe resolve to DIFFERENT SQL identities'
$pipe2 = Get-LocalDbInfo
Check ($pipe1 -ceq $pipe2) 'LocalDB runtime changed during read-only checks; STOP'
$auditOutput = @(& sqlcmd -S $pipe1 -d 'Wayhunter_M5_Registry_IsolatedTest' -E -I -b -V 11 -i "$pack\00_READ_ONLY_RECOVERY_AUDIT.sql" 2>&1)
Check ($LASTEXITCODE -eq 0 -and (($auditOutput -join "`n") -like '*PASS: permitted two-table empty-history recovery state; NO CHANGES MADE*')) 'Independent read-only catalog audit failed'
$pipe3 = Get-LocalDbInfo
Check ($pipe1 -ceq $pipe3) 'LocalDB runtime changed after catalog audit; STOP'
$verifiedPipe = $pipe1
$verifiedServerName = $expectedServer
$verifiedInstanceName = $instanceFromPipe
Write-Output "READ-ONLY TARGET BINDING PASS; NO DATABASE CHANGES; NOT EXECUTION AUTHORIZATION"
Write-Output "Reviewed working commit: $commit"
Write-Output "Windows operator: $operator; logical instance: $aliasTarget"
Write-Output "Verified UTC evidence time: $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))"
Write-Output "Alias read-only binding row: $aliasBinding"
Write-Output "Physical-pipe read-only binding row: $pipeBinding"
Write-Output "Verified runtime server: $verifiedServerName"
Write-Output "Verified runtime instance: $verifiedInstanceName"
Write-Output "Verified named pipe: $verifiedPipe"
Write-Output "Database identity, fixture, empty history, catalog audit and zero other target sessions: PASS"
Write-Output 'Human confirmation of exclusive process control is still required before any FUTURE authorized execution.'
Write-Output 'No baseline SQL execution has been attempted by this script.'
