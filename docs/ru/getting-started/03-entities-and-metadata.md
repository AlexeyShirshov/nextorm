# Сущности и метаданные

> Отображайте типы и члены CLR на таблицы и столбцы с помощью атрибутов или fluent-построителя либо вовсе откажитесь от сущностей и адресуйте столбцы по имени через `TableAlias`.

**Предварительные требования:** [Установка](01-installation.md) · [Быстрый старт](02-quickstart.md).

## Обзор

Прежде чем строить SQL, NextORM нужно знать три вещи: таблицу, на которую отображается тип, столбец, на который отображается каждое свойство, и то, как создавать материализованные строки. Метаданные объявляются одним из трёх способов:

1. **Атрибуты** на интерфейсе или классе (`[SqlTable]`, `[Column]`, при необходимости `[Table]`).
2. **Fluent-построитель**, передаваемый в `Create<T>(cfg => …)`.
3. **Полное отсутствие метаданных** - начните с имени таблицы через `From("table")` и читайте столбцы через
   `TableAlias` (`tbl.Int("id")`, `tbl.String("name")`, …).

Метаданные разрешаются лениво и кэшируются **на уровне процесса** в `DataContextCache.Metadata` по типу при первом запросе типа через `Create<T>()`. Из-за этого кэша:

* делегат конфигурации, переданный в `Create<T>(…)`, выполняется только при первом вызове для этого типа в
  процессе;
* последующие вызовы для того же типа используют уже построенный `IEntityMeta` и игнорируют новый делегат;
* in-memory- и SQL-контексты используют одни и те же метаданные (in-memory-контекст предоставляет их как
  `InMemoryContext.Metadata`).

Тип, который не был зарегистрирован, при использовании в качестве источника `FROM` выбрасывает `BuildSqlCommandException` с указанием имени типа.

## Атрибуты

`[SqlTable]` (в `nextorm.core`) задаёт имя таблицы; `[Column]` из `System.ComponentModel.DataAnnotations.Schema` задаёт имя столбца. `[Table]` из того же пространства имён также распознаётся как альтернатива `[SqlTable]`.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using nextorm.core;

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
* **`[Key]`** - `System.ComponentModel.DataAnnotations.KeyAttribute`, показан здесь, чтобы задокументировать,
  какой столбец является первичным ключом. NextORM не читает его для генерации запросов (генерации DDL и
  отслеживания изменений нет), но он сохраняет сущность корректной для внешних инструментов работы со схемой
  и является общепринятым выбором.

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

`EntityBuilder` читает отображение из интерфейса: он определяет имя таблицы по реализуемым интерфейсам и для каждого записываемого свойства ищет совпадающее свойство интерфейса, чтобы найти `[Column]`. Тогда и `dataContext.Create<ISimpleEntity>()`, и `dataContext.Create<SimpleEntity>()` дают один и тот же SQL. Класс с атрибутами непосредственно на нём работает так же, без необходимости в интерфейсе.

## Fluent-регистрация

Вместо атрибутов передайте делегат конфигурации в `Create<T>()`. `EntityBuilder<T>` предоставляет `Table(string)` и `Property(Expression<Func<T, object>>)`; возвращаемый `EntityPropertyBuilder<T>` предоставляет `HasColumnName(string)`.

```csharp
dataContext.Create<SimpleEntity>(cfg => cfg
    .Table("simple_entity")
    .Property(x => x.Id)
    .HasColumnName("id"));
```

Чтобы отобразить несколько свойств, вызывайте `Property` по одному разу на член:

```csharp
public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

dataContext.Create<Product>(cfg =>
{
    cfg.Table("products");
    cfg.Property(x => x.Id).HasColumnName("id");
    cfg.Property(x => x.Name).HasColumnName("name");
});
```

Правила fluent-пути (`EntityBuilder<T>.Build`):

* если `Table(...)` опущен, имя таблицы автоматически строится из атрибутов, а затем из имени типа;
* если настроено хотя бы одно `Property(...)`, отображаются **только** эти свойства - автоматически
  обнаруженные свойства не добавляются;
