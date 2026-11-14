
# WAYHUNTER — M1-MS01-T01

## Authentication and Home Flow Requirements

**Owner:** M1 — Shayanthan
**Module:** IdentitySocial
**Task:** M1-MS01-T01
**Status:** Draft — Pending Review

## 1. Registration Flow

1. User opens Register.
2. User enters name, email or phone, password and confirmation.
3. Backend validates the fields and account uniqueness.
4. User completes required OTP verification.
5. Account verification state is updated.
6. User continues to Login or an authenticated session, according to the approved authentication policy.

## 2. Login Flow

1. User opens Login.
2. User provides valid credentials.
3. Backend validates account status and credentials.
4. Unverified or suspended accounts cannot bypass required restrictions.
5. On successful authentication, user enters Home.

## 3. Forgot Password Flow

1. User selects Forgot Password.
2. User requests an approved recovery method.
3. Backend validates a time-limited recovery token or OTP.
4. User sets a new password.
5. User returns to Login.

## 4. Home Navigation

After successful authentication:

- Home
- Selected Module: Traveler / Hunter / Game
- Leaderboard for selected module
- Profile

M7 owns the Home shell and shared navigation.
M1 owns authentication state and access control.

## 5. Profile and Logout

1. User opens Profile.
2. User may navigate to Profile Settings or Friends.
3. User chooses Logout.
4. Backend invalidates the applicable session.
5. User returns to Login.

## 6. Backend Security Requirements

- Passwords must never be saved in plaintext.
- OTP expiry, attempts and resend cooldown require server validation.
- Authentication and authorization must be enforced server-side.
- Private user data must not be exposed in public responses.
- Device-binding requirements must follow approved onboarding rules.
- Sensitive values must not appear in application logs.

## 7. Cross-Module Dependencies

- M2–M7 require a stable UserId contract.
- M6 requires a unique verified-account eligibility event.
- M7 requires the authenticated navigation state.
- Shared contracts require affected-owner review.

## 8. Decisions Requiring Confirmation

- Post-registration OTP success: Login or automatic authenticated Home?
- Exact OTP expiry and resend configuration.
- Recovery channel behavior for email versus phone.
- Session expiry and renewal policy.

These are not claimed as approved implementation details.

## 9. Acceptance Checklist

- [x] Registration flow documented
- [x] OTP verification flow documented
- [x] Login flow documented
- [x] Password recovery flow documented
- [x] Logout flow documented
- [x] Home navigation requirements documented
- [ ] Peer review completed
- [ ] Team approval recorded

## Implementation Status

Documentation only. No live APIs, database migrations or Flutter connections implemented by this task.
