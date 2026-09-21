# Quickstart

> Define an entity, create a SQLite context, run a typed query and print the rows - the smallest end-to-end NextORM program.

**Prerequisites:** [Installation](01-installation.md).

## Overview

A NextORM query always has four parts:

1. an **entity** (a class or an interface) whose properties map to columns;
2. a **context** ([`IDataContext`](xref:NextORM.Core.IDataContext)) built from a connection or a connection string;
3. a **query** built with [`From`](xref:NextORM.Core.DataContextExtensions), [`Select`](xref:NextORM.Core.EntityBuilder`1), [`Where`](xref:NextORM.Core.EntityBuilder`1), and so on;
4. a **terminal** such as [`ToList`](xref:NextORM.Core.EntityBuilder`1), [`First`](xref:NextORM.Core.EntityBuilder`1), [`Any`](xref:NextORM.Core.EntityBuilder`1) or [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilder`1) that executes it.

[`From`](xref:NextORM.Core.DataContextExtensions) also registers `T`'s metadata the first time it is seen. The context is disposable: a
context created from a connection string owns and closes the connection, while a context created from a
supplied `DbConnection` leaves that connection open.

## Complete minimal program

The following program is self-contained. It creates an in-memory SQLite database, seeds one table,
builds a context with [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions), and streams the rows back as an anonymous type.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var setup = connection.CreateCommand())
{
    setup.CommandText =
        "create table simple_entity (id integer primary key);" +
        "insert into simple_entity (id) values (1), (2), (3);";
    setup.ExecuteNonQuery();
}

var builder = new DataContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDataContext();

await foreach (var row in dataContext.From<ISimpleEntity>()
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

[`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions) also accepts a file path (or an SQLite connection string fragment), and the context then
creates and owns the connection:

```csharp
var builder = new DataContextBuilder().UseSqlite("app.db");
using var dataContext = builder.CreateDataContext();
```

> **Note:** in `DEBUG` builds `UseSqlite(string filepath)` throws `ArgumentException` when the file does
> not exist. Pass `:memory:` or an existing path while developing; pass a `DbConnection` to keep full
> control. `UseSqlServer(connectionString)` and `UsePostgres(connectionString)` have the corresponding
> string overloads.

## Reading the data

The query above uses [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilder`1). The other common terminals are:

| Terminal | Result |
|---|---|
| [`ToList`](xref:NextORM.Core.EntityBuilder`1) / [`ToListAsync`](xref:NextORM.Core.EntityBuilder`1) | `List<TResult>` |
| [`First`](xref:NextORM.Core.EntityBuilder`1) / [`FirstAsync`](xref:NextORM.Core.EntityBuilder`1) | the first row; throws if the sequence is empty |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilder`1) / [`FirstOrDefaultAsync`](xref:NextORM.Core.EntityBuilder`1) | the first row or `default` |
| [`Single`](xref:NextORM.Core.EntityBuilder`1) / [`SingleOrDefault`](xref:NextORM.Core.EntityBuilder`1) (and `…Async`) | exactly one row (or `default`) |
| [`Any`](xref:NextORM.Core.EntityBuilder`1) / [`AnyAsync`](xref:NextORM.Core.EntityBuilder`1) | `bool` |
| [`Count`](xref:NextORM.Core.EntityBuilder`1) / [`CountAsync`](xref:NextORM.Core.EntityBuilder`1) | `int` |

Every terminal has a synchronous and an async form; prefer the async form in application code.

## See also

* [Entities and metadata](03-entities-and-metadata.md) - attributes, interface/class mapping, the
  entity-free [`TableAlias`](xref:NextORM.Core.TableAlias) mode and fluent configuration.
* [Dependency injection](04-dependency-injection.md) - register the context instead of constructing it
  by hand.

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs:32`;
`tests/nextorm.sqlite.tests/ConnectionManagementTests.cs:115`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`.
