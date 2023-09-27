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

        var delPayload = queryCommand.GetOrAddPayload(() =>
        {
            var mi = typeof(InMemoryDataProvider).GetMethod(nameof(CreateEnumerator), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var param = Expression.Parameter(typeof(InMemoryDataProvider));
            var callExp = Expression.Call(param, mi.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!), Expression.Constant(queryCommand));
            return new CreateEnumeratorPayload(Expression.Lambda(callExp, param).Compile());
        });

        return (IAsyncEnumerator<TResult>)delPayload!.Delegate.DynamicInvoke(this)!;
    }
    protected static IEnumerator<TEntity> GetData<TEntity>(QueryCommand queryCommand)
    {
        var dataPayload = queryCommand.GetOrAddPayload(() => new InMemoryDataPayload<TEntity>(Array.Empty<TEntity>().AsEnumerable()))!;
        var data = dataPayload.Data!;
        if (queryCommand.Condition is not null)
        {
            var predicate = (queryCommand.Condition as Expression<Func<TEntity, bool>>)!.Compile();
            data = data.Where(predicate);
        }
        return data.GetEnumerator();
    }
    protected InMemoryEnumerator<TResult, TEntity> CreateEnumerator<TResult, TEntity>(QueryCommand<TResult> queryCommand)
    {
        return new InMemoryEnumerator<TResult, TEntity>(queryCommand, GetData<TEntity>(queryCommand), this);
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
}