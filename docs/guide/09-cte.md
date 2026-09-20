# Common table expressions (CTE)

> Declare one or more named `with` queries and use them as the `from` source of a query, including
> recursive CTEs for series and hierarchies.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Set operations](07-set-operations.md)

## Overview

A CTE is declared with `With(name, query)` (non-recursive) or `WithRecursive(name, query, maxRecursion)`
(recursive) on [`IDataContext`](xref:NextORM.Core.IDataContext). Both are extension methods ([`DataContextExtensions`](xref:NextORM.Core.DataContextExtensions)) and both return a
[`CteQuery`](xref:NextORM.Core.CteQuery) scope that holds the declarations collected so far in [`Ctes`](xref:NextORM.Core.CteQuery.Ctes):

```csharp
public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query);

public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query,
    int? maxRecursion = null);
```

Declarations are immutable: every [`With`](xref:NextORM.Core.DataContextExtensions)/[`WithRecursive`](xref:NextORM.Core.DataContextExtensions) call returns a **new** scope that appends a
[`CteDefinition`](xref:NextORM.Core.CteDefinition) to the previous ones. A definition records the name, the [`QueryCommand`](xref:NextORM.Core.QueryCommand) that produces it
and whether the body may reference its own name.

[`From`](xref:NextORM.Core.CteQuery) (or `From(CteDefinition)`) starts a new query whose `from` is one of the
declared CTEs, carrying every declaration into the resulting command. From there the entity-free
[`TableAlias`](xref:NextORM.Core.TableAlias) mode is used to read the CTE columns (`t["id"].AsInt`), and the normal [`Where`](xref:NextORM.Core.EntityBuilder`1)/[`Join`](xref:NextORM.Core.EntityBuilder`1)/
[`Select`](xref:NextORM.Core.EntityBuilder`1) operators apply. Recursive bodies reference their own name the same way
(`dataContext.From("nums")` inside the step query).

Rendering: dialects that use the ANSI form emit `with recursive` when any definition is recursive
(SQLite, PostgreSQL); SQL Server declares a recursive CTE with `with` alone and appends the depth option
after the statement.

## Non-recursive CTE

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id });

var rows = dataContext
    .With("recent", recent)
    .From("recent")
    .Select(t => new { Id = t["id"].AsInt })
    .ToList();
```

```sql
-- SQLite (all providers produce the same shape)
with recent as (select id from complex_entity where (id > 1)) select id from recent
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id |
|----|
| 2 |
| 3 |

## Chained declarations

Each [`With`](xref:NextORM.Core.DataContextExtensions) appends to the previous scope, so a later CTE can be defined in terms of an earlier one.
The declarations are rendered in declaration order:

```csharp
var first = dataContext.From<IComplexEntity>()
    .Where(x => x.Id > 1)
    .Select(x => new { x.Id });

var second = dataContext.From("first")
    .Select(t => new { id = t["id"].AsInt });

var rows = dataContext
    .With("first", first)
    .With("second", second)
    .From("second")
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

```sql
with first as (select id from complex_entity where (id > 1)), second as (select id from first) select id from second
```

`From(CteDefinition)` is equivalent to `From(definition.Name)` and is convenient when you kept the scope
instead of the name:

```csharp
var cte = dataContext.With("recent", recent);
var rows = cte.From(cte.Ctes[0])
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

## Recursive CTE: a number series

A recursive CTE is a `union all` of an **anchor** (a non-recursive query) and a **step** that reads the
CTE by name and stops when the predicate no longer matches. Call [`WithRecursive`](xref:NextORM.Core.DataContextExtensions) with the union as the
body:

```csharp
public sealed class CteNumberRow
{
    public int n { get; set; }
}

var anchor = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new CteNumberRow { n = s.Id });

var step = dataContext.From("nums")
    .Where(t => t["n"].AsInt < 5)
    .Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });

var body = anchor.UnionAll(step);

var numbers = dataContext
    .WithRecursive("nums", body)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();

