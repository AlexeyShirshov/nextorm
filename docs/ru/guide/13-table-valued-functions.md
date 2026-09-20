# Табличные функции

> Запрашивайте табличную функцию базы данных как источник `FROM` с помощью [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions) и
> сопоставляйте её строки как любую другую сущность.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Соединения](03-joins.md) · [Группировка и агрегаты](04-grouping-and-aggregates.md)

## Обзор

[`SqlTableFunctionAttribute`](xref:NextORM.Core.SqlTableFunctionAttribute) сопоставляет статический метод-заглушку табличной функции базы данных. Метод
должен возвращать `IQueryable<T>` (где `T` описывает форму строки) и упоминается только внутри выражения,
переданного в [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions):

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SqlTableFunctionAttribute : Attribute
{
    public SqlTableFunctionAttribute();
    public SqlTableFunctionAttribute(string name);
    public string? Name { get; set; }       // defaults to the CLR method name
    public string? Schema { get; set; }     // optional schema/owner prefix
    public string? WithClause { get; set; } // optional trailing WITH (...) body
}
```

```csharp
public static EntityBuilder<T> FromTableFunction<T>(this IDataContext dataContext,
    Expression<Func<IQueryable<T>>> call);
```

`call` должен быть вызовом метода, помеченного `[SqlTableFunction]` (или объявленного в помеченном типе);
что-либо иное бросает `ArgumentException`. Вызов преобразуется в `[schema.]name(arg1, arg2, ...)`, при этом
аргументы рендерятся через обычный посетитель выражений, поэтому **захваченные значения становятся
параметрами**. nextorm только генерирует вызов — функция уже должна существовать в целевой базе данных.

Возвращаемый `EntityBuilder<T>` — обычный источник запроса, поэтому [`Where`](xref:NextORM.Core.EntityBuilder`1), [`OrderBy`](xref:NextORM.Core.EntityBuilder`1), [`GroupBy`](xref:NextORM.Core.EntityBuilder`1), [`Join`](xref:NextORM.Core.EntityBuilder`1),
[`Select`](xref:NextORM.Core.EntityBuilder`1), разбиение на страницы и терминалы работают с ним.

## Объявление сопоставления

Форма строки — обычная сущность, а методы-заглушки несут сопоставление:

```csharp
public interface ITvfRow
{
    [Column("id")]
    long Id { get; set; }
    [Column("value")]
    string? Value { get; set; }
}

public interface IJsonEachRow
{
    [Column("key")]
    long Key { get; set; }
    [Column("value")]
    long Value { get; set; }
}

private static class Tvf
{
    [SqlTableFunction("all_rows")]
    public static IQueryable<ITvfRow> AllRows() => throw new NotSupportedException();

    [SqlTableFunction("rows_by_id")]
    public static IQueryable<ITvfRow> ById(long id) => throw new NotSupportedException();

    [SqlTableFunction("rows_between", Schema = "app")]
    public static IQueryable<ITvfRow> Between(long lo, long hi) => throw new NotSupportedException();

    [SqlTableFunction("json_each")]
    public static IQueryable<IJsonEachRow> JsonEach(string json) => throw new NotSupportedException();
}
```

## Базовый пример

```csharp
var values = dataContext
    .FromTableFunction(() => Tvf.JsonEach("[1,2,3]"))
    .Select(r => r.Value)
    .ToList();
// 1, 2, 3
```

```sql
-- SQLite
select value from json_each('[1,2,3]')
-- SQL Server / PostgreSQL append an alias
select value from json_each('[1,2,3]') as [t1]
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Value |
|-------|
| 1     |
| 2     |
| 3     |

## Аргументы становятся параметрами

```csharp
var id = 5L;

var prepared = dataContext
    .FromTableFunction(() => Tvf.ById(id))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
-- SQLite placeholder; SQL Server/PostgreSQL use @id
select id from rows_by_id($id)
```

Имя, квалифицированное схемой, с двумя аргументами рендерится как `app.rows_between(...)`:

```csharp
var lo = 1L;
var hi = 3L;

var prepared = dataContext
    .FromTableFunction(() => Tvf.Between(lo, hi))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
select id from app.rows_between($lo, $hi)
```

## Объединение, фильтрация и сортировка TVF

TVF — обычный источник, поэтому его можно объединить с таблицей и читать через проекцию `t1`/`t2`:

```csharp
var rows = dataContext
    .FromTableFunction(() => Tvf.AllRows())
    .Join(dataContext.From<IComplexEntity>(), (r, c) => r.Id == c.Id)
    .Select(p => new { p.Item1.Value, p.Item2.String })
    .ToList();
```

```sql
-- SQLite
select t1.value, t2.somestring as 'String' from all_rows() as 't1' join complex_entity as 't2' on t1.id = t2.id
```

Фильтрация, сортировка и группировка работают как обычно:

```csharp
var values = dataContext
    .FromTableFunction(() => Tvf.JsonEach("[3,1,2]"))
    .Where(r => r.Value > 1)
    .OrderByDescending(r => r.Value)
    .Select(r => r.Value)
    .ToList();
