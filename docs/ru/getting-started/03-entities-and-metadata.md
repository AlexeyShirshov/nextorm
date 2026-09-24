# Сущности и метаданные

> Отображайте типы и члены CLR на таблицы и столбцы с помощью атрибутов или fluent-построителя либо вовсе откажитесь от сущностей и адресуйте столбцы по имени через [`TableAlias`](xref:NextORM.Core.TableAlias).

**Предварительные требования:** [Установка](01-installation.md) · [Быстрый старт](02-quickstart.md).

## Обзор

Прежде чем строить SQL, NextORM нужно знать три вещи: таблицу, на которую отображается тип, столбец, на который отображается каждое свойство, и то, как создавать материализованные строки. Отображение получается одним из следующих способов:

1. **Атрибуты** на интерфейсе или классе (`[SqlTable]`, `[Column]`, при необходимости `[Table]`).
2. **Fluent-построитель**, передаваемый в `From<T>(cfg => …)`.
3. **Ничего** - `From<T>()` строит отображение из формы типа CLR (таблица = имя типа, столбец = имя
   свойства). См. [Типы без атрибутов](#типы-без-атрибутов).
4. **Имя таблицы** через `From("table")` и доступ к столбцам через
   [`TableAlias`](xref:NextORM.Core.TableAlias) (`tbl.GetInt32("id")`, `tbl.GetString("name")`, …) -
   без типа сущности вовсе.

Имена, автоматически построенные в п. 3, можно привести к написанию базы данных с помощью
[соглашения об именовании](#соглашения-об-именовании); имена, объявленные атрибутом или fluent-отображением,
всегда берутся дословно.

Метаданные разрешаются лениво и кэшируются **на уровне процесса** в [`Metadata`](xref:NextORM.Core.DataContextCache.Metadata) по типу при первом запросе типа через [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)). Из-за этого кэша:

* делегат конфигурации, переданный в `From<T>(…)`, выполняется только при первом вызове для этого типа в
  процессе;
* последующие вызовы для того же типа используют уже построенный [`IEntityMetadata`](xref:NextORM.Core.IEntityMetadata) и игнорируют новый делегат;
* in-memory- и SQL-контексты используют одни и те же метаданные (in-memory-контекст предоставляет их как
  [`Metadata`](xref:NextORM.Core.InMemoryDataContext.Metadata)).

Тип, который не был зарегистрирован, при использовании в качестве источника `FROM` выбрасывает [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException) с указанием имени типа.

## Атрибуты

`[SqlTable]` (в [`NextORM.Core`](xref:NextORM.Core)) задаёт имя таблицы; `[Column]` из `System.ComponentModel.DataAnnotations.Schema` задаёт имя столбца. `[Table]` из того же пространства имён также распознаётся как альтернатива `[SqlTable]`.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
```

```sql
select id from simple_entity
```

* **Имя таблицы** - берётся из `[SqlTable]` (предпочтительно) или `[Table]`. Когда нет ни того, ни другого,
  используется имя типа CLR, так что `SimpleEntity` отображается на `SimpleEntity`. Поиск атрибута проходит
  по типу, а затем по его интерфейсам; при наличии обоих побеждает атрибут на интерфейсе.
* **Имя столбца** - берётся из `[Column]`, если он есть, иначе имя свойства используется дословно.
  Отображаются только свойства с **сеттером** (`CanWrite`); свойства только для чтения пропускаются.
* **`[Key]` и `[DatabaseGenerated]`** - `KeyAttribute` помечает первичный ключ, а
  `DatabaseGeneratedAttribute` - столбец, генерируемый базой данных (`DatabaseGeneratedOption.Identity`
  или `.Computed`). Запросы их игнорируют, но билдер вставки читает их: столбцы identity/computed
  исключаются из записываемых значений (см. [Изменение данных (INSERT)](../guide/19-insert-statement.md)).
  Если ключ не объявлен, свойство с именем `Id` или `<TypeName>Id` считается ключом по соглашению.
* **Бинарные столбцы** - свойство `byte[]` отображается на бинарный столбец (`bytea` в PostgreSQL,
  `varbinary`/`image` в SQL Server, `blob` в SQLite). `byte[]` можно также проецировать напрямую
  (`Select(x => x.Data)`) и сравнивать с параметром `byte[]` через [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)).

### Интерфейс плюс класс

Метаданные атрибутов могут находиться на интерфейсе, тогда как запрос строго типизирован классом:

```csharp
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

public class SimpleEntity : ISimpleEntity
{
    public int Id { get; set; }
}
```

`EntityMetadataBuilder` читает отображение из интерфейса: он определяет имя таблицы по реализуемым интерфейсам и для каждого записываемого свойства ищет совпадающее свойство интерфейса, чтобы найти `[Column]`. Тогда и `dataContext.From<ISimpleEntity>()`, и `dataContext.From<SimpleEntity>()` дают один и тот же SQL. Класс с атрибутами непосредственно на нём работает так же, без необходимости в интерфейсе.

## Типы без атрибутов

Типу не нужны атрибуты, чтобы быть доступным для запросов: [`From<T>()`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) строит отображение по форме типа (и кэширует его) при первом использовании типа.

* **Имя таблицы** - имя типа CLR дословно. `Product` отображается на `Product`; интерфейс сохраняет свой
  префикс, поэтому `IProduct` отображается на `IProduct` (а не на `products`).
* **Имя столбца** - имя свойства дословно - без snake_case и без смены регистра. `Id` отображается на `Id`.
* **Только записываемые свойства** - свойство отображается, если у него есть сеттер, в том числе
  непубличный; свойство только для чтения (`{ get; }`) пропускается.
* Построенные имена выводятся как есть, поэтому они должны точно совпадать с каталогом; включите
  [Квотирование идентификаторов](#квотирование-идентификаторов), чтобы провайдер заквотировал их, или примените
  [соглашение об именовании](#соглашения-об-именовании), чтобы транслировать их.

```csharp
public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; }            // только get -> не отображается
    public int Stock { get; private set; }   // непубличный сеттер -> отображается
}

