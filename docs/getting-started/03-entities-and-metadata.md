# Entities and metadata

> Map CLR types and members to tables and columns with attributes or a fluent builder, or skip entities entirely and address columns by name with `TableAlias`.

**Prerequisites:** [Installation](01-installation.md) · [Quickstart](02-quickstart.md).

## Overview

NextORM needs to know three things before it can build SQL: the table a type maps to, the column each
property maps to, and how to create materialised rows. Metadata is declared in one of three ways:

1. **Attributes** on an interface or on a class (`[SqlTable]`, `[Column]`, optionally `[Table]`).
2. A **fluent builder** passed to `Create<T>(cfg => …)`.
3. **No metadata at all** - start from a raw table name with `From("table")` and read columns through a
   `TableAlias` (`tbl.Int("id")`, `tbl.String("name")`, …).

Metadata is resolved lazily and cached **process-wide** in `DataContextCache.Metadata`, keyed by type,
the first time a type is queried through `Create<T>()`. Because of that cache:

* the config delegate passed to `Create<T>(…)` runs only on the first call for that type in the
  process;
* later calls for the same type reuse the already-built `IEntityMeta` and ignore a new delegate;
* the in-memory and SQL contexts share the same metadata (the in-memory context exposes it as
  `InMemoryContext.Metadata`).

A type that was never registered throws `BuildSqlCommandException` when used as a `FROM` source, naming
the type.

## Attributes

`[SqlTable]` (in `nextorm.core`) sets the table name; `[Column]` from
`System.ComponentModel.DataAnnotations.Schema` sets the column name. `[Table]` from the same namespace
is also recognised as an alternative to `[SqlTable]`.

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

* **Table name** - taken from `[SqlTable]` (preferred) or `[Table]`. When neither is present the CLR
  type name is used, so `SimpleEntity` maps to `SimpleEntity`. Attribute lookup walks the type and then
  its interfaces; an attribute on an interface wins when both are present.
* **Column name** - taken from `[Column]` when present, otherwise the property name is used verbatim.
  Only properties with a **setter** (`CanWrite`) are mapped; get-only properties are skipped.
* **`[Key]`** - `System.ComponentModel.DataAnnotations.KeyAttribute`, shown here to document which
  column is the primary key. NextORM does not read it for query generation (there is no DDL generation
  and no change tracking), but it keeps the entity valid for upstream schema tooling and is the
  conventional choice.

### Interface plus class

The attribute metadata can live on an interface while the query is strongly typed with a class:

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

`EntityBuilder` reads the mapping from the interface: it resolves the table name from the implemented
interfaces and, for each writable property, looks up the matching interface property to find `[Column]`.
Both `dataContext.Create<ISimpleEntity>()` and `dataContext.Create<SimpleEntity>()` then produce the same
SQL. A class with attributes directly on it works the same way, with no interface required.

## Fluent registration

Instead of attributes, pass a configuration delegate to `Create<T>()`. `EntityBuilder<T>` exposes
`Table(string)` and `Property(Expression<Func<T, object>>)`; the returned `EntityPropertyBuilder<T>`
exposes `HasColumnName(string)`.

```csharp
dataContext.Create<SimpleEntity>(cfg => cfg
    .Table("simple_entity")
    .Property(x => x.Id)
    .HasColumnName("id"));
```

To map several properties, call `Property` once per member:

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

Rules for the fluent path (`EntityBuilder<T>.Build`):

* if `Table(...)` is omitted, the table name is auto-built from attributes and then the type name;
* if at least one `Property(...)` is configured, **only** those properties are mapped - auto-discovered
  properties are not merged in;
* if no property is configured, all writable properties are mapped automatically by name/`[Column]`.

## Entities from a raw table: `TableAlias`

No entity and no metadata are required to query. Start from a table name with `From("table")` and read
columns through the `TableAlias` passed to `Select`/`Where`:

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

`TableAlias` accessor methods (each takes the column name and returns the CLR value for that type):

| Method | Returns | Method | Returns |
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

`TableAlias` also has an indexer, `this[string]`, returning a `TableColumn` with the typed
`AsInt`, `AsString` and `AsNullableString` accessors - useful when the same column alias is referenced
from a joined/CTE query:

```csharp
var query = dataContext.From("complex_entity")
    .Where(c => c["id"].AsInt > 1)
    .Select(c => new { Id = c["id"].AsInt });
```

`From` is available both on the concrete `DbContext` (`dataContext.From("simple_entity")`) and as an
extension on `IDataContext`, so it works whether the context is used through its concrete type or the
interface. Independently of entities, `From` can also wrap a subquery
(`dataContext.From(innerQuery)`) or another entity builder (`dataContext.From(entity)`).

## Provider differences

Table and column names are emitted **verbatim** - NextORM does not quote or case-fold identifiers - so
the string in `[SqlTable]`/`[Column]`/`Table(...)`/`HasColumnName(...)` must match the catalog name
exactly for every provider.

| Provider | Behaviour |
|---|---|
| SQLite | Names used as given. |
| SQL Server | Names used as given. |
| PostgreSQL | Names used as given; an unquoted mixed-case or reserved-word identifier is still folded by the server, so declare the catalog spelling. |
| In-memory | Identical metadata, shared with the SQL contexts through `DataContextCache`. |

## See also

* [Installation](01-installation.md)
* [Quickstart](02-quickstart.md)
* [Dependency injection](04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `test/nextorm.integration.tests/Entities.cs:7`;
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`test/nextorm.sqlite.tests/MetadataRegistrationTests.cs:18`;
`src/nextorm.core/DataContext/Meta/EntityBuilder.cs:111`;
`src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:16`;
`src/nextorm.core/DataContext/DataContextCache.cs:20`;
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:116`.
