# Специфичный для SQL Server SQL

> SQL Server даёт условную функцию `CHOOSE`, библиотеку скаляров, специфичных для T-SQL
> (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `UNICODE`, `NCHAR`, `FORMAT`,
> тригонометрические функции, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID`, семейство
> дат часов/смещения/`*FROMPARTS`, скаляры `CHECKSUM`/`COMPRESS`/`RAND`/`STUFF`, функции метаданных и
> конструкторы/агрегаты SQL/JSON), хинты инструкции/таблицы и форму результата `FOR JSON`/`FOR XML`,
> методы типа XML (`value`/`query`/`exist` и rowset `nodes`), нативные конструкции источника
> `PIVOT`/`UNPIVOT`, а также табличные функции `string_split`/`openjson`.

**Что нужно знать:** [Запросы и проекции](../../engine/index.md) · [Провайдер SQL Server](../../connecting-providers/sqlserver.md)

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
select choose(2, 'a', 'b', 'c') as [Label]
 from complex_entity
```

См. [Скалярные функции](../../scalar-functions/04-conditionals-and-conversion.md#условные-функции).

## Хинты

[`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])) привязывает табличный хинт к таблице `FROM`
запроса ([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)), а хинт запроса
рендерит завершающую клаузу `OPTION (...)`, например `OPTION (RECOMPILE)`
([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints)); параметр CTE
`maxRecursion` отображается в `option (maxrecursion n)`. См.
[Хинты запросов](../15-query-hints.md).

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
select id
 from simple_entity with (updlock)
```

`ForUpdate` рендерит `updlock` (блокировка обновления до конца транзакции), а `ForShare` — `holdlock`
(разделяемая блокировка). Хинт блокировки комбинируется с
[`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])): `dataContext.From<ISimpleEntity>(o => o.WithTableHint("rowlock")).ForUpdate()` даёт
`with (rowlock, updlock)`. См.
[Блокировку строк](../../guide/31-provider-specific-features.md#блокировка-строк-for-update--for-share).

## `FOR JSON` и `FOR XML`

`ForJson`/`ForXml` — терминалы: выполняют запрос и возвращают весь набор одним JSON- или
XML-документом
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
`WithForJson`/`WithForXml` присоединяют предложение без выполнения; два вида предложений
взаимоисключающи. См. [Поддержка JSON в разных провайдерах](../16-json.md).

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
 from xml_entity as [t1]
 cross apply t1.payload.nodes('/root/item') as [t2](value)
```

См. [Скалярные функции](../../scalar-functions/07-json-and-xml.md#методы-типа-xml-sql-server).

## Табличные функции

`string_split(...)` и `openjson(...)` доступны через
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})). См.
[Табличные функции](../13-table-valued-functions.md) и
[Поддержка JSON в разных провайдерах](../16-json.md).

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
select id
 from simple_entity for system_time as of '2020-01-01 00:00:00'
```

MariaDB поддерживает ту же клаузу на системно-версионированных таблицах, кроме `CONTAINED IN`.

## `PIVOT` / `UNPIVOT`

[`EntityBuilder.Pivot`](xref:NextORM.SqlServer.SqlServerEntityBuilderExtensions.Pivot``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PivotAggregate,System.Linq.Expressions.Expression{System.Func{``0,System.Object}},System.Linq.Expressions.Expression{System.Func{``0,System.Object}},NextORM.Core.PivotValue[])) разворачивает источник в колонки
нативным оператором T-SQL `PIVOT`, а
[`Unpivot`](xref:NextORM.SqlServer.SqlServerEntityBuilderExtensions.Unpivot``1(NextORM.Core.EntityBuilder{``0},System.String,System.String,NextORM.Core.UnpivotColumn[])) складывает колонки в строки через `UNPIVOT`
([`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot) /
[`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot)). Это extension-методы в пространстве имён
`NextORM.SqlServer`; добавьте `using NextORM.SqlServer;`, чтобы их использовать. Результат — нетипизированный
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
        Q1 = SqlFunctions.Column<decimal?>(t, "1"),
        Q2 = SqlFunctions.Column<decimal?>(t, "2")
    })
    .ToList();

var stacked = ctx.From<Quarterly>()
    .Unpivot("value", "quarter", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
    .Select(t => new { Quarter = t.GetString("quarter"), Value = t.GetNullableDecimal("value") })
    .ToList();
```

**Сырые имена, всегда безопасно квотируемые.** Строки-идентификаторы этих API — это исходные
неэкранированные имена, а не SQL: `PivotValue.Create` несёт **значение данных** pivot, `UnpivotColumn.Create` —
имя исходной колонки, а сведённые/сложенные колонки результата читаются по имени. Каждое выведенное
вхождение проходит ровно одну стадию квотирования провайдера, независимо от опции контекста
[`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder.UseQuotedIdentifiers(System.Boolean)) —
при выключенном квотировании доступ по имени всё равно рендерит `[1]` в SQL Server / `"1"` в
PostgreSQL и SQLite / `` `1` `` в MySQL и ClickHouse. Точка — литеральный символ имени (никогда не
квалификатор schema/alias), SQL-подобная пунктуация остаётся внутри одного квотированного
идентификатора, а ручное предварительное квотирование **не** поддерживается: `Column(t, "[1]")` — это
литеральное трёхсимвольное имя `[1]` (рендерится `[[1]]]` в SQL Server), поэтому для колонки с именем
`1` пишите `Column(t, "1")` — именно поэтому пример выше читает сведённые колонки через
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)), а не
`GetNullableDecimal("[1]")`. В SQL Server встроенная `]` удваивается, поэтому значение pivot `a]b`
рендерится `[a]]b]` и в списке `IN (...)`, и в ссылке проекции.

Производный запрос может быть источником разворота — тогда во вход `PIVOT` попадают join'ы и
вычисляемые aggregate/`FOR`-колонки (эквивалент разворота CTE):

```csharp
var orderMargins = ctx.From<OrderDetail>()
    .Join<OrderHeader>((d, h) => d.OrderId == h.OrderId)
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
        Q1 = SqlFunctions.Column<decimal?>(t, "1"),
        Q4 = SqlFunctions.Column<decimal?>(t, "4")
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

