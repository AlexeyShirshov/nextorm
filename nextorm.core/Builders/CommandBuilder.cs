using System.Collections;
using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class CommandBuilder<TEntity> : IAsyncEnumerable<TEntity>, ICloneable
{
    #region Fields
    //private IPayloadManager _payloadMgr = new FastPayloadManager(new Dictionary<Type, object?>());
    private readonly IDataProvider _dataProvider;
    private QueryCommand? _query;
    private Expression<Func<TEntity, bool>>? _condition;
    #endregion
    public CommandBuilder(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }
    public CommandBuilder(IDataProvider dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    #region Properties
    internal ILogger? Logger { get; set; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    internal IDataProvider DataProvider => _dataProvider;
    internal Expression<Func<TEntity, bool>>? Condition { get => _condition; set => _condition = value; }
    //internal IPayloadManager PayloadManager { get => _payloadMgr; init => _payloadMgr = value; }
    public delegate void CommandCreatedHandler<T>(CommandBuilder<T> sender, QueryCommand queryCommand);
    public event CommandCreatedHandler<TEntity>? CommandCreatedEvent;
    #endregion

    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(exp, _condition);
        cmd.Logger = Logger;

        if (_query is not null)
            cmd.From = new FromExpression(_query);

        OnCommandCreated(cmd);
        RaiseCommandCreated(cmd);

        return cmd;
    }

    internal void RaiseCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {
        CommandCreatedEvent?.Invoke(this, cmd);
    }

    protected virtual void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {

    }

    public CommandBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var b = Clone();
        if (_condition is not null)
        {
            var replVisitor = new ReplaceExpressionVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TEntity, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public CommandBuilder<TEntity> Clone()
    {
        return (CommandBuilder<TEntity>)CloneImp();
    }
    protected virtual void CopyTo(CommandBuilder<TEntity> dst)
    {
        dst._query = _query;
        //dst._payloadMgr = _payloadMgr;
        dst.CommandCreatedEvent += CommandCreatedEvent;
    }
    protected virtual object CloneImp()
    {
        var r = new CommandBuilder<TEntity>(_dataProvider) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    public static implicit operator QueryCommand<TEntity>(CommandBuilder<TEntity> builder) => builder.ToCommand();
    public QueryCommand<TEntity> ToCommand() => Select(it => it);
    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);
    public CommandBuilderP2<TEntity, TJoinEntity> Join<TJoinEntity>(CommandBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    {
        QueryCommand? query = null;
        if (_condition is not null || _query is not null)
        {
            query = ToCommand();
        }

        var cb = new CommandBuilderP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition)) { Logger = Logger, _query = query/*, PayloadManager = PayloadManager*/, BaseBuilder = this };
        return cb;
    }
    public CommandBuilderP2<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    {
        QueryCommand? queryBase = null;
        if (_condition is not null || _query is not null)
        {
            queryBase = ToCommand();
        }

        var cb = new CommandBuilderP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition) { Query = query }) { Logger = Logger, _query = queryBase/*, PayloadManager = PayloadManager*/, BaseBuilder = this };
        return cb;
    }
}
public class CommandBuilder
{
    private readonly SqlDataProvider _dataProvider;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression? _condition;
    public CommandBuilder(SqlDataProvider dataProvider, string table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TableAlias, TResult>> exp)
    {
        if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        return new QueryCommand<TResult>(_dataProvider, exp, _condition) { From = new FromExpression(_table), Logger = Logger };
    }

    public CommandBuilder Where(Expression<Func<TableAlias, bool>> condition)
    {
        if (_condition is not null)
            _condition = Expression.AndAlso(_condition, condition);
        else
            _condition = condition;

        return this;
    }
}
