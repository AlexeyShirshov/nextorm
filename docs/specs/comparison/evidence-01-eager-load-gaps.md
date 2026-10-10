# Evidence 01: eager-load gap status/API/decomposition facts (comparison doc claim)

## Question

Read-only reconnaissance in `/home/alex/sources/nextorm`. Raw facts (file:line + verbatim quotes) about
(a) STATUS/DECISION statements for 4 eager-load gaps — (1) named/selectable eager-load strategy +
auto-dispatch, (2) child-ordering guarantee/remap, (3) per-parent limited children (Take/Skip), (4)
nested (level 2+) eager loading — plus relationship residuals (composite junction selectors, composite
keys, M:N under single-query); (b) any existing decomposition/task-breakdown/milestone proposal; (c) the
recorded relationship between GitHub issue #20 and builder-surface eager-load items (roadmap phases 6/7);
(d) current public API surface for eager loading; (e) whether any of these items has an OPEN GitHub issue.

## Commands

All commands run in `/home/alex/sources/nextorm` on 2026-10-10 (env date), branch `1.0.9-rc2`, HEAD
`b4d6594a`.

- `rg -n -i "<term>" docs/specs/...` — targeted reads per file (see Facts for `file:line`).
- `rg -n "AsSingleQuery" src/` → **no matches** (see Facts §4).
- `rg -n "AsSingleQuery|AsSplitQuery" src/` → no matches.
- `gh issue list --repo AlexeyShirshov/nextorm --state open --limit 200` (output in Facts §5).
- `gh issue view {105,148,20,95,107,184,171,172} --repo AlexeyShirshov/nextorm --json number,title,state,milestone,labels,url,closedAt`.
- `gh issue view 20 --repo AlexeyShirshov/nextorm --json body`.
- `gh issue list --repo AlexeyShirshov/nextorm --state all --search "eager OR nested OR ordering OR navigation OR relationship" --limit 40`.
- `roslyn members NextORM.Core.EntityBuilder` / `roslyn refs NextORM.Core.EntityBuilder.LoadWith` /
  `roslyn refs NextORM.Core.EntityBuilder.JoinInto` (symbol resolution; `AsSingleQuery` → `symbol not found`).

## Facts

### 1. STATUS / DECISION statements about the 4 eager-load gaps and relationship residuals

