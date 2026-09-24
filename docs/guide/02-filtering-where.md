# Filtering (WHERE)

> Build SQL `WHERE` predicates from C# operators, null tests, conditionals and parameterised value lists.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) takes a boolean expression and returns a new [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1); like every builder method it
is immutable, so the original is unchanged. Repeating [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) combines the predicates with `and`:

```csharp
public EntityBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
```

The lambda is translated into the `WHERE` clause and nothing is executed until a terminal such as
[`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) is called. The same expression tree is also what the in-memory provider compiles and
runs, so a query that works against a real database can be exercised in memory.

Two kinds of values reach the database differently:

* A **captured local** (a variable from the enclosing scope) and [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) become command
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
var rows = await dataContext.From<SimpleEntity>()
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
var rows = await dataContext.From<ComplexEntity>()
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
var rows = await dataContext.From<ComplexEntity>()
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
var ids = await dataContext.From<ComplexEntity>()
    .Where(x => !x.Boolean!.Value)
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id from complex_entity where not (b)
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id |
|----|
| 2 |
| 3 |

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
    .Where(tbl => tbl.GetInt64("id") + 2 == 1)
    .Select(tbl => new { Id = tbl.GetInt64("id") })
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
var rows = await dataContext.From<SimpleEntity>()
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
var rows = await dataContext.From<ComplexEntity>()
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
var rows = await dataContext.From<ComplexEntity>()
    .Select(x => new { x.Id, Size = x.Id > 1 ? "big" : "small" })
    .ToListAsync();
```

```sql
select id, case when (id > 1) then 'big' else 'small' end as 'Size' from complex_entity
```

A conditional can appear inside a predicate:

```csharp
var count = await dataContext.From<ComplexEntity>()
    .Where(x => (x.Int == null ? 0 : x.Int) == 1)
    .CountAsync();
```

```sql
select count(*) from complex_entity where case when nullableint is null then 0 else nullableint end = 1
```

A C# `switch` expression over constant patterns becomes a searched `CASE`:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(e => new { e.Id, Label = e.Id switch { 1 => "one", 2 => "two", _ => "other" } })
    .ToListAsync();
```

```sql
select id, case when id = 1 then 'one' when id = 2 then 'two' else 'other' end as 'Label' from complex_entity
```

A switch whose comparison is a method call (for example a string equality overload, which the C#
compiler uses for some string patterns) is not supported and throws `NotSupportedException`.

## Captured parameters and [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))

A captured local is extracted as a named command parameter:

```csharp
var threshold = 5L;
var rows = await dataContext.From<ComplexEntity>()
    .Where(x => x.Id > threshold)
    .Select(x => new { x.Id })
    .ToListAsync();
```

| Provider | SQL |
|---|---|
| SQLite | `select id from complex_entity where (id > $threshold)` |
| SQL Server | `select id from complex_entity where (id > @threshold)` |
| PostgreSQL | `select id from complex_entity where (id > @threshold)` |

[`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) declares a runtime parameter whose value is supplied to the terminal, which is
useful when the same query shape is prepared or cached and executed repeatedly:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
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

`SqlFunctions.Sql.@in` takes a column plus a `QueryCommand<T>`, an `IEnumerable<T>` or a `params T[]`:

```csharp
var values = new long[] { 1, 3, 10 };
var ids = await dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.Sql.@in(e.Id, values))
    .Select(e => e.Id)
    .ToListAsync();
```

```sql
-- SQLite
select id from complex_entity where id in ($p0, $p1, $p2)
```

Output:

| Id |
|----|
| 1 |
| 3 |

`Contains` on a captured `List<T>` or `T[]` produces the same `IN` predicate:

```csharp
var values = new List<long> { 1, 3 };
var ids = await dataContext.From<ComplexEntity>()
    .Where(e => values.Contains(e.Id))
    .Select(e => e.Id)
    .ToListAsync();
```

```sql
select id from complex_entity where id in ($p0, $p1)
```

Output:

| Id |
|----|
| 1 |
| 3 |

Special cases:

| Case | SQL |
|---|---|
| Empty collection | `where 1 = 0` (no parameters) |
| One element | `where id in ($p0)` |
| Collection with `null` on a nullable column | `where (nullableint in ($p0) or nullableint is null)` |
| Collection that is only `null` | `where nullableint is null` |

A captured list or array is captured by reference by the expression tree, but its values are built
into the prepared command. When the collection is mutated or reassigned between executions, nextorm
detects the changed shape and rebuilds the command, so a second [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) sees the new values rather
than stale results.

### `GLOBAL IN` (ClickHouse)

ClickHouse's distributed predicate `column GLOBAL IN (subquery | values)` is exposed through
[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})), which takes the same
left-hand column and right-hand side as [`SqlFunctions.Sql.@in`](xref:NextORM.Core.CommonFunctions.in``1(``0,NextORM.Core.QueryCommand{``0})):

```csharp
var values = new int[] { 1, 3 };
var ids = dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.ClickHouse.global_in(e.Id, values))
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from complex_entity where global in (@p0, @p1)
```

