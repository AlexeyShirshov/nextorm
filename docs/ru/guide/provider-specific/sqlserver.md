# Специфичный для SQL Server SQL

> SQL Server даёт условную функцию `CHOOSE`, хинты инструкции/таблицы и форму результата `FOR JSON`/
> `FOR XML`, а также табличные функции `string_split`/`openjson`. Поверхности temporal-таблиц,
> `CONTAINSTABLE`, `PIVOT`/`UNPIVOT` и XML-типов пока не входят в nextorm.

**Что нужно знать:** [Запросы и проекции](../01-querying-and-projections.md) · [Провайдер SQL Server](../../providers/sqlserver.md)

## `CHOOSE`

`SqlFunctions.SqlServer.choose(index, v1, v2, ...)` рендерит `CHOOSE(index, v1, v2, ...)` и гейтится
[`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose) (только SQL Server; все остальные
провайдеры выбрасывают исключение).

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new { Label = SqlFunctions.SqlServer.choose(2, "a", "b", "c") })
    .ToList();
```

```sql
select choose(2, 'a', 'b', 'c') as [Label] from complex_entity
```

См. [Скалярные функции](../11-scalar-functions.md#условные-функции).

## Хинты

[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1) привязывает табличный хинт к таблице `FROM`
запроса ([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)), а хинт запроса
рендерит завершающую клаузу `OPTION (...)`, например `OPTION (RECOMPILE)`
([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints)); параметр CTE
`maxRecursion` отображается в `option (maxrecursion n)`. См.
[Хинты запросов](../17-query-hints.md).

## `FOR JSON` и `FOR XML`

`ForJson`/`ForXml` формируют результат как JSON или XML
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
они взаимоисключающи. См. [Поддержка JSON в разных провайдерах](../18-json.md).

## Табличные функции

`string_split(...)` и `openjson(...)` доступны через
[`FromTableFunction`](xref:NextORM.Core.EntityBuilder`1). См.
[Табличные функции](../13-table-valued-functions.md) и
[Поддержка JSON в разных провайдерах](../18-json.md).

## Temporal-таблицы

`ForSystemTime` запрашивает системно-версионированные (temporal) таблицы
([`SupportsTemporalTable`](xref:NextORM.Core.ISqlDialect.SupportsTemporalTable)): `AS OF`,
`BETWEEN ... AND ...`, `FROM ... TO ...`, `CONTAINED IN (...)` и `ALL` доступны через фабричные методы
[`TemporalClause`](xref:NextORM.Core.TemporalClause).

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1)))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from simple_entity for system_time as of '2020-01-01 00:00:00'
```

MariaDB поддерживает ту же клаузу на системно-версионированных таблицах, кроме `CONTAINED IN`.

## Пока не поддерживается

`CONTAINSTABLE`/`FREETEXTTABLE` (полнотекстовый поиск с колонкой `RANK`, ссылающийся на базовую
таблицу по имени), `PIVOT`/`UNPIVOT` и методы типа XML (`nodes`, `value`, `query`) значатся в
бэклоге. См. [Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер SQL Server](../../providers/sqlserver.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
