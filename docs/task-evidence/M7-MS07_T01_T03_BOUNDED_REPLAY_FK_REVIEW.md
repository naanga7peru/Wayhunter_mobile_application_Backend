# M7-MS07-T01/T03 bounded replay and FK lifecycle review

**Status** M1 review candidates, joint sign-off pending. Shayanthan supplied the 10-minute first-acceptance, 24-hour replay and pending 366-day receipt candidates in the task authorization. Source baseline `ea2eb1ad272998fd7c94c777ee36a1d1cafefa4b`, fetched develop `588867ffd6b39686092de9f84d15cc34e110c3b7`. Preserve the conditionally accepted three tables, composite retry PK, required fact-to-receipt FK and atomic acceptance. Unbounded receipt retention was declined. This review authorizes no cleanup, deletion, ingestion or SQL. [Existing M1 API review](M7-MS07_T01_T03_M1_API_CONTRACT_REVIEW.md)

## Exact deadlines and transaction boundary

| Value | Candidate definition |
| --- | --- |
| `CredentialIssuedAtUtc` or T0 | Trusted UTC sampled by the approved issuance service for the original identity. No client timestamp enters issuance, eligibility or retention. |
| `FirstAcceptanceDeadlineUtc` or A | T0 + 10 minutes. Only `T0 <= trustedUtc < A` permits a fresh event. At exactly A or later, a missing key cannot create a receipt, fact or quota increment. |
| `ReplayDeadlineUtc` or R | T0 + 24 hours. Only `T0 <= trustedUtc < R` permits authorized disclosure from an existing receipt. At R or later, return the approved generic expiry outcome without receipt fields, even if both rows still exist. A missing receipt never changes a failed fresh attempt into a replay. |

Use synchronized authoritative UTC for issuer and SQL decisions. Candidate SQL source is the primary's UTC clock. Validate signed time ordering, exact durations for the credential version and clock health. Reject future issuance. Compare a conservative upper clock bound for expiry if a finite clock uncertainty is approved. It may close a window early and must never extend it. Pause admission and disclosure if trusted time is unhealthy or the bound is unknown. Tests of exact boundaries assume zero clock uncertainty. The ten-minute signed window is independent of the existing candidate internal transaction age/settlement limit.

Validate authenticated account and credential integrity before looking up a receipt. Recheck the correct deadline after taking the exact retry-key lock, and again after all other blocking work at the last guarded fresh-acceptance decision before COMMIT. Roll back tentative writes if expired. Candidate logical acceptance linearizes at that final guarded write decision. SQL acknowledgement and physical commit visibility may follow under an approved finite settlement bound. A transaction that reaches the guarded decision at or after A must roll back, even if it arrived earlier. If owners require physical commit visibility before A, a pre-commit time read cannot prove it. That stronger boundary remains blocked until an enforceable mechanism is approved.

A request arriving near A can wait past A and lose eligibility. An already committed event can be replayed from A until R if still authorized. Recheck time before releasing any receipt-derived response, including after primary commit verification. New valid bearer sessions for the same active, verified account preserve the original identity and deadlines. Invalid, expired or revoked bearer sessions and deactivated accounts give candidate 401 without receipt access.

After an ambiguous commit, reauthorize and verify the exact key on the primary after settlement under the same key lock. Check ownership, pinned payload, and source ID. Positive verification can restore the original 200 only before R. Decisive absence allows a fresh attempt only before A using the original credential. After A, or if the outcome cannot be verified, return the approved generic expired or indeterminate outcome. Never reserve quota on uncertainty. Retry, clock correction and key rotation cannot renew A or R.

## Admission credential and permanent non-reissuance

Require a versioned authenticated credential on **all** proposed intake and replay routes, including unattributed v1. Sign or otherwise authenticate these immutable claims. No credential format is approved yet.

- Issuer, credential format version, purpose and audience, and controlled signing-key selector.
- Stable authenticated M1 account binding, derived by a trusted server and independent of the session.
- AdvertisementId, validated EventType, exact server-minted RetryEventId, and approved stable DeliveryId for v2 where required.
- IssuedAtUtc, FirstAcceptanceDeadlineUtc and ReplayDeadlineUtc.

