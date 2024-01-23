using Microsoft.Extensions.Logging;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace nextorm.core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3897:Classes that provide \"Equals(<T>)\" should implement \"IEquatable<T>\"", Justification = "<Pending>")]
public class QueryCommand : /*IPayloadManager,*/ IQueryContext, ICloneable
{
    private List<QueryCommand>? _referencedQueries;
    private readonly JoinExpression[]? _joins;
    protected SelectExpression[]? _selectList;
    private object _customData;
    // private int _columnsHash;
    // private int _joinHash;
    // private int _sortingHash;
    // private int _whereHash;
    protected FromExpression? _from;
    protected IDataContext _dataContext;
    protected readonly LambdaExpression? _exp;
    protected readonly LambdaExpression? _condition;
    protected readonly LambdaExpression? _groupExp;
    protected readonly LambdaExpression? _having;
    protected bool _isPrepared;
    // protected int? _hash;
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
    internal int ResultPlanHash;
    internal Type? ResultType;
    private int _paramIdx;
    public Paging Paging;
    internal Expression? PreparedCondition;
    private QueryPlanEqualityComparer? _queryPlanComparer;
    private ExpressionPlanEqualityComparer? _expressionPlanComparer;
    private SelectExpressionPlanEqualityComparer? _selectExpressionPlanComparer;
    private FromExpressionPlanEqualityComparer? _fromExpressionPlanComparer;
    private JoinExpressionPlanEqualityComparer? _joinExpressionPlanComparer;
    private PreciseExpressionEqualityComparer? _preciseExpressionComparer;
    private SortingExpressionPlanEqualityComparer? _sortingExpressionPlanComparer;
    protected readonly Sorting[]? _sorting;

