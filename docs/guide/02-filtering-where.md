# Filtering (WHERE)

> Build SQL `WHERE` predicates from C# operators, null tests, conditionals and parameterised value lists.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

`Where` takes a boolean expression and returns a new `Entity<TEntity>`; like every builder method it
is immutable, so the original is unchanged. Repeating `Where` combines the predicates with `and`:

```csharp
public Entity<TEntity> Where(Expression<Func<TEntity, bool>> condition)
```

The lambda is translated into the `WHERE` clause and nothing is executed until a terminal such as
`ToListAsync` is called. The same expression tree is also what the in-memory provider compiles and
runs, so a query that works against a real database can be exercised in memory.

Two kinds of values reach the database differently:

* A **captured local** (a variable from the enclosing scope) and `NORM.Param<T>(index)` become command
  parameters, so the plan can be reused across executions with different values.
* A **literal constant** written directly in the lambda is rendered inline in the SQL text.

## Comparison operators

| C# | SQL | Notes |
|---|---|---|
| `x.Id == 1` | `id = 1` | `=`; null-aware (see below) |
| `x.Id != 1` | `id != 1` | `!=` |
| `x.Id > 1` | `(id > 1)` | `>`; comparison operands are parenthesised |
| `x.Id >= 1` | `(id >= 1)` | `>=` |
| `x.Id < 1` | `(id < 1)` | `<` |
| `x.Id <= 1` | `(id <= 1)` | `<=` |

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Where(x => x.Id >= 9)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from simple_entity where (id >= 9)
```

## Null handling

Comparing a column with `null` renders `is null` / `is not null` instead of `=` / `!=`:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Where(x => x.String == null)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from complex_entity where somestring is null
```

`!= null` emits `is not null`. The rewrite is driven by the comparison operand being the `null`
literal; a nullable column should be tested with a literal `null`, not with a captured value that is
merely expected to hold `null`.

## Combining predicates

`&&` and `||` map to `and` and `or` and are parenthesised as a group:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Where(x => x.Boolean == true && x.Id > 1)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from complex_entity where (b = 1 and (id > 1))
```

On PostgreSQL the boolean literal is `true` (`b = true`); on SQLite and SQL Server it is `1`
(`b = 1`).

## Negation

`!` maps to `not (...)` and negates the whole operand:

```csharp
var ids = await dataContext.Create<ComplexEntity>()
    .Where(x => !x.Boolean!.Value)
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id from complex_entity where not (b)
```

```csharp
.Where(x => !(x.Boolean!.Value && x.Id > 1L))
```

```sql
select id from complex_entity where not ((b and (id > 1)))
```

When a negation is projected rather than filtered, SQL Server has to materialise it as a `bit` value
(`cast(case when not (b) then 1 else 0 end as bit)`); SQLite and PostgreSQL keep the boolean scalar
(`not (b)`).

## Arithmetic and bitwise operators

Arithmetic operators are rendered verbatim and parenthesised: `+`, `-`, `*`, `/` and `%`.

```csharp
var rows = await dataContext.From("simple_entity")
    .Where(tbl => tbl.Long("id") + 2 == 1)
    .Select(tbl => new { Id = tbl.Long("id") })
    .ToListAsync();
```

```sql
select id from simple_entity where (id + 2) = 1
```

Unary minus is `-(x)` and the ones-complement is `~(x)`. Integral bitwise operators map as follows:

| C# | SQL | Notes |
|---|---|---|
| `x.Id & 1` | `(id & 1)` | bitwise AND |
| `x.Id \| 1` | `(id \| 1)` | bitwise OR |
| `x.Id << 1` | `(id << 1)` | left shift; not valid T-SQL |
| `x.Id >> 1` | `(id >> 1)` | right shift; not valid T-SQL |
| `x.Id ^ 1` | - | XOR is **not supported** and throws `NotSupportedException` |

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Where(x => (x.Id & 1) == 1)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from simple_entity where (id & 1) = 1
```