### Типизированная форма результата

Перегрузки выше возвращают нетипизированный источник [`TableAlias`](xref:NextORM.Core.TableAlias) —
каждая группирующая и каждая сведённая колонка читается по имени. SQL Server также предоставляет
**типизированные** перегрузки, которые объявляют члены результата и CLR-тип каждой сведённой колонки
один раз, а список `IN (...)` выводят из этой проекции:

```csharp
var typed = ctx.From<Sales>()
    .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
        (s, pivot) => new
        {
            s.Category,                       // прямой член внутреннего источника — группирующая колонка
            Q1 = pivot.Value<decimal?>("1"),  // явный CLR-тип ячейки сведённой колонки "1"
            Q2 = pivot.Value<decimal?>("2")
        });

var rows = typed
    .OrderBy(x => x.Q1)                       // типизированные члены доступны в Where/OrderBy/Select
    .Select(x => new { x.Category, x.Q1, x.Q2 })
    .ToList();
```

```sql
select category, [Q1], [Q2]
 from (select category, [1] as [Q1], [2] as [Q2]
 from sales pivot (sum([Margin]) for [Quarter] in ([1], [2])) as [t1]) as [t2]
 order by t2.[Q1]
```

Типизированные члены — это **псевдонимы** над сведёнными колонками
(`Q1 = pivot.Value<decimal?>("1")` рендерит `[1] as [Q1]`), поэтому форма может переименовывать
ячейки и располагать их иначе, чем в списке `IN`. Группирующий член — любой другой прямой член
внутреннего источника (`s.Category`); члены, которые проекция не упоминает, остаются входом
группировки. `Value<T>` требует CLR-тип явно — вывода нет и nullability не выводится автоматически:
используйте `decimal?`/`int?`/`string?`, когда возможны отсутствующее значение или исключённая
`NULL`-ячейка (см. ниже). Разрешено объявить подмножество группирующих членов; оператор всё равно
группирует по всем остальным членам внутреннего источника. Поскольку проекция — производный
источник, она компонуется дальше: `Where`/`OrderBy`/`Select` по результату, ещё один `From(...)` или
`AsCte` для переиспользования как CTE. Как и для любого производного источника, для материализации
нужен явный `Select`.

```csharp
var byQuarter = ctx.From(orderMargins)   // производный запрос (join + вычисляемые Margin/FOR-колонки)
    .Pivot(PivotAggregate.Sum, m => m.Margin, m => m.QuarterNum,
        (m, pivot) => new { m.Category, Q4 = pivot.Value<decimal?>("4") })
    .Select(m => new { m.Category, m.Q4 })
    .ToList();
```

`Unpivot` устроен так же: проекция объявляет две ролевые колонки
(`unpivot.Value<decimal?>("val")`, `unpivot.Name<string>("qtr")`) с теми же идентификаторами, что и
клауза `UNPIVOT ([val] for [qtr] in (...))`, а любой другой прямой член — группирующая колонка.
SQL Server **исключает** ячейку источника со значением `NULL`, поэтому в сведённом результате может
оказаться меньше строк, чем `колонки × строки источника`; объявляйте ячейку значения nullable, чтобы
смоделировать это исключение.

