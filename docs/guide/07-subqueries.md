# Subqueries

> Use a query inside another query: read its rows through `FROM`, use one value as a scalar subquery, or test its results with `EXISTS` or `IN`.

**Prerequisites:** [Querying and projections](../engine/index.md) · [Filtering (WHERE)](03-filtering-where.md) · [Joins](04-joins.md)

## Overview

Choose a subquery form based on what the outer query needs from the inner query:

* Use a **derived table** when the outer query needs the inner query's rows as a source. Pass the command
  to [`From`](xref:NextORM.Core.DataContext.From(System.String)).
* Use a **scalar subquery** when the outer query needs one value in a projection, `WHERE`, `ORDER BY` or
  `HAVING`. Call a single-row method such as
  [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) or
  [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) inside
  the outer expression.
* Use a **predicate** when you need to test the inner query's results. Choose
  `SqlFunctions.Sql.exists(...)`, `SqlFunctions.Sql.@in(column, query)`,
  `SqlFunctions.Sql.any(query)` or `SqlFunctions.Sql.all(query)`.

nextorm prepares the inner command independently and renders it in parentheses. A correlated subquery
also reads a value from the outer query; nextorm qualifies that reference with the outer table alias.

One provider limitation is worth knowing up front: `SqlFunctions.Sql.any` and `SqlFunctions.Sql.all` are valid SQL
only on SQL Server and PostgreSQL. SQLite has neither operator, so the query reaches the database and
fails with a `SqliteException` at execution time.

**Routed statements.** A subquery travels inside its statement, so it adds no separate hop: every routed
source of the statement must resolve to one topology, and a sharded source carries its bucket variant. See
[Routing data to topologies](34-routing-data-to-topologies.md) and
[Sharding and horizontal partitioning](35-sharding-and-horizontal-partitioning.md).

## Subquery as a FROM source

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) accepts a prepared `QueryCommand<T>` (or an `EntityBuilder<T>`) and produces a builder over its
columns:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id });
var rows = dataContext.From(nested).Select(t => new { t.Id }).ToList();
```

```sql
-- SQLite (derived tables do not require an alias)
select id
 from (select id
 from complex_entity)
```

```sql
-- SQL Server / PostgreSQL (the derived table is aliased)
select id
 from (select id
 from complex_entity) as [t1]
```

A derived table can itself be projected, filtered and joined like any source. A column that was
renamed in the inner projection is referenced by its projected name from the outside:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id, Calc = x.String + x.String });
var rows = dataContext.From(nested).Select(t => new { t.Id, t.Calc }).ToList();
```

`From(nested)` creates the derived source; reading it is a separate step. In the common case, add an
outer `Select` to name the output columns that you want to read. The outer query can address only
columns declared by the inner `SELECT`.

A bare `From(nested).ToList()` works only when the inner projection already contains resolved column
references, such as a mapped column, the renamed `Calc` above, or a computed scalar addressed by its
output name. A projection built from row-member expressions—an anonymous `new { x.Id }` or a DTO
`new Dto { Id = x.Id }`—would need to be translated again against the derived source. Preparation
therefore rejects it with `QueryPreparationException`.

This restriction is about the **expression shape**, not anonymous types. DTO projections work for a
direct read (see [Projections](02-projections.md)), but that does not make a bare derived read of the
same DTO reusable. Add an outer `Select` (`From(nested).Select(t => t.Id)`) to read such a source.
This form works with member-init DTOs and positional records. A tested constructor-only class
projection has a known pre-existing failure (`Cannot find inner column Id`), so use one of those
supported forms instead.

A **single-column scalar** derived source—such as a member projection or a set-operation command such
as `left.UnionAll(right)`—has no row shape to build, so it materializes directly to `List<T>`. This is
true whether the type's process-wide select-list cache is cold or was populated by an earlier scalar
read. The derived read builds its own one-column shape and does not reuse or overwrite the cached
entry. Before the fix, the warm path reused the cached object-materialization list and threw
`QueryPreparationException: Cannot get ctor from T`; the cold path succeeded.

These examples use two kinds of aliases. The **table alias** (`t1`, `t2`, ...) names the derived source
in `FROM` and qualifies its references (`t1.id`). The provider generates this alias; you don't write
it. The **output-column alias** is declared by the derived `SELECT`: it is either a projected member's
name (`Calc`, `requiredstring as 'String'`) or, for an unaliased computed scalar, a generated name
referenced by the outer query.

