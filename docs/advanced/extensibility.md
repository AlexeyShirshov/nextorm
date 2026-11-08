# Extensibility

> nextorm exposes a small set of public hooks an external assembly can use without friend access: declare application-wide data rules, map custom CLR storage, build reusable query operators, and observe execution. This page explains which hook to choose, who calls it and when, and gives a short working example for each.

**Prerequisites:** [Provider overview](../connecting-providers/overview.md) · [API quick reference](api-reference.md)

## Why extend nextorm

Reach for an extension point when you need one of these:

- **Application-wide data rules** — a predicate that applies to every query over an entity (tenant isolation, soft delete, row-level security). Use global query filters.
- **Custom CLR storage and mapping** — an `enum` stored as text, a strong-typed value stored as `decimal`, identifiers translated to the database's spelling, a CLR method that calls a database function. Use value converters, naming conventions and SQL functions.
- **Reusable query operators without forking the core** — a new operator that changes the generated SQL (an added predicate, a reshaped row source) while keeping the plan cache, parameter binding and provider independence of an ordinary query. Use query components.
- **Instrumentation and provider-specific SQL** — observe commands and connections, or override a provider dialect and its native extreme-row renderer. Use interceptors and custom dialects.

Extension points fall into three families:

- **Configuration hooks** — declarative, per context, query or property. They change how names, values, filters and commands are handled; some add predicates to the generated SQL, but the statement shape stays the engine's.
- **Structural query plugins** — query components. They change the statement itself, with no registry and no global state: a component is an object attached to one immutable builder.
- **Advanced dialect work** — overriding a provider dialect (and, for PostgreSQL and ClickHouse, its native extreme-row renderer) to emit provider-specific SQL. This is the deepest point and the only one that writes SQL text.

`BaseExpressionVisitor` is public but it is not a registered hook: there is no translator registry and no materializer-plugin registration. Several dialects are `sealed` (`SqliteDialect`, `SqlServerDialect`, `MariaDbDialect`); `MySqlDialect` is intentionally left unsealed so `MariaDbDialect` can reuse it. Public members of `SqlDialectBase` exist for provider implementations; the documented external compatibility promise covers the extreme-row renderer on PostgreSQL and ClickHouse only.

## Map of public hooks

