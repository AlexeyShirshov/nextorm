# Провайдер SQL Server

> Используйте `nextorm.sqlserver` для Microsoft SQL Server и Azure SQL; он отрисовывает параметры `@name`, разбиение на страницы `top(n)` / `offset … fetch` (внедряя `ORDER BY`, когда это необходимо), конкатенацию `+`, `isnull`, `datepart` и `count_big`, с идентификаторами в квадратных скобках.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) (`src/nextorm.sqlserver/SqlServerDataContext.cs`) оборачивает `Microsoft.Data.SqlClient`. Он
создаёт `SqlConnection`, передаёт значения параметров null как `DBNull` (иначе SqlClient не отправляет значение
вообще), и переопределяет `MapColumnExpression`, чтобы читать числовые столбцы без боксинга: типизированные
геттеры SqlClient строги к расширению, а storage-тип столбца известен только после открытия reader'а, поэтому
маппер в рантайме разрешает `IDataRecord.GetFieldType` и читает соответствующим storage-типизированным
геттером, конвертируя типизированным `Convert.To<T>` (маппинг `GetNumericGetter`/`GetTypedConversion`,
общий с терминалом CSV). Только storage-тип вне закрытого числового набора откатывается к
`GetValue`/`Convert.ChangeType`.

[`SqlServerDialect`](xref:NextORM.SqlServer.SqlServerDialect) (`src/nextorm.sqlserver/SqlServerDialect.cs`) — это диалект:

- плейсхолдер параметра `@name`;
- идентификаторы заключаются в квадратные скобки ([`Escape`](xref:NextORM.Core.ISqlDialect.Escape(System.String)) возвращает `[name]`), включая ссылки на столбцы, чтобы псевдонимы, которые
  конфликтуют с ключевыми словами T-SQL, оставались пригодными;
- отображение типов: `byte`→`tinyint`, `short`→`smallint`, `int`→`int`, `long`→`bigint`, `float`→`real`,
  `double`→`float`, `decimal`→`decimal(38, 10)`;
- длина строки — `len(...)`, части даты — `datepart(part, value)`, `DateTime.Now`/`UtcNow` —
  `getdate()`/`getutcdate()`;
- `Math.Truncate` становится `round(x, 0, 1)`, а `Math.Round` подставляет отсутствующий аргумент длины:
  `round(x, 0)`;
- в T-SQL нет логического типа, поэтому выражение с логическим значением, используемое как значение, материализуется как
  `cast(case when ... then 1 else 0 end as bit)`; в контексте условия логический `CASE` сравнивается
  с `1`.

Наконец, в SQL Server нет ключевого слова `RECURSIVE` (рекурсивные CTE объявляются только с `with`) и
предоставляется `option (maxrecursion n)` для повышения глубины по умолчанию.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.SqlServer;

var byString = new DataContextBuilder()
    .UseSqlServer("Server=localhost;Database=app;Trusted_Connection=True;TrustServerCertificate=True");

using var connection = new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Database=app;...");
var byConnection = new DataContextBuilder().UseSqlServer(connection);

using var ctx = byString.CreateDataContext();   // IDataContext
```

Напрямую:

```csharp
using NextORM.Core;
using NextORM.SqlServer;

