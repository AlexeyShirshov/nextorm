#define PARAM_CONDITION
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public partial class InMemoryDataProvider : IDataProvider
{
    private readonly static MethodInfo miCreateAsyncEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(CreateAsyncEnumerator), BindingFlags.Public | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateEnumeratorAdapter = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miLoopJoin = typeof(InMemoryDataProvider).GetMethod(nameof(LoopJoin), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly static MethodInfo miCreateCompiledQuery = typeof(InMemoryDataProvider).GetMethod(nameof(CreateCompiledQuery), BindingFlags.NonPublic | BindingFlags.Instance)!;
    //delegate CreateEnumeratorDelegateFunc<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[], CancellationToken,TResult>
    private readonly IDictionary<ExpressionKey, Delegate> _expCache = new ExpressionCache<Delegate>();
    private readonly IDictionary<Type, object?> _data = new Dictionary<Type, object?>();
    private bool _disposedValue;
    private readonly IDictionary<QueryCommand, CacheEntry> _cmdIdx = new Dictionary<QueryCommand, CacheEntry>();
    public InMemoryDataProvider()
    {
        _data[typeof(TableAlias)] = new TableAlias?[] { null };
    }
    public QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<TResult>(this, exp, condition);
    }
    public ILogger? Logger { get; set; }
    public bool NeedMapping => false;
    public IDictionary<Type, object?> Data => _data;
    public IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;
    public IDictionary<Type, IEntityMeta> Metadata => throw new NotImplementedException();
    public ILogger? CommandLogger { get; set; }

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);
        var cacheEntry = GetCacheEntry(queryCommand, cancellationToken);

        return cacheEntry.CreateEnumerator(queryCommand, cacheEntry, @params, cancellationToken)!;
    }

    private InMemoryCacheEntry<TResult> GetCacheEntry<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        var ce = queryCommand.CacheEntry;
        if (ce is InMemoryCacheEntry<TResult> cc)
            return cc;

        if (queryCommand.Cache && _cmdIdx.TryGetValue(queryCommand, out ce) && ce is InMemoryCacheEntry<TResult> cache2)
            return cache2;

        var del = CreateEnumeratorDelegate(queryCommand, cancellationToken);
        var cache = new InMemoryCacheEntry<TResult>(null, del);

        if (queryCommand.Cache)
            _cmdIdx[queryCommand] = cache;

        return cache;
    }

    protected Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>> CreateEnumeratorDelegate<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        if (queryCommand.From is not null)
        {
            if (queryCommand.From.Table.IsT0)
                throw new DataProviderException("Cannot use table as source for in-memory provider");

            var subQuery = queryCommand.From.Table.AsT1;
            var subQueryType = subQuery.GetType();
            if (!subQueryType.IsGenericType)
                throw new DataProviderException("Cannot use table as source for in-memory provider");

            var resultType = subQueryType.GenericTypeArguments[0];

            //var delPayload = subQuery.GetOrAddPayload(() =>
            //{

            var @this = Expression.Constant(this);
            var p1 = Expression.Parameter(typeof(QueryCommand<TResult>));
            var p2 = Expression.Parameter(typeof(InMemoryCacheEntry<TResult>));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var p4 = Expression.Parameter(typeof(object[]));
            var callCreateEnumerator = Expression.Call(@this, miCreateAsyncEnumerator.MakeGenericMethod(resultType),
                Expression.Convert(Expression.Property(p1, nameof(QueryCommand.FromQuery)), subQueryType), p4, p3
            );


            var callCreateEnumeratorAdapter = Expression.Call(@this, miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType),
                p1, p2, callCreateEnumerator
            );

            var del = Expression.Lambda<Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>>>(callCreateEnumeratorAdapter, p1, p2, p4, p3).Compile();
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
            var p2 = Expression.Parameter(typeof(InMemoryCacheEntry<TResult>));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var p4 = Expression.Parameter(typeof(object[]));
            var callExp = Expression.Call(@this, miCreateEnumerator.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!),
                p1, p2, p4, p3
            );

            var key = new ExpressionKey(callExp, _expCache, queryCommand);
            Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>> createEnumeratorDelegate;
            if (!_expCache.TryGetValue(key, out var d))
            {
                createEnumeratorDelegate = Expression.Lambda<Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>>>(callExp, p1, p2, p4, p3).Compile();
                _expCache[key] = createEnumeratorDelegate;
            }
            else
                createEnumeratorDelegate = (Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>>)d;

            return createEnumeratorDelegate;
        }
    }
    private IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryCacheEntry<TResult> cacheEntry, object[] @params, CancellationToken cancellationToken)
    {
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
        return CreateEnumeratorAdapter(queryCommand, cacheEntry, asyncData.GetAsyncEnumerator(cancellationToken));

    next:
        {

            if (queryCommand.Joins.Any() && typeof(TEntity).IsAssignableTo(typeof(IProjection)))
            {
                var dim = 2;
                object? joinResult = null;
                Type? firstType = null;
                foreach (var join in queryCommand.Joins)
                {
                    firstType ??= join.JoinCondition.Parameters[0].Type;
                    var secondType = join.JoinCondition.Parameters[1].Type;

                    switch (join.JoinType)
                    {
                        case JoinType.Inner:
                            {
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
                                var key = new ExpressionKey(callExp, _expCache, queryCommand);
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
                                break;
                            }
                        default:
                            throw new NotSupportedException(join.JoinType.ToString());
                    }

                    dim++;
                }

                data = (IEnumerable<TEntity>)joinResult!;
            }
            else if (data is null)
            {
                if (v is not IEnumerable<TEntity> ev)
                    ev = Array.Empty<TEntity>();

                data = ev;

                cacheEntry.Data = data;
            }

            if (cacheEntry.Enumerator is not InMemoryEnumerator<TResult, TEntity> enumerator)
            {
                if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
                {
                    compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
                    cacheEntry.CompiledQuery = compiledQuery;
                }

                enumerator = new InMemoryEnumerator<TResult, TEntity>(compiledQuery, cancellationToken);
#if PARAM_CONDITION
                enumerator.Init(data, @params);
#else
                enumerator.Init(data, GetCondition<TEntity>(queryCommand.Condition, @params));
#endif
                cacheEntry.Enumerator = enumerator;
            }
            else
            {
#if PARAM_CONDITION
                enumerator.Init(data, @params);
#else
                enumerator.Init(data, GetCondition<TEntity>(queryCommand.Condition, @params));
#endif
            }

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
        if (leftData is null)
        {
            //var dataPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TLeft>(Array.Empty<TLeft>().AsEnumerable()));
            if (!_data.TryGetValue(typeof(TLeft), out var vl))
            {
                vl = Array.Empty<TLeft>();
            }
            leftData = (IEnumerable<TLeft>)vl;
        }

        //var joinPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TRight>(Array.Empty<TRight>().AsEnumerable()));
        if (!_data.TryGetValue(typeof(TRight), out var vr))
        {
            vr = Array.Empty<TRight>();
        }
        var joinPayload = (IEnumerable<TRight>)vr;

        var res = new List<TResult>();

        foreach (var item in leftData)
        {
            foreach (var itemInner in joinPayload)
            {
                var d = ((Expression<Func<TLeft, TRight, bool>>)join.JoinCondition).Compile();
                var r = d(item, itemInner)!;

                if (r)
                {
                    res.Add((TResult)CreateProjection(item, itemInner, dim));
                }
            }
        }

        return res;
    }
    static Type CreateProjectionType(Type firstType, Type secondType, int dim)
    {
        var typeName = $"nextorm.core.Projection`{dim}";
        var t = typeof(InMemoryDataProvider).Assembly.GetType(typeName) ?? throw new InvalidOperationException($"Cannot create type {typeName}");
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
        if (dim >= 3 && left is IProjection proj)
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

        throw new NotSupportedException(dim.ToString());

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
    private InMemoryEnumeratorAdapter<TResult, TEntity> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryCacheEntry<TResult> cacheEntry, IAsyncEnumerator<TEntity> enumerator)
    {
        if (queryCommand.Joins.Any() && typeof(TEntity).IsAssignableTo(typeof(IProjection)))
        {
            throw new NotImplementedException("joins");
        }

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
        {
            compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
            cacheEntry.CompiledQuery = compiledQuery;
        }

        return new InMemoryEnumeratorAdapter<TResult, TEntity>(compiledQuery, enumerator);
    }
    public FromExpression? GetFrom(Type srcType, QueryCommand queryCommand)
    {
        return null;
    }

    public Expression MapColumn(SelectExpression column, Expression param)
    {
        if (column.Expression.IsT0)
            throw new NotSupportedException();

        var replace = new ReplaceParameterVisitor(param);
        return replace.Visit(column.Expression.AsT1);
        //return Expression.PropertyOrField(param, column.PropertyName!);
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        //   queryCommand.RemovePayload<CreateEnumeratorPayload>();
    }

    public ValueTask DisposeAsync()
    {
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
    public void Compile<TResult>(QueryCommand<TResult> queryCommand, bool forToListCalls, CancellationToken cancellationToken)
    {
        if (!queryCommand.IsPrepared)
            queryCommand.PrepareCommand(cancellationToken);

        //query.Compiled = CreateCompiledQuery(query);

        var @this = Expression.Constant(this);
        var param = Expression.Parameter(typeof(QueryCommand<TResult>));
        var callExp = Expression.Call(@this, miCreateCompiledQuery.MakeGenericMethod(typeof(TResult), queryCommand.EntityType),
            param
        );

        var key = new ExpressionKey(callExp, _expCache, queryCommand);
        Func<QueryCommand<TResult>, object> createCompiledQueryDelegate;
        if (!_expCache.TryGetValue(key, out var del))
        {
            var body = Expression.Convert(callExp, typeof(object));
            createCompiledQueryDelegate = Expression.Lambda<Func<QueryCommand<TResult>, object>>(body, param).Compile();
            _expCache[key] = createCompiledQueryDelegate;
        }
        else
            createCompiledQueryDelegate = (Func<QueryCommand<TResult>, object>)del;

        var ce = new InMemoryCacheEntry<TResult>(createCompiledQueryDelegate(queryCommand), CreateEnumeratorDelegate(queryCommand, cancellationToken));
        queryCommand.CacheEntry = ce;
        ce.Enumerator = ce.CreateEnumerator(queryCommand, ce, null, cancellationToken)!;
    }

    private CompiledQuery<TResult, TEntity> CreateCompiledQuery<TResult, TEntity>(QueryCommand<TResult> query)
    {
        Func<TEntity, object[]?, bool>? conditionDelegate = null;
        if (query.Condition is Expression<Func<TEntity, bool>> condition)
        {
            var key = new ExpressionKey(condition, _expCache, query);
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
        }
        return new InMemoryCompiledQuery<TResult, TEntity>(GetMap<TResult, TEntity>(query), conditionDelegate);
    }

    public Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        return Task.FromResult((IEnumerator<TResult>)CreateAsyncEnumerator(queryCommand, @params, cancellationToken));
    }

    public Task<List<TResult>> ToListAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
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
                lambda = queryCommand.SelectList![0].Expression.Match(_ => throw new NotImplementedException(), exp =>
                {
                    var corVisitor = new CorrelatedQueryExpressionVisitor(this);
                    var newExp = corVisitor.Visit(exp);
                    return (Expression<Func<TEntity, TResult>>)newExp;
                });
            }
            else
            {
                var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

                var param = Expression.Parameter(typeof(TEntity));

                if (queryCommand.IgnoreColumns)
                {
                    var ctor = Expression.New(ctorInfo);

                    lambda = Expression.Lambda<Func<TEntity, TResult>>(ctor, param);
                }
                else
                {
                    if (ctorInfo.GetParameters().Length == queryCommand.SelectList!.Count)
                    {
                        var newParams = queryCommand.SelectList!.Select(column => MapColumn(column, param)).ToArray();

                        var ctor = Expression.New(ctorInfo, newParams);

                        lambda = Expression.Lambda<Func<TEntity, TResult>>(ctor, param);
                    }
                    else
                    {
                        var bindings = queryCommand.SelectList!.Select(column =>
                        {
                            var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
                            return Expression.Bind(propInfo, MapColumn(column, param));
                        }).ToArray();

                        var ctor = Expression.New(ctorInfo);

                        var memberInit = Expression.MemberInit(ctor, bindings);

                        var body = memberInit;
                        lambda = Expression.Lambda<Func<TEntity, TResult>>(body, param);
                    }
                }
            }

            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);
            var key = new ExpressionKey(lambda, _expCache, queryCommand);
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

    public IEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        return (IEnumerator<TResult>)CreateAsyncEnumerator(queryCommand, @params, CancellationToken.None);
    }

    public List<TResult> ToList<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        var cacheEntry = GetCacheEntry(queryCommand, CancellationToken.None);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(queryCommand, cacheEntry, @params, CancellationToken.None)!;
        var l = new List<TResult>(cacheEntry.LastRowCount);
        while (ee.MoveNext())
            l.Add(ee.Current);

        cacheEntry.LastRowCount = l.Count;

        return l;
    }

    public class InMemoryCacheEntry<TResult> : CacheEntry
    {
        public InMemoryCacheEntry(object? compiledQuery, Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>> createEnumerator)
            : base(compiledQuery!)
        {
            CreateEnumerator = createEnumerator;
        }
        public Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>> CreateEnumerator { get; }
        public object? Data { get; set; }
        public object? Enumerator { get; set; }
        public int LastRowCount { get; internal set; }
    }
}