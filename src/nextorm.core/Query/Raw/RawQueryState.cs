using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Kind of the raw <c>FROM</c> source in a <see cref="RawQueryState"/>.
/// </summary>
internal enum RawSourceKind
{
    /// <summary>No source; only a default-constructed (empty) state carries this value.</summary>
    None = 0,
    /// <summary>A mapped entity type; metadata is resolved at the key boundary.</summary>
    Entity = 1,
    /// <summary>A physical table name, read without entity metadata.</summary>
    Table = 2,
    /// <summary>A derived query (a set-operation operand, a CTE body or a subquery source).</summary>
    Derived = 3,
}

/// <summary>
/// Immutable raw query description: the current logical shape only. It carries no prepared columns,
/// no resolved bindings, no cache slots and no execution state; those belong to the compiled plan.
/// </summary>
/// <remarks>
/// The description is a value type. An operator copies it and replaces the affected slot, so a branch
/// can never mutate its source or a sibling. Arrays it holds are published once and never mutated
/// afterwards; shared arrays are therefore safe to reuse across branches.
/// </remarks>
internal readonly struct RawQueryState
{
    private readonly RawSource _source;
    private readonly Type? _entityType;
    private readonly Type _resultType;
    private readonly LambdaExpression? _projection;
    private readonly LambdaExpression[]? _conditions;
    private readonly RawJoin[]? _joins;
    private readonly RawOrdering[]? _ordering;
    private readonly Paging _paging;
    private readonly RawSetOperation? _setOperation;
    private readonly RawCteDefinition[]? _ctes;
    private readonly RawQueryFlags _flags;
    private readonly string? _tag;
    private readonly string[]? _hints;
    private readonly QueryFilterScope? _filterScope;

    private RawQueryState(
        RawSource source,
        Type? entityType,
        Type resultType,
        LambdaExpression? projection,
        LambdaExpression[]? conditions,
        RawJoin[]? joins,
        RawOrdering[]? ordering,
        Paging paging,
        RawSetOperation? setOperation,
        RawCteDefinition[]? ctes,
        RawQueryFlags flags,
        string? tag,
        string[]? hints,
        QueryFilterScope? filterScope)
    {
        _source = source;
        _entityType = entityType;
        _resultType = resultType;
        _projection = projection;
        _conditions = conditions;
        _joins = joins;
        _ordering = ordering;
        _paging = paging;
        _setOperation = setOperation;
        _ctes = ctes;
        _flags = flags;
        _tag = tag;
        _hints = hints;
        _filterScope = filterScope;
    }

    /// <summary>The raw <c>FROM</c> source.</summary>
    public RawSource Source => _source;
    /// <summary>The entity type that supplies table metadata, or <c>null</c> for a table/derived source.</summary>
    public Type? EntityType => _entityType;
    /// <summary>The projected result type.</summary>
    public Type ResultType => _resultType;
    /// <summary>The projection lambda, or <c>null</c> when the query projects the whole element.</summary>
    public LambdaExpression? Projection => _projection;
    /// <summary>The <c>WHERE</c> predicates in application order, or <c>null</c> when there are none.</summary>
    public LambdaExpression[]? Conditions => _conditions;
    /// <summary>The joins in application order, or <c>null</c> when there are none.</summary>
    public RawJoin[]? Joins => _joins;
    /// <summary>The <c>ORDER BY</c> keys in application order, or <c>null</c> when there are none.</summary>
    public RawOrdering[]? Ordering => _ordering;
    /// <summary>The <c>LIMIT</c>/<c>OFFSET</c> state.</summary>
    public Paging Paging => _paging;
    /// <summary>The set operation that combines this query with a right operand, or <c>null</c>.</summary>
    public RawSetOperation? SetOperation => _setOperation;
    /// <summary>The CTE declarations in scope, or <c>null</c> when there are none.</summary>
    public RawCteDefinition[]? Ctes => _ctes;
    /// <summary>Shape flags that affect SQL or the result shape.</summary>
    public RawQueryFlags Flags => _flags;
    /// <summary>The query tag, or <c>null</c>.</summary>
    public string? Tag => _tag;
    /// <summary>The provider hints, or <c>null</c>.</summary>
    public string[]? Hints => _hints;
    /// <summary>The global query filters disabled on this query, or <c>null</c> for none.</summary>
    public QueryFilterScope? FilterScope => _filterScope;

    /// <summary>Whether this state carries a source and can be keyed.</summary>
    public bool HasSource => _source.Kind != RawSourceKind.None;

    /// <summary>Creates an entity source.</summary>
    public static RawQueryState ForEntity(Type entityType, Type resultType) =>
        new(RawSource.ForEntity(entityType), entityType, resultType, null, null, null, null,
            default, null, null, RawQueryFlags.None, null, null, null);

    /// <summary>Creates a physical table source.</summary>
    public static RawQueryState ForTable(string table, Type resultType) =>
        new(RawSource.ForTable(table), null, resultType, null, null, null, null,
            default, null, null, RawQueryFlags.None, null, null, null);

    /// <summary>Creates a derived source over another raw query.</summary>
    public static RawQueryState ForDerived(in RawQueryState body, Type resultType) =>
        new(RawSource.ForDerived(new RawQueryNode(body)), null, resultType, null, null, null, null,
            default, null, null, RawQueryFlags.None, null, null, null);

    /// <summary>Appends a <c>WHERE</c> predicate. The source state is unchanged.</summary>
    public RawQueryState WithCondition(LambdaExpression condition) =>
        new(_source, _entityType, _resultType, _projection, RawStateArrays.Append(_conditions, condition),
            _joins, _ordering, _paging, _setOperation, _ctes, _flags, _tag, _hints, _filterScope);

    /// <summary>Sets the projection and the result type it produces.</summary>
    public RawQueryState WithProjection(LambdaExpression projection, Type resultType) =>
        new(_source, _entityType, resultType, projection, _conditions, _joins, _ordering, _paging,
            _setOperation, _ctes, _flags, _tag, _hints, _filterScope);

    /// <summary>Appends a join.</summary>
    public RawQueryState WithJoin(in RawJoin join) =>
        new(_source, _entityType, _resultType, _projection, _conditions,
            RawStateArrays.Append(_joins, join), _ordering, _paging, _setOperation, _ctes, _flags, _tag, _hints, _filterScope);

    /// <summary>Appends an <c>ORDER BY</c> key.</summary>
    public RawQueryState WithOrdering(in RawOrdering ordering) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins,
            RawStateArrays.Append(_ordering, ordering), _paging, _setOperation, _ctes, _flags, _tag, _hints, _filterScope);

    /// <summary>Replaces the paging state.</summary>
    public RawQueryState WithPaging(Paging paging) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, paging,
            _setOperation, _ctes, _flags, _tag, _hints, _filterScope);

    /// <summary>Sets the set operation that combines this query with <paramref name="right"/>.</summary>
    public RawQueryState WithSetOperation(UnionType kind, in RawQueryState right) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, _paging,
            new RawSetOperation(kind, new RawQueryNode(right)), _ctes, _flags, _tag, _hints, _filterScope);

    /// <summary>Appends a CTE declaration.</summary>
    public RawQueryState WithCte(in RawCteDefinition cte) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, _paging,
            _setOperation, RawStateArrays.Append(_ctes, cte), _flags, _tag, _hints, _filterScope);

    /// <summary>Attaches a whole declaration list to a source (the shape a <see cref="RawQueryScope"/> publishes).</summary>
    public RawQueryState WithCtes(RawCteDefinition[] ctes)
    {
        if (_ctes is null || _ctes.Length == 0)
        {
            return new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering,
                _paging, _setOperation, ctes, _flags, _tag, _hints, _filterScope);
        }

        var merged = new RawCteDefinition[_ctes.Length + ctes.Length];
        Array.Copy(_ctes, merged, _ctes.Length);
        Array.Copy(ctes, 0, merged, _ctes.Length, ctes.Length);
        return new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering,
            _paging, _setOperation, merged, _flags, _tag, _hints, _filterScope);
    }

    /// <summary>Adds shape flags.</summary>
    public RawQueryState WithFlags(RawQueryFlags flags) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, _paging,
            _setOperation, _ctes, _flags | flags, _tag, _hints, _filterScope);

    /// <summary>Sets the query tag.</summary>
    public RawQueryState WithTag(string? tag) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, _paging,
            _setOperation, _ctes, _flags, tag, _hints, _filterScope);

    /// <summary>Sets the provider hints.</summary>
    public RawQueryState WithHints(string[]? hints) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, _paging,
            _setOperation, _ctes, _flags, _tag, hints, _filterScope);

    /// <summary>Sets the global query filters disabled on this query.</summary>
    public RawQueryState WithFilterScope(QueryFilterScope? filterScope) =>
        new(_source, _entityType, _resultType, _projection, _conditions, _joins, _ordering, _paging,
            _setOperation, _ctes, _flags, _tag, _hints, filterScope);
}