    //protected ArrayList _params = new();
    public QueryCommand(IDataContext dataProvider, LambdaExpression exp, LambdaExpression? condition, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having)
        : this(dataProvider, exp, null, condition, null, paging, sorting, group, having)
    {
    }
    public QueryCommand(IDataContext dataProvider, Type srcType, LambdaExpression? condition, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having)
        : this(dataProvider, null, srcType, condition, null, paging, sorting, group, having)
    {
    }
    protected QueryCommand(IDataContext dataProvider, LambdaExpression? exp, Type? srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having)
    {
        _dataContext = dataProvider;
        _exp = exp;
        _srcType = srcType;
        _condition = condition;
        //_payloadMgr = payloadMgr;
        _joins = joins;
        Paging = paging;
        _sorting = sorting;
        _groupExp = group;
        _having = having;
    }
    public ILogger? Logger { get; set; }
    public FromExpression? From { get => _from; set => _from = value; }
    public SelectExpression[]? SelectList => _selectList;
    public Type? EntityType => _srcType;
    // public IDataContext DataProvider
    // {
    //     get => _dataProvider;
    //     set
    //     {
    //         ResetPreparation();
    //         _dataProvider = value;
    //     }
    // }    
    public bool IsPrepared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isPrepared;
    }
    //public Expression? Condition => _condition;
    public JoinExpression[]? Joins => _joins;
    public bool Cache
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_dontCache;
        set => _dontCache = !value;
    }
    //public bool CacheList { get; set; }
    internal QueryCommand? FromQuery => From?.Table.AsT1;
    internal bool OneColumn { get; set; }
    internal bool IgnoreColumns { get; set; }
    public IReadOnlyList<QueryCommand> ReferencedQueries => _referencedQueries!;
    public Sorting[]? Sorting => _sorting;

    public object CustomData { get => _customData; set => _customData = value; }
    public IDataContext DataContext { get => _dataContext; set => _dataContext = value; }
    public LambdaExpression? GroupBy { get => _groupExp; }
    public LambdaExpression? Having { get => _having; }
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;
        // _hash = null;

        _dataContext.ResetPreparation(this);
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

        FromExpression? from = _from ?? _dataContext.GetFrom(srcType, this);
        var joinPlanHash = PrepareJoin(prepareMapOnly, cancellationToken);
        var (selectList, columnsPlanHash) = PrepareColumns(prepareMapOnly, srcType, cancellationToken);
        var wherePlanHash = PrepareWhere(prepareMapOnly, cancellationToken);
        var sortingPlanHash = PrepareSorting(prepareMapOnly, cancellationToken);

        _isPrepared = true;
        _selectList = selectList ?? [];
        // _columnsHash = columnsHash;
        _srcType = srcType;
        _from = from;
        // _joinHash = joinHash;
        // _sortingHash = sortingHash;
        // _whereHash = whereHash;

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

    private (SelectExpression[]?, int) PrepareColumns(bool noHash, Type? srcType, CancellationToken cancellationToken)
    {
        //SelectExpressionPlanEqualityComparer? comparer = null;
        var selectList = _selectList;
        var columnsPlanHash = 7;
        if (selectList is null && !IgnoreColumns)
        {
            if (_exp is not null)
            {

                if (_exp.Body is NewExpression ctor)
                {
                    //if (!CacheList || !_dataProvider.SelectListExpessionCache.TryGetValue(ctor, out selectList))
                    //{
                    //var selList = new List<SelectExpression>();
                    var args = ctor.Arguments;
                    var argsCount = args.Count;

                    selectList = new SelectExpression[argsCount];

                    var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext, this, cancellationToken);
                    for (var idx = 0; idx < argsCount; idx++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return (selectList, columnsPlanHash);

                        SelectExpression selExp;
                        var arg = args[idx];
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

                        selExp = new SelectExpression(ctorParam.ParameterType, _dataContext.ExpressionsCache, this)
                        {
                            Index = idx,
                            PropertyName = ctorParam.Name!,
                            Expression = innerQueryVisitor.Visit(arg)
                        };
                        //}
                        // selExp.HashCode = selExp.GetHashCode();
                        selExp.PlanHashCode = GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                        selectList[idx] = selExp;

                        if (!_dontCache && !noHash) unchecked
                            {
                                //columnsHash = columnsHash * 13 + selExp.HashCode;

                                //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                                columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                            }
                    }

                    if (selectList.Length == 0)
                        throw new PrepareException("Select must return new anonymous type with at least one property");

                    // selectList = selList.ToArray();
                    // if (CacheList)
                    //     _dataProvider.SelectListExpessionCache[ctor] = selectList;
                    //}
                    // else if (!_dontCache && !noHash)
                    // {
                    //     for (int i = 0; i < selectList.Count; i++) unchecked
                    //         {
                    //             var selExp = selectList[i];

                    //             columnsHash = columnsHash * 13 + selExp.HashCode;
                    //             //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                    //             columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                    //         }
                    // }
                }
                else if (_exp.Body.Type.IsPrimitive || _exp.Body.Type == typeof(string) || (_exp.Body.Type.IsGenericType && _exp.Body.Type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    // var selList = new List<SelectExpression>();

                    OneColumn = true;
                    var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext, this, cancellationToken);
                    var selectExp = innerQueryVisitor.Visit(_exp);
                    // if (_dataProvider.NeedMapping)
                    // {

                    // }
                    // else
                    //{
                    var selExp = new SelectExpression(_exp.Body.Type, _dataContext.ExpressionsCache, this)
                    {
                        //Index = 0,
                        Expression = selectExp,
                        //ReferencedQueries = innerQueryVisitor.ReferencedQueries
                    };
                    // selExp.HashCode = selExp.GetHashCode();
                    selExp.PlanHashCode = GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                    // selList.Add(selExp);

                    if (!_dontCache && !noHash) unchecked
                        {
                            //columnsHash = columnsHash * 13 + selExp.HashCode;
                            //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                            columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                        }

                    selectList = [selExp];
                }
                else if (_exp.Body is MemberInitExpression init)
                {
                    // var selList = new List<SelectExpression>();
                    var bindings = init.Bindings;
                    var bindingsCount = bindings.Count;

                    selectList = new SelectExpression[bindingsCount];

                    var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext, this, cancellationToken);
                    for (var idx = 0; idx < bindingsCount; idx++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return (selectList, columnsPlanHash);

                        var binding = bindings[idx] as MemberAssignment;

                        var selExp = new SelectExpression((binding.Member as PropertyInfo).PropertyType, _dataContext.ExpressionsCache, this)
                        {
                            Index = idx,
                            PropertyName = binding.Member.Name!,
                            Expression = innerQueryVisitor.Visit(binding.Expression)
                        };
                        //}
                        // selExp.HashCode = selExp.GetHashCode();
                        selExp.PlanHashCode = GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                        // selList.Add(selExp);
                        selectList[idx] = selExp;

                        if (!_dontCache && !noHash) unchecked
                            {
                                //columnsHash = columnsHash * 13 + selExp.HashCode;

                                //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                                columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                            }
                    }

                    if (selectList.Length == 0)
                        throw new PrepareException("Select must return new anonymous type with at least one property");

                    // selectList = selList.ToArray();
                }
            }
            else
            {
                if (srcType is null)
                    throw new PrepareException("Lambda expression or source type must exists");

                if (_dataContext.NeedMapping)
                {
                    if (/*!CacheList || */!_dataContext.SelectListCache.TryGetValue(srcType, out selectList))
                    {
                        // var selList = new List<SelectExpression>();

                        var p = Expression.Parameter(srcType);

                        if (_dataContext.Metadata.TryGetValue(srcType, out var entityMeta))
                        {
                            var props = entityMeta.Properties;
                            var propsCount = props.Count;

                            selectList = new SelectExpression[propsCount];

                            for (int idx = 0; idx < propsCount; idx++)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    return (selectList, columnsPlanHash);

                                var prop = props[idx];

                                var pi = prop.PropertyInfo;

                                Expression exp = Expression.Lambda(Expression.Property(p, pi), p);

                                var selExp = new SelectExpression(pi.PropertyType, _dataContext.ExpressionsCache, this)
                                {
                                    Index = idx,
                                    PropertyName = pi.Name,
                                    Expression = exp,
                                    PropertyInfo = pi
                                };

                                // selExp.HashCode = selExp.GetHashCode();
                                selExp.PlanHashCode = GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                                // selList.Add(selExp);
                                selectList[idx] = selExp;

                                if (!_dontCache && !noHash) unchecked
                                    {
                                        //columnsHash = columnsHash * 13 + selExp.HashCode;
                                        //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                                        columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                                    }
                            }
                        }

                        if (!(selectList?.Length > 0))
                            throw new PrepareException("Select must return new anonymous type");

                        // selectList = selList.ToArray();
                        _dataContext.SelectListCache[srcType] = selectList;
                    }
                    else if (!_dontCache && !noHash)
                    {
                        for (int i = 0; i < selectList.Length; i++) unchecked
                            {
                                var selExp = selectList[i];

                                //columnsHash = columnsHash * 13 + selExp.HashCode;
                                //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                                columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                            }
                    }
                }
            }
            // else
            //     throw new NotImplementedException();
        }

        return (selectList, columnsPlanHash);
    }
    private int PrepareJoin(bool noHash, CancellationToken cancellationToken)
    {
        var joinPlanHash = 7;
        if (_joins is not null)
        {
            if (_from is not null && string.IsNullOrEmpty(_from.TableAlias)) _from.TableAlias = "t1";

            for (var idx = 0; idx < _joins.Length; idx++)
            {
                var join = _joins[idx];

                if (!(join.Query?._isPrepared ?? true))
                    join.Query!.PrepareCommand(noHash, cancellationToken);

                if (!_dontCache && !noHash) unchecked
                    {
                        //comparer ??= new JoinExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                        //joinHash = joinHash * 13 + join.GetHashCode();
                        joinPlanHash = joinPlanHash * 13 + GetJoinExpressionPlanEqualityComparer().GetHashCode(join);
                    }
            }
        }

        return joinPlanHash;
    }
    private int PrepareSorting(bool noHash, CancellationToken cancellationToken)
    {
        var sortingPlanHash = 7;
        if (_sorting is not null)
        {
            var sortingSpan = _sorting.AsSpan();
            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext, this, cancellationToken);

            for (var i = 0; i < _sorting.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                ref var sort = ref sortingSpan[i];

                if (sort.SortExpression is not null)
                {
                    sort.PreparedExpression = innerQueryVisitor.Visit(sort.SortExpression);

                    if (!_dontCache && !noHash) unchecked
                        {
                            // sortingHash = sortingHash * 13 + (int)sort.Direction;
                            // //comp ??= new PreciseExpressionEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                            // sortingHash = sortingHash * 13 + GetPreciseExpressionEqualityComparer().GetHashCode(sort.PreparedExpression);

                            //compPlan ??= new ExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                            sortingPlanHash = sortingPlanHash * 13 + GetSortingExpressionPlanEqualityComparer().GetHashCodeRef(in sort);
                        }
                }
                else if (sort.ColumnIndex.HasValue)
                {
                    if (!_dontCache && !noHash) unchecked
                        {
                            // sortingHash = sortingHash * 13 + (int)sort.Direction;
                            // //comp ??= new PreciseExpressionEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                            // sortingHash = sortingHash * 13 + GetPreciseExpressionEqualityComparer().GetHashCode(sort.PreparedExpression);

                            //compPlan ??= new ExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                            sortingPlanHash = sortingPlanHash * 13 + sort.ColumnIndex.Value;
                        }
                }
                else
                    throw new InvalidOperationException("Expression on column index numst be specified");
            }
        }

        return sortingPlanHash;
    }
    private int PrepareWhere(bool noHash, CancellationToken cancellationToken)
    {
        var wherePlanHash = 7;
        if (_condition is not null)
        {
            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext, this, cancellationToken);
            PreparedCondition = innerQueryVisitor.Visit(_condition);

            if (!_dontCache && !noHash) unchecked
                {
                    //comp ??= new PreciseExpressionEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                    //whereHash = whereHash * 13 + GetPreciseExpressionEqualityComparer().GetHashCode(PreparedCondition);

                    //compPlan ??= new ExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this, _dataProvider.Logger);
                    wherePlanHash = wherePlanHash * 13 + GetExpressionPlanEqualityComparer().GetHashCode(PreparedCondition);
                }
        }

        return wherePlanHash;
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
        throw new NotSupportedException();

        // if (_hash.HasValue)
        //     return _hash.Value;

        // if (!_isPrepared)
        //     PrepareCommand(false, CancellationToken.None);

        // unchecked
        // {
        //     HashCode hash = new();
        //     if (_from is not null)
        //         hash.Add(_from);

        //     if (_srcType is not null)
        //         hash.Add(_srcType);

        //     hash.Add(_whereHash);

        //     hash.Add(_columnsHash);

        //     hash.Add(_joinHash);

        //     hash.Add(_sortingHash);

        //     hash.Add(Paging.Limit);
        //     hash.Add(Paging.Offset);

        //     _hash = hash.ToHashCode();

        //     //Console.WriteLine(_hash);

        //     return _hash.Value;
        // }
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryCommand);
    }
    public bool Equals(QueryCommand? cmd)
    {
        throw new NotSupportedException();
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
        if (idx <= 1) return null;
        return Joins[idx - 2].Query;
    }
    public string GetParamName()
    {
        return string.Format("p{0}", _paramIdx++);
    }
    public void ReplaceCommand(QueryCommand cmd, int idx)
    {
        if (_dataContext != cmd._dataContext)
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
        // dst._columnsHash = _columnsHash;
        // dst._joinHash = _joinHash;
        // dst._sortingHash = _sortingHash;
        dst._from = _from;
        dst._isPrepared = _isPrepared;
        dst._srcType = _srcType;
        dst._dontCache = _dontCache;
        // dst._hash = _hash;
        dst.ColumnsPlanHash = ColumnsPlanHash;
        dst.JoinPlanHash = JoinPlanHash;
        dst.WherePlanHash = WherePlanHash;
        dst.SortingPlanHash = SortingPlanHash;
        dst.PreparedCondition = PreparedCondition;
        dst.ResultPlanHash = ResultPlanHash;
        dst.ResultType = ResultType;
        dst.Paging = Paging;
        dst._queryPlanComparer = _queryPlanComparer;
        dst._fromExpressionPlanComparer = _fromExpressionPlanComparer;
        dst._expressionPlanComparer = _expressionPlanComparer;
        dst._selectExpressionPlanComparer = _selectExpressionPlanComparer;
        dst._joinExpressionPlanComparer = _joinExpressionPlanComparer;
        dst._sortingExpressionPlanComparer = _sortingExpressionPlanComparer;
    }

    protected virtual QueryCommand CreateSelf()
    {
        return new QueryCommand(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting, _groupExp, _having);
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
    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => _queryPlanComparer ??= new QueryPlanEqualityComparer(this);
    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => _expressionPlanComparer ??= new ExpressionPlanEqualityComparer(this);
    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => _selectExpressionPlanComparer ??= new SelectExpressionPlanEqualityComparer(this);
    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => _fromExpressionPlanComparer ??= new FromExpressionPlanEqualityComparer(this);
    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => _joinExpressionPlanComparer ??= new JoinExpressionPlanEqualityComparer(this);
    public PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer() => _preciseExpressionComparer ??= new PreciseExpressionEqualityComparer(_dataContext.ExpressionsCache, this);
    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => _sortingExpressionPlanComparer ??= new SortingExpressionPlanEqualityComparer(this);
}

