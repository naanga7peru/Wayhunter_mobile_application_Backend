# WAYHUNTER - Historical Route Eligibility and Privacy Rules

Task: M2-MS01-T03
Owner: Thadsha (M2)
Status: DRAFT - PENDING REVIEW
Migration: NOT REQUIRED

## Eligibility
Only authorized, genuinely completed, same-trip consecutive segments are eligible.
M3 must verify actual Start and Finish events and segment completion.
Draft, cancelled, unfinished, paused and unverified segments are ineligible.

## Privacy
Private routes require server-side authorization.
Trip invitations do not grant live GPS consent.
Do not expose private memories or another user live GPS data.

## Reuse
Create a separate editable draft without changing the original trip.
Never fabricate geometry, distance or ETA.

## Ownership
M2 owns planned route reuse and drafts.
M3 owns live sessions, GPS consent and completion evidence.
M1 owns trusted identity contracts.

## QA Cases
Completed authorized consecutive segment: eligible for privacy evaluation.
Never-started or unfinished trip: rejected.
Different-trip stops: rejected.
Unauthorized private route: access denied.
Copied route edited: original remains unchanged.
Unavailable geometry: no fabricated route.

## Pending Approvals
Exact completion evidence, privacy permissions and M2-M3 handoff require review.
M3 Review: PENDING
Team Leader Approval: PENDING
QA Cases: DOCUMENTED - NOT EXECUTED

## Detailed Eligibility Rules
HR-R01: Verify trusted authenticated owner identity.
HR-R02: Require actual Start and Finish evidence from M3.
HR-R03: Both segment endpoints must belong to the same trip.
HR-R04: Only verified consecutive visited stops are eligible.
HR-R05: A saved plan alone is never completed history.
HR-R06: Do not combine unrelated trips or skipped segments.
HR-R07: Deny access without valid privacy permission.

## Historical Reuse Rules
HR-R08: Copy eligible route data into a new editable draft.
HR-R09: Preserve original completed trip and source attribution.
HR-R10: Editing a copied draft must not modify source records.
HR-R11: Reuse must not automatically start a live session.
HR-R12: Unavailable geometry must not produce fake route or ETA.

## Additional Privacy Rules
HR-P01: Server must verify route access for each request.
HR-P02: Never automatically publish private trip histories.
HR-P03: Do not expose raw live GPS or private memories.
HR-P04: A trip invitation does not grant GPS sharing consent.
HR-P05: M3 owns live GPS consent and completion evidence.
HR-P06: Exact sharing and retention policy requires approval.

## QA Acceptance Matrix
HR-Q01: Authorized completed consecutive segment - eligible for privacy evaluation.
HR-Q02: Draft or cancelled trip - ineligible.
HR-Q03: Started but unfinished trip - ineligible.
HR-Q04: Mixed-trip segments - ineligible.
HR-Q05: Unauthorized route access - denied.
HR-Q06: Copied draft editing - original unchanged.
HR-Q07: Missing geometry - no fabricated ETA.
HR-Q08: Private memory without consent - not exposed.
HR-Q09: Paused or unverified segment - not presented as completed.

## Dependencies and Decisions
M1: Trusted UserId and route authorization contract.
M3: Start/Finish evidence and completed segment contract.
M2: Historical route eligibility and reusable draft design.
Pending: skipped stop behavior, retention, sharing policy and geometry format.

## Source References
Second Brain M2_FULLSTACK_MILESTONES_TASKS.md - M2-MS01-T03.
Second Brain PROJECT_RULES.md - Traveler completion and privacy.
Second Brain M2_TRAVELER_CROSS_MODULE_CONTRACTS_2026-11-13.md.

## Final Review Status
Documentation: DRAFT
QA: CASES DOCUMENTED, NOT EXECUTED
M3 Approval: PENDING
Team Leader Approval: PENDING
Database Migration: NOT REQUIRED
