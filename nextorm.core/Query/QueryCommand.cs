using Microsoft.Extensions.Logging;
using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace nextorm.core;

public class QueryCommand : /*IPayloadManager,*/ IQueryContext, ICloneable
{
    private List<QueryCommand>? _referencedQueries;
    private readonly List<JoinExpression> _joins;
    protected List<SelectExpression>? _selectList;
    private int _columnsHash;
    private int _joinHash;
    private int _sortingHash;
    private int _whereHash;
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
    //    internal int? PlanHash;
    internal int ColumnsPlanHash;
    internal int JoinPlanHash;
    internal int SortingPlanHash;
    internal int WherePlanHash;
    private int _paramIdx;
    public Paging Paging;
    internal Expression? PreparedCondition;
    private QueryPlanEqualityComparer? _queryPlanComparer;
    private ExpressionPlanEqualityComparer? _expressionPlanComparer;
    private SelectExpressionPlanEqualityComparer? _selectExpressionPlanComparer;
    private FromExpressionPlanEqualityComparer? _fromExpressionPlanComparer;
    private JoinExpressionPlanEqualityComparer? _joinExpressionPlanComparer;
    private PreciseExpressionEqualityComparer? _preciseExpressionComparer;
    protected readonly List<Sorting>? _sorting;

