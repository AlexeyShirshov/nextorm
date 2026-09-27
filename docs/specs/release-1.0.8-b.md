# План релиза 1.0.8-b (milestone 1.0.8-b)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.7-b.md`, `git show v1.0.7-b`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.8-b` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка в работе; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.8-b](https://github.com/AlexeyShirshov/nextorm/milestones/1.0.8-b) — 10 issues
  (#25, #27, #70, #73, #92, #93, #96, #97, #98, #99), все реализованы и закрыты (open=0, closed=10).
- **Тег:** `v1.0.8-b` (планируется)
- **Ветка релиза:** `1.0.8-b`
- **База:** тег `v1.0.7-b`; HEAD до подготовки — `aa5cfa7` (#27 implementation)
- **Версии пакетов:** `1.0.8-b` задана **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.8-b`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.7.

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#25](https://github.com/AlexeyShirshov/nextorm/issues/25) | Multi-resultset support |
| [#27](https://github.com/AlexeyShirshov/nextorm/issues/27) | BLOB/CLOB streaming как `Stream`/`TextReader` |
| [#70](https://github.com/AlexeyShirshov/nextorm/issues/70) | Хранимые процедуры и функции (вызов, output-параметры, несколько result-set) |
| [#73](https://github.com/AlexeyShirshov/nextorm/issues/73) | Table-valued parameters (TVP) |
| [#92](https://github.com/AlexeyShirshov/nextorm/issues/92) | Паритет опций bulk copy / bulk insert |
| [#93](https://github.com/AlexeyShirshov/nextorm/issues/93) | Command timeout (per-context / per-query) |
| [#96](https://github.com/AlexeyShirshov/nextorm/issues/96) | Варианты хинтов — join / subquery / tables-in-scope |
| [#97](https://github.com/AlexeyShirshov/nextorm/issues/97) | Управление кэшем планов/запросов (`ClearCache`, disable, sliding expiration) |
| [#98](https://github.com/AlexeyShirshov/nextorm/issues/98) | Комментарий-метка запроса (`TagQuery`) |
| [#99](https://github.com/AlexeyShirshov/nextorm/issues/99) | Per-query переопределение источника (`WithTableExpression`) |

Плюс: новая глава руководства EN + RU — **30 «Streaming large objects (BLOB/CLOB)»**
(`docs/guide/30-large-objects.md`, `docs/ru/guide/30-large-objects.md`).

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).

## 2. Предрелизные проверки

- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error**; `-c Debug` — **0 warning / 0 error**.
- [x] Unit / SQL-gen тесты (Debug, `--no-build`) — **2825 total / 2824 passed / 1 skipped**:
      core 570, sqlite 602 (1 skip — probe `SqliteRowIdLobProbeTests`, `Assert.SkipUnless`),
      sqlserver 456, postgres 539, mysql 182, mariadb 102, clickhouse 374.
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=…`) — **Total 2139, Errors 0, Failed 0, Skipped 155**
      (скипы capability-based); контейнеры реально стартовали (Testcontainers: 4 reusable + запуск;
      PostgreSQL / SQL Server / MySQL / ClickHouse + SQLite).
- [x] Покрытие (репро CI: `dotnet-coverage collect` + `reportgenerator`) — **Line 84.2% / Branch 75.5% /
      Method 73.8%** ≥ `MIN_LINE_COVERAGE` (75%); core 84.4%; `collect` exit 0, 0 failed
      (после фикса флейков, §3.5).
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **0 warning / 0 error**.
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.8-b` — ок; nuspec проверен:
      `nextorm.mariadb → nextorm.mysql 1.0.8-b`, провайдеры → `nextorm 1.0.8-b`, все пакеты `1.0.8-b`.
- [ ] `nextorm-code-auditor` — release-blocking P0/P1 нет; регистры обновлены.

## 3. Изменения в репозитории (поверх HEAD `aa5cfa7`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.7-beta</Version>` → `1.0.8-b`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.7-beta` → `1.0.8-b`.
- в список `### Guide` добавлена глава 30 (Streaming large objects (BLOB/CLOB)).
- `## Releases` — добавлен раздел `### 1.0.8-b` (issues #25–#99 + строка про новую главу).

### 3.3. CI: вернуть в git CPM-версии примеров

- `examples/Directory.Packages.props` был исключён локальным `.git/info/exclude` (строка повторена 8 раз)
  и не попадал в репозиторий → на CI `dotnet restore` падал `NU1010` (примеры ссылаются на пакеты
  `nextorm*` как `PackageReference`, а версии — во вложенном props). Предыдущие прогоны (в т.ч. `466a1bc`)
  были красными по этой причине.
- Фикс: строка убрана из exclude; файл добавлен в git. Внутри `NextOrmVersion=1.0.7-b` (последняя
  опубликованная); после публикации 1.0.8-b — бампнуть (см. §4).

### 3.4. Служебное

- **`docs/specs/release-1.0.8-b.md`** — этот план.
- Удалён status-файл закрытого docs-потока `docs/specs/status/guide-24-sql-1.md`; его незакрытый
  `Deferred` перенесён в задачу
  ([#102](https://github.com/AlexeyShirshov/nextorm/issues/102)) и план
  `docs/specs/roadmap/todo_guide24_time_range.md`.

### 3.5. Флаки SQLite под coverage — исправлены

- **#1 `SqliteIntegrationTests.Merge_Returning_ShouldReturnWrittenRow`**
  (`SQLITE Error 5: database is locked`): `SqliteTestProvider` использует **один файл БД на процесс**,
  а SQLite-интеграционные классы шли параллельно → под замедлением coverage ловился `SQLITE_BUSY` на
  `MERGE … RETURNING`. **Исправлено** паттерном релиза 1.0.7: `[Collection("Sqlite")]` на
  `SqliteIntegrationTests`, `SqliteSpecificTests`, `SqliteTableValuedParameterTests`,
  `LobPerfHarnessTests` (сериализация доступа к общему файлу).
- **#2 `InListCacheTests.In_InlineArray_StructurallyEqualCallSites_ShouldReuseCachedPlan`** (ранее
  «accepted, триггер — повтор в CI»; триггер сработал). Причина подтверждена: `DataContext.cs:62`
  создаёт `new QueryCache(QueryPlanStore.Clear)`, а `QueryPlanStore.Clear()` — **process-wide**
  (generation bump в `[ThreadStatic]`-хранилище): параллельный sqlite-тест с `ctx.PurgeQueryCache()`
  обнулял план-кэш между `first` и `second` этого теста. **Исправлено** (решение владельца, вариант A):
  сериализация тестовых сборок, делящих process-wide кэш —
  `[assembly: Parallelization(Mode = ParallelMode.None)]` (xunit v3) в
  `tests/nextorm.sqlite.tests/Usings.cs` и `tests/nextorm.core.tests/Usings.cs`
  (устаревшее `CollectionBehavior(DisableTestParallelization)` отклоняется компилятором — `CS0619`).
- Проверка после фиксов: `dotnet build nextorm.slnx -c Debug` — **0/0**; повторный coverage-прогон
  (репро CI) — `collect` **exit 0**, **total 4964 / failed 0 / succeeded 3276 / skipped 1688**.
- Не регресс 1.0.8-b: оба флейка — тестовая инфраструктура; утверждения тестов не менялись.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.8-b` и запушить её.
- [ ] Убедиться, что ветка `1.0.8-b` зелёная (coverage хардфейлит только на `main`).
- [ ] Создать тег `v1.0.8-b` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#108-b> + 7 пакетов + issues #25–#99.
- [ ] Убедиться, что milestone 1.0.8-b закрыт (его issues уже closed).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.8-b`.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.8-b`; `docs/index.md` обновлён (Status + Guide + Releases).
- [ ] Тег `v1.0.8-b` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#108-b` доступен.
- [ ] Milestone 1.0.8-b и issues #25–#99 закрыты.

## 6. Открытые вопросы

- **Версия:** фактически опубликованный прошлый релиз — пакет `1.0.7-b` / тег `v1.0.7-b` (план писал
  `1.0.7-beta`); выбран согласованный с milestone вариант `1.0.8-b` / `v1.0.8-b`.
- **`examples/Directory.Packages.props`** — почему игнорировался: `.git/info/exclude` (локальный,
  на CI не влияет; ломал restore отсутствием файла). Значение `NextOrmVersion` — бампать после публикации.
- **pg-xid (`uint`/`xid`/`xmin`)** — незакоммиченный параллельный поток (ProviderDialect/PostgresDataContext,
  docs guide 27 / provider-specific); **в milestone 1.0.8-b не входит**, решает владелец.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.
