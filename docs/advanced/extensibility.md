# Extensibility: custom SQL dialects and renderers

> nextorm renders SQL through an [`ISqlDialect`](xref:NextORM.Core.ISqlDialect); the supported extension boundary lets an external assembly supply its own dialect, for the native extreme-row strategy its own renderer, or a query component that adds structural behaviour to query construction — without friend access.

**Prerequisites:** [Native extreme-row strategies (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md) · [Provider overview](../connecting-providers/overview.md) · [API quick reference](api-reference.md)

## Overview

The native extreme-row strategy is a public dialect capability, not a hard-coded provider branch:

- [`ISqlDialect.ExtremeRowRenderer`](xref:NextORM.Core.ISqlDialect.ExtremeRowRenderer) is the optional renderer, declared as a default interface member returning `null`; [`SqlDialectBase.ExtremeRowRenderer`](xref:NextORM.Core.SqlDialectBase.ExtremeRowRenderer) overrides it virtually with the same default. `null` means no native strategy.
- [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) exposes `CanRender(`[`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription)`)`, a side-effect-free decision taken before any SQL is assembled, and `Render(`[`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest)`)`, which returns the winning-row source. Only a positive `CanRender` leads to `Render`, and `Render` has no late fallback to the portable path.
- [`ExtremeRowRenderColumn`](xref:NextORM.Core.ExtremeRowRenderColumn), [`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription) and [`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest) carry only shape facts (CLR type, nullability, direct-mapped/converter flags, and the prepared source/aliases), never the expression or build context. See the [API quick reference](api-reference.md).
- Each of the three DTOs also exposes a **public eager constructor**, so an assembly without friend access can build them and drive its own [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer). Construction validates references and collection elements immediately and structurally: a required reference that is `null` throws `ArgumentNullException`; a `null` element, an empty-string alias element or a `SourceSql` that is empty/whitespace-only throws `ArgumentException`; an undeclared `KeywordCase` value throws `ArgumentException`. It performs **no** SQL parsing or provider-type/security validation, adds no minimum-count, cross-list cardinality or alias-disjointness rule (empty shapes and overlapping aliases are allowed), never reads `ExtremeRowDescription.Payload`, and never calls `CanRender`/`Render`. The collections are **borrowed**, not copied and not made immutable: the same instance is observed through the property, and a caller must not mutate it while the DTO is in use, including from a concurrent caller. The core's internal lazy payload/candidate-preparation path (and the unvalidated trusted request construction used on the hot path) remains an implementation detail and is **not** public API.

`PostgresDialect` and `ClickHouseDialect` are deliberately **not `sealed`**: keeping them inheritable, together with their public constructors (`PostgresDialect()`, `PostgresDialect(Version?)`, `ClickHouseDialect()`), the shared `Instance` singleton and the virtual `ExtremeRowRenderer` hook, is the supported external extension boundary — a public compatibility commitment, not a test detail. A custom dialect can derive from either provider dialect, override the hook and install its own public [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) implementation; the built-in renderers (`PostgresExtremeRowRenderer`, `ClickHouseExtremeRowRenderer`) are `internal` and are not part of that contract. Returning `null` from the override — or installing a renderer whose `CanRender` answers `false` — keeps the portable window-function lowering, and the provider defaults and constructors are unchanged.

## Creating a custom dialect

```csharp
// A non-friend assembly subclasses the provider dialect and installs a custom renderer.
public sealed class MyPostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => MyRenderer.Instance;
}

// Only the global (ungrouped) single-key form is handled natively; every other shape
// falls back to the portable lowering because CanRender answers false.
public sealed class MyRenderer : IExtremeRowRenderer
{
    public static readonly MyRenderer Instance = new();

    public bool CanRender(ExtremeRowDescription description)
        => description.Groups.Count == 0 && description.Keys.Count == 1;

    public string Render(ExtremeRowRenderRequest request)
    {
        var direction = request.IsMax ? "desc" : "asc";
        return request.SourceSql
            + " order by \"" + request.KeyAliases[0] + "\" " + direction + " limit 1";
    }
}

// The second example: returning null from the override forces the portable window-function
// lowering for every request, without installing a renderer.
public sealed class PortablePostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => null;
}
```

## Registering a custom dialect

Creating the dialect subclass is only half of it — the context has to be told to use it. [`DataContext.Dialect`](xref:NextORM.Core.DataContext.Dialect) is an **`abstract`** property (`src/nextorm.core/DataContext/DataContext.cs:153`), and every provider context overrides it to return its built-in dialect: `PostgresDataContext` and `MariaDbDataContext` return the instance they built at construction, while `SqliteDataContext`, `MySqlDataContext`, `SqlServerDataContext` and `ClickHouseDataContext` return a shared `Instance` singleton. There is no `DataContextBuilder`/`UseDialect` switch; the supported way to install a custom dialect is to subclass the **provider context** and override `Dialect` to return your dialect:

```csharp
public sealed class MyPostgresDataContext : PostgresDataContext
{
    private readonly ISqlDialect _dialect = new MyPostgresDialect();

    public MyPostgresDataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder)
    {
    }

    public override ISqlDialect Dialect => _dialect;
}

using var ctx = new MyPostgresDataContext(connectionString, new DataContextBuilder());

// Reaches the database through MyPostgresDialect → MyRenderer.
var top = ctx.From<Player>().SelectWhereMax(p => p.Score).ToList();
```

The same shape works for `MyClickHouseDataContext : ClickHouseDataContext` (`ClickHouseDialect` is likewise not `sealed`). The plan cache is keyed by the **context type**, so derive a distinct `DataContext` subclass per dialect — the same rule the PostgreSQL server-version guard already states. Installing a dialect that returns `null` from `ExtremeRowRenderer` (or a renderer whose `CanRender` answers `false`) keeps the portable window-function lowering.

## Extending query construction with query components

A **query component** lets a third-party package add structural behaviour to query construction without a core
change per operator, friend access, a global registry or raw SQL. A component carries its own slot identity,
its preparation/validation logic and its structural build handler, and attaches to an immutable builder through
[`WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent)):

```csharp
var top = ctx.From<Player>()
    .ExternalSelectWhereMin(p => p.Score)   // an ordinary extension method from the package
    .ToList();
```

The public contract is five small types: [`IQueryComponent`](xref:NextORM.Core.IQueryComponent) (slot,
contribution kind, `Prepare`, `BuildQuery`), [`IQueryComponentDescriptor`](xref:NextORM.Core.IQueryComponentDescriptor)
(the immutable prepared state), [`QueryComponentContribution`](xref:NextORM.Core.QueryComponentContribution)
(`AdditivePredicate` or `ExclusiveRowSource`) and the contexts
[`IQueryComponentPrepareContext`](xref:NextORM.Core.IQueryComponentPrepareContext) /
[`IQueryComponentBuildContext`](xref:NextORM.Core.IQueryComponentBuildContext), which the engine instantiates as
[`QueryComponentContext`](xref:NextORM.Core.QueryComponentContext). The command exposes the attached components
as [`QueryCommand.Components`](xref:NextORM.Core.QueryCommand.Components) and the prepared descriptors as
[`QueryCommand.PreparedComponents`](xref:NextORM.Core.QueryCommand.PreparedComponents); both are engine-managed
and a component author normally never touches them.

- **Prepare/validate → an immutable prepared descriptor.** `Prepare` runs once per plan build against the
  command being prepared. It validates the combination — an unsupported one throws instead of being composed
  arbitrarily — and returns a descriptor carrying the slot, the concrete component type, the contribution kind,
  a semantic `Discriminator`, a `StructuralHash` over the full state and `StructurallyEquals`. The descriptor is
  immutable and shared by the cached plan, so it must not reference mutable component state.
- **Structural construction on a cache miss only.** `BuildQuery` is called when the plan is created. It receives
  the command through `IQueryComponentBuildContext` and rebuilds the statement from engine primitives: a derived
  source (`FromExpression`), the original select list plus a hidden auxiliary column (`SelectExpression`,
  `SetComponentSelectList`), an optional rewritten predicate (`SetComponentPreparedCondition`) and cleared
  sorting. On a confirmed **warm cache hit** the engine returns the cached plan, so `BuildQuery` is not called
  again and the SQL is not rebuilt; captured values and positional parameters are re-bound by the normal
  execution path against the original, unmodified command.
- **Slot identity.** `Slot` is the stable identity of a logical operator. Attaching a component whose slot is
  already present **replaces it in place**; the builder and the command never nest two components of one slot.
  `Min` and `Max` of the same operator family therefore share a slot and replace each other. `AdditivePredicate`
  components compose — each contributes a predicate combined with `AND` — while at most **one**
  `ExclusiveRowSource` slot may be present: two distinct exclusive slots are rejected during preparation with a
  [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException).
- **Hidden-column collision safety.** An auxiliary column must neither leak into the result nor collide with a
  mapped column. Derive its alias from the target metadata ([`DataContextCache.Metadata`](xref:NextORM.Core.DataContextCache.Metadata)),
  keep it out of the outer select list and read it back through the typed
  [`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)) primitive, so
  a user column named `rn`, `rank` or `__nextorm_ext_rn` can neither collide nor surface.
