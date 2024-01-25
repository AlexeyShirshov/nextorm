using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;
// public class BaseEntity
// {
//     protected BaseEntity()
//     {
//     }
//     protected static readonly IDictionary<IDataContext, Lazy<QueryCommand<bool>>> _anyCommandCache = new ConcurrentDictionary<IDataContext, Lazy<QueryCommand<bool>>>();
// }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented", Justification = "<Pending>")]
public class Entity<TEntity> : ICloneable //IAsyncEnumerable<TEntity>
{
    // private const string AnyCommandProperty = "nextorm.core.AnyCommand";
    #region Fields
    //private IPayloadManager _payloadMgr = new FastPayloadManager(new Dictionary<Type, object?>());
    private readonly IDataContext _dataProvider;
    private QueryCommand? _query;
    private Expression<Func<TEntity, bool>>? _condition;
    private LambdaExpression? _group;
    private Expression<Func<TEntity, bool>>? _having;
    private List<Sorting>? _sorting;
    protected List<JoinExpression>? _joins;
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
    internal ILogger? Logger { get; init; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    internal IDataContext DataProvider => _dataProvider;
    internal Expression<Func<TEntity, bool>>? Condition { get => _condition; set => _condition = value; }
    public List<Sorting>? Sorting { get => _sorting; set => _sorting = value; }
    public List<JoinExpression>? Joins { get => _joins; set => _joins = value; }
    public Paging Paging;

    //internal IPayloadManager PayloadManager { get => _payloadMgr; init => _payloadMgr = value; }
    //public delegate void CommandCreatedHandler<T>(Entity<T> sender, QueryCommand queryCommand);
    //public event CommandCreatedHandler<TEntity>? CommandCreatedEvent;
    #endregion

    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(exp, _condition, _joins?.ToArray(), Paging, _sorting?.ToArray(), _group, _having, Logger);

        if (_query is not null)
            cmd.From = new FromExpression(_query);

        // OnCommandCreated(cmd);
        //RaiseCommandCreated(cmd);

        return cmd;
    }
    public QueryCommand<TEntity> ToCommand()
    {
        var cmd = _dataProvider.CreateCommand<TEntity>(typeof(TEntity), _condition, _joins?.ToArray(), Paging, _sorting?.ToArray(), _group, _having, Logger);

        if (_query is not null)
            cmd.From = new FromExpression(_query);

        // OnCommandCreated(cmd);
        //RaiseCommandCreated(cmd);

        return cmd;
    }

    // protected virtual void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    // {

    // }

