# M7-MS03-T01 Campaign domain foundation

Owner Thashvini (M7). Scope ADM-14. Source is the Second Brain M7 brief, MS03-T01 and its linked campaign eligibility and lifecycle acceptance criteria.

- Branch `task/m7-ms03-t01-campaign-domain-model`. Baseline `f35f186` contains cached `origin/develop` and the existing image-upload work. GitHub was unreachable, so compare the local branch with refreshed `origin/develop` before preparing the PR.
- VERIFIED IMPLEMENTED for the domain slice. Campaigns hold a sponsor ID, typed linked content ID, optional district, a nonnegative Coins budget, and a UTC schedule. Scheduling, publication, pause, and archive are explicit. Delivery requires publication, a matching district where targeted, a current schedule, and caller-supplied content eligibility.
- PARTIAL for the full Data task. EF mapping, physical schema, sponsor and linked-content relationship contracts, and authoritative district and eligibility lookups remain open. No acceptance or PR evidence for this task yet.
- Domain files are `src/Wayhunter.Domain/Modules/HomeAdsAnalytics/Entities/Campaign.cs` and `Enums/CampaignType.cs` and `Enums/CampaignStatus.cs` under the same module. Coverage is in `tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/CampaignDomainTests.cs`.
- Review decisions. The model accepts zero as a valid configured budget, represents targeting as one optional district label, requires a district for a district pass, and uses opaque GUID sponsor and content references. Publication may precede the start of delivery. Paused campaigns may resume before expiry. Confirm these choices and the canonical district and content identifiers before persistence or public contracts. The model does not issue Coins.

## Verification

- Release solution build passed with zero warnings and zero errors using cached restore assets.
- Full Release unit suite passed 3,146, failed 0, skipped 0.
- Focused M7 HTTP integration regression passed 71, failed 0, skipped 0 using process-scoped `Logging__EventLog__LogLevel__Default=None`. The initial run encountered Windows Event Log access denial.
- Full integration suite and live SQL and Cloudinary checks were not executed.

## Remaining backend tracking

Tasks below have no new branch, commit, or test run from this stage. Source references describe existing evidence.

| Task | Status | Evidence and next action |
| --- | --- | --- |
| M7-MS03-T02 Campaign API | BLOCKED | First merge the foundation PR under the M7 task workflow. Confirm persistence and API contracts and use trusted Admin authorization. |
| M7-MS03-T04 Budget safeguards | PENDING | Domain rejects negative budgets. Agree on caps and reward-source boundaries, then implement concurrency and duplicate-source protection. |
| M7-MS07-T01/T02/T03 Tracking | PARTIAL | Advertisement counters exist. Add event persistence, independent deduplication, rate limits and aggregate and audit references after schema and contract review. |
| M7-MS04-T01/T02/T03/T08 Dashboard | PENDING | Confirm authoritative read interfaces and eligible statuses with M1–M6 before building aggregates. |
| M7-MS05-T01/T03/T04 Reports | PENDING | Define authorized data and filters, then add safe CSV and role-filtered PDF coverage. |
| M7-MS06-T01/T03 Sponsored places | PARTIAL | AdvertisementPlacement contains SponsoredPoi. Obtain M2/M4/M5 eligibility and placement contracts before exposing sponsored results. |
| M7-MS02-T02 Advertisement SQL | PARTIAL | Shared migration and demo seed preparation exist. DB Coordinator approval and isolated SQL are needed for actual seed and restart verification. |
| M7-MS02-T03 Advertisement API and RBAC | PARTIAL | Routes, policy and URL validation exist. Trusted Admin role issuance requires M1 wiring and role storage. District persistence and delivery require an approved contract. |
| M7-MS02-T04 Secure image upload | PARTIAL | Existing upload implementation retained. Real provider, role and SQL dependencies and full Admin acceptance remain open. PR #264 merge was reported by the owner and remote confirmation was unavailable. |
| M7-MS02-T05 AdMob readiness | BLOCKED | Official Second Brain task. D16 approval/access and consent verification remain open. |
| Durable image reconciliation | PARTIAL | Existing offline policies, audit mapping and fencing drafts remain. The backend proposal also names this MS02-T05. Owner decision required for a distinct ID. Shared schema review is required for durable storage. |
| M7-MS09-T03 SQL coordination | PARTIAL | Shared migration and advertisement seed strategy are documented. DB Coordinator owns execution and acceptance. |
| M7-MS09-T05 Shared interfaces | PENDING | Review media, outbox and configuration ownership contracts before integration. |

The client, QA and Ops items remain in the Second Brain plan. Home navigation and banner checks are Flutter work, campaign/dashboard/report editors and shell are Angular work, and mobile map targeting is shared client work. Fresh-clone regression, privacy and analytics QA, cross-module UAT, and handover evidence remain unverified. CI, staging, provider monitoring, secret handling, and backup/retention changes require coordination with their responsible owners.

Next workflow action is review of MS03-T01 and verification of the branch against current develop. Begin MS03-T02 after the foundation PR merges.