> The shift operators are emitted literally. SQLite and PostgreSQL accept `<<` / `>>`; T-SQL does
> not, so a query using them fails on SQL Server.

## COALESCE (`??`)

`??` becomes the provider's coalesce function:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Select(x => new { x.Id, V = x.String ?? "" })
    .ToListAsync();
```

| Provider | SQL |
|---|---|
| SQLite | `select id, ifnull(somestring, '') as 'V' from complex_entity` |
| SQL Server | `select id, isnull(somestring,'') as [V] from complex_entity` |
| PostgreSQL | `select id, coalesce(somestring, '') as "V" from complex_entity` |

## Conditional (`?:`) and switch

The ternary operator becomes an ANSI `CASE WHEN`:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Select(x => new { x.Id, Size = x.Id > 1 ? "big" : "small" })
    .ToListAsync();
```

```sql
select id, case when (id > 1) then 'big' else 'small' end as 'Size' from complex_entity
```

A conditional can appear inside a predicate:

```csharp
var count = await dataContext.Create<ComplexEntity>()
    .Where(x => (x.Int == null ? 0 : x.Int) == 1)
    .CountAsync();
```

```sql
select count(*) from complex_entity where case when nullableint is null then 0 else nullableint end = 1
```

A C# `switch` expression over constant patterns becomes a searched `CASE`:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .Select(e => new { e.Id, Label = e.Id switch { 1 => "one", 2 => "two", _ => "other" } })
    .ToListAsync();
```

```sql
select id, case when id = 1 then 'one' when id = 2 then 'two' else 'other' end as 'Label' from complex_entity
```

A switch whose comparison is a method call (for example a string equality overload, which the C#
compiler uses for some string patterns) is not supported and throws `NotSupportedException`.

## Captured parameters and `NORM.Param`

A captured local is extracted as a named command parameter:

```csharp
var threshold = 5L;
var rows = await dataContext.Create<ComplexEntity>()
    .Where(x => x.Id > threshold)
    .Select(x => new { x.Id })
    .ToListAsync();
```

| Provider | SQL |
|---|---|
| SQLite | `select id from complex_entity where (id > $threshold)` |
| SQL Server | `select id from complex_entity where (id > @threshold)` |
| PostgreSQL | `select id from complex_entity where (id > @threshold)` |

`NORM.Param<T>(index)` declares a runtime parameter whose value is supplied to the terminal, which is
useful when the same query shape is prepared or cached and executed repeatedly:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Where(x => x.Id == NORM.Param<int>(0))
    .Select(x => new { x.Id })
    .ToListAsync(42);
```

```sql
-- SQLite
select id from simple_entity where id = $norm_p0
```

Runtime parameters are named `norm_p{index}`; the value `42` is bound to `norm_p0` by the terminal.
See [Query reuse: cache vs Prepare](15-query-reuse.md) for the lifetime rules.

## `IN` and `Contains`

`NORM.SQL.@in` takes a column plus a `QueryCommand<T>`, an `IEnumerable<T>` or a `params T[]`:

```csharp
var values = new long[] { 1, 3, 10 };
var ids = await dataContext.Create<ComplexEntity>()
    .Where(e => NORM.SQL.@in(e.Id, values))
    .Select(e => e.Id)
    .ToListAsync();
```

```sql
-- SQLite
select id from complex_entity where id in ($p0, $p1, $p2)
```

`Contains` on a captured `List<T>` or `T[]` produces the same `IN` predicate:

```csharp
var values = new List<long> { 1, 3 };
var ids = await dataContext.Create<ComplexEntity>()
    .Where(e => values.Contains(e.Id))
    .Select(e => e.Id)
    .ToListAsync();
```

```sql
select id from complex_entity where id in ($p0, $p1)
```

Special cases:

| Case | SQL |
|---|---|
| Empty collection | `where 1 = 0` (no parameters) |
| One element | `where id in ($p0)` |
| Collection with `null` on a nullable column | `where (nullableint in ($p0) or nullableint is null)` |
| Collection that is only `null` | `where nullableint is null` |

