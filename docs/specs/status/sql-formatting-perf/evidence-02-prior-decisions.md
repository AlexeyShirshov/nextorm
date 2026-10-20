# Evidence 02 — prior records about generated-SQL text layout (single-line vs multi-line)

Repo: `/home/alex/sources/nextorm` (branch `1.0.9`, HEAD `0b9ff9fc`, collected 2026-10-12 20:31 +05).
Read-only fact gathering. No product code or test was modified.

Note: sibling `evidence-01-nextorm-sql-build-cache.md:3` claims HEAD `c689f518`; the current tree is
`0b9ff9fc`. Both files are untracked (`git status --porcelain docs/specs/status/sql-formatting-perf/`
=> `?? docs/specs/status/sql-formatting-perf/`; `git ls-files` for that path returns nothing).

## Question

Was the generated-SQL text layout ever the subject of a recorded decision — single-line vs multi-line,
whether `from`/`join` sit on the SELECT line or a new line, indentation, and which newline character?
Does any existing issue/design record already cover it?

## Commands

All run from `/home/alex/sources/nextorm`.

```
ls docs/specs/design/ docs/specs/status/ docs/specs/roadmap/ docs/superpowers/
find .github -type f
rg -n -i -e 'AppendLine' -e 'Environment\.NewLine' -e 'newline' -e 'pretty' -e 'indent' \
   -e 'golden sql' -e 'exact sql' -e 'SQL formatting' -e 'SQL layout' -e 'single-line' -e 'multi-line' \
   docs/ .github/ AGENTS.md
rg -n -i -e 'whitespace' -e 'line break' -e 'readab' -e 'human-readable' -e 'SQL formatting' docs/specs/ docs/superpowers/
rg -n -i -e 'AppendLine' -e 'newline' -e 'SQL layout' -e 'formatting' -e 'SqlBuilder' AGENTS.md
rg -n -i -e '//.*format' -e '//.*layout' -e '//.*indent' -e '//.*readab' -e '//.*newline' \
   src/nextorm.core/DataContext/SqlBuilder.cs src/nextorm.core/DataContext/SqlSourceRenderer.cs
git log --all --oneline -i --grep='format' --grep='newline' --grep='pretty' --grep='indent' --grep='layout' --grep='multiline' --grep='NewLine'
find docs -type f \( -iname '*format*' -o -iname '*pretty*' -o -iname '*layout*' -o -iname '*readab*' \)
rg -n -i -e 'byte-exact' -e 'byte.for.byte' -e 'golden' tests -g '*.cs'
```

## Facts

### 1. Only prior record is the sibling evidence file (no decision)

- `docs/specs/status/sql-formatting-perf/evidence-01-nextorm-sql-build-cache.md` is the only
  in-tree record that names the clause layout. It **describes** today's shape and lists **open
  questions**, it does not record a decision:
  - `:81-82` "Concise shape today: `select <cols> from <src> <joins>` then each later clause
    (`where`, `group by`, `having`, `order by`, union, paging, ...) starts on a new line; there is no
    per-clause indentation."
  - `:215-223` `## Open questions` incl. "Is a formatted (multi-line, indented) SQL string intended to
    be produced on the cold/build path only..." and "Are there integration/acceptance tests asserting
    exact SQL text that would pin the current whitespace".
- No file under `docs/specs/design/`, `docs/specs/roadmap/`, `docs/specs/performance/`,
  `docs/specs/experiments/` or `docs/superpowers/` was found whose subject is the SELECT/FROM/JOIN
  layout, indentation, or newline character. (Filename scan for `*format*`/`*pretty*`/`*layout*`/
  `*readab*` under `docs/` returned only unrelated API/coverage artifacts, e.g.
  `docs/api/NextORM.Core.IStringFormatFunctions.yml`.)

### 2. A separate, deliberate multi-line feature exists — but for batch `ToSql()`, not clause layout

