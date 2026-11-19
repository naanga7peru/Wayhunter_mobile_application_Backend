# M6 Coins, Wallet and Vouchers backend completion

## Evidence

- Owner: Kaviyan (M6)
- Baseline: `origin/develop` `0d7fe5a2a953f228bbeaa5e2f2ce51c6ae3d3419` (includes reviewed `70bc204` / M3 PR #232)
- Branch: `task/m6-backend-completion`
- Worktree: `tmp/worktrees/m6-backend-completion`
- State: local review only; no commit, push, migration generation, or database update

## Completed M6 task IDs

- MS02-T03, MS02-T05
- MS03-T01–T06
- MS04-T01, MS04-T03, MS04-T06, MS04-T08
- MS05-T02, MS05-T04, MS05-T06, MS05-T08
- MS06-T01, MS06-T04, MS06-T05
- MS07-T01, MS07-T02, MS07-T05, MS07-T06, MS07-T09
- MS08-T02, MS08-T06
- MS09-T04, MS09-T05

Cross-module task completion means the M6 persistence adapters, handlers, reconciliation source and durable processing contracts are implemented. The M1/M4/M5 owning-module call sites listed below remain coordination dependencies.

## Implemented behavior

- Retry-enabled execution strategy now encloses the complete reward transaction; transaction-scoped locks precede protected reads; ledger and wallet updates commit atomically and duplicate/uncertain-commit retries do not double-credit.
- Authoritative M1 verified-account reader and event adapter; no public grant endpoint.
- Existing wallet response retained and extended with capped 2,000-Coin lifetime-earned progress; owner-only paginated history covers rewards, holds, debits, releases and corrections.
- Demo offers, eligibility, request/status, idempotent 10,000-Coin reservation with the 10,100 spendable threshold.
- Trusted-Admin/fail-closed review queue, approve/reject, owner cancellation, stock allocation and retryable issuance. Approval debits once; rejection/cancellation releases once; issuance never debits. Owner-only protected code/image/QR retrieval.
- Persisted M4/M5 approval readers, one-winner enforcement, reconciliation source, retryable outbox worker and independent durable notification/ranking/profile consumer outcomes.
- One-use/expiry lifecycle, AES-256-GCM voucher protection, exact audited compensating reversals, immutable rule-version history, operations totals and reconciliation exceptions.

## Dependencies / decision required

- M1: after the first verified OTP activation and `VerifiedAccountActivatedEvent` are committed, enqueue/call `HandleVerifiedAccountActivatedReward.HandleAsync`.
- M4: after an approved `HunterReviewDecision` commits, enqueue the M6 reward or invoke `GrantApprovedHunterCoinsHandler` post-commit.
- M5: the production SQL creation approval transaction must enqueue its returned creator reward; after approved `GameResultDecision` and `GameWinner` commit, enqueue/invoke the winner handler.
- Trusted Admin authorization remains fail-closed unless M1 supplies authenticated claims with configured `Identity:AdministratorRole=Admin`.
- M6 model changes require a separately reviewed M6-only migration after the pending M3 schema delta is isolated. See `M6_BACKEND_COMPLETION_SCHEMA_CHANGES.md`.

## Verification

- Focused M6 unit tests: 324 passed, 0 failed, 0 skipped.
- `dotnet build Wayhunter.slnx`: passed, 0 errors; NU1900 warnings only because the NuGet vulnerability endpoint was unavailable.
- `dotnet test Wayhunter.slnx`: unit 2,820 passed / 0 failed / 0 skipped; integration 91 passed / 0 failed / 59 skipped. SQL-dependent skips were caused by their existing opt-in test database configuration; no shared database was touched.
- `git diff --check`: passed.
- Migration diff: empty; `InitialSharedSchema` unchanged.

## Proposed commit / PR

Commit: `feat(m6): complete coins wallet voucher backend workflows`

PR: Complete M6 retry-safe rewards, owner wallet history/progress, demo voucher reservation/review/issuance/use, durable reward reconciliation and Admin correction/reporting APIs. Explicitly call out the M1/M4/M5 post-commit integration points and the deferred M6-only migration packaging.