public class QueryCommand<TResult> : QueryCommand//, IAsyncEnumerable<TResult>
{
    public QueryCommand(IDataContext dataProvider, LambdaExpression exp, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having)
        : this(dataProvider, exp, null, condition, joins, paging, sorting, group, having)
    {
    }
    public QueryCommand(IDataContext dataProvider, Type srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having)
        : this(dataProvider, null, srcType, condition, joins, paging, sorting, group, having)
    {
    }
    protected QueryCommand(IDataContext dataProvider, LambdaExpression? exp, Type? srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having)
        : base(dataProvider, exp, srcType, condition, joins, paging, sorting, group, having)
    {

    }
    public override void PrepareCommand(bool prepareMapOnly, CancellationToken cancellationToken)
    {
        base.PrepareCommand(prepareMapOnly, cancellationToken);
        if (!prepareMapOnly)
        {
            ResultPlanHash = typeof(TResult).GetHashCode();
        }
        ResultType = typeof(TResult);
    }
    public bool SingleRow { get; set; }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IPreparedQueryCommand<TResult> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
    {
        return _dataContext.GetPreparedQueryCommand(this, !nonStreamUsing, false, cancellationToken);
    }
    // public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    // {
    //     var preparedCommand = _dataContext.GetPreparedQueryCommand(this, true, true, cancellationToken);

