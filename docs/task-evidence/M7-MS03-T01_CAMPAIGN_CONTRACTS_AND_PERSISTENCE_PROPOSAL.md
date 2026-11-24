# M7-MS03-T01 Campaign contracts and EF Core persistence proposal

ADM-14 · Baseline `task/m7-ms03-t01-campaign-domain-pr` at `10c8812` · Review draft

**SOURCE** records a documented requirement or planning reference. **CODE_VERIFIED** records behavior inspected at this checkout. **PROPOSED** requires review. **BLOCKED** identifies a decision or contract required before implementation.

**SOURCE** The Second Brain M7 milestone tracker and member brief assign Campaign type, sponsor, audience and budget schema to M7-MS03-T01, traced to original tracker task M7-MS05-T01. Acceptance calls for sponsored game, place, mission and district-pass references. ADM-14 in the draft Master BRD describes campaign configuration, preview, schedule, publication and monitoring, with no bypass of Hunter/Game approval or duplicate Coins payout. The Active Rules govern Game expiry, review and Coins. The repository's [M7 scope](../members/M7_SCOPE.md) and [domain foundation evidence](M7-MS03-T01_CAMPAIGN_DOMAIN_FOUNDATION.md) describe ownership and the partial delivery. Tracker status and prior test evidence provide planning and historical context.

## 1. Existing Campaign domain

**CODE_VERIFIED** [Campaign](../../src/Wayhunter.Domain/Modules/HomeAdsAnalytics/Entities/Campaign.cs) stores immutable `Id`, `Type`, `SponsorId`, `ContentId`, optional `TargetDistrict`, `BudgetCoins`, `StartAtUtc` and `EndAtUtc`. Only `Status` changes through domain operations. Construction requires nonempty GUIDs, a defined type, a nonnegative `long` budget and an ordered window. It rejects blank district labels, trims valid labels and normalizes timestamps to UTC. It starts in Draft. The [domain tests](../../tests/Wayhunter.UnitTests/Modules/HomeAdsAnalytics/CampaignDomainTests.cs) cover configuration, time boundaries, district matching, eligibility and lifecycle transitions. The shared EF context has Advertisement mapping. Campaign remains a domain slice.

## 2. CampaignType and sponsored-content references

**CODE_VERIFIED** The type and required opaque `ContentId` form a logical typed reference. The enum values below are domain values. Their use as persisted or wire values is **PROPOSED**.

| Domain type | BLOCKED reference decision |
| --- | --- |
| `SponsoredGame = 1` | M5 must confirm the canonical Game key and authoritative eligibility contract. |
| `SponsoredPlace = 2` | M4 and M2 must identify the approved-place key and how Traveler resolves it. A Hunter submission key and a provider POI key require explicit agreement before either can be used. |
| `SponsoredMission = 3` | Confirm the mission owner, key, verification and visibility contract. |
| `DistrictPass = 4` | Confirm the pass owner, what the required content GUID identifies and the applicable eligibility rules. The district target is a separate field. |

**PROPOSED** Resolve references by type and key in the owning module. Accept only types with an approved resolver and fail closed for unknown, missing or mismatched references. Review Draft-save behavior for unresolved references before exposing write contracts.

## 3. Sponsor ownership and authorization

**CODE_VERIFIED** `SponsorId` is validated only as a nonempty GUID. The Advertisement administrator policy uses an authenticated principal and trusted Admin role checks.

**PROPOSED** Separate the sponsoring principal or organization from the authenticated operator. Start with Admin-mediated campaign management using a Campaign permission contract reviewed with M1. Validate the requested sponsor and its status using server-owned data. Authorize every protected read and mutation, recording the trusted actor, campaign, sponsor, previous and resulting status, and UTC timestamp for lifecycle actions. Apply resource-level authorization to any later delegated sponsor access. Record mutation and corresponding audit atomically when stored together.

**BLOCKED** Confirm sponsor authority, key, account or organization ownership, delegation, reassignment, deactivation and access rules. The Ads role contract is evidence for a neighboring module and requires an explicit Campaign authorization decision.

## 4. Audience and district targeting

**CODE_VERIFIED** A null district permits any district at the domain gate. A targeted campaign requires an ordinal case-insensitive match after trimming the supplied district. DistrictPass requires a target. The Ads application has a district-label validator. That validator does not establish a canonical Campaign key or viewer district.

**PROPOSED** Retain one optional district for the initial contract. A targeted placement with missing or unknown context is unavailable. Resolve district from an approved source appropriate to the surface and apply matching server-side before delivery. Use sponsorship labels on sponsored cards. Treat audience segmentation beyond district as a separate decision.

**BLOCKED** M2 and M7 must agree on the canonical district key, label normalization and aliases, and which context drives matching on each surface, such as a selected district or route context. Confirm privacy and consent constraints before using location inputs.

