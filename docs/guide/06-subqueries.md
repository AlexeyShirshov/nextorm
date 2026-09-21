# Subqueries

> Use a `QueryCommand<T>` as a `FROM` source, as a scalar value in a projection, `WHERE`, `ORDER BY` or `HAVING`, or as a correlated `EXISTS` / `IN` / `ANY` / `ALL` predicate.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md) · [Joins](03-joins.md)

## Overview

Any `QueryCommand<T>` — the object you get back from `EntityBuilder<T>.Select(...)` — can be embedded in
another query in four ways:

* as a **derived table** in `FROM`, through [`From`](xref:NextORM.Core.DataContext);
* as a **scalar subquery** in a projection, `WHERE`, `ORDER BY` or `HAVING`, by calling a single-row terminal
  such as [`First`](xref:NextORM.Core.EntityBuilder`1) or [`Single`](xref:NextORM.Core.EntityBuilder`1) inside the outer expression;
* as a **correlated predicate** with `SqlFunctions.Sql.exists(...)`, `SqlFunctions.Sql.@in(column, query)`,
  `SqlFunctions.Sql.any(query)` or `SqlFunctions.Sql.all(query)`.

The nested command is prepared independently and rendered in parentheses. A correlated subquery may
reference the outer query's parameter; nextorm tracks those outer references and qualifies them with
the outer table alias.

One provider limitation is worth knowing up front: `SqlFunctions.Sql.any` and `SqlFunctions.Sql.all` are valid SQL
only on SQL Server and PostgreSQL. SQLite has neither operator, so the query reaches the database and
fails with a `SqliteException` at execution time.

## Subquery as a FROM source

[`From`](xref:NextORM.Core.DataContextExtensions) accepts a prepared `QueryCommand<T>` (or an `EntityBuilder<T>`) and produces a builder over its
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

Calling a single-row terminal ([`First`](xref:NextORM.Core.EntityBuilder`1), [`FirstOrDefault`](xref:NextORM.Core.EntityBuilder`1), [`Single`](xref:NextORM.Core.EntityBuilder`1), [`SingleOrDefault`](xref:NextORM.Core.EntityBuilder`1)) inside the
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

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id | sid |
|----|-----|
| 1 | 1 |
| 2 | 1 |
| 3 | 1 |

The inner query can also be sorted:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().OrderByDescending(it => it.Id).Select(it => it.Id).First() })
    .ToListAsync();
```

Output:

| Id | sid |
|----|-----|
| 1 | 10 |
| 2 | 10 |
| 3 | 10 |

## Scalar subquery in WHERE

The same single-row terminal used in a predicate becomes a scalar subquery on the right-hand side:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => it.Id == dataContext.From<ISimpleEntity>().OrderBy(it => it.Id).Select(it => it.Id).First())
    .Select(it => new { it.Id })
    .FirstOrDefaultAsync();
```

Output:

| Id |
|----|
| 1 |

## Scalar subquery in ORDER BY

An `ORDER BY` key may be an expression containing a scalar subquery. The first [`OrderBy`](xref:NextORM.Core.EntityBuilder`1) uses the
subquery, the second breaks ties:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(_ => dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First())
    .OrderBy(it => it.Id)
    .Select(it => new { it.Id })
    .ToList();
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |

## Correlated scalar subquery

A scalar subquery may reference a member of the outer query; nextorm then qualifies it with the outer
table alias. This works on every SQL provider and in every value position (projection, `WHERE`,
`ORDER BY`, `HAVING`):

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(s => s.Id == it.Id).Select(s => s.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select t2.id from simple_entity as 't2' where t2.id = t1.id limit 1) as 'sid'
from complex_entity as 't1'
```

The outer query qualifies only the referenced member; the inner query keeps its own columns and
aliases. [`FirstOrDefault`](xref:NextORM.Core.EntityBuilder`1)/[`SingleOrDefault`](xref:NextORM.Core.EntityBuilder`1) yield `NULL` when no inner row matches.

The referenced member may also come from a join projection: `p.Item1.Id` resolves the table alias
from the projection item's position and the column from its mapping, so a subquery can correlate to
any of the joined sources:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, cid = dataContext.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id).Select(c => c.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select top(1) t3.id from complex_entity as [t3]
 where t3.id = cast(t1.id as bigint)) as [cid] from simple_entity as [t1] join complex_entity as [t2] on cast(t1.id as bigint) = t2.id
```

A subquery may also appear in `HAVING`, where it can reference the grouping key. It is prepared through
the same correlated-query visitor as `WHERE`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(g => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Int == g.Int)))
    .Select(g => new { g.Int, count = SqlFunctions.Sql.count() })
    .ToListAsync();
```

