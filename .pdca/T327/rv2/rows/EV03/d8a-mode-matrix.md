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
