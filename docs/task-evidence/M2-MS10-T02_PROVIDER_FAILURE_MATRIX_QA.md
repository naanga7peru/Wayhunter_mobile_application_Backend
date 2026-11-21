# M2-MS10-T02 — Provider Failure Matrix QA

## Acceptance and scope

**Official task:** M2-MS10-T02, Maps / AI / Weather Provider Failure Matrix QA.

**Acceptance supplied for this audit:** Provider failures must not destroy an existing draft or live-trip session; provider failures must not produce fabricated output.

The official Second Brain task brief was not present in the local checkout during this audit. Its source verification is **NOT EXECUTED**. This note records the supplied acceptance and current backend code/test evidence only. It does not claim Flutter, device, live-provider, SQL, or M3 end-to-end acceptance.

## Provider failure matrix

| Area | Existing offline evidence | Result and boundary |
|---|---|---|
| AI proposal | `AiTripPlansControllerTests.ProviderFailure_ReturnsSanitizedError`, `MalformedAiOutput_Returns502`, and `ReviewedProposal_UsesNormalDraftCreationWithEditedBudget`; `GenerateAiTripPlanServiceTests.MalformedAiJson_IsRejected` | **TEST_VERIFIED**: timeout/provider failures return code-only errors; malformed output is rejected; generation itself creates no draft, and the normal draft path is exercised separately after review. The controller/service do not receive a trip repository or live-session dependency. |
| Route preview | `RoutePreviewControllerTests.ProviderFailure_Returns503`, `InvalidProviderResponse_Returns502`; `PreviewTripRouteServiceTests.ProviderFailure_DoesNotInventRoute`, `InvalidProviderGeometry_IsRejected`, `MissingEta_IsRejected`, `NonFiniteDistance_IsRejected` | **TEST_VERIFIED**: provider failure returns 503 with code-only body; submitted ordered stops remain unchanged; failed/malformed results do not return fabricated geometry, distance, or ETA. Preview service has no persistence/session dependency. |
| Nearby places | `NearbyPlacesControllerTests.ProviderFailure_Returns503`, `InvalidProviderResult_Returns502`; `NearbyPlaceSearchServiceTests.ProviderFailure_ShouldReturnFailure`, `FailedProviderWithFakePlaces_ShouldThrow`, `InvalidProviderCoordinates_ShouldBeExcluded`; `NearbyPlacesHttpTests.ProviderFailure_Returns503` | **TEST_VERIFIED**: unavailable/malformed results fail safely; a failed provider cannot supply fake places as a successful result. Search service has no trip persistence/session dependency. |
| Current weather | `WeatherCacheServiceTests.ProviderFailure_WithoutCache_ShouldBeUnavailable`, `ProviderFailure_WithOldCache_ShouldReturnStale`, `ProviderFailure_ObservationExpiresAfter24Hours`, `InvalidProviderResponse_ShouldThrow` | **TEST_VERIFIED**: no cache yields unavailable; bounded stale fallback retains original observation/cache timestamps; data older than the existing 24-hour limit is unavailable; malformed successful provider data is rejected. |
| Forecast | `WeatherForecastAndQuotaTests.ForecastCache_FiltersExpiredPointsAndUsesBoundedStaleFallback`, `OpenWeatherForecast_ValidatesUpstreamShape` | **TEST_VERIFIED**: expired points are filtered, stale points are bounded by current policy, and invalid upstream structures do not become forecast data. Null provider output with no eligible cache yields no forecast result. |
| Cross-module state | The AI, route, nearby, and weather services/controllers above have no injected M2 trip repository or M3 live-session store. | **CODE_VERIFIED** for the inspected dependency boundaries. No cross-module test can claim physical draft/session preservation from these unit tests. M3 session preservation remains **NOT EXECUTED — CROSS-MODULE E2E PENDING**. |

## State preservation and non-fabrication findings

- AI generation is a reviewable proposal path. Persistence occurs only through the separate normal draft creation service in the existing test; provider failure does not invoke that path.
- Route preview returns provider output only after validation. Failure responses are code-only; the existing controller test also checks that the caller's ordered stop list is unchanged.
- Nearby search returns provider suggestions only after validation and labels them `hunterVerified = false`; provider failure returns unavailable rather than synthetic places.
- Weather returns validated provider observations or cache entries within existing freshness rules. The failure path does not synthesize observation or forecast values.
- None of the inspected M2 provider services has a dependency capable of mutating an existing trip draft or M3 live session. This is a source-level boundary observation, not a database transaction or M3 integration test.

## Verification status

The existing tests listed above were inspected and run on the task branch:

- `dotnet build Wayhunter.slnx --no-restore`: **PASS**, 0 errors, 2 existing xUnit2031 warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs` (lines 152 and 154).
- `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --filter "FullyQualifiedName~TravelPlanning"`: **PASS**, 477 passed, 0 failed, 0 skipped.
- `dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~NearbyPlacesHttpTests"`: **PASS**, 30 passed, 0 failed, 0 skipped. The loopback test host disables SQL persistence/routes and injects deterministic providers for the tested integrations.

- Real Google, Gemini, and OpenWeather calls: **NOT EXECUTED**.
- Physical SQL transaction/state checks: **NOT EXECUTED**.
- M3 live-session persistence/E2E preservation: **NOT EXECUTED — CROSS-MODULE E2E PENDING**.
- Flutter/device recovery and user-visible retry behavior: **NOT EXECUTED**.
- Existing service response contracts and offline failure test coverage: **CODE_VERIFIED / TEST_VERIFIED** by the named source and test files and the commands above.
- New regression tests: **None added**; the audited acceptance-level offline failure cases were already covered, so duplicating them would add no distinct evidence.

## Commands for reproducible offline verification

```text
dotnet build Wayhunter.slnx --no-restore
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --filter "FullyQualifiedName~TravelPlanning"
```

Focused integration tests may use only the repository's loopback test host and deterministic test providers. No physical SQL or live-provider test is part of this task.

## Remaining boundaries

- **NOT EXECUTED:** device-level provider outage/retry UX.
- **NOT EXECUTED:** live provider outage/timeout behavior against provider services.
- **NOT EXECUTED:** persisted draft preservation under actual SQL faults.
- **NOT EXECUTED:** M3 live-session preservation across module boundaries.
- **DECISION_REQUIRED:** any broader M2/M3 cross-module acceptance that requires a shared failure contract or authorized isolated SQL fixture.
