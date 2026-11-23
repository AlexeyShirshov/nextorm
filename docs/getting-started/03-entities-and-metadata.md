# Entities and metadata

> Use this page to map a CLR type to a table and its properties to columns. Start with conventions, then add attributes or a fluent mapping when your database names differ. To query a table without an entity type, use [`TableAlias`](xref:NextORM.Core.TableAlias).

**Prerequisites:** [Installation](01-installation.md) · [Quickstart](02-quickstart.md).

## Overview

Before NextORM can build SQL, it needs a table name, a column name for each mapped property, and a way
to create result objects. Choose the mapping approach that fits your model:

1. **Start with conventions** when the CLR type and property names match the database. `From<T>()`
   builds and caches a mapping from the type shape. See [Types without attributes](#types-without-attributes).
2. **Add attributes** (`[SqlTable]`, `[Column]`, and optionally `[Table]`) when you want the mapping
   next to the class or interface.
3. **Use a fluent mapping** with `From<T>(cfg => …)` when you want to configure the mapping in code.
4. **Skip the entity type** when you need a one-off query by table and column name. Start with
   `From("table")` and read columns through [`TableAlias`](xref:NextORM.Core.TableAlias).

You can translate names derived by convention with a [naming convention](#naming-conventions).
Names declared with attributes or a fluent mapping are always used verbatim.

If the schema already exists, you can [generate the mapped classes and repository
context](05-existing-database.md) instead of writing the initial mapping by hand.

When you first query a type through [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)),
NextORM resolves its metadata and caches it by type in the process-wide
[`Metadata`](xref:NextORM.Core.DataContextCache.Metadata) cache. As a result:

* The config delegate passed to `From<T>(…)` runs only on the first call for that type in the process.
* Later calls for the same type reuse the existing [`IEntityMetadata`](xref:NextORM.Core.IEntityMetadata)
  and ignore a new delegate.
* SQL and in-memory contexts share the same metadata. The in-memory context exposes it as
  [`Metadata`](xref:NextORM.Core.InMemoryDataContext.Metadata).

If a type has no registered metadata and you use it as a `FROM` source, NextORM throws
[`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException) and names the type.

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
select id
 from simple_entity
```

* **Table name** - taken from `[SqlTable]` (preferred) or `[Table]`. When neither is present the CLR
  type name is used, so `SimpleEntity` maps to `SimpleEntity`. Attribute lookup walks the type and then
  its interfaces; an attribute on an interface wins when both are present.
* **Column name** - taken from `[Column]` when present, otherwise the property name is used verbatim.
  Convention-based class mapping skips get-only properties. For a generated interface entity,
  NextORM also maps get-only properties.
* **`[Key]` and `[DatabaseGenerated]`** - `KeyAttribute` marks the primary key and
  `DatabaseGeneratedAttribute` marks a database-generated column (`DatabaseGeneratedOption.Identity`
  or `.Computed`). Queries ignore both, but the insert builder reads them: identity/computed columns
  are excluded from the written values (see [Data modification (INSERT)](../guide/17-insert-statement.md)).
  When no key is declared, a property named `Id` or `<TypeName>Id` is treated as the key by convention.
* **Binary columns** - a `byte[]` property maps to a binary column (`bytea` on PostgreSQL,
  `varbinary`/`image` on SQL Server, `blob` on SQLite). A `byte[]` can also be projected directly
  (`Select(x => x.Data)`) and compared against a `byte[]` parameter of [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)).

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

A type does not need any attribute to be queryable: [`From<T>()`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) builds a mapping from
the type's shape (and caches it) the first time the type is used.

* **Table name** - the CLR type name, verbatim. `Product` maps to `Product`; an interface keeps its
  prefix, so `IProduct` maps to `IProduct` (not `products`).
* **Column name** - the property name, verbatim - no snake_case, no case folding. `Id` maps to `Id`.
* **Class properties** - a property is mapped when it has a setter, including a non-public one.
  For interface properties, see [Query an interface entity](#query-an-interface-entity).
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
select Id, Name
 from Product
```

## Query an interface entity

Use an interface as the entity type when you want a typed result without writing an implementation
class. NextORM's source generator creates the implementation at compile time. You don't need to
register it or add any attributes to enable generation.

To read the `id` column from the `simple_entity` table, declare this interface at namespace scope:

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

[SqlTable("simple_entity")]
public interface ISimpleRow
{
    [Column("id")]
    int Id { get; }
}
```

With your configured `dataContext`, read the rows as `ISimpleRow` objects:

```csharp
var rows = dataContext.From<ISimpleRow>()
    .OrderBy(row => row.Id)
    .ToList();
```

Each returned object implements `ISimpleRow`, and its get-only `Id` contains the value from `id`.
The attributes in this example set the SQL names. Without them, the default names are `ISimpleRow`
and `Id`, subject to any naming convention you configure.

Use a `public` or otherwise accessible interface. For example, an `internal` interface in a
referenced assembly must be visible to the consuming assembly through `InternalsVisibleTo`.
Use public instance properties with `get`, `get; set;`, or `get; init;`. NextORM reads mapping
attributes on inherited properties. Methods, events, indexers, interface-valued properties, and
same-name inherited properties with conflicting types aren't supported.

Reference the concrete interface type as a generic argument in code the generator compiles, as in
`From<ISimpleRow>()`. The generator can't discover a type supplied only at runtime.
To read just one column, use a projection such as `Select(row => row.Id)`. To read back an
inserted interface entity, see [Return the inserted rows](../guide/17-insert-statement.md#returning-the-inserted-rows).

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

// select id, name
// from product where (id > 1)
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

Instead of attributes, pass a configuration delegate to [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)). [`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) exposes
`Table(string)` and `Property(Expression<Func<T, object>>)`; the returned [`EntityPropertyBuilder<T>`](xref:NextORM.Core.EntityPropertyBuilder`1)
exposes `HasColumnName(string)`.

```csharp
dataContext.From<SimpleEntity>(cfg => cfg
    .Table("simple_entity")
    .Property(x => x.Id)
    .HasColumnName("id"));
```

To map several properties, call [`Property`](xref:NextORM.Core.EntityMetadataBuilder`1.Property(System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) once per member:

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
columns through the [`TableAlias`](xref:NextORM.Core.TableAlias) passed to [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))/[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})):

```csharp
await foreach (var row in dataContext.From("simple_entity")
                                     .Select(tbl => new { Id = tbl.GetInt64("id") })
                                     .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}
```

```sql
select id
 from simple_entity
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

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) is available both on the concrete [`DataContext`](xref:NextORM.Core.DataContext) (`dataContext.From("simple_entity")`) and as an
extension on [`IDataContext`](xref:NextORM.Core.IDataContext), so it works whether the context is used through its concrete type or the
interface. Independently of entities, [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) can also wrap a subquery
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

// select "id"
// from "simple_entity" where ("id" > 1)     (PostgreSQL, SQLite)
// select [id]
// from [simple_entity] where ([id] > 1)     (SQL Server)
// select `id`
// from `simple_entity` where (`id` > 1)     (MySQL, MariaDB, ClickHouse)
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

## Runtime mapping variant (one entity, several tables)

One entity class can address a different physical table, schema or column set per runtime *variant* — the
mechanism behind multi-tenancy. Choose the variant with `IEntityVariantResolver`, map it with
`IEntityMetadataProvider`, and override the choice for a single command with `WithVariant(...)`:

```csharp
var builder = new DataContextBuilder()
    .UseVariantResolver(new TenantScopeResolver())
    .UseEntityMetadataProvider(new TenantMetadataProvider());

var rows = new SqliteDataContext(connectionString, builder)
    .From<Order>()
    .WithVariant(new MappingVariant("acme"))
    .ToList();
```

`MappingVariant.Default` keeps the ordinary mapping. Plans, metadata and parameter names are isolated per
variant, and the DML builders resolve the variant before they build their statement. Full guide:
[Runtime mapping variant](../guide/33-runtime-mapping-variant.md).

## See also

* [Installation](01-installation.md)
* [Quickstart](02-quickstart.md)
* [Dependency injection](04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.integration.tests/Support/Entities/Entities.cs:7`;
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:37`;
`tests/nextorm.sqlite.tests/Metadata/MetadataRegistrationTests.cs:18`;
`src/nextorm.core/DataContext/Meta/Entities/EntityMetadataBuilder.cs:111`;
`src/nextorm.core/DataContext/Meta/Entities/EntityPropertyBuilder.cs:16`;
`src/nextorm.core/DataContext/Caches/DataContextCache.cs:20`;
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:116`.
