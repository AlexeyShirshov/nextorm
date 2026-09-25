# Специфичный для SQL Server SQL

> SQL Server даёт условную функцию `CHOOSE`, библиотеку скаляров, специфичных для T-SQL
> (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `UNICODE`, `NCHAR`, `FORMAT`,
> тригонометрические функции, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID` и
> конструкторы/агрегаты SQL/JSON), хинты инструкции/таблицы и форму результата `FOR JSON`/`FOR XML`,
> методы типа XML (`value`/`query`/`exist` и rowset `nodes`), нативные конструкции источника
> `PIVOT`/`UNPIVOT`, а также табличные функции `string_split`/`openjson`.

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

См. [Скалярные функции](../../scalar-functions/04-conditionals-and-conversion.md#условные-функции).

## Хинты

[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])) привязывает табличный хинт к таблице `FROM`
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
[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])): `.WithTableHint("rowlock").ForUpdate()` даёт
`with (rowlock, updlock)`. См.
[Блокировку строк](../01-querying-and-projections.md#блокировка-строк-for-update--for-share).

## `FOR JSON` и `FOR XML`

`ForJson`/`ForXml` — терминалы: выполняют запрос и возвращают весь набор одним JSON- или
XML-документом
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
`WithForJson`/`WithForXml` присоединяют предложение без выполнения; два вида предложений
взаимоисключающи. См. [Поддержка JSON в разных провайдерах](../18-json.md).

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

Строковый метод `.nodes` — коррелированный источник `CrossApply`/`OuterApply`: `xml_nodes(xml, xpath)`
разворачивает XML-значение в строки (по одной на узел) и рендерит
`<xml>.nodes('xpath') as [alias]([value])`, после чего развёрнутый `IXmlNodesRow.Value` проецируется
скалярными методами выше. Операнд обязан быть колонкой внешней строки, а XQuery — строковым литералом.

```csharp
var rows = dataContext.From<IXmlEntity>()
    .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
    .Select(p => new { Id = SqlFunctions.SqlServer.xml_value<int>(p.Item2.Value, "(.)[1]/@id", "int") })
    .ToList();
```

```sql
select t2.value.value('(.)[1]/@id', 'int') as [Id]
from xml_entity as [t1] cross apply t1.payload.nodes('/root/item') as [t2](value)
```

См. [Скалярные функции](../../scalar-functions/07-json-and-xml.md#методы-типа-xml-sql-server).

## Табличные функции

`string_split(...)` и `openjson(...)` доступны через
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})). См.
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
[`Unpivot`](xref:NextORM.Core.EntityBuilder`1.Unpivot(System.String,System.String,NextORM.Core.UnpivotColumn[])) складывает колонки в строки через `UNPIVOT`
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

[`FromOptions.TableSample`](xref:NextORM.Core.FromOptions.TableSample(System.Double,NextORM.Core.TableSampleMethod,System.Nullable{System.Double})) добавляет модификатор `TABLESAMPLE` к
основной таблице запроса ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample));
необязательный seed делает выборку воспроизводимой. SQL Server рендерит
`tablesample (10 percent)` / `tablesample (10 percent) repeatable (3)`
(`ITableSampleMethods.Render`).

