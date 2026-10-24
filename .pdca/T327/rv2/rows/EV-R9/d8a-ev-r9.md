# EV-R9 - captured/explicit NUL and second execution (final live acceptance)

All seven targeted suites green with 0 failed and 0 skipped (counts in
`.pdca/T327/rv2/rows/EV02/d8a-live-counts.md`):

- PostgreSQL: U+0000 rejected with the prescribed `NotSupportedException` on the inline and captured
  paths, including a warm cached command and `default(char)`; a safe execution after the NUL failure
  succeeds; zero executing commands observed for the negative paths.
- SQLite/SQL Server/MySQL/ClickHouse: captured NUL values are exact; the second NUL execution on a
  warm command is exact; recreated contexts transport the full corpus exactly.
- MySQL/MariaDB: the projection-parameter transport no longer depends on driver escaping; the
  previously failing mismatch cells are green across all 12 configurations.
- ClickHouse: captured `char` (including `default(char)`) now binds as a one-character string and is
  exact.
