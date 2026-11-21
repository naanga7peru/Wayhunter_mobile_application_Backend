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
| `CallerBindingAlgorithmVersion` | Candidate `smallint NOT NULL`, positive and explicitly allowlisted. Version 1 defines the HMAC framing below. |
| `CallerBindingKeyId` | Candidate `uniqueidentifier NOT NULL`, nonzero opaque managed-key identifier, never the key material. |
| `CallerBindingDigest` | `binary(32) NOT NULL`, full HMAC-SHA-256 output. Verify exact length before persistence. Store these three caller-binding fields only. |

Keep these fields separate from the existing payload `BindingVersion` and `BindingDigest`. The account binding establishes receipt ownership. The payload binding establishes equality of the canonical event and approved delivery identity. Neither is a substitute for authentication or authorization. Neither new field belongs in accepted facts, reports, exported audit records or public responses.

Candidate v1 is HMAC-SHA-256 with a dedicated managed receipt key over ASCII `Wayhunter.M7.AdEvent.ReceiptCaller` followed by one zero byte, algorithm version as unsigned 16-bit big-endian, then the canonical M1 issuer namespace and canonical M1 UserId each encoded as an unsigned 32-bit big-endian byte length followed by strict UTF-8 bytes. Append the 16-byte AdvertisementId in RFC network order, EventType as unsigned 32-bit big-endian (1 or 2), and the 16-byte RetryEventId in RFC network order. M1 must define canonical account bytes and maximum lengths. Reject malformed input, unknown versions and zero GUIDs. Do not trim, re-case or normalize identity independently of M1. Validate synthetic cross-platform encoding vectors. New receipts use the active key and algorithm version. Historical verification resolves the stored key ID and version and compares the full digest in constant time inside the trusted boundary. Session values are excluded, so valid sessions for the same account yield the same per-key binding. This digest is pseudonymous personal data. [HMAC specification](https://www.rfc-editor.org/rfc/rfc2104.html)

Keep historical verification keys only for an approved receipt verification lifetime and rotate new writes without changing the accepted records. Approve key custody, access boundary, recovery and compromise handling, version lookup, retirement, and storage and retention as one decision. M1 declined unbounded receipts. A bounded verification and retirement lifecycle is blocked on the approved admission design. Missing or retired verification material must fail closed through the generic path, preserving the key for deduplication. Never reaccept an occupied key solely because its account binding cannot be verified. Deletion or migration to tombstones still requires privacy and coordinator approval. The earlier privacy inventory covers the pre-review columns. The privacy assessment classifies these three proposed fields and records their storage-cost implications. Approval remains pending.

## Recommended HTTP and retry state matrix

All externally visible behavior is **PROPOSED**. The stable body for successful acceptance and authorized matching replay contains only `sourceEventId`, `acceptedAtUtc` and `status="accepted"`. Restore values from the immutable receipt, using approved canonical UTC serialization. Campaign attribution remains internal. Do not include session fields or a mutable Campaign lookup in the response.

| State | Recommended outcome | Accepted-event effect |
| --- | --- | --- |
| Missing, invalid, expired or revoked bearer session, or deactivated account | 401 Unauthorized. Fail before receipt access. | Zero writes |
| Authenticated request at attempt limit | 429, approved Retry-After. Rate check happens before replay. | Zero accepted-event writes |
| First eligible, authorized event | 200 OK with stable accepted body after confirmed commit. | One receipt, one fact, one quota increment |
| Same account and identical canonical payload | Internal Duplicate. HTTP 200 with the same accepted body, including across refreshed sessions or at exhausted accepted quota. | Zero new accepted-event writes |
| Same account, same key, differing canonical payload | Generic 409 Conflict only after ownership and disclosure authorization. | Zero accepted-event writes |
| Valid admission credential belonging to another authenticated account | Generic 403 Forbidden before receipt lookup, regardless of key existence. Unverifiable binding fails closed with no existing-key disclosure. Exact security equivalence remains pending. | Zero accepted-event writes |
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
- **DB Coordinator and M7:** Review the three receipt fields, controlled-writer and verification boundary, key version recovery, capacity, isolation and migration ownership. Approve a disposable SQL environment before tests. Final SQL approval remains pending.

## Acknowledgement and privacy dependency

Kaviyan acknowledged the M1 contract review as relayed in the task authorization. Joint sign-off and final SQL approval remain pending. Shayanthan separately declined unbounded receipt retention. See the [privacy assessment](M7-MS07_T01_T03_PRIVACY_RETENTION_ASSESSMENT.md) for the binding classification, bounded admission analysis and retention gates. The recommended API matrix above remains subject to joint sign-off.

### Additional review-only scenarios

| Required scenario | Expected evidence | Status |
| --- | --- | --- |
| Deactivate an account or revoke its access after acceptance | Fresh and replay paths deny access without revealing key or receipt. Re-activation cannot extend original admission. | NOT EXECUTED |
| Reuse accepted key with a fresh different delivery proof | Owned retained key conflicts. A deleted receipt cannot make an old key admissible under any approved issuer/version. | NOT EXECUTED |
| Retry at receipt-expiry and admission boundaries | Delays, version rollover, restored sessions and unknown commit never reopen accepted keys. After replay coverage ends, return only the approved generic disposition. | NOT EXECUTED |
| Archive facts or authorize erasure | Retained replay identity persists within approved lifetime. Reports label missing coverage. Govern any approved retained exception and downstream copies. | NOT EXECUTED |
| Withdraw consent | Future processing dependent on it stops. Delayed delivery and previously accepted records follow explicit approved handling. | NOT EXECUTED |
| Race cleanup against acceptance and ambiguous commit | Independent DbContexts prove key settlement, FK and deadline safety under locking. No double fact or quota. | NOT EXECUTED |

No test here ran against SQL or a production API.

## Latest M1 direction and payload version

Shayanthan supplied the latest joint-review direction in the task authorization. Kaviyan's earlier acknowledgement stands. The updated HMAC fields, 403 for a foreign credential and bounded admission below are review directions awaiting final M1/M7 sign-off. V1 is canonical ad/type/retry identity for explicitly approved unattributed flows only. V2 adds authenticated server-issued DeliveryId. The owned receipt pins its original payload version and commitment. Do not compare payload before ownership. A different DeliveryId cannot reactivate the key.

## Server-issued admission and replay — proposed contract change

Replace the current T01 caller-generated RetryEventId admission rule with a server-minted value in a versioned, authenticated M7 admission credential. The composite uniqueness constraint remains `(AdvertisementId, EventType, RetryEventId)`. Every fresh and replay path, including unattributed v1, must require the approved credential and current M1 bearer authentication. Reject legacy caller-chosen keys before any production cutover. This contract change needs M1/M7 approval. Sponsored intake stays disabled pending Campaign approval.

Credential claims bind the M1 issuer/account, AdvertisementId, EventType, exact server-minted RetryEventId, issued-at UTC, immutable admission and replay deadlines, and the approved delivery identity for payload v2. Candidate binding uses an opaque account reference derived with an admission-specific managed key distinct from the receipt HMAC, or an encrypted authenticated credential if the approved issuer must carry canonical account data. Do not log or persist the credential. Verify format, issuer, audience and purpose, approved version and key, integrity, exact typed tuple and current caller before a receipt lookup. Require constant-time subject verification using the approved scheme. Do not accept a new DeliveryId for an issued identity or extend its deadlines on reissue. Reauthentication or rotation may restore verification of the original credential only within its original bounds. Client bearer and admission proofs have independent expiry.

**Non-reissuance proof obligation.** Require a single logical issuer across replicas with an externally durable, non-rollback allocation fence. One candidate is a UUIDv8 encoding with 32 bits of permanently non-reused issuer epoch and 90 bits of monotonic allocation sequence in the 122 application bits, filled in RFC network-bit order skipping the fixed version and variant bits. Durably reserve non-overlapping ranges before issuance, never recycle unused ranges, stop at exhaustion, and fence an old issuer after loss of lease, failover or restore. Advance to a never-used epoch if allocator state is uncertain. An approved authority must prove epoch uniqueness outside any restorable database, bound the signer to its authorized range, and fence compromised or retired issuers and keys. The mint endpoint never signs a supplied RetryEventId. A new delivery must receive a new allocated key. Prevent minting a second logical admission for the same delivery/type where the delivery contract promises one event. Treat ordered identifiers as observable metadata and review UUIDv8 and issuer allocation for privacy. No allocator service, production table or approved issuer exists in this design. The UUID encoding and credential expiry alone do not establish uniqueness. [RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html)

| Window | Review candidate | Rationale and tradeoff |
| --- | --- | --- |
| First admission | From issued-at, inclusive, to issued-at plus 15 minutes, exclusive | Covers ordinary delivery/tap and short network delays. Offline or delayed clicks past the deadline fail. Measure delivery latency before choosing. |
| Authorized replay | From issued-at, inclusive, to issued-at plus 24 hours, exclusive | Allows response recovery across valid renewed sessions. Longer recovery also increases the time in which receipt disclosure is possible. No expiry extension per retry or new proof. |
| In-flight settlement and cleanup buffer | Candidate 1 hour after the longest replay deadline, provided approved locking, timeout, verification and clock evidence supports a hard bound | Preserves time to verify an uncertain commit without extending disclosure eligibility. No cleanup if any relevant transaction or verification can still be unsettled. |

Use trusted database UTC to enforce both deadlines after acquiring the exact retry-key lock. The fresh path also checks admission immediately before committing the write decision, with bounded transaction settlement and rollback on expiration before that decision. Define and test the lock-to-commit boundary so a wait cannot authorize a fresh write at or after the deadline. No clock skew margin extends eligibility. Pause on clock confidence loss. An already completed acceptance may be recovered during the replay window if the caller remains authorized. Check replay authorization before releasing a receipt-derived response, including in commit verification. After replay expiry, deny disclosure even if the receipt and fact still exist. Define the generic expired and indeterminate responses at joint sign-off.

### Admission and response state order

1. Apply M1 bearer validation and the active verified-account check. Invalid, revoked or expired session or deactivated account returns candidate 401 without lookup.
2. Apply the attempt limiter to each authenticated attempt, including a replay. Candidate 429 uses approved Retry-After independent of receipt existence.
3. Verify the credential and subject binding before lookup. A valid credential issued to a different account returns generic 403 regardless of whether a receipt exists. Other invalid or unavailable admission/delivery cases use the approved generic response. No protected state is disclosed.
4. Under the scoped retry lock, for a retained receipt verify caller HMAC with its recorded key and version before comparing the pinned payload. Only the authorized matching replay within its deadline returns the stable 200. Only an authorized payload mismatch within the approved disclosure window returns generic 409. For an unverifiable or foreign owner, deny generically, never treat it as a missing row.
5. With no receipt, require a matching signed identity inside the first-admission window and current eligibility. An old key with a different DeliveryId or a fresh proof is inadmissible. Only then reserve accepted quota. At capacity use candidate 429 with approved Retry-After. Commit the receipt, fact and increment together.
6. For an ambiguous commit, verify the original scoped key on the primary with ownership, pinned payload and candidate source ID after settlement. Restore the original response only where replay is still authorized. A decisive absent receipt can proceed only within the original first-admission deadline and with the original credential. An indeterminate or expired outcome preserves the original key and returns no invented acceptance or new increment.

For cross-account denial, test equivalent requests with and without an existing receipt, controlling for attempt budget and proof validity. Compare status, body, headers and timing distributions. Reject foreign credentials independently of database state. Test retained-owner mismatch and unavailable verification material for comparable generic behavior and absence of receipt fields. Exact external responses and anti-disclosure criteria remain pending joint sign-off.

## Finite receipt storage with the existing composite FK

**M1 declined unbounded receipts. Bounded retention remains BLOCKED FOR DESIGN DECISION** until the issuer, every ingress path, recovery and cleanup invariants are proven and governance approves durations. Separate eligibility, disclosure and physical retention.

- Candidate receipt lifetime is finite and tied to fact retention. With the pending 365-day fact candidate, propose a seven-day cleanup completion budget after fact expiry for review. At that fact horizon, a receipt retained for FK integrity could therefore have a normal maximum of 372 days from stored AcceptedAtUtc. These numbers are design scenarios, not approved durations. Trigger eligible cleanup only after the replay and settlement floors have passed. If no per-credential deadline is persisted, a conservative floor is AcceptedAtUtc plus the maximum replay window for any admitted version plus the approved settlement buffer, since issuance precedes acceptance. Do not lower this floor on config rollout. A different approved fact horizon needs a matching receipt budget.
- Stop user-facing replay at the signed deadline even while the receipt and fact remain available for required storage and scoped audit. Cleanup first removes or relocates the fact under specifically authorized archival/erasure rules, then may remove the receipt only once no dependent fact remains, including across any approved archive references. Keep the required composite FK with NO ACTION. Where archival still relies on the original FK or requires exact replay fields, that dependency must be resolved within the approved lifecycle. If a legal hold, failed job, archive dependency or clock/issuer uncertainty prevents the approved deadline, escalate, suspend the affected intake if required, and preserve FK and key safety. No automatic extension, deletion or exception is granted.
- Cleanup and replay coordinate on the same exact retry-key lock. After acquiring it, recheck current DB time, the retirement floor, all dependent facts, approved archive state and transaction settlement. Do not delete while an accepted fact or required recovery dependency exists. Recheck expiry at response release when a replay wins the lock first. When cleanup wins, expired proof is rejected before fresh acceptance. Never use the absence of a receipt as evidence that a previously issued key is unused. Archive and restore must preserve the issuance fence and active invariants, and intake stays closed until reconciliation if any current replay horizon was affected.
- An old credential stays expired after physical cleanup, independent of whether its former fact is queryable. A new credential cannot contain the old key because issuance ranges cannot be reused, signers cannot rebind a previously allocated value, and missing-key acceptance always verifies the immutable signed tuple/deadline. A new DeliveryId cannot repair the mismatch. This proves conditional safety only if the issuer and restore/failover invariants hold for all versions. A client-generated key plus an expiring delivery token gives no such proof.
- Retire receipt HMAC keys after the last approved replay and commit-verification horizon using that key, including skew, in-flight and restore reconciliation. Old facts held solely for audit/FK integrity do not extend public replay. Use a separate approved verification lifecycle for admission signing keys. A missing or compromised key fails closed without disclosing the receipt or creating a new acceptance. If signing compromise might have allowed a fresh credential for a used value, disable intake for the affected epoch until coordinator-approved recovery. Do not re-sign old keys or extend deadlines as a workaround.

| Alternative | Finding |
| --- | --- |
| Current client-generated retry key with time-only receipt deletion | Unsafe. A fresh proof may authorize the same key once all memory disappears. BLOCKED. |
| Time-limited server credential with a reversible allocator or re-signing of supplied keys | Unsafe. Another credential or restore can revive the key. BLOCKED. |
| Verified non-reissuable admission plus FK-ordered finite receipt lifecycle | Candidate with conditional proof above. Requires owner approval and isolated SQL evidence. |
| Finite minimal replay marker | Could bridge an approved recovery gap after the fact FK is released. Stores linkable key and only required versioned commitments and expiry. It cannot restore exact 200 fields unless they remain available. Marker deletion still requires permanent inadmissibility. No new table or permanent marker is proposed. |

## Latest Shayanthan joint review test additions

All cases are **NOT EXECUTED**. These extend the earlier matrix; earlier fake-store results do not prove SQL locking, cleanup or side-channel behavior.

| Case | Required evidence |
| --- | --- |
| Same account, refreshed valid session | Ownership HMAC and authorized identical body stay stable; same key, one fact/increment. |
| Foreign credential and receipt existence | Identical generic 403 across present/absent controls, no receipt content, headers or timing signal; include unverifiable binding tests. |
| Revoked/expired session or deactivated account | 401 without receipt lookup. Re-activation never extends either deadline. |
| Payload mismatch and pinned version | Owned canonical mismatch gives generic 409, no increment. Exercise v1/v2 pinning and separate caller binding. |
| HMAC rotation or unavailable historical key | New receipts use new KeyId; old receipt verifies using stored KeyId; missing/revoked key denies without disclosure or key reuse. |
| Admission expiry boundary | First valid attempt just before expiry may win; at/after deadline no new fact, even after a lock wait. |
| Replay expiry boundary and retained fact | Before boundary authorized exact stable 200; at/after no disclosure even if both rows remain. |
| Old key and different delivery proof | Issuer rejects rebinding across replicas, key versions and restore. Database path cannot accept old key after fact/receipt expiry. |
| Exhausted accepted quota and attempt limiter | Owned replay at full accepted quota returns 200 if attempt capacity exists; exhausted attempt budget returns 429 before lookup. |
| Lost response and ambiguous commit | Same body from original receipt within replay authorization. Verify rollback, commit success and indeterminate cases with no blind increment. |
| FK order and concurrent cleanup | Dependent fact blocks receipt delete. Race cleanup/replay/acceptance using independent DbContexts, including expiry while waiting and restore gates. |
| Consent withdrawal and erasure | Govern future admission, receipt disclosure, approved exceptions, archive/backup reconciliation and incomplete historical coverage. |