The provider also chooses how to quote a generated name (`'c0'`, `"c0"`, `[c0]`, `` `c0` ``). Its
spelling is an internal detail of the rendered statement, not a public naming guarantee. Read the
value through the builder rather than hard-coding the name:

```csharp
var inner = dataContext.From<IComplexEntity>().Where(x => x.Id <= 2).Select(x => x.Id * 2);
var rows = dataContext.From(inner).OrderBy(x => x).Select(x => x * 3).ToList(); // 6, 12
```

## Naming a derived table with `As`

`As<TResult>(...)` wraps a projection as a derived table in `FROM`: it projects each row into the
named type and returns a builder over the result, so the projected members can be joined, filtered
and projected further. Because the derived table resets the join arity counter, this is how you keep
composing after a join, including past the eight-table cap:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .As(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String })
    .Where(d => d.OrderId > 100)
    .Select(d => new { d.OrderId, d.CustomerName })
    .ToList();
```

Only the projected members remain visible after `As`, and the in-memory provider rejects a `Join`
on the derived table with `NotSupportedException`. See [Joins](04-joins.md#naming-an-intermediate-projection-as)
for the full operator.

## Scalar subquery in SELECT

Call a single-row method inside the projection to embed the inner query as a scalar column. The method
can be [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})),
[`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})),
[`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) or
[`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})).
nextorm gives the scalar column the outer property name:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First() })
    .ToListAsync();
```

```sql
select id, (select id
 from simple_entity where (id = 1) limit 1) as 'sid'
 from complex_entity
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

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

An `ORDER BY` key may be an expression containing a scalar subquery. The first [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) uses the
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
select t1.id, (select t2.id
 from simple_entity as 't2' where t2.id = t1.id limit 1) as 'sid'
 from complex_entity as 't1'
```

The outer query qualifies only the referenced member; the inner query keeps its own columns and
aliases. [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0}))/[`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) yield `NULL` when no inner row matches.

The referenced member may also come from a join projection: `p.Item1.Id` resolves the table alias
from the projection item's position and the column from its mapping, so a subquery can correlate to
any of the joined sources:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, cid = dataContext.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id).Select(c => c.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select top(1) t3.id
 from complex_entity as [t3]
 where t3.id = cast(t1.id as bigint)) as [cid]
 from simple_entity as [t1]
 join complex_entity as [t2] on cast(t1.id as bigint) = t2.id
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

### Project a correlated aggregate

In correlated **projections**, the supported builder aggregate methods
([`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})), `LongCount`,
`Sum`, `Min`, `Max`, `Avg`, `Stdev`, `Stdevp`, `Var` and `Varp`) are translated to aggregate scalar
subqueries, so you can use them with an outer reference. `LongCount()` returns a `long` count for each
outer row, including `0` when no inner rows match. Projection support does not by itself extend
aggregate-method support in `WHERE` predicates:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, cnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Count() })
    .ToListAsync();
```

```sql
select t1.id, (select count(*)
 from complex_entity as 't2'
 where cast(t2.id as bigint) = t1.id
limit 1) as 'cnt'
 from complex_entity as 't1'
```

The ordered result makes the wider count explicit: `LongCount()` returns the same sequence as `Count()`
converted to `long`. It returns `0L` for an outer row whose inner predicate matches no rows.

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .OrderBy(it => it.Id)
    .Select(it => new
    {
        it.Id,
        cnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Count(),
        longCnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).LongCount()
    })
    .ToListAsync();
// cnt:     [1, 2, 3]
// longCnt: [1L, 2L, 3L]
```


### Filter by a correlated aggregate

You can also put a supported aggregate method on the left side of a filter comparison. It becomes a
correlated scalar subquery evaluated for each outer row:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Where(it => dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Sum(c => c.Int) > 6)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select t1.id
 from complex_entity as 't1'
 where ((select sum(t2.nullableint)
 from complex_entity as 't2'
 where t2.id = t1.id
limit 1) > 6)
```

`Count`, `Sum`, `Min`, `Max` and `Avg` compose this way with ordered comparisons, `==`/`!=`,
arithmetic and logical operators. The comparison stays outside the inner `select`.

SQL `NULL` semantics still apply. A sum over no rows or only `NULL` values is `NULL`; a row whose
aggregate is `NULL` doesn't satisfy the comparison and isn't treated as zero. In the reported data,
parent `8001` has child amounts 5 and 7, `8002` has only a `NULL` amount, and `8003` has no children.
The predicate returns only `[8001]`.

`LongCount` composes here too. It uses a 64-bit result (`count_big(*)` where the provider has it,
`count(*)` otherwise). A builder method outside the supported aggregate list (a scalar
`First`/`Single`, for example) cannot take part in such a comparison: preparation fails closed with
`QueryPreparationException` naming the terminal and the operator, instead of raising an internal
expression-tree error.

Correlation nests arbitrarily: a correlated subquery may itself contain a correlated subquery, and
each outer reference resolves to the alias of the scope that declared it.

The in-memory provider evaluates a correlated subquery once per outer row too, so scalar subqueries,
aggregate terminals, `EXISTS` and `IN` work there for depth-one correlation as well. It still rejects
correlation depth greater than one, an outer reference inside the inner projection or `ORDER BY`, a
correlated `GROUP BY`/`HAVING`, and an async source with `NotSupportedException`; use a SQL provider for
those forms.

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
select t1.id
 from simple_entity as 't1' where exists(select *
 from complex_entity where (id = cast(t1.id as bigint)))
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

A correlated `exists` (like `@in`, `any` and `all`) is a boolean predicate, so it combines with the
logical operators `&&`, `||` and `!` and renders as the matching `exists`/`and`/`or`/`not` SQL:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(s => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id))
                || s.Id == 100)
    .Select(s => s.Id)
    .ToListAsync();
