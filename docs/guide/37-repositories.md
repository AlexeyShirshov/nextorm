# Repositories and application context

Use an application `DbContext` to expose named repositories such as `app.Orders`.
This guide shows how to register that context and use a repository to query and
change data. In NextORM, `IRepository<T>` is a typed source for query and mutation
builders, not a domain repository that hides persistence or an Entity Framework Core
context with change tracking and `SaveChanges`.

**Before you begin:** Map `Order` and `Customer` to database tables with unique
`Id` values. The examples also use `Order.CustomerId` and `Order.IsArchived`.
See [Entities and metadata](../getting-started/03-entities-and-metadata.md)
and [Dependency injection](../getting-started/04-dependency-injection.md).

## Set up the application context

1. Derive your application context from `NextORM.Core.Repositories.DbContext`
   and expose each mapped entity through a property:

   ```csharp
   using NextORM.Core;
   using NextORM.Core.Repositories;

   public sealed class AppDbContext(IDataContext provider) : DbContext(provider)
   {
       public IRepository<Order> Orders => Repository<Order>();
       public IRepository<Customer> Customers => Repository<Customer>();
   }
   ```

2. After registering your provider as a scoped `IDataContext`, register the
   application context in the same service collection:

   ```csharp
   services.AddScoped<AppDbContext>();
   ```

3. Inject `AppDbContext` into a service resolved in that scope. To verify the setup,
   call `app.Orders.Count()`: it returns the number of orders visible through the
   provider's filters.

The application context borrows the provider. It neither executes commands itself
nor disposes the provider. Access the same `IDataContext` through `app.DataContext`.
Mapping, global filters, transactions, and caching still belong to the provider.

## Start independent queries

Start each query from the repository. For example, read active orders and then count
archived orders without carrying over the first query's filter:

```csharp
var activeOrders = await app.Orders.Where(o => !o.IsArchived)
    .ToListAsync(cancellationToken);
var archivedCount = app.Orders.Where(o => o.IsArchived).Count();
```

The context caches one repository per entity type, not its builders. Call `Query()`
for a fresh builder, or use an extension such as `Where` to start a query.
`Repository<T>(configure)` follows the same first-registration-per-process rule
as `From<T>(configure)`.

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
