# Запросы и проекции

> Формируйте результат запроса с помощью [`Select`](xref:NextORM.Core.EntityBuilder`1): одна колонка, анонимный тип, DTO или запись (record), кортеж, инициализатор членов, вложенная сущность или вычисляемая колонка.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)

## Обзор

`dataContext.From<TEntity>()` возвращает [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Каждый запрос начинается с
проецирования этой сущности с помощью [`Select`](xref:NextORM.Core.EntityBuilder`1):

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

Лямбда не выполняется - она транслируется в список `SELECT` генерируемого оператора.
[`Select`](xref:NextORM.Core.EntityBuilder`1) возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); терминальный метод ([`ToListAsync`](xref:NextORM.Core.EntityBuilder`1), [`FirstAsync`](xref:NextORM.Core.EntityBuilder`1),
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilder`1), [`AnyAsync`](xref:NextORM.Core.EntityBuilder`1), ...) выполняет его. См. [Сортировку и постраничную выборку](05-sorting-and-paging.md)
для терминальных методов и их асинхронных форм.

Правила, применимые к любой проекции:

* Форма лямбды определяет список выборки. Колонки, которые не проецируются, не читаются.
* Проецируемый член, имя которого совпадает с именем отображаемой колонки (без учёта регистра),
  генерируется без псевдонима. Переименованный или вычисляемый член получает псевдоним: `as 'Calc'` в
  SQLite, `as [Calc]` в SQL Server, `as "Calc"` в PostgreSQL.
* Аргументы конструктора, элементы кортежа и присваивания в инициализаторе членов отображаются в
  элементы списка выборки; материализатор времени выполнения строит объект из reader'а.
* В in-memory провайдере SQL не существует; то же самое выражение компилируется и выполняется над
  набором данных в памяти.

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных
тестов: `simple_entity` содержит идентификаторы `1`-`10`, а `complex_entity` — три строки
(`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

## Анонимный тип

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`entity.Id` отображается на колонку `id` таблицы `simple_entity`, поэтому псевдоним не генерируется.

## Изменённые и вычисляемые колонки

Член может вычисляться из других колонок или констант:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Id = entity.Id + 1 })
    .ToListAsync();
```

```sql
select (id + 1) as 'Id' from simple_entity
```

Вывод:

| Id |
|----|
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |
| 11 |

Арифметическое выражение заключается в скобки и, поскольку оно не является обычной колонкой, получает
псевдоним с именем проецируемого члена. Именование члена делает псевдоним стабильным для объемлющего
запроса:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.Id + 1 })
    .ToListAsync();
```

```sql
select id, (id + 1) as 'Calc' from complex_entity
```

Строковые члены конкатенируются оператором конкатенации провайдера (`||` в SQLite и PostgreSQL, `+` в
SQL Server):

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Display = it.String + "/" + it.RequiredString })
    .ToListAsync();
```

```sql
-- SQLite
select id, ((somestring || '/') || requiredstring) as 'Display' from complex_entity
```

Вывод:

| Id | Display |
|----|---------|
| 1 | dadfasd/sdf |
| 2 | xxx/asdfgoi |
| 3 | null |

## DTO

Неанонимный тип проецируется через свой конструктор:

```csharp
public class SimpleEntityDto(int id)
{
    public int Id { get; } = id;
}

var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new SimpleEntityDto(entity.Id))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Запись (record)

Позиционные записи также проецируются через свой конструктор:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`tbl.GetInt64("id")` уже имеет тип члена записи, поэтому преобразование не добавляется. Когда проецируемый
тип шире или уже исходной колонки, nextorm отображает преобразование как `cast(...)`.

## Кортеж

```csharp
var rows = await dataContext.From("simple_entity")
    .Select(tbl => new Tuple<long>(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Item1 |
|-------|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Инициализатор членов

Вместо конструктора проекция может использовать инициализатор объекта:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Примитивная и скалярная проекция

[`Select`](xref:NextORM.Core.EntityBuilder`1) может возвращать одно значение вместо объекта строки:

