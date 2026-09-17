# Set operations

> Combine two result sets with `Union`, `UnionAll`, `Intersect`, `IntersectAll`, `Except` and `ExceptAll`, and chain them left to right.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Subqueries](06-subqueries.md) · [SELECT DISTINCT](08-distinct.md)

## Overview

Every `QueryCommand<TResult>` exposes six set-operation methods that take another command and return
a new command:

| Method | SQL keyword | Keeps duplicates |
|---|---|---|
| `Union(query)` | `union` | no |
| `UnionAll(query)` | `union all` | yes |
| `Intersect(query)` | `intersect` | no |
| `IntersectAll(query)` | `intersect all` | yes |
| `Except(query)` | `except` | no |
| `ExceptAll(query)` | `except all` | yes |

The right-hand command may project a different element type: the method is generic over the other
side (`Union<T>(QueryCommand<T>)`), so `SimpleEntity.Select(it => it.Id).Union(ComplexEntity.Select(it => (int)it.Id))`
is valid as long as the shapes line up.

The result is itself a `QueryCommand`, so it can be executed through `From(result)` or have another
set operation chained onto it. **Chaining applies left to right**: each new operation combines the
accumulated left side with the next command, and standard operator precedence applies within the
chain (`(A except B) intersect C`, not `A except (B intersect C)`).

The biggest portability caveat is `*ALL`. PostgreSQL implements `intersect all` and `except all`;
SQLite and SQL Server do not, and nextorm throws `NotSupportedException` when such a query is
prepared. `union`, `union all`, `intersect` and `except` are supported everywhere.

## Union and UnionAll

```csharp
var distinct = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Union(dataContext.Create<ISimpleEntity>().Select(it => it.Id));

var all = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .UnionAll(dataContext.Create<ISimpleEntity>().Select(it => it.Id));
```

```sql
select id from simple_entity
 union 
select id from simple_entity
```

```sql
select id from simple_entity
 union all 
select id from simple_entity
```

The two sides may also be different entities as long as the element types line up. In the
integration suite `SimpleEntity` (ids `1..10`) is unioned with `ComplexEntity` (ids `1..3`) cast to
`int`, so the deduplicated union has 10 rows and the `UnionAll` of the same pair has 13:

```csharp
var distinctCount = dataContext.From(
    dataContext.Create<ISimpleEntity>().Select(it => it.Id)
        .Union(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id))).Count(); // 10

var allCount = dataContext.From(
    dataContext.Create<ISimpleEntity>().Select(it => it.Id)
        .UnionAll(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id))).Count(); // 13
```

## Intersect and Except

```csharp
var common = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Intersect(dataContext.Create<ISimpleEntity>().Select(it => it.Id));

var onlyLeft = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Except(dataContext.Create<ISimpleEntity>().Select(it => it.Id));
```

```sql
select id from simple_entity
 intersect 
select id from simple_entity
```

```sql
select id from simple_entity
 except 
select id from simple_entity
```

Cross-entity: because `simple_entity` holds ids `1..10` and `complex_entity` holds `1..3`,
`INTERSECT` keeps `1, 2, 3` and `EXCEPT` leaves `4..10`.

## Chaining is left to right

```csharp
// (simple EXCEPT complex) INTERSECT simple = {4..10} INTERSECT {1..10} = {4..10}.
var cmd = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Except(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id))
    .Intersect(dataContext.Create<ISimpleEntity>().Select(it => it.Id));

var count = dataContext.From(cmd).Count(); // 7
```

## IntersectAll and ExceptAll

```csharp
var cmd = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .IntersectAll(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id));

dataContext.From(cmd).Count();
```

```sql
-- PostgreSQL only
select id from simple_entity
 intersect all 
select id from simple_entity
```

```csharp
var cmd = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .ExceptAll(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id));

dataContext.From(cmd).Count();
```

When the dialect does not support the operation, preparing the command throws
`NotSupportedException` with the operation name in the message (`"The IntersectAll set operation is
not supported by this SQL dialect"`).

## Querying a set-operation result

A set operation returns a command, so you query it through `From`:

```csharp
var cmd = dataContext.Create<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.Create<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

An entity-typed result can be projected the same way:

```csharp
var count = dataContext.From(
    dataContext.Create<ISimpleEntity>().Select(it => new { it.Id })
        .Union(dataContext.Create<IComplexEntity>().Select(it => new { it.Id })))
    .Count();
```

## Provider differences

| Provider | `union` / `union all` | `intersect` / `except` | `intersect all` / `except all` |
|---|---|---|---|
| SQLite | supported | supported | **`NotSupportedException`** when prepared |
| SQL Server | supported | supported | **`NotSupportedException`** when prepared |
| PostgreSQL | supported | supported | supported |
| In-memory | not covered by the in-memory test suite | not covered | not covered |

This matches the `SupportsIntersectExceptAll` capability: PostgreSQL is the only shipped provider
that returns `true`; SQLite and SQL Server return `false` and the SQL builder refuses to render an
`*ALL` set operation for them.

## See also

- [SELECT DISTINCT](08-distinct.md) - `Distinct()` and set operations interact.
- [Subqueries](06-subqueries.md) - a set-operation command can be used as a `FROM` source.
- [Querying and projections](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SetOperations.cs:8`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:743,761`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:49,69,89`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:62,102`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:48,88`.
