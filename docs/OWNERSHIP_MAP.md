# WAYHUNTER — 7-Member Full-Stack Owners

Planning ownership; **not a claim of implementation**. Read `AGENTS.md` and `docs/second-brain/ACTIVE_RULES.md` first. Official source remains the Obsidian Second Brain vault.

| Member | End-to-end module | Backend module | Angular admin modules |
|---|---|---|---|
| M1 | Identity, Profile, Friends & Settings | `IdentitySocial` | ADM-01, 03, 17 |
| M2 | Traveler Planning, Maps & AI | `TravelPlanning` | ADM-07 |
| M3 | Live Trip, Safety & Notifications | `TripSafety` | ADM-08, 09, 16 |
| M4 | Hunter, Evidence & Places | `HunterPlaces` | ADM-04, 06 |
| M5 | Games, Anti-Cheat & Leaderboards | `Games` | ADM-10 |
| M6 | Coins, Wallet & Vouchers | `CoinsWallet` | ADM-11, 12, 13 |
| M7 | Home, Advertisements & Analytics | `HomeAdsAnalytics` | ADM-02, 05, 14, 15 |

## Cross-member contracts

- M1 owns auth, identity, UserId and role permissions.
- M2 owns trip planning; M3 owns active-trip sessions, safety, consent and hazard.
- M4/M5 produce validated approval or winning events. **M6** alone owns final Coins ledger mutations and redemption holds/debits.
- M3 provides notification delivery, including failures/retries, to other modules.
- M7 owns Home shell, sponsored content and analytics views (no invented KPIs).
- M5 owns common leaderboard UI, but ranking data has source-specific owners.
- Cross-module API DTOs and events need both affected members' review.

## Repo policy (for tomorrow's future review)

Today this is folders/docs only. Do not treat placeholders as completed implementation. No commit/push is performed by this pack. The 70 milestones / 523 task Excel remains in the Obsidian Second Brain.
