# WAYHUNTER — M1-MS01-T02

## Demo Login vs Real Login — Requirements Comparison

**Owner:** Shayanthan (M1)
**Module:** IdentitySocial
**Task ID:** M1-MS01-T02
**Status:** Draft — Pending Verification and Peer Review

### 1. Purpose

Document the difference between the existing Wayhunter demonstration login behavior and the required authenticated login system.

The demo implementation must be verified against the original Run Guide and source code before its behavior is marked as confirmed.

### 2. Demo vs Real Login

| Feature | Demo / Prototype | Required Backend Login |
|---|---|---|
| User credentials | To verify from prototype | Server validates credentials |
| Registration | To verify | Name, email/phone, password, confirmation |
| Password storage | To verify | Secure password hashing |
| OTP verification | To verify | Server-side expiry, attempts and resend rules |
| Account status | To verify | Unverified/suspended restrictions |
| Session handling | To verify | Secure session/token lifecycle |
| Device binding | To verify | Follow approved device-binding policy |
| Logout | To verify | Applicable session revoked |
| Error handling | To verify | Safe validation and authentication errors |
| Database | To verify | Persistent user and authentication records |

### 3. Required Real Login Flow

1. User enters their login credentials.
2. Backend validates the credentials securely.
3. Backend checks the user's verification and account status.
4. Backend applies the approved device/session security requirements.
5. An authenticated session is established only after successful checks.
6. Flutter navigates to Home.
7. Logout invalidates the applicable session.

### 4. Security Requirements

- Never store plaintext passwords.
- Do not expose OTP codes, passwords or secrets in logs.
- Enforce authentication and authorization on the backend.
- Prevent unverified or suspended accounts from bypassing restrictions.
- Protect private user information.
- Record security-relevant actions according to approved audit requirements.

### 5. Development Plan

**Phase 1 — Backend**

Create authentication entities, DTO contracts, interfaces, services, controllers and tests.

**Phase 2 — Flutter**

Create authentication screens, ViewModels and mock repositories.

**Phase 3 — API Integration**

Connect Flutter authentication repositories to verified Backend endpoints.

**Phase 4 — Angular Admin**

Implement Admin Authentication and Security after mobile integration.

### 6. Evidence Required

- Exact Run Guide section: Pending verification
- Demo login source files: Pending inspection
- Observed prototype login behavior: Pending verification
- Backend requirement references: M1 milestone brief and approved authentication rules

Do not mark prototype features as implemented without source-code or test evidence.

### 7. Acceptance Checklist

- [x] Real login requirements documented
- [x] Demo comparison fields identified
- [x] Security and integration differences recorded
- [ ] Existing demo behavior verified from Run Guide/source
- [ ] Comparison reviewed by peer reviewer
- [ ] Pull Request approved and merged

### 8. Implementation Status

Documentation draft only. This task does not implement the authentication API, database persistence or Flutter integration.