Verify integrity, expected issuer, purpose, audience, key and version, immutable duration rules, exact canonical identity and current caller. Generic 403 for valid foreign-account credential precedes any receipt lookup regardless of key existence. M1 must approve status, body, headers and timing equivalence. The first accepted request and an owned identical replay use the proposed stable 200 body. Only an authorized owned canonical payload mismatch within replay coverage can yield generic 409. Attempt limits apply first and may return 429 with approved Retry-After. No new quota is consumed on replay.

**Conditional proof.** The issuer can never sign caller-selected or previously allocated keys into a new identity. Every fresh write verifies an approved server-issued tuple under the original deadline. A different DeliveryId fails that tuple; rotating a credential key cannot renew the deadline. After A the genuine old credential cannot create a new event. After storage removal, a newly valid proof for the same key cannot exist if issuance is non-reusing, old signers are fenced and every route enforces both invariants. See the M1 API review for the candidate externally durable epoch/allocation fence. Issuance state, reserved ranges and epochs must survive retries, concurrent issuance, failover, signing-key rotation, compromise and database restore outside rollback scope. Disable legacy, internal, fallback and recovery bypasses. Refuse new issuance or acceptance where fence state or restored replay coverage is uncertain. A different delivery, fresh proof or reactivation of the account cannot renew a previously allocated GUID. Credential expiry and a UUID by themselves do not prove non-reissuance.

**BLOCKED FOR DESIGN DECISION.** Current T01 accepts caller-generated RetryEventId. Changing every path to server-minted identity requires M1/M7 approval. No authoritative issuer or non-reissuance proof through restoration has been validated. Do not enable intake under the bounded design or delete receipts. Sponsored ingestion remains disabled until Campaign proof and attribution are approved.

## Caller ownership HMAC and expiry options

Receipt-only fields `CallerBindingAlgorithmVersion`, `CallerBindingKeyId`, and `CallerBindingDigest binary(32)` are proposed. Use dedicated managed-key HMAC-SHA-256 over the canonical trusted M1 issuer and UserId plus ad/type/retry ID. The API review specifies the versioned domain separator, length-framed strict UTF-8 identity and network-order GUIDs and integers. Store the algorithm version, key ID and digest. Keep raw UserId, bearer token, session ID and secrets outside facts and receipts. Verify ownership using the historical version/key before comparing separately pinned payload v1 or v2 or returning any receipt information. New receipts use the active key. A missing or compromised verification key fails closed and never makes the occupied key available for reuse.

| Option | Impact and review conditions |
| --- | --- |
| **A. Clear ownership fields after replay closure and a bounded verification/settlement horizon** | Preferred for minimization, subject to explicit approval. The three original proposed fields are NOT NULL and the receipt is ordinarily immutable. Candidate alternative makes key ID and digest nullable together while keeping the algorithm version as technical metadata. Require a named pair-consistency CHECK, enforcement that the acceptance writer always supplies both fields, and a one-way, audited, restricted UPDATE exception solely for clearing both after replay and recovery close. Verify under the same retry-key lock, settle writers and verifiers, recheck trusted UTC and approved replay bounds, and leave the composite PK, alternate/FK tuple, source ID, time and Campaign untouched. The FK contains neither caller field. No change to existing constraints or write controls is approved. |
| **B. Retain the binding through physical receipt storage** | Compatible with current proposed NOT NULL and append-only semantics. Requires a demonstrated, approved purpose such as specifically authorized incident evidence or erasure verification after replay closes. Routine replay and FK integrity provide no need for this extra retention. Restrict to authorized roles and audit access. A stored HMAC remains pseudonymous and linkable even when its HMAC key is unavailable because the receipt carries other correlatable fields and existing copies or joins can remain. Retaining HMAC keys for 366 days would need separate necessity and key-custody approval. |

Option A cannot restore account ownership after clearing. That is acceptable only when late replay is denied independently by the authenticated credential deadline, current authorization and permanent non-reissuance. Missing fields on a live replay path fail closed, with no key-existence signal or reacceptance. Historical auditors continue to use separate approved scope and immutable facts. Agree with M1 and governance how erasure can be located and verified without the digest, using only approved external evidence and without building a shadow account-to-event map. Report coverage gaps after any authorized deletion. Shared-key retirement is not per-row cryptographic erasure and does not clear online, archived, exported or backed-up digests. Consider cryptographic erasure only with specifically approved key granularity, proof of copy/key destruction and recovery design, and no damage to other live receipts.

