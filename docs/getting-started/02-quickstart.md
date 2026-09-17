# Quickstart

> Define an entity, create a SQLite context, run a typed query and print the rows - the smallest end-to-end NextORM program.

**Prerequisites:** [Installation](01-installation.md).

## Overview

A NextORM query always has four parts:

1. an **entity** (a class or an interface) whose properties map to columns;
2. a **context** (`IDataContext`) built from a connection or a connection string;
3. a **query** built with `Create<T>()`, `Select`, `Where`, and so on;
4. a **terminal** such as `ToList()`, `First()`, `Any()` or `ToAsyncEnumerable()` that executes it.

`Create<T>()` also registers `T`'s metadata the first time it is seen. The context is disposable: a
context created from a connection string owns and closes the connection, while a context created from a
supplied `DbConnection` leaves that connection open.

## Complete minimal program

The following program is self-contained. It creates an in-memory SQLite database, seeds one table,
builds a context with `UseSqlite`, and streams the rows back as an anonymous type.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using nextorm.core;
using nextorm.sqlite;

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var setup = connection.CreateCommand())
{
    setup.CommandText =
        "create table simple_entity (id integer primary key);" +
        "insert into simple_entity (id) values (1), (2), (3);";
    setup.ExecuteNonQuery();
}

var builder = new DbContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDbContext();

await foreach (var row in dataContext.Create<ISimpleEntity>()
                                   .Select(entity => new { Id = (long)entity.Id })
                                   .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}

// The entity can live in its own file. In a single-file top-level program the type
// declaration must come after the top-level statements.
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
```

Output:

```text
Id = 1
Id = 2
Id = 3
```

The generated SQL is a plain `select` over the mapped table and column:

```sql
select id from simple_entity
```

## Using a connection string

`UseSqlite` also accepts a file path (or an SQLite connection string fragment), and the context then
creates and owns the connection:

```csharp
var builder = new DbContextBuilder().UseSqlite("app.db");
using var dataContext = builder.CreateDbContext();
```

> **Note:** in `DEBUG` builds `UseSqlite(string filepath)` throws `ArgumentException` when the file does
> not exist. Pass `:memory:` or an existing path while developing; pass a `DbConnection` to keep full
> control. `UseSqlServer(connectionString)` and `UsePostgres(connectionString)` have the corresponding
> string overloads.

## Reading the data

The query above uses `ToAsyncEnumerable()`. The other common terminals are:

| Terminal | Result |
|---|---|
| `ToList()` / `ToListAsync()` | `List<TResult>` |
| `First()` / `FirstAsync()` | the first row; throws if the sequence is empty |
| `FirstOrDefault()` / `FirstOrDefaultAsync()` | the first row or `default` |
| `Single()` / `SingleOrDefault()` (and `…Async`) | exactly one row (or `default`) |
| `Any()` / `AnyAsync()` | `bool` |
| `Count()` / `CountAsync()` | `int` |

Every terminal has a synchronous and an async form; prefer the async form in application code.

## See also

* [Entities and metadata](03-entities-and-metadata.md) - attributes, interface/class mapping, the
  entity-free `TableAlias` mode and fluent configuration.
* [Dependency injection](04-dependency-injection.md) - register the context instead of constructing it
  by hand.

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`test/nextorm.integration.tests/Providers/SqliteTestProvider.cs:32`;
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:115`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:111`.
