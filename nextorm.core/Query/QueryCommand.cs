#define PLAN_CACHE
#define INITALGO_1

using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class QueryCommand : /*IPayloadManager,*/ ISourceProvider, IParamProvider, IQueryProvider
{
    private List<QueryCommand> _referencedQueries;
    private readonly List<JoinExpression> _joins;
    protected List<SelectExpression>? _selectList;
    private int _columnsHash;
    private int _joinHash;
    protected FromExpression? _from;
    protected IDataContext _dataProvider;
    protected readonly LambdaExpression _exp;
    protected readonly Expression? _condition;
    protected bool _isPrepared;
    protected int? _hash;
    // #if PLAN_CACHE
    //     internal int? _hashPlan;
    // #endif
    protected Type? _srcType;
    private bool _dontCache;
#if DEBUG
    private int? _conditionHash;

    public int? ConditionHash => _conditionHash;
#endif
    public Expression SelectExpression => _exp;
#if PLAN_CACHE
    //    internal int? PlanHash;
    internal int ColumnsPlanHash;
    internal int JoinPlanHash;
#endif
    private int _paramIdx;
    public Paging Paging;
    //protected ArrayList _params = new();
    public QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition, Paging paging)
        : this(dataProvider, exp, condition/*, new FastPayloadManager(new Dictionary<Type, object?>())*/, new(), paging)
    {
    }
    protected QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition/*, IPayloadManager payloadMgr*/, List<JoinExpression> joins, Paging paging)
    {
        _dataProvider = dataProvider;
        _exp = exp;
        _condition = condition;
        //_payloadMgr = payloadMgr;
        _joins = joins;
        Paging = paging;
    }
    public ILogger? Logger { get; set; }
    public FromExpression? From { get => _from; set => _from = value; }
    public List<SelectExpression>? SelectList => _selectList;
    public Type? EntityType => _srcType;
    public IDataContext DataProvider
    {
        get => _dataProvider;
        set
        {
            ResetPreparation();
            _dataProvider = value;
        }
    }
    public bool IsPrepared => _isPrepared;
    public Expression? Condition => _condition;
    public List<JoinExpression> Joins => _joins;
    public bool Cache
    {
        get => !_dontCache;
        set => _dontCache = !value;
    }
    internal QueryCommand? FromQuery => From?.Table.AsT1;
    internal bool OneColumn { get; set; }
    internal bool IgnoreColumns { get; set; }
    public IReadOnlyList<QueryCommand> ReferencedQueries => _referencedQueries;

    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;
        _hash = null;

        _dataProvider.ResetPreparation(this);
    }
    public virtual void PrepareCommand(CancellationToken cancellationToken)
    {
#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Preparing command");
#endif
        OneColumn = false;

        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1.IsPrepared)
            _from.Table.AsT1.PrepareCommand(cancellationToken);

        var srcType = _srcType;
        if (srcType is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            srcType = _exp.Parameters[0].Type;
        }

        FromExpression? from = _from ?? _dataProvider.GetFrom(srcType, this);
        var (joinHash, joinPlanHash) = JoinHash(cancellationToken);
        var (selectList, columnsHash, columnsPlanHash) = ColumnsHash(srcType, cancellationToken);

        _isPrepared = true;
        _selectList = selectList;
        _columnsHash = columnsHash;
        _srcType = srcType;
        _from = from;
        _joinHash = joinHash;

#if PLAN_CACHE
        ColumnsPlanHash = columnsPlanHash;
        JoinPlanHash = joinPlanHash;