/// <summary>
/// An immutable raw query held by reference: the graph lifetime unit for nested descriptions
/// (set-operation operands, CTE bodies and derived sources).
/// </summary>
internal sealed class RawQueryNode
{
    public RawQueryNode(in RawQueryState state) => State = state;

    /// <summary>The immutable state this node publishes.</summary>
    public RawQueryState State { get; }
}

/// <summary>Shape flags that affect the rendered SQL or the materialized result.</summary>
[Flags]
internal enum RawQueryFlags
{
    /// <summary>No flag.</summary>
    None = 0,
    /// <summary><c>DISTINCT</c> over the projection.</summary>
    Distinct = 1 << 0,
    /// <summary>A single-row terminal shape.</summary>
    SingleRow = 1 << 1,
    /// <summary>A scalar terminal that yields <c>default</c> on an empty result.</summary>
    DefaultOnEmpty = 1 << 2,
    /// <summary>A scalar terminal that rejects more than one row.</summary>
    SingleScalar = 1 << 3,
}

/// <summary>An immutable raw <c>FROM</c> source.</summary>
internal readonly struct RawSource
{
    private readonly RawSourceKind _kind;
    private readonly Type? _entityType;
    private readonly string? _table;
    private readonly RawQueryNode? _derived;

    private RawSource(RawSourceKind kind, Type? entityType, string? table, RawQueryNode? derived)
    {
        _kind = kind;
        _entityType = entityType;
        _table = table;
        _derived = derived;
    }

    /// <summary>Source kind.</summary>
    public RawSourceKind Kind => _kind;
    /// <summary>Entity type of an <see cref="RawSourceKind.Entity"/> source.</summary>
    public Type? EntityType => _entityType;
    /// <summary>Table name of a <see cref="RawSourceKind.Table"/> source.</summary>
    public string? Table => _table;
    /// <summary>Body of a <see cref="RawSourceKind.Derived"/> source.</summary>
    public RawQueryNode? Derived => _derived;

    /// <summary>Creates an entity source.</summary>
    public static RawSource ForEntity(Type entityType) =>
        new(RawSourceKind.Entity, entityType, null, null);

    /// <summary>Creates a physical table source.</summary>
    public static RawSource ForTable(string table) =>
        new(RawSourceKind.Table, null, table, null);

    /// <summary>Creates a derived source over <paramref name="body"/>.</summary>
    public static RawSource ForDerived(RawQueryNode body) =>
        new(RawSourceKind.Derived, null, null, body);
}