    public Entity<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        var b = Clone();
        if (_condition is not null && condition is not null)
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
        //dst.CommandCreatedEvent += CommandCreatedEvent;
        dst.Paging = Paging;
        dst._condition = _condition;
        dst._sorting = _sorting;
        dst._group = _group;
        dst._having = _having;
    }
    protected virtual object CloneImp()
    {
        var r = new Entity<TEntity>(_dataProvider) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    public static implicit operator QueryCommand<TEntity>(Entity<TEntity> builder) => builder.ToCommand();
    public static implicit operator QueryCommand(Entity<TEntity> builder) => builder.ToCommand();
    // public QueryCommand<TEntity> ToCommand() => Select<TEntity>(typeof(TEntity));
    // public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TEntity> ToAsyncEnumerable(params object[] @params) => ToAsyncEnumerable(CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TEntity> ToAsyncEnumerable(CancellationToken cancellationToken, params object[] @params) => ToCommand().ToAsyncEnumerable(cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TEntity> ToEnumerable(params object[] @params) => ToCommand().ToEnumerable(@params);
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
    public Entity<TEntity> GroupBy<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;

        return b;
    }
    public Entity<TEntity> Having(Expression<Func<TEntity, bool>> condition)
    {
        var b = Clone();
        if (_having is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterVisitor(_having.Parameters[0]);
            var newBody = Expression.AndAlso(_having.Body, replVisitor.Visit(condition.Body));
            b._having = Expression.Lambda<Func<TEntity, bool>>(newBody, _having.Parameters[0]);
        }
        else
            b._having = condition;

        return b;
    }
    public QueryCommand<bool> AnyCommand()
    {
        var cmd = ToCommand();
        var queryCommand = _dataProvider.CreateCommand<bool>((TableAlias _) => NORM.SQL.exists(cmd), null, null, default, null, null, null, Logger);
        queryCommand.SingleRow = true;
        cmd.IgnoreColumns = true;
        return queryCommand;
    }
    public bool Any(params object[] @params)
    {
        var cmd = ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(_dataProvider, cmd);
        var preparedCommand = _dataProvider.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
        return _dataProvider.ExecuteScalar<bool>(preparedCommand, @params, true);
    }
    public List<TEntity> ToList(params object[] @params)
    {
        return ToCommand().ToList(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TEntity>> ToListAsync(params object[] @params) => ToListAsync(CancellationToken.None, @params);
    public Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().ToListAsync(cancellationToken, @params);
    }
    public TEntity First(params object[] @params)
    {
        return ToCommand().First(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity> FirstAsync(params object[] @params) => FirstAsync(CancellationToken.None, @params);
    public Task<TEntity> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().FirstAsync(cancellationToken, @params);
    }
    public QueryCommand<TEntity?> FirstOrFirstOrDefaultCommand()
    {
        var cmd = ToCommand();
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public QueryCommand<TResult?> FirstOrFirstOrDefaultCommand<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = Select(exp);
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public TEntity? FirstOrDefault(params object[] @params)
    {
        return ToCommand().FirstOrDefault(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity?> FirstOrDefaultAsync(params object[] @params) => FirstOrDefaultAsync(CancellationToken.None, @params);
    public Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().FirstOrDefaultAsync(cancellationToken, @params);
    }
    public TEntity Single(params object[] @params)
    {
        return ToCommand().Single(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity> SingleAsync(params object[] @params) => SingleAsync(CancellationToken.None, @params);
    public Task<TEntity> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().SingleAsync(cancellationToken, @params);
    }
    public QueryCommand<TResult?> SingleOrSingleOrDefaultCommand<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = Select(exp);
        cmd.Paging.Limit = 2;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TEntity?> SingleOrDefaultAsync(params object[] @params) => SingleOrDefaultAsync(CancellationToken.None, @params);
    public Task<TEntity?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        return ToCommand().SingleOrDefaultAsync(cancellationToken, @params);
    }
    internal protected static QueryCommand<bool> GetAnyCommand(IDataContext dataProvider, QueryCommand cmd)
    {
        var created = false;
        if (dataProvider.AnyCommand is not Lazy<QueryCommand<bool>> anyCommand)
        {
            anyCommand = new Lazy<QueryCommand<bool>>(() =>
            {
                created = true;
                var queryCommand = dataProvider.CreateCommand<bool>((TableAlias _) => NORM.SQL.exists(cmd), null, null, default, null, null, null, cmd.Logger);
                queryCommand.SingleRow = true;
                queryCommand.PrepareCommand(false, CancellationToken.None);
                return queryCommand;
            });
            dataProvider.AnyCommand = anyCommand;
        }

        var queryCommand = anyCommand.Value;
        if (!created)
        {
            cmd.PrepareCommand(false, CancellationToken.None);
            queryCommand.ReplaceCommand(cmd, 0);
        }

        return queryCommand;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(params object[] @params) => AnyAsync(CancellationToken.None, @params);
    public async Task<bool> AnyAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(_dataProvider, cmd);
        var preparedCommand = _dataProvider.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        return await _dataProvider.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
    }
    public Entity<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp, OrderDirection direction)
    {
        var b = Clone();
        if (b._sorting is null)
            b._sorting = [new Sorting(orderExp) { Direction = direction }];
        else
            b._sorting.Add(new Sorting(orderExp) { Direction = direction });
        return b;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Asc);
    public Entity<TEntity> OrderBy(int columnIdx, OrderDirection direction)
    {
        if (columnIdx < 1) throw new ArgumentException("Column index must be greater than zero", nameof(columnIdx));

        var b = Clone();
        if (b._sorting is null)
            b._sorting = [new Sorting(columnIdx) { Direction = direction }];
        else
            b._sorting.Add(new Sorting(columnIdx) { Direction = direction });
        return b;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<TEntity> OrderBy(int columnIdx) => OrderBy(columnIdx, OrderDirection.Asc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Desc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<TEntity> OrderByDescending(int columnIdx) => OrderBy(columnIdx, OrderDirection.Desc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPreparedQueryCommand<TEntity> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default) => ToCommand().Prepare(nonStreamUsing, cancellationToken);
    public int Count(params object[] @params)
    {
        var cmd = Select(e => NORM.SQL.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<int> CountAsync(params object[] @params) => CountAsync(CancellationToken.None, @params);
    public Task<int> CountAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(e => NORM.SQL.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Min<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.MinMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> MinAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => MinAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> MinAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.MinMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Max<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.MaxMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> MaxAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => MaxAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> MaxAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.MaxMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Avg<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.AvgMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> AvgAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => AvgAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> AvgAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.AvgMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Sum<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.SumMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SumAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => SumAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> SumAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.SumMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Stdev<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.StdevMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.StdevMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Stdevp<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.StdevpMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevpAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.StdevpMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Var<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.VarMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.VarMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
    public TResult? Varp<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.VarpMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarpAsync(exp, CancellationToken.None, @params);
    public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(NORM.NORM_SQL.SQLExpression, NORM.NORM_SQL.VarpMI.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
}

public class Entity : ICloneable
{
    private readonly IDataContext _dataProvider;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression<Func<TableAlias, bool>>? _condition;
    private List<Sorting>? _sorting;
    private LambdaExpression? _group;
    private Expression<Func<TableAlias, bool>>? _having;
    protected List<JoinExpression>? _joins;
    public Entity(IDataContext dataProvider) : this(dataProvider, null) { }
    public Entity(IDataContext dataProvider, string? table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TableAlias, TResult>> exp)
    {
        //if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        var cmd = new QueryCommand<TResult>(_dataProvider, exp, _condition, _joins?.ToArray(), default, _sorting?.ToArray(), _group, _having, Logger);

        if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        return cmd;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public Entity Clone()
    {
        return (Entity)CloneImp();
    }
    protected virtual void CopyTo(Entity dst)
    {
        dst._condition = _condition;
        dst._sorting = _sorting;
    }
    protected virtual object CloneImp()
    {
        var r = new Entity(_dataProvider, _table) { Logger = Logger };

        CopyTo(r);

        return r;
    }

    public Entity Where(Expression<Func<TableAlias, bool>> condition)
    {
        var b = Clone();
        if (_condition is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TableAlias, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
}
