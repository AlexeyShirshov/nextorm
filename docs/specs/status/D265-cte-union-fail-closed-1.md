# D265 — fail closed для legacy WithRecursive с distinct UNION на SQL Server

| Поле | Значение |
|---|---|
| Issue | [#265](https://github.com/AlexeyShirshov/nextorm/issues/265), OPEN |
| Milestone | `1.0.9-rc3` |
| Branch | `1.0.9-rc3` |
| Baseline | `b2c5765c` |
| Plan state | `ready` |
| Cycle | `1` |
| Plan revision | `r=1` |
| Attempt | `1/3` |
| Contract | `rv=1` |
| Status file | `docs/specs/status/D265-cte-union-fail-closed-1.md` |
| Evidence root | `.pdca/D265/rv1/` |
| Manifest | `.pdca/D265/rv1/manifest.json` |
| Note | lane mandates `docs/specs/status/` for this cycle (repo default `.pdca/status/` overridden) |

**Факт старта DO:** рабочее дерево — один tree на branch `1.0.9-rc3`, наблюдаемый HEAD `d1e271d5`; baseline `b2c5765c` — его прямой предок, поэтому метаданные сохраняют baseline из брифа, а фактический HEAD и состояние дерева фиксируются в журнале. Дерево несёт чужие незакоммиченные изменения другой lane; они не читаются, не правятся, не стейджатся и не откатываются.

## Цель и непреложные ограничения

SQL Server отвергает рекурсивный CTE, объявленный через distinct `UNION`, сообщением «does not contain a top-level UNION ALL operator». nextorm legacy-путь `WithRecursive` такой CTE строит и падает только на сервере — то есть после подготовки команды, после записи плана в кеш и после сетевого round-trip. Цель — падать локально, **до исполнения и до попадания в plan cache**, с `NotSupportedException`, который называет SQL Server и указывает на `UnionAll`/`AsRecursiveCte`.

Непреложные ограничения:

- **§13 freeze.** Меняются только emission/validation-точки: `CteHoister`, union-ветка `SqlBuilder.MakeSelect`, валидация CTE в `Builders/CteQuery.cs`/`MutationCteQuery.cs` плюс узкий вызов валидации в `QueryCommand.QueryPreparer.cs:381-404`, `QueryPlanner.RenderSource`/`SqlSourceRenderer`, capability-флаги диалектов. Реконструкция construction/allocation пути запрещена.
- Никаких бенчмарков.
- Никакого изменения публичных сигнатур сверх аддитивной capability-свойства `ISqlDialect` с default-значением.
- Типизированный путь `AsRecursiveCte` не затронут.
- PostgreSQL/SQLite/MySQL/MariaDB продолжают выдавать **байт-идентичный** distinct `UNION`.
- Один tree, без worktree, без push и без чужих коммитов.

## Критерии приёмки

| ID | Требуемый результат | Негативный случай |
|---|---|---|
| **R265-01** | Legacy `WithRecursive` с телом `anchor.Union(step)` на SQL Server бросает `NotSupportedException`, чьё сообщение называет SQL Server, `UnionAll` и `AsRecursiveCte`; SQL не исполняется, невалидный план не кешируется. | Серверное исключение вместо локального, сообщение без actionable-указания, либо валидный SQL всё же уходит в execution/кеш. |
| **R265-02** | Валидация выполняется **до** lookup и до вставки в plan cache и повторяется при каждом preparation; повторная подготовка того же/эквивалентного запроса отвергается так же. | Первый вызов отвергнут, второй проходит из теплого кеша; или проверка срабатывает только на cache miss. |
| **R265-03** | SQL PostgreSQL/SQLite/MySQL/MariaDB байт-идентичен pre-change baseline; нет подмены на `UNION ALL`, нет новых отказов. | Любая дельта SQL, замена set-оператора или новый `NotSupportedException` на этих диалектах. |
| **R265-04** | Типизированная рекурсия (`AsRecursiveCte`) и legacy `UnionAll` не меняются — те же SQL и те же сообщения. | Изменённый SQL/поведение типизированного пути или `UnionAll`. |
| **R265-05** | Обе legacy-поверхности (`CteQuery.WithRecursive`, `MutationCteQuery.WithRecursive`) закрыты там, где preparation достижим. | Одна поверхность отвергается, другая молча уходит на сервер. |
| **R265-06** | Изменён только разрешённый footprint (§13): emission/validation-точки и аддитивная capability. | Правки construction/allocation пути, публичных сигнатур, чужих файлов или lane-изменений. |
| **R265-07** | EN+RU документация объясняет исключение и миграцию. | Односторонняя правка, отсутствие миграционного указания или ссылка на `docs/specs/**`. |
| **R265-08** | Scoped-тесты зелёные; `dotnet build nextorm.slnx -c Debug` — exit 0, 0 warnings/0 errors; CRLF сохранён. | Красный scoped-прогон, warning/error в build, LF/mixed EOL. |

Каждый критерий несёт негативный случай из правой колонки; CHECK проверяет обе половины.

## Минимальное решение

Выбран вариант **(a) — отвергать локально**. Отклонены:

- **(b) переписать тело на `UNION ALL` или эквивалентную форму** — `UNION ALL` меняет семантику дубликатов, а эквивалентная перезапись требует отдельного доказательства корректности и существенно большего объёма; это не «минимальное» изменение и не входит в §13.
- **render-only guard** — кешированный план не проходит через rendering, поэтому такой guard не закрывает warm-cache путь и нарушает R265-02.

Дизайн:

- В `ISqlDialect` рядом с `SupportsRecursiveCte` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:1495-1511`) добавляется `bool RequiresRecursiveCteUnionAll => false`; геттер никогда не бросает.
- `SqlDialectBase` (`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:1000-1007`) даёт default `false`.
- `SqlServerDialect` переопределяет на `true`.
- Общий внутренний guard на поверхности валидации CTE с условием: `dialect.RequiresRecursiveCteUnionAll && cte.Recursive && cte.RecursiveReference == null && cte.Query.UnionType == UnionType.Distinct`.
- Диалект достижим в preparer через существующий паттерн `(cmd._dataContext as DataContext)?.Dialect` — он уже используется в `QueryCommand.QueryPreparer.cs:749` и `:2824` (проверенный факт; реконструкция construction не нужна).
- Точное сообщение: `"SQL Server does not support UNION (distinct) in a legacy recursive CTE. Use UnionAll or the typed AsRecursiveCte API."`.
- Порядок: `QueryPlanner.cs:656` выполняет preparation до cache lookup `:692-701`, поэтому guard стоит раньше и кеша.
- Типизированный путь выставляет `RecursiveReference` и принудительно строит `UnionAll` (`Cte.cs:135-139`) — под guard не попадает.
- `SqlBuilder.MakeSelect:345-367` остаётся без изменений.

## Матрица вариантов и приоритетов

| Вариант | Закрытие | Приоритет |
|---|---|---|
| Legacy `WithRecursive` + `Union` (distinct) на SQL Server | test: локальный `NotSupportedException` до execution/кеша | 🔴 P1 (D265-01) |
| Legacy `WithRecursive` + `UnionAll` на SQL Server | test: SQL и поведение без изменений | 🔴 P1 (D265-02) |
| Legacy `WithRecursive` без set-оператора (`UnionType.None`) | test: distinct-guard не срабатывает | 🔴 P1 (D265-03) |
| Типизированный `AsRecursiveCte` на SQL Server | test: путь не затронут | 🔴 P1 (D265-04) |
| Distinct recursive на PostgreSQL/SQLite/MySQL/MariaDB | test: байт-идентичный goldens | 🔴 P1 (D265-05) |
| `MutationCteQuery`-поверхность `WithRecursive` | test/guard: shared validation, если preparation достижим | 🔴 P1 (D265-06) |
| Повторный prepare / warm cache | test: отклонение идентично, запись в кеш не появляется | 🔴 P1 (D265-07) |
| `maxRecursion` присутствует/отсутствует | test: guard не зависит от hint | 🔴 P1 (D265-08) |
| Нерекурсивный distinct CTE (`With`) на SQL Server | test: остаётся поддержанным | 🔴 P1 (D265-09) |
| Диалект без переопределения (default capability) | test: `DialectRestriction_DefaultsOff` | 🔴 P1 (D265-10) |
| Остальные кастомные диалекты вне решения | documented: default `false`, поведение не меняется | ℹ️ scope exclusion |
| ClickHouse recursive | существующее поведение `SupportsRecursiveCte=false` не трогается | ℹ️ scope exclusion |
| Эквивалентная перезапись distinct → `UNION ALL` | deferred, trigger: отдельная задача/issue с доказательством семантики | 🟡 P2 |
| Benchmark/perf-измерения | запрещены §13 | ℹ️ scope exclusion |

## DO-задачи и зависимости

| Unit | Работа | Зависимость |
|---|---|---|
| **D265-A** | Baselines/scaffolding: зафиксировать pre-change SQL для PG/SQLite/MySQL/MariaDB, evidence root, scope JSON. | первая, до production-правок |
| **D265-B** | Capability (`ISqlDialect`/`SqlDialectBase`/`SqlServerDialect`) + shared guard в валидации CTE + SQL Server регрессии. | после A |
| **D265-C** | Compatibility/boundary tests: PG/SQLite/MySQL/MariaDB goldens, typed/`UnionAll`/non-recursive/ClickHouse. | после B |
| **D265-D** | Bilingual docs: `docs/advanced/limitations.md`, `docs/guide/10-cte.md` + `docs/ru/**` зеркала. | параллельно B/C |
| **D265-E** | Final verification: шесть scoped-проектов, `dotnet build nextorm.slnx -c Debug`, CRLF/footprint-аудит, manifest. | после B/C/D |

Режим: последовательно, один tree. Затронутые тест-проекты и селекторы для каждой единицы:

- `tests/nextorm.core.tests` — `FullyQualifiedName~T265RecursiveUnionTests` (MutationCteQuery, capability default, typed-path unit-контракт);
- `tests/nextorm.sqlserver.tests` — `FullyQualifiedName~T265RecursiveUnionTests` (reject/cache/`UnionAll`/typed/non-recursive);
- `tests/nextorm.postgres.tests` — `FullyQualifiedName~T265RecursiveUnionTests` (compatibility goldens);
- `tests/nextorm.sqlite.tests` — `FullyQualifiedName~T265RecursiveUnionTests` (compatibility goldens);
- `tests/nextorm.mysql.tests` — `FullyQualifiedName~T265RecursiveUnionTests` (MySQL + MariaDB);
- `tests/nextorm.clickhouse.tests` — `FullyQualifiedName~T265RecursiveUnionTests` (recursive остаётся неподдержанным).

Boundary sweep point — D265-E: все шесть проектов зелёные плюс `dotnet build nextorm.slnx -c Debug` 0W/0E. `integration_projects = []`: контейнерные integration-тесты для этого SQL-generation/validation фикса не нужны (см. «Что не сказала постановка»).

## Тест-стратегия и coverage

Планируемый класс: `T265RecursiveUnionTests` в каждом из перечисленных проектов (одинаковое имя, разные namespace). Планируемые имена тестов:

- `LegacyDistinct_ThrowsActionableBeforeCaching`
- `LegacyDistinct_RepeatedPrepareDoesNotPopulateCache`
- `LegacyDistinct_WarmCacheCannotBypassValidation`
- `LegacyAll_RemainsSupported`
- `LegacyWithoutUnion_DoesNotHitDistinctGuard`
- `TypedRecursive_RemainsUnchanged`
- `CompatibilityDistinct_SqlIsByteIdentical`
- `CompatibilityAllAndNone_RemainUnchanged`
- `NonrecursiveDistinct_RemainsSupported`
- `MutationLegacyDistinct_UsesSharedValidationOrExistingProviderGate`
- `ClickHouseRecursive_RemainsUnsupported`
- `DialectRestriction_DefaultsOff`

Правила:

- SQL Server тесты идут через нормальный planner path (`GetPreparedQueryCommand`), а не через `SqlOf`-подобную одиночную сборку SQL, чтобы доказать именно pre-execution/pre-cache отказ.
- Только FluentAssertions; xunit v3; `Assert.Skip*` не добавляются; база данных не используется (placeholder-connection).
- Compatibility-тесты сравнивают с goldens, снятыми **до** production-правок (`.pdca/D265/rv1/baseline/<provider>.txt`), без нормализации пробелов, кроме перевода CRLF→LF на уровне сравнения, если источник различается.
- Coverage-пороги не меняются: 85/75 (unit) и 65/75 (integration). Красный integration baseline (78.0 % line / 70.1 % branch при пороге 75 % branch) — предсуществующий факт, D265 его не трогает и не объявляет зелёным.

## Измерения и команды

**Перф-замер не нужен.** Аргумент: guard исполняется один раз в ходе CTE preparation (`QueryCommand.QueryPreparer.cs:381-404`) до cache lookup (`QueryPlanner.cs:656-701`) и никогда не находится на per-row materialization пути. Бенчмарки запрещены §13, поэтому perf-evidence в контракт не входит.

**Разведка/спайк не нужны:** все опорные факты (capability-точки, ветка union, порядок prepare→cache, typed-путь) уже поставлены в брифе и подтверждены чтением исходников при PLAN.

Unit mode: последовательные запуски, один tree, фильтры `FullyQualifiedName~T265RecursiveUnionTests`; полный scoped-прогон шести проектов — только на границе D265-E (DO→CHECK). Integration-прогоны не планируются.

## Versioned evidence contract rv=1

Определение контракта — глобальный `pdca-dotnet` (разделы Versioned evidence contract и CHECK completeness gate). `rv=1` — первый контракт этой задачи.

| ID ↔ requirement | Scenario / expected evidence kinds+sources | Exact invocation | Required exit/result | Artifact | Owner | Applicability |
|---|---|---|---|---|---|---|
| **D265-01 ↔ R265-01** | SQL Server legacy distinct бросает actionable `NotSupportedException` до execution | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T265RecursiveUnionTests"` | exit 0; `LegacyDistinct_ThrowsActionableBeforeCaching` и `LegacyAll_RemainsSupported` исполнены | `.pdca/D265/rv1/tests/sqlserver.log` | B | always |
| **D265-02 ↔ R265-02** | Валидация раньше cache lookup/insert; повторный prepare отвергается так же | тот же запуск, тесты `LegacyDistinct_RepeatedPrepareDoesNotPopulateCache`, `LegacyDistinct_WarmCacheCannotBypassValidation` | exit 0; повторное отклонение идентично, исполняемая команда не возвращена | `.pdca/D265/rv1/tests/sqlserver-cache.log` | B | always |
| **D265-03 ↔ R265-03** | PG/SQLite/MySQL/MariaDB distinct SQL байт-идентичен pre-change baseline | `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~T265RecursiveUnionTests"`; аналогично sqlite и mysql | exit 0 в каждом; совпадение с `.pdca/D265/rv1/baseline/<provider>.txt` | `.pdca/D265/rv1/tests/compat.log`, `.pdca/D265/rv1/baseline/` | C | always |
| **D265-04 ↔ R265-04** | Typed `AsRecursiveCte`, legacy `UnionAll` и non-recursive distinct не меняются | sqlserver + postgres + sqlite + mysql + clickhouse filtered runs | exit 0; SQL и сообщения прежние | `.pdca/D265/rv1/tests/unchanged.log` | C | always |
| **D265-05 ↔ R265-05** | `MutationCteQuery` legacy-поверхность закрыта или имеет существующий provider-gate | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T265RecursiveUnionTests"` | exit 0; достижимость зафиксирована фактом, не выдумана | `.pdca/D265/rv1/tests/core.log` | B | always |
| **D265-06 ↔ R265-06** | Только разрешённый footprint | `git diff --stat` и `git status --short` на frozen tree | перечислены только разрешённые §13 файлы; чужие изменения не в diff | `.pdca/D265/rv1/footprint.log` | E | always |
| **D265-07 ↔ R265-07** | EN+RU docs объясняют исключение и миграцию | `git diff --name-only -- docs docs/ru`; проверка отсутствия ссылок на `docs/specs/**` | обе локали изменены; миграция описана; ссылок на specs нет | `.pdca/D265/rv1/docs.log` | D | always |
| **D265-08 ↔ R265-08** | Build 0W/0E и шесть scoped-проектов зелёные | `dotnet build nextorm.slnx -c Debug` + шесть filtered runs | build exit 0, 0 warnings/0 errors; все проекты exit 0 | `.pdca/D265/rv1/build.log`, `.pdca/D265/rv1/tests/boundary/` | E | always |
| **D265-09 ↔ R265-08** | CRLF сохранён; новых suppressions/skips нет | `file`/`grep -c $'\r'` по изменённым файлам; сверка diff на `#pragma warning disable`, `[Fact(Skip`, `Assert.Skip` | все файлы CRLF; счётчики новых suppressions/skips = 0 | `.pdca/D265/rv1/audit.log` | E | always |
| **D265-10 ↔ R265-01…08** | Замороженный manifest проходит оба валидатора | `python3 .pdca/scripts/validate_inner_loop.py manifest .pdca/D265/rv1/manifest.json`; `python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest .pdca/D265/rv1/manifest.json` | оба exit 0; row coverage совпадает | `.pdca/D265/rv1/manifest.json`, `.pdca/D265/rv1/manifest-validation.log`, `.pdca/D265/rv1/evidence-lint.log` | E | always |

Manifest замораживается на границе DO→CHECK; CHECK открывает его первым. CHECK re-gather budget: **2 точечных request суммарно**, owner — CHECK.

## Документация и tracking

- `docs/advanced/limitations.md` — строка ограничения SQL Server: legacy recursive CTE с distinct `UNION` отвергается локально.
- `docs/guide/10-cte.md` — раздел миграции: использовать `UnionAll` либо типизированный `AsRecursiveCte`.
- Зеркала `docs/ru/advanced/limitations.md` и `docs/ru/guide/10-cte.md` — то же содержание по-русски.
- Утверждение для обеих локалей: SQL Server legacy recursive CTE с distinct `UNION` бросает исключение локально; миграция — `UnionAll`/`AsRecursiveCte`; остальные диалекты без изменений.
- Публичные страницы **не** ссылаются на `docs/specs/**`.
- Issue #265 остаётся OPEN до ACT; закрытие/обновление — на ACT, не в DO.

## Что не сказала постановка

| Пробел | Решение |
|---|---|
| **(a)** Issue просит integration negative test | **Записанное отклонение:** lane явно говорит, что integration-тесты для этого SQL-generation фикса не нужны. Provider SQL-generation/validation тесты плюс доказательство отказа **до** execution закрывают критерий; это deviation, а не missing row. |
| **(b)** Cache-observability seam не специфицирован | Использовать существующий test access или test-only reflection; если наблюдаемость недостижима, наблюдаемое доказательство — «оба attempt отвергаются идентично и исполняемая команда не возвращается». |
| **(c)** Достижимость `MutationCteQuery` | Устанавливается эмпирически при D265-B/C, не выдумывается. |
| **(d)** Status-path | Lane-мандат переопределяет repo default `.pdca/status/`; зафиксировано в метаданных. |
| Baseline vs фактический HEAD | `b2c5765c` — предок наблюдаемого `d1e271d5`; фиксируются оба, дерево не переключается. |

## Предусловия DO и первый шаг

1. Дерево: branch `1.0.9-rc3`, HEAD `d1e271d5`, чужая незакоммиченная работа присутствует и не трогается; stage — только явные пути этой задачи.
2. Test scope валидируется до первой правки: `python3 .pdca/scripts/validate_inner_loop.py brief .pdca/D265/rv1/scope.json`.
3. Baselines снимаются **до** production-правок: PG/SQLite/MySQL/MariaDB goldens пишутся red-прогоном, не чтением production-кода.
4. Все записываемые файлы — CRLF; после записи `perl -pi -e 's/\r?\n/\r\n/g' <file>`.
5. Бенчмарки не запускаются; `docs/specs/design/code-smells-review.md` не трогается.
6. Первый шаг: D265-A — scope JSON, baseline-классы и baseline-артефакты.

## Done / Verified (ACT, после CHECK PASS)

| Проверка | Результат |
|---|---|
| Commit | `101152ea` — `#265 reject legacy recursive UNION on SQL Server before caching` |
| Build | `dotnet build nextorm.slnx -c Debug` — exit 0, 0 warnings / 0 errors (`.pdca/D265/rv1/check/build.log`) |
| Scoped tests | filtered `FullyQualifiedName~T265RecursiveUnionTests` — 20/20 passed, 6 проектов, все exit 0: core 1, sqlserver 8, postgres 3, sqlite 3, mysql 4 (вкл. MariaDB), clickhouse 1 (`.pdca/D265/rv1/check/filtered-summary.txt`) |
| Full sqlserver | 988 total — 980 passed, 0 failed, 8 skipped; все 8 — предсуществующие `[Fact(Explicit = true)]` (не `Assert.Skip*`), ancestor `b5ab6502` (`.pdca/D265/rv1/check/unfiltered-sqlserver.log`) |
| Full core | 2648/2648 passed, 0 failed, 0 skipped, exit 0 (`.pdca/D265/rv1/check/unfiltered-core.log`) |
| Validators | `validate_inner_loop.py report/manifest` exit 0; `pdca_evidence.py lint` exit 0 |
| Suppressions/skips | 0 новых: `#pragma warning disable` / `[Fact(Skip` / `Assert.Skip` в diff — none (`.pdca/D265/rv1/audit.log`) |
| EOL | CRLF во всех 16 затронутых файлах (CR == lines) |
| Footprint | 16 paths: 5 src, 6 tests, 4 docs, 1 status — §13 соблюдён, чужие изменения не затронуты (`.pdca/D265/rv1/footprint.log`) |
| Typed path | типизированный `AsRecursiveCte` не изменён — те же SQL и сообщения |
| Compatibility | PG/SQLite/MySQL/MariaDB SQL байт-идентичен `.pdca/D265/rv1/baseline/*.txt` |

## CHECK-адъюдикации (rv=1)

- **Edge A — raw-recipe probe (`QueryPlanner.cs:609-653`)**: структурно недостижим; обход скелета без тела CTE не доходит до emission/validation-точки, поэтому probe не может обойти guard.
- **Edge B — cross-context `IsPrepared` reuse**: конструкцией не поддержан — подготовка принадлежит своему `QueryCommand`/`DataContext` и повторно проходит `PrepareCommand`, снова упираясь в guard.
- **`MutationCteQuery`**: отвергнут раньше — provider-gate в `MutationCteQuery.cs:155` (scope build), до `WithRecursive`/`PrepareCtes`; bypass не найден.
- **Integration-negative deviation**: принято (см. «Что не сказала постановка» (a)); provider SQL-generation/validation тесты плюс доказательство отказа до execution закрывают критерий.

## Changed files

| # | Path | Kind |
|---|---|---|
| 1 | `src/nextorm.core/Builders/CteQuery.cs` | src — shared guard |
| 2 | `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` | src — `RequiresRecursiveCteUnionAll` (default `false`) |
| 3 | `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs` | src — default `false` |
| 4 | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` | src — вызов guard в `PrepareCtes` |
| 5 | `src/nextorm.sqlserver/SqlServerDialect.cs` | src — `true` |
| 6 | `tests/nextorm.core.tests/Cte/T265RecursiveUnionTests.cs` | test — capability default/typed unit |
| 7 | `tests/nextorm.sqlserver.tests/Cte/T265RecursiveUnionTests.cs` | test — reject/cache/`UnionAll`/typed/non-recursive |
| 8 | `tests/nextorm.postgres.tests/Cte/T265RecursiveUnionTests.cs` | test — compatibility goldens |
| 9 | `tests/nextorm.sqlite.tests/Cte/T265RecursiveUnionTests.cs` | test — compatibility goldens |
| 10 | `tests/nextorm.mysql.tests/Cte/T265RecursiveUnionTests.cs` | test — MySQL + MariaDB goldens |
| 11 | `tests/nextorm.clickhouse.tests/Cte/T265RecursiveUnionTests.cs` | test — recursive остаётся неподдержанным |
| 12 | `docs/guide/10-cte.md` | docs EN — ограничение + миграция |
| 13 | `docs/ru/guide/10-cte.md` | docs RU — зеркало |
| 14 | `docs/advanced/limitations.md` | docs EN — строка ограничения |
| 15 | `docs/ru/advanced/limitations.md` | docs RU — зеркало |
| 16 | `docs/specs/status/D265-cte-union-fail-closed-1.md` | status — этот файл |

## Pointers

- Evidence root: `.pdca/D265/rv1/`
- Manifest: `.pdca/D265/rv1/manifest.json` (frozen на DO→CHECK, оба валидатора exit 0)
- Baseline dir: `.pdca/D265/rv1/baseline/` (`postgres.txt`, `sqlite.txt`, `mysql.txt`, `mariadb.txt` + `README.md` с sha256)
- CHECK evidence: `.pdca/D265/rv1/check/`
- DO evidence: `.pdca/D265/rv1/tests/`, `.pdca/D265/rv1/{build,footprint,audit,docs}.log`

## Next plan

`T275 (#275) — OPTION (MAXRECURSION n) must survive hoisting in QueryPlanner.RenderSource; next cycle 1 of task D275`

## Журнал прогресса (append-only)

2026-10-18T11:11:56Z | PLAN | r=1 | n=1/3 | PLAN ready — DO started (autonomous lane, no user approval) | docs/specs/status/D265-cte-union-fail-closed-1.md
2026-10-18T12:02:10Z | DO | r=1 | n=1/3 | D265-A closed — pre-change baselines for postgres/sqlite/mysql/mariadb verified and kept as goldens; the baseline capture classes are the named T265 test classes, no extra scaffolding remains | .pdca/D265/rv1/baseline/
2026-10-18T12:03:00Z | DO | r=1 | n=1/3 | D265-B closed — additive `RequiresRecursiveCteUnionAll` (ISqlDialect default false, SqlDialectBase false, SqlServerDialect true) + shared guard `CteDefinition.EnsureRecursiveUnionSupported` called from `PrepareCtes` for every definition before the typed early-return and before body preparation; SQL Server tests 8/8 exit 0 | src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:392,401; .pdca/D265/rv1/tests/sqlserver-inner.log
2026-10-18T12:03:10Z | DO | r=1 | n=1/3 | ordering fact — `QueryPlanner.cs:656` `PrepareCommand` precedes plan-cache lookup `:692` and store `:814`, so the guard runs before any cache read/write; `Recursive`/`RecursiveReference`/`Query.UnionType` are read-only getters | src/nextorm.core/DataContext/QueryPlanner.cs:656,692,814
2026-10-18T12:03:20Z | DO | r=1 | n=1/3 | MutationCteQuery legacy surface — on SQL Server the existing provider gate throws in `MutationCteQuery.EnsureSupported` while the scope is built (`MutationCteQuery.cs:155`), before `WithRecursive` and before `PrepareCtes`; the shared guard is unreachable there, recorded as the observed rejection path (no production change) | tests/nextorm.sqlserver.tests/Cte/T265RecursiveUnionTests.cs
2026-10-18T12:09:22Z | DO | r=1 | n=1/3 | D265-C closed — compatibility/typed/non-recursive/ClickHouse tests: PG 3/3, SQLite 3/3, MySQL+MariaDB 4/4, ClickHouse 1/1, core 1/1, every run exit 0; goldens byte-identical, no new rejections | .pdca/D265/rv1/tests/{postgres,sqlite,mysql,clickhouse,core}-inner.log
2026-10-18T12:15:00Z | DO | r=1 | n=1/3 | D265-D closed — bilingual docs: SQL Server restriction + migration subsection in guide/10-cte (EN/RU) and one limitations row (EN/RU); no links to `docs/specs/**` | .pdca/D265/rv1/docs.log
2026-10-18T12:20:50Z | DO | r=1 | n=1/3 | boundary sweep green — six filtered runs exit 0 (3/3/4/1/1/8), full sqlserver 988 total (980 passed, 8 skipped) exit 0, full core 2648/2648 exit 0 | .pdca/D265/rv1/tests/boundary/
2026-10-18T12:21:15Z | DO | r=1 | n=1/3 | build gate — `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s), 0 Error(s) | .pdca/D265/rv1/build.log
2026-10-18T12:22:00Z | DO | r=1 | n=1/3 | inner-loop evidence — journal, project and report validators all exit 0 | .pdca/D265/rv1/journal.json, .pdca/D265/rv1/evidence.json
2026-10-18T12:22:30Z | DO | r=1 | n=1/3 | D265-E closed, DO → CHECK — all six scoped projects green, build 0W/0E, CRLF verified, footprint limited to §13 surfaces, manifest frozen and both validators exit 0 | .pdca/D265/rv1/manifest.json, .pdca/D265/rv1/footprint.log, .pdca/D265/rv1/audit.log
2026-10-18T16:24:10Z | DO | r=1 | n=1/3 | coder gather report — evidence pack for CHECK assembled: build.log, filtered/unfiltered summaries, footprint/audit/docs logs, manifest frozen | .pdca/D265/rv1/check/
2026-10-18T16:24:40Z | DO | r=1 | n=1/3 | scout diff-facts report — diff facts across the 16 paths: 9 production/doc diffs, 6 new test files, guard reached only from `PrepareCtes`; no construction-path edits | .pdca/D265/rv1/footprint.log, .pdca/D265/rv1/audit.log
2026-10-18T16:25:00Z | CHECK | r=1 | n=1/3 | DO → CHECK boundary opened — manifest rv=1 and frozen; CHECK re-gather budget used: 0/2 | .pdca/D265/rv1/manifest.json
2026-10-18T16:26:20Z | CHECK | r=1 | n=1/3 | CHECK verdict: PASS (r=1, n=1/3, rv=1, 0 open rows) — все 8 критериев met по обеим половинам; 2 non-binding пробы закрыты адъюдикацией; 0 re-gather request | .pdca/D265/rv1/check/
2026-10-18T16:27:00Z | ACT | r=1 | n=1/3 | D265-A closure carried into ACT — baselines/goldens неизменны и совпадают байт-в-байт с PG/SQLite/MySQL/MariaDB SQL | .pdca/D265/rv1/baseline/
2026-10-18T16:27:05Z | ACT | r=1 | n=1/3 | D265-B closure carried into ACT — capability + shared guard, SQL Server 8/8 | .pdca/D265/rv1/tests/sqlserver-inner.log
2026-10-18T16:27:10Z | ACT | r=1 | n=1/3 | D265-C closure carried into ACT — compatibility/typed/non-recursive/ClickHouse 12/12 | .pdca/D265/rv1/tests/
2026-10-18T16:27:15Z | ACT | r=1 | n=1/3 | D265-D closure carried into ACT — EN+RU docs, без ссылок на `docs/specs/**` | .pdca/D265/rv1/docs.log
2026-10-18T16:27:20Z | ACT | r=1 | n=1/3 | D265-E closure carried into ACT — build 0W/0E, шесть scoped-проектов зелёные, CRLF/footprint-аудит чист | .pdca/D265/rv1/{build,footprint,audit}.log
2026-10-18T16:28:30Z | ACT | r=1 | n=1/3 | ACT closed; next task T275 — flow продолжается в lane (next cycle 1 of task D275) | docs/specs/status/D265-cte-union-fail-closed-1.md