**`docs/specs/roadmap/todo_navigation_properties.md`** (spec for #105; "единственный источник истины по объёму", :14-15):

- `:5` — "> (eager loading, закрыт), `sql-capabilities-gap-analysis.md` §4 п.49, §6 workstream 13."
- `:25-26` — "eager loading уровня 1 — split-query `LoadWith` (#95), у которого связь задаётся
  селекторами на месте вызова, а не метаданными."
- `:28-35` — Non-goals: `:31-32` "Составные ключи (и составные junction-селекторы) в `JoinInto` — модель
  их представляет, реализация отклоняет явно"; `:33` "Изменение `LoadWith` (#95, split-query) — сосуществует".
- `:79-81` — "O2O и полная форма M2M реализованы после слайса B; отложены только составные
  junction-селекторы и M:N под `AsSingleQuery` (см. §6)."
- `:192-193` — "**Порядок родителей** — порядок первого появления строки в результате запроса;
  **порядок детей** — порядок строк; добавь `OrderBy` при необходимости."
- `:205-207` — "**Paging** (`Take`/`Page`/`Limit`/`Offset`) ограничивает **родителей**, не
  денормализованные строки: паренты выбираются в подзапросе с лимитом, `LEFT JOIN` оборачивается вокруг."
- `:217` — "**Составной ключ** (несколько `IsKey`-колонок или несколько FK-колонок) — `NotSupportedException`."
- `:261` — "M:N-`JoinInto` под `AsSingleQuery` и составной junction-селектор (исполнение O2O и M:N через junction теперь есть)."
- `:323` — "**Решено** (был открытый пункт): … отложены только составные junction-селекторы и M:N под `AsSingleQuery`."
- `:327-338` (Deferred / follow-ups) — `:335-338` "**Анонимная/производная проекция дочерней коллекции** …
  проекция детей в произвольную (анонимную) форму не предоставляется. Триггер: конкретный потребитель +
  согласованные API/семантика → отдельный issue."
- `:340-344` (contract limits accepted) — ":342 Смешивание обычного `Join`/`LeftJoin` с `JoinInto` …
  отклоняется"; ":343 `.Select(...)` / `.As(...)` поверх `JoinInto` отклоняются"; ":344 Стичинг/дедуп
  выполняют только list-терминалы".

**`docs/specs/roadmap/sql-capabilities-gap-analysis.md`** item 49 (eager loading):

- `:196` — "49. **Eager loading of a graph (`LoadWith`) — level one <span style="color:green">shipped</span>
  in two modes (`1.0.9-a`, issue #107).**"
- `:201-202` — "The opt-in `AsSingleQuery()` collapses both levels into one denormalized `LEFT JOIN`
  command …"
- `:205-208` — "Contract limits: split is the default; `AsSingleQuery()` requires a mapped parent key and a
  child query reduced to `.Where(...)` (other child shapes are rejected), and `LoadWith`/`AsSingleQuery`
  cannot be composed with `Join`/`As`/`Select`/`ArrayJoin`/`Pivot`/`SelectMany`/`GroupJoin`."
- `:208-211` — "**Nested (level 2+) loads**, support for the previously-rejected child shapes / child
  `.IgnoreFilters` in single-query, extending the shared eager integration suite to ClickHouse and a
  memory-time follow-up (single-query allocates ~1.68x split) are **deferred**."
- `:213-215` — "The work plan (`todo_eager_loading.md`) was deleted after shipment; the deferred items
  listed above are tracked here, in the guide and in `docs/specs/design/code-smells-review.md`."
- `:30` and `:724` (workstream 13 row) — "**Slices A+B shipped (1:1 and M:N execution done); slice C
  open** … **implicit joins (slice C)** and composite keys remain open".

**`docs/specs/comparison/linq2db-backlog-gap-analysis.md`**:

- `:54` (epic row) — "`epic: eager-load` | 12 | `[Association]`, `LoadWith`, `Include`, ordering/strategy |
  … | **Частичный паритет**: … (остатки: составные junction-селекторы, составные ключи, M:N под
  `AsSingleQuery`); вывод по конвенции FK отсутствует и у linq2db; **ordering/strategy — out-of-scope**".
- `:88` — "| `#5904`, `#5937`, `#5941`, `#5940`, `#5865` | eager-load ordering/strategy | **<span
  style="color:orange">Out-of-scope</span>** |".
- `:110` — "| `#4139` | composite-объекты для associations | **Out-of-scope** |".
- `:346-348` (§4 deliberate out of scope) — "**Inheritance/TPH** (и остатки `epic: eager-load`: составные
  junction-селекторы, составные ключи, M:N под `AsSingleQuery`) …".
- `:405` — association/eager-load row, "остатки: … составные junction-селекторы, составные ключи, M:N под `AsSingleQuery`".

**`docs/specs/comparison/linq2db-comparison.md`** ("deliberately leaves out" claim):

- `:220` — "## Deliberate boundaries: what nextorm leaves to linq2db".
- `:233-234` — "what linq2db still adds is **eager-load ordering/strategy** — convention-over-FK inference
  is absent in both, since associations are declared in each library."
- `:276-279` (Summary) — "linq2db remains the better fit only when the same layer must also track changes,
  generate the data layer from a live schema, or **expose eager-load ordering/strategy — surface nextorm
  deliberately leaves out** (O2M/M2O/O2O relationships, many-to-many through a junction,
  declared-relationship implicit navigation and level-1 eager loading are already covered)."
- `:109` (capability matrix) — "level-1 `LoadWith` (split / single-query) … composite keys, a composite
  junction selector and a many-to-many `JoinInto` under `AsSingleQuery`".

**`docs/specs/design/code-smells-review.md`**:

- `:8126-8129` — "**Eager loading (#107, level one shipped).** Отложено: вложенные (level 2+) `LoadWith`;
  ранее отклонённые формы child-запроса и child `.IgnoreFilters` в single-query; распространение общей
  eager-интеграции на ClickHouse; аллокационный follow-up single-query (~1.68× split). Docs:
  `docs/advanced/eager-loading.md`; gap-analysis §4 п.49."
- `:8106-8109` — plans deleted as shipped: "`todo_eager_loading.md` (#95, level one shipped) … удалены как отгруженные".

**`docs/specs/comparison/capability-matrix.md`**:

- `:110` — "Navigation properties / relationships / eager loading | **partial** — … level-1 `LoadWith`
  eager loading (split-query by default, **opt-in `AsSingleQuery`**), … composite keys, a composite
  junction selector or a many-to-many `JoinInto` under `AsSingleQuery` are rejected". (NB: `AsSingleQuery`
  naming is stale — see §4; removed by #184.)

**`docs/specs/design/implicit-navigation-queries.md`**:

- `:29` (non-goals) — "SQL `SelectMany`, финальная материализация вложенной коллекции/группы,
  **автоматическая загрузка/заполнение графа**, составные ключи связей/junction, конвенции именования …".
- `:63` — "… включая captured enumerable и composite keys." (reject path).
- `:25` — "6. **M2M через явный junction**, вложенная навигация/query scopes в пределах существующих возможностей движка."

### 2. Decomposition / task-breakdown / milestone proposals

- `docs/specs/roadmap/todo_navigation_properties.md:37-45` — decomposition table: "| **A — фундамент** |
  … | | **B — `JoinInto`** | Явная single-query загрузка O2M/M2O … | | **C — неявные join'ы** | … Отдельный цикл после B |".
- `docs/specs/roadmap/todo_navigation_properties.md:301-310` — PR split: "**PR1 (слайс A)** … **PR2
  (слайс B1)** — `JoinInto` LEFT … **PR3 (слайс B2)** — INNER, `Where`, paging парентов, несколько
  коллекций … Слайс C … отдельная спека … tracking — [#148]( … ) (milestone `1.0.9-b`)."
- `docs/specs/status/collection-1.0.9-rc2.md:15-18` — relationship residuals admitted as **excluded-gap**
  placeholders: "**D171** (#171) … issue stays **OPEN** in milestone `1.0.9-rc2`. Trigger: concrete
  consumer + agreed API." / "**D172** (#172) — awaiting-consumer … docs `relationships.md:67` record the
  feature as *not provided* … stays **OPEN** … Trigger: consuming scenario + agreed API/semantics." (gh
  now shows both CLOSED — see §5.)
- `docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md:19` — EagerLoadMode is its own task: "D184, issue
  #184: заменить отдельный публичный `AsSingleQuery()` параметром `EagerLoadMode mode = EagerLoadMode.Default`
  у `LoadWith` …".
- `docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md:25` — out of scope: "**Вне scope:** новая
  архитектура eager loading, изменение JOIN/stitching/chunking … поддержка нового eager-loading пути
  ClickHouse (**deferred**, триггер — включение провайдера в общий eager suite …)".
- `docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md:164-173` — 10-phase table (phase 6/7
  quoted in §3).
- No file naming the 4 gaps (named/selectable strategy, child-ordering remap, per-parent Take/Skip,
  level-2) as separate planned tasks was found — see Not found.

### 3. Recorded relationship between issue #20 and builder-surface eager-load items

- `docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md:7` — "**Трекинг:** GitHub issue
  [#20](…) — «TODO: LINQ support», статус OPEN; milestone **1.2-a.1** …".
- `:17` — "**InMemory исключён** из новой parity-приёмки; существующие InMemory/builders сохраняются как есть."
- `:40` (non-goal) — "Глобальная замена существующих builders или обязательная миграция."
- `:59` — "Нативные **declared relationships** #105/#148 — это **уже поставленный (delivered) scope**, а не
  undone новым roadmap."
- `:164-173` phases. Verbatim (English table row `:170`): "| 6 | Complex shapes | non-flat `GroupJoin`,
  `IGrouping`, nested collections/DTO, multiple-query plans, no hidden N+1; ordering/batching/reassembly,
  resource lifecycle. …". Row `:171`: "| 7 | EF import/roots | navigation, `Include`/`ThenInclude`,
  filters, `EF.Property`, `EF.Functions`, raw SQL, split/single. … EF Include/loading зависит от фазы 6. |
  1 (metadata), 6 (loading) |".
- `:175` (DAG) — "6 зависит от 4–5; … EF Include/loading зависит от 6".
- Issue #20 body (verbatim, `gh issue view 20 --json body`), checklist: "- [ ] **6 Complex shapes** — non-flat
  `GroupJoin`, `IGrouping`, nested collections/DTO, multiple-query plans без hidden N+1;
  ordering/batching/reassembly, resource lifecycle. …" and "- [ ] **7 EF import/roots** — navigation,
  `Include`/`ThenInclude`, filters, `EF.Property`, `EF.Functions`, raw SQL, split/single. …".
- Architecture lowerings (`:71`, `:83`): "нормализовать LINQ в общий IR и опускать его в существующие
  `QueryCommand`/`QueryDefinition`/planner/dialects/cache"; "immutable query IR / result shape → (lowering)
  → `QueryCommand` / `QueryDefinition`". **No statement found that the `IQueryable` tree is converted into
  `EntityBuilder`.**

### 4. Current public API surface for eager loading (src/)

- `src/nextorm.core/EagerLoadMode.cs:8-15` — `public enum EagerLoadMode { Default = 0, SplitQuery = 1,
  SingleQuery = 2 }`; XML-doc `:4-6` "Selects how the collections declared with `LoadWith` are loaded. The
  mode belongs to the whole builder …".
- `src/nextorm.core/Builders/EntityBuilder.cs:563-568` — `public EntityBuilder<TEntity> LoadWith<TChild,
  TKey>(Expression<Func<TEntity, ICollection<TChild>>> collection, Func<IDataContext,
  EntityBuilder<TChild>> childQuery, Expression<Func<TEntity, TKey>> parentKey,
  Expression<Func<TChild, TKey>> childKey, EagerLoadMode mode = EagerLoadMode.Default)`.
- `src/nextorm.core/Builders/EntityBuilder.cs:535-539` — "`mode` selects the whole builder's loading
  strategy. `EagerLoadMode.Default` inherits the mode already chosen by an earlier `LoadWith` (split when
  none was); an explicit `EagerLoadMode.SplitQuery` or `EagerLoadMode.SingleQuery` applies to every
  declared collection …".
- `AsSingleQuery(): **ABSENT from src/**` — `rg -n "AsSingleQuery" src/` → no matches; `roslyn refs
  NextORM.Core.EntityBuilder.AsSingleQuery` → "symbol not found". Historical removal recorded:
  `docs/specs/roadmap/evidence-01-eager-loading-single-query-api.md:44-51` — "**Superseded (D184 / #184,
  `1.0.9-rc2`).** … D184 removed `AsSingleQuery()` and folded the choice into an optional `EagerLoadMode
  mode = EagerLoadMode.Default` parameter of `LoadWith` …".
- `src/nextorm.core/Builders/EntityBuilder.cs:662-667` — `JoinInto<TChild>(EntityBuilder<TChild> child,
  Expression<Func<TEntity,TChild,bool>> predicate, Expression<Func<TEntity,ICollection<TChild>>>
  collection, Action<JoinOptions>? options = null)` (LEFT default).
- `:683-688` — `JoinInto<TChild>(… ICollection<TChild> collection, JoinType joinType, Action<JoinOptions>? options = null)`.
- `:741-746` — `JoinInto<TChild>(… Expression<Func<TEntity,TChild?>> navigation, Action<JoinOptions>? options = null) where TChild : class` (one-to-one).
- `:764-770` — one-to-one with `JoinType`.
- `:809-816` — `JoinInto<TChild, TKey>(… Expression<Func<TEntity,TKey>> parentKey,
  Expression<Func<TChild,TKey>> childKey, Action<JoinOptions>? options = null)` (explicit-key fallback).
- `src/nextorm.core/Builders/EntityBuilder.cs:1119-1142` — `UnsupportedSingleQueryChildShapes()` names
  `OrderBy` (`:1122-1123`), `Limit/Offset/Page` (`:1125-1126`), `Distinct`, `GroupBy`, `Having`,
  `DistinctOn`, `SelectWhereExtreme`, … — "Only the child's own `Where` is supported; every other shape
  (ordering, paging, distinct, grouping, joins, table modifiers, ...) is reported so the single-query path
  can reject it" (`:1112-1116`).
- **Nested / ordering / strategy / per-parent Take/Skip options:** none found as eager-load parameters.
  `rg -n -i "Take|Skip|Limit|Offset|Page|OrderBy|nested" src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`
  → no matches. The only ordering in the eager-load surface is the general `EntityBuilder.OrderBy` family
  (`src/nextorm.core/Builders/EntityBuilder.cs:3724-3770`), which orders the parent query, not children.
- Public docs confirm no nesting: `docs/advanced/eager-loading.md:11` — "restricted to **level one (no
  nested loads)**"; `:125` — "The child query is a plain `EntityBuilder<TChild>`; it carries no loader of
  its own and nested `LoadWith` calls are **not honoured**, so grandchild collections are not populated.
  N-level eager loading (level two and deeper) is **not supported** in either split or single-query mode".
- Child ordering is documented as row order, not a remap: `docs/advanced/eager-loading.md:152` — "**Child
  order follows the child query.** Children keep the order the child statement returned them in, so add an
  `OrderBy` to `childQuery` when the order matters."; `:151` parent order preserved.
- Split-path source (no desired-order option, no per-parent limit): `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:228-235`
  `Load(…)` runs `BuildChild(context, parentScope).Where(BuildPredicate(chunk)).ToList()`; assignment in
  `EagerLoadSpec.Assign` preserves child-query order.

### 5. Open GitHub issues (#20 and others)

`gh issue list --repo AlexeyShirshov/nextorm --state open --limit 200` (verbatim, 14 rows):

```
207	OPEN	Uniform cancellation handling during projection build
201	OPEN	Perf: M12 #3 — паритет cached-пути с LINQ to DB по времени и аллокациям
187	OPEN	Repository API: provider-neutral DbContext and bounded query DELETE
129	OPEN	ClickHouse: распределённые табличные функции (remote/cluster/s3/file)
69	OPEN	TODO: Шардирование (consistent hashing) поверх mapping scope
65	OPEN	TODO: Mapping scope — scope-зависимый маппинг сущности
62	OPEN	TODO: ClickHouse AggregateFunction(...) state type (-State/-Merge, runningAccumulate)
53	OPEN	IF6: Заморозка и трекинг публичного API (PublicApiAnalyzers + PublicAPI.Shipped/Unshipped) к 1.0
51	OPEN	WebContext
50	OPEN	FileContext
34	OPEN	POCO
26	OPEN	TODO: struct support (zero allocations)
20	OPEN	TODO: LINQ support
16	OPEN	TODO: Thread safety
```

- **None** of the 4 eager-load gaps (named/selectable strategy, child-ordering remap, per-parent
  Take/Skip, level-2 nested) has an OPEN issue title.
- `#20` — `gh issue view 20`: `{"number":20,"state":"OPEN","title":"TODO: LINQ support","milestone":{"number":10,"title":"1.2-a.1"},"labels":[{"name":"enhancement"}],"closedAt":null}`.
- `#105` — `{"number":105,"state":"CLOSED","title":"Navigation properties / relationships (implicit joins)","milestone":{"number":17,"title":"1.0.9-a"},"closedAt":"2026-09-28T10:06:21Z"}`.
- `#148` — `{"number":148,"state":"CLOSED","title":"Неявные навигационные запросы: одиночные связи и AsEntityBuilder","milestone":{"number":18,"title":"1.0.9-b"},"closedAt":"2026-10-03T10:39:50Z"}`.
- `#95` — CLOSED, milestone #17 `1.0.9-a`, "TODO: Eager loading графа (`LoadWith`/`Include`)".
- `#107` — CLOSED, milestone #17 `1.0.9-a`, "Fix LoadWith: round-trip reduction + ignored terminals (split vs single-query)".
- `#184` — CLOSED, milestone #20 `1.0.9-rc2`, "LoadWith: fold AsSingleQuery into an EagerLoadMode parameter".
- `#171` — CLOSED 2026-10-10T05:33:36Z, milestone #20 `1.0.9-rc2`, "JoinInto over As/derived source: rejected now, revisit on demand".
- `#172` — CLOSED 2026-10-10T06:22:28Z, milestone #20 `1.0.9-rc2`, "Child-collection projection into anonymous/derived form (NORM.ChildCollection)".
- `gh issue list --search "eager OR nested OR ordering OR navigation OR relationship"` (state all) returns
  **no open** eager/ordering/nested/relationship issue; the only OPEN in that search is #20.

## Not found

- No file in `docs/specs/**` (excl. `experiments/**`) or `docs/superpowers/**` that decomposes the 4 gaps
  (named/selectable eager strategy + auto-dispatch; child-ordering guarantee/remap; per-parent Take/Skip;
  level-2 nested) into separate tasks. Searches over `docs/specs/roadmap/*.md`, `docs/specs/status/*.md`,
  `docs/superpowers/**/*.md` for `per-parent`, `limited children`, `Take/Skip`, `child ordering`,
  `ordering guarantee`, `selectable eager`, `auto-dispatch`, `named.*strategy` produced only unrelated
  matches (e.g. OneToOne per-parent identity scope in `rc2-170`, order-by-hint `D184` cache).
- No spec/issue text stating that the `IQueryable` tree of issue #20 will be implemented by **converting
  into `EntityBuilder`**. The roadmap records lowering into `QueryCommand`/`QueryDefinition`
  (`docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md:71,83`) and preserving builders
  (`:40`, `:59`).
- No `AsSingleQuery()` method or `AsSplitQuery()` method anywhere in `src/` (`rg` → no matches); only the
  `EagerLoadMode.SingleQuery`/`SplitQuery` enum values.
- No per-parent child limit (`Take`/`Skip` per parent) or child-`OrderBy` parameter in the eager-load API;
  `EntityBuilderEagerLoading.cs` contains no `Take`/`Skip`/`OrderBy`/`nested` tokens.
- No dedicated query-smoke `todo_eager_loading.md` remains in-tree (deleted after shipment:
  `code-smells-review.md:8106-8109`, `sql-capabilities-gap-analysis.md:213-215`).

## Open questions

- The comparison-doc wording "eager-load ordering/strategy — surface nextorm deliberately leaves out"
  (`linq2db-comparison.md:233,276-279`) vs `linq2db-backlog-gap-analysis.md:54` "ordering/strategy —
  out-of-scope": both are DECISION/OUT-OF-SCOPE statements, but neither is tied to a tracked issue or a
  revisit trigger; whether the user intends to reverse that decision is not recorded anywhere found.
- The `capability-matrix.md:110` cell still reads "opt-in `AsSingleQuery`", although the method was removed
  by #184 (`EagerLoadMode` parameter). This is a factual doc/source mismatch, not resolved in-tree.
- `docs/advanced/eager-loading.md:125` states nested `LoadWith` "not honoured", while the split path calls
  `BuildChild(...).ToList()` (`EntityBuilderEagerLoading.cs:232`) and `ToList` consults `builder.LoadSpecs`
  (`EntityBuilderExtensions.cs:486,512-514`); no explicit nested-load guard was found in
  `LoadWith`/`EntityBuilderEagerLoading`. Whether nested calls are silently executed or actually dropped is
  not conclusively established by the inspected code + docs (no nested-eager test found).
- Whether #171/#172 (now CLOSED 2026-10-10) supersede the `collection-1.0.9-rc2.md:15-18` "excluded-gap …
  stays OPEN" record is not restated in the collection tracker (it was written at r1).
