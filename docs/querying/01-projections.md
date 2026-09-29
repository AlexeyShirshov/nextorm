# Projections

## Anonymous type

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`entity.Id` maps to the `id` column of `simple_entity`, so no alias is emitted.

## Modified and calculated columns

A member can be computed from other columns or constants:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Id = entity.Id + 1 })
    .ToListAsync();
```

```sql
select (id + 1) as 'Id' from simple_entity
```

Output:

| Id |
|----|
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |
| 11 |

The arithmetic expression is parenthesised and, because it is not a plain column, it is aliased with
the projected member name. Naming the member keeps the alias stable for an enclosing query:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.Id + 1 })
    .ToListAsync();
```

```sql
select id, (id + 1) as 'Calc' from complex_entity
```

String members concatenate with the provider's concat operator (`||` on SQLite and PostgreSQL, `+` on
SQL Server):

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Display = it.String + "/" + it.RequiredString })
    .ToListAsync();
```

```sql
-- SQLite
select id, ((somestring || '/') || requiredstring) as 'Display' from complex_entity
```

Output:

| Id | Display |
|----|---------|
| 1 | dadfasd/sdf |
| 2 | xxx/asdfgoi |
| 3 | null |

## Columns by name

A mapped entity only exposes the columns declared on it. A column that has no property — for example
in a wide ClickHouse table — is projected with
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)):

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Region = SqlFunctions.Column<ulong>(entity, "region_id") })
    .ToListAsync();
```

```sql
-- SQLite
select region_id as 'Region' from simple_entity
```

The first argument must be the query lambda parameter (a source), and the name is matched against the
database column verbatim, so quoting follows the provider (`` `region_id` `` on ClickHouse and MySQL,
`"region_id"` on PostgreSQL and SQLite, `[region_id]` on SQL Server). The value is materialized as
`T`, so the type must be one the row reader supports. Unlike a mapped member, the column is not
validated against entity metadata: a misspelled name fails at the database.

The same accessor works in a predicate and on a joined projection:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Join(dataContext.From<ComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => SqlFunctions.Column<long>(p.Item2, "region_id") > 0)
    .Select(p => new { p.Item1.Id, Region = SqlFunctions.Column<long>(p.Item2, "region_id") })
    .ToListAsync();
```

For a source that has no entity type at all (`From("table")`), columns are read through
[`TableAlias`](xref:NextORM.Core.TableAlias) — see [Joins](../guide/02-joins.md) and [CTE](../guide/08-cte.md). The
in-memory provider has no column-name concept and rejects `SqlFunctions.Column`.

## DTO

A non-anonymous type is projected through its constructor:

```csharp
public class SimpleEntityDto(int id)
{
    public int Id { get; } = id;
}

var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new SimpleEntityDto(entity.Id))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Record

Positional records are projected by their constructor too:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`tbl.GetInt64("id")` already has the record member's type, so no conversion is added. When the projected
type is wider or narrower than the source column, nextorm renders the conversion as `cast(...)`.

## Tuple

```csharp
var rows = await dataContext.From("simple_entity")
    .Select(tbl => new Tuple<long>(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Item1 |
|-------|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Member initialiser

Instead of a constructor, a projection can use an object initialiser:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Primitive and scalar projection

[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) can return a single value instead of a row object:

```csharp
var ids = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id < 5)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select id from simple_entity where (id < 5)
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |

A boolean member works the same way:

```csharp
var flags = await dataContext.From<ComplexEntity>()
    .Where(it => it.Boolean == true)
    .Select(it => it.Boolean)
    .ToListAsync();
```

```sql
select b from complex_entity where b = 1
```

Output:

| Boolean |
|---------|
| true |

When that single column is a `byte[]` or `string`, it can be read as a stream instead of being materialised: `ToStream`/`ToTextReader` (and their async forms) open the provider's LOB accessors for that one column — see [Streaming large objects](../guide/26-large-objects.md).

## Nested entity and calculated columns over a projection

A [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) can itself be used as the source of another query with
`dataContext.From(query)`, so an inner projection (including calculated columns) can be read and
projected again:

```csharp
var inner = dataContext.From<ComplexEntity>()
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