- `DataContextBuilder.UseMultilineBatchSql()` is a recorded, deliberate multi-line decision — but it
  controls the separator **between statements in a batch**, not `from`/`join` clause placement:
  - `docs/guide/25-sql-batch.md:150` "By default `ToSql()` prints the statements on one line separated
    by `; `; enable `DataContextBuilder.UseMultilineBatchSql()` to render each statement on its own line,
    as shown here."
  - `docs/specs/design/API-NAMING-REVIEW.md:4755` "### Аудит 24.09.2026 — многострочный батч-`ToSql`
    (`MultilineBatchSql`) ..." (P0 none, P1 names none; MBS1 P2, MBS2 informational).
  - `docs/specs/design/API-NAMING-REVIEW.md:4758` maps the surface: `DataContextBuilder.MultilineBatchSql`
    (`src/nextorm.core/DI/DataContextBuilder.cs:55`), `UseMultilineBatchSql(bool)` (`:159`),
    `IContextEnvironment.MultilineBatchSql` (`DataContext/Roles/IContextEnvironment.cs:56`).
  - `evidence-01:181-182` ties the separator to a char: `src/nextorm.core/DataContext/Roles/IBatchExecutor.cs:227`
    `separator = multiline ? '\n' : ' '`.
- This is not a decision about the single-query clause layout or its newline character.

### 3. AGENTS.md — no statement that SQL text layout is intentional

- The only `Layout` heading in `AGENTS.md` (`AGENTS.md:26` `## Layout`) is about source-file layout
  ("Core file map ...", `:28`), not about generated SQL text. No mention of `AppendLine`,
  `Environment.NewLine`, `formatting`, `SQL layout`, or clause placement.

### 4. No code comment near SqlBuilder.cs marks the layout as intentional

- `rg` for layout/format/indent/readable/newline comments in `SqlBuilder.cs` and
  `SqlSourceRenderer.cs` returned only comments about *CLR member readability*, not SQL text layout:
  - `src/nextorm.core/DataContext/SqlBuilder.cs:1939` "// exists, otherwise the slot's readable CLR surface (a metadata-less derived/read-CTE joined"
  - `src/nextorm.core/DataContext/SqlSourceRenderer.cs:824` "/// mapped row type: <c>name type, name type</c> over the row type's readable properties. The column"
- No comment states the SQL whitespace/layout must stay a given shape.

### 5. Exact-SQL-string tests: some are described as a frozen/byte-exact contract, others normalize newlines

- Some SQL-generation tests are explicitly described as **frozen/golden/byte-exact contracts**:
  - `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs:9` "The
    canonical shape is uppercase keywords with quoted identifiers, matching the frozen contract."
  - `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs:139-142` "// D196 (#196): the
    verbatim table name must survive byte-for-byte through all eight ... The expected SQL is literal
    golden data: only the verbatim name is substituted, every keyword, column, quoted identifier and
    value is written out, and no form carries a trailing semicolon."
  - `tests/nextorm.sqlite.tests/PositionalChainSqlInvariantTests.cs:10-14` "Test-only: it regenerates the
    frozen positional-only join chain on the current (revised) tree and compares the generated SQL
    <b>byte-for-byte</b> against the committed baseline corpus captured at HEAD <c>0495ee84</c> ...
    a single differing byte fails the test."
  - `tests/nextorm.sqlite.tests/PositionalChainSqlInvariantTests.cs:57-58` "Raw, un-normalised
    <c>CommandText</c>: the corpus is compared byte-for-byte, so aliases and ordinals must survive
    exactly as the renderer produced them."
  - `tests/nextorm.integration.tests/SqliteSpecificTests.cs:1962,1996` "the rendered SQL must match the
    canonical FTS5 control form byte-for-byte".
- The ordinary per-provider `SqlGenerationTests` classes do **not** state a layout contract in their
  class summary (e.g. `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:12-16` only says the tests
  never open a database); but they do assert exact literal strings and normalize only the newline
  characters:
  - `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:19`
    `private static string Normalize(string sql) => sql.Replace("\r\n", "\n");`
  - `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:183` "// Regression guard: an entity without a
    dynamic store must render the exact pre-change SQL."

### 6. Committed golden SQL baseline corpus exists and pins current whitespace (byte-for-byte)

- `tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite/` holds 16 tracked files
  (`git ls-files tests/nextorm.sqlite.tests/Baselines/D160/ | wc -l` => `16`), `<name>.sql` +
  `<name>.sha256` sidecars.
- The committed `01-inner.sql` content is a single-line JOIN:
  `tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite/01-inner.sql`:
  `select t2.id from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id`
