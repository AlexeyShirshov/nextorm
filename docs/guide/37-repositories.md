# Repositories and application context

This guide shows how to set up an application context and use it to share named
repositories and reusable queries across your services.

## Why use a `RepositoryContext`?

In the code that reads and writes your application's data, you can use the
provider's `IDataContext` directly. Add a `RepositoryContext` subclass when several
services need the same named entry points and reusable queries. For example,
inject `AppRepositoryContext` so each service can start from `app.Orders` or call
`app.QueryUnpaidOrders()` without restating what makes an order unpaid.

NextORM provides the base class `NextORM.Core.Repositories.RepositoryContext`;
you write `AppRepositoryContext`. The base class keeps the `IDataContext` you
pass in and provides the protected `Repository<T>()` method, which creates and
reuses the repository for an entity type. The provider still executes the queries.
Adding this layer doesn't create another connection or transaction.

An `IRepository<Order>` is the entry point for one entity type. The application
context groups entries such as `Orders` and `Customers` and is where you put
queries that callers share. You don't need to implement a repository class for
each entity. If `dataContext.From<Order>()` already meets your needs, use it
directly: both the application context and its repositories are optional.

## A named entry point for an entity

Both calls count orders using the same database context and its mapping and filters.
Use the cancellation token supplied by your caller here and in the following examples:

```csharp
var countFromContext = await dataContext.From<Order>()
    .CountAsync(cancellationToken);
var countFromRepository = await app.Orders.CountAsync(cancellationToken);
```

Here, `dataContext` is the same instance as `app.DataContext`. `app.Orders` has
type `IRepository<Order>`: it's neither a table nor a list of loaded orders.
Accessing the property doesn't read any rows. In this example, `CountAsync()` runs
the query.

A domain repository might expose a domain operation such as `FindUnpaidOrders`
and hide how it queries the database. NextORM's `IRepository<T>` doesn't define
such operations: the caller still chooses the query and its filters. It also
doesn't track entity changes or save them through `SaveChanges`.

**Before you begin:** Map `Order` and `Customer` to database tables with unique
`Id` values. The examples also use `Order.CustomerId`, `Order.IsArchived`, and
`Order.IsPaid`.
See [Entities and metadata](../getting-started/03-entities-and-metadata.md)
and [Dependency injection](../getting-started/04-dependency-injection.md).

## Set up the application context

1. Derive your application context from `RepositoryContext`.
   Expose mapped entities through properties and put reusable queries in methods
   such as `QueryUnpaidOrders()`:

   ```csharp
   using NextORM.Core;
   using NextORM.Core.Repositories;

   public sealed class AppRepositoryContext(IDataContext provider)
       : RepositoryContext(provider)
   {
       public IRepository<Order> Orders => Repository<Order>();
       public IRepository<Customer> Customers => Repository<Customer>();

       public EntityBuilder<Order> QueryUnpaidOrders() =>
           Orders.Where(order => !order.IsPaid);
   }
   ```

2. After registering your provider as a scoped `IDataContext`, register the
   application context in the same service collection:

   ```csharp
   services.AddScoped<AppRepositoryContext>();
   ```

3. Inject `AppRepositoryContext` into a service resolved in that scope. To
   verify the setup, await `app.Orders.CountAsync(cancellationToken)`: it
   returns the number of orders visible through the provider's filters.

The application context borrows the provider. It neither executes commands itself
nor disposes the provider. Access the same `IDataContext` through `app.DataContext`.
Mapping, global filters, transactions, and caching still belong to the provider.

## Start independent queries

Start each query from the repository. For example, read active orders and then count
archived orders without carrying over the first query's filter:

```csharp
var activeOrders = await app.Orders.Where(o => !o.IsArchived)
    .ToListAsync(cancellationToken);
var archivedCount = await app.Orders.Where(o => o.IsArchived)
    .CountAsync(cancellationToken);
```

