# WAYHUNTER — M3 Hazard Lifecycle & Threshold Decisions

**Task ID:** M3-MS01-T02  
**Owner:** Mathi (M3)  
**Module:** Active Trip, Safety & Notifications  
**Status:** DRAFT — PENDING REVIEW  
**Migration:** NOT REQUIRED

## 1. Hazard Lifecycle States

The planned hazard states are:

- **Reported:** Hazard submitted by a traveler; initially unverified.
- **Verified:** Hazard reviewed and verified by an authorized Admin.
- **Resolved:** Hazard has been marked resolved through an authorized action.
- **Expired:** Hazard warning has reached its 24-hour expiry.

### Proposed Transitions — Pending Review

| From | To | Trigger |
|---|---|---|
| Reported | Verified | Authorized Admin verification |
| Reported | Resolved | Authorized resolution |
| Reported | Expired | Warning expiry |
| Verified | Resolved | Authorized resolution |
| Verified | Expired | Warning expiry |

Final transition permissions and handling require review. No automatic verification is assumed.

## 2. Hazard Reporting Rules

- Every new community hazard is initially unverified.
- Record the hazard location and required report details.
- Preserve the hazard photo capture timestamp.
- Record `ReportedAtUtc` using trusted Backend server time.
- Validate photo capture time metadata.
- Do not treat editable photo metadata as trusted server time.
- Verification and resolution actions must be authorized and auditable.

## 3. Planned Route Matching

- Hazard warnings apply only to eligible trips whose planned route passes within **1 km** of the hazard.
- The distance must be checked against the planned route, not merely the user's current GPS position.
- Relevant warnings may appear during trip planning or an active trip.
- Users whose planned routes do not match must not receive the warning.

## 4. Expiry Rules

- A hazard warning expires **24 hours after `ReportedAtUtc`**.
- Expired warnings must disappear from active hazard views and alerts.
- Resolved hazards must not continue generating active warnings.
- Historical and audit records need not be physically deleted.
- Automatic expiry must not falsely imply Admin verification.

## 5. Notification Rules

- Show applicable safety warning information to eligible travelers.
- Display photo capture time and hazard verification status.
- Avoid unrelated route broadcasts.
- Prevent inappropriate repeated notifications.
- Exact alert cooldown duration remains pending configuration approval.

## 6. D08 Review Requirements

The following require alignment with the approved D08 hazard requirements:

- Hazard status lifecycle
- Report GPS and server timestamps
- Photo evidence and capture timestamp
- Verification and resolution actions
- 1 km planned-route matching
- 24-hour expiry
- Historical record and audit handling

## 7. Future Implementation

- Hazard status Domain Model
- Hazard lifecycle Unit Tests
- Route matching service
- Expiry processing
- Backend hazard endpoints
- Flutter hazard warning views
- Angular Admin hazard review

## 8. Approval

- Mathi (M3): PENDING
- M2 Route Integration Review: PENDING
- Team Leader: PENDING
- Peer Review: PENDING

**Final Status: DRAFT — NOT YET ACCEPTED**

No database migration is permitted by this document.