var rows = await dataContext.From<Product>()
    .Select(p => new { p.Id, p.Name })
    .ToListAsync();
```

```sql
select Id, Name from Product
```

Голый интерфейс годится как источник запроса, пока проекция даёт скаляр, анонимный тип, кортеж или DTO -
у интерфейса нет конструктора, поэтому материализовать его целиком как строку нельзя:

```csharp
public interface IProduct
{
    int Id { get; set; }
    string? Name { get; set; }
}

var names = await dataContext.From<IProduct>()
    .Where(p => p.Id > 1)
    .Select(p => p.Name)
    .ToListAsync();
```

```sql
select Name from IProduct
 where (Id > 1)
```

Поскольку автоматически построенное имя таблицы сохраняет префикс `I`, сущности на основе интерфейса
обычно задают явное отображение - см. [Атрибуты](#атрибуты) или [Fluent-регистрация](#fluent-регистрация).

## Соглашения об именовании

Автоматически построенные имена из раздела [Типы без атрибутов](#типы-без-атрибутов) по умолчанию выводятся дословно. Соглашение об именовании приводит их к написанию базы данных - например, встроенный `SnakeCaseNamingConvention` превращает `SimpleEntity` в `simple_entity`, а `FirstName` в `first_name`. Настройте его на контексте:

```csharp
var builder = new DataContextBuilder()
    .UseNamingConvention(SnakeCaseNamingConvention.Instance)
    .UseSqlite(connection);
```

или для отдельной команды через `WithNamingConvention(…)` (доступно на [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) и
[`QueryCommand<T>`](xref:NextORM.Core.QueryCommand`1)), точно так же, как `WithQuotedIdentifiers`:

```csharp
public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

var rows = await dataContext.From<Product>()
    .Where(p => p.Id > 1)
    .Select(p => new { p.Id, p.Name })
    .ToListAsync();

// select id, name from product where (id > 1)
```

Соглашение применяется только к именам, производным от имени типа/свойства CLR. Имя, объявленное через
`[SqlTable]`/`[Column]` или fluent-отображением, берётся дословно, поэтому смешанные отображения работают:

```csharp
[SqlTable("ExplicitTable")]
public class ExplicitEntity
{
    [Column("ExplicitColumn")] public int Value { get; set; }
    public string? FirstName { get; set; }   // -> first_name
}
```

