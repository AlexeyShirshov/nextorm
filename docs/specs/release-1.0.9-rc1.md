# План релиза 1.0.9-rc1 (milestone 1.0.9-rc1)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-b.md`, `git show v1.0.9-b`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.9-rc1` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка в работе; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-rc1](https://github.com/AlexeyShirshov/nextorm/milestone/19) — 23 issues, все закрыты (open=0, closed=23).
- **Тег:** `v1.0.9-rc1` (планируется)
- **Ветка релиза:** `1.0.9-rc1` (синхронна с `origin/1.0.9-rc1`)
- **База:** тег `v1.0.9-b` (`815e0127`); HEAD до подготовки — `f6bd8fa0`, 58 коммитов от `v1.0.9-b`.
- **Версии пакетов:** `1.0.9-rc1` задаётся **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-rc1`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.9-b.

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#198](https://github.com/AlexeyShirshov/nextorm/issues/198) | ClickHouse: нативная поддержка JSON (`[JsonColumn]`, `JsonDocument`, `JsonElement`) |
| [#197](https://github.com/AlexeyShirshov/nextorm/issues/197) | PostgreSQL: чтение `JsonNode` из нативных `json`/`jsonb`-колонок |
| [#194](https://github.com/AlexeyShirshov/nextorm/issues/194) | Raw-row materialization: поддержка PostgreSQL `ROW`/composite |
| [#182](https://github.com/AlexeyShirshov/nextorm/issues/182) | SQL Server: паритет скалярных функций с linq2db (metadata / date-part / checksum) |
| [#181](https://github.com/AlexeyShirshov/nextorm/issues/181) | SQLite: полнотекстовый поиск FTS3/FTS4/FTS5 |
| [#161](https://github.com/AlexeyShirshov/nextorm/issues/161) | ClickHouse: экранирование обратных слэшей в идентификаторах |
| [#150](https://github.com/AlexeyShirshov/nextorm/issues/150) | ClickHouse: нативный паритет extreme-row для float/double ключей |
| [#141](https://github.com/AlexeyShirshov/nextorm/issues/141) | Version-gates: MariaDB 13 и PostgreSQL FILTER-агрегаты для 9.2/9.3 |
| [#140](https://github.com/AlexeyShirshov/nextorm/issues/140) | PostgreSQL: свободный (provider-specific) список колонок |
| [#134](https://github.com/AlexeyShirshov/nextorm/issues/134) | `ToDataReader` на SQLite |
| [#133](https://github.com/AlexeyShirshov/nextorm/issues/133) | Streaming LOB: MySQL/MariaDB и ClickHouse |
| [#132](https://github.com/AlexeyShirshov/nextorm/issues/132) | SQLite: поддержка JSON1 |
| [#131](https://github.com/AlexeyShirshov/nextorm/issues/131) | PostgreSQL: маппинг нативной колонки `json`/`jsonb` |
| [#128](https://github.com/AlexeyShirshov/nextorm/issues/128) | ClickHouse: маппинг нативного типа колонки JSON |
| [#127](https://github.com/AlexeyShirshov/nextorm/issues/127) | SQL Server: `OUTPUT INTO` в табличную переменную (`DECLARE @t TABLE`) |
| [#126](https://github.com/AlexeyShirshov/nextorm/issues/126) | Tuple-конструктор `(a, b)` на MySQL/MariaDB/SQLite |
| [#193](https://github.com/AlexeyShirshov/nextorm/issues/193) | Tuple `IN`/`Contains`: трансляция и исполнение во всех провайдерах |
| [#195](https://github.com/AlexeyShirshov/nextorm/issues/195) | FTS5 maintenance/control: поверхность команд (`AutoMerge`/`CrisisMerge`/`Merge`/`Optimize`/`Rebuild`/`IntegrityCheck`) |
| [#196](https://github.com/AlexeyShirshov/nextorm/issues/196) | FTS5 maintenance: тесты на реальном SQLite + документация EN/RU |
| [#168](https://github.com/AlexeyShirshov/nextorm/issues/168) | SQL Server: `MapColumnExpression` больше не боксит числовые значения |
| [#163](https://github.com/AlexeyShirshov/nextorm/issues/163) | ClickHouse: `reference→collection` навигация — принятое ограничение провайдера (явный gate) |
| [#199](https://github.com/AlexeyShirshov/nextorm/issues/199) | `InValuesTranslator`: скалярный путь больше не выставляет sticky `QueryCommand.Cache=false` (отравление plan-cache) |
| [#200](https://github.com/AlexeyShirshov/nextorm/issues/200) | Perf: снижены аллокации свежего cached-пути CTE/RecursiveCTE (fast-path `CteHoister.Hoist`) |

Новых страниц документации в этом релизе нет — только правки существующих страниц EN + RU
(`guide/03`, `guide/11`, `guide/12`, `guide/14`, `guide/15`, `guide/26`, `guide/28`,
`guide/provider-specific/*`, `advanced/limitations`, `advanced/api-reference`,
`advanced/select-where-extrema-native`).

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).
Пакет `nextorm.entityframeworkcore` в публикацию **не входит** (не в списке job `publish`).
**Breaking changes:** нет.

## 2. Предрелизные проверки

- [x] `dotnet build nextorm.slnx -c Debug` — **0 warning / 0 error** (50.7 s).
- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error** (27.3 s).
- [x] Unit / SQL-gen тесты (`dotnet test nextorm.slnx -c Debug --no-build`) — **Total 8931, Failed 0**
      (Succeeded 6314, Skipped 2617 — provider-тесты `nextorm.integration.tests` без `DOCKER_HOST`).
- [ ] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) —
      **не валидно / блокер**. Первый прогон дал Total 3311, Failed 0, но он шёл на **reuse-контейнере** Postgres,
      уже содержавшем extensions `hstore`/`ltree` с прошлых прогонов (reuse-маскировка). На **чистом** Postgres-контейнере
      (reuse удалён, воспроизведено локально) падают **2** теста `PostgresRawRowTests` — ровно как на CI (см. §6).
      Остальные провайдеры (SQL Server, MySQL, MariaDB, ClickHouse, SQLite) стартуют и проходят.
- [x] Покрытие (репро CI: `dotnet-coverage collect` + `reportgenerator`) — **Line 87 % / Branch 79.2 %**
      (порог 85 / 75).
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **exit 0**, 0 error
      (2 warning — дубликаты исходников в пустом `nextorm.core.sourcegenerator`, не связаны с релизом).
- [x] `nextorm-code-auditor` — **release-blocking P0/P1 отсутствуют**; P2: `RG-1`
      (`ExtremeRowRenderRequest.keyColumns` — публичный positional-record), `RG-2` (публичная
      поверхность не заморожена, #53), Находка 240 (`ReplaceCommand` пересчитывает
      `HasUnkeyedScalarInValues` на каждом `.Any()`/`.Count()`).
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.9-rc1` (dry-run) — **успешно** (7 `.nupkg`).

## 3. Изменения в репозитории (поверх HEAD `f6bd8fa0`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.9-b</Version>` → `1.0.9-rc1`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.9-b` → `1.0.9-rc1`.
- `## Releases` — добавлен раздел `### 1.0.9-rc1` (issues #126–#200).

### 3.3. `docs/ru/index.md`

- Без изменений: новых страниц в релизе нет, секции `Status`/`Releases` в русском индексе не ведутся.

### 3.4. Гигиена: `artifacts/` больше не отслеживается

- 123 файла под `artifacts/` (~972k строк coverage-отчётов и test-дампов) были закоммичены в
  ходе цикла 1.0.9-rc1. Каталог добавлен в `.gitignore` и снят с отслеживания
  (`git rm -r --cached artifacts`, файлы остаются на диске) — коммит `f6bd8fa0`.

### 3.5. Служебное

- **`docs/specs/release-1.0.9-rc1.md`** — этот план.
- **`docs/specs/design/code-smells-review.md`**, **`docs/specs/design/API-NAMING-REVIEW.md`** — записи
  предрелизного аудита (только регистры, продуктовый код не менялся).

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9-rc1` и запушить её.
- [ ] Убедиться, что ветка `1.0.9-rc1` зелёная (coverage hard-fail только на `main`).
- [ ] (Опционально) влить `1.0.9-rc1` в `main` через PR — как для предыдущих релизов.
- [ ] Создать тег `v1.0.9-rc1` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-rc1> + 7 пакетов + issues #126–#200.
- [ ] Убедиться, что milestone 1.0.9-rc1 закрыт (его issues уже closed).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc1`
      (сейчас там `1.0.8-b`).

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.9-rc1`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.9-rc1` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-rc1` доступен.
- [ ] Milestone 1.0.9-rc1 и issues #126–#200 закрыты.

## 6. Открытые вопросы

- **Красный CI — [исправлено #202, ждёт push].** С влития #194 job `build` был красный: на **чистом** Postgres
  падали 2 теста `PostgresRawRowTests.UnmappedColumnType_/UnmappedLtreeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError`
  (ждали `InvalidOperationException: None of the result-set columns…`, получали
  `NotSupportedException: …a named composite column…`). Причина — `RawMapperFactory.TryGetUnresolvableCompositeColumn`
  (`src/nextorm.core/DataContext/RawMapperFactory.cs:369`, discriminator `InvalidCastException` `:380`, throw `:617`)
  на чистом каталоге классифицирует незамапленный не-композит (`hstore`/`ltree`) как named composite; локально
  маскировалось reuse-контейнером Postgres. **#202 (PDCA-цикл)**: test-only фикс — прогрев type-catalog
  (`ReloadTypes`) + явный guard + cold-тесты; продуктовый код не менялся. Полный прогон на чистом контейнере
  зелёный (8933/0 failed, coverage 87/79.2). Фикс **uncommitted**; после push владельцем CI должен быть
  зелёным (**E09 — post-cycle release gate**). Продуктовая часть (вернуть R05, унификация single/multi путей) —
  [#203](https://github.com/AlexeyShirshov/nextorm/issues/203). **До зелёного CI тег `v1.0.9-rc1` создавать нельзя.**
- **Breaking changes:** отсутствуют; релиз prerelease (`rc`), backward compatibility между
  пререлизами не гарантируется (см. `readme.md` §Status).
- **1.0.9-rc2** — отдельный милстоун (26 открытых issues) и следующая итерация; в этот релиз не входит.
- **Остаточный alloc-разрыв cached-пути** (Cte/RecursiveCte/Distinct/Except/Intersect) остаётся
  бюджетированным; структурное закрытие — [#201](https://github.com/AlexeyShirshov/nextorm/issues/201) (M12 #3).
- **`nextorm.entityframeworkcore`** — пакет есть, но в job `publish` (список из 7) его нет; решает владелец.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.
