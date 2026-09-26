# Dynamic columns (read side)

> A mapped entity can keep the row's unmapped columns in a dictionary whose keys come from the result
> set (the database schema) rather than from CLR properties. This is the **read side** of the
> `DynamicColumnsStore` feature; writing the dictionary keys back as columns is not implemented.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

A normal nextorm entity maps a fixed set of CLR properties to a fixed set of columns. When the same
table carries columns a given model does not declare (an extensible attribute table, a sparse set of
optional columns, a schema shared by several models), the unmapped columns are simply not read.

A **dynamic-columns store** is a writable property marked with
[`DynamicColumnsAttribute`](xref:NextORM.Core.DynamicColumnsAttribute) (or the fluent
[`EntityPropertyBuilder<T>.DynamicColumnsStore`](xref:NextORM.Core.EntityPropertyBuilder`1.DynamicColumnsStore)).
When the entity is queried **as a whole**, the generated `SELECT` appends the source's `*` after the
mapped columns and the store is materialised with every returned column whose name is not one of the
mapped columns. The store type must be assignable from `Dictionary<string, object?>` — typically
`Dictionary<string, object?>`, `IDictionary<string, object?>` or `IReadOnlyDictionary<string, object?>`.

```csharp
[SqlTable("product")]
public class Product
{
    [Key]
    public int Id { get; set; }

    public string? Name { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Attributes { get; set; } = new();
}
```

```csharp
var products = ctx.From<Product>().ToList();

foreach (var product in products)
{
    // "Id" and "Name" are mapped properties; every other column of the "product" table
    // (for example "weight", "colour", "supplier_code") lands in the dictionary.
    foreach (var (column, value) in product.Attributes)
        Console.WriteLine($"{column} = {value ?? "<null>"}");
}
```

The generated statement is:

```sql
select Id, Name, * from product
```

The mapped columns are read by ordinal as usual; the store reads the fields after them through
`IDataRecord.GetName`/`GetValue`, skipping any field whose name matches a mapped column (compared
verbatim and after dropping underscores/lower-casing, so a snake-case physical name matches its
PascalCase property name).

## Fluent mapping

Use [`DynamicColumnsStore`](xref:NextORM.Core.EntityPropertyBuilder`1.DynamicColumnsStore) when the
mapping is declared fluently instead of with attributes:

```csharp
var products = ctx.From<Product>(b => b
    .Property(x => x.Id).Key()
    .Property(x => x.Name)
    .Property(x => x.Attributes).DynamicColumnsStore());
```

A store cannot also declare a column name, a value/JSON converter or a range-column pair; the property
must have a setter.

## Provider matrix

The store is engine-independent: the columns come from the result set and their names are read from the
data reader, so no provider needs a dialect hook.

| Provider | Read | Note |
|---|---|---|
| PostgreSQL | yes | `select <mapped>, *`; value types come from the `DbDataReader` |
| SQL Server | yes | identifiers stay unquoted unless quoting is enabled |
| MySQL / MariaDB | yes | `select <mapped>, *` |
| SQLite | yes | `select <mapped>, *` |
| ClickHouse | yes | the table columns must exist; the store is read like any other result |
| In-memory | yes | the in-memory provider returns the registered row, so its own `Attributes` dictionary is the one returned |

## Constraints

The store only applies to a query over a **single physical source** read as a whole
(`ctx.From<TEntity>()` and its terminals). It is not applied to an explicit projection
(`Select(x => new { ... })`), and a query with joins is rejected with `NotSupportedException` because
the appended `*` would also pull in the joined table's columns. A correlated subquery over the entity
likewise does not collect the store.

The keys are only as trustworthy as the schema they come from. The store never quotes or renders a key
into SQL, so it cannot introduce an injection through the column names — the names are produced by the
database, not by the caller.

## Limitations

**Write side is not implemented.** `INSERT`/`UPDATE`/`MERGE` do not render the dictionary keys as
columns; a value converter or a JSON column is the supported way to persist an open-ended payload
today. See [Limitations and out-of-scope features](../advanced/limitations.md).

## See also

- [Entities and metadata](../getting-started/03-entities-and-metadata.md)
- [Value converters and JSON columns](30-value-converters.md)
- [API reference](../advanced/api-reference.md)
