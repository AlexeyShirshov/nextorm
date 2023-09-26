using System.Linq.Expressions;

namespace nextorm.core;

public interface IQueryCommand<TResult> : IAsyncEnumerable<TResult>, IQueryCommand
{
    protected Delegate? Factory {get;set;}
    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType);
    public TResult Map(object dataRecord)
    {
        if (!IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        var resultType = typeof(TResult);

        // foreach (var column in _selectList)
        // {
        //     resultType.GetMember(column.PropertyName).OfType<PropertyInfo>().Single().SetValue(result, dataRecord.GetValue(column.Index));
        // }

        // return (TResult)Activator.CreateInstance(resultType, SelectList!.Select(column =>
        //     Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType!)).ToArray())!;

        if (Factory is null)
        {
            var recordType = dataRecord.GetType();

            var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new BuildSqlCommandException($"Cannot get ctor from {resultType}");

            var param = Expression.Parameter(recordType);
            //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

            var newParams = SelectList!.Select(column => MapColumn(column, param, recordType)).ToArray();

            var exp = Expression.New(ctorInfo, newParams);

            Factory = Expression.Lambda(exp, param).Compile();
        }

        //return (TResult)_factory.DynamicInvoke(SelectList!.Select(column => Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType!)).ToArray())!;

        return (TResult)Factory.DynamicInvoke(dataRecord)!;
    }
}