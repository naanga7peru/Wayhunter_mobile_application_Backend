# M2 backend handover

Start locally with `dotnet run --project src/Wayhunter.Api`. The default trip repository is in memory. The mobile endpoints use the existing trusted identity claim contract.

| Integration | Environment variable names |
| --- | --- |
| Google Places | `TravelPlanning__Maps__EnablePlaces`, `TravelPlanning__Maps__PlacesServerApiKey` |
| Google Routes | `TravelPlanning__Maps__EnableRoutes`, `TravelPlanning__Maps__RoutesServerApiKey` |
| Gemini | `TravelPlanning__AiTrip__Enabled`, `TravelPlanning__AiTrip__ApiKey`, `TravelPlanning__AiTrip__Model`, `TravelPlanning__AiTrip__TimeoutSeconds`, `TravelPlanning__AiTrip__MaxRequestsPerUserPerDay` |
| Weather | `TravelPlanning__Weather__Enabled`, `TravelPlanning__Weather__ApiKey`, `TravelPlanning__Weather__CacheMaxAgeMinutes`, `TravelPlanning__Weather__TimeoutSeconds` |
| Persistence gates | `TravelPlanning__EnableSqlPersistence`, `TripSafety__EnableSqlPersistence` (suggested list/detail only when both are enabled) |

Flutter can use the same search and detail endpoints when location permission is denied. Render Places as external suggestions. Route preview requires ordered coordinates and returns a traffic-unaware estimate. Fetch a fresh preview after stop edits. Weather displays current observation time and freshness. AI generation returns a proposal for review. Submit reviewed stops in order as initialStops on the existing POST /api/mobile/trip-plans. Each stop accepts name and optional placeId and paired latitude/longitude. The request creates an editable draft. Preserve current draft version when editing stops. ADM-07 remains pending.

Suggested detail uses the same access and field projection as the list. The member-copy flag is retired from runtime while reuse approval is pending. Unknown, denied and disabled copies return 404.

Weather forecasts use OpenWeather’s five-day, three-hour product. Forecast timestamps are UTC and expired points are omitted. Forecast age is measured from retrieval, and current weather age is measured from observation and cache timestamps. Each weather cache holds up to 256 exact coordinate pairs. Stale fallback lasts up to 24 hours. Weather is advisory and independent from M3 hazards. [OpenWeather forecast contract](https://openweathermap.org/api/forecast5)

Existing Nearby, Routes and Gemini limits retain their original daily values. Place search and details share a 60-request UTC-day limit per verified identity. Current weather and forecasts share a separate 60-request limit. Each quota holds up to 10,000 identities per UTC day, denies new identities at capacity and clears counters on rollover. These are process-local controls. Distributed provider-budget enforcement remains open.

The local completion branch is based on `origin/develop` at `d13a350`. The changes introduced by `e2a2465` are present as working-tree edits, with its DI changes reconciled against newer Nearby wiring. Review with `git status -sb` and `git diff`, then inspect each untracked M2 file. Stage reviewed M2 paths explicitly, commit locally and push the completion branch only after review. Exclude the pre-existing prompt and Microsoft directory.

## Changed code and test files

- `src/Wayhunter.Api/Controllers/Mobile/TravelPlanning/CopySuggestedTripPlansController.cs`
- `src/Wayhunter.Api/Controllers/Mobile/TravelPlanning/PlacePickerController.cs`
- `src/Wayhunter.Api/Controllers/Mobile/TravelPlanning/RoutePreviewController.cs`
- `src/Wayhunter.Api/Controllers/Mobile/TravelPlanning/SuggestedTripPlansController.cs`
- `src/Wayhunter.Api/Controllers/Mobile/TravelPlanning/WeatherController.cs`
- `src/Wayhunter.Api/Extensions/TravelPlanningServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/TravelPlanningSqlServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/TravelPlanningWeatherExtensions.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Commands/CreateTripPlanService.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Dtos/CreateTripPlanDto.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Dtos/RoutePreviewRequest.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Interfaces/IPlaceDetailsProvider.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Interfaces/IRoutesProvider.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Interfaces/ISuggestedTripPlansReader.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Interfaces/IWeatherForecastProvider.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Queries/BoundedWeatherCache.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Queries/GetSuggestedTripPlansService.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Queries/PreviewTripRouteService.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Queries/WeatherCacheService.cs`
- `src/Wayhunter.Application/Modules/TravelPlanning/Queries/WeatherForecastService.cs`
- `src/Wayhunter.Infrastructure/Integrations/AI/InMemoryAiTripDailyQuota.cs`
- `src/Wayhunter.Infrastructure/Integrations/Maps/GooglePlacePickerProvider.cs`
- `src/Wayhunter.Infrastructure/Integrations/Maps/GoogleRoutesProvider.cs`
- `src/Wayhunter.Infrastructure/Integrations/Maps/InMemoryNearbyPlacesDailyQuota.cs`
- `src/Wayhunter.Infrastructure/Integrations/Maps/InMemoryRoutePreviewDailyQuota.cs`
- `src/Wayhunter.Infrastructure/Integrations/Maps/InMemoryTravelProviderQuota.cs`
- `src/Wayhunter.Infrastructure/Integrations/Weather/OpenWeatherMapProvider.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/TravelPlanning/SqlSuggestedTripPlansReader.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/NearbyPlacesHttpTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/CreateTripPlanServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/GooglePlacePickerProviderTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/GoogleRoutesProviderTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/PreviewTripRouteServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/RoutePreviewControllerTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/TravelPlanningSqlServiceCollectionExtensionsTests.cs`
- `tests/Wayhunter.UnitTests/Modules/TravelPlanning/WeatherForecastAndQuotaTests.cs`
