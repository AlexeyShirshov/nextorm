using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public partial class InMemoryDataProvider : IDataProvider
{
    private bool disposedValue;
    private readonly IDictionary<QueryCommand, InMemoryCacheEntry> _cmdIdx = new Dictionary<QueryCommand, InMemoryCacheEntry>();

    public QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<TResult>(this, exp, condition);
    }
    public ILogger? Logger { get; set; }

    public bool NeedMapping => false;

    public IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var ce = queryCommand.CacheEntry;
        if (ce is InMemoryCacheEntry cache || _cmdIdx.TryGetValue(queryCommand, out cache!) && cache is not null)
        {
            return (IAsyncEnumerator<TResult>)cache.CreateEnumerator.DynamicInvoke(this, queryCommand, cache, cancellationToken)!;
        }

        var del = CreateEnumeratorDelegate(queryCommand, cancellationToken);

        cache = new InMemoryCacheEntry(null, del);
        _cmdIdx[queryCommand] = cache;

        return (IAsyncEnumerator<TResult>)del!.DynamicInvoke(this, queryCommand, cache, cancellationToken)!;
    }
    protected Delegate CreateEnumeratorDelegate<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
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
            var miCreateEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.Public | BindingFlags.Instance)!;
            var param = Expression.Parameter(typeof(InMemoryDataProvider));
            var callCreateEnumerator = Expression.Call(param, miCreateEnumerator.MakeGenericMethod(resultType), Expression.Constant(subQuery), Expression.Constant(cancellationToken));

            var miCreateEnumeratorAdapter = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var callCreateEnumeratorAdapter = Expression.Call(
                param, miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType), Expression.Constant(queryCommand), callCreateEnumerator);

            var delPayload = new CreateMainEnumeratorPayload(Expression.Lambda(callCreateEnumeratorAdapter, param).Compile());
            // });

            return delPayload!.Delegate;
        }
        else
        {
            // var delPayload = queryCommand.GetOrAddPayload(() =>
            // {
            var miCreateEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var param = Expression.Parameter(typeof(InMemoryDataProvider));
            var p1 = Expression.Parameter(typeof(QueryCommand<TResult>));
            var p2 = Expression.Parameter(typeof(InMemoryCacheEntry));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var callExp = Expression.Call(param, miCreateEnumerator.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!),
                p1, p2, p3
            );
            var delPayload = new CreateEnumeratorPayload(Expression.Lambda(callExp, param, p1, p2, p3).Compile());
            //});

            return delPayload!.Delegate;
        }
    }
    private IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryCacheEntry cacheEntry, CancellationToken cancellationToken)
    {
        if (cacheEntry.Data is not IAsyncEnumerable<TEntity> asyncData)
        {
            if (queryCommand.TryGetPayload<InMemoryAsyncDataPayload<TEntity>>(out var asyncDataPayload) && asyncDataPayload!.Data is not null)
                asyncData = asyncDataPayload.Data;
            else
                asyncData = null;
        }

        if (asyncData is not null)
        {
            cacheEntry.Data = asyncData;
            return CreateEnumeratorAdapter(queryCommand, cacheEntry, asyncData.GetAsyncEnumerator(cancellationToken));
        }
        else
        {
            IEnumerable<TEntity>? data = cacheEntry.Data as IEnumerable<TEntity>;

            if (queryCommand.Joins.Any() && typeof(TEntity).IsAssignableFrom(typeof(IProjection)))
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
                                var miCreateEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(LoopJoin), BindingFlags.NonPublic | BindingFlags.Static)!;
                                var param = Expression.Parameter(typeof(InMemoryDataProvider));
                                var p1 = Expression.Parameter(typeof(QueryCommand));
                                var p2 = Expression.Parameter(typeof(object));
                                var p3 = Expression.Parameter(typeof(JoinExpression));
                                var p4 = Expression.Parameter(typeof(int));
                                var callExp = Expression.Call(null, miCreateEnumerator.MakeGenericMethod(firstType, secondType, prjType),
                                    p1,
                                    Expression.Convert(p2, typeof(IEnumerable<>).MakeGenericType(firstType)),
                                    p3,
                                    p4
                                );
                                var d = Expression.Lambda(callExp,
                                    p1,
                                    p2,
                                    p3,
                                    p4
                                ).Compile();

                                joinResult = d.DynamicInvoke(queryCommand, joinResult, join, dim);

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
                var dataPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TEntity>(Array.Empty<TEntity>().AsEnumerable()));

                data = dataPayload.Data!;

                cacheEntry.Data = data;
            }

            if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
            {
                compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(queryCommand);
                cacheEntry.CompiledQuery = compiledQuery;
            }

            return new InMemoryEnumerator<TResult, TEntity>(compiledQuery, data.GetEnumerator());
        }
    }
    static IEnumerable<TResult> LoopJoin<TLeft, TRight, TResult>(QueryCommand queryCommand, IEnumerable<TLeft> leftData, JoinExpression join, int dim)
    {
        if (leftData is null)
        {
            var dataPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TLeft>(Array.Empty<TLeft>().AsEnumerable()));
            leftData = dataPayload.Data!;
        }

        var joinPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TRight>(Array.Empty<TRight>().AsEnumerable()));

        var res = new List<TResult>();

        foreach (var item in leftData)
        {
            foreach (var itemInner in joinPayload.Data!)
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
    private InMemoryEnumeratorAdapter<TResult, TEntity> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, InMemoryCacheEntry cacheEntry, IAsyncEnumerator<TEntity> enumerator)
    {
        if (queryCommand.Joins.Any() && typeof(TEntity).IsAssignableFrom(typeof(IProjection)))
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

    public Expression MapColumn(SelectExpression column, Expression param, Type recordType)
    {
        if (column.Expression.IsT0)
            throw new NotSupportedException();

        var replace = new ReplaceExpressionVisitor(param);
        return replace.Visit(column.Expression.AsT1);
        //return Expression.PropertyOrField(param, column.PropertyName!);
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        queryCommand.RemovePayload<CreateEnumeratorPayload>();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    record CreateEnumeratorPayload(Delegate Delegate) : IPayload;
    record CreateMainEnumeratorPayload(Delegate Delegate) : IPayload;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
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
    public void Compile<TResult>(QueryCommand<TResult> query, CancellationToken cancellationToken)
    {
        if (!query.IsPrepared)
            query.PrepareCommand(cancellationToken);

        //query.Compiled = CreateCompiledQuery(query);
        var miCreateCompiledQuery = typeof(InMemoryDataProvider).GetMethod(nameof(CreateCompiledQuery), BindingFlags.NonPublic | BindingFlags.Static)!;
        var param = Expression.Parameter(typeof(QueryCommand<TResult>));
        var callExp = Expression.Call(null, miCreateCompiledQuery.MakeGenericMethod(typeof(TResult), query.EntityType),
            param
        );

        var lambda = Expression.Lambda<Func<QueryCommand<TResult>, CompiledQuery<TResult>>>(callExp, param).Compile();

        query.CacheEntry = new InMemoryCacheEntry(lambda(query), CreateEnumeratorDelegate(query, cancellationToken));
    }

    private static CompiledQuery<TResult> CreateCompiledQuery<TResult, TEntity>(QueryCommand<TResult> query)
    {
        return new InMemoryCompiledQuery<TResult, TEntity>(query.GetMap(typeof(TResult), query.EntityType), query.Condition as Expression<Func<TEntity, bool>>);
    }
    class InMemoryCacheEntry : CacheEntry
    {
        public InMemoryCacheEntry(object? compiledQuery, Delegate createEnumerator)
            : base(compiledQuery!)
        {
            CreateEnumerator = createEnumerator;
        }

        public Delegate CreateEnumerator { get; }
        public object? Data { get; set; }
    }
}