using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;
// public class BaseEntity
// {
//     protected BaseEntity()
//     {
//     }
//     protected static readonly IDictionary<IDataContext, Lazy<QueryCommand<bool>>> _anyCommandCache = new ConcurrentDictionary<IDataContext, Lazy<QueryCommand<bool>>>();
// }
/// <summary>
/// Fluent, immutable query builder over a mapped entity type; created by
/// <c>IDataContext.From&lt;TEntity&gt;()</c>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type the query projects.</typeparam>
public class EntityBuilder<TEntity> : ICloneable //IAsyncEnumerable<TEntity>
{
    // private const string AnyCommandProperty = "nextorm.core.AnyCommand";
    #region Fields
    protected readonly IDataContext _dataProvider;
    private QueryCommand? _query;
    private Expression<Func<TEntity, bool>>? _condition;
    private LambdaExpression? _group;
    private Expression<Func<TEntity, bool>>? _having;
    private List<Sorting>? _sorting;
    protected List<JoinExpression>? _joins;
    private string? _table;
    private FromExpression? _from;
    #endregion
    public EntityBuilder(IDataContext dataProvider)
    {
        _dataProvider = dataProvider;
    }
    public EntityBuilder(IDataContext dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    public EntityBuilder(IDataContext dataProvider, string table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    #region Properties
    internal ILogger? Logger { get; init; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    internal IDataContext DataProvider => _dataProvider;
    internal Expression<Func<TEntity, bool>>? Condition { get => _condition; set => _condition = value; }
    public List<Sorting>? Sorting { get => _sorting; set => _sorting = value; }
    public List<JoinExpression>? Joins { get => _joins; set => _joins = value; }
    public Paging Paging;
    internal bool IsDistinct { get; set; }
    /// <summary>Super-aggregate modifier applied to the grouping list (<c>ROLLUP</c>/<c>CUBE</c>).</summary>
    internal GroupingType GroupingType { get; set; }
    /// <summary>Explicit grouping-set indices used when <see cref="GroupingType"/> is <c>GroupingSets</c>.</summary>
    internal IReadOnlyList<int[]>? GroupingSets { get; set; }
    /// <summary>Table-level hints applied to the primary physical table (for example SQL Server <c>nolock</c>).</summary>
    internal IReadOnlyList<string>? TableHints { get; set; }
    internal string? Table { get => _table; set => _table = value; }
    /// <summary>
    /// Explicit FROM source, used for table-valued functions (and any other source that is neither a
    /// raw table name nor a derived <see cref="QueryCommand"/>). Propagated through the join builders
    /// so a TVF can be used as the base of a joined query.
    /// </summary>
    internal FromExpression? SourceFrom { get => _from; set => _from = value; }
    /// <summary>CTE declarations that must be attached to commands this builder creates.</summary>
    internal IReadOnlyList<CteDefinition>? Ctes { get; set; }
    //public delegate void CommandCreatedHandler<T>(EntityBuilder<T> sender, QueryCommand queryCommand);
    //public event CommandCreatedHandler<TEntity>? CommandCreatedEvent;
    #endregion

    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(exp, _condition, _joins?.ToArray(), Paging, _sorting?.ToArray(), _group, _having, Logger, IsDistinct);

        if (_query is not null)
            cmd.From = new FromExpression(_query);
        else if (_from is not null)
            cmd.From = _from;
        else if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        cmd.GroupingType = GroupingType;
        cmd.TableHints = TableHints;
        cmd.GroupingSets = GroupingSets;
        // OnCommandCreated(cmd);
        //RaiseCommandCreated(cmd);

        return cmd;
    }
    public QueryCommand<TEntity> ToCommand()
    {
        var cmd = _dataProvider.CreateCommand<TEntity>(typeof(TEntity), _condition, _joins?.ToArray(), Paging, _sorting?.ToArray(), _group, _having, Logger, IsDistinct);

        if (_query is not null)
            cmd.From = new FromExpression(_query);
        else if (_from is not null)
            cmd.From = _from;
        else if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        cmd.GroupingType = GroupingType;
        cmd.TableHints = TableHints;
        cmd.GroupingSets = GroupingSets;

        // OnCommandCreated(cmd);
        //RaiseCommandCreated(cmd);

        return cmd;
    }

    // protected virtual void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    // {

    // }

    public EntityBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        var b = Clone();
        if (_condition is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TEntity, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
    public EntityBuilder<TEntity> Distinct()
    {
        var b = Clone();
        b.IsDistinct = true;
        return b;
    }
    public EntityBuilder<TEntity> Limit(int limit)
    {
        var b = Clone();

        b.Paging.Limit = limit;

        return b;
    }
    public EntityBuilder<TEntity> Offset(int offset)
    {
        var b = Clone();

        b.Paging.Offset = offset;

        return b;
    }
    public EntityBuilder<TEntity> Page(int limit, int offset)
    {
        var b = Clone();

        b.Paging.Limit = limit;
        b.Paging.Offset = offset;

        return b;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public EntityBuilder<TEntity> Clone()
    {
        return (EntityBuilder<TEntity>)CloneImp();
    }
    protected virtual void CopyTo(EntityBuilder<TEntity> dst)
    {
        dst._query = _query;
        //dst._payloadMgr = _payloadMgr;
        //dst.CommandCreatedEvent += CommandCreatedEvent;
        dst.Paging = Paging;
        dst._condition = _condition;
        dst._sorting = _sorting;
        dst._group = _group;
        dst._having = _having;
        dst._table = _table;
        dst._from = _from;
        dst.IsDistinct = IsDistinct;
        dst.GroupingType = GroupingType;
        dst.GroupingSets = GroupingSets;
        dst.TableHints = TableHints;
        dst.Ctes = Ctes;
    }
    protected virtual object CloneImp()
    {
        var r = new EntityBuilder<TEntity>(_dataProvider) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    public static implicit operator QueryCommand<TEntity>(EntityBuilder<TEntity> builder) => builder.ToCommand();
    public static implicit operator QueryCommand(EntityBuilder<TEntity> builder) => builder.ToCommand();
    // public QueryCommand<TEntity> ToCommand() => Select<TEntity>(typeof(TEntity));
    // public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TEntity> ToAsyncEnumerable(params object[] @params) => ToAsyncEnumerable(CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TEntity> ToAsyncEnumerable(CancellationToken cancellationToken, params object[] @params) => ToCommand().ToAsyncEnumerable(cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TEntity> ToEnumerable(params object[] @params) => ToCommand().ToEnumerable(@params);

    /// <summary>
    /// Flattens the sequence produced by <paramref name="collectionSelector"/> for every row of this
    /// query (<c>source.SelectMany(...)</c>), yielding the selected elements.
    /// <para>
    /// Supported by the <c>InMemoryContext</c> only; SQL providers throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public EntityBuilder<TCollection> SelectMany<TCollection>(Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);
        EnsureInMemory(nameof(SelectMany));

        return CreateLinqSourceBuilder<TCollection>(new LinqSourceExpression
        {
            OuterCommand = ToCommand(),
            OuterType = typeof(TEntity),
            ResultType = typeof(TCollection),
            CollectionType = typeof(TCollection),
            CollectionSelector = collectionSelector
        });
    }

    /// <summary>
    /// Flattens the sequence produced by <paramref name="collectionSelector"/> for every row of this
    /// query and projects each pair with <paramref name="resultSelector"/>.
    /// <para>
    /// Supported by the <c>InMemoryContext</c> only; SQL providers throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public EntityBuilder<TResult> SelectMany<TCollection, TResult>(
        Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector,
        Expression<Func<TEntity, TCollection, TResult>> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        EnsureInMemory(nameof(SelectMany));

        return CreateLinqSourceBuilder<TResult>(new LinqSourceExpression
        {
            OuterCommand = ToCommand(),
            OuterType = typeof(TEntity),
            ResultType = typeof(TResult),
            CollectionType = typeof(TCollection),
            CollectionSelector = collectionSelector,
            ResultSelector = resultSelector
        });
    }

    /// <summary>
    /// Correlates the rows of this query with <paramref name="inner"/> by key and projects each pair
    /// with the grouped inner rows (<c>source.GroupJoin(...)</c>). Rows without a match receive an
    /// empty group.
    /// <para>
    /// Supported by the <c>InMemoryContext</c> only; SQL providers throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public EntityBuilder<TResult> GroupJoin<TInner, TKey, TResult>(
        EntityBuilder<TInner> inner,
        Expression<Func<TEntity, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TEntity, IEnumerable<TInner>, TResult>> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        EnsureInMemory(nameof(GroupJoin));

        return CreateLinqSourceBuilder<TResult>(new LinqSourceExpression
        {
            OuterCommand = ToCommand(),
            InnerCommand = inner.ToCommand(),
            OuterType = typeof(TEntity),
            InnerType = typeof(TInner),
            KeyType = typeof(TKey),
            ResultType = typeof(TResult),
            OuterKeySelector = outerKeySelector,
            InnerKeySelector = innerKeySelector,
            ResultSelector = resultSelector,
            IsGroupJoin = true
        });
    }

    private void EnsureInMemory(string method)
    {
        if (_dataProvider is not InMemoryContext)
            throw new NotSupportedException(
                $"{method} is not supported by this provider; it is only available on the in-memory provider. " +
                "SQL providers do not translate SelectMany/GroupJoin yet.");
    }

    private EntityBuilder<TResult> CreateLinqSourceBuilder<TResult>(LinqSourceExpression source)
    {
        var builder = new EntityBuilder<TResult>(_dataProvider) { Logger = Logger };
        builder._from = new FromExpression(source);
        return builder;
    }

    public EntityP2<TEntity, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public EntityP2<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public EntityP2<TEntity, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public EntityP2<TEntity, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public EntityP2<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Cross, null);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row (<c>CROSS APPLY</c> / <c>CROSS JOIN LATERAL</c>).
    /// There is no <c>ON</c> condition.
    /// </summary>
    public EntityP2<TEntity, TJoinEntity> CrossApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.CrossApply, null);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row, preserving left-hand rows with an empty
    /// result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). There is no <c>ON</c> condition.
    /// </summary>
    public EntityP2<TEntity, TJoinEntity> OuterApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private EntityP2<TEntity, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        QueryCommand? query = null;
        if (_condition is not null || _query is not null)
        {
            query = ToCommand();
        }

