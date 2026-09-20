using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;
// public class BaseEntity
// {
//     protected BaseEntity()
//     {
//     }
//     protected static readonly IDictionary<IDataContext, Lazy<QueryCommand<bool>>> _anyCommandCache = new ConcurrentDictionary<IDataContext, Lazy<QueryCommand<bool>>>();
// }
/// <summary>
/// Fluent, immutable query builder over a mapped entity type; created by
/// <c>IDataContext.From&lt;TEntity&gt;()</c>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type the query projects.</typeparam>
public class EntityBuilder<TEntity> : ICloneable //IAsyncEnumerable<TEntity>
{
    // private const string AnyCommandProperty = "NextORM.Core.AnyCommand";
    #region Fields
    protected readonly IDataContext _dataProvider;
    private QueryCommand? _query;
    private Expression<Func<TEntity, bool>>? _condition;
    private LambdaExpression? _group;
    private LimitByClause? _limitBy;
    private DistinctOnClause? _distinctOn;
    private TableSampleClause? _tablesample;
    private TemporalClause? _temporal;
    private LockClause? _rowLock;
    private LambdaExpression? _preWhere;
    private List<LambdaExpression>? _arrayJoins;
    private ArrayJoinKind _arrayJoinKind;
    private Type? _sourceEntityType;
    private bool _bindArrayJoinElement;
    private List<KeyValuePair<string, string>>? _settings;
    private Expression<Func<TEntity, bool>>? _having;
    private List<Sorting>? _sorting;
    protected List<JoinExpression>? _joins;
    private string? _table;
    private FromExpression? _from;
    #endregion
    public EntityBuilder(IDataContext dataProvider)
    {
        _dataProvider = dataProvider;
    }
    public EntityBuilder(IDataContext dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    public EntityBuilder(IDataContext dataProvider, string table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    #region Properties
    internal ILogger? Logger { get; init; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    internal IDataContext DataProvider => _dataProvider;
    internal Expression<Func<TEntity, bool>>? Condition { get => _condition; set => _condition = value; }
    public List<Sorting>? Sorting { get => _sorting; set => _sorting = value; }
    public List<JoinExpression>? Joins { get => _joins; set => _joins = value; }
    public Paging Paging;
    internal bool IsDistinct { get; set; }
    /// <summary>Super-aggregate modifier applied to the grouping list (<c>ROLLUP</c>/<c>CUBE</c>).</summary>
    internal GroupingType GroupingType { get; set; }
    /// <summary>Explicit grouping-set indices used when <see cref="GroupingType"/> is <c>GroupingSets</c>.</summary>
    internal IReadOnlyList<int[]>? GroupingSets { get; set; }
    /// <summary>Whether the grouping carries the <c>WITH TOTALS</c> modifier (ClickHouse).</summary>
    internal bool GroupByWithTotals { get; set; }
    /// <summary>The <c>LIMIT n BY expr</c> clause (ClickHouse), or <c>null</c> when there is none.</summary>
    internal LimitByClause? LimitByClause { get => _limitBy; set => _limitBy = value; }
    /// <summary>The <c>DISTINCT ON (expr, ...)</c> clause (PostgreSQL), or <c>null</c> when there is none.</summary>
    internal DistinctOnClause? DistinctOnClause { get => _distinctOn; set => _distinctOn = value; }
    /// <summary>The <c>TABLESAMPLE</c> table modifier, or <c>null</c> when there is none.</summary>
    internal TableSampleClause? TableSampleClause { get => _tablesample; set => _tablesample = value; }
    /// <summary>The <c>FOR SYSTEM_TIME</c> temporal-table clause, or <c>null</c> when there is none.</summary>
    internal TemporalClause? TemporalClause { get => _temporal; set => _temporal = value; }
    /// <summary>The trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause, or <c>null</c> when there is none.</summary>
    internal LockClause? RowLockClause { get => _rowLock; set => _rowLock = value; }
    /// <summary>Whether the query carries the ClickHouse <c>FINAL</c> modifier.</summary>
    internal bool IsFinal { get; set; }
    /// <summary>The ClickHouse <c>SAMPLE</c> ratio, or <c>null</c> when the modifier is absent.</summary>
    internal double? SampleRatio { get; set; }
    /// <summary>The ClickHouse <c>SAMPLE ... OFFSET</c> value; zero when absent.</summary>
    internal double SampleOffset { get; set; }
    /// <summary>The trailing ClickHouse <c>SETTINGS</c> entries, or <c>null</c> when there are none.</summary>
    internal IReadOnlyList<KeyValuePair<string, string>>? SettingsList { get => _settings; set => _settings = value is null ? null : [.. value]; }
    /// <summary>The <c>PREWHERE</c> predicate (ClickHouse), or <c>null</c> when there is none.</summary>
    internal LambdaExpression? PreWhereCondition { get => _preWhere; set => _preWhere = value; }
    /// <summary>The <c>ARRAY JOIN</c> expressions (ClickHouse), or <c>null</c> when there are none.</summary>
    internal IReadOnlyList<LambdaExpression>? ArrayJoins { get => _arrayJoins; set => _arrayJoins = value is null ? null : [.. value]; }
    /// <summary>The <c>ARRAY JOIN</c> kind (plain or <c>LEFT</c>) shared by <see cref="ArrayJoins"/>.</summary>
    internal ArrayJoinKind ArrayJoinKind { get => _arrayJoinKind; set => _arrayJoinKind = value; }
    /// <summary>Table-level hints applied to the primary physical table (for example SQL Server <c>nolock</c>).</summary>
    internal IReadOnlyList<string>? TableHints { get; set; }
    internal string? Table { get => _table; set => _table = value; }
    /// <summary>
    /// Explicit FROM source, used for table-valued functions (and any other source that is neither a
    /// raw table name nor a derived <see cref="QueryCommand"/>). Propagated through the join builders
    /// so a TVF can be used as the base of a joined query.
    /// </summary>
    internal FromExpression? SourceFrom { get => _from; set => _from = value; }
    /// <summary>
    /// Overrides the command's entity type when the projection lambda parameter differs from the
    /// physical source (only <c>ArrayJoinElement</c>/<c>LeftArrayJoinElement</c> do this: the lambda
    /// parameter is an <see cref="ArrayJoinProjection{TEntity, TElement}"/> while the source stays the
    /// original entity). <c>null</c> means the source is the lambda parameter's type.
    /// </summary>
    internal Type? SourceEntityType { get => _sourceEntityType; set => _sourceEntityType = value; }
    /// <summary>Whether the last <c>ARRAY JOIN</c> expression is bound to an element alias.</summary>
    internal bool BindArrayJoinElement { get => _bindArrayJoinElement; set => _bindArrayJoinElement = value; }
    /// <summary>CTE declarations that must be attached to commands this builder creates.</summary>
    internal IReadOnlyList<CteDefinition>? Ctes { get; set; }
    //public delegate void CommandCreatedHandler<T>(EntityBuilder<T> sender, QueryCommand queryCommand);
    //public event CommandCreatedHandler<TEntity>? CommandCreatedEvent;
    #endregion

    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(new QueryDefinition
        {
            Exp = exp,
            SrcType = _sourceEntityType,
            Condition = _condition,
            Joins = _joins?.ToArray(),
            Paging = Paging,
            Sorting = _sorting?.ToArray(),
            Group = _group,
            Having = _having,
            Logger = Logger,
            IsDistinct = IsDistinct,
            Final = IsFinal,
            SampleRatio = SampleRatio,
            SampleOffset = SampleOffset,
            Settings = _settings,
            PreWhere = _preWhere,
            ArrayJoins = _arrayJoins,
            ArrayJoinKind = _arrayJoinKind,
            BindArrayJoinElement = _bindArrayJoinElement,
        });

        if (_query is not null)
            cmd.From = new FromExpression(_query);
        else if (_from is not null)
            cmd.From = _from;
        else if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        cmd.GroupingType = GroupingType;
        cmd.TableHints = TableHints;
        cmd.GroupingSets = GroupingSets;
        cmd.GroupByWithTotals = GroupByWithTotals;
        cmd.LimitBy = LimitByClause;
        cmd.DistinctOn = _distinctOn;
        cmd.TableSample = _tablesample;
        cmd.Temporal = _temporal;
        cmd.RowLock = _rowLock;
        // OnCommandCreated(cmd);
        //RaiseCommandCreated(cmd);

        return cmd;
    }
    public QueryCommand<TEntity> ToCommand()
    {
        var cmd = _dataProvider.CreateCommand<TEntity>(new QueryDefinition
        {
            SrcType = _sourceEntityType ?? typeof(TEntity),
            Condition = _condition,
            Joins = _joins?.ToArray(),
            Paging = Paging,
            Sorting = _sorting?.ToArray(),
            Group = _group,
            Having = _having,
            Logger = Logger,
            IsDistinct = IsDistinct,
            Final = IsFinal,
            SampleRatio = SampleRatio,
            SampleOffset = SampleOffset,
            Settings = _settings,
            PreWhere = _preWhere,
            ArrayJoins = _arrayJoins,
            ArrayJoinKind = _arrayJoinKind,
            BindArrayJoinElement = _bindArrayJoinElement,
        });

        if (_query is not null)
            cmd.From = new FromExpression(_query);
        else if (_from is not null)
            cmd.From = _from;
        else if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        cmd.GroupingType = GroupingType;
        cmd.TableHints = TableHints;
        cmd.GroupingSets = GroupingSets;
        cmd.GroupByWithTotals = GroupByWithTotals;
        cmd.LimitBy = LimitByClause;
        cmd.DistinctOn = _distinctOn;
        cmd.TableSample = _tablesample;
        cmd.Temporal = _temporal;
        cmd.RowLock = _rowLock;

        // OnCommandCreated(cmd);
        //RaiseCommandCreated(cmd);

        return cmd;
    }

    // protected virtual void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    // {

    // }

    public EntityBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        var b = Clone();
        if (_condition is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterExpressionVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TEntity, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
    /// <summary>
    /// Adds the ClickHouse <c>FINAL</c> modifier to the primary <c>FROM</c> table (a forced merge of a
    /// ReplacingMergeTree/CollapsingMergeTree before the read). Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsFinal"/>).
    /// </summary>
    public EntityBuilder<TEntity> Final()
    {
        var b = Clone();
        b.IsFinal = true;
        return b;
    }
    /// <summary>
    /// Adds the ClickHouse <c>SAMPLE ratio</c> modifier: reads roughly <paramref name="ratio"/> of the
    /// rows (a value in <c>[0, 1]</c>). Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsSample"/>).
    /// </summary>
    public EntityBuilder<TEntity> Sample(double ratio) => Sample(ratio, 0);
    /// <summary>
    /// Adds the ClickHouse <c>SAMPLE ratio OFFSET offset</c> modifier: reads roughly
    /// <paramref name="ratio"/> of the rows starting at <paramref name="offset"/> (both in <c>[0, 1]</c>).
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsSample"/>).
    /// </summary>
    public EntityBuilder<TEntity> Sample(double ratio, double offset)
    {
        if (!double.IsFinite(ratio) || ratio is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Sample ratio must be a finite value in [0, 1].");

        if (!double.IsFinite(offset) || offset is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Sample offset must be a finite value in [0, 1].");

        var b = Clone();
        b.SampleRatio = ratio;
        b.SampleOffset = offset;
        return b;
    }
    /// <summary>
    /// Adds a trailing ClickHouse <c>SETTINGS key = value, ...</c> clause. The values are rendered
    /// verbatim, so only pass trusted literals (for example <c>max_threads = "2"</c>). Requires a dialect
    /// that supports it (see <see cref="ISqlDialect.SupportsSettings"/>).
    /// </summary>
    public EntityBuilder<TEntity> Settings(params (string Key, string Value)[] settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var b = Clone();
        var list = b._settings ??= [];

        foreach (var (key, value) in settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A SETTINGS key must not be empty.", nameof(settings));

            list.Add(new KeyValuePair<string, string>(key, value));
        }

        return b;
    }
    /// <summary>
    /// Adds the ClickHouse <c>PREWHERE</c> predicate, applied before the regular <c>WHERE</c> so the read
    /// can skip the other columns. A repeated call combines the predicates with <c>and</c>. Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsPreWhere"/>).
    /// </summary>
    public EntityBuilder<TEntity> PreWhere(Expression<Func<TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var b = Clone();

        if (_preWhere is not null)
        {
            var replVisitor = new ReplaceParameterExpressionVisitor(_preWhere.Parameters[0]);
            var newBody = Expression.AndAlso(_preWhere.Body, replVisitor.Visit(condition.Body));
            b._preWhere = Expression.Lambda<Func<TEntity, bool>>(newBody, _preWhere.Parameters[0]);
        }
        else
            b._preWhere = condition;

        return b;
    }
    /// <summary>
    /// Adds the ClickHouse <c>ARRAY JOIN</c> clause over <paramref name="array"/>, expanding one row per
    /// array element (a row whose array is empty is dropped). Repeated calls append to the same clause.
    /// The expanded element is not bound to a CLR member: use
    /// <see cref="ClickHouseFunctions.array_join{T}(T[])"/> in the projection when the value is needed.
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsArrayJoinClause"/>).
    /// </summary>
    public EntityBuilder<TEntity> ArrayJoin<TArray>(Expression<Func<TEntity, TArray>> array)
        => AddArrayJoin(array, ArrayJoinKind.Inner);

    /// <summary>
    /// Adds the ClickHouse <c>LEFT ARRAY JOIN</c> clause: like <see cref="ArrayJoin{TArray}"/> but a row
    /// whose array is empty is kept (with the array column at its default). Requires a dialect that
    /// supports it (see <see cref="ISqlDialect.SupportsArrayJoinClause"/>).
    /// </summary>
    public EntityBuilder<TEntity> LeftArrayJoin<TArray>(Expression<Func<TEntity, TArray>> array)
        => AddArrayJoin(array, ArrayJoinKind.Left);

    private EntityBuilder<TEntity> AddArrayJoin<TArray>(Expression<Func<TEntity, TArray>> array, ArrayJoinKind kind)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (typeof(TArray) == typeof(string) || !typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(TArray)))
            throw new ArgumentException("ARRAY JOIN requires an array or sequence expression.", nameof(array));

        if (_arrayJoins is { Count: > 0 } && _arrayJoinKind != kind)
            throw new InvalidOperationException("An ARRAY JOIN clause cannot mix ARRAY JOIN and LEFT ARRAY JOIN.");

        var b = Clone();

        var joins = _arrayJoins is null ? new List<LambdaExpression>(1) : new List<LambdaExpression>(_arrayJoins);
        joins.Add(array);
        b._arrayJoins = joins;
        b._arrayJoinKind = kind;

        return b;
    }

    /// <summary>
    /// Adds a ClickHouse <c>ARRAY JOIN</c> over <paramref name="array"/> and returns a builder whose
    /// projection parameter exposes both the original entity (<c>p.Item1</c>) and the expanded element
    /// (<c>p.Element</c>). A row whose array is empty is dropped. Requires a dialect that supports the
    /// clause (see <see cref="ISqlDialect.SupportsArrayJoinClause"/>).
    /// </summary>
    /// <typeparam name="TElement">The element type of the joined array.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// The builder already carries a bound array join, a join, or a Where/Having (which must be applied
    /// after this call), or the array join kind conflicts with an earlier <c>ArrayJoin</c>.
    /// </exception>
    public EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> array)
        => ToArrayJoinElement(array, ArrayJoinKind.Inner);

    /// <summary>
    /// Adds a ClickHouse <c>LEFT ARRAY JOIN</c> over <paramref name="array"/> and returns a builder
    /// whose projection parameter exposes both the original entity (<c>p.Item1</c>) and the expanded
    /// element (<c>p.Element</c>). Unlike <see cref="ArrayJoinElement{TElement}"/> a row whose array is
    /// empty is kept (with the element at its default). Requires a dialect that supports the clause
    /// (see <see cref="ISqlDialect.SupportsArrayJoinClause"/>).
    /// </summary>
    /// <typeparam name="TElement">The element type of the joined array.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// The builder already carries a bound array join, a join, or a Where/Having (which must be applied
    /// after this call), or the array join kind conflicts with an earlier <c>ArrayJoin</c>.
    /// </exception>
    public EntityBuilder<ArrayJoinProjection<TEntity, TElement>> LeftArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> array)
        => ToArrayJoinElement(array, ArrayJoinKind.Left);

    private EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ToArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> array, ArrayJoinKind kind)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (_sourceEntityType is not null)
            throw new InvalidOperationException("Only one ArrayJoinElement/LeftArrayJoinElement is supported per query.");

