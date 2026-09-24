# Duration (`TimeSpan`) columns

> A `TimeSpan` property is a **duration**. nextorm stores it natively where the database has a duration or time-of-day type, and in an integer column otherwise. The storage unit of the integer form is declared with [`DurationAttribute`](xref:NextORM.Core.DurationAttribute) or the fluent [`Duration(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.Duration(NextORM.Core.DurationUnit,System.Int32)) mapping; the default is ticks.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Data modification (INSERT)](19-insert-statement.md) · [Provider overview](../providers/overview.md) · [Limitations](../advanced/limitations.md)

## Where the value is stored

| Provider | Storage of a `TimeSpan` property |
|---|---|
| PostgreSQL | native `interval` |
| MySQL / MariaDB | native `TIME` |
| SQL Server | integer column (`bigint`), ticks by default — no native duration type |
| SQLite | integer column (`bigint`), ticks by default |
| ClickHouse | integer column (`bigint`), ticks by default |
| In-memory | the CLR `TimeSpan` value |

On the integer providers the value is written as `DurationUnit` units and read back with the same unit, so `[Duration(DurationUnit.Seconds)]` over a `bigint` column stores whole seconds. Reading, writing and comparisons all apply the same conversion, so a `TimeSpan` property round-trips without manual conversion.

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

Writes go through the same conversion, including bulk insert.

## Details and limitations

* PostgreSQL `interval` also stores months, which a `TimeSpan` cannot express; a value with whole months is not round-tripped faithfully. Use a unit-based integer column if months matter.
* SQL Server has a `time` type, but it is a time-of-day (less than 24 hours); nextorm therefore uses an integer column so durations longer than a day are representable.
* A non-tick unit truncates the sub-unit remainder, exactly like a database column of that unit.
* Only a directly projected duration **property** carries its declared unit; a computed duration expression (`Select(x => x.Estimate + something)`) is read with the default ticks unit. Project the property itself when a non-tick unit is in use.
* `DateTimeOffset` is read directly by the provider driver; a date-part convention (`value.Date` vs `value.UtcDateTime.Date`) is not applied.

The dialect surface used by schema-generation tooling is [`ISqlDialect.MakeDurationType`](xref:NextORM.Core.ISqlDialect.MakeDurationType(NextORM.Core.DurationUnit,System.Int32)) for a non-nullable column and [`ISqlDialect.MakeNullableDurationType`](xref:NextORM.Core.ISqlDialect.MakeNullableDurationType(NextORM.Core.DurationUnit,System.Int32)) for a nullable one, with [`SupportsNativeDuration`](xref:NextORM.Core.ISqlDialect.SupportsNativeDuration). Both return the same type on every provider except ClickHouse, whose non-nullable `Int64` becomes `Nullable(Int64)` for the nullable case.