Negate it with C# `!` to render `GLOBAL NOT IN`. The predicate requires
[`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates) and is available
only on ClickHouse; every other provider and the in-memory context throw `NotSupportedException`.
See [Provider-specific SQL](provider-specific/overview.md) for the full catalogue.

## Pattern and subquery predicates

`SqlFunctions.Sql.like`, `SqlFunctions.Sql.exists`, `SqlFunctions.Sql.any` and `SqlFunctions.Sql.all` are the remaining predicate
helpers:

| Expression | SQL |
|---|---|
| `x.String.Contains("df")` | `somestring like '%df%'` |
| `x.String.StartsWith("xx")` | `somestring like 'xx%'` |
| `x.String.EndsWith("sd")` | `somestring like '%sd'` |
| `x.String.Contains("a%b_c")` | `somestring like '%a\%b\_c%' escape '\'` |
| `SqlFunctions.Sql.like(x.String, "%a%")` | `somestring like '%a%'` |
| `SqlFunctions.Sql.like(x.String, "%a!%", "!")` | `somestring like '%a!%' escape '!'` |
| `SqlFunctions.Sql.exists(query)` | `exists(<query>)` |
| `x.Id == SqlFunctions.Sql.any(query)` | `id = any(<query>)` |
| `x.Id == SqlFunctions.Sql.all(query)` | `id = all(<query>)` |
| `SqlFunctions.Postgres.any(x, array)` | `x = any(@array)` (PostgreSQL) |
| `x == SqlFunctions.Postgres.any(array)` | `x = any(@array)` (PostgreSQL) |
| `SqlFunctions.Sql.contains(x.String, "foo")` | `contains(somestring, 'foo')` (SQL Server) |
| `SqlFunctions.Sql.freetext(x.String, "foo")` | `freetext(somestring, 'foo')` (SQL Server) |

`SqlFunctions.Sql.contains`/`SqlFunctions.Sql.freetext` are full-text predicates ([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText),
rendered by [`MakeFullText`](xref:NextORM.Core.ISqlDialect.MakeFullText(System.String,System.String,System.String))); the column must be full-text indexed. SQL Server renders
`contains`/`freetext` (materialised as a `bit` when projected), PostgreSQL
`to_tsvector(col) @@ plainto_tsquery(search)` (or `websearch_to_tsquery` for `freetext`), and
MySQL/MariaDB `match(col) against(search in boolean mode) > 0` (natural-language mode for `freetext`).

A scalar subquery is also valid in `WHERE`: a single-row terminal on the right-hand side (`First`,
`Single`, their `*OrDefault` forms, or an aggregate such as `Count`) renders as a parenthesised
`select` and may reference the outer row (correlated). These forms, together with the full set of
subquery positions and the in-memory limits, are covered in [Subqueries](06-subqueries.md).

For a captured pattern the wildcards are concatenated around the parameter at build time, for example
`somestring like '%' || $needle || '%'` on SQLite. `any` and `all` are not supported by the SQLite
engine and fail when the statement runs. Over an **array** they require PostgreSQL, where the whole
array is bound as one parameter; see [Arrays](11-scalar-functions.md#arrays-postgresql).

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
| `SqlFunctions.Sql.@in` / `Contains` | `in (...)` |
| `SqlFunctions.Sql.like` / `Contains` / `StartsWith` / `EndsWith` | `like` |
| `SqlFunctions.Sql.contains` / `SqlFunctions.Sql.freetext` | `contains` / `freetext` (SQL Server); `@@` match (PostgreSQL); `match ... against` (MySQL/MariaDB) |

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Parameters use `$` (`$norm_p0`); `??` is `ifnull`; boolean literals are `1` / `0`; `any` / `all` are not available. |
| SQL Server | Parameters use `@`; `??` is `isnull`; boolean literals are `1` / `0`; a projected boolean predicate is wrapped in `cast(case ... as bit)`; shifts are not valid T-SQL. |
| PostgreSQL | Parameters use `@`; `??` is `coalesce`; boolean literals are `true` / `false`; boolean scalars need no cast. |
| MySQL | Parameters use `@`; `??` is `coalesce`; boolean literals are `1` / `0`; `any` / `all` are not available; the integer ones-complement renders as `(-(x) - 1)`. |
| MariaDB | Same as MySQL: `@` parameters, `coalesce`, `1` / `0` booleans, no `any` / `all`. |
| ClickHouse | Parameters use `@` (the driver rewrites them to `{name:Type}`); `??` is `coalesce`; boolean literals are `true` / `false`; `any` / `all` are not available; `global_in` adds the distributed `GLOBAL IN` predicate. |
| In-memory | Predicates are compiled as .NET delegates; there is no parameter prefix or SQL rendering. |

## See also

* [Querying and projections](01-querying-and-projections.md)
* [Sorting and paging](05-sorting-and-paging.md)
* [Scalar functions](11-scalar-functions.md)
* [Subqueries](06-subqueries.md)
* [Provider-specific SQL](provider-specific/overview.md)
* [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:187`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:202`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:216`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:230`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:244`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:254`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:276`,
`tests/nextorm.integration.tests/CommonTestSuite.Conditional.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.Unary.cs:8`,
`tests/nextorm.integration.tests/CommonTestSuite.In.cs:9`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:602`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:664`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:775`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:821`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:997`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1051`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1107`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:422`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:367`,
`src/nextorm.core/Query/SqlFunctions.cs:185`;
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:2032`.
