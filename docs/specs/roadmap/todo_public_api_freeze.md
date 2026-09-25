# TODO: Заморозка и трекинг публичного API (IF6 / Шаг 5)

> Рабочий план (design RFC). Источник: GitHub issue
> [#53 «IF6: Заморозка и трекинг публичного API»](https://github.com/AlexeyShirshov/nextorm/issues/53),
> реестр [`docs/specs/design/API-NAMING-REVIEW.md`](../design/API-NAMING-REVIEW.md) (находки
> IF6/DC3/DC8, «Список заморозки»). Это **единственный незакрытый пункт закрытой фичи
> «capability-объекты диалекта» (Фазы 1–3)**: поверхность сформирована (capability-объекты),
> но её состав в `PublicAPI.*.txt` не зафиксирован.

## Пункт и цель

- Проблема: `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` отсутствуют, `PublicApiAnalyzers`
  не подключён, `ApiCompat`/API-approval-теста нет; `CS1591` — в `<NoWarn>` всех библиотечных
  `.csproj`. Новые публичные члены (capability-интерфейсы, DIM/`virtual`-свойства, class-B/C
  `Supports*`) не трекаются — `RS0016`/`RS0017`/`RS0025` не срабатывают.
- Цель: подключить `PublicApiAnalyzers`, зафиксировать текущую поверхность в `PublicAPI.Shipped.txt`,
  завести `ApiCompat`/approval-тест в CI, чтобы дальнейшие изменения публичного API были явными.
- Критерий приёмки: `dotnet build nextorm.sln -c Release` с `PublicApiAnalyzers` — 0/0;
  `PublicAPI.Shipped.txt` содержит текущую поверхность; CI-шаг падает при незаявленном
  добавлении/удалении публичного члена.

## Что вносить (не дублировать удалённое)

- **Новое (Фазы 1–2):** 16 capability-интерфейсов `NextORM.Core.I*` и 16 nullable DIM-свойств
  `ISqlDialect.<Prop>.get` + одноимённые `SqlDialectBase.<Prop>.get` — точный перечень ведёт
  `API-NAMING-REVIEW.md` (блоки IF6/DC3/DC8).
- **Остаточная не-объектная поверхность** класса B/C (`Supports*`-флаги без собственного
  `Make*`-хука) — критерий и перечень зафиксированы в `API-NAMING-REVIEW.md` («Отложенные
  ANSI-гейты (класс C, остались `bool`)»).
- **НЕ вносить** 41 дублирующий `Supports*`/`Make*`-член, удалённый Фазой 3 (перечень —
  в `API-NAMING-REVIEW.md`, блок «Фаза 3»).
- Файл `PublicAPI.Shipped.txt` — с `#nullable enable` (nullable-аннотации `?` обязательны).

## Шаги

1. Подключить `Microsoft.CodeAnalysis.PublicApiAnalyzers` (версия в `Directory.Packages.props`,
   по CPM) и завести `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` в библиотечных проектах.
2. Сгенерировать содержимое `PublicAPI.Shipped.txt` точным выводом анализатора по текущему дереву.
3. ✅ **Выполнено 22.09.2026:** `CS1591` убран из `<NoWarn>` всех 7 библиотечных `.csproj`;
   задокументированы все 1283 публичных члена (покрытие 100 %), сборка Debug `0/0`.
4. Добавить `Microsoft.DotNet.ApiCompat`/approval-тест публичной поверхности и включить в CI.
5. Закрыть находки IF6/DC3/DC8 в `API-NAMING-REVIEW.md`, удалить этот файл (по скиллу
   `implementing-todo-features`).

## Открытые вопросы

1. Один общий `PublicAPI.*.txt` на solution или по файлу на проект?
2. `PublicApiAnalyzers` как hard-gate в CI или сначала warning-only?
3. `ApiCompat` против последнего опубликованного пакета (нужен baseline) или достаточно
   approval-теста поверхности решения?
4. ~~Убирать ли `CS1591` из `NoWarn` глобально или только для capability-поверхности?~~
   **Решено 22.09.2026:** убран глобально во всех 7 библиотечных `.csproj` (шаг 3); вся публичная
   поверхность задокументирована.

## Файлы к изменению

- `Directory.Packages.props`, `Directory.Build.props`, библиотечные `src/nextorm.*/*.csproj`,
  новые `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`, `.github/workflows/dotnet.yml`.
- Документация: `docs/specs/design/API-NAMING-REVIEW.md` (IF6/DC3/DC8 → закрыто).

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр шагов 2/4 — 2 блокера**; механика заморозки не согласована с build-дисциплиной.

- **[SRP] 🔴** Hard-gate конфликтует с alpha-политикой и build-дисциплиной: `TreatWarningsAsErrors=true` (`Directory.Build.props:40`) делает RS0016/RS0017/RS0025 ошибками сборки, а критерий `:18-20` хочет падения только на «незаявленном» изменении; это противоречит принятому «alpha допускает слом» (`docs/specs/design/code-smells-review.md:3125`). Fix: зафиксировать политику (правка `PublicAPI.*.txt` — часть каждого API-изменения) либо до заморозки держать анализатор warning-only.
- **[DRY] 🔴** Генерация `Shipped` в один проход (`:38`) благословляет всю поверхность как shipped и обходит курированный перечень «Что вносить» (`:22-32`, исключающий 41 удалённый член); RS0017 не сработает — предыдущего файла нет. Fix: генерировать в `PublicAPI.Unshipped.txt`, сверить diff с IF6/DC3/DC8, затем промоутировать.
- **[TYPE]/[ISP] 🟡** Путь `ApiCompat` неверен (`:41`): `Microsoft.DotNet.ApiCompat` — не `PackageReference`; штатно `EnablePackageValidation` + `PackageValidationBaselineVersion` (или таск `Microsoft.DotNet.ApiCompat.Task`). Baseline не решён (`:49`), текущая версия `Directory.Build.props:56` = `1.0.5-alpha`. Fix.
- **[TYPE] 🟡** Место подключения анализатора (`:57`, `Directory.Build.props`) применится к `nextorm.core.sourcegenerator` (`netstandard2.0`, `IsPackable=false`) и ко всем `tests/**`/`benchmarks/**` → поток RS0016. Fix: `src/Directory.Build.props` под `IsPackable` либо 7 библиотечных `.csproj`.
- **[DRY] 🟡** Перечень 16+16 интерфейсов/свойств дублирует `API-NAMING-REVIEW.md` (IF6/DC3/DC8); источники разъедутся. Fix: реестр — единственный источник, план — ссылка.
- **[BUILD] 🟡** CRLF сгенерированных `PublicAPI.*.txt` ничем не гейтится: нет `.gitattributes` и `end_of_line` в `.editorconfig`. Fix: добавить и нормализовать.
- **[SRP] ℹ️** В критерии `:18` команда `nextorm.sln` — в репозитории `nextorm.slnx`. Fix.
- **[SRP] ℹ️** Шаг 4 (`:41,49-50`) не выполним без решения: кто/где заводит baseline/approval. Fix: выбрать (рекомендуется approval-тест по snapshot `PublicAPI.*.txt`).
- **[ISP]/[TYPE] ℹ️** Трекируемая поверхность `ISqlDialect` — принятый долг F12 (`:24-29`). Не переоткрывать; поставить ссылку на F12.
- **[DRY] ℹ️** 16 capability-типов (`Supports`+`Render`) — кандидат на обобщение, но унификация конфликтует с ISP. Deferred.