// 3, 2

var rows = dataContext
    .FromTableFunction(() => Tvf.JsonEach("[1,1,2]"))
    .GroupBy(r => new { r.Value })
    .Select(g => new { g.Value, Cnt = SqlFunctions.Sql.count() })
    .ToList();
// (1, 2), (2, 1)
```

Вывод:

| Value |
|-------|
| 3     |
| 2     |

Вывод:

| Value | Cnt |
|-------|-----|
| 1     | 2   |
| 2     | 1   |

## Встроенные табличные функции

Несколько распространённых табличных функций уже объявлены с `[SqlTableFunction]`, поэтому
пользовательская обёртка не нужна.

`SqlFunctions.Postgres.generate_series` и `SqlFunctions.Postgres.unnest` — из PostgreSQL (возвращают [`SqlFunctions.IGenerateSeriesRow`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow)
с колонкой `generate_series` и [`SqlFunctions.IUnnestRow<T>`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1) с колонкой `unnest`):

```csharp
var numbers = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

PostgreSQL также предоставляет наборные функции для regexp, JSON и полнотекстового поиска:

| `SqlFunctions.Postgres.*` | Колонки SQL | Row-shape |
| --- | --- | --- |
| `regexp_matches(source, pattern[, flags])` | `regexp_matches text[]` | `IRegexpMatchesRow` (`.Matches`) |
| `regexp_split_to_table(source, pattern[, flags])` | `regexp_split_to_table` | `IRegexpSplitToTableRow` (`.Value`) |
| `jsonb_array_elements(json)` / `jsonb_array_elements_text(json)` | `value` | `IJsonArrayElementsRow` (`.Value`) |
| `jsonb_each(json)` / `jsonb_each_text(json)` | `key`, `value` | `IJsonbEachRow` (`.Key`, `.Value`) |
| `jsonb_object_keys(json)` | `jsonb_object_keys` | `IJsonObjectKeysRow` (`.Key`) |
| `jsonb_path_query(json, jsonpath)` | `jsonb_path_query` | `IJsonPathQueryRow` (`.Value`) |
| `ts_stat(query)` | `word`, `ndoc`, `nentry` | `ITsStatRow` (`.Word`, `.Ndoc`, `.Nentry`) |

```csharp
var json = JsonDocument.Parse("""{"a":1,"b":2}""");

var entries = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.jsonb_each_text(json))
    .Select(r => new { r.Key, r.Value })
    .ToList();
// ("a", "1"), ("b", "2")
```

JSONPath-аргумент передаётся текстом через `SqlFunctions.Postgres.jsonpath(path)`, что рендерится как
`cast(path as jsonpath)`:

```csharp
var json = JsonDocument.Parse("""{"a":[1,2]}""");
var path = "$.a[*]";

var values = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.jsonb_path_query(json, SqlFunctions.Postgres.jsonpath(path)))
    .Select(r => r.Value)
    .ToList();
// "1", "2"
```

PostgreSQL переименовывает единственную колонку скалярной наборной функции (`generate_series`, `unnest`,
`regexp_matches`, `regexp_split_to_table`, `jsonb_object_keys`, `jsonb_path_query`) в псевдоним, который
nextorm добавляет производному источнику, поэтому диалект оборачивает такие вызовы в подзапрос с одной
колонкой (`select generate_series from generate_series(...)`); функции с явной колонкой (`value`,
`key`/`value`, `word`/`ndoc`/`nentry`) эмитятся без обёртки.

`SqlFunctions.SqlServer.string_split` — из SQL Server 2016+ и возвращает [`SqlFunctions.IStringSplitRow`](xref:NextORM.Core.SqlFunctions.IStringSplitRow) (единственная колонка
`value`). Порядок фрагментов не гарантируется, поэтому добавляйте `order by`, если важен порядок входной
строки:

```csharp
var csv = "a,b,c";
var separator = ",";

var fragments = dataContext
    .FromTableFunction(() => SqlFunctions.SqlServer.string_split(csv, separator))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value from string_split(@csv, @separator) as [t1]
```

