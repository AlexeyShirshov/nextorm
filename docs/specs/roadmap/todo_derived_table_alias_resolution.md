# TODO: Alias производной таблицы (join к фильтрованному builder'у и ORDER BY над спроецированным запросом)

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps surfaced in `nextorm 1.0.5-alpha`», пункты 3 и 6. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.44.

## Пункт и цель

- **Проблема A (пункт 3 README):** join к entity builder'у, несущему `Where`, рендерит основной
  источник как производную таблицу с переименованными колонками, а `ON` ссылается на «сырую» колонку →
  `no such column: t1.product_id`.
- **Проблема B (пункт 6 README):** `db.From(grouped).OrderByDescending(x => x.OrderTotal)` эмитит
  `order by t3.OrderTotal`, тогда как производная таблица названа `t1`.
- **Цель:** единый источник правды для идентичности производной таблицы: и `ON`, и `ORDER BY` (и
  фильтры/проекции) ссылаются на фактический alias/колонки.
- **Критерий приёмки:** SQL-generation тесты: (A) `From<A>().Where(...).Join<B>(...)` ссылается в `ON`
  на alias derived-таблицы; (B) `From(projected).OrderByDescending(...).Page(...)` — на фактический
  alias; alias-identity входит в ключ плана; один и тот же результат на всех SQL-провайдерах; тесты
  `CommonTestSuite.Join.cs` / пейджинга на контейнерах.

## Падающие формы

- nopCommerce query 9 (`OrderService.GetOrderByOrderItemAsync`) и query 10
  (`ProductService.GetProductByOrderItemIdAsync`) — проблема A; обход: join только к нефильтрованным
  builder'ам, фильтр на join-проекции.
- Пейджинг через derived table из спроецированного запроса — проблема B; обход: не страничить через
  производную таблицу.

## Гипотеза и область

- Рассогласование между `DataContext/SqlSourceRenderer.cs` (оборачивает фильтрованный источник в
  derived table и алиасит колонки) и рендером ссылок (`Visitors/{AliasResolver,AliasFromProjectionVisitor}.cs`,
  `Query/{DefaultAliasProvider,DefaultColumnsProvider}.cs`).
- Проблемы A и B, вероятно, один корень — alias-identity производной таблицы; чинить совместно,
  одним заходом. Затронут план-кэш: alias обязан быть частью ключа
  (`Query/QueryPlanEqualityComparer.cs`), иначе кэш отдаст SQL с чужим alias.

## Файлы к изменению

- `src/nextorm.core/DataContext/{SqlSourceRenderer,QueryPlanner}.cs`,
  `Visitors/{AliasResolver,AliasFromProjectionVisitor}.cs`,
  `Query/{DefaultAliasProvider,DefaultColumnsProvider,QueryPlanEqualityComparer}.cs`,
  `Builders/Joins/JoinedEntityBuilder.cs`
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.Join.cs` (+ пейджинг)
- Доки EN+RU (при изменении наблюдаемого поведения), `sql-capabilities-gap-analysis.md` §4 п.44

## Открытые вопросы

1. Схлопывать ли derived table для фильтрованного источника (push-down `Where` в `ON`) вместо
   оборачивания — или чинить алиасинг обёртки?
2. Нужен ли alias-identity отдельным ключом плана или он выводится из структуры источников (сравнить с
   H1/H2 в [`todo_join_projection_mapping.md`](todo_join_projection_mapping.md))?

## Источник

README портов, пункты 3 и 6; после закрытия — убрать/пометить их в README примеров.
