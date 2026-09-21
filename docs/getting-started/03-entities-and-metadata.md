# Entities and metadata

> Map CLR types and members to tables and columns with attributes or a fluent builder, or skip entities entirely and address columns by name with [`TableAlias`](xref:NextORM.Core.TableAlias).

**Prerequisites:** [Installation](01-installation.md) · [Quickstart](02-quickstart.md).

## Overview

NextORM needs to know three things before it can build SQL: the table a type maps to, the column each
property maps to, and how to create materialised rows. A mapping is obtained in one of these ways:

1. **Attributes** on an interface or on a class (`[SqlTable]`, `[Column]`, optionally `[Table]`).
2. A **fluent builder** passed to `From<T>(cfg => …)`.
3. **Nothing at all** - `From<T>()` auto-builds the mapping from the CLR type's shape (table = type
   name, column = property name). See [Types without attributes](#types-without-attributes).
4. A **raw table name** with `From("table")` and column access through a
   [`TableAlias`](xref:NextORM.Core.TableAlias) (`tbl.GetInt32("id")`, `tbl.GetString("name")`, …) -
   no entity type at all.

The names auto-derived in item 3 can be translated to the database's spelling with a
[naming convention](#naming-conventions); names declared with an attribute or a fluent mapping are
always taken verbatim.

Metadata is resolved lazily and cached **process-wide** in [`Metadata`](xref:NextORM.Core.DataContextCache.Metadata), keyed by type,
the first time a type is queried through [`From`](xref:NextORM.Core.DataContextExtensions). Because of that cache:

* the config delegate passed to `From<T>(…)` runs only on the first call for that type in the
  process;
* later calls for the same type reuse the already-built [`IEntityMetadata`](xref:NextORM.Core.IEntityMetadata) and ignore a new delegate;
* the in-memory and SQL contexts share the same metadata (the in-memory context exposes it as
  [`Metadata`](xref:NextORM.Core.InMemoryDataContext.Metadata)).

A type that was never registered throws [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException) when used as a `FROM` source, naming
the type.

## Attributes

`[SqlTable]` (in [`NextORM.Core`](xref:NextORM.Core)) sets the table name; `[Column]` from
`System.ComponentModel.DataAnnotations.Schema` sets the column name. `[Table]` from the same namespace
is also recognised as an alternative to `[SqlTable]`.

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

* **Table name** - taken from `[SqlTable]` (preferred) or `[Table]`. When neither is present the CLR
  type name is used, so `SimpleEntity` maps to `SimpleEntity`. Attribute lookup walks the type and then
  its interfaces; an attribute on an interface wins when both are present.
* **Column name** - taken from `[Column]` when present, otherwise the property name is used verbatim.
  Only properties with a **setter** (`CanWrite`) are mapped; get-only properties are skipped.
* **`[Key]`** - `System.ComponentModel.DataAnnotations.KeyAttribute`, shown here to document which
  column is the primary key. NextORM does not read it for query generation (there is no DDL generation
  and no change tracking), but it keeps the entity valid for upstream schema tooling and is the
  conventional choice.
* **Binary columns** - a `byte[]` property maps to a binary column (`bytea` on PostgreSQL,
  `varbinary`/`image` on SQL Server, `blob` on SQLite). A `byte[]` can also be projected directly
  (`Select(x => x.Data)`) and compared against a `byte[]` parameter of [`Parameter`](xref:NextORM.Core.SqlFunctions).

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

`EntityMetadataBuilder` reads the mapping from the interface: it resolves the table name from the implemented
interfaces and, for each writable property, looks up the matching interface property to find `[Column]`.
Both `dataContext.From<ISimpleEntity>()` and `dataContext.From<SimpleEntity>()` then produce the same
SQL. A class with attributes directly on it works the same way, with no interface required.

## Types without attributes

A type does not need any attribute to be queryable: [`From<T>()`](xref:NextORM.Core.DataContextExtensions) builds a mapping from
the type's shape (and caches it) the first time the type is used.

* **Table name** - the CLR type name, verbatim. `Product` maps to `Product`; an interface keeps its
  prefix, so `IProduct` maps to `IProduct` (not `products`).
* **Column name** - the property name, verbatim - no snake_case, no case folding. `Id` maps to `Id`.
* **Writable properties only** - a property is mapped when it has a setter, including a non-public one;
  a get-only property (`{ get; }`) is skipped.
* The auto-built names are emitted as-is, so they must match the catalog exactly; enable
  [Quoted identifiers](#quoted-identifiers) to have the provider quote them, or apply a
  [naming convention](#naming-conventions) to translate them.

```csharp
public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; }            // get-only -> not mapped
    public int Stock { get; private set; }   // non-public setter -> mapped
}

var rows = await dataContext.From<Product>()
    .Select(p => new { p.Id, p.Name })
    .ToListAsync();
```

```sql
select Id, Name from Product
```

A bare interface works as a query source as long as the projection produces a scalar, an anonymous
type, a tuple or a DTO - an interface has no constructor, so it cannot be materialised as a whole row:

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

Because the auto-built table name keeps the interface's `I` prefix, an interface-backed entity is
normally given an explicit mapping - see [Attributes](#attributes) or [Fluent registration](#fluent-registration).

## Naming conventions

The auto-built names of [Types without attributes](#types-without-attributes) are emitted verbatim by
default. A naming convention translates them to the database's spelling - for example the built-in
`SnakeCaseNamingConvention` turns `SimpleEntity` into `simple_entity` and `FirstName` into
`first_name`. Configure it on the context:

```csharp
var builder = new DataContextBuilder()
    .UseNamingConvention(SnakeCaseNamingConvention.Instance)
    .UseSqlite(connection);
```

or per command with `WithNamingConvention(…)` (available on [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) and
[`QueryCommand<T>`](xref:NextORM.Core.QueryCommand`1)), exactly like `WithQuotedIdentifiers`:

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

The convention applies only to names derived from the CLR type/property name. A name declared with
`[SqlTable]`/`[Column]` or a fluent mapping is taken verbatim, so mixed mappings work:

```csharp
[SqlTable("ExplicitTable")]
public class ExplicitEntity
{
    [Column("ExplicitColumn")] public int Value { get; set; }
    public string? FirstName { get; set; }   // -> first_name
}
```

For an interface source the leading `I` is dropped when it is followed by another capital
(`IProduct` becomes `product`, but `Idle` stays `idle`). Runs of capitals are treated as one word, so
`OrderID` becomes `order_id` and `HTTPServer` becomes `http_server`. Combine the convention with
[Quoted identifiers](#quoted-identifiers) when the translated names still need quoting.

The per-command setting resolves for the whole outer command, so set `WithNamingConvention` on the
query you execute rather than on a nested subquery.

[`INamingConvention`](xref:NextORM.Core.INamingConvention) has two members - `TableName(string clrName, bool isInterface)`
and `ColumnName(string propertyName)` - so a project can plug in its own spelling; return the input
unchanged to opt out.

## Fluent registration

Instead of attributes, pass a configuration delegate to [`From`](xref:NextORM.Core.DataContextExtensions). [`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) exposes
`Table(string)` and `Property(Expression<Func<T, object>>)`; the returned [`EntityPropertyBuilder<T>`](xref:NextORM.Core.EntityPropertyBuilder`1)
exposes `HasColumnName(string)`.

```csharp
dataContext.From<SimpleEntity>(cfg => cfg
    .Table("simple_entity")
    .Property(x => x.Id)
    .HasColumnName("id"));
```

To map several properties, call [`Property`](xref:NextORM.Core.EntityMetadataBuilder`1) once per member:

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

Rules for the fluent path ([`Build`](xref:NextORM.Core.EntityMetadataBuilder`1.Build)):

* if `Table(...)` is omitted, the table name is auto-built from attributes and then the type name;
* if at least one `Property(...)` is configured, **only** those properties are mapped - auto-discovered
  properties are not merged in;
* if no property is configured, all writable properties are mapped automatically by name/`[Column]`.

## Entities from a raw table: [`TableAlias`](xref:NextORM.Core.TableAlias)

No entity and no metadata are required to query. Start from a table name with `From("table")` and read
columns through the [`TableAlias`](xref:NextORM.Core.TableAlias) passed to [`Select`](xref:NextORM.Core.EntityBuilder`1)/[`Where`](xref:NextORM.Core.EntityBuilder`1):

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

[`TableAlias`](xref:NextORM.Core.TableAlias) accessor methods (each takes the column name and returns the CLR value for that type):

| Method | Returns | Method | Returns |
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

[`TableAlias`](xref:NextORM.Core.TableAlias) also has an indexer, `this[string]`, returning a [`TableColumn`](xref:NextORM.Core.TableColumn) with the typed
[`AsInt`](xref:NextORM.Core.TableColumn.AsInt), [`AsString`](xref:NextORM.Core.TableColumn.AsString), [`AsNullableString`](xref:NextORM.Core.TableColumn.AsNullableString), [`AsBytes`](xref:NextORM.Core.TableColumn.AsBytes) and [`AsNullableBytes`](xref:NextORM.Core.TableColumn.AsNullableBytes) accessors - useful when the
same column alias is referenced from a joined/CTE query:

```csharp
var query = dataContext.From("complex_entity")
    .Where(c => c["id"].AsInt > 1)
    .Select(c => new { Id = c["id"].AsInt });
```

[`From`](xref:NextORM.Core.DataContextExtensions) is available both on the concrete [`DataContext`](xref:NextORM.Core.DataContext) (`dataContext.From("simple_entity")`) and as an
extension on [`IDataContext`](xref:NextORM.Core.IDataContext), so it works whether the context is used through its concrete type or the
interface. Independently of entities, [`From`](xref:NextORM.Core.DataContextExtensions) can also wrap a subquery
(`dataContext.From(innerQuery)`) or another entity builder (`dataContext.From(entity)`).

## Quoted identifiers

By default table and column names are emitted verbatim (see [Provider differences](#provider-differences)),
so a physical name that is a reserved word has to be pre-quoted in its `[SqlTable]`/`[Column]` mapping.
Identifier quoting lets the provider quote them instead. Enable it on the context:

```csharp
var builder = new DataContextBuilder()
    .UseQuotedIdentifiers()
    .UseSqlite(connection);
```

or per command with `WithQuotedIdentifiers()` (available on [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) and
[`QueryCommand<T>`](xref:NextORM.Core.QueryCommand`1)); `WithQuotedIdentifiers(false)` explicitly turns it off for that
command even when the context default is on:

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

Each provider uses its own delimiter and doubles an embedded one (`"a""b"`, `[a]]b]`, `` `a``b` ``).
A schema-qualified name is quoted part by part (`"sales"."orders"`), and column aliases keep the quoting
they already had. A reserved word mapped as a physical name therefore works without manual quoting:

```csharp
[SqlTable("orders")]
public interface IOrder
{
    [Column("select")] int Value { get; set; }
}
```

Quoting composes with [naming conventions](#naming-conventions): the convention translates the
auto-built name first, then quoting protects the translated identifier.

## Provider differences

Table and column names are emitted **verbatim** by default - NextORM does not quote or case-fold
identifiers - so the string in `[SqlTable]`/`[Column]`/`Table(...)`/`HasColumnName(...)` must match the
catalog name exactly for every provider. [Quoted identifiers](#quoted-identifiers) opts into
provider-side quoting instead.

| Provider | Behaviour |
|---|---|
| SQLite | Names used as given. |
| SQL Server | Names used as given. |
| PostgreSQL | Names used as given; an unquoted mixed-case or reserved-word identifier is still folded by the server, so declare the catalog spelling. |
| MySQL | Names used as given; identifiers and aliases are backtick-quoted when the SQL is rendered. |
| MariaDB | Names used as given; identifiers and aliases are backtick-quoted (the MySQL driver and dialect). |
| ClickHouse | Names used as given; identifiers and aliases are backtick-quoted when the SQL is rendered. |
| In-memory | Identical metadata, shared with the SQL contexts through [`DataContextCache`](xref:NextORM.Core.DataContextCache). |

## See also

* [Installation](01-installation.md)
* [Quickstart](02-quickstart.md)
* [Dependency injection](04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.integration.tests/Entities.cs:7`;
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`tests/nextorm.sqlite.tests/MetadataRegistrationTests.cs:18`;
`src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:111`;
`src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:16`;
`src/nextorm.core/DataContext/DataContextCache.cs:20`;
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:116`.
