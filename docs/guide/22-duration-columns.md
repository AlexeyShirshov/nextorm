# Duration (`TimeSpan`) columns

> A `TimeSpan` property is a **duration**. nextorm stores it natively where the database has a duration or time-of-day type, and in an integer column otherwise. The storage unit of the integer form is declared with [`DurationAttribute`](xref:NextORM.Core.DurationAttribute) or the fluent [`Duration(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.Duration(NextORM.Core.DurationUnit,System.Int32)) mapping; the default is ticks.

**Prerequisites:** [Querying and projections](../querying/index.md) · [Data modification (INSERT)](15-insert-statement.md) · [Provider overview](../providers/overview.md) · [Limitations](../advanced/limitations.md)

## Where the value is stored

| Provider | Storage of a `TimeSpan` property |
|---|---|
| PostgreSQL | native `interval` |
| MySQL / MariaDB | native `TIME` |
| SQL Server | integer column (`bigint`), ticks by default — no native duration type |
| SQLite | integer column (`bigint`), ticks by default |
| ClickHouse | integer column (`Int64`), ticks by default |
| In-memory | the CLR `TimeSpan` value |

On the integer providers the value is written as `DurationUnit` units and read back with the same unit, so `[Duration(DurationUnit.Seconds)]` over a `bigint` column stores whole seconds. Reading, writing and comparisons all apply the same conversion, so a `TimeSpan` property round-trips without manual conversion.

A schema generator emits these column types for the declarations below:

```sql
-- PostgreSQL: native duration
create table tasks (id bigint, estimate interval, paused interval(3));

-- MySQL / MariaDB: native time-of-day
create table tasks (id bigint, estimate time, paused time(3));

-- SQL Server / SQLite: integer column (`bigint`), the type is independent of the declared unit
create table tasks (id bigint, estimate bigint, paused bigint);

-- ClickHouse: a nullable TimeSpan becomes Nullable(Int64)
create table tasks (id Int64, estimate Int64, paused Nullable(Int64));
```

## Declaring the unit

Use the attribute on the property:

```csharp
public class Task
{
    public long Id { get; set; }

    [Duration(DurationUnit.Seconds)]
    public TimeSpan Estimate { get; set; }

    // No attribute: ticks on the integer providers, the native type on PostgreSQL/MySQL/MariaDB.
    public TimeSpan Elapsed { get; set; }

    [Duration(DurationUnit.Milliseconds, Precision = 3)]
    public TimeSpan? Paused { get; set; }
}
```

or the fluent mapping:

```csharp
ctx.From<Task>(b => b
    .Table("tasks")
    .Property(x => x.Estimate).Duration(DurationUnit.Seconds).HasColumnName("estimate"));
```

`DurationUnit` is one of `Ticks`, `Microseconds`, `Milliseconds`, `Seconds`, `Minutes`, `Hours`, `Days`. `Precision` is the fractional-second precision of a **native** type (for example `TIME(3)` or `interval(3)`) and is ignored for the integer form. On PostgreSQL and MySQL/MariaDB the unit is ignored because the value is stored natively.

## Queries and comparisons

A duration column is projected and filtered like any other column. A `TimeSpan` constant in a comparison is converted to the column's storage form, on either side of the operator:

```csharp
// Reads the stored integer in the declared unit on SQL Server/SQLite/ClickHouse,
// and the native interval/TIME on PostgreSQL/MySQL/MariaDB.
var overdue = ctx.From<Task>()
    .Where(x => x.Estimate > TimeSpan.FromMinutes(5))
    .Select(x => new { x.Id, x.Estimate })
    .ToList();
```

The generated SQL has the same shape on every provider; only the placeholder and the bound parameter differ:

```sql
-- PostgreSQL / MySQL / MariaDB: @p0 carries a native duration
select id, estimate from tasks
 where (estimate > @p0);   -- @p0 = interval '00:05:00' / time '00:05:00'

-- SQL Server / SQLite / ClickHouse: @p0 carries the integer storage value
select id, estimate from tasks
 where (estimate > @p0);   -- @p0 = 300 (seconds, from [Duration(DurationUnit.Seconds)])
```

On SQLite the placeholder is `$p0` instead of `@p0`.

Writes go through the same conversion, including bulk insert.

## Details and limitations

* PostgreSQL `interval` also stores months, which a `TimeSpan` cannot express; a value with whole months is not round-tripped faithfully. Use a unit-based integer column if months matter.
* SQL Server has a `time` type, but it is a time-of-day (less than 24 hours); nextorm therefore uses an integer column so durations longer than a day are representable.
* A non-tick unit truncates the sub-unit remainder, exactly like a database column of that unit.
* Only a directly projected duration **property** carries its declared unit; a computed duration expression (`Select(x => x.Estimate + something)`) is read with the default ticks unit. Project the property itself when a non-tick unit is in use.
* `DateTimeOffset` is read directly by the provider driver; a date-part convention (`value.Date` vs `value.UtcDateTime.Date`) is not applied.
* MySQL/MariaDB also has a limited native `TIME` range, `−838:59:59`…`838:59:59` (about ±34.9 days); a longer duration cannot be stored in a `TimeSpan` property there (nextorm always uses native `TIME`), so keep it in a separate integer-typed property (for example `long`).

The dialect surface used by schema-generation tooling is [`ISqlDialect.MakeDurationType`](xref:NextORM.Core.ISqlDialect.MakeDurationType(System.Nullable{NextORM.Core.DurationUnit},System.Int32)) for a non-nullable column and [`ISqlDialect.MakeNullableDurationType`](xref:NextORM.Core.ISqlDialect.MakeNullableDurationType(System.Nullable{NextORM.Core.DurationUnit},System.Int32)) for a nullable one, with [`SupportsNativeDuration`](xref:NextORM.Core.ISqlDialect.SupportsNativeDuration). Both return the same type on every provider except ClickHouse, whose non-nullable `Int64` becomes `Nullable(Int64)` for the nullable case.