    //protected ArrayList _params = new();
    public QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition, Paging paging, List<Sorting>? sorting)
        : this(dataProvider, exp, condition/*, new FastPayloadManager(new Dictionary<Type, object?>())*/, new(), paging, sorting)
    {
    }
    protected QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition/*, IPayloadManager payloadMgr*/, List<JoinExpression> joins, Paging paging, List<Sorting>? sorting)
    {
        _dataProvider = dataProvider;
        _exp = exp;
        _condition = condition;
        //_payloadMgr = payloadMgr;
        _joins = joins;
        Paging = paging;
        _sorting = sorting;
    }
    public Expression SelectExpression => _exp;
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
    //public Expression? Condition => _condition;
    public List<JoinExpression> Joins => _joins;
    public bool Cache
    {
        get => !_dontCache;
        set => _dontCache = !value;
    }
    internal QueryCommand? FromQuery => From?.Table.AsT1;
    internal bool OneColumn { get; set; }
    internal bool IgnoreColumns { get; set; }
    public IReadOnlyList<QueryCommand> ReferencedQueries => _referencedQueries!;
    public List<Sorting>? Sorting => _sorting;
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;
        _hash = null;

        _dataProvider.ResetPreparation(this);
    }
    public void PrepareCommand(CancellationToken cancellationToken) => PrepareCommand(false, cancellationToken);
    public virtual void PrepareCommand(bool prepareMapOnly, CancellationToken cancellationToken)
    {
#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Preparing command");
#endif
        OneColumn = false;

        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1._isPrepared)
            _from.Table.AsT1.PrepareCommand(prepareMapOnly, cancellationToken);

        var srcType = _srcType;
        if (srcType is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            srcType = _exp.Parameters[0].Type;
        }

        FromExpression? from = _from ?? _dataProvider.GetFrom(srcType, this);
        var (joinHash, joinPlanHash) = PrepareJoin(prepareMapOnly, cancellationToken);
        var (selectList, columnsHash, columnsPlanHash) = PrepareColumns(prepareMapOnly, srcType, cancellationToken);
        var (whereHash, wherePlanHash) = PrepareWhere(prepareMapOnly, cancellationToken);
        var (sortingHash, sortingPlanHash) = PrepareSorting(prepareMapOnly, cancellationToken);

        _isPrepared = true;
        _selectList = selectList ?? [];
        _columnsHash = columnsHash;
        _srcType = srcType;
        _from = from;
        _joinHash = joinHash;
        _sortingHash = sortingHash;
        _whereHash = whereHash;

        ColumnsPlanHash = columnsPlanHash;
        JoinPlanHash = joinPlanHash;
        SortingPlanHash = sortingPlanHash;
        WherePlanHash = wherePlanHash;
        //_payloadMgr = new FastPayloadManager(cache ? new Dictionary<Type, object?>() : null);

        // string capitalize(string str)
        // {
        //     return string.Concat(new ReadOnlySpan<char>(Char.ToUpper(str[0])), str.AsSpan()[1..]);
        // }
    }

    private (List<SelectExpression>?, int, int) PrepareColumns(bool noHash, Type? srcType, CancellationToken cancellationToken)
    {
        //SelectExpressionPlanEqualityComparer? comparer = null;
        var selectList = _selectList;
        var columnsHash = 7;
        var columnsPlanHash = 7;
        if (selectList is null && !IgnoreColumns)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression must exists");

            if (_exp.Body is NewExpression ctor)
            {
                selectList = new List<SelectExpression>();


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

                    if (!_dontCache && !noHash) unchecked
                        {
                            columnsHash = columnsHash * 13 + selExp.GetHashCode();

                            //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                            columnsPlanHash = columnsPlanHash * 13 + GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                        }
                }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type with at least one property");

            }
            else if (_exp.Body.Type.IsPrimitive || _exp.Body.Type == typeof(string) || (_exp.Body.Type.IsGenericType && _exp.Body.Type.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                selectList = new List<SelectExpression>();

                OneColumn = true;
                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataProvider, this, cancellationToken);
                var selectExp = innerQueryVisitor.Visit(_exp);
                // if (_dataProvider.NeedMapping)
                // {

                // }
                // else
                //{
                var selExp = new SelectExpression(_exp.Body.Type, _dataProvider.ExpressionsCache, this)
                {
                    //Index = 0,
                    Expression = selectExp,
                    //ReferencedQueries = innerQueryVisitor.ReferencedQueries
                };
                selectList.Add(selExp);
                if (!_dontCache && !noHash) unchecked
                    {

                        columnsHash = columnsHash * 13 + selExp.GetHashCode();
                        //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                        columnsPlanHash = columnsPlanHash * 13 + GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                    }
                // }
            }
            else if (_dataProvider.NeedMapping && !_dataProvider.SelectListCache.TryGetValue(srcType!, out selectList))
            {
                selectList = new List<SelectExpression>();

                var p = Expression.Parameter(srcType!);

                if (_dataProvider.Metadata.TryGetValue(srcType!, out var entityMeta))
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

                        if (!_dontCache && !noHash) unchecked
                            {
                                columnsHash = columnsHash * 13 + selExp.GetHashCode();
                                //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                                columnsPlanHash = columnsPlanHash * 13 + GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                            }
                    }
                }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type");

                _dataProvider.SelectListCache[srcType!] = selectList;
            }
            // else
            //     throw new NotImplementedException();
        }
        return (selectList, columnsHash, columnsPlanHash);
    }
    private (int, int) PrepareJoin(bool noHash, CancellationToken cancellationToken)
    {
        //JoinExpressionPlanEqualityComparer? comparer = null;
        var joinHash = 7;
        var joinPlanHash = 7;
        if (Joins.Count != 0)
        {
            if (_from is not null && string.IsNullOrEmpty(_from.TableAlias)) _from.TableAlias = "t1";

            foreach (var join in Joins)
            {
                if (!(join.Query?._isPrepared ?? true))
                    join.Query!.PrepareCommand(noHash, cancellationToken);

                if (!_dontCache && !noHash) unchecked
                    {
                        //comparer ??= new JoinExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                        joinHash = joinHash * 13 + join.GetHashCode();
                        joinPlanHash = joinPlanHash * 13 + GetJoinExpressionPlanEqualityComparer().GetHashCode(join);
                    }
            }
        }

        return (joinHash, joinPlanHash);
    }
    private (int, int) PrepareSorting(bool noHash, CancellationToken cancellationToken)
    {
        //PreciseExpressionEqualityComparer? comp = null;
        //ExpressionPlanEqualityComparer? compPlan = null;

        var sortingHash = 7;
        var sortingPlanHash = 7;
        if (_sorting is not null)
        {
            var sortingSpan = CollectionsMarshal.AsSpan(_sorting);
            for (var i = 0; i < _sorting.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                ref var sort = ref sortingSpan[i];

                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataProvider, this, cancellationToken);
                sort.PreparedExpression = innerQueryVisitor.Visit(sort.SortExpression);

                if (!_dontCache && !noHash) unchecked
                    {
                        sortingHash = sortingHash * 13 + (int)sort.Direction;
                        //comp ??= new PreciseExpressionEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                        sortingHash = sortingHash * 13 + GetPreciseExpressionEqualityComparer().GetHashCode(sort.PreparedExpression);

                        //compPlan ??= new ExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                        sortingPlanHash = sortingPlanHash * 13 + GetExpressionPlanEqualityComparer().GetHashCode(sort.PreparedExpression);
                    }
            }
        }

        return (sortingHash, sortingPlanHash);
    }
    private (int, int) PrepareWhere(bool noHash, CancellationToken cancellationToken)
    {
        //PreciseExpressionEqualityComparer? comp = null;
        //ExpressionPlanEqualityComparer? compPlan = null;

        var whereHash = 7;
        var wherePlanHash = 7;
        if (_condition is not null)
        {
            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataProvider, this, cancellationToken);
            PreparedCondition = innerQueryVisitor.Visit(_condition);

            if (!_dontCache && !noHash) unchecked
                {
                    //comp ??= new PreciseExpressionEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                    whereHash = whereHash * 13 + GetPreciseExpressionEqualityComparer().GetHashCode(PreparedCondition);

                    //compPlan ??= new ExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                    wherePlanHash = wherePlanHash * 13 + GetExpressionPlanEqualityComparer().GetHashCode(PreparedCondition);
                }
        }

        return (whereHash, wherePlanHash);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
    public override int GetHashCode()
    {
        if (_hash.HasValue)
            return _hash.Value;

        if (!_isPrepared)
            PrepareCommand(false, CancellationToken.None);

        unchecked
        {
            HashCode hash = new();
            if (_from is not null)
                hash.Add(_from);

            if (_srcType is not null)
                hash.Add(_srcType);

            hash.Add(_whereHash);

            hash.Add(_columnsHash);

            hash.Add(_joinHash);

            hash.Add(_sortingHash);

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

        if (!_isPrepared)
            PrepareCommand(false, CancellationToken.None);

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

        if (!GetPreciseExpressionEqualityComparer().Equals(PreparedCondition, cmd.PreparedCondition)) return false;

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

        if (Sorting is null && cmd.Sorting is not null) return false;
        if (Sorting is not null && cmd.Sorting is null) return false;

        if (Sorting is not null && cmd.Sorting is not null)
        {
            if (Sorting.Count != cmd.Sorting.Count) return false;
            var comp = GetPreciseExpressionEqualityComparer();

            for (int i = 0; i < Sorting.Count; i++)
            {
                if (Sorting[i].Direction != cmd.Sorting[i].Direction) return false;

                if (!comp.Equals(Sorting[i].PreparedExpression, cmd.Sorting[i].PreparedExpression)) return false;
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

        _referencedQueries![idx] = cmd;
        var comparer = GetSelectExpressionPlanEqualityComparer();
        ColumnsPlanHash = 7 * 13 + comparer.GetHashCode(_selectList![0]);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0028:Simplify collection initialization", Justification = "<Pending>")]
    public int AddCommand(QueryCommand cmd)
    {
        // if (_dataProvider != cmd._dataProvider)
        //     throw new InvalidOperationException("Different data context");

        _referencedQueries ??= new List<QueryCommand>();
        var idx = _referencedQueries.Count;
        _referencedQueries.Add(cmd);
        return idx;
    }

    protected virtual void CopyTo(QueryCommand dst)
    {
        dst._selectList = _selectList;
        dst._columnsHash = _columnsHash;
        dst._joinHash = _joinHash;
        dst._from = _from;
        dst._isPrepared = _isPrepared;
        dst._srcType = _srcType;
        dst._dontCache = _dontCache;
        dst._hash = _hash;
        dst.ColumnsPlanHash = ColumnsPlanHash;
        dst.JoinPlanHash = JoinPlanHash;
        dst.WherePlanHash = WherePlanHash;
        dst.SortingPlanHash = SortingPlanHash;
        dst.PreparedCondition = PreparedCondition;
    }

    protected virtual QueryCommand CreateSelf()
    {
        return new QueryCommand(_dataProvider, _exp, _condition, Joins, Paging, _sorting);
    }
    public QueryCommand Clone()
    {
        return (QueryCommand)(this as ICloneable).Clone();
    }
    object ICloneable.Clone()
    {
        var cmd = CreateSelf();
        CopyTo(cmd);
        return cmd;
    }

    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => _queryPlanComparer ??= new QueryPlanEqualityComparer(_dataProvider.ExpressionsCache, this);

    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => _expressionPlanComparer ??= new ExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);

    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => _selectExpressionPlanComparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);

    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => _fromExpressionPlanComparer ??= new FromExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);

    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => _joinExpressionPlanComparer ??= new JoinExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);

    public PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer() => _preciseExpressionComparer ??= new PreciseExpressionEqualityComparer(_dataProvider.ExpressionsCache, this);
}

