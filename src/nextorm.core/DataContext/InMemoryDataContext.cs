#define PARAM_CONDITION
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

public partial class InMemoryDataContext : IDataContext
{
    internal static readonly MethodInfo miCreateAsyncEnumerator = typeof(InMemoryDataContext).GetMethod(nameof(CreateAsyncEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    internal static readonly MethodInfo miCreateEnumeratorAdapter = typeof(InMemoryDataContext).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Instance)!;
    internal static readonly MethodInfo miCreateEnumerator = typeof(InMemoryDataContext).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    internal static readonly MethodInfo miLoopJoin = typeof(InMemoryDataContext).GetMethod(nameof(LoopJoin), BindingFlags.NonPublic | BindingFlags.Instance)!;
    internal static readonly MethodInfo miCreateCompiledQuery = typeof(InMemoryDataContext).GetMethod(nameof(CreateCompiledQuery), BindingFlags.NonPublic | BindingFlags.Instance)!;
    internal static readonly MethodInfo miApplySelectMany = typeof(InMemoryDataContext).GetMethod(nameof(ApplySelectMany), BindingFlags.NonPublic | BindingFlags.Instance)!;
    internal static readonly MethodInfo miApplyGroupJoin = typeof(InMemoryDataContext).GetMethod(nameof(ApplyGroupJoin), BindingFlags.NonPublic | BindingFlags.Instance)!;
    // Entity metadata and select lists are provider-independent and process-wide, so the in-memory
    // provider shares them with the SQL contexts through DataContextCache instead of keeping a
    // second set of static dictionaries. The duplicates that used to live here were dead: nothing
    // ever wrote to them and nothing ever read them (see Metadata / SelectListCache below, which
    // now delegate).
    //
    // Compiled delegates, on the other hand, are deliberately per-instance: the expressions cached
    // below embed Expression.Constant(this) (see GetPreparedQueryCommand and
    // BuildCreateEnumeratorDelegate), so a delegate compiled by one context calls back into that
    // same context. Sharing them process-wide would hand the wrong instance to the caller. Only the
    // metadata/select-list caches may be global.
    private readonly IDictionary<ExpressionKey, Delegate> _expCache = new ExpressionCache<Delegate>();
    // In-memory-specific: typed condition predicates (factory / direct). They have no SQL
    // counterpart, so they are not duplicates of any DataContextCache entry.
    private readonly IDictionary<ExpressionKey, Delegate> _conditionFactoryCache = new ExpressionCache<Delegate>();
    private readonly IDictionary<ExpressionKey, Delegate> _conditionDirectCache = new ExpressionCache<Delegate>();
    // Compiled aggregate value selectors: an aggregate command is executed repeatedly (for example in
    // a loop), so the (typed, unboxed) selector is compiled once per expression/command pair.
    private readonly IDictionary<ExpressionKey, Delegate> _aggregateSelectorCache = new ExpressionCache<Delegate>();
    // Compiled ORDER BY key selectors, for the same reason: ordered execution must not recompile.
    private readonly IDictionary<ExpressionKey, Delegate> _sortingSelectorCache = new ExpressionCache<Delegate>();
    // Compiled SelectMany/GroupJoin selectors. Without this, Expression.Compile runs on every call and
    // dominates the operator cost (it is far more expensive than evaluating the flatten itself).
    private readonly IDictionary<ExpressionKey, Delegate> _linqSelectorCache = new ExpressionCache<Delegate>();
    private readonly IDictionary<Type, object?> _data = new Dictionary<Type, object?>();
    private bool _disposedValue;
    private readonly Dictionary<QueryPlan, object> _cmdIdx = [];
    public InMemoryDataContext()
    {
        _data[typeof(TableAlias)] = new TableAlias?[] { null };
        _queryCache = new QueryCache(_cmdIdx.Clear);
    }
    // public QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition)
    // {
    //     return new QueryCommand<TResult>(this, exp, condition);
    // }
    // Ambient state and the plan cache use the same collaborators as the SQL contexts, so the
    // in-memory provider no longer re-declares these members inline (F1 environment/cache axis).
    private readonly ContextEnvironment _environment = new(null, typeof(InMemoryDataContext), needMapping: false, logSensitiveData: false);
    private readonly QueryCache _queryCache;
    private readonly InMemoryQueryExecutor _executor = new();
    public ILogger? Logger => _environment.Logger;
    public bool NeedMapping => _environment.NeedMapping;
    public IDictionary<Type, object?> Data => _data;
    /// <summary>
    /// Per-instance compiled-delegate cache. Deliberately <b>not</b> shared, unlike
    /// <see cref="Metadata"/> and <see cref="SelectListCache"/>: its entries capture the context
    /// instance they were compiled for (see the field comment on <c>_expCache</c>).
    /// </summary>
    public IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;

    /// <summary>
    /// Provider-independent entity metadata, shared process-wide with the SQL contexts through
    /// <see cref="DataContextCache"/>. Kept as a member for API compatibility; there is a single
    /// source of truth.
    /// </summary>
    public IDictionary<Type, IEntityMetadata> Metadata => DataContextCache.Metadata;

    /// <inheritdoc cref="Metadata"/>
    public IDictionary<Type, SelectExpression[]> SelectListCache => DataContextCache.SelectListCache;
    public ILogger? CommandLogger => _environment.CommandLogger;
    public ILogger? ResultSetEnumeratorLogger => _environment.ResultSetEnumeratorLogger;
    public Dictionary<string, object> Properties => _environment.Properties;
    public Lazy<QueryCommand<bool>>? AnyCommand
    {
        get => _queryCache.AnyCommand;
        set => _queryCache.AnyCommand = value;
    }
    public bool CacheExpressions { get; set; }

    /// <summary>
    /// Internal seam for the extracted in-memory helpers: the plan cache and the per-instance compiled
    /// delegate caches stay owned by the context and are handed to <see cref="InMemoryQueryBuilder"/>.
    /// </summary>
    internal Dictionary<QueryPlan, object> CommandIndex => _cmdIdx;
    internal IDictionary<ExpressionKey, Delegate> SortingSelectorCache => _sortingSelectorCache;
    internal IDictionary<ExpressionKey, Delegate> AggregateSelectorCache => _aggregateSelectorCache;
    internal IDictionary<ExpressionKey, Delegate> ConditionFactoryCache => _conditionFactoryCache;
    internal IDictionary<ExpressionKey, Delegate> ConditionDirectCache => _conditionDirectCache;
    internal IDictionary<ExpressionKey, Delegate> LinqSelectorCache => _linqSelectorCache;

    private InMemoryPreparedQueryCommand<TResult> GetCacheEntry<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        return (InMemoryPreparedQueryCommand<TResult>)GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
    }
    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => InMemoryQueryBuilder.GetPreparedQueryCommand(this, queryCommand, createEnumerator, storeInCache, cancellationToken);
    protected CreateEnumeratorDelegate<TResult> BuildCreateEnumeratorDelegate<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
        => InMemoryQueryBuilder.BuildCreateEnumeratorDelegate(this, queryCommand, cancellationToken);
    private CreateEnumeratorDelegate<TResult> BuildLinqSourceDelegate<TResult>(LinqSourceExpression source)
        => InMemoryLinqSource.BuildLinqSourceDelegate<TResult>(source, this, miApplySelectMany, miApplyGroupJoin, miCreateEnumeratorAdapter);

