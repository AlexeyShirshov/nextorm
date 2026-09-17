# Querying and projections

> Shape the result of a query with `Select`: one column, an anonymous type, a DTO or record, a tuple, a member initialiser, a nested entity or a calculated column.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

`dataContext.Create<TEntity>()` returns an `Entity<TEntity>`. Every query starts by projecting that
entity with `Select`:

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

The lambda is not executed - it is translated into the `SELECT` list of the generated statement.
`Select` returns a `QueryCommand<TResult>`; the terminal (`ToListAsync`, `FirstAsync`,
`ToAsyncEnumerable`, `AnyAsync`, ...) executes it. See [Sorting and paging](05-sorting-and-paging.md)
for the terminals and their async forms.

Rules that apply to every projection:

* The lambda's shape decides the select list. Columns that are not projected are not read.
* A projected member whose name matches the mapped column name (case-insensitive) is emitted without
  an alias. A renamed or calculated member is aliased: `as 'Calc'` on SQLite, `as [Calc]` on SQL
  Server, `as "Calc"` on PostgreSQL.
* Constructor arguments, tuple items and member-initialiser assignments all map to select-list
  entries; the runtime materialiser builds the object from the reader.
* On the in-memory provider no SQL exists; the same expression is compiled and run over the
  in-memory data set.

## Anonymous type

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

`entity.Id` maps to the `id` column of `simple_entity`, so no alias is emitted.

## Modified and calculated columns

A member can be computed from other columns or constants:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(entity => new { Id = entity.Id + 1 })
    .ToListAsync();
```

```sql
select (id + 1) as 'Id' from simple_entity
```

The arithmetic expression is parenthesised and, because it is not a plain column, it is aliased with
the projected member name. Naming the member keeps the alias stable for an enclosing query:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.Id + 1 })
    .ToListAsync();
```

```sql
select id, (id + 1) as 'Calc' from complex_entity
```

String members concatenate with the provider's concat operator (`||` on SQLite and PostgreSQL, `+` on
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

A non-anonymous type is projected through its constructor:

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

## Record

Positional records are projected by their constructor too:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.Long("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

`tbl.Long("id")` already has the record member's type, so no conversion is added. When the projected
type is wider or narrower than the source column, nextorm renders the conversion as `cast(...)`.

## Tuple

```csharp
var rows = await dataContext.From("simple_entity")
    .Select(tbl => new Tuple<long>(tbl.Long("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

## Member initialiser

Instead of a constructor, a projection can use an object initialiser:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

## Primitive and scalar projection

`Select` can return a single value instead of a row object:

```csharp
var ids = await dataContext.Create<SimpleEntity>()
    .Where(it => it.Id < 5)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select id from simple_entity where (id < 5)
```

A boolean member works the same way:

```csharp
var flags = await dataContext.Create<ComplexEntity>()
    .Where(it => it.Boolean == true)
    .Select(it => it.Boolean)
    .ToListAsync();
```

```sql
select b from complex_entity where b = 1
```

## Nested entity and calculated columns over a projection

A `QueryCommand<TResult>` can itself be used as the source of another query with
`dataContext.From(query)`, so an inner projection (including calculated columns) can be read and
projected again:

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

SQL Server requires the derived table to be aliased, and PostgreSQL likewise:

```sql
-- SQL Server
select id, Calc from (select id, (somestring + somestring) as [Calc] from complex_entity) as [t1]
```

## Subquery as source

`From(query)` also wraps a filtered query, which is the SQL `FROM` equivalent of a subquery:

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

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Column aliases are single-quoted (`as 'Calc'`); derived tables do not require an alias. |
| SQL Server | Column aliases are bracket-quoted (`as [Calc]`); every derived table must be aliased (`as [t1]`). String concatenation uses `+`. |
| PostgreSQL | Column aliases are double-quoted (`as "Calc"`); derived tables must be aliased (`as "t1"`). |
| In-memory | No SQL is generated; the projection delegates are compiled and run over in-memory objects. |

## See also

* [Filtering (WHERE)](02-filtering-where.md)
* [Sorting and paging](05-sorting-and-paging.md)
* [Entities and metadata](../getting-started/03-entities-and-metadata.md)
* [Provider overview](../providers/overview.md)

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