// numbers -> 1, 2, 3, 4, 5
```

```sql
-- SQLite: `with recursive` prefix
with recursive nums as (select id as 'n' from simple_entity where (id = 1) union all select (n + 1) as 'n' from nums where (n < 5)) select n from nums
```

Output:

| n |
|---|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |

`maxRecursion` is an optional depth limit. Only SQL Server has a statement-level option for it, and the
dialect appends `option (maxrecursion n)` to the end of the statement; SQLite and PostgreSQL ignore it
and use their own default:

```csharp
var numbers = dataContext
    .WithRecursive("nums", body, maxRecursion: 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();
```

```sql
-- SQL Server: no `recursive` keyword, depth option appended
with nums as (select id as [n] from simple_entity where (id = 1) union all select (n + 1) as [n] from nums where (n < 5)) select n from nums option (maxrecursion 100)
```

## CTE plus a captured parameter

A captured value in the CTE body becomes a parameter exactly like anywhere else, and the
parameter-extraction pass walks the `with` clause, not only the outer statement:

```csharp
var threshold = 1L;

var prepared = dataContext
    .With("recent", dataContext.From<IComplexEntity>()
        .Where(x => x.Id > threshold)
        .Select(x => new { x.Id }))
    .From("recent")
    .Select(t => new { id = t["id"].AsInt })
    .Prepare();

var ids = prepared.ToList(dataContext);
```

```sql
-- SQLite parameter placeholder; SQL Server/PostgreSQL use @threshold
with recent as (select id from complex_entity where (id > $threshold)) select id from recent
```

## Plan-cache reuse

The CTE definitions take part in the plan-cache key, so two queries that differ only in a CTE body do
**not** share a cached plan. Conversely, freshly built but structurally equal chains do reuse the cached
plan, including a recursive CTE whose body is a `union all` of two fresh commands:

* a cached CTE plan re-extracts the CTE's captured parameter on a cache hit
  (`PlanCacheTests.Cte_WithCapturedParam_RepeatedExecution_ShouldRefreshParam`);
* two different CTE bodies produce two plans
  (`PlanCacheTests.Cte_DifferentDefinitions_ShouldNotSharePlan`);
* an equivalent recursive CTE built again reuses the cached plan
  (`PlanCacheTests.RecursiveCte_FreshCommand_ShouldReuseCachedPlan`).

See [Query reuse: cache vs Prepare](15-query-reuse.md) for the lifetime and invalidation rules of the
plan cache.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | `with` for non-recursive, `with recursive` for recursive; no depth option. |
| SQL Server | Recursive CTEs are declared with `with` alone (no `recursive` keyword); `maxRecursion` renders `option (maxrecursion n)` at the end of the statement. |
| PostgreSQL | `with` / `with recursive`; no depth option. |
| MySQL | `with` for non-recursive, `with recursive` for recursive; no depth option. |
| MariaDB | `with` / `with recursive`; no depth option. |
| ClickHouse | Every CTE is declared with plain `with`; recursive CTEs are not supported. |
| In-memory | Not applicable: CTEs are rendered by the SQL dialects and are not part of the in-memory provider. |

## See also

* [Set operations](07-set-operations.md) - [`UnionAll`](xref:NextORM.Core.QueryCommand`1) and friends, used to build a recursive body.
* [Joins](03-joins.md) - joining a CTE to a table, as in `CommonTestSuite.Cte.cs`.
* [Raw SQL](14-raw-sql.md) - when the whole statement is hand-written.
* [Query reuse: cache vs Prepare](15-query-reuse.md) - how CTE plans are cached.

---

Source: `src/nextorm.core/Builders/CteQuery.cs:7`, `src/nextorm.core/DataContext/DataContextExtensions.cs:9`;
`tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:14`, `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:33`;
`tests/nextorm.core.tests/CteQueryTests.cs:8`;
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:190`, `tests/nextorm.sqlite.tests/PlanCacheTests.cs:343`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1188`, `:1202`, `:1216`, `:1231`, `:1248`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:827`, `:856`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:759`, `:788`.