`SqlFunctions.SqlServer.openjson` — из SQL Server 2016+ и возвращает [`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`key`/`value`/`type`).
Схема по умолчанию даёт свойства JSON-объекта или элементы JSON-массива:

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type from openjson(@json) as [t1]
```

Для **типизированной схемы** (`OPENJSON ... WITH (...)`) объявите свой хелпер, чья форма строки
соответствует схеме, и задайте [`WithClause`](xref:NextORM.Core.SqlTableFunctionAttribute.WithClause); тело предложения эмитится дословно после вызова:

```csharp
public interface IOpenJsonTypedRow
{
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

private static class OpenJsonTvf
{
    [SqlTableFunction("openjson", WithClause = "name nvarchar(50) '$.name', age int '$.age'")]
    public static IQueryable<IOpenJsonTypedRow> Typed(string json) => throw new NotSupportedException();
}

var person = dataContext
    .FromTableFunction(() => OpenJsonTvf.Typed("""{"name":"Ada","age":36}"""))
    .Select(r => new { r.Name, r.Age })
    .First();
```

```sql
select name, age from openjson(@json) with (name nvarchar(50) '$.name', age int '$.age') as [t1]
```

`SqlFunctions.ClickHouse.numbers`/`numbers_mt` — табличные функции ClickHouse, возвращающие
[`SqlFunctions.INumbersRow`](xref:NextORM.Core.SqlFunctions.INumbersRow) (единственная колонка `number`). `numbers(count)` даёт
последовательные целые с нуля, `numbers(start, stop[, step])` — произвольный диапазон:

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.ClickHouse.numbers(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select number as `Value` from (select toInt64(number) as number from numbers(@count)) as `t1`
```

Колонка `number` имеет тип `UInt64`, который не материализуется row reader'ом, поэтому диалект
оборачивает вызов в подзапрос с приведением (`toInt64(number) as number`).

`SqlFunctions.ClickHouse.zeros`/`zeros_mt` — табличные функции ClickHouse, генерирующие строки и
возвращающие [`SqlFunctions.IZerosRow`](xref:NextORM.Core.SqlFunctions.IZerosRow) (единственная колонка `zero UInt8`):

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select zero as `Value` from zeros(@count) as `t1`
```

В отличие от `numbers`, колонка `zero` имеет тип `UInt8`, который row reader материализует напрямую
как `byte`, поэтому подзапрос-обёртка с приведением не нужен.

`SqlFunctions.ClickHouse.generate_random`/`generate_random(seed)` — табличные функции генерации
тестовых данных ClickHouse, возвращающие
[`SqlFunctions.IGenerateRandomRow`](xref:NextORM.Core.SqlFunctions.IGenerateRandomRow) (`id UInt64`,
`value Float64`, `name String`). Схема в ClickHouse задаётся строкой, а форма без аргументов даёт
случайную схему, поэтому встроенный хелпер фиксирует структуру, а колонка `id` приводится к `Int64`
той же подзапрос-обёрткой, что и `numbers`; поток бесконечен, поэтому добавляйте page/limit:

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.ClickHouse.generate_random())
    .Page(3, 0)
    .Select(r => new { r.Id, r.Value, r.Name })
    .ToList();
```

```sql
select id as `Id`, value as `Value`, name as `Name`
from (select toInt64(id) as id, value, name from generateRandom('id UInt64, value Float64, name String')) as `t1`
limit 3
```

Сопоставленная функция должна существовать в базе — nextorm только генерирует вызов, он её не создаёт, —
поэтому используйте хелпер только на провайдере, где она определена. Встроенные хелперы гейтятся
[`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect): PostgreSQL разрешает `generate_series`, `unnest`,
`regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`,
`jsonb_object_keys`, `jsonb_path_query` и `ts_stat`; SQL Server —
`string_split`/`openjson`, ClickHouse — `numbers`/`numbers_mt`, `zeros`/`zeros_mt` и `generateRandom`, а любой другой
провайдер отклоняет их с `NotSupportedException` (пользовательская `[SqlTableFunction]` не гейтится).

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | Вызов функции генерируется без псевдонима для простого источника; псевдоним всё равно генерируется, когда источник объединяется. |
| SQL Server | Производный источник получает псевдоним (`as [t1]`). |
| PostgreSQL | Псевдоним обязателен и генерируется всегда (`as "t1"`). |
| MySQL | Производный источник получает псевдоним (`` as `t1` ``). |
| MariaDB | Производный источник получает псевдоним (`` as `t1` ``). |
| ClickHouse | Производный источник получает псевдоним (`` as `t1` ``). |
| In-memory | Источники табличных функций **не поддерживаются** (`NotSupportedException`: "Table-valued function sources are not supported by the in-memory provider."). |

> Интеграционные тесты используют встроенную в SQLite `json_each`, поэтому общий набор тестов выполняет
> поведенческие тесты TVF только на SQLite; SQL Server и PostgreSQL пропускаются через
> `ITestProvider.SupportsTableValuedFunctions`, потому что функцию сначала нужно создать в базе данных.
> Генерация SQL для всех трёх провайдеров по-прежнему покрывается тестами `TableFunction_*`.

## См. также

* [Соединения](03-joins.md) — соединение TVF с таблицей или другим TVF.
* [Пользовательские функции](12-user-defined-functions.md) — скалярный эквивалент.
* [Обзор провайдеров](../providers/overview.md) — требование псевдонима TVF для каждого провайдера.

---

Source: `src/nextorm.core/SqlTableFunctionAttribute.cs:17`, `src/nextorm.core/DataContext/DataContextExtensions.cs:117`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:121`;
`tests/nextorm.integration.tests/CommonTestSuite.Tvf.cs:34`, `:47`, `:61`, `:76`;
`tests/nextorm.core.tests/SqlTableFunctionAttributeTests.cs:8`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1453`, `:1462`, `:1475`, `:1489`, `:1503`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1044`, `:1066`, `:1080`, `:1094`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:976`, `:998`, `:1012`, `:1026`.
