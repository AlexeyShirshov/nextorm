using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class Entity<TEntity> : IAsyncEnumerable<TEntity>, ICloneable
{
    #region Fields
    //private IPayloadManager _payloadMgr = new FastPayloadManager(new Dictionary<Type, object?>());
    private readonly IDataContext _dataProvider;
    private QueryCommand? _query;
    private Expression<Func<TEntity, bool>>? _condition;
    private static Lazy<QueryCommand<bool>>? _anyCommand;
    #endregion
    public Entity(IDataContext dataProvider)
    {
        _dataProvider = dataProvider;
    }
    public Entity(IDataContext dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    #region Properties
    internal ILogger? Logger { get; set; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    internal IDataContext DataProvider => _dataProvider;
    internal Expression<Func<TEntity, bool>>? Condition { get => _condition; set => _condition = value; }
    public Paging Paging;

    //internal IPayloadManager PayloadManager { get => _payloadMgr; init => _payloadMgr = value; }
    public delegate void CommandCreatedHandler<T>(Entity<T> sender, QueryCommand queryCommand);
    public event CommandCreatedHandler<TEntity>? CommandCreatedEvent;
    #endregion

    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(exp, _condition, Paging);
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

    public Entity<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var b = Clone();
        if (_condition is not null)
        {
            var replVisitor = new ReplaceParameterVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TEntity, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
    public Entity<TEntity> Limit(int limit)
    {
        var b = Clone();

        b.Paging.Limit = limit;

        return b;
    }
    public Entity<TEntity> Offset(int offset)
    {
        var b = Clone();

        b.Paging.Offset = offset;

        return b;
    }
    public Entity<TEntity> Page(int limit, int offset)
    {
        var b = Clone();

        b.Paging.Limit = limit;
        b.Paging.Offset = offset;

        return b;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public Entity<TEntity> Clone()
    {
        return (Entity<TEntity>)CloneImp();
    }
    protected virtual void CopyTo(Entity<TEntity> dst)
    {
        dst._query = _query;
        //dst._payloadMgr = _payloadMgr;
        dst.CommandCreatedEvent += CommandCreatedEvent;
        dst.Paging = Paging;
    }
    protected virtual object CloneImp()
    {
        var r = new Entity<TEntity>(_dataProvider) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    public static implicit operator QueryCommand<TEntity>(Entity<TEntity> builder) => builder.ToCommand();
    public QueryCommand<TEntity> ToCommand() => Select(it => it);
    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);
    public EntityP2<TEntity, TJoinEntity> Join<TJoinEntity>(Entity<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    {
        QueryCommand? query = null;
        if (_condition is not null || _query is not null)
        {
            query = ToCommand();
        }

        var cb = new EntityP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition)) { Logger = Logger, _query = query/*, PayloadManager = PayloadManager*/, BaseBuilder = this };
        return cb;
    }
    public EntityP2<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    {
        QueryCommand? queryBase = null;
        if (_condition is not null || _query is not null)
        {
            queryBase = ToCommand();
        }

        var cb = new EntityP2<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition) { Query = query }) { Logger = Logger, _query = queryBase/*, PayloadManager = PayloadManager*/, BaseBuilder = this };
        return cb;
    }
    public QueryCommand<bool> AnyCommand()
    {
        var cmd = ToCommand();
        var queryCommand = _dataProvider.CreateCommand<bool>((TableAlias _) => NORM.SQL.exists(cmd), null, default);
        queryCommand.SingleRow = true;
        cmd.IgnoreColumns = true;
        return queryCommand;
    }
    public bool Any(params object[] @params)
    {
        var cmd = ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(_dataProvider, cmd);

        return _dataProvider.ExecuteScalar(queryCommand, @params);
    }
    public List<TEntity> ToList(params object[] @params)
    {
        return ToCommand().ToList(@params);
    }
    public Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().ToListAsync(cancellationToken, @params);
    }
    public TEntity First(params object[] @params)
    {
        return ToCommand().First(@params);
    }
    public Task<TEntity> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().FirstAsync(cancellationToken, @params);
    }
    public QueryCommand<TEntity> FirstOrFirstOrDefaultCommand()
    {
        var cmd = ToCommand();
        cmd.Paging.Limit = 1;
        return cmd;
    }
    public TEntity? FirstOrDefault(params object[] @params)
    {
        return ToCommand().FirstOrDefault(@params);
    }
    public Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().FirstOrDefaultAsync(cancellationToken, @params);
    }
    public TEntity Single(params object[] @params)
    {
        return ToCommand().Single(@params);
    }
    public Task<TEntity> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().SingleAsync(cancellationToken, @params);
    }
    public QueryCommand<TEntity> SingleOrSingleOrDefaultCommand()
    {
        var cmd = ToCommand();
        cmd.Paging.Limit = 2;
        return cmd;
    }
    public TEntity? SingleOrDefault(params object[] @params)
    {
        return ToCommand().SingleOrDefault(@params);
    }
    public Task<TEntity?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().SingleOrDefaultAsync(cancellationToken, @params);
    }
    internal protected static QueryCommand<bool> GetAnyCommand(IDataContext dataProvider, QueryCommand cmd)
    {
        var created = false;
        if (_anyCommand is null)
        {
            _anyCommand = new Lazy<QueryCommand<bool>>(() =>
            {
                created = true;
                var queryCommand = dataProvider.CreateCommand<bool>((TableAlias _) => NORM.SQL.exists(cmd), null, default);
                queryCommand.SingleRow = true;
                queryCommand.PrepareCommand(CancellationToken.None);
                return queryCommand;
            });
        }
        var queryCommand = _anyCommand.Value;
        if (!created)
        {
            cmd.PrepareCommand(CancellationToken.None);
            queryCommand.ReplaceCommand(cmd, 0);
        }

        return queryCommand;
    }

    public Task<bool> AnyAsync(params object[] @params) => AnyAsync(CancellationToken.None, @params);
    public async Task<bool> AnyAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(_dataProvider, cmd);

        return await _dataProvider.ExecuteScalar(queryCommand, @params, cancellationToken).ConfigureAwait(false);
    }
}
public class CommandBuilder
{
    private readonly IDataContext _dataProvider;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression? _condition;
    public CommandBuilder(IDataContext dataProvider) : this(dataProvider, null) { }
    public CommandBuilder(IDataContext dataProvider, string? table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TableAlias, TResult>> exp)
    {
        //if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        var cmd = new QueryCommand<TResult>(_dataProvider, exp, _condition, default) { Logger = Logger };

        if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        return cmd;
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
