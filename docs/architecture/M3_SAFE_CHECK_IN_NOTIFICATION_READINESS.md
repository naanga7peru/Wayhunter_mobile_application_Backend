# M3 safe check-in notification retention and replay readiness

Status: code-level readiness proposal. The coordinator accepted the schema design and serializable approach for the next stage. Production activation and migration execution remain outside that decision.

## Retention and privacy

| Record | Proposed treatment | Decision required |
|---|---|---|
| Delivered event | Retain the minimal event record and outcome as evidence of the committed check-in and for delivery reconciliation. Restrict access to operational staff who need it. | DECISION_REQUIRED for retention duration, archival or anonymization policy, and identity deletion handling. |
| Dead-letter event | Retain the original event ID, attempt count, timestamps, and sanitized failure classification for investigation and controlled replay. Preserve the link to the check-in. | DECISION_REQUIRED for duration, review cadence, and terminal disposition. |
| Replay audit | Keep a separate access-controlled audit history of each replay request, approval if required, actor, UTC time, reason reference, event ID, previous terminal state, and eventual outcome. | DECISION_REQUIRED for audit destination, retention, access, and any additional approval requirement. |

The current retry contract requires the companion outbox row to remain verifiable. Deleting a delivered or dead-letter row while the associated check-in can still be retried would make that retry fail closed. Any approved archival or purging strategy must preserve verifiable event identity for the check-in or explicitly change the replay contract. Consumer deduplication must cover the full period in which this event can be redelivered or replayed. Do not purge based only on age without resolving these dependencies.

Record stable IDs and classified outcomes only where needed. Keep GPS coordinates, location traces, consent data, recipient contact details, provider response bodies, and secrets out of payloads, diagnostics, and audit history. Protect identity references behind least-privilege access.

## Manual replay

DECISION_REQUIRED for the designated administrators and a server-enforced permission scoped to safe-check-in notification replay. No existing general Admin role is assumed to grant replay. No replay endpoint or operator control is active.

- Authenticate the operator, verify the dedicated replay permission, and require a reason. Check that the row is dead-lettered, has no active lease, is inside the approved replay window, and still has its associated check-in.
- Read authoritative trip ownership and recipient account eligibility. A changed owner or ineligible original recipient must prevent replay. Do not redirect or replace the original recipient. Revalidate immediately before any eventual provider call.
- Propose a conditional audited re-arm that preserves the original event ID and original recipient, clears terminal and lease fields, and assigns an approved bounded retry budget. Record the audit and re-arm atomically in an approved store. Never re-arm a delivered row through this workflow.
- The current shared worker settings default to six attempts with exponential backoff from 30 seconds capped at one hour. These are code defaults. DECISION_REQUIRED for production limits, any replay budget, total replay bounds, lease length, and provider timeout. Expired final-attempt leases dead-letter. An operator should investigate recurring failure before replay, including whether the original send was already accepted.

## Production provider verification

**NOT VERIFIED.** The repository contains the dedicated `ITripSafeCheckInOwnerNotificationSender` contract and an unregistered worker. It has no production implementation or registration for that sender. The existing `WebhookTripMembershipRevocationNotificationSender` belongs to a different event contract. It sends an `Idempotency-Key` header, but the repository provides no evidence that a deployed recipient durably deduplicates safe-check-in owner events or that a safe-check-in provider is configured.

DECISION_REQUIRED for transport/provider selection and proof of its contract. The receiver must accept the original stable outbox event ID, durably enforce one acceptance or user-visible effect per ID across concurrent requests and restarts, acknowledge only after durable acceptance, and return a compatible acknowledgement for an already accepted event. Its deduplication window must cover retry and operator replay. Test provider acceptance followed by a lost response, simultaneous duplicate requests, restart, and replay of the same ID. SQL lease fencing protects the outbox row. It cannot make an external send and SQL acknowledgement atomic.

Safe check-in, owner notification delivery and reminder dispatch remain disabled. Production enablement requires approved schema deployment and end-to-end verification, including provider deduplication evidence.
