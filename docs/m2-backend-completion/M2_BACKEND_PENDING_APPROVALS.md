# M2 pending approvals

- **Shared SQL.** Approve schema and migration separately, then authorize a disposable SQL Server for row-version, transactional stop ordering, source immutability and retry QA.
- **M2–M3 geometry.** PROPOSED: save ordered stops and route geometry in one transaction with an optimistic version check. A stop edit invalidates the geometry version. An M3 read should require the same plan/version and return unavailable for missing or stale geometry. M3's existing hazard provider remains in place.
- **Historical evidence.** Obtain M3 authoritative completed-session and verified visited-stop evidence, then agree on authorization, revocation and invalidation before surfacing history.
- **Suggested plan reuse.** Obtain explicit permission for member copy and attribution storage. The candidate SQL store has no runtime registration and the legacy flag cannot activate it. Approve durable request identity and test ambiguous commits and concurrent retry behavior on authorized SQL before enabling write access.
- **D11 and ADM-07.** Confirm rank metric, period, eligibility and tie policy. Confirm Admin permission, route/location visibility, flag reasons and audit requirements.
- **M4.** Confirm a public-safe approved-only Hunter place read contract.
- **External providers.** Authorize restricted credentials for live Google Places, Google Routes, Gemini and OpenWeather verification.
