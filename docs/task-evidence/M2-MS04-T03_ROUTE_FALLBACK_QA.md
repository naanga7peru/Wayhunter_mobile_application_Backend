# M2-MS04-T03 Route preview fallback QA

The official acceptance is “No fabricated ETA; user can retry or edit stops.” This evidence covers the backend preview contract and offline verification. [Official M2 brief](https://github.com/naanga7peru/Wayhunter_SecondBrain_Docs/blob/main/Wayhunter_Second_Brain_v1_0/planning/member_briefs/M2_FULLSTACK_MILESTONES_TASKS.md#L112)

## Backend contract

`POST /api/mobile/trip-plans/routes/preview` accepts two to 27 ordered stops with finite, in-range coordinates. It requires a trusted authenticated user and applies a daily quota. It calculates a transient preview and has no trip create, save or start operation.

| HTTP status | Conditions and payload |
| --- | --- |
| 200 | A usable provider result supplies geometry, distance, estimated duration, provider name and optional encoded polyline. The estimate is traffic-unaware. |
| 400 | Invalid stop count or coordinates yields `INVALID_ROUTE_REQUEST`. Framework body validation can return its own 400. |
| 401 | Missing or invalid trusted identity. |
| 429 | Per-user quota reached, with `ROUTE_DAILY_LIMIT_REACHED`. |
| 502 | An invalid result reaching preview validation yields `INVALID_ROUTE_RESPONSE`, including empty geometry or missing or invalid metrics in a claimed success. |
| 503 | Disabled routing or a provider failure yields `ROUTES_PROVIDER_UNAVAILABLE`. The Google adapter can classify malformed upstream data as a provider failure before preview validation. Invalid enabled configuration can fail application startup through options validation. |

The controller's 502 and 503 bodies contain only the error code. Keep the existing stop selections on failure, display that the route estimate is unavailable, and let the traveler retry or edit stops. Show a distance, ETA or polyline only from a successful preview for the current stop order. These are client expectations. Flutter device and end-to-end behavior were outside this verification. MS04-T04 versioned route persistence remains a separate blocked task.

## Offline verification

The controller regression covers unavailable configuration, sanitized error payloads, unchanged stops on failure, a successful retry using provider metrics and polyline, and missing or invalid metrics in otherwise successful results. Existing controller, service and adapter cases cover access control, quota, invalid stops, malformed geometry and provider failures.

| Check | Result |
| --- | --- |
| Solution build with `--no-restore` after refreshing declared package assets | Passed, 0 errors and 2 existing `xUnit2031` warnings |
| Route preview controller, service and Google Routes adapter unit cases | 35 passed, 0 failed, 0 skipped |
| TravelPlanning unit cases | 469 passed, 0 failed, 0 skipped |
| TravelPlanning integration cases | 38 passed, 0 failed, 0 skipped |

Verification used test doubles and loopback HTTP with process-local SQL and external provider overrides for the integration suite. Live Google and physical SQL checks were outside this run.
