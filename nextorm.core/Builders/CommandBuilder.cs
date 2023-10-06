using System.Collections;
using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class CommandBuilder<TEntity> : IAsyncEnumerable<TEntity>
{
    #region Fields
    private ArrayList _payload = new();
    private readonly IDataProvider _dataProvider;
    private QueryCommand? _query;
    private Expression? _condition;
    #endregion
    #region Properties
    internal ILogger? Logger { get; set; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    internal IDataProvider DataProvider => _dataProvider;
    internal Expression? Condition { get => _condition; set => _condition = value; }
    internal ArrayList Payload { get => _payload; init => _payload = value; }
    public delegate void CommandCreatedHandler<T>(CommandBuilder<T> sender, QueryCommand queryCommand);
    public event CommandCreatedHandler<TEntity>? CommandCreatedEvent;
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
    #region Payload
    public bool RemovePayload<T>()
        where T : class, IPayload
    {
        foreach (var item in _payload)
        {
            if (item is T)
            {
                _payload.Remove(item);
                return true;
            }
        }
        return false;
    }
    public bool TryGetPayload<T>(out T? payload)
        where T : class, IPayload
    {
        foreach (var item in _payload)
        {
            if (item is T p)
            {
                payload = p;
                return true;
            }
        }
        payload = default;
        return false;
    }
    public bool TryGetNotNullPayload<T>(out T? payload)
        where T : class, IPayload
    {
        foreach (var item in _payload)
        {
            if (item is T p && p is not null)
            {
                payload = p;
                return true;
            }
        }
        payload = default;
        return false;
    }
    public T GetNotNullOrAddPayload<T>(Func<T> factory)
        where T : class, IPayload
    {
        if (!TryGetNotNullPayload<T>(out var payload))
        {
            ArgumentNullException.ThrowIfNull(factory);

            payload = factory();
            _payload.Add(payload);
        }
        return payload!;
    }
    public T? GetOrAddPayload<T>(Func<T?> factory)
        where T : class, IPayload
    {
        if (!TryGetPayload<T>(out var payload))
        {
            payload = factory is null
                ? default
                : factory();
            _payload.Add(payload);
        }
        return payload;
    }
    public T? AddOrUpdatePayload<T>(Func<T?> factory, Func<T?, T?>? update = null)
        where T : class, IPayload
    {
        for (int i = 0; i < _payload.Count; i++)
        {
            var item = _payload[i];

            if (item is T exists)
            {
                var p = update != null
                    ? update(exists)
                    : factory();

                _payload[i] = p;
                return p;
            }
        }

        var payload = factory();
        _payload.Add(payload);
        return payload;
    }
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
        if (_condition is not null)
            _condition = Expression.AndAlso(_condition, condition);
        else
            _condition = condition;

        return this;
    }
    public static implicit operator QueryCommand<TEntity>(CommandBuilder<TEntity> builder) => builder.ToCommand();
    public QueryCommand<TEntity> ToCommand() => Select(it => it);
    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);
    public CommandBuilderP2<TEntity, TJoinEntity> Join<TJoinEntity>(CommandBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    {
        var cb = new CommandBuilderP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition)) { Logger = Logger, _condition = _condition, _query = _query, Payload = Payload, BaseBuilder = this };
        return cb;
    }
    public CommandBuilderP2<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    {
        var cb = new CommandBuilderP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition) {Query = query}) { Logger = Logger, _condition = _condition, _query = _query, Payload = Payload, BaseBuilder = this };
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
