# NativeAOT

> Native applications on .NET 11 for `linux-x64`: `<PublishAot>true</PublishAot>` is the only
> setting. No compiled model, no separate query generation, no hand-written code.

**Read first:** [Connections](01-connections.md) · [Value converters and JSON columns](03-value-converters.md).

## Enabling it

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

```bash
dotnet publish -c Release
```

The standard NativeAOT prerequisites apply: `clang`, `zlib1g-dev` and a native toolchain for the RID.

Entity mappings are compiled as well: the package ships a source generator that describes every
`[SqlTable]`/`[Table]` entity declared in your assembly — table, columns, keys, precision, collation,
durations, range pairs, query filters, value/JSON converters and relationship navigations, across the
type's base classes — so building a mapping reads that description instead of reflecting over the
entity's members. A type outside that coverage keeps the
reflective path, so nothing has to be registered or opted in.

## What is supported

Providers: **SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse**.

Query surface: typed and anonymous projections, whole-entity materialization, filters, ordering, paging, aggregates,
`GROUP BY`/`HAVING`, joins, CTEs, window functions (where the provider supports them), SQLite JSON
functions, ClickHouse arrays, `SelectGroup`/`SelectGroupHaving`, `SelectWhereMin`/`SelectWhereMax`,
prepared-command reuse, correlated subqueries, value converters, JSON columns (see below), eager
loading of a declared `[Relationship]` collection through the split loader (`LoadWith`), the
insert/update/delete builders, bulk insert, raw-SQL row mapping (`ExecuteRaw`, scalar and mapped-entity
results) and the streaming terminals — `WriteCsv`, `WriteJson` and the asynchronous row sequence
(`ToAsyncEnumerable`).

Every supported scenario runs natively and under JIT and is compared row by row: row counts and
values must match. That comparison runs automatically on every change, so "works natively" is a
standing build condition, not a one-off check. Each of the six providers is verified with its own
published native binary against a live server: the projection, filtering, grouping, aggregate,
`HAVING`/`SelectGroupHaving`, join, CTE, window, prepared-command, converter, eager-loading, write and
streaming shapes are all exercised that way, so no provider relies on another provider's run.

## JSON columns

The mapping side of a JSON column is native-ready: the converter is created by generated code, so
there is no run-time generic instantiation. The JSON document itself is `System.Text.Json`'s, and a
trimmed application has to give it the contract metadata it cannot build by reflection. Declare a
source-generated context that lists every stored model type — the compile-time entity description finds
that context and serializes through its options, so nothing is set up at run time:

```csharp
[JsonSerializable(typeof(Address))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

A column that declares its own serializer options
(`b.Property(x => x.Address).JsonColumn(o => o.Options = ...)`) keeps them; the context supplies the
contract metadata, not the settings. On the regular JIT runtime no context is required: a model no
context declares is serialized with the default options.

## Limitations

- The EF Core bridge (`nextorm.entityframeworkcore`) is not part of the native build yet — keep that
  scenario on the regular JIT runtime.
- Single-query eager loading — `JoinInto`, or `LoadWith(..., EagerLoadMode.SingleQuery)` — closes its
  row type over the parent and child types at run time, which a native image cannot materialize. A
  many-to-many navigation cannot be loaded in one query for the same reason: its link row type is
  closed over the junction key types, and a composite junction also emits a carrier with
  `Reflection.Emit`. Both shapes are rejected before any SQL runs; the split loader (`LoadWith` default)
  covers the same navigations, many-to-many included.
- A query outside the surface above can fail on first execution. Run your own query set against the
  native build before shipping: the analyzers do not see through the dynamic query pipeline, so the
  native run is the check.

## Performance

Publishing natively removes the JIT and the cold start; on short-lived processes that is the whole
win. A start-up plus one small query drops from almost a second to tens of milliseconds, and the 13 GB
`oc-report` run below finishes in tenths of a second instead of one to two seconds.

That win has a price: without JIT the row materializer expressions are interpreted — roughly twice
the time and several times the allocations of compiled code. The build removes most of it for you: a
source generator covers every projection type declared in your assembly — named types and record DTOs,
and anonymous types as well — so a prepared query keeps its usual speed on a native build. On a
prepared 500-row, three-column command that is 543 → 364 µs and 246 → 39 KB.

```csharp
// no attribute, no registration: covered automatically
public sealed record OrderSummary(int Id, string? Customer, decimal Total);

var summaries = context.From<Order>()
    .Select(order => new OrderSummary(order.Id, order.Customer, order.Total))
    .ToList();

var rows = context.From<Order>().Select(order => new { order.Id, order.Customer }).ToList();
```

Streaming terminals are not affected by that cost: `WriteCsv` and `WriteJson` do not materialize rows
and write scalar columns with compiled code on a native build as well.

For bulk exports of wide rows on the AOT runtime the interpretation is still noticeable — such jobs
are better left on the JIT runtime.

## Real-world check

`oc-report` — a reporting utility over a 13 GB SQLite database — publishes natively with no source
change beyond `PublishAot=true`: an 8.2 MB binary, a 0.12 s run and a 30 MB peak.

The reference app is
[`examples/nextorm.examples.oc-report`](https://github.com/AlexeyShirshov/nextorm/tree/main/examples/nextorm.examples.oc-report).

## Reporting a failure

Open an issue with the smallest failing query, the target RID, the .NET SDK version and the
`dotnet publish` output. A native reproducer is worth more than a JIT one: some failures appear only
after publishing.
