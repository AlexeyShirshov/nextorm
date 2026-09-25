# Range columns (a range stored as a pair of scalar columns)

> A `Range<T>` property can be stored as a **pair of scalar columns** on a provider without a native range type. The two bounds go into two ordinary columns, SQL `NULL` means an unbounded side, and the range predicates and inspection functions are translated over the pair.

**Prerequisites:** [Filtering (WHERE)](02-filtering-where.md) · [Value converters and JSON columns](30-value-converters.md) · [PostgreSQL-specific SQL](provider-specific/postgresql.md) · [Limitations](../advanced/limitations.md)

PostgreSQL has [native range types](provider-specific/postgresql.md#range-types) (`int4range`…`daterange`), so a `Range<T>` property maps directly to a range column there. The other providers have no range type; nextorm bridges the gap by letting you map the two bounds to two scalar columns and translating the range operators over them.

## Storage model

| Provider | How a `Range<T>` property is stored |
|---|---|
| PostgreSQL | a native range column by default; a mapped scalar pair is also accepted |
| SQL Server | a pair of scalar columns ([`RangeColumnsAttribute`](xref:NextORM.Core.RangeColumnsAttribute)) |
| MySQL / MariaDB | a pair of scalar columns |
| SQLite | a pair of scalar columns |
| ClickHouse | a pair of scalar columns |
| In-memory | the CLR `Range<T>` value |

The pair is **two nullable columns**: the lower bound and the upper bound. A `NULL` column means that side is unbounded (`LowerInfinite` / `UpperInfinite`). The bound **inclusivity is part of the mapping**, not of the stored value — it is declared once with the attribute and reapplied on every read, because a plain scalar column cannot carry it.

The surface is gated by [`SupportsRangeColumns`](xref:NextORM.Core.ISqlDialect.SupportsRangeColumns), which is enabled on PostgreSQL, SQL Server, MySQL/MariaDB, SQLite and ClickHouse.

## Declaring the pair

Use the attribute on the property ([`RangeColumnsAttribute`](xref:NextORM.Core.RangeColumnsAttribute)):

```csharp
[SqlTable("reservation")]
public class Reservation
{
    public int Id { get; set; }

    // Two integer columns; the range is [lower, upper) by default.
    [RangeColumns("during_lower", "during_upper")]
    public Range<int> During { get; set; }
}
```

The default inclusivity is `[)` (lower inclusive, upper exclusive) — the PostgreSQL default for a discrete range. Pass it explicitly, or use the fluent [`EntityPropertyBuilder<T>.RangeColumns(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.RangeColumns(System.String,System.String,System.Boolean,System.Boolean)):

```csharp
ctx.From<Reservation>(b => b
    .Table("reservation")
    .Property(x => x.During).RangeColumns("during_lower", "during_upper", lowerInclusive: false, upperInclusive: true));
```

Both columns must be nullable so an unbounded side can be stored as `NULL`.

## Reading and writing

A pair property reads and writes like any other property. Writing a `Range<int>(1, 10)` stores `1` and `10` in the two columns; reading rebuilds `new Range<int>(lower, upper, …, lowerIsNull, upperIsNull, …)` with the mapping's inclusivity:

```csharp
ctx.InsertInto<Reservation>()
    .Values(new Reservation { Id = 1, During = new Range<int>(1, 10) })
    .Insert();

var r = ctx.From<Reservation>().Where(x => x.Id == 1).Single();
// r.During.Lower == 1, r.During.Upper == 10,
// r.During.LowerInclusive == true, r.During.UpperInclusive == false (from the mapping)
```

Both bounds reach the declared columns, and a read selects both back (SQLite output; other providers quote identifiers differently and use their own parameter markers):

```sql
-- Insert
insert into reservation (id, during_lower, during_upper) values ($p0, $p1, $p2);

-- Read
select id, during_lower, during_upper from reservation where id = 1;
```

An unbounded side is written as a SQL `NULL`: `new Range<int>(…, lowerInfinite: true)` writes `during_lower = NULL`.

Writes go through the entity-value forms (`Values(entity)` / `Set(entity)`); see the limitations below for the selector forms.

## Filtering and inspection

The PostgreSQL range operators are translated into comparisons between the two bounds. A constant `Range<T>` argument is folded into parameters, and an unbounded bound short-circuits the corresponding side.

```csharp
var overlapping = ctx.From<Reservation>()
    .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(5, 15)))
    .Select(x => x.Id)
    .ToList();
