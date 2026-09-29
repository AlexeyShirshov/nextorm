# План релиза 1.0.9-a (milestone 1.0.9-a)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.8-b.md`, `git show v1.0.8-b`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.9-a` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполнена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-a](https://github.com/AlexeyShirshov/nextorm/milestones/1.0.9-a) — 17 issues
  (#40, #52, #61, #67, #76, #94, #95, #100–#102, #104–#110), все реализованы и закрыты (open=0, closed=17).
- **Тег:** `v1.0.9-a` (планируется)
- **Ветка релиза:** `1.0.9-a` (синхронна с `origin/1.0.9-a`)
- **База:** тег `v1.0.8-b`; HEAD до подготовки — `f113f6e` (`documentation`), 23 коммита от `v1.0.8-b`
- **Версии пакетов:** `1.0.9-a` задана **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-a`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.8.

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#40](https://github.com/AlexeyShirshov/nextorm/issues/40) | Child collections (перекрыт #105) |
| [#52](https://github.com/AlexeyShirshov/nextorm/issues/52) | Navigation properties (переведён в #105) |
| [#61](https://github.com/AlexeyShirshov/nextorm/issues/61) | Интеграция с EF Core (`nextorm.entityframeworkcore`) |
| [#67](https://github.com/AlexeyShirshov/nextorm/issues/67) | Глобальные фильтры запросов (query filters) |
| [#76](https://github.com/AlexeyShirshov/nextorm/issues/76) | Проекция join в пользовательский тип (`As`) и снятие потолка арности |
| [#94](https://github.com/AlexeyShirshov/nextorm/issues/94) | Динамические колонки (dynamic columns store) |
| [#95](https://github.com/AlexeyShirshov/nextorm/issues/95) | Eager loading графа (`LoadWith`/`Include`) |
| [#100](https://github.com/AlexeyShirshov/nextorm/issues/100) | Server-side LOB chunking (MySQL/MariaDB, чанковые `GetBytes`/`GetChars`) |
| [#101](https://github.com/AlexeyShirshov/nextorm/issues/101) | LOB streaming — `TableAlias`-аксессоры и фаза 3 (проекция строкового стрима) |
| [#102](https://github.com/AlexeyShirshov/nextorm/issues/102) | Глава 24 — диапазон MySQL/MariaDB `TIME` в «Детали и ограничения» |
| [#104](https://github.com/AlexeyShirshov/nextorm/issues/104) | Dynamic columns — write side (INSERT/UPDATE/MERGE рендерят ключи словаря) |
| [#105](https://github.com/AlexeyShirshov/nextorm/issues/105) | Navigation properties / relationships (implicit joins) |
| [#106](https://github.com/AlexeyShirshov/nextorm/issues/106) | EF Core — shared-transaction integration tests (PostgreSQL / SQL Server / MySQL) |
| [#107](https://github.com/AlexeyShirshov/nextorm/issues/107) | Fix LoadWith: round-trip reduction + ignored terminals (split vs single-query) |
| [#108](https://github.com/AlexeyShirshov/nextorm/issues/108) | Global query filters — Фаза 2 (keyed, selective `IgnoreFilters`, DML + INSERT-валидация, `FilterFunc`) |
| [#109](https://github.com/AlexeyShirshov/nextorm/issues/109) | Dynamic columns — value-type (struct) entity и отсутствие parameterless ctor |
| [#110](https://github.com/AlexeyShirshov/nextorm/issues/110) | Dynamic columns — MySQL/MariaDB read: qualified star (`<mapped>, *`) |

Плюс новые страницы документации EN + RU: `advanced/relationships.md`, `advanced/eager-loading.md`,
`advanced/query-filters.md`, `advanced/integration-efcore.md`, `infrastructure/05-logging.md`,
глава `guide/27-dynamic-columns.md`; перенумерация глав руководства (13–27).

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).
Пакет `nextorm.entityframeworkcore` в публикацию **не входит** (не в списке job `publish`).

## 2. Предрелизные проверки

- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error** (38 s); `-c Debug` — **0 warning / 0 error** (25 s).
- [x] Unit / SQL-gen тесты (Debug, `--no-build`, xunit v3) — **3437 total / 0 failed / 1 skipped**:
      core 884, sqlite 707 (1 skip — probe `SqliteRowIdLobProbeTests`), sqlserver 481, postgres 567,
      mysql 214, mariadb 120, clickhouse 404, entityframeworkcore 60.
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) —
      **Total 2494, Errors 0, Failed 0, Skipped 187** (скипы capability-based); контейнеры реально стартовали
      (Testcontainers: 4 reusable + запуск — PostgreSQL `pg_isready`, SQL Server `sqlcmd`, MySQL `innodb_initialized`,
      ClickHouse; + SQLite без контейнера).
- [x] Покрытие (репро CI: `dotnet-coverage collect` + `reportgenerator`) — **Line 84.9% / Branch 76.4% / Method 75.6%**;
      `collect` exit 0, total 5931, failed 0, skipped 1969. Per-assembly: core 85.1, sqlite 89.2, sqlserver 77.2,
      postgres 76.6. Branch ≥ `MIN_BRANCH_COVERAGE` (75); Line 84.9 — на 0.1 п.п. ниже `MIN_LINE_COVERAGE` (85):
      на ветке это **warning** (hard-fail только на `main`), публикацию не блокирует (см. §6).
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **0 warning / 0 error**.
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.9-a` — ок; nuspec проверен:
      `nextorm.mariadb → nextorm.mysql 1.0.9-a`, провайдеры → `nextorm 1.0.9-a`, все пакеты `1.0.9-a`.
- [ ] `nextorm-code-auditor` — release-blocking P0/P1 нет; регистры обновлены (см. §6).

## 3. Изменения в репозитории (поверх HEAD `f113f6e`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.8-b</Version>` → `1.0.9-a`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.8-b` → `1.0.9-a`.
- `### Advanced` — добавлены 4 новые страницы: глобальные фильтры запросов, интеграция с EF Core,
  eager loading, связи (порядок как в `docs/advanced/toc.yml`).
- `## Releases` — добавлен раздел `### 1.0.9-a` (issues #40–#110 + строки про новые страницы и перенумерацию).

### 3.3. `docs/ru/index.md`

- `### Продвинутое` — добавлены те же 4 страницы (RU-заголовки из `docs/ru/advanced/*.md`).

### 3.4. Служебное

- **`docs/specs/release-1.0.9-a.md`** — этот план.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9-a` и запушить её.
- [ ] Убедиться, что ветка `1.0.9-a` зелёная (coverage хардфейлит только на `main`).
- [ ] (Опционально) влить `1.0.9-a` в `main` через PR — как для 1.0.6-alpha.
- [ ] Создать тег `v1.0.9-a` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-a> + 7 пакетов + issues #40–#110.
- [ ] Убедиться, что milestone 1.0.9-a закрыт (его issues уже closed).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-a`.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.9-a`; `docs/index.md` обновлён (Status + Advanced + Releases).
- [ ] Тег `v1.0.9-a` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-a` доступен.
- [ ] Milestone 1.0.9-a и issues #40–#110 закрыты.

## 6. Открытые вопросы

- **Покрытие:** Line 84.9% — на 0.1 п.п. ниже `MIN_LINE_COVERAGE` (85). На ветке только warning
  (порог жёстко применяется на `main`), публикацию не блокирует; для 1.0.8-b было 84.2% по той же логике.
  Решение владельца: принять как есть или добить тестами (свежие непокрытые пути — флейки/ветки
  `nextorm.entityframeworkcore`, `nextorm.postgres`, `nextorm.sqlserver`).
- **`nextorm.entityframeworkcore`** — есть пакет-проект и тесты, но в job `publish` (список из 7) его нет;
  вне скоупа 1.0.9-a, решает владелец.
- **`examples/Directory.Packages.props`** — `NextOrmVersion` = `1.0.8-b`; бампать после публикации (§4).
- **Опечатка** в `docs/index.md` (`## Status`): «is a prof of concept» — оставлена как есть (не в скоупе).
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.
- **Доки-спеки этой сессии** (`docs/specs/comparison/*`, `docs/specs/roadmap/*`, `docs/specs/design/*`) —
  внутренние, в release notes не попадают.
