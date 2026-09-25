# План релиза 1.0.5-alpha (milestone 1.0-a.5)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.4-a.md`, `git show v1.0.4-alpha`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.5-alpha` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка завершена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0-a.5](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.5) — 6 issues
  (#3–#6, #32, #60), все реализованы и закрыты (open=0, closed=6)
- **Тег:** `v1.0.5-alpha` (планируется)
- **Ветка релиза:** `1.0.5-alpha` → вливается в `main` через PR
- **Версии пакетов:** `1.0.5-alpha` задана **одной строкой** в `Directory.Build.props` (`<Version>`)
  на все проекты; CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный
  бамп нужен локальному `dotnet pack` и зависимостям nuspec
  (`nextorm.mariadb → nextorm.mysql 1.0.5-alpha`)
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.3/1.0.4

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#3](https://github.com/AlexeyShirshov/nextorm/issues/3) | INSERT: одиночная и многострочная вставка значений и сущностей, возврат сгенерированного ключа |
| [#4](https://github.com/AlexeyShirshov/nextorm/issues/4) | UPDATE: присваивания по колонкам, фильтр `WHERE` и обновление сущности по ключу |
| [#5](https://github.com/AlexeyShirshov/nextorm/issues/5) | DELETE: удаление по предикату и по объявленному ключу |
| [#6](https://github.com/AlexeyShirshov/nextorm/issues/6) | MERGE: key upsert (`ON CONFLICT` / `ON DUPLICATE KEY`) и полный `MERGE` с `WHEN MATCHED` / `WHEN NOT MATCHED` |
| [#32](https://github.com/AlexeyShirshov/nextorm/issues/32) | Транзакции: созданные nextorm и переданные извне (ADO.NET / EF Core) |
| [#60](https://github.com/AlexeyShirshov/nextorm/issues/60) | CREATE TABLE ... AS SELECT (CTAS) и временные таблицы |

Плюс объём, не привязанный к отдельному issue (в ветке `1.0.5-alpha`, коммиты `8c7e60c`…`ebd58c9`):

- **Массовая вставка (bulk):** `BulkInsertBuilder` / `BulkInsertOptions` / `BulkInsertReturningBuilder`,
  пакетная отправка (values / сущности / `DataTable`), `RETURNING` / `OUTPUT` где поддерживается;
- **CTE внутри изменяющих запросов** (`CteMerge`, `MutationCteQuery`), коррелированные подзапросы
  в in-memory провайдере;
- **Документация EN + RU:** 7 новых глав руководства (19 INSERT, 20 DELETE, 21 UPDATE, 22 CTAS,
  23 MERGE, 24 bulk insert, 25 transactions) в `docs/guide` и `docs/ru/guide`, регистрация в обоих
  `toc.yml` и в списке Guide на `docs/index.md`.

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).

### 1.1. Tooling

- Решение переведено на **`.slnx`**: `nextorm.sln` удалён, `nextorm.slnx` сгенерирован
  (`dotnet sln migrate`), живые ссылки в `.opencode/agents/**` и
  `.opencode/skills/implementing-todo-features/SKILL.md` обновлены. CI (`dotnet restore`/`dotnet build`
  без имени файла) подхватывает единственный `nextorm.slnx`.

## 2. Предрелизные проверки

- [x] Issues милстоуна (#3–#6, #32, #60) реализованы и закоммичены (HEAD `ebd58c9`); блокеров из
      бэклога (`docs/specs/roadmap/todo_*.md`) для вошедшего функционала нет (удалённые `todo_insert.md`,
      `todo_update.md`, `todo_delete.md`, `todo_merge.md`, `todo_transactions.md`, `todo_bulk_insert.md`,
      `todo_cte_in_update.md`, `todo_correlated_inmemory.md`).
- [x] `dotnet build nextorm.slnx -c Release` и `-c Debug` — **0 warning / 0 error**
      (`TreatWarningsAsErrors=true`; `CS1591` не подавляется).
- [x] Unit / SQL-gen тесты (Debug, `--no-build`) — **1861 passed, 0 failed, 0 skipped**:
      core 289, sqlite 371, sqlserver 328, postgres 398, mysql 129, mariadb 57, clickhouse 289.
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`)
      — **1509 total, 0 failed, 66 skipped**. Скипы capability-based (TVF `json_each`, `INTERSECT/EXCEPT ALL`,
      integer `AVG`, `FULL JOIN`, временные таблицы, `IgnoreDuplicates` там, где движок не умеет и т.п.).
- [x] Покрытие (CI-пайплайн, unit-прогон) — **Line 84.9% / Branch 74.5% / Method 74.9%** ≥
      `MIN_LINE_COVERAGE` (75%). По сборкам: core 85.1%, sqlite 85.9%, sqlserver 79.2%, postgres 76.6%
      (инструментируются только эти 4).
- [x] Docs EN + RU синхронны (по 7 новых глав в каждой); `dotnet docfx docs/docfx.json` — **0 errors, 0 warnings**.
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.5-alpha` — ок; зависимости в nuspec проверены:
      `nextorm.mariadb → nextorm.mysql 1.0.5-alpha`, провайдеры → `nextorm 1.0.5-alpha`.

## 3. Изменения в репозитории (поверх HEAD `ebd58c9`)

- **`Directory.Build.props`:** `<Version>1.0.4-alpha</Version>` → `1.0.5-alpha`.
- **`docs/index.md`:**
  - `## Status` — `1.0.5-alpha` (была строка без версии);
  - в список `### Guide` добавлены главы 19–25;
  - `## Releases` — добавлен раздел `### 1.0.5-alpha` (#3–#6, #32, #60 + строки про bulk и новые главы).
- **`docs/specs/release-1.0.5-a.md`** — этот план.
- **Tooling `.slnx`** (см. §1.1): `nextorm.sln` удалён, добавлен `nextorm.slnx`.

## 4. Публикация (шаги владельца)

- [ ] Убедиться, что ветка `1.0.5-alpha` зелёная (coverage хардфейлит только на `main`).
- [ ] Закоммитить подготовленный объём (бамп версии, `docs/index.md`, этот план, `.slnx`) и запушить
      ветку `1.0.5-alpha`.
- [ ] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.5-alpha` — проверить зависимости
      (`nextorm.mariadb → nextorm.mysql 1.0.5-alpha`, провайдеры → `nextorm 1.0.5-alpha`) — **уже проверено §2**.
- [ ] Свести `main` с веткой через PR.
- [ ] Создать тег `v1.0.5-alpha` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease, target `main`): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#105-alpha> + 7 пакетов + issues #3–#6, #32, #60.
- [ ] Убедиться, что milestone 1.0-a.5 закрыт (его issues уже closed).

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.5-alpha`; `docs/index.md` обновлён (Status + Releases + Guide).
- [ ] Тег `v1.0.5-alpha` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#105-alpha` доступен.
- [ ] Milestone 1.0-a.5 и issues #3–#6, #32, #60 закрыты.

## 6. Открытые вопросы

- **Охват `publish`:** все 7 пакетов (без изменений с 1.0.3/1.0.4).
- **Release notes:** остаются секцией `## Releases` в `docs/index.md`; отдельный `docs/releases.md` не нужен.
- **Фикс `concurrency` для tag-push** (кандидат с 1.0.3): не блокирует.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.
- **`nextorm-code-auditor`** в этом цикле не перезапускался; регистры
  (`docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`) обновлялись
  в feature-коммитах.

## 7. Что осталось (не блокирует релиз)

- После публикации перепроверить, что все 7 пакетов `1.0.5-alpha` видны на nuget.org (индексация — от минут до часов).
- `todo_clickhouse_aggregate_function_state.md` (§4.6) — заблокирован драйвером `ClickHouse.Driver` 1.4.0.
- Открытые `todo_*.md` следующего скоупа: `todo_query_filters.md`,
  `todo_efcore_integration.md`, `todo_json_*`, `todo_sharding.md` и др.
