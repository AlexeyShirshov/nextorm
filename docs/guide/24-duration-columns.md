# Duration (`TimeSpan`) columns

> A `TimeSpan` property represents a **duration**. nextorm uses a native duration or time-of-day type when the provider has one; otherwise, it stores the value in an integer column. For integer storage, declare the unit with [`DurationAttribute`](xref:NextORM.Core.DurationAttribute) or the fluent [`Duration(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.Duration(NextORM.Core.DurationUnit,System.Int32)) mapping. If you omit the unit, nextorm uses ticks.

**Prerequisites:** [Querying and projections](../engine/index.md) · [Data modification (INSERT)](17-insert-statement.md) · [Provider overview](../connecting-providers/overview.md) · [Limitations](../advanced/limitations.md)

## Where the value is stored

| Provider | Storage of a `TimeSpan` property |
|---|---|
| PostgreSQL | native `interval` |
| MySQL / MariaDB | native `TIME` |
| SQL Server | integer column (`bigint`), ticks by default — no native duration type |
| SQLite | integer column (`bigint`), ticks by default |
| ClickHouse | integer column (`Int64`), ticks by default |
| In-memory | the CLR `TimeSpan` value |

On integer-backed providers, nextorm writes and reads the value in the same `DurationUnit`. For example, `[Duration(DurationUnit.Seconds)]` stores whole seconds in a `bigint` column. Reads, writes, and comparisons use the same conversion, so the `TimeSpan` property round-trips without manual conversion.

For the property declarations shown below, schema generation emits these provider-specific column types:

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

The two mapping options control different parts of storage:

| Option | Applies to | Effect |
|---|---|---|
| `DurationUnit` | Integer-backed providers | Choose `Ticks`, `Microseconds`, `Milliseconds`, `Seconds`, `Minutes`, `Hours`, or `Days`. PostgreSQL and MySQL/MariaDB store the value natively and ignore this unit. |
| `Precision` | Native types | Sets fractional-second precision, such as `TIME(3)` or `interval(3)`. Integer storage ignores it. |

## Queries and comparisons

Project or filter a mapped duration property like any other column. In a comparison, nextorm converts a `TimeSpan` constant to the column's storage form, regardless of which side of the operator contains the constant:

```csharp
// Reads the stored integer in the declared unit on SQL Server/SQLite/ClickHouse,
// and the native interval/TIME on PostgreSQL/MySQL/MariaDB.
var overdue = ctx.From<Task>()
    .Where(x => x.Estimate > TimeSpan.FromMinutes(5))
    .Select(x => new { x.Id, x.Estimate })
    .ToList();
```

The SQL shape stays the same across providers; the provider determines the placeholder and the value bound to it:

```sql
-- PostgreSQL / MySQL / MariaDB: @p0 carries a native duration
select id, estimate
 from tasks
 where (estimate > @p0);   -- @p0 = interval '00:05:00' / time '00:05:00'

-- SQL Server / SQLite / ClickHouse: @p0 carries the integer storage value
select id, estimate
 from tasks
 where (estimate > @p0);   -- @p0 = 300 (seconds, from [Duration(DurationUnit.Seconds)])
```

On SQLite the placeholder is `$p0` instead of `@p0`.

Writes use the same storage conversion as reads and comparisons, including bulk insert.

## Details and limitations

* PostgreSQL `interval` also stores months, which a `TimeSpan` cannot represent losslessly. If a mapped interval returns a non-zero month component of either sign, reading it into a `TimeSpan` throws `InvalidCastException` rather than approximating. A zero-month value (days plus microseconds) converts exactly when it fits in a `TimeSpan`; an unrepresentable value throws `OverflowException` rather than wrapping. Use a unit-based integer column if months matter.
* SQL Server has a `time` type, but it is a time-of-day (less than 24 hours); nextorm therefore uses an integer column so durations longer than a day are representable.
* A non-tick unit truncates the sub-unit remainder, exactly like a database column of that unit.
* Only a directly projected duration **property** carries its declared unit; a computed duration expression (`Select(x => x.Estimate + something)`) is read with the default ticks unit. Project the property itself when a non-tick unit is in use.
* `DateTimeOffset` is read directly by the provider driver; a date-part convention (`value.Date` vs `value.UtcDateTime.Date`) is not applied.
* MySQL/MariaDB also has a limited native `TIME` range: `−838:59:59`…`838:59:59` (about ±34.9 days). Because nextorm always uses native `TIME` there, a `TimeSpan` property cannot store a longer duration; use a separate integer-typed property such as `long`.

The dialect surface used by schema-generation tooling is [`ISqlDialect.MakeDurationType`](xref:NextORM.Core.ISqlDialect.MakeDurationType(System.Nullable{NextORM.Core.DurationUnit},System.Int32)) for a non-nullable column and [`ISqlDialect.MakeNullableDurationType`](xref:NextORM.Core.ISqlDialect.MakeNullableDurationType(System.Nullable{NextORM.Core.DurationUnit},System.Int32)) for a nullable one, with [`SupportsNativeDuration`](xref:NextORM.Core.ISqlDialect.SupportsNativeDuration). Both return the same type on every provider except ClickHouse, whose non-nullable `Int64` becomes `Nullable(Int64)` for the nullable case.
