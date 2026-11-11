using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Compact value handle over an immutable <see cref="RawQueryState"/>: the raw description a fluent
/// operator produces before anything is keyed, translated or rendered.
/// </summary>
/// <remarks>
/// Every operator returns a new handle with a copied state; the state the receiver holds is never
/// mutated, so two branches built from one handle stay independent and a shared array is never
/// written after publication.
/// </remarks>
internal readonly struct RawQuery<TResult>
{
    private readonly RawQueryState _state;

    public RawQuery(in RawQueryState state) => _state = state;

    /// <summary>The immutable raw description.</summary>
    public RawQueryState State => _state;

    /// <summary>The projected result type.</summary>
    public Type ResultType => typeof(TResult);

    /// <summary>Appends a <c>WHERE</c> predicate.</summary>
    public RawQuery<TResult> Where(Expression<Func<TResult, bool>> condition) =>
        new(_state.WithCondition(condition));

    /// <summary>Projects each element.</summary>
    public RawQuery<TNew> Select<TNew>(Expression<Func<TResult, TNew>> projection) =>
        new(_state.WithProjection(projection, typeof(TNew)));

    /// <summary>Adds a join to another raw source.</summary>
    public RawQuery<TResult> Join<TRight>(
        RawQuery<TRight> right,
        Expression<Func<TResult, TRight, bool>> condition,
        JoinType joinType = JoinType.Inner,
        JoinStrictness strictness = JoinStrictness.Default) =>
        new(_state.WithJoin(new RawJoin(joinType, strictness, condition, right.State.Source)));

    /// <summary>Adds an <c>ORDER BY</c> key (ascending by default).</summary>
    public RawQuery<TResult> OrderBy<TKey>(
        Expression<Func<TResult, TKey>> key,
        OrderDirection direction = OrderDirection.Asc,
        NullsPosition nullsPosition = NullsPosition.Default) =>
        new(_state.WithOrdering(new RawOrdering(key, direction, nullsPosition)));

    /// <summary>Adds a further <c>ORDER BY</c> key.</summary>
    public RawQuery<TResult> ThenBy<TKey>(
        Expression<Func<TResult, TKey>> key,
        OrderDirection direction = OrderDirection.Asc,
        NullsPosition nullsPosition = NullsPosition.Default) =>
        new(_state.WithOrdering(new RawOrdering(key, direction, nullsPosition)));

    /// <summary>Applies <c>LIMIT</c>.</summary>
    public RawQuery<TResult> Take(int count)
    {
        var paging = _state.Paging;
        paging.Limit = count;
        return new(_state.WithPaging(paging));
    }

    /// <summary>Applies <c>OFFSET</c>.</summary>
    public RawQuery<TResult> Skip(int count)
    {
        var paging = _state.Paging;
        paging.Offset = count;
        return new(_state.WithPaging(paging));
    }

    /// <summary>Applies <c>DISTINCT</c>.</summary>
    public RawQuery<TResult> Distinct() => new(_state.WithFlags(RawQueryFlags.Distinct));

    /// <summary>Combines with <paramref name="right"/> through <c>UNION</c>.</summary>
    public RawQuery<TResult> Union<T>(RawQuery<T> right) => SetOperation(UnionType.Distinct, right);

    /// <summary>Combines with <paramref name="right"/> through <c>UNION ALL</c>.</summary>
    public RawQuery<TResult> UnionAll<T>(RawQuery<T> right) => SetOperation(UnionType.All, right);

    /// <summary>Combines with <paramref name="right"/> through <c>INTERSECT</c>.</summary>
    public RawQuery<TResult> Intersect<T>(RawQuery<T> right) => SetOperation(UnionType.Intersect, right);

    /// <summary>Combines with <paramref name="right"/> through <c>INTERSECT ALL</c>.</summary>
    public RawQuery<TResult> IntersectAll<T>(RawQuery<T> right) => SetOperation(UnionType.IntersectAll, right);

    /// <summary>Combines with <paramref name="right"/> through <c>EXCEPT</c>.</summary>
    public RawQuery<TResult> Except<T>(RawQuery<T> right) => SetOperation(UnionType.Except, right);

    /// <summary>Combines with <paramref name="right"/> through <c>EXCEPT ALL</c>.</summary>
    public RawQuery<TResult> ExceptAll<T>(RawQuery<T> right) => SetOperation(UnionType.ExceptAll, right);

    private RawQuery<TResult> SetOperation<T>(UnionType kind, RawQuery<T> right) =>
        new(_state.WithSetOperation(kind, right.State));
}

/// <summary>Entry points for raw descriptions without a data context.</summary>
internal static class RawQueryFactory
{
    /// <summary>Reads a mapped entity type.</summary>
    public static RawQuery<TEntity> From<TEntity>() =>
        new(RawQueryState.ForEntity(typeof(TEntity), typeof(TEntity)));

    /// <summary>Reads a physical table without entity metadata.</summary>
    public static RawQuery<TableAlias> FromTable(string table) =>
        new(RawQueryState.ForTable(table, typeof(TableAlias)));

    /// <summary>Declares a common table expression and opens the source scope that can read it.</summary>
    public static RawQueryScope With(string name, in RawQueryState body) =>
        RawQueryScope.Start(new RawCteDefinition(name, new RawQueryNode(body), recursive: false));

    /// <summary>Declares a recursive common table expression and opens the source scope that can read it.</summary>
    public static RawQueryScope WithRecursive(string name, in RawQueryState body, int? maxRecursion = null) =>
        RawQueryScope.Start(new RawCteDefinition(name, new RawQueryNode(body), recursive: true, maxRecursion));
}

/// <summary>
/// The declaration scope opened by <see cref="RawQueryFactory.With"/>: every source it creates
/// carries the same CTE declarations, so a flat and a nested <c>WITH</c> chain keep one shape.
/// </summary>
internal readonly struct RawQueryScope
{
    private readonly RawCteDefinition _first;
    private readonly RawCteDefinition[]? _rest;

    private RawQueryScope(in RawCteDefinition first, RawCteDefinition[]? rest)
    {
        _first = first;
        _rest = rest;
    }

    internal static RawQueryScope Start(in RawCteDefinition declaration) => new(declaration, null);

    /// <summary>Declares one more CTE in the same scope.</summary>
    public RawQueryScope With(string name, in RawQueryState body) =>
        new(_first, RawStateArrays.Append(_rest, new RawCteDefinition(name, new RawQueryNode(body), recursive: false)));

    /// <summary>Declares one more recursive CTE in the same scope.</summary>
    public RawQueryScope WithRecursive(string name, in RawQueryState body, int? maxRecursion = null) =>
        new(_first, RawStateArrays.Append(_rest, new RawCteDefinition(name, new RawQueryNode(body), recursive: true, maxRecursion)));

    /// <summary>Reads the declared CTE as a derived element type.</summary>
    public RawQuery<TableAlias> From(string cteName) =>
        new(RawQueryState.ForTable(cteName, typeof(TableAlias)).WithCtes(Declarations()));

    /// <summary>Reads an entity source inside the declaration scope.</summary>
    public RawQuery<TEntity> From<TEntity>() =>
        new(RawQueryState.ForEntity(typeof(TEntity), typeof(TEntity)).WithCtes(Declarations()));

    private RawCteDefinition[] Declarations()
    {
        if (_rest is null) return [_first];

        var all = new RawCteDefinition[_rest.Length + 1];
        all[0] = _first;
        Array.Copy(_rest, 0, all, 1, _rest.Length);
        return all;
    }
}
