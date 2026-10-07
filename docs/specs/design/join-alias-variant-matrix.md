# Join-alias variant matrix (issue #113, Block A; extended by issue #160)

Evidence map for the join-alias feature (milestone `1.0.9-b`; #160 targets `1.0.9-rc2`): every
behavioural variant of the alias chain is tied to the test or the runtime guard that proves it, with
`file:line`. `#113` scope was the owner-decided **alias-only** surface (no positional mixing);
**#160 supersedes that restriction** with free mixing of positional and alias steps plus the root
alias (`.WithAlias`), still with `JoinInto` excluded and SQL providers only (in-memory fails closed).
The #160 variants and their evidence are in §9 below. Public API surface is frozen by
`tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs`; API-naming register is
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
| Positional `Join`/`Apply` cannot follow an alias `Join` (nested projection) | G | `EntityBuilder.cs:3137` (`CreateJoined`) | **gap** (no direct test) |

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

## 9. Issue #160 — free mixing + root alias (rv=3)

| Variant | Evidence | Location | Status |
|---|---|---|---|
| Alias→positional transition; alias slot == `ItemK`; distinct SQL slots | T | `tests/nextorm.alias.tests/MixedJoinChainTests.cs:16` | covered |
| Positional→alias transition binds the alias to the next slot | T | `MixedJoinChainTests.cs:51` | covered |
| Alternating alias/positional/alias assigns slots in chain order | T | `MixedJoinChainTests.cs:82` | covered |
| Mixed chain parity with the equivalent positional chain | T | `MixedJoinChainTests.cs:117` | covered |
| Digit-ending alias (`Buyer2`) is a name, not a slot number | T | `MixedJoinChainTests.cs:173` | covered |
| Mixed alias chain fails closed in-memory; pure positional is not refused | T+G | `MixedJoinChainTests.cs:147`, `:160` | covered |
| Root `.WithAlias` + alias join maps slot 1 to `t1`; `Root` == `Item1` | T | `tests/nextorm.alias.tests/RootAliasTests.cs:107` | covered |
| Root alias alone keeps the physical source (`select Id from orders`) | T | `RootAliasTests.cs:134` | covered |
| Positional join after a root alias (generated `new` instance transition) | T | `RootAliasTests.cs:154` | covered |
| Root `.WithAlias` on a raw table source | T | `RootAliasTests.cs:175` | covered |
| Derived roots preserved under root alias: `FromSql` / `From(builder)` / `From(QueryCommand<T>)` | T | `RootAliasTests.cs:196`, `:248`, `:220` | covered |
| dim-1 root projection plans; `Extend` yields slot 2 preserving `Item1` | T | `RootAliasTests.cs:62`, `:88` | covered |
| Root alias fails closed in-memory; hand-written `AliasRoot` refused after a join | T+G | `RootAliasTests.cs:293`, `:304`; seam `src/nextorm.core/Builders/EntityBuilder.cs:3096` (in-memory `:3101`, join/projection `:3105`–`:3111`) | covered |
| `.WithAlias` after a join / repeated `WithAlias` → `NORMGEN008` | T | `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs:102`, `:111` | covered |
| Slot-encoded generated names incl. root/positional (`P{slot}`/`A{slot}_{name}`) frozen | T | `AliasGeneratedSurfaceTests.cs:24` (`JoinAlias_P1_A2_Buyer`, `…_A1_Root…`) | covered |
| Generated naming mechanism (suffix from ordered slots) | source | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:748` (`BuildSchema`, `:763`/`:768`) | covered |
| Alias members expression-only | T | `AliasExpressionOnlyContractTests.cs:17`, `:36` | covered |

## Gaps (tracked)

1. ~~**Positional-after-alias guard is untested**~~ — **closed by #160**: positional-after-alias is
   now a supported generated `new` instance transition (§9), covered by
   `MixedJoinChainTests.cs:16`, `:51`, `:117`; the base `EntityBuilder.cs:3137` guard remains only as
   a failsafe for the non-generated receiver.
2. **Correlated `CrossApply`/`OuterApply` alias has no positive SQL test** — only the in-memory
   refusal and the generated-surface check. Add a SQLite e2e (and, if supported, an integration case)
   exercising `AliasJoinApply` (`EntityBuilder.cs:2799`).
3. ~~**`TryParseItemPosition` digit-edge is untested**~~ — **closed by #160**:
   `MixedJoinChainTests.cs:173` (`Digit_ending_alias_is_a_name_and_does_not_shift_the_slot`) resolves
   `Buyer2` via `JoinSlotAttribute`, not as `ItemN`.

These gaps are evidence backlog, not functional blockers; each maps to a single small test.