```csharp
var stacked = ctx.From<Quarterly>()
    .Unpivot(
        (m, unpivot) => new
        {
            m.Category,
            Quarter = unpivot.Name<string>("qtr"),
            Value = unpivot.Value<decimal?>("val")
        },
        UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
    .Select(x => new { x.Category, x.Quarter, x.Value })
    .ToList();
```

Строки значений и ролей — **рантайм-идентификаторы** и **сырые имена**: они не проверяются ни против данных, ни против
внутреннего маппинга на этапе компиляции, и каждое выводится как один безопасно квотированный
идентификатор независимо от опции контекста
[`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder.UseQuotedIdentifiers(System.Boolean)) —
точка литеральна, а встроенные скобки не являются предварительно квотированной формой, поэтому
`Value("[1]")` называет колонку `[1]` (рендерится `[[1]]]` в SQL Server), а не молча `1`. Отсутствующее значение PIVOT всё равно рендерит свою
колонку, но даёт SQL `NULL`, поэтому non-nullable ячейка упадёт при материализации — объявляйте её
nullable; неверное имя или несовместимый тип ячейки могут упасть при подготовке или материализации
запроса, а не на компиляции. По-настоящему динамический PIVOT, список значений которого известен
только в рантайме, нельзя выразить типизированной формой — он остаётся на нетипизированной
перегрузке `TableAlias`. Типизированная проекция fail-closed: дублирующееся значение, проекция без
значений, неконстантное имя значения, группирующий член, совпадающий с агрегатом или `FOR`-колонкой,
пустой список колонок UNPIVOT, отсутствующая роль UNPIVOT и конфликтующие идентификаторы ролей
выбрасывают `ArgumentException` (или `NotSupportedException` при чтении внутреннего источника
целиком), а не порождают неверный SQL.

**Варианты и границы.** Типизированные операторы покрывают те же внутренние источники, что и
нетипизированные — обычную таблицу/сущность, **табличную функцию** или производный запрос, поэтому
`ctx.FromTableFunction(() => Db.MyTvf()).Pivot(...)` разворачивает вызов функции так же, как таблицу.
Строки значений остаются **рантайм-идентификаторами** (сырыми именами, всегда безопасно квотируемыми, как выше): опечатка или отсутствующее значение не
проверяются на этапе компиляции, поэтому её колонка всё равно рендерится, а ячейка, которой не
соответствует ни одна строка источника, даёт SQL `NULL` (объявляйте ячейку nullable). По-настоящему
**динамический PIVOT** — список значений, известный только в рантайме — не имеет типизированной формы и
остаётся на нетипизированной перегрузке [`TableAlias`](xref:NextORM.Core.TableAlias), которая не
менялась. Оба оператора сохраняют гвард eager-load: вызов на builder'е с состоянием
`LoadWith`/single-query бросает `NotSupportedException` (композиция его не переживает), а чередующиеся
типизированные запросы не делят между собой залипшее состояние команды или кэша планов.

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
select id
 from simple_entity tablesample (10 percent) repeatable (3)
```