```csharp
var rows = await dataContext.From<ISimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, seed: 3))
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
[Табличные функции](../13-table-valued-functions.md#встроенные-табличные-функции).

## Скаляры T-SQL

`SqlFunctions.SqlServer` открывает скалярные функции, специфичные для T-SQL, через
[`ISqlDialect.SqlServerFunctions`](xref:NextORM.Core.ISqlDialect.SqlServerFunctions)
([`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions)); SQL Server — единственный
провайдер, который её реализует, поэтому все остальные провайдеры для этих членов бросают
`NotSupportedException`. Переносимых аналогов у них нет: кросс-провайдерные `ascii`/`char`/`translate`
живут в [`SqlFunctions.Sql`](../../scalar-functions/01-string-functions.md#кросс-провайдерные-скалярные-функции), а
`Math.Log10` и так рендерит T-SQL `LOG10`.

Функции можно использовать как обычные значения (и как предикаты там, где форма T-SQL — условие):

```csharp
var data = System.Text.Encoding.UTF8.GetBytes("abc");

var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Pos = SqlFunctions.SqlServer.patindex("%a%", e.String),
        Bracketed = SqlFunctions.SqlServer.quotename(e.String),
        Code = SqlFunctions.SqlServer.soundex(e.String),
        Similar = SqlFunctions.SqlServer.difference(e.String, "abc"),
        Escaped = SqlFunctions.SqlServer.string_escape(e.String, "json"),
        CodePoint = SqlFunctions.SqlServer.unicode(e.String),
        Letter = SqlFunctions.SqlServer.nchar(65),
        Number = SqlFunctions.SqlServer.format(e.Id, "D6"),
        Angle = SqlFunctions.SqlServer.acos(0.5),
        Bucket = SqlFunctions.SqlServer.date_bucket("day", 1, e.Datetime),
        Month = SqlFunctions.SqlServer.datename("month", e.Datetime),
        Digest = SqlFunctions.SqlServer.hashbytes("SHA2_256", data),
        Arr = SqlFunctions.SqlServer.json_array("a", e.Id, "b"),
        Obj = SqlFunctions.SqlServer.json_object("id", e.Id),
        HasId = SqlFunctions.SqlServer.json_path_exists(e.String, "$.id")
    })
    .ToList();
```

```sql
select patindex('%a%', somestring) as [Pos],
       quotename(somestring) as [Bracketed],
       soundex(somestring) as [Code],
       difference(somestring, 'abc') as [Similar],
       string_escape(somestring, 'json') as [Escaped],
       unicode(somestring) as [CodePoint],
       nchar(65) as [Letter],
       format(id, 'D6') as [Number],
       acos(0.5) as [Angle],
       date_bucket(day, 1, dt) as [Bucket],
       datename(month, dt) as [Month],
       hashbytes('SHA2_256', @p0) as [Digest],
       json_array('a', id, 'b') as [Arr],
       json_object('id' : id) as [Obj],
       cast(case when json_path_exists(somestring, '$.id') = 1 then 1 else 0 end as bit) as [HasId]
from complex_entity
```

* **Строковые:** `patindex(pattern, expression)`, `quotename(value)` / `quotename(value, quote)`,
  `soundex(value)`, `difference(first, second)`, `string_escape(value, type)`, `unicode(value)`,
  `nchar(code)`, `format(value, format)` / `format(value, format, culture)`. `format` — нативный
  T-SQL `FORMAT`, не путать с трансляцией CLR `string.Format`.
* **Числовые:** `acos`, `asin`, `atan`, `atn2(y, x)`, `square` (`cot`/`degrees`/`radians`/`pi` переносимы — см. [`SqlFunctions.Sql`](../../scalar-functions/02-math-functions.md)).
* **Дата/время:** `datename(datepart, date)` (часть — константа) и
  `date_bucket(datepart, width, date[, origin])` (SQL Server 2022+).
* **Бинарные/системные:** `hashbytes(algorithm, data)` (алгоритм — константа, например `SHA2_256`) и
  `newsequentialid()`; последний допустим только как `DEFAULT` столбца, но не в обычном `SELECT`.
* **JSON:** `json_array(value, ...)` и `json_object(key, value, ...)` (SQL Server 2022+),
  `json_objectagg(key, value)` и `json_arrayagg(value)` (SQL Server 2025+),
  `json_path_exists(json, path)` (SQL Server 2022+) и `json_contains(json, searchValue, path)`
  (SQL Server 2025+). Два предиката материализуются в `bit`, как `isjson`.

## Пока не поддерживается

Открытых специфичных для SQL Server поверхностей не осталось. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер SQL Server](../../providers/sqlserver.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
