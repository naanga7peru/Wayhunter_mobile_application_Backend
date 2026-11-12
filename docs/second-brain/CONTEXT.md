# ASP.NET Core Backend — Second Brain Context

Repository: `https://github.com/naanga7peru/Wayhunter_mobile_application_Backend`
Intended responsibility: Shared API and authoritative transactions for mobile + Angular Admin.

Canonical documents in the separate Wayhunter Second Brain hub:
- Master BRD v2.1 (draft)
- 17 Admin Working Process v1.1
- Flutter Frontend Architecture v2.1
- Angular Admin Architecture v3.1
- ERD v1.1 (proposed; not migrated)

Current status: owner confirms **no folder scaffolding and no development started**. GitHub source remains unverified (previous 404). Inspect contents before planning new file paths/endpoints; do not assume existing code.

Required shared invariants:
- Coins only; welcome +100 once, Hunter +200 approved, Game create +100 approved, Game completion +100 verified/admin approved.
- Every new Hunter and Game creation must receive Manual Admin approval. Player game completion separately reviewed.
- Trip leaderboard only valid Start/Finish. No unconsented live GPS. Hazard alerts properly labeled and scoped.
- Redeem requires >=10,100 spendable; Pending reserves 10,000; **Admin Approve debits 10,000 once**; rejection releases. Sample voucher code/QR exists only after issue.
- OCR/QR/Barcode suggested scan is not a confirmed production feature.
- Do not infer implementation from architecture PDFs; backend performs irreversible operations.

READ `ACTIVE_RULES.md` (copied in this same repo overlay) **first**. For full detail consult central hub `knowledge/PROJECT_RULES.md`, `DECISIONS_PENDING.md`, `WORKFLOWS.md`, `STATUS_DICTIONARY.md` and `TEAM_GIT_WORKFLOW.md`. Source PDFs are historical/draft where superseded.

- Game: one verified winner; Admin approval + 30 days then public hidden / **Archived** in creator My Games and DB; never physically delete merely on expiry.
- Hazard: relevant planned route within 1 km, capture timestamp, 24-hour validity. Four bottom tabs: Home / selected module / Leaderboard / Profile.
