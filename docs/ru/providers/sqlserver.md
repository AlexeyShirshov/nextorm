# Провайдер SQL Server

> Используйте `nextorm.sqlserver` для Microsoft SQL Server и Azure SQL; он отрисовывает параметры `@name`, разбиение на страницы `top(n)` / `offset … fetch` (внедряя `ORDER BY`, когда это необходимо), конкатенацию `+`, `isnull`, `datepart` и `count_big`, с идентификаторами в квадратных скобках.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) (`src/nextorm.sqlserver/SqlServerDataContext.cs`) оборачивает `Microsoft.Data.SqlClient`. Он
создаёт `SqlConnection`, передаёт значения параметров null как `DBNull` (иначе SqlClient не отправляет значение
вообще), и переопределяет `MapColumnExpression`, чтобы читать числовые столбцы через `Convert.ChangeType`, потому что
типизированные геттеры SqlClient строги к расширению.

[`SqlServerDialect`](xref:NextORM.SqlServer.SqlServerDialect) (`src/nextorm.sqlserver/SqlServerDialect.cs`) — это диалект:

- плейсхолдер параметра `@name`;
- идентификаторы заключаются в квадратные скобки ([`Escape`](xref:NextORM.Core.ISqlDialect) возвращает `[name]`), включая ссылки на столбцы, чтобы псевдонимы, которые
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
// select top(5) id from simple_entity

ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);
// select id from simple_entity order by (select null as anyorder)
// offset 10 rows
// fetch next 5 rows only
```

SQL Server отклоняет `OFFSET`/`FETCH` без `ORDER BY`, поэтому когда у запроса с разбиением на страницы нет сортировки, диалект
внедряет постоянную сортировку `(select null as anyorder)` ([`GetPagingOrderBy`](xref:NextORM.Core.ISqlDialect)). Запрос только с offset выдаёт
`offset m rows` и без `fetch`. Когда у запроса уже есть `ORDER BY`, ничего не внедряется.

```csharp
ctx.From<ISimpleEntity>().Offset(10).OrderBy(x => x.Id).Select(x => x.Id);
// ... order by id offset 10 rows
```

## Агрегаты и скалярные функции

```csharp
var count = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.count_big());        // count_big(*)
var std   = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.stdev((double)x.Id)); // stdev(...)
```

`count_big` / `count_big_distinct` отрисовывают `count_big(...)`; обычные `count`/`count_distinct` отрисовывают
`count(...)`. `stdev`, `stdevp`, `var` и `varp` сохраняют свои имена (SQL Server предоставляет их нативно).
`Math.Round`, `Math.Truncate`, `len`, `datepart`, `getdate` и `isnull` все выдаются, как показано выше.

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
выбрасывает исключение. Типизированный набор строк `OPENJSON ... WITH (...)` объявляется через
`[SqlTableFunction("openjson", WithClause = "...")]` (см. гайд по табличным функциям).
Предикаты полнотекстового поиска `SqlFunctions.Sql.contains` и `SqlFunctions.Sql.freetext` ([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText))
отрисовываются как T-SQL `contains(...)`/`freetext(...)` и требуют полнотекстового индекса на колонке.
`SqlFunctions.Sql.iif(condition, whenTrue, whenFalse)` отрисовывает `iif(...)`
([`Iif`](xref:NextORM.Core.ISqlDialect.Iif), написание через [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render)), а
`SqlFunctions.SqlServer.choose(index, ...)` — `choose(...)` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose));
специализированное написание `SqlFunctions.SqlServer.iif` по-прежнему работает по наследованию.
Семейство session/info ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)) отрисовывает
`SqlFunctions.Sql.current_user()`/`session_user()` как ключевые слова ANSI, а
`current_schema()`/`current_database()`/`version()` — как `schema_name()`/`db_name()`/`@@version`.
SQL Server отрисовывает оконные квантили `SqlFunctions.Sql.percentile_cont(fraction, property).Over()` и
`percentile_disc(...)` как `percentile_cont(fraction) within group (order by property) over (...)`
([`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow)); точной
упорядоченно-агрегатной формы у него нет. Агрегат произвольного значения `any_agg` (`ANY_VALUE`)
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
Табличные хинты ([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)) отрисовываются как `WITH (hint, ...)` после имени основной
таблицы: `ctx.From<IComplexEntity>().WithTableHint("nolock")` даёт `from complex_entity with (nolock)`.
Блокировка строк использует тот же механизм: `ForUpdate`/`ForShare`
([`Lock`](xref:NextORM.Core.ISqlDialect.Lock),
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints)) привязывают
`with (updlock)`/`with (holdlock)` к основной таблице вместо завершающего предложения
`FOR UPDATE`/`FOR SHARE`.
`QueryCommand.ForJson(...)` ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)) добавляет завершающее предложение
`FOR JSON PATH`/`FOR JSON AUTO` (с необязательными `ROOT('...')` и `INCLUDE_NULL_VALUES`), а
`QueryCommand.ForXml(...)` ([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)) — `FOR XML RAW/AUTO/EXPLICIT/PATH` (с необязательными
именем элемента строки, `ROOT('...')` и `ELEMENTS`).

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
select n from nums option (maxrecursion 100)
```

Модификатор `recursive` опускается (в T-SQL его нет), а `maxRecursion` отрисовывается как завершающая
опция инструкции; глубина по умолчанию в SQL Server — 100, поэтому передавайте значение только когда вам нужен другой предел.

## Операции над множествами `*ALL`

В SQL Server нет ни `INTERSECT ALL`, ни `EXCEPT ALL`. Вызов [`IntersectAll`](xref:NextORM.Core.QueryCommand`1) или [`ExceptAll`](xref:NextORM.Core.QueryCommand`1) бросает
`NotSupportedException` из диалекта до того, как какой-либо SQL достигнет базы данных; [`Intersect`](xref:NextORM.Core.QueryCommand`1) и [`Except`](xref:NextORM.Core.QueryCommand`1)
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
| `*ALL` | не поддерживается (бросает исключение) |
| Рекурсивный CTE | `with` + `option (maxrecursion n)` |
| Имена агрегатов | `stdev`/`var` нативные; доступен `count_big` |
| `greatest` / `least` | поддерживаются (SQL Server 2022+; игнорируют NULL-аргументы) |
| `date_trunc` | `datetrunc(part, value)` (SQL Server 2022+) |
| `date_add` / `date_diff` / `date_from_parts` / `end_of_month` | `dateadd(field, amount, value)` / `datediff(field, start, end)` / `datefromparts(y, m, d)` / `eomonth(value)` |
| `string_agg` / `array_agg` | `string_agg` поддерживается (SQL Server 2017+); `array_agg` — нет (бросает исключение) |
| Текстовый JSON | `json_value` / `json_query` / `json_modify` (SQL Server 2016+) |
| Session/info-функции | `current_user`, `session_user`, `schema_name()`, `db_name()`, `@@version` |
| Оконные квантили | `percentile_cont`/`percentile_disc` как `... within group (order by x) over (...)` (SQL Server 2012+) |
| Агрегат произвольного значения | не поддерживается (`ANY_VALUE` только в SQL Server 2025 / Fabric) |
| Условные функции | `iif(...)` (переносимая, [`Iif`](xref:NextORM.Core.ISqlDialect.Iif)) / `choose(...)` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)) |
| Предикаты полнотекстового поиска | `contains(...)` / `freetext(...)` (колонка должна быть полнотекстово проиндексирована) |
| Полнотекстовое ранжирование | табличные функции `containstable(table, column, search)` / `freetexttable(...)` возвращают `KEY`/`RANK` ([`SqlFunctions.SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer), [`IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1)) |
| Табличные хинты | `with (hint, ...)` после основной таблицы ([`WithTableHint`](xref:NextORM.Core.EntityBuilder`1)) |
| Блокировка строк | `ForUpdate`/`ForShare` рендерят `with (updlock)`/`with (holdlock)` на основной таблице ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock), [`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints)) |
| JSON-вывод | завершающие `for json path` / `for json auto` ([`ForJson`](xref:NextORM.Core.QueryCommand`1)) |
| XML-вывод | завершающие `for xml raw/auto/explicit/path` ([`ForXml`](xref:NextORM.Core.QueryCommand`1)) |
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

Source: `tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs:25,37,43,52,60,69,84,93,102`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:133,146,160,185,218,231,249,268,856,1044`,
`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:24,43`,
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.sqlserver/SqlServerDataContext.cs`,
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs`.
