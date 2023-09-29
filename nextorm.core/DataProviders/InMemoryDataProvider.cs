using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class InMemoryDataProvider : IDataProvider
{
    public ILogger? Logger { get; set; }

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

                var miCreateEnumeratorAdapter = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumeratorAdapter), BindingFlags.NonPublic | BindingFlags.Static)!;
                var callCreateEnumeratorAdapter = Expression.Call(null, miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType), Expression.Constant(queryCommand), callCreateEnumerator);

                return new CreateMainEnumeratorPayload(Expression.Lambda(callCreateEnumeratorAdapter, param).Compile());
            });

            return (IAsyncEnumerator<TResult>)delPayload!.Delegate.DynamicInvoke(this)!;
        }
        else
        {
            var delPayload = queryCommand.GetOrAddPayload(() =>
            {
                var mi = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
                var param = Expression.Parameter(typeof(InMemoryDataProvider));
                var callExp = Expression.Call(param, mi.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!), Expression.Constant(queryCommand));
                return new CreateEnumeratorPayload(Expression.Lambda(callExp, param).Compile());
            });

            return (IAsyncEnumerator<TResult>)delPayload!.Delegate.DynamicInvoke(this)!;
        }
    }
    protected static IEnumerator<TEntity> GetData<TEntity>(QueryCommand queryCommand)
    {
        var dataPayload = queryCommand.GetOrAddPayload(() =>
        {
            return new InMemoryDataPayload<TEntity>(Array.Empty<TEntity>().AsEnumerable());
        });

        var data = dataPayload!.Data!;

        if (queryCommand.Condition is not null)
        {
            var predicate = (queryCommand.Condition as Expression<Func<TEntity, bool>>)!.Compile();
            data = data.Where(predicate);
        }
        return data.GetEnumerator();
    }
    protected InMemoryEnumerator<TResult, TEntity> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand)
    {
        return new InMemoryEnumerator<TResult, TEntity>(queryCommand, GetData<TEntity>(queryCommand));
    }
    protected static InMemoryEnumeratorAdapter<TResult, TEntity> CreateEnumeratorAdapter<TResult, TEntity>(QueryCommand<TResult> queryCommand, IAsyncEnumerator<TEntity> enumerator)
    {
        return new InMemoryEnumeratorAdapter<TResult, TEntity>(queryCommand, enumerator);
    }
    public FromExpression? GetFrom(Type srcType)
    {
        return null;
    }

    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType)
    {
        return Expression.PropertyOrField(param, column.PropertyName!);
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        queryCommand.RemovePayload<CreateEnumeratorPayload>();
    }
    record CreateEnumeratorPayload(Delegate Delegate) : IPayload;
    record CreateMainEnumeratorPayload(Delegate Delegate) : IPayload;
}