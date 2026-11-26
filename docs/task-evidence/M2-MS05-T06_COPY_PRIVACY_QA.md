# M2-MS05-T06 Suggested plan copy privacy QA

Acceptance requires the source plan to remain unchanged after editing its copy and private plan access to be denied. [Official M2 brief](https://github.com/naanga7peru/Wayhunter_SecondBrain_Docs/blob/main/Wayhunter_Second_Brain_v1_0/planning/member_briefs/M2_FULLSTACK_MILESTONES_TASKS.md#L147)

| Case | Existing evidence | Gap and action |
| --- | --- | --- |
| Independent draft and attribution | `DomainCopy_MakesIndependentDraft_WithNewIdsAndSourceAttribution` checks new plan and stop IDs, ownership, order and provenance. | TEST_VERIFIED with existing coverage. |
| Source immutability after copy edits | `EditingCopy_NeverMutatesSource` checked title, budget and stop count. | TEST_VERIFIED after extending it to dates, stop add/remove/reorder, exact source stop identities, order, fields and retained provenance. |
| Private detail and existence privacy | `SuggestedDetail_UsesTrustedViewerAndCurrentEligibility` compares revoked and missing responses, ignoring per-request trace IDs. Returned cards omit owner ID and stops. | TEST_VERIFIED with a stubbed eligibility reader. SQL eligibility remains NOT_EXECUTED. |
| Copy endpoint access | Invalid service input and repeated runtime copy denial already had coverage. | TEST_VERIFIED with `SuggestedCopy_RejectsMissingOrMalformedTrustedIdentity`. Anonymous and malformed identities receive 401. Valid authenticated requests to the disabled copy path receive 404 and create no draft. |
| Copy response projection and invalid store results | Existing tests check source owner identity stays server-side and wrong-owner results are rejected. | TEST_VERIFIED with `StoreReturningCopyOfDifferentSource_IsRejected` for mismatched provenance. |
| Memories, media, GPS and consent | CODE_VERIFIED. The candidate copy store reads planned stops and checks membership and completion. The draft and copy response do not carry private M3 records. | Physical SQL behavior and activated copy policy are NOT_EXECUTED. |

The copy endpoint is `POST /api/mobile/trip-plans/suggested/{sourceTripPlanId}/copy`. Runtime DI uses the unavailable store, and the legacy flag does not activate writes. The candidate SQL store remains unregistered.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build Wayhunter.slnx --no-restore` | Passed, 0 errors and 2 existing xUnit2031 warnings |
| Unit tests filtered by `FullyQualifiedName~CopySuggestedTripPlanServiceTests` | 9 passed, 0 failed, 0 skipped |
| Unit tests filtered by `FullyQualifiedName~TravelPlanning` | 470 passed, 0 failed, 0 skipped |
| Integration tests filtered by `FullyQualifiedName~TravelPlanning` | 40 passed, 0 failed, 0 skipped |

The integration suite used test doubles and loopback HTTP with shared SQL and live providers disabled. Domain independence and fake-store tests establish offline behavior. SQL transaction integrity, concurrent revocation and retries, Flutter, and device acceptance are NOT_EXECUTED.

The sections above record the pre-approval baseline. See [the implemented contract](M2-MS05-T03_COPY_PLAN_PRIVACY_SCHEMA.md) for the supplied approval and current behavior. Shared SQL and production activation remain outside the isolated LocalDB approval.

## Final isolated verification

The functional run verified the runtime SQL factory against `(localdb)\WayhunterTest2026`, verified server LocalDB status and the intended empty catalog `Wayhunter_M2_Test_135b1dedb7094d42a652a38883ff5b97`, and applied all nine migrations. The M2 scenario passed all 18 original operations and the new consent route. The M1 social and authentication scenarios passed on separate fresh catalogs. Three focused copy fail-closed cases passed in a separate test process with the opt-in SQL switches unset. The API build reported zero errors and warnings. The focused test run passed 975 M2, identity, and migration-readiness unit tests.

| Scenario | Expected HTTP | Actual HTTP | SQL or response evidence |
| --- | --- | --- | --- |
| Copy or consent with missing or invalid bearer authentication | 401 | 401 | Real session authentication ran before either action. |
| Missing, invalid, or repeated key header, empty source ID, or invalid consent body | 400 | 400 | No copy was created. |
| Unknown source, nonmember, removed member, incomplete source, or missing/revoked consent | 404 | 404 | Private source access stayed restricted. Previously owned drafts remained readable after revocation. |
| Source owner grants or revokes reuse | 204 | 204 | The owner-scoped SQL value governed copying. Another user received 404. |
| Eligible copy and identical retry, including concurrent requests | 201 | 201 | One draft and attribution per key, with a stable ID, independent ownership, fresh stop IDs and preserved itinerary order and coordinates. |
| Same traveler and key, different source | 409 | 409 | Existing draft was preserved. |
| Injected failure before commit | 500 | 500 | Trip, stop and attribution counts were unchanged. A retry could succeed. |
| Injected lost acknowledgement after commit | 201 | 201 | The committed identity resolved to the same draft and subsequent retry created no duplicate. |

SQL confirmed the source row version and stop IDs stayed unchanged during copying. The copied draft had no source membership, invitation, session, or private memory rows. Swagger exposed the key header and retry description. OTP and reset delivery were captured in memory, application workers were disabled, and Gemini, Maps, and weather were simulated. Live provider verification and deployment were outside this run.
