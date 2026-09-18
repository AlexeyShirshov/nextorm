#define PARAM_CONDITION
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public partial class InMemoryContext : IDataContext
{
    private readonly static MethodInfo miCreateAsyncEnumerator = typeof(InMemoryContext).GetMethod(nameof(CreateAsyncEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateEnumeratorAdapter = typeof(InMemoryContext).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateEnumerator = typeof(InMemoryContext).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miLoopJoin = typeof(InMemoryContext).GetMethod(nameof(LoopJoin), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateCompiledQuery = typeof(InMemoryContext).GetMethod(nameof(CreateCompiledQuery), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miApplySelectMany = typeof(InMemoryContext).GetMethod(nameof(ApplySelectMany), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miApplyGroupJoin = typeof(InMemoryContext).GetMethod(nameof(ApplyGroupJoin), BindingFlags.NonPublic | BindingFlags.Instance)!;
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
    public InMemoryContext()
    {
        _data[typeof(TableAlias)] = new TableAlias?[] { null };
    }
    // public QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition)
    // {
    //     return new QueryCommand<TResult>(this, exp, condition);
    // }
    private readonly Dictionary<string, object> _properties = [];
    public ILogger? Logger { get; }
    public bool NeedMapping => false;
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
    public IDictionary<Type, IEntityMeta> Metadata => DataContextCache.Metadata;

    /// <inheritdoc cref="Metadata"/>
    public IDictionary<Type, SelectExpression[]> SelectListCache => DataContextCache.SelectListCache;
    public ILogger? CommandLogger { get; }
    public ILogger? ResultSetEnumeratorLogger { get; }
    public Dictionary<string, object> Properties => _properties;
    public Lazy<QueryCommand<bool>>? AnyCommand { get; set; }
    public bool CacheExpressions { get; set; }

    private InMemoryPreparedQueryCommand<TResult> GetCacheEntry<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        return (InMemoryPreparedQueryCommand<TResult>)GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
    }
    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
    {
        QueryPlan? queryPlan = null;
        IPreparedQueryCommand<TResult>? planCache = null;

        if (!queryCommand.IsPrepared) queryCommand.PrepareCommand(false, cancellationToken);
        else queryCommand.RefreshInValuesShape();

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = new QueryPlan(queryCommand, null);
            if (_cmdIdx.TryGetValue(queryPlan, out var planCache2)) planCache = planCache2 as IPreparedQueryCommand<TResult>;
        }

        if (planCache is null)
        {
            var @this = Expression.Constant(this);
            var param = Expression.Parameter(typeof(QueryCommand<TResult>));
            var callExp = Expression.Call(@this, miCreateCompiledQuery.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!),
                param
            );

            var key = new ExpressionKey(callExp, queryCommand);
            Func<QueryCommand<TResult>, object> createCompiledQueryDelegate;
            if (!_expCache.TryGetValue(key, out var del))
            {
                var body = Expression.Convert(callExp, typeof(object));
                createCompiledQueryDelegate = Expression.Lambda<Func<QueryCommand<TResult>, object>>(body, param).Compile();
                _expCache[key] = createCompiledQueryDelegate;
            }
            else
                createCompiledQueryDelegate = (Func<QueryCommand<TResult>, object>)del;

            var ce = new InMemoryPreparedQueryCommand<TResult>(createCompiledQueryDelegate(queryCommand), BuildCreateEnumeratorDelegate(queryCommand, cancellationToken), queryCommand);
            ce.Enumerator = ce.CreateEnumerator(queryCommand, ce, null, cancellationToken)!;

            planCache = ce;

            if (storeInCache && queryCommand.Cache)
                _cmdIdx[queryPlan!.GetCacheVersion()] = planCache;

        }

        return planCache!;
    }
    protected CreateEnumeratorDelegate<TResult> BuildCreateEnumeratorDelegate<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        if (queryCommand.From?.TableFunction is not null)
            throw new NotSupportedException("Table-valued function sources are not supported by the in-memory provider.");

        if (queryCommand.From?.LinqSource is { } linqSource)
            return BuildLinqSourceDelegate<TResult>(linqSource);

        if (queryCommand.From?.SubQuery is not null)
        {
            if (!string.IsNullOrEmpty(queryCommand.From.Table))
                throw new DataContextException("Cannot use table as source for in-memory provider");

            var subQuery = queryCommand.From.SubQuery;
            var subQueryType = subQuery.GetType();
            if (!subQueryType.IsGenericType)
                throw new DataContextException("Cannot use table as source for in-memory provider");

            var resultType = subQueryType.GenericTypeArguments[0];

            //var delPayload = subQuery.GetOrAddPayload(() =>
            //{

            var @this = Expression.Constant(this);
            var p1 = Expression.Parameter(typeof(QueryCommand<TResult>));
            var p2 = Expression.Parameter(typeof(InMemoryPreparedQueryCommand<TResult>));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var p4 = Expression.Parameter(typeof(object[]));
            var callCreateEnumerator = Expression.Call(@this, miCreateAsyncEnumerator.MakeGenericMethod(resultType),
                Expression.Convert(Expression.Property(p1, nameof(QueryCommand.FromQuery)), subQueryType), p4, p3
            );


            var callCreateEnumeratorAdapter = Expression.Call(@this, miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType),
                p1, p2, callCreateEnumerator
            );

            var del = Expression.Lambda<CreateEnumeratorDelegate<TResult>>(callCreateEnumeratorAdapter, p1, p2, p4, p3).Compile();
            //var delPayload = new CreateMainEnumeratorPayload();
            // });

            return del;
        }
        else
        {
            // var delPayload = queryCommand.GetOrAddPayload(() =>
            // {

            var @this = Expression.Constant(this);
            var p1 = Expression.Parameter(typeof(QueryCommand<TResult>));
            var p2 = Expression.Parameter(typeof(InMemoryPreparedQueryCommand<TResult>));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var p4 = Expression.Parameter(typeof(object[]));
            var callExp = Expression.Call(@this, miCreateEnumerator.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!),
                p1, p2, p4, p3
            );

            var key = new ExpressionKey(callExp, queryCommand);
            CreateEnumeratorDelegate<TResult> factory;
            if (!_expCache.TryGetValue(key, out var d))
            {
                factory = Expression.Lambda<CreateEnumeratorDelegate<TResult>>(callExp, p1, p2, p4, p3).Compile();
                _expCache[key] = factory;
            }
            else
                factory = (CreateEnumeratorDelegate<TResult>)d;

            return factory;
        }
    }
    private CreateEnumeratorDelegate<TResult> BuildLinqSourceDelegate<TResult>(LinqSourceExpression source)
    {
        // SelectMany/GroupJoin produce an IAsyncEnumerator<ResultType> from the outer (and, for
        // GroupJoin, inner) command. The downstream pipeline (WHERE/map/DISTINCT) is then exactly the
        // same as for a subquery source, so it is reused through CreateEnumeratorAdapter.
        var resultType = source.ResultType;
        var apply = source.IsGroupJoin
            ? miApplyGroupJoin.MakeGenericMethod(source.OuterType, source.InnerType!, source.KeyType!, resultType)
            : miApplySelectMany.MakeGenericMethod(source.OuterType, source.CollectionType!, resultType);
        var createAdapter = miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType);

        return (queryCommand, cacheEntry, @params, cancellationToken) =>
        {
            var flattened = apply.Invoke(this, [source, @params, cancellationToken])!;
            return (IAsyncEnumerator<TResult>)createAdapter.Invoke(this, [queryCommand, cacheEntry, flattened])!;
        };
    }

    /// <summary>
    /// Applies <c>SelectMany</c> over the prepared outer command: for every outer row the collection
    /// selector is evaluated and each element is yielded (optionally projected with the result
    /// selector). The result is buffered so the enumerator supports both sync and async terminals.
    /// </summary>
    private IAsyncEnumerator<TResult> ApplySelectMany<TOuter, TCollection, TResult>(LinqSourceExpression source, object[]? @params, CancellationToken cancellationToken)
    {
        var outer = CreateAsyncEnumerator((QueryCommand<TOuter>)source.OuterCommand, @params, cancellationToken);
        var collectionSelector = GetCompiledLinqSelector<Func<TOuter, IEnumerable<TCollection>>>(source.CollectionSelector!, source.OuterCommand);
        var resultSelector = source.ResultSelector is null
            ? null
            : GetCompiledLinqSelector<Func<TOuter, TCollection, TResult>>(source.ResultSelector, source.OuterCommand);

        var rows = new List<TResult>();
        Enumerate(outer, current =>
        {
            foreach (var item in collectionSelector(current))
                rows.Add(resultSelector is null ? (TResult)(object)item! : resultSelector(current, item));
        });

        return new InMemoryListEnumerator<TResult>(rows, false);
    }

    /// <summary>
    /// Applies <c>GroupJoin</c>: the inner command is materialised once into a key lookup, the outer
    /// rows are read and each is projected with the matching inner rows (empty for no match). The
    /// lookup keeps the operator O(outer + inner) rather than O(outer × inner).
    /// </summary>
    private IAsyncEnumerator<TResult> ApplyGroupJoin<TOuter, TInner, TKey, TResult>(LinqSourceExpression source, object[]? @params, CancellationToken cancellationToken)
    {
        var outerKeySelector = GetCompiledLinqSelector<Func<TOuter, TKey>>(source.OuterKeySelector!, source.OuterCommand);
        var innerKeySelector = GetCompiledLinqSelector<Func<TInner, TKey>>(source.InnerKeySelector!, source.InnerCommand!);
        var resultSelector = GetCompiledLinqSelector<Func<TOuter, IEnumerable<TInner>, TResult>>(source.ResultSelector!, source.OuterCommand);

        var innerRows = new List<TInner>();
        Materialize(CreateAsyncEnumerator((QueryCommand<TInner>)source.InnerCommand!, @params, cancellationToken), innerRows);

        // Null keys cannot be dictionary keys; they are collected separately (a null outer key matches
        // only null inner keys, mirroring EqualityComparer<TKey>.Default used by LINQ's GroupJoin).
        // CS8714: TKey is unconstrained, but null keys never reach the dictionary.
#pragma warning disable CS8714
        var lookup = new Dictionary<TKey, List<TInner>>();
#pragma warning restore CS8714
        List<TInner>? nullKeys = null;
        foreach (var row in innerRows)
        {
            var key = innerKeySelector(row);
            if (key is null)
            {
                (nullKeys ??= []).Add(row);
                continue;
            }

            if (!lookup.TryGetValue(key, out var list))
                lookup[key] = list = [];
            list.Add(row);
        }

        var outer = CreateAsyncEnumerator((QueryCommand<TOuter>)source.OuterCommand, @params, cancellationToken);
        var rows = new List<TResult>();
        Enumerate(outer, current =>
        {
            var key = outerKeySelector(current);
            List<TInner>? group;
            if (key is null)
                group = nullKeys;
            else if (!lookup.TryGetValue(key, out group))
                group = null;

            rows.Add(resultSelector(current, group ?? (IEnumerable<TInner>)Array.Empty<TInner>()));
        });

        return new InMemoryListEnumerator<TResult>(rows, false);
    }

    /// <summary>
    /// Compiles a <c>SelectMany</c>/<c>GroupJoin</c> selector once and caches it by the expression's
    /// structural (closure-aware) key. Expression compilation is orders of magnitude more expensive
    /// than the per-row work, so the operator must not recompile on every call.
    /// </summary>
    private TDelegate GetCompiledLinqSelector<TDelegate>(LambdaExpression selector, QueryCommand queryCommand) where TDelegate : Delegate
    {
        var key = new ExpressionKey(selector, queryCommand);
        if (_linqSelectorCache.TryGetValue(key, out var cached))
            return (TDelegate)cached;

        var compiled = (TDelegate)selector.Compile();
        _linqSelectorCache[key] = compiled;
        return compiled;
    }

    /// <summary>
    /// Streams an enumerator through <paramref name="body"/>. Prefers the synchronous view when the
    /// source provides one; an async-only source is drained by blocking, matching
    /// <see cref="Materialize{T}"/>.
    /// </summary>
    private static void Enumerate<T>(IAsyncEnumerator<T> enumerator, Action<T> body)
    {
        try
        {
            if (enumerator is IEnumerator<T> sync)
            {
                while (sync.MoveNext())
                    body(sync.Current);
            }
            else
            {
                while (enumerator.MoveNextAsync().GetAwaiter().GetResult())
                    body(enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Buffers an enumerator. Prefers the synchronous view when the source provides one (buffered
    /// in-memory rows do) so no thread is blocked; an async-only source is drained by blocking, which
    /// matches the provider's other buffered operators (grouping, set operations).
    /// </summary>
    private static void Materialize<T>(IAsyncEnumerator<T> enumerator, List<T> rows)
    {
        try
        {
            if (enumerator is IEnumerator<T> sync)
            {
                while (sync.MoveNext())
                    rows.Add(sync.Current);
            }
            else
            {
                while (enumerator.MoveNextAsync().GetAwaiter().GetResult())
                    rows.Add(enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    private IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, object[] @params, CancellationToken cancellationToken)
    {
        if (queryCommand.UnionQuery is not null)
            return CreateSetOperationEnumerator(queryCommand, @params, cancellationToken);

        if (cacheEntry.Resolver is not null)
            return cacheEntry.Resolver(@params);

        object? v = null;

        IEnumerable<TEntity>? data = cacheEntry.Data as IEnumerable<TEntity>;
        if (data is not null)
            goto next;

        if (cacheEntry.Data is not IAsyncEnumerable<TEntity> asyncData)
        {
            if (!_data.TryGetValue(typeof(TEntity), out v) || v is not IAsyncEnumerable<TEntity> av)
                goto next;

            asyncData = av;
        }

        cacheEntry.Data = asyncData;
        if (TryGetAggregate(queryCommand, out var asyncAggregateName, out _, out _))
            throw new NotSupportedException($"Aggregate '{asyncAggregateName}' over an async source is not supported by the in-memory provider.");
        if (queryCommand.GroupBy is not null)
            throw new NotSupportedException("GroupBy over an async source is not supported by the in-memory provider.");
        if (queryCommand.Sorting is not null)
        {
            // An async source cannot be sorted lazily without buffering; wrap it in an iterator that
            // materialises once and then serves the ordered rows. The raw source is cached above so a
            // repeat call does not wrap an already-wrapped sequence.
            var ordered = OrderAsyncEnumerable(asyncData, queryCommand, cancellationToken);
            return CreateEnumeratorAdapter(queryCommand, cacheEntry, ordered.GetAsyncEnumerator(cancellationToken));
        }
        return CreateEnumeratorAdapter(queryCommand, cacheEntry, asyncData.GetAsyncEnumerator(cancellationToken));

    next:
        {

            if (queryCommand.Joins?.Length > 0 && typeof(TEntity).IsAssignableTo(typeof(IProjection)))
            {
                var dim = 2;
                object? joinResult = null;
                Type? firstType = null;
                for (var idx = 0; idx < queryCommand.Joins.Length; idx++)
                {
                    var join = queryCommand.Joins[idx];

                    firstType ??= join.JoinCondition?.Parameters[0].Type ?? typeof(TEntity).GetGenericArguments()[0];
                    var secondType = join.JoinCondition?.Parameters[1].Type ?? join.EntityType
                        ?? throw new NotSupportedException($"A {join.JoinType} join requires a join condition or an entity type");

                    var prjType = CreateProjectionType(firstType, secondType, dim);

                    var @this = Expression.Constant(this);
                    var p1 = Expression.Parameter(typeof(QueryCommand));
                    var p2 = Expression.Parameter(typeof(object));
                    var p3 = Expression.Parameter(typeof(JoinExpression));
                    var p4 = Expression.Parameter(typeof(int));
                    var callExp = Expression.Call(@this, miLoopJoin.MakeGenericMethod(firstType, secondType, prjType),
                        p1,
                        Expression.Convert(p2, typeof(IEnumerable<>).MakeGenericType(firstType)),
                        p3,
                        p4
                    );
                    var key = new ExpressionKey(callExp, queryCommand);
                    if (!_expCache.TryGetValue(key, out var del))
                    {
                        var d = Expression.Lambda<Func<QueryCommand, object?, JoinExpression, int, object>>(callExp,
                            p1,
                            p2,
                            p3,
                            p4
                        ).Compile();
                        _expCache[key] = d;
                        del = d;
                    }

                    joinResult = ((Func<QueryCommand, object?, JoinExpression, int, object>)del)(queryCommand, joinResult, join, dim);

                    firstType = prjType;

                    dim++;
                }

                data = (IEnumerable<TEntity>)joinResult!;
            }
            else if (data is null)
            {
                if (v is not IEnumerable<TEntity> ev)
                    ev = Array.Empty<TEntity>();

                data = ev;
            }

            if (queryCommand.GroupBy is not null)
                return CreateGroupedEnumerator<TResult, TEntity>(queryCommand, cacheEntry, data!, @params);

            if (queryCommand.Sorting is not null)
            {
                data = ApplyOrdering(data, queryCommand);
            }
            cacheEntry.Data = data;

            if (TryGetAggregate(queryCommand, out var aggregateName, out var aggregateParam, out var aggregateBody))
            {
                if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> aggregateCompiled)
                {
                    aggregateCompiled = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
                    cacheEntry.CompiledQuery = aggregateCompiled;
                }

                // Aggregates must see the same filtered rows as the mapped path: apply WHERE before
                // folding, otherwise Count/Sum/... would include rows the query excludes.
                var aggregatePredicate = aggregateCompiled.ConditionDirect;
                if (aggregateCompiled.ConditionFactory is not null && @params is not null)
                    aggregatePredicate = aggregateCompiled.ConditionFactory(@params);

                var aggregateSource = aggregatePredicate is null ? data! : data!.Where(aggregatePredicate);

                var aggregateValueType = aggregateBody?.Type ?? typeof(object);
                var aggregateSelector = GetAggregateSelector<TEntity>(aggregateBody, aggregateParam, aggregateValueType, queryCommand);
                var aggregateValue = InMemoryAggregates.Compute(aggregateSource, aggregateName!, aggregateSelector, typeof(TEntity), typeof(TResult), aggregateValueType);

                return new InMemoryScalarEnumerator<TResult>((TResult)aggregateValue!);
            }

            if (cacheEntry.Enumerator is not InMemoryEnumerator<TResult, TEntity> enumerator)
            {
                if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
                {
                    compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
                    cacheEntry.CompiledQuery = compiledQuery;
                }

                enumerator = new InMemoryEnumerator<TResult, TEntity>(compiledQuery, cancellationToken, queryCommand.IsDistinct);
#if PARAM_CONDITION
                enumerator.Init(data!, @params);
#else
                enumerator.Init(data, GetCondition<TEntity>(queryCommand.Condition, @params));
#endif
                cacheEntry.Enumerator = enumerator;
            }
            else
            {
#if PARAM_CONDITION
                enumerator.Init(data!, @params);
#else
                enumerator.Init(data, GetCondition<TEntity>(queryCommand.Condition, @params));
#endif
            }

            // The compiled plan does not change between calls, so cache a resolver that only
            // re-initialises the enumerator with new parameter values.
            var resolvedEnumerator = enumerator;
            var resolvedData = data!;
            cacheEntry.Resolver = p => { resolvedEnumerator.Init(resolvedData, p); return resolvedEnumerator; };

            return enumerator;
        }
    }

    private Delegate? GetAggregateSelector<TEntity>(Expression? selectorBody, ParameterExpression? parameter, Type valueType, QueryCommand queryCommand)
    {
        if (selectorBody is null || parameter is null) return null;

        var key = new ExpressionKey(selectorBody, queryCommand);
        if (_aggregateSelectorCache.TryGetValue(key, out var cached))
            return cached;

        var compiled = InMemoryAggregates.CompileSelector<TEntity>(selectorBody, parameter, valueType);
        _aggregateSelectorCache[key] = compiled;
        return compiled;
    }

    /// <summary>
    /// Detects an aggregate projection (<c>NORM.SQL.min/max/sum/avg/count/...</c>) in a single-column
    /// select list, so the enumerator can compute the value over the whole source instead of mapping
    /// every row through the (CLR no-op) aggregate method.
    /// </summary>
    private static bool TryGetAggregate(QueryCommand queryCommand, out string? name, out ParameterExpression? parameter, out Expression? selectorBody)
    {
        name = null;
        parameter = null;
        selectorBody = null;

        if (queryCommand.SelectList is not [var column]) return false;
        if (column.Expression is not LambdaExpression lambda) return false;
        if (lambda.Body is not MethodCallExpression call) return false;
        if (call.Method.DeclaringType != typeof(NORM.NORM_SQL)) return false;
        if (!InMemoryAggregates.IsAggregate(call.Method.Name)) return false;

        name = call.Method.Name;
        parameter = lambda.Parameters.Count > 0 ? lambda.Parameters[0] : null;

        if (call.Arguments.Count > 0)
        {
            switch (call.Arguments[0])
            {
                // count() is emitted as a single empty-array argument for the params array.
                case NewArrayExpression { Expressions.Count: 0 }:
                    break;
                case NewArrayExpression { Expressions.Count: 1 } array:
                    selectorBody = array.Expressions[0];
                    break;
                case NewArrayExpression:
                    throw new NotSupportedException($"Aggregate '{name}' over multiple properties is not supported by the in-memory provider.");
                default:
                    selectorBody = call.Arguments[0];
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Evaluates a <c>GROUP BY</c> query in process: the source is filtered, grouped by the key
    /// selector, and for each group the <c>HAVING</c> predicate and the projection are evaluated with
    /// aggregate calls folded to per-group constants. ORDER BY is applied to the projected rows,
    /// matching SQL where grouping precedes ordering.
    /// </summary>
    private IAsyncEnumerator<TResult> CreateGroupedEnumerator<TResult, TEntity>(
        QueryCommand<TResult> queryCommand,
        InMemoryPreparedQueryCommand<TResult> cacheEntry,
        IEnumerable<TEntity> data,
        object[]? @params)
    {
        if (queryCommand.ProjectionExpression is not LambdaExpression projection)
            throw new NotSupportedException("GroupBy requires a projection in the in-memory provider.");

        if (queryCommand.GroupingType != GroupingType.None)
            throw new NotSupportedException("The ROLLUP/CUBE grouping modifiers are not supported by the in-memory provider.");

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiled)
        {
            compiled = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
            cacheEntry.CompiledQuery = compiled;
        }

        Func<TEntity, bool>? predicate = compiled.ConditionDirect;
        if (compiled.ConditionFactory is not null && @params is not null)
            predicate = compiled.ConditionFactory(@params);

        var source = predicate is null ? data : data.Where(predicate);

        var entityParam = (ParameterExpression)projection.Parameters[0];
        var keySelectorBody = Expression.Convert(queryCommand.GroupBy!.Body, typeof(object));
        var keySelector = Expression.Lambda<Func<TEntity, object?>>(keySelectorBody, (ParameterExpression)queryCommand.GroupBy.Parameters[0]).Compile();

        // Grouping by value: anonymous types and records expose structural equality. A linear scan
        // keeps null keys working without a null-hostile dictionary key.
        var keys = new List<object?>();
        var groups = new List<List<TEntity>>();
        foreach (var row in source)
        {
            var key = keySelector(row);
            var found = false;
            for (var i = 0; i < keys.Count; i++)
            {
                if (Equals(keys[i], key))
                {
                    groups[i].Add(row);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                keys.Add(key);
                groups.Add([row]);
            }
        }

        var results = new List<TResult>(groups.Count);
        foreach (var group in groups)
        {
            if (queryCommand.Having is not null)
            {
                var havingParam = (ParameterExpression)queryCommand.Having.Parameters[0];
                var havingBody = new InMemoryGroupAggregateVisitor<TEntity>(group, havingParam).Visit(queryCommand.Having.Body);
                var having = Expression.Lambda<Func<TEntity, bool>>(havingBody, havingParam).Compile();
                if (!having(group[0])) continue;
            }

            var projectionBody = new InMemoryGroupAggregateVisitor<TEntity>(group, entityParam).Visit(projection.Body);
            var map = Expression.Lambda<Func<TEntity, TResult>>(projectionBody, entityParam).Compile();
            results.Add(map(group[0]));
        }

        if (queryCommand.Sorting is not null)
            results = ApplyProjectedOrdering(results, queryCommand);

        return new InMemoryListEnumerator<TResult>(results, queryCommand.IsDistinct);
    }

    /// <summary>
    /// Orders projected rows by the select-list column index, as SQL does for an <c>ORDER BY</c> on a
    /// grouped query. Expression-based ordering is not supported on grouped in-memory results.
    /// </summary>
    private static List<TResult> ApplyProjectedOrdering<TResult>(List<TResult> results, QueryCommand queryCommand)
    {
        if (queryCommand.SelectList is null)
            throw new NotSupportedException("Grouped ordering requires a select list in the in-memory provider.");

        IOrderedEnumerable<TResult>? ordered = null;
        foreach (var sorting in queryCommand.Sorting!)
        {
            if (sorting.ColumnIndex is not int columnIndex)
                throw new NotSupportedException("Grouped ordering by expression is not supported by the in-memory provider; order by column index instead.");

            var propertyName = queryCommand.SelectList[columnIndex - 1].PropertyName
                ?? throw new NotSupportedException($"{nameof(Sorting)} column {columnIndex} has no name in the in-memory provider.");
            var property = typeof(TResult).GetProperty(propertyName)
                ?? throw new NotSupportedException($"Grouped result type '{typeof(TResult).Name}' has no property '{propertyName}'.");

            object? Key(TResult row) => property.GetValue(row);

            ordered = sorting.Direction == OrderDirection.Asc
                ? (ordered is null ? results.OrderBy(Key) : ordered.ThenBy(Key))
                : (ordered is null ? results.OrderByDescending(Key) : ordered.ThenByDescending(Key));
        }

        return ordered?.ToList() ?? results;
    }

    /// <summary>
    /// Evaluates a set operation (<c>UNION</c>/<c>UNION ALL</c>/<c>INTERSECT</c>/<c>INTERSECT ALL</c>/
    /// <c>EXCEPT</c>/<c>EXCEPT ALL</c>): both operands are materialised and combined with SQL value
    /// semantics, then the whole result is ordered/paged.
    /// </summary>
    private IAsyncEnumerator<TResult> CreateSetOperationEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var left = MaterializeBuffered(queryCommand.CloneWithoutUnion(), @params, cancellationToken);

        if (queryCommand.UnionQuery is not QueryCommand<TResult> rightQuery)
            throw new NotSupportedException("Set operations between different result types are not supported by the in-memory provider.");

        var right = MaterializeBuffered(rightQuery, @params, cancellationToken);

        var combined = CombineSet(left, right, queryCommand.UnionType);

        if (queryCommand.Sorting is not null)
            combined = ApplyProjectedOrdering(combined, queryCommand);

        return new InMemoryListEnumerator<TResult>(combined, false);
    }

    /// <summary>
    /// Fully materialises a command. Set operations are buffered, so an async source cannot be combined
    /// and is rejected instead of silently producing a wrong result.
    /// </summary>
    private List<TResult> MaterializeBuffered<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var prepared = (InMemoryPreparedQueryCommand<TResult>)GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        var enumerator = prepared.CreateEnumerator(prepared.QueryCommand, prepared, @params, cancellationToken);

        if (enumerator is not IEnumerator<TResult> sync)
        {
            if (enumerator is IAsyncDisposable disposable)
                disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new NotSupportedException("Set operations over an async source are not supported by the in-memory provider.");
        }

        var list = new List<TResult>();
        try
        {
            while (sync.MoveNext()) list.Add(sync.Current);
        }
        finally
        {
            (sync as IDisposable)?.Dispose();
        }

        return list;
    }

    private static List<TResult> CombineSet<TResult>(List<TResult> left, List<TResult> right, UnionType type)
    {
        var comparer = InMemoryDistinct.GetComparer<TResult>();
        switch (type)
        {
            case UnionType.All:
            {
                var all = new List<TResult>(left.Count + right.Count);
                all.AddRange(left);
                all.AddRange(right);
                return all;
            }
            case UnionType.Distinct:
            {
                var seen = new HashSet<TResult>(comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                    if (seen.Add(item)) result.Add(item);
                foreach (var item in right)
                    if (seen.Add(item)) result.Add(item);
                return result;
            }
            case UnionType.Intersect:
            {
                var rightSet = new HashSet<TResult>(right, comparer);
                var seen = new HashSet<TResult>(comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                    if (rightSet.Contains(item) && seen.Add(item)) result.Add(item);
                return result;
            }
            case UnionType.IntersectAll:
            {
                var counts = CountValues(right, comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                {
                    var idx = FindValue(counts, item, comparer);
                    if (idx < 0 || counts[idx].Count == 0) continue;
                    counts[idx] = (counts[idx].Value, counts[idx].Count - 1);
                    result.Add(item);
                }

                return result;
            }
            case UnionType.Except:
            {
                var rightSet = new HashSet<TResult>(right, comparer);
                var seen = new HashSet<TResult>(comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                    if (!rightSet.Contains(item) && seen.Add(item)) result.Add(item);
                return result;
            }
            case UnionType.ExceptAll:
            {
                var counts = CountValues(right, comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                {
                    var idx = FindValue(counts, item, comparer);
                    if (idx >= 0 && counts[idx].Count > 0)
                        counts[idx] = (counts[idx].Value, counts[idx].Count - 1);
                    else
                        result.Add(item);
                }

                return result;
            }
            default:
                throw new NotSupportedException(type.ToString("G"));
        }
    }

    private static List<(TResult Value, int Count)> CountValues<TResult>(List<TResult> values, IEqualityComparer<TResult> comparer)
    {
        var counts = new List<(TResult Value, int Count)>();
        foreach (var item in values)
        {
            var idx = FindValue(counts, item, comparer);
            if (idx < 0) counts.Add((item, 1));
            else counts[idx] = (counts[idx].Value, counts[idx].Count + 1);
        }

        return counts;
    }

    private static int FindValue<TResult>(List<(TResult Value, int Count)> counts, TResult item, IEqualityComparer<TResult> comparer)
    {
        for (var i = 0; i < counts.Count; i++)
            if (comparer.Equals(counts[i].Value, item))
                return i;
        return -1;
    }

    /// <summary>Applies the query's <c>ORDER BY</c> to a buffered source using the compiled key selectors.</summary>
    private IEnumerable<TEntity> ApplyOrdering<TEntity>(IEnumerable<TEntity> data, QueryCommand queryCommand)
    {
        IOrderedEnumerable<TEntity>? intData = null;
        foreach (var sorting in queryCommand.Sorting!)
        {
            var del = GetSortingSelector<TEntity>(sorting, queryCommand);
            if (sorting.Direction == OrderDirection.Asc)
                intData = (intData ?? data).OrderBy(del);
            else
                intData = (intData ?? data).OrderByDescending(del);
        }
        return intData ?? data;
    }

    /// <summary>
    /// Compiles (once per expression/command pair) the ordering key selector. Without the cache an
    /// ordered query recompiled its selector on every execution, which dominated <c>Last</c>/ordered
    /// iteration.
    /// </summary>
    private Func<TEntity, object> GetSortingSelector<TEntity>(Sorting sorting, QueryCommand queryCommand)
    {
        var expression = (Expression<Func<TEntity, object>>)sorting.PreparedExpression!;
        var key = new ExpressionKey(expression, queryCommand);
        if (_sortingSelectorCache.TryGetValue(key, out var cached))
            return (Func<TEntity, object>)cached;

        var compiled = expression.Compile();
        _sortingSelectorCache[key] = compiled;
        return compiled;
    }

    /// <summary>
    /// Orders an async source by buffering it: ordering needs the whole set before the first row can
    /// be yielded, and an <see cref="IAsyncEnumerable{T}"/> cannot be re-sorted lazily.
    /// </summary>
    private async IAsyncEnumerable<TEntity> OrderAsyncEnumerable<TEntity>(
        IAsyncEnumerable<TEntity> source,
        QueryCommand queryCommand,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var data = new List<TEntity>();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            data.Add(item);

        foreach (var item in ApplyOrdering(data, queryCommand))
            yield return item;
    }
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
    {
        if (join.From.TableFunction is not null)
            throw new NotSupportedException("Table-valued function sources are not supported by the in-memory provider.");

        if (leftData is null)
        {
            //var dataPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TLeft>(Array.Empty<TLeft>().AsEnumerable()));
            if (!_data.TryGetValue(typeof(TLeft), out var vl))
            {
                vl = Array.Empty<TLeft>();
            }
            leftData = (IEnumerable<TLeft>)vl!;
        }

        //var joinPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TRight>(Array.Empty<TRight>().AsEnumerable()));
        if (!_data.TryGetValue(typeof(TRight), out var vr))
        {
            vr = Array.Empty<TRight>();
        }
        var joinPayload = (IEnumerable<TRight>)vr!;

        var res = new List<TResult>();

        switch (join.JoinType)
        {
            case JoinType.Cross:
            case JoinType.FullCross:
                foreach (var item in leftData)
                {
                    foreach (var itemInner in joinPayload)
                    {
                        res.Add((TResult)CreateProjection(item, itemInner, dim));
                    }
                }
                break;

            case JoinType.Inner:
                {
                    var condition = CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var item in leftData)
                    {
                        foreach (var itemInner in joinPayload)
                        {
                            if (condition(item, itemInner))
                            {
                                res.Add((TResult)CreateProjection(item, itemInner, dim));
                            }
                        }
                    }
                }
                break;

            case JoinType.Left:
                {
                    var condition = CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var item in leftData)
                    {
                        var matched = false;
                        foreach (var itemInner in joinPayload)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                res.Add((TResult)CreateProjection(item, itemInner, dim));
                            }
                        }

                        if (!matched)
                            res.Add((TResult)CreateProjection(item, default(TRight)!, dim));
                    }
                }
                break;

            case JoinType.Right:
                {
                    var condition = CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var itemInner in joinPayload)
                    {
                        var matched = false;
                        foreach (var item in leftData)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                res.Add((TResult)CreateProjection(item, itemInner, dim));
                            }
                        }

                        if (!matched)
                            res.Add((TResult)CreateProjection(default(TLeft)!, itemInner, dim));
                    }
                }
                break;

            case JoinType.Full:
                {
                    var condition = CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var item in leftData)
                    {
                        var matched = false;
                        foreach (var itemInner in joinPayload)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                res.Add((TResult)CreateProjection(item, itemInner, dim));
                            }
                        }

                        if (!matched)
                            res.Add((TResult)CreateProjection(item, default(TRight)!, dim));
                    }

                    foreach (var itemInner in joinPayload)
                    {
                        var matched = false;
                        foreach (var item in leftData)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                break;
                            }
                        }

                        if (!matched)
                            res.Add((TResult)CreateProjection(default(TLeft)!, itemInner, dim));
                    }
                }
                break;

            default:
                throw new NotSupportedException(join.JoinType.ToString());
        }

        return res;
    }

    private static Func<TLeft, TRight, bool> CompileJoinCondition<TLeft, TRight>(JoinExpression join)
        => ((Expression<Func<TLeft, TRight, bool>>)join.JoinCondition!).Compile();
    static Type CreateProjectionType(Type firstType, Type secondType, int dim)
    {
        var typeName = $"nextorm.core.Projection`{dim}";
        var t = typeof(InMemoryContext).Assembly.GetType(typeName) ?? throw new InvalidOperationException($"Cannot create type {typeName}");
        var types = dim switch
        {
            2 => new List<Type> { firstType, secondType },
            >= 3 => new List<Type>(firstType.GetGenericArguments()) { secondType },
            _ => throw new NotImplementedException(dim.ToString())
        };

        return t.MakeGenericType(types.ToArray());
    }
    static IProjection CreateProjection<TLeft, TRight>(TLeft left, TRight right, int dim)
    {
        if (dim == 2) return new Projection<TLeft, TRight> { t1 = left, t2 = right };
        if (left is IExtendableProjection proj)
        {
            return proj.Extend(right);
            // var (types, values) = ExtractTypesFromProjection(left);
            // types.Add(typeof(TRight));
            // var typeName = $"nextorm.core.Projection`{dim}";
            // var t = Type.GetType(typeName)!;
            // var prjType = t.MakeGenericType(types.ToArray());
            // values.Add(right!);

            // //var leftType = typeof(TLeft);

            // var bindings = values.Select((value, idx) =>
            // {
            //     var propInfo = prjType.GetProperty("t" + (idx + 1).ToString())!;
            //     return Expression.Bind(propInfo, Expression.Constant(value));
            // }).ToArray();

            // var ctor = Expression.New(prjType.GetConstructor(Type.EmptyTypes)!);

            // var memberInit = Expression.MemberInit(ctor, bindings);

            // var lambda = Expression.Lambda(memberInit);

            // return lambda.Compile().DynamicInvoke()!;
        }
        if (left is null && dim >= 3)
        {
            // A RIGHT/FULL join matched no accumulated row on this side: the whole left-hand
            // projection is absent, so materialize a projection whose earlier items keep their
            // defaults and whose last item is the joined entity. Reflection is only paid on this
            // (unmatched) path.
            var prjType = CreateProjectionType(typeof(TLeft), typeof(TRight), dim);
            var projection = (IProjection)Activator.CreateInstance(prjType)!;
            prjType.GetProperty("t" + dim)!.SetValue(projection, right);
            return projection;
        }

        throw new NotSupportedException($"Joins of dimension {dim} are not supported");

        // static (List<Type>, List<object?>) ExtractTypesFromProjection(TLeft projection)
        // {
        //     var types = new List<Type>();
        //     var values = new List<object?>();
        //     var leftType = typeof(TLeft);
        //     foreach (var propInfo in leftType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        //     {
        //         types.Add(propInfo.PropertyType);
        //         values.Add(propInfo.GetValue(projection));
        //     }
        //     return (types, values);
        // }
    }
    private IAsyncEnumerator<TResult> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, IAsyncEnumerator<TEntity> enumerator)
    {
        if (queryCommand.Joins?.Length > 0 && typeof(TEntity).IsAssignableTo(typeof(IProjection)))
        {
            throw new NotImplementedException("joins");
        }

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
        {
            compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
            cacheEntry.CompiledQuery = compiledQuery;
        }

        // An aggregate over a subquery (for example ctx.From(cmd).Count()) is not a per-row map: fold
        // the buffered subquery rows instead. Only a buffered subquery can be folded synchronously.
        if (TryGetAggregate(queryCommand, out var aggregateName, out var aggregateParam, out var aggregateBody))
        {
            if (enumerator is not IEnumerator<TEntity> sync)
                throw new NotSupportedException($"Aggregate '{aggregateName}' over an async subquery is not supported by the in-memory provider.");

            var rows = new List<TEntity>();
            try
            {
                while (sync.MoveNext()) rows.Add(sync.Current);
            }
            finally
            {
                (sync as IDisposable)?.Dispose();
            }

            if (compiledQuery.ConditionDirect is not null)
                rows = rows.Where(compiledQuery.ConditionDirect).ToList();

            var aggregateValueType = aggregateBody?.Type ?? typeof(object);
            var aggregateSelector = GetAggregateSelector<TEntity>(aggregateBody, aggregateParam, aggregateValueType, queryCommand);
            var aggregateValue = InMemoryAggregates.Compute(rows, aggregateName!, aggregateSelector, typeof(TEntity), typeof(TResult), aggregateValueType);

            return new InMemoryScalarEnumerator<TResult>((TResult)aggregateValue!);
        }

        return new InMemoryEnumeratorAdapter<TResult, TEntity>(compiledQuery, enumerator, queryCommand.IsDistinct);
    }
    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
    {
        return new FromExpression(srcType);
    }

    public Expression MapColumn(SelectExpression column, Expression param)
    {
        var replace = new ReplaceParameterVisitor(param);
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

    record CreateEnumeratorPayload(Delegate Delegate) : IPayload;
    record CreateMainEnumeratorPayload(Delegate Delegate) : IPayload;

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
    public IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(string sql, object? @params, QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken) => throw new NotImplementedException();
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
    {
        Func<TEntity, object[]?, bool>? conditionDelegate = null;
        Func<object[]?, Func<TEntity, bool>>? conditionFactory = null;
        Func<TEntity, bool>? conditionDirect = null;

        if (query.PreparedCondition is Expression<Func<TEntity, bool>> condition)
        {
            var key = new ExpressionKey(condition, query);
            if (!_expCache.TryGetValue(key, out var d))
            {
                var p = Expression.Parameter(typeof(object[]));
                var replaceParam = new ParamExpressionVisitor2(p);
                var lambda = (LambdaExpression)replaceParam.Visit(condition)!;

                var @params = new List<ParameterExpression>(lambda.Parameters) { p };
                conditionDelegate = Expression.Lambda<Func<TEntity, object[]?, bool>>(lambda.Body, @params).Compile();
                _expCache[key] = conditionDelegate;
            }
            else
                conditionDelegate = (Func<TEntity, object[]?, bool>?)d;

            (conditionFactory, conditionDirect) = GetConditionPredicates(query, condition);
        }
        return new InMemoryCompiledQuery<TResult, TEntity>(GetMap<TResult, TEntity>(query), conditionDelegate, conditionFactory, conditionDirect);
    }

    /// <summary>
    /// Returns a strongly typed predicate (or a factory that builds one from the current parameters)
    /// so the enumerator does not index/box an <c>object[]</c> on every row.
    /// </summary>
    private (Func<object[]?, Func<TEntity, bool>>? Factory, Func<TEntity, bool>? Direct) GetConditionPredicates<TResult, TEntity>(QueryCommand<TResult> query, Expression<Func<TEntity, bool>> condition)
    {
        var key = new ExpressionKey(condition, query);
        if (_conditionFactoryCache.TryGetValue(key, out var f))
            return ((Func<object[]?, Func<TEntity, bool>>)f, null);
        if (_conditionDirectCache.TryGetValue(key, out var d))
            return (null, (Func<TEntity, bool>)d);

        var collector = new ParamCollectorVisitor();
        collector.Visit(condition.Body);
        var ps = collector.Parameters;

        if (ps.Count == 0)
        {
            var direct = condition.Compile();
            _conditionDirectCache[key] = direct;
            return (null, direct);
        }

        var factory = BuildConditionFactory(condition, ps);
        _conditionFactoryCache[key] = factory;
        return (factory, null);
    }

    /// <summary>
    /// Compiles <c>(object[] p) =&gt; { var p0 = (T0)p[0]; ...; return (TEntity e) =&gt; &lt;body&gt;; }</c>.
    /// Parameters are unpacked once per query; the returned predicate is fully typed.
    /// </summary>
    private static Func<object[]?, Func<TEntity, bool>> BuildConditionFactory<TEntity>(Expression<Func<TEntity, bool>> condition, SortedDictionary<int, Type> ps)
    {
        var p = Expression.Parameter(typeof(object[]), "p");
        var locals = new Dictionary<int, ParameterExpression>();
        var variables = new List<ParameterExpression>();
        var body = new List<Expression>();

        foreach (var (idx, type) in ps)
        {
            var v = Expression.Variable(type, "p" + idx);
            locals[idx] = v;
            variables.Add(v);
            body.Add(Expression.Assign(v, Expression.Convert(Expression.ArrayIndex(p, Expression.Constant(idx)), type)));
        }

        var entity = condition.Parameters[0];
        var newBody = new ParamLocalSubstitutionVisitor(locals).Visit(condition.Body)!;
        body.Add(Expression.Lambda<Func<TEntity, bool>>(newBody, entity));

        return Expression.Lambda<Func<object[]?, Func<TEntity, bool>>>(Expression.Block(variables, body), p).Compile();
    }

    // public Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    // {
    //     return Task.FromResult((IEnumerator<TResult>)CreateAsyncEnumerator(queryCommand, @params, cancellationToken));
    // }

    public Func<Func<TEntity, TResult>> GetMap<TResult, TEntity>(QueryCommand<TResult> queryCommand)
    {
#if DEBUG
        if (!queryCommand.IsPrepared)
            throw new InvalidOperationException("Command not prepared");
#endif
        // var key = new ExpressionKey(_exp);
        // if (!(_dataProvider as SqlDataProvider).MapCache.TryGetValue(key, out var del))
        // {
        //     if (Logger?.IsEnabled(LogLevel.Information) ?? false) Logger.LogInformation("Map delegate cache miss for: {exp}", _exp);
        var resultType = typeof(TResult);

        // An entity-sourced command (for example ctx.From(scalarSubquery) or From<Entity>()) materialises
        // the source rows as-is: the in-memory data is already TResult, so no row materializer is needed.
        // Without this, scalar sources (int, ...) would fail in RowMaterializerBuilder, which expects a
        // constructor.
        if (resultType == typeof(TEntity))
            return static () => static (TEntity e) => (TResult)(object)e!;

        return () =>
        {
            Expression<Func<TEntity, TResult>> lambda;
            if (queryCommand.OneColumn)
            {
                var corVisitor = new CorrelatedQueryExpressionVisitor(this, queryCommand, typeof(TEntity), Logger);
                var newExp = corVisitor.Visit(queryCommand.SelectList![0].Expression);
                lambda = (Expression<Func<TEntity, TResult>>)newExp!;
            }
            else
            {
                var param = Expression.Parameter(typeof(TEntity));

                var body = RowMaterializerBuilder.Build(
                    resultType,
                    param,
                    queryCommand.SelectList!,
                    queryCommand.IgnoreColumns,
                    column => MapColumn(column, param));

                lambda = Expression.Lambda<Func<TEntity, TResult>>(body, param);
            }

            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);
            var key = new ExpressionKey(lambda, queryCommand);
            if (!_expCache.TryGetValue(key, out var d))
            {
                d = lambda.Compile();
                _expCache[key] = d;
            }

            return (Func<TEntity, TResult>)d;
        };

        //         (_dataProvider as SqlDataProvider).MapCache[key] = del;
        //     }
    }
    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        return (IEnumerator<TResult>)CreateAsyncEnumerator<TResult>(preparedQueryCommand, @params, CancellationToken.None);
    }

    /// <summary>
    /// In-memory override: returns the enumerator directly (it is already <see cref="IEnumerable{T}"/>),
    /// avoiding the compiler-generated <c>yield</c> state machine used by the default interface method.
    /// </summary>
    public IEnumerable<TResult> GetEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[]? @params)
    {
        var enumerator = CreateEnumerator(preparedCommand, @params);
        if (enumerator is IEnumerable<TResult> enumerable)
            return enumerable;

        return new EnumeratorEnumerable<TResult>(enumerator);
    }

    private sealed class EnumeratorEnumerable<TResult>(IEnumerator<TResult> enumerator) : IEnumerable<TResult>
    {
        public IEnumerator<TResult> GetEnumerator() => enumerator;
        IEnumerator IEnumerable.GetEnumerator() => enumerator;
    }
    protected IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = GetCacheEntry(queryCommand, cancellationToken);
        return cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, cancellationToken)!;
    }
    /// <summary>
    /// Single entry guard for the public execution overloads: the in-memory context only executes
    /// its own command representation (mixing storage backends is not supported). Foreign
    /// implementations are rejected here, once, instead of in every overload.
    /// </summary>
    private static InMemoryPreparedQueryCommand<TResult> AsInMemoryCommand<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand)
        => preparedQueryCommand as InMemoryPreparedQueryCommand<TResult>
           ?? throw new ArgumentException($"Expected {nameof(InMemoryPreparedQueryCommand<TResult>)}, got {preparedQueryCommand.GetType().Name}", nameof(preparedQueryCommand));

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        return cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, cancellationToken)!;
    }

    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;
        var l = new List<TResult>(cacheEntry.LastRowCount);

        var offset = cacheEntry.QueryCommand.Paging.Offset;
        var limit = cacheEntry.QueryCommand.Paging.Limit;
        var (rowCnt, absRowCnt) = (0, 0);

        while (await ee.MoveNextAsync())
        {
            if (offset > 0 && absRowCnt++ < offset)
                continue;

            l.Add(ee.Current);

            if (limit > 0 && ++rowCnt >= limit)
                break;
        }

        cacheEntry.LastRowCount = l.Count;

        return l;
    }
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;
        var l = new List<TResult>(cacheEntry.LastRowCount);

        var offset = cacheEntry.QueryCommand.Paging.Offset;
        var limit = cacheEntry.QueryCommand.Paging.Limit;
        var (rowCnt, absRowCnt) = (0, 0);

        while (ee.MoveNext())
        {
            if (offset > 0 && absRowCnt++ < offset)
                continue;

            l.Add(ee.Current);

            if (limit > 0 && ++rowCnt >= limit)
                break;
        }

        cacheEntry.LastRowCount = l.Count;

        return l;
    }

    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        if (await ee.MoveNextAsync())
            return ee.Current;

        if (throwIfNull) throw new InvalidOperationException();

        return default;
    }
    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        if (ee.MoveNext())
            return ee.Current;

        if (throwIfNull) throw new InvalidOperationException();

        return default;
    }

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        throw new InvalidOperationException();
    }
    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        return default;
    }
    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        throw new InvalidOperationException();
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        return default;
    }

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        if (!hasResult)
            throw new InvalidOperationException();

        return r!;
    }
    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        return r;
    }
    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        if (!hasResult)
            throw new InvalidOperationException();

        return r!;
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        return r;
    }

    public void PurgeQueryCache()
    {
        _cmdIdx.Clear();
    }

    /// <summary>
    /// The in-memory core still consumes an <c>object[]</c> (the enumerator stores it), so the
    /// span-based sync entry points materialize here. This is allocation-neutral vs. the previous
    /// <c>params object[]</c> public API.
    /// </summary>
    private static object[]? ToParams(ReadOnlySpan<object?> @params)
    {
        if (@params.IsEmpty) return null;

        var arr = new object[@params.Length];
        for (var i = 0; i < @params.Length; i++) arr[i] = @params[i]!;
        return arr;
    }
}
