# WAYHUNTER - AI Trip Plan Validation and Estimate Labels

## Task Information
Task ID: M2-MS01-T04
Owner: Thadsha (M2)
Module: Traveler Planning, Maps and AI
Status: DRAFT - PENDING REVIEW
Migration: NOT REQUIRED

## Objective
Define validation and estimate labels for AI-generated trip plans.
External AI provider integration is in scope for future tasks.
Building a proprietary AI model is OUT OF SCOPE.

## AI Plan Rules
AI output is a proposal, not an automatically approved trip.
Generated stops, dates, costs and travel estimates must remain editable.
Users must review and accept an AI proposal before saving it as a trip draft.
Saving a draft must never automatically start a live trip.
AI must not modify an existing source trip without user action.

## Validation Rules
AI-generated trip titles must not be empty.
Trip dates must be valid and in an accepted order.
Budget and cost values must be numeric and non-negative.
Costs must identify LKR when presented in Sri Lankan Rupees.
Unknown or invalid places must require user review.
Coordinates must be validated before use.
Malformed or incomplete provider responses must not be silently accepted.

## Estimate Labels
Estimated trip cost must display an Estimate label.
Estimated travel duration must display an Estimate label.
AI suggestions must not be represented as confirmed bookings.
Unverified places must not be labelled Hunter Approved.
Route distance and ETA must not be fabricated if routing fails.

## Scope Exclusions
No proprietary AI model development in the current scope.
No automatic payment or booking from AI-generated plans.
No automatic live trip session creation.
No database migration under this task.

## Provider Failure Handling
AI timeout or quota failure must show a clear unavailable or retry state.
Provider failure must not erase the user original input.
Partial AI responses must be reviewed before conversion to a draft.
Fake places, costs, route geometry or weather must not be introduced as real data.

## QA Acceptance Cases
AI-Q01: Editable generated stops - PASS expected.
AI-Q02: Editable estimated LKR budget - PASS expected.
AI-Q03: Invalid or missing dates - validation error.
AI-Q04: Negative or nonnumeric cost - validation error.
AI-Q05: Unknown place - requires user review.
AI-Q06: Malformed AI response - reject safely.
AI-Q07: AI provider unavailable - show retry without losing input.
AI-Q08: Estimated cost and duration - clearly labelled estimates.
AI-Q09: AI proposal creation - does not start live trip.
AI-Q10: User edits copied proposal - original source remains unchanged.

## Module Responsibilities
M2: AI proposal validation, editable planning drafts and estimate labels.
M3: Actual live trip start, pause and finish actions.
M4: Approved Hunter place authority.
M1: Trusted user identity and authorization.

## Pending Decisions
Confirm final structured AI response fields and validation limits.
Confirm provider selection, quotas and retry configuration.
Confirm user-facing labels with Traveler UI review.

## References
Second Brain M2_FULLSTACK_MILESTONES_TASKS.md - M2-MS01-T04.
Second Brain M2_TRAVELER_CROSS_MODULE_CONTRACTS_2026-11-13.md.

## Review Status
Documentation: DRAFT
QA: CASES DOCUMENTED - NOT EXECUTED
M2 Review: PENDING
Team Leader Approval: PENDING
Database Migration: NOT REQUIRED