Rotate new writes to the active key. Retain old verification material only for the maximum approved live replay/commit-verification horizon across receipts under that key, plus validated settlement and recovery needs. No post-replay public disclosure follows from key availability. Lost key denies disclosure and never admits a fresh attempt. A compromised signer or lost issuance fence requires blocking affected admissions and governed recovery before reopening. Reconcile clear/erasure state and original deadlines before restoring access from backup and age archival copies under the approved policy.

## Finite retention and cleanup order

| Lifecycle | Review position |
| --- | --- |
| First admission and replay | A and R as above, independent of row storage. |
| Binding material | Option A candidate clears key ID/digest after R and the approved verification and settlement horizon. Option B is pending necessity evidence. For any version without a stored deadline, `AcceptedAtUtc + maximum replay window for admitted versions` plus approved settlement allowance is a conservative lower bound for clearing, since issuance precedes acceptance. Never reduce that floor on a version change. |
| Accepted fact | 365 days from the candidate server-derived AcceptedAtUtc, pending governance. Archive or remove only if explicitly authorized, and track resulting reporting coverage. |
| Receipt row | 366 days from the same AcceptedAtUtc, pending. At 365 days the proposed fact disposition can release the dependency, leaving a candidate one-day completion margin. Only if no dependent fact or required archive/recovery dependency remains can the receipt be removed at or after its authorized cutoff. Do not promise 366-day deletion if the FK remains. |
| Quota | 48 hours after UTC window end, conditionally acceptable. Separate from receipt and consent expiry and still subject to safe pending transaction cutoffs. |
| Retry identity | Permanently non-reissuable across archived and deleted storage. Expired proof and all legacy routes stay inadmissible. This is an issuer property, not a purge operation. |

1. Close fresh acceptance at A. Close receipt disclosure separately at R. Settle ambiguous writes and protect original identity. No absent-row shortcut.
2. Under the retry-key lock, consider option A only after the approved replay/settlement floor and any governed erasure decision. The FK and immutable event values stay intact.
3. Under appropriate approved locks, recheck DB UTC, governance disposition, authorized archive state and active recovery/write fences. Process or archive facts first, and release any archive dependency only under the approved lifecycle. Quota cleanup uses its separate bucket lock and cutoff.
4. Once at the approved receipt cutoff, acquire the same exact key lock used by admission/replay, verify no dependent fact exists and checks are settled, then consider authorized receipt removal. The existing composite NO ACTION FK must reject any premature delete. Recheck after waiting. If holds, unavailable archive or failed cleanup prevent 366-day completion, escalate to governance and DB Coordinator. Preserve the FK and suspend affected intake if required. No automatic extension, cleanup exception or deletion authority.
5. Verify expired credentials cannot reach the fresh path after row removal, including under another proof, retired key or restore. Evidence must cover all replicas and versions. Mark incomplete historical reporting rather than producing verified zero.

## Exact review-only acceptance matrix

**All API and SQL cases are NOT EXECUTED.** Use signed synthetic credentials, independent DbContexts for database races, deterministic barriers, a trusted controllable UTC clock and a committed-state observer in an approved disposable environment. Implicit clock uncertainty is zero for the exact boundary rows. **J** = M1/M7 joint contract, **D** = DB Coordinator and isolated SQL, **C** = Campaign owner, **P** = Privacy/Data Governance. Each listed approval is required before its dependent verification.

