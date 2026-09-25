# План релиза 1.0.7-beta (milestone 1.0-b.1)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.6-a.md`, `git show v1.0.6-alpha`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.7-beta` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка завершена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0-b.1](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-b.1) («1.0-beta») —
  11 issues (#15, #31, #63, #64, #66, #85–#90), все реализованы и закрыты (open=0, closed=11)
- **Тег:** `v1.0.7-beta` (планируется)
- **Ветка релиза:** `1.0-b.1` → вливается в `main` через PR
- **База:** тег `v1.0.6-alpha`; HEAD до подготовки — `da5caad` (#90 docs, specs, audit registers)
- **Версии пакетов:** `1.0.7-beta` задана **одной строкой** в `Directory.Build.props` (`<Version>`)
  на все проекты; CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный
  бамп нужен локальному `dotnet pack` и зависимостям nuspec
  (`nextorm.mariadb → nextorm.mysql 1.0.7-beta`)
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4/1.0.5/1.0.6

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#15](https://github.com/AlexeyShirshov/nextorm/issues/15) | `OUTPUT INTO`, несколько result-set'ов, upsert-with-output |
| [#31](https://github.com/AlexeyShirshov/nextorm/issues/31) | Value converters — `EnumToStringConverter`, конвертеры в предикатах и проекциях |
| [#63](https://github.com/AlexeyShirshov/nextorm/issues/63) | Динамическая схема результата (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`) |
| [#64](https://github.com/AlexeyShirshov/nextorm/issues/64) | JSON-колонка ↔ CLR-объект (авто-сериализация свойства) |
| [#66](https://github.com/AlexeyShirshov/nextorm/issues/66) | PostgreSQL range-типы и `Overlaps` (`&&`) |
| [#85](https://github.com/AlexeyShirshov/nextorm/issues/85) | SQL Server 2025 — `regexp_like` / `regexp_replace` |
| [#86](https://github.com/AlexeyShirshov/nextorm/issues/86) | Портативный range как пара колонок — `[RangeColumns]` |
| [#87](https://github.com/AlexeyShirshov/nextorm/issues/87) | Скалярные функции: кросс-провайдерный фасад `SqlFunctions.Sql` и пробелы по провайдерам |
| [#88](https://github.com/AlexeyShirshov/nextorm/issues/88) | Value converters — фаза 2 и JSON-колонки |
| [#89](https://github.com/AlexeyShirshov/nextorm/issues/89) | In-memory функции, структурный ключ плана запроса, dictionary lookup |
| [#90](https://github.com/AlexeyShirshov/nextorm/issues/90) | Спеки, регистры аудита, документация EN + RU |

Плюс объём, не привязанный к отдельному issue (коммиты `940c0a1`, `671cee2` — bug fixes / refactoring)
и новые главы руководства EN + RU: 30 (Value converters and JSON columns), 31 (Range columns).

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).

## 2. Предрелизные проверки

- [x] Issues милстоуна (#15, #31, #63, #64, #66, #85–#90) реализованы и закоммичены
      (HEAD `da5caad`); открытых блокеров из бэклога (`docs/specs/roadmap/todo_*.md`) для вошедшего
      функционала нет (удалены `todo_dynamic_result_schema.md`, `todo_json_column_mapping.md`,
      `todo_postgres_ranges.md`, `todo_value_converters.md`).
- [x] `dotnet build nextorm.slnx -c Release` и `-c Debug` — **0 warning / 0 error**
      (`TreatWarningsAsErrors=true`; `CS1591` не подавляется).
- [x] Unit / SQL-gen тесты (Debug, `--no-build`) — **2423 passed, 0 failed, 0 skipped**:
      core 401, sqlite 514, sqlserver 388, postgres 521, mysql 168, mariadb 93, clickhouse 338.
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`)
      — **Total 1812, Errors 0, Failed 0, Skipped 86**. Все 5 контейнеров (PostgreSQL, SQL Server,
      MySQL, ClickHouse + ryuk) подняты с сокета, class-level skip'ов нет; скипы capability-based
      (RETURNING/`OUTPUT`, CTAS-батчи, `INTERSECT/EXCEPT ALL`, integer `AVG`, `FULL JOIN`, JSON-колонки и т.п.).
- [x] Покрытие (репро CI: `dotnet-coverage collect` + `reportgenerator`) — **Line 83.9% /
      Branch 75.1% / Method 72.4%** ≥ `MIN_LINE_COVERAGE` (75%). Инструментируются 4 сборки
      (core, sqlite, sqlserver, postgres), core 83.9%.
- [x] Docs EN + RU синхронны (по 2 новые главы 30/31 в каждой); `dotnet docfx docs/docfx.json` —
      **0 errors, 0 warnings** (починен битый xref `NextORM.Core.SqlFunctions.PostgresFunctions` →
      `NextORM.Core.PostgresFunctions` в `providers/postgres.md`, EN+RU — §3).
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.7-beta` — ок; зависимости в nuspec проверены:
      `nextorm.mariadb → nextorm.mysql 1.0.7-beta`, провайдеры → `nextorm 1.0.7-beta`.
- [x] `nextorm-code-auditor` (25.09.2026, HEAD `da5caad` + uncommitted): **release-blocking P0/P1 нет**.
      Подавления — 11/11 оправданных, реальных `<NoWarn>` 0, XML-doc покрытие публичного API — 100%.
      Регистры (`docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`)
      обновлены разделом предрелизного аудита v1.0.7-beta.

## 3. Изменения в репозитории (поверх HEAD `da5caad`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.6-alpha</Version>` → `1.0.7-beta` (одна строка на 7 пакетов).

### 3.2. `docs/index.md`

- `## Status` — `1.0.7-beta`.
- `## Roadmap` — `[1.0-beta]` → `[1.0.7-beta]` (milestone `1.0-b.1`).
- в список `### Guide` добавлены главы 30 (Value converters and JSON columns) и 31 (Range columns).
- `## Releases` — добавлен раздел `### 1.0.7-beta` (issues #15–#90 + строка про новые главы).

### 3.3. `readme.md`

- `## Status`: `Alpha` → `Beta`.

### 3.4. Битый xref (DocFX)

- `docs/providers/postgres.md` и `docs/ru/providers/postgres.md`:
  `xref:NextORM.Core.SqlFunctions.PostgresFunctions` → `xref:NextORM.Core.PostgresFunctions`
  (тип объявлен как `NextORM.Core.PostgresFunctions`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`).

### 3.5. Служебное

- **`docs/specs/release-1.0.7-b.md`** — этот план.
- **Регистры аудита** — `docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`
  (раздел предрелизного аудита).

### 3.6. Флак интеграционных тестов SQL Server (гонка на `MergeEntities`)

- CI (`36171803706`, sha `6680248`) упал на
  `SqlServerIntegrationTests.Merge_ConditionalMatchedBranch_ShouldUpdateOnlyWhenConditionHolds`:
  `Single()` не нашёл строку (`throw new InvalidOperationException()` в `QueryExecutor.cs:801`).
  Повторный прогон **того же коммита** — зелёный → флак, а не регресс.
- Причина: `SupportsMergeBySourceDelete => true` только у SQL Server (`SqlServerDialect.cs:116`).
  Тесты с `WHEN NOT MATCHED BY SOURCE THEN DELETE` — `CommonTestSuite.Merge.Merge_WhenNotMatchedBySource_ShouldDeleteStaleTarget`
  и `SqlServerSpecificTests.FullMerge_NotMatchedBySource_ShouldDeleteOrphan` — удаляют **все** строки
  общей таблицы `MergeEntities`, кроме своей. Классы `SqlServerIntegrationTests` (общий набор) и
  `SqlServerSpecificTests` идут параллельно (разные xunit-коллекции) на одной БД, поэтому первый удалял
  строку второго между его двумя MERGE.
- Фикс: `[Collection("SqlServer")]` на обоих классах — пара сериализуется, провайдеры по-прежнему
  параллельны. Контрольный полный прогон — **1812 passed, 0 failed, 86 skipped**.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0-b.1` и запушить её.
- [ ] Убедиться, что ветка `1.0-b.1` зелёная (coverage хардфейлит только на `main`).
- [ ] Свести `main` с веткой через PR.
- [ ] Создать тег `v1.0.7-beta` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease, target `main`): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#107-beta> + 7 пакетов + issues #15–#90.
- [ ] Убедиться, что milestone 1.0-b.1 закрыт (его issues уже closed).

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.7-beta`; `docs/index.md` обновлён (Status + Roadmap + Releases + Guide).
- [ ] Тег `v1.0.7-beta` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#107-beta` доступен.
- [ ] Milestone 1.0-b.1 и issues #15–#90 закрыты.

## 6. Открытые вопросы

- **Почему `1.0.7-beta`, а не `1.0-b.1`:** `1.0-b.1` нормализуется NuGet'ом в `1.0.0-b.1`, что по SemVer
  **ниже** уже опубликованного `1.0.6-alpha` (patch `0` < `6`) — пакет выглядел бы даунгрейдом и не был бы
  «последним» для `dotnet add package --prerelease`. Числовая часть обязана быть ≥ 7.
- **Охват `publish`:** все 7 пакетов (без изменений с 1.0.4/1.0.5/1.0.6).
- **Release notes:** остаются секцией `## Releases` в `docs/index.md`; отдельный `docs/releases.md` не нужен.
- **Фикс `concurrency` для tag-push** (кандидат с 1.0.3): не блокирует.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.

## 7. Что осталось (не блокирует релиз)

- После публикации перепроверить, что все 7 пакетов `1.0.7-beta` видны на nuget.org (индексация — от минут до часов).
- Открытые P2 аудита: заморозка публичного API (issue #53) и P2-семейства по Duration/String/Interceptors/Batch.
- Открытые `todo_*.md` следующего скоупа: `todo_query_filters.md`, `todo_efcore_integration.md`,
  `todo_json_*`, `todo_sharding.md`, `todo_tvp.md`, `todo_stored_procedures.md` и др.
