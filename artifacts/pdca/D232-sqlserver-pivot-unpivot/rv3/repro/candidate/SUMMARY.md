# D232 additive-fallback candidate evidence (r=3, rv=3, N=1)

Authorized by the re-escalated binding decision for r=3: **one additive fallback** in
`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, placed immediately before the
`throw new QueryPreparationException("Select must return new anonymous type")` (now `:930`).
No plan revision change (r stays 3), no `planner` authoring.

## R05 diagnosis (step 1)

R05 (pivot-free control) is green because `From(QueryCommand)` embeds the projection command
directly as the outer builder's source, so the outer `Select` wraps it in one derived layer whose
`SelectList` is non-null.

R01–R04 are red because `From(EntityBuilder<TShape>)` materialises the builder through its implicit
`QueryCommand` conversion (`ToCommand()`), which produces an **intermediate command with no
projection** (`Exp == null`, `SrcType = TShape`) whose `From` is a **derived subquery**
(`FromExpression(SubQuery)`, `ColumnShape == null`). In `PrepareColumns` that command falls into the
"no projection" branch, finds no entity metadata for the metadata-less shape, and throws
`QueryPreparationException: Select must return new anonymous type` — before the consumer's own
`Select` ever gets to resolve its members.

Control **R05b** (`ctx.From(ctx.From(inner))` — the same pivot-free projection command, consumed
through `From(EntityBuilder<T>)` + `Select`) is **red before the fallback with the identical
exception** (see `candidate-before/`). That confirms the defect is a **pre-existing general core
defect of the metadata-less derived builder**, not PIVOT-specific. A follow-up issue is warranted
for the general case (not created in this step; reported).

## Fallback gating

Fires only where the code previously threw: `selectList` still empty **after** the entity-metadata
path, `_from.ColumnShape is null`, `_from.SubQuery.SelectList is non-empty`, and the derived shape
is safely reusable (`TryGetReusableDerivedShape`: every column addressable by name, and no
row-member-access column expression).

- never writes `DataContextCache.SelectListCache` (a metadata-less `srcType` key would collide
  across different subqueries);
- never writes `queryCommand.Cache`, never touches `_dontCache`, never mutates the source command;
- existing branches `:550`/`:565`/`:631`/`:747`/`:856`/`:862` are untouched; no shared helper was
  extracted from them;
- no `Exp`-copying was added to `ToCommand()`; `DataContextExtensions.cs:1377` was **not** touched
  (not needed — all repros green after step 3).

**Tripwire status: not hit.** No existing branch condition/behaviour, no
`FromExpression`/`ColumnShape` for derived sources, no `ToCommand` for non-pivot builders and no
`SelectList` contract change was required.

A shape whose columns are row-member accesses (for example a metadata-less builder chained through
`As`) is **not** passed through: reusing such columns would re-enter derived-member translation while
the derived source is the command being rendered and recurse. The guard keeps that shape on the
previous throwing behaviour instead of turning it into a stack overflow.

## Before / after (isolated children, `verify.py repro-candidate`)

| Probe | before (C34, fallback absent) | after (C35, fallback present) |
|---|---|---|
| R01 typed builder `.Select` | red — `QueryPreparationException` | green |
| R02 typed builder + `Where` + `Select` | red — `QueryPreparationException` | green |
| R03 `From(typed QueryCommand).Select` | red — `QueryPreparationException` | green |
| R04 self-join of the typed command | red — `QueryPreparationException` | green |
| R05 pivot-free direct control | green | green |
| R05b pivot-free builder control | red — `QueryPreparationException` | green |

## Regressions

| Suite | argv | exit | total / passed / failed / skipped |
|---|---|---|---|
| core | `dotnet test tests/nextorm.core.tests -c Debug` | 0 | 2529 / 2529 / 0 / 0 |
| sqlite | `dotnet test tests/nextorm.sqlite.tests -c Debug` | 0 | 1747 / 1746 / 0 / 1 |
| postgres | `dotnet test tests/nextorm.postgres.tests -c Debug` | 0 | 976 / 976 / 0 / 0 |
| sqlserver | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | 0 | 946 / 940 / 0 / 6 |
| mysql | `dotnet test tests/nextorm.mysql.tests -c Debug` | 0 | 454 / 454 / 0 / 0 |
| mariadb | `dotnet test tests/nextorm.mariadb.tests -c Debug` | 0 | 336 / 336 / 0 / 0 |
| clickhouse | `dotnet test tests/nextorm.clickhouse.tests -c Debug` | 0 | 733 / 733 / 0 / 0 |
| alias | `dotnet test tests/nextorm.alias.tests -c Debug` | 0 | 147 / 147 / 0 / 0 |

The 6 sqlserver skips are exactly the Explicit R01–R05b repro probes; the 1 sqlite skip is the
pre-existing `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`.

Targeted filters (all `--no-build`, exit 0):

| Target | argv filter | total / passed / failed / skipped |
|---|---|---|
| frozen legacy pivot/unpivot + typed cases | `nextorm.sqlserver.tests --filter "FullyQualifiedName~Pivot\|FullyQualifiedName~Unpivot"` | 57 / 55 / 0 / 2 |
| `From`-into-derived consumers | `nextorm.postgres.tests --filter "FullyQualifiedName~DerivedSource\|FullyQualifiedName~ProjectedCommand\|FullyQualifiedName~AsThenJoin"` | 15 / 15 / 0 / 0 |
| sqlite derived/raw consumers | `nextorm.sqlite.tests --filter "FullyQualifiedName~QuotedIdentifiers_DerivedTable\|FullyQualifiedName~RawSourceBindingDiagnostics\|FullyQualifiedName~DerivedSource"` | 20 / 20 / 0 / 0 |
| root/join alias mirrors | `nextorm.alias.tests --filter "FullyQualifiedName~RootAlias\|FullyQualifiedName~JoinAlias"` | 95 / 95 / 0 / 0 |
| `JoinAliasMutationGapTests` + `Iteration14PrepareRegressionTests` | `nextorm.core.tests --filter "FullyQualifiedName~JoinAliasMutationGapTests\|FullyQualifiedName~Iteration14PrepareRegressionTests"` | 27 / 27 / 0 / 0 |

The 14 frozen legacy `SqlGenerationTests` pivot/unpivot cases are included in the 57-selected run and
pass (2 skips are the Explicit repro probes matched by the `Pivot` substring).

Build gate: `dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings / 0 errors
(`build-solution.out`).

## Frozen oracles

Compared by sha256 against `repro/production-before.json`: the only changed oracle file is
`tests/nextorm.sqlserver.tests/T232DerivedSourceReproTests.cs` (the R05b control added by the binding
decision). `T232ProjectionContractTests.cs`, `T232TypedPivotTests.cs`, `rv1/test-contract.json`,
`SqlGenerationTests.cs`, the integration fixtures and every production file except
`QueryCommand.QueryPreparer.cs` are byte-identical.
