# Branch / mutation map — T218 (#218) n=3 corrective closure (row `:91`)

Bound to the corrective revision (test-only): no production file changes; `src/` byte-identical to
`22b96233` (`git diff 22b96233..HEAD -- src` → 0 files). Evidence revision = the n=3 corrective commit
(see `source-identity.txt`).

## (a) Added/changed branch → test or guard

New public markers — `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`:

| branch / surface | file:line | test or guard |
|---|---|---|
| `next_value_for(string) → long` marker | `SqlFunctions.SqlServer.cs:294` | `SequenceFunctions_ShouldReturnLong` (`SqlServerFunctionsSqlGenerationTests.cs:761`) asserts CLR return type `long` |
| `nextval(string) → long` alias marker | `SqlFunctions.SqlServer.cs:301` | same + `SequenceFunctions_ShouldEmitNextValueForForBothAliases` (`:624`) |
| name-set membership (both aliases) | `SqlServerScalarFunctionTranslator.cs:60` | `Functions_ShouldBeGated` (`:591`), both-alias test (`:624`) |
| `SequenceFunctions` sequence-name-set guard | `SqlServerScalarFunctionTranslator.cs:90-93` | both-alias test (`:624`); mutation **M3** |
| provider gate: capability `null` / `Supports(name)` false → `NotSupportedException` | `SqlServerScalarFunctionTranslator.cs:106-107` | provider rejection suites (`tests/nextorm.{postgres,mysql,mariadb,clickhouse,sqlite}.tests/…RejectionTests`), in-memory guard `InMemoryScalarFunctionsTests.cs:98`; mutation **M1** |
| sequence branch taken **before** generic argument traversal | `SqlServerScalarFunctionTranslator.cs:109-113` | `SequenceFunctions_ShouldNotBindNameAsParameter` (`:745`) |
| arity guard `args.Count != 1` | `SqlServerScalarFunctionTranslator.cs:171-172` | `SequenceFunctions_Guards_ShouldThrow` (`:699`) |
| constant-string requirement (`TypeFacts.UnwrapConvert` / `ConstantExpression`) | `SqlServerScalarFunctionTranslator.cs:174-175` | `SequenceFunctions_NonConstantName_ShouldThrow` (`:732`) |
| `IsParamMode` early return (name never a parameter) | `SqlServerScalarFunctionTranslator.cs:179-180` | `SequenceFunctions_ShouldNotBindNameAsParameter` (`:745`) |
| `NeedAliasForColumn` + `Render` emit | `SqlServerScalarFunctionTranslator.cs:182-183` | both-alias (`:624`), schema (`:644`) |
| empty raw name guard | `SqlServerScalarFunctionTranslator.cs:194-195` | `SequenceFunctions_Guards_ShouldThrow` (`:699`) |
| NUL guard | `SqlServerScalarFunctionTranslator.cs:197-198` | `SequenceFunctions_Guards_ShouldThrow` (`:699`) |
| `.`-split | `SqlServerScalarFunctionTranslator.cs:200` | schema test (`:644`) |
| empty-component guard | `SqlServerScalarFunctionTranslator.cs:203-207` | `SequenceFunctions_Guards_ShouldThrow` (`:699`) |
| component length > 128 guard | `SqlServerScalarFunctionTranslator.cs:209-210` | `SequenceFunctions_Guards_ShouldThrow` (`:699`, incl. 128 boundary accept) |
| per-component `QuoteIdentifier` | `SqlServerScalarFunctionTranslator.cs:212` | escaping test (`:660`); mutation **M4** |
| `> 2` components guard | `SqlServerScalarFunctionTranslator.cs:215-216` | `SequenceFunctions_Guards_ShouldThrow` (`:699`) |
| `Render` both aliases → `next value for {args[0]}` | `SqlServerDialect.cs:1130` | both-alias (`:624`), schema (`:644`); mutation **M2** |
| `Supports` membership `next_value_for`/`nextval` | `SqlServerDialect.cs:1045` | `Functions_ShouldBeGated` (`:591`) |
| identifier quoting `[name]` with `]`→`]]` (reused) | `SqlServerDialect.cs:171` | escaping test (`:660`); mutation **M4** |

## (b) Mutation outcomes — each RED → GREEN (live exit codes, product restored)

