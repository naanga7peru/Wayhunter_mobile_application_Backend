# Test matrix and stop conditions — no tests executed

| ID | Scope | Mechanism | Pass criterion / HOLD |
|---|---|---|---|
| M01 | E metadata | 06 catalog read | 16 claim columns, 5 checks, 3 FK, 5 indexes and principal keys |
| M02 | E metadata | sys.indexes/sys.index_columns | seven-column key declared max 1204 bytes, nonclustered |
| M03 | E metadata | sys.columns collation | namespace/canonical BIN2 pair same on op+claim; player FK collation matches |
| M04 | D least privilege | EXECUTE AS loginless probe | direct table INSERT/UPDATE/DELETE denied; only named procs executable |
| S01 | E synthetic | TX-A matching create | one op + one claim, no partial commit |
| S02 | E synthetic | deliberate test-only constraint violation | rollback both, no M4 fake call |
| S03 | E synthetic | duplicate canonical key different Game/operation | second reservation rejected by UNIQUE, first permanent |
| S04 | E synthetic | same operation changed binding | mismatch rejected, no mutation |
| S05 | E synthetic | synthetic lease + confirmed fake receipt then TX-B | one op receipt + one claim state=1 |
| S06 | E synthetic | fake Unknown/timeout | no TX-B and no provider replay, HOLD reconciliation |
| S07 | E synthetic | TX-C | exactly one evidence, progress waiting, op Completed, claim state=2; no Coins |
| S08 | E synthetic | fault injection between TX-C updates | entire TX-C rolls back |
| S09 | E synthetic | two connections simultaneous Game / media | one authoritative claim; Game-first lock ordering |
| S10 | E synthetic | lost commit acknowledgment client side | fresh-connection reconciliation; unknown => manual HOLD |
| S11 | E synthetic | multi-row UPDATE/DELETE trigger stress | all attempted immutable mutations rejected atomically |
| S12 | E synthetic | malicious role attempts direct DML | rejected; no bypass of evidence/operation guard |
| S13 | E synthetic | terminal tombstone + attempted reuse | forever reserved even if rejected/expired |
| S14 | E synthetic | stale claim token/lease | TX-B refuses unless current confirmed token/lease |
| M4-01 | NOT IN E | real namespace/canonicalization vectors | pending M4 authoritative decision; no execution |
| M4-02 | NOT IN E | Game-purpose capture session/receipt | pending M4 contract; no real provider integration |
| H01 | NOT IN E | historical provenance/backfill | H0-H3 disabled; no historical mutation |

**Phase E synthetic tests require a separately reviewed test-runner/fault-injection implementation**; these definitions and procedures alone are **not a complete executed integration suite**. No provider is invoked by SQL procedures. Any skipped or missing test is **NOT VERIFIED**, never "passed".

Stop immediately for: wrong login/server/DB, absent manifest, unexpected data, collation/physical FK mismatch, failed row-count, duplicate ownership, trigger bypass, ambiguous commit, leaked permissions, unapproved SQL changes, scope creep to M4 or history.