* если не настроено ни одного свойства, все записываемые свойства отображаются автоматически по
  имени/`[Column]`.

## Сущности из необработанной таблицы: `TableAlias`

Чтобы выполнить запрос, не нужны ни сущность, ни метаданные. Начните с имени таблицы через `From("table")` и читайте столбцы через `TableAlias`, передаваемый в `Select`/`Where`:

```csharp
await foreach (var row in dataContext.From("simple_entity")
                                     .Select(tbl => new { Id = tbl.Long("id") })
                                     .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}
```

```sql
select id from simple_entity
```

Методы-аксессоры `TableAlias` (каждый принимает имя столбца и возвращает значение CLR для этого типа):

| Метод | Возвращает | Метод | Возвращает |
|---|---|---|---|
| `Int(string)` | `int` | `NullableInt(string)` | `int?` |
| `Long(string)` | `long` | `NullableLong(string)` | `long?` |
| `Short(string)` | `short` | `NullableShort(string)` | `short?` |
| `String(string)` | `string` | `NullableString(string)` | `string?` |
| `Float(string)` | `float` | `NullableFloat(string)` | `float?` |
| `Double(string)` | `double` | `NullableDouble(string)` | `double?` |
| `DateTime(string)` | `DateTime` | `NullableDateTime(string)` | `DateTime?` |
| `Decimal(string)` | `decimal` | `NullableDecimal(string)` | `decimal?` |
| `Byte(string)` | `byte` | `NullableByte(string)` | `byte?` |
| `Boolean(string)` | `bool` | `NullableBoolean(string)` | `bool?` |
| `Guid(string)` | `Guid` | `NullableGuid(string)` | `Guid?` |
| `Column(string)` | `object` | | |

`TableAlias` также имеет индексатор `this[string]`, возвращающий `TableColumn` с типизированными аксессорами `AsInt`, `AsString` и `AsNullableString` - это полезно, когда один и тот же псевдоним столбца упоминается в запросе с соединением/CTE:

```csharp
var query = dataContext.From("complex_entity")
    .Where(c => c["id"].AsInt > 1)
    .Select(c => new { Id = c["id"].AsInt });
```

`From` доступен и на конкретном `DbContext` (`dataContext.From("simple_entity")`), и как расширение на `IDataContext`, поэтому работает независимо от того, используется контекст через конкретный тип или через интерфейс. Независимо от сущностей, `From` также может обернуть подзапрос (`dataContext.From(innerQuery)`) или другой построитель сущности (`dataContext.From(entity)`).

## Различия провайдеров

Имена таблиц и столбцов выводятся **дословно** - NextORM не заключает идентификаторы в кавычки и не меняет регистр - поэтому строка в `[SqlTable]`/`[Column]`/`Table(...)`/`HasColumnName(...)` должна точно совпадать с именем в каталоге для каждого провайдера.

| Провайдер | Поведение |
|---|---|
| SQLite | Имена используются как заданы. |
| SQL Server | Имена используются как заданы. |
| PostgreSQL | Имена используются как заданы; незаключённый в кавычки идентификатор со смешанным регистром или зарезервированным словом всё равно сворачивается сервером, поэтому объявляйте написание из каталога. |
| In-memory | Идентичные метаданные, общие с SQL-контекстами через `DataContextCache`. |

## См. также

* [Установка](01-installation.md)
* [Быстрый старт](02-quickstart.md)
* [Внедрение зависимостей](04-dependency-injection.md)
* [Индекс документации](../index.md)

---

Source: `test/nextorm.integration.tests/Entities.cs:7`;
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`test/nextorm.sqlite.tests/MetadataRegistrationTests.cs:18`;
`src/nextorm.core/DataContext/Meta/EntityBuilder.cs:111`;
`src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:16`;
`src/nextorm.core/DataContext/DataContextCache.cs:20`;
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:116`.
