using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

public partial class QueryCommand : IQueryRegistry, ICloneable
{

    private QueryCommand? _union;
    private UnionType _unionType;
    private IReadOnlyList<CteDefinition>? _ctes;
    private IReadOnlyList<string>? _hints;
    private List<QueryCommand>? _referencedQueries;
    private List<Expression>? _outerRefs;
    private readonly JoinExpression[]? _joins;
    /// <summary>The prepared projection columns after <c>PrepareCommand</c>, or <c>null</c> before preparation.</summary>
    protected SelectExpression[]? _selectList;
    private object? _customData;
    /// <summary>The prepared <c>FROM</c> source (physical table, subquery, CTE or set-operation operand), or <c>null</c> before preparation.</summary>
    protected FromExpression? _from;
    /// <summary>The context that executes the command, or <c>null</c> for an unbound cached clone.</summary>
    protected IDataContext? _dataContext;
    /// <summary>The original projection lambda, or <c>null</c> for a command without an explicit projection.</summary>
    protected readonly LambdaExpression? _exp;
    /// <summary>The <c>WHERE</c> predicate lambda before preparation, or <c>null</c> when there is none.</summary>
    protected readonly LambdaExpression? _condition;
    /// <summary>The <c>GROUP BY</c> key selector before preparation, or <c>null</c> when there is no grouping.</summary>
    protected readonly LambdaExpression? _groupExp;
    /// <summary>The <c>HAVING</c> predicate before preparation, or <c>null</c> when there is none.</summary>
    protected readonly LambdaExpression? _having;
    /// <summary>The ClickHouse <c>PREWHERE</c> predicate before preparation, or <c>null</c> when there is none.</summary>
    protected LambdaExpression? _preWhere;
    /// <summary>The ClickHouse <c>ARRAY JOIN</c> expressions before preparation, or <c>null</c> when the clause is absent.</summary>
    protected LambdaExpression[]? _arrayJoins;
    internal Expression[]? _preparedArrayJoin;
    private IReadOnlyList<WindowDefinition>? _windows;
    /// <summary>Whether the command has been prepared and its cached plan hashes are current.</summary>
    protected bool _isPrepared;
    /// <summary>The entity type the command reads from, or <c>null</c> for a command without a source type.</summary>
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
    /// <summary>
    /// Hash of <see cref="Hints"/>. It is part of the plan key so two otherwise identical commands
    /// with different hints do not share a cached plan (and therefore the wrong SQL).
    /// </summary>
    internal int HintsPlanHash;
    /// <summary>
    /// Hash of <see cref="Windows"/>. It is part of the plan key so two otherwise identical commands
    /// with different named windows (or the same name with different keys) do not share a cached plan.
    /// </summary>
    internal int WindowsPlanHash;
    internal Type? ResultType;
    /// <summary>The paging state (<c>LIMIT</c>/<c>OFFSET</c>) applied to the query.</summary>
    public Paging Paging;
    /// <summary>
    /// True when this command is used as a correlated scalar with a <c>*OrDefault</c> terminal
    /// (<c>FirstOrDefault</c>/<c>SingleOrDefault</c>). A SQL NULL result then means "no row" and must
    /// materialise as <c>default</c> instead of throwing.
    /// </summary>
    internal bool DefaultOnEmpty;
    /// <summary>
    /// True when this command is used as a correlated scalar with a <c>Single</c>/<c>SingleOrDefault</c>
    /// terminal. At most one row is allowed; the renderer may reject it on dialects whose scalar
    /// subqueries do not enforce cardinality.
    /// </summary>
    internal bool SingleScalar;
    internal Expression? PreparedCondition;
    /// <summary>
    /// The HAVING predicate after it has been run through the correlated-query visitor. Subqueries in
    /// HAVING register their outer references here, exactly like <see cref="PreparedCondition"/> does
    /// for WHERE; the renderer prefers it over the raw <see cref="Having"/> lambda.
    /// </summary>
    internal Expression? PreparedHaving;
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
    private SelectExpression[]? _limitByColumns;
    private SelectExpression[]? _distinctOnColumns;
    /// <summary>The sort columns before preparation, or <c>null</c> when the query has no <c>ORDER BY</c>.</summary>
    protected readonly Sorting[]? _sorting;

