# Nextorm — MariaDB provider

[![NuGet](https://img.shields.io/nuget/v/nextorm.mariadb?logo=nuget)](https://www.nuget.org/packages/nextorm.mariadb)

`nextorm.mariadb` adds MariaDB support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a
zero-SQL object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows
without change tracking. It shares the MySQL data context and dialect, keeps MariaDB's own grammar
differences, and ships the `UseMariaDb` builder extension over `MySqlConnector`.

## Installation

Stable version `1.0.9` is available. Add the packages without prerelease flags:

```bash
dotnet add package nextorm
dotnet add package nextorm.mariadb
```

Package Manager Console: `Install-Package nextorm` and
`Install-Package nextorm.mariadb`.

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.MariaDb;

var builder = new DataContextBuilder()
    .UseMariaDb("Server=localhost;Database=app;User ID=app;Password=secret");
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

Values are bound as `@name` parameters and identifiers are backtick-quoted. `UseMariaDb` also accepts an
open `DbConnection`.

## Good to know

- MariaDB has no source-level row lock: `FOR UPDATE OF` / `FOR SHARE OF` are rejected with
  `NotSupportedException`, while whole-query `FOR UPDATE` works as usual.
- `UPDATE … RETURNING` is available only when the server version is explicitly configured as 13.0+;
  unset or older versions reject it instead of guessing.
- Paging uses `limit`/`offset`, and key upserts render `ON DUPLICATE KEY UPDATE`.

## Documentation

- [Connecting a provider: MariaDB](https://alexeyshirshov.github.io/nextorm/connecting-providers/mariadb.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

The public API may change between prereleases, and backward compatibility across prereleases is not
guaranteed. Review the release notes before upgrading between prereleases:
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