```csharp
var ids = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id < 5)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select id from simple_entity where (id < 5)
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |

Логический член работает так же:

```csharp
var flags = await dataContext.From<ComplexEntity>()
    .Where(it => it.Boolean == true)
    .Select(it => it.Boolean)
    .ToListAsync();
```

```sql
select b from complex_entity where b = 1
```

Вывод:

| Boolean |
|---------|
| true |

## Вложенная сущность и вычисляемые колонки поверх проекции

[`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) сам может использоваться как источник другого запроса с помощью
`dataContext.From(query)`, поэтому внутреннюю проекцию (включая вычисляемые колонки) можно прочитать и
спроецировать снова:

```csharp
var inner = dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.String + it.String });

var rows = await dataContext.From(inner)
    .Select(t => new { t.Id, t.Calc })
    .ToListAsync();
```

```sql
-- SQLite: no derived-table alias is required
select id, Calc from (select id, (somestring || somestring) as 'Calc' from complex_entity)
```

SQL Server требует, чтобы производная таблица имела псевдоним, и PostgreSQL аналогично:

```sql
-- SQL Server
select id, Calc from (select id, (somestring + somestring) as [Calc] from complex_entity) as [t1]
```

## Подзапрос в качестве источника

`From(query)` также оборачивает отфильтрованный запрос, что соответствует подзапросу в SQL `FROM`:

```csharp
var inner = dataContext.From<SimpleEntity>()
    .Where(it => it.Id > 8)
    .Select(it => new { it.Id });

var rows = await dataContext.From(inner)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
-- SQLite
select id from (select id from simple_entity where (id > 8))
```

Вывод:

| Id |
|----|
| 9 |
| 10 |

## Табличная функция в качестве источника

`dataContext.FromTableFunction(() => ...)` использует табличную функцию как источник `FROM`. Встроенные
помощники покрывают распространённые функции, возвращающие набор; `SqlFunctions.Postgres.unnest`
разворачивает массив PostgreSQL в одну строку на элемент:

```csharp
var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

```sql
select unnest as "Value" from unnest(@norm_p0) as "t1"
```

Если полные имена `SqlFunctions.*` кажутся слишком громоздкими, импортируйте поверхность через
`using static NextORM.Core.SqlFunctions;` и опустите префикс класса:

```csharp
using static NextORM.Core.SqlFunctions;

var elements = dataContext
    .FromTableFunction(() => Postgres.unnest(Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`SqlFunctions.Postgres.generate_series(start, stop)` аналогично генерирует числовую последовательность.
Встроенные помощники включаются провайдером ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect)), а
пользовательская функция объявляется через `[SqlTableFunction]`; см.
[Табличные функции](13-table-valued-functions.md).

## Сэмплирование таблицы (`TABLESAMPLE`)

[`TableSample`](xref:NextORM.Core.EntityBuilder`1) добавляет модификатор `TABLESAMPLE` к
основной таблице, поэтому база читает только процент её строк вместо полного сканирования таблицы.
Процент должен находиться в диапазоне `(0, 100]`; метод сэмплирования по умолчанию —
[`TableSampleMethod.System`](xref:NextORM.Core.TableSampleMethod.System), а необязательное зерно (seed)
делает выборку повторяемой:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .TableSample(10, TableSampleMethod.System, seed: 42)
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- PostgreSQL
select id from simple_entity tablesample system (10) repeatable (42)

-- SQL Server
select id from simple_entity tablesample (10 percent) repeatable (42)
```

PostgreSQL поддерживает и `System`, и [`Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli);
SQL Server поддерживает только `System`. Все остальные провайдеры выбрасывают `NotSupportedException`
при построении SQL ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample) и
[`ITableSampleMethods.Render`](xref:NextORM.Core.ITableSampleMethods.Render)). Модификатор применяется только к
основной таблице запроса.

## JSON-вывод (SQL Server)

[`ForJson`](xref:NextORM.Core.QueryCommand`1) добавляет предложение SQL Server `FOR JSON`, поэтому база
возвращает один JSON-документ вместо строк ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)). Проекция должна быть
одним скаляром/колонкой, потому что набор результатов сворачивается в одну JSON-колонку:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true)
    .First();