using IDataContext ctx = new SqlServerDataContext("Server=localhost;Database=app;...", new DataContextBuilder());
```

## Разбиение на страницы: `TOP` и `OFFSET … FETCH`

```csharp
ctx.From<ISimpleEntity>().Limit(5).Select(x => x.Id);
// select top(5) id
// from simple_entity

ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);
// select id
// from simple_entity order by (select null as anyorder)
// offset 10 rows
// fetch next 5 rows only
```

SQL Server отклоняет `OFFSET`/`FETCH` без `ORDER BY`, поэтому когда у запроса с разбиением на страницы нет сортировки, диалект
внедряет постоянную сортировку `(select null as anyorder)` ([`GetPagingOrderBy`](xref:NextORM.Core.ISqlDialect.GetPagingOrderBy(NextORM.Core.QueryCommand))). Запрос только с offset выдаёт
`offset m rows` и без `fetch`. Когда у запроса уже есть `ORDER BY`, ничего не внедряется.

```csharp
ctx.From<ISimpleEntity>().Offset(10).OrderBy(x => x.Id).Select(x => x.Id);
// ... order by id offset 10 rows
```

`WITH TIES` поддерживается только в форме `TOP(n) WITH TIES` без offset. Положительный `OFFSET`
вместе с `WithTies()` — например `Page(5, 3).WithTies()` или `Offset(3).Limit(5).WithTies()` —
бросает `NotSupportedException` во время подготовки, до того как SQL отрендерен, отправлен или
закэширован: T-SQL принимает `WITH TIES` только в `TOP`, но не в `OFFSET … FETCH … ONLY`. Используйте
`WithTies()` без `OFFSET` либо обычное разбиение на страницы без ties.

## Агрегаты и скалярные функции

```csharp
var count = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.count_big());        // count_big(*)
var std   = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.stdev((double)x.Id)); // stdev(...)
```

`count_big` / `count_big_distinct` отрисовывают `count_big(...)`; обычные `count`/`count_distinct` отрисовывают
`count(...)`. `stdev`, `stdevp`, `var` и `varp` сохраняют свои имена (SQL Server предоставляет их нативно).
`Math.Round`, `Math.Truncate`, `len`, `datepart`, `getdate` и `isnull` все выдаются, как показано выше.
Агрегаты вариативности следуют общему контракту кардинальности: `Stdev`/`Var` дают `null` при нуле или
одном вносящем вклад значении, `Stdevp`/`Varp` — `null` при нуле и `0.0` при одном, а два и более
значений сохраняют нативные формулы SQL Server. Nullable-результат доходит до вызывающего как `null`, а
не бросает исключение на non-nullable `double`, поэтому один и тот же запрос возвращает одинаковую форму
здесь и на других провайдерах.

BASE64 требует SQL Server 2025: там `SqlFunctions.Sql.base64_encode` и `base64_decode` рендерятся как
`BASE64_ENCODE`/`BASE64_DECODE`, без compatibility level и preview-флага. `base64_decode` принимает
`varchar`, поэтому столбец `nvarchar` нужно привести. `crypt_gen_random` и decimal-функции
`parse`/`try_parse` (`decimal(38,18)`, фиксированный `en-US`) доступны на том же сервере. nextorm не
опрашивает версию сервера, поэтому на старом сервере отрисованная функция падает с собственной
ошибкой сервера «not a recognized function».



SQL Server 2022+ также включает `greatest`/`least` (стандартный синтаксис) и `SqlFunctions.Sql.date_trunc`,
который отрисовывает `datetrunc(part, value)`, сворачивая множественные ANSI-части в единственные
T-SQL-написания (`milliseconds` → `millisecond`); `decade`/`century`/`millennium` выбрасывают исключение.
Арифметика дат нативная: `SqlFunctions.Sql.date_add(field, amount, value)` отрисовывает
`dateadd(field, amount, value)` (а `decade`/`century`/`millennium` сворачиваются в масштабированное
прибавление `year`), `SqlFunctions.Sql.date_diff(field, start, end)` — `datediff(field, start, end)`,
`SqlFunctions.Sql.date_from_parts(year, month, day)` — `datefromparts(year, month, day)`,
`SqlFunctions.Sql.end_of_month(value)` — `eomonth(value)`.

Форматирование дат и чисел намеренно **не** вынесено на кросс-провайдерную поверхность: T-SQL `FORMAT`
принимает .NET-шаблон (и зависит от CLR; SQL Server 2012+), тогда как PostgreSQL `to_char` и `%`-шаблоны
`DATE_FORMAT`/`strftime`/`formatDateTime` остальных провайдеров используют несовместимые языки шаблонов,
поэтому переносимого аргумента `template` нет. Для форматирования на SQL Server объявляйте
`[SqlFunction("format")]`; PG-эквивалент — `SqlFunctions.Postgres.to_char`.

SQL Server 2017+ включает
`SqlFunctions.Sql.string_agg` → `string_agg(value, delimiter)`. Типа-массива нет, поэтому `array_agg`
по-прежнему выбрасывает исключение ([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg) равно `false`). SQL Server 2016+ также
включает текстовые JSON-функции ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)): `SqlFunctions.SqlServer.json_value`, `SqlFunctions.SqlServer.json_query`,
`SqlFunctions.SqlServer.json_modify` и `SqlFunctions.SqlServer.isjson` отрисовывают свои T-SQL-имена над текстовой колонкой,
используя строку JSONPath (`'$.name'`); поверхность `json`/`jsonb` из PostgreSQL по-прежнему
выбрасывает исключение. Типизированный набор строк `OPENJSON ... WITH (...)` объявляется фабрикой
`TvfEntityBuilder<T>`, чей `withClause` несёт тело клаузы (см. гайд по табличным функциям).
Предикаты полнотекстового поиска `SqlFunctions.Sql.contains` и `SqlFunctions.Sql.freetext` ([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText))
отрисовываются как T-SQL `contains(...)`/`freetext(...)` и требуют полнотекстового индекса на колонке.
`SqlFunctions.Sql.iif(condition, whenTrue, whenFalse)` отрисовывает `iif(...)`
([`Iif`](xref:NextORM.Core.ISqlDialect.Iif), написание через [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String))), а
`SqlFunctions.SqlServer.choose(index, ...)` — `choose(...)` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose));
специализированное написание `SqlFunctions.SqlServer.iif` по-прежнему работает по наследованию.
Семейство session/info ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)) отрисовывает
`SqlFunctions.Sql.current_user()`/`session_user()` как ключевые слова ANSI, а
`current_schema()`/`current_database()`/`version()` — как `schema_name()`/`db_name()`/`@@version`.
SQL Server отрисовывает оконные квантили `SqlFunctions.Sql.percentile_cont(fraction, property).Over()` и
`percentile_disc(...)` как `percentile_cont(fraction) within group (order by property) over (...)`
([`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow)); точной
упорядоченно-агрегатной формы у него нет. Медиана — та же нативная композиция:
`SqlFunctions.SqlServer.median(value).Over(...)` рендерит
`percentile_cont(0.5) within group (order by value) over (...)`, одна строка на входную строку (не на
группу); поддерживается только окно с `partition by`, поэтому оконный `ORDER BY` или рамка отклоняются,
а вызов обязан быть завершён `Over(...)`. У общего `SqlFunctions.SqlServer.percentile` нет отдельного
нативного написания — вызов бросает `NotSupportedException`. Дроби — это `double` в `[0, 1]`, а не
проценты, и проверяются до рендеринга. Агрегат произвольного значения `any_agg` (`ANY_VALUE`)
**не** включён: T-SQL даёт `ANY_VALUE` только в SQL Server 2025 / Fabric, чего версионно-агностичный
диалект предположить не может.
Методы типа XML `SqlFunctions.SqlServer.xml_value(xml, xpath, sqlType)`,
`xml_query(xml, xpath)` и `xml_exist(xml, xpath)`
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)) рендерят
постфиксную форму T-SQL `xmlcol.value('xpath', 'type')` / `xmlcol.query('xpath')` / `xmlcol.exist('xpath')`;
XQuery и SQL-тип обязаны быть строковыми литералами. Строковый метод `.nodes` доступен как
`SqlFunctions.SqlServer.xml_nodes(xml, xpath)` и используется как коррелированный источник
`CrossApply`/`OuterApply`: он рендерит `<xml>.nodes('xpath') as [alias]([value])`, а развёрнутый
`IXmlNodesRow.Value` проецируется скалярными методами выше (операнд обязан быть колонкой внешней строки,
XQuery — строковым литералом).
Блокирующие табличные хинты ([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)) отрисовываются как `WITH (hint, ...)` после
источника (после его псевдонима, если он есть): `ctx.From<IComplexEntity>(o => o.WithTableHint("nolock"))` даёт `from complex_entity with (nolock)`,
а источник с псевдонимом — `... as [t1] with (nolock)`.
Блокировка строк использует тот же механизм: `ForUpdate`/`ForShare`
([`Lock`](xref:NextORM.Core.ISqlDialect.Lock),
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints)) привязывают
`with (updlock)`/`with (holdlock)` к каждому физическому источнику `FROM`/`JOIN` вместо завершающего
предложения `FOR UPDATE`/`FOR SHARE` ([`FromOptions.ForUpdate`](xref:NextORM.Core.FromOptions.ForUpdate)/[`JoinOptions.ForUpdate`](xref:NextORM.Core.JoinOptions.ForUpdate)
нацелены на один источник). Режим [`LockWaitMode`](xref:NextORM.Core.LockWaitMode) добавляет `nowait` или
`readpast` в тот же хинт (`with (updlock, nowait)` / `with (updlock, readpast)`); `readpast` приближает `SKIP LOCKED`.
`QueryCommand.ForJson(...)` ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)) — терминал: выполняет
запрос и возвращает весь набор результатов одним JSON-документом (`FOR JSON PATH`/`FOR JSON AUTO`, с
необязательными `ROOT('...')` и `INCLUDE_NULL_VALUES`), а `QueryCommand.ForXml(...)`
([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)) делает то же для `FOR XML RAW/AUTO/EXPLICIT/PATH` (с
необязательными именем элемента строки, `ROOT('...')` и `ELEMENTS`). `QueryCommand.WithForJson(...)`/
`WithForXml(...)` присоединяют предложение без выполнения.

