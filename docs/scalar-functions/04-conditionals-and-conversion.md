# COALESCE (`??`) and CAST

`a ?? b` maps to the provider's two-argument null replacement. A numeric conversion of a numeric operand
- a C# cast such as `(double)e.Id`, or a `Convert.ToXxx(value)` call - maps to `cast(x as <type>)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { V = e.String ?? "" })
    .ToList();

var halves = dataContext.From<IComplexEntity>()
    .Select(e => (double)e.Id / 2.0)
    .ToList();
```

```sql
-- SQLite
select ifnull(somestring, '')
 from complex_entity
select (cast(id as double precision) / 2)
 from complex_entity

-- SQL Server
select isnull(somestring, '')
 from complex_entity
select (cast(id as float) / 2)
 from complex_entity

-- PostgreSQL
select coalesce(somestring, '')
 from complex_entity
select (cast(id as double precision) / 2)
 from complex_entity
```

Numeric cast targets come from [`MakeTypeName`](xref:NextORM.Core.ISqlDialect.MakeTypeName(System.Type)):

| CLR type | SQLite / PostgreSQL | SQL Server |
|---|---|---|
| `byte` | `smallint` | `tinyint` |
| `short` | `smallint` | `smallint` |
| `int` | `integer` | `int` |
| `long` | `bigint` | `bigint` |
| `float` | `real` | `real` |
| `double` | `double precision` | `float` |
| `decimal` | `numeric` | `decimal(38, 10)` |

## Conditional helpers

`SqlFunctions.Sql.nullif` is ANSI and works on every SQL provider; `greatest`/`least` are gated by
[`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast) (PostgreSQL, MySQL/MariaDB, ClickHouse, SQL Server 2022+ and SQLite opt in; SQLite renders `max`/`min`). NULL handling is provider-specific: PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments and return NULL only when every argument is NULL, while MySQL/MariaDB and SQLite return NULL when any argument is NULL:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        NoZero = SqlFunctions.Sql.nullif(e.Int, 0),
        Hi = SqlFunctions.Sql.greatest(e.Id, 10L),
        Lo = SqlFunctions.Sql.least(e.Id, 10L)
    })
    .ToList();
```

```sql
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo"
 from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Sql.nullif(a, b)` | `nullif(a, b)` |
| `SqlFunctions.Sql.greatest(a, b, ...)` | `greatest(a, b, ...)` |
| `SqlFunctions.Sql.least(a, b, ...)` | `least(a, b, ...)` |
| `SqlFunctions.Postgres.num_nulls(a, b, ...)` | `num_nulls(a, b, ...)` |
| `SqlFunctions.Postgres.num_nonnulls(a, b, ...)` | `num_nonnulls(a, b, ...)` |
| `SqlFunctions.Sql.iif(condition, a, b)` | `iif(...)` (SQL Server, SQLite 3.32+), `if(...)` (MySQL/MariaDB, ClickHouse), `case when ... then ... else ... end` (PostgreSQL) |
| `SqlFunctions.SqlServer.choose(index, a, b, ...)` | `choose(index, a, b, ...)` (SQL Server) |
| `SqlFunctions.ClickHouse.multi_if(when(c1, v1), ..., otherwise(v))` | `multiIf(c1, v1, ..., v)` (ClickHouse) |

`num_nulls`/`num_nonnulls` are part of the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions)).
`iif` is portable ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif)) and each dialect supplies its native
spelling through [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String)); `choose` remains SQL Server-only
([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)). Calling `iif` through the specialized
`SqlFunctions.SqlServer` surface still works by inheritance. The C# ternary `condition ? a : b` is separate
and always renders the portable `case when ... end`.

ClickHouse additionally has the multi-branch `multiIf` surface
([`MultiIf`](xref:NextORM.Core.ISqlDialect.MultiIf),
[`IMultiIfRenderer.Render`](xref:NextORM.Core.IMultiIfRenderer.Render(System.Collections.Generic.IReadOnlyList{System.String},System.Type))): build each branch with `when(condition, value)`
and close it with `otherwise(value)`, which must be last. Other providers just use `case when`, which is
already the portable form behind `iif`/the C# conditional, so they reject the ClickHouse-native spelling.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        Bucket = SqlFunctions.ClickHouse.multi_if(
            SqlFunctions.ClickHouse.when(e.Id == 1L, "one"),
            SqlFunctions.ClickHouse.when(e.Id == 2L, "two"),
            SqlFunctions.ClickHouse.otherwise("many"))
    })
    .ToList();
```

```sql
-- ClickHouse
select id, multiIf((id = 1), 'one', (id = 2), 'two', 'many') as `Bucket`
 from complex_entity
```

