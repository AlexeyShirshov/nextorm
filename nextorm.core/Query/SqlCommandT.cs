using System.Data;
using System.Linq.Expressions;

namespace nextorm.core;

public class SqlCommand<TResult> : SqlCommand, IQueryCommand<TResult>
{
    private Delegate? _factory;
    public Delegate? Factory
    {
        get=>_factory;
        set=>_factory = value;
    }
    public SqlCommand(SqlClient sqlClient, LambdaExpression exp, Expression? condition) : base(sqlClient, exp, condition)
    {
    }
    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return SqlClient.CreateEnumerator<TResult>(this, cancellationToken);
    }
    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType)
    {
        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(param, recordType.GetMethod(nameof(IDataRecord.IsDBNull))!, Expression.Constant(column.Index)),
                Expression.Constant(null, column.PropertyType),
                Expression.Convert(
                    Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index)),
                    column.PropertyType
                )
            );
        }

        return Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index));
    }
    // public TResult Map(IDataRecord dataRecord)
    // {
    //     if (!IsPrepared)
    //         throw new InvalidOperationException("Command not prepared");

    //     var resultType = typeof(TResult);

    //     // foreach (var column in _selectList)
    //     // {
    //     //     resultType.GetMember(column.PropertyName).OfType<PropertyInfo>().Single().SetValue(result, dataRecord.GetValue(column.Index));
    //     // }

    //     // return (TResult)Activator.CreateInstance(resultType, SelectList!.Select(column =>
    //     //     Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType!)).ToArray())!;

    //     if (_factory is null)
    //     {
    //         var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new BuildSqlCommandException($"Cannot get ctor from {resultType}");

    //         var param = Expression.Parameter(typeof(IDataRecord));
    //         //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

    //         var newParams = SelectList!.Select(column =>
    //         {
    //             if (column.Nullable)
    //             {
    //                 var recordType = typeof(IDataRecord);

    //                 return Expression.Condition(
    //                     Expression.Call(param, recordType.GetMethod(nameof(IDataRecord.IsDBNull))!, Expression.Constant(column.Index)),
    //                     Expression.Constant(null, column.PropertyType),
    //                     Expression.Convert(
    //                         Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index)),
    //                         column.PropertyType
    //                     )
    //                 );
    //             }

    //             return (Expression)Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index));
    //         }).ToArray();

    //         var exp = Expression.New(ctorInfo, newParams);

    //         _factory = Expression.Lambda(exp, param).Compile();
    //     }

    //     //return (TResult)_factory.DynamicInvoke(SelectList!.Select(column => Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType!)).ToArray())!;

    //     return (TResult)_factory.DynamicInvoke(dataRecord)!;
    // }

}