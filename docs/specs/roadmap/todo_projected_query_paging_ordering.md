# TODO: Пейджинг и expression-сортировка у спроецированного `QueryCommand<T>`

> **Статус: реализовано (SHIPPED) в `1.0-b.1`.** `QueryCommand<T>` получил expression-перегрузки
> `OrderBy`/`OrderByDescending`, а также `Limit`/`Offset`/`Page` поверх уже выбранных колонок, на всех
> SQL-провайдерах. Доки: `docs/guide/05-sorting-and-paging.md` (+RU); gap-analysis §4 п.46.

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps surfaced in `nextorm 1.0.5-alpha`», пункт 5. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.46.

## Статус верификации (`nextorm 1.0.6-alpha`)

Валиден на всех провайдерах, compile-time (харнесс `Gaps/`: рефлексия по `QueryCommand<>`): нет
`Page`/`Limit`/`Offset`, из `OrderBy*` есть только ordinal-перегрузки `OrderBy(int)`/
`OrderByDescending(int)` — expression-сортировки нет. Поэтому grouped + ordered + paged запросы
(nopCommerce query 1/2/3) не компилируются в «естественном» виде и вынуждены сортировать/страничить на
builder'е до `Select`.

## Пункт и цель

- **Проблема:** после терминального `Select` у `QueryCommand<T>` нет `Page`/`Limit`/`Offset` и нет
  expression-`OrderByDescending` (только ordinal-`OrderBy*`). Grouped + ordered + paged запросы
  вынуждены сортировать/страничить на builder'е **до** `Select`, повторяя агрегат в `ORDER BY`.
- **Цель:** спроецированный `QueryCommand<T>` поддерживает сортировку по выражению (asc/desc) и
  пейджинг поверх уже выбранных колонок.
- **Критерий приёмки:** `Select(...).OrderByDescending(x => x.OrderTotal).Page(1, 20)`
  (и `Limit`/`Offset`) компилируется и генерирует `ORDER BY ... DESC` + провайдерный пейджинг на всех
  SQL-провайдерах; SQL-generation тесты; `CommonTestSuite` проверяет порядок и границы; публичный API
  аддитивен, покрыт XML-доком (`CS1591`).

## Падающая форма

- Сгруппированные запросы с сортировкой и пейджингом (nopCommerce query 1/2/3): обход — сортировка и
  страница на builder'е до `Select`, агрегат повторяется в `ORDER BY`.

## Гипотеза и область

- Поверхность спроецированного результата (`Query/QueryCommand.TResult.cs`,
  `Builders/EntityBuilder.cs:170`) не выставляет paging/order-by-expression после `Select`;
  в отличие от билдера (`Builders/Paging.cs`), где они есть.
- Решение — зеркалировать paging/order на `QueryCommand<T>` (или дать `As`/`From(projected)`-путь,
  см. [`todo_join_projection_mapping.md`](todo_join_projection_mapping.md)), не ломая публичный API.

## Файлы к изменению

- `src/nextorm.core/Query/{QueryCommand.TResult.cs,QueryCommandExtensions.cs}`,
  `src/nextorm.core/Builders/{Paging.cs,EntityBuilder.cs}`,
  `src/nextorm.core/DataContext/{SqlBuilder.cs,SqlSourceRenderer.cs}`
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.Paging.cs` (или существующий аналог)
- Доки EN+RU, `sql-capabilities-gap-analysis.md` §4 п.46

## Открытые вопросы

1. Делать пейджинг на `QueryCommand<T>` или требовать `From(projected)` — что согласуется с моделью
   `As` из [`todo_join_projection_mapping.md`](todo_join_projection_mapping.md)?
2. Expression-`OrderByDescending` резолвит члены проекции или исходные колонки?

## Источник

README портов, пункт 5; после закрытия — убрать/пометить его в README примеров.