| Hook | Configure | Who calls it | When | Section |
|---|---|---|---|---|
| `[SqlFunction]` | attribute on method/type | expression translator | while translating a LINQ expression | [SQL functions](#sql-functions-sqlfunction) |
| `INamingConvention` | `DataContextBuilder.UseNamingConvention` / `EntityBuilder.WithNamingConvention` | metadata + SQL builder | table/column name resolution | [Naming conventions](#naming-conventions-inamingconvention) |
| `IPropertyValueConverter` / `ValueConverter<TModel,TProvider>` | attribute or fluent `HasConversion` | read path and write/parameter path | read and write of a property | [Value converters](#value-converters-ipropertyvalueconverter--valueconvertertmodeltprovider) |
| `HasQueryFilter` | `EntityMetadataBuilder<T>` | query planner | every query over the entity (subject to bypass flags) | [Global query filters](#global-query-filters-hasqueryfilter) |
| `IQueryInterceptor` / `IConnectionInterceptor` | `DataContextBuilder.AddInterceptor` / `DataContext.AddInterceptor` | command executor and connection manager | command and connection lifecycle | [Interceptors](#interceptors-iqueryinterceptor--iconnectioninterceptor) |
| `IQueryComponent` (+ descriptor, optional shape) | `EntityBuilder.WithComponent` | planner + `SqlBuilder` | prepare (possibly more than once) and render (miss only) | [Extending query construction with query components](#extending-query-construction-with-query-components) |
| `ISqlDialect` / `SqlDialectBase` (+ `IExtremeRowRenderer`) | subclass provider context | SQL builder throughout | every SQL fragment | [Advanced: custom SQL dialects and renderers](#advanced-custom-sql-dialects-and-renderers) |

## Example types used below

In the snippets below, `ctx` is an already-created SQL data context and `connectionString` is its connection string; declarations and use fragments are shown separately.

```csharp declarations
using NextORM.Core;
using NextORM.Sqlite;

public sealed class Player
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; }
    public int Score { get; set; }
}

public enum Status { Unknown, Active, Closed }

public sealed class Order
{
    public long Id { get; set; }
    public Status Status { get; set; }
}
```

## SQL lifecycle: preparation, rendering and cache

Structural hooks (components) participate in the same lifecycle as every other query. Understanding it prevents two common mistakes: assuming `Prepare` runs once per execution, and assuming `BuildQuery` runs on a cache hit.

1. **Attach** — `WithComponent` copies the builder; a same-slot component replaces in place.
2. **Prepare** — `QueryCommand.QueryPreparer` prepares the command. For a component, `Prepare` can run more than once for the same cached plan: a new command prepared against a warmed plan cache re-runs preparation and can rebuild the descriptor, even though the plan itself is reused. A raw recipe cache hit for a component that implements `IQueryComponentShape` can skip `Prepare` entirely.
3. **Descriptor → plan key** — the returned descriptor lands in `QueryCommand.PreparedComponents`; the plan key hashes `StructuralHash` and compares with `StructurallyEquals`. Two structurally equal descriptors share a plan only when the rest of the query matches too; the descriptor is one part of the key, not the whole key.
4. **BuildQuery** — on a plan build (cache miss) only, `SqlBuilder.MakeSelect` clones the prepared command, calls `BuildQuery` on the clone for every component, and renders the clone. The handler mutates a throwaway copy.
5. **Execute / warm hit** — the SQL is stored on the plan. A warm hit reuses it: `BuildQuery` is not called. Captured values and positional parameters are rebound against the original command. A parameter-refresh re-render reuses the shape built on the miss, so `BuildQuery` does not run again.

In practice: you never call `Prepare` or `BuildQuery` yourself. A component's `BuildQuery` never sees SQL text; the engine owns rendering.

## Configuration hooks

### SQL functions (`[SqlFunction]`)

Maps a CLR method to a database scalar function. The placeholder body is never executed, so `=> throw new NotSupportedException()` or `=> default!` is enough. The attribute applies to a method or to the declaring type.

```csharp declarations
using NextORM.Core;

public static class Udf
{
    [SqlFunction("upper")]
    public static string ToUpper(string value) => throw new NotSupportedException();
}

```

```csharp example
var value = ctx.From<Player>()
    .Where(p => p.Id == 2)
    .Select(p => Udf.ToUpper(p.Name!))
    .First();
```

`[SqlFunction]` works only inside a query expression: the call is translated, never executed, so calling `Udf.ToUpper(...)` directly in ordinary C# throws. Built-in `string`/`Math`/`DateTime` translations run first and cannot be overridden. nextorm only emits the call; the function must already exist in the database. Full contract and SQL output: [User-defined functions](../guide/12-user-defined-functions.md).

### Naming conventions (`INamingConvention`)

`ColumnName(string)` and `TableName(string, bool)` (the boolean tells you whether the CLR name came from an interface). Configure globally or per query; explicit `[SqlTable]`/`[Column]`/fluent names are taken verbatim.

```csharp declarations
using NextORM.Core;

public sealed class PrefixedNamingConvention : INamingConvention
{
    public string TableName(string clrName, bool isInterface)
        => "tbl_" + (isInterface && clrName.StartsWith('I') ? clrName[1..] : clrName);

    public string ColumnName(string propertyName) => "col_" + propertyName;
}

```

```csharp example
var builder = new DataContextBuilder()
    .UseNamingConvention(new PrefixedNamingConvention())
    .UseSqlite(connectionString);
```

See [Entities and metadata](../getting-started/03-entities-and-metadata.md#naming-conventions).

### Value converters (`IPropertyValueConverter` / `ValueConverter<TModel,TProvider>`)

Implement `ProviderType`, `ConvertToProvider`, `ConvertFromProvider` and `ConvertsNulls` (or derive from `ValueConverter<TModel,TProvider>`). Apply via attribute or fluent `HasConversion`.

```csharp declarations
using NextORM.Core;

public sealed class StatusConverter : ValueConverter<Status, string>
{
    public override string? ConvertToProvider(Status model) => model.ToString();
    public override Status ConvertFromProvider(string? provider) =>
        Enum.Parse<Status>(provider!);
}

```

```csharp example
// fluent:
ctx.From<Order>(b => b.Property(x => x.Status).HasConversion(new StatusConverter()));
```

`ConvertToProvider` runs on the write and parameter paths (model → provider, including query parameters); `ConvertFromProvider` runs when reading rows (provider → model). When `ConvertsNulls` is false, a database `NULL` maps to `null` without calling the converter, so a nullable property stays null. Details: [Value converters and JSON columns](../infrastructure/03-value-converters.md).

### Global query filters (`HasQueryFilter`)

Attach a predicate to an entity type; it is applied to every query over that entity. Overloads accept `Expression<Func<T,bool>>`, `Expression<Func<T,IDataContext,bool>>`, named variants and builder-transformer delegates.

```csharp example
ctx.From<Player>(m => m.HasQueryFilter(p => p.IsActive));
```

Filters are applied during normal query preparation. Bypass flags and the full overload list are documented in [Global query filters](query-filters.md).

### Interceptors (`IQueryInterceptor` / `IConnectionInterceptor`)

Observe command execution and connection opening without deriving from `DataContext`. Register on the builder or on a context instance.

```csharp declarations
using System.Data.Common;
using NextORM.Core;

public sealed class TimingInterceptor : IQueryInterceptor
{
    public void CommandExecuted(CommandEventData e, DbCommand cmd, TimeSpan elapsed)
        => Console.WriteLine($"{cmd.CommandText} took {elapsed}");
}

public sealed class AuditConnectionInterceptor : IConnectionInterceptor
{
    public void ConnectionOpened(ConnectionEventData e)
        => Console.WriteLine("Database connection opened.");
}

```

```csharp example
var builder = new DataContextBuilder()
    .UseSqlite(connectionString)
    .AddInterceptor(new TimingInterceptor())
    .AddInterceptor(new AuditConnectionInterceptor());
```

Callbacks fire in registration order: `CommandInitialized` → `CommandExecuting` → `CommandExecuted` or `CommandFailed`; `ConnectionOpening` → `ConnectionOpened`. Both interfaces use default interface methods, so implement only the callbacks you need. The in-memory provider does not raise them. Type-based registration (`AddInterceptor<T>()`, `AddInterceptor(Type)`) resolves the interceptor from the DI scope when the builder is DI-backed; on a standalone builder it fails at context creation. Details: [Interceptors](../infrastructure/02-interceptors.md).

## Extending query construction with query components

A query component lets a third-party package add structural behaviour to query construction without a core change per operator, friend access, a global registry or raw SQL. It carries its own slot identity, preparation/validation logic and structural build handler, and attaches through [`WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent)).

```csharp example
var rows = ctx.From<Player>()
    .ActiveOnly()          // ordinary extension method from the package
    .ToList();
```

### Who implements what, and who calls it

| Type | You | The engine |
|---|---|---|
| `IQueryComponent` | One object per operator instance. | Reads `Slot` on attach and plan-key compare; reads `Contribution`; calls `Prepare` while preparing; calls `BuildQuery` while rendering. |
| `IQueryComponentDescriptor` | Return from `Prepare`; immutable, no mutable component state. | Stores in `QueryCommand.PreparedComponents`; hashes `StructuralHash`; compares with `StructurallyEquals`; drops on preparation reset. |
| `QueryComponentContribution` | Declare once: `AdditivePredicate`, `ExclusiveRowSource` or `ExclusiveGrouping`. | Rejects a second `ExclusiveRowSource` slot and a second distinct `ExclusiveGrouping` owner while preparing, and treats the two exclusive modes as mutually exclusive. Additive predicates are **not** auto-combined: the component must AND them itself (see rules). |
| `IQueryComponentPrepareContext` | Consume in `Prepare`. | Creates over the command being prepared. |
| `IQueryComponentBuildContext` | Consume in `BuildQuery`. | Creates over the rebuilt command. |
| `QueryComponentContext` | Usually never touch; public for direct tests. | Implements both contexts; resolves `ExpressionComparer` from the command. |
| `IQueryComponentShape` | Optional: report scalar state and selectors for the raw recipe cache. | Compares `ShapeState` with `Equals` and selectors with the expression comparer. Without it, the ordinary preparation route. |
| `WithComponent` | Call from your extension method. | Copies the builder; replaces a same-slot component in place. |
| `QueryCommand.Components` / `PreparedComponents` | Read only, for diagnostics. | Owned, published and cleared by the engine. |

### When each call happens

See [SQL lifecycle](#sql-lifecycle-preparation-rendering-and-cache). In short: attach (no validation), `Prepare` (possibly more than once per cached plan), descriptor → plan key, `BuildQuery` (miss only), execute/warm hit (no `BuildQuery`).

### Rules the contract imposes

- **Prepare/validate → immutable descriptor.** Reject unsupported combinations by throwing; return a descriptor carrying slot, concrete component type, contribution, a string `Discriminator`, `StructuralHash` over full state, and `StructurallyEquals`. The descriptor is shared by the cached plan, so it must not reference mutable component state. Captured values must be bound as parameters; any SQL-affecting state must be included in `StructuralHash`, `StructurallyEquals` and (where useful) `Discriminator` — the engine does not compare captured values or runtime state magically.
- **Exclusive reshape on the miss.** Applies to an `ExclusiveRowSource` component: `BuildQuery` receives the command via `IQueryComponentBuildContext` and rebuilds from engine primitives: derived source (`FromExpression`), original select list plus a hidden auxiliary column (`SelectExpression`, `SetComponentSelectList`), an optional rewritten predicate (`SetComponentPreparedCondition`), cleared sorting. It never renders SQL text. An additive component only calls `SetComponentPreparedCondition`.
- **Grouping is declared, not rewritten.** A grouping component returns an `IQueryPreparationDescriptor` carrying a `QueryPreparationContribution`: the ordered grouping-key lambdas, a `QueryPreparationHavingMode` (`Unchanged`, `Clear` or `Replace`) with an optional HAVING lambda, an optional source predicate and `RequireDistinct`. The engine applies it to a separate effective preparation state before column and grouping preparation and never mutates the raw [`QueryCommand`](xref:NextORM.Core.QueryCommand) fields; an active contribution with an empty key vector is an explicit global aggregation. A `Replace` contribution carries only the *added* condition — the engine ANDs the raw source `Having` with it exactly once.
- **Additive predicates are combined by you.** The engine does **not** AND them automatically. Take the existing `LambdaExpression`, reuse its parameter (or create `Expression.Parameter(typeof(Player), "p")`), AND the bodies, then wrap the result in a new lambda: `SetComponentPreparedCondition(Expression.Lambda(Expression.AndAlso(existingBody, predicateBody), param))`. Never `Expression.AndAlso(existingLambda, predicateLambda)`.
- **Slot identity.** `Slot` is the stable identity of a logical operator. Attaching a component whose slot is present replaces it in place; the builder/command never nest two components of one slot. `Min` and `Max` of one family share a slot. At most one `ExclusiveRowSource` slot and one `ExclusiveGrouping` owner may be present; a second distinct exclusive slot or grouping owner is rejected during preparation with `BuildSqlCommandException`, and a grouping owner cannot be combined with a row-source reshape.
- **Hidden-column alias safety.** An auxiliary column must not leak or collide. The component author must choose a unique alias (for example derived from target metadata via `DataContextCache.Metadata`), keep it out of the outer select list, and read it back via `SqlFunctions.Column<T>`. The engine does not automatically guarantee that an arbitrary user column cannot collide; exclude the alias from any outer projection and check metadata.
- **No in-memory support unless you add it.** A SQL-only component rejects `InMemoryDataContext` in `Prepare` with `NotSupportedException`; it must not silently degrade.
- **Shape (optional).** Implement `IQueryComponentShape` to let the raw recipe cache serve your component: report immutable scalar state in `ShapeState` (with stable `Equals`/`GetHashCode`) and selectors via `ShapeSelectorCount`/`GetShapeSelector`. Two components with equal shape state and structurally equal selectors must render the same statement. Without it, the ordinary preparation route.

### Additive component example

A stateless component that adds a constant business rule (`IsActive == true`), AND-ing it with any existing predicate. The descriptor is a record with no mutable state. The component also implements `IQueryComponentShape` for the raw recipe cache. The example is intentionally limited to an ordinary single-entity `From<Player>().Where(...).ActiveOnly()` before joins or projections; a component that reshapes more advanced queries needs its own validation and must not assume generic shape compatibility.

```csharp declarations
using System.Linq.Expressions;
using NextORM.Core;

internal sealed class ActiveOnlyComponent : IQueryComponent, IQueryComponentShape
{
    public Type Slot => typeof(ActiveOnlyComponent);
    public QueryComponentContribution Contribution =>
        QueryComponentContribution.AdditivePredicate;

    public object? ShapeState => typeof(ActiveOnlyComponent);
    public int ShapeSelectorCount => 0;
    public LambdaExpression GetShapeSelector(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index));

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException("ActiveOnly is SQL-only.");
        return new ActiveOnlyDescriptor();
    }

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var existing = cmd.GetComponentPreparedCondition();
        ParameterExpression param;
        Expression? existingBody;
        if (existing is null)
        {
            param = Expression.Parameter(typeof(Player), "p");
            existingBody = null;
        }
        else if (existing is LambdaExpression lambda && lambda.Parameters.Count > 0)
        {
            param = lambda.Parameters[0];
            existingBody = lambda.Body;
        }
        else
        {
            throw new InvalidOperationException("Existing prepared condition must be a lambda with a parameter.");
        }

        var activeBody = Expression.Equal(
            Expression.Property(param, nameof(Player.IsActive)),
            Expression.Constant(true));
        var combined = existingBody is null ? activeBody : Expression.AndAlso(existingBody, activeBody);
        cmd.SetComponentPreparedCondition(Expression.Lambda(combined, param));
    }
}
```

```csharp declarations
internal sealed record ActiveOnlyDescriptor : IQueryComponentDescriptor
{
    public Type Slot => typeof(ActiveOnlyComponent);
    public Type ComponentType => typeof(ActiveOnlyComponent);
    public QueryComponentContribution Contribution =>
        QueryComponentContribution.AdditivePredicate;
    public string Discriminator => "active-only";
    public int StructuralHash => HashCode.Combine(Slot, ComponentType, Contribution, Discriminator);
    public bool StructurallyEquals(IQueryComponentDescriptor? other) =>
        other is ActiveOnlyDescriptor;
}

public static class ActiveOnlyExtensions
{
    public static EntityBuilder<Player> ActiveOnly(this EntityBuilder<Player> source)
        => source.WithComponent(new ActiveOnlyComponent());
}
```

On a warmed plan cache `BuildQuery` is not called; `Prepare` may still run for a new command. On a miss, `IsActive = 1` is AND-ed with any predicate the query already carries.

### Exclusive components

`ExclusiveRowSource` is a contribution mode, not a separate extension point: it declares that the component reshapes the row source instead of adding a predicate. At most one such slot may be present. A shipped example is `SelectWhereMax`/`SelectWhereMin` from the extensions package:

```csharp example
using NextORM.Extensions;

var rows = ctx.From<Player>()
    .SelectWhereMax(p => p.Score)
    .ToList();
```

The package implements the full reshape (derived source, auxiliary column, prepared condition, cleared sorting) on the public component contract. See [Extensions package](extensions-package.md) and [Native extreme-row strategies](select-where-extrema-native.md).

`ExclusiveGrouping` is the sibling exclusive mode: the component owns the grouping of the command and contributes keys and `HAVING` declaratively instead of reshaping rows. `SelectGroup`/`SelectGroupHaving` from the same package are its shipped example:

```csharp example
using NextORM.Core;
using NextORM.Extensions;

var rows = ctx.From<Order>()
    .SelectGroup(o => new { o.CustomerId, Total = SqlFunctions.Sql.sum(o.Amount) })
    .ToList();
```

See [Extensions package § Inferred grouping](extensions-package.md) for the inference rules and the rejection boundaries.

## Advanced: custom SQL dialects and renderers

`ISqlDialect` exposes capabilities; `SqlDialectBase` provides virtual defaults. PostgreSQL and ClickHouse dialects are deliberately not `sealed` and expose public constructors, a shared `Instance` singleton and a virtual `ExtremeRowRenderer`. SQLite, SQL Server and MariaDB dialects are `sealed`.

### Installing a custom dialect

There is no `DataContextBuilder.UseDialect`. `DataContext.Dialect` is abstract; each provider context overrides it. Subclass the provider context and override `Dialect`:

```csharp declarations
using NextORM.Core;
using NextORM.Postgres;

public sealed class MyPostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => MyRenderer.Instance;
}

public sealed class MyPostgresDataContext : PostgresDataContext
{
    private readonly ISqlDialect _dialect = new MyPostgresDialect();

    public MyPostgresDataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder) { }

    public override ISqlDialect Dialect => _dialect;
}

```

```csharp example
using var ctx = new MyPostgresDataContext(connectionString, new DataContextBuilder());
```

The plan cache is keyed by context type, so derive a distinct `DataContext` subclass per dialect. The same shape works for `MyClickHouseDataContext : ClickHouseDataContext`.

### Native extreme-row renderer (specialized, legacy operators)

`IExtremeRowRenderer` is the native strategy hook for the **legacy** operators `SelectWhereMaxOld`/`SelectWhereMinOld`. The recommended `SelectWhereMax`/`SelectWhereMin` from `nextorm.extensions` are built on query components and do **not** call this renderer. The hook remains supported for the legacy path.

`CanRender(ExtremeRowDescription)` is a side-effect-free decision taken before any SQL is assembled; only a positive answer leads to `Render(ExtremeRowRenderRequest)`, which returns the winning-row source. A `null` renderer (or `CanRender == false`) keeps the portable window-function lowering. `Render` has no late fallback: a rendering failure propagates. Callbacks occur during SQL generation, not on every reuse of cached SQL; a parameter-refresh re-render may invoke them again. The built-in native path is only eligible for the `ExtremeRowTies.One` form over a mapped entity shape; other tie modes stay on the portable lowering.

A decorator that adds instrumentation without writing SQL:

```csharp declarations
using System;
using NextORM.Core;
using NextORM.Postgres;

public sealed class MyRenderer : IExtremeRowRenderer
{
    public static readonly MyRenderer Instance = new();

    private readonly IExtremeRowRenderer? _inner =
        PostgresDialect.Instance.ExtremeRowRenderer;

    public bool CanRender(ExtremeRowDescription description)
        => _inner?.CanRender(description) ?? false;

    public string Render(ExtremeRowRenderRequest request)
    {
        if (_inner is null)
            throw new NotSupportedException("No inner renderer.");
        Console.WriteLine("Rendering extreme row natively.");
        return _inner.Render(request);
    }
}
```

The three DTOs (`ExtremeRowRenderColumn`, `ExtremeRowDescription`, `ExtremeRowRenderRequest`) carry only shape facts (CLR type, nullability, direct-mapped/converter flags, prepared source/aliases), never the expression or build context. They expose public eager constructors for assemblies without friend access; collections are borrowed, not copied. Built-in renderers (`PostgresExtremeRowRenderer`, `ClickHouseExtremeRowRenderer`) are `internal` and are not part of the contract.

### Other dialect capabilities

`SqlDialectBase` exposes many virtual members (`Iif`, `MultiIf`, `Tuple`, `Lock`, `DistinctOn`, `LimitBy`, `IndexHints`, `StringSplit`, `ArrayJoin`, `DateConversion`, `Pivot`, aggregate renderers, ...). They are public for provider implementations; the documented external compatibility promise covers the extreme-row renderer on PostgreSQL and ClickHouse. Override them only when deriving a dialect for a new provider.

## Limitations

- No public translator registry and no materializer-plugin registration.
- Sealed dialects (`SqliteDialect`, `SqlServerDialect`, `MariaDbDialect`) cannot be subclassed; `MySqlDialect` is the extension point for MariaDB.
- Components have no in-memory support unless implemented explicitly; exclusive row-source components inherit the modifier compatibility gate.
- `Render` has no late fallback to the portable path.
- Interceptors are not raised by the in-memory provider.
- The plan cache is keyed by context type; one dialect per context subclass.

## See also

- [Native extreme-row strategies](select-where-extrema-native.md)
- [Extensions package](extensions-package.md)
- [User-defined functions](../guide/12-user-defined-functions.md)
- [Entities and metadata](../getting-started/03-entities-and-metadata.md)
- [Value converters and JSON columns](../infrastructure/03-value-converters.md)
- [Interceptors](../infrastructure/02-interceptors.md)
- [Global query filters](query-filters.md)
- [API quick reference](api-reference.md)
- [Provider overview](../connecting-providers/overview.md)