Product files were mutated byte-level, built, tested (RED), restored with `git checkout --`, rebuilt and
re-tested (GREEN). `src/` is clean afterwards (`git status --porcelain -- src` empty). Logs:
`logs/n3-m<N>-{build-red,red,build-green,green}.log`.

Build A: `["dotnet","build","tests/nextorm.postgres.tests","-c","Debug"]`
Build B: `["dotnet","build","tests/nextorm.sqlserver.tests","-c","Debug"]`
Test P: `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~SqlServerFunctionsRejectionTests"]`
Test S: `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~SqlServerFunctionsSqlGenerationTests"]`

| # | mutation | file | RED | GREEN |
|---|---|---|---|---|
| **M1** | remove the provider gate (drop the `null`/`Supports` check → `var functions = …!;`) | `SqlServerScalarFunctionTranslator.cs:106-107` | `Build A`=0; `Test P`=**2** — `3 failed / 0 succeeded` (`NotSupportedException` no longer thrown; postgres now throws `NullReferenceException: Object reference not set…`) | `Build A`=0; `Test P`=**0** — `3 succeeded / 0 failed` |
| **M2** | corrupt the `NEXT VALUE FOR` render (`next value for` → `nextvaluefor`) | `SqlServerDialect.cs:1130` | `Build B`=0; `Test S`=**2** — `6 failed / 27 succeeded` (e.g. `…ShouldNotBindNameAsParameter`: expected `next value for [order_seq]`, got `nextvaluefor [order_seq]`) | `Build B`=0; `Test S`=**0** — `33 succeeded` |
| **M3** | break alias dispatch (drop `nextval` from `SequenceFunctions`) | `SqlServerScalarFunctionTranslator.cs:90-93` | `Build B`=0; `Test S`=**2** — `4 failed / 29 succeeded` (`NonConstantName_ShouldThrow`: no exception; escaping case rendered `next value for 'x]; drop table t; --'`) | `Build B`=0; `Test S`=**0** — `33 succeeded` |
| **M4** | bypass name guarding (drop the `]]` escaping in `QuoteIdentifier`) | `SqlServerDialect.cs:171` | `Build B`=0; `Test S`=**2** — `1 failed / 32 succeeded` (`…ShouldEscapeClosingBracketAndNotEscapeTheIdentifier`: got `[s]q].[t]`, expected `[s]]q].[t]`) | `Build B`=0; `Test S`=**0** — `33 succeeded` |

Each mutation is detected by at least one executed assertion and every mutated branch has a covering
test; the corrected sources restore GREEN. No product change is committed.

## Variant guard confirmation (CHECK re-gather round 2) — rows `:83` and `:90`

Appended by the check owner; the table above is unchanged. Full artifact:
`variant-guard-confirmation.md`.

- **Row `:83` (reference-type result guard)** — the two public markers return only CLR `long`; there is
  no reference-type overload. `roslyn members NextORM.Core.SqlServerFunctions` lists exactly one
  `next_value_for(string)` and one `nextval(string)`; sources `SqlFunctions.SqlServer.cs:294,:301` are
  `public long`; `SequenceFunctions_ShouldReturnLong` (`SqlServerFunctionsSqlGenerationTests.cs:761`)
  asserts `ReturnType == typeof(long)`. Covered by the "`next_value_for(string) → long` marker" and
  "`nextval(string) → long` alias marker" rows above.
- **Row `:90` (cache/default preparation control)** — the T218-only source delta
  (`git diff f8caa690..HEAD -- src`) is exactly the three files above; it contains no `QueryCommand`
  / `_dontCache` / `Builders/**` / `Cache = false` change (`git diff f8caa690..HEAD -- src | rg
  "QueryCommand|_dontCache|Builders/|Cache = false"` → no match), so T218 does **not** mutate shared
  `QueryCommand` state or the sticky `_dontCache`. The `QueryCommand`/`Builders` hunks visible in the
  wider `58025147..HEAD` range belong to T216/T217/T213 and are not T218. The preparation path through
  the D1-changed translator is exercised by the E08 `t218-preparation` control benchmark
  (`Prepare_Control_Patindex_Uncached`), whose matched-job A/B found no product regression (E08 `met`,
  commit `5ea7e50d`).