- `PositionalChainSqlInvariantTests.CompareToBaseline` asserts `revisedBytes.Should().Equal(baselineBytes...)`
  and that the SHA-256 sidecar matches (`PositionalChainSqlInvariantTests.cs:83-98`).

### 7. Newline character in emitted SQL is `Environment.NewLine` via `AppendLine()`

- `evidence-01:52-54` records the layout primitive: "Newlines are already emitted for many clauses using
  `StringBuilder.AppendLine()` (which appends `Environment.NewLine`)" with occurrences
  `SqlBuilder.cs:211, 219, 224, ...`. No record was found that chooses a fixed newline character
  (`\n` vs `\r\n`) for the generic clause separation: the value is whatever `Environment.NewLine` is on
  the host, and `SqlGenerationTests.Normalize` collapses `\r\n` to `\n` for comparison.

### 8. Labels available in the repo config

- `.github/` contains only two files (`find .github -type f`): `.github/workflows/dotnet.yml`,
  `.github/workflows/docs.yml`.
- There is **no** `.github/ISSUE_TEMPLATE/` directory ("NO .github/ISSUE_TEMPLATE"), and **no**
  `labels.yml`/label-configuration file anywhere (`find . -name 'labels*.yml'` => empty).
- The only label name recorded in-tree is `enhancement`, seen as metadata of real GitHub issues:
  - `docs/superpowers/plans/2026-10-04-linq-phase-0-plan.md:8` "OPEN, milestone **1.2-a.1**
    (milestone/10), label `enhancement`".
  - `docs/specs/status/rc2-177-native-json-1.md:6` "milestone verified: #177 open, label enhancement,
    milestone 1.0.9-rc2".
  - `docs/specs/status/issue-222-evidence/evidence-01-alias-join-surface.md:29`
    `"labels":[{"name":"enhancement"}],"number":222,...`.
  - Others naming the same label: `docs/specs/status/rc2-159-cte-direct-joins-1.md:4`,
    `rc2-167-csv-chunked-read-1.md:15`, `rc2-180-json-stream-provider-conversion-1.md:4`,
    `rc1-126-tuple-ctor-1.md:23`, `rc1-168-mssql-boxing-1.md`,
    `docs/specs/comparison/evidence-01-eager-load-gaps.md:233` (raw `gh issue view 20` JSON).

## Not found

- **No record found** anywhere under `docs/specs/design/**`, `docs/specs/status/**`,
  `docs/specs/roadmap/**`, `docs/superpowers/**`, `AGENTS.md`, or code comments stating a decision
  that the generated SQL clause layout (single-line vs multi-line, `from`/`join` line placement,
  indentation) is intentional or must stay a given shape.
- **No record found** about which newline character to emit in generated SQL (`\n` vs `\r\n` vs
  `Environment.NewLine`); the only concrete statement is that `AppendLine()` is used.
- **No issue/design record found** that covers "add linq2db-style formatting" — no file, commit
  message (`git log --all -i --grep='format|newline|pretty|indent|layout|multiline'` returned only an
  unrelated `#211 ... coverage re-snapshot`, `#185`, etc.) or doc title matched.
- **No label configuration file** in the repo (no `.github/ISSUE_TEMPLATE/`, no `labels.yml`); the repo
  cannot enumerate its labels from tracked files.
- `git log --all -- docs/specs/status/sql-formatting-perf/*` returned nothing (the directory is untracked).

## Open questions

- Is the clause layout (`select ... from ... joins`, then later clauses on new lines) an unwritten
  convention, or is it pinned only by the ordinary `SqlGenerationTests` exact-string assertions
  (which normalize newlines, so `\n` vs `\r\n` is not pinned) plus the `D160` byte-exact baseline
  corpus (which does pin raw bytes for 8 join cases)?
- Should the raw/un-normalized `PositionalChainSqlInvariantTests` corpus and the FTS5 "frozen contract"
  be read as the authoritative pin on current whitespace for those shapes, and are they the only
  byte-exact pins?
- Which newline character is the intended contract, given `AppendLine()` uses `Environment.NewLine`
  while the committed `D160` baselines (written on this Linux host) use `\n`?
- Does the existing `UseMultilineBatchSql()` batch feature (opt-in, `\n` separator) set any precedent
  for the single-query layout, or is it considered unrelated?