    /// <summary>
    /// Applies <c>SelectMany</c> over the prepared outer command: for every outer row the collection
    /// selector is evaluated and each element is yielded (optionally projected with the result
    /// selector). The result is buffered so the enumerator supports both sync and async terminals.
    /// </summary>
    private IAsyncEnumerator<TResult> ApplySelectMany<TOuter, TCollection, TResult>(LinqSourceExpression source, object[]? @params, CancellationToken cancellationToken)
        => InMemoryLinqSource.ApplySelectMany<TOuter, TCollection, TResult>(this, source, @params, cancellationToken);

    /// <summary>
    /// Applies <c>GroupJoin</c>: the inner command is materialised once into a key lookup, the outer
    /// rows are read and each is projected with the matching inner rows (empty for no match). The
    /// lookup keeps the operator O(outer + inner) rather than O(outer × inner).
    /// </summary>
    private IAsyncEnumerator<TResult> ApplyGroupJoin<TOuter, TInner, TKey, TResult>(LinqSourceExpression source, object[]? @params, CancellationToken cancellationToken)
        => InMemoryLinqSource.ApplyGroupJoin<TOuter, TInner, TKey, TResult>(this, source, @params, cancellationToken);

    private TDelegate GetCompiledLinqSelector<TDelegate>(LambdaExpression selector, QueryCommand queryCommand) where TDelegate : Delegate
        => InMemoryLinqSource.GetCompiledLinqSelector<TDelegate>(selector, queryCommand, _linqSelectorCache);

    private static void Enumerate<T>(IAsyncEnumerator<T> enumerator, Action<T> body)
        => InMemoryLinqSource.Enumerate(enumerator, body);

    private static void Materialize<T>(IAsyncEnumerator<T> enumerator, List<T> rows)
        => InMemoryLinqSource.Materialize(enumerator, rows);

