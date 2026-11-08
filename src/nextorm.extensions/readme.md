# nextorm.extensions

Install:

```sh
dotnet add package nextorm.extensions
```

Public query-component operators built entirely on the `nextorm.core` extension surface
(`IQueryComponent` + `EntityBuilder.WithComponent`): no friend access, no raw SQL and no global state.

- `SelectWhereMin` / `SelectWhereMax` — keep the row(s) with the extreme value, globally or per group.
- `SelectGroup` / `SelectGroupHaving` — infer the grouping keys from a projection and apply `HAVING`.

```csharp
using NextORM.Extensions;

var cmd = ctx.From<Order>()
    .SelectWhereMin(x => x.Amount)
    .Select(x => new { x.Id, x.Amount });
```

```csharp
using NextORM.Core;
using NextORM.Extensions;

// No explicit GroupBy: the aggregate-free, row-dependent parts of the projection become the keys.
var byCustomer = ctx.From<Order>()
    .SelectGroup(x => new { x.CustomerId, Total = SqlFunctions.Sql.sum(x.Amount) })
    .ToList();
```

Guide: <https://alexeyshirshov.github.io/nextorm/advanced/extensions-package.html>.