        if (_condition is not null || _having is not null)
            throw new InvalidOperationException("Where/Having must be applied after ArrayJoinElement/LeftArrayJoinElement, whose projection parameter differs from the entity.");

        if (_joins is { Count: > 0 })
            throw new InvalidOperationException("ArrayJoinElement/LeftArrayJoinElement is not supported on a joined query; use ArrayJoin/LeftArrayJoin.");

        if (_arrayJoins is { Count: > 0 } && _arrayJoinKind != kind)
            throw new InvalidOperationException("An ARRAY JOIN clause cannot mix ARRAY JOIN and LEFT ARRAY JOIN.");

        var b = new EntityBuilder<ArrayJoinProjection<TEntity, TElement>>(_dataProvider)
        {
            Logger = Logger,
            SourceEntityType = typeof(TEntity)
        };

        // Carry the query shape. The projection parameter type changes, so the Where/Having lambdas
        // cannot be carried (they are rejected above); everything else is projection independent.
        CopyProjectionIndependentStateTo(b);

        var joins = _arrayJoins is null ? new List<LambdaExpression>(1) : new List<LambdaExpression>(_arrayJoins);
        joins.Add(array);
        b._arrayJoins = joins;
        b._arrayJoinKind = kind;
        b.BindArrayJoinElement = true;

