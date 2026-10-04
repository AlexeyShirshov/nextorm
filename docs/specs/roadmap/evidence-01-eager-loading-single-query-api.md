# Evidence 01: eager-loading single-query API — prior design decisions

## Question

Read-only fact-gathering: what API shape was agreed for single-query eager loading in nextorm —
is `AsSingleQuery` a separate builder method or a parameter/option of `LoadWith`? Plus: every
`docs/specs/**` place stating/deciding the shape (`AsSingleQuery`, `single-query`, `single query`),
the recorded status/approval state of the eager-loading design (grilled/agreed, written spec
approved, implemented, open questions about API shape), and the state/title/milestone of GitHub
issues #95 and #107 (repo `AlexeyShirshov/nextorm`).

## Commands

All commands run in `/home/alex/sources/nextorm` on 2026-10-04 (env date), git worktree branch
`1.0.9-b`.

- `git remote -v` →
  ```
  origin	git@github.com:AlexeyShirshov/nextorm.git (fetch)
  origin	git@github.com:AlexeyShirshov/nextorm.git (push)
  ```
- `git log --oneline --all -- docs/specs/roadmap/todo_eager_loading.md | head -30` →
  ```
  f113f6e documentation
  29853ad #107 LoadWith: opt-in single-query (AsSingleQuery), ToArray stitching, cancellation
  6ac5c0b #95 eager loading: level-1 split-query LoadWith (IN-batched child query)
  ...
  ```