Помимо этого SQL Server даёт широкую библиотеку скаляров, специфичных для T-SQL, на
`SqlFunctions.SqlServer`, с гейтом по имени через
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlDialect.SqlServerFunctions); все остальные провайдеры её
отвергают:

- **Дата/время:** семейство часов/смещения `sysdatetime`/`sysdatetimeoffset`/`sysutcdatetime`,
  `switchoffset`/`todatetimeoffset` (строковое смещение или минуты со знаком) и семейство
  конструкторов `timefromparts`/`smalldatetimefromparts`/`datetimefromparts`/`datetime2fromparts`/
  `datetimeoffsetfromparts` (точность `*FROMPARTS` должна быть константой `0..7`);
- **Бинарные/checksum:** `checksum`/`binary_checksum` (минимум один аргумент; форма с подстановочным
  знаком `checksum(*)` не открыта), `compress`/`decompress` (GZIP над `varbinary(max)`) и
  псевдослучайная `rand`/`rand(seed)`;
- **Метаданные:** `col_length`, `col_name`, `ident_incr`/`ident_seed`, `index_col`,
  `object_definition`/`object_id`/`object_name`/`object_schema_name`, `stats_date`,
  `db_id`/`db_name`/`original_db_name`, `schema_id`/`schema_name`, `type_id`/`type_name`,
  `filegroup_id`/`filegroup_name`/`file_id`/`file_idex`/`file_name`, `current_timezone`/
  `current_timezone_id`, `getansinull`, `parsename` и `publishingservername`;
