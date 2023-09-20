using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace nextorm.core;
public class SqlCommand<TResult> : IAsyncEnumerable<TResult>
{
    private DbCommand _dbCommand;
    private SqlClient _sqlClient;
    private readonly LambdaExpression _exp;
    private List<SelectExpression> _selectList;
    private FromExpression _from;
    private bool _hasCtor;
    private Type _srcType;

    public FromExpression From { get => _from; set => _from = value; }
    public List<SelectExpression> SelectList => _selectList;
    public Type EntityType => _srcType;

    public SqlCommand(SqlClient sqlClient, LambdaExpression exp)
    {
        _sqlClient = sqlClient;
        _exp = exp;
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

        return (TResult)Activator.CreateInstance(resultType, _selectList.Select(column =>
            Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType)).ToArray());
    }

    private DbCommand PrepareDbCommand()
    {
        var resultType = typeof(TResult);

        _hasCtor = resultType.IsValueType || resultType.GetConstructor(Type.EmptyTypes) is not null;

        if (!_hasCtor)
        {
            //resultType.GetConstructors().OrderByDescending(it=>it.GetParameters().Length).FirstOrDefault()
        }

        if (_exp.Body is NewExpression ctor)
        {
            _selectList = new List<SelectExpression>();

            for (var idx = 0; idx < ctor.Arguments.Count; idx++)
            {
                var arg = ctor.Arguments[idx];
                var ctorParam = ctor.Constructor!.GetParameters()[idx];

                _selectList.Add(new SelectExpression { Index = idx, PropertyName = ctorParam.Name!, PropertyType = ctorParam.ParameterType, Expression = arg });
            }

            if (_selectList.Count == 0)
                throw new BuildSqlCommandException("Select must return new anonymous type with at least one property");

        }
        else
        {
            throw new BuildSqlCommandException("Select must return new anonymous type");
        }

        _srcType = _exp.Parameters[0].Type;

        if (_from is null)
        {
            if (_srcType != typeof(TableAlias))
            {
                var sqlTable = _srcType.GetCustomAttribute<SqlTableAttribute>();

                var tableName = sqlTable is not null
                    ? sqlTable.Name
                    : _sqlClient.GetTableName(_srcType);

                _from = new FromExpression { TableName = tableName };
            }
        }

        return _sqlClient.CreateCommand(this);
    }
}
