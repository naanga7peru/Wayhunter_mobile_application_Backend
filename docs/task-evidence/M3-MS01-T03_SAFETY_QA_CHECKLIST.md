
# WAYHUNTER M3 - Social and Safety QA Checklist

Task: M3-MS01-T03
Owner: Mathi (M3)
Status: DRAFT - PENDING REVIEW
Migration: NOT REQUIRED

## QA-01 - GPS Consent Required
- Accept trip invitation without GPS consent.
- Expected: Location sharing remains disabled.
- Result: NOT RUN

## QA-02 - GPS Consent Revocation
- Enable consent and then revoke it.
- Expected: No more GPS updates shared.
- Result: NOT RUN

## QA-03 - Duplicate Invitations
- Send the same invitation twice.
- Expected: Only one valid invitation.
- Result: NOT RUN

## QA-04 - Unauthorized Viewer
- Try to view trip GPS without permission.
- Expected: Access denied.
- Result: NOT RUN

## QA-05 - Stale GPS
- Simulate offline device.
- Expected: Show Offline or Stale status.
- Stale threshold: PENDING DECISION
- Result: NOT RUN

## QA-06 - Pause Sharing
- Pause an active trip.
- Expected: Live GPS publishing stops.
- Result: NOT RUN

## QA-07 - Finish Trip
- Finish an active trip.
- Expected: Sharing stops.
- Result: NOT RUN

## QA-08 - Reconnect After Revocation
- Reconnect after consent revoked.
- Expected: Sharing does not auto-start.
- Result: NOT RUN

## Review
- Peer review: PENDING
- Runtime testing: NOT RUN
- Final approval: PENDING
