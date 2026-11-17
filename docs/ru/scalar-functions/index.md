# Скалярные функции

> Преобразуйте члены `string`, `Math` и `DateTime`, объединение `??`, логические предикаты и числовые
> преобразования в SQL, специфичный для провайдера.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](../engine/index.md) · [Фильтрация (WHERE)](../guide/03-filtering-where.md)

## Обзор

В выражениях запросов к базе данных nextorm преобразует распознанные выражения CLR в SQL. Сгенерированное
выражение вычисляет база данных; такой же вызов C# вне запроса выполняется локально в CLR. К распознаваемым
формам относятся члены `string`, `Math` и `DateTime`, `SqlFunctions.Sql.like`, `??` и числовые
преобразования. Диспетчеризация находится в
[`BaseExpressionVisitor`](xref:NextORM.Core.BaseExpressionVisitor), а зависящий от провайдера SQL рендерит
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect).

Выбирайте поверхность API с учётом переносимости:

* Используйте член CLR или оператор из списка, если для него есть встроенная трансляция nextorm.
* Используйте [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) для SQL-функции без подходящего
  аналога в CLR. Поверхность общая, но поддержка может зависеть от провайдера; проверяйте примечания к
  возможностям каждой функции.
* Используйте провайдерную поверхность `SqlFunctions`, только если намерены зависеть от API этого
  провайдера. Остальные провайдеры отклоняют неподдерживаемые вызовы, а не эмулируют их.

Считайте вызовы `SqlFunctions` SQL-выражениями для запросов, а не локальными вычислениями C#.

Повсюду действуют два правила:

* **захваченное** значение (локальная переменная или параметр) становится **параметром** запроса, а не
  литералом;
* **константа** встраивается в собственной строковой форме литерала провайдера. Для
  `Contains`/`StartsWith`/`EndsWith` это также означает, что `%`, `_` и `\` в константе экранируются перед
  рендерингом.

Предложение `ESCAPE` у сгенерированного шаблона `LIKE` зависит от провайдера: ClickHouse его не генерирует
(и отказывает при пользовательском символе экранирования), MySQL/MariaDB передают операнд как независимый
от режима hex-токен, остальные провайдеры сохраняют предложение.

Встроенные преобразования применяются **до** любого сопоставления
[`[SqlFunction]`](../guide/12-user-defined-functions.md), поэтому пользовательский атрибут не может изменить
поведение членов `string`/`Math`/`DateTime`.

Кросс-провайдерные помощники находятся в [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql). Функции, которые поддерживает только один
провайдер, сгруппированы в отдельную провайдерную поверхность: [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (PostgreSQL: нативные
массивы, нативный JSON, расширенная библиотека скалярных функций, PG-only агрегаты и табличные
функции `generate_series`/`unnest`), [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) (SQL Server: JSON-как-текст,
версионно-зависимые скаляры 2022/2025, такие как `left_shift` и `regexp_count`, агрегат `product` и
`string_split`/`openjson`/`regexp_split_to_table`), [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (ClickHouse: `arg_min`/`arg_max`, комбинатор `-If`,
семейство строкового JSON `JSONExtract*`, быстрый разбор плоского JSON `visitParamExtract*`,
JSONPath-скаляры `json_value`/`json_query`/`json_exists` и функции
словарей `dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in`) и
[`Sqlite`](xref:NextORM.Core.SqlFunctions.Sqlite) (SQLite: функции ядра, JSON1 и агрегат `total`, функции дат и
математики, поверхность полнотекстового поиска).
Вызов любой из них на провайдере, который не opt-in, бросает `NotSupportedException`.

## См. также

* [Фильтрация (WHERE)](../guide/03-filtering-where.md) — `Contains`/`in`, `??` и условные выражения в предикатах.
* [Группировка и агрегаты](../guide/05-grouping-and-aggregates.md) — агрегатные функции (`count`, `sum`, ...).
* [Пользовательские функции](../guide/12-user-defined-functions.md) — когда скалярная функция не встроена.
* [Обзор провайдеров](../connecting-providers/overview.md) — флаги возможностей и кавычки.

---

Source: `src/nextorm.core/Visitors/Core/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/Functions/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/Aggregates/AggregateFilter.cs`;
`src/nextorm.core/Query/SqlFunctions/SqlFunctions.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`tests/nextorm.integration.tests/CommonTestSuite/Functions/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
