# WAYHUNTER — M3-MS02-T06 Group & Hazard Active-Session Contract

**Owner:** Mathi (M3)  
**Task:** M3-MS02-T06  
**Status:** DRAFT — PENDING REVIEW  
**Database Migration:** NOT REQUIRED

## 1. Purpose

Define how TripSafety session status controls live GPS events shared with Group Trip modules.

Keep live GPS publishing separate from planned-route hazard warning eligibility.

## 2. Live GPS Publishing Rules

Live GPS publishing requires all of the following:

1. Authenticated user identity from the Backend.
2. Authorized trip ownership or accepted membership.
3. A matching TripPlanId and SessionId.
4. LiveTripSession status = Active.
5. Explicit GPS sharing consent for the same user and session.
6. Consent is currently granted and not revoked.
7. Recipient access is independently authorized.

Invitation acceptance must never automatically grant GPS sharing consent.

## 3. Session Status Contract

| Session Status | Live GPS Publish |
|---|---|
| Active | Allowed only after all authorization and consent checks |
| Paused | Blocked |
| Finished | Blocked |
| Missing session | Blocked |
| Wrong TripPlanId | Blocked |

Resume requires valid current consent; resuming a trip must not automatically restore previously revoked consent.

## 4. Current Application Components

- ActiveSessionLocationEventValidator
- ActiveSessionLocationEventValidatorTests
- GroupLiveLocationPublishValidator
- GroupLiveLocationPublishValidatorTests

These are validation components, not completed GPS publishing endpoints.

## 5. Hazard Module Boundary

Planned-route hazard warnings are independent of active trip status.

Approved product requirements:

- Match hazards within 1 km of the planned route.
- Include eligible planning and active-trip views.
- Use trusted server ReportedAtUtc for the 24-hour warning expiry.
- Show validated hazard photo capture timestamp.
- Do not broadcast warnings to unrelated routes.

The live GPS publish validator must not block planned-route hazard alerts.

Hazard report submission authorization and evidence validation require a separate integration contract. Do not automatically apply the live GPS publish rule to every hazard operation.

## 6. Backend Integration Requirements

Before publishing any group location event, the Backend must:

- Resolve authenticated user identity from a trusted authentication context.
- Load the current session and verify it is Active.
- Verify current trip membership or ownership.
- Load the matching GPS consent for the authenticated user and session.
- Validate permission for each recipient or recipient group.
- Stop publishing immediately when the trip is paused, finished, or consent is revoked.

Do not trust a client-supplied authorization boolean, UserId, session