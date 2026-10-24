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

## D8b full fail-closed boundary (r=3, single run)

One canonical full-suite run, serialized under the collection container lease, with no `NEXTORM_SKIP_PROVIDERS`: Total 5352, Failed 7, Skipped 197, Not Run 0, exit 1; skip budget 197/197 (delta 0). Log `.pdca/T327/rv2/logs/D8b-full-boundary.log`, details in `.pdca/T327/rv2/rows/EV09/d8b-boundary.json`. The boundary is red: 2 product regressions (DEF-T327-03, DEF-T327-04), 4 stale expectations under the declared representation (DEF-T327-06), 1 pre-existing unrelated SQLite failure (DEF-T327-05).

No StringLiteralSafety roundtrip class failed in the full run; the seven failures listed in `EV09/d8b-boundary.json` are the remaining blocked rows.
