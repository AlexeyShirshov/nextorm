# #327 D3 addendum — inline `char` constants take the string-literal policy

## Finding (exposed by the D8a live char arm)

D7's residual note assumed a `char` constant "flows through the same string policy" as a string.
It did not. `BaseExpressionVisitor.TryEmitValue` routed only `string`/`Guid` through
`RenderStringLiteral`; `char` is `IsPrimitive`, so it fell into the raw `Convert.ToString` branch and
was spliced into the statement as a bare token (`Select(e => 'a')` → `select a from …`). A `'` or `\`
broke the statement, and `default(char)` produced an incomplete statement. This was a silent
data-splicing hazard for the exact surface AC02/§8 lists ("each inline cell includes string and char").

## Fix (internal only, no role metadata added)

`src/nextorm.core/Visitors/BaseExpressionVisitor.cs` — `TryEmitValue` now treats `typeof(char)` like a
one-character string: `RenderStringLiteral(v.ToString()!)` in the ordinary `Value` role, exactly the
spelling of a one-character string with the same character.

| Provider | `'a'` | `'\''` | `'\\'` | `default(char)` | `'\u0001'` |
|---|---|---|---|---|---|
| SQLite | `'a'` | `''''` | `'\'` | `CAST(X'00' AS TEXT)` | raw U+0001 |
| SQL Server | `N'a'` | `N''''` | `N'\'` | `NCHAR(0)` | raw U+0001 |
| PostgreSQL | `E'a'` | `E'\''` | `E'\\'` | `NotSupportedException` (`*PostgreSQL*U+0000*`) | `E'\x01'` |
| MySQL | `_utf8mb4 X'61'` | `X'27'` | `X'5c'` | `X'00'` | `X'01'` |
| MariaDB | `_utf8mb4 X'61'` | `X'27'` | `X'5c'` | `X'00'` | `X'01'` |
| ClickHouse | `'a'` | `'\''` | `'\\'` | `'\x00'` | `'\x01'` |

Captured `char` parameters are untouched (the parameter transport already preserved the exact char,
including `default(char)` through the PostgreSQL final-value guard).

## Verification

- `dotnet build src/nextorm.core -c Debug` and the six provider projects: 0 warnings / 0 errors each.
- `dotnet test tests/nextorm.core.tests -c Debug`: 2843/2843 passed, 0 failed, 0 skipped.
- Six `InlineStringLiteralSqlGenerationTests` classes: sqlite 25, sqlserver 25, postgres 24,
  mysql 24, mariadb 24, clickhouse 24 — all 0 failed, 0 skipped.
- Surface audit (`D3-surface-audit.sh 5b651f38`): anchored public/protected declarations 5207 = 5207
  (diff exit 0), tokens 5413 = 5413, IVT 14 = 14; the only src path change is the internal
  `TryEmitValue` body. PASS.
- `git diff --check` exit 0; CRLF normalized on every touched `.cs` file.

Raw logs: `.pdca/T327/rv2/logs/D3-*.log`; shape-class logs: `.pdca/T327/rv2/logs/D3-shape-<provider>.log`.