```

```sql
select id, somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) строит документ по псевдонимам проекции, [`Auto`](xref:NextORM.Core.ForJsonMode.Auto) — по структуре
таблицы. Предложение ставится после `ORDER BY` и перед завершающим `OPTION (...)`. Остальные
провайдеры выбрасывают `NotSupportedException`.

## XML-вывод (SQL Server)

[`ForXml`](xref:NextORM.Core.QueryCommand`1) — XML-аналог ([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)); поддерживаются
`RAW`, `AUTO`, `EXPLICIT` и `PATH`, с необязательным именем элемента строки, обёрткой `ROOT('...')` и
флагом `ELEMENTS`:

```csharp
var xml = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id })
    .ForXml(ForXmlMode.Raw, elementName: "row", root: "items", elements: true)
    .First();
```

```sql
select id from complex_entity for xml raw('row'), root('items'), elements
```

`FOR JSON` и `FOR XML` взаимно исключают друг друга; их сочетание выбрасывает `NotSupportedException`.

## Блокировка строк (`FOR UPDATE` / `FOR SHARE`)

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) и
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) блокируют выбранные строки до конца
окружающей транзакции. PostgreSQL, MySQL и MariaDB генерируют завершающее предложение, которое
ставится последним — после `WHERE`, `ORDER BY` и запроса страницы; SQL Server вместо этого
привязывает табличный хинт `WITH (updlock)`/`WITH (holdlock)` к основной таблице:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id > 5)
    .ForUpdate()
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from simple_entity where (id > 5) for update

-- SQL Server
select id from simple_entity with (updlock) where (id > 5)
```

`ForUpdate()` блокирует строки монопольно; [`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare)
берёт разделяемую блокировку — PostgreSQL генерирует `for share`, MySQL/MariaDB генерируют
`lock in share mode`, а SQL Server — `holdlock` (разделяемая) против `updlock` для `ForUpdate`.
Предложение реализовано в PostgreSQL, MySQL, MariaDB и SQL Server
([`Lock`](xref:NextORM.Core.ISqlDialect.Lock); в SQL Server — через
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints));
все остальные провайдеры выбрасывают `NotSupportedException` при построении SQL.

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Псевдонимы колонок заключаются в одинарные кавычки (`as 'Calc'`); производные таблицы не требуют псевдонима. |
| SQL Server | Псевдонимы колонок заключаются в квадратные скобки (`as [Calc]`); каждая производная таблица должна иметь псевдоним (`as [t1]`). Конкатенация строк использует `+`. |
| PostgreSQL | Псевдонимы колонок заключаются в двойные кавычки (`as "Calc"`); производные таблицы должны иметь псевдоним (`as "t1"`). |
| MySQL | Псевдонимы колонок заключаются в обратные кавычки (`` as `Calc` ``); производные таблицы должны иметь псевдоним (`` as `t1` ``). Конкатенация строк использует `concat(a, b)`. |
| MariaDB | То же, что MySQL: обратные кавычки для псевдонимов, обязательный псевдоним производной таблицы и конкатенация через `concat(a, b)`. |
| ClickHouse | Псевдонимы колонок заключаются в обратные кавычки (`` as `Calc` ``); производные таблицы должны иметь псевдоним (`` as `t1` ``). Конкатенация строк использует `concat(a, b)`. |
| In-memory | SQL не генерируется; делегаты проекции компилируются и выполняются над объектами в памяти. |

## См. также

* [Фильтрация (WHERE)](02-filtering-where.md)
* [Сортировка и постраничная выборка](05-sorting-and-paging.md)
* [Табличные функции](13-table-valued-functions.md)
* [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)
* [Обзор провайдеров](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:26`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:45`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:75`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:94`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:103`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:112`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:122`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:352`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:367`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:382`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:727`;
`tests/nextorm.integration.tests/TestModels.cs:3`;
`tests/nextorm.integration.tests/TestModels.cs:12`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:217`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:242`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:268`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:223`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1117`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1132`.
