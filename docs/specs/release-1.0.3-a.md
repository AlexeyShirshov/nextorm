# План релиза 1.0.3-alpha (milestone 1.0-a.3)

> Основа — релизы предыдущих милстоунов (см. `git show v1.0.2-alpha`: `release 1.0.2-alpha`).
> Прошлый релиз состоял ровно из: списка release notes в `docs/index.md`, строки `Status` и бампа
> `<Version>` в `.csproj` затронутых пакетов. Здесь то же ядро плюс проверки, которые накопились
> к текущему состоянию репозитория (7 пакетов, интеграционные тесты, порог покрытия, docs EN/RU).

- **Milestone:** [1.0-a.3](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.3) — 9 issues, все закрыты
- **Тег:** `v1.0.3-alpha`
- **Ветка релиза:** `1.0.3-alpha` (`2a2dfa6`, отслеживает `origin/1.0.3-alpha`), 49 коммитов впереди `main`
- **Версии пакетов:** `1.0.3-alpha` (в `src/*.csproj` сейчас `1.0.1-alpha`); CI всё равно переопределяет
  версию тегом через `-p:Version`, поэтому бамп — подстраховка для локального `dotnet pack`, не источник истины
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
      `guide/11-scalar-functions.md` (EN+RU зеркальны). Предупреждения не блокируют релиз, но xref/якоря
      стоит поправить.
- [x] `nextorm-code-auditor`: **release-blocking P0/P1 нет**. Регистры обновлены
      (`docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`).
- [ ] Бенчмарки (#17) воспроизводимы:
      `dotnet run --project benchmarks/nextorm.benchmark -c Release`.

## 3. Изменения в репозитории (коммит `release 1.0.3-alpha`)

### 3.1. Бамб версий (подстраховка; CI переопределит тегом)

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

### 3.2. `docs/index.md`

- `## Status`: `1.0.1-alpha` → `1.0.3-alpha`.
- `## Releases`: добавить `### 1.0.3-alpha` (список из §1) над `### 1.0.2-alpha`.
- **Проверить:** секция `### 1.0.2-alpha` в ветке отсутствует (живёт только в `main`, `59470de`).
  Либо восстановить её здесь, либо подтвердить, что сайт публикует release notes только из GitHub-релизов.
- `## Installation`: список провайдеров — все 6 (`sqlserver`, `sqlite`, `postgres`, `mysql`, `mariadb`,
  `clickhouse`); строка про встроенный in-memory уже есть.

### 3.3. Служебное

- Убедиться, что per-feature `docs/specs/roadmap/todo_*.md` закрытых пунктов актуальны (выполненное либо
  удалено, либо помечено как сделанное).
- Проверить `PackageReleaseNotes`/`PackageProjectUrl` в `nextorm.core.csproj` (ведут на docs-сайт).

## 4. Публикация

**Предусловие (блокер):** рабочая ветка `1.0.3-alpha` содержит новый layout (`src/`, `tests/`) и новый
`dotnet.yml` (coverage, actions v7), но **в ней нет job `publish`**. Job живёт только в `main` (`bd903d3`)
и написан под старый плоский layout (`nextorm.core/...`) и 3 пакета. Нужно перенести/переписать его в
ветку под `src/` и все 7 пакетов, сохранив coverage-часть.

**Интеграция `main`:** `main` (= `1.0.2-alpha` + publish CI) и `1.0.3-alpha` разошлись: у `main` старый
плоский layout, у ветки — реорганизация. `main` **не** является предком ветки (6 коммитов не входят).
Способ сведения нужно выбрать (см. «Открытые вопросы»); мерджи не делать без явного указания.

Шаги:

1. Дождаться зелёного CI на ветке `1.0.3-alpha` (coverage хардфейлит только на `main`).
2. Портировать job `publish` в `dotnet.yml` ветки: `src/`-пути, все 7 пакетов, тег → версия,
   OIDC (`NuGet/login@v1`, секрет `NUGET_USER`, trusted publishing настроен на nuget.org).
3. Закоммитить бамп версий (§3.1), `docs/index.md` (§3.2) и `dotnet.yml`: сообщение `release 1.0.3-alpha`.
4. Проставить тег и запушить (делает владелец вручную; `git push` в этой сессии не выполняется):
   `git tag v1.0.3-alpha && git push origin v1.0.3-alpha`.
   CI job `publish` соберёт и отправит пакеты в nuget.org.
5. GitHub Release: `gh release create v1.0.3-alpha --prerelease --title 1.0.3-alpha --notes-file <file>`,
   в теле — ссылка на release notes `https://alexeyshirshov.github.io/nextorm/#103-alpha` и список
   пакетов (`dotnet add package nextorm`, `...sqlite`, `...sqlserver`, `...postgres`, `...mysql`,
   `...mariadb`, `...clickhouse`).
6. Убедиться, что docs-сайт задеплоился (`docs.yml`) и якорь `#103-alpha` резолвится.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.3-alpha`; `docs/index.md` обновлён (Status + Releases).
- [ ] job `publish` перенесён под `src/` и 7 пакетов; CI на ветке зелёный.
- [ ] Тег `v1.0.3-alpha` запушен; CI опубликовал 7 пакетов на nuget.org (видны в пререлизах).
- [ ] GitHub Release (prerelease) с notes.
- [ ] Docs EN/RU обновлены и задеплоены; якорь `#103-alpha` работает.

## 6. Открытые вопросы

- **Интеграция ветки и `main`.** Варианты: (a) смерджить `main` в `1.0.3-alpha` (нужно явное разрешение —
  будет merge-коммит и конфликты layout), (b) cherry-pick только релизного коммита 1.0.2 + publish-части
  `dotnet.yml`, (c) позже свести `1.0.3-alpha` в `main` как новый layout и там доработать publish.
- Паковать ли в CI все 7 пакетов (сейчас job знает только core/sqlite/sqlserver).
- Куда класть release notes: держать `## Releases` в `docs/index.md` или завести `docs/releases.md` и
  починить потерянную секцию 1.0.2.