/// <summary>An immutable raw join clause.</summary>
internal readonly struct RawJoin
{
    public RawJoin(JoinType joinType, JoinStrictness strictness, LambdaExpression? condition, in RawSource right, bool isGlobal = false)
    {
        JoinType = joinType;
        Strictness = strictness;
        Condition = condition;
        Right = right;
        IsGlobal = isGlobal;
    }

    /// <summary>The join kind.</summary>
    public JoinType JoinType { get; }
    /// <summary>The ClickHouse null-treatment modifier.</summary>
    public JoinStrictness Strictness { get; }
    /// <summary>The <c>ON</c> condition, or <c>null</c> for a conditionless join.</summary>
    public LambdaExpression? Condition { get; }
    /// <summary>The joined source.</summary>
    public RawSource Right { get; }
    /// <summary>Whether the join is applied globally rather than per-clause.</summary>
    public bool IsGlobal { get; }
}

/// <summary>An immutable raw <c>ORDER BY</c> key.</summary>
internal readonly struct RawOrdering
{
    public RawOrdering(Expression expression, OrderDirection direction, NullsPosition nullsPosition)
    {
        Expression = expression;
        Direction = direction;
        NullsPosition = nullsPosition;
    }

    /// <summary>The sort key expression.</summary>
    public Expression Expression { get; }
    /// <summary>Ascending or descending.</summary>
    public OrderDirection Direction { get; }
    /// <summary>Where SQL <c>NULL</c> values are placed.</summary>
    public NullsPosition NullsPosition { get; }
}

/// <summary>An immutable raw set operation combining this query with a right operand.</summary>
internal readonly struct RawSetOperation
{
    public RawSetOperation(UnionType kind, RawQueryNode right)
    {
        Kind = kind;
        Right = right;
    }

    /// <summary>The set-operation kind.</summary>
    public UnionType Kind { get; }
    /// <summary>The right operand.</summary>
    public RawQueryNode Right { get; }
}

/// <summary>An immutable raw CTE declaration.</summary>
internal readonly struct RawCteDefinition
{
    public RawCteDefinition(string name, RawQueryNode body, bool recursive, int? maxRecursion = null)
    {
        Name = name;
        Body = body;
        Recursive = recursive;
        MaxRecursion = maxRecursion;
    }

    /// <summary>The CTE name.</summary>
    public string Name { get; }
    /// <summary>The CTE body.</summary>
    public RawQueryNode Body { get; }
    /// <summary>Whether the declaration contributes a <c>WITH RECURSIVE</c>.</summary>
    public bool Recursive { get; }
    /// <summary>The provider recursion limit, or <c>null</c>.</summary>
    public int? MaxRecursion { get; }
}

/// <summary>Copy-on-append helpers for the immutable arrays a raw state publishes.</summary>
internal static class RawStateArrays
{
    public static T[] Append<T>(T[]? source, T value)
    {
        if (source is null || source.Length == 0) return [value];

        var appended = new T[source.Length + 1];
        Array.Copy(source, appended, source.Length);
        appended[^1] = value;
        return appended;
    }
}
