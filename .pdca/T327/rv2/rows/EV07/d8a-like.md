# EV07 - LIKE against a native typed-parameter reference

Green on SQLite/PostgreSQL/SQL Server/MySQL (common suite) and on MySQL/MariaDB in the three
mode/flag cells where the parameter transport is correct. SQLite/SQL Server have no default LIKE
escape, so escaped patterns use the explicit 3-arg `like(..., "\\")`; the reference uses a typed
parameter plus the provider's native ESCAPE spelling (MySQL's is the mode-independent
`_utf8mb4 X'5c'`).

Patterns verified: `%`, `_`, `%\%` (literal wildcard), `%\_%`, `a\\b` (literal backslash),
`it's%`, `%'s%--`, Cyrillic, plus the LIKE corpus rows. Inline and captured patterns both equal the
native reference and the documented expected set.

ClickHouse (dedicated class): default backslash escaping matches the expected sets; a prepared
default-like statement contains no `escape` token; the custom escape `"!"` throws the product's
`NotSupportedException` (`*ClickHouse*ESCAPE*`). The ClickHouse class is 3/3 green.
