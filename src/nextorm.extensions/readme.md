# nextorm.extensions

Install:

```sh
dotnet add package nextorm.extensions
```

`SelectWhereMinEx` and `SelectWhereMaxEx` attach an extreme-row query component to an `EntityBuilder<TEntity>` using only the public `IQueryComponent` layer.

```csharp
var cmd = ctx.From<Order>()
    .SelectWhereMinEx(x => x.Amount)
    .Select(x => new { x.Id, x.Amount });
```
