using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Canonical key of a raw description: a structural hash plus the description itself, so a hash
/// collision is resolved by structural equality instead of being trusted.
/// </summary>
/// <remarks>
/// The key is derived from the raw description only. Context/configuration state joins the key at
/// materialization, not here; the description must never read prepared-plan fields back.
/// </remarks>
internal readonly struct RawQueryKey : IEquatable<RawQueryKey>
{
    public RawQueryKey(in RawQueryState state, in RawQueryContext context, int hash)
    {
        State = state;
        Context = context;
        Hash = hash;
    }

    /// <summary>The immutable raw description the key was derived from.</summary>
    public RawQueryState State { get; }

    /// <summary>The materialization-time provider/configuration identity.</summary>
    public RawQueryContext Context { get; }

    /// <summary>The structural hash of <see cref="State"/> combined with <see cref="Context"/>.</summary>
    public int Hash { get; }

    /// <inheritdoc />
    public bool Equals(RawQueryKey other) =>
        Hash == other.Hash
        && Context.Equals(other.Context)
        && RawQueryStateEquality.Equals(State, other.State);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RawQueryKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Hash;
}

/// <summary>Cache-facing comparer over <see cref="RawQueryKey"/>.</summary>
internal sealed class RawQueryKeyComparer : IEqualityComparer<RawQueryKey>
{
    /// <summary>The shared comparer instance.</summary>
    public static readonly RawQueryKeyComparer Instance = new();

    /// <inheritdoc />
    public bool Equals(RawQueryKey x, RawQueryKey y) => x.Equals(y);

    /// <inheritdoc />
    public int GetHashCode(RawQueryKey obj) => obj.Hash;
}

/// <summary>
/// Structural equality and hashing of a raw description. Every dimension that can change the
/// rendered SQL or the materialized result participates; nothing prepared or execution-scoped does.
/// </summary>
internal static class RawQueryStateEquality
{
    private static readonly RawQueryRegistry Registry = new();
    private static readonly ExpressionPlanEqualityComparer Expressions = Registry.GetExpressionPlanEqualityComparer();

    /// <summary>
    /// Derives the canonical key of <paramref name="state"/> under the execution identity of
    /// <paramref name="context"/>. Returns <see langword="false"/> when the description contains a
    /// shape the raw key cannot represent (a nested command or an outer reference); the caller must
    /// then fail closed and compile without the native plan path instead of keying a partial
    /// description.
    /// </summary>
    public static bool TryCreateKey(in RawQueryState state, in RawQueryContext context, out RawQueryKey key)
    {
        if (!state.HasSource)
        {
            key = default;
            return false;
        }

        try
        {
            key = new RawQueryKey(state, context, GetHashCode(state, context));
            return true;
        }
        catch (NotSupportedException)
        {
            key = default;
            return false;
        }
    }

    /// <summary>Structural equality of two raw descriptions.</summary>
    public static bool Equals(in RawQueryState x, in RawQueryState y)
    {
        if (x.Source.Kind != y.Source.Kind) return false;
        if (!SourceEquals(x.Source, y.Source)) return false;
        if (x.EntityType != y.EntityType) return false;
        if (x.ResultType != y.ResultType) return false;
        if (!Expressions.Equals(x.Projection, y.Projection)) return false;
        if (!ExpressionArraysEqual(x.Conditions, y.Conditions)) return false;
        if (!JoinsEqual(x.Joins, y.Joins)) return false;
        if (!OrderingEqual(x.Ordering, y.Ordering)) return false;
        if (!PagingEqual(x.Paging, y.Paging)) return false;
        if (!SetOperationEqual(x.SetOperation, y.SetOperation)) return false;
        if (!CtesEqual(x.Ctes, y.Ctes)) return false;
        if (x.Flags != y.Flags) return false;
        if (!string.Equals(x.Tag, y.Tag, StringComparison.Ordinal)) return false;
        if (!StringArraysEqual(x.Hints, y.Hints)) return false;
        if (!ReferenceEquals(x.FilterScope, y.FilterScope)) return false;

        return true;
    }

