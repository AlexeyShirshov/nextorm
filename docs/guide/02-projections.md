# Projections

Rules that apply to every projection:

* The lambda's shape decides the select list. Columns that are not projected are not read.
* A projected member whose name matches the mapped column name (case-insensitive) is emitted without an alias. A renamed or calculated member is aliased: `as 'Calc'` on SQLite, `as [Calc]` on SQL Server, `as "Calc"` on PostgreSQL.
* Constructor arguments, tuple items and member-initialiser assignments all map to select-list entries; the runtime materialiser builds the object from the reader.
* On the in-memory provider no SQL exists; the same expression is compiled and run over the in-memory data set.

The `Output:` tables below show the rows returned by each example against the integration-test seed data: `simple_entity` holds ids `1`-`10` and `complex_entity` three rows (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

## Anonymous type

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id
 from simple_entity
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
select (id + 1) as 'Id'
 from simple_entity
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
select id, (id + 1) as 'Calc'
 from complex_entity
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
select id, ((somestring || '/') || requiredstring) as 'Display'
 from complex_entity
```

Output:

| Id | Display |
|----|---------|
| 1 | dadfasd/sdf |
| 2 | xxx/asdfgoi |
| 3 | null |

## Columns by name

A mapped entity gives access to its columns through the properties declared on it. When you need an
undeclared column — for example in a wide ClickHouse table — use
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)):

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Region = SqlFunctions.Column<ulong>(entity, "region_id") })
    .ToListAsync();
```

```sql
-- SQLite
select region_id as 'Region'
 from simple_entity
```

The first argument must be the query lambda parameter (a source), and the name is a **raw** database
column name: it is always safely quoted by the provider, independently of the context's
[`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder.UseQuotedIdentifiers(System.Boolean)) option
(`` `region_id` `` on ClickHouse and MySQL, `"region_id"` on PostgreSQL and SQLite, `[region_id]` on
SQL Server). A dot is a literal name character — not a schema or alias qualifier — and the name is never
interpreted as SQL, so manual pre-quoting is not supported: `Column(t, "[1]")` is the literal
three-character name `[1]` (rendered `[[1]]]` on SQL Server), while the column named `1` is
`Column(t, "1")`. The value is materialized as
`T`, so the type must be one the row reader supports. Unlike a mapped member, the column is not
validated against entity metadata: a misspelled name raises an error on the database side.

The same accessor works in a predicate and on a joined projection:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Join<ComplexEntity>((s, c) => s.Id == c.Id)
    .Where(p => SqlFunctions.Column<long>(p.Item2, "region_id") > 0)
    .Select(p => new { p.Item1.Id, Region = SqlFunctions.Column<long>(p.Item2, "region_id") })
    .ToListAsync();
```

For a source that has no entity type at all (`From("table")`), columns are read through
[`TableAlias`](xref:NextORM.Core.TableAlias) — see [Joins](04-joins.md) and [CTE](10-cte.md). The
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
select id
 from simple_entity
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

A DTO projection is a direct-read projection: it binds the mapped columns of the entity you query.
It does not change the rule for a **derived** source read without an outer `Select` — a DTO used as
the *inner* projection of `From(query)` is refused exactly like the anonymous form when its members
have to be re-translated, because the restriction is the projection's expression shape, not the type
being anonymous. Name the columns with an outer `Select` when the DTO is the inner projection (see
[Subqueries](07-subqueries.md#subquery-as-a-from-source)). This rewrite works with member-init DTO and
positional-record projections; the tested constructor-only class projection has a known pre-existing
failure (`Cannot find inner column Id`), so use one of those supported forms instead.
See [Limitations](../advanced/limitations.md#limitations).

## Record

Positional records are projected by their constructor too:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id
 from simple_entity
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
select id
 from simple_entity
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

`Tuple.Create` is the row-value constructor surface: it renders a tuple column where the provider supports row values, and remains subject to the provider and context restrictions (for example, SQLite accepts it only as a comparison operand, not as a `SELECT` projection). A `ValueTuple` is different: a top-level generic `ValueTuple.Create(...)` factory call and an explicit `new ValueTuple<...>(...)` construction are **not supported projections**. Both are rejected during query preparation with a `NotSupportedException` that names the projection type and the `Select` position, before any SQL is rendered or a database is touched — not as a row-materializer failure. Project an explicit DTO or anonymous shape instead; nested tuple members keep their own existing restrictions.

## Member initialiser

Instead of a constructor, a projection can use an object initialiser:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id
 from simple_entity
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
select id
 from simple_entity where (id < 5)
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
select b
 from complex_entity where b = 1
```

Output:

| Boolean |
|---------|
| true |

When that single column is a `byte[]` or `string`, it can be read as a stream instead of being materialised: `ToStream`/`ToTextReader` (and their async forms) open the provider's LOB accessors for that one column — see [Streaming large objects](28-large-objects.md).

An **enum-typed** column is the one scalar shape that needs a value converter: a bare `enum` or
nullable-enum projection without one (`Select(it => it.State)`, `Select(it => it.MaybeState)`) is
refused during preparation with `NotSupportedException` — "Enum scalar projections without a value
converter are not supported in a server-side query." — instead of falling through to row
materialization. Map the column through a converter (see
[Value converters and JSON columns](../infrastructure/03-value-converters.md)) and its scalar
projection behaves like any other converted column. The refusal applies to server-side query
preparation only: the in-memory provider materializes the projection from the source objects, so an
enum scalar keeps working there.

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
select id, Calc
 from (select id, (somestring || somestring) as 'Calc'
 from complex_entity)
```

SQL Server requires the derived table to be aliased, and PostgreSQL likewise:

```sql
-- SQL Server
select id, Calc
 from (select id, (somestring + somestring) as [Calc]
 from complex_entity) as [t1]
```

The enclosing `Select` is what makes such a read work: the outer query addresses the derived
`SELECT`'s output columns, so a named or computed inner column is reused as-is. Reading the same inner
query without an outer projection (`dataContext.From(inner).ToList()`) is accepted only when every
inner column is already such a resolved reference; a projection built from row-member expressions (an
anonymous `new { it.Id }` or a DTO `new Dto { Id = it.Id }`) is refused during preparation with a
`QueryPreparationException` and must be read through an outer `Select`. That is an expression-shape
restriction, not an anonymous-type requirement — see
[Subqueries](07-subqueries.md#subquery-as-a-from-source).

## Named join aliases

A joined query can name its slots instead of addressing them positionally. A join carries an optional
trailing `Alias.<Name>` argument that names the new slot, and the root source can be named with
`.WithAlias(Alias.X)`:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .WithAlias(Alias.Order)
    .Join<Person>(people, (p, buyer) => p.Order.BuyerId == buyer.Id, Alias.Buyer)
    .Select(p => new { OrderId = p.Order.Id, Buyer = p.Buyer.Id })
    .ToListAsync();
```

The named members are **expression-only**: they exist so a `Select`/`Where` expression tree can name
the joined table of the slot, and the translator rewrites them to that table. Reading one outside an
expression tree — for example materialising `p.Buyer` directly — throws `NotSupportedException` by
design instead of returning a defaulted value; project the column you need (`Select(p => p.Buyer.Id)`)
instead. A named member can be used anywhere a positional `p.Item2.Id` would be — in `Select`, `Where`
and later joins. A purely positional chain uses the accumulator types
`Projection<T1>`..`Projection<T1..T8>`, whose `ItemN` members are ordinary properties; a root alias
uses `Projection<T1>` — the only dimension-1 projection, a plain `IProjection` that is not directly
extendable, its slot-1 transition being supplied by the generated positional members — and a named
slot is an expression-only property added on top of the same accumulator. See
[Joins](04-joins.md#named-join-aliases).

