# План релиза 1.0.9-rc5 (milestone 1.0.9-rc5)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-rc4.md`, `git show v1.0.9-rc4`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все пакеты), тег `v1.0.9-rc5` → tag-triggered
> CI-публикация пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполняется; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-rc5](https://github.com/AlexeyShirshov/nextorm/milestone/23) — 30 issues, все закрыты (open=0, closed=30): 26 `completed`, 4 `not_planned` (#246, #271, #366, #367).
- **Тег:** `v1.0.9-rc5` (планируется)
- **Ветка релиза:** `1.0.9-rc5` (синхронна с `origin/1.0.9-rc5`)
- **База:** тег `v1.0.9-rc4` (`a331aaed`); 301 коммит от `v1.0.9-rc4` до HEAD подготовки.
- **Версии пакетов:** `1.0.9-rc5` задаётся **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-rc5`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC).
- **Пакетов к публикации — 8:** `nextorm`, `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`,
  `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` и **новый `nextorm.extensions`**.
  `nextorm.entityframeworkcore` в публикацию по-прежнему не входит (решает владелец, §6).

## 1. Скоуп милстоуна

Реестр задач — release notes `docs/index.md` §`### 1.0.9-rc5` (30 issue-ссылок, порядок по номеру).
Крупные кластеры:

| Кластер | Issues |
| --- | --- |
| Публичные query-компоненты и `nextorm.extensions` | #362, #365, #367, #368 |
| NativeAOT | #366 (закрыт с остатком, продолжение — #370) |
| Производительность и аллокации | #245, #247, #250, #267–#274, #302, #311, #317–#319, #363, #368 |
| Корректность и стабильность | #352, #353, #359, #360, #361, #369 |
| Покрытие | #364 |

Принятые ограничения (закрыты как `not_planned`):

| # | Причина |
| --- | --- |
| [#246](https://github.com/AlexeyShirshov/nextorm/issues/246) | `ToDataReader` raw reader остаётся медленнее linq2db; дальнейший выигрыш требует смены модели владения reader'ом, принято как остаток |
| [#271](https://github.com/AlexeyShirshov/nextorm/issues/271) | Пример CH Retention всё ещё аллоцирует 3.02× linq2db; остаток принят вместе с остальными example-задачами |
| [#366](https://github.com/AlexeyShirshov/nextorm/issues/366) | NativeAOT parity не доведён до полной матрицы: измеренная граница — архитектура runtime expression/generic pipeline; остаток вынесен в [#370](https://github.com/AlexeyShirshov/nextorm/issues/370) |
| [#367](https://github.com/AlexeyShirshov/nextorm/issues/367) | Tracking-issue переноса `SelectGroup`; фактическая работа выполнена и принята в #362/#368 |

**Breaking changes (consumer-visible).** Пререлиз, совместимость между пререлизами не гарантируется:

- **#365** (`33655536`) — `SelectWhereMin`/`SelectWhereMax` теперь в пакете `nextorm.extensions`
  (четыре перегрузки, без `*Ex`-алиасов); встроенные методы в core переименованы в
  `SelectWhereMinOld`/`SelectWhereMaxOld` и запланированы к удалению. Приложению нужна ссылка на
  `nextorm.extensions`, иначе прежние вызовы не компилируются.
- **#362/#368** (`5bfe206e`) — `SelectGroup` удалён из core и реализован в `nextorm.extensions`
  вместе с новым `SelectGroupHaving`; core получил публичный декларативный механизм подготовки
  (`IQueryPreparationDescriptor`, `QueryPreparationContribution`, `ExclusiveGrouping`). In-memory
  для группирующих операторов явно не поддерживается (`NotSupportedException`). При явных generic-
  аргументах нужно указывать оба.
- **Новый пакет.** `nextorm.extensions` ранее не публиковался (на nuget.org отсутствует), поэтому
  job `publish` расширен с 7 до 8 пакетов — без этого релиз удаляет API, не поставляя замену.

Поведенческие изменения без изменения сигнатур: `OrderBy(p => new { … })` раскрывается в колонки
(#360), CTE-дескриптор из aliased-цепочки сохраняет `Alias.X` (#361), рукописные `ManyToOne` с
неверной арностью дают именную диагностику (#353), `group_array` по `Tuple` рендерит валидный тип
(#359), `xxh3`/`xxh32` на MariaDB 11.4 больше не рекламируются (#352), flaky-порядок `UNION ALL`
в тесте устранён (#369).

## 2. Релизные гейты — в пайплайне

Релизные проверки **не дублируются локально**: их выполняет CI на мердже в `main`, а публикация —
tag-triggered job `publish`. Локальный прогон (тесты, coverage, DocFX, pack) не является
приёмочным доказательством: он зависит от локального контейнерного рантайма и перезапусков
окружения агента, а CI — источник истины.

### 2.1. Локально (только для коммита)

- [x] `dotnet build nextorm.slnx -c Debug` — **0 warning / 0 error** (2m50s).
- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error** (3m12s) после отката раннего
      бампа версии (см. §3.1: ранний бамп давал `NU1605` в примерах).

Попытка прогнать решение локально была остановлена: контейнерный рантайм Podman не запущен, и
интеграционная сюита fail-closed падает на `Provider.IsAvailable` (`Could not start a SQL Server
test container`). Это ограничение окружения, а не дефект продукта; такие прогоны заменяет CI.

### 2.2. CI, job `build` (`dotnet.yml`, запускается на push в любую ветку) — выполняется на мердже

- [ ] `dotnet restore` + `dotnet build --no-restore`.
- [ ] Solution-wide `dotnet test` всех 16 модулей под `dotnet-coverage`
      (`--filter-not-trait Category=Diagnostic --max-parallel-test-modules 1`).
- [ ] Unit-гейт: line 85 % / branch 75 % (hard-fail на `main`).
- [ ] Integration-гейт: **оба** live-проекта (built-in + extensions), union через ReportGenerator,
      пороги 65 % / 75 % (hard-fail на `main`).
- [ ] Self-tests генератора benchmark-таблиц.

### 2.3. CI, workflow `Docs` (push в `main`, paths `docs/**`, `src/**`)

- [ ] DocFX EN+RU → GitHub Pages; якорь `#109-rc5` появляется автоматически после мерджа.

### 2.4. CI, job `publish` (тег `v*` или `workflow_dispatch`)

- [ ] `dotnet pack` **8** пакетов с `-p:Version=$TAG` (включая `nextorm.extensions`) → nuget.org
      через NuGet trusted publishing (OIDC).

### 2.5. Справочные измерения (не гейт)

- `tests/nextorm.integration.tests` — 8659 total, 8462 passed, 0 failed, 197 skipped.
- `tests/nextorm.extensions.integration.tests` — 130 total, 130 passed.
- Skip-budget: 197 = `totalSkipped` из pinned `tests/nextorm.integration.tests/skip-budget.json`;
  штатный `check-integration-skip-budget.py` не парсит формат summary текущего раннера
  (`skipped: N` вместо `Skipped: N`) — дефект парсера, §6.
- Integration coverage (оба live-проекта): line 81.1 % (67508/83214), branch 75.3 % (40227/53396);
  `nextorm.extensions` — 68.7 %.

## 3. Изменения в репозитории

### 3.1. Версия и публикация

- **`Directory.Build.props` — версия остаётся `1.0.9-rc4` до публикации.** Источник истины версии —
  тег: CI-публикация собирает пакеты с `-p:Version=<tag>`. Ранний бамп до `1.0.9-rc5` ломает
  solution-build: примеры потребляют опубликованный пакет `nextorm 1.0.9-rc4`, а `nextorm.extensions`
  подключён в них ProjectReference'ом, и NuGet видит `NU1605` (downgrade `1.0.9-rc5 → 1.0.9-rc4`).
  Пакета rc5 на nuget.org ещё нет, поэтому `NextOrmVersion` в примерах тоже нельзя поднять заранее.
  После публикации — отдельный коммит: `<Version>` → `1.0.9-rc5` и
  `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc5` (как и предписывает комментарий
  в этом файле).
- **`.github/workflows/dotnet.yml`:** в список `dotnet pack` job'а `publish` добавлен
  `src/nextorm.extensions/nextorm.extensions.csproj` (8 пакетов).

### 3.2. `docs/index.md`

- `## Status` — `1.0.9-rc4` → `1.0.9-rc5`.
- `## Releases` — добавлен раздел `### 1.0.9-rc5` (30 issues, breaking-строка, принятые ограничения);
  `### 1.0.9-rc4` и ниже не тронуты.

### 3.3. Coverage-гейт (уже в HEAD)

- Integration-гейт измеряет оба live-проекта и юнионит отчёты через ReportGenerator
  (`42486d67`); `dotnet-coverage merge` не используется — его cobertura-экспорт теряет ветки.
- `MIN_INTEGRATION_BRANCH_COVERAGE` = **75** (поднят #364 с 74); измерено 75.3 %.

### 3.4. Служебное

- **`docs/specs/release-1.0.9-rc5.md`** — этот план.
- **`NextormConfig.cs`** (`5e2dc343`) — `NEXTORM_BENCH_FULL=1` остаётся на in-process toolchain.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9-rc5` и запушить её.
- [ ] Влить `1.0.9-rc5` в `main` через PR. **Здесь и закрываются релизные гейты:** job `build`
      прогоняет build, все 16 тестовых модулей и оба coverage-гейта с hard-fail на `main`;
      workflow `Docs` деплоит документацию EN+RU.
- [ ] Дождаться зелёного run `build` на `main` и успешного деплоя `Docs` (якорь `#109-rc5`).
- [ ] Создать тег `v1.0.9-rc5` → CI job `publish` (OIDC) отправляет **8** пакетов на nuget.org,
      включая `nextorm.extensions`.
- [ ] Проверить run `publish`: 8 пакетов, ошибок нет.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-rc5> + 8 пакетов + issues #245–#369.
- [ ] Закрыть milestone 1.0.9-rc5 (его issues уже closed, open=0).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc5`
      и обновить дату/строку прогона в `examples/VERIFICATION.md`.

## 5. Definition of Done

- [ ] Версии всех 8 пакетов = `1.0.9-rc5`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.9-rc5` запушен; CI отправил 8 пакетов на nuget.org (включая `nextorm.extensions`).
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-rc5` доступен.
- [ ] Milestone 1.0.9-rc5 закрыт.

## 6. Открытые вопросы

- **`nextorm.extensions`** публикуется впервые; после тега проверить, что пакет появился на
  nuget.org и что `nextorm.core` 1.0.9-rc5 не содержит `SelectGroup`/`SelectWhereMin` (без `Old`).
- **`nextorm.entityframeworkcore`** — пакет собирается, но в job `publish` его нет; решает владелец.
- **Branch-гейт** держится на запасе 0.3 п.п. (75.3 против 75); расширение сюиты extensions подняло
  его с 74.7. Дальнейший рост покрытия — отдельная задача (#364 цель 80 % не достигнута: 75.6 %).
- **#366** закрыт с остатком; продолжение — #370 (compile-time query pipeline + native matrix).
- **#245** закрыт с принятым остатком (EXCEPT/INTERSECT 1.27–1.35× linq2db по аллокациям).
- **Skip-budget checker** не разбирает формат summary текущего тест-раннера; нужен фикс парсера
  (`Skipped: N` + `[SKIP]` → `skipped: N` + `skipped <test>`).