- **No in-memory support.** A component whose semantics are SQL-only rejects `InMemoryDataContext` in `Prepare`
  with `NotSupportedException`; it must not silently degrade or return different rows.

A complete `SelectWhereMin`-style operator — derived source, window, outer `rank = 1`, original result shape
restored:

```csharp
using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace Acme.NextOrm;

// 1. Public surface: ordinary extension methods. No registration, no core change, no raw SQL.
public enum ExternalExtremeKind { Min, Max }

public static class ExternalExtremeExtensions
{
    public static EntityBuilder<TEntity> ExternalSelectWhereMin<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(
            new ExternalExtremeComponent<TEntity>(ExternalExtremeKind.Min, valueSelector, ties, groupBy));

    public static EntityBuilder<TEntity> ExternalSelectWhereMax<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(
            new ExternalExtremeComponent<TEntity>(ExternalExtremeKind.Max, valueSelector, ties, groupBy));
}

// 2. The engine primitives the component emits into its expression tree, resolved once per process.
internal static class EngineHandles
{
    public static readonly MethodInfo Asc =
        typeof(CommonFunctions).GetMethod("asc", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo Desc =
        typeof(CommonFunctions).GetMethod("desc", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo RowNumber =
        typeof(CommonFunctions).GetMethod("row_number", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo Rank =
        typeof(CommonFunctions).GetMethod("rank", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo ColumnOfInt =
        typeof(SqlFunctions).GetMethod("Column", BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int));
    public static readonly MethodInfo Over = typeof(WindowFunction<int>).GetMethod("Over",
        new[] { typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame) })!;
}

// 3. The component: slot identity, validation, prepared descriptor, structural build.
public sealed class ExternalExtremeComponent<TEntity> : IQueryComponent
{
    private readonly ExternalExtremeKind _kind;
    private readonly LambdaExpression _valueSelector;
    private readonly ExtremeRowTies _ties;
    private readonly Expression<Func<TEntity, object?>>? _groupBy;

    public ExternalExtremeComponent(ExternalExtremeKind kind, LambdaExpression valueSelector,
        ExtremeRowTies ties, Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        _kind = kind;
        _valueSelector = valueSelector;
        _ties = ties;
        _groupBy = groupBy;
    }

    // Min and Max declare the same slot on purpose: attaching one after the other replaces it in
    // place instead of nesting. A second, different exclusive slot is rejected by the engine.
    public Type Slot => typeof(ExternalExtremeComponent<TEntity>);
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException(
                "ExternalSelectWhereMin/Max is not supported by the in-memory provider.");
        if (_ties is not (ExtremeRowTies.One or ExtremeRowTies.All))
            throw new ArgumentOutOfRangeException(nameof(_ties), _ties, "Unknown ExtremeRowTies value.");

        // Operator-specific shape checks belong here: reject what the operator cannot express
        // (for example paging or HAVING) instead of composing arbitrarily.

        var hash = new HashCode();
        hash.Add(Slot);
        hash.Add(GetType());
        hash.Add(_kind);
        hash.Add(_ties);
        hash.Add(_valueSelector, context.ExpressionComparer);
        if (_groupBy is not null)
            hash.Add(_groupBy, context.ExpressionComparer);

        return new Descriptor(this, hash.ToHashCode(), context.ExpressionComparer);
    }

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var rankAlias = PickFreeAlias(entityType);
        var entity = Expression.Parameter(entityType, "x");
        var sql = Expression.Property(null, typeof(SqlFunctions), "Sql");

        // 4. Derived source: the original command, its select list plus one hidden rank column.
        var inner = cmd.Clone();
        inner.Components = null;

        var direction = _kind == ExternalExtremeKind.Max ? EngineHandles.Desc : EngineHandles.Asc;
        var orderKeys = Expression.NewArrayInit(typeof(WindowOrder),
            Decompose(_valueSelector, entity).Select(v => (Expression)Expression.Call(sql, direction,
                Expression.Quote(Expression.Lambda<Func<object?>>(
                    Expression.Convert(v, typeof(object)))))).ToArray());

        Expression partition = _groupBy is null
            ? Expression.Constant(null, typeof(Expression<Func<object?>>[]))
            : Expression.NewArrayInit(typeof(Expression<Func<object?>>),
                Decompose(_groupBy, entity).Select(k => (Expression)Expression.Quote(
                    Expression.Lambda<Func<object?>>(Expression.Convert(k, typeof(object))))).ToArray());

        var rankMethod = _ties == ExtremeRowTies.All ? EngineHandles.Rank : EngineHandles.RowNumber;
        var rank = Expression.Call(Expression.Call(sql, rankMethod), EngineHandles.Over,
            partition, orderKeys, Expression.Constant(null, typeof(WindowFrame)));

        var hidden = new SelectExpression(typeof(int))
        {
            Index = inner.SelectList?.Length ?? 0,
            PropertyName = rankAlias,
            Expression = rank,
        };
        inner.SetComponentSelectList(inner.SelectList is null
            ? new[] { hidden }
            : inner.SelectList.Append(hidden).ToArray());
        inner.SetComponentSorting(null);

        // 5. Outer statement: read the derived source, keep rank = 1, restore the original shape.
        cmd.From = new FromExpression(inner);
        var outer = Expression.Parameter(entityType, "o");
        var rankColumn = Expression.Call(null, EngineHandles.ColumnOfInt, outer,
            Expression.Constant(rankAlias));
        cmd.SetComponentPreparedCondition(Expression.Lambda(
            Expression.Equal(rankColumn, Expression.Constant(1)), outer));
    }

    private static Expression[] Decompose(LambdaExpression selector, ParameterExpression entity)
    {
        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } boxed && boxed.Type == typeof(object))
            body = boxed.Operand;
        var rewritten = new ParameterReplacer(selector.Parameters[0], entity).Visit(body)!;
        // Anonymous types and ValueTuple contribute one key per argument, not one opaque key.
        return rewritten is NewExpression { Arguments.Count: > 0 } composite
            ? composite.Arguments.ToArray()
            : new[] { rewritten };
    }

    private static string PickFreeAlias(Type entityType)
    {
        const string seed = "__nextorm_ext_rn";
        if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return seed;
        var name = seed;
        while (metadata.Properties.Any(p =>
                   string.Equals(p.ColumnName, name, StringComparison.OrdinalIgnoreCase)))
            name += "_";
        return name;
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }

    private sealed class Descriptor : IQueryComponentDescriptor
    {
        private readonly LambdaExpression _valueSelector;
        private readonly Expression? _groupBy;
        private readonly ExtremeRowTies _ties;
        private readonly IEqualityComparer<Expression> _comparer;

        public Descriptor(ExternalExtremeComponent<TEntity> component, int hash,
            IEqualityComparer<Expression> comparer)
        {
            Slot = component.Slot;
            ComponentType = component.GetType();
            Contribution = component.Contribution;
            Discriminator = component._kind.ToString();
            _valueSelector = component._valueSelector;
            _groupBy = component._groupBy;
            _ties = component._ties;
            _comparer = comparer;
            StructuralHash = hash;
        }

        public Type Slot { get; }
        public Type ComponentType { get; }
        public QueryComponentContribution Contribution { get; }
        public string Discriminator { get; }
        public int StructuralHash { get; }

        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is Descriptor rhs
               && Slot == rhs.Slot && ComponentType == rhs.ComponentType
               && Contribution == rhs.Contribution
               && Discriminator == rhs.Discriminator && _ties == rhs._ties
               && _comparer.Equals(_valueSelector, rhs._valueSelector)
               && (_groupBy is null
                   ? rhs._groupBy is null
                   : rhs._groupBy is not null && _comparer.Equals(_groupBy, rhs._groupBy));
    }
}
```

The engine owns quoting, aliases and parameter binding; the component only assembles the logical tree, so the
same operator renders on every SQL provider without dialect-specific code. The same operator — both directions,
`One`/`All`, composite keys, NULL-value exclusion and the compatibility checks — is exercised end to end by the
repository's query-component proof suite.

## See also

- [Native extreme-row strategies (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md)
- [API quick reference](api-reference.md)
- [Provider overview](../connecting-providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/**`, `src/nextorm.core/Query/QueryComponents.cs`, `src/nextorm.postgres/**`, `src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