Для интерфейсного источника ведущая `I` отбрасывается, если за ней следует ещё одна заглавная
(`IProduct` становится `product`, но `Idle` остаётся `idle`). Идущие подряд заглавные считаются одним
словом, поэтому `OrderID` становится `order_id`, а `HTTPServer` - `http_server`. Сочетайте соглашение с
[квотированием идентификаторов](#квотирование-идентификаторов), если транслированные имена всё ещё нужно квотировать.

Настройка для отдельной команды действует на всю внешнюю команду, поэтому задавайте `WithNamingConvention`
на выполняемом запросе, а не на вложенном подзапросе.

У [`INamingConvention`](xref:NextORM.Core.INamingConvention) два члена - `TableName(string clrName, bool isInterface)`
и `ColumnName(string propertyName)` - поэтому проект может подставить собственное написание; верните входное
значение без изменений, чтобы отказаться от трансляции.

## Fluent-регистрация

Вместо атрибутов передайте делегат конфигурации в [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)). [`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) предоставляет `Table(string)` и `Property(Expression<Func<T, object>>)`; возвращаемый [`EntityPropertyBuilder<T>`](xref:NextORM.Core.EntityPropertyBuilder`1) предоставляет `HasColumnName(string)`.

```csharp
dataContext.From<SimpleEntity>(cfg => cfg
    .Table("simple_entity")
    .Property(x => x.Id)
    .HasColumnName("id"));
```

Чтобы отобразить несколько свойств, вызывайте [`Property`](xref:NextORM.Core.EntityMetadataBuilder`1.Property(System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) по одному разу на член:

```csharp
public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

dataContext.From<Product>(cfg =>
{
    cfg.Table("products");
    cfg.Property(x => x.Id).HasColumnName("id");
    cfg.Property(x => x.Name).HasColumnName("name");
});
```

Правила fluent-пути ([`Build`](xref:NextORM.Core.EntityMetadataBuilder`1.Build)):

* если `Table(...)` опущен, имя таблицы автоматически строится из атрибутов, а затем из имени типа;
* если настроено хотя бы одно `Property(...)`, отображаются **только** эти свойства - автоматически
  обнаруженные свойства не добавляются;
* если не настроено ни одного свойства, все записываемые свойства отображаются автоматически по
  имени/`[Column]`.

## Сущности из необработанной таблицы: [`TableAlias`](xref:NextORM.Core.TableAlias)

Чтобы выполнить запрос, не нужны ни сущность, ни метаданные. Начните с имени таблицы через `From("table")` и читайте столбцы через [`TableAlias`](xref:NextORM.Core.TableAlias), передаваемый в [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))/[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})):

```csharp
await foreach (var row in dataContext.From("simple_entity")
                                     .Select(tbl => new { Id = tbl.GetInt64("id") })
                                     .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}
```

```sql
select id from simple_entity
```

Методы-аксессоры [`TableAlias`](xref:NextORM.Core.TableAlias) (каждый принимает имя столбца и возвращает значение CLR для этого типа):

| Метод | Возвращает | Метод | Возвращает |
|---|---|---|---|
| `GetInt32(string)` | `int` | `GetNullableInt32(string)` | `int?` |
| `GetInt64(string)` | `long` | `GetNullableInt64(string)` | `long?` |
| `GetInt16(string)` | `short` | `GetNullableInt16(string)` | `short?` |
| `GetString(string)` | `string` | `GetNullableString(string)` | `string?` |
| `GetSingle(string)` | `float` | `GetNullableSingle(string)` | `float?` |
| `GetDouble(string)` | `double` | `GetNullableDouble(string)` | `double?` |
| `GetDateTime(string)` | `DateTime` | `GetNullableDateTime(string)` | `DateTime?` |
| `GetDecimal(string)` | `decimal` | `GetNullableDecimal(string)` | `decimal?` |
| `GetByte(string)` | `byte` | `GetNullableByte(string)` | `byte?` |
| `GetBoolean(string)` | `bool` | `GetNullableBoolean(string)` | `bool?` |
| `GetGuid(string)` | `Guid` | `GetNullableGuid(string)` | `Guid?` |
| `GetBytes(string)` | `byte[]` | `GetNullableBytes(string)` | `byte[]?` |
| `GetColumn(string)` | `object` | | |

[`TableAlias`](xref:NextORM.Core.TableAlias) также имеет индексатор `this[string]`, возвращающий [`TableColumn`](xref:NextORM.Core.TableColumn) с типизированными аксессорами [`AsInt`](xref:NextORM.Core.TableColumn.AsInt), [`AsString`](xref:NextORM.Core.TableColumn.AsString), [`AsNullableString`](xref:NextORM.Core.TableColumn.AsNullableString), [`AsBytes`](xref:NextORM.Core.TableColumn.AsBytes) и [`AsNullableBytes`](xref:NextORM.Core.TableColumn.AsNullableBytes) - это полезно, когда один и тот же псевдоним столбца упоминается в запросе с соединением/CTE:

