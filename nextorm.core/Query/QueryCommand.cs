using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public interface IQueryCommand
{
    FromExpression? From { get; set; }
    List<SelectExpression>? SelectList { get; }
    Type? EntityType { get; }
    DataProvider SqlClient { get; set; }
    bool IsPrepared { get; }
    Expression? Condition { get; }
    ILogger? Logger { get; set; }
    void ResetPreparation();
    void PrepareCommand(CancellationToken cancellationToken);
}

public class QueryCommand : IQueryCommand
{
    protected List<SelectExpression>? _selectList;
    protected FromExpression? _from;
    protected DataProvider _sqlClient;
    protected readonly LambdaExpression _exp;
    protected readonly Expression? _condition;
    protected bool _isPrepared;
    //private bool _hasCtor;
    protected Type? _srcType;
    public ILogger? Logger { get; set; }
    public QueryCommand(DataProvider sqlClient, LambdaExpression exp, Expression? condition)
    {
        _sqlClient = sqlClient;
        _exp = exp;
        _condition = condition;
    }
    public FromExpression? From { get => _from; set => _from = value; }
    public List<SelectExpression>? SelectList => _selectList;
    public Type? EntityType => _srcType;
    public DataProvider SqlClient
    {
        get => _sqlClient;
        set
        {
            ResetPreparation();
            _sqlClient = value;
        }
    }
    public bool IsPrepared => _isPrepared;
    public Expression? Condition => _condition;
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;
    }
    public virtual void PrepareCommand(CancellationToken cancellationToken)
    {
        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1.IsPrepared)
            _from.Table.AsT1.PrepareCommand(cancellationToken);

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

        _isPrepared = true;
        _selectList = selectList;
        _srcType = srcType;
    }
}

public class QueryCommand<TResult> : QueryCommand, IQueryCommand<TResult>
{
    private Delegate? _factory;
    public Delegate? Factory
    {
        get=>_factory;
        set=>_factory = value;
    }
    public QueryCommand(DataProvider sqlClient, LambdaExpression exp, Expression? condition) : base(sqlClient, exp, condition)
    {
    }

    public IEnumerator<object> Data { get; set; }

    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        return new InMemoryEnumerator<TResult>(this, Data);
    }

    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType)
    {
        return Expression.PropertyOrField(param, column.PropertyName!);
    }
}