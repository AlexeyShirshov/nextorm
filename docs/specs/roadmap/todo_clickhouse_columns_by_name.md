# TODO: ClickHouse — доступ к колонкам без свойства сущности

> Остаток ClickHouse. Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.10.
> **Статус: готово (22.09.2026).** Реализован `SqlFunctions.Column<T>(entity, name)`.

## Проблема

У mapped-сущности доступны только объявленные свойства; колонки без свойства (широкие таблицы вроде
`hits_v1`, ~105 колонок) были достижимы только через `WithSql`. By-name-поверхность существовала лишь
в untyped-режиме (`From("table")` → `TableAlias`/`TableColumn`).

## Что сделано

- Публичный API `SqlFunctions.Column<T>(object entity, string columnName)` — типизированная ссылка на
  колонку mapped-сущности по имени (маркер, как `SqlFunctions.Parameter<T>`).
- Трансляция в `NormSqlTranslator` + общий хелпер `BaseExpressionVisitor.AppendColumnReference`,
  переиспользующий резолвинг алиаса из `EmitTableAliasColumn` (в т.ч. `p.ItemN` join-проекции);
  не-source-аргумент даёт `BuildSqlCommandException`.
- `visitor.ColumnName = columnName` → `renameAware` в `MakeColumn` даёт `... as <PropertyName>` при
  переименовании в проекции.
- In-memory: тело метода бросает `NotSupportedException` (как `openjson`/`string_split`), поэтому
  компиляция выражения не возвращает молча `default`.

## Матрица провайдеров

Фича — builder-level ссылка на идентификатор, не SQL-функция. Новые `Supports*`-гейты не нужны:
рендер `alias.column` уже есть в каждом SQL-диалекте через `QuoteIdentifier`/`AppendIdentifier`.

| Провайдер | Нативный вид | Поддержка | Примечание |
|---|---|---|---|
| PostgreSQL | `t."region_id"` | ✅ | стандартный идентификатор, double-quote |
| SQL Server | `t.[region_id]` | ✅ | bracket-quoting |
| MySQL | `` t.`region_id` `` | ✅ | backtick |
| MariaDB | `` t.`region_id` `` | ✅ | наследует MySQL |
| ClickHouse | `` t.`region_id` `` | ✅ (origin) | backtick; origin из-за `hits_v1` |
| SQLite | `t."region_id"` | ✅ | double-quote |
| InMemory | — | ❌ `NotSupportedException` | нет понятия колонки/имени в объекте |

## Использование

```csharp
ctx.From<IHit>().Select(h => new { h.Url, Region = SqlFunctions.Column<ulong>(h, "RegionID") });
```

## Тесты

- SQL-gen: `tests/nextorm.clickhouse.tests` (проекция + rename, join-проекция `p.ItemN`, quoted,
  не-source → `BuildSqlCommandException`), `tests/nextorm.postgres.tests`, `tests/nextorm.sqlite.tests`.
- Core/in-memory: `tests/nextorm.core.tests.InMemoryTests` — `NotSupportedException`.
- Интеграция (реальный ClickHouse): `wide_entity` без свойства `regionid` —
  `ColumnByName_ShouldReadUnmappedColumn`, `ColumnByName_InWhere_ShouldFilterByUnmappedColumn`.
- Покрытие: line 85.5% / branch 74.6% (без падения).

## Документация

- `docs/guide/01-querying-and-projections.md` (+RU) — раздел «Columns by name / Колонки по имени».
- `docs/providers/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU).
- `sql-capabilities-gap-analysis.md` §4 п.10 → shipped.

## Источник

- `IHit` (подмножество колонок `hits_v1`).
