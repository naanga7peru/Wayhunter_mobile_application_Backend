# M2-MS07-T07 AI output and budget edit QA

Acceptance requires safe rejection of invalid AI schemas and a reviewed proposal entering the normal trip draft flow. [Official M2 brief](https://github.com/naanga7peru/Wayhunter_SecondBrain_Docs/blob/main/Wayhunter_Second_Brain_v1_0/planning/member_briefs/M2_FULLSTACK_MILESTONES_TASKS.md#L205)

| Case | Existing test | Gap and result |
| --- | --- | --- |
| Missing fields, wrong types and invalid flags | `MissingTotalCost_ShouldFail`, `MissingReviewFlag_ShouldFail`, `DaysAsString_ShouldFail`, `CostAsString_ShouldFail`, `ReviewFlagAsString_ShouldFail` | TEST_VERIFIED. Extended `MalformedAiOutput_Returns502` to assert a code-only response for malformed JSON and a missing-field schema. |
| Invalid days, dates and stops | `MissingDayDate_ShouldFail`, `MissingStops_ShouldFail`, `EmptyStops_ShouldFail`, `DayOutsideTripDates_ShouldFail`, `DuplicateDates_ShouldFail` | TEST_VERIFIED with existing tests. |
| Negative or nonnumeric costs | `NegativeBudget_ShouldFail`, `NegativeTotalCost_ShouldFail`, `NegativeStopCost_ShouldFail`, `CostAsString_ShouldFail` | TEST_VERIFIED with existing tests. |
| Timeout and provider outage | `DisabledAi_Returns503`, `RateLimit_IsRejected`, `EmptyCandidates_AreRejected` | TEST_VERIFIED. Added `ProviderFailure_ReturnsSanitizedError` for timeout and HTTP failure. |
| Place review | `UnknownProviderId_ShouldRequireReview`, `InvalidCoordinates_ShouldRequireReview`, `MatchingExternalPlace_ShouldRemainUnverified` | TEST_VERIFIED with existing evaluator tests. |
| Identity, validation and quota | `AnonymousUser_IsRejected`, `ConflictingIdentityClaims_AreRejected`, `InvalidRequest_DoesNotCallProvider`, `InvalidInput_DoesNotConsumeQuota`, `DailyLimit_RejectsSecondRequest`, `NewUtcDay_ResetsQuota` | TEST_VERIFIED with existing tests. |
| Proposal-only behavior, budget and normal draft creation | `ValidRequest_ReturnsReviewableProposal`, `CreateAsync_ReviewedStops_SavesOwnedDraftInOrder` | TEST_VERIFIED. Added `ReviewedProposal_UsesNormalDraftCreationWithEditedBudget` using the proposal controller and ordinary create service with an in-memory repository. The test models reviewed input, checks no draft after generation or negative-budget rejection, and verifies the saved draft uses the edited budget, dates and reviewed stop while the proposal is preserved. |

## Backend boundary

CODE_VERIFIED. `POST /api/mobile/trip-plans/ai/generate` returns a reviewable proposal. Trip persistence occurs through a separate ordinary draft request. The generation path has no trip write or live-session operation. Live trip activation belongs to M3.

The endpoint returns `502 INVALID_AI_RESPONSE` for rejected itinerary output, `502 AI_PROVIDER_FAILED` for provider failure, `504 AI_PROVIDER_TIMEOUT` for timeout, and `503 AI_TRIP_UNAVAILABLE` when disabled. The error tests verify code-only payloads without raw output or provider exception text.

## Offline verification

| Check | Result |
| --- | --- |
| `dotnet build Wayhunter.slnx --no-restore` | Passed, 0 errors and 2 existing xUnit2031 warnings |
| AI/Gemini unit filter `FullyQualifiedName~AiTrip|FullyQualifiedName~GeminiTripGeneration` | 84 passed, 0 failed, 0 skipped |
| TravelPlanning unit filter `FullyQualifiedName~TravelPlanning` | 474 passed, 0 failed, 0 skipped |
| TravelPlanning integration filter `FullyQualifiedName~TravelPlanning` | 40 passed, 0 failed, 0 skipped |

The integration run disabled shared SQL and live external providers. The draft regression models the backend handoff with test inputs.

**USER-ACCEPTED AI DRAFT HANDOFF: NOT VERIFIED — FLUTTER INTEGRATION PENDING**

NOT_EXECUTED. Device review, budget editing and user acceptance, live Gemini, and physical SQL verification. DECISION_REQUIRED. Approve a restricted server credential, model and live test scope before Gemini acceptance.