    //     return _dataContext.CreateAsyncEnumerator<TResult>(preparedCommand, null, cancellationToken);
    // }
    public IAsyncEnumerable<TResult> Pipeline(params object[] @params) => Pipeline(CancellationToken.None, @params);
#pragma warning disable CS8425 // Async-iterator member has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed
    public async IAsyncEnumerable<TResult> Pipeline(CancellationToken cancellationToken, params object[] @params)
#pragma warning restore CS8425 // Async-iterator member has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed
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
                foreach (var item in ToEnumerable(@params))
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(params object[] @params) => _dataContext.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(CancellationToken cancellationToken, params object[] @params) => _dataContext.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, cancellationToken), cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TResult> ToEnumerable(params object[] @params) => _dataContext.GetEnumerable(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerable<TResult>> ToEnumerableAsync(params object[] @params) => _dataContext.GetEnumerableAsync<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerable<TResult>> ToEnumerableAsync(CancellationToken cancellationToken, params object[] @params) => _dataContext.GetEnumerableAsync<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, cancellationToken), cancellationToken, @params);
    public List<TResult> ToList(params object[] @params)
    {
        var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None);

        return _dataContext.ToList<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(params object[] @params) => _dataContext.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None), @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params) => _dataContext.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken), @params, cancellationToken);
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

                queryCommand = Entity<TResult>.GetAnyCommand(_dataContext, this);
            }

            var preparedCommand = _dataContext.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
            return _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true);
        }
        finally
        {
            IgnoreColumns = oldIgnoreColumns;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

                queryCommand = Entity<TResult>.GetAnyCommand(_dataContext, this);
            }

            var preparedCommand = _dataContext.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
            return await _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IgnoreColumns = oldIgnoreColumns;
        }
    }
    public TResult? ExecuteScalar(params object[] @params)
    {
        var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false);
    }
    public Task<TResult?> ExecuteScalarAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false, cancellationToken);
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
            return _dataContext.First<TResult>(preparedCommand, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken);
            return _dataContext.FirstAsync<TResult>(preparedCommand, @params, cancellationToken);
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
            return _dataContext.FirstOrDefault<TResult>(preparedCommand, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken);
            return _dataContext.FirstOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
            return _dataContext.Single<TResult>(preparedCommand, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken);
            return _dataContext.SingleAsync<TResult>(preparedCommand, @params, cancellationToken);
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
            return _dataContext.SingleOrDefault<TResult>(preparedCommand, @params);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var preparedCommand = _dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken);
            return _dataContext.SingleOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    protected override QueryCommand CreateSelf()
    {
        return new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting, _groupExp, _having);
    }
    public QueryCommand<TResult> OrderBy(int columnIndex, OrderDirection direction)
    {
        if (columnIndex < 1) throw new ArgumentException("Column index must be greater than zero", nameof(columnIndex));
        var cmd = new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting is null
            ? [new Sorting(columnIndex) { Direction = direction }]
            : [.. _sorting, new Sorting(columnIndex) { Direction = direction }], _groupExp, _having);
        CopyTo(cmd);
        return cmd;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderBy(int columnIndex) => OrderBy(columnIndex, OrderDirection.Asc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderByDescending(int columnIndex) => OrderBy(columnIndex, OrderDirection.Desc);
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
}
