# EV03/EV-R2/EV-R4 - MySQL/MariaDB four modes x NoBackslashEscapes absent/false/true

12 cells per provider, one theory case each, full corpus through inline/captured/INSERT/UPDATE/LIKE
per cell. Final result: **12 passed / 0 failed / 0 skipped** for both MySQL and MariaDB.

The cells are exactly the cross-product of:
- sql_mode: `''`, `NO_BACKSLASH_ESCAPES`, `ANSI_QUOTES`, `NO_BACKSLASH_ESCAPES,ANSI_QUOTES`
- driver `NoBackslashEscapes`: absent, `False`, `True`
(including the mismatched combinations and a post-mode-change re-execution on the same connection).

The session mode is unchanged after every ORM operation (read back on the same connection), proving
the product issues no `SET`. The native LIKE reference in these classes transports the pattern as the
mode-independent `_utf8mb4 X'...'` literal and keeps the marker as a typed parameter, so the oracle
itself does not depend on driver text escaping.

Logs: `.pdca/T327/rv2/logs/D8a-mysql-modes.log`, `.pdca/T327/rv2/logs/D8a-mariadb.log`.

## D8b full fail-closed boundary (r=3, single run)

One canonical full-suite run, serialized under the collection container lease, with no `NEXTORM_SKIP_PROVIDERS`: Total 5352, Failed 7, Skipped 197, Not Run 0, exit 1; skip budget 197/197 (delta 0). Log `.pdca/T327/rv2/logs/D8b-full-boundary.log`, details in `.pdca/T327/rv2/rows/EV09/d8b-boundary.json`. The boundary is red: 2 product regressions (DEF-T327-03, DEF-T327-04), 4 stale expectations under the declared representation (DEF-T327-06), 1 pre-existing unrelated SQLite failure (DEF-T327-05).

The MySQL and MariaDB 12-cell classes are in the full run and emitted no `[FAIL]`/`[SKIP]` line (zero targeted skips), matching the D8a 12/12. The MySQL failure in the full run is `MySqlSpecificTests.StringAgg_ShouldConcatenateGroupValues` (DEF-T327-03, a GROUP_CONCAT `SEPARATOR` grammar regression), not a mode-matrix cell. EV03/EV-R2/EV-R4 stay open until the boundary is green.
