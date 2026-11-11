# Nextorm — SQL Server provider

[![NuGet](https://img.shields.io/nuget/vpre/nextorm.sqlserver?logo=nuget)](https://www.nuget.org/packages/nextorm.sqlserver)

`nextorm.sqlserver` adds Microsoft SQL Server and Azure SQL support to
[Nextorm](https://github.com/AlexeyShirshov/nextorm) — a zero-SQL object-relational mapper for .NET that
turns typed C# queries into SQL and materializes rows without change tracking. The package supplies the
T-SQL dialect and the `UseSqlServer` builder extension over `Microsoft.Data.SqlClient`.

## Installation

All current nextorm releases are prereleases, so `--prerelease` is required:

```bash
dotnet add package nextorm --prerelease
dotnet add package nextorm.sqlserver --prerelease
```

Package Manager Console: `Install-Package nextorm -Prerelease` and
`Install-Package nextorm.sqlserver -Prerelease`.

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.SqlServer;

var builder = new DataContextBuilder()
    .UseSqlServer("Server=localhost;Database=app;User Id=app;Password=secret;TrustServerCertificate=true");
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

Identifiers are bracket-quoted and values are bound as `@name` parameters. `UseSqlServer` also accepts
an open `DbConnection`, including one owned by an ambient transaction.

## Good to know

- Bulk insert goes through the native `SqlBulkCopy` path, the only one that honours
  `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers`.
- `MERGE` and `OUTPUT` are supported here and on PostgreSQL 15+; the other providers reject them.
- SQL Server is the only provider whose `datetimeoffset` keeps the original offset; locking hints
  (`WITH (NOLOCK)`) and native index hints are T-SQL-only too.

## Documentation

- [Connecting a provider: SQL Server](https://alexeyshirshov.github.io/nextorm/connecting-providers/sqlserver.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

Beta: every release so far is a prerelease, the public API still changes between them, and backward
compatibility is not preserved yet. Release notes are on
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
