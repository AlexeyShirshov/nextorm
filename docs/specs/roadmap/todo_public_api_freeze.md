# TODO: Заморозка и трекинг публичного API (IF6 / Шаг 5)

> Рабочий план (design RFC). Источник: GitHub issue
> [#53 «IF6: Заморозка и трекинг публичного API»](https://github.com/AlexeyShirshov/nextorm/issues/53),
> реестр [`docs/specs/design/API-NAMING-REVIEW.md`](design/API-NAMING-REVIEW.md) (находки
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
3. `CS1591` — не глушить для публичной поверхности (или отдельный CI-шаг проверки XML-доков).
4. Добавить `Microsoft.DotNet.ApiCompat`/approval-тест публичной поверхности и включить в CI.
5. Закрыть находки IF6/DC3/DC8 в `API-NAMING-REVIEW.md`, удалить этот файл (по скиллу
   `implementing-todo-features`).

## Открытые вопросы

1. Один общий `PublicAPI.*.txt` на solution или по файлу на проект?
2. `PublicApiAnalyzers` как hard-gate в CI или сначала warning-only?
3. `ApiCompat` против последнего опубликованного пакета (нужен baseline) или достаточно
   approval-теста поверхности решения?
4. Убирать ли `CS1591` из `NoWarn` глобально или только для capability-поверхности?

## Файлы к изменению

- `Directory.Packages.props`, `Directory.Build.props`, библиотечные `src/nextorm.*/*.csproj`,
  новые `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`, `.github/workflows/dotnet.yml`.
- Документация: `docs/specs/design/API-NAMING-REVIEW.md` (IF6/DC3/DC8 → закрыто).
