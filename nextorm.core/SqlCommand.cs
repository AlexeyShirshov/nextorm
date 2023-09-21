using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class SqlCommand
{
    private DbCommand? _dbCommand;
    private List<SelectExpression>? _selectList;
    private FromExpression? _from;
    private SqlClient _sqlClient;
    private readonly LambdaExpression _exp;
    private bool _isPrepared;
    //private bool _hasCtor;
    private Type? _srcType;
    internal ILogger? Logger { get; set; }
    public SqlCommand(SqlClient sqlClient, LambdaExpression exp)
    {
        _sqlClient = sqlClient;
        _exp = exp;
    }
    public FromExpression? From { get => _from; set => _from = value; }
    public List<SelectExpression>? SelectList => _selectList;
    public Type? EntityType => _srcType;
    public SqlClient SqlClient
    {
        get => _sqlClient;
        set
        {
            _dbCommand = null;
            _isPrepared = false;
            _sqlClient = value;
        }
    }
    public bool IsPrepared => _isPrepared;

    protected void PrepareDbCommand()
    {
        if (_exp is null)
            throw new BuildSqlCommandException("Lambda expression for anonymous type must exists");

        //var resultType = _exp.ReturnType;

        // _hasCtor = resultType.IsValueType || resultType.GetConstructor(Type.EmptyTypes) is not null;

        // if (!_hasCtor)
        // {
        //     //resultType.GetConstructors().OrderByDescending(it=>it.GetParameters().Length).FirstOrDefault()
        // }

        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1.IsPrepared)
            _from.Table.AsT1.PrepareDbCommand();

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

                _from = new FromExpression(tableName);
            }
            else throw new BuildSqlCommandException("");
        }

        _isPrepared = true;
    }
    protected DbCommand GetDbCommand()
    {
        if (!_isPrepared) PrepareDbCommand();
        return _dbCommand ??= _sqlClient.CreateCommand(this);
    }
}
public class SqlCommand<TResult> : SqlCommand, IAsyncEnumerable<TResult>
{
    private Delegate? _factory;

    public SqlCommand(SqlClient sqlClient, LambdaExpression exp) : base(sqlClient, exp)
    {
    }

    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new ResultSetEnumerator<TResult>(this, SqlClient, GetDbCommand(), cancellationToken);
    }
    public TResult Map(IDataRecord dataRecord)
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

        if (_factory is null)
        {
            var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new BuildSqlCommandException($"Cannot get ctor from {resultType}");

            var param = Expression.Parameter(typeof(IDataRecord));
            //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

            var newParams = SelectList!.Select(column => Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index))).ToArray();
            var exp = Expression.New(ctorInfo, newParams);

            _factory = Expression.Lambda(exp, param).Compile();
        }

        //return (TResult)_factory.DynamicInvoke(SelectList!.Select(column => Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType!)).ToArray())!;

        return (TResult)_factory.DynamicInvoke(dataRecord)!;
    }
}