```csharp
var query = dataContext.From("complex_entity")
    .Where(c => c["id"].AsInt > 1)
    .Select(c => new { Id = c["id"].AsInt });
```

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) доступен и на конкретном [`DataContext`](xref:NextORM.Core.DataContext) (`dataContext.From("simple_entity")`), и как расширение на [`IDataContext`](xref:NextORM.Core.IDataContext), поэтому работает независимо от того, используется контекст через конкретный тип или через интерфейс. Независимо от сущностей, [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) также может обернуть подзапрос (`dataContext.From(innerQuery)`) или другой построитель сущности (`dataContext.From(entity)`).

## Квотирование идентификаторов

По умолчанию имена таблиц и столбцов выводятся дословно (см. [Различия провайдеров](#различия-провайдеров)),
поэтому физическое имя, совпадающее с зарезервированным словом, приходится заранее квотировать в
маппинге `[SqlTable]`/`[Column]`. Квотирование идентификаторов позволяет провайдеру сделать это самому.
Включите его на контексте:

```csharp
var builder = new DataContextBuilder()
    .UseQuotedIdentifiers()
    .UseSqlite(connection);
```

или на отдельном запросе через `WithQuotedIdentifiers()` (есть на [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1)
и [`QueryCommand<T>`](xref:NextORM.Core.QueryCommand`1)); `WithQuotedIdentifiers(false)` явно выключает его для
запроса, даже если на контексте он включён:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .WithQuotedIdentifiers()
    .Where(e => e.Id > 1)
    .Select(e => new { e.Id })
    .ToListAsync();

// select "id" from "simple_entity" where ("id" > 1)     (PostgreSQL, SQLite)
// select [id] from [simple_entity] where ([id] > 1)     (SQL Server)
// select `id` from `simple_entity` where (`id` > 1)     (MySQL, MariaDB, ClickHouse)
```

Каждый провайдер использует свой разделитель и удваивает внутренний (`"a""b"`, `[a]]b]`, `` `a``b` ``).
Схемно-квалифицированное имя квотируется по частям (`"sales"."orders"`), а псевдонимы столбцов сохраняют
прежнее квотирование. Поэтому зарезервированное слово в физическом имени работает без ручного
квотирования:

```csharp
[SqlTable("orders")]
public interface IOrder
{
    [Column("select")] int Value { get; set; }
}
```

Квотирование сочетается с [соглашениями об именовании](#соглашения-об-именовании): сначала соглашение
транслирует автоматически построенное имя, затем квотирование защищает транслированный идентификатор.

## Различия провайдеров

Имена таблиц и столбцов выводятся **дословно** по умолчанию - NextORM не заключает идентификаторы в кавычки и не меняет регистр - поэтому строка в `[SqlTable]`/`[Column]`/`Table(...)`/`HasColumnName(...)` должна точно совпадать с именем в каталоге для каждого провайдера. [Квотирование идентификаторов](#квотирование-идентификаторов) включает квотирование на стороне провайдера.

| Провайдер | Поведение |
|---|---|
| SQLite | Имена используются как заданы. |
| SQL Server | Имена используются как заданы. |
| PostgreSQL | Имена используются как заданы; незаключённый в кавычки идентификатор со смешанным регистром или зарезервированным словом всё равно сворачивается сервером, поэтому объявляйте написание из каталога. |
| MySQL | Имена используются как заданы; при отрисовке SQL идентификаторы и псевдонимы выделяются обратными кавычками. |
| MariaDB | Имена используются как заданы; идентификаторы и псевдонимы выделяются обратными кавычками (драйвер и диалект MySQL). |
| ClickHouse | Имена используются как заданы; при отрисовке SQL идентификаторы и псевдонимы выделяются обратными кавычками. |
| In-memory | Идентичные метаданные, общие с SQL-контекстами через [`DataContextCache`](xref:NextORM.Core.DataContextCache). |

## См. также

* [Установка](01-installation.md)
* [Быстрый старт](02-quickstart.md)
* [Внедрение зависимостей](04-dependency-injection.md)
* [Индекс документации](../index.md)

---

Source: `tests/nextorm.integration.tests/Entities.cs:7`;
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`tests/nextorm.sqlite.tests/MetadataRegistrationTests.cs:18`;
`src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:111`;
`src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:16`;
`src/nextorm.core/DataContext/DataContextCache.cs:20`;
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:116`.
