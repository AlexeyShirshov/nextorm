# Nextorm — ClickHouse provider

[![NuGet](https://img.shields.io/nuget/v/nextorm.clickhouse?logo=nuget)](https://www.nuget.org/packages/nextorm.clickhouse)

`nextorm.clickhouse` adds ClickHouse support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a
zero-SQL object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows
without change tracking. The package supplies the ClickHouse dialect, ClickHouse-only fluent members and
the `UseClickHouse` builder extension over the official `ClickHouse.Driver`.

## Installation

Stable version `1.0.9` is available. Add the packages without prerelease flags:

```bash
dotnet add package nextorm
dotnet add package nextorm.clickhouse
```

Package Manager Console: `Install-Package nextorm` and
`Install-Package nextorm.clickhouse`.

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.ClickHouse;

var builder = new DataContextBuilder()
    .UseClickHouse("Host=localhost;Port=8123;Database=app;Username=default;Password=secret");
using var dataContext = builder.CreateDataContext();

await foreach (var row in dataContext.From<IOrder>()
                                     .Where(order => order.Total > 100)
                                     .Select(order => new { order.Id, order.Total })
                                     .ToAsyncEnumerable())
{
    Console.WriteLine($"{row.Id}: {row.Total}");
}

[SqlTable("orders")]
public interface IOrder
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("total")]
    decimal Total { get; set; }
}
```

Values are bound as `@name` parameters, which the driver rewrites to ClickHouse's native
`{name:Type}` form; identifiers are backtick-quoted. `UseClickHouse` also accepts an open `DbConnection`.

## Good to know

- The ClickHouse-only fluent members — `Final`, `PreWhere`, `Settings`, `Sample`, `LimitBy`,
  `WithTotals`, `ArrayJoin`/`LeftArrayJoin`, the semi/anti/paste joins and `j => j.Global()` — ship in
  this package as extension methods: add `using NextORM.ClickHouse;`.
- There are no transactions, no statement batches and no stored procedures; ClickHouse rejects them.
- `SelectWhereMax`/`SelectWhereMin` map to native `argMin`/`argMax`, and bulk insert uses the portable
  `INSERT … VALUES` path.

## Documentation

- [Connecting a provider: ClickHouse](https://alexeyshirshov.github.io/nextorm/connecting-providers/clickhouse.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

The public API may change between prereleases, and backward compatibility across prereleases is not
guaranteed. Review the release notes before upgrading between prereleases:
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
