# Dynamic columns

> A mapped entity can keep the row's unmapped columns in a dictionary whose keys come from the result
> set (the database schema) rather than from CLR properties — the **read side** — and write that
> dictionary back into physical columns on `INSERT`, `UPDATE` and `MERGE` — the **write side**. Both
> sides use the same `DynamicColumnsStore`.

**Prerequisites:** [Querying and projections](03-filtering-where.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

A normal nextorm entity maps a fixed set of CLR properties to a fixed set of columns. If a table also contains columns that a particular model does not declare—for example, extensible attributes or optional fields—nextorm normally skips those columns when it reads the entity.

To collect those extra columns, add a writable **dynamic-columns store** marked with
[`DynamicColumnsAttribute`](xref:NextORM.Core.DynamicColumnsAttribute) or configured through
[`EntityPropertyBuilder<T>.DynamicColumnsStore`](xref:NextORM.Core.EntityPropertyBuilder`1.DynamicColumnsStore).
When you query the entity **as a whole**, the generated `SELECT` appends the source's `*` after the
mapped columns. nextorm puts each returned column that is not mapped into the store. Its type must accept
a `Dictionary<string, object?>`; common choices are `Dictionary<string, object?>`,
`IDictionary<string, object?>`, and `IReadOnlyDictionary<string, object?>`.

### Map the store

Mark a writable property to collect the columns the entity does not map:

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

### Read the extra columns

Query the whole entity, then enumerate the populated dictionary:

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
select Id, Name, *
 from product
```

nextorm reads mapped columns by ordinal as usual, then reads the remaining fields through
`IDataRecord.GetName`/`GetValue`. It skips any field whose name matches a mapped column, comparing names
case-insensitively after removing underscores and lowercasing; this lets a snake-case column match a
PascalCase property. Dictionary keys retain the database column names exactly (the dictionary uses
ordinal comparison); only the mapped-column check ignores case.

## Writing dynamic columns

The store also supplies columns for `INSERT`, `UPDATE`, and `MERGE`: its keys become physical column
names, and its values become bound parameters. nextorm reads the store from the entity—or from each row
in a batch—at execution time. You can therefore persist an open-ended set of columns without declaring
a mapped property for each one:

```csharp
var product = new Product
{
    Id = 7,
    Name = "Widget",
    Attributes = new Dictionary<string, object?>
    {
        ["weight"] = 1.5m,
        ["supplier_code"] = "ACME",
    },
};

ctx.CreateInsertBuilder<Product>().Values(product).Insert();
```

The generated statement (PostgreSQL/SQLite quoting shown; each dialect uses its own):

```sql
insert into product (Id, Name, "supplier_code", "weight") values (@p0, @p1, @p2, @p3)
```

The write side is fixed to these semantics:

* **Keys are physical column names.** The key is used as-is — the naming convention is skipped — and
  every dynamic key is quoted with the dialect's identifier quoting even when the global
  `UseQuotedIdentifiers` option is off.
* **Key order is deterministic.** Keys are sorted ordinal (`StringComparer.Ordinal`) before rendering,
  so the column order does not depend on the dictionary's enumeration order.
* **`null` versus a missing key.** A present key whose value is `null` writes SQL `NULL`; a key absent
  from the dictionary is omitted — on `INSERT` the column default applies, and on `UPDATE`/`MERGE` the
  column is left unchanged (you cannot clear a column by omitting its key).
* **Multi-row `INSERT`.** Every row must expose the same key set; a mismatch is rejected with
  `InvalidOperationException`.
* **`MERGE`.** Dynamic columns go to the source, the `INSERT` branch and the matched `SET`, never into
  the `ON` condition.
* **Values bind as runtime CLR values** (pass-through): there is no per-key value converter, no JSON
  mapping and no fabricated `IPropertyMetadata`.
* **Invalid keys are rejected.** An empty key or a key containing a NUL character throws
  `ArgumentException`.

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
data reader, so the materialisation itself needs no dialect hook. The one dialect difference is the star
form — on MySQL/MariaDB the [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) capability
`RequiresQualifiedSelectStar` (default `false`, overridden to `true` on MySQL and inherited by MariaDB)
makes the statement alias-qualify the mapped columns and the star.

| Provider | Read | Write | Note |
|---|---|---|---|
| PostgreSQL | yes | yes | `select <mapped>, *`; value types come from the `DbDataReader`/the runtime CLR value |
| SQL Server | yes | yes | mapped identifiers stay unquoted unless quoting is enabled; dynamic keys are always quoted |
| MySQL / MariaDB | yes | yes | ``select t1.<mapped>, `t1`.*``; the mapped columns and the star are alias-qualified |
| SQLite | yes | yes | `select <mapped>, *`; the write side follows SQLite's dynamic typing |
| ClickHouse | yes | yes | the table columns must exist; its `MERGE` is rejected independently of the store |
| In-memory | yes | no | the in-memory provider returns the registered row and does not implement the write side |

## Constraints

### Query shape

The store applies only when you query a **single physical source** as a whole
(`ctx.From<TEntity>()` and its terminals). It does not apply to an explicit projection
(`Select(x => new { ... })`). A query with joins throws `NotSupportedException`, because the appended
`*` would also include columns from joined tables. A correlated subquery over the entity does not collect
the store either.

### Read and write trust boundaries

On reads, the database supplies column names and the store never renders them into SQL, so those names
cannot introduce injection through this feature. On writes, the caller supplies the keys; nextorm
validates each one (see [Writing dynamic columns](#writing-dynamic-columns)) and quotes it with the
dialect's identifier rules.

### Entity requirements

The store-bearing entity must be a **reference type**. A value-type (`struct`) entity is rejected with
`InvalidOperationException` while nextorm builds its metadata, rather than being boxed silently. The entity
must also have a **public parameterless constructor** because nextorm materializes the store with
member-init; otherwise, preparation throws `QueryPreparationException`.

## Limitations

The write side carries these restrictions:

* **No per-key converters or JSON columns.** A dynamic value binds from its runtime CLR value, mirroring
  read-side materialisation; to store a converted or JSON payload, map a normal property with a value
  converter or a JSON column.
* **No change tracking.** nextorm never tracks the dictionary: every write is an explicit `INSERT`,
  `UPDATE` or `MERGE`, and only the keys you supply are written.
* **A column cannot be cleared by omission.** A missing key means "column default" on `INSERT` and
  "leave unchanged" on `UPDATE`/`MERGE`; write an explicit `null` to set SQL `NULL`.
* **No key allow-list.** Keys are validated (non-empty, no NUL) and always identifier-quoted, but there
  is no allow-list of permitted column names.
* **The in-memory provider does not implement the write side** (its read side returns the registered
  dictionary as-is).
* **Reference type with a public parameterless constructor.** The store-bearing entity must be a class:
  a value-type (`struct`) entity is rejected with `InvalidOperationException` when its metadata is built
  (no silent boxing), and a class without a public parameterless constructor is rejected during
  preparation with `QueryPreparationException` (see [Constraints](#constraints)).
* **A `null` or empty store means no dynamic columns.** On a SQL read the store is created from the
  result set even when it starts `null`; on write a `null` store, or one with no keys, contributes no
  dynamic columns.

See [Limitations and out-of-scope features](../advanced/limitations.md).

## See also

- [Entities and metadata](../getting-started/03-entities-and-metadata.md)
- [Value converters and JSON columns](../infrastructure/03-value-converters.md)
- [API reference](../advanced/api-reference.md)
