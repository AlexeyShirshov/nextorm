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

Guide: <https://alexeyshirshov.github.io/nextorm/advanced/extensions-package.html>.