- `git log --diff-filter=D --oneline -- docs/specs/roadmap/todo_eager_loading.md` → `f113f6e documentation`
- `test -f docs/specs/roadmap/todo_eager_loading.md` → `ABSENT` (deleted in `f113f6e`, 2026-09-29)
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md | nl -ba` (last #107 version — full text quoted below)
- `git show 6ac5c0b:docs/specs/roadmap/todo_eager_loading.md | nl -ba` (last #95 version)
- `git grep -n 'AsSingleQuery' -- 'docs/specs/**' ':!docs/specs/experiments/**'`
- `git grep -n -i 'single-query' -- 'docs/specs/**' ':!docs/specs/experiments/**'`
- `git grep -n --fixed-strings 'AsSingleQuery' -- 'docs/specs/design/API-NAMING-REVIEW.md'` → `NO MATCH`
- `git grep -n --fixed-strings 'AsSingleQuery' -- 'docs/specs/release-1.0.9-a.md'` → `NO MATCH`
- `git log --all --diff-filter=A --name-only -- 'docs/specs/status/**' | grep -i -E 'eager|95|107|load'` → empty
- `gh issue view 95 --repo AlexeyShirshov/nextorm --json number,title,state,milestone,labels,closedAt,url`
- `gh issue view 107 --repo AlexeyShirshov/nextorm --json number,title,state,milestone,labels,closedAt,url`
- `gh --version` → `gh version 2.101.0 (2026-09-15)`; `gh auth status` → `✓ Logged in to github.com account AlexeyShirshov`, token scopes `gist, read:org, repo, workflow`.

## Facts

### 1. Agreed API shape — `AsSingleQuery` is a separate builder method, not a `LoadWith` parameter/option

- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:13-19` (status block, issue #107) —
  “Уровень-1 eager loading **поставлен** в двух режимах. По умолчанию — split-query:
  `EntityBuilder<TEntity>.LoadWith<TChild,TKey>(collection, childQueryFactory, parentKey, childKey)`
  даёт 2 round trip … Опционально `AsSingleQuery()` сводит всё к одной денормализованной команде
  `LEFT JOIN`”.
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:24-28` (contract limits, marked accepted) —
  “**Границы контракта (приняты, `1.0.9-a`).** Split — режим по умолчанию; opt-in `AsSingleQuery()`
  требует, чтобы родительский ключ был частью маппинга, а дочерний запрос сводился к `.Where(...)`
  (прочие формы дочернего запроса отклоняются). `LoadWith`/`AsSingleQuery` не компонуются с
  `Join`/`As`/`Select`/`ArrayJoin`/`Pivot`/`SelectMany`/`GroupJoin`; N-level eager loading не
  поддерживается.”
- Source (public surface): `src/nextorm.core/Builders/EntityBuilder.cs:518` —
  `public EntityBuilder<TEntity> LoadWith<TChild, TKey>(...)`; `src/nextorm.core/Builders/EntityBuilder.cs:567` —
  `public EntityBuilder<TEntity> AsSingleQuery()`. `AsSingleQuery()` takes no arguments; it clones the
  builder and sets `b._singleQuery = true` (`EntityBuilder.cs:569-571`; backing field `_singleQuery`
  at `EntityBuilder.cs:49`, exposed as `internal bool SingleQuery` at `EntityBuilder.cs:111`).
- Public docs (not specs, supporting): `docs/advanced/eager-loading.md:55` heading
  “`## Single-query mode (`AsSingleQuery`)`”; `:57` — “Call `AsSingleQuery()` on the builder to fetch
  the parents and every declared collection with **one** denormalized command instead”; usage sample
  `docs/advanced/eager-loading.md:62-63` chains `.LoadWith(...).AsSingleQuery()`.
- No evidence found anywhere in `docs/specs/**` of `AsSingleQuery` being a parameter/option of
  `LoadWith` (no `singleQuery` parameter; `git grep -n -i 'singleQuery' -- 'docs/specs/**'` only
  matches the public method/option wording, and `src` only has the private `_singleQuery` field).
- History: `git show 6ac5c0b:docs/specs/roadmap/todo_eager_loading.md` (#95) contains **no**
  `AsSingleQuery` mention — the single-query mode was introduced by #107 (commit `29853ad`,
  “#107 LoadWith: opt-in single-query (AsSingleQuery), ToArray stitching, cancellation”).

### 2. Every `docs/specs/**` place stating/deciding `AsSingleQuery` / `single-query`

Current working tree (exact `file:line`, verbatim quote):

- `docs/specs/roadmap/sql-capabilities-gap-analysis.md:196` — “**Eager loading of a graph
  (`LoadWith`) — level one <span style="color:green">shipped</span> in two modes (`1.0.9-a`, issue #107).**”
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md:201-202` — “The opt-in `AsSingleQuery()`
  collapses both levels into one denormalized `LEFT JOIN` command — any number of parent keys, no
  chunked `IN` list and no silent fallback to split”
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md:205-208` — “Contract limits: split is the
  default; `AsSingleQuery()` requires a mapped parent key and a child query reduced to `.Where(...)`
  (other child shapes are rejected), and `LoadWith`/`AsSingleQuery` cannot be composed with
  `Join`/`As`/`Select`/`ArrayJoin`/`Pivot`/`SelectMany`/`GroupJoin`. Nested (level 2+) loads … are
  deferred.”
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md:213-215` — “The work plan
  (`todo_eager_loading.md`) was deleted after shipment; the deferred items listed above are tracked
  here, in the guide and in `docs/specs/design/code-smells-review.md`.”
- `docs/specs/roadmap/todo_navigation_properties.md:20-21` — “…явную single-query загрузку дочерней
  коллекции `JoinInto` (`LEFT JOIN` + дедуп родителя + in-memory группировка)”
- `docs/specs/roadmap/todo_navigation_properties.md:25-26` — “eager loading уровня 1 — split-query
  `LoadWith` (#95), у которого связь задаётся селекторами на месте вызова, а не метаданными.”
- `docs/specs/roadmap/todo_navigation_properties.md:33` — “Изменение `LoadWith` (#95, split-query) —
  сосуществует; связь по-прежнему задаётся селекторами.”
- `docs/specs/roadmap/todo_navigation_properties.md:42` — “| **B — `JoinInto`** | Явная single-query
  загрузка O2M/M2O на всех провайдерах + in-memory | Требует A |”
- `docs/specs/roadmap/todo_navigation_properties.md:81` — “…отложены только составные junction-селекторы
  и M:N под `AsSingleQuery` (см. §6).”
- `docs/specs/roadmap/todo_navigation_properties.md:261` — “M:N-`JoinInto` под `AsSingleQuery` и
  составной junction-селектор”
- `docs/specs/roadmap/todo_navigation_properties.md:294` — “…`AsSingleQuery` отклоняется, `ToCommand`
  даёт денормализованные строки).”
- `docs/specs/roadmap/todo_navigation_properties.md:323` — “**Решено** (был открытый пункт): …
  отложены только составные junction-селекторы и M:N под `AsSingleQuery`.”
- `docs/specs/comparison/capability-matrix.md:110` — “level-1 `LoadWith` eager loading (split-query by
  default, opt-in `AsSingleQuery`)” and “…a many-to-many `JoinInto` under `AsSingleQuery` are rejected”.
- `docs/specs/comparison/linq2db-comparison.md:109` — “…level-1 `LoadWith` (split / single-query)…”
- `docs/specs/comparison/linq2db-comparison.md:162` — “…`LoadWith` (split-query by default, opt-in
  `AsSingleQuery`) with global query filters applied to children”
- `docs/specs/comparison/linq2db-backlog-gap-analysis.md:54` — “…уровень-1 `LoadWith` + неявная
  навигация …; остатки: составные junction-селекторы, составные ключи, M:N под `AsSingleQuery`…”
- `docs/specs/comparison/linq2db-backlog-gap-analysis.md:340` — “…составные junction-селекторы, составные
  ключи, M:N под `AsSingleQuery`)”
- `docs/specs/comparison/linq2db-backlog-gap-analysis.md:398` — “…составные ключи, M:N под `AsSingleQuery`…”
- `docs/specs/comparison/linq2db-backlog-gap-analysis.md:499` — “`AsSingleQuery`. В матрицу и сравнение
  добавлены уже поставленные, но ранее не отражённые поверхности:”
- `docs/specs/ru/comparison/linq2db-comparison.md:109` — “уровень-1 `LoadWith` (split / single-query) …
  many-to-many `JoinInto` под `AsSingleQuery`”
- `docs/specs/ru/comparison/linq2db-comparison.md:165` — “уровень-1 `LoadWith` (по умолчанию split-query,
  включаемый `AsSingleQuery`)”
- `docs/specs/status/collection-1.0.9-b.md:47` — “Residual deferrals (rejected by design, not backlog):
  composite junction selectors and M:N under `AsSingleQuery`.”
- `docs/specs/design/code-smells-review.md:8102-8105` — “**Eager loading (#107, level one shipped).**
  Отложено: вложенные (level 2+) `LoadWith`; ранее отклонённые формы child-запроса и child
  `.IgnoreFilters` in single-query; распространение общей eager-интеграции на ClickHouse;
  аллокационный follow-up single-query (~1.68× split). Docs: `docs/advanced/eager-loading.md`;
  gap-analysis §4 п.49.”
- `docs/specs/design/API-NAMING-REVIEW.md:55` — #95 API-naming record (split-query only):
  “**Обновление 27.09.2026 (uncommitted worktree — жадная загрузка `LoadWith`, issue #95, уровень-1
  split-query).** Добавлен **один** публичный член `EntityBuilder<TEntity>.LoadWith<TChild,TKey>(…)`;
  … P0/P1 по **именам** нет … Открыт P2 **EL1** (трекинг `PublicAPI.*.txt`, Шаг 5).”
  `AsSingleQuery` itself: `git grep -n --fixed-strings 'AsSingleQuery' -- docs/specs/design/API-NAMING-REVIEW.md`
  → `NO MATCH` (the new `AsSingleQuery()` surface from #107 is not listed in the naming review).
- `docs/specs/design/API-NAMING-REVIEW.md:5237` — mentions #107 only as a behavior change: “изменение
  поведения #107 (родительский `IgnoreFilters()` также отключает eager-loaded детей) зафиксировано в
  `docs/advanced/query-filters.md`.”
- `docs/specs/release-1.0.9-a.md:38` — “| [#107](https://github.com/AlexeyShirshov/nextorm/issues/107)
  | Fix LoadWith: round-trip reduction + ignored terminals (split vs single-query) |”;
  `:31` — “| [#95](https://github.com/AlexeyShirshov/nextorm/issues/95) | Eager loading графа
  (`LoadWith`/`Include`) |”. `git grep -n --fixed-strings 'AsSingleQuery' -- docs/specs/release-1.0.9-a.md`
  → `NO MATCH`.

Deleted plan (last content at commit `29853ad`, file removed in `f113f6e`):

- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:17-19` — “Опционально `AsSingleQuery()`
  сводит всё к одной денормализованной команде `LEFT JOIN`: >1000 ключей без чанкового `IN`-списка,
  без молчаливого отката к split; собственный `Where` дочернего запроса встраивается в предикат
  соединения, глобальные фильтры применяются.”
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:20-23` — terminals: “Сшивают четыре
  терминала: `ToList`/`ToListAsync` и `ToArray`/`ToArrayAsync`; `ToHashSet`, `ToDictionary`,
  `First`/`FirstOrDefault`, `Single*`, `ToEnumerable`, `ToAsyncEnumerable` и `ToCommand` eager loading
  не выполняют, а `Any`/`Count` скалярны.”
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:24-28` — accepted contract limits (quoted in §1).
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:29-33` — deferred + triggers:
  “ранее отклонённых форм дочернего запроса и дочернего `.IgnoreFilters` в single-query;
  распространение общего eager-интеграционного набора на ClickHouse; follow-up по памяти/времени
  (single-query аллоцирует ~1.68x от split) — триггер: конкретный потребитель/регрессия.”
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:36-39` — implementation/tests:
  “Реализация — `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`; … Тесты: core
  `EagerLoadingTests` + `EagerLoadingSingleQueryTests`, SQLite `EagerLoadingSqlGenerationTests`
  (split 2 round trip + `IN`, single-query ровно 1 команда), integration `CommonTestSuite.EagerLoading`.”
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:41` — “> Рабочий план (design RFC).”
- `git show 29853ad:docs/specs/roadmap/todo_eager_loading.md:90-94` — “`## Открытые вопросы`”:
  1. “Делаем ли вообще (пересмотр out-of-scope) или закрываем решением? От этого зависит объём.”
  2. “Если делаем — одна ступень (parent→children) или произвольная вложенность?”
  3. “JOIN+дедуп или split-query (два запроса)? Влияет на семантику `Page`/`Limit`.”

Earlier plan (last content at commit `6ac5c0b`, #95 — split-query only):

- `git show 6ac5c0b:docs/specs/roadmap/todo_eager_loading.md:13-19` — “**Статус реализации
  (27.09.2026, ветка `1.0.9-a`).** Уровень-1 split-query eager loading **поставлен**: … Загрузка
  учитывается только списочными терминалами `ToList`/`ToListAsync`; `ToCommand()` и прочие терминалы
  её игнорируют. **Отложено:** вложенность/N-level eager loading и вариант JOIN+дедуп — до появления
  конкретного потребителя / требования на вложенность (триггер пересмотра).”
- `git show 6ac5c0b:docs/specs/roadmap/todo_eager_loading.md:74-78` — same three “Открытые вопросы”
  (1, 2, 3) as above; the #95 text contains no `AsSingleQuery`.

Experimental artifacts (under `docs/specs/experiments/`, not live specs) also quote the API-naming
record: `docs/specs/experiments/112-csv-stream/artifacts/upstream.patch:528` repeats the #95
API-NAMING paragraph verbatim.

### 3. Recorded status / approval state

- No standalone approved spec/handoff/status file for eager loading exists:
  - `git log --all --diff-filter=A --name-only -- 'docs/specs/status/**' | grep -i -E 'eager|95|107|load'`
    → empty; `docs/specs/status/` contains no eager/#95/#107 file.
  - `find docs -iname '*eager*'` → only `docs/advanced/eager-loading.md`, `docs/ru/advanced/eager-loading.md`
    and generated `docs/_site/**` HTML; no spec file.
  - The only written plan was `docs/specs/roadmap/todo_eager_loading.md`, which self-identifies as a
    “design RFC” (`git show 29853ad:…:41`) and was deleted after shipment
    (`sql-capabilities-gap-analysis.md:213-215`, deletion commit `f113f6e` 2026-09-29).
- Design approval markers found: contract limits marked “**приняты**” (accepted) in
  `git show 29853ad:…:24`; API-NAMING-REVIEW records “P0/P1 по **именам** нет” at
  `docs/specs/design/API-NAMING-REVIEW.md:55` and an open P2 **EL1** (PublicAPI tracking). No
  “grill”/approved-written-spec marker for the eager-loading single-query design was found
  (`git grep -n -i 'grill' …` combined with eager/LoadWith/107/95 → no match).
- Implemented status (recorded as shipped):
  - `docs/specs/roadmap/sql-capabilities-gap-analysis.md:196` — “level one shipped in two modes
    (`1.0.9-a`, issue #107)”.
  - `docs/specs/roadmap/sql-capabilities-gap-analysis.md:213-215` — work plan deleted after shipment.
  - `docs/specs/design/code-smells-review.md:8102` — “Eager loading (#107, level one shipped).”
  - `docs/specs/release-1.0.9-a.md:10-11` — milestone `1.0.9-a`, “17 issues (#40, #52, #61, #67, #76,
    #94, #95, #100–#102, #104–#110), все реализованы и закрыты (open=0, closed=17).”
- Explicitly-open questions about the API shape (in the final deleted plan, never edited after the
  status blocks were added): `git show 29853ad:…:90-94` — the three questions quoted in §2. Question 3
  (“JOIN+дедуп или split-query (два запроса)?”) is the one the shipped design resolves as “split by
  default + opt-in `AsSingleQuery()`”; question 2 resolves to level-one only; the section itself was
  left in the document.

### 4. GitHub issues #95 and #107 (repo `AlexeyShirshov/nextorm`)

`gh` is available and authenticated as `AlexeyShirshov`.

- `#95` — `gh issue view 95 --repo AlexeyShirshov/nextorm --json number,title,state,milestone,labels,closedAt,url`:
  ```
  {"closedAt":"2026-09-27T18:06:07Z","labels":[],"milestone":{"number":17,"title":"1.0.9-a","description":"","dueOn":null},"number":95,"state":"CLOSED","title":"TODO: Eager loading графа (`LoadWith`/`Include`)","url":"https://github.com/AlexeyShirshov/nextorm/issues/95"}
  ```
- `#107` — `gh issue view 107 --repo AlexeyShirshov/nextorm --json number,title,state,milestone,labels,closedAt,url`:
  ```
  {"closedAt":"2026-09-28T10:06:10Z","labels":[],"milestone":{"number":17,"title":"1.0.9-a","description":"","dueOn":null},"number":107,"state":"CLOSED","title":"Fix LoadWith: round-trip reduction + ignored terminals (split vs single-query)","url":"https://github.com/AlexeyShirshov/nextorm/issues/107"}
  ```
- Both issues: state `CLOSED`, no labels, milestone number `17` title `1.0.9-a`.

## Not found

- `docs/specs/roadmap/todo_eager_loading.md` does not exist in the working tree — it was deleted by
  commit `f113f6e` (2026-09-29); its content was read from commit `29853ad` (last #107 version).
- No `docs/specs/status/**` file and no separate approved written spec for eager loading (#95/#107);
  the only written plan was the deleted `todo_eager_loading.md`, self-described as “design RFC”.
- `AsSingleQuery` is not mentioned in `docs/specs/design/API-NAMING-REVIEW.md` (fixed-string grep:
  `NO MATCH`) nor in `docs/specs/release-1.0.9-a.md` (`NO MATCH`); `release-1.0.9-a.md` references
  #107 only by its title.
- A `grill`/written-spec approval marker specifically for the eager-loading single-query design was
  not found in `docs/specs/**`.
- No evidence in `docs/specs/**` of `AsSingleQuery` being designed as a parameter/option of
  `LoadWith` (no `singleQuery` parameter recorded).

## Open questions

- Which design-decision artifact (if any) the orchestrator intends as the “approved spec” for the
  single-query API, given only the deleted `todo_eager_loading.md` “design RFC” existed.
- Whether the API-NAMING-REVIEW P2 **EL1** (PublicAPI tracking) item, opened for the #95 split-query
  `LoadWith` surface, was ever extended to cover the `AsSingleQuery()` surface added by #107
  (`AsSingleQuery` absent from API-NAMING-REVIEW).
