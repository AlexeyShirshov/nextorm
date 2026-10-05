# E09 — CHECK mechanical audit (issue #166, branch 1.0.9-b)

Read-only gather for the nested-read-CTE warm-path change. No source edits. No stage/commit.
UTC: 2026-10-05T12:53Z. Base: working tree diff of #166.

## 1. Build baseline

Command (array): `["dotnet","build","nextorm.slnx","-c","Debug"]`

- exit code = **0**
- `Build succeeded.` — **0 Warning(s) / 0 Error(s)**
- log: `docs/specs/status/nested-read-cte-1-evidence/E09-build.log`

## 2. Suppression / slop scan

Tooling: `slopwatch` is **NOT installed** — absent from `.config/dotnet-tools.json`
(only `dotnet-coverage`, `dotnet-reportgenerator-globaltool`, `docfx`) and from `dotnet tool list`.
Scan is **manual** (`git grep`, ignore-aware; no `grep -r`/`find`).

Added by the #166 diff (added lines only, both changed files): **0** for every marker.

Pre-existing counts in `src/nextorm.core`:

| marker | added by diff | pre-existing in core |
|---|---|---|
| `#pragma warning disable` | 0 | 5 |
| `[SuppressMessage]` | 0 | 6 |
| `<NoWarn>` | 0 | 0 |
| `Skip=` | 0 | 0 |
| empty `catch { }` | 0 | 0 |
| `Task.Delay` | 0 | 0 |
| `TODO` | 0 | 4 |
| `FIXME` | 0 | 0 |
| `HACK` | 0 | 0 |

Pre-existing `#pragma`/`SuppressMessage` sites (all carry inline justification):
`EntityBuilderExtensions.cs:286,303,319`; `DbPreparedQueryCommand.cs:110`;
`InMemoryLinqSource.cs:82`; `QueryExecutor.cs:256`; `SelectExpression.cs:13`;
`ExpressionPlanEqualityComparer.cs:594`; `AggregateTerminalRewriter.cs:12`;
`CorrelatedQueryExpressionVisitor.cs:9,10`.

Suppression ratio (added / pre-existing directives): **0 / 11**
(0+0+0 added; 5 `#pragma` + 6 `[SuppressMessage]` + 0 `<NoWarn>` pre-existing).

## 3. Active analyzer severities

`.editorconfig` has **7** `dotnet_diagnostic.*.severity` entries (all `silent`):
S125, S108, CA2254, S3060, S1104, S3604, S2292.

The diff touches no `.editorconfig` (not in `git status`); it adds only two comment
blocks plus `if (IsPrepared) return false;`, so it activates **no** new diagnostic.

## 4. Public API surface

`roslyn members NextORM.Core.QueryCommand` → `HasDataModifyingCte` is
`internal property … .cs:504` with `internal method …get … .cs:506`. The diff changes
only the getter body — **no public type or member added/changed**.

Changed symbols:
- `NextORM.Core.QueryCommand.HasDataModifyingCte` — internal getter body (QueryCommand.cs:504-554)
- test project (no public API surface): `CteIdRow`, `NestedReadCteWarmReuse_AllocatesZero`,
  `CteReuse_VariantMatrix`, `NestedReadCte_PreservesSqlParametersAndPlanKey`,
  `NestedReadCte_SyncAsyncPolicyMatches`, helpers `BuildFlatRead`, `BuildNestedReadTyped`,
  `BuildFlatMutation`, `BuildNestedMutation`

XML-doc / CS1591: `src/Directory.Build.props:11` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>`;
CS1591 is not `NoWarn`-suppressed anywhere, so it is active for `src/**`. Because the diff adds
no public member, **no new public member requires docs** (build 0 warnings confirms).

## 5. Branch coverage of the changed file (existing E05 artifacts, not re-run)

Source: `cov-core.cobertura.xml` + `c5-report/nextorm.core_QueryCommand.html`.

- per-file `src/nextorm.core/Query/QueryCommand.cs`: line **86.56 %** (0.8656), branch **58.47 %** (0.5847), complexity 118.
- changed getter `get_HasDataModifyingCte()` (File 2, line 507): line **100 %**, branch **100 % (22/22)**.
- `HasDataModifyingCteIn`: line 100 %, branch 100 % (12/12).

Getter branches and the test exercising each (coverage rows: L511 4/4, L516 2/2, L523 2/2,
L525 4/4, L532 2/2, L539 2/2, L545/L548 covered):

| branch | test(s) |
|---|---|
| flat / no-cte (`_ctes` empty → false) | `CteReuse_VariantMatrix` flat variants |
| mutation-hit (`Mutation is not null` → true) | `CteReuse_VariantMatrix` mutation variants, `NestedMutation_DisablesReadReuse` |
| nested && !prepared (recurse) | `CteReuse_VariantMatrix` nested-cold variants, `CyclicNestedGraph_TerminatesAndFindsMutation` |
| nested && prepared (`IsPrepared` early return, NEW) | `NestedReadCteWarmReuse_AllocatesZero`, `NestedReadCte_PreservesSqlParametersAndPlanKey`, `NestedReadCte_SyncAsyncPolicyMatches`, matrix nested-prepared variants |
| recursion cycle (visited set terminates) | `CyclicNestedGraph_TerminatesAndFindsMutation`, `CyclicNestedGraph_WithDeepMutation_TerminatesAndFindsMutation` |

## 6. Diff size / scope

- `git diff --stat -- src/nextorm.core tests/nextorm.core.tests`: 2 files, +159 / -2
  (`QueryCommand.cs` 9+/2-, `Iteration14CteLookupTests.cs` 150+/0-).
- `git diff --check` exit code = **0** (no whitespace errors).
- only the two expected files changed in the scoped dirs (`git diff --name-only`).
- CRLF preserved: `file` reports both changed files "with CRLF line terminators";
  `QueryCommand.cs` has 929 CRLF lines = 929 total lines (no LF-only lines).

Scope: scoped to `src/nextorm.core` + `tests/nextorm.core.tests`, only
`QueryCommand.cs` (internal `HasDataModifyingCte` getter body) and
`Iteration14CteLookupTests.cs` (tests) changed; added suppressions **0**, ratio **0/11**.
