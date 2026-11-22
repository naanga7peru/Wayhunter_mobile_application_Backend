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

DECISION_REQUIRED before activation. Approve explicit reuse consent and revocation, attribution storage, durable request identity and a disposable SQL environment. Verify source immutability and denied reuse against that environment before enabling copy writes.