| # | Input and clock boundary | Expected candidate result | Persisted-state invariant | Approval / status |
| --- | --- | --- | --- | --- |
| 1 | Approved issuance at server T0 with divergent client clock | Authenticate T0, A=T0+10m and R=T0+24h; ignore client clock | Allocated key is non-reused. No fact, receipt or quota on issue | J/D — NOT EXECUTED |
| 2 | Valid unused identity, final locked decision at A−1 tick | 200 after confirmed commit within authorized replay coverage | One fact, receipt and increment | J/D — NOT EXECUTED |
| 3 | Unused identity, locked decision exactly A | Generic expiry, no replay | No accepted writes | J/D — NOT EXECUTED |
| 4 | Unused identity arrives before A but obtains final lock after A | Generic expiry; rollback tentative work | No accepted writes | J/D — NOT EXECUTED |
| 5 | Owned compatible receipt, release at R−1 tick | Stable identical 200 body | No extra fact, receipt or quota | J/D — NOT EXECUTED |
| 6 | Owned receipt, release exactly R | Generic expiry, no receipt fields | Existing rows unchanged | J/D — NOT EXECUTED |
| 7 | Replay after R while dependent fact remains | Generic expiry, no source ID, time or Campaign | Fact and receipt unaffected | J/D/P — NOT EXECUTED |
| 8 | Same active account refreshes session before R | Same authorized 200 with original deadlines | One historical fact and increment | J/D — NOT EXECUTED |
| 9 | Revoked session or deactivated account before R | 401 before lookup | No receipt access or accepted writes | J — NOT EXECUTED |
| 10 | Foreign valid credential before R, present/absent controls | Generic 403 with equivalent status/body/headers/timing; no existence signal | No accepted writes or receipt disclosure | J/D — NOT EXECUTED |
| 11 | Owned key and differing canonical payload before R | Generic 409 only after verified ownership | Original receipt and fact unchanged | J/D — NOT EXECUTED |
| 12 | Owned identical retry before R with accepted quota full | 200 if attempt capacity exists | No increment | J/D — NOT EXECUTED |
| 13 | Owned replay before R with attempt quota exhausted | 429 and approved Retry-After before lookup | No accepted writes | J — NOT EXECUTED |
| 14 | Old key with different DeliveryId from issuance through expiry | Generic denial, or authorized 409 for a genuine owned canonical mismatch, never rebind | No second acceptance | J/C/D — NOT EXECUTED |
| 15 | After A and after cleanup, attempt fresh proof with expired old RetryEventId | Issuer refuses old ID and ingress rejects any replacement/mismatch | No recreated receipt, fact or increment | J/C/D — NOT EXECUTED |
| 16 | Concurrent acceptance/cleanup, early attempt holds key and other waiter crosses A or R | Wait, recheck locked deadlines and settlement; no expiry bypass | No partial acceptance or premature deletion | J/D/P — NOT EXECUTED |
| 17 | Lost acknowledgement, receipt lookup missing on primary | Before A only decisive absence may allow original attempt; after A or unresolved, generic indeterminate/expiry | No blind increment or invented 200 | J/D — NOT EXECUTED |
| 18 | At candidate day 366 with dependent fact still retained | Cleanup refused by checks and NO ACTION FK | Both rows preserved | D/P — NOT EXECUTED |
| 19 | Approved fact expiry at day 365 and receipt cutoff at day 366 | Remove/archive fact as allowed first, then receipt only with no dependency | No cascade and expired key stays inadmissible | D/P — NOT EXECUTED |
| 20 | Option A after R and approved settlement floor or earlier authorized erasure | Controlled one-way clearing only under approved policy; old replay denies | No change to composite FK or acceptance facts; clear both ownership fields together | J/D/P — NOT EXECUTED |
| 21 | Rotate key during replay and lose historic key | Use recorded KeyId while available; missing historic key fails generically | No digest rewrite or reacceptance | J/D/P — NOT EXECUTED |
| 22 | Approved archival changes requested historical window after fact horizon | Report complete coverage only if proven; otherwise label partial/unavailable/unknown | Never turn known coverage gaps into verified zero | J/D/P — NOT EXECUTED |
| 23 | Restore snapshot from before expiry or allocation increment, after old rows removed | Fence old epoch, block intake until authorized reconciliation, reject stale replay | No reissued identity, reactivation or duplicate fact | J/C/D/P — NOT EXECUTED |
| 24 | Consent withdrawal or approved erasure request during replay or retained history | Stop dependent intake/disclosure; enforce governed exception, archive/backup treatment and incomplete coverage | No unauthorized delete; no stale disclosure or old-key reacceptance | J/D/P — NOT EXECUTED |

## Pending decisions

M1/M7 approve issuance and every route, the deadline linearization and exact response behavior, canonical HMAC and key retirement. Campaign owner approves required DeliveryId and actual-delivery binding. Privacy Governance approves the purpose, consent and erasure policy, option A exception or option B necessity, archive/backup limits and retention durations. DB Coordinator approves constraints, clock/locking evidence, FK-preserving expiry, restore and isolated validation. Team Leader coordinates sign-off. No route, SQL, migration, database test or cleanup was run.
