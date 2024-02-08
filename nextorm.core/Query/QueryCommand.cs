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
    private QueryCommand _union;
    private UnionType _unionType;
    private List<QueryCommand>? _referencedQueries;
    private List<Expression>? _outerRefs;
    private readonly JoinExpression[]? _joins;
    protected SelectExpression[]? _selectList;
    private object? _customData;
    // private int _columnsHash;
    // private int _joinHash;
    // private int _sortingHash;
    // private int _whereHash;
    protected FromExpression? _from;
    protected IDataContext? _dataContext;
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
    internal int GroupingPlanHash;
    internal int ResultPlanHash;
    internal int FromPlanHash;
    internal int UnionPlanHash;
    internal int ReferencedQueriesPlanHash;
    internal Type? ResultType;
    public Paging Paging;
    internal Expression? PreparedCondition;
    private QueryPlanEqualityComparer? _queryPlanComparer;
    private ExpressionPlanEqualityComparer? _expressionPlanComparer;
    private SelectExpressionPlanEqualityComparer? _selectExpressionPlanComparer;
    private FromExpressionPlanEqualityComparer? _fromExpressionPlanComparer;
    private JoinExpressionPlanEqualityComparer? _joinExpressionPlanComparer;
    // private PreciseExpressionEqualityComparer? _preciseExpressionComparer;
    private SortingExpressionPlanEqualityComparer? _sortingExpressionPlanComparer;
    private SelectExpression[]? _groupingList;
    protected readonly Sorting[]? _sorting;

    //protected ArrayList _params = new();
    public QueryCommand(IDataContext? dataProvider, LambdaExpression exp, LambdaExpression? condition, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : this(dataProvider, exp, null, condition, null, paging, sorting, group, having, logger)
    {
    }
    public QueryCommand(IDataContext? dataProvider, Type srcType, LambdaExpression? condition, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : this(dataProvider, null, srcType, condition, null, paging, sorting, group, having, logger)
    {
    }
    protected QueryCommand(IDataContext? dataProvider, LambdaExpression? exp, Type? srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
    {
        _dataContext = dataProvider;
        _exp = exp;
        _srcType = srcType;
        _condition = condition;
        _joins = joins;
        Paging = paging;
        _sorting = sorting;
        _groupExp = group;
        _having = having;
        Logger = logger;
    }
    public ILogger? Logger { get; }
    public FromExpression? From { get => _from; set => _from = value; }
    public SelectExpression[]? SelectList => _selectList;
    public SelectExpression[]? GroupingList => _groupingList;
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
    public Expression? Condition => _condition;
    public JoinExpression[]? Joins => _joins;
    public bool Cache
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_dontCache;
        set => _dontCache = !value;
    }
    //public bool CacheList { get; set; }
    internal QueryCommand? FromQuery => From?.SubQuery;
    internal bool OneColumn { get; set; }
    internal bool IgnoreColumns { get; set; }
    public IReadOnlyList<QueryCommand> ReferencedQueries => _referencedQueries!;
    public Sorting[]? Sorting => _sorting;
    public object? CustomData { get => _customData; set => _customData = value; }
    public IDataContext? DataContext { get => _dataContext; set => _dataContext = value; }
    public LambdaExpression? GroupBy { get => _groupExp; }
    public LambdaExpression? Having { get => _having; }
    public QueryCommand? UnionQuery { get => _union; }
    public UnionType UnionType { get => _unionType; }
    public IReadOnlyList<Expression>? OuterReferences => _outerRefs;

    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _groupingList = null;
        _from = null;
        PreparedCondition = null;
        // _hash = null;

        _dataContext?.ResetPreparation(this);
    }
    public void PrepareCommand(CancellationToken cancellationToken) => PrepareCommand(false, cancellationToken);
    public virtual void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
    {
        if (_dataContext is null) throw new InvalidOperationException("Cannot prepare command in cache");
#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Preparing command");
#endif
        OneColumn = false;

        var srcType = _srcType;
        if (srcType is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            srcType = _exp.Parameters[0].Type;
        }

        FromExpression? from = _from ?? _dataContext.GetFrom(srcType, this);
        QueryCommand.PrepareFrom(from, dontCalculateHash, cancellationToken);
        var joinPlanHash = PrepareJoin(dontCalculateHash, cancellationToken);
        var (selectList, columnsPlanHash) = PrepareColumns(dontCalculateHash, srcType, cancellationToken);
        var wherePlanHash = PrepareWhere(dontCalculateHash, cancellationToken);

        // if (from is not null && string.IsNullOrEmpty(from.TableAlias) && (shouldAliasFrom || _outerRefs?.Count > 0))
        //     from.TableAlias = "t1";

        var (groupingList, groupingPlanHash) = PrepareGrouping(dontCalculateHash, cancellationToken);
        var sortingPlanHash = PrepareSorting(dontCalculateHash, cancellationToken);

        _union?.PrepareCommand(dontCalculateHash, cancellationToken);

        _isPrepared = true;
        _selectList = selectList ?? [];
        _groupingList = groupingList ?? [];
        _srcType = srcType;
        _from = from;
        // _joinHash = joinHash;
        // _sortingHash = sortingHash;
        // _whereHash = whereHash;

        ColumnsPlanHash = columnsPlanHash == 7 ? 0 : columnsPlanHash;
        JoinPlanHash = joinPlanHash == 7 ? 0 : joinPlanHash;
        SortingPlanHash = sortingPlanHash == 7 ? 0 : sortingPlanHash;
        WherePlanHash = wherePlanHash == 7 ? 0 : wherePlanHash;
        GroupingPlanHash = groupingPlanHash == 7 ? 0 : groupingPlanHash;
        FromPlanHash = dontCalculateHash ? 0 : GetFromExpressionPlanEqualityComparer().GetHashCode(from);

        if (!dontCalculateHash && _union is not null)
        {
            unchecked
            {
                var h = 7 * 13 + ((int)_unionType);
                h = h * 13 + GetQueryPlanEqualityComparer().GetHashCode();
                UnionPlanHash = h;
            }
        }

        if (!dontCalculateHash)
        {
            if (_referencedQueries?.Count == 1)
            {
                ReferencedQueriesPlanHash = GetQueryPlanEqualityComparer().GetHashCode();
            }
            else if (_referencedQueries?.Count > 1)
            {
                HashCode hash = new();
                unchecked
                {
                    for (var (i, cnt) = (0, _referencedQueries.Count); i < cnt; i++)
                    {
                        var item = _referencedQueries[i];
                        hash.Add(item, GetQueryPlanEqualityComparer());
                    }
                    ReferencedQueriesPlanHash = hash.ToHashCode();
                }
            }
        }
    }
    private static void PrepareFrom(FromExpression? from, bool dontCalculateHash, CancellationToken cancellationToken)
    {
        if (from?.SubQuery is not null && !from.SubQuery._isPrepared)
            from.SubQuery.PrepareCommand(dontCalculateHash, cancellationToken);
    }
    private (SelectExpression[]?, int) PrepareColumns(bool noHash, Type? srcType, CancellationToken cancellationToken)
    {
        //SelectExpressionPlanEqualityComparer? comparer = null;
        var selectList = _selectList;
        int columnsPlanHash = 7;
        if (selectList is null && !IgnoreColumns)
        {
            if (_exp is not null)
            {
                if (_exp.Body is NewExpression ctor)
                {
                    //if (!CacheList || !_dataProvider.SelectListExpressionCache.TryGetValue(ctor, out selectList))
                    //{
                    //var selList = new List<SelectExpression>();
                    var args = ctor.Arguments;
                    var argsCount = args.Count;

                    selectList = new SelectExpression[argsCount];

                    var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext!, this, cancellationToken);
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

                        selExp = new SelectExpression(ctorParam.ParameterType)
                        {
                            Index = idx,
                            PropertyName = ctorParam.Name!,
                            Expression = innerQueryVisitor.Visit(arg)
                        };
                        //}
                        // selExp.HashCode = selExp.GetHashCode();
                        if (!_dontCache && !noHash)
                            selExp.PlanHashCode = GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                        selectList[idx] = selExp;

                        if (!_dontCache && !noHash) unchecked
                            {
                                //columnsHash = columnsHash * 13 + selExp.HashCode;

                                //comparer ??= new SelectExpressionPlanEqualityComparer(_dataProvider.ExpressionsCache, this);
                                columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                            }
                    }

                    // if (selectList.Length == 0)
                    //     throw new PrepareException("Select must return new anonymous type with at least one property");

                    // selectList = selList.ToArray();
                    // if (CacheList)
                    //     _dataProvider.SelectListExpressionCache[ctor] = selectList;
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
                    var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext!, this, cancellationToken);
                    var selectExp = innerQueryVisitor.Visit(_exp);
                    // if (_dataProvider.NeedMapping)
                    // {

                    // }
                    // else
                    //{
                    var selExp = new SelectExpression(_exp.Body.Type)
                    {
                        //Index = 0,
                        Expression = selectExp,
                        //ReferencedQueries = innerQueryVisitor.ReferencedQueries
                    };
                    if (!_dontCache && !noHash)
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

                    var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext!, this, cancellationToken);
                    for (var idx = 0; idx < bindingsCount; idx++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return (selectList, columnsPlanHash);

                        var binding = bindings[idx] as MemberAssignment;

                        var selExp = new SelectExpression((binding.Member as PropertyInfo).PropertyType)
                        {
                            Index = idx,
                            PropertyName = binding.Member.Name!,
                            Expression = innerQueryVisitor.Visit(binding.Expression)
                        };
                        //}
                        if (!_dontCache && !noHash)
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

                    // if (selectList.Length == 0)
                    //     throw new PrepareException("Select must return new anonymous type with at least one property");

                    // selectList = selList.ToArray();
                }
            }
            else
            {
                if (srcType is null)
                    throw new PrepareException("Lambda expression or source type must exists");

                if (_dataContext!.NeedMapping)
                {
                    if (/*!CacheList || */!DataContextCache.SelectListCache.TryGetValue(srcType, out selectList))
                    {
                        // var selList = new List<SelectExpression>();

                        var p = Expression.Parameter(srcType);

                        if (DataContextCache.Metadata.TryGetValue(srcType, out var entityMeta))
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

                                var selExp = new SelectExpression(pi.PropertyType)
                                {
                                    Index = idx,
                                    PropertyName = pi.Name,
                                    Expression = exp,
                                    PropertyInfo = pi
                                };

                                if (!_dontCache && !noHash)
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
                        DataContextCache.SelectListCache[srcType] = selectList;
                    }
                    else if (!_dontCache && !noHash)
                    {
                        for (var (i, cnt) = (0, selectList.Length); i < cnt; i++) unchecked
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
        int joinPlanHash = 7;
        if (_joins is not null)
        {
            // if (_from is not null && string.IsNullOrEmpty(_from.TableAlias)) _from.TableAlias = "t1";

            for (var (idx, cnt) = (0, _joins.Length); idx < cnt; idx++)
            {
                var join = _joins[idx];

                PrepareFrom(join.From, noHash, cancellationToken);
                // if (!(join.Query?._isPrepared ?? true))
                //     join.Query!.PrepareCommand(noHash, cancellationToken);

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
        int sortingPlanHash = 7;
        if (_sorting is not null)
        {
            var sortingSpan = _sorting.AsSpan();
            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext!, this, cancellationToken);

            for (var (i, cnt) = (0, _sorting.Length); i < cnt; i++)
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
                    throw new InvalidOperationException("Expression on column index must be specified");
            }
        }

        return sortingPlanHash;
    }
    private int PrepareWhere(bool noHash, CancellationToken cancellationToken)
    {
        int wherePlanHash = 7;
        if (_condition is not null)
        {
            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(_dataContext!, this, cancellationToken);
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
    private (SelectExpression[]?, int) PrepareGrouping(bool noHash, CancellationToken cancellationToken)
    {
        var groupingList = _groupingList;
        int groupingPlanHash = 7;
        if (_groupExp is not null && groupingList is null)
        {
            if (_groupExp.Body is NewExpression ctor)
            {
                //if (!CacheList || !_dataProvider.SelectListExpressionCache.TryGetValue(ctor, out selectList))
                //{
                //var selList = new List<SelectExpression>();
                var args = ctor.Arguments;
                var argsCount = args.Count;

                groupingList = new SelectExpression[argsCount];

                for (var idx = 0; idx < argsCount; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (groupingList, groupingPlanHash);

                    var arg = args[idx];
                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    var selExp = new SelectExpression(ctorParam.ParameterType)
                    {
                        Index = idx,
                        PropertyName = ctorParam.Name!,
                        Expression = arg
                    };
                    //}
                    if (!noHash)
                        selExp.PlanHashCode = GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                    groupingList[idx] = selExp;

                    if (!_dontCache && !noHash) unchecked
                        {
                            groupingPlanHash = groupingPlanHash * 13 + selExp.PlanHashCode;
                        }
                }
            }
            else
                throw new InvalidOperationException("Only new expression is supported");
        }

        if (_having is not null)
        {
            if (!_dontCache && !noHash) unchecked
                {
                    groupingPlanHash = groupingPlanHash * 13 + GetExpressionPlanEqualityComparer().GetHashCode(_having);
                }
        }

        return (groupingList, groupingPlanHash);
    }
    // public QueryCommand? FindSourceFromAlias(string? alias)
    // {
    //     if (string.IsNullOrEmpty(alias))
    //     {
    //         if (_from?.SubQuery is not null)
    //             return _from.SubQuery;

    //         return null;
    //     }

    //     if (!(_joins?.Length > 0)) throw new InvalidOperationException("There is no joins");

    //     var idx = int.Parse(alias[1..]);
    //     if (idx <= 1) return null;
    //     return _joins[idx - 2].Query;
    // }
    internal void ReplaceCommand(QueryCommand cmd, int idx)
    {
#if DEBUG
        if (_dataContext != cmd._dataContext)
            throw new InvalidOperationException("Different data context");

        if (_referencedQueries is null) throw new InvalidOperationException("Referenced queries must be initialized");
#endif

        _referencedQueries[idx] = cmd;
        // var comparer = GetSelectExpressionPlanEqualityComparer();
        // _selectList[0].PlanHashCode = comparer.GetHashCode(_selectList[0]);
        // ColumnsPlanHash = 7 * 13 + _selectList[0].PlanHashCode;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0028:Simplify collection initialization", Justification = "<Pending>")]
    int IQueryProvider.AddCommand(QueryCommand cmd)
    {
#if DEBUG
        if (_isPrepared) throw new InvalidOperationException("QueryCommand prepared");

        if (!cmd.IsPrepared) throw new InvalidOperationException("QueryCommand must be prepared");
#endif
        // if (_dataProvider != cmd._dataProvider)
        //     throw new InvalidOperationException("Different data context");

        _referencedQueries ??= new List<QueryCommand>();
        var idx = _referencedQueries.Count;
        _referencedQueries.Add(cmd);
        return idx;
    }

    protected virtual void CopyTo(QueryCommand dst, bool copyAll)
    {
        dst._selectList = _selectList;
        dst._groupingList = _groupingList;
        // dst._joinHash = _joinHash;
        // dst._sortingHash = _sortingHash;
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
        dst.GroupingPlanHash = GroupingPlanHash;

        dst.ResultType = ResultType;
        dst.Paging = Paging;

        dst._queryPlanComparer = _queryPlanComparer;
        dst._fromExpressionPlanComparer = _fromExpressionPlanComparer;
        dst._expressionPlanComparer = _expressionPlanComparer;
        dst._selectExpressionPlanComparer = _selectExpressionPlanComparer;
        dst._joinExpressionPlanComparer = _joinExpressionPlanComparer;
        dst._sortingExpressionPlanComparer = _sortingExpressionPlanComparer;
        dst._unionType = _unionType;

        if (copyAll)
        {
            dst._customData = _customData;
            dst._referencedQueries = _referencedQueries;
            // dst._paramIdx = _paramIdx;
            dst._union = _union;
            dst._from = _from;
        }
        else
        {
            if (_from is not null)
                dst._from = _from.CloneForCache();
            if (_union is not null)
                dst._union = _union.CloneForCache();

            if (_referencedQueries?.Count > 0)
            {
                dst._referencedQueries = _referencedQueries.Select(it => it.CloneForCache()).ToList();
            }
        }
    }

    protected virtual QueryCommand CreateSelf()
    {
        return new QueryCommand(_dataContext, _exp, _srcType, _condition, _joins, Paging, _sorting, _groupExp, _having, Logger);
    }
    protected virtual QueryCommand CreateSelfForClone()
    {
        return new QueryCommand(null, null, _srcType, null, CloneForCache(_joins), Paging, _sorting, null, _having, Logger);
    }

    protected static JoinExpression[]? CloneForCache(JoinExpression[]? joins)
    {
        if (joins is null) return null;

        var cnt = joins.Length;
        var newJoins = new JoinExpression[cnt];
        for (var idx = 0; idx < cnt; idx++)
        {
            newJoins[idx] = joins[idx].CloneForCache();
        }

        return newJoins;
    }

    public QueryCommand CloneForCache()
    {
        var cmd = CreateSelfForClone();
        CopyTo(cmd, false);
        return cmd;
    }
    public QueryCommand Clone()
    {
        return (QueryCommand)(this as ICloneable).Clone();
    }
    object ICloneable.Clone()
    {
        var cmd = CreateSelf();
        CopyTo(cmd, true);
        return cmd;
    }
    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => _queryPlanComparer ??= new QueryPlanEqualityComparer(this);
    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => _expressionPlanComparer ??= new ExpressionPlanEqualityComparer(this);
    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => _selectExpressionPlanComparer ??= new SelectExpressionPlanEqualityComparer(this);
    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => _fromExpressionPlanComparer ??= new FromExpressionPlanEqualityComparer(this);
    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => _joinExpressionPlanComparer ??= new JoinExpressionPlanEqualityComparer(this);
    // public PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer() => _preciseExpressionComparer ??= new PreciseExpressionEqualityComparer(this);
    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => _sortingExpressionPlanComparer ??= new SortingExpressionPlanEqualityComparer(this);

    protected void SetUnion(QueryCommand queryCommand, UnionType unionType)
    {
        if (_union is null)
        {
            _union = queryCommand;
            _unionType = unionType;
        }
        else
        {
            _union.SetUnion(queryCommand, unionType);
        }
    }

    public int AddOuterReference(Expression node)
    {
        var outerRefs = _outerRefs ??= [];

        var idx = outerRefs.Count;

        outerRefs.Add(node);

        return idx;
    }
}

public class QueryCommand<TResult> : QueryCommand//, IAsyncEnumerable<TResult>
{
    public QueryCommand(IDataContext? dataProvider, LambdaExpression exp, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : this(dataProvider, exp, null, condition, joins, paging, sorting, group, having, logger)
    {
    }
    public QueryCommand(IDataContext? dataProvider, Type srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : this(dataProvider, null, srcType, condition, joins, paging, sorting, group, having, logger)
    {
    }
    protected QueryCommand(IDataContext? dataProvider, LambdaExpression? exp, Type? srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : base(dataProvider, exp, srcType, condition, joins, paging, sorting, group, having, logger)
    {

    }
    public override void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
    {
        base.PrepareCommand(dontCalculateHash, cancellationToken);
        if (!dontCalculateHash)
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
        return _dataContext!.GetPreparedQueryCommand(this, !nonStreamUsing, false, cancellationToken);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(params object[]? @params) => CreateAsyncEnumerator(CancellationToken.None, @params);
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(CancellationToken cancellationToken, params object[]? @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        return _dataContext.CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(params object[]? @params) => CreateEnumeratorAsync(CancellationToken.None, @params);
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(CancellationToken cancellationToken, params object[]? @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        return _dataContext.CreateEnumeratorAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
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
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(params object[] @params) => _dataContext!.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(CancellationToken cancellationToken, params object[] @params) => _dataContext!.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, cancellationToken), cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TResult> ToEnumerable(params object[] @params) => _dataContext!.GetEnumerable(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), @params);
    public List<TResult> ToList(params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);

        return _dataContext.ToList<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(params object[] @params) => _dataContext!.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None), @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params) => _dataContext!.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken), @params, cancellationToken);
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

                queryCommand = Entity<TResult>.GetAnyCommand(_dataContext!, this);
            }

            var preparedCommand = _dataContext!.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
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

                queryCommand = Entity<TResult>.GetAnyCommand(_dataContext!, this);
            }

            var preparedCommand = _dataContext!.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
            return await _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IgnoreColumns = oldIgnoreColumns;
        }
    }
    public TResult? ExecuteScalar(params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false);
    }
    public Task<TResult?> ExecuteScalarAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
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
            var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
            return _dataContext.SingleOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
        }
        finally
        {
            Paging.Limit = oldLim;
        }
    }
    protected override QueryCommand CreateSelf()
    {
        return new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting, _groupExp, _having, Logger);
    }
    protected override QueryCommand CreateSelfForClone()
    {
        return new QueryCommand<TResult>(null, null, _srcType, null, CloneForCache(Joins), Paging, _sorting, null, _having, Logger);
    }
    public QueryCommand<TResult> OrderBy(int columnIndex, OrderDirection direction)
    {
        if (columnIndex < 1) throw new ArgumentException("Column index must be greater than zero", nameof(columnIndex));

        var cmd = new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting is null
            ? [new Sorting(columnIndex) { Direction = direction }]
            : [.. _sorting, new Sorting(columnIndex) { Direction = direction }], _groupExp, _having, Logger);

        CopyTo(cmd, true);
        cmd.ResetPreparation();
        return cmd;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderBy(int columnIndex) => OrderBy(columnIndex, OrderDirection.Asc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderByDescending(int columnIndex) => OrderBy(columnIndex, OrderDirection.Desc);
    public QueryCommand<TResult> Union<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetUnion(queryCommand, UnionType.Distinct);
        return cmd;
    }
    public QueryCommand<TResult> UnionAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetUnion(queryCommand, UnionType.All);
        return cmd;
    }
}
