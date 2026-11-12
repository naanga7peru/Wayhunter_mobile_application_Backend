# AGENTS.md — Wayhunter ASP.NET Core Backend (Codex team rules)

    **Repository:** https://github.com/naanga7peru/Wayhunter_mobile_application_Backend
    **Technology direction (proposed):** ASP.NET Core API / EF Core / SQL Server / Clean Architecture candidate
    **Module scope:** Shared API and authoritative transactions for mobile + Angular Admin.

    ## Required process
    1. Project owner reports **folder structures and development have not begun**. Before scaffolding, check whether a repository has existing files; GitHub source is **not code-verified** (previous connector 404).
    2. **Read `docs/second-brain/ACTIVE_RULES.md` first**, then `docs/second-brain/CONTEXT.md` and the draft Master BRD; no file proves implemented behavior.
    3. Distinguish `SOURCE`, `PROPOSED`, `CODE_VERIFIED`, `DECISION_REQUIRED`. Do not invent endpoints/tables/routes.
    4. Make a KEEP / MODIFY / ADD plan with testing before patching. Do not silently alter working UI, branch history, or schema.
    5. **VCH-DEC-01 RESOLVED:** request requires >=10,100 spendable; reserve 10,000; **Admin Approve debits 10,000 once**; reject releases. OCR/barcode *intake* optional, demo voucher QR generation/display required.
    6. Never commit API secrets, production OTP, voucher codes, raw contacts or GPS trails. Use least-privilege auth, backend validation, audit/idempotency.
    7. Never claim a feature complete just because a screenshot or previous demo repo showed it.

    ## Repo-specific rules
    - No solution/controllers/EF migrations have been verified or created by this package. Before scaffolding inspect repo contents; ERD is only proposed.
- Unique immutable UserId, OTP/session security, role/permission controls server-side, audit approvals.
- Idempotent approval events: welcome +100 once, Hunter +200 once, Game creation +100 once, verified play +100 once.
- Coins ledger/reservation with concurrency checks: require >=10,100 spendable, reserve 10,000, Admin Approve debits once; no double voucher issue. Failed issue remains Approved/Unissued for controlled retry.
- Protected voucher source image/PDF and manual verification; OCR scanner optional, no automatic trust in scanned code.
- Hunter place and Game creation undergo Admin review; Game completed-player proof separate. Exactly one verified winner. At Admin approval +30 days, Game becomes Expired/Archived in creator My Games and hidden from public/play (not physically deleted). Trip true Start/Finish, route-scoped 1km/24h hazard, group GPS consent.
- Private Cloudinary evidence/voucher codes and secure keys; no raw secrets in API responses/logs.

    ## Additional non-negotiable active rules
- Game expiry is 30 days after Admin approval; **Archive and retain creator My Games/database record**, NOT physical DB deletion.
- One verified winner per Game. Hazard warnings: 1 km from planned trip route, photo capture time, 24-hour active window.
- No new commercial payout scope; academic demo voucher uses sample image, unique code and QR.
- M1–M7 ownership in central `knowledge/TEAM_GIT_WORKFLOW.md` is planning allocation, not a product-rule change.

## Evidence requirements for every PR
    Mention BRD rule / Admin module, branch/commit, actual changed files, tests run, risks, any remaining `Decision Required`. No direct main push without review.
