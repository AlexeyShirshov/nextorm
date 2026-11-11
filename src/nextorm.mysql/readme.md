# Nextorm — MySQL provider

[![NuGet](https://img.shields.io/nuget/vpre/nextorm.mysql?logo=nuget)](https://www.nuget.org/packages/nextorm.mysql)

`nextorm.mysql` adds MySQL support to [Nextorm](https://github.com/AlexeyShirshov/nextorm) — a zero-SQL
object-relational mapper for .NET that turns typed C# queries into SQL and materializes rows without
change tracking. The package supplies the MySQL dialect and the `UseMySql` builder extension over
`MySqlConnector`.

## Installation

All current nextorm releases are prereleases, so `--prerelease` is required:

```bash
dotnet add package nextorm --prerelease
dotnet add package nextorm.mysql --prerelease
```

Package Manager Console: `Install-Package nextorm -Prerelease` and
`Install-Package nextorm.mysql -Prerelease`.

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.MySql;

var builder = new DataContextBuilder()
    .UseMySql("Server=localhost;Database=app;User ID=app;Password=secret");
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

Values are bound as `@name` parameters and identifiers are backtick-quoted. `UseMySql` also accepts an
open `DbConnection`.

## Good to know

- Key upserts render `ON DUPLICATE KEY UPDATE`, and statements batch through `MySqlBatch`.
- Native index hints (`USE`/`FORCE`/`IGNORE INDEX`) and source-level `FOR UPDATE OF`/`FOR SHARE OF`
  locks are available; SQL Server's locking table hints are not.
- Bulk insert uses the portable `INSERT … VALUES` path, so the SQL Server-only bulk-copy flags are
  rejected rather than ignored.

## Documentation

- [Connecting a provider: MySQL](https://alexeyshirshov.github.io/nextorm/connecting-providers/mysql.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

Beta: every release so far is a prerelease, the public API still changes between them, and backward
compatibility is not preserved yet. Release notes are on
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
