# M7-MS07-T01/T03 M1 API contract review

**Reviewer** Shayanthan — M1. **Status** Authentication requirements confirmed for the proposed design. API behavior is recommended for M1/M7 joint sign-off. SQL approval, Campaign proof approval, and privacy and retention governance are pending. This review authorizes no endpoints, persistence, or SQL execution.

The review starts from the published privacy assessment at `9068cb8571678af2109b70b09a106ebe7a2e486f` and verified develop `ac7e790572b45b57594800a33cd991da977253ae`. The existing `src/Wayhunter.Api/Services/IdentitySocial/SessionBearerAuthenticationHandler.cs` delegates bearer validation to M1 session authorization and supplies separate account and session claims. T01's current application outcome remains Duplicate for a retained replay. T03's audit access boundary remains independent of the intake response. The preserved three-table design retains unique `(AdvertisementId, EventType, RetryEventId)`.

## Confirmed authentication and admission requirements

- Reuse trusted M1 bearer-session validation. Require an active, verified account and a valid, unexpired, non-revoked session. Resolve the caller from trusted authentication alone. A new valid session for the same account can replay the receipt. An old invalid, expired or revoked session cannot.
- Apply request-attempt limiting to each authenticated request, including a first attempt, matching retry, foreign key attempt or conflict. Apply the limiter before a retry receipt lookup. This control is independent of the accepted-event quota.
- Verify receipt ownership before comparing payload or returning a receipt or conflict. Use current authentication and the separately approved admission scope for any disclosure. For a foreign account, prove no receipt, source ID, time, attribution, distinct conflict, header or other key-existence signal is returned. M1 must approve a generic failure with equivalent behavior for the relevant absent-key denial paths, including response timing and rate-limit side effects. Do not add caller binding to the composite unique key to bypass an ownership failure.
- M1 authenticates the request. M7 checks ad and delivery eligibility. Require approved caller-bound server-issued delivery proof for sponsored intake. Keep sponsored intake disabled until the Campaign owner, M1 and M7 approve actual-delivery attribution and the proof format and rules. Define for authorized replay which stable proof identity must remain verifiable and when admission expiry bars only fresh acceptance. Keep historic attribution immutable.
- The accepted receipt, fact and one quota increment commit together once. Neither an identical authorized retry nor a conflict consumes accepted-event capacity. A healthy attempt allowance and verified ownership can permit matching replay when accepted-event quota is exhausted.

## Proposed receipt-only caller binding

| Proposed field | Candidate |
| --- | --- |
| `CallerBindingVersion` | `smallint NOT NULL` for any new receipt under this proposal, strictly positive and mapped to an approved algorithm/key generation. Exact constraint and version registry pending. |
| `CallerBindingDigest` | `binary(32) NOT NULL`, candidate HMAC-SHA-256 output over the trusted account identity. Validate the full digest length before persistence. No raw account or credential inputs persist in the receipt. |

Keep these fields separate from the existing payload `BindingVersion` and `BindingDigest`. The account binding establishes receipt ownership. The payload binding establishes equality of the canonical event and approved delivery identity. Neither is a substitute for authentication or authorization. Neither new field belongs in accepted facts, reports, exported audit records or public responses.

Candidate HMAC input uses a dedicated M7 receipt-caller domain separator and unambiguous length-prefixed canonical M1 issuer namespace and stable immutable account identity. Use exact canonical identity bytes supplied by the approved M1 adapter and no arbitrary case folding or session ID. Include the binding format version in the domain framing. Prefer a per-purpose managed secret. Resolve the saved version for existing receipts, use the current approved version for new receipts, and compare digests in constant time in the approved trusted component. No unkeyed or unsalted account hash is treated as anonymous. This remains pseudonymous linkable data.

Keep historical verification keys only for an approved receipt verification lifetime and rotate new writes without changing the accepted records. Approve key custody, access boundary, recovery and compromise handling, version lookup, retirement, and storage and retention as one decision. Initially unbounded receipts make key retirement a blocking decision. Missing or retired verification material must fail closed through the generic path, preserving the key for deduplication. Never reaccept an occupied key solely because its account binding cannot be verified. Deletion or migration to tombstones still requires privacy and coordinator approval. The earlier privacy inventory covers the pre-review columns. These two proposed additions require supplemental classification, access and storage-cost review before adoption.

## Recommended HTTP and retry state matrix

All externally visible behavior is **PROPOSED**. The stable body for successful acceptance and authorized matching replay contains only `sourceEventId`, `acceptedAtUtc` and `status="accepted"`. Restore values from the immutable receipt, using approved canonical UTC serialization. Campaign attribution remains internal. Do not include session fields or a mutable Campaign lookup in the response.

