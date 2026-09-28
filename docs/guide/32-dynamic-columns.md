# Dynamic columns

> A mapped entity can keep the row's unmapped columns in a dictionary whose keys come from the result
> set (the database schema) rather than from CLR properties — the **read side** — and write that
> dictionary back into physical columns on `INSERT`, `UPDATE` and `MERGE` — the **write side**. Both
> sides use the same `DynamicColumnsStore`.

**Prerequisites:** [Querying and projections](01-filtering-where.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

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
case-insensitively and after dropping underscores/lower-casing, so a snake-case physical name matches
its PascalCase property name). The store keys preserve the database column name exactly (the dictionary
is ordinal); only the skip matching against mapped columns is case-insensitive.

## Writing dynamic columns

The same store is written back on `INSERT`, `UPDATE` and `MERGE`: the dictionary keys become physical
column names and their values become bound parameters. The store is read from the entity (or from every
row of a batch) at execution time, so an open-ended set of columns is persisted without a mapped
property per column:

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

ctx.InsertInto<Product>().Values(product).Insert();
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

The store only applies to a query over a **single physical source** read as a whole
(`ctx.From<TEntity>()` and its terminals). It is not applied to an explicit projection
(`Select(x => new { ... })`), and a query with joins is rejected with `NotSupportedException` because
the appended `*` would also pull in the joined table's columns. A correlated subquery over the entity
likewise does not collect the store.

On the read side the keys are only as trustworthy as the schema they come from: the store never
renders a key into SQL, so it cannot introduce an injection through the column names — the names are
produced by the database, not by the caller. On the write side the caller supplies the keys, so each one
is validated (see [Writing dynamic columns](#writing-dynamic-columns)) and quoted with the dialect's
identifier quoting.

Two entity-shape rules apply to the store. The entity must be a **reference type**: a value-type
(`struct`) entity that declares a store is rejected with `InvalidOperationException` when its metadata
is built, rather than boxing silently - use a class. It must also expose a **public parameterless
constructor**, because the store is materialised with member-init; a store-bearing entity without one
is rejected during preparation with `QueryPreparationException`.

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
- [Value converters and JSON columns](28-value-converters.md)
- [API reference](../advanced/api-reference.md)
