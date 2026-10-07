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
select ifnull(somestring, '') from complex_entity
select (cast(id as double precision) / 2) from complex_entity

-- SQL Server
select isnull(somestring, '') from complex_entity
select (cast(id as float) / 2) from complex_entity

-- PostgreSQL
select coalesce(somestring, '') from complex_entity
select (cast(id as double precision) / 2) from complex_entity
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
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo" from complex_entity
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
select id, multiIf((id = 1), 'one', (id = 2), 'two', 'many') as `Bucket` from complex_entity
```

## SQL Server metadata, checksum and other scalars

The remaining T-SQL-only scalars live on `SqlFunctions.SqlServer` and are gated per name by
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (SQL Server is the only provider that
implements the flag, so every other provider throws `NotSupportedException`). `isdate`/`isnumeric`
deliberately return the native T-SQL `int` (1/0), **not** a `bit`, so they are projected as ordinary
integer values and are not materialised into a `cast(case when ... then 1 else 0 end as bit)`
predicate.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Rand = SqlFunctions.SqlServer.rand(),
        Seeded = SqlFunctions.SqlServer.rand(42),
        Replaced = SqlFunctions.SqlServer.stuff(e.String, 2, 3, "xy"),
        Check = SqlFunctions.SqlServer.checksum(e.Id, e.String),
        Zipped = SqlFunctions.SqlServer.compress(e.String),
        IsDate = SqlFunctions.SqlServer.isdate(e.String),
        Number = SqlFunctions.SqlServer.str(1.5, 10, 2)
    })
    .ToList();
```

```sql
select rand() as [Rand], rand(42) as [Seeded], stuff(somestring, 2, 3, 'xy') as [Replaced],
       checksum(id, somestring) as [Check], compress(somestring) as [Zipped],
       isdate(somestring) as [IsDate], str(1.5, 10, 2) as [Number]
from complex_entity
```

| C# | SQL |
|---|---|
| `rand()` / `rand(seed)` | `rand()` / `rand(seed)` |
| `stuff(value, start, length, newValue)` (string) | `stuff(...)` |
| `checksum(values...)` / `binary_checksum(values...)` | `checksum(...)` / `binary_checksum(...)` |
| `compress(value)` (string and `byte[]`) / `decompress(value)` | `compress(...)` / `decompress(...)` |
| `isdate(value)` / `isnumeric(value)` | `isdate(...)` / `isnumeric(...)` |
| `str(value)` / `str(value, length)` / `str(value, length, decimalPlaces)` | `str(...)` |
| `formatmessage(message, args...)` / `formatmessage(messageId, args...)` | `formatmessage(...)` |

* `rand` without a seed is **non-deterministic** and evaluated by the server; a fixed integer seed makes
  the sequence reproducible. `checksum`/`binary_checksum` are non-cryptographic change-detection
  checksums, not a substitute for `hashbytes`; they require at least one argument (`checksum()`/
  `binary_checksum()` throw), and the `checksum(*)` wildcard form is not exposed.
* `compress`/`decompress` use GZIP and produce/consume `varbinary(max)`; `decompress` returns `null` for
  an invalid or truncated value.
* `str` fixes the total length (default 10) and decimal places (default 0); `formatmessage` accepts a
  format string or a `sys.messages` id and at most 20 formatting arguments (more throws
  `NotSupportedException`).

### Metadata functions

The metadata surface is a curated subset of the T-SQL metadata catalog, grouped A-D. Each function
takes values (column names, ids, expressions) and returns a value; a missing
object/column/index/statistic yields `null` exactly as the native function does, and the metadata is
resolved by the server when the query runs. Optional second arguments (database id, object type) are
exposed as overloads.

| C# | SQL | Group |
|---|---|---|
| `col_length(table, column)` | `col_length(...)` | A |
| `col_name(tableId, columnId)` | `col_name(...)` | A |
| `ident_incr(table)` / `ident_seed(table)` | `ident_incr(...)` / `ident_seed(...)` | A |
| `index_col(table, indexId, keyId)` | `index_col(...)` | A |
| `object_definition(objectId)` | `object_definition(...)` | A |
| `object_id(name)` / `object_id(name, type)` | `object_id(...)` | A |
| `object_name(id)` / `object_name(id, databaseId)` | `object_name(...)` | A |
| `object_schema_name(id)` / `object_schema_name(id, databaseId)` | `object_schema_name(...)` | A |
| `stats_date(tableId, statsId)` | `stats_date(...)` | A |
| `db_id()` / `db_id(database)` | `db_id(...)` | B |
| `db_name()` / `db_name(databaseId)` | `db_name(...)` | B |
| `original_db_name()` | `original_db_name()` | B |
| `schema_id()` / `schema_id(schema)` | `schema_id(...)` | B |
| `schema_name()` / `schema_name(schemaId)` | `schema_name(...)` | B |
| `type_id(typeName)` / `type_name(typeId)` | `type_id(...)` / `type_name(...)` | B |
| `filegroup_id(name)` / `filegroup_name(id)` | `filegroup_id(...)` / `filegroup_name(...)` | C |
| `file_id(name)` / `file_idex(name)` / `file_name(id)` | `file_id(...)` / `file_idex(...)` / `file_name(...)` | C |
| `current_timezone()` / `current_timezone_id()` | `current_timezone()` / `current_timezone_id()` | D |
| `getansinull()` / `getansinull(database)` | `getansinull(...)` | D |
| `parsename(objectName, piece)` | `parsename(...)` | D |
| `publishingservername()` | `publishingservername()` | D |

`object_definition` returns the T-SQL source only when the caller has the permission (otherwise
`null`); `file_idex` differs from `file_id` in that it is not bounded to the current database.

### Not exposed

Ten connection-, session- or statement-scope names from the same T-SQL catalog are intentionally
**not** modelled: they report the state of the connection or statement rather than compute a per-row
value, so a query projection would be misleading. They remain reachable through raw SQL or a
`[SqlFunction]` wrapper, and each has a review trigger if a concrete per-row use appears.

| Function | Scope |
|---|---|
| `CURRENT_REQUEST_ID` | connection / request |
| `CURRENT_TRANSACTION_ID`, `XACT_STATE` | transaction |
| `APP_NAME`, `HOST_ID`, `HOST_NAME` | session |
| `IDENT_CURRENT` | session / table |
| `MIN_ACTIVE_ROWVERSION` | database / transaction |
| `ROWCOUNT_BIG` | statement |
| `SCOPE_IDENTITY` | session / scope |
