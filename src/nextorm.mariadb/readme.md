# Nextorm — MariaDB provider

[![NuGet](https://img.shields.io/nuget/vpre/nextorm.mariadb?logo=nuget)](https://www.nuget.org/packages/nextorm.mariadb)

`nextorm.mariadb` adds MariaDB support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a
zero-SQL object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows
without change tracking. It shares the MySQL data context and dialect, keeps MariaDB's own grammar
differences, and ships the `UseMariaDb` builder extension over `MySqlConnector`.

## Installation

All current nextorm releases are prereleases, so `--prerelease` is required:

```bash
dotnet add package nextorm --prerelease
dotnet add package nextorm.mariadb --prerelease
```

Package Manager Console: `Install-Package nextorm -Prerelease` and
`Install-Package nextorm.mariadb -Prerelease`.

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

Beta: every release so far is a prerelease, the public API still changes between them, and backward
compatibility is not preserved yet. Release notes are on
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
