# EV04 - live ordinal roundtrip

Green on the four shared-suite providers (10/10 facts each): inline projection, captured parameter,
async parameter, INSERT/readback, UPDATE/readback, LIKE, null/default(char), long value, alternation,
recreated context and shared-cache flag. Every comparison is `actual == expected` plus exact UTF-8
bytes; no SQL length function is used as an oracle.

Exact values verified live include the 33 D1 strings, every individual non-NUL C0 code point, the
char set (inline and captured on four providers), 4096-character Unicode/control values, and
Unicode/emoji.

Final: all seven targeted suites green, zero failed and zero skipped. The previously blocked
MySQL/MariaDB projection parameter cells and the ClickHouse captured char cell are fixed and verified
(`EV03/d8a-mode-matrix.md`, `EV02/d8a-clickhouse-char.md`). Logs:
`D8a-{sqlite,postgres,sqlserver,mysql,clickhouse,mysql-modes,mariadb}.log`.