        return b;
    }

    /// <summary>
    /// Applies a ClickHouse join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>) to the most recently added
    /// join. The builder is copied — the source join is replaced by a copy carrying the modifier, so
    /// neither the source builder nor a sibling built from it is affected. Call it after the join it
    /// should affect and before the next join. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsJoinStrictness"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The builder has no join to modify.</exception>
    public EntityBuilder<TEntity> WithStrictness(JoinStrictness strictness)
        => ReplaceLastJoin(strictness);

    /// <summary>
    /// Marks the most recently added join as the ClickHouse <c>GLOBAL</c> variant (the right-hand side
    /// is resolved once and broadcast, for distributed queries). The builder is copied — the source
    /// join is replaced by a copy carrying the modifier, so neither the source builder nor a sibling
    /// built from it is affected. Call it after the join it should affect and before the next join.
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsGlobalJoin"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The builder has no join to modify.</exception>
    public EntityBuilder<TEntity> Global() => ReplaceLastJoin(null, isGlobal: true);

    /// <summary>
    /// Returns a copy of this builder whose last join carries the given modifiers. The clone copies the
    /// join objects by reference, so the last one is replaced with a fresh copy; mutating it in place
    /// would leak the modifier into the source builder and its other clones.
    /// </summary>
    private EntityBuilder<TEntity> ReplaceLastJoin(JoinStrictness? strictness = null, bool isGlobal = false)
    {
        if (_joins is not { Count: > 0 })
            throw new InvalidOperationException("A join modifier requires a preceding join.");

        var b = Clone();

        var joins = b._joins;
        if (joins is null)
        {
            joins = [.. _joins];
            b._joins = joins;
        }

        var last = joins[^1];
        joins[^1] = new JoinExpression(last.JoinCondition, last.JoinType)
        {
            From = last.From,
            EntityType = last.EntityType,
            Strictness = strictness ?? last.Strictness,
            IsGlobal = isGlobal || last.IsGlobal
        };

        b.OnLastJoinReplaced(joins[^1]);

        return b;
    }

    /// <summary>
    /// Called on the copied builder after <see cref="WithStrictness"/> replaced its last join, so a
    /// derived builder can re-point any property that aliases that join (see
    /// <see cref="JoinedEntityBuilder{T1, T2}.JoinCondition"/>).
    /// </summary>
    protected virtual void OnLastJoinReplaced(JoinExpression join)
    {
    }
    public EntityBuilder<TEntity> Distinct()
    {
        if (_distinctOn is not null)
            throw new InvalidOperationException("DISTINCT cannot be combined with DISTINCT ON.");

        var b = Clone();
        b.IsDistinct = true;
        return b;
    }
    /// <summary>
    /// Adds a <c>DISTINCT ON (expr, ...)</c> clause (PostgreSQL): keeps the first row of each distinct
    /// key, where <paramref name="exp"/> is a single column or an anonymous type to key on several
    /// columns. PostgreSQL requires the leading <c>ORDER BY</c> expressions to match the key. Cannot be
    /// combined with <see cref="Distinct"/>. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsDistinctOn"/>).
    /// </summary>
    /// <param name="exp">The key selector: a single column or an anonymous type keying on several columns.</param>
    public EntityBuilder<TEntity> DistinctOn<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        ArgumentNullException.ThrowIfNull(exp);

        if (IsDistinct)
            throw new InvalidOperationException("DISTINCT ON cannot be combined with DISTINCT.");

        var b = Clone();
        b._distinctOn = new DistinctOnClause(exp);

        return b;
    }
    /// <summary>
    /// Adds a <c>TABLESAMPLE</c> modifier to the primary table: reads only <paramref name="percent"/>
    /// percent of the table using <paramref name="method"/>. <paramref name="seed"/> makes the sample
    /// repeatable. Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsTableSample"/>).
    /// </summary>
    /// <param name="percent">The percentage of the table to sample; must be in <c>(0, 100]</c>.</param>
    /// <param name="method">The sampling algorithm.</param>
    /// <param name="seed">An optional seed that makes the sample repeatable.</param>
    public EntityBuilder<TEntity> TableSample(double percent, TableSampleMethod method = TableSampleMethod.System, double? seed = null)
    {
        if (percent <= 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "TABLESAMPLE percent must be in (0, 100].");

        var b = Clone();
        b._tablesample = new TableSampleClause(method, percent, seed);

        return b;
    }
    /// <summary>
    /// Adds a <c>FOR SYSTEM_TIME</c> clause to the primary table, querying a system-versioned (temporal)
    /// table as of a point in time or over a range. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsTemporalTable"/>).
    /// </summary>
    /// <param name="clause">The temporal clause, built with the <see cref="TemporalClause"/> factory methods.</param>
    public EntityBuilder<TEntity> ForSystemTime(TemporalClause clause)
    {
        ArgumentNullException.ThrowIfNull(clause);

        var b = Clone();
        b._temporal = clause;

        return b;
    }
    /// <summary>
    /// Adds a trailing <c>FOR UPDATE</c> row-locking clause: the selected rows are locked exclusively
    /// until the transaction ends. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsLocking"/>).
    /// </summary>
    public EntityBuilder<TEntity> ForUpdate() => WithRowLock(LockMode.Update);

    /// <summary>
    /// Adds a trailing <c>FOR SHARE</c> row-locking clause: the selected rows are locked in shared mode
    /// until the transaction ends. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsLocking"/>).
    /// </summary>
    public EntityBuilder<TEntity> ForShare() => WithRowLock(LockMode.Share);

    private EntityBuilder<TEntity> WithRowLock(LockMode mode)
    {
        var b = Clone();
        b._rowLock = new LockClause(mode);
        return b;
    }
    public EntityBuilder<TEntity> Limit(int limit)
    {
        var b = Clone();

        b.Paging.Limit = limit;

        return b;
    }
    public EntityBuilder<TEntity> Offset(int offset)
    {
        var b = Clone();

        b.Paging.Offset = offset;

        return b;
    }
    public EntityBuilder<TEntity> Page(int limit, int offset)
    {
        var b = Clone();

        b.Paging.Limit = limit;
        b.Paging.Offset = offset;

        return b;
    }
    /// <summary>
    /// Marks the page request as <c>WITH TIES</c>: the result keeps every row tied with the last row of
    /// the page by the <c>ORDER BY</c>. Requires a positive page limit and a dialect that supports it
    /// (see <see cref="ISqlDialect.SupportsWithTies"/>).
    /// </summary>
    public EntityBuilder<TEntity> WithTies()
    {
        var b = Clone();
        b.Paging.HasWithTies = true;

        return b;
    }
    /// <summary>
    /// Adds a <c>LIMIT n BY expr</c> clause (ClickHouse): at most <paramref name="limit"/> rows per
    /// distinct value of <paramref name="exp"/>. <paramref name="exp"/> may be a single column or an
    /// anonymous type to key on several columns. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsLimitBy"/>).
    /// </summary>
    public EntityBuilder<TEntity> LimitBy<TResult>(int limit, Expression<Func<TEntity, TResult>> exp)
        => LimitBy(limit, 0, exp);
    /// <summary>
    /// Adds a <c>LIMIT offset, n BY expr</c> clause (ClickHouse): skips <paramref name="offset"/> rows
    /// and then returns at most <paramref name="limit"/> rows per distinct key. <paramref name="limit"/>
    /// must be positive and <paramref name="offset"/> non-negative. Requires a dialect that supports it
    /// (see <see cref="ISqlDialect.SupportsLimitBy"/>).
    /// </summary>
    public EntityBuilder<TEntity> LimitBy<TResult>(int limit, int offset, Expression<Func<TEntity, TResult>> exp)
    {
        ArgumentNullException.ThrowIfNull(exp);

        var b = Clone();

        b._limitBy = new LimitByClause(exp, limit, offset);

        return b;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public EntityBuilder<TEntity> Clone()
    {
        return (EntityBuilder<TEntity>)CloneImp();
    }
    protected virtual void CopyTo(EntityBuilder<TEntity> dst)
    {
        CopyProjectionIndependentStateTo(dst);
        dst._condition = _condition;
        dst._having = _having;
        dst._arrayJoins = _arrayJoins is null ? null : [.. _arrayJoins];
        dst._arrayJoinKind = _arrayJoinKind;
        dst._sourceEntityType = _sourceEntityType;
        dst._bindArrayJoinElement = _bindArrayJoinElement;
    }
    /// <summary>
    /// Copies every piece of query state whose type does not depend on the projection parameter, so it
    /// can be carried onto a builder with a different <typeparamref name="TOther"/> (used by
    /// <see cref="ArrayJoinElement{TElement}"/>). The projection-typed state (<c>_condition</c>,
    /// <c>_having</c>) is intentionally excluded.
    /// </summary>
    private void CopyProjectionIndependentStateTo<TOther>(EntityBuilder<TOther> dst)
    {
        dst._query = _query;
        dst.Paging = Paging;
        dst._sorting = _sorting;
        dst._group = _group;
        dst._table = _table;
        dst._from = _from;
        dst.IsDistinct = IsDistinct;
        dst.GroupingType = GroupingType;
        dst.GroupingSets = GroupingSets;
        dst.GroupByWithTotals = GroupByWithTotals;
        dst.LimitByClause = LimitByClause;
        dst.DistinctOnClause = DistinctOnClause;
        dst.TableSampleClause = TableSampleClause;
        dst.TemporalClause = TemporalClause;
        dst.RowLockClause = RowLockClause;
        dst.IsFinal = IsFinal;
        dst.SampleRatio = SampleRatio;
        dst.SampleOffset = SampleOffset;
        dst.SettingsList = _settings is null ? null : [.. _settings];
        dst.PreWhereCondition = _preWhere;
        dst.TableHints = TableHints;
        dst.Ctes = Ctes;
    }
    protected virtual object CloneImp()
    {
        var r = new EntityBuilder<TEntity>(_dataProvider) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    public static implicit operator QueryCommand<TEntity>(EntityBuilder<TEntity> builder) => builder.ToCommand();
    public static implicit operator QueryCommand(EntityBuilder<TEntity> builder) => builder.ToCommand();
    // public QueryCommand<TEntity> ToCommand() => Select<TEntity>(typeof(TEntity));
    // public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => ToCommand().GetAsyncEnumerator(cancellationToken);

    /// <summary>
    /// Flattens the sequence produced by <paramref name="collectionSelector"/> for every row of this
    /// query (<c>source.SelectMany(...)</c>), yielding the selected elements.
    /// <para>
    /// Supported by the <c>InMemoryDataContext</c> only; SQL providers throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public EntityBuilder<TCollection> SelectMany<TCollection>(Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);
        EnsureInMemory(nameof(SelectMany));

        return CreateLinqSourceBuilder<TCollection>(new LinqSourceExpression
        {
            OuterCommand = ToCommand(),
            OuterType = typeof(TEntity),
            ResultType = typeof(TCollection),
            CollectionType = typeof(TCollection),
            CollectionSelector = collectionSelector
        });
    }

    /// <summary>
    /// Flattens the sequence produced by <paramref name="collectionSelector"/> for every row of this
    /// query and projects each pair with <paramref name="resultSelector"/>.
    /// <para>
    /// Supported by the <c>InMemoryDataContext</c> only; SQL providers throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public EntityBuilder<TResult> SelectMany<TCollection, TResult>(
        Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector,
        Expression<Func<TEntity, TCollection, TResult>> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        EnsureInMemory(nameof(SelectMany));

        return CreateLinqSourceBuilder<TResult>(new LinqSourceExpression
        {
            OuterCommand = ToCommand(),
            OuterType = typeof(TEntity),
            ResultType = typeof(TResult),
            CollectionType = typeof(TCollection),
            CollectionSelector = collectionSelector,
            ResultSelector = resultSelector
        });
    }

    /// <summary>
    /// Correlates the rows of this query with <paramref name="inner"/> by key and projects each pair
    /// with the grouped inner rows (<c>source.GroupJoin(...)</c>). Rows without a match receive an
    /// empty group.
    /// <para>
    /// Supported by the <c>InMemoryDataContext</c> only; SQL providers throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public EntityBuilder<TResult> GroupJoin<TInner, TKey, TResult>(
        EntityBuilder<TInner> inner,
        Expression<Func<TEntity, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TEntity, IEnumerable<TInner>, TResult>> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        EnsureInMemory(nameof(GroupJoin));

        return CreateLinqSourceBuilder<TResult>(new LinqSourceExpression
        {
            OuterCommand = ToCommand(),
            InnerCommand = inner.ToCommand(),
            OuterType = typeof(TEntity),
            InnerType = typeof(TInner),
            KeyType = typeof(TKey),
            ResultType = typeof(TResult),
            OuterKeySelector = outerKeySelector,
            InnerKeySelector = innerKeySelector,
            ResultSelector = resultSelector,
            IsGroupJoin = true
        });
    }

    private void EnsureInMemory(string method)
    {
        if (_dataProvider is not InMemoryDataContext)
            throw new NotSupportedException(
                $"{method} is not supported by this provider; it is only available on the in-memory provider. " +
                "SQL providers do not translate SelectMany/GroupJoin yet.");
    }

    private EntityBuilder<TResult> CreateLinqSourceBuilder<TResult>(LinqSourceExpression source)
    {
        var builder = new EntityBuilder<TResult>(_dataProvider) { Logger = Logger };
        builder._from = new FromExpression(source);
        return builder;
    }

    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Cross, null);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row (<c>CROSS APPLY</c> / <c>CROSS JOIN LATERAL</c>).
    /// There is no <c>ON</c> condition.
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.CrossApply, null);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row, preserving left-hand rows with an empty
    /// result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). There is no <c>ON</c> condition.
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        QueryCommand? query = null;
        if (_condition is not null || _query is not null)
        {
            query = ToCommand();
        }

        // A TVF (or other explicit source) on either side is carried as a FromExpression: the right
        // side keeps the joined entity's own source, the left side keeps the one propagated below.
        var cb = new JoinedEntityBuilder<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = _.SourceFrom ?? _dataProvider.GetFrom(typeof(TJoinEntity), null)!, EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, _query = query, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        cb.SourceFrom = SourceFrom;
        return cb;
    }
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Inner, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Left, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Right, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Full, joinCondition);
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.Cross, null);
    /// <summary>Applies a derived query to every left-hand row (<c>CROSS APPLY</c>).</summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.CrossApply, null);
    /// <summary>
    /// Applies a derived query to every left-hand row, preserving left-hand rows with an empty result
    /// (<c>OUTER APPLY</c>).
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.OuterApply, null);
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinCore<TJoinEntity>(QueryCommand<TJoinEntity> query, JoinType joinType, LambdaExpression? joinCondition)
    {
        QueryCommand? queryBase = null;
        if (_condition is not null || _query is not null)
        {
            queryBase = ToCommand();
        }

        var cb = new JoinedEntityBuilder<TEntity, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = new FromExpression(query), EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, _query = queryBase, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        cb.SourceFrom = SourceFrom;
        return cb;
    }
    public EntityBuilder<TEntity> GroupBy<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;

        return b;
    }
    /// <summary>
    /// Groups by <c>ROLLUP (columns)</c>: the grouped rows plus every prefix subtotal and the grand
    /// total. Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsRollup"/>).
    /// </summary>
    public EntityBuilder<TEntity> GroupByRollup<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;
        b.GroupingType = GroupingType.Rollup;

        return b;
    }
    /// <summary>
    /// Groups by <c>CUBE (columns)</c>: the grouped rows plus every combination subtotal. Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsCube"/>).
    /// </summary>
    public EntityBuilder<TEntity> GroupByCube<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var b = Clone();

        b._group = exp;
        b.GroupingType = GroupingType.Cube;

        return b;
    }
    /// <summary>
    /// Adds the <c>WITH TOTALS</c> modifier to the grouping (ClickHouse): an extra row with the totals
    /// over all groups. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsGroupByWithTotals"/>); it cannot be combined with
    /// <see cref="GroupByGroupingSets"/>. It is a no-op when the query has no grouping.
    /// </summary>
    public EntityBuilder<TEntity> WithTotals()
    {
        var b = Clone();

        b.GroupByWithTotals = true;

        return b;
    }
    /// <summary>
    /// Groups by an explicit list of grouping sets, e.g. <c>GROUP BY GROUPING SETS ((a, b), (a), ())</c>.
    /// <paramref name="exp"/> declares the full grouping list (an anonymous type / DTO) and each entry of
    /// <paramref name="sets"/> is a set of 0-based indices into that list; an empty set produces the
    /// grand total. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsGroupingSets"/>).
    /// </summary>
    public EntityBuilder<TEntity> GroupByGroupingSets<TResult>(Expression<Func<TEntity, TResult>> exp, params int[][] sets)
    {
        var b = Clone();

        b._group = exp;
        b.GroupingType = GroupingType.GroupingSets;
        b.GroupingSets = sets is { Length: > 0 } ? sets : null;

        return b;
    }
    /// <summary>
    /// Attaches table-level hints to the primary physical table, for example
    /// <c>From&lt;IComplexEntity&gt;().WithTableHint("nolock")</c> which renders
    /// <c>from complex_entity with (nolock)</c>. Requires a dialect that supports table hints (see
    /// <see cref="ISqlDialect.SupportsTableHints"/>); the hints are rendered verbatim, so only use
    /// trusted values.
    /// </summary>
    public EntityBuilder<TEntity> WithTableHint(params string[] hints)
    {
        var b = Clone();

        b.TableHints = hints is { Length: > 0 }
            ? hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : null;

        return b;
    }
    public EntityBuilder<TEntity> Having(Expression<Func<TEntity, bool>> condition)
    {
        var b = Clone();
        if (_having is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterExpressionVisitor(_having.Parameters[0]);
            var newBody = Expression.AndAlso(_having.Body, replVisitor.Visit(condition.Body));
            b._having = Expression.Lambda<Func<TEntity, bool>>(newBody, _having.Parameters[0]);
        }
        else
            b._having = condition;

        return b;
    }
    public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp, OrderDirection direction)
    {
        var b = Clone();
        if (b._sorting is null)
            b._sorting = [new Sorting(orderExp) { Direction = direction }];
        else
            b._sorting.Add(new Sorting(orderExp) { Direction = direction });
        return b;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Asc);
    public EntityBuilder<TEntity> OrderBy(int columnIdx, OrderDirection direction)
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
    public EntityBuilder<TEntity> OrderBy(int columnIdx) => OrderBy(columnIdx, OrderDirection.Asc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Desc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderByDescending(int columnIdx) => OrderBy(columnIdx, OrderDirection.Desc);
}