| State | Recommended outcome | Accepted-event effect |
| --- | --- | --- |
| Missing, invalid, expired or revoked bearer session | 401 Unauthorized. Fail before receipt access. | Zero writes |
| Authenticated request at attempt limit | 429, approved Retry-After. Rate check happens before replay. | Zero accepted-event writes |
| First eligible, authorized event | 200 OK with stable accepted body after confirmed commit. | One receipt, one fact, one quota increment |
| Same account and identical canonical payload | Internal Duplicate. HTTP 200 with the same accepted body, including across refreshed sessions or at exhausted accepted quota. | Zero new accepted-event writes |
| Same account, same key, differing canonical payload | Generic 409 Conflict only after ownership and disclosure authorization. | Zero accepted-event writes |
| Foreign account or unverifiable binding | Generic failure, without disclosing key existence. Exact status and equivalence rules pending M1 security approval. | Zero accepted-event writes |
| Fresh admissible event at accepted quota | 429 with approved Retry-After semantics for the governing bucket. | Zero accepted-event writes |
| Unavailable or unauthorized ad/delivery, including invalid required proof | Generic failure without protected ad or Campaign state. Exact status pending. | Zero accepted-event writes |
| Lost HTTP response, then matching authorized replay | Reconstruct the original accepted body from the receipt. | Zero additional writes |
| Ambiguous SQL commit | Under the same authorization boundary, verify the exact key on the primary after locks settle. Verify caller ownership, payload and source ID before confirming the operation. A different source with identical authorized identity is Duplicate. Indeterminate outcome returns a pending generic retryable failure. | No blind acceptance or quota increment. Preserve key. |

Inside the candidate transaction, protect the exact composite key first. For a found receipt, validate saved caller binding before payload comparison or returning any replay state. Use the same order in unique-constraint and ambiguous-commit verification paths. A missing key proceeds through approved current ad/delivery checks and guarded quota before atomically writing receipt and fact. Classify foreign-account ownership before exposing conflict or data even in concurrent races. See the [physical review package](M7-MS07_T01_T03_PHYSICAL_SCHEMA_REVIEW_PACKAGE.md).

**Rate review.** The existing 120 impressions and 30 clicks per advertisement/event-type/60-second UTC window are **load-test values only**. An opened quota bucket retains its original configuration version, end and limit. Neither value is approved for production. Exact production partition, quota values, attempt limits, Retry-After handling and effective policy changes require M1, M7 and DB Coordinator approval. Any personal scope requires privacy review.

## Future acceptance matrix

All checks below are **NOT EXECUTED**. The existing harness provides prior synthetic contract evidence. It cannot demonstrate SQL concurrency or implemented HTTP behavior.

| Required scenario | Expected evidence | Gate |
| --- | --- | --- |
| Same account with valid refreshed bearer session | Both sessions resolve to the same stable account. Body is unchanged, with one fact and increment. | Future API and isolated SQL, NOT EXECUTED |
| Different account repeats the same composite key | Generic failure and zero writes, with a control request for the equivalent absent-key denial path. | Future API and isolated SQL, NOT EXECUTED |
| Caller-binding mismatch or missing version/key | No receipt, source or attribution and no detectable conflict distinction. Include comparison of response status, headers, body and timing. | Future security tests, NOT EXECUTED |
| Same account reuses key with different canonical payload | Generic 409 and no accepted writes. Verify owner check occurs first. | Future API and isolated SQL, NOT EXECUTED |
| Bearer expired or revoked on retry | 401 before replay verification. Confirm missing and invalid session variants. | Future API, NOT EXECUTED |
| Identical retry while accepted quota is exhausted | 200 replay, previous fact and count unchanged. | Future API and isolated SQL, NOT EXECUTED |
| Attempt limit exhausted before replay | 429 with approved Retry-After, no lookup or accepted writes. | Future API, NOT EXECUTED |
| Lost response and ambiguous commit | Authorized first response and replay have the same stable body. Verify commit-success, rollback and indeterminate outcomes using original identity. | Future API and isolated SQL, NOT EXECUTED |
| Campaign changes after acceptance | Historical attribution remains on the original receipt and fact. Replay restores original response independently of the changed relationship. | Future isolated SQL after Campaign approval, NOT EXECUTED |
| Missing, invalid, foreign-account or unapproved sponsored proof | Generic denial with no Campaign state, receipt, fact or accepted quota change. | Future API after proof approval, NOT EXECUTED |
| Concurrent retry race across independent DbContexts | One accepted fact, receipt and quota increment. Include matching account, different payload and foreign-account variants. No response leak. | Future isolated SQL, NOT EXECUTED |

## Joint approvals and blockers

- **M1 and M7:** Approve exact authenticated account namespace, schema and canonical binding algorithm, proof semantics, replay-body and generic non-disclosure responses, Retry-After policy, rate partitions and thresholds, and authorization tests.
- **M1, privacy/data owners and Team Leader:** Approve pseudonymous caller digest handling, secret custody and lifetime, linkage and disclosure, retention/erasure and deletion effects. Existing privacy and retention assessment remains pending formal governance.
- **Campaign owner, M1 and M7:** Approve caller-bound trusted delivery proof and immutable attribution to the actual Campaign shown. Sponsored intake stays disabled.
- **DB Coordinator and M7:** Review the two receipt fields, controlled-writer and verification boundary, key version recovery, capacity, isolation and migration ownership. Approve a disposable SQL environment before tests. Final SQL approval remains pending.