    /// <summary>
    /// Initializes a command from a query shape, copying each collaborator and leaving the command
    /// unprepared.
    /// </summary>
    /// <param name="dataProvider">The context that executes the command, or <c>null</c> for an unbound clone.</param>
    /// <param name="definition">The shape to copy from.</param>
    protected QueryCommand(IDataContext? dataProvider, QueryDefinition definition)
    {
        _dataContext = dataProvider;
        _exp = definition.Exp;
        _srcType = definition.SrcType;
        _condition = definition.Condition;
        _joins = definition.Joins;
        Paging = definition.Paging;
        _sorting = definition.Sorting;
        _groupExp = definition.Group;
        _having = definition.Having;
        Logger = definition.Logger;
        IsDistinct = definition.IsDistinct;
        GroupByWithTotals = definition.GroupByWithTotals;
        LimitBy = definition.LimitBy;
        DistinctOn = definition.DistinctOn;
        TableSample = definition.TableSample;
        Temporal = definition.Temporal;
        RowLock = definition.RowLock;
        Final = definition.Final;
        SampleRatio = definition.SampleRatio;
        SampleOffset = definition.SampleOffset;
        Settings = definition.Settings;
        _preWhere = definition.PreWhere;
        _arrayJoins = definition.ArrayJoins?.ToArray();
        _windows = definition.Windows;
        ArrayJoinKind = definition.ArrayJoinKind;
        BindArrayJoinElement = definition.BindArrayJoinElement;
    }
    /// <summary>
    /// Snapshot of this command's shape as a <see cref="QueryDefinition"/>, used by the clone and
    /// re-ordering paths to build a variant with <c>with</c> instead of repeating every collaborator.
    /// </summary>
    internal QueryDefinition Definition => new()
    {
        Exp = _exp,
        SrcType = _srcType,
        Condition = _condition,
        Joins = _joins,
        Paging = Paging,
        Sorting = _sorting,
        Group = _groupExp,
        Having = _having,
        Logger = Logger,
        IsDistinct = IsDistinct,
        GroupByWithTotals = GroupByWithTotals,
        LimitBy = LimitBy,
        DistinctOn = DistinctOn,
        TableSample = TableSample,
        Temporal = Temporal,
        RowLock = RowLock,
        Final = Final,
        SampleRatio = SampleRatio,
        SampleOffset = SampleOffset,
        Settings = Settings,
        PreWhere = _preWhere,
        ArrayJoins = _arrayJoins,
        Windows = _windows,
        ArrayJoinKind = ArrayJoinKind,
        BindArrayJoinElement = BindArrayJoinElement,
    };
    /// <summary>The logger that receives command-preparation diagnostics, or <c>null</c> when logging is disabled.</summary>
    public ILogger? Logger { get; }
    /// <summary>The prepared <c>FROM</c> source, or <c>null</c> before preparation.</summary>
    public FromExpression? From { get => _from; set => _from = value; }
    /// <summary>The prepared projection columns, or <c>null</c> before preparation.</summary>
    public SelectExpression[]? SelectList => _selectList;
    /// <summary>The prepared <c>GROUP BY</c> columns, or <c>null</c> when the query has no grouping.</summary>
    public SelectExpression[]? GroupingList => _groupingList;
    /// <summary>The entity type the command reads from, or <c>null</c> for a command without a source type.</summary>
    public Type? EntityType => _srcType;
    /// <summary>Whether the command has been prepared and its cached plan hashes are current.</summary>
    public bool IsPrepared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isPrepared;
    }
    /// <summary>The <c>WHERE</c> predicate lambda, or <c>null</c> when there is none.</summary>
    public Expression? Condition => _condition;
    /// <summary>
    /// The original projection lambda. Exposed internally so the in-memory grouped path can rewrite
    /// aggregate calls in the projection body; SQL providers use <see cref="SelectList"/> instead.
    /// </summary>
    internal LambdaExpression? ProjectionExpression => _exp;
    /// <summary>The join clauses declared for the query, or <c>null</c> when there are none.</summary>
    public JoinExpression[]? Joins => _joins;
    /// <summary>Whether the prepared plan may be stored in and reused from the plan cache; set to <c>false</c> for commands that must not be cached.</summary>
    public bool Cache
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_dontCache;
        set => _dontCache = !value;
    }
    internal QueryCommand? FromQuery => From?.SubQuery;
    internal bool OneColumn { get; set; }
    /// <summary>Whether the command is shaped to return at most a single row (set by the <c>First</c>/<c>Single</c> terminals).</summary>
    public bool SingleRow { get; set; }
    /// <summary>
    /// Whether the command was marked with <c>DISTINCT</c>. Named <c>IsDistinct</c> rather than
    /// <c>Distinct</c> so the fluent <see cref="QueryCommand{TResult}.Distinct"/> method does not hide
    /// the property on the generic command type.
    /// </summary>
    public bool IsDistinct { get; internal set; }
    /// <summary>
    /// Whether the grouping carries the <c>WITH TOTALS</c> modifier (ClickHouse). Only meaningful when
    /// the command has a grouping list; a dialect that does not support it rejects the command when its
    /// SQL is built.
    /// </summary>
    public bool GroupByWithTotals { get; internal set; }
    /// <summary>
    /// The <c>LIMIT n BY expr</c> clause (ClickHouse), or <c>null</c> when the query has none. A dialect
    /// that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal LimitByClause? LimitBy { get; set; }
    /// <summary>The prepared key columns of <see cref="LimitBy"/>, or <c>null</c> when there is none.</summary>
    internal SelectExpression[]? LimitByColumns => _limitByColumns;
    /// <summary>
    /// The <c>DISTINCT ON (expr, ...)</c> clause (PostgreSQL), or <c>null</c> when the query has none. A
    /// dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal DistinctOnClause? DistinctOn { get; set; }
    /// <summary>The prepared key columns of <see cref="DistinctOn"/>, or <c>null</c> when there is none.</summary>
    internal SelectExpression[]? DistinctOnColumns => _distinctOnColumns;
    /// <summary>
    /// The <c>TABLESAMPLE</c> table modifier, or <c>null</c> when the query has none. A dialect that does
    /// not support it rejects the command when its SQL is built.
    /// </summary>
    internal TableSampleClause? TableSample { get; set; }
    /// <summary>
    /// The <c>FOR SYSTEM_TIME</c> temporal-table clause, or <c>null</c> when the query has none. A
    /// dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal TemporalClause? Temporal { get; set; }
    /// <summary>
    /// The trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause, or <c>null</c> when the query
    /// has none. A dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal LockClause? RowLock { get; set; }
    /// <summary>Whether the query carries the ClickHouse <c>FINAL</c> modifier.</summary>
    public bool Final { get; internal set; }
    /// <summary>The ClickHouse <c>SAMPLE</c> ratio (a value in <c>[0, 1]</c>), or <c>null</c> when absent.</summary>
    public double? SampleRatio { get; internal set; }
    /// <summary>The ClickHouse <c>SAMPLE ... OFFSET</c> value; zero when absent.</summary>
    public double SampleOffset { get; internal set; }
    /// <summary>The trailing ClickHouse <c>SETTINGS</c> entries, or <c>null</c> when there are none.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Settings { get; internal set; }
    /// <summary>The ClickHouse <c>PREWHERE</c> predicate, or <c>null</c> when there is none.</summary>
    public LambdaExpression? PreWhere => _preWhere;
    /// <summary>
    /// The named window definitions declared for this query (<c>WINDOW w AS (...)</c>), or <c>null</c>
    /// when the query declares none. A dialect that does not support the clause rejects a command that
    /// carries them when its SQL is built.
    /// </summary>
    public IReadOnlyList<WindowDefinition>? Windows => _windows;
    /// <summary>
    /// The prepared ClickHouse <c>ARRAY JOIN</c> expressions, or <c>null</c> when the clause is absent.
    /// A dialect that does not support it rejects the command when its SQL is built. Internal so the
    /// mutable prepared array cannot be reached from outside and used to corrupt the plan-cache key.
    /// </summary>
    internal IReadOnlyList<Expression>? ArrayJoinExpressions => _preparedArrayJoin;
    /// <summary>The <c>ARRAY JOIN</c> kind (plain or <c>LEFT</c>) shared by <see cref="ArrayJoinExpressions"/>.</summary>
    public ArrayJoinKind ArrayJoinKind { get; internal set; }
    /// <summary>
    /// Whether the last <c>ARRAY JOIN</c> expression is aliased as the element referenced by
    /// <see cref="ArrayJoinProjection{TEntity, TElement}.Element"/>.
    /// </summary>
    internal bool BindArrayJoinElement { get; set; }
    /// <summary>The prepared <see cref="PreWhere"/> expression, or <c>null</c> when there is none.</summary>
    internal Expression? PreparedPreWhere;    /// <summary>Shape hash of any captured value-list in <see cref="PreparedPreWhere"/>; zero when none.</summary>
    internal int PreWhereShapeHash;
    /// <summary>Whether <see cref="PreparedPreWhere"/> contains a top-level value-list whose shape is refreshed per run.</summary>
    internal bool HasPreWhereInValues;
    internal bool IgnoreColumns { get; set; }
    /// <summary>The commands referenced by correlated subqueries in this command's expressions, in registration order; empty until preparation registers one.</summary>
    public IReadOnlyList<QueryCommand> ReferencedQueries => _referencedQueries!;
    /// <summary>The sort columns declared for the query, or <c>null</c> when the query has no <c>ORDER BY</c>.</summary>
    public Sorting[]? Sorting => _sorting;
    /// <summary>An extension slot for provider- or consumer-specific data; the core engine never reads or writes it.</summary>
    public object? CustomData { get => _customData; set => _customData = value; }
    /// <summary>The context that executes the command, or <c>null</c> for an unbound cached clone.</summary>
    public IDataContext? DataContext { get => _dataContext; set => _dataContext = value; }
    /// <summary>
    /// Common table expressions declared for this command, in declaration order, or <c>null</c> when
    /// the command has none. The list renders as a <c>with</c> clause ahead of the outer select and
    /// participates in the plan cache key.
    /// </summary>
    public IReadOnlyList<CteDefinition>? Ctes { get => _ctes; internal set => _ctes = value; }
    /// <summary>
    /// Statement-level query hints attached to this command (for example SQL Server <c>RECOMPILE</c>),
    /// or <c>null</c> when the command has none. How they are rendered is provider specific; a dialect
    /// that does not implement query hints rejects a command that carries them.
    /// </summary>
    public IReadOnlyList<string>? Hints { get => _hints; internal set => _hints = value; }
    /// <summary>Appends <paramref name="hints"/> to the command's hint list, ignoring null/blank entries.</summary>
    internal void AddHints(string[]? hints)
    {
        if (hints is null || hints.Length == 0) return;

        var list = _hints is null ? new List<string>(hints.Length) : new List<string>(_hints);

        for (var i = 0; i < hints.Length; i++)
        {
            var hint = hints[i];
            if (!string.IsNullOrWhiteSpace(hint))
                list.Add(hint);
        }

        if (list.Count > 0)
            _hints = list;
    }
    /// <summary>
    /// Table-level hints attached to the command's physical <c>FROM</c> table (for example SQL Server
    /// <c>nolock</c>), or <c>null</c> when there are none. How they are rendered is provider specific;
    /// a dialect that does not implement table hints rejects a command that carries them.
    /// </summary>
    public IReadOnlyList<string>? TableHints { get; internal set; }
    /// <summary>
    /// Per-command override of identifier quoting: <c>true</c> quotes physical table/column names with
    /// the provider's delimiter, <c>false</c> emits them verbatim, and <c>null</c> inherits the
    /// context default (<c>DataContextBuilder.UseQuotedIdentifiers</c>). Set through
    /// <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public bool? QuoteIdentifiers { get; internal set; }
    /// <summary>
    /// The identifier-quoting flag resolved for this command during preparation: the command override
    /// when present, otherwise the context default. It is part of the plan key (the inherited value
    /// must not let two contexts with different defaults share a cached plan).
    /// </summary>
    internal bool ResolvedQuoteIdentifiers;
    /// <summary>
    /// Per-command override of the naming convention applied to auto-derived table/column names:
    /// <see langword="null"/> inherits the context default
    /// (<c>DataContextBuilder.UseNamingConvention</c>). Set through <c>WithNamingConvention</c>.
    /// </summary>
    public INamingConvention? NamingConvention { get; internal set; }
    /// <summary>
    /// The naming convention resolved for this command during preparation: the command override when
    /// present, otherwise the context default. It is part of the plan key (the inherited value must
    /// not let two contexts with different defaults share a cached plan).
    /// </summary>
    internal INamingConvention? ResolvedNamingConvention;
    /// <summary>
    /// The trailing <c>FOR JSON</c> clause (SQL Server), or <c>null</c> when the result set is returned
    /// as rows. A dialect that does not implement it rejects a command that carries it.
    /// </summary>
    public ForJsonClause? ForJsonClause { get; internal set; }
    /// <summary>
    /// The trailing <c>FOR XML</c> clause (SQL Server), or <c>null</c> when the result set is returned
    /// as rows. Mutually exclusive with <see cref="ForJsonClause"/>.
    /// </summary>
    public ForXmlClause? ForXmlClause { get; internal set; }
    /// <summary>The <c>GROUP BY</c> key selector, or <c>null</c> when the query has no grouping.</summary>
    public LambdaExpression? GroupBy { get => _groupExp; }
    /// <summary>
    /// The super-aggregate modifier applied to the grouping list (<c>ROLLUP</c>/<c>CUBE</c>), or
    /// <see cref="GroupingType.None"/> for a plain <c>GROUP BY</c>. Only meaningful when a grouping
    /// list is present.
    /// </summary>
    public GroupingType GroupingType { get; internal set; }
    /// <summary>
    /// The explicit grouping sets (0-based indices into the grouping list) used when
    /// <see cref="GroupingType"/> is <see cref="NextORM.Core.GroupingType.GroupingSets"/>, or
    /// <c>null</c> otherwise. An empty set yields the grand total.
    /// </summary>
    public IReadOnlyList<int[]>? GroupingSets { get; internal set; }
    /// <summary>The <c>HAVING</c> predicate lambda, or <c>null</c> when there is none.</summary>
    public LambdaExpression? Having { get => _having; }
    /// <summary>The second operand of the command's set operation, or <c>null</c> when the command carries none.</summary>
    public QueryCommand? UnionQuery { get => _union; }
    /// <summary>The set operation (<c>UNION</c>/<c>INTERSECT</c>/<c>EXCEPT</c> and their <c>ALL</c> variants) applied to the query.</summary>
    public UnionType UnionType { get => _unionType; }
    /// <summary>The expressions registered as correlated outer references during preparation, or <c>null</c> when there are none.</summary>
    public IReadOnlyList<Expression>? OuterReferences => _outerRefs;

    /// <summary>
    /// The registry that owns the <see cref="ReferencedQueries"/> and <see cref="OuterReferences"/>
    /// this command's expressions resolve against when it was built as a correlated subquery. Nested
    /// commands share the root command's registry, so the renderer - which always resolves references
    /// against the root - can resolve markers registered one or more correlation levels up.
    /// </summary>
    internal IQueryRegistry? OuterRegistry { get; set; }

    /// <summary>
    /// True for the projection types a scalar <c>Single</c>/<c>SingleOrDefault</c> subquery can guard
    /// itself against a second row on a dialect that does not enforce cardinality: the guard raises a
    /// database error through a numeric overflow, so only numeric (and nullable numeric) projections
    /// are supported.
    /// </summary>
    internal static bool IsCardinalityGuardable(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short)
            || underlying == typeof(byte) || underlying == typeof(double) || underlying == typeof(float)
            || underlying == typeof(decimal);
    }

    /// <summary>
    /// The outermost registry in the <see cref="OuterRegistry"/> chain (this command when it has no
    /// outer registry).
    /// </summary>
    internal IQueryRegistry RootRegistry
    {
        get
        {
            IQueryRegistry registry = this;
            while (registry is QueryCommand { OuterRegistry: { } owner })
                registry = owner;
            return registry;
        }
    }

    /// <summary>
    /// Discards every prepared piece (from, columns, grouping, conditions, array joins and hashes) so the
    /// command is prepared again on its next execution. Called after a clone is mutated.
    /// </summary>
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _groupingList = null;
        _limitByColumns = null;
        _distinctOnColumns = null;
        PreparedPreWhere = null;
        _preparedArrayJoin = null;
        PreWhereShapeHash = 0;
        HasPreWhereInValues = false;
        _from = null;
        PreparedCondition = null;
        PreparedHaving = null;
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
    int IQueryRegistry.AddCommand(QueryCommand cmd)
    {
        // A nested command shares the root registry so the renderer (which resolves
        // ReferencedQueries against the root command) sees the same list the visitor appended to.
        if (OuterRegistry is { } owner) return owner.AddCommand(cmd);

#if DEBUG
        if (_isPrepared) throw new InvalidOperationException("QueryCommand prepared");

        if (!cmd.IsPrepared) throw new InvalidOperationException("QueryCommand must be prepared");
#endif

        _referencedQueries ??= [];
        var idx = _referencedQueries.Count;
        _referencedQueries.Add(cmd);
        return idx;
    }

    /// <summary>Attaches a set operation to this command, chaining it onto an existing operation when one is present.</summary>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <param name="unionType">The kind of set operation to apply.</param>
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

    /// <summary>Drops the set operation so the first operand can be executed on its own.</summary>
    internal void ClearUnion()
    {
        _union = null;
        _unionType = UnionType.None;
    }

    /// <summary>Registers an expression as a correlated outer reference and returns its index.</summary>
    /// <param name="node">The expression that references an outer query.</param>
    /// <returns>The zero-based index of the reference in the command's outer-reference list.</returns>
    public int AddOuterReference(Expression node)
    {
        // See IQueryRegistry.AddCommand: outer references live on the root registry so a marker
        // registered while preparing a nested command is resolvable when the root is rendered.
        if (OuterRegistry is { } owner) return owner.AddOuterReference(node);

        var outerRefs = _outerRefs ??= [];

        var idx = outerRefs.Count;

        outerRefs.Add(node);

        return idx;
    }

}