/// <summary>
/// Fluent query builder over a table addressed by name (<c>TableAlias</c> mode); created by
/// <c>IDataContext.From("table")</c>.
/// </summary>
public class EntityBuilder : ICloneable
{
    private readonly IDataContext _dataProvider;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression<Func<TableAlias, bool>>? _condition;
    private List<Sorting>? _sorting;
    protected List<JoinExpression>? _joins;
    /// <summary>CTE declarations that must be attached to commands this builder creates.</summary>
    internal IReadOnlyList<CteDefinition>? Ctes { get; set; }
    public EntityBuilder(IDataContext dataProvider) : this(dataProvider, null) { }
    public EntityBuilder(IDataContext dataProvider, string? table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TableAlias, TResult>> exp)
    {
        //if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        var cmd = new QueryCommand<TResult>(_dataProvider, new QueryDefinition
        {
            Exp = exp,
            Condition = _condition,
            Joins = _joins?.ToArray(),
            Sorting = _sorting?.ToArray(),
            Logger = Logger,
        });

        if (!string.IsNullOrEmpty(_table))
            cmd.From = new FromExpression(_table);

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        return cmd;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    public EntityBuilder Clone()
    {
        return (EntityBuilder)CloneImp();
    }
    protected virtual void CopyTo(EntityBuilder dst)
    {
        dst._condition = _condition;
        dst._sorting = _sorting;
        dst.Ctes = Ctes;
    }
    protected virtual object CloneImp()
    {
        var r = new EntityBuilder(_dataProvider, _table) { Logger = Logger };

        CopyTo(r);

        return r;
    }

