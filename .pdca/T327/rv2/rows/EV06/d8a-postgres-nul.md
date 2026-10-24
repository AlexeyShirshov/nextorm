# EV06 - PostgreSQL U+0000 live guard

Green (10/10 facts). U+0000 is rejected on **both** paths with `NotSupportedException` naming
PostgreSQL and `U+0000`, never echoing the supplied value:

- inline translation (`Select(e => "<const>")` and `default(char)`), and
- the captured-parameter execution path (including a warm cached command).

An `IQueryInterceptor` on a dedicated context observed **zero executing commands** for both negative
paths. Other C0 values, Unicode/emoji and the 4096-character value round-trip exactly. INSERT/UPDATE
with NUL throw the same prescribed exception. Log: `D8a-postgres.log`.

## D8b full fail-closed boundary (r=3, single run)

One canonical full-suite run, serialized under the collection container lease, with no `NEXTORM_SKIP_PROVIDERS`: Total 5352, Failed 7, Skipped 197, Not Run 0, exit 1; skip budget 197/197 (delta 0). Log `.pdca/T327/rv2/logs/D8b-full-boundary.log`, details in `.pdca/T327/rv2/rows/EV09/d8b-boundary.json`. The boundary is red: 2 product regressions (DEF-T327-03, DEF-T327-04), 4 stale expectations under the declared representation (DEF-T327-06), 1 pre-existing unrelated SQLite failure (DEF-T327-05).

No PostgreSQL NUL-guard assertion failed in the full run. The two PostgreSQL failures are declared-representation expectations (`Postgres/PostgresJsonAccessExecutionTests.cs:789`, `Postgres/PostgresSpecificTests.cs:518`), not guard behaviour.
