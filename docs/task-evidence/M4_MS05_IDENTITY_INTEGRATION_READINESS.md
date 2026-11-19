# M4-MS05 — Trusted Identity Integration Readiness Verification



**Module:** M4 HunterPlaces

**Related Module:** M1 IdentitySocial

**Branch:** `task/m4-ms05-identity-integration-readiness`

**Date:** 2026-11-19

**Status:** REVIEW REQUIRED — PRODUCTION INTEGRATION HOLD

**Scope:** Documentation and existing-code verification only



## 1. Purpose



Verify the readiness of M1 authenticated identity integration with M4 Hunter capture-session operations.



This task does not authorize production code changes, dependency injection registration, database schema modifications, or SQL migrations.



## 2. Reviewed Evidence



\- `docs/architecture/M4\_MS04\_TRUSTED\_IDENTITY\_MAPPING\_CONTRACT.md`

\- `docs/task-evidence/M1\_M4\_TRUSTED\_IDENTITY\_OWNER\_CONTRACT\_REVIEW.md`

\- `src/Wayhunter.Api/Program.cs` (first 180 lines)

\- `src/Wayhunter.Api/Services/HunterPlaces/TrustedHunterCaptureSessionIdentityResolver.cs`

\- Repository-wide searches for authentication, identity interfaces, handlers, and DI registrations.



## 3. Verified Findings



### F1 — Identity Mapping



The M4-MS04 contract records Team Leader approval that M1 `UserAccount.UserId` and M4 `HunterId` represent the same logical user identity.



M1 uses a GUID-formatted string, while M4 capture sessions use `Guid`.



Production integration requires additional approvals.



### F2 — M1 Authentication



`Program.cs` conditionally enables shared authentication using `Identity:EnableSharedAuthentication`.



The M1 owner review proposes JWT `sub` as the authoritative identity claim and `NameIdentifier` as a compatibility claim.



Final cross-module confirmation remains pending.



### F3 — M4 Trusted Identity Resolver



`TrustedHunterCaptureSessionIdentityResolver` implements `IHunterCaptureSessionIdentityResolver`.



The implementation:



\- Rejects missing, malformed, and empty GUID identities.

\- Requires GUID `"D"` format.

\- Loads trusted account information through `ITrustedIdentityAccountReader`.

\- Validates account ownership using `TrustedIdentityAccessContract.IsActiveAccountOwner`.

\- Returns a validated `Guid` HunterId.



Client-provided HunterId must never be trusted as authenticated identity.



### F4 — Trusted Account Reader



`ITrustedIdentityAccountReader` exists.



The inspected source searches did not identify a concrete production implementation or its DI registration.



This requires further verification.



### F5 — Dependency Injection



The inspected source searches did not identify production DI registration for `IHunterCaptureSessionIdentityResolver`.



Production registration remains unverified and must not be added without authorization.



### F6 — M4 Handlers and API Integration



The Application layer contains:



\- `HunterStartAuthorizedCaptureSessionHandler`

\- `HunterConsumeAuthorizedCaptureSessionHandler`

\- `HunterCaptureSessionIdentityResolutionService`



The M1 owner review records that these handlers reference identity resolution.



Complete API endpoint wiring, authenticated principal propagation, and HTTP error responses remain unverified.



### F7 — Account Deletion-State Consistency



The M1 owner review identifies a possible inconsistency involving `DeletionRequestedAtUtc`.



`TrustedIdentityAccessContract.IsActiveAccountOwner` reportedly does not explicitly check this timestamp, while other M1 authentication components do.



The M1 owner must confirm the required behavior.



### F8 — Database Relationship



The M4-MS04 contract records:



\- `Users.UserId`: `string` / `nvarchar(256)`.

\- `Hunter\_CaptureSessions.HunterId`: `Guid` / `uniqueidentifier`.

\- Existing `Hunter\_Submissions.UserId` to `Users.UserId` foreign key.

\- No configured direct capture-session HunterId-to-Users foreign key.



The DB Coordinator must approve the final relationship and integrity strategy.



## 4. Outstanding Approvals



\- \[ ] M1 owner confirms the final identity claim contract.

\- \[ ] M1 owner confirms account eligibility and deletion-state behavior.

\- \[ ] M1 owner confirms authentication failure behavior.

\- \[ ] M4 owner verifies API and handler wiring.

\- \[ ] M4 owner verifies authenticated principal propagation.

\- \[ ] Trusted account reader and DI strategy confirmed.

\- \[ ] DB Coordinator approves the database FK strategy.

\- \[ ] Team Leader approves the final cross-module contract.

\- \[ ] Production integration separately authorized.

\- \[ ] SQL migration separately authorized.



## 5. Verification Limitations



This report is based on inspected source snippets, repository search outputs, and existing M1/M4 contract documents.



Absence from source searches is not conclusive proof that a component is absent.



No fresh build, tests, runtime authentication verification, or live SQL verification was performed as part of this report.



## 6. Final Readiness Decision



**M4-MS05 Status: REVIEW REQUIRED**



The identity mapping principle is approved, and the proposed trusted resolver exists.



Production DI wiring, trusted account reader implementation, API integration, account lifecycle consistency, and final cross-module approvals remain outstanding.



**Production Integration: HOLD**



**Database Migration: HOLD**
