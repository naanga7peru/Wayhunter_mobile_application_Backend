# M6 backend completion — schema change manifest

Classification: **PROPOSED / DECISION_REQUIRED for migration packaging**. The code model is implemented, but no shared migration was generated or applied because `origin/develop` contains pending M3 model changes and the applied `InitialSharedSchema` must remain unchanged.

Baseline: `origin/develop` at `0d7fe5a` (contains reviewed `70bc204`). Branch: `task/m6-backend-completion`.

M6-owned additions required by this branch:

- `SampleVoucherRedemptionRequests`: owner-name snapshot, decision actor and decision reason.
- `SampleVoucherInventory`: protected code reference, code hash, protected image reference, expiry, stock assignment, creator/time audit and row version.
- `SampleVoucherIssuances`: nullable inventory for durable failed issuance, expiry/use audit, attempt/error fields and row version; statuses include Used and Expired.
- `CoinLedgerEntries`: non-reward source uniqueness, one correction per original entry, correction actor/reason.
- `CoinRuleVersions`: effective range and immutable creation actor/time.
- Existing M6 draft tables/configurations for wallets, reservations, offers, reward outbox and consumer-delivery records are now used by runtime services. Consumer delivery references the durable reward outbox event and publication is accepted only from a separately confirmed ledger event.

Migration packaging must be performed after M3's pending schema delta is isolated. Generate an M6-only reviewed migration from a clean agreed baseline; do not update `Wayhunter_Dev` or any shared database as part of this task.

Cross-module integration points still owned elsewhere:

- M1: after the first verified OTP activation and `VerifiedAccountActivatedEvent` are committed, enqueue/call `HandleVerifiedAccountActivatedReward.HandleAsync`.
- M4: after a committed approved `HunterReviewDecision`, enqueue the M6 reward or invoke `GrantApprovedHunterCoinsHandler` post-commit.
- M5: the persisted creation approval contract is available; the production SQL approval store must atomically enqueue its returned creator reward. After a committed approved `GameResultDecision`/`GameWinner`, enqueue or invoke the M6 winner handler.