```

The constant range is folded into parameters (SQLite renders the query above as):

```sql
select id from reservation
 where (((during_lower is null) or during_lower < $p1)
    and ((during_upper is null) or $p0 < during_upper));
-- $p0 = 5, $p1 = 15
```

| Function | Translated over the pair |
|---|---|
| `overlaps(a, b)` | the two ranges share a point |
| `range_contains(range, value)` / `range_contains(outer, inner)` | the range contains a value / another range |
| `range_contained_by(inner, outer)` | the range is contained by another |
| `range_adjacent(a, b)` | the ranges touch but do not overlap |
| `range_strictly_left_of(a, b)` / `range_strictly_right_of(a, b)` | ordered with no touching |
| `range_not_extend_right_of(a, b)` / `range_not_extend_left_of(a, b)` | `a` does not extend past `b` on that side |
| `lower(range)` / `upper(range)` | the bound column itself |
| `lower_inc` / `upper_inc` | the mapping's inclusivity, `false` when the bound is unbounded |
| `lower_inf` / `upper_inf` | the bound column `is null` |
| `isempty(range)` | always `false` (a pair is never empty) |

The semantics match PostgreSQL and the in-memory provider, including the boundary cases (equal values with differing inclusivity).

### Generated predicates

For a default `[)` mapping the pair columns are `a = [al, au)` and a constant `b = [bl, bu)`; an unbounded bound makes the matching term true. SQLite renders:

| Call | Predicate |
|---|---|
| `overlaps(a, b)` | `(al is null or al < bu) and (au is null or bl < au)` |
| `range_contains(a, v)` | `(al is null or al <= v) and (au is null or v < au)` |
| `range_contains(a, b)` | `(al is null or al <= bl) and (au is null or au >= bu)` |
| `range_contained_by(a, b)` | `(al is not null and bl <= al) and (au is not null and au <= bu)` |
| `range_adjacent(a, b)` | `(au is not null and au = bl) or (al is not null and bu = al)` |
| `range_strictly_left_of(a, b)` | `au is not null and au <= bl` |
| `range_strictly_right_of(a, b)` | `al is not null and al >= bu` |
| `range_not_extend_right_of(a, b)` | `au is not null and au <= bu` |
| `range_not_extend_left_of(a, b)` | `al is not null and al >= bl` |
| `lower(a)` / `upper(a)` | the `al` / `au` column |
| `lower_inf(a)` / `upper_inf(a)` | `al is null` / `au is null` |
| `lower_inc(a)` / `upper_inc(a)` | the mapping's inclusivity, `false` when that bound is null |
| `isempty(a)` | `false` |

The `<=`/`>=` terms carry the boundary equality; with another inclusivity the equality part is emitted only when the relevant bound is inclusive. For example the closed mapping `(]` renders `lower_inc` as `1=0` and `upper_inc` as `not (au is null)`.

## Limitations

* **An empty range cannot be represented.** A pair of `NULL` bounds is the *unbounded* range, so `Range<T>.Empty` is rejected on write and during translation with a `NotSupportedException`.
* **Range-returning operators are rejected.** `range_union`, `range_intersection`, `range_difference`, the range constructors and the `range_agg`/`range_intersect_agg` aggregates have no scalar form over a pair; they raise a `NotSupportedException`. Use PostgreSQL when you need them.
* **Multiranges are not a pair.** A `Range<T>[]` maps only to a native PostgreSQL multirange column.
* **Selector-based writes and returning are rejected.** `InsertInto<T>().Value(x => x.During, …)`, a mapping-based `Values(source, mapping)` and `Returning(x => x.During)` address a single column and raise a `NotSupportedException`; use the entity-value forms instead.
* A `[RangeColumns]` property cannot be combined with `[ValueConverter]` or `[JsonColumn]`.

## See also

* [PostgreSQL-specific SQL — Range types](provider-specific/postgresql.md#range-types) and [Multiranges](provider-specific/postgresql.md#multiranges) for the native surface.
* [Limitations](../advanced/limitations.md) for the provider capability matrix.
* [API reference — mapping attributes](../advanced/api-reference.md).
