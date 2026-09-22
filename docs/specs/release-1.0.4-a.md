# План релиза 1.0.4-alpha (milestone 1.0-a.4)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.3-a.md`, `git show v1.0.3-alpha`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в 7 пакетах,
> тег `v1.0.4-alpha` → tag-triggered CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка завершена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0-a.4](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.4) — 4 issues (#55–#58), все реализованы; закрыть при публикации
- **Тег:** `v1.0.4-alpha` (планируется)
- **Ветка релиза:** `1.0.4-alpha` → вливается в `main` через PR
- **Версии пакетов:** `1.0.4-alpha` задана **одной строкой** в `Directory.Build.props` (`<Version>`) на все проекты;
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.4-alpha`)
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted publishing (OIDC) — без изменений с 1.0.3

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#55](https://github.com/AlexeyShirshov/nextorm/issues/55) | ClickHouse: закрыт остаток backlog — UInt64 row reader, серверные/кластерные TVF, нативный JSON |
| [#56](https://github.com/AlexeyShirshov/nextorm/issues/56) | ClickHouse: массивы Array(T)/Tuple — row reader, array-агрегаты и higher-order (lambda) функции |
| [#57](https://github.com/AlexeyShirshov/nextorm/issues/57) | ClickHouse: join kinds SEMI/ANTI/PASTE — `JoinType.Semi/Anti/Paste`, `SemiJoin`/`AntiJoin`/`PasteJoin` |
| [#58](https://github.com/AlexeyShirshov/nextorm/issues/58) | Типизированный доступ к колонке по имени — `SqlFunctions.Column<T>` |

Плюс объём, не привязанный к отдельному issue (в рабочем дереве): **полная XML-документация публичного API
во всех 7 пакетах** — снят `<NoWarn>CS1591</NoWarn>`, `GenerateDocumentationFile` теперь под
`TreatWarningsAsErrors=true`; правки docs EN/RU (корректные xref на `EntityBuilderExtensions.*` и
`QueryCommand<T>.ExecuteScalar*`, удалены публичные ссылки на `docs/specs/**`).

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).

## 2. Предрелизные проверки

- [x] Issues милстоуна (#55–#58) реализованы и закоммичены (HEAD `91379b1`); блокеров из бэклога
      (`docs/specs/roadmap/todo_*.md`) для вошедшего функционала нет.
- [x] `dotnet build nextorm.sln -c Release` и `-c Debug` — **0 warning / 0 error**
      (`TreatWarningsAsErrors=true`; `CS1591` больше не подавляется).
- [x] Unit / SQL-gen тесты (Debug) — **1339 passed, 0 failed, 0 skipped**: core 198, sqlite 273,
      sqlserver 246, postgres 273, mysql 71, mariadb 31, clickhouse 247.
- [x] Интеграционные тесты (Podman, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`)
      — **1052 total, 0 failed, 30 skipped**. Скипы capability-based: SQLite `json_each`-TVF,
      `INTERSECT ALL`/`EXCEPT ALL`, integer `AVG`, `FULL JOIN`, ClickHouse-кардинальность скалярных подзапросов.
- [x] Покрытие — **Line 85.5% / Branch 74.5% / Method 72.4%** ≥ `MIN_LINE_COVERAGE` (75%).
      По сборкам: core 85.4%, postgres 85.7%, sqlite 80.4%, sqlserver 93.7% (инструментируются только эти 4).
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **0 errors, 0 warnings**
      (в 1.0.3 было 46/55 — все починены правками xref/ссылок).
- [x] `nextorm-code-auditor` (22.09.2026, HEAD `91379b1` + uncommitted): **release-blocking P0/P1 нет**.
      Подавления: 6 `SuppressMessage` + 5 `#pragma` = **11/11 оправданных**, `Skip=` 0, пустых `catch` 0;
      `CS1591` снят во всех 7 `.csproj`; XML-doc покрытие публичного API — 100%. Регистры обновлены
      (`docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`).
- [x] Бенчмарки: `SqliteBenchmarkColumnByName` (#58) — build-путь ускорен (**1.42× → 1.11×**);
      before/after DefaultJob по `SqliteBenchmarkWhere`, `SqliteBenchmarkCachedPlan`,
      `SqliteBenchmarkFeaturesFair` — общей деградации нет.

## 3. Изменения в репозитории

### 3.1. Бамп версий — готово

Версия вынесена в **одну точку** — `Directory.Build.props`:

```xml
<PropertyGroup>
  <Version>1.0.4-alpha</Version>
</PropertyGroup>
```

Дублирующие `<Version>` из всех 7 `src/*.csproj` удалены. Проверено: `-getProperty:Version` даёт
`1.0.4-alpha` и для `src/*`, и для `tests/*`; `dotnet pack` даёт `nextorm.1.0.4-alpha.nupkg` и
`nextorm.mariadb` с зависимостью `nextorm.mysql 1.0.4-alpha`; `-p:Version=<тег>` по-прежнему переопределяет
(CI-путь не сломан).

### 3.2. `docs/index.md` — готово

- `## Status`: `1.0.3-alpha` → `1.0.4-alpha`.
- `## Releases`: добавлена `### 1.0.4-alpha` (#55–#58 + строка про XML-документацию публичного API).
- Ссылки на `docs/specs/**` из публичных разделов убраны (спеки — внутренние, вне DocFX).

### 3.3. Объём для коммита

Рабочее дерево поверх `91379b1` содержит ~193 файла (XML-доки публичного API во всех 7 пакетах,
снятие `NoWarn CS1591`, правки docs EN/RU, регистры аудита, бамп версий, `docs/index.md`, этот план).
Коммит и push делает владелец (подтверждено).

## 4. Публикация (шаги владельца)

- [ ] Убедиться, что ветка `1.0.4-alpha` зелёная (coverage хардфейлит только на `main`).
- [ ] Закоммитить подготовленный объём (XML-доки, 7 csproj, `docs/index.md`, оба регистра, этот план)
      и запушить ветку `1.0.4-alpha`.
- [ ] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.4-alpha` — проверить зависимости
      (`nextorm.mariadb → nextorm.mysql 1.0.4-alpha`, провайдеры → `nextorm 1.0.4-alpha`).
- [ ] Свести `main` с веткой через PR.
- [ ] Создать тег `v1.0.4-alpha` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease, target `main`): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#104-alpha> + 7 пакетов + issues #55–#58.
- [ ] Закрыть issues #55–#58 и milestone 1.0-a.4.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.4-alpha`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.4-alpha` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#104-alpha` доступен.
- [ ] Issues #55–#58 и milestone 1.0-a.4 закрыты.

## 6. Открытые вопросы

- **Охват `publish`:** все 7 пакетов (без изменений с 1.0.3).
- **Release notes:** остаются секцией `## Releases` в `docs/index.md`; отдельный `docs/releases.md` не нужен.
- **Фикс `concurrency` для tag-push** (в 1.0.3 двойной tag-push отменял первую публикацию): остаётся
  кандидатом, не блокирует.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа.

## 7. Что осталось (не блокирует релиз)

- После публикации перепроверить, что все 7 пакетов `1.0.4-alpha` видны на nuget.org (индексация — от минут до часов).
- `todo_clickhouse_aggregate_function_state.md` (§4.6) — заблокирован драйвером `ClickHouse.Driver` 1.4.0.
- Заморозка публичного API (issue #53).