SQL Server поддерживает только метод `System`
(`TableSample`); `Bernoulli`
выбрасывает `NotSupportedException`. См.
[Сэмплирование таблицы](../../guide/01-query-sources.md#сэмплирование-таблицы-tablesample).

## Полнотекстовый поиск

Кросс-провайдерные boolean-предикаты `contains`/`freetext` рендерят `CONTAINS`/`FREETEXT`. Для
ранжирования `SqlFunctions.SqlServer.containstable`/`freetexttable` отдают ключ совпавшей строки и
оценку `RANK` через `SqlFunctions.IKeyRankRow<TKey>`; см.
[Табличные функции](../13-table-valued-functions.md#встроенные-табличные-функции).

## Последовательности

`SqlFunctions.SqlServer.next_value_for(sequence)` и его псевдоним `nextval(sequence)` рендерят
T-SQL-выражение `NEXT VALUE FOR` и возвращают CLR `long`. Имя должно быть константной строкой,
записанной одной компонентой (`name`) или двумя (`schema.name`); компоненты квотируются квадратными
скобками отдельно, а встроенная `]` удваивается, поэтому текст имени не может выйти за пределы
идентификатора:

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new { Next = SqlFunctions.SqlServer.next_value_for("dbo.order_seq") })
    .ToList();
```

```sql
select next value for [dbo].[order_seq] as [Next]
 from order_entity
```

Не-константные или захваченные имена (колонка, захваченная локальная переменная, `null`/`default`)
бросают `NotSupportedException` до генерации SQL, и имя никогда не привязывается как параметр. Сама
последовательность — обычный объект базы, создаваемый через DDL; открыто только её следующее
значение (без сессионных `currval`/`lastval` и без возвращающего значение `setval`). См.
[Последовательности](../../scalar-functions/10-sequences.md#sql-server).

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
  `nchar(code)`, `format(value, format)` / `format(value, format, culture)` и
  `stuff(value, start, length, newValue)` (только строка — T-SQL `STUFF` неявно преобразует бинарные аргументы в символьные, поэтому перегрузка `byte[]` не открыта). `format` — нативный
  T-SQL `FORMAT`, не путать с трансляцией CLR `string.Format`; `str(value[, length[, decimals]])` —
  преобразование числа с плавающей точкой в строку фиксированной ширины. Апостроф как разделитель —
  обычный строковый аргумент: `quotename("abc", "'")` рендерится в `quotename('abc', '''')`, а сервер
  возвращает `'abc'`. Апострофы внутри значения удваиваются в сгенерированном литерале (`O'Brien`
  превращается в `'O''Brien'`), поэтому значение передаётся как есть и никогда не экранируется
  вызывающим кодом заранее.
* **Числовые:** `acos`, `asin`, `atan`, `atn2(y, x)`, `square` (`cot`/`degrees`/`radians`/`pi` переносимы — см. [`SqlFunctions.Sql`](../../scalar-functions/02-math-functions.md)) и `rand()` / `rand(seed)` (без seed — недетерминирован).
* **Дата/время:** `datename(datepart, date)` (часть — константа), `date_bucket(datepart, width, date[, origin])` (SQL Server 2022+), серверные часы `sysdatetime`/`sysdatetimeoffset`/`sysutcdatetime`, функции смещения `switchoffset(value, offset)`/`todatetimeoffset(value, offset)` (строка вида `"+02:00"` или минуты со знаком) и семейство конструкторов `timefromparts`/`smalldatetimefromparts`/`datetimefromparts`/`datetime2fromparts`/`datetimeoffsetfromparts`. Завершающая точность `*FROMPARTS` должна быть константой `0..7` (неконстантное, `null` или выходящее за диапазон значение бросает исключение до генерации SQL).
* **Бинарные/системные:** `hashbytes(algorithm, data)` (алгоритм — константа, например `SHA2_256`), `newsequentialid()` (допустим только как `DEFAULT` столбца, но не в обычном `SELECT`), `checksum(values...)`/`binary_checksum(values...)` (минимум одно значение; форма `checksum(*)` не открыта) и `compress(value)`/`decompress(value)` (GZIP над `varbinary(max)`).
* **Метаданные:** `col_length`, `col_name`, `ident_incr`/`ident_seed`, `index_col`, `object_definition`/`object_id`/`object_name`/`object_schema_name`, `stats_date`, `db_id`/`db_name`/`original_db_name`, `schema_id`/`schema_name`, `type_id`/`type_name`, `filegroup_id`/`filegroup_name`/`file_id`/`file_idex`/`file_name`, `current_timezone`/`current_timezone_id`, `getansinull`, `parsename` и `publishingservername`. Отсутствующий объект/столбец/индекс/статистика даёт `null`, как и нативная функция.
* **Преобразование/логика:** `isdate(value)`/`isnumeric(value)` возвращают нативный T-SQL `int` (1/0), а не `bit`, поэтому остаются целочисленными значениями, а не материализованными предикатами; `formatmessage(messageOrId, args...)` принимает не более 20 аргументов форматирования.
* **JSON:** `json_array(value, ...)` и `json_object(key, value, ...)` (SQL Server 2022+),
  `json_objectagg(key, value)` и `json_arrayagg(value)` (SQL Server 2025+),
  `json_path_exists(json, path)` (SQL Server 2022+) и `json_contains(json, searchValue, path)`
  (SQL Server 2025+). Два предиката материализуются в `bit`, как `isjson`.

Семь имён, сообщающих живое изменяемое состояние соединения, сессии или инструкции, намеренно **не**
открыты: `CURRENT_REQUEST_ID`, `CURRENT_TRANSACTION_ID`, `XACT_STATE`, `IDENT_CURRENT`,
`MIN_ACTIVE_ROWVERSION`, `ROWCOUNT_BIG` и `SCOPE_IDENTITY`; стабильные конфигурационные константы и
константы идентичности той же области (`CURRENT_TIMEZONE`/`CURRENT_TIMEZONE_ID`, `SERVER_NAME`,
`HOST_ID`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) открыты. Используйте сырой SQL или
обёртку `[SqlFunction]` при появлении конкретного построчного применения.

## Пока не поддерживается

Открытых специфичных для SQL Server поверхностей не осталось. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер SQL Server](../../connecting-providers/sqlserver.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs`.