        // A TVF (or other explicit source) on either side is carried as a FromExpression: the right
        // side keeps the joined entity's own source, the left side keeps the one propagated below.
        var cb = new EntityP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = _.SourceFrom ?? _dataProvider.GetFrom(typeof(TJoinEntity), null)!, EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, _query = query, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, TableHints = TableHints, Ctes = Ctes };
        cb.SourceFrom = SourceFrom;
        return cb;
    }
    public EntityP2<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Inner, joinCondition);
    public EntityP2<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Left, joinCondition);
    public EntityP2<TEntity, TJoinEntity> RightJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Right, joinCondition);
    public EntityP2<TEntity, TJoinEntity> FullJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Full, joinCondition);
    public EntityP2<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.Cross, null);
    /// <summary>Applies a derived query to every left-hand row (<c>CROSS APPLY</c>).</summary>
    public EntityP2<TEntity, TJoinEntity> CrossApply<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.CrossApply, null);
    /// <summary>
    /// Applies a derived query to every left-hand row, preserving left-hand rows with an empty result
    /// (<c>OUTER APPLY</c>).
    /// </summary>
    public EntityP2<TEntity, TJoinEntity> OuterApply<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.OuterApply, null);
    private EntityP2<TEntity, TJoinEntity> JoinCore<TJoinEntity>(QueryCommand<TJoinEntity> query, JoinType joinType, LambdaExpression? joinCondition)
    {
        QueryCommand? queryBase = null;
        if (_condition is not null || _query is not null)
        {
            queryBase = ToCommand();
        }

        var cb = new EntityP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = new FromExpression(query), EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, _query = queryBase, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, TableHints = TableHints, Ctes = Ctes };
        cb.SourceFrom = SourceFrom;
        return cb;
    }
    public EntityBuilder<TEntity> GroupBy<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;

        return b;
    }
    /// <summary>
    /// Groups by <c>ROLLUP (columns)</c>: the grouped rows plus every prefix subtotal and the grand
    /// total. Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsRollup"/>).
    /// </summary>
    public EntityBuilder<TEntity> GroupByRollup<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;
        b.GroupingType = GroupingType.Rollup;

        return b;
    }
    /// <summary>
    /// Groups by <c>CUBE (columns)</c>: the grouped rows plus every combination subtotal. Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsCube"/>).
    /// </summary>
    public EntityBuilder<TEntity> GroupByCube<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;
        b.GroupingType = GroupingType.Cube;

        return b;
    }
    /// <summary>
    /// Groups by an explicit list of grouping sets, e.g. <c>GROUP BY GROUPING SETS ((a, b), (a), ())</c>.
    /// <paramref name="exp"/> declares the full grouping list (an anonymous type / DTO) and each entry of
    /// <paramref name="sets"/> is a set of 0-based indices into that list; an empty set produces the
    /// grand total. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsGroupingSets"/>).
    /// </summary>
    public EntityBuilder<TEntity> GroupByGroupingSets<TResult>(Expression<Func<TEntity, TResult>> exp, params int[][] sets)
    {
        var b = Clone();

        b._group = exp;
        b.GroupingType = GroupingType.GroupingSets;
        b.GroupingSets = sets is { Length: > 0 } ? sets : null;

        return b;
    }
    /// <summary>
    /// Attaches table-level hints to the primary physical table, for example
    /// <c>From&lt;IComplexEntity&gt;().WithTableHint("nolock")</c> which renders
    /// <c>from complex_entity with (nolock)</c>. Requires a dialect that supports table hints (see
    /// <see cref="ISqlDialect.SupportsTableHints"/>); the hints are rendered verbatim, so only use
    /// trusted values.
    /// </summary>
    public EntityBuilder<TEntity> WithTableHint(params string[] hints)
    {
        var b = Clone();

        b.TableHints = hints is { Length: > 0 }
            ? hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : null;

        return b;
    }
    public EntityBuilder<TEntity> Having(Expression<Func<TEntity, bool>> condition)
    {
        var b = Clone();
        if (_having is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterVisitor(_having.Parameters[0]);
            var newBody = Expression.AndAlso(_having.Body, replVisitor.Visit(condition.Body));
            b._having = Expression.Lambda<Func<TEntity, bool>>(newBody, _having.Parameters[0]);
        }
        else
            b._having = condition;

        return b;
    }
    public QueryCommand<bool> AnyCommand()
    {
        var cmd = ToCommand();
        var queryCommand = _dataProvider.CreateCommand<bool>((TableAlias _) => NORM.SQL.exists(cmd), null, null, default, null, null, null, Logger);
        queryCommand.SingleRow = true;
        cmd.IgnoreColumns = true;
        return queryCommand;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Any() => AnyCore(ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Any(params ReadOnlySpan<object?> @params) => AnyCore(@params);
    private bool AnyCore(ReadOnlySpan<object?> @params)
    {
        var cmd = ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(_dataProvider, cmd);
        var preparedCommand = _dataProvider.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
        return _dataProvider.ExecuteScalar<bool>(preparedCommand, @params, true);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TEntity> ToList(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().ToList(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TEntity>> ToListAsync(params object[] @params) => ToListAsync(CancellationToken.None, @params);
    public Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().ToListAsync(cancellationToken, @params);
    }
    public TEntity[] ToArray(params ReadOnlySpan<object?> @params) => ToCommand().ToArray(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity[]> ToArrayAsync(params object[] @params) => ToArrayAsync(CancellationToken.None, @params);
    public Task<TEntity[]> ToArrayAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().ToArrayAsync(cancellationToken, @params);
    }
    public HashSet<TEntity> ToHashSet(params ReadOnlySpan<object?> @params) => ToCommand().ToHashSet(@params);
    public HashSet<TEntity> ToHashSet(IEqualityComparer<TEntity>? comparer, params ReadOnlySpan<object?> @params) => ToCommand().ToHashSet(comparer, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<HashSet<TEntity>> ToHashSetAsync(params object[] @params) => ToHashSetAsync(CancellationToken.None, @params);
    public Task<HashSet<TEntity>> ToHashSetAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().ToHashSetAsync(cancellationToken, @params);
    }
    public Task<HashSet<TEntity>> ToHashSetAsync(IEqualityComparer<TEntity>? comparer, CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().ToHashSetAsync(comparer, cancellationToken, @params);
    }
    public Dictionary<TKey, TEntity> ToDictionary<TKey>(Func<TEntity, TKey> keySelector, params ReadOnlySpan<object?> @params) where TKey : notnull
        => ToCommand().ToDictionary(keySelector, @params);
    public Dictionary<TKey, TEntity> ToDictionary<TKey>(Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer, params ReadOnlySpan<object?> @params) where TKey : notnull
        => ToCommand().ToDictionary(keySelector, comparer, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TKey>(Func<TEntity, TKey> keySelector, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(keySelector, CancellationToken.None, @params);
    public Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TKey>(Func<TEntity, TKey> keySelector, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
    {
        return ToCommand().ToDictionaryAsync(keySelector, cancellationToken, @params);
    }
    public Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TKey>(Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
    {
        return ToCommand().ToDictionaryAsync(keySelector, comparer, cancellationToken, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEntity First() => First(ReadOnlySpan<object?>.Empty);
    public TEntity First(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().First(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity> FirstAsync(params object[] @params) => FirstAsync(CancellationToken.None, @params);
    public Task<TEntity> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().FirstAsync(cancellationToken, @params);
    }
    public QueryCommand<TEntity?> FirstOrFirstOrDefaultCommand()
    {
        var cmd = ToCommand();
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public QueryCommand<TResult?> FirstOrFirstOrDefaultCommand<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = Select(exp);
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEntity? FirstOrDefault() => FirstOrDefault(ReadOnlySpan<object?>.Empty);
    public TEntity? FirstOrDefault(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().FirstOrDefault(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity?> FirstOrDefaultAsync(params object[] @params) => FirstOrDefaultAsync(CancellationToken.None, @params);
    public Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().FirstOrDefaultAsync(cancellationToken, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEntity Single() => Single(ReadOnlySpan<object?>.Empty);
    public TEntity Single(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().Single(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity> SingleAsync(params object[] @params) => SingleAsync(CancellationToken.None, @params);
    public Task<TEntity> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().SingleAsync(cancellationToken, @params);
    }
    public QueryCommand<TResult?> SingleOrSingleOrDefaultCommand<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = Select(exp);
        cmd.Paging.Limit = 2;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public QueryCommand<TEntity> SingleOrSingleOrDefaultCommand()
    {
        var cmd = ToCommand();
        cmd.Paging.Limit = 2;
        return cmd;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEntity? SingleOrDefault() => SingleOrDefault(ReadOnlySpan<object?>.Empty);
    public TEntity? SingleOrDefault(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().SingleOrDefault(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity?> SingleOrDefaultAsync(params object[] @params) => SingleOrDefaultAsync(CancellationToken.None, @params);
    public Task<TEntity?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().SingleOrDefaultAsync(cancellationToken, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEntity Last() => Last(ReadOnlySpan<object?>.Empty);
    public TEntity Last(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().Last(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity> LastAsync(params object[] @params) => LastAsync(CancellationToken.None, @params);
    public Task<TEntity> LastAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().LastAsync(cancellationToken, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEntity? LastOrDefault() => LastOrDefault(ReadOnlySpan<object?>.Empty);
    public TEntity? LastOrDefault(params ReadOnlySpan<object?> @params)
    {
        return ToCommand().LastOrDefault(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity?> LastOrDefaultAsync(params object[] @params) => LastOrDefaultAsync(CancellationToken.None, @params);
    public Task<TEntity?> LastOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().LastOrDefaultAsync(cancellationToken, @params);
    }
    internal protected static QueryCommand<bool> GetAnyCommand(IDataContext dataProvider, QueryCommand cmd)
    {
        var created = false;
        if (dataProvider.AnyCommand is not Lazy<QueryCommand<bool>> anyCommand)
        {
            anyCommand = new Lazy<QueryCommand<bool>>(() =>
            {
                created = true;
                var queryCommand = dataProvider.CreateCommand<bool>((TableAlias _) => NORM.SQL.exists(cmd), null, null, default, null, null, null, cmd.Logger);
                queryCommand.SingleRow = true;
                queryCommand.PrepareCommand(false, CancellationToken.None);
                return queryCommand;
            });
            dataProvider.AnyCommand = anyCommand;
        }

        var queryCommand = anyCommand.Value;
        if (!created)
        {
            if (!cmd.IsPrepared) cmd.PrepareCommand(false, CancellationToken.None);
            queryCommand.ReplaceCommand(cmd, 0);
        }

        return queryCommand;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(params object[] @params) => AnyAsync(CancellationToken.None, @params);
    public async Task<bool> AnyAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(_dataProvider, cmd);
        var preparedCommand = _dataProvider.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        return await _dataProvider.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
    }
    public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp, OrderDirection direction)
    {
        var b = Clone();
        if (b._sorting is null)
            b._sorting = [new Sorting(orderExp) { Direction = direction }];
        else
            b._sorting.Add(new Sorting(orderExp) { Direction = direction });
        return b;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Asc);
    public EntityBuilder<TEntity> OrderBy(int columnIdx, OrderDirection direction)
    {
        if (columnIdx < 1) throw new ArgumentException("Column index must be greater than zero", nameof(columnIdx));

        var b = Clone();
        if (b._sorting is null)
            b._sorting = [new Sorting(columnIdx) { Direction = direction }];
        else
            b._sorting.Add(new Sorting(columnIdx) { Direction = direction });
        return b;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderBy(int columnIdx) => OrderBy(columnIdx, OrderDirection.Asc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Desc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderByDescending(int columnIdx) => OrderBy(columnIdx, OrderDirection.Desc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPreparedQueryCommand<TEntity> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default) => ToCommand().Prepare(nonStreamUsing, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => CountCore(ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count(params ReadOnlySpan<object?> @params) => CountCore(@params);
    private int CountCore(ReadOnlySpan<object?> @params)
    {
        var cmd = Select(e => NORM.SQL.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> CountAsync(params object[] @params) => CountAsync(CancellationToken.None, @params);
    public Task<int> CountAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(e => NORM.SQL.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    // The eight aggregate families differ only in the NORM_SQL method they wrap, so every public
    // member is a one-line forwarder and the body lives once in AggregateCore/AggregateAsyncCore.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Min<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.MinMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Min<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.MinMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> MinAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => MinAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> MinAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.MinMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Max<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.MaxMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Max<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.MaxMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> MaxAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => MaxAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> MaxAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.MaxMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Avg<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.AvgMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Avg<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.AvgMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> AvgAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => AvgAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> AvgAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.AvgMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Sum<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.SumMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Sum<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.SumMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SumAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => SumAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> SumAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.SumMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Stdev<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.StdevMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Stdev<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.StdevMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.StdevMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Stdevp<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.StdevpMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Stdevp<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.StdevpMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevpAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.StdevpMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Var<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.VarMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Var<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.VarMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.VarMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Varp<TResult>(Expression<Func<TEntity, TResult>> exp) => AggregateCore(NORM.NORM_SQL.VarpMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? Varp<TResult>(Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(NORM.NORM_SQL.VarpMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarpAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(NORM.NORM_SQL.VarpMI, exp, cancellationToken, @params);

    private TResult? AggregateCore<TResult>(MethodInfo sqlMethod, Expression<Func<TEntity, TResult>> exp, ReadOnlySpan<object?> @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    private Task<TResult?> AggregateAsyncCore<TResult>(MethodInfo sqlMethod, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
}

/// <summary>
/// Fluent query builder over a table addressed by name (<c>TableAlias</c> mode); created by
/// <c>IDataContext.From("table")</c>.
/// </summary>
public class EntityBuilder : ICloneable
{
    private readonly IDataContext _dataProvider;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression<Func<TableAlias, bool>>? _condition;
    private List<Sorting>? _sorting;
    protected List<JoinExpression>? _joins;
    /// <summary>CTE declarations that must be attached to commands this builder creates.</summary>
    internal IReadOnlyList<CteDefinition>? Ctes { get; set; }
    public EntityBuilder(IDataContext dataProvider) : this(dataProvider, null) { }
    public EntityBuilder(IDataContext dataProvider, string? table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TableAlias, TResult>> exp)
    {
        //if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        var cmd = new QueryCommand<TResult>(_dataProvider, exp, _condition, _joins?.ToArray(), default, _sorting?.ToArray(), null, null, Logger);

        if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        return cmd;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public EntityBuilder Clone()
    {
        return (EntityBuilder)CloneImp();
    }
    protected virtual void CopyTo(EntityBuilder dst)
    {
        dst._condition = _condition;
        dst._sorting = _sorting;
        dst.Ctes = Ctes;
    }
    protected virtual object CloneImp()
    {
        var r = new EntityBuilder(_dataProvider, _table) { Logger = Logger };

        CopyTo(r);

        return r;
    }

    public EntityBuilder Where(Expression<Func<TableAlias, bool>> condition)
    {
        var b = Clone();
        if (_condition is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TableAlias, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
    public EntityP2<TableAlias, TableAlias> Join(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Inner, joinCondition);
    public EntityP2<TableAlias, TableAlias> LeftJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Left, joinCondition);
    public EntityP2<TableAlias, TableAlias> RightJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Right, joinCondition);
    public EntityP2<TableAlias, TableAlias> FullJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Full, joinCondition);
    public EntityP2<TableAlias, TableAlias> CrossJoin(EntityBuilder from)
        => JoinCore(from, JoinType.Cross, null);
    public EntityP2<TableAlias, TableAlias> CrossApply(EntityBuilder from)
        => JoinCore(from, JoinType.CrossApply, null);
    public EntityP2<TableAlias, TableAlias> OuterApply(EntityBuilder from)
        => JoinCore(from, JoinType.OuterApply, null);
    private EntityP2<TableAlias, TableAlias> JoinCore(EntityBuilder from, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP2<TableAlias, TableAlias>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = new FromExpression(from._table!), EntityType = joinCondition is null ? typeof(TableAlias) : null }) { Logger = Logger, Table = _table, Ctes = Ctes };
        return cb;
    }
    public EntityP2<TableAlias, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public EntityP2<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public EntityP2<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public EntityP2<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public EntityP2<TableAlias, TJoinEntity> CrossJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Cross, null);
    public EntityP2<TableAlias, TJoinEntity> CrossApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public EntityP2<TableAlias, TJoinEntity> OuterApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private EntityP2<TableAlias, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP2<TableAlias, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(TJoinEntity), null)!, EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, Table = _table, Ctes = Ctes };
        return cb;
    }
}
