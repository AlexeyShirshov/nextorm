# План релиза 1.0.3-alpha (milestone 1.0-a.3)

> Основа — релизы предыдущих милстоунов (см. `git show v1.0.2-alpha`: `release 1.0.2-alpha`).
> Прошлый релиз состоял ровно из: списка release notes в `docs/index.md`, строки `Status` и бампа
> `<Version>` в `.csproj` затронутых пакетов. Здесь то же ядро плюс проверки, которые накопились
> к текущему состоянию репозитория (7 пакетов, интеграционные тесты, порог покрытия, docs EN/RU).
>
> **Статус: релиз выпущен** (тег `v1.0.3-alpha`, GitHub Release, CI-публикация 7 пакетов).

- **Milestone:** [1.0-a.3](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.3) — 9 issues, все закрыты
- **Тег:** `v1.0.3-alpha`
- **Ветка релиза:** `1.0.3-alpha` → сведена с `main` через PR [#54](https://github.com/AlexeyShirshov/nextorm/pull/54)
  (merge-коммит `314a6c0` с разрешением конфликтов layout в пользу `src/`); `main` — `2b95d63`
- **Версии пакетов:** `1.0.3-alpha` во всех `src/*.csproj` (CI дополнительно переопределяет версию тегом
  через `-p:Version` — источник истины тег, бамп нужен локальному `dotnet pack` и зависимостям nuspec)
- **Публикация:** CI (`dotnet.yml`, job `publish`), **tag-triggered** (`refs/tags/v*`) через NuGet
  trusted publishing (OIDC, `NuGet/login@v1`); ручной `dotnet nuget push` больше не нужен

## 1. Скоуп милстоуна

Все issues милстоуна закрыты (open = 0):

| # | Release notes |
| --- | --- |
| [#8](https://github.com/AlexeyShirshov/nextorm/issues/8) | Table-valued functions |
| [#9](https://github.com/AlexeyShirshov/nextorm/issues/9) | Scalar-valued functions |
| [#14](https://github.com/AlexeyShirshov/nextorm/issues/14) | Table hints |
| [#17](https://github.com/AlexeyShirshov/nextorm/issues/17) | Benchmark with Dapper and EF |
| [#19](https://github.com/AlexeyShirshov/nextorm/issues/19) | Новые возможности SQL-генерации |
| [#21](https://github.com/AlexeyShirshov/nextorm/issues/21) | PostgreSQL support |
| [#22](https://github.com/AlexeyShirshov/nextorm/issues/22) | MySQL support |
| [#33](https://github.com/AlexeyShirshov/nextorm/issues/33) | SQL functions |
| [#49](https://github.com/AlexeyShirshov/nextorm/issues/49) | ClickHouse support |

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse`.

## 2. Предрелизные проверки

- [x] Все issues милстоуна закрыты; незакрытых блокеров из бэклога (`docs/specs/roadmap/todo_*.md`; индекс —
      `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4) для вошедшего функционала нет
      (остальное — осознанно за скоупом).
- [x] `dotnet build -c Release` и `dotnet build` (Debug) — без предупреждений (0 warning / 0 error,
      `TreatWarningsAsErrors=true`).
- [x] Unit / SQL-gen тесты: `dotnet test -c Release` по всем `tests/nextorm.<provider>.tests` —
      **1222 passed, 0 failed** (core 192, sqlite 268, sqlserver 238, postgres 247, mysql 65, mariadb 29,
      clickhouse 183).
- [x] Интеграционные тесты (Podman):
      `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Release -- -noColor`
      — **1016 passed, 0 failed, 30 skipped** (скипы — capability-based: TVF `json_each`,
      `INTERSECT ALL`/`EXCEPT ALL`, integer `AVG`, `FULL JOIN`).
- [x] Покрытие — **Line 85.4%** (Branch 74.5%, Method 72.6%) ≥ `MIN_LINE_COVERAGE` (75%); core 85.3%,
      postgres 88.8%, sqlite 80.3%, sqlserver 93.6%.
- [x] Docs EN + RU синхронны по составу (38 EN vs 39 RU guide/providers/advanced; RU-only:
      `motivation.md`, `overview.md` — намеренно). `dotnet docfx docs/docfx.json` — **0 errors, 46 warnings**:
      26 `InvalidFileLink` на `~/specs/**` (спеки исключены из сборки — by design), 16 `UidNotFound`
      (неоднозначные короткие xref: `WindowFrame.Groups`/`WithExclusion`, `IIifRenderer.Render`,
      `EntityBuilder`1.DistinctOn`, `percentile_cont`, …), 4 `InvalidBookmark` в
      `scalar-functions/index.md` (EN+RU зеркальны). Предупреждения не блокируют релиз, но xref/якоря
      стоит поправить (не сделано).
- [x] `nextorm-code-auditor`: **release-blocking P0/P1 нет**. Регистры обновлены
      (`docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`).
- [x] Бенчмарки (#17) воспроизводимы: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*InMemoryBenchmarkGroupBy*' --job short`
      — BenchmarkDotNet v0.15.8, прогнаны `Nextorm_GroupByCount`, `Linq_GroupByCount`,
      `EFCoreInMemory_GroupByCount`, без ошибок.

## 3. Изменения в репозитории

### 3.1. Бамб версий — готово

`<Version>` → `1.0.3-alpha` во всех пакетах:

| Файл | PackageId | Строка |
| --- | --- | --- |
| `src/nextorm.core/nextorm.core.csproj` | `nextorm` | 12 |
| `src/nextorm.sqlite/nextorm.sqlite.csproj` | `nextorm.sqlite` | 11 |
| `src/nextorm.sqlserver/nextorm.sqlserver.csproj` | `nextorm.sqlserver` | 20 |
| `src/nextorm.postgres/nextorm.postgres.csproj` | `nextorm.postgres` | 12 |
| `src/nextorm.mysql/nextorm.mysql.csproj` | `nextorm.mysql` | 12 |
| `src/nextorm.mariadb/nextorm.mariadb.csproj` | `nextorm.mariadb` | 12 |
| `src/nextorm.clickhouse/nextorm.clickhouse.csproj` | `nextorm.clickhouse` | 12 |

### 3.2. `docs/index.md` — готово

- `## Status`: `1.0.1-alpha` → `1.0.3-alpha`.
- `## Releases`: добавлены `### 1.0.3-alpha` (список из §1) и восстановленная `### 1.0.2-alpha`
  над `### 1.0.1-alpha`.
- `## Installation`: список провайдеров — все 6 (`sqlserver`, `sqlite`, `postgres`, `mysql`, `mariadb`,
  `clickhouse`); строка про встроенный in-memory уже есть.

### 3.3. Служебное — проверено

- Per-feature `docs/specs/roadmap/todo_*.md` — prose-спеки под открытые/заблокированные пункты;
  механически устаревших (полностью выполненных, но не удалённых) нет. Выполненный бэклог Phase 2 уже удалён,
  индекс Phase 3 (`todo_phase3.md`) актуален.
- `PackageReleaseNotes`/`PackageProjectUrl` в `nextorm.core.csproj` ведут на docs-сайт — ок.

## 4. Публикация — выполнено

Журнал:

1. Ветка `1.0.3-alpha` зелёная (coverage хардфейлит только на `main`).
2. Job `publish` перенесён в `dotnet.yml` ветки: `src/`-пути, все 7 пакетов, тег → версия,
   OIDC (`NuGet/login@v1`, секрет `NUGET_USER`). YAML провалидирован; локальный `dotnet pack` всех 7
   пакетов с `-p:Version=1.0.3-alpha` подтвердил зависимости (`mariadb → nextorm.mysql 1.0.3-alpha`,
   провайдеры → `nextorm 1.0.3-alpha`).
3. Бамп версий, `docs/index.md`, `dotnet.yml` закоммичены; ветка запушена.
4. `main` сведён с веткой через PR [#54](https://github.com/AlexeyShirshov/nextorm/pull/54)
   (merge `2b95d63`, конфликты — в пользу `src/`/7 пакетов/наших release notes).
5. Создан релиз `v1.0.3-alpha` (prerelease, target `main`) и заполнено описание:
   <https://github.com/AlexeyShirshov/nextorm/releases/tag/v1.0.3-alpha> (ссылка на
   `https://alexeyshirshov.github.io/nextorm/#103-alpha` + 7 пакетов + список issues/PR).
6. CI на теге: первый прогон (`35640599105`) отменён своим же `concurrency` (дубль tag-push);
   актуальный `35640740953` — **success**: build+coverage и `publish`
   (`Pack` → `NuGet login (OIDC)` → `Push to nuget.org`), все 7 пакетов ответили
   «Your package was pushed».
7. Docs-сайт: workflow `Docs` на `main` — success.

Итог: 7 пакетов отправлены. Индексация на nuget.org может занять от минут до часов
(4 пакета — `postgres`, `mysql`, `mariadb`, `clickhouse` — публикуются впервые; для
`nextorm`/`nextorm.sqlite`/`nextorm.sqlserver` версия `1.0.3-alpha` на момент записи ещё не появилась
в flat-container, для `sqlite`/`sqlserver`/`postgres`/`mysql` — уже видна).

## 5. Definition of Done

- [x] Версии всех 7 пакетов = `1.0.3-alpha`; `docs/index.md` обновлён (Status + Releases).
- [x] job `publish` перенесён под `src/` и 7 пакетов; CI на ветке/`main` зелёный.
- [x] Тег `v1.0.3-alpha` запушен; CI отправил 7 пакетов на nuget.org (индексация идёт).
- [x] GitHub Release (prerelease) с заполненным описанием.
- [x] Docs EN/RU обновлены и задеплоены; якорь `#103-alpha` доступен.

## 6. Открытые вопросы — решены

- **Интеграция ветки и `main`:** `main` влит в `1.0.3-alpha` (merge-коммит) и затем ветка через
  PR #54 влита в `main`. Rebase/cherry-pick не понадобились.
- **Охват `publish`:** публикуются все 7 пакетов (было 3).
- **Release notes:** остаются секцией `## Releases` в `docs/index.md` (1.0.3 / 1.0.2 / 1.0.1);
  отдельная `docs/releases.md` не нужна.

## 7. Что осталось (не блокирует релиз)

- Через несколько часов перепроверить, что все 7 пакетов `1.0.3-alpha` видны на nuget.org
  (после индексации), в т.ч. впервые публикуемые `nextorm.postgres/mysql/mariadb/clickhouse`.
- Почистить 20 docfx-предупреждений: 16 неоднозначных xref (указать полные сигнатуры) и 4 `InvalidBookmark`
  в `scalar-functions/index.md` (EN+RU).
- Рассмотреть фикс `concurrency` в `dotnet.yml`, чтобы двойной tag-push (`release` + `push`) не отменял
  первую публикацию (например, группировать publish отдельно или `cancel-in-progress: false` для тегов).
