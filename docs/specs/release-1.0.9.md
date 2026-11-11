# План релиза 1.0.9 (milestone 1.0.9)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-rc5.md`, `git show v1.0.9-rc5`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, тег `v1.0.9` → tag-triggered
> CI-публикация пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполняется; публикация (§4) — за владельцем репозитория.**
>
> **Это первый стабильный релиз** (все предыдущие теги — `-alpha`/`-beta`/`-b`/`-rc*`), поэтому
> вместе с ним меняется ряд формулировок в публичных доках: `--prerelease` больше не нужен,
> `## Status` перестаёт быть «beta/proof of concept».

- **Milestone:** [1.0.9](https://github.com/AlexeyShirshov/nextorm/milestone/21) — 9 issues:
  8 закрыты (`completed`), 1 открыт — [#334](https://github.com/AlexeyShirshov/nextorm/issues/334)
  (промо на Reddit), это задача **после** релиза, блокером не является.
- **Тег:** `v1.0.9` (планируется)
- **Ветка релиза:** `1.0.9` (синхронна с `origin/1.0.9`)
- **База:** тег `v1.0.9-rc5` (`8c484206`); 26 коммитов от `v1.0.9-rc5` до HEAD подготовки (включая
  коммит подготовки этого плана).
- **Версии пакетов:** `1.0.9` задаётся тегом; CI переопределяет `Version` через `-p:Version`.
  `<Version>` в `Directory.Build.props` **остаётся `1.0.9-rc5`** до публикации: примеры потребляют
  опубликованный `nextorm 1.0.9-rc5` и одновременно `ProjectReference` на `src/`, поэтому ранний
  бамп даёт `NU1605` (downgrade) — тот же эффект, что описан в `release-1.0.9-rc5.md` §3.1. После
  публикации — отдельный коммит: `<Version>` → `1.0.9` и
  `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9`.
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC). Job зависит от `build` **и** `aot`.
- **Пакетов к публикации — 9:** `nextorm`, `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`,
  `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse`, `nextorm.extensions` и **впервые**
  `nextorm.entityframeworkcore`.

## 1. Скоуп милстоуна

Реестр задач — release notes `docs/index.md` §`### 1.0.9`.

| Issue | Что вошло |
| --- | --- |
| [#387](https://github.com/AlexeyShirshov/nextorm/issues/387) | .NET 11: пакеты multi-target `net10.0;net11.0`, тесты/бенчмарки/примеры на `net11.0`, тулчейн на .NET 11 SDK |
| [#53](https://github.com/AlexeyShirshov/nextorm/issues/53) | Заморозка и трекинг публичного API: `PublicApiAnalyzers` + `PublicAPI.Shipped.txt` в каждом пакете, RS0016/RS0017 — ошибки сборки, `EnablePackageValidation` |
| [#384](https://github.com/AlexeyShirshov/nextorm/issues/384) | Join без `From(...)` — по типу сущности и по имени таблицы, все семь операторов |
| [#385](https://github.com/AlexeyShirshov/nextorm/issues/385) | Операции над множествами: правый операнд из селектора сущности |
| [#386](https://github.com/AlexeyShirshov/nextorm/issues/386) | Валидация имён таблиц в `From(string)` и строковых join-перегрузках |
| [#388](https://github.com/AlexeyShirshov/nextorm/issues/388) | JSON streaming: `decimal`-проекция над float/REAL-колонкой без бинарного «хвоста» |
| [#380](https://github.com/AlexeyShirshov/nextorm/issues/380) | `SelectGroupHaving`: условие типизировано по результату проекции |
| [#319](https://github.com/AlexeyShirshov/nextorm/issues/319) | Build-once query construction (M1–M3): нативный raw-plan route, привязка значений замыкания вместо пересборки |

**Breaking changes (consumer-visible):**

- **#53** — `ISqlDialect.SupportsAnyAggregates` переименован в `SupportsAnyLastAggregate` (флаг гейтит
  только `any_last`). Source- и binary-breaking для реализаторов `ISqlDialect` и для кода, читающего
  флаг напрямую; для обычных запросов изменений нет.
- **#380** — `SelectGroupHaving` типизирован по результату проекции: явные generic-аргументы прежней
  формы больше не компилируются.
- **#387** — падают два ассета вместо одного (`net10.0;net11.0`); потребители на .NET 10 получают
  `net10.0`-ассет, на .NET 11 — `net11.0`. Исходников это не ломает.
- **Новый пакет в публикации.** `nextorm.entityframeworkcore` (ранее в job `publish` отсутствовал).

## 2. Релизные гейты — в пайплайне

Релизные проверки **не дублируются локально**: их выполняет CI на мердже в `main`, а публикация —
tag-triggered job `publish`.

### 2.1. Локально (только для коммита)

- [x] `dotnet build nextorm.slnx -c Release --no-restore` — **0 warning / 0 error**.
- [x] `dotnet build nextorm.slnx -c Debug --no-restore` — **0 warning / 0 error**.
- [x] `nextorm.core.tests` 4033/4033, `nextorm.clickhouse.tests` 1525/1525 (после переименования #53).
- [x] `dotnet pack -c Release --no-build` всех 9 пакетов с `EnablePackageValidation` — exit 0;
      `nextorm.entityframeworkcore` проверен отдельно с `-p:Version=1.0.9`.

### 2.2. CI, job `build` (`dotnet.yml`, запускается на push в любую ветку)

- [ ] `dotnet restore` + `dotnet build --no-restore`.
- [ ] Solution-wide `dotnet test` всех модулей под `dotnet-coverage`
      (`--filter-not-trait Category=Diagnostic --max-parallel-test-modules 1`).
- [ ] Unit-гейт: line ≥ 85 / branch ≥ 75.
- [ ] Integration-гейт: оба live-проекта, union через ReportGenerator, line ≥ 65 / branch ≥ 75.

На ветке гейты дают warning при недоборе, hard-fail — только на `main`; это и есть проверка мерджа.

### 2.3. CI, workflow `Docs` (push в `main`, paths `docs/**`, `src/**`)

- [ ] DocFX EN+RU → GitHub Pages; якорь `#109` появляется автоматически после мерджа.

### 2.4. CI, job `publish` (тег `v*` или `workflow_dispatch`)

- [ ] `dotnet pack` **9** пакетов с `-p:Version=$TAG` (включая `nextorm.entityframeworkcore`) →
      nuget.org через NuGet trusted publishing (OIDC).

## 3. Изменения в репозитории

### 3.1. Версия и публикация

- **`Directory.Build.props` — версия остаётся `1.0.9-rc5` до публикации** (см. шапку). Бамп — отдельным
  коммитом после тега, вместе с `examples/Directory.Packages.props`.
- **`.github/workflows/dotnet.yml`** — в список `dotnet pack` job'а `publish` добавлен
  `src/nextorm.entityframeworkcore/nextorm.entityframeworkcore.csproj` (9 пакетов).

### 3.2. Документация

- **`docs/index.md`** — `## Status` → 1.0.9 (первый стабильный релиз, поверхность заморожена);
  `## Installation` — `--prerelease` убран, добавлены `nextorm.extensions`/`nextorm.entityframeworkcore`;
  `## Releases` — новый раздел `### 1.0.9` (8 issues + строка про новый пакет + breaking-строка).
- **`readme.md`** — то же в `## Installation` (таблица пакетов дополнена `nextorm.entityframeworkcore`)
  и `## Status` (Beta → Stable, API заморожен и трекается).
- **`docs/specs/release-1.0.9.md`** — этот план.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9` и запушить её.
- [ ] Влить `1.0.9` в `main` через PR. **Здесь и закрываются релизные гейты:** job `build` прогоняет
      build, все тестовые модули и оба coverage-гейта с hard-fail на `main`; workflow `Docs` деплоит
      документацию EN+RU.
- [ ] Дождаться зелёного run `build` на `main` и успешного деплоя `Docs` (якорь `#109` доступен).
- [ ] Создать тег `v1.0.9` → CI job `publish` (OIDC) отправляет **9** пакетов на nuget.org.
- [ ] Проверить run `publish`: 9 сообщений «Your package was pushed», версии `1.0.9` подтверждены
      через flat-container API для всех 9 `PackageId`.
- [ ] Создать GitHub Release **stable** (не prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109> + 9 пакетов + issues #319–#388.
- [ ] Закрыть milestone 1.0.9 (кроме #334 — промо, остаётся открытым).
- [ ] После публикации: `Directory.Build.props` `<Version>` → `1.0.9`,
      `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9`, дата/строка прогона в
      `examples/VERIFICATION.md`, перепрогнать примеры на опубликованных пакетах.
- [ ] Открыть [#389](https://github.com/AlexeyShirshov/nextorm/issues/389): после публикации 1.0.9
      добавить `PackageValidationBaselineVersion=1.0.9` и проверить baseline-сравнение негативной
      пробой.

## 5. Definition of Done

- [ ] Версии всех 9 пакетов = `1.0.9`; `docs/index.md` обновлён (Status + Installation + Releases).
- [ ] Тег `v1.0.9` запушен; CI отправил 9 пакетов на nuget.org (включая `nextorm.entityframeworkcore`).
- [ ] GitHub Release (stable) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109` доступен.
- [ ] CI job `aot` зелёный: нативный раннер опубликован, `AotNativeRunTests` прошёл под
      `NEXTORM_AOT_EVIDENCE=1`.
- [ ] Milestone 1.0.9 закрыт (кроме #334).

## 6. Открытые вопросы

- **`nextorm.entityframeworkcore`** публикуется впервые: после тега проверить, что пакет появился на
  nuget.org и что его зависимости (`nextorm`, 4 провайдера) разрешаются из `1.0.9`.
- **Стабильность 1.0.9.** Поверхность заморожена трекингом (#53), но `PackageValidationBaselineVersion`
  появится только после публикации (#389) — то есть первый релиз, сравнённый с 1.0.9, будет 1.0.10-a.1.
- **#334** (промо на Reddit) — задача после релиза, milestone закрывать с оговоркой.
- **Branch-гейт** integration по ветвям держится с малым запасом (75.3 против 75 на rc5); рост покрытия —
  отдельная задача.
