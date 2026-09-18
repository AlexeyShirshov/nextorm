# Табличные функции

> Запрашивайте табличную функцию базы данных как источник `FROM` с помощью `FromTableFunction` и
> сопоставляйте её строки как любую другую сущность.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Соединения](03-joins.md) · [Группировка и агрегаты](04-grouping-and-aggregates.md)

## Обзор

`SqlTableFunctionAttribute` сопоставляет статический метод-заглушку табличной функции базы данных. Метод
должен возвращать `IQueryable<T>` (где `T` описывает форму строки) и упоминается только внутри выражения,
переданного в `FromTableFunction`:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SqlTableFunctionAttribute : Attribute
{
    public SqlTableFunctionAttribute();
    public SqlTableFunctionAttribute(string name);
    public string? Name { get; set; }     // defaults to the CLR method name
    public string? Schema { get; set; }   // optional schema/owner prefix
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

Возвращаемый `EntityBuilder<T>` — обычный источник запроса, поэтому `Where`, `OrderBy`, `GroupBy`, `Join`,
`Select`, разбиение на страницы и терминалы работают с ним.

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
    .Select(p => new { p.t1.Value, p.t2.String })
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
    .Select(g => new { g.Value, Cnt = NORM.SQL.count() })
    .ToList();
// (1, 2), (2, 1)
```

## Встроенные табличные функции

Несколько распространённых табличных функций уже объявлены с `[SqlTableFunction]`, поэтому
пользовательская обёртка не нужна.

`NORM.PG_SQL.generate_series` и `NORM.PG_SQL.unnest` — из PostgreSQL (возвращают `NORM.IGenerateSeriesRow`
с колонкой `generate_series` и `NORM.IUnnestRow<T>` с колонкой `unnest`):

```csharp
var numbers = dataContext
    .FromTableFunction(() => NORM.PG_SQL.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => NORM.PG_SQL.unnest(NORM.Param<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`NORM.MS_SQL.string_split` — из SQL Server 2016+ и возвращает `NORM.IStringSplitRow` (единственная колонка
`value`). Порядок фрагментов не гарантируется, поэтому добавляйте `order by`, если важен порядок входной
строки:

```csharp
var csv = "a,b,c";
var separator = ",";

var fragments = dataContext
    .FromTableFunction(() => NORM.MS_SQL.string_split(csv, separator))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value from string_split(@csv, @separator) as [t1]
```

`NORM.MS_SQL.openjson` — из SQL Server 2016+ и возвращает `NORM.IOpenJsonRow` (`key`/`value`/`type`).
Схема по умолчанию даёт свойства JSON-объекта или элементы JSON-массива; для типизированной проекции
объявите свой `[SqlTableFunction("openjson")]`-хелпер, чья форма строки соответствует предложению
`WITH (...)`:

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .FromTableFunction(() => NORM.MS_SQL.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type from openjson(@json) as [t1]
```

Сопоставленная функция должна существовать в базе — nextorm только генерирует вызов, он её не создаёт, —
поэтому используйте хелпер только на провайдере, где она определена. Встроенные хелперы гейтятся
`ISqlDialect.SupportsTableFunction`: PostgreSQL разрешает `generate_series`/`unnest`, SQL Server —
`string_split`/`openjson`, а любой другой провайдер отклоняет их с `NotSupportedException`
(пользовательская `[SqlTableFunction]` не гейтится).

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | Вызов функции генерируется без псевдонима для простого источника; псевдоним всё равно генерируется, когда источник объединяется. |
| SQL Server | Производный источник получает псевдоним (`as [t1]`). |
| PostgreSQL | Псевдоним обязателен и генерируется всегда (`as "t1"`). |
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
`test/nextorm.integration.tests/CommonTestSuite.Tvf.cs:34`, `:47`, `:61`, `:76`;
`test/nextorm.core.tests/SqlTableFunctionAttributeTests.cs:8`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:1453`, `:1462`, `:1475`, `:1489`, `:1503`;
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:1044`, `:1066`, `:1080`, `:1094`;
`test/nextorm.postgres.tests/SqlGenerationTests.cs:976`, `:998`, `:1012`, `:1026`.