    private IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, object[] @params, CancellationToken cancellationToken)
        => InMemoryQueryBuilder.CreateEnumerator<TResult, TEntity>(this, queryCommand, cacheEntry, @params, cancellationToken);

    private Delegate? GetAggregateSelector<TEntity>(Expression? selectorBody, ParameterExpression? parameter, Type valueType, QueryCommand queryCommand)
        => InMemoryGrouping.GetAggregateSelector<TEntity>(selectorBody, parameter, valueType, queryCommand, _aggregateSelectorCache);

    private static bool TryGetAggregate(QueryCommand queryCommand, out string? name, out ParameterExpression? parameter, out Expression? selectorBody)
        => InMemoryGrouping.TryGetAggregate(queryCommand, out name, out parameter, out selectorBody);

    private IAsyncEnumerator<TResult> CreateGroupedEnumerator<TResult, TEntity>(
        QueryCommand<TResult> queryCommand,
        InMemoryPreparedQueryCommand<TResult> cacheEntry,
        IEnumerable<TEntity> data,
        object[]? @params)
        => InMemoryGrouping.CreateGroupedEnumerator<TResult, TEntity>(this, queryCommand, cacheEntry, data, @params);

    private static List<TResult> ApplyProjectedOrdering<TResult>(List<TResult> results, QueryCommand queryCommand)
        => InMemoryOrdering.ApplyProjectedOrdering(results, queryCommand);

    private IAsyncEnumerator<TResult> CreateSetOperationEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
        => InMemorySetOperations.CreateSetOperationEnumerator(this, queryCommand, @params, cancellationToken);

    private IEnumerable<TEntity> ApplyOrdering<TEntity>(IEnumerable<TEntity> data, QueryCommand queryCommand)
        => InMemoryOrdering.ApplyOrdering(data, queryCommand, _sortingSelectorCache);

    private IAsyncEnumerable<TEntity> OrderAsyncEnumerable<TEntity>(
        IAsyncEnumerable<TEntity> source,
        QueryCommand queryCommand,
        CancellationToken cancellationToken)
        => InMemoryOrdering.OrderAsyncEnumerable(source, queryCommand, cancellationToken, _sortingSelectorCache);
#if !PARAM_CONDITION
    private Func<TEntity, bool>? GetCondition<TEntity>(Expression? condition, object[] @params)
    {
        if (condition is not null && @params is not null && @params.Length > 0)
        {
            var replaceParam = new ParamExpressionVisitor(@params);
            var lambda = (Expression<Func<TEntity, bool>>)replaceParam.Visit(condition);
            var expKey = new ExpressionKey(lambda);
            if (!_expCache.TryGetValue(expKey, out var del))
            {
                del = lambda.Compile();
                _expCache[expKey] = del;
            }

            return (Func<TEntity, bool>)del;
        }

        return null;
    }
