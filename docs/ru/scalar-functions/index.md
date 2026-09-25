# Скалярные функции

> Преобразуйте члены `string`, `Math` и `DateTime`, объединение `??`, логические предикаты и числовые
> преобразования в SQL, специфичный для провайдера.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](../guide/01-querying-and-projections.md) · [Фильтрация (WHERE)](../guide/02-filtering-where.md)

## Обзор

nextorm распознаёт фиксированный набор членов CLR и переписывает их в SQL внутри любого выражения запроса.
Диспетчеризация находится в [`BaseExpressionVisitor`](xref:NextORM.Core.BaseExpressionVisitor): методы `string`, методы `Math`, члены `DateTime`,
`SqlFunctions.Sql.like`, оператор `??` и числовые преобразования. Всё, что зависит от провайдера, делегируется
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect), поэтому один и тот же код C# генерирует правильную функцию на каждом провайдере.

Повсюду действуют два правила:

* **захваченное** значение (локальная переменная или параметр) становится **параметром** запроса, а не
  литералом;
* **константа** встраивается. Для `Contains`/`StartsWith`/`EndsWith` это также означает, что `%`, `_` и `\`
  в константе экранируются и генерируется предложение `escape '\'`.

Встроенные преобразования применяются **до** любого сопоставления
[`[SqlFunction]`](../guide/12-user-defined-functions.md), поэтому пользовательский атрибут не может изменить
поведение членов `string`/`Math`/`DateTime`.

Кросс-провайдерные помощники находятся в [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql). Функции, которые поддерживает только один
провайдер, сгруппированы в отдельную провайдерную поверхность: [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (PostgreSQL: нативные
массивы, нативный JSON, расширенная библиотека скалярных функций, PG-only агрегаты и табличные
функции `generate_series`/`unnest`), [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) (SQL Server: JSON-как-текст и
`string_split`/`openjson`) и [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (ClickHouse: `arg_min`/`arg_max`, комбинатор `-If`,
семейство строкового JSON `JSONExtract*`, быстрый разбор плоского JSON `visitParamExtract*`,
JSONPath-скаляры `json_value`/`json_query`/`json_exists` и функции
словарей `dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in`).
Вызов любой из них на провайдере, который не opt-in, бросает `NotSupportedException`.

## См. также

* [Фильтрация (WHERE)](../guide/02-filtering-where.md) — `Contains`/`in`, `??` и условные выражения в предикатах.
* [Группировка и агрегаты](../guide/04-grouping-and-aggregates.md) — агрегатные функции (`count`, `sum`, ...).
* [Пользовательские функции](../guide/12-user-defined-functions.md) — когда скалярная функция не встроена.
* [Обзор провайдеров](../providers/overview.md) — флаги возможностей и кавычки.

---

Source: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/AggregateFilter.cs`;
`src/nextorm.core/Query/SqlFunctions.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
