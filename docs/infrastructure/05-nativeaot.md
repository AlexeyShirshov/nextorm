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

## What is supported

Providers: **SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse**.

Query surface: typed and anonymous projections, filters, ordering, paging, aggregates,
`GROUP BY`/`HAVING`, joins, CTEs, window functions (where the provider supports them), SQLite JSON
functions, ClickHouse arrays, `SelectGroup`/`SelectGroupHaving`, `SelectWhereMin`/`SelectWhereMax`,
prepared-command reuse and correlated subqueries.

Every supported scenario runs natively and under JIT and is compared row by row: row counts and
values must match. That comparison runs automatically on every change, so "works natively" is a
standing build condition, not a one-off check.

## Limitations

- The EF Core bridge (`nextorm.entityframeworkcore`), bulk insert, raw SQL row mapping and the DML
  builders are not part of the native build yet — keep those scenarios on the regular JIT runtime.
- `System.Text.Json` converters work through `JsonSerializerContext`: serialization stays
  reflection-based, so the context has to be set up as usual.
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