#endif
    IEnumerable<TResult> LoopJoin<TLeft, TRight, TResult>(QueryCommand queryCommand, IEnumerable<TLeft>? leftData, JoinExpression join, int dim)
        => InMemoryJoin.LoopJoin<TLeft, TRight, TResult>(this, queryCommand, leftData, join, dim);

    private static Func<TLeft, TRight, bool> CompileJoinCondition<TLeft, TRight>(JoinExpression join)
        => InMemoryProjectionFactory.CompileJoinCondition<TLeft, TRight>(join);
    private static Type CreateProjectionType(Type firstType, Type secondType, int dim)
        => InMemoryProjectionFactory.CreateProjectionType(firstType, secondType, dim);
    private static IProjection CreateProjection<TLeft, TRight>(TLeft left, TRight right, int dim)
        => InMemoryProjectionFactory.CreateProjection(left, right, dim);
    private IAsyncEnumerator<TResult> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, IAsyncEnumerator<TEntity> enumerator)
        => InMemoryQueryBuilder.CreateEnumeratorAdapter<TResult, TEntity>(this, queryCommand, cacheEntry, enumerator);
    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
    {
        return new FromExpression(srcType);
    }

    public Expression MapColumn(SelectExpression column, Expression param)
    {
        var replace = new ReplaceParameterExpressionVisitor(param);
        return replace.Visit(column.Expression)!;
        //return Expression.PropertyOrField(param, column.PropertyName!);
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        //   queryCommand.RemovePayload<CreateEnumeratorPayload>();
    }

    public ValueTask DisposeAsync()
    {
        // Route through Dispose() so _disposedValue is set consistently with Dispose().
        Dispose();

        return ValueTask.CompletedTask;
    }

    record CreateEnumeratorPayload(Delegate Delegate);
    record CreateMainEnumeratorPayload(Delegate Delegate);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~InMemoryDataProvider()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    // public void Compile<TResult>(QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken)
    // {
    //     if (!queryCommand.IsPrepared)
    //         queryCommand.PrepareCommand(cancellationToken);

    //     //query.Compiled = CreateCompiledQuery(query);

    //     var @this = Expression.Constant(this);
    //     var param = Expression.Parameter(typeof(QueryCommand<TResult>));
    //     var callExp = Expression.Call(@this, miCreateCompiledQuery.MakeGenericMethod(typeof(TResult), queryCommand.EntityType),
    //         param
    //     );

    //     var key = new ExpressionKey(callExp, _expCache, queryCommand);
    //     Func<QueryCommand<TResult>, object> createCompiledQueryDelegate;
    //     if (!_expCache.TryGetValue(key, out var del))
    //     {
    //         var body = Expression.Convert(callExp, typeof(object));
    //         createCompiledQueryDelegate = Expression.Lambda<Func<QueryCommand<TResult>, object>>(body, param).Compile();
    //         _expCache[key] = createCompiledQueryDelegate;
    //     }
    //     else
    //         createCompiledQueryDelegate = (Func<QueryCommand<TResult>, object>)del;

    //     var ce = new InMemoryPreparedQueryCommand<TResult>(createCompiledQueryDelegate(queryCommand), BuildCreateEnumeratorDelegate(queryCommand, cancellationToken));
    //     queryCommand._compiledQuery = ce;
    //     ce.Enumerator = ce.CreateEnumerator(queryCommand, ce, null, cancellationToken)!;
    // }

    private PreparedQueryCommand<TResult, TEntity> CreateCompiledQuery<TResult, TEntity>(QueryCommand<TResult> query)
        => InMemoryQueryBuilder.CreateCompiledQuery<TResult, TEntity>(this, query);

    /// <summary>
    /// Internal seam for <see cref="InMemoryGrouping"/>: the reflection-addressed
    /// <c>CreateCompiledQuery</c> stays private (it is resolved via <c>NonPublic | Instance</c>), so
    /// the grouping helper reaches the same compiled query through this accessor.
    /// </summary>
    internal PreparedQueryCommand<TResult, TEntity> GetCompiledQuery<TResult, TEntity>(QueryCommand<TResult> queryCommand)
        => CreateCompiledQuery<TResult, TEntity>(queryCommand);

    private (Func<object[]?, Func<TEntity, bool>>? Factory, Func<TEntity, bool>? Direct) GetConditionPredicates<TResult, TEntity>(QueryCommand<TResult> query, Expression<Func<TEntity, bool>> condition)
        => InMemoryConditionFactory.GetConditionPredicates(query, condition, _conditionFactoryCache, _conditionDirectCache);

    // public Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    // {
    //     return Task.FromResult((IEnumerator<TResult>)CreateAsyncEnumerator(queryCommand, @params, cancellationToken));
    // }

    public Func<Func<TEntity, TResult>> GetMap<TResult, TEntity>(QueryCommand<TResult> queryCommand)
        => InMemoryRowMaterializer.GetMap<TResult, TEntity>(this, queryCommand, _expCache);
    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
        => _executor.CreateEnumerator(preparedQueryCommand, @params);

    /// <summary>
    /// In-memory override: returns the enumerator directly (it is already <see cref="IEnumerable{T}"/>),
    /// avoiding the compiler-generated <c>yield</c> state machine used by the default interface method.
    /// </summary>
    public IEnumerable<TResult> GetEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[]? @params)
        => _executor.GetEnumerable(preparedCommand, @params);
    protected IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
        => InMemoryQueryBuilder.CreateAsyncEnumerator(this, queryCommand, @params, cancellationToken);
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

    public Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.ToListAsync(preparedQueryCommand, @params, cancellationToken);

    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.ToList(preparedQueryCommand, @params);

    public Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
        => _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull, cancellationToken);

    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
        => _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull);

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.First(preparedQueryCommand, @params);

    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.FirstOrDefault(preparedQueryCommand, @params);
    public Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.FirstAsync(preparedQueryCommand, @params, cancellationToken);

    public Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.FirstOrDefaultAsync(preparedQueryCommand, @params, cancellationToken);

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.Single(preparedQueryCommand, @params);

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.SingleOrDefault(preparedQueryCommand, @params);
    public Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.SingleAsync(preparedQueryCommand, @params, cancellationToken);

    public Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.SingleOrDefaultAsync(preparedQueryCommand, @params, cancellationToken);

    public void PurgeQueryCache() => _queryCache.PurgeQueryCache();
}
