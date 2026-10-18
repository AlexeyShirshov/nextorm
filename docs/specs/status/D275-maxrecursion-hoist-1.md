# D275 — OPTION (MAXRECURSION n) должен переживать hoisting

| Поле | Значение |
|---|---|
| Issue | [#275](https://github.com/AlexeyShirshov/nextorm/issues/275) |
| Milestone | `1.0.9-rc3` |
| Branch | `1.0.9-rc3` |
| Baseline | `6c9dd49b` |
| Plan state | `ready` |
| Cycle | `1` |
| Plan revision | `r=1` |
| Attempt | `1/3` |
| Contract | `rv=1` |
| Status file | `docs/specs/status/D275-maxrecursion-hoist-1.md` |
| Evidence root | `.pdca/D275/rv1/` |
| Manifest | `.pdca/D275/rv1/manifest.json` |
| Note | lane mandates `docs/specs/status/` for this cycle (repo default `.pdca/status/` overridden) |

**Факт старта PLAN:** рабочее дерево — один tree на branch `1.0.9-rc3`, наблюдаемый HEAD `6c9dd49b`
(= baseline брифа). Дерево несёт чужую незакоммиченную работу другой lane (24 пути: `AGENTS.md`,
`.github/workflows/dotnet.yml`, `docs/superpowers/specs/**`, `.pdca/status/integration-branch-coverage-*`,
`tests/nextorm.integration.tests/**`); она не читается, не правится, не стейджится и не откатывается.
Stage — только явные пути этой задачи.

## Цель и непреложные ограничения

`QueryPlanner.RenderSource` строит `WITH`-префикс через
`SqlSourceRenderer.MakeWithClause(in ctx, ctes, out _)` (`src/nextorm.core/DataContext/QueryPlanner.cs:153`)
и **выбрасывает** `out`-значение `maxRecursionStmt`. Сам `WITH`-префикс возвращается отдельным
элементом кортежа, а `OPTION (MAXRECURSION n)` — это хвост **всего statement**, а не часть
`WITH`-префикса. Поэтому при hoisting рекурсивного CTE в source (INSERT…SELECT, MERGE, CTAS/SELECT…INTO,
шаг batch) опция теряется: SQL уходит на сервер без запрошенного лимита, и SQL Server применяет
дефолт в 100 уровней.

Цель — довести `maxRecursionStmt`, возвращаемый `MakeWithClause`, до финальной сборки statement и
приклеить его ровно один раз в конце тела statement (перед необязательным терминатором `;`) для всех
шести мест прикрепления в `DataContext.cs`.

Непреложные ограничения:

- **§13 freeze.** Footprint = `QueryPlanner.RenderSource` (+ три его forwarding-overload'а) и его
  вызовы в build-методах `DataContext.cs`. Контракт `SqlSourceRenderer` не меняется.
  `SqlMutationBuilder` — только если строго необходимо (план исходит из того, что не нужно).
  `CteHoister`, `EntityBuilder`, construction-path `QueryCommand` не трогаются.
- `RenderSource` — `internal`; публичного API изменения нет. Сигнатура `MakeWithClause` неизменна
  (тот же `out string? maxRecursionStmt`), поэтому `SqlSourceRenderer.cs:22,39-44` не правится.
- Существующие хвосты обычных SELECT/UPDATE-JOIN/DELETE-JOIN (`SqlBuilder.cs:618-623`,
  `QueryPlanner.cs:484-492`, `QueryPlanner.cs:533-541`) остаются посимвольно прежними.
- Non-SQL-Server провайдеры: `MakeMaxRecursion` возвращает `null` (`SqlDialectBase.cs:397`), поэтому их
  SQL обязан остаться **байт-идентичным**.
- Никаких бенчмарков. Никаких новых валидаций `maxRecursion` (поведение 0/отрицательных/out-of-range
  сохраняется как есть — см. «Что не сказала постановка»).
- Один tree, без worktree, без push, без чужих коммитов. CRLF после каждой записи.
- `docs/specs/design/code-smells-review.md` не редактируется (Finding 150 — auditor-owned).

## Критерии приёмки

**Определение «tail»:** позиция сразу после полного тела statement и перед необязательным **терминатором**
`;`, который принадлежит statement'у (SQL Server требует `;` после `MERGE`, `SqlServerDialect.cs:137`
→ `MakeMergeStatementTerminator`). Формально:

```
<statement body>[<optional whitespace>][;][<optional whitespace>]
```

Опция вставляется между `body` и `;`, а не после `;`.

| ID | Требуемый результат | Негативный случай |
|---|---|---|
| **R275-01** | Для всех шести потребителей (INSERT…SELECT, MERGE key path, full MERGE, CTAS/SELECT…INTO, batch CTAS, batch SELECT) при рекурсивном CTE с `maxRecursion: 7` опция `option (maxrecursion 7)` присутствует **ровно один раз**, в позиции tail, со значением звонящего. | Опция отсутствует; присутствует дважды; стоит в середине statement (после SELECT-тела перед `OUTPUT`/`USING`/`INTO`); значение подменено (100 или чужое); для MERGE — после `;`. |
| **R275-02** | Без `maxRecursion` (`null`) опция не появляется вообще, SQL **байт-идентичен** pre-change baseline. | Появился `option (maxrecursion 100)` или любой другой хвост; дельта форматирования. |
| **R275-03** | Не-SQL-Server провайдеры (PostgreSQL, SQLite, MySQL/MariaDB, ClickHouse) выдают байт-идентичный SQL для тех же форм. | Любая дельта SQL или новый `OPTION`-фрагмент на этих диалектах. |
| **R275-04** | Обычный SELECT, UPDATE-JOIN и DELETE-JOIN с рекурсивным CTE не меняются: прежний хвост из `SqlBuilder.cs:618-623` / `QueryPlanner.cs:484-492` / `:533-541`. | Изменённый хвост, дублирование опции, изменение места `OPTION` относительно `FOR JSON`/hints. |
| **R275-05** | Batch: опция statement-local — ровно по одной на затронутый шаг, без дублирования и без протечки в соседние шаги (включая affected+unaffected). | Опция одного шага попадает в другой; опция теряется при непустом префиксе параметров `b<i>_`; опция после внешнего `;`-join. |
| **R275-06** | Scoped-тесты зелёные с **ненулевым** числом выбранных тестов; `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings/0 errors; footprint укладывается в §13; CRLF сохранён. | Красный/пустой прогон, warning/error в build, правки вне §13, LF/mixed EOL. |
| **R275-07** | EN+RU документация: `docs/guide/10-cte.md` + `docs/ru/guide/10-cte.md` объясняют, что явный лимит рекурсии сохраняется при CTE → INSERT…SELECT / MERGE / CTAS/SELECT…INTO / batch-шаг; ссылок на `docs/specs/**` нет. | Односторонняя правка, неверное утверждение, ссылка на specs. |

Каждый критерий несёт негативный случай из правой колонки; CHECK проверяет обе половины.

## Минимальное решение

Выбран вариант **(a) — протащить опцию в кортеже и приклеить на финальной сборке statement'а**.
Отклонены:

- **(b) приклеивать внутри `RenderSource.Sql`** — для INSERT…SELECT/MERGE/CTAS опция оказалась бы в
  середине statement (после SELECT-тела, но до `;` MERGE и до `OUTPUT`/`RETURNING`), т.е. синтаксически
  неверно и нарушает R275-01.
- **(c) складывать в `WithSql`** — `WITH` предшествует statement, а `OPTION` его завершает; это
  гарантированно неверный T-SQL.
- **(d) новая абстракция/хелпер-тип** — deferred: второго потребителя нет, кортеж уже существует.
  Локальная приватная функция добавления хвоста живёт в `DataContext.cs` рядом с шестью вызовами
  (шесть потребителей — не «второй потребитель» абстракции, а один и тот же приём; новый тип/интерфейс
  не вводится).

Дизайн:

1. Внутренний кортеж расширяется: `(string? WithSql, string Sql, List<Parameter> Parameters, string? MaxRecursionStmt)`
   для всех трёх overload'ов `RenderSource` (`QueryPlanner.cs:116`, `:121`, `:126`).
2. В теле (`QueryPlanner.cs:153`) фиксируется существующий `out`: `MakeWithClause(in ctx, ctes, out var maxRecursionStmt)`;
   в `return` (`QueryPlanner.cs:169`) добавляется четвёртый элемент. `MakeWithClause` не меняется.
3. Приклеивание — только на финальной сборке statement'а, ровно один владелец на statement:

| # | Потребитель | `RenderSource` | Место сборки | Tail сейчас | Точка вставки |
|---|---|---|---|---|---|
| 1 | INSERT…SELECT | `DataContext.cs:1195` | `:1199` `withSql + insertSql` | `insert … select …` без терминатора | после `insertSql`, в конец результата |
| 2 | MERGE key path (source) | `:1262` | `:1264` `withInsert + insertSql` | `merge … ;` (SQL Server) | перед терминатором `;` |
| 3 | full MERGE | `:1301` | `:1305` `withSql + fullSql` | `merge … ;` | перед терминатором `;` |
| 4 | CTAS/SELECT…INTO (single) | `:1329` | `:1330` `MakeCreateTableAsSelect(...)` | SQL Server: `with … select … into …` без терминатора; прочие: `create table … as <body>` без терминатора | после результата `MakeCreateTableAsSelect` |
| 5 | batch CTAS | `:1398` | `:1399` → `BatchStatement(sql, …)` | как (4) | после результата, до `statements.Add` |
| 6 | batch SELECT | `:1408` | `:1409` `withSql + sourceSql` | `with … select …` без терминатора | после конкатенации, до `results.Add` |

4. Вставка перед терминатором: если строка (после `TrimEnd`) оканчивается на `;`, опция вставляется
   перед ним (`body + " " + option + ";"`); иначе — в конец. Ровно один вызов на statement; повторный
   вызов для того же statement'а невозможен, т.к. каждое из шести мест возвращает одну строку.
5. Batch: `BatchPlan.Statements` — отдельные строки; `BatchPlan.ToSql()` соединяет их `"; "`
   (`IBatchExecutor.cs:212-236`), на SQL Server `BatchUsesJoinedCommand = true`
   (`SqlServerDialect.cs:67`). Поэтому опция остаётся внутри своей строки и не касается соседних шагов.

Guard/no-edit (не трогается):

- `SqlSourceRenderer.cs:22,39-44` — контракт `MakeWithClause` и вычисление `maxRecursion` (первое
  ненулевое значение по списку CTE) сохраняются.
- `SqlBuilder.cs:618-623` — обычный SELECT-хвост (`RenderQueryHints` + `maxRecursionStmt`).
- `QueryPlanner.cs:484-492`, `:533-541` — UPDATE-JOIN/DELETE-JOIN хвосты.
- `SqlMutationBuilder.MakeCreateTableAsSelect` (`:598-614`) — сигнатура и поведение не меняются.

## Матрица вариантов и приоритетов

| Ось | Варианты | Закрытие | Приоритет |
|---|---|---|---|
| Потребитель | шесть мест прикрепления из таблицы выше | tests по каждому | 🔴 P1 (R275-01) |
| Наличие лимита | `maxRecursion = 7` / `null` | тест «ровно один раз» / байт-идентичный baseline | 🔴 P1 (R275-01/02) |
| Диалект | SQL Server / PostgreSQL / SQLite / MySQL+MariaDB / ClickHouse | SQL Server-хвост; остальные — байт-идентичны | 🔴 P1 (R275-01/03) |
| Hoisting | CTE в source (hoisted) / source без CTE (non-hoisted) | `option` есть ровно один раз / SQL не меняется | 🔴 P1 (R275-01/02) |
| Batch | один затронутый шаг / два затронутых / affected+unaffected | statement-local, без протечки | 🔴 P1 (R275-05) |
| Значение лимита | `0` (без лимита) / `1` / `32767` | значение переносится как есть | 🔴 P1 (R275-01) |
| Немутационные формы | plain SELECT / UPDATE-JOIN / DELETE-JOIN | прежний хвост | 🔴 P1 (R275-04) |
| Невалидные лимиты | `-1`, `0`, `32768+` | guard: новой валидации нет, поведение существующее | ℹ️ documented |
| Несколько CTE с разными лимитами | первое ненулевое значение (`SqlSourceRenderer.cs:40`) | deferred + trigger: отдельная задача, если понадобится диагностика | ℹ️ deferred |
| Hints на source + hoisted CTE | потенциально два `OPTION`-клаузулы в одном statement | deferred + trigger: нужен отдельный дизайн свёртки; вне шести потребителей | ℹ️ deferred |
| MERGE key path с query source | публично недостижим (`MergeBuilder.cs:659`) | фикс структурный; тест фиксирует существующий guard | ℹ️ documented |
| INSERT…SELECT + `KeepIdentity` на SQL Server | `MakeInsert` оборачивает statement в `set identity_insert on; …; set identity_insert off` (`SqlMutationBuilder.cs:66-68`) | deferred + trigger: нужна отдельная точка вставки опции внутри обёртки | ℹ️ deferred |
| Новая абстракция/тип для хвоста | не вводится | — | ℹ️ scope exclusion |
| Бенчмарки | запрещены | — | ℹ️ scope exclusion |

## DO-задачи и зависимости

| Unit | Работа | Зависимость |
|---|---|---|
| **D275-00** | Preflight (шесть форм, границы batch, терминаторы, достижимость), `scope.json`, baseline-снимки pre-change (SQL Server + PG/SQLite), red-прогон. | первая, до production-правок |
| **D275-01** | Расширение кортежа `RenderSource` + INSERT…SELECT и MERGE (key path + full). | после 00 |
| **D275-02** | CTAS/SELECT…INTO: single и batch-форма. | после 01 |
| **D275-03** | Batch SELECT: statement-local опция, без протечки. | после 02 |
| **D275-04** | Regression guards: отсутствие лимита, non-hoisted, plain SELECT/UPDATE-JOIN/DELETE-JOIN, PG/SQLite/MySQL/ClickHouse байт-идентичность. | после 03 |
| **D275-05** | Docs EN+RU, финальные evidence, manifest, boundary sweep. | после 04 |

Режим: последовательно, один tree. Затронутые тест-проекты и точные селекторы (MTP filter, как в T265):

- `tests/nextorm.sqlserver.tests` — `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T275"`;
- `tests/nextorm.postgres.tests` — `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~T275"`;
- `tests/nextorm.sqlite.tests` — `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~T275"`;
- `tests/nextorm.core.tests` — `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T275"` (если понадобится unit-контракт кортежа; иначе проект не входит в scope).

Boundary sweep point — D275-05: перечисленные проекты зелёные (selected > 0) плюс
`dotnet build nextorm.slnx -c Debug` 0W/0E. `integration_projects = []`: контейнерные integration-тесты
для SQL-assembly фикса не нужны (нет изменения исполнения на живой БД; см. «Что не сказала постановка»).

## Тест-стратегия и coverage

Планируемый класс: `T275MaxRecursionHoistTests` в `tests/nextorm.sqlserver.tests`
(`Cte/T275MaxRecursionHoistTests.cs`), плюс compatibility-класс с тем же именем в
`tests/nextorm.postgres.tests` и `tests/nextorm.sqlite.tests`; `tests/nextorm.core.tests` — только если
понадобится unit-контракт. Планируемые имена:

- `InsertSelect_HoistedRecursiveCte_AppendsMaxRecursionOnce`
- `MergeKeyPath_HoistedRecursiveCte_AppendsMaxRecursionOnce`
- `MergeKeyPath_QuerySource_RemainsRejected` (фиксирует `MergeBuilder.cs:659`)
- `MergeSource_HoistedRecursiveCte_AppendsMaxRecursionOnce`
- `CreateTableAsSelect_HoistedRecursiveCte_AppendsMaxRecursionOnce`
- `Batch_CtasStep_PreservesStatementLocalMaxRecursion`
- `Batch_SelectStep_PreservesStatementLocalMaxRecursion`
- `Batch_MultipleSteps_DoesNotDuplicateOrLeakMaxRecursion`
- `WithoutMaxRecursion_NoOption`
- `NonHoistedSource_SqlUnchanged`
- `Postgres_RecursiveMutationSource_SqlUnchanged`
- `Sqlite_RecursiveMutationSource_SqlUnchanged`
- `PlainSelect_ExistingTailUnchanged`
- `UpdateJoin_ExistingTailUnchanged`
- `DeleteJoin_ExistingTailUnchanged`

Правила:

- Assertions: число вхождений `option (maxrecursion` (case-insensitive) == 1 для present-строк; хвост
  анкорится regex'ом, допускающим только пробелы и один терминальный `;`; значение — точное (7).
  Для absent-строк: count == 0 **и** полное строковое равенство baseline-снимку.
- Только FluentAssertions; xunit v3; база данных не открывается (placeholder connection);
  `Assert.Skip*` не добавляются.
- Baselines снимаются **до** production-правок: `.pdca/D275/rv1/baseline/<consumer>.txt` + sha256 в
  `baseline/README.md`. Red-прогон (assertions на желаемый SQL) фиксируется exit code и списком упавших
  тестов; после фикса те же тесты зеленеют (red→green).
- Coverage-пороги не меняются: 85/75 (unit) и 65/75 (integration).

## Измерения и команды

**Перф-замер не нужен.** Аргумент: SQL собирается один раз на statement/preparation
(`QueryPlanner.cs:153` и шесть мест `DataContext.cs`), не на строку результата; добавление одного
строкового concatenation на statement не наблюдаемо. Бенчмарки запрещены.

**Разведка/спайк не нужны — только bounded preflight:** шесть выходных форм, границы batch, терминаторы,
достижимые варианты source, грамматика T-SQL (`MERGE` grammar: `[ OPTION ( <query_hint> … ) ] ;`).
Это факты, а не дизайн-спайк.

Unit mode: последовательные запуски, один tree, селектор `FullyQualifiedName~T275`; полный scoped-прогон —
только на границе D275-05 (DO→CHECK). Integration-прогоны не планируются.

## Versioned evidence contract rv=1

Определение контракта — глобальный `pdca-dotnet`. `rv=1` — первый контракт этой задачи.

| ID ↔ requirement | Scenario / expected evidence kinds+sources | Exact invocation | Required exit/result | Artifact | Owner | Applicability |
|---|---|---|---|---|---|---|
| **D275-00 ↔ R275-06** | Preflight (шесть форм, batch-границы, терминаторы, достижимость) + scope + pre-change baselines + red-прогон | `python3 .pdca/scripts/validate_inner_loop.py brief .pdca/D275/rv1/scope.json`; `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T275"` | validator exit 0; red-прогон: exit != 0, перечислены упавшие тесты; baseline-файлы с sha256 | `.pdca/D275/rv1/00-preflight.md`, `scope.json`, `baseline/`, `tests/red-sqlserver.log` | D275-00 | always |
| **D275-01 ↔ R275-01** | INSERT…SELECT и MERGE (key path, full): `option (maxrecursion 7)` ровно один раз в tail | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T275"` | exit 0; 3 insert/merge-теста passed | `.pdca/D275/rv1/tests/sqlserver-insert-merge.log` | D275-01 | always |
| **D275-02 ↔ R275-01** | CTAS/SELECT…INTO (single + batch): опция ровно один раз, до терминатора | тот же filtered run, CTAS-тесты | exit 0; 2+ теста passed | `.pdca/D275/rv1/tests/sqlserver-ctas.log` | D275-02 | always |
| **D275-03 ↔ R275-01/05** | Batch SELECT: statement-local, без дублирования и протечки (один/два/affected+unaffected) | тот же filtered run, batch-тесты | exit 0; 3 batch-теста passed | `.pdca/D275/rv1/tests/sqlserver-batch.log` | D275-03 | always |
| **D275-04 ↔ R275-02/03/04** | Отсутствие лимита, non-hoisted, plain SELECT/UPDATE-JOIN/DELETE-JOIN, PG/SQLite/MySQL/ClickHouse байт-идентичны | filtered runs sqlserver + postgres + sqlite + mysql; sql-снимки сравниваются с `baseline/` | exit 0 в каждом; delta == 0 | `.pdca/D275/rv1/tests/compat.log` | D275-04 | always |
| **D275-05 ↔ R275-06** | Build 0W/0E и footprint §13 | `dotnet build nextorm.slnx -c Debug`; `git status --short`; `git diff --stat` | build exit 0, 0 warnings/0 errors; только разрешённые пути | `.pdca/D275/rv1/build.log`, `footprint.log` | D275-05 | always |
| **D275-06 ↔ R275-06** | CRLF и отсутствие новых suppressions/skips | `file`/`grep -c $'\r'` по изменённым файлам; сверка diff на `#pragma warning disable`, `[Fact(Skip`, `Assert.Skip` | все файлы CRLF; счётчики новых suppressions/skips = 0 | `.pdca/D275/rv1/audit.log` | D275-05 | always |
| **D275-07 ↔ R275-07** | EN+RU docs объясняют сохранение лимита; ссылок на specs нет | `git diff --name-only -- docs docs/ru`; `rg -n "docs/specs" docs docs/ru` | обе локали изменены; совпадений со specs нет | `.pdca/D275/rv1/docs.log` | D275-05 | always |
| **D275-08 ↔ R275-01..07** | Boundary sweep: все scoped-проекты зелёные с selected > 0 | четыре filtered runs + build на границе DO→CHECK | все exit 0; selected > 0 | `.pdca/D275/rv1/tests/boundary/` | D275-05 | always |
| **D275-09 ↔ R275-01..07** | Manifest заморожен и проходит оба валидатора | `python3 .pdca/scripts/validate_inner_loop.py manifest .pdca/D275/rv1/manifest.json`; `python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest .pdca/D275/rv1/manifest.json` | оба exit 0; row coverage совпадает | `.pdca/D275/rv1/manifest.json`, `manifest-validation.log`, `evidence-lint.log` | D275-05 | always |
| **D275-10 ↔ R275-02/05** | Пост-фиксовые строки без лимита/на чужих диалектах совпадают с pre-change baseline байт-в-байт | сравнение сохранённых SQL с `baseline/*.txt` (sha256) | delta == 0 | `.pdca/D275/rv1/baseline/`, `.pdca/D275/rv1/delta.log` | D275-05 | always |

CHECK re-gather budget: **2 точечных request суммарно**, owner — CHECK. Manifest замораживается на
границе DO→CHECK (`rv=1`), CHECK открывает его первым. Mechanical gates: `validate_inner_loop.py manifest`
+ `pdca_evidence.py lint`.

## Документация и tracking

- `docs/guide/10-cte.md` + `docs/ru/guide/10-cte.md`: «On SQL Server, an explicitly supplied recursion
  limit is preserved when a recursive CTE feeds INSERT…SELECT, MERGE, CTAS/SELECT…INTO, or a batch step»
  (RU-зеркало — то же утверждение).
- `docs/advanced/limitations.md` + RU-зеркало — строка добавляется только если фикс ей противоречит
  (план исходит из того, что не противоречит; проверяется при D275-05).
- Публичные страницы **не** ссылаются на `docs/specs/**`.
- Finding 150 в `docs/specs/design/code-smells-review.md` — auditor-owned; файл не редактируется.
  Фикс-коммит сообщается в конце цикла (ACT), не в DO.
- Issue #275 остаётся OPEN до ACT; закрытие/обновление — на ACT.

## Что не сказала постановка

| Пробел | Решение |
|---|---|
| **(a)** Batch-шаги — отдельные команды или одна склеенная строка? | Preflight: `BatchPlan.Statements` — отдельные строки; `ToSql()`/`BatchRunner.CreateJoinedCommand` соединяют их `"; "` (`IBatchExecutor.cs:212-236`, `BatchRunner.cs:236`). Значит, опция должна жить внутри строки шага. Зафиксировано фактом. |
| **(b)** Грамматика хвоста MERGE/CTAS | Preflight: T-SQL `MERGE` допускает `[ OPTION ( <query_hint> … ) ]` **перед** обязательным `;` (MS Learn, MERGE syntax). CTAS в SQL Server — это `SELECT … INTO`, хвост как у SELECT. Без БД. |
| **(c)** Терминальные разделители шести мест | Preflight цитирует последние символы каждой формы; вставка — перед терминатором `;`. |
| **(d)** Достижимые non-hoisted формы | Preflight: hoisting происходит всегда, когда `source.Ctes` непуст; подготовка поднимает CTE в командный список (`QueryPreparer.cs:390`). Non-hoisted = source без CTE → `maxRecursionStmt == null`. |
| **(e)** Поведение при `maxRecursion` = 0 / отрицательном / out-of-range | Валидации нет (ни `CteDefinition`, ни `WithRecursive`, ни `MakeMaxRecursion`); значение переносится как есть. Новая валидация не добавляется — поведение сохраняется. |
| **(f)** Несколько CTE с разными лимитами | `SqlSourceRenderer.cs:40` берёт первое ненулевое значение; поведение не меняется. Deferred + trigger: задача на диагностику конфликта, если появится запрос. |
| **(g)** Hints на source + hoisted CTE | Потенциально два `OPTION`-клаузулы в одном statement. Вне шести потребителей и вне R275-01; deferred + trigger (нужен дизайн свёртки). |
| **(h)** MERGE key path с query source | Preflight: публично недостижим — `MergeBuilder.cs:659` бросает `NotSupportedException`. Место `:1262` правится структурно (единый кортеж); тест фиксирует существующий guard, а не выдумывает сценарий. |
| **(i)** INSERT…SELECT + `KeepIdentity` | Preflight: `MakeInsert` возвращает три statement'а (`set identity_insert on; insert …; set identity_insert off`, `SqlMutationBuilder.cs:66-68`), у которых нет единственного tail. Опция вставляется по общему правилу (конец строки) и для этого случая **не** гарантируется — deferred + trigger: отдельная точка внутри обёртки, если появится запрос. Тест на этот случай в scope не входит. |
| **(j)** Status-path | Lane-мандат переопределяет repo default `.pdca/status/`; зафиксировано в метаданных. |

## Предусловия DO и первый шаг

1. Дерево: branch `1.0.9-rc3`, HEAD `6c9dd49b`, чужая незакоммиченная работа присутствует и не трогается;
   stage — только явные пути этой задачи.
2. Preflight выполнен и записан (`.pdca/D275/rv1/00-preflight.md`) до первой production-правки.
3. Test scope валидируется до первой правки: `python3 .pdca/scripts/validate_inner_loop.py brief .pdca/D275/rv1/scope.json`.
4. Baselines снимаются **до** production-правок red-прогоном, не чтением production-кода.
5. Все записываемые файлы — CRLF; после записи `perl -pi -e 's/\r?\n/\r\n/g' <file>`.
6. Бенчмарки не запускаются; `docs/specs/design/code-smells-review.md` не трогается.
7. Первый шаг: D275-00 — preflight-факты, scope JSON, baseline-класс и baseline-артефакты.

## Журнал прогресса (append-only)

2026-10-18T11:37:24Z | PLAN | r=1 | n=1/3 | PLAN ready — DO started (autonomous lane, no user approval) | docs/specs/status/D275-maxrecursion-hoist-1.md
2026-10-18T11:44:45Z | PLAN | r=1 | n=1/3 | D275-00 closed — preflight + baselines + red evidence; the SQL Server class is intentionally red before the fix (red-sqlserver.log exit 2, 6 failed/5 passed), compat runs pre-change green: PostgreSQL 3/3, SQLite 2/2, MySQL 2/2 | .pdca/D275/rv1/00-preflight.md, .pdca/D275/rv1/baseline/, .pdca/D275/rv1/tests/red-sqlserver.log, .pdca/D275/rv1/tests/red-postgres.log, .pdca/D275/rv1/tests/red-sqlite.log, .pdca/D275/rv1/tests/red-mysql.log
