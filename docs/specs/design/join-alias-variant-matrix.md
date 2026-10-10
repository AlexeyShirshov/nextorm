# Join-alias variant matrix (issue #113, Block A)

Evidence map for the join-alias feature (milestone `1.0.9-b`; extended by issue #160): every
behavioural variant of the alias chain is tied to the test or the runtime guard that proves it, with
`file:line`. The original owner-decided surface was **alias-only** chains (no positional mixing); #160
supersedes that with **free positional/alias mixing** and the **root alias** (`.WithAlias`), keeping
`JoinInto` excluded and SQL providers only (in-memory fails closed on any alias). Public API surface is
frozen by `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs`; API-naming register is
`docs/specs/design/API-NAMING-REVIEW.md`.

Legend: **T** = test, **G** = runtime guard (fail-closed `throw`). Gaps are listed at the end.

## 1. Operator surface (7 projection operators)

| Variant | Evidence | Location | Status |
|---|---|---|---|
| `Join` alias renders like positional inner join | T | `tests/nextorm.alias.tests/AliasSqliteEndToEndTests.cs:53` | covered |
| `LeftJoin` alias | T | `AliasSqliteEndToEndTests.cs:74` | covered |
| `RightJoin` alias | T | `AliasSqliteEndToEndTests.cs:95` | covered |
| `FullJoin` alias | T | `AliasSqliteEndToEndTests.cs:116` | covered |
| `CrossJoin` alias (conditionless) | T | `AliasSqliteEndToEndTests.cs:137` | covered |
| `CrossApply` alias (conditionless) | T (type-level only) | `AliasGeneratedSurfaceTests.cs:78` | **partial** |
| `OuterApply` alias (conditionless) | T (type-level only) | `AliasGeneratedSurfaceTests.cs:78` | **partial** |
| Correlated `CrossApply`/`OuterApply` alias (`AliasJoinApply`) | T (in-memory refusal only) | `AliasInMemoryRefusalTests.cs:26-27` | **partial** |
| All 7 operators emitted by the generator | T | `AliasGeneratedSurfaceTests.cs:78` (`JoinOperators`, `:20-21`) | covered |
| Generator operator set = the 7 only | source | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:46` (`JoinOperators`) | covered |

## 2. Chain shape & slot resolution

| Variant | Evidence | Location | Status |
|---|---|---|---|
| Alias received from `From<T>` (`EntityBuilder<T>` receiver) | T | `AliasSqliteEndToEndTests.cs:23-24` | covered |
| Alias→alias chaining (Buyer→Approver) | T | `AliasSqliteEndToEndTests.cs:24-25`; `AliasProjectionShapeTests.cs:29-31` | covered |
| Repeated CLR type → distinct slots `t2`/`t3` | T | `AliasSqliteEndToEndTests.cs:15` (`:39-43`) | covered |
| `ItemN` retained alongside alias members | T | `AliasProjectionShapeTests.cs:15` (`:18-22`) | covered |
| Alias member maps to the right slot (`JoinSlotAttribute`) | T | `AliasProjectionShapeTests.cs:45` (`:49-50`) | covered |
| 1-based attribute ↔ 0-based engine slot (`Buyer`=2→1, `Item2`=1) | T | `AliasProjectionShapeTests.cs:54-57` | covered |
| Slot resolution cached per projection (repeated types) | source+T | `src/nextorm.core/DataContext/ProjectionAliasCache.cs:85` (`GetOccurrence`), `:94` (`BuildMap`); exercised by `AliasProjectionShapeTests.cs:60` | covered |
| `ItemN` fast-path parsed before `JoinSlotAttribute` probe | source | `ProjectionAliasCache.cs:43-49` | covered |
| Alias name ending in digits is not misread as `ItemN` | source | `ProjectionAliasCache.cs:61` (`TryParseItemPosition`, `:64` length/prefix check) | **gap** (no direct test) |
| Alias column alias derived from projection member | source+T | `src/nextorm.core/Visitors/AliasFromProjectionVisitor.cs:27` (`VisitMember`), `:45` (`ResolveAliasCore`); asserted via SQL text `AliasSqliteEndToEndTests.cs:41-42` | covered |
| Column position→occurrence in translator | source | `src/nextorm.core/Visitors/MemberTranslator.cs:485-486` | covered |

## 3. `Where` interplay

| Variant | Evidence | Location | Status |
|---|---|---|---|
| `Where` on base before a single alias join keeps base slot (`t1`) | T | `AliasJoinWhereTests.cs:18` (`:32`) | covered |
| `Where` on base before a second alias join keeps base slot | T | `AliasJoinWhereTests.cs:42` (`:61-63`) | covered |
| `Where` between two alias joins targets the first joined slot (`t2`) | T | `AliasJoinWhereTests.cs:73` (`:94`, via runtime `JoinAlias` seam `EntityBuilder.cs:2671`) | covered |
| `Where` after the second alias join targets the second slot (`t3`) | T | `AliasJoinWhereTests.cs:104` (`:119`) | covered |
| Slot-mapping rewriter for between-joins predicate | source | `EntityBuilder.cs:2929` (`ApplyWhereToAliasJoined`), `:2959`+ (`RebaseAliasProjectionVisitor`) | covered |

## 4. Plan cache / shared command

| Variant | Evidence | Location | Status |
|---|---|---|---|
| Alternating Buyer/Approver on one context keeps slots | T | `AliasPlanCacheTests.cs:14` (`:29-35`) | covered |
| Alias command keeps `Cache == true` (no sticky disable) | T | `AliasPlanCacheTests.cs:38` | covered |
| Second prepare reuses cached command (`storeInCache`) | T | `AliasPlanCacheTests.cs:42-44` | covered |

## 5. Guards (fail-closed)

| Variant | Evidence | Location | Status |
|---|---|---|---|
| In-memory refuses all 7 alias operators (`NotSupportedException`) | T+G | `AliasInMemoryRefusalTests.cs:15`; guard `EntityBuilder.cs:2774` (`JoinAliasEntity`), `:2813` (`JoinAliasApply`) | covered |
| Alias member read outside an expression tree throws | T | `AliasExpressionOnlyContractTests.cs:17`, `:36` | covered |
| `JoinInto` cannot combine with other joins | G | `EntityBuilder.cs:2853` (`CreateAliasJoined`), `:3127` (`CreateJoined`) | covered (guard) |
| Windows must be declared after joins | G | `EntityBuilder.cs:2771`, `:2817` | covered (guard) |
| Correlated APPLY source cannot reference a projection | G | `EntityBuilder.cs:2809` | covered (guard) |
| Positional `Join` after an alias/root builder binds the generated `new` instance transition (mixing) | T | `AliasProjectionShapeTests.cs:45` (`Positional_join_after_alias_binds_the_generated_receiver`), `:121` (`Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable`) | covered |
| Base (non-generated) positional `Join`/`Apply` on a projection-shaped builder fails closed | G+T | guard `EntityBuilder.cs:3240-3242` (`CreateJoined`); generated receivers hide it (`AliasProjectionShapeTests.cs:74`) | covered |
| `WithAlias` applied after a join or a named window fails closed | G+T | `EntityBuilder.cs:2870-2879` (`AliasRoot`); NORMGEN008 (`JoinAliasGenerator.cs:135`); `RootAliasTests.cs:424`, `:680` | covered |
| In-memory refuses any alias (root or join), positional-only unaffected | T+G | `RootAliasTests.cs:295`, `MixedJoinChainTests.cs:146`, `:159` | covered |

## 6. Generator diagnostics & incrementality

| Variant | Evidence | Location | Status |
|---|---|---|---|
| NORMGEN001 duplicate alias (span = 2nd alias) | T | `JoinAliasGeneratorDiagnosticTests.cs:28`; descriptor `JoinAliasGenerator.cs:56`, report `:567` | covered |
| NORMGEN002 alias collides with retained `ItemN` | T | `JoinAliasGeneratorDiagnosticTests.cs:39`; `:65`, `:501/509` | covered |
| NORMGEN003 invalid alias identifier | T | `JoinAliasGeneratorDiagnosticTests.cs:48`; `:74`, `:494` | covered |
| NORMGEN004 arity cap 8 (9th slot) | T | `JoinAliasGeneratorDiagnosticTests.cs:57`; `:83`, `:549` | covered |
| NORMGEN005 non-approved alias argument (`Alias.Buyer.Approver`) | T | `JoinAliasGeneratorDiagnosticTests.cs:75`; `:92`, `:401` | covered |
| NORMGEN006 non-normalizable assembly name | T | `JoinAliasGeneratorDiagnosticTests.cs:84`; `:101`, `:390` | covered |
| Valid snippet emits exactly one source, no diagnostics | T | `JoinAliasGeneratorDiagnosticTests.cs:103` | covered |
| Output is layout-independent (determinism) | T | `JoinAliasGeneratorDiagnosticTests.cs:114` | covered |
| Unrelated edit reuses cached output (incrementality) | T | `JoinAliasGeneratorDiagnosticTests.cs:126` | covered |

## 7. Frozen public surface

| Variant | Evidence | Location | Status |
|---|---|---|---|
| Generated type set frozen (`Alias`, `JoinAliasExtensions`, `AliasProjection_*`/`AliasJoin_*`) | T | `AliasGeneratedSurfaceTests.cs:24` | covered |
| Marker type static + one public marker per alias | T | `AliasGeneratedSurfaceTests.cs:49` | covered |
| Projection/builder pairs public, slot-bound, base `Projection<...>`/`EntityBuilder<>` | T | `AliasGeneratedSurfaceTests.cs:61` | covered |
| Namespace `NextORM.Generated.<AssemblyName>` | T | `AliasGeneratedSurfaceTests.cs:18`, `:52` | covered |
| `Alias` marker is the only `Alias.*` argument form | T | `JoinAliasGeneratorDiagnosticTests.cs:75` (NORMGEN005) | covered |

## 8. Cross-provider (integration, container-backed)

Common suite runs once per provider (`Postgres`/`SqlServer`/`MySql`/`MariaDb`/`Sqlite`/`ClickHouse` via `ProviderTestSuite`).

| Variant | Evidence | Location | Status |
|---|---|---|---|
| Alias inner join | T | `tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs:12` | covered |
| Alias left join | T | `CommonTestSuite.JoinAlias.cs:25` | covered |
| Alias right join (keeps unmatched right) | T | `CommonTestSuite.JoinAlias.cs:38` | covered |
| Alias full join; fail-closed where unsupported (MySQL/MariaDB) | T | `CommonTestSuite.JoinAlias.cs:51` | covered |
| Alias cross join (cartesian) | T | `CommonTestSuite.JoinAlias.cs:76` | covered |
| Buyer/Approver (same CLR type) resolve to distinct ids | T | `CommonTestSuite.JoinAlias.cs:87` | covered |
| Alias chain across a method boundary that names generated types | T | `CommonTestSuite.JoinAlias.cs:104` (`:119`) | covered |

## 9. Mixing & root alias (issue #160)

| Variant | Evidence | Location | Status |
|---|---|---|---|
| Alias→positional join keeps slot identity and distinct SQL aliases | T | `MixedJoinChainTests.cs:16` | covered |
| Positional→alias join binds the alias to the next slot | T | `MixedJoinChainTests.cs:50` | covered |
| Alternating alias→positional→alias assigns slots in chain order | T | `MixedJoinChainTests.cs:81` | covered |
| Mixed chain matches the equivalent positional chain | T | `MixedJoinChainTests.cs:116` | covered |
| Digit-ending alias is a name, never a slot (`Buyer2`), incl. non-matching slot | T | `MixedJoinChainTests.cs:172`, `:196` | covered |
| Slot-encoded generated naming `P{slot}`/`A{slot}_{name}` | T+source | `JoinAliasGenerator.cs:762` (`BuildSchema`); `AliasProjectionShapeTests.cs:17` | covered |
| `From<Order>().WithAlias` maps slot 1 to `t1` and mixes with an alias join | T | `RootAliasTests.cs:109`, `:136` | covered |
| Root alias on a raw `TableAlias` source | T | `RootAliasTests.cs:177`, `:472` | covered |
| Root alias on `FromSql` keeps the derived source | T | `RootAliasTests.cs:198` | covered |
| Root alias on `From(QueryCommand<T>)` / `From(builder)` keeps the derived source | T | `RootAliasTests.cs:222`, `:250` | covered |
| Root alias on a typed CTE source keeps the CTE name | T | `RootAliasTests.cs:528` | covered |
| Root alias on a temp-table source keeps the batch read | T | `RootAliasTests.cs:553` | covered |
| Root alias on a table-valued function source maps the call to `t1` | T (render-only) | `RootAliasTests.cs:578` | covered |
| Root alias through `CreateQueryBuilder*` forwarders | T | `RootAliasTests.cs:495` | covered |
| Root alias preserves pre-join `Where`/`OrderBy`/paging/`GroupBy`/`Having` | T | `RootAliasTests.cs:310`–`:395` | covered |
| All executable positional operators after a root alias | T | `RootAliasTests.cs:611` | covered |
| Root-alias APPLY fails closed without a lateral source | T | `RootAliasTests.cs:653` | covered |
| `Projection<T1>` is dim-1, a plain `IProjection`, not directly extendable | T | `RootAliasTests.cs:87` | covered |
| Generated `WithAlias<T>` + `Alias.RootMarker` surface (null marker rejected) | T | `JoinAlias.g.cs` (generated); `RootAliasTests.cs:284` | covered |

## Gaps (tracked)

1. **Correlated `CrossApply`/`OuterApply` alias has no positive SQL test** — only the in-memory
   refusal and the generated-surface check. Add a SQLite e2e (and, if supported, an integration case)
   exercising `AliasJoinApply` (`EntityBuilder.cs:2799`). The mixing transition itself is now covered
   (`AliasProjectionShapeTests.cs:45`).
2. **`TryParseItemPosition` digit-edge is untested by name** — the digit-edge behavior is now covered
   end to end (`MixedJoinChainTests.cs:172`, `:196`), but `ProjectionAliasCache.cs:61` still has no
   focused unit test asserting the parse directly.

Resolved in #160 (kept for traceability): **positional-after-alias was a gap** (old
`EntityBuilder.cs:3137` guard, no direct test). It is now **T** — a generated `public new` instance
transition lets a positional `Join` follow an alias/root receiver (`AliasProjectionShapeTests.cs:45`,
`:121`), and the base guard fails closed only on the non-generated path (`EntityBuilder.cs:3240-3242`,
`AliasProjectionShapeTests.cs:74`).

These gaps are evidence backlog, not functional blockers; each maps to a single small test.
