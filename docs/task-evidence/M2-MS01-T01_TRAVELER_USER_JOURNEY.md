# WAYHUNTER - M2 Traveler User Journey Mapping

Task ID: M2-MS01-T01
Module: Traveler Planning, Maps and AI
Owner: M2 - Thadsha
Status: Draft - Pending Team Review

## 1. Objective

Document Traveler T-01 to T-15 user journeys.

Create Trip, Start Trip, Finish Trip and Leaderboard
must have separate flows and business rules.

Saving a trip must never count as completing a trip.

## 2. Traveler Screen Inventory

The following mapping is a working draft.
Screen labels require final BRD review.

| ID | Screen |
|---|---|
| T-01 | Traveler Home |
| T-02 | Create New Trip |
| T-03 | Place Picker |
| T-04 | Suggested Plans |
| T-05 | My Plans |
| T-06 | Start Trip / Live Map |
| T-07 | Find Nearby |
| T-08 | Report Hazard |
| T-09 | Add Memories |
| T-10 | Friends Nearby |
| T-11 | Finish Trip |
| T-12 | Share Trip / Add Friends |
| T-13 | AI Trip Planning |
| T-14 | Completed History |
| T-15 | Traveler Leaderboard |

## 3. Journey A - Create Trip

Flow:
T-01 -> T-02 -> T-03 -> T-02 -> T-05

1. User opens Traveler Home.
2. User selects Create New Trip.
3. User enters trip name and dates.
4. User enters budget in LKR.
5. User selects vehicle and headcount.
6. User selects start, stops and destination.
7. Backend validates the trip.
8. Trip is saved as an owned draft.
9. Draft appears in My Plans.

Acceptance:
- Empty trip name is rejected.
- Invalid dates are rejected.
- Negative budget is rejected.
- GPS denial must allow manual place search.
- Failed validation preserves user inputs.
- Saving a draft does not start a live session.
- Saving a draft does not increase leaderboard rank.

Owner: M2.

## 4. Journey B - Suggested and AI Plans

Suggested:
T-01 -> T-04 -> Preview -> Copy -> T-02 -> T-05

AI:
T-01 -> T-13 -> Review -> Edit -> T-02 -> T-05

Acceptance:
- Suggested plan copy creates a new draft.
- Original source plan remains unchanged.
- AI results are proposals, not verified facts.
- Estimated costs and times are labeled.
- Invalid places require user verification.
- Provider errors preserve entered data.
- AI must never auto-start a live trip.

Owner: M2.

## 5. Journey C - Start Trip

Flow:
T-05 -> Select Trip -> Start Trip -> T-06

1. User selects a saved authorized trip.
2. Backend verifies authenticated ownership.
3. User explicitly selects Start Trip.
4. M3 validates live-session eligibility.
5. M3 creates or resumes the live session.
6. Actual start time is recorded.
7. Live map controls become available.

Acceptance:
- Planned start date does not auto-start a trip.
- Duplicate active sessions are prevented.
- Location sharing requires consent.
- Starting a trip is not trip completion.

Owner:
M2 - Planned trip, stops and route handoff.
M3 - Actual live trip sessions and GPS.

## 6. Journey D - Active Trip

Flow:
T-06 -> T-07 / T-08 / T-09 / T-10 -> T-06

Actions:
- Find Nearby
- Report Hazard
- Add Memories
- Friends Nearby
- Pause or Stop Trip
- I Am Safe
- Finish Trip

Acceptance:
- Nearby Find is an on-demand action.
- No fake routes, distances or ETA on provider failure.
- GPS-sharing consent is enforced.
- Hazards use trusted reporting timestamps.
- Planned route hazard eligibility uses 1 km boundary.
- Active hazard alerts expire after 24 hours.
- Private memories and GPS data remain protected.

Owner:
M2 - Planned routes and Traveler presentation.
M3 - Live GPS, safety and hazard lifecycle.

## 7. Journey E - Finish Trip

Flow:
T-06 -> T-11 -> Confirm -> Completed History

1. User explicitly selects Finish Trip.
2. M3 verifies an authorized live session.
3. Backend records actual finish timestamp.
4. Session is finalized.
5. Live location sharing ends.
6. Valid completed-trip information is available
   to authorized history and ranking queries.

Acceptance:
- Saved trips do not count as completed.
- Cancelled drafts do not count as completed.
- Finish without valid start is rejected.
- Actual timestamps differ from planned dates.
- Invalid completion must not increase rank.

Owner:
M3 - Authoritative completion.
M2 - History and ranking eligibility queries.

## 8. Journey F - Traveler Leaderboard

Flow:
Traveler Leaderboard -> Public Rank / Own Position

Acceptance:
- Only eligible completed trips count.
- Saved drafts never increase completion count.
- Invalid or suspicious completions are excluded.
- Public fields respect privacy restrictions.
- Rank must use authoritative Backend records.

M2 owns Traveler ranking data.
Shared leaderboard UI requires M5/M7 coordination.

## 9. Cross-Module Responsibilities

| Member | Responsibility |
|---|---|
| M1 | Authentication and trusted UserId |
| M2 | Trip planning, drafts, stops, maps and AI |
| M3 | Live sessions, safety, GPS, Start and Finish |
| M4 | Approved Hunter places |
| M5 | Shared leaderboard presentation |
| M7 | Home shell and global navigation |

Other module business logic must not be duplicated.

## 10. Failure and Boundary Cases

| Case | Expected Result |
|---|---|
| Empty trip title | Validation error |
| End date before start | Validation error |
| Invalid budget | Validation error |
| GPS permission denied | Manual place search |
| Provider unavailable | Unavailable or retry state |
| Unauthorized trip | Access denied |
| Duplicate Start | No duplicate session |
| Save only | Not completed |
| Invalid Finish | No completion |
| AI provider error | Preserve user inputs |
| Suggested plan copy | Source unchanged |
| Cancelled draft edit | Rejected |
| Private history | Not publicly exposed |

## 11. Acceptance Checklist

- [x] T-01 to T-15 working inventory documented
- [x] Create Trip journey documented
- [x] Start Trip journey documented separately
- [x] Finish Trip journey documented separately
- [x] Traveler Leaderboard journey documented
- [x] M2 and M3 responsibilities separated
- [x] Authentication dependency documented
- [x] Provider failure behavior documented
- [ ] Final T-01 to T-15 labels verified against BRD
- [ ] Affected module owners reviewed
- [ ] Team Leader approved

## 12. References

- WAYHUNTER Second Brain M2-MS01-T01
- Master BRD v2.1 working Traveler journeys
- Latest approved project rules
- M2 cross-module contracts (proposed)

Implementation status:
Documentation only.
No Flutter or live API implementation is claimed.

Task status:
Draft - Pending review and acceptance.
