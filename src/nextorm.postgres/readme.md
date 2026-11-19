# Nextorm — PostgreSQL provider

[![NuGet](https://img.shields.io/nuget/v/nextorm.postgres?logo=nuget)](https://www.nuget.org/packages/nextorm.postgres)

`nextorm.postgres` adds PostgreSQL support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a
zero-SQL object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows
without change tracking. The package supplies the PostgreSQL dialect and the `UsePostgres` builder
extension over `Npgsql`.

## Installation

Stable version `1.0.9` is available. Add the packages without prerelease flags:

```bash
dotnet add package nextorm
dotnet add package nextorm.postgres
```

Package Manager Console: `Install-Package nextorm` and
`Install-Package nextorm.postgres`.

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.Postgres;

var builder = new DataContextBuilder()
    .UsePostgres("Host=localhost;Database=app;Username=app;Password=secret");
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

Values are bound as `@name` parameters, which Npgsql rewrites to native positional placeholders.
`UsePostgres` also accepts an open `DbConnection`.

## Good to know

- Version-gated syntax is opt-in: the server version must be configured explicitly for aggregate
  `FILTER (WHERE …)` (9.4+), `MERGE` (15+) and similar. A concrete context type keeps one immutable
  version for the process lifetime.
- `SelectWhereMax`/`SelectWhereMin` use native `DISTINCT ON` / `LIMIT 1` instead of a window function.
- Bulk insert goes through `COPY`; the SQL Server-only bulk-copy flags are rejected rather than ignored.

## Documentation

- [Connecting a provider: PostgreSQL](https://alexeyshirshov.github.io/nextorm/connecting-providers/postgres.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

The public API may change between prereleases, and backward compatibility across prereleases is not
guaranteed. Review the release notes before upgrading between prereleases:
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