A captured list or array is captured by reference by the expression tree, but its values are built
into the prepared command. When the collection is mutated or reassigned between executions, nextorm
detects the changed shape and rebuilds the command, so a second `ToList()` sees the new values rather
than stale results.

## Pattern and subquery predicates

`NORM.SQL.like`, `NORM.SQL.exists`, `NORM.SQL.any` and `NORM.SQL.all` are the remaining predicate
helpers:

| Expression | SQL |
|---|---|
| `x.String.Contains("df")` | `somestring like '%df%'` |
| `x.String.StartsWith("xx")` | `somestring like 'xx%'` |
| `x.String.EndsWith("sd")` | `somestring like '%sd'` |
| `x.String.Contains("a%b_c")` | `somestring like '%a\%b\_c%' escape '\'` |
| `NORM.SQL.like(x.String, "%a%")` | `somestring like '%a%'` |
| `NORM.SQL.like(x.String, "%a!%", "!")` | `somestring like '%a!%' escape '!'` |
| `NORM.SQL.exists(query)` | `exists(<query>)` |
| `x.Id == NORM.SQL.any(query)` | `id = any(<query>)` |
| `x.Id == NORM.SQL.all(query)` | `id = all(<query>)` |

For a captured pattern the wildcards are concatenated around the parameter at build time, for example
`somestring like '%' || $needle || '%'` on SQLite. `any` and `all` are not supported by the SQLite
engine and fail when the statement runs.

## Function and operator mapping

| C# construct | SQL |
|---|---|
| `==` / `!=` | `=` / `!=`; `is` / `is not` against `null` |
| `>`, `>=`, `<`, `<=` | `>`, `>=`, `<`, `<=` (parenthesised) |
| `&&` / `\|\|` | `and` / `or` |
| `!` | `not (...)`; a projected boolean is cast to `bit` on SQL Server |
| `+ - * / %` | `+ - * / %`; string `+` is `\|\|` on SQLite/PostgreSQL and `+` on SQL Server |
| `&` / `\|` | `&` / `\|` |
| `<<` / `>>` | `<<` / `>>` |
| `^` | not supported (`NotSupportedException`) |
| `~x` | `~(x)` |
| `-x` | `-(x)` |
| `??` | `ifnull` (SQLite), `isnull` (SQL Server), `coalesce` (PostgreSQL) |
| `?:` | `case when ... then ... else ... end` |
| `switch` | searched `case when ... then ... end` |
| `NORM.SQL.@in` / `Contains` | `in (...)` |
| `NORM.SQL.like` / `Contains` / `StartsWith` / `EndsWith` | `like` |

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Parameters use `$` (`$norm_p0`); `??` is `ifnull`; boolean literals are `1` / `0`; `any` / `all` are not available. |
| SQL Server | Parameters use `@`; `??` is `isnull`; boolean literals are `1` / `0`; a projected boolean predicate is wrapped in `cast(case ... as bit)`; shifts are not valid T-SQL. |
| PostgreSQL | Parameters use `@`; `??` is `coalesce`; boolean literals are `true` / `false`; boolean scalars need no cast. |
| In-memory | Predicates are compiled as .NET delegates; there is no parameter prefix or SQL rendering. |

## See also

* [Querying and projections](01-querying-and-projections.md)
* [Sorting and paging](05-sorting-and-paging.md)
* [Scalar functions](11-scalar-functions.md)
* [Subqueries](06-subqueries.md)
* [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:187`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:202`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:216`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:230`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:244`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:254`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:276`,
`test/nextorm.integration.tests/CommonTestSuite.Conditional.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.Unary.cs:8`,
`test/nextorm.integration.tests/CommonTestSuite.In.cs:9`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:602`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:664`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:775`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:821`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:997`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:1051`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:1107`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:422`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:367`,
`src/nextorm.core/Query/NORM.cs:185`;
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:2032`.