## 5. BudgetCoins and spending semantics

**CODE_VERIFIED** Zero is valid configuration. The domain availability check does not inspect budget or spend, and the aggregate has no spend or Coins transaction operation.

**SOURCE** T04 requires duplicate reward-source prevention, and T06 expects over-budget and disabled content to stay inactive. Existing module reward rules and their approval triggers still apply.

**PROPOSED** Treat `BudgetCoins` as a configured whole-Coins cap. Attribution alone creates no wallet transaction. For an approved chargeable action, use M6-reviewed atomic cap enforcement and idempotency keyed to the authoritative reward source. Replay should return the prior outcome without spending or crediting twice, including when multiple placements see the same source event. Keep spend observable from accepted operations. Once a charge policy is approved, suppress paid delivery when the next required charge would exceed the remaining budget, and enforce the cap again at write time.

**BLOCKED** M6 and M7 must decide the funding source, chargeable actions and timing, cap bounds, reservation or debit behavior, and handling of zero, exhausted budgets, refunds and edits after spend. Paid delivery depends on these decisions and T04 safeguards.

## 6. UTC schedule and lifecycle

**CODE_VERIFIED** Draft can schedule only before start. Draft, Scheduled or Paused can publish with eligible content before end, including ahead of start. Only Published can pause. Archive assigns Archived from any state, including Archived. Archived cannot schedule or publish. Availability requires Published, eligible content, district match and `StartAtUtc <= now < EndAtUtc`. Passing the end makes delivery unavailable and does not change the stored status.

**PROPOSED** Use a trusted server clock, persist UTC and distinguish configured status from effective availability in admin views. Preserve archived history. Handle competing updates with optimistic concurrency and re-evaluate a rejected stale mutation against current state. Keep scheduling an explicit state transition.

**BLOCKED** Confirm edit rules after scheduling or publication, automatic publication or archive policy, and any scheduled jobs and audit requirements. Game expiry remains governed independently by the Active Rules.

## 7. Content eligibility dependencies

**SOURCE** Campaign promotion cannot bypass manual Hunter or Game creation review. A Game expires 30 days after Admin approval, leaves public discovery and play, and remains in creator history and storage. Game winning-result approval and reward rules remain separate.

**CODE_VERIFIED** The aggregate consumes a caller-supplied eligibility boolean. It performs no owner lookup.

**PROPOSED** Compute eligibility from owner-authoritative state at publication and delivery. A missing, disabled, expired, unapproved or unverifiable record suppresses promotion where applicable. Use the owner contract for moderation and visibility, return only public metadata, and define a freshness or invalidation policy before caching eligibility. Public reads should filter by both campaign availability and owner eligibility.

**BLOCKED** Agree per-type lookups, statuses, invalidation, failure behavior and public projection with the owners in section 12.

## 8. Proposed EF Core entity mapping

**PROPOSED** Add a dedicated Campaign mapping to `WayhunterSharedDbContext` after approval, following the explicit configuration pattern in [AdvertisementConfiguration](../../src/Wayhunter.Infrastructure/Persistence/Configurations/HomeAdsAnalytics/AdvertisementConfiguration.cs). Table and schema names require Coordinator review.

| Domain values | PROPOSED SQL Server mapping |
| --- | --- |
| `Id` | Required application-generated `uniqueidentifier` primary key, with value generation disabled. |
| `SponsorId`, `ContentId` | Required `uniqueidentifier` if owner contracts confirm GUID-compatible references. |
| `Type`, `Status` | Required integer conversions using reviewed enum values. Current status values are Draft 0, Scheduled 1, Published 2, Paused 3 and Archived 4. |
| District targeting | Nullable canonical district value. Storage type, length and collation follow the approved district contract. |
| `BudgetCoins` | Required `bigint`. |
| Window | Required `datetimeoffset(7)` values with UTC normalization. |
| Concurrency | SQL Server `rowversion` concurrency token for administrative writes. |

**PROPOSED** Verify full rehydration, including immutable values and persisted status. The public constructor time parameters differ from the UTC property names and immutable getters need explicit mapping. Review either a persistence-specific materialization path or a separate persistence row with explicit mapping, preserving validation for new campaigns. Round-trip all statuses and confirm stale updates fail before approving a migration.

## 9. Relationships, indexes and SQL constraints

**PROPOSED** Use restrictive delete behavior for approved relationships so archived campaigns and audit records retain stable references. Add a sponsor FK only once its authoritative principal is confirmed. Add a district FK only if the approved district contract has a relational principal. A single `ContentId` cannot enforce a foreign key across multiple tables. Start the relationship review with a typed logical reference and owner validation. If database-enforced integrity is required, review per-type relational references after the principal keys and same-database boundaries are confirmed.