    public EntityBuilder Where(Expression<Func<TableAlias, bool>> condition)
    {
        var b = Clone();
        if (_condition is not null && condition is not null)
        {
            var replVisitor = new ReplaceParameterExpressionVisitor(_condition.Parameters[0]);
            var newBody = Expression.AndAlso(_condition.Body, replVisitor.Visit(condition.Body));
            b._condition = Expression.Lambda<Func<TableAlias, bool>>(newBody, _condition.Parameters[0]);
        }
        else
            b._condition = condition;

        return b;
    }
    public JoinedEntityBuilder<TableAlias, TableAlias> Join(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Inner, joinCondition);
    public JoinedEntityBuilder<TableAlias, TableAlias> LeftJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Left, joinCondition);
    public JoinedEntityBuilder<TableAlias, TableAlias> RightJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Right, joinCondition);
    public JoinedEntityBuilder<TableAlias, TableAlias> FullJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Full, joinCondition);
    public JoinedEntityBuilder<TableAlias, TableAlias> CrossJoin(EntityBuilder from)
        => JoinCore(from, JoinType.Cross, null);
    public JoinedEntityBuilder<TableAlias, TableAlias> CrossApply(EntityBuilder from)
        => JoinCore(from, JoinType.CrossApply, null);
    public JoinedEntityBuilder<TableAlias, TableAlias> OuterApply(EntityBuilder from)
        => JoinCore(from, JoinType.OuterApply, null);
    private JoinedEntityBuilder<TableAlias, TableAlias> JoinCore(EntityBuilder from, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<TableAlias, TableAlias>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = new FromExpression(from._table!), EntityType = joinCondition is null ? typeof(TableAlias) : null }) { Logger = Logger, Table = _table, Ctes = Ctes };
        return cb;
    }
    public JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Cross, null);
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public JoinedEntityBuilder<TableAlias, TJoinEntity> OuterApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<TableAlias, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<TableAlias, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(TJoinEntity), null)!, EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, Table = _table, Ctes = Ctes };
        return cb;
    }
}
