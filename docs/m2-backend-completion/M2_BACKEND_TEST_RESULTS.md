# M2 backend test results

Baseline is origin/develop d13a350 with the restored MS04 patch, before completion edits.

| Command | Baseline | Final |
| --- | --- | --- |
| dotnet build Wayhunter.slnx | Passed, 0 warnings/errors | Passed, 0 warnings/errors |
| dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --filter FullyQualifiedName~TravelPlanning | 446 passed | 456 passed |
| dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --filter FullyQualifiedName~TravelPlanning | 22 passed | 31 passed |
| dotnet test Wayhunter.slnx --no-restore | 3,040 unit and 129 integration passed, 70 skipped | 3,050 unit and 138 integration passed, 70 skipped |

Zero failures in all listed suites. Baseline focused commands reused the baseline build via --no-build. Final focused tests built normally.

New coverage checks place request method/path/credential placement, provider coordinates, wrong-ID rejection, malformed payloads, upstream quota responses and caller cancellation. HTTP coverage checks identity challenges, invalid picker/weather input and disabled weather/Routes behavior. Draft tests check ordered owner-only creation and that invalid or duplicate initial stops produce no saved draft.

The 70 full-suite skips include SQL-dependent integration cases. SQL execution and live provider credential verification remain unavailable for this task. HTTP provider tests use simulated responses.

## Continuation results

| Command | Result |
| --- | --- |
| dotnet build Wayhunter.slnx | Passed, 0 warnings and 0 errors |
| dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --filter FullyQualifiedName~TravelPlanning | 464 passed, 0 failed, 0 skipped |
| dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-build --filter FullyQualifiedName~TravelPlanning | 37 passed, 0 failed, 0 skipped |
| dotnet test Wayhunter.slnx --no-restore | 3,058 unit and 144 integration passed, 0 failed, 70 skipped |

Suggested detail HTTP tests cover the authorized projection, revoked and unknown records, non-cacheable responses, and malformed trusted identity. Copy retry tests cover repeated 404 responses with no drafts, plus a registration test that proves the legacy flag cannot enable writes. SQL rollback, ambiguous commit and concurrent duplicate retries remain unverified pending approval and an authorized database.

Forecast tests cover the upstream contract, invalid JSON and fields, expired points, fresh cache hits, bounded stale fallback and eviction. HTTP tests exercise forecast identity and response shape, and the shared Places picker budget. Quota concurrency, capacity and UTC rollover are tested without provider calls. The full suite retains the same 70 skips as the prior run.
