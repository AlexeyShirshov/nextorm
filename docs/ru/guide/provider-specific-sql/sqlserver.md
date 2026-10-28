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
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})) с вызовом runtime-фабрики. См.
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
`ctx.From(() => Db.MyTvf()).Pivot(...)` разворачивает вызов функции так же, как таблицу.
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

## Версионно-зависимые функции (2022 и 2025)

Имена [#282](https://github.com/AlexeyShirshov/nextorm/issues/282) ниже закрыты **объявленной
мажорной версией сервера**. Гейт версионный: в отличие от нативного типа хранения JSON
(`SupportsJson`, [#345](https://github.com/AlexeyShirshov/nextorm/issues/345)), который также учитывает
allowlist edition, потому что некоторые продукты Azure/Fabric имеют этот тип, сообщая меньшую мажорную
версию, доступность функций следует самой мажорной версии движка. Объявление edition здесь никогда не
подменяет версию, а неверсионированный контекст отклоняет их до SQL.

| Член | SQL | Требование |
|---|---|---|
| `SqlFunctions.SqlServer.left_shift` / `right_shift` / `bit_count` / `get_bit` / `set_bit` (набор перегрузок по типу операнда `byte`/`short`/`int`/`long`) | `CASE WHEN … THEN CAST(NULL AS <тип операнда>) ELSE left_shift(cast(value as tinyint\|smallint\|int\|bigint), cast(shift as bigint)) END`; `bit_count` → `bigint`, `get_bit` → `bit` | мажорная версия 16 (SQL Server 2022) |
| `SqlFunctions.SqlServer.regexp_count(input, pattern)` | `regexp_count(input, pattern)` | мажорная версия 17 (SQL Server 2025); уровень совместимости не требуется |
| `SqlFunctions.SqlServer.regexp_substr(input, pattern[, start[, occurrence[, match_type]]])` / `regexp_instr(input, pattern[, start[, occurrence[, return_option[, match_type]]]])` | нативный вызов; `0` для `regexp_instr` без совпадения; match type — аргумент `varchar` и рендерится как `cast(… as varchar(4000))` (аргумент `nvarchar` даёт Msg 8116) | мажорная версия 17; уровень совместимости не требуется |
| `SqlFunctions.SqlServer.regexp_matches(input, pattern[, match_type])` | источник `FROM`; строки — `match_id bigint`, `start_position`/`end_position int`, `match_value`, `substring_matches` (полный JSON-текст захватов) | мажорная версия 17 **и** уровень совместимости БД 170 |
| `SqlFunctions.SqlServer.edit_distance(input, pattern[, maximum_distance])` / `edit_distance_similarity` / `jaro_winkler_distance` / `jaro_winkler_similarity` | нативный вызов; ограниченная форма `edit_distance` возвращает `maximum_distance + 1`, когда расстояние больше предела | мажорная версия 17 **и** уровень совместимости 170 **и** `PREVIEW_FEATURES = ON` **и** Windows-коллация |
| `SqlFunctions.SqlServer.product(value)` | `product(value)` (только агрегатный контекст — без `OVER`) | мажорная версия 17 |
| `SqlFunctions.SqlServer.regexp_split_to_table(input, pattern)` | источник `FROM`; строки — `value` и one-based `ordinal bigint` | мажорная версия 17 **и** уровень совместимости БД 170 |
| `SqlFunctions.SqlServer.approx_count_distinct(value)` | `approx_count_distinct(value)` (агрегатный контекст; `0` для пустой группы и группы из NULL) | мажорная версия 15 (SQL Server 2019) |
| `SqlFunctions.SqlServer.checksum_agg(value)` / `grouping_id(expressions...)` | `checksum_agg(value)`; `grouping_id(a, b, …)` в списке `SELECT`, в `HAVING` или в `ORDER BY` сгруппированной команды | версионного порога нет; `GROUPING_ID` требует контекста `GROUP BY`, и каждый аргумент должен быть выражением группировки (оба условия проверяются до SQL) |
| `SqlFunctions.SqlServer.approx_percentile_cont(fraction, value)` / `approx_percentile_disc(fraction, value)` | `approx_percentile_cont(f) within group (order by value)`; форма `_disc` возвращает тип входного выражения сортировки | мажорная версия 16 (SQL Server 2022); формы `OVER` нет |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Count = SqlFunctions.SqlServer.regexp_count(e.String, "[0-9]"),
        Shifted = SqlFunctions.SqlServer.left_shift(e.Int, 1),
        Bits = SqlFunctions.SqlServer.bit_count(e.Int),
        Bit = SqlFunctions.SqlServer.get_bit(e.Int, 1),
        Set = SqlFunctions.SqlServer.set_bit(e.Int, 1, false)
    })
    .ToList();

var fragments = dataContext
    .From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b,c", ","))
    .Select(r => new { r.Value, r.Ordinal }) // value + ordinal bigint
    .ToList();
```

Контекст, объявленный для 15, 16 или 17, разрешает ровно те строки, которые перечисляет его мажорная
версия; всё, что ниже или не версионировано, отклоняется **до SQL**, например:

```text
The LEFT_SHIFT function requires SQL Server major version 16 or later; declared major version: 15.
The REGEXP_SPLIT_TO_TABLE function requires SQL Server major version 17 or later; declared major version: 16.
```

Битовое семейство типизирует каждый аргумент внутри вызова и закрывает NULL-операнд, сдвиг, смещение или
значение бита типизированным NULL, потому что «голый» `left_shift(NULL, …)` даёт Msg 8116; тип самого
операнда выбирает `tinyint`/`smallint`/`int`/`bigint`, `bit_count` возвращает `bigint` (`long?`), а
`get_bit` — `bit` (`bool?`). `right_shift` — логический (zero-fill) сдвиг движка: `right_shift(-8, 1)`
равно `2147483644`. Поскольку семейство — набор перегрузок по типу операнда, нетипизированная константа
привязывается к наименьшей перегрузке (`left_shift(1, 2)` → `byte?`/`tinyint`, `left_shift(-8, 1)` →
`short?`): приводите операнд (`left_shift((int?)1, 2)`), когда нужен конкретный тип SQL.
`regexp_split_to_table` — реальный гейт по двум осям: на уровне совместимости 160 движок мажорной версии
17 сообщает, что функции нет (Msg 208), поэтому условие 170 проверяется вживую, а не предполагается.
`product` предлагается только в агрегатном контексте.

Regex-скаляры U3 и fuzzy-семейство выполнены на протестированной базовой сборке `17.0.5005.3`:
`regexp_substr`/`regexp_instr` работают на уровнях совместимости **160 и 170**, тогда как
`regexp_matches` исчезает на 160 (Msg 208), а четырём fuzzy-именам нужны совместимость 170,
`PREVIEW_FEATURES = ON` (по умолчанию выключен) и Windows-коллация (коллация `SQL_*` даёт Msg 9847, а
отсутствие preview-флага — Msg 195). Это рантайм-предусловия, которые диалект не может прочитать,
поэтому они документируются и проверяются вживую; версионный флаг обещает только гейт мажорной версии,
и обещаний для будущих сборок не даётся.

Семейство агрегатов U4 существует только в агрегатном контексте. `approx_count_distinct` — это `long`
(`bigint`), и для пустой группы и группы из NULL он считает `0`, а не `NULL`; `checksum_agg` — `int?` и
возвращает там `NULL`; приблизительные квантили — `double?` и тип входного выражения сортировки
соответственно, и тоже возвращают `NULL`. Постоянный NULL-операнд рендерится как типизированный
`CAST(NULL AS <тип>)` («голый» `NULL` даёт Msg 8117); дробь квантиля должна быть независимым от строки
литералом или захваченной переменной в `[0, 1]`, зависящая от строки дробь отклоняется до SQL, а
литеральный или NULL-ключ `WITHIN GROUP` отклоняется до SQL, потому что движок отвечает Msg 5309.
Обязанности `GROUPING_ID` по контексту группировки и по списку группировки проверяются до выполнения при
нуле отправленных операторов; ни у одного приблизительного квантиля нет перегрузки `OVER`
(упорядоченные агрегаты, только `WITHIN GROUP`), а форма `OVER`, сконструированная другим способом,
отвергается движком с Msg 4113 (`checksum_agg` — единственное имя, чью нативную форму `OVER` движок
принимает, но здесь предоставляется только агрегатная форма). Оба квантиля и подсчёт
различных значений приблизительны: вендор документирует ошибку ранга около 1,33 % при 99 % доверия для
квантилей, и это статистическая граница, а не детерминированный допуск.

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
  обычный строковый аргумент: `quotename("abc", "'")` рендерится в `quotename(N'abc', N'''')`, а сервер
  возвращает `'abc'`. Текстовые литералы — юникодные литералы `N'…'` с удвоенными внутренними
  апострофами (`O'Brien` превращается в `N'O''Brien'`), а значение, содержащее символ NUL, рендерится как
  составное выражение над `NCHAR(0)`, а не как один литерал, поэтому значение передаётся как есть и никогда
  не экранируется вызывающим кодом заранее. Этот абзац описывает только SQL Server и не является
  кросс-провайдерной гарантией.
* **Числовые:** `acos`, `asin`, `atan`, `atn2(y, x)`, `square` (`cot`/`degrees`/`radians`/`pi` переносимы — см. [`SqlFunctions.Sql`](../../scalar-functions/02-math-functions.md)) и `rand()` / `rand(seed)` (без seed — недетерминирован).
* **Дата/время:** `datename(datepart, date)` (часть — константа), `date_bucket(datepart, width, date[, origin])` (SQL Server 2022+), серверные часы `sysdatetime`/`sysdatetimeoffset`/`sysutcdatetime`, функции смещения `switchoffset(value, offset)`/`todatetimeoffset(value, offset)` (строка вида `"+02:00"` или минуты со знаком) и семейство конструкторов `timefromparts`/`smalldatetimefromparts`/`datetimefromparts`/`datetime2fromparts`/`datetimeoffsetfromparts`. Завершающая точность `*FROMPARTS` должна быть константой `0..7` (неконстантное, `null` или выходящее за диапазон значение бросает исключение до генерации SQL).
* **Бинарные/системные:** `hashbytes(algorithm, data)` (алгоритм — константа, например `SHA2_256`), `newsequentialid()` (допустим только как `DEFAULT` столбца, но не в обычном `SELECT`), `checksum(values...)`/`binary_checksum(values...)` (минимум одно значение; форма `checksum(*)` не открыта) и `compress(value)`/`decompress(value)` (GZIP над `varbinary(max)`).
* **Метаданные:** `col_length`, `col_name`, `ident_incr`/`ident_seed`, `index_col`, `object_definition`/`object_id`/`object_name`/`object_schema_name`, `stats_date`, `db_id`/`db_name`/`original_db_name`, `schema_id`/`schema_name`, `type_id`/`type_name`, `filegroup_id`/`filegroup_name`/`file_id`/`file_idex`/`file_name`, `current_timezone`/`current_timezone_id`, `getansinull`, `parsename` и `publishingservername`. Отсутствующий объект/столбец/индекс/статистика даёт `null`, как и нативная функция.
* **Живое состояние:** `current_request_id()` (`short?`, NULL вне запроса), `current_transaction_id()`
  (`long?`, NULL вне транзакции), `xact_state()` (нативный `smallint` 0/1/-1), `ident_current(table)`
  (`decimal?`, последняя identity таблицы в **любой** сессии — гонка, не API последней вставки),
  `min_active_rowversion()` (нативный `binary(8)`, не счётчик), `rowcount_big()` (предыдущая инструкция
  того же соединения) и `scope_identity()` (`decimal?`, только в том же батче, что и `INSERT`).
* **Преобразование/логика:** `isdate(value)`/`isnumeric(value)` возвращают нативный T-SQL `int` (1/0), а не `bit`, поэтому остаются целочисленными значениями, а не материализованными предикатами; `formatmessage(messageOrId, args...)` принимает не более 20 аргументов форматирования. `try_cast<T>(value)`/`try_convert<T>(value[, style])` (плюс перегрузки с целью `string` поверх `nvarchar(4000)`) рендерят T-SQL `TRY_CAST`/`TRY_CONVERT` через **закрытое** отображение CLR→SQL и не версионно-зависимы: неверное значение даёт SQL NULL, NULL-стиль даёт SQL NULL даже для конвертируемого значения, а цель вне отображения отклоняется до SQL. См. [TRY_CAST и TRY_CONVERT](../../scalar-functions/04-conditionals-and-conversion.md#sql-server-try_cast-и-try_convert).

Переносимые BASE64 и crypto на этом провайдере требуют SQL Server 2025: `SqlFunctions.Sql.base64_encode` рендерится как `BASE64_ENCODE(value)` над `varbinary`, `base64_decode` — как `BASE64_DECODE(value)` над `varchar` (столбец `nvarchar` нужно привести), а `crypt_gen_random` рендерит типизированную обёртку `CASE WHEN length IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(length) END`, поэтому NULL-длина остаётся типизированным NULL. Compatibility level и preview-флаг не нужны.
* **JSON:** `json_array(value, ...)` и `json_object(key, value, ...)` (SQL Server 2022+),
  `json_objectagg(key, value)` и `json_arrayagg(value)` (SQL Server 2025+),
  `json_path_exists(json, path)` (SQL Server 2022+) и `json_contains(json, searchValue, path)`
  (SQL Server 2025+). Два предиката материализуются в `bit`, как `isjson`.

Живое изменяемое состояние **не** является критерием исключения: семь имён, сообщающих живое
состояние соединения, транзакции, сессии или инструкции, открыты как есть, с нативной семантикой SQL
Server и оговорками выше. nextorm не гарантирует построчную стабильность, непрерывность соединения
между запросами и получение identity — отдельный вызов `SCOPE_IDENTITY`/`IDENT_CURRENT` не является
API последней вставки, поэтому для ключа используйте `ReturningIdentity<TKey>()`. Стабильные
конфигурационные константы и константы идентичности той же области (`CURRENT_TIMEZONE`/
`CURRENT_TIMEZONE_ID`, `SERVER_NAME`, `HOST_ID`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`,
`ORIGINAL_LOGIN`) остаются на той же поверхности.

## Null-safe-сравнение

В нижней границе поддерживаемых версий SQL Server нет `IS [NOT] DISTINCT FROM`, поэтому оба помощника
эмулируются поверх обычного равенства всегда двузначным охраняемым предикатом:
`((a is not null and b is not null and a = b) or (a is null and b is null))`, а `is_distinct_from` —
его дополнение `not (...)`. Логическая проекция легализуется как
`cast(case when ... then 1 else 0 end as bit)`. Поскольку операнды сравниваются дважды, захваченному
null-операнду нужен SQL-тип, выведенный из метаданных; операнд без маппинга отклоняется до выполнения с
`Null-safe comparison requires a SQL Server type mapping for null operands.` См.
[Null-safe-сравнение](../../scalar-functions/04-conditionals-and-conversion.md#null-safe-сравнение-is-not-distinct-from).

## Отклонённые функции (детерминированно, до SQL)

[#282](https://github.com/AlexeyShirshov/nextorm/issues/282) закрыт для этого среза:
**22 поставлено / 0 имён поставки осталось / 11 протестированных отказов**. Одиннадцать отказов — часть
поверхности: вызываемые члены `SqlFunctions.SqlServer`, бросающие `NotSupportedException` с одним
стабильным сообщением на член
(`SqlFunctions.SqlServer.<member> (<TOKEN>) is refused: <reason>`) на границе команды SQL-билдера, до
генерации SQL и до создания любой команды. Это не отложенные имена: это детерминированные,
протестированные отказы, и каждый сохраняет рендеринг поддерживаемых соседей той же поверхности
(`rowcount_big`, `xact_state`, `scope_identity`, `db_name`, `current_user`, …).

| Член | Токен SQL | Вид и причина |
|---|---|---|
| `session_context(key)` | `SESSION_CONTEXT` | Область/CLR: возвращает `sql_variant`, для которого у nextorm нет достоверного контракта результата CLR; замена с потерями (lossy cast) не выполняется. |
| `session_id()` | `SESSION_ID` | Отклонение движком: не является функцией движка SQL Server (Msg 195); документированная функция принадлежит Azure Synapse. Идентичность сессии — `@@SPID` или `sys.dm_exec_sessions.session_id`; подстановка не выполняется. |
| `rowcount()` | `@@ROWCOUNT` | Область: описывает предыдущую инструкцию и привязан к инструкции; построчная проекция запроса не может достоверно отдать это значение. |
| `error()` | `@@ERROR` | Область: та же область предыдущей инструкции, что у `@@ROWCOUNT`. |
| `error_number()` | `ERROR_NUMBER` | Область: требует области блока `CATCH`; вне `CATCH` возвращает NULL, а nextorm не создаёт эту область. |
| `error_severity()` | `ERROR_SEVERITY` | Область: та же область только `CATCH`. |
| `error_state()` | `ERROR_STATE` | Область: та же область только `CATCH`. |
| `error_line()` | `ERROR_LINE` | Область: та же область только `CATCH`. |
| `error_message()` | `ERROR_MESSAGE` | Область: та же область только `CATCH`. |
| `error_procedure()` | `ERROR_PROCEDURE` | Область: та же область только `CATCH`. |
| `any_value(value)` | `ANY_VALUE` | Отклонение движком: сборка SQL Server `17.0.5005.3` отклонила её с `Msg 195` в агрегатной форме и в форме `OVER ()`, на совместимости 160/170 и при `PREVIEW_FEATURES = ON`; поддерживаемый контракт выполнения для SQL Server не установлен. |

Два вида остаются различимыми: десять системных имён — это **ограничения области/CLR** (область
инструкции или `CATCH`, либо `sql_variant` без достоверного носителя CLR), кроме `SESSION_ID`, который —
как и `ANY_VALUE` — является **наблюдаемым отклонением движком**. `ANY_VALUE` — не версионный пробел,
который закроет будущий релиз nextorm, а переносимый `CommonFunctions.any_agg` не затронут на
провайдерах, которые его включают. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md) и
[системные функции](../../scalar-functions/09-system-functions.md#отклонённые-имена-инструкций-и-области-ошибок-sql-server).

## См. также

* [Провайдер SQL Server](../../connecting-providers/sqlserver.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs`.
