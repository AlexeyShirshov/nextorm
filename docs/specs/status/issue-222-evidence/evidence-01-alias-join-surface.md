# Evidence 01: alias-join surface + prior decisions on join source explicitness / source-less (type-only) overloads

## Question

Collect facts for grilling issue #222 open questions. (1) Prior decision records in
`docs/specs/design/**`, `docs/specs/roadmap/**`, `docs/superpowers/specs/**`, status/plan/handoff/collection
records deciding: (a) whether join sources must stay explicit, (b) the alias-join design/overload shape,
(c) any mention of `source-less`, `type-only`, `optional source`, overloads without a source builder.
(2) The actual alias-join API shape in `src/nextorm.core`: all public `JoinAlias` overloads; what the
generator emits (`AliasJoin_*`/`AliasProjection_*` builders + methods), whether they take a source
builder explicitly plus the `Alias` marker; the `Alias` marker and `JoinSlotAttribute` location; whether
Cte-source alias overloads exist. (3) Conditionless alias operators `CrossJoin`/`CrossApply`/`OuterApply`
with `Alias.<Name>`: where declared, signatures. (4) Issue/PR provenance (believe #113). (5) Tests
asserting the alias overload surface or SQL, with asserted SQL strings for alias CrossJoin and alias Join.

## Commands

All commands run with cwd `/home/alex/sources/nextorm` (read-only; no edits to product code/tests).

```
$ rg -n -i "source-?less|type-only|type only|without a source|optional source|no source|explicit source" \
      docs/specs/design docs/specs/roadmap docs/superpowers/specs docs/specs/status
  -> only unrelated hits (bulk-insert "source-less returning"; "type-only" = QueryFilterScope / KeyTypeClause docs;
     rc2-159 "explicit sources ... resolved faithfully" for TableAlias joins). No join source-explicitness decision.
$ rg -n "#222|issue 222" . -g '!**/bin/**' -g '!**/obj/**' -g '!**/.git/**'
  -> (no output)  # no docs/specs record for issue #222
$ gh issue view 222 --json number,title,state,createdAt,milestone,labels,url
  -> {"state":"OPEN","createdAt":"2026-10-12T14:50:43Z","milestone":{"number":4,"title":"1.0.9"},
      "labels":[{"name":"enhancement"}],"number":222,"url":"https://github.com/AlexeyShirshov/nextorm/issues/222"}
$ rg -n "public TNext JoinAlias" src/nextorm.core/Builders/EntityBuilder.cs
$ git log --oneline --all --grep="alias" -i
```

## Facts

### 1. Prior decision records

- `docs/specs/status/join-alias-113-1.md:2` — `- Task: #113 "В join добавить новый параметр - алиас" (milestone 1.0.9-b)`.
- `docs/specs/status/join-alias-113-1.md:24` — recommended frozen semantics: `[JoinSlot(n)] n = entity position in the chain, starting at 2 (additive-extensible, never break). Arity cap = existing Projection<T1..T8> (do NOT extend beyond 8). ... Generated types internal in a reserved namespace`.
- `docs/specs/status/join-alias-113-1.md:33` — owner decisions: `generated types PUBLIC in NextORM.Generated.<AssemblyName>; prefixes AliasJoin_/AliasProjection_; marker Alias; STABLE (no [Experimental]).`
- `docs/specs/status/join-alias-113-1.md:38` — `RESOLVED (owner decision 2026-10-01) — join aliases are alias-only, JoinInto excluded: JoinInto gets no alias surface at all.`
- `docs/specs/status/join-alias-113-1.md:39` — `RESOLVED (owner decision 2026-10-01) — positional mixing removed: ... Join aliases are a self-contained chained API; alias→alias inheritance ... remains in scope`.
- `docs/specs/status/join-alias-113-1.md:46` — `Owner resolved both scope decisions: the alias feature is ALIAS-ONLY (no mixing with positional joins; positional API untouched), and JoinInto is EXCLUDED from aliases. Feature surface = the 7 projection operators (Join/LeftJoin/RightJoin/FullJoin/CrossJoin/CrossApply/OuterApply).`
- `docs/specs/design/join-alias-mixing-and-root-alias.md:5` — tracking: `GitHub issue: #160 — https://github.com/AlexeyShirshov/nextorm/issues/160`; `:7` related `#113 (родительская join-alias фича, закрыта), #159 (CTE alias-API)`.
- `docs/specs/design/join-alias-mixing-and-root-alias.md:13` — `#113 добавил именованные join-алиасы (Alias.<Name>), но зафиксировал **alias-only** семантику`.
- `docs/specs/design/join-alias-mixing-and-root-alias.md:23-24` — goal: `(2) алиас корня через цепочный .WithAlias(Alias.X) для всех источников.`
- `docs/specs/design/join-alias-mixing-and-root-alias.md:28-29` — `Утверждённые решения`: `.WithAlias(Alias.X) — **только на корне** и именует slot 1; у джойнов имя остаётся трейлинг-аргументом Alias.<Name>.`
- `docs/specs/design/join-alias-mixing-and-root-alias.md:199` — `- JoinInto — без alias-поверхности (как решено ранее).`
- `docs/specs/design/join-alias-variant-matrix.md:5-8` — `The original owner-decided surface was **alias-only** chains (no positional mixing); #160 supersedes that with **free positional/alias mixing** and the **root alias** (.WithAlias), keeping JoinInto excluded and SQL providers only (in-memory fails closed on any alias).`
- `docs/specs/status/rc2-160-join-alias-mixing-1.md:24` — `Implement aliases as names for existing join slots (free positional/alias mixing; p.ItemK ≡ slot-K alias; alias optional) and root alias .WithAlias(Alias.X) naming slot 1 for all 8 root families; in-memory fail-closed; positional path zero-overhead.`
- `docs/specs/status/rc2-160-join-alias-mixing-1.md:36` — R160-02 negative: `pure positional must not enter alias refusal/seam; unsupported provider op not silently supported.`
- `docs/superpowers/specs/2026-10-02-cte-join-overloads-design.md:84` — `Добавляются публичные Cte-принимающие перегрузки существующего seam-метода JoinAlias, сохраняющие прежние ограничения фабрики/возврата (TNext : EntityBuilder<TNextEntity>) и JoinType; внутри контекст готовится как context.From(cte) и вызывается существующий EntityBuilder-вариант JoinAlias.`
- `docs/superpowers/specs/2026-10-02-cte-join-overloads-design.md:94` — `Для найденных alias-цепочек/оператора эмитятся ОБЕ формы extension: EntityBuilder-source и Cte-source.`
- `docs/superpowers/specs/2026-10-02-cte-join-overloads-design.md:110` — `Перегрузки аддитивны бинарно; source-совместимость подлежит проверке, включая выбор перегрузки, вывод лямбд и method hiding.`
- `docs/specs/status/rc2-159-cte-direct-joins-1.md:377` — `R159-12': "TableAlias-join uses JoinSourceResolver ...; explicit sources (raw SQL / Table override / derived / TVF / Cte<T>) are resolved faithfully; plain mapped entity keeps the metadata fallback. Boundaries unchanged: slot ceiling 8, no Item9, no CteReference overload."`
- `docs/specs/status/rc2-159-cte-direct-joins-1.md:381` — `legacy fallback **is** the mapped fallback (with no explicit source the joined type resolves through QueryPlanner.GetFrom metadata). ... Explicit sources are now resolved faithfully through JoinSourceResolver.Resolve (EntityBuilder.cs:4478).`

**Decision on "sources must stay explicit":** none found in any spec as an explicit decision. The closest
statements are the signature documentation (`docs/specs/status/rc2-159-cte-direct-joins-1.md:377,381`)
which say explicit sources are *honoured*, not that they are *required*. See Not found.

**`docs/guide/02-joins.md` claims (NOT a decision record; listed because issue #222 cites it):**
- `docs/guide/02-joins.md:15-21` — right-hand side can be: another typed entity `EntityBuilder<TJoinEntity>`; a `QueryCommand<TJoinEntity>`; a raw table `EntityBuilder` from `From(string)` read via `TableAlias`. No statement that a source must be supplied.
- `docs/guide/02-joins.md:162-171` — `Named join aliases ... A join may additionally carry an optional trailing Alias.<Name> argument ... aliases matter when two joined sources share the same CLR type — for example a buyer and an approver are both Person — because positional members cannot tell them apart.` (the stated reason aliases exist = repeated CLR type / slot disambiguation, not source explicitness).
- `docs/guide/02-joins.md:189-200` — generated `AliasProjection_<suffix>` + `AliasJoin_<suffix>`; namespace `NextORM.Generated.<assembly name>`; `[JoinSlot(n)]` = 1-based entity position.
- `docs/guide/02-joins.md:209-211` — `All seven projection join operators accept an alias; JoinInto is not one of them ... The conditionless three take only the source and the marker`.
- `docs/guide/02-joins.md:223` — alias CrossJoin SQL string shown: `select t2.id from orders as 't1' cross join person as 't2'`.
- `docs/guide/02-joins.md:186` — alias inner join SQL string shown: `select t2.id as 'Buyer', t3.id as 'Approver' from orders as 't1' join person as 't2' on t1.buyerid = t2.id join person as 't3' on t1.approverid = t3.id`.
- `docs/guide/02-joins.md:584-589` — `A raw-table (TableAlias) join now honours an explicit source ... A plain mapped entity with no explicit source keeps the metadata fallback.`

**Issue #222 itself (from `gh issue view 222`, not a decision record):** title `Джойны: перегрузки без
явного источника (Join/LeftJoin/RightJoin/FullJoin по типу)`; body states `Покрывается только один из
четырёх видов источника (typed mapped entity); явные источники не удаляются.` and open question
`1. Нужны ли type-only варианты для alias-поверхности (.Join<Person>((o, a) => …, Alias.Approver))?`.

### 2. Actual alias-join API shape

All six **public** `JoinAlias` overloads on `EntityBuilder<TEntity>` (`src/nextorm.core/Builders/EntityBuilder.cs`),
each taking `Func<IDataContext, TNext> create` + an explicit source + (for conditionals) a condition;
**no marker parameter in core** (`Alias` is generated):

- `src/nextorm.core/Builders/EntityBuilder.cs:2752` — `public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(Func<IDataContext, TNext> create, EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, JoinType joinType = JoinType.Inner, Action<JoinOptions>? options = null)` (conditional).
- `:2782` — same but `EntityBuilder<TJoinEntity> _, JoinType joinType, Action<JoinOptions>? options = null` (conditionless).
- `:2812` — `Cte<TJoinEntity> cte, Expression<...> joinCondition, JoinType joinType = JoinType.Inner, Action<JoinOptions>? options = null` (typed-CTE, conditional).
- `:2838` — `Cte<TJoinEntity> cte, JoinType joinType, Action<JoinOptions>? options = null` (typed-CTE, conditionless).
- `:2865` — `Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source, JoinType joinType, Action<JoinOptions>? options = null` (correlated APPLY over an EntityBuilder source).
- `:2886` — `Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source, JoinType joinType, Action<JoinOptions>? options = null` (correlated APPLY over a derived query).

All six delegate to private `JoinAliasEntity` (`:2900`) / `JoinAliasApply` (`:2936`) / `CreateAliasJoined` (`:2983`).
`JoinAliasEntity` guards in-memory (`:2911-2913`), builds `JoinExpression` and calls `CreateAliasJoined`
(`:2928`); `JoinAliasApply` guards projection-source correlation (`:2946-2948`) and in-memory (`:2950-2952`).

- `Alias` marker type: **not defined in `src/nextorm.core`** (grep found none). It is emitted by the generator at `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:921-937`:
  `public static class Alias { ... public sealed class <Name>Marker { } ... public static <Name>Marker <Name> { get; } = new <Name>Marker(); }`.
- `JoinSlotAttribute`: `src/nextorm.core/Builders/JoinSlotAttribute.cs:14` — `public sealed class JoinSlotAttribute : Attribute`; `:18` ctor `JoinSlotAttribute(int position) => Position = position;`; `:13` `[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]`.

**Generator operator set & emitted surface** (`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`):
- `:52-55` `JoinOperators = { "Join","LeftJoin","RightJoin","FullJoin","CrossJoin","CrossApply","OuterApply" }`; `:62-65` `JoinOperatorOrder`; `:67-70` `ConditionlessOperators = { "CrossJoin","CrossApply","OuterApply" }`.
- `:955-963` emits `public class AliasProjection_<suffix><T1..Tn> : global::NextORM.Core.Projection<...>` with one member per alias `[global::NextORM.Core.JoinSlot(slot)] public T<slot> <Name> => throw new global::System.NotSupportedException();`.
- `:966-968` emits `public class AliasJoin_<suffix><T1..Tn> : global::NextORM.Core.EntityBuilder<AliasProjection_...>` with public ctor `(global::NextORM.Core.IDataContext dataProvider)`.
- `:989-1035` `RenderPositionalMethods` emits `public new <AliasJoin_...> <Operator><TJoin>( EntityBuilder<TJoin> _, [condition,] Action<JoinOptions> options = null)` and `... (Cte<TJoin> cte, [condition,] ...)` instance methods (no marker) → route to `JoinAlias` seam.
- `:1125-1191` `RenderAliasExtension` emits the **alias** overloads as extension methods on `JoinAliasExtensions`:
  `public static <AliasJoin_next> <Operator><TJoin>( this <receiver>, <EntityBuilder<TJoin> _ | Cte<TJoin> cte>, [Expression<Func<...,TJoin,bool>> condition,] <Alias.<Name>Marker> marker ) => self.JoinAlias<...>(...)`.
  Signature string built at `:1188-1190`. The last parameter is always the `...Marker` (asserted by tests).
- `:1100-1117` `RenderWithAliasExtension` emits `public static AliasJoin_A1_<X><T> WithAlias<T>(this EntityBuilder<T> self, Alias.<X>Marker marker)`.
- `:1086-1092` all extension methods live in one `public static class JoinAliasExtensions`.

So the generated alias join operators take the source **explicitly** (`EntityBuilder<TJoinEntity>` or
`Cte<TJoinEntity>`) **plus** the `Alias` marker; there is **no** generated overload taking only a marker/type.

### 3. Conditionless alias operators (`CrossJoin`/`CrossApply`/`OuterApply` with `Alias.<Name>`)

- They are **not** declared in `src/nextorm.core` as marker-taking overloads (no `Alias` type there). They are
  emitted by the generator from `ConditionlessOperators` (`JoinAliasGenerator.cs:67-70`) via `RenderAliasExtension`
  (`:1168-1186`): parameters are `<source> _/cte` + `Alias.<Name>Marker marker`, **no** condition; `JoinTypeName`
  maps them (`:1194-1204`: `CrossJoin`→Cross, `CrossApply`→CrossApply, `OuterApply`→OuterApply).
- The core conditionless seam they call is `EntityBuilder.cs:2782` (`EntityBuilder<TJoinEntity> _, JoinType, ...`);
  correlated APPLY seam is `:2865` / `:2886`.
- Exact generated signatures (frozen test, `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs`):
  - `:210` `AliasJoin_P1_A2_Buyer<Order, TJoin> CrossJoin`1(EntityBuilder<Order>, EntityBuilder<TJoin>, Alias+BuyerMarker)`.
  - `:209` `... CrossApply`1(EntityBuilder<Order>, EntityBuilder<TJoin>, Alias+BuyerMarker)`.
  - `:215` `... OuterApply`1(EntityBuilder<Order>, EntityBuilder<TJoin>, Alias+BuyerMarker)`.
- Core public positional conditionless operators (source-taking) are `EntityBuilder.cs:2537` (`CrossJoin`), `:2546` (`CrossApply`), `:2555` (`OuterApply`) — cited by issue #222.

### 4. Issue / PR provenance

- Issue that introduced alias joins: **#113** (confirmed). `git log --oneline --all --grep=alias -i`:
  - `d83444e4 #113 join aliases: typed alias projections via source generator (Block A) + PoC`
  - `575589ae #113 join aliases: alias-only, JoinInto excluded; close Block A`
  - `e29a4806 #113 join aliases: evidence pack (variant matrix, full coverage)`
- Mixed/root-alias follow-up: `a60ecb84 #160 Join aliases: free positional/alias mixing + root .WithAlias(Alias.X)`.
- Spec/design docs for #113: `docs/specs/status/join-alias-113-1.md` (status, task #113 at `:2`) and
  `docs/specs/design/join-alias-variant-matrix.md:1` (`# Join-alias variant matrix (issue #113, Block A)`).
- Issue #222 has **no** docs/specs/design record; git log has no #222 commit (`git log --grep=222` → empty).
- PRs: `gh pr list --state all --search "alias"` → `[]` (no matching PR).

### 5. Tests asserting the alias overload surface / SQL

Generated surface:
- `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs:20-21` — `JoinOperators = ["Join","LeftJoin","RightJoin","FullJoin","CrossJoin","CrossApply","OuterApply"]`.
- `:24-77` `Generated_public_type_set_is_frozen` (frozen type list incl. `Alias`, `JoinAliasExtensions`, `AliasJoin_*`, `AliasProjection_*`).
- `:109-129` `Generated_extension_class_exposes_the_seven_alias_operators` — each op has overloads, return `AliasJoin_*`, and `GetParameters().Last().ParameterType.Name.EndsWith("Marker")`.
- `:132-167` `Generated_alias_join_overloads_do_not_accept_a_join_options_configuration` — no `Action<JoinOptions>` on any marker-taking join extension; specifically no `(inner, condition, Action<JoinOptions>, marker)` form.
- `:207-219` `FrozenExtensionSignatures` — exact signatures incl. `:212` the Cte form `Join`1(EntityBuilder<Order>, Cte<TJoin>, Expression<...>, Alias+BuyerMarker)` and `:213` the EntityBuilder form.

Alias Join SQL:
- `tests/nextorm.sqlite.tests/JoinAliasSqlGenerationTests.cs:28-32` — `alias.Should().Be(positional); alias.Should().Contain(" join complex_entity");`
- `tests/nextorm.postgres.tests/JoinAliasSqlGenerationTests.cs:31-32` — same pattern (`Be(positional)` + `Contain(" join complex_entity")`).
- `tests/nextorm.alias.tests/AliasSqliteEndToEndTests.cs:61-64` — positional vs alias parity + `AssertParity(" join ", ...)`; `:157-161` `AssertParity` asserts `sql.Should().Contain(keyword)`.
- `tests/nextorm.alias.tests/AliasSqliteEndToEndTests.cs:39-42` — `Contain("person as 't2'")`, `Contain("person as 't3'")`, `Contain("select t2.Id")`, `Contain("select t3.Id")`.

Alias CrossJoin SQL:
- `tests/nextorm.sqlite.tests/JoinAliasSqlGenerationTests.cs:84-88` — `alias.Should().Be(positional); alias.Should().Contain(" cross join complex_entity");`
- `tests/nextorm.postgres.tests/JoinAliasSqlGenerationTests.cs:88` / `:120` / `:160`; `tests/nextorm.mariadb.tests/JoinAliasSqlGenerationTests.cs:86,118`; `tests/nextorm.clickhouse.tests/JoinAliasSqlGenerationTests.cs:87`; `tests/nextorm.sqlserver.tests/JoinAliasSqlGenerationTests.cs:88`; `tests/nextorm.mysql.tests/...` — all `Contain(" cross join complex_entity")`.
- `tests/nextorm.alias.tests/AliasSqliteEndToEndTests.cs:145-148` — `alias = orders.CrossJoin(people, Alias.Buyer).Select(p => p.Buyer.Id)` + `AssertParity(" cross join ", ...)`.

Alias CrossApply / OuterApply (type-level + fail-closed):
- `tests/nextorm.sqlite.tests/JoinAliasSqlGenerationTests.cs:110-120` and `:122-132` — `CrossApply(complex, Alias.Buyer)` / `OuterApply(complex, Alias.Buyer)` throw `NotSupportedException` with message `*CrossApply*` / `*OuterApply*`.
- Surface type-level evidence: `AliasGeneratedSurfaceTests.cs:209-210,215`.

Integration:
- `tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs:20-27` alias inner join; `:89-99` alias cross join (`HaveCount(6)`); `:106-118` buyer/approver distinct slots; `:138-141` method boundary naming generated `AliasJoin_P1_A2_Buyer_A3_Approver<...>`.

Note: no test asserts a full exact alias CrossJoin/Join SQL string via `Should().Be(<literal>)`; the alias
SQL-gen tests use `Be(positional)` + `Contain(keyword)`. The full literal alias SQL appears only in
`docs/guide/02-joins.md:223` (cross join) and `:186` (join).

## Not found

- **No decision record** (design/roadmap/superpowers/status) stating that join sources must stay explicit.
  Searched `docs/specs/design/**`, `docs/specs/roadmap/**`, `docs/superpowers/specs/**`, `docs/specs/status/**`
  for `explicit source`, `sources must/stay/remain explicit`, `явн* источник`, `source-less`, `type-only`,
  `optional source`, `without a source`. Only hits: bulk-insert "source-less returning" (unrelated) and
  rc2-159 "explicit sources are resolved faithfully" (honouring, not requiring).
- **No decision record** about source-less / type-only / optional-source join overloads (in particular no
  type-only join overload exists and none is decided). Issue #222's open question 1 is the only place the
  topic appears.
- **No `Alias` marker type** defined in `src/nextorm.core` (`rg -l "JoinSlotAttribute"` lists only
  `TypeExtensions.cs`, `ProjectionAliasCache.cs`, `JoinSlotAttribute.cs`, `EntityBuilder.cs`,
  `AliasFromProjectionVisitor.cs`, `MemberTranslator.cs`; no `Alias` class there).
- **No core `CrossJoin`/`CrossApply`/`OuterApply` overload accepting `Alias.<Name>`** — the marker-taking
  conditionless forms are generator-emitted extension methods only.
- **No docs/specs record mentioning issue #222** (grep `#222` over the repo excluding bin/obj/.git → empty).
- **No PR** for alias (`gh pr list --state all --search "alias"` → `[]`).
- **No test** asserting a full exact alias CrossJoin/Join SQL literal via `Should().Be(...)`.

## Open questions

- Issue #222 open Q1: whether type-only (source-less) alias overloads (`.Join<Person>((o, a) => …, Alias.Approver)`)
  should be added — no prior decision located.
- Whether requiring an explicit source is an intentional product constraint or merely the current API shape —
  no spec states either way; only the signature documentation and issue #222's own non-goal ("явные источники
  не удаляются") exist.
