# Data modification (INSERT)

> nextorm adds a small, explicit DML surface: [`InsertInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.InsertInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) builds a parameterised `INSERT ... VALUES` and returns the affected-row count, the generated key or the inserted rows. There is no change tracking and no `SaveChanges`: every terminal issues exactly one command.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Provider overview](../providers/overview.md)

## Overview

nextorm has always been a query *builder*; writing a row is the same idea applied to `INSERT`. The
builder collects the target table and the columns to write, the active dialect renders the statement
(parameterising every value, exactly like a query), and the context executes it once:

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

var affected = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Id, 1)
    .Value(x => x.Name, "a")
    .Insert();
```

There is deliberately **no** unit of work, change tracker or entity graph: this is the explicit-command
model of linq2db/`ExecuteNonQuery`, not EF Core's `SaveChanges`. See
[Limitations and out-of-scope features](../advanced/limitations.md).

## Declaring keys, identity and computed columns

The insert builder uses the entity mapping, so it needs to know which columns are database-generated.
Declare them with the standard data-annotation attributes (on the interface when the entity is
interface-mapped) or fluently on the metadata builder:

| Concept | Attribute | Fluent |
|---|---|---|
| Entity key | `[Key]` | `Property(x => x.Id).Key()` |
| Identity / auto-increment | `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` | `Property(x => x.Id).Identity()` |
| Computed (never written) | `[DatabaseGenerated(DatabaseGeneratedOption.Computed)]` | `Property(x => x.Total).Computed()` |

```csharp
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}
```

Or declare the same mapping fluently on a plain POCO, with no attributes at all - the delegate goes to
`InsertInto<T>` (the identical `cfg` can be passed to `From<T>` to register the mapping beforehand):

```csharp
public class Order            // no attributes
{
    public long Id { get; set; }
    public decimal Total { get; set; }
    public decimal TotalWithVat { get; set; }
}

ctx.InsertInto<Order>(cfg =>
{
    cfg.Table("orders");
    cfg.Property(x => x.Id).HasColumnName("id").Key().Identity();
    cfg.Property(x => x.Total).HasColumnName("total");
    cfg.Property(x => x.TotalWithVat).HasColumnName("total_with_vat").Computed();
});
```

When no property is declared with `[Key]`, the metadata infers the key from the `Id` or
`<TypeName>Id` convention. `Identity` and `Computed` are **not** inferred: declare them explicitly, or
they are treated as ordinary writable columns.

## Writing values

Every value is bound as a parameter. Several shapes are available; they are mutually exclusive within one builder.

**Column by column** (single row). The value is a CLR value:

```csharp
ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Id, 1)
    .Value(x => x.Name, "a")
    .Insert();
```

The value may also be another mapped column of the same entity, which renders a column reference
instead of a parameter:

```csharp
ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, x => x.OtherName)
    .Insert();
```

Only a mapped property access (or a parameter-free expression) is accepted here; anything else throws
`NotSupportedException`.

**A whole entity.** All mapped, writable columns are written; `Identity` and `Computed` columns are
excluded automatically:

```csharp
ctx.InsertInto<ISimpleEntity>().Values(entity).Insert();
```

**A batch of entities.** The same as the previous form, repeated over a sequence:

```csharp
ctx.InsertInto<ISimpleEntity>().Values(new[] { e1, e2, e3 }).Insert();
```

**A batch from a source.** `Values(source, mapping)` writes one row per element and lets the mapping
select the columns. A concrete entity uses an object initializer, an interface-mapped entity uses an
anonymous type; every member name must match an entity property:

```csharp
ctx.InsertInto<Order>()
    .Values(dtos, d => new Order { Total = d.Total })
    .Insert();

ctx.InsertInto<ISimpleEntity>()
    .Values(dtos, d => new { Name = d.Name })
    .Insert();
```

**A batch from a query (server-side).** The same `Values(source, mapping)` signature also accepts an
`EntityBuilder<TSource>`, which renders `INSERT ... SELECT` and reads the rows in the database instead of
the client - useful for copying or filtering server-side. The mapping selects the written columns exactly
as above:

```csharp
ctx.InsertInto<IOrder>()
    .Values(ctx.From<OrderDto>().Where(d => d.Active), d => new { d.Id, d.Amount })
    .Insert();
```

The source is an ordinary query builder (`Where`/`Join`/`GroupBy`/`OrderBy`/...), and its parameters are
carried into the insert. A source that declares a CTE (`With`) is supported: the `WITH` clause is hoisted
to precede `INSERT` (`with c as (...) insert into ... select ... from c`), because a data-modifying CTE must
sit at the top level. A runtime `SqlFunctions.Parameter` placeholder is not supported, and `DEFAULT` is not
valid as a selected value - both throw `NotSupportedException`. `Returning`/`ReturningIdentity`/`ReturningKey`
compose with it wherever the provider supports them, and `.Single()` throws when the query writes more than
one row. The target-column rules (member names, no `Computed` target) are the same as the client-side mapping.

A **CTE source** is a valid server-side source: build the scope with `With`, read the CTE by name and pass
the resulting `EntityBuilder<TableAlias>` to `Values`. CTE columns are read by name, so the mapping must use
the CTE body's output aliases:

```csharp
var source = ctx
    .With("recent", ctx.From<ISimpleEntity>()
        .Where(s => s.Id > 1)
        .Select(s => new { s.Id }))
    .From("recent");

ctx.InsertInto<IOrder>()
    .Values(source, t => new { CustomerId = t.GetInt32("id") })
    .Insert();
// with recent as (select id from simple_entity where (id > 1))
// insert into "order" (customer_id) select id from recent as "t1"
```

On PostgreSQL the source may even be a data-modifying CTE (`ctx.With(name, insert.Returning(...))`); see
[Data-modifying CTE (PostgreSQL)](#data-modifying-cte-postgresql).

A **table-valued function** is a valid source too: `FromTableFunction` yields an `EntityBuilder<TSource>`,
so `unnest`/`generate_series` can feed the insert. The array is a real parameter, so pass it as a captured
variable - the runtime `SqlFunctions.Parameter` placeholder is rejected (as above):

```csharp
var ids = new long[] { 1, 2, 3 };

ctx.InsertInto<IdRow>()
    .Values(ctx.FromTableFunction(() => SqlFunctions.Postgres.unnest(ids)), r => new { r.Value })
    .Insert();
// insert into id_row (value) select unnest as "Value" from (select unnest from unnest(@ids)) as "t1"
```

`unnest` and `generate_series` are PostgreSQL-only.

**A scalar column.** When the entity has exactly one writable column (the others are `Identity`/
`Computed`), pass the value or values directly, without a selector or a projection:

```csharp
ctx.InsertInto<ISimpleEntity>().Value("a").Insert();                      // one row
ctx.InsertInto<ISimpleEntity>().Values(new[] { "a", "b", "c" }).Insert(); // three rows
```

The scalar form needs **exactly one** writable column: with several, use a mapping or name the column
instead (both throw `InvalidOperationException` when called on a scalar). With none (every column is
`Identity`/`Computed`) there is nothing to pass - insert the all-defaults row with
`ctx.InsertInto<T>().Insert()` (see [Defaults](#writing-values)).

**A batch by column.** For column-oriented data, chain one call per column; every column must supply
the same number of values:

```csharp
ctx.InsertInto<Product>()
    .Values(x => x.Name, new[] { "a", "b" })
    .Values(x => x.Price, new[] { 1m, 2m })
    .Insert();
```

Writing to a `Computed` column always throws `NotSupportedException`; an `Identity` column may be set
explicitly (its value is then taken from the input, not generated).

**Defaults.** A single value can be the column's database `DEFAULT`, and an entity whose every column
is generated (`Identity`/`Computed`) needs no values at all - it inserts a single all-defaults row:

```csharp
ctx.InsertInto<ISimpleEntity>().Value(x => x.Name, SqlDefault.Value).Insert();

// every column is Identity/Computed:
ctx.InsertInto<AuditRow>().Insert();
```

The all-defaults row uses the provider's native form (`DEFAULT VALUES` on PostgreSQL, SQL Server and
SQLite; `() VALUES ()` on MySQL/MariaDB). Writing `DEFAULT` as a value is not expressible on SQLite
(omit the column instead - the same default is applied), and ClickHouse supports neither form; both
throw `NotSupportedException`. `SqlDefault.Value` also works as a mapped member in a
`Values(source, mapping)` projection.

## Data-modifying CTE (PostgreSQL)

PostgreSQL is the only supported provider that accepts a data-modifying statement as a CTE body
(`WITH <name> AS (INSERT ... RETURNING ...)`), gated by
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). Start the scope with
the `With(name, insert)` overload: it takes a returning insert and returns a
[`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1) typed by the `RETURNING` projection.
Read the returned rows with `From(name)` (typed):

```csharp
var rows = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("ins")
    .Where(r => r.Total > 0)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
-- PostgreSQL
with ins as (insert into orders (customer_id) values (@p0) returning id, total) select id from ins as "t1"
 where (t1.total > 0)
```

`From(name)` reads the mutation's `RETURNING` columns with the full operator set — filter, join, aggregate,
order and page — and the projection may rename them (`Returning(x => new { x.Total })` is read back as
`r.Total`). [`FromTable`](xref:NextORM.Core.MutationCteQuery`1.FromTable(System.String)) reads an
accompanying read CTE as a regular [`TableAlias`](xref:NextORM.Core.TableAlias) source:

```csharp
var rows = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id }))
    .With("src", dataContext.From<IOrder>().Select(x => new { x.Total }))
    .FromTable("src")
    .Select(t => new { total = t["Total"].AsInt })
    .ToList();
```

> The rows a data-modifying CTE inserts are **not** visible to the other CTEs of the same statement:
> PostgreSQL executes the sub-statements concurrently against the same snapshot. Read them back through
> `From(name)`, or in a later statement.

The body may be a `VALUES` insert or an `INSERT ... SELECT` (`Values(source, mapping)` composes with
`Returning`); any CTE the source declares is dropped, because it is already declared by the enclosing
`WITH`. Declare read CTEs first so the mutation can reference them: `CteQuery.With(name, insert)` appends
the data-modifying CTE **after** the read CTEs collected so far, so its body may read them:

```csharp
var scope = dataContext.With("src",
    dataContext.From<ICustomer>().Where(c => c.Active).Select(c => new { c.Id }));

var rows = scope
    .With("ins", dataContext.InsertInto<IOrder>()
        .Values(scope.From("src"), a => new { CustomerId = a.GetInt32("Id") })
        .Returning(x => new { x.Id }))
    .From("ins")
    .Select(r => new { r.Id })
    .ToList();
```

A data-modifying CTE can also feed a main `INSERT ... SELECT`: the `WITH` is hoisted to precede `INSERT`
(PostgreSQL requires the data-modifying statement at the top level):

```csharp
var source = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.CustomerId }))
    .From("ins");

dataContext.InsertInto<IOrder>()
    .Values(source, r => new { r.CustomerId })
    .Insert();
```

A statement whose `WITH` contains a data-modifying CTE is never stored in the plan cache (it is
side-effecting). Other providers reject `With(name, insert)` with `NotSupportedException`, because their
CTE body must be a `SELECT`. For general read CTEs see [Common table expressions](09-cte.md); `UPDATE` will be documented in its own guide.

## Reading the generated key

The key-reading terminals all return an [`InsertReturningBuilder<TEntity,TResult>`](xref:NextORM.Core.InsertReturningBuilder`2)
and are read through its `Single()`/`ToList()` terminals (see [Returning the inserted rows](#returning-the-inserted-rows)):

| Terminal | Selects | Requires |
|---|---|---|
| `ReturningIdentity(x => x.Id)` | a column named by the selector | the column is declared `Identity`; the provider has `RETURNING`/`OUTPUT` |
| `ReturningIdentity<long>()` | the provider's scalar identity function | `ISqlDialect.SupportsIdentityFunction` |
| `ReturningKey<long>()` | the key column resolved from metadata | the entity has a key; `RETURNING`/`OUTPUT` or the identity-function fallback |

The provider's native form is used:

| Provider | Column selector / key | Identity function |
|---|---|---|
| SQLite | `INSERT ... RETURNING id` | `SELECT last_insert_rowid()` |
| PostgreSQL | `INSERT ... RETURNING id` | `SELECT lastval()` |
| SQL Server | `OUTPUT inserted.id` | `SELECT SCOPE_IDENTITY()` (same batch as the insert) |
| MySQL / MariaDB | `SELECT LAST_INSERT_ID()` fallback | `SELECT LAST_INSERT_ID()` |
| ClickHouse | not expressible | not expressible |
| In-memory | not expressible | not expressible (read-only) |

```csharp
long fromColumn = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .ReturningIdentity(x => x.Id)
    .Single();

long fromFunction = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .ReturningIdentity<long>()
    .Single();

long fromMetadata = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .ReturningKey<long>()
    .Single();
```

* `ReturningIdentity(selector)` requires the selected column to be declared `Identity`; `ReturningKey<TKey>()`
  resolves the key from metadata (`[Key]`, `.Key()` or the `Id`/`<Type>Id` convention) and rejects an entity
  with no key or several keys.
* Where the provider has no `RETURNING`/`OUTPUT` (MySQL/MariaDB) an identity column falls back to
  `LAST_INSERT_ID()`; a non-identity key is rejected with `NotSupportedException` rather than returning the
  wrong value.
* `ReturningIdentity<TKey>()` never names a column: it appends the provider's identity function to the insert
  in the same batch, so SQL Server keeps `SCOPE_IDENTITY()` correct in the presence of triggers. Prefer the
  selector form there.
* ClickHouse and the in-memory provider throw `NotSupportedException`.

## Returning the inserted rows

Beyond the single generated key, `Returning()` materialises the rows the database actually wrote -
including identity and computed columns - through the provider's native `RETURNING`/`OUTPUT` form,
without a second `SELECT`:

```csharp
var row = ctx.InsertInto<Order>()
    .Value(x => x.Name, "a")
    .Returning()
    .Single();                        // an Order carrying the generated Id

var projected = ctx.InsertInto<Order>()
    .Value(x => x.Name, "a")
    .Returning(x => new { x.Id, x.Name })
    .Single();                        // an anonymous { Id, Name }

var rows = ctx.InsertInto<Order>()
    .Values([o1, o2])
    .Returning()
    .ToList();                        // IReadOnlyList<Order>, one entry per inserted row
```

* `Returning()` returns the whole mapped entity; `Returning(projection)` returns the projected shape.
  The projection may be the identity, a single mapped property, an anonymous type, a positional
  constructor or a member-init, and may only reference mapped properties.
* Terminals: `Single()`/`SingleAsync()` expect a single-row insert and throw
  `InvalidOperationException` on a batch; `ToList()`/`ToListAsync()` work
  for both and return `IReadOnlyList<TResult>`.
* The rows are materialised with the same projection pipeline as a query, so the entity form returns
  the entity and the projection form returns its shape.
* `ToSql()` renders the statement - including the `RETURNING`/`OUTPUT` list - without executing it.

| Provider | Form | Support |
|---|---|---|
| SQLite | `INSERT ... RETURNING <cols>` (3.35.0+) | yes |
| PostgreSQL | `INSERT ... RETURNING <cols>` | yes |
| SQL Server | `INSERT ... OUTPUT inserted.<cols>` (placed between the column list and `VALUES`) | yes |
| MySQL | — | `NotSupportedException` |
| MariaDB | — (the dialect is MySQL-based) | `NotSupportedException` |
| ClickHouse | — | `NotSupportedException` |
| In-memory | — (read-only) | `NotSupportedException` |

**Ordering.** The rows arrive in result-set order, but no provider guarantees that order matches the
input order of a multi-row insert (SQLite documents the output order as arbitrary). Treat
`ToList()` as an unordered set and do not rely on `rows[i]` matching input `i`.

**Whole entity versus interface.** `Returning()` needs a concrete `TEntity` to materialise. For an
interface-mapped entity either project the columns (`Returning(x => new { x.Id, x.Name })`) or use the
class that implements the interface; `Returning()` on the interface itself throws a
`NotSupportedException` at execution.

## Upsert (key merge)

[`MergeInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.MergeInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) writes a source row set into the table and lets the database decide, per declared key, whether to update the existing row or insert a new one. The source is a single mapped entity or a batch, the match key is resolved from the entity mapping with `OnKeys()`, and both branches — `WhenMatchedUpdate()` (set every non-key writable column from the source) and `WhenNotMatchedInsert()` — are required:

```csharp
ctx.MergeInto<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })   // or Using(new[] { e1, e2 })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .Merge();
```

The builder renders the provider's native form; `ToSql()` inspects it without a connection:

| Provider | Rendered form |
|---|---|
| PostgreSQL, SQLite | `INSERT ... ON CONFLICT (<keys>) DO UPDATE SET <col> = excluded.<col>` |
| MySQL, MariaDB | `INSERT ... ON DUPLICATE KEY UPDATE <col> = VALUES(<col>)` |
| SQL Server | `MERGE ... USING (VALUES ...) AS source (...) ON ... WHEN MATCHED THEN UPDATE SET ... WHEN NOT MATCHED THEN INSERT ...;` |
| ClickHouse, in-memory | `NotSupportedException` |

`Merge()`/`MergeAsync()` return the number of affected rows. The key must be declared (`[Key]`/`.Key()`) and must not be database-generated; an entity with only key columns is rejected because there is nothing to update. This is the *key upsert* form only — a full `MERGE` with arbitrary `WHEN MATCHED`/`WHEN NOT MATCHED` branches (including `DELETE`) is not part of this surface.

## Inspecting the SQL

[`ToSql()`](xref:NextORM.Core.InsertBuilder`1.ToSql) renders the parameterised SQL a plain
`Insert()` would execute, without opening a connection. It is useful for diagnostics and for
SQL-generation tests.

```csharp
var sql = ctx.InsertInto<ISimpleEntity>().Value(x => x.Name, "a").ToSql();
// insert into simple_entity (name) values (@p0)
```

## Provider and capability summary

| Provider | `INSERT ... VALUES` | Generated key | Returns rows | Notes |
|---|---|---|---|---|
| SQLite | yes | `RETURNING` (also `last_insert_rowid()`) | yes | |
| PostgreSQL | yes | `RETURNING` | yes | |
| SQL Server | yes | `OUTPUT inserted.<col>` | yes | |
| MySQL | yes | `LAST_INSERT_ID()` | — | no `RETURNING` |
| MariaDB | yes | `LAST_INSERT_ID()` | — | `RETURNING` (10.5+) is not used |
| ClickHouse | yes (small batches) | — | — | bulk load goes through the driver's binary API |
| In-memory | — | — | — | read-only context; `NotSupportedException` |

`INSERT ... VALUES` itself is cross-provider and ungated: the same `InsertInto<T>()` API works on every
SQL provider. Only the generated-key form differs, and a provider that cannot express it rejects
`ReturningIdentity`/`ReturningKey` with `NotSupportedException` instead of emitting invalid SQL.

## Notes and phase-1 limits

* **Values are parameters.** A constant passed to `Value(...)` is never inlined; it becomes a named
  parameter (`@p0`, `$p0`, ...) bound on execution.
* **`null` overloads.** `Value(x => x.Name, null)` is ambiguous between the value and the column-expression
  overload; cast the literal: `Value(x => x.Name, (string?)null)`.
* **Key upsert, no full `MERGE`** — `ON CONFLICT`/`ON DUPLICATE KEY`/`MERGE` key upsert **is** implemented
  (see [Upsert (key merge)](#upsert-key-merge)); a full `MERGE` with arbitrary `WHEN MATCHED`/`WHEN NOT
  MATCHED` branches is not. `INSERT ... SELECT` **is** implemented (see *A batch from a query*
  above). (An all-defaults row *is* supported: see [Writing values](#writing-values).)
* **Mutations are not prepared or plan-cached.** Optimisation in nextorm targets read-only queries
  only (`Prepare`, the implicit plan cache, benchmarks); a mutation always renders and executes one
  command per call.
* **No chunking of a large batch**; thousands of rows may hit the provider's per-statement limit. Use
  the provider's bulk-copy/binary API for bulk loads.
* The in-memory provider is read-only: every write terminal (`Insert()`, `ReturningIdentity`, `ReturningKey`, ...) throws `NotSupportedException`.

## See also

- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../providers/overview.md)
- [API reference](../advanced/api-reference.md)
