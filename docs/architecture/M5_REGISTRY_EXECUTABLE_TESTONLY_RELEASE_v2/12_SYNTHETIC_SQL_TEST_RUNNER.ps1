# M5 registry synthetic SQL runner; DEFAULT MODE IS LIST ONLY (no SQL calls).
# NEVER use -Execute without written DB Coordinator Phase E_SYNTHETIC authorization.
[CmdletBinding()]
param(
    [ValidateSet('S01','S02','S03','S04','S05','S06','S07','S08','S09','S10','S11','S12','S13','S14','S15')]
    [string[]]$TestFilter = @(),
    [switch]$Execute,
    [string]$CoordinatorGrantReference,
    [string]$ApprovedReleaseCommitSha,
    [string]$ApprovedBaselineSha256
)
$ErrorActionPreference='Stop'
$catalog = [ordered]@{
 S01='16-column registry and five CHECK-object inventory (read only)'
 S02='TX-A one operation + one reserved claim'
 S03='TX-A injected fault between operation and claim; atomic rollback'
 S04='TX-B fake receipt with changed canonical media rejected'
 S05='TX-B injected fault between receipt and registry; atomic rollback'
 S06='TX-C stale/missing finalizer token rejected'
 S07='Synthetic TX-A/B/C successful lifecycle with claim-finalizer token'
 S08='Multi-row registry illegal self-transition rejected'
 S09='Unknown synthetic receipt (not in fixture ledger) rejected'
 S10='TX-C injected fault after evidence insert; atomic rollback'
 S11='Expired finalizer lease rejected (5-second lease, 6-second wait)'
 S12='Active finalizer lease cannot be stolen by second token'
 S13='Multi-row operation immutable trigger rejection'
 S14='Multi-row registry tombstone release rejection'
 S15='Multi-row evidence update and delete rejection'
}
if (-not $Execute) {
  Write-Output 'LIST ONLY - No SQL will be executed. Phase E remains HOLD.'
  $catalog.GetEnumerator() | ForEach-Object { '{0}: {1}' -f $_.Key,$_.Value }
  return
}
if ($TestFilter.Count -eq 0) { throw 'Explicit -TestFilter is mandatory.' }
if ([string]::IsNullOrWhiteSpace($CoordinatorGrantReference) -or
    [string]::IsNullOrWhiteSpace($ApprovedReleaseCommitSha) -or
    [string]::IsNullOrWhiteSpace($ApprovedBaselineSha256)) {
    throw 'Written Phase E grant reference, commit SHA and coordinator-approved baseline SHA256 required.'
}
if ($CoordinatorGrantReference.Length -lt 8 -or $ApprovedReleaseCommitSha -notmatch '^[0-9a-f]{40}$' -or
    $ApprovedBaselineSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Invalid approval reference / SHA.' }
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'Not inside a Git repository.' }
$head=(git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne $ApprovedReleaseCommitSha) {
  throw 'Current release commit differs from coordinator-approved SHA.'
}
if ((git status --porcelain).Count -gt 0) { throw 'Working tree must be clean.' }
$baseline=Join-Path $repoRoot 'src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql'
if (-not (Test-Path -LiteralPath $baseline)) { throw 'Baseline file absent.' }
if ((Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash -ine $ApprovedBaselineSha256) {
  throw 'InitialSharedSchema SHA256 mismatch. No SQL execution.'
}
if ((whoami).Trim() -ine 'UNICOMTIC\ut011148') { throw 'Wrong original Windows identity.' }
$dir=Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath=Join-Path $dir 'SHA256_MANIFEST.json'
if (-not (Test-Path $manifestPath)) { throw 'Release SHA256 manifest missing.' }
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
# Verify the dispatcher itself against immutable packaged file hash.
$dispatcher=Join-Path $dir '13_SYNTHETIC_CASES_E_SCOPE_DO_NOT_RUN.sql'
if (-not (Test-Path $dispatcher)) { throw 'Dispatcher missing.' }
if ((Get-FileHash -LiteralPath $dispatcher -Algorithm SHA256).Hash -ine $manifest.'13_SYNTHETIC_CASES_E_SCOPE_DO_NOT_RUN.sql') {
  throw 'Dispatcher changed since review; STOP.'
}
$sqlcmd=Get-Command sqlcmd -ErrorAction Stop
Write-Warning 'Phase E synthetic fixture writes can persist in the isolated database. F disposal is NOT granted by E.'
foreach ($id in $TestFilter) {
  Write-Output ('START {0} | Grant {1} | Commit {2}' -f $id,$CoordinatorGrantReference,$head)
  & $sqlcmd.Source -S '(localdb)\MSSQLLocalDB' -E -b -X -l 10 -d 'Wayhunter_M5_Registry_IsolatedTest' `
    -v 'M5AuthorizedPhase=E_SYNTHETIC' ("M5TestFilter=$id") -i $dispatcher
  if ($LASTEXITCODE -ne 0) { throw ('FAIL {0}; stop and preserve sanitized evidence' -f $id) }
  Write-Output ('FINISH {0}: reported PASS by sqlcmd; preserve raw output privately' -f $id)
}
