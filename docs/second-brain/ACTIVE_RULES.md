# WAYHUNTER — CURRENT OWNER RULES (2026-11-11; archive clarification)

**READ FIRST.** These are the current *documented product decisions* for the academic final project, not proof of implementation. The five historical PDFs in `../sources/` remain untouched. Where a PDF or previous note conflicts, use this current decision notice only for the specifically listed rule. Do not invent additional business decisions.

## Game (M5; M6 owns shared Coins service)
- Exactly **one verified winner per community game**. Eligible winning result requires Admin verification; award the winner **+100 Coins exactly once**. Game creator cannot win their own game.
- A game becomes public after **Admin creation approval**. Set `ExpiresAtUtc = ApprovedAtUtc + 30 * 24 hours`; Flutter displays the remaining time from the backend expiry timestamp.
- **Owner clarification on 2026-11-11: ARCHIVE, DO NOT PHYSICALLY DELETE.** At 30-day expiry remove the game from **public/active discovery and playable lists**, mark it **Expired/Archived**, and retain its database record and creator-owned **My Games** history. This supersedes the earlier same-day instruction to physically delete Game data. Never re-expose an expired game to players.
- An expired game cannot accept new starts, resumed play, completion or winning approval. An already running attempt cannot produce a valid completion after expiry. Keep prior awarded Coins ledger and Admin audit records intact. The expiry process must be repeatable without duplicate changes.
- Game creation approval: **+100 Coins** to creator once; player verified win: **+100 Coins** once. Both have separate Admin review processes; no Coins for pending/rejected results.

## Route hazard alerts (M2 & M3)
- A hazard alert is shown **only for an eligible trip whose planned route passes within 1 km of the hazard**, including route planning and active trip views; no broadcast outside matching routes.
- Show the hazard **photo capture timestamp** to the affected traveler. Also retain `ReportedAtUtc` as trusted server timestamp for 24-hour expiry; validate capture time rather than treating user-editable camera metadata as a server clock.
- Active hazard warning expires **24 hours after the stored report timestamp** and disappears from active views/alerts. Retain safety status/verification labels and do not invent official verification. Historic/audit records need not be physically deleted.
- Alert repeat cooldown is still a technical configuration, not a newly approved number.

## Coins and academic demo voucher (M6)
- Only the **Coins** currency; no HP/RC/XP. Welcome +100 once per eligible verified account; approved Hunter +200 once; approved Game Creation +100 once; verified single winner +100 once. Lifetime-earned increments count toward proposed **2,000-Coin level progress**.
- For the **10,000-Coin / LKR 100 sample voucher**, user needs **at least 10,100 spendable Coins before request**, leaving at least 100 spendable Coins after redemption. Use sample voucher image + unique code + QR; no real cash transfer or partner settlement for this academic demonstration.
- Request: **reserve 10,000** without posting a permanent debit; reject/cancel: release the reservation exactly once. **Admin APPROVE: finalize the 10,000-Coin debit exactly once and consume the reservation.** Issue the demo voucher afterward; never debit again on issue/fulfill.
- If issuance fails after approval, do **not** issue a second debit or fake an issued voucher. Preserve approved debit and unissued state, allow controlled idempotent issue/retry and document the error. Automatic refunds are **not** an approved business policy.
- Voucher QR **generation/display** is part of the academic demo output. Image/PDF upload and manual stock verification are documented; **OCR / scanner / barcode extraction for intake is OPTIONAL**, not a required feature.

## Flutter bottom navigation
1. **Home**
2. **Selected module**: Traveler / Hunter / Game (dynamic second tab)
3. **Leaderboard**: currently selected module's Game/Hunter/Traveler ranking
4. **Profile**
If no module is selected, tab 2 may open module selection as a documented implementation fallback. Rankings remain module-specific. Older instructions to hide the global Leaderboard button are superseded **only in bottom-navigation behavior**.

## Source / integration discipline
- These Rules are **approved requirement context**, not implemented features. The team has **not yet created project folder structures or begun development** per owner confirmation; three target repos have not been code-verified through GitHub.
- Backend's `/api/mobile/v1/...` is **only a target namespace proposal**, never a verified live route. Inspect source/OpenAPI after development before claiming DTO/API compatibility.
- 7-person full-stack ownership is a *team work allocation*, not a new business rule. See `TEAM_GIT_WORKFLOW.md`.
- Codex must read this file (or a byte-identical repo-local `docs/second-brain/ACTIVE_RULES.md`) **before** older architecture, PDF and sample instructions.
- Preserve all other previously agreed business logic. New ambiguities should be recorded as `DECISION_REQUIRED`, not silently decided.