#endif
        //_payloadMgr = new FastPayloadManager(cache ? new Dictionary<Type, object?>() : null);

        // string capitalize(string str)
        // {
        //     return string.Concat(new ReadOnlySpan<char>(Char.ToUpper(str[0])), str.AsSpan()[1..]);
        // }
    }

    private (List<SelectExpression>?, int, int) ColumnsHash(Type? srcType, CancellationToken cancellationToken)
    {
        var comparer = new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
        var selectList = _selectList;
        var columnsHash = 7;
#if PLAN_CACHE
        var columnsPlanHash = 7;
#endif
        if (selectList is null && !IgnoreColumns)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression must exists");

            selectList = new List<SelectExpression>();

            if (_exp.Body is NewExpression ctor)
            {
                //var isTuple = ctor.Type.IsTuple();

                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataProvider, this, cancellationToken);
                for (var idx = 0; idx < ctor.Arguments.Count; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (selectList, columnsHash, columnsPlanHash);

                    SelectExpression selExp;
                    var arg = ctor.Arguments[idx];
                    // if (arg is MemberExpression me)
                    // {
                    //     selExp = new SelectExpression(me.Type)
                    //     {
                    //         Index = idx,
                    //         PropertyName = me.Member.Name!,
                    //         Expression = arg
                    //     };
                    // }
                    // else
                    // {
                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    selExp = new SelectExpression(ctorParam.ParameterType, _dataProvider.ExpressionsCache, this)
                    {
                        Index = idx,
                        PropertyName = ctorParam.Name!,
                        Expression = innerQueryVisitor.Visit(arg)
                    };
                    //}
                    selectList.Add(selExp);

                    if (!_dontCache) unchecked
                        {
                            columnsHash = columnsHash * 13 + selExp.GetHashCode();
#if PLAN_CACHE
                            columnsPlanHash = columnsPlanHash * 13 + comparer.GetHashCode(selExp);
#endif
                        }
                }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type with at least one property");

            }
            else if (_exp.Body.Type.IsPrimitive || _exp.Body.Type == typeof(string) || (_exp.Body.Type.IsGenericType && _exp.Body.Type.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                OneColumn = true;
                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataProvider, this, cancellationToken);
                var selectExp = innerQueryVisitor.Visit(_exp);
                // if (_dataProvider.NeedMapping)
                // {

                // }
                // else
                {
                    var selExp = new SelectExpression(_exp.Body.Type, _dataProvider.ExpressionsCache, this)
                    {
                        //Index = 0,
                        Expression = selectExp,
                        //ReferencedQueries = innerQueryVisitor.ReferencedQueries
                    };
                    selectList.Add(selExp);
                    if (!_dontCache) unchecked
                        {
                            columnsHash = columnsHash * 13 + selExp.GetHashCode();
#if PLAN_CACHE
                            columnsPlanHash = columnsPlanHash * 13 + comparer.GetHashCode(selExp);
#endif
                        }
                }
            }
            else if (_dataProvider.NeedMapping)
            {
                var p = Expression.Parameter(srcType);

                if (_dataProvider.Metadata.TryGetValue(srcType, out var entityMeta))
                {
                    for (int idx = 0; idx < entityMeta.Properties.Count; idx++)
                    {
                        var prop = entityMeta.Properties[idx];

                        var pi = prop.PropertyInfo;

                        if (cancellationToken.IsCancellationRequested)
                            return (selectList, columnsHash, columnsPlanHash);

                        Expression exp = Expression.Lambda(Expression.Property(p, pi), p);

                        var selExp = new SelectExpression(pi.PropertyType, _dataProvider.ExpressionsCache, this)
                        {
                            Index = idx,
                            PropertyName = pi.Name,
                            Expression = exp,
                            PropertyInfo = pi
                        };

                        selectList.Add(selExp);

                        if (!_dontCache) unchecked
                            {
                                columnsHash = columnsHash * 13 + selExp.GetHashCode();
#if PLAN_CACHE
                                columnsPlanHash = columnsPlanHash * 13 + comparer.GetHashCode(selExp);
#endif
                            }
                    }
                }
                //                 var props = srcType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanWrite).ToArray();
                //                 for (int idx = 0; idx < props.Length; idx++)
                //                 {
                //                     if (cancellationToken.IsCancellationRequested)
                //                         return;

                //                     var prop = props[idx];
                //                     if (prop is null) continue;
                //                     var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true);
                //                     if (colAttr is not null)
                //                     {
                //                         //Expression<Func<TableAlias, object>> exp = tbl => tbl.Column(colAttr.Name!);
                //                         Expression exp = Expression.Lambda(Expression.Property(p, prop), p);

                //                         var selExp = new SelectExpression(prop.PropertyType)
                //                         {
                //                             Index = idx,
                //                             PropertyName = prop.Name,
                //                             Expression = exp,
                //                             PropertyInfo = prop
                //                         };

                //                         selectList.Add(selExp);

                //                         if (!_dontCache) unchecked
                //                             {
                //                                 columnsHash = columnsHash * 13 + selExp.GetHashCode();
                // #if PLAN_CACHE
                //                                 columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
                // #endif
                //                             }
                //                     }
                //                     else
                //                     {
                //                         foreach (var interf in srcType.GetInterfaces())
                //                         {
                //                             if (cancellationToken.IsCancellationRequested)
                //                                 return;

                //                             var intMap = srcType.GetInterfaceMap(interf);

                //                             var implIdx = Array.IndexOf(intMap.TargetMethods, prop!.GetMethod);
                //                             if (implIdx >= 0)
                //                             {
                //                                 var intMethod = intMap.InterfaceMethods[implIdx];

                //                                 var intProp = interf.GetProperties().FirstOrDefault(prop => prop.GetMethod == intMethod);
                //                                 colAttr = intProp?.GetCustomAttribute<ColumnAttribute>(true);
                //                                 if (colAttr is not null)
                //                                 {
                //                                     //Expression<Func<TableAlias, object>> exp = tbl => tbl.Column(colAttr.Name!);
                //                                     Expression exp = Expression.Lambda(Expression.Property(p, prop), p);

                //                                     var selExp = new SelectExpression(prop!.PropertyType)
                //                                     {
                //                                         Index = idx,
                //                                         PropertyName = prop.Name,
                //                                         Expression = exp,
                //                                         PropertyInfo = prop
                //                                     };

                //                                     selectList.Add(selExp);

                //                                     if (!_dontCache) unchecked
                //                                         {
                //                                             columnsHash = columnsHash * 13 + selExp.GetHashCode();
                // #if PLAN_CACHE
                //                                             columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
                // #endif
                //                                         }
                //                                 }
                //                             }
                //                         }
                //                     }
                //                 }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type");
            }
            // else
            //     throw new NotImplementedException();
        }
        return (selectList, columnsHash, columnsPlanHash);
    }

    private (int, int) JoinHash(CancellationToken cancellationToken)
    {
        var comparer = new JoinExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
        var joinHash = 7;
#if PLAN_CACHE
        var joinPlanHash = 7;
#endif
        if (Joins.Any())
        {
            if (_from is not null && string.IsNullOrEmpty(_from.TableAlias)) _from.TableAlias = "t1";

            foreach (var join in Joins)
            {
                if (!(join.Query?.IsPrepared ?? true))
                    join.Query!.PrepareCommand(cancellationToken);

                if (!_dontCache) unchecked
                    {
                        joinHash = joinHash * 13 + join.GetHashCode();
#if PLAN_CACHE
                        joinPlanHash = joinPlanHash * 13 + comparer.GetHashCode(join);
#endif
                    }
            }
        }

        return (joinHash, joinPlanHash);
    }

    // public bool RemovePayload<T>() where T : class, IPayload
    // {
    //     return _payloadMgr.RemovePayload<T>();
    // }

    // public bool TryGetPayload<T>(out T? payload) where T : class, IPayload
    // {
    //     return _payloadMgr.TryGetPayload<T>(out payload);
    // }

    // public bool TryGetNotNullPayload<T>(out T? payload) where T : class, IPayload
    // {
    //     return _payloadMgr.TryGetNotNullPayload<T>(out payload);
    // }

    // public T GetNotNullOrAddPayload<T>(Func<T> factory) where T : class, IPayload
    // {
    //     return _payloadMgr.GetNotNullOrAddPayload<T>(factory);
    // }

    // public T? GetOrAddPayload<T>(Func<T?> factory) where T : class, IPayload
    // {
    //     return _payloadMgr.GetOrAddPayload<T>(factory);
    // }

    // public T? AddOrUpdatePayload<T>(Func<T?> factory, Func<T?, T?>? update = null) where T : class, IPayload
    // {
    //     return _payloadMgr.AddOrUpdatePayload<T>(factory, update);
    // }
    // public void AddOrUpdatePayload<T>(T? payload) where T : class, IPayload
    // {
    //     _payloadMgr.AddOrUpdatePayload<T>(payload);
    // }
    public override int GetHashCode()
    {
        if (_hash.HasValue)
            return _hash.Value;

        if (!IsPrepared)
            PrepareCommand(CancellationToken.None);

        unchecked
        {
            HashCode hash = new();
            if (_from is not null)
                hash.Add(_from);

            if (_srcType is not null)
                hash.Add(_srcType);

            if (_condition is not null)
            {
                var comp = new PreciseExpressionEqualityComparer((_dataProvider as DbContext)?.ExpressionsCache, this, (_dataProvider as DbContext)?.Logger);
                var condHash = comp.GetHashCode(_condition);
                hash.Add(condHash);
#if DEBUG
                _conditionHash = condHash;
#endif
            }

            // foreach (var param in _params)
            // {
            //     hash.Add(param);
            // }

            hash.Add(_columnsHash);

            hash.Add(_joinHash);

            hash.Add(Paging.Limit);
            hash.Add(Paging.Offset);

            _hash = hash.ToHashCode();

            //Console.WriteLine(_hash);

            return _hash.Value;
        }
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryCommand);
    }
    public bool Equals(QueryCommand? cmd)
    {
        if (cmd is null) return false;

        if (!IsPrepared)
            PrepareCommand(CancellationToken.None);

        // if (_params is null && cmd._params is not null) return false;
        // if (_params is not null && cmd._params is null) return false;

        // if (_params is not null && cmd._params is not null)
        // {
        //     if (_params.Count != cmd._params.Count) return false;

        //     for (int i = 0; i < _params.Count; i++)
        //     {
        //         if (!Equals(_params[i], cmd._params[i])) return false;
        //     }
        // }

        if (!Equals(_from, cmd._from)) return false;

        if (_srcType != cmd._srcType) return false;

        if (Paging.Limit != cmd.Paging.Limit || Paging.Offset != cmd.Paging.Offset) return false;

        if (!new PreciseExpressionEqualityComparer((_dataProvider as DbContext)?.ExpressionsCache, this, (_dataProvider as DbContext)?.Logger).Equals(_condition, cmd._condition)) return false;

        if (_selectList is null && cmd._selectList is not null) return false;
        if (_selectList is not null && cmd._selectList is null) return false;

        if (_selectList is not null && cmd._selectList is not null)
        {
            if (_selectList.Count != cmd._selectList.Count) return false;

            for (int i = 0; i < _selectList.Count; i++)
            {
                if (!_selectList[i].Equals(cmd._selectList[i])) return false;
            }
        }

        if (Joins is null && cmd.Joins is not null) return false;
        if (Joins is not null && cmd.Joins is null) return false;

        if (Joins is not null && cmd.Joins is not null)
        {
            if (Joins.Count != cmd.Joins.Count) return false;

            for (int i = 0; i < Joins.Count; i++)
            {
                if (!Joins[i].Equals(cmd.Joins[i])) return false;
            }
        }

        return true;
    }
    public QueryCommand? FindSourceFromAlias(string? alias)
    {
        if (string.IsNullOrEmpty(alias))
        {
            if (_from?.Table.IsT1 ?? false)
                return _from!.Table.AsT1;

            return null;
        }

        var idx = int.Parse(alias[1..]);
        return Joins[idx - 2].Query;
    }
    public string GetParamName()
    {
        return string.Format("p{0}", _paramIdx++);
    }
    public void ReplaceCommand(QueryCommand cmd, int idx)
    {
        if (_dataProvider != cmd._dataProvider)
            throw new InvalidOperationException("Different data context");

        _referencedQueries[idx] = cmd;
        var comparer = new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
        ColumnsPlanHash = 7 * 13 + comparer.GetHashCode(_selectList![0]);
    }
    public int AddCommand(QueryCommand cmd)
    {
        if (_dataProvider != cmd._dataProvider)
            throw new InvalidOperationException("Different data context");

        _referencedQueries ??= new List<QueryCommand>();
        var idx = _referencedQueries.Count;
        _referencedQueries.Add(cmd);
        return idx;
    }

    // protected virtual void CopyTo(QueryCommand dst)
    // {
    //     dst._selectList = _selectList;
    //     dst._columnsHash = _columnsHash;
    //     dst._joinHash = _joinHash;
    //     dst._from = _from;
    //     dst._isPrepared = _isPrepared;
    //     dst._srcType = _srcType;
    //     dst._dontCache = _dontCache;
    //     dst.Logger = Logger;
    // }
}

