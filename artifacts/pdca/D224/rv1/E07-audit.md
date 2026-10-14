# D224 E07 — SQL-emitter / no-CR / suppression audit (R2, R3)

## 1. SQL-emitter inventory

Raw scan (regenerated into `E07-emitters.txt` by `bash artifacts/pdca/D224/rv1/run-evidence.sh audit`):

```
$ rg -n --glob '*.cs' 'AppendLine' src            # 71 hits
$ rg -n --glob '*.cs' 'Environment\.NewLine' src  # 3 hits
```

Classification — SQL emission (must use `\n`) vs non-emission (logging / CSV / C#-codegen):

| Site | Count | Class |
|---|---|---|
| `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:909-1386` | 68 `emitter.AppendLine(...)` | **non-SQL**: C# *source-code generator* `StringBuilder`; its newlines never reach generated SQL bytes. |
| `src/nextorm.core/DataContext/ResultSetEnumerator.cs:335` | 1 | **non-SQL**: debug log text `sb.Append("Executing query: {sql}").AppendLine()`. |
| `src/nextorm.core/DataContext/ResultSetEnumerator.cs:344` | 1 | **non-SQL**: debug log text (`param {name_..}` rows). |
| `src/nextorm.core/DataContext/ResultSetEnumerator.cs:324` | 1 | **non-SQL**: log format literal `"Executing query: {sql}" + Environment.NewLine`. |
| `src/nextorm.core/DataContext/ResultSetEnumerator.cs:349` | 1 | **non-SQL**: `sb.Length -= Environment.NewLine.Length;` trims the log string built above (fed to `_logger.LogDebug` at `:350`), never to `CommandText`. |
| `src/nextorm.core/DataContext/SqlText.cs:7-8` | 2 | documentation `<see cref>` references only. |

There is **no `AppendLine()` / `Environment.NewLine` left in any SQL-text builder**. Generated
SQL assembles through `NextORM.Core.SqlText.NewLine` (`'\n'`) exclusively.
Roslyn `refs NextORM.Core.SqlText.NewLine` → **38 references**:
`SqlBuilder.cs` (28 sites: 163,198,223,231,236,279,319,342,352,361,372,403,427,439,446,460,470,695,
700,842,902,907,1009,1045,1122,1339,1344,1394), `SqlMutationBuilder.cs:1169`,
`SqlServerDialect.cs:690`, `ClickHouseExtremeRowRenderer.cs:86,88,91,94,110,112,115,118` (8).

## 2. No-CR evidence

Definition: `SqlText.NewLine = '\n'` — `src/nextorm.core/DataContext/SqlText.cs:16`.

```
$ rg -n --glob '*.cs' -F '\r' src   # 6 literal-\r sites, none a SQL line separator
```

- `src/nextorm.core/DataContext/SqlBuilder.cs:1532` — `case '\r':` inside `MakeTagComment`:
  it *detects* CR/LF in a comment **tag** and replaces it with a space (input sanitising), not
  emission.
- `src/nextorm.core/Query/Csv/CsvDialect.cs:13,49`; `Query/Csv/CsvStreamWriter.cs:172,201`;
  `Query/Csv/CsvRowBuffer.cs:304` — CSV record delimiter / quoting, unrelated to SQL text.

⇒ **no generated-SQL emission site writes CR**; every SQL newline is the explicit `'\n'`.
(Independently corroborated by the D160 raw-byte baselines and E02's "no generated CR" sweep.)

## 3. Suppression / slop scan

`slopwatch` is **not installed** in this environment (`command -v slopwatch` → empty), so the
Slopwatch CLI could not run. Replaced with a manual added-line scan of the DO commit
`4e6a2abde7d7513e4bfab0232c4bdf0bbba10443`; the numbers below are the raw regenerated
`E07-suppression.txt` produced by `bash artifacts/pdca/D224/rv1/run-evidence.sh audit`
(exit 0), which resolves the commit itself via `git rev-parse HEAD`.

```
$ git diff 4e6a2abd^ 4e6a2abd -- tests src     # 110 files changed, 692 insertions(+)
```

Scan definition (exactly as executed by `run-evidence.sh audit`):

```
scope        git diff 4e6a2abd^ 4e6a2abd -- tests src
denominator  git diff 4e6a2abd^ 4e6a2abd --numstat -- tests src  → 692 added lines / 110 files
anchor       ^\+[[:space:]]*<pat>          (added-line marker + optional indentation)
escaping     sed 's/[][\\.^$*+?(){}|]/\\&/g' on the fixed string, then `grep -cE`
empty catch  ^\+[[:space:]]*.*catch[[:space:]]*\{[[:space:]]*\}
```

All nine patterns are anchored as `^\+[[:space:]]*…`; the earlier form `grep -F "+<pat>"`
required the pattern at column 2 and therefore **missed every indented added line** (e.g. an
indented `[Fact(Skip = …)]`). Verified: `+        [Fact(Skip = "x")]` → 1 hit with the anchored
regex, 0 hits with the old `+<pat>` form.

| added-line pattern | hits |
|---|---|
| `^\+[[:space:]]*Skip =` | 0 |
| `^\+[[:space:]]*\[Fact\(Skip` | 0 |
| `^\+[[:space:]]*#pragma warning disable` | 0 |
| `^\+[[:space:]]*Assert\.True\(true\)` | 0 |
| `^\+[[:space:]]*\.Only\(` | 0 |
| `^\+[[:space:]]*SuppressMessage` | 0 |
| `^\+[[:space:]]*ExcludeFromCodeCoverage` | 0 |
| `^\+[[:space:]]*\[Obsolete` | 0 |
| `^\+[[:space:]]*.*catch[[:space:]]*\{[[:space:]]*\}` (empty `catch {}`) | 0 |
| **TOTAL (`# TOTAL_NEW_SUPPRESSIONS=`)** | **0 / 692 added lines** (ratio 0) |

The zeroes are non-vacuous: the recorded SHA resolves to the real DO commit
`4e6a2abde7d7513e4bfab0232c4bdf0bbba10443`, the denominator 692 matches the 110-file insertions
of the same diff, and the anchored form was shown above to catch indented added lines. Scan
scope is the DO commit *vs its parent* only (not the accumulated branch diff), as stated in the
`# scope:` header of `E07-suppression.txt`.
