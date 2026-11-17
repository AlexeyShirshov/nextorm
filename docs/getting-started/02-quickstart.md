# Quickstart

Run a typed query against an in-memory SQLite database and print its results. This page is for .NET developers who have installed NextORM and want to see a complete query before learning its options.

**Prerequisites:** [Installation](01-installation.md).

## Complete minimal program

Create a .NET console project, then replace the contents of its `Program.cs` file with this code. Keep
the entity interface declarations after the top-level statements, as shown; C# requires top-level
statements to precede type declarations.

The program creates `orders` and `customers` tables, adds three sample orders, and reads each order's
ID and status. It uses an in-memory database, so it doesn't need a database server. Install `nextorm`
and `nextorm.sqlite` in the console project first:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var setup = connection.CreateCommand())
{
    setup.CommandText =
        "create table customers (id integer primary key, name text not null);" +
        "create table orders (id integer primary key, customer_id integer not null, " +
        "status text not null, total_cents integer not null);" +
        "insert into customers (id, name) values (1, 'Amina'), (2, 'Ravi');" +
        "insert into orders (id, customer_id, status, total_cents) values " +
        "(1, 1, 'Paid', 2550), (2, 2, 'Pending', 1800), (3, 1, 'Paid', 4250);";
    setup.ExecuteNonQuery();
}

var builder = new DataContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDataContext();

var orders = await dataContext.From<IOrder>()
    .OrderBy(order => order.Id)
    .Select(order => new { order.Id, order.Status })
    .ToListAsync();

foreach (var order in orders)
{
    Console.WriteLine($"Order {order.Id}: {order.Status}");
}

[SqlTable("orders")]
public interface IOrder
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("customer_id")]
    int CustomerId { get; set; }

    [Column("status")]
    string Status { get; set; }

    [Column("total_cents")]
    int TotalCents { get; set; }
}

[SqlTable("customers")]
public interface ICustomer
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }
}
```

From the project directory, run the program:

```bash
dotnet run
```

The program prints:

```text
Order 1: Paid
Order 2: Pending
Order 3: Paid
```

The query selects two mapped columns from `orders`:

```sql
select id, status
 from orders
 order by id
```

## Overview

A query uses a mapped entity, a context, and a sequence of query operations. In this example,
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
chooses the `orders` source, [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
chooses which values to return, and [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[]))
executes the query and returns a list.

[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
also registers the entity's metadata the first time it is used. The context is disposable. A context
created from a connection string owns and closes its connection; a context created from a supplied
`DbConnection` leaves that connection open. This example supplies a connection and disposes it
separately.

## Using a connection string

[`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) also accepts a file path (or an SQLite connection string fragment), and the context then
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

The example uses `ToListAsync` to collect the rows in a list. Use
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[]))
to stream results instead. Other common terminal methods include:

| Terminal | Result |
|---|---|
| [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `List<TResult>` |
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | the first row; throws if the sequence is empty |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | the first row or `default` |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) (and `…Async`) | exactly one row (or `default`) |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) / [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `bool` |
| [`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})) / [`CountAsync`](xref:NextORM.Core.EntityBuilderExtensions.CountAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `int` |

Every terminal has a synchronous and an async form; prefer the async form in application code.

## See also

* [Filter rows](../guide/03-filtering-where.md) - add conditions before selecting the result.
* [Select the fields you need](../guide/02-projections.md) - shape the returned rows.
* [Join tables](../guide/04-joins.md) - combine orders with customer details.
* [Entities and metadata](03-entities-and-metadata.md) - attributes, interface/class mapping, the
  entity-free [`TableAlias`](xref:NextORM.Core.TableAlias) mode and fluent configuration.
* [Dependency injection](04-dependency-injection.md) - register the context instead of constructing it
  by hand.

---

The quickstart uses the documented SQLite connection and query APIs. See
[`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection))
and the query examples in [Filtering](../guide/03-filtering-where.md),
[Projections](../guide/02-projections.md), and [Joins](../guide/04-joins.md).
