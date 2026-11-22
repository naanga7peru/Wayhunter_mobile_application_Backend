# M2-MS10-T05 — Provider Configuration and Safe Local Run Guide

## Scope and source status

**Acceptance supplied for this task:** document API environment-variable names, provider limits, and demo steps without exposing credentials or secrets. The linked official Second Brain brief could not be fetched during this work; source verification is unavailable, and this guide relies on the acceptance text supplied in the task.

This guide reflects the current API options, controllers, provider adapters, quota implementations, and test fixtures. Configuration names are documented; effective configuration and secret values were not inspected. External providers and SQL persistence must remain disabled unless separately approved and configured by the local developer.

## Keep / modify / add

| Action | Document | Reason |
|---|---|---|
| KEEP | `M2_BACKEND_ENDPOINT_INVENTORY.md` | Existing route and DTO inventory remains the endpoint reference. |
| MODIFY | `M2_BACKEND_HANDOVER.md` | Correct the old branch instructions and outdated quota summary; link this guide. |
| ADD | This guide | The handover lacked verified limits, a safe synthetic demo sequence, and precise provider-disabled behavior. |

## Local prerequisites and run commands

Use the repository's .NET SDK version recorded by `dotnet --info` and the checked-in solution/project files. From the repository root:

```powershell
dotnet restore Wayhunter.slnx
dotnet build Wayhunter.slnx --no-restore
dotnet run --project src/Wayhunter.Api
```

The API project exists at the documented path. Its `Wayhunter.Api` launch profile sets the local HTTP base URL to `http://localhost:5055`; confirm the selected profile if using a different launch method. The default TripPlan repository registration is in memory unless `TravelPlanning:EnableSqlPersistence` is enabled by configuration. This audit did not inspect effective local configuration. Before launching, verify only that both SQL gates and provider enable flags are false; do not print or expose any neighboring secret values. If those flags cannot be safely confirmed, do not launch the API. Do not enable SQL as part of this guide; shared SQL approval and a separately authorized disposable test database are prerequisites for SQL work.

All mobile controllers use `[Authorize]`. Supply a token obtained through the approved local identity setup:

```powershell
$BaseUrl = 'http://localhost:5055'
$Headers = @{ Authorization = 'Bearer <LOCAL_ACCESS_TOKEN>' }
```

`<LOCAL_ACCESS_TOKEN>` is a placeholder, not a usable token. Do not bypass authentication or paste tokens into source files, documentation, terminal transcripts, or support chats. Identity setup and trusted user claims remain governed by the existing identity module.

## Verified server-side configuration names

Use the double-underscore form shown below for environment variables. These names map to the current .NET options sections. API keys remain server-side secrets and are never sent to mobile clients. Do not place actual values in this document.

| Provider / gate | Environment variable names | Code default and validation |
|---|---|---|
| Google Places picker and Nearby | `TravelPlanning__Maps__EnablePlaces`, `TravelPlanning__Maps__PlacesServerApiKey` | `EnablePlaces` defaults false. A nonblank server key is required when enabled. |
| Google Routes preview | `TravelPlanning__Maps__EnableRoutes`, `TravelPlanning__Maps__RoutesServerApiKey` | `EnableRoutes` defaults false. A nonblank server key is required when enabled. |
| Maps geocoding configuration | `TravelPlanning__Maps__EnableGeocoding`, `TravelPlanning__Maps__GeocodingServerApiKey` | `EnableGeocoding` defaults false. A nonblank key is required when enabled. No geocoding API route was found in the inspected M2 controller inventory. |
| Gemini itinerary proposal | `TravelPlanning__AiTrip__Enabled`, `TravelPlanning__AiTrip__ApiKey`, `TravelPlanning__AiTrip__Model`, `TravelPlanning__AiTrip__TimeoutSeconds`, `TravelPlanning__AiTrip__MaxRequestsPerUserPerDay` | Disabled by default; timeout defaults to 30 seconds (valid 5–120); quota defaults to 10 (valid 1–100). Key and model are required when enabled. |
| OpenWeather current and forecast | `TravelPlanning__Weather__Enabled`, `TravelPlanning__Weather__ApiKey`, `TravelPlanning__Weather__CacheMaxAgeMinutes`, `TravelPlanning__Weather__TimeoutSeconds` | Disabled by default; cache freshness defaults to 30 minutes (valid 1–1440); provider timeout defaults to 15 seconds (valid 5–60); key is required when enabled. |
| M2 SQL persistence gate | `TravelPlanning__EnableSqlPersistence` | Boolean gate read by the persistence registration. Keep disabled absent separate schema/runtime and disposable SQL approvals. |
| M3 SQL persistence gate | `TripSafety__EnableSqlPersistence` | Boolean gate used by M3/shared registration; keep disabled absent its approvals. Suggested-trip SQL reading requires both M2 and M3 SQL registration. |

