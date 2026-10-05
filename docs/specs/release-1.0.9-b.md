# План релиза 1.0.9-b (milestone 1.0.9-b)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-a.md`, `git show v1.0.9-a`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.9-b` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполнена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-b](https://github.com/AlexeyShirshov/nextorm/milestones/1.0.9-b) — 37 issues, все закрыты (open=0, closed=37).
- **Тег:** `v1.0.9-b` (планируется)
- **Ветка релиза:** `1.0.9-b` (синхронна с `origin/1.0.9-b`)
- **База:** тег `v1.0.9-a`; HEAD до подготовки — `9b560a3f` (`benchmarks`), 94 коммита от `v1.0.9-a`.
- **Версии пакетов:** `1.0.9-b` задана **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-b`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.9-a.

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#148](https://github.com/AlexeyShirshov/nextorm/issues/148) | Неявные навигационные запросы: одиночные связи, многошаговые цепочки, presence-проверки, `AsEntityBuilder` |
| [#135](https://github.com/AlexeyShirshov/nextorm/issues/135) | Связи: `JoinInto` для many-to-many и one-to-one |
| [#113](https://github.com/AlexeyShirshov/nextorm/issues/113) | Алиасы в join: типизированные alias-проекции через source generator |
| [#146](https://github.com/AlexeyShirshov/nextorm/issues/146) | CTE: типизированные источники с сохранением проекции `Select` (включая рекурсивные) |
| [#136](https://github.com/AlexeyShirshov/nextorm/issues/136) | CTE: тела UPDATE/DELETE как тело CTE |
| [#116](https://github.com/AlexeyShirshov/nextorm/issues/116) | Hoist вложенных CTE в верхний `WITH` (flatten/merge) |
| [#144](https://github.com/AlexeyShirshov/nextorm/issues/144) | `SelectWhereMax/Min`: нативные PG `DISTINCT ON` и ClickHouse `argMax` |
| [#115](https://github.com/AlexeyShirshov/nextorm/issues/115) | `SelectWhereMax/Min` (базовая форма) |
| [#145](https://github.com/AlexeyShirshov/nextorm/issues/145) | Join `.Returning(...)`: явный терминал DELETE вместо неявного |
| [#143](https://github.com/AlexeyShirshov/nextorm/issues/143) | Join/multi-table `Returning`: identity (whole-Projection) форма |
| [#147](https://github.com/AlexeyShirshov/nextorm/issues/147) | API: `Create…Builder` для DML/batch и альтернативные query-фабрики |
| [#130](https://github.com/AlexeyShirshov/nextorm/issues/130) | Хинты: табличные (`WITH`) на присоединённых таблицах |
| [#121](https://github.com/AlexeyShirshov/nextorm/issues/121) | Fluent API для хинтов |
| [#120](https://github.com/AlexeyShirshov/nextorm/issues/120) | Хинты параметрами |
| [#114](https://github.com/AlexeyShirshov/nextorm/issues/114) | `WithStrictness` и `Global` — перенос в конфиг Join через лямбду |
| [#112](https://github.com/AlexeyShirshov/nextorm/issues/112) | Потоковая выдача данных в `Stream` (JSON, CSV) |
| [#39](https://github.com/AlexeyShirshov/nextorm/issues/39) | Потоковая выдача JSON в `Stream` (`WriteJson`/`WriteJsonAsync`) |
| [#118](https://github.com/AlexeyShirshov/nextorm/issues/118) | `ProcedureResult`: стриминг нескольких result set'ов (гетерогенный курсор) |
| [#119](https://github.com/AlexeyShirshov/nextorm/issues/119) | Raw SQL как шаг батча (публичный `BatchBuilder.Raw`) |
| [#117](https://github.com/AlexeyShirshov/nextorm/issues/117) | params-форма для `ExecuteRaw`/`ExecuteRawAsync`/`ExecuteProcedure(Async)` |
| [#125](https://github.com/AlexeyShirshov/nextorm/issues/125) | Глобальные фильтры: мост keyed-фильтров EF Core 10 |
| [#124](https://github.com/AlexeyShirshov/nextorm/issues/124) | Глобальные фильтры: `FromSql`/сырые источники |
| [#123](https://github.com/AlexeyShirshov/nextorm/issues/123) | Глобальные фильтры: фильтрация цели INSERT/MERGE/UPSERT |
| [#139](https://github.com/AlexeyShirshov/nextorm/issues/139) | In-memory: `PrepareFromSql` для сырого SQL |
| [#138](https://github.com/AlexeyShirshov/nextorm/issues/138) | In-memory: источники табличных функций |
| [#137](https://github.com/AlexeyShirshov/nextorm/issues/137) | Dynamic columns: write-side в in-memory, per-key конвертеры/JSON |
| [#186](https://github.com/AlexeyShirshov/nextorm/issues/186) | Унификация обхода result-set: `BatchResult`/`ProcedureResult` напрямую перечисляемы, `ReadSets` удалён (**breaking**) |
| [#122](https://github.com/AlexeyShirshov/nextorm/issues/122) | Удаление `Final()`, `PreWhere(predicate)`, `Settings(("key","value"), ...)` из общего API (**breaking**) |
| [#183](https://github.com/AlexeyShirshov/nextorm/issues/183) | Iteration 15: снижение overhead fresh-fluent cached-пути (perf) |
| [#166](https://github.com/AlexeyShirshov/nextorm/issues/166) | Iteration-14 residual: nested read-CTE warm reuse больше не аллоцирует |
| [#165](https://github.com/AlexeyShirshov/nextorm/issues/165) | Iteration 14: восстановление аллокаций implicit-cache prepare и CTE lookup |
| [#155](https://github.com/AlexeyShirshov/nextorm/issues/155) | ExtremeRow native: убраны неиспользуемое описание payload и дублирующее разрешение alias |
| [#149](https://github.com/AlexeyShirshov/nextorm/issues/149) | Flake: `EfCoreQueryFilterLifecycleTests.Lifecycle_ClearOrEvict_ColdWarmPrepared_Live` |
| [#156](https://github.com/AlexeyShirshov/nextorm/issues/156) | Fix public XML `cref`, указывавшего на internal `SqlBuilder` |
| [#152](https://github.com/AlexeyShirshov/nextorm/issues/152) | nextorm-pdca: фиксация обязательного CHECK-списка анкеров на этапе PLAN (tooling) |
| [#164](https://github.com/AlexeyShirshov/nextorm/issues/164) | Post-restart live-проверка evidence-контракта #152 (tooling) |
| [#158](https://github.com/AlexeyShirshov/nextorm/issues/158) | nextorm-brainstorming: проверка прежних дизайн-решений перед прожаркой (docs/tooling) |

Плюс новые страницы документации EN + RU: глава `guide/28-streaming-data`, глава
`guide/29-implicit-navigation`, `advanced/select-where-extrema-native`, `advanced/ef-core-query-filters`.

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).
Пакет `nextorm.entityframeworkcore` в публикацию **не входит** (не в списке job `publish`).

## 2. Предрелизные проверки

- [x] `dotnet build nextorm.slnx -c Debug --no-restore` — **0 warning / 0 error** (12.9 s).
- [x] `dotnet build nextorm.slnx -c Release --no-restore` — **0 warning / 0 error** (13.3 s).
- [x] Unit / SQL-gen тесты (`dotnet test --no-build`) — **8064 total / 0 failed** (без `DOCKER_HOST`
      provider-тесты `nextorm.integration.tests` скипаются; см. следующий пункт).
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) —
      **Total 3136, Errors 0, Failed 0, Skipped 193** (скипы capability-based); контейнеры реально
      стартовали (PostgreSQL, SQL Server, MySQL, ClickHouse + SQLite; 5 контейнеров остановлены в конце).
- [ ] Покрытие (репро CI: `dotnet-coverage collect` + `reportgenerator`) — **не перемерялось** в этой
      подготовке; CI прогоняет его на push (на ветке порог Line 85 hard-fail только на `main`).
- [ ] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **не запускался** в этой подготовке.
- [ ] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.9-b` — за владельцем (см. §4).
- [ ] `nextorm-code-auditor` — release-blocking P0/P1 не проверялись в этой подготовке.