public class QueryCommand<TResult> : QueryCommand, IAsyncEnumerable<TResult>
{
    internal object? _compiledQuery;
    //private readonly IPayloadManager _payloadMap = new FastPayloadManager(new Dictionary<Type, object?>());
    public QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition, Paging paging, List<Sorting>? sorting) : base(dataProvider, exp, condition, paging, sorting)
    {
    }
    protected QueryCommand(IDataContext dataProvider, LambdaExpression exp, Expression? condition/*, IPayloadManager payloadMgr*/, List<JoinExpression> joins, Paging paging, List<Sorting>? sorting)
        : base(dataProvider, exp, condition/*, payloadMgr*/, joins, paging, sorting)
    {

    }
    //internal CompiledQuery<TResult>? Compiled => CacheEntry?.CompiledQuery as CompiledQuery<TResult>;
    public CompiledQuery<TResult, TRecord>? GetCompiledQuery<TRecord>() => _compiledQuery as CompiledQuery<TResult, TRecord>;
    public bool SingleRow { get; set; }
    public QueryCommand<TResult> FromSql(string sql, CancellationToken cancellationToken = default) => Compile(sql, null, true, true, cancellationToken);
    public QueryCommand<TResult> FromSql(string sql, object? @params, CancellationToken cancellationToken = default) => Compile(sql, @params, true, true, cancellationToken);
    public QueryCommand<TResult> Compile(string sql, CancellationToken cancellationToken = default) => Compile(sql, null, true, cancellationToken);
    public QueryCommand<TResult> Compile(string sql, object? @params, CancellationToken cancellationToken = default) => Compile(sql, @params, true, cancellationToken);
    public QueryCommand<TResult> Compile(string sql, bool nonStreamUsing, CancellationToken cancellationToken = default) => Compile(sql, null, nonStreamUsing, cancellationToken);
    public QueryCommand<TResult> Compile(string sql, object? @params, bool nonStreamUsing, CancellationToken cancellationToken = default) => Compile(sql, @params, nonStreamUsing, true, cancellationToken);
    public QueryCommand<TResult> Compile(string sql, object? @params, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken = default)
    {
        _dataProvider.Compile(sql, @params, this, nonStreamUsing, storeInCache, cancellationToken);

        return this;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public QueryCommand<TResult> Compile(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
    {
        _dataProvider.Compile(this, nonStreamUsing, false, cancellationToken);

        return this;
    }
    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!_isPrepared)
            PrepareCommand(false, cancellationToken);

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
        if (!_isPrepared)
            PrepareCommand(false, cancellationToken);

        await using var ee = _dataProvider.CreateAsyncEnumerator(this, @params, cancellationToken);
        while (await ee.MoveNextAsync().ConfigureAwait(false))
            yield return ee.Current;
    }
    public IEnumerable<TResult> AsEnumerable(params object[] @params)
    {
        if (!_isPrepared)
            PrepareCommand(false, CancellationToken.None);

        return (IEnumerable<TResult>)_dataProvider.CreateEnumerator(this, @params);
    }
    public Task<IEnumerable<TResult>> Exec(params object[] @params) => Exec(CancellationToken.None, @params);
    public async Task<IEnumerable<TResult>> Exec(CancellationToken cancellationToken, params object[] @params)
    {
        if (!_isPrepared)
            PrepareCommand(false, cancellationToken);

        // return Array.Empty<TResult>();
        var enumerator = await _dataProvider.CreateEnumeratorAsync(this, @params, cancellationToken).ConfigureAwait(false);

        if (enumerator is IEnumerable<TResult> ee)
            return ee;

        return new InternalEnumerable(enumerator);
    }
    public List<TResult> ToList(params object[] @params)
    {
        if (!_isPrepared)
            PrepareCommand(false, CancellationToken.None);

        return _dataProvider.ToList(this, @params);
    }
    public Task<List<TResult>> ToListAsync(params object[] @params) => ToListAsync(CancellationToken.None, @params);
    public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (!_isPrepared)
            PrepareCommand(false, cancellationToken);

        return await _dataProvider.ToListAsync(this, @params, cancellationToken).ConfigureAwait(false);
    }
    public bool Any(params object[] @params)
    {
        bool oldIgnoreColumns = IgnoreColumns;
        try
        {
            if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
            {
                if (!IgnoreColumns || !_isPrepared)
                {
                    IgnoreColumns = true;
                    PrepareCommand(false, CancellationToken.None);
                }

                queryCommand = Entity<TResult>.GetAnyCommand(_dataProvider, this);
            }

            return _dataProvider.ExecuteScalar(queryCommand, @params, true);
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
                if (!IgnoreColumns || !_isPrepared)
                {
                    IgnoreColumns = true;
                    PrepareCommand(false, cancellationToken);
                }

                queryCommand = Entity<TResult>.GetAnyCommand(_dataProvider, this);
            }

            return await _dataProvider.ExecuteScalar(queryCommand, @params, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IgnoreColumns = oldIgnoreColumns;
        }
    }
    public TResult First(params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 1 || !_isPrepared)
            {
                SingleRow = true;
                Paging.Limit = 1;
                PrepareCommand(false, CancellationToken.None);
            }

            return _dataProvider.First(this, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public Task<TResult> FirstAsync(params object[] @params) => FirstAsync(CancellationToken.None, @params);
    public Task<TResult> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 1 || !_isPrepared)
            {
                SingleRow = true;
                Paging.Limit = 1;
                PrepareCommand(false, cancellationToken);
            }

            return _dataProvider.FirstAsync(this, @params, cancellationToken);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public TResult? FirstOrDefault(params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 1 || !_isPrepared)
            {
                SingleRow = true;
                Paging.Limit = 1;
                PrepareCommand(false, CancellationToken.None);
            }

            return _dataProvider.FirstOrDefault(this, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public Task<TResult?> FirstOrDefaultAsync(params object[] @params) => FirstOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 1 || !_isPrepared)
            {
                SingleRow = true;
                Paging.Limit = 1;
                PrepareCommand(false, cancellationToken);
            }

            return _dataProvider.FirstOrDefaultAsync(this, @params, cancellationToken);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public TResult Single(params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 2 || !_isPrepared)
            {
                Paging.Limit = 2;
                PrepareCommand(false, CancellationToken.None);
            }

            return _dataProvider.Single(this, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public Task<TResult> SingleAsync(params object[] @params) => SingleAsync(CancellationToken.None, @params);
    public Task<TResult> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 2 || !_isPrepared)
            {
                Paging.Limit = 2;
                PrepareCommand(false, cancellationToken);
            }

            return _dataProvider.SingleAsync(this, @params, cancellationToken);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public TResult? SingleOrDefault(params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 2 || !_isPrepared)
            {
                Paging.Limit = 2;
                PrepareCommand(false, CancellationToken.None);
            }

            return _dataProvider.SingleOrDefault(this, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    public Task<TResult?> SingleOrDefaultAsync(params object[] @params) => SingleOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        int oldLim = Paging.Limit;
        try
        {
            if (Paging.Limit != 2 || !_isPrepared)
            {
                Paging.Limit = 2;
                PrepareCommand(false, cancellationToken);
            }

            return _dataProvider.SingleOrDefaultAsync(this, @params, cancellationToken);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    protected override QueryCommand CreateSelf()
    {
        return new QueryCommand<TResult>(_dataProvider, _exp, _condition, Joins, Paging, _sorting);
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