The context caches one repository per entity type, not its builders. Call `Query()`
for a fresh builder, or use an extension such as `Where` to start a query.
`Repository<T>(configure)` follows the same first-registration-per-process rule
as `From<T>(configure)`.

## Reuse and extend a query from your context

Put a condition in a context method when several callers need it but need to
finish the query differently. `QueryUnpaidOrders()` returns a fresh
`EntityBuilder<Order>` with the unpaid-order condition. It doesn't run SQL or
load orders. For example, add a customer filter to list one customer's unpaid
orders, or reuse the same method to count all unpaid orders:

```csharp
var orders = await app.QueryUnpaidOrders()
    .Where(order => order.CustomerId == customerId)
    .ToListAsync(cancellationToken);

var unpaidCount = await app.QueryUnpaidOrders().CountAsync(cancellationToken);
```

`customerId` is the ID of the customer whose orders you want to list. The list
query applies both conditions. The count runs a separate query and doesn't
inherit the customer filter. Execute both within the scope that owns the
provider context.

Return `EntityBuilder<Order>` when callers need to compose the query before
executing it. To hide the data-access API from callers, execute the query
inside your method and return data instead of the builder.

## Share a prepared query across context instances

When you run the same query with different parameter values, you can prepare it
once and keep it in a static field. Add these members to `AppRepositoryContext`
and import `System.Collections.Generic`, `System.Threading`, and
`System.Threading.Tasks`:

```csharp
private static IPreparedQueryCommand<Order>? _unpaidByCustomerPrepared;
private static object? _prepareLock;

public Task<List<Order>> FindUnpaidByCustomer(
    int customerId, CancellationToken cancellationToken)
{
    var prepared = LazyInitializer.EnsureInitialized(
        ref _unpaidByCustomerPrepared,
        ref _prepareLock,
        () => QueryUnpaidOrders()
            .Where(o => o.CustomerId == SqlFunctions.Parameter<int>(0))
            .Prepare(cancellationToken: cancellationToken));

    return prepared.ToListAsync(this.DataContext, cancellationToken, customerId);
}
```

On the first call, `LazyInitializer` prepares the query under a lock. Later calls,
including calls from other dependency injection scopes, reuse the query and bind
`customerId` to parameter `0`. The static field stores neither results nor a
context. `this.DataContext` is the property inherited from `RepositoryContext`. It
returns the `IDataContext` passed to the application context's constructor as
`provider`. Pass that provider to `ToListAsync`, not `AppRepositoryContext`.
The method forwards the cancellation token to the initial preparation and to
every execution.

Share the field only across instances with the same provider and the same mapping
and query shape. If tenants use different schemas or tables, prepare a separate
query for each mapping. Run the same prepared query sequentially on a single
connection. For details, see [Query plan cache and reuse](../engine/03-plan-cache-and-reuse.md).

## Use a repository in `From` or `Join`

Pass a repository to use its mapped table directly. For example, join orders to
customers without wrapping either table in a derived query:

```csharp
var withCustomers = app.DataContext.From(app.Orders)
    .Join(app.Customers, (order, customer) => order.CustomerId == customer.Id);
```

Both `From` and `Join` reject a repository backed by a different `IDataContext`
instance. To use the result of a filtered query as a derived source, pass its
builder instead:

```csharp
var archived = app.Orders.Where(o => o.IsArchived);
var fromArchived = app.DataContext.From(archived);
```

## Change data through a repository

`MakeInsert()`, `MakeUpdate()`, and `MakeDelete()` each create a fresh builder.
For example, to delete an archived order, set `orderId` to its ID and execute
the delete explicitly:

```csharp
var deleted = await app.Orders.MakeDelete()
    .Where(o => o.Id == orderId && o.IsArchived)
    .DeleteAsync(cancellationToken);
```

On providers that report affected-row counts, `deleted` is `1` if the order
matches and is visible through the active filters, or `0` otherwise. The command
uses the same provider and participates in its active transaction, if any.
For limits, provider restrictions, and more examples, see
[Data modification (DELETE)](18-delete-statement.md).
