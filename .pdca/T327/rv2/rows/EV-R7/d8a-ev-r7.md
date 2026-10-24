# EV-R7 - D8a evidence (final)

Inline NUL six providers: PostgreSQL throws the prescribed `NotSupportedException` (inline and
`default(char)`, message names PostgreSQL and U+0000, the supplied value is not echoed, zero commands
executed); SQLite stores/reads NUL exactly (`CAST(X'00' AS TEXT)`); SQL Server `NCHAR(0)`; MySQL/MariaDB
`_utf8mb4 X'00'`; ClickHouse `'\x00'`. Captured `default(char)` is exact on every provider after the
ClickHouse char normalization (`EV02/d8a-clickhouse-char.md`). Logs: all `D8a-*.log`.

## D8b boundary

Full run: no NUL-case failure in any provider class. Boundary red for other reasons (DEF-T327-03/04); row continues.