## Null-safe comparison (`IS [NOT] DISTINCT FROM`)

`SqlFunctions.Sql.is_not_distinct_from` and `SqlFunctions.Sql.is_distinct_from` are the explicit,
opt-in null-safe comparison. Both take one common operand type `T` and return a non-nullable `bool`:

```csharp
bool is_not_distinct_from<T>(T left, T right);
bool is_distinct_from<T>(T left, T right);
```

The result is always two-valued — it is never SQL `NULL`/UNKNOWN, so it cannot drop a row the way an
ordinary comparison does:

| `left` | `right` | `is_not_distinct_from` | `is_distinct_from` |
|---|---|---|---|
| `1` | `1` | `true` | `false` |
| `1` | `2` | `false` | `true` |
| `NULL` | `NULL` | `true` | `false` |
| `NULL` | `1` | `false` | `true` |
| `1` | `NULL` | `false` | `true` |

`is_distinct_from` is the complement of `is_not_distinct_from`. Ordinary `==` / `!=` are **unchanged**:
they keep SQL three-valued logic and are never rewritten to this helper (a `NULL` comparison yields no
row in `WHERE`). Use the helpers when "both are `NULL`" must count as equal.

```csharp
int? previous = 42;

var rows = await dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.Sql.is_not_distinct_from(e.Int, previous))
    .Select(e => new { e.Id })
    .ToListAsync();
```

| Provider | `is_not_distinct_from(a, b)` | `is_distinct_from(a, b)` | Disposition |
|---|---|---|---|
| PostgreSQL | `(a is not distinct from b)` | `(a is distinct from b)` | native |
| SQLite | `(a is b)` | `(a is not b)` | native |
| MySQL, MariaDB | `(a <=> b)` | `(not (a <=> b))` | native |
| SQL Server | `((a is not null and b is not null and a = b) or (a is null and b is null))` | `(not ((a is not null and b is not null and a = b) or (a is null and b is null)))` | emulated |
| ClickHouse | `((a is null and b is null) or (a is not null and b is not null and a = b))` | `(not ((a is null and b is null) or (a is not null and b is not null and a = b)))` | emulated |

A boolean projection is shaped by the dialect: SQL Server legalises the predicate as
`cast(case when ... then 1 else 0 end as bit)`, while the other providers project it directly (on
ClickHouse the guarded predicate is already a `UInt8` value).

Folding, typing and parameters behave deterministically:

* two literal `NULL` operands fold to `1 = 1` (`is_not_distinct_from`) or `1 = 0` (`is_distinct_from`);
* exactly one literal `NULL` lowers to `is null` / `is not null` on the other operand;
* a **captured** operand stays one command parameter, and its SQL type comes from the compared column's
  mapping (or its converter), never from the runtime value — a captured `NULL` works and never
  specializes the cached SQL;
* two statically non-nullable CLR **value** operands that read no query source (two captured `int`
  locals, for example) may lower to `=` / `<>`; a nullable value, a column, a navigation or an
  outer-join null-extended operand always keeps the null-safe rendering. A nullable reference
  annotation is **not** runtime proof: a reference declared non-nullable that actually holds `NULL`
  still takes the null-safe path.

The single operand type `T` is inferred when both arguments already share it. A bare `null` literal has
no type of its own, so name it explicitly — `is_not_distinct_from<int?>(null, null)` — or cast it
(`(int?)null`). Operands whose effective CLR types differ (after nullable unwrapping) are rejected with
`NotSupportedException` and `Null-safe comparison requires compatible operand types.` instead of being
silently converted. On SQL Server the SQL type of a null operand must be resolvable from metadata (the
bound column/converter mapping, then the operand's CLR type); an unmapped operand fails before execution
with `Null-safe comparison requires a SQL Server type mapping for null operands.`

The in-memory provider applies the same truth table — both `NULL` equal, exactly one `NULL` unequal,
otherwise `EqualityComparer<T>.Default` — so a `NULL` reference is never dereferenced. CLR string
equality is **not** database collation: the in-memory provider compares CLR strings, while a SQL
provider compares with the column's collation, so no cross-collation equality is promised.

## SQL Server type predicates

`SqlFunctions.SqlServer.isdate(value)` and `isnumeric(value)` render `isdate(...)` / `isnumeric(...)`
and are gated per name by [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (SQL Server
only):

| C# | SQL |
|---|---|
| `isdate(value)` / `isnumeric(value)` | `isdate(...)` / `isnumeric(...)` |

They deliberately return the native T-SQL `int` (1/0), **not** a `bit`, so they are projected as
ordinary integer values and are not materialised into a
`cast(case when ... then 1 else 0 end as bit)` predicate.
