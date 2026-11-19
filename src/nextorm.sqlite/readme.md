# Nextorm — SQLite provider

[![NuGet](https://img.shields.io/nuget/v/nextorm.sqlite?logo=nuget)](https://www.nuget.org/packages/nextorm.sqlite)

`nextorm.sqlite` adds SQLite support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a
zero-SQL object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows
without change tracking. The package supplies the SQLite dialect and the `UseSqlite` builder extension
over `Microsoft.Data.Sqlite`.

## Installation

Stable version `1.0.9` is available. Add the packages without prerelease flags:

```bash
dotnet add package nextorm
dotnet add package nextorm.sqlite
```

Package Manager Console: `Install-Package nextorm` and
`Install-Package nextorm.sqlite`.

## Usage

Before you run this sample, make sure `app.db` contains the `orders` table expected by `IOrder`:
`id` is an integer primary key and `total` is a non-null numeric column. For example:

```sql
CREATE TABLE orders (id INTEGER PRIMARY KEY, total NUMERIC NOT NULL);
```

Query the existing table with the following mapped entity:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.Sqlite;

var builder = new DataContextBuilder().UseSqlite("app.db");
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

Values are bound as `$name` parameters and paging renders `limit`/`offset`. `UseSqlite` also accepts an
open `DbConnection` when you want to own the connection yourself.

## Good to know

- Paging renders `limit`/`offset`, and `DateTime.Now`/`UtcNow` render `datetime('now')` — SQLite has no `now()`.
- Custom `stdev`/`var` aggregates are registered on every connection the context creates.
- FTS5 maintenance (merge, optimize, rebuild) is a SQLite-only command surface; stored procedures,
  row locks and `CrossApply`/`OuterApply` are rejected with `NotSupportedException`.

## Documentation

- [Connecting a provider: SQLite](https://alexeyshirshov.github.io/nextorm/connecting-providers/sqlite.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

The public API may change between prereleases, and backward compatibility across prereleases is not
guaranteed. Review the release notes before upgrading between prereleases:
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
