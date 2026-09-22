# Специфичный для SQL Server SQL

> SQL Server даёт условную функцию `CHOOSE`, хинты инструкции/таблицы и форму результата `FOR JSON`/
> `FOR XML`, скалярные методы типа XML (`value`/`query`/`exist`), нативные конструкции источника
> `PIVOT`/`UNPIVOT`, а также табличные функции `string_split`/`openjson`. `CONTAINSTABLE` и
> строковый метод `.nodes` — оставшиеся поверхности SQL Server, пока не входящие в nextorm.

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

## Блокировка строк

В SQL Server нет завершающего предложения `FOR UPDATE`, поэтому
[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) и
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) привязывают табличный хинт
`updlock`/`holdlock` к основной таблице запроса
([`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints), `ILockRenderer.Render`):

```csharp
var rows = dataContext.From<ISimpleEntity>().ForUpdate().Select(e => e.Id).ToList();
```

```sql
select id from simple_entity with (updlock)
```

`ForUpdate` рендерит `updlock` (блокировка обновления до конца транзакции), а `ForShare` — `holdlock`
(разделяемая блокировка). Хинт блокировки комбинируется с
[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1): `.WithTableHint("rowlock").ForUpdate()` даёт
`with (rowlock, updlock)`. См.
[Блокировку строк](../01-querying-and-projections.md#блокировка-строк-for-update--for-share).

## `FOR JSON` и `FOR XML`

`ForJson`/`ForXml` формируют результат как JSON или XML
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
они взаимоисключающи. См. [Поддержка JSON в разных провайдерах](../18-json.md).

## Методы типа XML

`SqlFunctions.SqlServer.xml_value<T>(xml, xpath, sqlType)`, `xml_query(xml, xpath)` и
`xml_exist(xml, xpath)` рендерят постфиксные методы T-SQL
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)); XQuery и
SQL-тип обязаны быть строковыми литералами, а `xml_exist` возвращает `bit` (в предикатном контексте
сравнивается с `1`).

```csharp
var rows = dataContext.From<IXmlEntity>()
    .Select(x => new
    {
        Value = SqlFunctions.SqlServer.xml_value<string>(x.Payload, "(/root/item)[1]", "nvarchar(100)"),
        Exists = SqlFunctions.SqlServer.xml_exist(x.Payload, "/root/item[2]")
    })
    .ToList();
```

```sql
select payload.value('(/root/item)[1]', 'nvarchar(100)') as [Value],
       payload.exist('/root/item[2]') as [Exists]
from xml_entity
```

Строковый метод `.nodes` не поддерживается: ему нужна внешняя ссылка в `FROM`/`CROSS APPLY`.

См. [Скалярные функции](../11-scalar-functions.md#методы-типа-xml).

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

## `PIVOT` / `UNPIVOT`

[`EntityBuilder<TEntity>.Pivot`](xref:NextORM.Core.EntityBuilder`1) разворачивает источник в колонки
нативным оператором T-SQL `PIVOT`, а
[`Unpivot`](xref:NextORM.Core.EntityBuilder`1) складывает колонки в строки через `UNPIVOT`
([`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot) /
[`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot)). Результат — нетипизированный
источник: группирующие колонки и колонки-значения выбираются по имени через `TableAlias`. Операторы
применяются к табличному выражению, поэтому источником может быть простая таблица/сущность,
табличная функция или производный запрос (`ctx.From(derivedQuery)`); фильтры и прочие модификаторы
применяются к результату (а для производного источника — внутри самого запроса).

```csharp
var rows = ctx.From<Sales>()
    .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"), PivotValue.Create("2"))
    .Select(t => new
    {
        Category = t.GetString("category"),
        Q1 = t.GetNullableDecimal("[1]"),
        Q2 = t.GetNullableDecimal("[2]")
    })
    .ToList();

var stacked = ctx.From<Quarterly>()
    .Unpivot("value", "quarter", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
    .Select(t => new { Quarter = t.GetString("quarter"), Value = t.GetNullableDecimal("value") })
    .ToList();
```

Производный запрос может быть источником разворота — тогда во вход `PIVOT` попадают join'ы и
вычисляемые aggregate/`FOR`-колонки (эквивалент разворота CTE):

```csharp
var orderMargins = ctx.From<OrderDetail>()
    .Join(ctx.From<OrderHeader>(), (d, h) => d.OrderId == h.OrderId)
    .Where(p => SqlFunctions.Sql.extract("year", p.Item2.OrderDate) == 2013)
    .Select(p => new
    {
        CategoryName = p.Item3.Name,
        QuarterNum = SqlFunctions.Sql.extract("quarter", p.Item2.OrderDate),
        Margin = p.Item1.LineTotal - p.Item3.StandardCost * p.Item1.OrderQty
    });

var byQuarter = ctx.From(orderMargins)
    .Pivot(PivotAggregate.Sum, m => m.Margin, m => m.QuarterNum,
        PivotValue.Create("1"), PivotValue.Create("2"), PivotValue.Create("3"), PivotValue.Create("4"))
    .OrderBy(t => t.GetString("CategoryName"))
    .Select(t => new
    {
        Category = t.GetString("CategoryName"),
        Q1 = t.GetNullableDecimal("[1]"),
        Q4 = t.GetNullableDecimal("[4]")
    })
    .ToList();
```

```sql
select CategoryName, [1], [4]
from (select name as [CategoryName], datepart(quarter, OrderDate) as [QuarterNum],
             (LineTotal - StandardCost * OrderQty) as [Margin]
      from sales_order_detail
      join sales_order_header on sales_order_detail.OrderId = sales_order_header.OrderId
      where datepart(year, OrderDate) = 2013) as [t1]
pivot (sum([Margin]) for [QuarterNum] in ([1], [2], [3], [4])) as [t2]
order by CategoryName
```

## `TABLESAMPLE`

[`TableSample`](xref:NextORM.Core.EntityBuilder`1) добавляет модификатор `TABLESAMPLE` к
основной таблице запроса ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample));
необязательный seed делает выборку воспроизводимой. SQL Server рендерит
`tablesample (10 percent)` / `tablesample (10 percent) repeatable (3)`
(`ITableSampleMethods.Render`).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .TableSample(10, TableSampleMethod.System, seed: 3)
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id from simple_entity tablesample (10 percent) repeatable (3)
```

SQL Server поддерживает только метод `System`
(`TableSample`); `Bernoulli`
выбрасывает `NotSupportedException`. См.
[Сэмплирование таблицы](../01-querying-and-projections.md#сэмплирование-таблицы-tablesample).

## Полнотекстовый поиск

Кросс-провайдерные boolean-предикаты `contains`/`freetext` рендерят `CONTAINS`/`FREETEXT`. Для
ранжирования `SqlFunctions.SqlServer.containstable`/`freetexttable` отдают ключ совпавшей строки и
оценку `RANK` через `SqlFunctions.IKeyRankRow<TKey>`; см.
[Табличные функции](../13-table-valued-functions.md#built-in-table-functions).

## Пока не поддерживается

Строковый метод `.nodes` (нужна внешняя ссылка в `FROM`/`CROSS APPLY`) значится в бэклоге. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер SQL Server](../../providers/sqlserver.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
