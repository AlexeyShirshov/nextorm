# Subqueries

> Use a `QueryCommand<T>` as a `FROM` source, as a scalar value in a projection, `WHERE` or `ORDER BY`, or as a correlated `EXISTS` / `IN` / `ANY` / `ALL` predicate.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md) · [Joins](03-joins.md)

## Overview

Any `QueryCommand<T>` — the object you get back from `EntityBuilder<T>.Select(...)` — can be embedded in
another query in four ways:

* as a **derived table** in `FROM`, through `DataContext.From(query)`;
* as a **scalar subquery** in a projection, `WHERE` or `ORDER BY`, by calling a single-row terminal
  such as `First()` or `Single()` inside the outer expression;
* as a **correlated predicate** with `NORM.SQL.exists(...)`, `NORM.SQL.@in(column, query)`,
  `NORM.SQL.any(query)` or `NORM.SQL.all(query)`.

The nested command is prepared independently and rendered in parentheses. A correlated subquery may
reference the outer query's parameter; nextorm tracks those outer references and qualifies them with
the outer table alias.

One provider limitation is worth knowing up front: `NORM.SQL.any` and `NORM.SQL.all` are valid SQL
only on SQL Server and PostgreSQL. SQLite has neither operator, so the query reaches the database and
fails with a `SqliteException` at execution time.

## Subquery as a FROM source

`From` accepts a prepared `QueryCommand<T>` (or an `EntityBuilder<T>`) and produces a builder over its
columns:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id });
var rows = dataContext.From(nested).Select(t => new { t.Id }).ToList();
```

```sql
-- SQLite (derived tables do not require an alias)
select id from (select id from complex_entity)
```

```sql
-- SQL Server / PostgreSQL (the derived table is aliased)
select id from (select id from complex_entity) as [t1]
```

A derived table can itself be projected, filtered and joined like any source. A column that was
renamed in the inner projection is referenced by its projected name from the outside:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id, Calc = x.String + x.String });
var rows = dataContext.From(nested).Select(t => new { t.Id, t.Calc }).ToList();
```

## Scalar subquery in SELECT

Calling a single-row terminal (`First`, `FirstOrDefault`, `Single`, `SingleOrDefault`) inside the
projection embeds the inner query as a scalar column. The scalar projection is aliased with the
outer property name:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First() })
    .ToListAsync();
```

```sql
select id, (select id from simple_entity where (id = 1) limit 1) as 'sid' from complex_entity
```

The inner query can also be sorted:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().OrderByDescending(it => it.Id).Select(it => it.Id).First() })
    .ToListAsync();
```

## Scalar subquery in WHERE

The same single-row terminal used in a predicate becomes a scalar subquery on the right-hand side:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => it.Id == dataContext.From<ISimpleEntity>().OrderBy(it => it.Id).Select(it => it.Id).First())
    .Select(it => new { it.Id })
    .FirstOrDefaultAsync();
```

## Scalar subquery in ORDER BY

An `ORDER BY` key may be an expression containing a scalar subquery. The first `OrderBy` uses the
subquery, the second breaks ties:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(_ => dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First())
    .OrderBy(it => it.Id)
    .Select(it => new { it.Id })
    .ToList();
```

## Correlated EXISTS

`NORM.SQL.exists(query)` returns a boolean and is translated to an `exists(...)` predicate. A
subquery that references the outer parameter is correlated; nextorm emits the outer alias inside the
inner predicate:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(s => NORM.SQL.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id)))
    .Select(it => it.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as 't1' where exists(select * from complex_entity where (id = cast(t1.id as bigint)))
```

An `EntityBuilder<T>` can also be passed directly to `exists` when only its existence matters:

```csharp
var all = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = NORM.SQL.exists(dataContext.From<ISimpleEntity>()) }).ToListAsync();
var none = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = NORM.SQL.exists(dataContext.From<ISimpleEntity>().Where(it => it.Id == 100)) }).ToListAsync();
```

## IN with a subquery

`NORM.SQL.@in(column, query)` emits `IN (SELECT ...)`:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => NORM.SQL.@in((int)it.Id, dataContext.From<ISimpleEntity>().Where(it => it.Id == 2).Select(it => it.Id)))
    .Select(it => it.Id)
    .FirstOrDefaultAsync();
```

```sql
select id from complex_entity where (cast(id as integer) in (select id from simple_entity where (id = 2)))
```

The same method also accepts an `IEnumerable<T>` or a `params T[]` of literal values (see
[Filtering (WHERE)](02-filtering-where.md)); the `QueryCommand<T>` overload is the subquery form.

## ANY and ALL

`NORM.SQL.any(query)` and `NORM.SQL.all(query)` produce a scalar that is compared with the outer
column:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id == NORM.SQL.any(dataContext.From<IComplexEntity>().Select(it => it.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id from simple_entity where (cast(id as bigint) = any(select id from complex_entity))
```

SQLite does not implement `ANY` or `ALL`; the same query is accepted by the translator but throws a
`Microsoft.Data.Sqlite.SqliteException` when executed (covered by
`test/nextorm.integration.tests/SqliteSpecificTests.cs:16` and `:30`).

## Provider differences

| Provider | Derived table alias | Scalar subquery | `any` / `all` |
|---|---|---|---|
| SQLite | optional | supported | **not supported** - `SqliteException` at execution |
| SQL Server | required (`as [t1]`) | supported | supported |
| PostgreSQL | required (`as "t1"`) | supported | supported |
| In-memory | not applicable | not covered by the in-memory test suite | not covered |

A subquery that references the outer query forces the outer `FROM` to be aliased (`t1`) on every
SQL provider. On SQL Server a boolean-valued subquery predicate projected as a scalar is wrapped in
`cast(case when ... then 1 else 0 end as bit)`, because T-SQL has no boolean scalar type.

## See also

- [Joins](03-joins.md) - joining a `QueryCommand<T>` as a derived table.
- [Set operations](07-set-operations.md)
- [Filtering (WHERE)](02-filtering-where.md) - `IN` over a literal list.

---

Source: `test/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:612,623,633,643,653,663`,
`test/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:248`.
