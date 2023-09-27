using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public interface IDataProvider
{
    public ILogger? Logger { get; set; }
    IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken);
    QueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition);
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType);
    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType);
    TResult Map<TResult>(QueryCommand queryCmd, object dataRecord)
    {
        if (!queryCmd.IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        var factory = queryCmd.GetOrAddPayload(() =>
        {
            var resultType = typeof(TResult);

            var recordType = dataRecord.GetType();

            var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

            var param = Expression.Parameter(recordType);
            //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

            var newParams = queryCmd.SelectList!.Select(column => MapColumn(column, param, recordType)).ToArray();

            var exp = Expression.New(ctorInfo, newParams);

            return new MapPayload(Expression.Lambda(exp, param).Compile());
        });

        return (TResult)factory!.Delegate.DynamicInvoke(dataRecord)!;
    }
    record MapPayload(Delegate Delegate) : IPayload;
}