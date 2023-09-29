using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class CommandBuilder<TEntity> : IAsyncEnumerable<TEntity>
{
    private readonly IDataProvider _dataProvider;
    private readonly QueryCommand? _query;
    private Expression? _condition;

    internal ILogger? Logger { get; set; }
    public CommandBuilder(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }
    public CommandBuilder(IDataProvider dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(exp, _condition);
        cmd.Logger = Logger;

        if (_query is not null)
            cmd.From = new FromExpression(_query);

        OnCommandCreated(cmd);

        return cmd;
    }

    protected virtual void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {

    }

    public CommandBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        if (_condition is not null)
            _condition = Expression.AndAlso(_condition, condition);
        else
            _condition = condition;

        return this;
    }
    public static implicit operator QueryCommand<TEntity>(CommandBuilder<TEntity> builder) => builder.ToCommand();
    public QueryCommand<TEntity> ToCommand() => Select(it => it);
    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);
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
