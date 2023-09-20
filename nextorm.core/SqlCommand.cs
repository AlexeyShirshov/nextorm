using System.Data;
using System.Data.Common;
using System.Reflection;

namespace nextorm.core;
public class SqlCommand<TResult> : IAsyncEnumerable<TResult>
{
    private DbCommand _dbCommand;
    private SqlClient _sqlClient;
    private List<SelectExpression> _selectList;
    public SqlCommand(SqlClient sqlClient)
    {
        _sqlClient = sqlClient;
    }

    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        _dbCommand ??= PrepareDbCommand();
        return new ResultSetEnumerator<TResult>(this, _sqlClient, _dbCommand, cancellationToken);
    }

    public TResult Map(IDataRecord dataRecord)
    {
        var resultType = typeof(TResult);

        // foreach (var column in _selectList)
        // {
        //     resultType.GetMember(column.PropertyName).OfType<PropertyInfo>().Single().SetValue(result, dataRecord.GetValue(column.Index));
        // }
        
        return (TResult)Activator.CreateInstance(resultType, _selectList.Select(column=>
            Convert.ChangeType(dataRecord.GetValue(column.Index),resultType.GetMember(column.PropertyName).OfType<PropertyInfo>().Single().PropertyType)).ToArray());
    }

    private DbCommand PrepareDbCommand()
    {
        var sql = "select id from simple_entity";
        _selectList = new List<SelectExpression>();
        _selectList.Add(new SelectExpression {Index=0, PropertyName="Id"});
        return _sqlClient.CreateCommand(sql);
    }
}
