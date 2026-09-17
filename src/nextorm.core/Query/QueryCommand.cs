using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public partial class QueryCommand : IQueryProvider, ICloneable
{

    private QueryCommand? _union;
    private UnionType _unionType;
    private IReadOnlyList<CteDefinition>? _ctes;
    private List<QueryCommand>? _referencedQueries;
    private List<Expression>? _outerRefs;
    private readonly JoinExpression[]? _joins;
    protected SelectExpression[]? _selectList;
    private object? _customData;
    protected FromExpression? _from;
    protected IDataContext? _dataContext;
    protected readonly LambdaExpression? _exp;
    protected readonly LambdaExpression? _condition;
    protected readonly LambdaExpression? _groupExp;
    protected readonly LambdaExpression? _having;
    protected bool _isPrepared;
    protected Type? _srcType;
    private bool _dontCache;
    internal int ColumnsPlanHash;
    internal int JoinPlanHash;
    internal int SortingPlanHash;
    internal int WherePlanHash;
    internal int GroupingPlanHash;
    internal int ResultPlanHash;
    internal int FromPlanHash;
    internal int UnionPlanHash;
    internal int ReferencedQueriesPlanHash;
    internal int CtesPlanHash;
    internal Type? ResultType;
    public Paging Paging;
    internal Expression? PreparedCondition;
    /// <summary>
    /// Hash of the value-list (<c>in</c>/<c>Contains</c>) shapes found in <see cref="PreparedCondition"/>.
    /// A captured collection's member access contributes nothing to the expression plan hash, so this
    /// folds the element count / null-presence into the plan key. Zero when there are no such nodes.
    /// </summary>
    internal int InValuesShapeHash;
    /// <summary>
    /// The condition plan hash before the value-list shape is folded in, so an already-prepared command
    /// can refresh the shape (and therefore the key) when its captured collection changed.
    /// </summary>
    private int _whereBasePlanHash;
    /// <summary>
    /// Whether <see cref="PreparedCondition"/> contains a top-level value-list node whose shape must be
    /// re-checked on every execution of an already-prepared command.
    /// </summary>
    internal bool HasTopLevelInValues;
    /// <summary>
    /// The value lists evaluated while preparing the condition, keyed by the collection expression, so
    /// the SQL and parameter passes reuse one evaluation of a captured collection. Only populated when
    /// the command participates in the plan cache.
    /// </summary>
    internal Dictionary<Expression, InValuesPartition>? InValuesPartitions;
    private QueryPlanEqualityComparer? _queryPlanComparer;
    private ExpressionPlanEqualityComparer? _expressionPlanComparer;
    private SelectExpressionPlanEqualityComparer? _selectExpressionPlanComparer;
    private FromExpressionPlanEqualityComparer? _fromExpressionPlanComparer;
    private JoinExpressionPlanEqualityComparer? _joinExpressionPlanComparer;
    private SortingExpressionPlanEqualityComparer? _sortingExpressionPlanComparer;
    private SelectExpression[]? _groupingList;
    protected readonly Sorting[]? _sorting;

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
    internal QueryCommand? FromQuery => From?.SubQuery;
    internal bool OneColumn { get; set; }
    public bool SingleRow { get; set; }
    /// <summary>
    /// Whether the command was marked with <c>DISTINCT</c>. Named <c>IsDistinct</c> rather than
    /// <c>Distinct</c> so the fluent <see cref="QueryCommand{TResult}.Distinct"/> method does not hide
    /// the property on the generic command type.
    /// </summary>
    public bool IsDistinct { get; internal set; }
    internal bool IgnoreColumns { get; set; }
    public IReadOnlyList<QueryCommand> ReferencedQueries => _referencedQueries!;
    public Sorting[]? Sorting => _sorting;
    public object? CustomData { get => _customData; set => _customData = value; }
    public IDataContext? DataContext { get => _dataContext; set => _dataContext = value; }
    /// <summary>
    /// Common table expressions declared for this command, in declaration order, or <c>null</c> when
    /// the command has none. The list renders as a <c>with</c> clause ahead of the outer select and
    /// participates in the plan cache key.
    /// </summary>
    public IReadOnlyList<CteDefinition>? Ctes { get => _ctes; internal set => _ctes = value; }
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
        InValuesShapeHash = 0;
        InValuesPartitions = null;
        HasTopLevelInValues = false;
        _whereBasePlanHash = 0;

        _dataContext?.ResetPreparation(this);
    }

    internal void ReplaceCommand(QueryCommand cmd, int idx)
    {
#if DEBUG
        if (_dataContext != cmd._dataContext)
            throw new InvalidOperationException("Different data context");
#endif

        if (_referencedQueries is null) throw new InvalidOperationException("Referenced queries must be initialized");

        _referencedQueries[idx] = cmd;
    }
    int IQueryProvider.AddCommand(QueryCommand cmd)
    {
#if DEBUG
        if (_isPrepared) throw new InvalidOperationException("QueryCommand prepared");

        if (!cmd.IsPrepared) throw new InvalidOperationException("QueryCommand must be prepared");
#endif

        _referencedQueries ??= [];
        var idx = _referencedQueries.Count;
        _referencedQueries.Add(cmd);
        return idx;
    }

    protected void SetOperation(QueryCommand queryCommand, UnionType unionType)
    {
        if (_union is null)
        {
            _union = queryCommand;
            _unionType = unionType;
        }
        else
        {
            _union.SetOperation(queryCommand, unionType);
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
