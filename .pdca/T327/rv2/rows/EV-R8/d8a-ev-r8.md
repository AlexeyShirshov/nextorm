# EV-R8 - D8a evidence (final)

Every non-NUL C0 code point U+0001..U+001F is an individual corpus case and round-trips exactly
inline and as a captured parameter on every tested provider (PostgreSQL `E'\xNN'`, ClickHouse
`'\xNN'`, the others verbatim). Captured char values, including U+0001 and U+001F, are exact after the
ClickHouse normalization. All seven targeted suites green with zero failed and zero skipped.

## D8b boundary

Full run: no C0-case failure in any provider class. Boundary red for other reasons (DEF-T327-03/04); row continues.
