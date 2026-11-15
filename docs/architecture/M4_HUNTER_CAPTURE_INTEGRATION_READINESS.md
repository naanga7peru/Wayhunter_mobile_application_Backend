
# WAYHUNTER — M4 Hunter Capture Integration Readiness

**Date:** 2026-11-15
**Owner:** M4 — Neela
**Related Modules:** M1 Identity / M4 Hunter Places
**Status:** PENDING REVIEW — MIGRATION HOLD
**Final Approval:** PENDING

---

## 1. Purpose

Review the integration requirements for secure Hunter capture
session creation before implementing production authentication,
identity mapping, persistent storage, or database migrations.

This document records existing implementation evidence and
unresolved decisions.

It does not authorize database migrations or schema changes.

## 2. Current Implementation

### 2.1 Existing Components

- HunterStartCaptureSessionHandler
- HunterStartAuthorizedCaptureSessionHandler
- HunterCaptureSubmissionOwnershipService
- HunterCaptureSessionIdentityResolutionService
- IHunterCaptureSubmissionAccessReader
- IHunterCaptureSessionIdentityResolver
- InMemoryHunterCaptureSubmissionAccessReader

### 2.2 Current Authorization Sequence

1. Receive an authenticated account identifier from a trusted
   backend authentication mechanism.
2. Validate the capture-session request.
3. Read the trusted Hunter submission ownership snapshot.
4. Verify that the authenticated user owns the submission.
5. Resolve the account identifier to a trusted session Hunter ID.
6. Create the capture session only after successful checks.

The existing handler coordinates steps 2–6.

Production authentication and identity mapping are not yet
established by this implementation.

### 2.3 Tests

- PR #88 — Capture Session Handler — Merged
- PR #89 — Ownership Validation — Merged
- PR #92 — Authorized Capture Session Handler — Merged
- PR #100 — In-Memory Submission Access Reader — Merged
- PR #102 — Reader Authorization Integration Tests — Merged

Latest locally verified full solution result:

- Build: PASS
- Tests: 1,320 passed
- Failed: 0
- Skipped: 0

These results verify the tested code paths, not production
authentication or database integration.

## 3. Existing Identity Contracts

### M1 UserAccount

- UserAccount.UserId: string
- Current account registration generates a GUID-formatted string.

### M4 HunterSubmission

- HunterSubmission.Id: Guid
- HunterSubmission.UserId: string
- HunterSubmission.Status: HunterSubmissionStatus

### M4 Capture Session

- Capture session store expects a Guid Hunter ID.
- IHunterCaptureSessionIdentityResolver resolves an
  authenticated string account ID to a nullable Guid.

### Identity Contract Gap

A formally approved, production-safe mapping between
M1 UserAccount.UserId and the session Hunter ID is pending.

The existing GUID-formatted account IDs do not themselves
constitute approval of a conversion strategy.

No random identity generation or client-controlled Hunter ID
may be used as a fallback.

## 4. Identity Mapping Decisions — PENDING

The M1 owner, M4 owner, database coordinator, and team leader
must review:

1. Is the capture-session Hunter ID the same logical identity
   as M1 UserAccount.UserId?
2. Will the team formally approve a GUID parsing strategy,
   or define another trusted mapping?
3. Which backend authentication claim identifies the account?
4. Where is the mapping validated and maintained?
5. What happens when the account is absent, inactive, or the
   mapping cannot be resolved?
6. How is cross-module identity integrity enforced?

**Decision status: PENDING**

No production mapping implementation is approved by this document.

## 5. Hunter Submission Status Contract

Current enum values:

| Status | Value |
|---|---:|
| Draft | 0 |
| PendingReview | 1 |
| Held | 2 |
| Approved | 3 |
| Rejected | 4 |

The current ownership service checks submission ownership.

It does not establish a complete capture-session eligibility
policy based on these statuses.

## 6. Capture Eligibility Decisions — PENDING

The M4 owner and team leader must confirm:

1. Which statuses permit a new capture session?
2. Should eligibility be checked before issuing a session?
3. What happens if a submission changes status during capture?
4. What happens to previously issued sessions after rejection,
   approval, or suspicious-activity hold?
5. How should these checks interact with the mandatory
   360-degree-video-before-photo workflow?
6. Which component is responsible for status enforcement?

**Decision status: PENDING**

No allowed-status matrix is approved by this document.

## 7. Persistence Integration

### Current Evidence

The InMemoryHunterCaptureSubmissionAccessReader provides
backend-controlled snapshots for prototype and test usage.

It does not provide production database persistence.

### Pending Integration

- Production submission access reader.
- Final ownership data storage strategy.
- Approved M1-to-M4 identity relationship.
- Persistent capture-session implementation.
- Database relationship and index review.
- Transaction and concurrency requirements.
- EF mapping and migration authorization.

**Persistence readiness: PENDING**

## 8. Security Requirements

The production integration must:

- Obtain authenticated account identity from trusted backend
  authentication, never directly from Flutter input.
- Deny access to submissions owned by another user.
- Deny access to nonexistent submissions.
- Reject missing or invalid identity mappings.
- Prevent session creation when authorization fails.
- Preserve cancellation handling.
- Enforce the approved submission eligibility policy.
- Avoid exposing unauthorized submission details.
- Use a trusted persistence implementation.

These are implementation and verification requirements;
not a claim that all production controls currently exist.

## 9. Remaining Verification

Before production integration can be considered complete:

- Verify authenticated API endpoint behavior.
- Verify M1 identity integration.
- Verify authorized session creation against real storage.
- Verify submission status transitions.
- Verify unauthorized access rejection.
- Verify concurrency and transaction behavior.
- Complete full regression tests.
- Complete authorized database integration testing.

Current integration-style unit tests use fake identity
resolution and session storage.

## 10. Migration Status

- Shared schema approval: PENDING
- Production identity mapping: PENDING
- Submission eligibility policy: PENDING
- Production persistence review: PENDING
- Migration created for this task: NO
- Database update executed for this task: NO

**Current decision: MIGRATION HOLD**

No `dotnet ef migrations add` or
`dotnet ef database update` is authorized by this document.

## 11. Approval Checklist

- [ ] M1 owner approves the identity contract.
- [ ] M4 owner confirms submission eligibility requirements.
- [ ] Database coordinator reviews persistence relationships.
- [ ] Team leader explicitly approves integration decisions.
- [ ] Shared schema freeze is confirmed.
- [ ] Migration authorization is explicitly granted.

Unchecked items must not be treated as approved.

## 12. Final Readiness Status

**M4 CAPTURE INTEGRATION: PENDING REVIEW**

Completed:

- Application-level capture session foundation.
- Submission ownership validation.
- Authorized handler orchestration.
- In-memory submission reader prototype.
- Integration-style authorization tests.

Pending:

- Production authentication wiring.
- Approved identity mapping.
- Submission status eligibility enforcement.
- Production persistence implementation.
- Shared database schema approval.
- EF migration.

This document is evidence for review only.
