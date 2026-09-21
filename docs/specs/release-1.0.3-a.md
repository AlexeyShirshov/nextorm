# План релиза 1.0.3-alpha (milestone 1.0-a.3)

> Основа — релизы предыдущих милстоунов (см. `git show v1.0.2-alpha`: `release 1.0.2-alpha`).
> Прошлый релиз состоял ровно из: списка release notes в `docs/index.md`, строки `Status` и бампа
> `<Version>` в `.csproj` затронутых пакетов. Здесь то же ядро плюс проверки, которые накопились
> к текущему состоянию репозитория (7 пакетов, интеграционные тесты, порог покрытия, docs EN/RU).

- **Milestone:** [1.0-a.3](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.3) — 9 issues, все закрыты
- **Тег:** `v1.0.3-alpha`
- **Версии пакетов:** `1.0.3-alpha` для всех пакетов (сейчас в репозитории стоит `1.0.1-alpha`)
- **Публикация:** вручную (`dotnet pack` + `dotnet nuget push`); автоматизации релиза в CI нет

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

- [ ] Все issues милстоуна закрыты; незакрытых блокеров из бэклога (`docs/specs/roadmap/todo_*.md`; индекс —
      `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4) для вошедшего функционала нет
      (остальное — осознанно за скоупом).
- [ ] `dotnet build -c Release` — без предупреждений (везде `TreatWarningsAsErrors=true`).
- [ ] Unit / SQL-gen тесты: `dotnet test -c Release` по всем `tests/nextorm.<provider>.tests`.
- [ ] Интеграционные тесты (Podman):
      `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Release -- -noColor`
      (без `DOCKER_HOST` идут только SQLite-тесты — для релиза запускать полный набор).
- [ ] Покрытие ≥ `MIN_LINE_COVERAGE` (75%) — прогнать `dotnet-coverage` + `reportgenerator` локально
      по `.github/workflows/dotnet.yml` или дождаться зелёного CI на `main`.
- [ ] Docs EN + RU синхронны: новые страницы/переименования отражены в `docs/**` и `docs/ru/**`
      (`docs/index.md`, `docs/ru/index.md`, `docs/toc.yml`, `docs/ru/toc.yml`).
- [ ] `nextorm-code-auditor`: нет незакрытых P0/P1, регистры
      `docs/specs/design/code-smells-review.md` и `docs/specs/design/API-NAMING-REVIEW.md` актуальны.
- [ ] Бенчмарки (#17) воспроизводимы:
      `dotnet run --project benchmarks/nextorm.benchmark -c Release`.

## 3. Изменения в репозитории (коммит `release 1.0.3-alpha`)

### 3.1. Бамб версий

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
- `## Releases`: добавить секцию `### 1.0.3-alpha` (список из §1) над `### 1.0.2-alpha`.
- **Проверить:** секция `### 1.0.2-alpha` в текущем `docs/index.md` отсутствует (потеряна при
  реорганизации решения после релиза). Восстановить её перед 1.0.3 или явно подтвердить, что сайт
  теперь публикует release notes только из GitHub-релизов.
- `## Installation`: список провайдеров — все 6 (`sqlserver`, `sqlite`, `postgres`, `mysql`, `mariadb`,
  `clickhouse`); строка про встроенный in-memory уже есть.

### 3.3. Служебное

- Убедиться, что per-feature `docs/specs/roadmap/todo_*.md` закрытых пунктов актуальны (выполненное либо
  удалено, либо помечено как сделанное).
- Проверить `PackageReleaseNotes`/`PackageProjectUrl` в `nextorm.core.csproj` (ведут на docs-сайт).

## 4. Публикация

1. Смерджить всё в `main`, дождаться зелёного CI на `main` (там порог покрытия хардфейлит).
2. Закоммитить бамп версий и `docs/index.md`: сообщение `release 1.0.3-alpha` (стиль прошлого релиза).
3. `dotnet pack -c Release` по каждому пакету (или `dotnet pack src/nextorm.core` и т.д.);
   артефакты появятся в `bin/linux/Release/`.
4. `dotnet nuget push bin/linux/Release/<package>.1.0.3-alpha.nupkg --source https://api.nuget.org/v3/index.json --api-key <key>`
   для всех 7 пакетов.
5. Проставить тег и запушить (делает владелец вручную):
   `git tag v1.0.3-alpha && git push origin main --tags`.
6. GitHub Release: `gh release create v1.0.3-alpha --prerelease --title 1.0.3-alpha --notes-file <file>`,
   в теле — ссылка на release notes `https://alexeyshirshov.github.io/nextorm/#103-alpha` и список
   пакетов (`dotnet add package nextorm`, `...sqlite`, `...sqlserver`, ...).
7. Убедиться, что docs-сайт задеплоился (`docs.yml`) и якорь `#103-alpha` резолвится.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.3-alpha`; `docs/index.md` обновлён (Status + Releases).
- [ ] CI на `main` зелёный, покрытие ≥ 75%, интеграционные тесты всех провайдеров прошли.
- [ ] 7 пакетов запушены на nuget.org и видны в списке пререлизов.
- [ ] Тег `v1.0.3-alpha` + GitHub Release (prerelease) с notes.
- [ ] Docs EN/RU обновлены и задеплоены; якорь `#103-alpha` работает.

## Открытые вопросы

- Куда класть эту версию release notes на сайте: держать `## Releases` в `docs/index.md` (как в 1.0.1/1.0.2)
  или завести отдельную страницу `docs/releases.md` и чинить потерянную секцию 1.0.2?
- Нужен ли отдельный релизный workflow (tag → pack → nuget push → gh release) вместо ручной процедуры?