    /// <summary>Structural hash of a raw description combined with its execution identity.</summary>
    public static int GetHashCode(in RawQueryState state, in RawQueryContext context)
    {
        var hash = new HashCode();
        hash.Add(HashState(state));
        hash.Add(context.GetHashCode());
        return hash.ToHashCode();
    }

    private static int HashState(in RawQueryState state)
    {
        var hash = new HashCode();

        hash.Add((int)state.Source.Kind);
        AddSource(ref hash, state.Source);
        hash.Add(state.EntityType);
        hash.Add(state.ResultType);
        AddExpression(ref hash, state.Projection);
        AddExpressions(ref hash, state.Conditions);
        AddJoins(ref hash, state.Joins);
        AddOrdering(ref hash, state.Ordering);
        hash.Add(state.Paging.Limit);
        hash.Add(state.Paging.Offset);
        hash.Add(state.Paging.HasWithTies);
        AddSetOperation(ref hash, state.SetOperation);
        AddCtes(ref hash, state.Ctes);
        hash.Add((int)state.Flags);
        hash.Add(state.Tag, StringComparer.Ordinal);
        AddStrings(ref hash, state.Hints);
        hash.Add(state.FilterScope is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state.FilterScope));

        return hash.ToHashCode();
    }

    private static bool SourceEquals(in RawSource x, in RawSource y) =>
        x.Kind switch
        {
            RawSourceKind.Entity => x.EntityType == y.EntityType,
            RawSourceKind.Table => string.Equals(x.Table, y.Table, StringComparison.Ordinal),
            RawSourceKind.Derived => x.Derived is not null && y.Derived is not null
                && Equals(x.Derived.State, y.Derived.State),
            _ => true,
        };

    private static void AddSource(ref HashCode hash, in RawSource source)
    {
        switch (source.Kind)
        {
            case RawSourceKind.Entity:
                hash.Add(source.EntityType);
                break;
            case RawSourceKind.Table:
                hash.Add(source.Table, StringComparer.Ordinal);
                break;
            case RawSourceKind.Derived:
                hash.Add(source.Derived is null ? 0 : HashState(source.Derived.State));
                break;
        }
    }

    private static bool ExpressionArraysEqual(LambdaExpression[]? x, LambdaExpression[]? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Length != y.Length) return false;
        for (var i = 0; i < x.Length; i++)
        {
            if (!Expressions.Equals(x[i], y[i])) return false;
        }

        return true;
    }

    private static void AddExpressions(ref HashCode hash, LambdaExpression[]? expressions)
    {
        if (expressions is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(expressions.Length);
        foreach (var expression in expressions)
        {
            AddExpression(ref hash, expression);
        }
    }

    private static bool JoinsEqual(RawJoin[]? x, RawJoin[]? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Length != y.Length) return false;
        for (var i = 0; i < x.Length; i++)
        {
            if (x[i].JoinType != y[i].JoinType) return false;
            if (x[i].Strictness != y[i].Strictness) return false;
            if (x[i].IsGlobal != y[i].IsGlobal) return false;
            if (!Expressions.Equals(x[i].Condition, y[i].Condition)) return false;
            if (!SourceEquals(x[i].Right, y[i].Right)) return false;
        }

        return true;
    }

    private static void AddJoins(ref HashCode hash, RawJoin[]? joins)
    {
        if (joins is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(joins.Length);
        foreach (var join in joins)
        {
            hash.Add((int)join.JoinType);
            hash.Add((int)join.Strictness);
            hash.Add(join.IsGlobal);
            AddExpression(ref hash, join.Condition);
            AddSource(ref hash, join.Right);
        }
    }

    private static bool OrderingEqual(RawOrdering[]? x, RawOrdering[]? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Length != y.Length) return false;
        for (var i = 0; i < x.Length; i++)
        {
            if (x[i].Direction != y[i].Direction) return false;
            if (x[i].NullsPosition != y[i].NullsPosition) return false;
            if (!Expressions.Equals(x[i].Expression, y[i].Expression)) return false;
        }

        return true;
    }

    private static void AddOrdering(ref HashCode hash, RawOrdering[]? ordering)
    {
        if (ordering is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(ordering.Length);
        foreach (var key in ordering)
        {
            hash.Add((int)key.Direction);
            hash.Add((int)key.NullsPosition);
            AddExpression(ref hash, key.Expression);
        }
    }

    private static bool PagingEqual(in Paging x, in Paging y) =>
        x.Limit == y.Limit && x.Offset == y.Offset && x.HasWithTies == y.HasWithTies;

    private static bool SetOperationEqual(RawSetOperation? x, RawSetOperation? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Value.Kind != y.Value.Kind) return false;
        if (x.Value.Kind == UnionType.None) return true;

        return Equals(x.Value.Right.State, y.Value.Right.State);
    }

    private static void AddSetOperation(ref HashCode hash, RawSetOperation? setOperation)
    {
        if (setOperation is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add((int)setOperation.Value.Kind);
        hash.Add(HashState(setOperation.Value.Right.State));
    }

    private static bool CtesEqual(RawCteDefinition[]? x, RawCteDefinition[]? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Length != y.Length) return false;
        for (var i = 0; i < x.Length; i++)
        {
            if (!string.Equals(x[i].Name, y[i].Name, StringComparison.Ordinal)) return false;
            if (x[i].Recursive != y[i].Recursive) return false;
            if (x[i].MaxRecursion != y[i].MaxRecursion) return false;
            if (!Equals(x[i].Body.State, y[i].Body.State)) return false;
        }

        return true;
    }

    private static void AddCtes(ref HashCode hash, RawCteDefinition[]? ctes)
    {
        if (ctes is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(ctes.Length);
        foreach (var cte in ctes)
        {
            hash.Add(cte.Name, StringComparer.Ordinal);
            hash.Add(cte.Recursive);
            hash.Add(cte.MaxRecursion);
            hash.Add(HashState(cte.Body.State));
        }
    }

    private static bool StringArraysEqual(string[]? x, string[]? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Length != y.Length) return false;
        for (var i = 0; i < x.Length; i++)
        {
            if (!string.Equals(x[i], y[i], StringComparison.Ordinal)) return false;
        }

        return true;
    }

    private static void AddStrings(ref HashCode hash, string[]? values)
    {
        if (values is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(values.Length);
        foreach (var value in values)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
    }

    private static void AddExpression(ref HashCode hash, Expression? expression)
    {
        hash.Add(expression is null ? 0 : Expressions.GetHashCode(expression));
    }
}

