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

### 2.2. CI, job `build` (`dotnet.yml`, запускается на push в любую ветку)

- [x] `dotnet restore` + `dotnet build --no-restore`.
- [x] Solution-wide `dotnet test` всех 16 модулей под `dotnet-coverage`
      (`--filter-not-trait Category=Diagnostic --max-parallel-test-modules 1`).
- [x] Unit-гейт: **line 89.2 % / branch 83.7 %** при порогах 85 / 75.
- [x] Integration-гейт: **оба** live-проекта (built-in + extensions), union через ReportGenerator,
      **line 81.1 % / branch 75.3 %** при порогах 65 / 75.
- [x] Self-tests генератора benchmark-таблиц.

Подтверждено run [`42400977470`](https://github.com/AlexeyShirshov/nextorm/actions/runs/42400977470)
(ветка `1.0.9-rc5`, commit `f8b95a9e`) — success, 11m57s, `Coverage OK` + `Integration coverage OK`.
На ветке гейты дают warning при недоборе, hard-fail — только на `main`; это и есть проверка мерджа.

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
- Запас integration-гейта по ветвям — **0.3 п.п.** (75.3 против 75).

### 2.6. Флейк, поймавшийся на гейте (исправлен)

CI run [`42398374420`](https://github.com/AlexeyShirshov/nextorm/actions/runs/42398374420) упал на
`nextorm.mysql.tests`: `QueryFilterSqlGenerationTests.FuncFilter_CachedPlan_SecondExecution_ShouldBindChangedValue`
— `ReferenceEquals(first, second)` ложно. Это известное семейство [#344](https://github.com/AlexeyShirshov/nextorm/issues/344):
поколение `QueryPlanStore` процессное, а кэш потоковый, поэтому `PurgeQueryCache()` на другом потоке
превращает тёплый hit в miss. Причина не в релизном коммите (продуктовый код не менялся; предыдущий
прогон той же кодовой базы был зелёным).

Прошлый проход сериализации (`e644ac5c`, `8ddda541`) покрыл postgres-копию этого класса, но пропустил
копии в mariadb, mysql, sqlserver и clickhouse. Все четыре добавлены в
`[Collection("DataContextCache clear")]` (`DisableParallelization = true`) — тот же приём, что уже
применён к остальным классам, утверждающим идентичность плана (**`f8b95a9e`**). Проверено локально:
полные сборки mariadb/mysql/sqlserver/clickhouse зелёные (`-parallel all`), а повторный CI-прогон
[`42400977470`](https://github.com/AlexeyShirshov/nextorm/actions/runs/42400977470) — success.
Новое доказательство добавлено в [#344](https://github.com/AlexeyShirshov/nextorm/issues/344#issuecomment-6711272432).

### 2.7. CI, job `aot` (новый гейт NativeAOT)

- [x] `dotnet publish tests/nextorm.aot.runner -c Release -r linux-x64` (native-тулчейн
      `clang` + `zlib1g-dev`).
- [x] `AotNativeRunTests` с `NEXTORM_AOT_EVIDENCE=1`: отсутствие бинарника — падение, а не skip.
- [x] Job `publish` теперь зависит от `build` **и** `aot` — тег не публикует пакеты без зелёного
      нативного прогона.
- [x] Локальная проверка 08.11.2026: publish rc=0, harness `1/1 passed` (4.9 s); прогон
      выполняется без контейнеров (SQLite + ленивый `DatabaseContainers`).
- Покрытие job'а сознательно частичное и описано в `docs/infrastructure/05-nativeaot.md`;
  расширение матрицы — Stream B [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

### 2.8. Сравнение на реальном кейсе (`oc-report`, NativeAOT)

`oc-report` (утилита отчётов по `opencode.db` на 13 ГБ) переписан в трёх вариантах и опубликован
с NativeAOT на одной машине. Окно 3 дня, `agent=pdca-executor`, тёплый page cache, медиана трёх
прогонов; вывод сверен с Python-двойником `oc-report.py` (3 дня и 7 дней `--all`) — побайтно
идентичен у всех шести прогонов.

| Вариант | JIT | AOT | Нативный бинарник | IL-предупреждения |
| --- | --- | --- | --- | --- |
| nextorm (типизированные запросы) | 0.42 s / 70.8 MB | **0.12 s / 30.2 MB** | 8.2 MB | 158 unique |
| EF Core 10.0.14 (LINQ + compiled model + precompiled queries) | 1.03 s / 89.7 MB | 0.23 s / 41.9 MB | 17.3 MB | 565 unique |
| Dapper 2.1.89 + Dapper.AOT 1.1.0 (ручной SQL) | 0.26 s / 51.5 MB | **0.12 s / 19.0 MB** | 3.5 MB | 0 |

Что выяснилось:

- **nextorm** — единственный вариант, которому для AOT не понадобилось ни одной правки исходника:
  в csproj добавлен только `PublishAot=true`. Это подтверждает заявленный zero-configuration
  контракт для данного среза (SQLite, три запроса, включая `json_extract`).
- **EF Core** — `PublishAot=true` в том же проекте ломает даже JIT-запуск
  (`Model building is not supported when publishing with NativeAOT`, нужен compiled model).
  Сам publish упал на багах кодогенерации 10.0.14: (1) сущность в CLR-пространстве имён даёт
  невалидный идентификатор `var ocReport.Ef.SessionRowPrimaryKeyProperties`; (2) `sealed`-сущность
  даёт `CS0030` на касте к `IInjectableService`. После переноса сущностей в global namespace и
  снятия `sealed` publish проходит. Плюс `CS9270`: EF 10.0.14 генерирует старую форму
  `InterceptsLocation(string,int,int)`, которую текущий Roslyn помечает как неподдерживаемую;
  precompiled SQL при этом в бинарнике присутствует.
- **Dapper.AOT** — работает, но требует `[module: DapperAot]` и
  `InterceptorsNamespaces=$(InterceptorsNamespaces);Dapper.AOT`; запросы обязаны быть ручным SQL
  с константным текстом, non-generic API и runtime-конфигурация не поддерживаются.
- Цифры не являются общим бенчмарком: Dapper не умеет композицию запросов, EF — статический
  precompile, nextorm — runtime-композицию. Артефакты эксперимента лежат вне репозитория
  (`/tmp/opencode/oc-report-compare`), в поставку не входят.

Эталонное приложение теперь **в репозитории**: `examples/nextorm.examples.oc-report` (тот же
`Program.cs`, `ProjectReference` на `src/` — как у mssql-примера, потому что AOT-фикс ещё не в
опубликованном пакете). `seed.sql` создаёт детерминированную синтетическую базу с теми же двумя
таблицами, поэтому приложение можно прогнать без приватной 13 ГБ базы. Проверено: JIT и AOT на
синтетической базе дают побайтно одинаковый отчёт, publish → 8.4 MB native.

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

### 3.5. NativeAOT: частичная поддержка (docs + CI)

- **`readme.md`** — новый раздел `## NativeAOT`: частичная/экспериментальная поддержка (SQLite,
  `net10.0`, `linux-x64`), перечисление проверенных форм запросов, ссылки на гайд и #370.
- **`docs/infrastructure/05-nativeaot.md`** + **`docs/ru/infrastructure/05-nativeaot.md`** —
  что проверено, что нет, таблица 230 unique / 236 raw диагностик, сравнение на `oc-report`,
  как попробовать, как сообщить о падении. Записи в `docs/infrastructure/toc.yml` и `docs/ru/toc.yml`.
- **`docs/infrastructure/03-value-converters.md`** (+RU) — ссылка на новую страницу из оговорки про
  `JsonTypeInfo<T>`.
- **`docs/index.md`** — пункт rc5 о частичной поддержке NativeAOT.
- **`AotNativeRunTests.cs`** — комментарий приведён в соответствие: раннер теперь публикует CI
  (job `aot`), а не «CI does not publish it».
- **`examples/nextorm.examples.oc-report`** — AOT-эталон в репозитории: `Program.cs` (типизированные
  запросы, `json_extract`, runtime-фильтр), `seed.sql` (детерминированная синтетическая база),
  `README.md` (запуск JIT/AOT и таблица сравнения). Добавлен в `nextorm.slnx`; помечен как
  `ProjectReference`-исключение в `examples/README.md` и `examples/VERIFICATION.md`.
- **`docs/comparisons/*`** (+RU) — во всех страницах сравнения (index, capabilities, benchmarks,
  provider-features) явно зафиксирован zero-config контракт NativeAOT и ссылка на эталон;
  внутренняя `docs/specs/comparison/capability-matrix.md` — новый раздел «NativeAOT migration cost».

### 3.6. Синхронизация с `main` перед мержем (PR #371)

К моменту PR #371 в `main` ушли 8 коммитов, которых нет на ветке: CodeQL в advanced setup
(`.github/workflows/codeql.yml`, `.github/codeql/codeql-config.yml`, #355–#356), least-privilege
`permissions: contents: read` в `.github/workflows/dotnet.yml` (#358) и рефреш бенчмарк-таблиц от
2026-10-30 (#357). Разрешено так:

- **CodeQL и `permissions`** — взяты из `main`. `dotnet.yml` смерджился без конфликта: блок
  `permissions` стоит на уровне workflow, job `aot` работает с `contents: read`.
- **Рефреш бенчмарков от 2026-10-30 — отброшен**: на ветке лежит более свежий прогон 2026-11-07
  (`chore(benchmarks): refresh comparison reports and tables`), он же источник чисел в `readme.md`,
  `docs/comparisons/benchmarks.md` и RU-зеркале. Конфликты в отчётах BenchmarkDotNet решены в пользу
  ветки; `*-report.html` остаются вне индекса (политика 1e31331d «артефакты локально»).
- **Текстовые конфликты** (`readme.md`, `docs/comparisons/benchmarks.md`, RU-зеркало) — везде сторона
  ветки: она содержит и новые числа, и секции NativeAOT, которых в `main` нет.

## 4. Публикация (шаги владельца)

- [x] Закоммитить подготовленный объём (§3) на ветке `1.0.9-rc5` и запушить её. (`b44c6d0f`)
- [x] Влить `1.0.9-rc5` в `main` через PR. **Здесь и закрываются релизные гейты:** job `build`
      прогоняет build, все 16 тестовых модулей и оба coverage-гейта с hard-fail на `main`;
      workflow `Docs` деплоит документацию EN+RU. (PR #371, merge `8c484206`; конфликты с `main`
      сняты merge-коммитом `dcc752db`, см. §3.6)
- [x] Дождаться зелёного run `build` на `main` и успешного деплоя `Docs` (якорь `#109-rc5`
      доступен на задеплоенной странице).
- [x] Создать тег `v1.0.9-rc5` → CI job `publish` (OIDC) отправляет **8** пакетов на nuget.org,
      включая `nextorm.extensions`.
- [x] Проверить run `publish`: run `42452841750` — `build`, `aot`, `publish` green, «Your package was
      pushed» × 8; все 8 версий `1.0.9-rc5` подтверждены через flat-container API nuget.org.
- [x] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-rc5> + 8 пакетов + issues #245–#369.
- [ ] Закрыть milestone 1.0.9-rc5 (его issues уже closed, open=0).
- [x] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc5`
      и обновить дату/строку прогона в `examples/VERIFICATION.md`.
- [x] Бампнуть `Directory.Build.props` `<Version>` → `1.0.9-rc5` и перевести
      `examples/nextorm.examples.oc-report` с `ProjectReference` на опубликованные пакеты.
      Примеры перепрогнаны на rc5: postgres 10/10, mssql 11/11, clickhouse 9/9, oc-report JIT/AOT
      идентичны (8,414,024 байта).

## 5. Definition of Done

- [ ] Версии всех 8 пакетов = `1.0.9-rc5`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.9-rc5` запушен; CI отправил 8 пакетов на nuget.org (включая `nextorm.extensions`).
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-rc5` доступен.
- [ ] CI job `aot` зелёный: нативный раннер опубликован, `AotNativeRunTests` прошёл под
      `NEXTORM_AOT_EVIDENCE=1`.
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
- **[#344](https://github.com/AlexeyShirshov/nextorm/issues/344) остаётся открытым**: копии
  `QueryFilterSqlGenerationTests` в четырёх провайдерных проектах сериализованы этим релизом, но
  аудит остальных проектов на «purge caller рядом с plan-identity assert» не закрыт.
