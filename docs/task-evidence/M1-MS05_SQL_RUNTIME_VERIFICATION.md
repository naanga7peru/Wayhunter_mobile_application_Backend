# M1-MS05 — IdentitySocial SQL Runtime Verification

## Environment

- Date: 2026-11-17
- Branch: task/m1-ms05-sql-runtime-verification
- SQL Server: localhost\MSSQLSERVER01
- Isolated database: Wayhunter_M1_RuntimeTest
- Authentication: Shared SQL-backed session authentication
- API: http://localhost:5055
- Runtime: .NET 10

## Migration Verification

- Migration: 20261117093023_InitialSharedSchema
- Applied using the existing idempotent SQL migration script.
- Shared schema: 41 tables.
- M1 Users and IdentitySessions tables verified.
- No new migration created.

Result: PASS

## SQL Persistence Verification

- Verified active user persisted in dbo.Users.
- Identity session persisted in dbo.IdentitySessions.
- User/session records remained available after API restart.
- Valid signed JWT reached authenticated API execution.

Result: PASS

## Authenticated Logout and Revocation

- Authenticated POST /api/mobile/auth/logout returned HTTP 204.
- SQL RevokedAtUtc was populated after logout.
- SessionState confirmed REVOKED.
- Reusing the revoked JWT returned HTTP 401.

Result: PASS

## Expired JWT Rejection

- Created a separate verified user and SQL-backed session.
- Generated a short-lived JWT with a two-minute session lifetime.
- JWT expiration observed at 2026-11-17 14:16:35 UTC.
- After expiry, POST /api/mobile/auth/logout returned HTTP 401.
- Both JWT and SQL session expiration were reached; the HTTP result does not independently isolate the two checks.

Result: PASS — end-to-end expired-session rejection

## Authentication Implementation Review

- JWT signature validation enabled.
- JWT issuer and audience validation enabled.
- JWT lifetime validation enabled.
- ClockSkew = TimeSpan.Zero.
- SQL-backed account and session checked for protected requests.
- Revoked and inactive sessions rejected.

Result: PASS — implementation review

## Backend Build and Tests

Commands:

- dotnet build Wayhunter.slnx --no-restore
- dotnet test Wayhunter.slnx --no-restore
- git diff --check

Results:

- Build: PASS
- Total tests: 2650
- Passed: 2647
- Failed: 0
- Skipped: 3

Skipped tests:

- M4SqlIntegrationVerificationTests.StatusTransition_ShouldRevokeUnusedSessions
- M4SqlIntegrationVerificationTests.RevocationFailure_ShouldRollbackStatusTransition
- M4SqlIntegrationVerificationTests.ConcurrentConsume_ExactlyOne_ShouldSucceed

Reason: M4 isolated SQL test database was not configured.

## Observed Cross-Module Issue

Authenticated GET /api/mobile/coins-wallet/me reached controller activation
but returned HTTP 500 because GetMyCoinWalletService was not registered
in dependency injection.

This is an M6 CoinsWallet dependency injection issue, not an M1
authentication failure. No M6 implementation was modified.

## Scope and Safety

- Existing shared SQL migration was not modified.
- No unrelated module source files were modified.
- Temporary test harness remained outside the repository.
- No JWT signing secret or raw bearer token is included in this evidence.
- Verification used an isolated development SQL database.

## Conclusion

M1 SQL persistence, authenticated logout, persisted revocation,
revoked-token rejection, and expired-session rejection were verified.

Full backend build passed, with 2647 tests passed, 0 failed,
and 3 unrelated M4 SQL integration tests skipped.

Status: M1-MS05 runtime verification completed.
