using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class InMemoryDataProvider : IDataProvider
{
    private bool disposedValue;
    private IDictionary<QueryCommand, object> _cmdIdx = new Dictionary<QueryCommand, object>();
    public ILogger? Logger { get; set; }

    public bool NeedMapping => false;

    public QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<TResult>(this, exp, condition);
    }

    public IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.From is not null)
        {
            if (queryCommand.From.Table.IsT0)
                throw new DataProviderException("Cannot use table as source for in-memory provider");

            var subQuery = queryCommand.From.Table.AsT1;
            var subQueryType = subQuery.GetType();
            if (!subQueryType.IsGenericType)
                throw new DataProviderException("Cannot use table as source for in-memory provider");

            var resultType = subQueryType.GenericTypeArguments[0];

            var delPayload = subQuery.GetOrAddPayload(() =>
            {
                var miCreateEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.Public | BindingFlags.Instance)!;
                var param = Expression.Parameter(typeof(InMemoryDataProvider));
                var callCreateEnumerator = Expression.Call(param, miCreateEnumerator.MakeGenericMethod(resultType), Expression.Constant(subQuery), Expression.Constant(cancellationToken));

                var miCreateEnumeratorAdapter = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Instance)!;
                var callCreateEnumeratorAdapter = Expression.Call(
                    param, miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType), Expression.Constant(queryCommand), callCreateEnumerator);

                return new CreateMainEnumeratorPayload(Expression.Lambda(callCreateEnumeratorAdapter, param).Compile());
            });

            return (IAsyncEnumerator<TResult>)delPayload!.Delegate.DynamicInvoke(this)!;
        }
        else
        {
            var delPayload = queryCommand.GetOrAddPayload(() =>
            {
                var miCreateEnumerator = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
                var param = Expression.Parameter(typeof(InMemoryDataProvider));
                var callExp = Expression.Call(param, miCreateEnumerator.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!), Expression.Constant(queryCommand), Expression.Constant(cancellationToken));
                return new CreateEnumeratorPayload(Expression.Lambda(callExp, param).Compile());
            });

            return (IAsyncEnumerator<TResult>)delPayload!.Delegate.DynamicInvoke(this)!;
        }
    }
    protected IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        if (queryCommand.TryGetPayload<InMemoryAsyncDataPayload<TEntity>>(out var asyncDataPayload) && asyncDataPayload!.Data is not null)
        {
            return CreateEnumeratorAdapter(queryCommand, asyncDataPayload.Data.GetAsyncEnumerator(cancellationToken));
        }
        else
        {
            if (queryCommand.Joins.Any() && typeof(TEntity).FullName!.StartsWith("nextorm.core.Projection"))
            {
                throw new NotImplementedException("joins");
            }

            var dataPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TEntity>(Array.Empty<TEntity>().AsEnumerable()));

            var data = dataPayload.Data!;

            if (queryCommand.Condition is not null)
            {
                var predicate = (queryCommand.Condition as Expression<Func<TEntity, bool>>)!.Compile();
                data = data.Where(predicate);
            }

            var compiledQuery = queryCommand.Compiled;
            if (compiledQuery is null)
            {
                if (!queryCommand.Cache || !_cmdIdx.TryGetValue(queryCommand, out var query))
                {
                    query = CreateCompiledQuery(queryCommand);

                    if (queryCommand.Cache)
                        _cmdIdx[queryCommand] = query;
                }

                compiledQuery = (CompiledQuery<TResult>)query;
            }
            return new InMemoryEnumerator<TResult, TEntity>(compiledQuery, data.GetEnumerator());
        }
    }
    protected InMemoryEnumeratorAdapter<TResult, TEntity> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, IAsyncEnumerator<TEntity> enumerator)
    {
        var compiledQuery = queryCommand.Compiled;
        if (compiledQuery is null)
        {
            if (!queryCommand.Cache || !_cmdIdx.TryGetValue(queryCommand, out var query))
            {
                query = CreateCompiledQuery(queryCommand);

                if (queryCommand.Cache)
                    _cmdIdx[queryCommand] = query;

            }
            
            compiledQuery = (CompiledQuery<TResult>)query;
        }

        return new InMemoryEnumeratorAdapter<TResult, TEntity>(compiledQuery, enumerator, queryCommand.Condition as Expression<Func<TEntity, bool>>);
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
    public void Compile<TResult>(QueryCommand<TResult> query)
    {
        if (!query.IsPrepared)
            query.PrepareCommand(CancellationToken.None);

        query.Compiled = CreateCompiledQuery(query);
    }

    private static CompiledQuery<TResult> CreateCompiledQuery<TResult>(QueryCommand<TResult> query)
    {
        return new CompiledQuery<TResult>(query.GetMap(typeof(TResult), query.EntityType));
    }
}