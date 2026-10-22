# Nextorm — PostgreSQL provider

[![NuGet](https://img.shields.io/nuget/vpre/nextorm.postgres?logo=nuget)](https://www.nuget.org/packages/nextorm.postgres)

`nextorm.postgres` adds PostgreSQL support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a
zero-SQL object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows
without change tracking. The package supplies the PostgreSQL dialect and the `UsePostgres` builder
extension over `Npgsql`.

## Installation

All current nextorm releases are prereleases, so `--prerelease` is required:

```bash
dotnet add package nextorm --prerelease
dotnet add package nextorm.postgres --prerelease
```

Package Manager Console: `Install-Package nextorm -Prerelease` and
`Install-Package nextorm.postgres -Prerelease`.

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

Beta: every release so far is a prerelease, the public API still changes between them, and backward
compatibility is not preserved yet. Release notes are on
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
