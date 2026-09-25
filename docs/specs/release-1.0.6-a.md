# План релиза 1.0.6-alpha (milestone 1.0-a.6)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.5-a.md`, `git show v1.0.5-alpha`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.6-alpha` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка завершена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0-a.6](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.6) — 13 issues
  (#28, #30, #68, #71, #72, #75, #77–#83), все реализованы и закрыты (open=0, closed=13)
- **Тег:** `v1.0.6-alpha` (планируется)
- **Ветка релиза:** `1.0.6-alpha` → вливается в `main` через PR
- **База:** тег `v1.0.5-alpha`; HEAD ветки — `9e82392` (`refactoring batch`) + объём подготовки (§3)
- **Версии пакетов:** `1.0.6-alpha` задана **одной строкой** в `Directory.Build.props` (`<Version>`)
  на все проекты; CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный
  бамп нужен локальному `dotnet pack` и зависимостям nuspec
  (`nextorm.mariadb → nextorm.mysql 1.0.6-alpha`)
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4/1.0.5

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#28](https://github.com/AlexeyShirshov/nextorm/issues/28) | column collation — `Collate(...)` в маппинге и запросах |
| [#30](https://github.com/AlexeyShirshov/nextorm/issues/30) | Interceptors — `IQueryInterceptor` / `IConnectionInterceptor` (команды и соединения) |
| [#68](https://github.com/AlexeyShirshov/nextorm/issues/68) | Трансляция Regex в запросах — `Regex.IsMatch` / `Regex.Replace` |
| [#71](https://github.com/AlexeyShirshov/nextorm/issues/71) | C# string-семантика — ordinal-сравнения, format-спецификаторы, culture |
| [#72](https://github.com/AlexeyShirshov/nextorm/issues/72) | TimeSpan/interval-колонки и точность дат — атрибут `[Duration]` |
| [#75](https://github.com/AlexeyShirshov/nextorm/issues/75) | DDL/DML + читающий запрос в одном SQL-батче (pgbouncer-safe CTAS) |
| [#77](https://github.com/AlexeyShirshov/nextorm/issues/77) | Кросс-провайдерные строковые/числовые обёртки `SqlFunctions.Sql` |
| [#78](https://github.com/AlexeyShirshov/nextorm/issues/78) | ClickHouse: пробелы функций (`lowerUTF8`/`upperUTF8`, `trim*`, `replaceRegexp*`, `map*`, массивы, `groupBitmap`/`sumMap`, хэши, `generateULID`) |
| [#79](https://github.com/AlexeyShirshov/nextorm/issues/79) | MariaDB: пробелы функций (`NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`, `TO_CHAR`/`TO_DATE`/`TO_NUMBER`, `KDF`, `XXH3`, `JSON_DETAILED`/`JSON_COMPACT`, последовательности) |
| [#80](https://github.com/AlexeyShirshov/nextorm/issues/80) | MySQL: пробелы функций (`FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `STR_TO_DATE`, `DATE_FORMAT`, `FROM_UNIXTIME`, JSON-mutation, `UUID_TO_BIN`) |
| [#81](https://github.com/AlexeyShirshov/nextorm/issues/81) | PostgreSQL: пробелы функций (`sha224/384/512`, `regexp_substr`, `make_*`, `age`, `date_bin`, `current_setting`, последовательности, SQL/JSON) |
| [#82](https://github.com/AlexeyShirshov/nextorm/issues/82) | SQLite: пробелы функций (JSON1, `printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`, `typeof`, `glob`, `unicode`/`char`, `timediff`, `Math.*`) |
| [#83](https://github.com/AlexeyShirshov/nextorm/issues/83) | SQL Server: пробелы функций (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `TRANSLATE`, `FORMAT`, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, JSON-агрегаты) |

Плюс объём, не привязанный к отдельному issue (коммиты `2e9a587` и `9e82392`):

- **SQL-батчи и temp-table surface** (#75): `BatchBuilder` / `BatchRunner` / `BatchQuery`, `CreateTableOptions`
  (`CreateTableOptionsBuilder`), `TempTableSource`, `TempTableExtensions`, `DropTableCommand`; CTAS/временные
  таблицы параметризованы (column list, `IF NOT EXISTS`, `ON COMMIT`, `WITH NO DATA` — по возможностям движка);
- **Документация EN + RU:** 4 новые главы руководства — 26 duration (`[Duration]`), 27 interceptors,
  28 SQL-батчи, 29 оптимистичный параллелизм/change tracking — и provider-specific главы MySQL/MariaDB и
  SQLite; регистрация в `docs/guide/toc.yml`, `docs/ru/toc.yml` и в списке Guide на `docs/index.md`;
- **Реестры аудита** обновлены (`docs/specs/design/code-smells-review.md`,
  `docs/specs/design/API-NAMING-REVIEW.md`), добавлен раздел «Предрелизный аудит v1.0.6-alpha».

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).

## 2. Предрелизные проверки

- [x] Issues милстоуна (#28, #30, #68, #71, #72, #75, #77–#83) реализованы и закоммичены
      (HEAD `9e82392`); блокеров из бэклога (`docs/specs/roadmap/todo_*.md`) для вошедшего
      функционала нет (удалены/закрыты `todo_regex.md`, `todo_string_semantics.md`, `todo_sql_batch.md`,
      `todo_timespan_columns.md`, `todo_interceptors.md`, провайдерные `todo_*_function_gaps.md`).
- [x] `dotnet build nextorm.slnx -c Release` и `-c Debug` — **0 warning / 0 error**
      (`TreatWarningsAsErrors=true`; `CS1591` не подавляется).
- [x] Unit / SQL-gen тесты (Debug, `--no-build`) — **2137 passed, 0 failed, 0 skipped**:
      core 319, sqlite 444, sqlserver 358, postgres 468, mysql 151, mariadb 81, clickhouse 316.
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`)
      — **Total 1697, Errors 0, Failed 0, Skipped 87**. Скипы capability-based (CTAS-батчи у SQL Server,
      `json_each`-TVF, `INTERSECT/EXCEPT ALL`, integer `AVG`, `FULL JOIN`, regex-операторы и т.п.);
      контейнеры PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse стартовали и были опрошены —
      «provider skipped из-за отсутствия `DOCKER_HOST`» в прогоне нет.
- [x] Покрытие (CI-пайплайн, unit-прогон) — **Line 83.2% / Branch 74% / Method 72%** ≥
      `MIN_LINE_COVERAGE` (75%). По сборкам: core 83.1%, sqlite 89.4%, sqlserver 82.3%, postgres 80.7%
      (инструментируются только эти 4).
- [x] Docs EN + RU синхронны (по 4 новые главы в каждой + provider-specific MySQL/SQLite);
      `dotnet docfx docs/docfx.json` — **0 errors, 0 warnings**.
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.6-alpha` — ок; зависимости в nuspec проверены:
      `nextorm.mariadb → nextorm.mysql 1.0.6-alpha`, провайдеры → `nextorm 1.0.6-alpha`.
- [x] `nextorm-code-auditor` (25.09.2026, HEAD `9e82392` + uncommitted): **release-blocking P0/P1 нет**.
      Подавления: 6 `SuppressMessage` + 5 `#pragma` = **11/11 оправданных**, `Skip=` 0, пустых `catch` 0,
      реальных `<NoWarn>` 0; XML-doc покрытие публичного API — 100%. Статусы 82/84/161/162 в реестре
      переведены в закрытые. Регистры обновлены.

### 2.1. Правки, внесённые в ходе подготовки

- **Устаревшие ожидания в 4 SQL-gen тестах** (`SqlGenerationTests` SQL Server / PostgreSQL / MySQL /
  ClickHouse, `IndexOfLastIndexOf_*`): коммит `18ed028` (#68) намеренно обернул `substringLength` в скобки
  в `SqlDialectBase.MakeStringLastIndexOf` (`({substringLength})`), но ожидания тестов не обновили —
  ассерты дополнены `(len('b'))` / `(length('b'))` / `(char_length('b'))` / `(lengthUTF8('b'))`.
  Сгенерированный SQL не менявлся (скобки избыточны, но корректны).
- **6 битых xref в docs** (DocFX `UidNotFound`, EN+RU): `IScalarFunctions.Supports` →
  `IScalarFunctions.Supports(System.String)`; `ISqlDialect.MakeDurationType` /
  `MakeNullableDurationType` → сигнатура `System.Nullable{NextORM.Core.DurationUnit}, System.Int32`.
  После правок DocFX — 0/0.

## 3. Изменения в репозитории (поверх HEAD `9e82392`)

- **`Directory.Build.props`:** `<Version>1.0.5-alpha</Version>` → `1.0.6-alpha`.
- **`docs/index.md`:**
  - `## Status` — `1.0.6-alpha`;
  - в список `### Guide` добавлены главы 26–29;
  - в `## Roadmap` добавлен milestone `1.0.6-alpha` (`1.0-a.6`);
  - `## Releases` — добавлен раздел `### 1.0.6-alpha` (#28–#83 + строка про новые главы).
- **`docs/specs/release-1.0.6-a.md`** — этот план.
- **4 тест-файла** (`tests/nextorm.{sqlserver,postgres,mysql,clickhouse}.tests/SqlGenerationTests.cs`) — §2.1.
- **6 docs-файлов** (EN+RU `limitations.md`, `11-scalar-functions.md`, `26-duration-columns.md`) — §2.1.
- **Регистры аудита** — `docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`.

## 4. Публикация (шаги владельца)

- [ ] Убедиться, что ветка `1.0.6-alpha` зелёная (coverage хардфейлит только на `main`).
- [ ] Закоммитить подготовленный объём (бамп версии, `docs/index.md`, этот план, §2.1-правки, регистры)
      и запушить ветку `1.0.6-alpha`.
- [ ] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.6-alpha` — проверить зависимости
      (`nextorm.mariadb → nextorm.mysql 1.0.6-alpha`, провайдеры → `nextorm 1.0.6-alpha`) — **уже проверено §2**.
- [ ] Свести `main` с веткой через PR.
- [ ] Создать тег `v1.0.6-alpha` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease, target `main`): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#106-alpha> + 7 пакетов + issues #28–#83.
- [ ] Убедиться, что milestone 1.0-a.6 закрыт (его issues уже closed).

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.6-alpha`; `docs/index.md` обновлён (Status + Roadmap + Releases + Guide).
- [ ] Тег `v1.0.6-alpha` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#106-alpha` доступен.
- [ ] Milestone 1.0-a.6 и issues #28–#83 закрыты.

## 6. Открытые вопросы

- **Охват `publish`:** все 7 пакетов (без изменений с 1.0.4/1.0.5).
- **Release notes:** остаются секцией `## Releases` в `docs/index.md`; отдельный `docs/releases.md` не нужен.
- **Фикс `concurrency` для tag-push** (кандидат с 1.0.3): не блокирует.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.

## 7. Что осталось (не блокирует релиз)

- После публикации перепроверить, что все 7 пакетов `1.0.6-alpha` видны на nuget.org (индексация — от минут до часов).
- Открытые P2 аудита: заморозка публичного API (issue #53), P2-семейства по Duration
  (вычисленные duration-проекции, `Precision`, бокс при non-native чтении), String, Interceptors, Batch,
  `CreateTableOptions? = null` на temp-table-членах.
- `todo_clickhouse_aggregate_function_state.md` (§4.6) — заблокирован драйвером `ClickHouse.Driver` 1.4.0.
- Открытые `todo_*.md` следующего скоупа: `todo_query_filters.md`, `todo_efcore_integration.md`,
  `todo_json_*`, `todo_sharding.md` и др.