/// <summary>
/// Registry used by the raw key's expression comparers. A raw description never owns nested
/// commands or outer references in the shapes the key represents; asking for one means the shape is
/// outside the native key, so the registry fails closed and the caller compiles without it.
/// </summary>
internal sealed class RawQueryRegistry : IQueryRegistry
{
    private readonly ExpressionPlanEqualityComparer _expression;
    private readonly SelectExpressionPlanEqualityComparer _select;
    private readonly FromExpressionPlanEqualityComparer _from;
    private readonly JoinExpressionPlanEqualityComparer _join;
    private readonly SortingExpressionPlanEqualityComparer _sorting;
    private readonly QueryPlanEqualityComparer _query;

    public RawQueryRegistry()
    {
        _expression = new ExpressionPlanEqualityComparer(this);
        _select = new SelectExpressionPlanEqualityComparer(this);
        _from = new FromExpressionPlanEqualityComparer(this);
        _join = new JoinExpressionPlanEqualityComparer(this);
        _sorting = new SortingExpressionPlanEqualityComparer(this);
        _query = new QueryPlanEqualityComparer(this);
    }

    /// <inheritdoc />
    public int AddCommand(QueryCommand cmd) => throw new NotSupportedException(
        "A raw description does not key nested query commands; the query must compile without the native plan path.");

    /// <inheritdoc />
    public IReadOnlyList<QueryCommand> ReferencedQueries => [];

    /// <inheritdoc />
    public IReadOnlyList<Expression>? OuterReferences => null;

    /// <inheritdoc />
    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => _query;

    /// <inheritdoc />
    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => _expression;

    /// <inheritdoc />
    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => _select;

    /// <inheritdoc />
    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => _from;

    /// <inheritdoc />
    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => _join;

    /// <inheritdoc />
    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => _sorting;

    /// <inheritdoc />
    public int AddOuterReference(Expression node) => throw new NotSupportedException(
        "A raw description does not key outer references; the correlated query must compile without the native plan path.");
}