**PROPOSED** Review nonunique indexes for sponsor administration (`SponsorId, Status`), content lookup (`Type, ContentId`) and delivery candidates (`Status, canonical district, StartAtUtc, EndAtUtc`). Check actual query plans for targeted and untargeted delivery before choosing the final order or filtered indexes. Confirm cardinality before imposing any uniqueness rule on sponsor, content, district or overlapping schedules.

**PROPOSED** Review SQL CHECK constraints for ordered windows, zero UTC offsets, nonnegative budget, supported enum values, nonempty GUIDs and a non-null district for DistrictPass. If a label is used, reject whitespace-only values consistently with application normalization. Enforce current eligibility, transitions, ownership and spending rules in the appropriate application or transactional boundary.

## 10. Relationship to existing Advertisements

**CODE_VERIFIED** Advertisement has its own placement, status, enabled flag, schedule, creative fields and counters. The checked-in Advertisement mapping has no Campaign FK. SponsoredPoi is an Advertisement placement value.

**PROPOSED** Preserve standalone advertisements. If Campaign-driven creatives are approved, evaluate an optional one-to-many link from Campaign to Advertisement. Require both records to be deliverable for a linked creative, and keep Campaign attribution explicit. Confirm required behavior for existing rows before any schema change or backfill.

**BLOCKED** M7 and the Coordinator must decide cardinality, placement and schedule precedence, unlinking and retention, and whether attribution needs a durable link for reporting.

## 11. Database Coordinator approval

**SOURCE** [M7 scope](../members/M7_SCOPE.md) and [Ads migration readiness](../api/M7_MS02_T02_ADS_SCHEMA_MIGRATION_READINESS.md) call for centralized SQL review. The existing Ads readiness document asks for explicit schema approval from all seven module owners before the Coordinator prepares a shared migration.

**BLOCKED** Record Team Leader and Database Coordinator review and explicit module approvals for the physical schema. Review principal keys, types, FK and delete behavior, query indexes, checks, concurrency, shared model snapshot and migration order. Agree on compatibility, backfill if relevant, rollback and isolated SQL verification. The Coordinator owns migration sequencing and authorized execution. This proposal supplies no approval evidence.

## 12. Owner dependency and approval matrix

| Owner | BLOCKED contract or approval |
| --- | --- |
| M2 · Traveler | Approve district source and route/map placement context, canonical key consumed by Traveler, and sponsored-card disclosure and filtering. Agree on the approved-place bridge with M4. |
| M4 · Hunter Places | Approve the canonical approved-place reference, manual review and visibility gates, and revocation or invalidation behavior. |
| M5 · Games | Approve the canonical Game reference and authoritative public/playable eligibility and expiry checks. Identify the existing reward-source contract with M6. |
| M1 · Identity | Approve trusted actor identity, permissions and any sponsor membership or delegation integration. |
| M6 · Coins | Approve funding, spend cap, accounting, deduplication and concurrency semantics for any chargeable action. |
| M7 · Campaigns | Resolve sponsor and audience decisions, lifecycle and Ads linkage. Identify mission and district-pass owners and secure their contracts. |
| Team Leader, Database Coordinator and module owners | Confirm shared schema approval and migration ownership, including cross-module impact. |

## 13. Open decisions and implementation sequence

**BLOCKED** Physical schema and API work depend on authoritative identifiers, owner eligibility contracts and approval gates listed above. Budget enforcement also depends on the M6 contract. The domain evidence calls for the foundation PR to merge before T02.

**PROPOSED** First review this document with M7 and resolve the sponsor, audience and per-type references with the source owners. Then agree on budget and lifecycle policy and obtain Coordinator review of the mapping and any shared migration. Verify persistence round trips, constraints, concurrency and compatibility in approved isolated infrastructure. After the foundation merge and persistence contract acceptance, implement T02 against trusted authorization and owner lookups. Gate paid delivery on T04 and carry expiry, ineligibility and budget scenarios into T06.

## 14. Acceptance checklist

- [x] Record domain behavior, evidence baseline and tracker acceptance for review.
- [ ] **BLOCKED** Approve all four typed references, sponsor authorization, district authority and eligibility behavior.
- [ ] **BLOCKED** Approve budget semantics and test zero, exhausted and over-budget cases, concurrent attempts and duplicate source replay.
- [ ] **PROPOSED** Verify UTC round-trip and exact start/end boundaries, pause/resume, archive, stale writes and owner eligibility changes.
- [ ] **PROPOSED** Verify SQL constraints, relationship behavior, query plans and existing standalone Ads compatibility in approved isolated infrastructure.
- [ ] **BLOCKED** Record module approvals and Coordinator acceptance before migration work. Record implementation and acceptance evidence before closing T01.

Proposal verification is limited to source inspection and Git whitespace and change-scope checks. The foundation evidence contains historical test results.
