# M2-MS08-T07 Nearby and Weather QA

Acceptance requires 1 km boundary, denied GPS, and stale weather testing with evidence for the no-location and real-data-unavailable states. [Official M2 brief](https://github.com/naanga7peru/Wayhunter_SecondBrain_Docs/blob/main/Wayhunter_Second_Brain_v1_0/planning/member_briefs/M2_FULLSTACK_MILESTONES_TASKS.md#L240)

| Scenario | Existing test | Gap and action |
| --- | --- | --- |
| Nearby request radius and filtering | `Provider_ShouldReceiveCorrectSearchParameters` checks 1,000 m. The previous filter test covered one clearly inside and one clearly outside point. | TEST_VERIFIED. `NearbyPlaces_FiltersAtInclusiveOneKmBoundary` covers 999.9 m, 1,000 m, and 1,000.1 m. |
| Missing location | `MissingGps_Returns400` and `PickerAndWeather_RejectInvalidInput` cover missing latitude. | TEST_VERIFIED. `LocationQueries_MissingCoordinate_ReturnsCodeOnly` covers either coordinate missing from Nearby, missing longitude from current weather, and either coordinate missing from forecast. Nearby never calls its provider without coordinates. |
| Nearby identity, quota and provider errors | `AnonymousUser_Returns401`, `DailyQuota_Rejects21stRequest`, `ProviderFailure_Returns503`, and `InvalidProviderResult_Returns502`. | TEST_VERIFIED with existing coverage. CODE_VERIFIED. External suggestions carry `hunterVerified: false`. |
| Current weather freshness and stale fallback | `FreshCache_ShouldAvoidSecondProviderCall`, `ProviderFailure_WithOldCache_ShouldReturnStale`, and `ProviderFailure_WithoutCache_ShouldBeUnavailable`. | TEST_VERIFIED. Stale fallback keeps original observation and cache timestamps. `ProviderFailure_ObservationExpiresAfter24Hours` checks the inclusive observation cutoff and absence of stale data afterward. |
| Forecast freshness and metadata | `ForecastCache_FiltersExpiredPointsAndUsesBoundedStaleFallback`, `OpenWeatherForecast_ValidatesUpstreamShape`, and `Forecast_RequiresIdentityAndReturnsDatedFuturePoints`. | TEST_VERIFIED with existing coverage. |
| Weather unavailable | `WeatherDisabled_ReturnsUnavailable` previously covered current weather. | TEST_VERIFIED on current and forecast endpoints. Both return a code-only 503 when disabled. |

## Backend and client behavior

CODE_VERIFIED. `GET /api/mobile/trip-plans/nearby` requests 1,000 m and discards results beyond 1,000 m. Missing coordinates produce `400 INVALID_NEARBY_REQUEST`. Provider failure produces `503 NEARBY_PROVIDER_UNAVAILABLE`. An invalid service result can produce `502 INVALID_NEARBY_RESPONSE`.

`GET /api/mobile/trip-plans/weather` and `GET /api/mobile/trip-plans/weather/forecast` return `400 INVALID_COORDINATES` for missing coordinates and `503 WEATHER_UNAVAILABLE` when usable data is unavailable. The 503 contains no observation, forecast or fabricated values. Current weather fallback reports `stale`, retains the original observation and cache timestamps, and stops serving an observation once it is older than 24 hours. Forecast retains retrieval time, location, and provider metadata and omits expired points.

The API receives coordinates. Flutter owns explaining why location is unavailable, requesting permission, and displaying unavailable or stale weather. Device permission dialogs and stale labels are NOT_EXECUTED. Live Google Places and OpenWeather verification is NOT_EXECUTED. DECISION_REQUIRED before a live run. Confirm restricted credentials and test scope.

## Verification

| Command or filter | Result |
| --- | --- |
| `dotnet build Wayhunter.slnx --no-restore` | Passed, 0 errors and 2 existing xUnit2031 warnings |
| Unit filter `FullyQualifiedName~Nearby` OR `FullyQualifiedName~Weather` | 125 passed, 0 failed, 0 skipped |
| Unit filter `FullyQualifiedName~TravelPlanning` | 477 passed, 0 failed, 0 skipped |
| Integration filter `FullyQualifiedName~TravelPlanning` | 46 passed, 0 failed, 0 skipped |

Tests use deterministic coordinates, fake providers and clock, and loopback HTTP. Integration process overrides disable shared SQL and live provider access. No physical SQL or live provider verification was run.
