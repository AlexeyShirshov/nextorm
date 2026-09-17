# Запросы и проекции

> Формируйте результат запроса с помощью `Select`: одна колонка, анонимный тип, DTO или запись (record), кортеж, инициализатор членов, вложенная сущность или вычисляемая колонка.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)

## Обзор

`dataContext.Create<TEntity>()` возвращает `Entity<TEntity>`. Каждый запрос начинается с
проецирования этой сущности с помощью `Select`:

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

Лямбда не выполняется - она транслируется в список `SELECT` генерируемого оператора.
`Select` возвращает `QueryCommand<TResult>`; терминальный метод (`ToListAsync`, `FirstAsync`,
`ToAsyncEnumerable`, `AnyAsync`, ...) выполняет его. См. [Сортировку и постраничную выборку](05-sorting-and-paging.md)
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

## Анонимный тип

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

`entity.Id` отображается на колонку `id` таблицы `simple_entity`, поэтому псевдоним не генерируется.

## Изменённые и вычисляемые колонки

Член может вычисляться из других колонок или констант:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(entity => new { Id = entity.Id + 1 })
    .ToListAsync();
```

```sql
select (id + 1) as 'Id' from simple_entity
```

Арифметическое выражение заключается в скобки и, поскольку оно не является обычной колонкой, получает
псевдоним с именем проецируемого члена. Именование члена делает псевдоним стабильным для объемлющего
запроса:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.Id + 1 })
    .ToListAsync();
```

```sql
select id, (id + 1) as 'Calc' from complex_entity
```

Строковые члены конкатенируются оператором конкатенации провайдера (`||` в SQLite и PostgreSQL, `+` в
SQL Server):

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Select(it => new { it.Id, Display = it.String + "/" + it.RequiredString })
    .ToListAsync();
```

```sql
-- SQLite
select id, ((somestring || '/') || requiredstring) as 'Display' from complex_entity
```

## DTO

Неанонимный тип проецируется через свой конструктор:

```csharp
public class SimpleEntityDto(int id)
{
    public int Id { get; } = id;
}

var rows = await dataContext.Create<SimpleEntity>()
    .Select(entity => new SimpleEntityDto(entity.Id))
    .ToListAsync();
```

```sql
select id from simple_entity
```

## Запись (record)

Позиционные записи также проецируются через свой конструктор:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.Long("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

`tbl.Long("id")` уже имеет тип члена записи, поэтому преобразование не добавляется. Когда проецируемый
тип шире или уже исходной колонки, nextorm отображает преобразование как `cast(...)`.

## Кортеж

```csharp
var rows = await dataContext.From("simple_entity")
    .Select(tbl => new Tuple<long>(tbl.Long("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

## Инициализатор членов

Вместо конструктора проекция может использовать инициализатор объекта:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

## Примитивная и скалярная проекция

`Select` может возвращать одно значение вместо объекта строки:

```csharp
var ids = await dataContext.Create<SimpleEntity>()
    .Where(it => it.Id < 5)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select id from simple_entity where (id < 5)
```

Логический член работает так же:

```csharp
var flags = await dataContext.Create<ComplexEntity>()
    .Where(it => it.Boolean == true)
    .Select(it => it.Boolean)
    .ToListAsync();
```

```sql
select b from complex_entity where b = 1
```

## Вложенная сущность и вычисляемые колонки поверх проекции

`QueryCommand<TResult>` сам может использоваться как источник другого запроса с помощью
`dataContext.From(query)`, поэтому внутреннюю проекцию (включая вычисляемые колонки) можно прочитать и
спроецировать снова:

```csharp
var inner = dataContext.Create<ComplexEntity>()
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
var inner = dataContext.Create<SimpleEntity>()
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

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Псевдонимы колонок заключаются в одинарные кавычки (`as 'Calc'`); производные таблицы не требуют псевдонима. |
| SQL Server | Псевдонимы колонок заключаются в квадратные скобки (`as [Calc]`); каждая производная таблица должна иметь псевдоним (`as [t1]`). Конкатенация строк использует `+`. |
| PostgreSQL | Псевдонимы колонок заключаются в двойные кавычки (`as "Calc"`); производные таблицы должны иметь псевдоним (`as "t1"`). |
| In-memory | SQL не генерируется; делегаты проекции компилируются и выполняются над объектами в памяти. |

## См. также

* [Фильтрация (WHERE)](02-filtering-where.md)
* [Сортировка и постраничная выборка](05-sorting-and-paging.md)
* [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)
* [Обзор провайдеров](../providers/overview.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:26`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:45`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:75`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:94`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:103`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:112`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:122`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:352`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:367`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:382`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:727`;
`test/nextorm.integration.tests/TestModels.cs:3`;
`test/nextorm.integration.tests/TestModels.cs:12`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:111`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:217`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:242`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:268`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:223`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:248`.