public class QueryCommand<TResult> : QueryCommand, IAsyncEnumerable<TResult>
{
    //private readonly IPayloadManager _payloadMap = new FastPayloadManager(new Dictionary<Type, object?>());
    public QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition, Paging paging) : base(dataProvider, exp, condition, paging)
    {
    }
    protected QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition/*, IPayloadManager payloadMgr*/, List<JoinExpression> joins, Paging paging)
        : base(dataProvider, exp, condition/*, payloadMgr*/, joins, paging)
    {

    }
    //internal CompiledQuery<TResult>? Compiled => CacheEntry?.CompiledQuery as CompiledQuery<TResult>;
    public CacheEntry? CacheEntry { get; set; }
    public bool SingleRow { get; set; }
    public QueryCommand<TResult> Compile(bool nonStreamCalls, CancellationToken cancellationToken = default)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        _dataProvider.Compile(this, nonStreamCalls, cancellationToken);

        return this;
    }
    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        return DataProvider.CreateAsyncEnumerator(this, null, cancellationToken);
    }
    public IAsyncEnumerable<TResult> Pipeline(params object[] @params) => Pipeline(CancellationToken.None, @params);
    public async IAsyncEnumerable<TResult> Pipeline(CancellationToken cancellationToken, params object[] @params)
    {
        var bus = Channel.CreateUnbounded<TResult>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        var t = Task.Run(async () =>
        {
            try
            {
                foreach (var item in AsEnumerable(@params))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await bus.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                bus.Writer.Complete();
            }
        }, cancellationToken);

        // return bus.Reader.ReadAllAsync(cancellationToken);
        while (await bus.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            yield return await bus.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        await t.ConfigureAwait(false);
    }
    public IAsyncEnumerable<TResult> ExecAsync(params object[] @params) => ExecAsync(CancellationToken.None, @params);
    public async IAsyncEnumerable<TResult> ExecAsync([EnumeratorCancellation] CancellationToken cancellationToken, params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        await using var ee = _dataProvider.CreateAsyncEnumerator(this, @params, cancellationToken);
        while (await ee.MoveNextAsync().ConfigureAwait(false))
            yield return ee.Current;
    }
    public IEnumerable<TResult> AsEnumerable(params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(CancellationToken.None);

        return (IEnumerable<TResult>)_dataProvider.CreateEnumerator(this, @params);
    }
    public Task<IEnumerable<TResult>> Exec(params object[] @params) => Exec(CancellationToken.None, @params);
    public async Task<IEnumerable<TResult>> Exec(CancellationToken cancellationToken, params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        // return Array.Empty<TResult>();
        var enumerator = await _dataProvider.CreateEnumeratorAsync(this, @params, cancellationToken).ConfigureAwait(false);

        if (enumerator is IEnumerable<TResult> ee)
            return ee;

        return new InternalEnumerable(enumerator);
    }
    public List<TResult> ToList(params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(CancellationToken.None);

        return _dataProvider.ToList(this, @params);
    }
    public Task<List<TResult>> ToListAsync(params object[] @params) => ToListAsync(CancellationToken.None, @params);
    public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        return await _dataProvider.ToListAsync(this, @params, cancellationToken).ConfigureAwait(false);
    }
    public bool Any(params object[] @params)
    {
        bool oldIgnoreColumns = IgnoreColumns;
        try
        {
            if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
            {
                if (!IgnoreColumns || !IsPrepared)
                {
                    IgnoreColumns = true;
                    PrepareCommand(CancellationToken.None);
                }

                queryCommand = Entity<TResult>.GetAnyCommand(_dataProvider, this);
            }

            return _dataProvider.ExecuteScalar(queryCommand, @params);
        }
        finally
        {
            IgnoreColumns = oldIgnoreColumns;
        }
    }
    public Task<bool> AnyAsync(params object[] @params) => AnyAsync(CancellationToken.None, @params);
    public async Task<bool> AnyAsync(CancellationToken cancellationToken, params object[] @params)
    {
        bool oldIgnoreColumns = IgnoreColumns;
        try
        {
            if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
            {
                if (!IgnoreColumns || !IsPrepared)
                {
                    IgnoreColumns = true;
                    PrepareCommand(cancellationToken);
                }

                queryCommand = Entity<TResult>.GetAnyCommand(_dataProvider, this);
            }

            return await _dataProvider.ExecuteScalar(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IgnoreColumns = oldIgnoreColumns;
        }
    }
    public TResult First(params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 1 || !IsPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(CancellationToken.None);
        }

        return _dataProvider.First(this, @params);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public Task<TResult> FirstAsync(params object[] @params) => FirstAsync(CancellationToken.None, @params);
    public Task<TResult> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 1 || !IsPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(cancellationToken);
        }

        return _dataProvider.FirstAsync(this, @params, cancellationToken);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public TResult? FirstOrDefault(params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 1 || !IsPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(CancellationToken.None);
        }

        return _dataProvider.FirstOrDefault(this, @params);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public Task<TResult?> FirstOrDefaultAsync(params object[] @params) => FirstOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 1 || !IsPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(cancellationToken);
        }

        return _dataProvider.FirstOrDefaultAsync(this, @params, cancellationToken);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public TResult Single(params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 2 || !IsPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(CancellationToken.None);
        }

        return _dataProvider.Single(this, @params);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public Task<TResult> SingleAsync(params object[] @params) => SingleAsync(CancellationToken.None, @params);
    public Task<TResult> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 2 || !IsPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(cancellationToken);
        }

        return _dataProvider.SingleAsync(this, @params, cancellationToken);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public TResult? SingleOrDefault(params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 2 || !IsPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(CancellationToken.None);
        }

        return _dataProvider.SingleOrDefault(this, @params);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    public Task<TResult?> SingleOrDefaultAsync(params object[] @params) => SingleOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        // int oldLim = Paging.Limit;
        // try
        // {
        if (Paging.Limit != 2 || !IsPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(cancellationToken);
        }

        return _dataProvider.SingleOrDefaultAsync(this, @params, cancellationToken);
        // }
        // finally
        // {
        //     Paging.Limit = oldLim;
        // }
    }
    // public QueryCommand<TResult> WithParams(params object[] @params)
    // {
    //     QueryCommand<TResult> cmd;

    //     if (CacheEntry?.CompiledQuery is IReplaceParam rp)
    //     {
    //         rp.ReplaceParams(@params, _dataProvider);
    //         cmd = this;
    //     }
    //     else
    //     {
    //         cmd = new QueryCommand<TResult>(_dataProvider, _exp, _condition, _payloadMgr, Joins);
    //         CopyTo(cmd);
    //         cmd._params.AddRange(@params);
    //     }

    //     return cmd;
    // }
    // protected override void CopyTo(QueryCommand dst)
    // {
    //     CopyTo(dst as QueryCommand<TResult>);
    // }
    // protected void CopyTo(QueryCommand<TResult>? dst)
    // {
    //     if (dst is not null)
    //     {
    //         dst.CacheEntry = CacheEntry;
    //     }
    // }
    class InternalEnumerable : IEnumerable<TResult>
    {
        private readonly IEnumerator<TResult> _enumerator;
        public InternalEnumerable(IEnumerator<TResult> enumerator)
        {
            _enumerator = enumerator;
        }
        IEnumerator<TResult> IEnumerable<TResult>.GetEnumerator() => _enumerator;
        IEnumerator IEnumerable.GetEnumerator() => _enumerator;
    }
}