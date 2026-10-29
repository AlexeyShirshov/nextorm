# EV-R1 - D8a evidence (final)

MySQL/MariaDB adjacent/trailing-backslash corpus (including `\'`, `\\'`, `'\`, `a\`, `a\\`):
exact inline, captured, INSERT/readback and UPDATE/readback in all 12 mode x NoBackslashEscapes
configurations on both providers (12/12 passed, 0 failed, 0 skipped each). Logs:
`D8a-mysql-modes.log`, `D8a-mariadb.log`; see `EV03/d8a-mode-matrix.md`.

## D8b boundary

Full run: MySQL/MariaDB 12-cell classes present, 0 failed, 0 skipped (DEF-T327-03 is a separate GROUP_CONCAT case, not this corpus). Boundary red overall; row continues.