The outer reference may be wrapped in a scalar function over the outer column (for example
`e.String.ToUpper()`); the function is rendered around the qualified outer alias just like any other
expression.

An aggregate terminal ([`Count`](xref:NextORM.Core.EntityBuilder`1), `Sum(...)`, `Min`/`Max`/`Avg`, ...)
is translated to the matching SQL aggregate over the subquery, so it can be used directly with an outer
reference:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, cnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Count() })
    .ToListAsync();
```

```sql
select t1.id, (select count(*) from complex_entity as 't2'
 where cast(t2.id as bigint) = t1.id
limit 1) as 'cnt' from complex_entity as 't1'
```

Correlation nests arbitrarily: a correlated subquery may itself contain a correlated subquery, and
each outer reference resolves to the alias of the scope that declared it.

One limit applies: the in-memory provider cannot bind the outer row while executing the inner query,
so correlated subqueries throw `NotSupportedException` there. Use a SQL provider for correlated queries.

On SQLite a numeric `Single`/`SingleOrDefault` over more than one row raises a database error through a
rendered count guard (the provider does not enforce scalar-subquery cardinality); a non-numeric
projection is rejected with `NotSupportedException`.

## Correlated EXISTS

`SqlFunctions.Sql.exists(query)` returns a boolean and is translated to an `exists(...)` predicate. A
subquery that references the outer parameter is correlated; nextorm emits the outer alias inside the
inner predicate:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(s => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id)))
    .Select(it => it.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as 't1' where exists(select * from complex_entity where (id = cast(t1.id as bigint)))
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |

An `EntityBuilder<T>` can also be passed directly to `exists` when only its existence matters:

```csharp
var all = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(dataContext.From<ISimpleEntity>()) }).ToListAsync();
var none = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(dataContext.From<ISimpleEntity>().Where(it => it.Id == 100)) }).ToListAsync();
```

## IN with a subquery

`SqlFunctions.Sql.@in(column, query)` emits `IN (SELECT ...)`:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => SqlFunctions.Sql.@in((int)it.Id, dataContext.From<ISimpleEntity>().Where(it => it.Id == 2).Select(it => it.Id)))
    .Select(it => it.Id)
    .FirstOrDefaultAsync();
```

```sql
select id from complex_entity where (cast(id as integer) in (select id from simple_entity where (id = 2)))
```

Output:

| Id |
|----|
| 2 |

The same method also accepts an `IEnumerable<T>` or a `params T[]` of literal values (see
[Filtering (WHERE)](02-filtering-where.md)); the `QueryCommand<T>` overload is the subquery form.

## ANY and ALL

`SqlFunctions.Sql.any(query)` and `SqlFunctions.Sql.all(query)` produce a scalar that is compared with the outer
column:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id == SqlFunctions.Sql.any(dataContext.From<IComplexEntity>().Select(it => it.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id from simple_entity where (cast(id as bigint) = any(select id from complex_entity))
```

SQLite does not implement `ANY` or `ALL`; the same query is accepted by the translator but throws a
`Microsoft.Data.Sqlite.SqliteException` when executed (covered by
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:16` and `:30`).

## Provider differences

| Provider | Derived table alias | Scalar subquery | `any` / `all` |
|---|---|---|---|
| SQLite | optional | supported (correlated too) | **not supported** - `SqliteException` at execution |
| SQL Server | required (`as [t1]`) | supported (correlated too) | supported |
| PostgreSQL | required (`as "t1"`) | supported (correlated too) | supported |
| MySQL | required (`` as `t1` ``) | supported (correlated too) | supported |
| MariaDB | required (`` as `t1` ``) | supported (correlated too) | supported |
| ClickHouse | required (`` as `t1` ``) | supported (correlated too) | supported |
| In-memory | not applicable | **not supported** - `NotSupportedException` for correlated | not covered |

A subquery that references the outer query forces the outer `FROM` to be aliased (`t1`) on every
SQL provider. On SQL Server a boolean-valued subquery predicate projected as a scalar is wrapped in
`cast(case when ... then 1 else 0 end as bit)`, because T-SQL has no boolean scalar type.

## Limitations

* Nested correlation (a subquery that references an outer reference of another subquery) is rejected
  with `NotSupportedException`; keeping it explicit avoids binding an outer marker to the wrong query.
* The in-memory provider rejects correlated subqueries (see above).

## See also

- [Joins](03-joins.md) - joining a `QueryCommand<T>` as a derived table.
- [Set operations](07-set-operations.md)
- [Filtering (WHERE)](02-filtering-where.md) - `IN` over a literal list.

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs`,
`tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:612,623,633,643,653,663`,
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`.
