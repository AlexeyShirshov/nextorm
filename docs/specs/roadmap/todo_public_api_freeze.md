# TODO: Заморозка и трекинг публичного API (IF6 / Шаг 5) — ✅ выполнено

> Рабочий план (design RFC). Источник: GitHub issue
> [#53 «IF6: Заморозка и трекинг публичного API»](https://github.com/AlexeyShirshov/nextorm/issues/53),
> реестр [`docs/specs/design/API-NAMING-REVIEW.md`](../design/API-NAMING-REVIEW.md) (находки
> IF6/DC3/DC8, «Список заморозки»). Это **единственный незакрытый пункт закрытой фичи
> «capability-объекты диалекта» (Фазы 1–3)**.
>
> **Статус: закрыто 2026-11-11.** Файл сохранён как запись о выполнении — на него ссылается реестр.
> **Дополнение (issue [#389](https://github.com/AlexeyShirshov/nextorm/issues/389), milestone `1.0.10-a`):**
> после публикации 1.0.9 включён `PackageValidationBaselineVersion=1.0.9` — вторая половина
> пакетного гейта (ApiCompat против опубликованного пакета) заработала; см. пункт 5.

## Пункт и цель

- Проблема: `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` отсутствовали, `PublicApiAnalyzers`
  не подключён, `ApiCompat`/API-approval-теста нет. Новые публичные члены (capability-интерфейсы,
  DIM/`virtual`-свойства, class-B/C `Supports*`) не трекались — `RS0016`/`RS0017` не срабатывали.
- Цель: подключить `PublicApiAnalyzers`, зафиксировать текущую поверхность, включить
  `EnablePackageValidation`, чтобы дальнейшие изменения публичного API были явными.

## Что сделано (2026-11-11)

1. **Пакет.** `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 добавлен в `Directory.Packages.props`
   (CPM). PackageReference без версии, `PrivateAssets="all"`, плюс `AdditionalFiles` на оба файла —
   в 9 библиотечных `.csproj` и в `src/nextorm.core.sourcegenerator` (у генератора публичного API
   нет: оба типа `internal`, поэтому его `PublicAPI.Shipped.txt` — только заголовок; файл держит
   инвариант на будущее).
2. **Файлы.** В каждом из 10 проектов — `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` с первой
   строкой `#nullable enable` (иначе nullability не трекается). Baseline сгенерирован анализатором
   (`dotnet format analyzers --diagnostics RS0016`) — не написан руками, поэтому он точно совпадает
   с деревом: `nextorm` — 6355 подписей, `nextorm.clickhouse` — 224, `nextorm.sqlserver` — 187,
   `nextorm.postgres` — 156, `nextorm.mysql` — 117, `nextorm.sqlite` — 88, `nextorm.mariadb` — 30,
   `nextorm.extensions` — 36, `nextorm.entityframeworkcore` — 12.
3. **Заморозка.** Baseline лежит в `Shipped` (решение владельца 2026-11-11: первая прод-версия —
   1.0.9, релиз сразу после вливания), `Unshipped` пуст. Новые члены после 1.0.9 пишутся в
   `Unshipped` и переносятся в `Shipped` следующим релизом.
4. **Гейт.** RS0016/RS0017 закреплены `error` в `.editorconfig` — падают независимо от
   `TreatWarningsAsErrors` в конкретном проекте. Пробы: публичный тип без объявления → RS0016;
   объявление без типа → RS0017; после правки файла — зелёно. Build Debug и Release — 0/0.
5. **Пакетная проверка.** `EnablePackageValidation=true` и `PackageValidationBaselineVersion=1.0.9`
   в `src/Directory.Build.props`: `dotnet pack -c Release` всех 9 публикуемых пакетов зелёный,
   ApiCompat сравнивает их с опубликованными пакетами 1.0.9. Проверено негативной пробой
   (переименование `nextorm.extensions.SelectGroupExtensions.SelectGroupHaving`) — pack валится с
   `CP0002` на net10.0 и net11.0, то есть baseline-сравнение работает, а не пропускается; против
   prerelease-baseline (1.0.8-b, 1.0.9-rc5) оно молча пропускалось. Осознанное ломающее изменение
   подавляется явно через `CompatibilitySuppressions.xml`
   (`dotnet pack -p:ApiCompatGenerateSuppressionFile=true`), baseline при этом не снимается.
   Выполнено по тикету [#389](https://github.com/AlexeyShirshov/nextorm/issues/389) (milestone `1.0.10-a`).
6. **Отложенные переименования.** XP3 выполнено (`ISqlDialect.SupportsAnyAggregates` →
   `SupportsAnyLastAggregate` с обновлением диалектов, транслятора, тестов, XML-доков и
   `docs/**` + `docs/ru/**`); XP6 дописан (два оставшихся вызова `SqlFunctions.ClickHouse.any_agg`
   переведены на `SqlFunctions.Sql.any_agg`). **UG1** (`gen_random_uuid` → `uuidv4`) и **JP2**
   (`MakeJsonExtract` → нейтральное имя) сознательно отложены и внесены в baseline под текущими
   именами — это не механические переименования, они меняют документированный контракт.

## Открытые вопросы — решения

1. *Один общий файл или по файлу на проект?* — по паре на проект (сборочная единица анализатора).
2. *Hard-gate или warning-only?* — hard-gate (`error`); правка `PublicAPI.*.txt` — часть каждого
   API-изменения.
3. *ApiCompat против пакета или approval-тест?* — `EnablePackageValidation`; baseline — первый
   стабильный релиз, не prerelease.
4. ~~Убирать ли `CS1591` из `NoWarn`?~~ — убран 22.09.2026, вся публичная поверхность
   задокументирована (шаг 3 плана).

## Файлы

- `Directory.Packages.props`, `src/Directory.Build.props`, `.editorconfig`, 10 пар
  `PublicAPI.Shipped/Unshipped.txt`, 10 `src/**/*.csproj`, `docs/specs/design/API-NAMING-REVIEW.md`
  (IF6/DC3/DC8/XP3), `docs/specs/comparison/capability-matrix.md`, `docs/guide/05-grouping-and-aggregates.md`
  (+RU).
- CI правок не требует: `dotnet build` в job `build` уже валит сборку на RS0016/RS0017.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24) — как учтено

- **[SRP] Hard-gate vs alpha-политика** — политика зафиксирована: правка `PublicAPI.*.txt` входит в
  каждое API-изменение; `TreatWarningsAsErrors` не ослабляется.
- **[DRY] Генерация `Shipped` в один проход** — baseline сгенерирован анализатором в `Unshipped`,
  сверен с курированным перечнем IF6/DC3/DC8 (удалённых Фазой 3 пар в файлах нет) и только затем
  промоутирован в `Shipped`.
- **[TYPE]/[ISP] `Microsoft.DotNet.ApiCompat` как PackageReference** — не используется; штатный путь
  `EnablePackageValidation` + `PackageValidationBaselineVersion`.
- **[TYPE] Место подключения анализатора** — по `.csproj`, не в корневом `Directory.Build.props`
  (иначе течёт в `tests/**`/`benchmarks/**`).
- **[BUILD] CRLF** — сгенерированные файлы в CRLF (`core.autocrlf=true`), как остальные `.cs`/`.md`.
- **[SRP] `nextorm.sln` vs `.slnx`** — везде `nextorm.slnx`.