No provider is enabled by this guide. Invalid or missing configuration for an enabled provider is validated at startup or returned as unavailable according to the current provider/controller path.

## Provider request bounds, timeouts, caches, and quotas

These are current application-enforced values, not a promise of a third-party account's billing quota. Provider-plan limits can change and must be checked independently before any approved live use.

| Operation | Current code limits |
|---|---|
| Google Place text search | Query is trimmed, nonempty, at most 200 characters, and cannot contain control characters. The request asks Google for at most 10 places; parsed results are deduplicated and capped at 10. Provider timeout is 12 seconds. |
| Google Place details | Provider place ID is limited to 512 ASCII letters/digits, `-`, or `_`. Search and details share a quota of 60 calls per authenticated user per UTC day. Provider timeout is 12 seconds. |
| Google Nearby | Fixed 1,000-metre radius; provider requests at most 20 results and the application filters coordinates to the inclusive 1 km boundary. Separate quota: 20 calls per authenticated user per UTC day. Provider timeout: 12 seconds. External results are not Hunter-verified. |
| Google Routes preview | 2–27 ordered waypoints; coordinates are validated; request body limit is 16 KiB. Quota: 20 calls per authenticated user per UTC day. Provider timeout: 15 seconds. The request is traffic-unaware. |
| Gemini AI proposal | Request body limit is 16 KiB; provider generation is capped at 4,096 output tokens and response input is bounded at 256 KiB. Timeout defaults to 30 seconds, configurable from 5 to 120. Daily user quota defaults to 10 and is configurable from 1 to 100. |
| Current weather | Current weather shares a 60-call-per-user-per-UTC-day quota with forecast. Cache capacity is 256 exact coordinate keys. Freshness defaults to 30 minutes; provider timeout defaults to 15 seconds. Stale fallback is limited to observations no older than 24 hours, retaining original observation and cache timestamps. |
| Weather forecast | Shares the same 60-call weather quota. Cache capacity is 256 coordinate keys and freshness defaults to 30 minutes. Returned forecast points are future-only and within five days. Stale cached points are usable only while still future and the cache is at most 24 hours old. |
| Quota storage | The quota implementation is in-memory and process-local, with capacity for 10,000 user identities per quota instance and UTC-day rollover. Counters are not shared across API replicas and are not a production-wide provider budget. |

AI generation returns a validated, review-required proposal. It does not persist a TripPlan or start an M3 live session. Route preview is transient and does not persist geometry. A provider failure or invalid response must not be replaced with invented route geometry, distance, ETA, place results, AI proposal, observation, or forecast.

## Safe synthetic traveler-planning demo

This sequence is documentation only; no API request or TripPlan was created during this task. Use a local API instance, an approved local identity, synthetic coordinates/data, and the in-memory TripPlan mode. Do not run it against production or enable SQL.

1. Start the API using the commands above and set `$BaseUrl` and `$Headers` as shown. Obtain the bearer token only through the approved local identity flow.
2. Create a synthetic draft with the existing `POST /api/mobile/trip-plans` contract. `CreateTripPlanDto` uses `tripName`, `startDate`, `endDate`, `budgetLkr`, `vehicleType`, `headcount`, and optional ordered `initialStops`. Each `InitialTripStopDto` uses `name`, optional `placeId`, and optional paired `latitude`/`longitude`.

   Example request body (synthetic values):

   ```json
   {
     "tripName": "Local demo plan",
     "startDate": "2026-12-01",
     "endDate": "2026-12-02",
     "budgetLkr": 12000,
     "vehicleType": "Car",
     "headcount": 2,
     "initialStops": [
       { "name": "Demo origin", "placeId": null, "latitude": 9.660, "longitude": 80.020 },
       { "name": "Demo destination", "placeId": null, "latitude": 9.670, "longitude": 80.030 }
     ]
   }
   ```

   To submit that synthetic body from PowerShell against the local API:

   ```powershell
   $DraftBody = @'
   { "tripName": "Local demo plan", "startDate": "2026-12-01", "endDate": "2026-12-02", "budgetLkr": 12000, "vehicleType": "Car", "headcount": 2, "initialStops": [ { "name": "Demo origin", "placeId": null, "latitude": 9.660, "longitude": 80.020 }, { "name": "Demo destination", "placeId": null, "latitude": 9.670, "longitude": 80.030 } ] }
   '@
   $Draft = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/mobile/trip-plans" -Headers $Headers -ContentType 'application/json' -Body $DraftBody
   $TripId = $Draft.tripPlanId
   Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/mobile/trip-plans/$TripId" -Headers $Headers
   ```

