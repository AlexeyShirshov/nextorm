# EV06 - PostgreSQL U+0000 live guard

Green (10/10 facts). U+0000 is rejected on **both** paths with `NotSupportedException` naming
PostgreSQL and `U+0000`, never echoing the supplied value:

- inline translation (`Select(e => "<const>")` and `default(char)`), and
- the captured-parameter execution path (including a warm cached command).

An `IQueryInterceptor` on a dedicated context observed **zero executing commands** for both negative
paths. Other C0 values, Unicode/emoji and the 4096-character value round-trip exactly. INSERT/UPDATE
with NUL throw the same prescribed exception. Log: `D8a-postgres.log`.
