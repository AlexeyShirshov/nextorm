#define PARAM_CONDITION
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

public partial class InMemoryContext : IDataContext
{
    private readonly static MethodInfo miCreateAsyncEnumerator = typeof(InMemoryContext).GetMethod(nameof(CreateAsyncEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateEnumeratorAdapter = typeof(InMemoryContext).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateEnumerator = typeof(InMemoryContext).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miLoopJoin = typeof(InMemoryContext).GetMethod(nameof(LoopJoin), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateCompiledQuery = typeof(InMemoryContext).GetMethod(nameof(CreateCompiledQuery), BindingFlags.NonPublic | BindingFlags.Instance)!;
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
    private IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, object[] @params, CancellationToken cancellationToken)
    {
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

        if (queryCommand.Sorting is not null)
        {
            throw new NotImplementedException();
            // IOrderedEnumerable<TEntity>? intData = null;
            // foreach (var sorting in queryCommand.Sorting)
            // {
            //     var del = ((Expression<Func<TEntity, object>>)sorting.Expression).Compile();
            //     if (sorting.Direction == OrderDirection.Asc)
            //         intData = asyncData.OrderBy(del);
            //     else
            //         intData = (intData ?? data).OrderByDescending(del);
            // }
            // data = intData;
        }
        cacheEntry.Data = asyncData;
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

            if (queryCommand.Sorting is not null)
            {
                IOrderedEnumerable<TEntity>? intData = null;
                foreach (var sorting in queryCommand.Sorting)
                {
                    var del = ((Expression<Func<TEntity, object>>)sorting.PreparedExpression!).Compile();
                    if (sorting.Direction == OrderDirection.Asc)
                        intData = (intData ?? data).OrderBy(del);
                    else
                        intData = (intData ?? data).OrderByDescending(del);
                }
                data = intData;
            }
            cacheEntry.Data = data;

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
    private InMemoryEnumeratorAdapter<TResult, TEntity> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, IAsyncEnumerator<TEntity> enumerator)
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