- **Строки/преобразование:** `stuff` и `str`; `isdate`/`isnumeric` возвращают нативный T-SQL `int`
  (1/0), а не `bit`; `formatmessage` принимает строку формата или id из `sys.messages` и не более 20
  аргументов.

Семь имён, сообщающих живое изменяемое состояние соединения, сессии или инструкции, открыты как есть:
`CURRENT_REQUEST_ID`, `CURRENT_TRANSACTION_ID`, `XACT_STATE`, `IDENT_CURRENT`, `MIN_ACTIVE_ROWVERSION`,
`ROWCOUNT_BIG` и `SCOPE_IDENTITY`. Живое изменяемое состояние **не** является критерием исключения:
нативная семантика SQL Server поставляется вместе с оговорками, и nextorm не гарантирует построчную
стабильность, непрерывность соединения между запросами и получение identity — отдельный вызов
`SCOPE_IDENTITY`/`IDENT_CURRENT` не является API последней вставки, используйте
`ReturningIdentity<TKey>()`. Стабильные конфигурационные константы и константы идентичности той же
области (`CURRENT_TIMEZONE`/`CURRENT_TIMEZONE_ID`, `SERVER_NAME`, `HOST_ID`, `HOST_NAME`, `APP_NAME`,
`SUSER_NAME`, `ORIGINAL_LOGIN`) остаются на той же поверхности. См.
[гайд по скалярам T-SQL](../guide/provider-specific-sql/sqlserver.md#скаляры-t-sql).

## Рекурсивные CTE и `maxRecursion`

```csharp
// Union branches must share one result type, so a named shape is used.
public sealed class CteNumberRow { public int n { get; set; } }

var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
var body = anchor.UnionAll(step);

var sql = ctx.WithRecursive("nums", body, 100).From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt });
```

```sql
with nums as (
    select ... union all select ...
)
select n
 from nums option (maxrecursion 100)
```

Модификатор `recursive` опускается (в T-SQL его нет), а `maxRecursion` отрисовывается как завершающая
опция инструкции; глубина по умолчанию в SQL Server — 100, поэтому передавайте значение только когда вам нужен другой предел.

## Операции над множествами `*ALL`

В SQL Server нет ни `INTERSECT ALL`, ни `EXCEPT ALL`. Вызов [`IntersectAll`](xref:NextORM.Core.QueryCommand`1.IntersectAll``1(NextORM.Core.QueryCommand{``0})) или [`ExceptAll`](xref:NextORM.Core.QueryCommand`1.ExceptAll``1(NextORM.Core.QueryCommand{``0})) бросает
`NotSupportedException` из диалекта до того, как какой-либо SQL достигнет базы данных; [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})) и [`Except`](xref:NextORM.Core.QueryCommand`1.Except``1(NextORM.Core.QueryCommand{``0}))
(без `ALL`) работают.

```csharp
// Throws NotSupportedException mentioning IntersectAll.
var q = a.Select(x => x.Id).IntersectAll(b.Select(x => x.Id));
```

## Псевдонимы

Производные таблицы (подзапрос в `FROM`) и табличные функции должны иметь псевдонимы, а идентификаторы используют
квадратные скобки:

```sql
select t1.value, t2.somestring as [String]
 from all_rows() as [t1]
 join complex_entity as [t2] on t1.id = t2.id
```

## Табличные параметры

SQL Server связывает табличный параметр нативно через пользовательский табличный тип: передайте имя типа в `ProcedureParameter.Table(name, typeName, rows)`. Строки передаются потоком как `SqlDataRecord` (один переиспользуемый record на перечисление), поэтому большой набор не буферизуется. Скалярный тип строки отображается на один столбец; тип-сущность — на её невычисляемые столбцы в порядке метаданных, включая identity-столбцы. Столбец `decimal` использует точность/масштаб, объявленные через `[DecimalPrecision(p, s)]` или fluent-отображение, по умолчанию — `decimal(38,18)`. Пустая последовательность связывается с пустой таблицей. См. [Сырой SQL](../guide/14-raw-sql.md#табличные-параметры).

```sql
create type dbo.IdList as table (value int not null);
```

```csharp
using var result = dataContext.ExecuteRaw(
    "select sum(value) as total from @p",
    [ProcedureParameter.Table("p", "dbo.IdList", new[] { 1, 2, 3 })]);

var total = result.Read<int>()[0];   // 6
```

Тип CLR каждого столбца выбирает фиксированный тип T-SQL; пользовательский табличный тип должен совпасть по порядку столбцов:

| CLR | T-SQL |
|---|---|
| `bool` | `bit` |
| `char` | `nchar(1)` |
| `sbyte`, `short` | `smallint` |
| `byte` | `tinyint` |
| `ushort`, `int` | `int` |
| `uint`, `long` | `bigint` |
| `ulong` | `decimal(20,0)` |
| `float` | `real` |
| `double` | `float` |
| `decimal` | `decimal(38,18)` (или объявленный `decimal(p,s)`) |
| `string` | `nvarchar(max)` |
| `Guid` | `uniqueidentifier` |
| `DateTime` | `datetime2` |
| `DateTimeOffset` | `datetimeoffset` |
| `DateOnly` | `date` |
| `TimeOnly` | `time` |
| `TimeSpan` | `bigint` (объявленная единица длительности или тики) |
| `byte[]` | `varbinary(max)` |

Точность/масштаб столбца `decimal` берётся из `[DecimalPrecision]` или fluent-отображения (по умолчанию `decimal(38,18)`). Для другой точности/масштаба у любого другого типа или длины строки, отличной от `max`, объявите табличный тип явно и передайте устаревший `DataTable` с `ProcedureParameter.TypeName`. Пустая последовательность связывается как незаданный параметр, который SqlClient отправляет как пустую таблицу.

## Большие объекты (потоковое чтение LOB)

Одну колонку `byte[]` или `string` можно прочитать как `Stream`/`TextReader`, не загружая значение целиком в managed-память. SqlClient реализует `GetStream`/`GetTextReader` для `varbinary(max)` и `nvarchar(max)`; потоковый режим требует `CommandBehavior.SequentialAccess` (без него значение буферизуется) — nextorm выставляет его на LOB-пути:

```csharp
await using var stream = ctx.From<BinaryEntity>()
    .Where(x => x.Id == 1)
    .Select(x => x.Payload)
    .ToStream();          // открывает reader с CommandBehavior.SequentialAccess

using var reader = ctx.From<Document>()
    .Where(x => x.Id == 1)
    .Select(x => x.Body)
    .ToTextReader();
```

Возвращённый поток владеет reader'ом и per-call командой до освобождения и не закрывает контекст; проекция обязана быть ровно одной колонкой `byte[]`/`string` (иначе `InvalidOperationException`). MySQL/MariaDB, ClickHouse и провайдер in-memory отклоняют терминалы через `NotSupportedException`. См. [Потоковое чтение больших объектов](../guide/28-large-objects.md).

SQL Server также поддерживает многоколоночный терминал `ToDataReader`/`ToDataReaderAsync`: он отдаёт ту же sequential-access команду как принадлежащий вызывающему `DbDataReader`, поэтому вызывающий может прочитать все колонки и строки (или несколько LOB-колонок по порядку), не материализуя результат. SQLite тоже его поддерживает, но через буферизованный reader без локатора (без `rowid`, без `SequentialAccess`, то есть без чанкового LOB); MySQL/MariaDB, ClickHouse и провайдер in-memory не имеют поддержки sequential access и отклоняют его.

## Массовая вставка

`CreateBulkInsertBuilder` пишет нативным путём `SqlBulkCopy`, когда `Mode` — `Auto` (по умолчанию) и все
запрошенные опции совместимы; `Native` отклоняет по имени вместо откатa, а `Portable` пишет
`INSERT ... VALUES` (единственный путь для `Returning*`).

- `MaxBatchSize` отображается в `SqlBulkCopy.BatchSize`; незаданное значение остаётся дефолтом драйвера
  (`0`, один батч).
- `KeepIdentity` отображается в `SqlBulkCopyOptions.KeepIdentity`; портируемое утверждение вместо этого
  оборачивается в `SET IDENTITY_INSERT ... ON/OFF`.
- `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` отображаются в соответствующие флаги
  `SqlBulkCopyOptions`; `false` учитывается отсутствием запроса флага, а `KeepNulls=false` принимается
  только для default-equivalent формы.
- Типизированная группа задаёт внутреннюю транзакцию:
  `SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = true })` выставляет
  `SqlBulkCopyOptions.UseInternalTransaction` для батча драйвера. Комбинация с внешней транзакцией контекста
  недопустима, при нескольких батчах покрыт только текущий, а на портируемом пути запрос отклоняется.
- `Timeout` — один бюджет на весь вызов: оставшееся время становится `BulkCopyTimeout` (минимум 1 с), а
  истёкший бюджет даёт `TimeoutException`; отмена вызывающего всегда побеждает.
- `DatabaseName` рендерится как часть многочастного имени; `ServerName` отклоняется (bulk-вставка никогда не
  перенаправляется на другой сервер).

Набор буферизуется в `DataTable` до копирования, потому что `SqlBulkCopy` нужны метаданные типов колонок;
лимит чанка делит батчи драйвера, но не этот буфер. См. [Массовая вставка](../guide/22-bulk-insert.md).

## Нативная колонка `json` (SQL Server 2025)

В SQL Server 2025 (17.x) появился **отдельный встроенный тип `json`**: сервер сообщает его как `json` в
метаданных колонки, это не псевдоним `nvarchar`, и он работает при любом уровне совместимости базы.
nextorm его не обнаруживает — возможность является чистой функцией объявленных вами версии сервера и
редакции движка, поэтому генерация SQL не зависит ни от соединения, ни от уровня совместимости, ни от
похода за метаданными.

| Построение контекста | `SupportsJson` | Хранение `[JsonColumn]` на колонке `json` |
|---|---|---|
| `new SqlServerDataContext(connectionString, builder)` — по умолчанию, без версии | `false` | только текст: `Auto`/`Text` пишут `nvarchar`, `Native` бросает `NotSupportedException` |
| `new SqlServerDataContext(new Version(16, 0), connectionString)` — SQL Server 2016–2022 | `false` | как в контексте без версии: только текст, `Native` отклоняется до выполнения любой команды |
| `new SqlServerDataContext(new Version(17, 0), connectionString)` — SQL Server 2025+ | `true` | `json`: `Auto`/`Text`/`Native` разрешаются в один и тот же текстовый конвертер |
| `new SqlServerDataContext(SqlServerEngineEdition.SqlDatabase, connectionString)` — Azure SQL Database | `true` | из объявленной редакции: движок сообщает major-версию 12, поэтому редакцию нужно объявить (`Auto`/`Text`/`Native` разрешаются в один и тот же текстовый конвертер). Только модульные тесты: живого экземпляра Azure не было |
| `…AzureSqlManagedInstance` / `…MicrosoftFabricSqlDatabase` — Azure SQL Managed Instance / база данных SQL в Microsoft Fabric | `true` | как выше; база данных SQL в Microsoft Fabric — редакция 12, а редакция 11 («Azure Synapse serverless SQL pool, или Microsoft Fabric») её **не** включает. Только модульные тесты |

Редакция — это **объявление вызывающего кода**: nextorm никогда не читает `SERVERPROPERTY('EngineEdition')`,
поэтому генерация SQL остаётся чистой функцией объявленного состояния. Конкретный тип контекста владеет
одним неизменяемым объявлением `(версия, редакция)` — план-кэш ключуется типом контекста, — и
конфликтующее объявление бросает `InvalidOperationException` до выполнения любой команды. Объявление
возможности не переносит колонку `nvarchar` в `json`, а `Auto` сохраняет текстовый транспорт.

```csharp
// Конструктор «версия первой»: объявленная версия открывает возможности, зависящие от версии.
using var dataContext = new SqlServerDataContext(new Version(17, 0), connectionString);
```

`(Version, DbConnection)` — вторая перегрузка, для соединения, которым владеет вызывающий код. У типа
контекста одно неизменяемое объявление `(версия, редакция движка)` — план-кэш ключуется типом
контекста, — поэтому построение *одного и того же* типа с конфликтующим объявлением любой из компонент
(другая версия, другая редакция либо сначала без объявления, а затем с ним) бросает
`InvalidOperationException` до выполнения любой команды; заводите отдельный подкласс
`SqlServerDataContext` на каждое объявление.

### Транспорт и ограничения

* Представление — обычная CLR-строка `string`: параметры выводятся как `nvarchar`, DOM-параметра нет,
  `SqlDbType.Json` не выставляется, двойной сериализации нет.
* В контексте с открытым гейтом — по объявленной major-версии 17+ либо по объявленной редакции
  Azure/Fabric SQL database — `Auto`, `Text` и `Native` разрешаются в один и тот же конвертер,
  поэтому существующее отображение `[JsonColumn]` на `nvarchar` остаётся байт-в-байт тем же, когда вы
  добавляете аргумент версии или редакции.
* Нативная колонка `json` хранит **только документы-объекты и массивы**. Скалярные корни (строка,
  число, булево) и документ JSON `null` отклоняет **сервер** ошибкой **13609** — для таких полезных
  нагрузок используйте хранение `Text`. Это ограничение сервера, а не отказ библиотеки, и nextorm не
  добавляет для него собственной клиентской валидации.
* JSON `null` в текстовом хранении — это четырёхсимвольный текст `null` (`DATALENGTH` 8 в `nvarchar`),
  который остаётся отличным от SQL `NULL`.

### Миграция

nextorm не генерирует DDL колонок, поэтому перевод колонки из `nvarchar(max)` в `json` — изменение схемы,
которое вы делаете в базе, например `alter table t alter column payload json` отдельным шагом релиза.
Со стороны библиотеки всё работает в обоих случаях: то же отображение `[JsonColumn]` пишет тот же текст
в колонку любого из двух типов, а `Auto` никогда не перенацеливает существующее отображение сам.
Объявление редакции движка — в равной степени объявление на стороне библиотеки: оно никогда не изменяет
колонку.

## Различия провайдеров

| Аспект | SQL Server |
|---|---|
| Плейсхолдер параметра | `@name` |
| Только limit | `top(n)` |
| Limit + offset | `offset m rows fetch next n rows only` |
| Только offset | `offset m rows` |
| Разбиение на страницы без `ORDER BY` | внедряет `order by (select null as anyorder)` |
| Concat | `+` |
| Coalesce | `isnull` |
| Логический литерал | `1` / `0` (материализация bit) |
| Квотирование идентификаторов | квадратные скобки (`as [t1]`) |
| Псевдоним производной таблицы / TVF | требуется |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`, `ToDataReader`) | поддерживается (`SequentialAccess`; одна колонка `byte[]`/`string` или многоколоночный reader, принадлежащий вызывающему) |
| `*ALL` | не поддерживается (бросает исключение) |
| Рекурсивный CTE | `with` + `option (maxrecursion n)` |
| Имена агрегатов | `stdev`/`var` нативные; доступен `count_big` |
| `greatest` / `least` | поддерживаются (SQL Server 2022+; игнорируют NULL-аргументы) |
| `date_trunc` | `datetrunc(part, value)` (SQL Server 2022+) |
| `date_add` / `date_diff` / `date_from_parts` / `end_of_month` | `dateadd(field, amount, value)` / `datediff(field, start, end)` / `datefromparts(y, m, d)` / `eomonth(value)` |
| `string_agg` / `array_agg` | `string_agg` поддерживается (SQL Server 2017+); `array_agg` — нет (бросает исключение) |
| Текстовый JSON | `json_value` / `json_query` / `json_modify` (SQL Server 2016+) |
| Session/info-функции | `current_user`, `session_user`, `schema_name()`, `db_name()`, `@@version` |
| Часы/смещение/`*FROMPARTS` T-SQL | `sysdatetime` / `sysdatetimeoffset` / `sysutcdatetime` / `switchoffset` / `todatetimeoffset` / `timefromparts` / `smalldatetimefromparts` / `datetimefromparts` / `datetime2fromparts` / `datetimeoffsetfromparts` (гейт по имени) |
| Бинарные/checksum/прочие T-SQL | `checksum` / `binary_checksum` / `compress` / `decompress` / `rand` / `stuff` / `str` / `isdate` / `isnumeric` / `formatmessage` (гейт по имени) |
| Метаданные T-SQL | `col_length` / `col_name` / `object_id` / `object_name` / `db_id` / `db_name` / `schema_id` / `schema_name` / `type_id` / `type_name` / `file_id` / `file_name` / `current_timezone` / `parsename` / `stats_date` и остальные семейства A-D (гейт по имени) |
| Оконные квантили | `percentile_cont`/`percentile_disc` как `... within group (order by x) over (...)` (SQL Server 2012+) |
| Медиана (аналитическая) | `SqlFunctions.SqlServer.median(v).Over(...)` → `percentile_cont(0.5) within group (order by v) over (...)`: одна строка на входную строку; окно только с `partition by` (`ORDER BY`/рамка отклоняются) |
| Общий квантиль | не поддерживается: `SqlFunctions.SqlServer.percentile` бросает `NotSupportedException` |
| Агрегат произвольного значения | не поддерживается (`ANY_VALUE` только в SQL Server 2025 / Fabric) |
| Условные функции | `iif(...)` (переносимая, [`Iif`](xref:NextORM.Core.ISqlDialect.Iif)) / `choose(...)` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)) |
| Предикаты полнотекстового поиска | `contains(...)` / `freetext(...)` (колонка должна быть полнотекстово проиндексирована) |
| Полнотекстовое ранжирование | табличные функции `containstable(table, column, search)` / `freetexttable(...)` возвращают `KEY`/`RANK` ([`SqlFunctions.SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer), [`IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1)) |
| Регулярные выражения | `regexp_like(value, pattern, 'c'/'i')` / `regexp_replace(value, pattern, replacement, 1, 0, 'c'/'i')` (SQL Server 2025+; для `regexp_like` дополнительно нужен уровень совместимости БД 170) |
| Блокирующие табличные хинты | `with (hint, ...)` после источника, после его псевдонима, если он есть ([`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[]))) |
| Блокировка строк | `ForUpdate`/`ForShare` рендерят `with (updlock)`/`with (holdlock)` на **каждом** физическом источнике `FROM`/`JOIN` (изменение поведения: раньше — только основной источник); [`FromOptions.ForUpdate`](xref:NextORM.Core.FromOptions.ForUpdate)/[`JoinOptions.ForUpdate`](xref:NextORM.Core.JoinOptions.ForUpdate) нацелены на один источник ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock), [`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints)); [`LockWaitMode`](xref:NextORM.Core.LockWaitMode) добавляет `nowait`/`readpast` (`with (updlock, nowait)`/`with (updlock, readpast)`) |
| Нативный bulk copy | `SqlBulkCopy`; [`BulkInsertOptions`](xref:NextORM.Core.BulkInsertOptions) `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` отображаются в `SqlBulkCopyOptions`, а [`SqlServerBulkInsertOptions.UseInternalTransaction`](xref:NextORM.Core.SqlServerBulkInsertOptions.UseInternalTransaction) выставляет флаг внутренней транзакции (см. [Массовая вставка](../guide/22-bulk-insert.md#опции-bulk-copy-в-sql-server)) |
| JSON-вывод | весь набор одним JSON-документом, терминальные `for json path` / `for json auto` ([`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[]))) |
| XML-вывод | весь набор одним XML-документом, терминальные `for xml raw/auto/explicit/path` ([`ForXml`](xref:NextORM.Core.QueryCommand`1.ForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean,System.Object[]))) |
| Методы типа XML | `xml.value('xpath', 'type')` / `xml.query('xpath')` / `xml.exist('xpath')` / `xml.nodes('xpath') as [alias]([value])` ([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)) |
| `AVG` по целочисленному столбцу | усекается до целого |
| Размещение null при `ORDER BY … DESC` | null сортируются последними по умолчанию |

## См. также

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.sqlserver.tests/Dialect/SqlServerDialectTests.cs:25,37,43,52,60,69,84,93,102`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:133,146,160,185,218,231,249,268,856,1044`,
`tests/nextorm.integration.tests/SqlServer/SqlServerSpecificTests.cs:24,43`,
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.sqlserver/SqlServerDataContext.cs`,
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs`.