```

```sql
-- SQLite
select t1.id
 from simple_entity as 't1'
where (exists(select *
 from complex_entity as 't2' where (t2.id = cast(t1.id as bigint))) or (t1.id = 100))
```

Negating a subquery predicate (`!SqlFunctions.Sql.exists(...)`) renders `not (exists(...))`. The
in-memory provider evaluates these combinations once per outer row for depth-one correlation.

## IN with a subquery

`SqlFunctions.Sql.@in(column, query)` emits `IN (SELECT ...)`:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => SqlFunctions.Sql.@in((int)it.Id, dataContext.From<ISimpleEntity>().Where(it => it.Id == 2).Select(it => it.Id)))
    .Select(it => it.Id)
    .FirstOrDefaultAsync();
```

```sql
select id
 from complex_entity where (cast(id as integer) in (select id
 from simple_entity where (id = 2)))
```

Output:

| Id |
|----|
| 2 |

The same method also accepts an `IEnumerable<T>` or a `params T[]` of literal values (see
[Filtering (WHERE)](03-filtering-where.md)); the `QueryCommand<T>` overload is the subquery form.

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
select id
 from simple_entity where (cast(id as bigint) = any(select id
 from complex_entity))
```

SQLite does not implement `ANY` or `ALL`; the same query is accepted by the translator but throws a
`Microsoft.Data.Sqlite.SqliteException` when executed (covered by
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:16` and `:30`).

## Provider differences

| Provider | Derived table alias | Scalar subquery | `any` / `all` |
|---|---|---|---|
| SQLite | optional | supported (correlated too) | **not supported** - `SqliteException` at execution |
| SQL Server | required (`as [t1]`) | supported (correlated too) | supported |
| PostgreSQL | required (`as "t1"`) | supported (correlated too) | supported |
| MySQL | required (`` as `t1` ``) | supported (correlated too) | supported |
| MariaDB | required (`` as `t1` ``) | supported (correlated too) | supported |
| ClickHouse | required (`` as `t1` ``) | supported (correlated too) | supported |
| In-memory | not applicable | supported (depth-one correlated scalar/`EXISTS`/`IN`); deeper forms throw `NotSupportedException` | not covered |

A subquery that references the outer query forces the outer `FROM` to be aliased (`t1`) on every
SQL provider. On SQL Server a boolean-valued subquery predicate projected as a scalar is wrapped in
`cast(case when ... then 1 else 0 end as bit)`, because T-SQL has no boolean scalar type.

## Limitations

* Nested correlation (a subquery that references an outer reference of another subquery) is rejected
  with `NotSupportedException`; keeping it explicit avoids binding an outer marker to the wrong query.
* The in-memory provider supports depth-one correlated scalar/`EXISTS`/`IN` and rejects the deeper forms listed above.

## See also

- [Joins](04-joins.md) - joining a `QueryCommand<T>` as a derived table.
- [Set operations](08-set-operations.md)
- [Filtering (WHERE)](03-filtering-where.md) - `IN` over a literal list.

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Queries/Structure/CommonTestSuite.CorrelatedQuery.cs`,
`tests/nextorm.sqlite.tests/SqlGeneration/Plan/CorrelatedQueryTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:612,623,633,643,653,663`,
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:16,30`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:248`.