3. Read the returned `tripPlanId` using `GET /api/mobile/trip-plans/{tripPlanId}`. The local default repository is in memory; these examples are not SQL persistence evidence.
4. For an editable draft only, add a stop with `POST /api/mobile/trip-plans/{tripPlanId}/stops` and the `AddTripStopDto` shape (`name`, optional `placeId`, paired coordinates, and optional `expectedRowVersion`). In the default in-memory demo, omit the version because that repository has no SQL RowVersion. A SQL-backed trip requires the current 8-byte version for add/reorder operations and SQL use is outside this guide's authorization. Reorder using `PUT /api/mobile/trip-plans/{tripPlanId}/stops/order` with `stopIds` in the requested order. Use the latest version where SQL-backed; stale edits are rejected.
5. Request a transient route preview with `POST /api/mobile/trip-plans/routes/preview`:

   ```json
   {
     "orderedStops": [
       { "latitude": 9.660, "longitude": 80.020 },
       { "latitude": 9.670, "longitude": 80.030 }
     ]
   }
   ```

   Submit the preview only after enabling Routes through an approved local configuration and supplying the server-side key securely; the example call is:

   ```powershell
   $RouteBody = '{ "orderedStops": [ { "latitude": 9.660, "longitude": 80.020 }, { "latitude": 9.670, "longitude": 80.030 } ] }'
   Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/mobile/trip-plans/routes/preview" -Headers $Headers -ContentType 'application/json' -Body $RouteBody
   ```

   The `RoutePreviewRequest` uses `orderedStops` of `RouteWaypoint`. The preview is neither persisted nor authoritative M3 geometry. With the provider disabled or unconfigured, expect HTTP 503 with `ROUTES_PROVIDER_UNAVAILABLE`. Do not substitute a fabricated route, distance, or estimated duration. After an outage, retry the preview or edit the stops and request a new preview.
6. Optional AI proposal route: `POST /api/mobile/trip-plans/ai/generate` accepts the current `AiTripItineraryRequest` fields: `startLocation`, `destination`, `startDate`, `endDate`, `budgetLkr`, `vehicleType`, `headcount`, and optional `preferences`. Disabled/unconfigured AI returns unavailable (503). When separately enabled, the response still requires traveler review. The user must choose edited values and explicitly use the normal TripPlan draft creation endpoint; AI generation itself does not save or activate a trip.
7. No provider request, route preview, or saved draft starts an M3 live session. Historical completed-trip evidence and active GPS sessions remain M3 concerns.

All endpoints require authorization and trusted identity. Placeholder tokens are not credentials. No real provider call is required for the disabled-provider path. If live route data is needed, obtain provider credential, billing/quota, and test-scope approval before enabling the provider; never place keys in a client app.

## Offline verification

From repository root:

```powershell
dotnet build Wayhunter.slnx --no-restore
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --filter "FullyQualifiedName~TravelPlanning"
dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~NearbyPlacesHttpTests"
```

The loopback HTTP test fixture disables SQL persistence and routes and uses injected test providers; inspect its current setup before running. These commands do not verify physical SQL transactions, live provider credentials, or device UI. Do not run integration suites whose configuration can reach shared SQL or live external providers without separate authorization.

## Approval-dependent and unavailable work

- **DECISION_REQUIRED:** shared SQL schema/runtime approval and authorized disposable SQL test environment. Physical SQL QA has not been executed.
- **DECISION_REQUIRED:** M2-MS04-T04 route geometry persistence and M3 version-aligned consumer contract; current preview is transient.
- **CODE_VERIFIED — unavailable:** M3 hazard route matching is registered with `UnavailableHazardRoutesProvider`; this guide does not enable an M3 production route adapter.
- **DECISION_REQUIRED:** TripStop-to-memory foreign key and M2 HTTP 409 contract approval; retained M3 memories/tombstones are not part of this run guide's implementation.
- **CODE_VERIFIED — disabled:** Suggested Plan Copy uses an unavailable store by default. Copy consent/attribution approval and SQL activation remain pending.
- **NOT EXECUTED:** M3 historical completed-trip evidence integration and D11 traveler ranking policy.
- **DECISION_REQUIRED:** Angular Admin ADM-07 backend endpoint and approval contract.
- **DECISION_REQUIRED:** M4 approved Hunter Places read contract.
- **NOT EXECUTED:** live Google, Gemini, and OpenWeather provider verification; restricted credentials and approved test scope are required.
- **NOT EXECUTED:** Flutter/device end-to-end and user-visible retry, permission, and stale-data states.
