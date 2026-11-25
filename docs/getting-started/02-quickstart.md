# Quickstart

Run a typed query against an in-memory SQLite database and print its results. This page is for .NET developers who have installed NextORM and want to see a complete query before learning its options.

**Prerequisites:** [Installation](01-installation.md).

## Complete minimal program

Create a .NET console project, then replace the contents of its `Program.cs` file with this code. Keep
the entity interface declarations after the top-level statements, as shown; C# requires top-level
statements to precede type declarations.

The program creates `customers` and `orders`, inserts sample data, and reads each order's ID and
status in one batch. SQLite runs in memory, so you don't need a database server. Install `nextorm`
and `nextorm.sqlite` in the console project first:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.Sqlite;

using var dataContext = new SqliteDataContext(
    "Data Source=:memory:", new DataContextBuilder());

var customers = new[] { (Id: 1, Name: "Amina"), (Id: 2, Name: "Ravi") };
var sampleOrders = new[]
{
    (Id: 1, CustomerId: 1, Status: "Paid", TotalCents: 2550),
    (Id: 2, CustomerId: 2, Status: "Pending", TotalCents: 1800),
    (Id: 3, CustomerId: 1, Status: "Paid", TotalCents: 4250),
};

var orders = await dataContext.CreateBatchBuilder()
    .Raw("""
        create table customers (
            id integer primary key,
            name text not null
        )
        """)
    .Raw("""
        create table orders (
            id integer primary key,
            customer_id integer not null,
            status text not null,
            total_cents integer not null
        )
        """)
    .Insert<ICustomer>(insert => insert
        .Values(customers, customer => new { customer.Id, customer.Name }))
    .Insert<IOrder>(insert => insert
        .Values(sampleOrders, order => new
        {
            order.Id, order.CustomerId, order.Status, order.TotalCents
        }))
    .Query(db => db.From<IOrder>()
        .OrderBy(order => order.Id)
        .Select(order => new { order.Id, order.Status }))
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

`CreateBatchBuilder()` collects the steps in execution order. `Raw` adds SQL to create the tables,
and `Insert<TEntity>` supplies a typed insert builder to configure with `Values`. `Query` supplies
the batch's context to build the final query. `ToListAsync()` runs the entire batch and returns the
rows from that query. See [SQL batches](../guide/25-sql-batch.md) and
[Inserting data](../guide/17-insert-statement.md).

In the final query,
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
chooses the `orders` source, and
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
chooses which columns to return.

## Using a connection string

The context creates and owns the connection, so disposing the context closes it. To use a SQLite
file instead of an in-memory database, set `Data Source` to the file path:

```csharp
using var dataContext = new SqliteDataContext(
    "Data Source=app.db", new DataContextBuilder());
```

## Reading the data

For a query outside a batch, you can also use `ToListAsync` to collect the rows in a list. Use
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
