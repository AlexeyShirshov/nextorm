# Nextorm — Entity Framework Core integration

[![NuGet](https://img.shields.io/nuget/vpre/nextorm.entityframeworkcore?logo=nuget)](https://www.nuget.org/packages/nextorm.entityframeworkcore)

Runs [Nextorm](https://github.com/AlexeyShirshov/nextorm) queries and commands over the connection and
transaction of an existing EF Core `DbContext`, reusing the EF model mapping instead of declaring it
again. Keep writes, change tracking and navigation fixup in EF Core; use nextorm for the analytical
reads it is built for — windows, CTEs, aggregates — on the very same connection.

## Installation

All current nextorm releases are prereleases, so `--prerelease` is required:

```bash
dotnet add package nextorm.entityframeworkcore --prerelease
```

Package Manager Console: `Install-Package nextorm.entityframeworkcore -Prerelease`.

The package already references `nextorm.core` and the PostgreSQL, SQL Server, SQLite and MySQL
providers, so no separate nextorm provider package is needed.

## Usage

```csharp
using Microsoft.EntityFrameworkCore;
using NextORM.Core;                 // From<T>() and the terminal extension methods
using NextORM.EntityFrameworkCore; // CreateNextOrmContext()

using var db = new AppDbContext(options); // your EF Core context

// Runs on db's connection and, when EF has one open, enlists in EF's current transaction.
using var next = db.CreateNextOrmContext();

var top = next.From<Order>()
    .Where(order => order.Total > 100)
    .OrderBy(order => order.Total)
    .Select(order => new { order.Id, order.Total })
    .ToList();
```

Pass a callback to override nextorm defaults after the provider is detected from
`Database.ProviderName`:

```csharp
using var next = db.CreateNextOrmContext(builder => builder
    .UseLoggerFactory(loggerFactory)
    .UseCommandTimeout(30));
```

## Good to know

- The bridge never commits, rolls back or closes the connection or transaction — EF Core keeps
  ownership of both.
- Table and column mapping comes from the EF `IModel`, so entity classes need no nextorm
  `[SqlTable]`/`[Column]` (or fluent) configuration. `NextOrmModelMapper.Register(db.Model)` maps a
  model without creating a context.
- EF Core **Relational** only: SQL Server, PostgreSQL, SQLite and MySQL/MariaDB providers are
  recognised; EF's non-relational `InMemory` provider is not supported.

## Documentation

- [Entity Framework Core integration](https://alexeyshirshov.github.io/nextorm/advanced/integration-efcore.html)
- [Quickstart](https://alexeyshirshov.github.io/nextorm/getting-started/02-quickstart.html)
- [Query reuse: plan cache vs `Prepare()`](https://alexeyshirshov.github.io/nextorm/engine/03-plan-cache-and-reuse.html)
- [Comparisons and benchmarks](https://alexeyshirshov.github.io/nextorm/comparisons/index.html)

## Status

Beta: every release so far is a prerelease, the public API still changes between them, and backward
compatibility is not preserved yet. Release notes are on
[GitHub Releases](https://github.com/AlexeyShirshov/nextorm/releases).

## License

[MIT](https://github.com/AlexeyShirshov/nextorm/blob/main/LICENSE)
