# M2 endpoint inventory

Prefix `/api/mobile/trip-plans`. All controllers require authentication and trusted user identity (401). Trip and stop operations enforce ownership. The table lists additional responses.

| Method and path suffix | Input → output | Status | Controller and test evidence |
| --- | --- | --- | --- |
| POST / | CreateTripPlanDto with optional ordered InitialStops → TripPlanResponseDto | 201, 400 | TripPlansController / TripPlansControllerTests |
| GET / | Query paging → owner plan list | 200, 400 | TripPlansController / GetMyTripPlansServiceTests |
| GET /{tripPlanId} | GUID → TripPlanResponseDto | 200, 400, 404 | TripPlansController / GetTripPlanServiceTests |
| PUT /{tripPlanId} | UpdateTripPlanDto → TripPlanResponseDto | 200, 400, 404, 409 | TripPlansController / UpdateTripPlanServiceTests |
| DELETE /{tripPlanId} | GUID → empty | 204, 400, 404, 409 | TripPlansController / CancelTripPlanServiceTests |
| POST /{tripPlanId}/stops | AddTripStopDto → updated plan | 200, 400, 404, 409 | TripStopsController / AddTripStopServiceTests |
| DELETE /{tripPlanId}/stops/{stopId} | GUIDs and If-Match → updated plan | 200, 400, 404, 409 | RemoveTripStopController / RemoveTripStopServiceTests |
| PUT /{tripPlanId}/stops/order | ReorderTripStopsDto → updated plan | 200, 400, 404, 409 | ReorderTripStopsController / ReorderTripStopsHttpTests |
| GET /suggested | Limit → SuggestedTripPlanDto list | 200, 400 | SuggestedTripPlansController / GetSuggestedTripPlansServiceTests |
| GET /suggested/{tripPlanId} | GUID → SuggestedTripPlanDto card fields only | 200, 400, 404 | SuggestedTripPlansController / NearbyPlacesHttpTests |
| POST /suggested/{sourceTripPlanId}/copy | GUID and required Idempotency-Key header → CopiedSuggestedTripPlanDto | 201, 400, 401, 404, 409 | CopySuggestedTripPlansController / LocalDbM2FunctionalTests |
| PUT /{tripPlanId}/reuse-consent | GUID and JSON `{"allowed":true}` to grant or `{"allowed":false}` to revoke | 204, 400, 401, 404 | CopySuggestedTripPlansController / LocalDbM2FunctionalTests |
| POST /ai/generate | AiTripItineraryRequest → AiTripItineraryResponse | 200, 400, 429, 502, 503, 504 | AiTripPlansController / AiTripPlansControllerTests |
| POST /routes/preview | RoutePreviewRequest → routeGeometry, encodedPolyline, distanceKm, estimatedDurationMinutes, providerName, trafficAware | 200, 400, 413, 429, 502, 503 | RoutePreviewController / RoutePreviewControllerTests, NearbyPlacesHttpTests |
| GET /nearby | Latitude, longitude, category → nearby results | 200, 400, 429, 502, 503 | NearbyPlacesController / NearbyPlacesHttpTests |
| GET /places/search | Query → array of external place IDs, names, coordinates, sources and verification flags | 200, 400, 429, 503 | PlacePickerController / NearbyPlacesHttpTests |
| GET /places/{providerPlaceId} | Google place ID → external place | 200, 400, 404, 429, 503 | PlacePickerController / NearbyPlacesHttpTests |
| GET /weather | Latitude and longitude → status, observation, cachedAtUtc, fromCache | 200, 400, 429, 503 | WeatherController / NearbyPlacesHttpTests, WeatherCacheServiceTests |
| GET /weather/forecast | Latitude and longitude → status, requested coordinates, retrievedAtUtc, providerName, future forecast points | 200, 400, 429, 503 | WeatherController / NearbyPlacesHttpTests, WeatherForecastAndQuotaTests |

Historical segments, D11 rankings and ADM-07 have no M2 HTTP routes in this worktree.