### Красный CI до подготовки — исправлен (§3.4)

Ветка `1.0.9-b` была **красной** с `cbb9896b` (2026-10-01, последний зелёный — `68dfc8b3`): на
`f3d1ece1`/`158233f4`/`9b560a3f` — одни и те же **17** падений: `nextorm.alias.tests` **15**
(`SQLite Error 1: 'near "Order": syntax error'`) и `nextorm.sqlite.tests` **2**
(`ImplicitNavigationR22Tests.Self_reference_should_join_and_read_the_parent`,
`Self_reference_two_hops_should_chain`). Причина и фикс — §3.4; после фикса оба набора зелёные.

## 3. Изменения в репозитории (поверх HEAD `9b560a3f`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.9-a</Version>` → `1.0.9-b`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.9-a` → `1.0.9-b`.
- `### Guide` — добавлена глава 29 «Implicit navigation queries» (`guide/29-implicit-navigation.md`).
- `### Advanced` — добавлена страница «EF Core query-filter bridge»
  (`advanced/ef-core-query-filters.md`, порядок как в `docs/advanced/toc.yml`).
- `## Releases` — добавлен раздел `### 1.0.9-b` (issues #39–#186 + новые страницы + удаления из API).

### 3.3. `docs/ru/index.md`

- `### Руководство` — добавлена глава 29 «Неявные навигационные запросы».
- `### Продвинутое` — добавлена страница «Мост фильтров EF Core».

### 3.4. Тест-инфраструктура: красный CI исправлен (тестовые файлы, без изменений продукта)

Корень обоих падений — **process-wide кэш metadata** (`DataContextCache.Metadata`, статический
`TimedDictionary`): `From<T>(cfg)` применяет конфиг **только при первом маппинге** типа, а любой
ранний конфиг-less `From<T>()` навсегда фиксирует дефолтное имя таблицы/отсутствие связей. Итог
зависел от порядка тестов (на CI иной, чем локально), поэтому падения были детерминированными на CI.

- **`nextorm.alias.tests`:** `AliasInMemoryRefusalTests` звал `ctx.From<Order>()`/`From<Person>()` без
  `.Table(...)` и мог выполниться раньше SQL-тестов, зафиксировав `Order`→`Order`/`Person`→`Person`;
  последующие `.Table("orders")`/`.Table("person")` игнорировались → `from Order` → SQLite syntax error.
  **Фикс:** сущностям `Order`/`Person` (`tests/nextorm.alias.tests/Entities.cs`) добавлен
  `[SqlTable("orders")]`/`[SqlTable("person")]` — декларативное имя таблицы делает маппинг
  порядко-независимым (проверено отдельным тестом в свежем процессе: до фикса — `near "Order"`, после — зелёно).
- **`nextorm.sqlite.tests`:** `ImplicitNavigationR22Tests.CreateDual()` звал `ctx.From<R22Node>()` без
  `HasOne(n => n.Parent, n => n.ParentId)`; при выполнении раньше self-reference тестов `R22Node`
  фиксировался без связи, и `n.Parent!.Name` биндился к источнику `FROM` (для корня возвращалось его же
  имя вместо `null`). **Фикс:** `CreateDual()` регистрирует ту же связь, что и `CreateSelf()`
  (отрицательный контроль воспроизвёл CI-симптом `Parent = "left-node"` до фикса).
- Продуктовый код не менялся: `DataContextCache.Metadata` first-call-wins — задокументированное
  поведение (`From<T>` XML-doc); это дефект изоляции тестов, а не продукта.

### 3.5. Служебное

- **`docs/specs/release-1.0.9-b.md`** — этот план.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3, включая фикс §3.4) на ветке `1.0.9-b` и запушить её.
- [ ] Убедиться, что ветка `1.0.9-b` зелёная (coverage hard-fail только на `main`).
- [ ] (Опционально) влить `1.0.9-b` в `main` через PR — как для предыдущих релизов.
- [ ] Создать тег `v1.0.9-b` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-b> + 7 пакетов + issues #39–#186.
- [ ] Убедиться, что milestone 1.0.9-b закрыт (его issues уже closed).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-b`.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.9-b`; `docs/index.md` обновлён (Status + Guide + Advanced + Releases).
- [ ] Тег `v1.0.9-b` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-b` доступен.
- [ ] Milestone 1.0.9-b и issues #39–#186 закрыты.

## 6. Открытые вопросы

- **Breaking changes в релизе:** #122 (удаление `Final()`/`PreWhere(predicate)`/`Settings(...)` из
  общего API), #186 (удалён `ReadSets`, `BatchResult`/`ProcedureResult` стали перечисляемыми),
  #114 (`WithStrictness`/`Global` перенесены в конфиг Join). Релиз prerelease (`b`), backward
  compatibility между пререлизами не гарантируется (см. `readme.md` §Status).
- **Покрытие и docfx** в этой подготовке не перемерялись — прогоняются CI на push; hard-fail Line 85
  только на `main`.
- **CI был красным 4 дня** (с 2026-10-01) — до пуша ветка не считалась зелёной; фикс §3.4 нужно
  закоммитить и запушить, чтобы получить зелёный прогон перед тегом.
- **`nextorm.entityframeworkcore`** — есть пакет-проект и тесты, но в job `publish` (список из 7) его
  нет; вне скоупа 1.0.9-b, решает владелец.
- **Follow-ups из #148** (milestone 1.0.9-rc2): #162 (V32 temp-table/TVP navigation — отложено с
  триггером) и #163 (ClickHouse reference→collection — принятое ограничение).
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.
