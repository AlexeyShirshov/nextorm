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
    /// <summary>The data context this builder creates commands from and resolves sources against.</summary>
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
    private List<WindowDefinition>? _windows;
    private Type? _sourceEntityType;
    private bool _bindArrayJoinElement;
    private List<KeyValuePair<string, string>>? _settings;
    private Expression<Func<TEntity, bool>>? _having;
    private List<Sorting>? _sorting;
    /// <summary>The join clauses collected so far, or <c>null</c> when the query has no joins.</summary>
    protected List<JoinExpression>? _joins;
    private string? _table;
    private FromExpression? _from;
    #endregion
    /// <summary>
    /// Initializes a builder over the mapped entity type; the source is resolved from entity metadata.
    /// </summary>
    /// <param name="dataProvider">The data context that creates the query command.</param>
    public EntityBuilder(IDataContext dataProvider)
    {
        _dataProvider = dataProvider;
    }
    /// <summary>Initializes a builder whose source is the derived <paramref name="query"/>.</summary>
    /// <param name="dataProvider">The data context that creates the query command.</param>
    /// <param name="query">The derived query to select from.</param>
    public EntityBuilder(IDataContext dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    /// <summary>Initializes a builder over the raw table named <paramref name="table"/>.</summary>
    /// <param name="dataProvider">The data context that creates the query command.</param>
    /// <param name="table">The physical table name to select from.</param>
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
    /// <summary>
    /// The ordering keys applied by <c>OrderBy</c>/<c>OrderByDescending</c>, or <c>null</c> when the
    /// query is unordered.
    /// </summary>
    public List<Sorting>? Sorting { get => _sorting; set => _sorting = value; }
    /// <summary>The join clauses applied to the query, or <c>null</c> when there are none.</summary>
    public List<JoinExpression>? Joins { get => _joins; set => _joins = value; }
    /// <summary>The limit/offset paging state set by <c>Limit</c>, <c>Offset</c> and <c>Page</c>.</summary>
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
    /// <summary>The named window definitions declared for this query, or <c>null</c> when there are none.</summary>
    internal IReadOnlyList<WindowDefinition>? Windows { get => _windows; set => _windows = value is null ? null : [.. value]; }
    /// <summary>The <c>ARRAY JOIN</c> kind (plain or <c>LEFT</c>) shared by <see cref="ArrayJoins"/>.</summary>
    internal ArrayJoinKind ArrayJoinKind { get => _arrayJoinKind; set => _arrayJoinKind = value; }
    /// <summary>Table-level hints applied to the primary physical table (for example SQL Server <c>nolock</c>).</summary>
    internal IReadOnlyList<string>? TableHints { get; set; }
    /// <summary>Index-hint index names applied to the primary physical table, or <c>null</c> when there are none.</summary>
    internal IReadOnlyList<string>? IndexHints { get; set; }
    /// <summary>The intent of <see cref="IndexHints"/> (<c>USE</c>, <c>FORCE</c> or <c>IGNORE</c>).</summary>
    internal IndexHintKind IndexHintKind { get; set; }
    /// <summary>
    /// Per-query override of identifier quoting (<c>null</c> inherits the context default). Set through
    /// <see cref="WithQuotedIdentifiers"/>.
    /// </summary>
    internal bool? QuoteIdentifiers { get; set; }
    /// <summary>
    /// Per-query override of the naming convention for auto-derived table/column names (<c>null</c>
    /// inherits the context default). Set through <see cref="WithNamingConvention"/>.
    /// </summary>
    internal INamingConvention? NamingConvention { get; set; }
    /// <summary>
    /// Per-query override of the SQL keyword case (<c>null</c> inherits the context default). Set
    /// through <see cref="WithKeywordCase"/>.
    /// </summary>
    internal KeywordCase? KeywordCase { get; set; }
    /// <summary>
    /// Query tag rendered as a block comment (<c>/* tag */</c>) right after <c>SELECT</c>. Set through
    /// <see cref="WithTag"/>; <c>null</c> means no tag.
    /// </summary>
    internal string? Tag { get; set; }
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

    /// <summary>
    /// Projects each row with <paramref name="exp"/> and creates the SELECT command for the query,
    /// carrying every modifier set on this builder.
    /// </summary>
    /// <typeparam name="TResult">The projection type produced by <paramref name="exp"/>.</typeparam>
    /// <param name="exp">The projection expression.</param>
    /// <returns>The command that produces <typeparamref name="TResult"/> rows.</returns>
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
            Windows = _windows,
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
        cmd.IndexHints = IndexHints;
        cmd.IndexHintKind = IndexHintKind;
        cmd.QuoteIdentifiers = QuoteIdentifiers;
        cmd.NamingConvention = NamingConvention;
        cmd.KeywordCase = KeywordCase;
        cmd.Tag = Tag;
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
    /// <summary>
    /// Creates a SELECT command over the entity type without a custom projection, carrying every
    /// modifier set on this builder.
    /// </summary>
    /// <returns>The command that produces <typeparamref name="TEntity"/> rows.</returns>
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
            Windows = _windows,
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
        cmd.IndexHints = IndexHints;
        cmd.IndexHintKind = IndexHintKind;
        cmd.QuoteIdentifiers = QuoteIdentifiers;
        cmd.NamingConvention = NamingConvention;
        cmd.KeywordCase = KeywordCase;
        cmd.Tag = Tag;
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

    /// <summary>
    /// Adds <paramref name="condition"/> to the WHERE clause. A repeated call combines the predicates
    /// with <c>and</c>. Returns a new builder; the current one is unchanged.
    /// </summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>A builder with the added predicate.</returns>
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
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.ArrayJoinClause"/>).
    /// </summary>
    public EntityBuilder<TEntity> ArrayJoin<TArray>(Expression<Func<TEntity, TArray>> array)
        => AddArrayJoin(array, ArrayJoinKind.Inner);

    /// <summary>
    /// Adds the ClickHouse <c>LEFT ARRAY JOIN</c> clause: like <see cref="ArrayJoin{TArray}"/> but a row
    /// whose array is empty is kept (with the array column at its default). Requires a dialect that
    /// supports it (see <see cref="ISqlDialect.ArrayJoinClause"/>).
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
    /// clause (see <see cref="ISqlDialect.ArrayJoinClause"/>).
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
    /// (see <see cref="ISqlDialect.ArrayJoinClause"/>).
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

        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be applied before ArrayJoinElement/LeftArrayJoinElement.");

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
    /// <summary>
    /// Adds <c>SELECT DISTINCT</c>, removing duplicate result rows. Cannot be combined with
    /// <see cref="DistinctOn{TResult}"/>.
    /// </summary>
    /// <returns>A builder with the DISTINCT modifier.</returns>
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
    /// <see cref="ISqlDialect.DistinctOn"/>).
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
    /// Declares a named window <c>w AS (PARTITION BY ... ORDER BY ... frame)</c> on this query. Window
    /// functions reference it with <c>Over("w")</c>; every declared window renders in a single
    /// <c>WINDOW</c> clause after <c>GROUP BY</c>/<c>HAVING</c>. Requires a dialect that supports named
    /// windows (see <see cref="ISqlDialect.SupportsNamedWindows"/>). Declare windows after any join, on
    /// the final builder.
    /// </summary>
    /// <param name="name">The window name (a simple SQL identifier).</param>
    /// <param name="partitionBy">Optional <c>PARTITION BY</c> key expressions.</param>
    /// <param name="orderBy">Optional <c>ORDER BY</c> keys, built with <see cref="Asc"/> or <see cref="Desc"/>.</param>
    /// <param name="frame">Optional frame.</param>
    public EntityBuilder<TEntity> Window(
        string name,
        Expression<Func<TEntity, object?>>[]? partitionBy = null,
        NamedWindowOrderKey[]? orderBy = null,
        WindowFrame? frame = null)
    {
        if (!WindowSql.IsValidWindowName(name))
            throw new ArgumentException("A window name must be a non-empty SQL identifier.", nameof(name));

        if (_windows is not null)
        {
            for (var i = 0; i < _windows.Count; i++)
            {
                if (string.Equals(_windows[i].Name, name, StringComparison.Ordinal))
                    throw new InvalidOperationException($"A window named '{name}' is already declared on this query.");
            }
        }

        IReadOnlyList<LambdaExpression> partitions = partitionBy ?? [];
        IReadOnlyList<NamedWindowOrderKey> keys = orderBy ?? [];

        var b = Clone();
        (b._windows ??= []).Add(new WindowDefinition(name, partitions, keys, frame));

        return b;
    }

    /// <summary>Ascending <c>ORDER BY</c> key of a named window declared with <see cref="Window"/>.</summary>
    public NamedWindowOrderKey Asc(Expression<Func<TEntity, object?>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new NamedWindowOrderKey(expression, OrderDirection.Asc);
    }

    /// <summary>Descending <c>ORDER BY</c> key of a named window declared with <see cref="Window"/>.</summary>
    public NamedWindowOrderKey Desc(Expression<Func<TEntity, object?>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new NamedWindowOrderKey(expression, OrderDirection.Desc);
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
    /// Reshapes this query's source into columns with the native <c>PIVOT</c> operator: for every
    /// distinct <paramref name="forColumn"/> value in <paramref name="values"/> a result column is
    /// produced from <paramref name="aggregate"/> of <paramref name="aggregateColumn"/>. The result is
    /// an untyped source (<see cref="TableAlias"/>): select the grouping columns and the pivoted columns
    /// by name. Each result column is named by its pivot value, so quote non-identifier values in the
    /// projection (<c>Q1 = t.GetNullableDecimal("[1]")</c>).
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.Pivot"/>). The source may
    /// be a plain table/entity, a table-valued function or a derived query (<c>From(query)</c>); filters
    /// and other modifiers belong to the reshaped result (or, for a derived source, inside the derived
    /// query).
    /// </summary>
    /// <param name="aggregate">The aggregate applied to each cell (<c>SUM</c>/<c>COUNT</c>/<c>AVG</c>/<c>MIN</c>/<c>MAX</c>).</param>
    /// <param name="aggregateColumn">The column expression aggregated into each cell.</param>
    /// <param name="forColumn">The column whose values become the result columns.</param>
    /// <param name="values">The pivot values; each value names its result column.</param>
    public EntityBuilder<TableAlias> Pivot(
        PivotAggregate aggregate,
        Expression<Func<TEntity, object?>> aggregateColumn,
        Expression<Func<TEntity, object?>> forColumn,
        params PivotValue[] values)
    {
        ArgumentNullException.ThrowIfNull(aggregateColumn);
        ArgumentNullException.ThrowIfNull(forColumn);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("A PIVOT requires at least one value.", nameof(values));

        var spec = PivotExpression.ForPivot(ResolvePivotInner(), typeof(TEntity), aggregate, aggregateColumn, forColumn, values);
        return new EntityBuilder<TableAlias>(_dataProvider) { Logger = Logger, SourceFrom = new FromExpression(spec) };
    }

    /// <summary>
    /// Reshapes this query's source with the native <c>UNPIVOT</c> operator: the listed columns are
    /// stacked into two result columns (<paramref name="valueColumnName"/> holding the value and
    /// <paramref name="nameColumnName"/> holding the originating column name). Requires a dialect that
    /// supports it (see <see cref="ISqlDialect.Pivot"/>). The source may be a plain
    /// table/entity or a derived query (<c>From(query)</c>).
    /// </summary>
    /// <param name="valueColumnName">The name of the output column that receives the values.</param>
    /// <param name="nameColumnName">The name of the output column that receives the source column name.</param>
    /// <param name="columns">The source columns to unpivot; each column's name becomes its name-column value.</param>
    public EntityBuilder<TableAlias> Unpivot(
        string valueColumnName,
        string nameColumnName,
        params UnpivotColumn[] columns)
    {
        ArgumentException.ThrowIfNullOrEmpty(valueColumnName);
        ArgumentException.ThrowIfNullOrEmpty(nameColumnName);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
            throw new ArgumentException("An UNPIVOT requires at least one column.", nameof(columns));

        var spec = PivotExpression.ForUnpivot(ResolvePivotInner(), typeof(TEntity), valueColumnName, nameColumnName, columns);
        return new EntityBuilder<TableAlias>(_dataProvider) { Logger = Logger, SourceFrom = new FromExpression(spec) };
    }

    /// <summary>
    /// Resolves the source a <c>PIVOT</c>/<c>UNPIVOT</c> wraps. A plain table/entity mapping, a
    /// table-valued function and a derived query (<c>From(query)</c>) are allowed; the native operators
    /// apply to a table expression, so any filter, join, grouping, ordering, paging or table modifier on
    /// this builder must be applied to the reshaped result (or, for a derived source, moved into the
    /// derived query).
    /// </summary>
    private FromExpression ResolvePivotInner()
    {
        if (_joins is { Count: > 0 } || _condition is not null || _having is not null
            || _group is not null || _sorting is { Count: > 0 } || !Paging.IsEmpty || IsDistinct
            || _tablesample is not null || _temporal is not null || _rowLock is not null || _preWhere is not null
            || _arrayJoins is { Count: > 0 } || _settings is { Count: > 0 } || _limitBy is not null
            || _distinctOn is not null || _windows is { Count: > 0 } || IsFinal || SampleRatio is not null
            || TableHints is { Count: > 0 } || IndexHints is not null || Ctes is { Count: > 0 })
            throw new NotSupportedException(_query is not null
                ? "PIVOT/UNPIVOT over a derived query accepts no modifiers on the pivot builder; apply filters, joins, grouping, ordering, paging and table modifiers inside the derived query or to the reshaped result."
                : "PIVOT/UNPIVOT can only be applied to a plain table/entity source, a table-valued function or a derived query; apply filters, joins, grouping, ordering, paging and table modifiers to the reshaped result.");

        if (_query is not null)
            return new FromExpression(_query);

        if (_from is not null)
            return _from;

        if (!string.IsNullOrEmpty(_table))
            return new FromExpression(_table);

        return _dataProvider.GetFrom(typeof(TEntity), null)
            ?? throw new BuildSqlCommandException($"A PIVOT/UNPIVOT source could not be resolved for type {typeof(TEntity)}.");
    }

    /// <summary>
    /// Locks the selected rows exclusively until the transaction ends, blocking while a locked row is held
    /// by another transaction. Renders a trailing <c>FOR UPDATE</c> clause on dialects that support it, or a
    /// table hint on the primary source (<c>with (updlock)</c> on SQL Server). Requires a dialect that
    /// supports row locking (see <see cref="ISqlDialect.Lock"/>).
    /// </summary>
    public EntityBuilder<TEntity> ForUpdate() => WithRowLock(LockMode.Update, LockWaitMode.Wait);

    /// <summary>
    /// Locks the selected rows exclusively until the transaction ends, with the requested wait behaviour for
    /// rows already locked by another transaction. Renders a trailing <c>FOR UPDATE [NOWAIT | SKIP LOCKED]</c>
    /// clause on dialects that support it, or the matching table hint on the primary source
    /// (<c>with (updlock, nowait)</c>/<c>with (updlock, readpast)</c> on SQL Server). Requires a dialect that
    /// supports row locking (see <see cref="ISqlDialect.Lock"/>).
    /// </summary>
    /// <param name="wait">How to react to a row already locked by another transaction.</param>
    public EntityBuilder<TEntity> ForUpdate(LockWaitMode wait) => WithRowLock(LockMode.Update, wait);

    /// <summary>
    /// Locks the selected rows in shared mode until the transaction ends, blocking while a locked row is held
    /// by another transaction. Renders a trailing <c>FOR SHARE</c> clause on dialects that support it, or a
    /// table hint on the primary source (<c>with (holdlock)</c> on SQL Server). Requires a dialect that
    /// supports row locking (see <see cref="ISqlDialect.Lock"/>).
    /// </summary>
    public EntityBuilder<TEntity> ForShare() => WithRowLock(LockMode.Share, LockWaitMode.Wait);

    /// <summary>
    /// Locks the selected rows in shared mode until the transaction ends, with the requested wait behaviour
    /// for rows already locked by another transaction. Renders a trailing <c>FOR SHARE [NOWAIT | SKIP LOCKED]</c>
    /// clause on dialects that support it, or the matching table hint on the primary source
    /// (<c>with (holdlock, nowait)</c>/<c>with (holdlock, readpast)</c> on SQL Server). Requires a dialect that
    /// supports row locking (see <see cref="ISqlDialect.Lock"/>).
    /// </summary>
    /// <param name="wait">How to react to a row already locked by another transaction.</param>
    public EntityBuilder<TEntity> ForShare(LockWaitMode wait) => WithRowLock(LockMode.Share, wait);

    private EntityBuilder<TEntity> WithRowLock(LockMode mode, LockWaitMode wait)
    {
        var b = Clone();
        b._rowLock = new LockClause(mode, wait);
        return b;
    }
    /// <summary>Sets the maximum number of rows to return; zero means no limit.</summary>
    /// <param name="limit">The page size; must be non-negative.</param>
    /// <returns>A builder with the new limit.</returns>
    public EntityBuilder<TEntity> Limit(int limit)
    {
        var b = Clone();

        b.Paging.Limit = limit;

        return b;
    }
    /// <summary>Sets the number of leading rows to skip; zero starts from the first row.</summary>
    /// <param name="offset">The number of rows to skip; must be non-negative.</param>
    /// <returns>A builder with the new offset.</returns>
    public EntityBuilder<TEntity> Offset(int offset)
    {
        var b = Clone();

        b.Paging.Offset = offset;

        return b;
    }
    /// <summary>Sets both the page size and the start position in a single call.</summary>
    /// <param name="limit">The page size; must be non-negative.</param>
    /// <param name="offset">The number of leading rows to skip; must be non-negative.</param>
    /// <returns>A builder with the new limit and offset.</returns>
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
    /// <see cref="ISqlDialect.LimitBy"/>).
    /// </summary>
    public EntityBuilder<TEntity> LimitBy<TResult>(int limit, Expression<Func<TEntity, TResult>> exp)
        => LimitBy(limit, 0, exp);
    /// <summary>
    /// Adds a <c>LIMIT offset, n BY expr</c> clause (ClickHouse): skips <paramref name="offset"/> rows
    /// and then returns at most <paramref name="limit"/> rows per distinct key. <paramref name="limit"/>
    /// must be positive and <paramref name="offset"/> non-negative. Requires a dialect that supports it
    /// (see <see cref="ISqlDialect.LimitBy"/>).
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
    /// <summary>
    /// Returns a copy of this builder. Because the modifier methods already return copies, this is
    /// useful to fork a query without applying a modifier.
    /// </summary>
    /// <returns>A new builder with the same state.</returns>
    public EntityBuilder<TEntity> Clone()
    {
        return (EntityBuilder<TEntity>)CloneImp();
    }
    /// <summary>
    /// Copies this builder's state onto <paramref name="dst"/>; called by <see cref="CloneImp"/>.
    /// Protected so derived builders can extend the copy with their own state.
    /// </summary>
    /// <param name="dst">The builder that receives the state.</param>
    protected virtual void CopyTo(EntityBuilder<TEntity> dst)
    {
        CopyProjectionIndependentStateTo(dst);
        dst._condition = _condition;
        dst._having = _having;
        dst._arrayJoins = _arrayJoins is null ? null : [.. _arrayJoins];
        dst._windows = _windows is null ? null : [.. _windows];
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
        dst.IndexHints = IndexHints;
        dst.IndexHintKind = IndexHintKind;
        dst.QuoteIdentifiers = QuoteIdentifiers;
        dst.NamingConvention = NamingConvention;
        dst.KeywordCase = KeywordCase;
        dst.Tag = Tag;
        dst.Ctes = Ctes;
    }
    /// <summary>
    /// Creates the copy returned by <see cref="Clone"/> and the explicit <c>ICloneable.Clone</c> call.
    /// Overridden by derived builders to clone their extra state.
    /// </summary>
    /// <returns>A new builder with the same state.</returns>
    protected virtual object CloneImp()
    {
        var r = new EntityBuilder<TEntity>(_dataProvider) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    /// <summary>
    /// Implicitly converts the builder to its command, so a builder can be passed where a
    /// <see cref="QueryCommand{TEntity}"/> is expected.
    /// </summary>
    /// <param name="builder">The builder to convert.</param>
    /// <returns>The command built by <see cref="ToCommand"/>.</returns>
    public static implicit operator QueryCommand<TEntity>(EntityBuilder<TEntity> builder) => builder.ToCommand();
    /// <summary>
    /// Implicitly converts the builder to its untyped command, so a builder can be passed where a
    /// <see cref="QueryCommand"/> is expected.
    /// </summary>
    /// <param name="builder">The builder to convert.</param>
    /// <returns>The command built by <see cref="ToCommand"/>.</returns>
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

    /// <summary>
    /// Adds an inner join to <paramref name="_"/>, using <paramref name="joinCondition"/> as the
    /// <c>ON</c> predicate; only matching pairs survive.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    /// <summary>
    /// Adds a left outer join: every left-hand row is kept, and right-hand columns are <c>NULL</c>
    /// when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    /// <summary>
    /// Adds a right outer join: every right-hand row is kept, and left-hand columns are <c>NULL</c>
    /// when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    /// <summary>
    /// Adds a full outer join: unmatched rows from both sides are kept, with the other side's columns
    /// set to <c>NULL</c>.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    /// <summary>
    /// Adds a cross join producing the Cartesian product of the two sources; there is no <c>ON</c>
    /// condition.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <returns>A builder over the joined projection.</returns>
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
    /// <summary>
    /// Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/> and returns this builder
    /// unchanged in shape: only the left-hand columns survive, and a left-hand row is kept once when at
    /// least one right-hand row matches. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsSemiAntiJoin"/>).
    /// </summary>
    public EntityBuilder<TEntity> SemiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => AddSemiAntiJoin(GetJoinSource(_), joinCondition, JoinType.Semi);
    /// <summary>
    /// Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>: only the left-hand columns
    /// survive, and a left-hand row is kept when no right-hand row matches (the complement of
    /// <see cref="SemiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}})"/>).
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsSemiAntiJoin"/>).
    /// </summary>
    public EntityBuilder<TEntity> AntiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => AddSemiAntiJoin(GetJoinSource(_), joinCondition, JoinType.Anti);
    /// <summary>
    /// Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>: the two sources are paired by row
    /// position with no <c>ON</c> condition, and the projection exposes both sides (as many rows as the
    /// shorter side). Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsPasteJoin"/>).
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Paste, null);
    /// <inheritdoc cref="SemiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}})"/>
    public EntityBuilder<TEntity> SemiJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => AddSemiAntiJoin(new FromExpression(query), joinCondition, JoinType.Semi);
    /// <inheritdoc cref="AntiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}})"/>
    public EntityBuilder<TEntity> AntiJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => AddSemiAntiJoin(new FromExpression(query), joinCondition, JoinType.Anti);
    /// <inheritdoc cref="PasteJoin{TJoinEntity}(EntityBuilder{TJoinEntity})"/>
    public JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TJoinEntity>(QueryCommand<TJoinEntity> query)
        => JoinCore(query, JoinType.Paste, null);
    private EntityBuilder<TEntity> AddSemiAntiJoin(FromExpression rightSource, LambdaExpression joinCondition, JoinType joinType)
    {
        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var b = Clone();
        var joins = b._joins;
        if (joins is null)
            b._joins = joins = _joins is null ? [] : [.. _joins];

        joins.Add(new JoinExpression(joinCondition, joinType) { From = rightSource, EntityType = null });

        return b;
    }
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        // A TVF (or other explicit source) on either side is carried as a FromExpression: the right
        // side keeps the joined entity's own source, the left side keeps the one propagated below.
        var joined = CreateJoined<TJoinEntity>(new JoinExpression(joinCondition, joinType) { From = GetJoinSource(_), EntityType = joinCondition is null ? typeof(TJoinEntity) : null }, ResolveJoinBase());
        joined.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        ApplyWhereToJoined(joined);
        return joined;
    }

    /// <summary>
    /// Resolves the joined side of a <c>Join</c>. A builder over a raw table or CTE name (the
    /// <c>From(string)</c> / CTE shape) carries the name in <see cref="Table"/> rather than a
    /// <see cref="FromExpression"/>, so it must be turned back into a table source here; otherwise the
    /// join would fall back to entity metadata that does not exist for <see cref="TableAlias"/>.
    /// </summary>
    private FromExpression GetJoinSource<TJoinEntity>(EntityBuilder<TJoinEntity> builder)
        => JoinSourceResolver.Resolve(_dataProvider, builder);

    /// <summary>
    /// Adds an inner join to the derived <paramref name="query"/>, using
    /// <paramref name="joinCondition"/> as the <c>ON</c> predicate; only matching pairs survive.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Inner, joinCondition);
    /// <summary>
    /// Adds a left outer join to the derived <paramref name="query"/>: every left-hand row is kept,
    /// and right-hand columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Left, joinCondition);
    /// <summary>
    /// Adds a right outer join to the derived <paramref name="query"/>: every right-hand row is kept,
    /// and left-hand columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Right, joinCondition);
    /// <summary>
    /// Adds a full outer join to the derived <paramref name="query"/>: unmatched rows from both sides
    /// are kept, with the other side's columns set to <c>NULL</c>.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => JoinCore(query, JoinType.Full, joinCondition);
    /// <summary>
    /// Adds a cross join to the derived <paramref name="query"/>, producing the Cartesian product of
    /// the two sources; there is no <c>ON</c> condition.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <returns>A builder over the joined projection.</returns>
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
    /// <summary>
    /// Applies a correlated derived query to every left-hand row (<c>CROSS APPLY</c> /
    /// <c>CROSS JOIN LATERAL</c>). <paramref name="source"/> receives the left-hand row, so the query
    /// it builds may reference its columns; there is no <c>ON</c> condition.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source)
        => JoinApply<TJoinEntity>(source, JoinType.CrossApply);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row, preserving left-hand rows with an
    /// empty result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). <paramref name="source"/>
    /// receives the left-hand row, so the query it builds may reference its columns.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source)
        => JoinApply<TJoinEntity>(source, JoinType.OuterApply);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row (<c>CROSS APPLY</c> /
    /// <c>CROSS JOIN LATERAL</c>). <paramref name="source"/> receives the left-hand row, so the query
    /// it builds may reference its columns; there is no <c>ON</c> condition.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source)
        => JoinApply<TJoinEntity>(source, JoinType.CrossApply);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row, preserving left-hand rows with an
    /// empty result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). <paramref name="source"/>
    /// receives the left-hand row, so the query it builds may reference its columns.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source)
        => JoinApply<TJoinEntity>(source, JoinType.OuterApply);
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinApply<TJoinEntity>(LambdaExpression source, JoinType joinType)
    {
        if (typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                "A correlated CROSS/OUTER APPLY source cannot reference a join projection; apply it to a single-entity source instead.");

        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "A correlated CROSS/OUTER APPLY source is not supported by the in-memory provider; run the query against a SQL provider.");

        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        QueryCommand? queryBase = ResolveJoinBase();

        var body = source.Body.Type == typeof(QueryCommand) ? source.Body : Expression.Convert(source.Body, typeof(QueryCommand));
        var applySource = Expression.Lambda(body, source.Parameters);

        var joined = CreateJoined<TJoinEntity>(new JoinExpression(null, joinType)
        {
            From = new FromExpression(typeof(TJoinEntity)),
            EntityType = typeof(TJoinEntity),
            ApplySource = applySource
        }, queryBase);
        ApplyWhereToJoined(joined);
        return joined;
    }
    /// <summary>
    /// Wraps a join into a <see cref="JoinedEntityBuilder{TEntity, TJoinEntity}"/>, carrying this
    /// builder's query modifiers (table, grouping, limits, hints, ...) onto the joined builder. Shared
    /// by the join and apply variants so they stay in sync.
    /// </summary>
    private JoinedEntityBuilder<TEntity, TJoinEntity> CreateJoined<TJoinEntity>(JoinExpression join, QueryCommand? query)
    {
        var cb = new JoinedEntityBuilder<TEntity, TJoinEntity>(_dataProvider, join) { Logger = Logger, Table = Table, _query = query, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        cb.SourceFrom = SourceFrom;
        return cb;
    }
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinCore<TJoinEntity>(QueryCommand<TJoinEntity> query, JoinType joinType, LambdaExpression? joinCondition)
    {
        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        QueryCommand? queryBase = ResolveJoinBase();

        var joined = CreateJoined<TJoinEntity>(new JoinExpression(joinCondition, joinType) { From = new FromExpression(query), EntityType = joinCondition is null ? typeof(TJoinEntity) : null }, queryBase);
        ApplyWhereToJoined(joined);
        return joined;
    }
    /// <summary>
    /// Resolves the left-hand command a <c>Join</c>/<c>Apply</c> builds on. A derived query
    /// (<c>From(query)</c>) is used directly as the join source: wrapping it in an extra subquery would
    /// hide its projection, and an anonymous projection has no entity metadata to fall back on. Only a
    /// <c>Where</c> may precede the join (it is carried onto the projection by
    /// <see cref="ApplyWhereToJoined{TJoinEntity}"/>); any other modifier must be moved into the derived
    /// query, because relocating it past the join would change the semantics.
    /// </summary>
    private QueryCommand? ResolveJoinBase()
    {
        if (_query is null)
            return _condition is not null ? ToCommand() : null;

        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "A derived query as the primary FROM source cannot be joined by the in-memory provider.");

        if (typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                "A derived query as the primary FROM source can be joined only once; add further joins to the derived query instead.");

        if (HasNoNonWhereModifiers())
            return _query;

        throw new NotSupportedException(
            "A derived query as the primary FROM source supports only a Where clause before Join; put OrderBy/GroupBy/Distinct and other modifiers into the derived query.");
    }
    /// <summary>
    /// True when no builder modifier other than <c>Where</c> is set. Such a modifier would be relocated
    /// past the join by <c>CreateJoined</c>, changing the query's semantics, so it must be rejected
    /// instead of being silently applied on the joined projection.
    /// </summary>
    private bool HasNoNonWhereModifiers()
        => !IsDistinct && !IsFinal && SampleRatio is null && SampleOffset == 0
        && _group is null && _having is null && _sorting is null && _preWhere is null
        && _arrayJoins is null && _windows is null && _limitBy is null && _distinctOn is null
        && _tablesample is null && _temporal is null && _rowLock is null && _settings is null
        && Paging.IsEmpty;
    /// <summary>
    /// Moves a <c>Where</c> written before the join onto the joined projection: the derived source
    /// becomes the projected item 1, so <c>d =&gt; d.X</c> is rewritten to <c>p =&gt; p.Item1.X</c> and
    /// resolved by the ordinary projection-column machinery. Only the exact lambda parameter is
    /// replaced (not every parameter of the same type), so a nested lambda over the same type keeps
    /// referring to its own parameter.
    /// </summary>
    private void ApplyWhereToJoined<TJoinEntity>(JoinedEntityBuilder<TEntity, TJoinEntity> joined)
    {
        if (_query is null || _condition is null)
            return;

        var param = Expression.Parameter(typeof(Projection<TEntity, TJoinEntity>), _condition.Parameters[0].Name);
        var item1 = Expression.Property(param, nameof(Projection<TEntity, TJoinEntity>.Item1));
        var body = new ReplaceTargetParameterVisitor(_condition.Parameters[0], item1).Visit(_condition.Body);
        joined.Condition = Expression.Lambda<Func<Projection<TEntity, TJoinEntity>, bool>>(body, param);
    }
    /// <summary>
    /// Replaces one specific parameter node (by reference) inside an expression, leaving parameters of
    /// the same type untouched.
    /// </summary>
    private sealed class ReplaceTargetParameterVisitor(ParameterExpression target, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => ReferenceEquals(node, target) ? replacement : base.VisitParameter(node);
    }
    /// <summary>
    /// Groups rows by <paramref name="exp"/>, a single column or an anonymous type/DTO keying on
    /// several columns.
    /// </summary>
    /// <typeparam name="TResult">The grouping key type.</typeparam>
    /// <param name="exp">The grouping key selector.</param>
    /// <returns>A builder with the grouping applied.</returns>
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
    /// <summary>
    /// Attaches an index hint to the primary physical table, asking the planner to consider
    /// <paramref name="indexes"/> (<c>USE INDEX</c> on MySQL/MariaDB, <c>INDEXED BY</c> on SQLite,
    /// <c>WITH (INDEX(...))</c> on SQL Server). Requires a dialect that supports index hints (see
    /// <see cref="ISqlDialect.IndexHints"/>); a dialect without a native form rejects the command with
    /// <see cref="NotSupportedException"/>. The names are emitted verbatim, so only use trusted values.
    /// Use the overload taking an <see cref="IndexHintKind"/> to force or ignore the indexes.
    /// </summary>
    /// <param name="indexes">The index names to hint; never empty.</param>
    /// <returns>A builder with the index hint applied.</returns>
    public EntityBuilder<TEntity> WithIndex(params string[] indexes)
        => WithIndex(IndexHintKind.Use, indexes);
    /// <summary>
    /// Attaches an index hint with an explicit <paramref name="kind"/> to the primary physical table.
    /// With <see cref="IndexHintKind.Ignore"/> and no names, SQLite renders <c>NOT INDEXED</c>. Requires
    /// a dialect that supports index hints (see <see cref="ISqlDialect.IndexHints"/>).
    /// </summary>
    /// <param name="kind">Whether to use, force or ignore the indexes.</param>
    /// <param name="indexes">The index names to hint; may be empty for <see cref="IndexHintKind.Ignore"/>.</param>
    /// <returns>A builder with the index hint applied.</returns>
    public EntityBuilder<TEntity> WithIndex(IndexHintKind kind, params string[] indexes)
    {
        var b = Clone();

        var names = indexes is { Length: > 0 }
            ? indexes.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray()
            : [];
        b.IndexHints = names.Length > 0 ? names : (kind == IndexHintKind.Ignore ? [] : null);
        b.IndexHintKind = kind;

        return b;
    }
    /// <summary>
    /// Tells the SQLite planner not to use any index (<c>NOT INDEXED</c>), or asks other dialects to
    /// ignore every named index. Requires a dialect that supports index hints (see
    /// <see cref="ISqlDialect.IndexHints"/>).
    /// </summary>
    /// <returns>A builder with the index suppression applied.</returns>
    public EntityBuilder<TEntity> WithoutIndex()
        => WithIndex(IndexHintKind.Ignore);
    /// <summary>
    /// Overrides identifier quoting for the commands this builder creates: when
    /// <paramref name="value"/> is <c>true</c>, physical table and column names are quoted with the
    /// provider's delimiter (<c>"id"</c> on PostgreSQL/SQLite, <c>[id]</c> on SQL Server,
    /// `` `id` `` on MySQL/MariaDB/ClickHouse); when <c>false</c>, names are emitted verbatim.
    /// Without a call the command inherits the context default set with
    /// <c>DataContextBuilder.UseQuotedIdentifiers</c>.
    /// </summary>
    public EntityBuilder<TEntity> WithQuotedIdentifiers(bool value = true)
    {
        var b = Clone();

        b.QuoteIdentifiers = value;

        return b;
    }
    /// <summary>
    /// Overrides the naming convention applied to auto-derived table/column names for the commands
    /// this builder creates (see <c>DataContextBuilder.UseNamingConvention</c> for the context default).
    /// </summary>
    public EntityBuilder<TEntity> WithNamingConvention(INamingConvention? convention)
    {
        var b = Clone();

        b.NamingConvention = convention;

        return b;
    }
    /// <summary>
    /// Overrides the letter case in which the SQL keywords of the commands this builder creates are
    /// emitted (see <c>DataContextBuilder.UseKeywordCase</c> for the context default).
    /// <see cref="KeywordCase.Upper"/> renders <c>SELECT ... FROM ...</c>; identifiers, string literals,
    /// function names, type names and raw SQL are never affected.
    /// </summary>
    /// <param name="keywordCase">The keyword case to apply.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithKeywordCase(KeywordCase keywordCase = global::NextORM.Core.KeywordCase.Upper)
    {
        var b = Clone();

        b.KeywordCase = keywordCase;

        return b;
    }
    /// <summary>
    /// Convenience form of <see cref="WithKeywordCase"/>: enables (<paramref name="value"/> is
    /// <see langword="true"/>) or disables upper-case SQL keywords for the commands this builder creates.
    /// </summary>
    /// <param name="value"><see langword="true"/> to emit keywords in upper case; otherwise lower case.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithUppercaseKeywords(bool value = true)
        => WithKeywordCase(value ? global::NextORM.Core.KeywordCase.Upper : global::NextORM.Core.KeywordCase.Lower);
    /// <summary>
    /// Attaches a query tag to the commands this builder creates. The tag is rendered as a block comment
    /// (<c>/* tag */</c>) immediately after the <c>SELECT</c> keyword on every SQL provider, so the
    /// statement is identifiable in logs, profilers and server-side query stores. It is not an optimizer
    /// hint (use <see cref="QueryCommand{TResult}.Hint(System.String[])"/> for that). Passing a new tag
    /// returns a new builder; <c>null</c> or an empty string clears it. The tag is part of the plan key.
    /// </summary>
    /// <param name="tag">The tag text to render, or <c>null</c> to clear the tag.</param>
    /// <returns>A builder with the tag applied.</returns>
    public EntityBuilder<TEntity> WithTag(string? tag)
    {
        var b = Clone();

        b.Tag = string.IsNullOrEmpty(tag) ? null : tag;

        return b;
    }
    /// <summary>
    /// Adds <paramref name="condition"/> to the HAVING clause, which filters grouped rows. A repeated
    /// call combines the predicates with <c>and</c>.
    /// </summary>
    /// <param name="condition">The predicate each group must satisfy.</param>
    /// <returns>A builder with the added predicate.</returns>
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
    /// <summary>
    /// Adds an ordering key. Repeated calls append keys, so the existing order is preserved.
    /// </summary>
    /// <param name="orderExp">The key selector.</param>
    /// <param name="direction">Ascending or descending.</param>
    /// <returns>A builder with the added ordering key.</returns>
    public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp, OrderDirection direction)
    {
        var b = Clone();
        if (b._sorting is null)
            b._sorting = [new Sorting(orderExp) { Direction = direction }];
        else
            b._sorting.Add(new Sorting(orderExp) { Direction = direction });
        return b;
    }
    /// <summary>Adds an ascending ordering key by <paramref name="orderExp"/>.</summary>
    /// <param name="orderExp">The key selector.</param>
    /// <returns>A builder with the added ordering key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Asc);
    /// <summary>Adds an ordering key by 1-based output column index.</summary>
    /// <param name="columnIdx">The 1-based result column index; must be greater than zero.</param>
    /// <param name="direction">Ascending or descending.</param>
    /// <returns>A builder with the added ordering key.</returns>
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
    /// <summary>Adds an ascending ordering key by 1-based output column index.</summary>
    /// <param name="columnIdx">The 1-based result column index; must be greater than zero.</param>
    /// <returns>A builder with the added ordering key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderBy(int columnIdx) => OrderBy(columnIdx, OrderDirection.Asc);
    /// <summary>Adds a descending ordering key by <paramref name="orderExp"/>.</summary>
    /// <param name="orderExp">The key selector.</param>
    /// <returns>A builder with the added ordering key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityBuilder<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Desc);
    /// <summary>Adds a descending ordering key by 1-based output column index.</summary>
    /// <param name="columnIdx">The 1-based result column index; must be greater than zero.</param>
    /// <returns>A builder with the added ordering key.</returns>
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
    /// <summary>The join clauses collected so far, or <c>null</c> when the query has no joins.</summary>
    protected List<JoinExpression>? _joins;
    /// <summary>CTE declarations that must be attached to commands this builder creates.</summary>
    internal IReadOnlyList<CteDefinition>? Ctes { get; set; }
    /// <summary>
    /// Per-query override of identifier quoting (<c>null</c> inherits the context default). Set through
    /// <see cref="WithQuotedIdentifiers"/>.
    /// </summary>
    internal bool? QuoteIdentifiers { get; set; }
    /// <summary>
    /// Per-query override of the SQL keyword case (<c>null</c> inherits the context default). Set
    /// through <see cref="WithKeywordCase"/>.
    /// </summary>
    internal KeywordCase? KeywordCase { get; set; }
    /// <summary>
    /// Query tag rendered as a block comment (<c>/* tag */</c>) right after <c>SELECT</c>. Set through
    /// <see cref="WithTag"/>; <c>null</c> means no tag.
    /// </summary>
    internal string? Tag { get; set; }
    /// <summary>
    /// Per-query override of the naming convention for auto-derived table/column names (<c>null</c>
    /// inherits the context default). Set through <see cref="WithNamingConvention"/>.
    /// </summary>
    internal INamingConvention? NamingConvention { get; set; }
    /// <summary>Initializes a builder in the named-table (<c>TableAlias</c>) mode.</summary>
    /// <param name="dataProvider">The data context that creates the query command.</param>
    public EntityBuilder(IDataContext dataProvider) : this(dataProvider, null) { }
    /// <summary>Initializes a builder over the table named <paramref name="table"/>.</summary>
    /// <param name="dataProvider">The data context that creates the query command.</param>
    /// <param name="table">The physical table name to select from, or <c>null</c> when it is supplied later.</param>
    public EntityBuilder(IDataContext dataProvider, string? table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    /// <summary>
    /// Projects each row with <paramref name="exp"/> and creates the SELECT command for the named
    /// table, carrying the set filters, joins and ordering.
    /// </summary>
    /// <typeparam name="TResult">The projection type produced by <paramref name="exp"/>.</typeparam>
    /// <param name="exp">The projection expression over <see cref="TableAlias"/>.</param>
    /// <returns>The command that produces <typeparamref name="TResult"/> rows.</returns>
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

        cmd.QuoteIdentifiers = QuoteIdentifiers;
        cmd.NamingConvention = NamingConvention;
        cmd.KeywordCase = KeywordCase;
        cmd.Tag = Tag;

        return cmd;
    }
    object ICloneable.Clone()
    {
        return CloneImp();
    }
    /// <summary>
    /// Returns a copy of this builder. Because the modifier methods already return copies, this is
    /// useful to fork a query without applying a modifier.
    /// </summary>
    /// <returns>A new builder with the same state.</returns>
    public EntityBuilder Clone()
    {
        return (EntityBuilder)CloneImp();
    }
    /// <summary>
    /// Copies this builder's state onto <paramref name="dst"/>; called by <see cref="CloneImp"/>.
    /// Protected so derived builders can extend the copy with their own state.
    /// </summary>
    /// <param name="dst">The builder that receives the state.</param>
    protected virtual void CopyTo(EntityBuilder dst)
    {
        dst._condition = _condition;
        dst._sorting = _sorting;
        dst.Ctes = Ctes;
        dst.QuoteIdentifiers = QuoteIdentifiers;
        dst.NamingConvention = NamingConvention;
        dst.KeywordCase = KeywordCase;
        dst.Tag = Tag;
    }
    /// <summary>
    /// Creates the copy returned by <see cref="Clone"/> and the explicit <c>ICloneable.Clone</c> call.
    /// Overridden by derived builders to clone their extra state.
    /// </summary>
    /// <returns>A new builder with the same state.</returns>
    protected virtual object CloneImp()
    {
        var r = new EntityBuilder(_dataProvider, _table) { Logger = Logger };

        CopyTo(r);

        return r;
    }

    /// <summary>
    /// Adds <paramref name="condition"/> to the WHERE clause over the named table. A repeated call
    /// combines the predicates with <c>and</c>. Returns a new builder; the current one is unchanged.
    /// </summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>A builder with the added predicate.</returns>
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

    /// <summary>
    /// Overrides identifier quoting for the commands this builder creates (see
    /// <see cref="EntityBuilder{TEntity}.WithQuotedIdentifiers"/>).
    /// </summary>
    public EntityBuilder WithQuotedIdentifiers(bool value = true)
    {
        var b = Clone();

        b.QuoteIdentifiers = value;

        return b;
    }
    /// <summary>
    /// Overrides the naming convention for the commands this builder creates (see
    /// <see cref="EntityBuilder{TEntity}.WithNamingConvention"/>).
    /// </summary>
    public EntityBuilder WithNamingConvention(INamingConvention? convention)
    {
        var b = Clone();

        b.NamingConvention = convention;

        return b;
    }
    /// <summary>
    /// Overrides the SQL keyword case for the commands this builder creates (see
    /// <see cref="EntityBuilder{TEntity}.WithKeywordCase"/>).
    /// </summary>
    /// <param name="keywordCase">The keyword case to apply.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder WithKeywordCase(KeywordCase keywordCase = global::NextORM.Core.KeywordCase.Upper)
    {
        var b = Clone();

        b.KeywordCase = keywordCase;

        return b;
    }
    /// <summary>
    /// Convenience form of <see cref="WithKeywordCase"/>: enables (<paramref name="value"/> is
    /// <see langword="true"/>) or disables upper-case SQL keywords for the commands this builder creates.
    /// </summary>
    /// <param name="value"><see langword="true"/> to emit keywords in upper case; otherwise lower case.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder WithUppercaseKeywords(bool value = true)
        => WithKeywordCase(value ? global::NextORM.Core.KeywordCase.Upper : global::NextORM.Core.KeywordCase.Lower);
    /// <summary>
    /// Attaches a query tag to the commands this builder creates (see
    /// <see cref="EntityBuilder{TEntity}.WithTag"/>).
    /// </summary>
    /// <param name="tag">The tag text to render, or <c>null</c> to clear the tag.</param>
    /// <returns>A builder with the tag applied.</returns>
    public EntityBuilder WithTag(string? tag)
    {
        var b = Clone();

        b.Tag = string.IsNullOrEmpty(tag) ? null : tag;

        return b;
    }
    /// <summary>
    /// Adds an inner join to <paramref name="from"/>, using <paramref name="joinCondition"/> as the
    /// <c>ON</c> predicate; only matching pairs survive.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> Join(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Inner, joinCondition);
    /// <summary>
    /// Adds a left outer join to <paramref name="from"/>: every left-hand row is kept, and right-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> LeftJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Left, joinCondition);
    /// <summary>
    /// Adds a right outer join to <paramref name="from"/>: every right-hand row is kept, and left-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> RightJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Right, joinCondition);
    /// <summary>
    /// Adds a full outer join to <paramref name="from"/>: unmatched rows from both sides are kept,
    /// with the other side's columns set to <c>NULL</c>.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> FullJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => JoinCore(from, JoinType.Full, joinCondition);
    /// <summary>
    /// Adds a cross join producing the Cartesian product of the two sources; there is no <c>ON</c>
    /// condition.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> CrossJoin(EntityBuilder from)
        => JoinCore(from, JoinType.Cross, null);
    /// <summary>
    /// Applies <paramref name="from"/> to every left-hand row (<c>CROSS APPLY</c> /
    /// <c>CROSS JOIN LATERAL</c>); there is no <c>ON</c> condition.
    /// </summary>
    /// <param name="from">The named-table builder whose source is applied.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> CrossApply(EntityBuilder from)
        => JoinCore(from, JoinType.CrossApply, null);
    /// <summary>
    /// Applies <paramref name="from"/> to every left-hand row, preserving left-hand rows with an empty
    /// result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). There is no <c>ON</c>
    /// condition.
    /// </summary>
    /// <param name="from">The named-table builder whose source is applied.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> OuterApply(EntityBuilder from)
        => JoinCore(from, JoinType.OuterApply, null);
    /// <summary>
    /// Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="from"/> and keeps this builder's
    /// shape: only the left-hand columns survive (see
    /// <see cref="EntityBuilder{TEntity}.SemiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}})"/>).
    /// </summary>
    public EntityBuilder SemiJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Semi) { From = new FromExpression(from._table!) });
    /// <inheritdoc cref="SemiJoin(EntityBuilder, Expression{Func{TableAlias, TableAlias, bool}})"/>
    public EntityBuilder AntiJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition)
        => AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Anti) { From = new FromExpression(from._table!) });
    /// <summary>
    /// Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="from"/> (see
    /// <see cref="EntityBuilder{TEntity}.PasteJoin{TJoinEntity}(EntityBuilder{TJoinEntity})"/>).
    /// </summary>
    public JoinedEntityBuilder<TableAlias, TableAlias> PasteJoin(EntityBuilder from)
        => JoinCore(from, JoinType.Paste, null);
    private EntityBuilder AddSemiAntiJoin(JoinExpression join)
    {
        var b = Clone();
        var joins = b._joins;
        if (joins is null)
            b._joins = joins = _joins is null ? [] : [.. _joins];

        joins.Add(join);

        return b;
    }
    private JoinedEntityBuilder<TableAlias, TableAlias> JoinCore(EntityBuilder from, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<TableAlias, TableAlias>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = new FromExpression(from._table!), EntityType = joinCondition is null ? typeof(TableAlias) : null }) { Logger = Logger, Table = _table, Ctes = CteMerge.Merge(Ctes, from.Ctes), QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        return cb;
    }
    /// <summary>
    /// Adds an inner join to <paramref name="_"/>, using <paramref name="joinCondition"/> as the
    /// <c>ON</c> predicate; only matching pairs survive.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    /// <summary>
    /// Adds a left outer join to <paramref name="_"/>: every left-hand row is kept, and right-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    /// <summary>
    /// Adds a right outer join to <paramref name="_"/>: every right-hand row is kept, and left-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    /// <summary>
    /// Adds a full outer join to <paramref name="_"/>: unmatched rows from both sides are kept, with
    /// the other side's columns set to <c>NULL</c>.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    /// <summary>
    /// Adds a cross join producing the Cartesian product of the two sources; there is no <c>ON</c>
    /// condition.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Cross, null);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row (<c>CROSS APPLY</c> /
    /// <c>CROSS JOIN LATERAL</c>); there is no <c>ON</c> condition.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.CrossApply, null);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row, preserving left-hand rows with an empty
    /// result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). There is no <c>ON</c>
    /// condition.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> OuterApply<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.OuterApply, null);
    /// <inheritdoc cref="SemiJoin(EntityBuilder, Expression{Func{TableAlias, TableAlias, bool}})"/>
    public EntityBuilder SemiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Semi) { From = _dataProvider.GetFrom(typeof(TJoinEntity), null)! });
    /// <inheritdoc cref="SemiJoin(EntityBuilder, Expression{Func{TableAlias, TableAlias, bool}})"/>
    public EntityBuilder AntiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Anti) { From = _dataProvider.GetFrom(typeof(TJoinEntity), null)! });
    /// <inheritdoc cref="PasteJoin(EntityBuilder)"/>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> PasteJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _)
        => JoinCore(_, JoinType.Paste, null);
    private JoinedEntityBuilder<TableAlias, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<TableAlias, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(TJoinEntity), null)!, EntityType = joinCondition is null ? typeof(TJoinEntity) : null }) { Logger = Logger, Table = _table, Ctes = CteMerge.Merge(Ctes, _.Ctes), QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        return cb;
    }
}
