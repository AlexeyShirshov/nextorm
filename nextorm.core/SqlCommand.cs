using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

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
            ResetPreparation();
            _sqlClient = value;
        }
    }
    public bool IsPrepared => _isPrepared;
    public void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;
        _dbCommand = null;
    }
    protected void PrepareDbCommand(CancellationToken cancellationToken)
    {
        //var resultType = _exp.ReturnType;

        // _hasCtor = resultType.IsValueType || resultType.GetConstructor(Type.EmptyTypes) is not null;

        // if (!_hasCtor)
        // {
        //     //resultType.GetConstructors().OrderByDescending(it=>it.GetParameters().Length).FirstOrDefault()
        // }

        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1.IsPrepared)
            _from.Table.AsT1.PrepareDbCommand(cancellationToken);

        var selectList = _selectList;

        if (selectList is null)
        {
            if (_exp is null)
                throw new BuildSqlCommandException("Lambda expression for anonymous type must exists");

            selectList = new List<SelectExpression>();

            if (_exp.Body is NewExpression ctor)
            {
                for (var idx = 0; idx < ctor.Arguments.Count; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var arg = ctor.Arguments[idx];
                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    selectList.Add(new SelectExpression(ctorParam.ParameterType) { Index = idx, PropertyName = ctorParam.Name!, Expression = arg });
                }

                if (selectList.Count == 0)
                    throw new BuildSqlCommandException("Select must return new anonymous type with at least one property");

            }
            else
            {
                throw new BuildSqlCommandException("Select must return new anonymous type");
            }
        }

        var srcType = _srcType;
        if (srcType is null)
        {
            if (_exp is null)
                throw new BuildSqlCommandException("Lambda expression for anonymous type must exists");
            
            srcType = _exp.Parameters[0].Type;
        }

        FromExpression? from = _from;

        if (from is null)
        {
            if (srcType != typeof(TableAlias))
            {
                var sqlTable = srcType.GetCustomAttribute<SqlTableAttribute>();

                var tableName = sqlTable is not null
                    ? sqlTable.Name
                    : _sqlClient.GetTableName(srcType);

                from = new FromExpression(tableName);
            }
            else throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
        }

        _isPrepared = true;
        _selectList = selectList;
        _srcType = srcType;
        _from = from;

    }
    protected DbCommand GetDbCommand(CancellationToken cancellationToken)
    {
        if (!_isPrepared) PrepareDbCommand(cancellationToken);
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
        return new ResultSetEnumerator<TResult>(this, SqlClient, GetDbCommand(cancellationToken), cancellationToken);
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

            var newParams = SelectList!.Select(column =>
            {
                if (column.Nullable)
                {
                    var recordType = typeof(IDataRecord);

                    return Expression.Condition(
                        Expression.Call(param, recordType.GetMethod(nameof(IDataRecord.IsDBNull))!, Expression.Constant(column.Index)),
                        Expression.Constant(null, column.PropertyType),
                        Expression.Convert(
                            Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index)),
                            column.PropertyType
                        )
                    );
                }

                return (Expression)Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index));
            }).ToArray();

            var exp = Expression.New(ctorInfo, newParams);

            _factory = Expression.Lambda(exp, param).Compile();
        }

        //return (TResult)_factory.DynamicInvoke(SelectList!.Select(column => Convert.ChangeType(dataRecord.GetValue(column.Index), column.PropertyType!)).ToArray())!;

        return (TResult)_factory.DynamicInvoke(dataRecord)!;
    }
}
