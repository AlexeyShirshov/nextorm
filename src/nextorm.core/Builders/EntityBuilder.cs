using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Globalization;
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
    private QueryFilterScope _filterScope = QueryFilterScope.None;
    private LambdaExpression? _group;
    private LimitByClause? _limitBy;
    private DistinctOnClause? _distinctOn;
    private ExtremeRowClause? _extremeRow;
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
    private List<IEagerLoadSpec<TEntity>>? _loadSpecs;
    private List<IJoinIntoSpec<TEntity>>? _joinIntos;
    private bool _singleQuery;
    /// <summary>The join clauses collected so far, or <c>null</c> when the query has no joins.</summary>
    protected List<JoinExpression>? _joins;
    private string? _table;
    private FromExpression? _from;
    private string? _subQueryHint;
    private string? _tableNameOverride;
    private string? _schemaOverride;
    private string? _databaseOverride;
    private string? _serverOverride;
    private string? _tableExpression;
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
    internal ILogger? Logger { get; set; }
    internal QueryCommand? Query { get => _query; set => _query = value; }
    /// <summary>
    /// Provider-specific hint attached to a derived-table primary source, or <c>null</c> when there is
    /// none. Set by the <c>From(...)</c> overloads that copy <see cref="FromOptions.SubQueryHint"/>.
    /// </summary>
    internal string? SubQueryHint { get => _subQueryHint; set => _subQueryHint = value; }
    internal IDataContext DataProvider => _dataProvider;
    /// <summary>
    /// The eager-load specifications applied by <see cref="LoadWith{TChild,TKey}"/>, or <c>null</c> when
    /// the builder carries none. Only the list terminals consult them; <see cref="ToCommand"/> ignores
    /// them so building the command cannot recurse into the loader.
    /// </summary>
    internal IReadOnlyList<IEagerLoadSpec<TEntity>>? LoadSpecs => _loadSpecs;
    /// <summary>
    /// The <c>JoinInto</c> declarations applied to this builder, or <c>null</c> when the builder carries
    /// none. The list terminals execute one denormalized command and stitch the children onto the
    /// deduplicated parents; the non-list terminals exclude the joins and see the parent only.
    /// </summary>
    internal IReadOnlyList<IJoinIntoSpec<TEntity>>? JoinIntos => _joinIntos;
    /// <summary>
    /// Whether <see cref="AsSingleQuery"/> switched the builder to single-query eager loading, so the
    /// list terminals execute one denormalized command for the <see cref="LoadWith{TChild, TKey}"/>
    /// collections instead of the default split child queries.
    /// </summary>
    internal bool SingleQuery => _singleQuery;
    /// <summary>
    /// Whether <see cref="IgnoreFilters()"/> (the all-or-nothing form) was applied to this builder. Used
    /// by single-query eager loading to decide the child side's global filters independently of the
    /// parent's.
    /// </summary>
    internal bool IgnoresFilters => _filterScope.All;
    /// <summary>
    /// The selective global-query-filter scope disabled on this builder. Carried onto every command the
    /// builder creates so the preparer skips exactly the filters the scope names. Settable internally so
    /// the join chain can hand the pre-join builder's scope to its joined projection builder.
    /// </summary>
    internal QueryFilterScope FilterScope { get => _filterScope; set => _filterScope = value; }
    /// <summary>
    /// Returns a builder whose selective filter scope is the union of this builder's scope and
    /// <paramref name="scope"/>, without mutating this builder. Used by the DML builders to fold their
    /// own <c>IgnoreFilters</c> state into a retained source builder: mutating that builder in place
    /// would make repeated terminal builds accumulate a stale scope.
    /// </summary>
    /// <param name="scope">The additional scope to disable.</param>
    /// <returns>This builder when <paramref name="scope"/> is empty, otherwise a scoped copy.</returns>
    internal EntityBuilder<TEntity> WithFilterScope(QueryFilterScope scope)
    {
        if (scope.IsEmpty)
            return this;

        var copy = Clone();
        copy._filterScope = copy._filterScope.Union(scope);
        return copy;
    }
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
    /// <summary>The <c>SelectWhereMax</c>/<c>SelectWhereMin</c> clause, or <c>null</c> when there is none.</summary>
    internal ExtremeRowClause? ExtremeRowClause { get => _extremeRow; set => _extremeRow = value; }
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
    /// <summary>Hints applied to every physical table in the command's scope, or <c>null</c> when there are none.</summary>
    internal IReadOnlyList<string>? TablesInScopeHints { get; set; }
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
    /// <summary>
    /// Per-query override of the command timeout in seconds (<c>null</c> inherits the context
    /// default). A value of zero or less means the provider default. Set through
    /// <see cref="WithCommandTimeout"/>.
    /// </summary>
    internal int? CommandTimeout { get; set; }
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
        EnsureNoJoinIntos(nameof(Select));
        EnsureNoEagerLoadState(nameof(Select));

        return SelectCore(exp);
    }
    /// <summary>
    /// Builds the projection command without the public composition guards. Used by <see cref="Select"/>
    /// (after the guards) and by the scalar terminal helpers (<c>Count</c>/<c>Any</c>), which project
    /// the parent and deliberately ignore any eager-load state.
    /// </summary>
    private QueryCommand<TResult> SelectCore<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(new QueryDefinition
        {
            Exp = exp,
            SrcType = _sourceEntityType,
            Condition = _condition,
            FilterScope = _filterScope,
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

        cmd.From = ResolveSource();

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        ApplyCommandOptions(cmd);

        return cmd;
    }
    /// <summary>
    /// Copies the builder's provider-independent modifiers onto an already-created command. Shared by
    /// <see cref="Select{TResult}"/>, <see cref="ToCommand"/> and the <c>JoinInto</c> pair command.
    /// </summary>
    private void ApplyCommandOptions(QueryCommand cmd)
    {
        cmd.GroupingType = GroupingType;
        cmd.TableHints = TableHints;
        cmd.IndexHints = IndexHints;
        cmd.IndexHintKind = IndexHintKind;
        cmd.TablesInScopeHints = TablesInScopeHints;
        cmd.QuoteIdentifiers = QuoteIdentifiers;
        cmd.NamingConvention = NamingConvention;
        cmd.KeywordCase = KeywordCase;
        cmd.Tag = Tag;
        cmd.CommandTimeout = CommandTimeout;
        cmd.GroupingSets = GroupingSets;
        cmd.GroupByWithTotals = GroupByWithTotals;
        cmd.LimitBy = LimitByClause;
        cmd.DistinctOn = _distinctOn;
        cmd.ExtremeRow = _extremeRow;
        cmd.TableSample = _tablesample;
        cmd.Temporal = _temporal;
        cmd.RowLock = _rowLock;
    }
    /// <summary>
    /// Projects each row with <paramref name="selector"/> and exposes the result as a derived table,
    /// returning a builder that can be joined, filtered and projected further. This names an
    /// intermediate join projection (typically <c>p =&gt; new { p.Item1.X, p.Item2.Y }</c>) and starts a
    /// new join from it, so the fixed <c>Projection&lt;T1..T8&gt;</c> arity cap no longer terminates the
    /// chain: <c>...Join(...).As(p =&gt; new { ... }).Join(...)</c> works on every arity.
    /// </summary>
    /// <remarks>
    /// Implemented as <c>DataProvider.From(Select(selector))</c>: only the projected members stay
    /// visible to later joins, and the projection becomes a materialization boundary, so outer-join
    /// semantics across the boundary follow derived-table rules. A subsequent <c>Join</c> on the
    /// in-memory provider throws <see cref="NotSupportedException"/> (derived sources cannot be joined
    /// in memory).
    /// </remarks>
    /// <typeparam name="TResult">The named projection type produced by <paramref name="selector"/>.</typeparam>
    /// <param name="selector">The projection expression, for example <c>p =&gt; new { p.Item1.Id, p.Item2.Name }</c>.</param>
    /// <returns>A builder over the derived table that can be composed further.</returns>
    /// <exception cref="NotSupportedException">A following <c>Join</c>/<c>Apply</c> on the derived source is rejected by the in-memory provider.</exception>
    public EntityBuilder<TResult> As<TResult>(Expression<Func<TEntity, TResult>> selector)
    {
        EnsureNoJoinIntos(nameof(As));
        EnsureNoEagerLoadState(nameof(As));

        return _dataProvider.From(Select(selector));
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
            FilterScope = _filterScope,
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

        cmd.From = ResolveSource();

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        ApplyCommandOptions(cmd);

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
    /// Disables <b>all</b> global query filters declared for the query's entity type (and for every
    /// entity joined by it), so the query reads the unfiltered rows. The current builder is unchanged;
    /// the returned builder is a copy with the all-or-nothing scope set. To disable only some filters,
    /// use one of the selective overloads.
    /// </summary>
    /// <returns>A builder that ignores every global query filter.</returns>
    public EntityBuilder<TEntity> IgnoreFilters()
    {
        var b = Clone();
        b._filterScope = b._filterScope.Union(QueryFilterScope.AllFilters);
        return b;
    }
    /// <summary>
    /// Disables every global query filter declared for the given entity types, leaving the filters of
    /// other entity types in the query active. A subsequent call accumulates: the returned builder
    /// carries the union of both scopes. An empty or <see langword="null"/>
    /// <paramref name="entityTypes"/> disables nothing. The current builder is unchanged.
    /// </summary>
    /// <param name="entityTypes">The entity types whose filters are disabled.</param>
    /// <returns>A builder that ignores the filters of the given entity types.</returns>
    public EntityBuilder<TEntity> IgnoreFilters(params Type[] entityTypes)
    {
        var scope = QueryFilterScope.FromTypes(entityTypes);
        if (scope.IsEmpty)
            return this;

        var b = Clone();
        b._filterScope = b._filterScope.Union(scope);
        return b;
    }
    /// <summary>
    /// Disables the named global query filters identified by <paramref name="filterKeys"/> on every
    /// entity type in the query. A subsequent call accumulates: the returned builder carries the union
    /// of both scopes. An empty or <see langword="null"/> <paramref name="filterKeys"/> disables
    /// nothing (mirroring EF Core). The current builder is unchanged.
    /// </summary>
    /// <param name="filterKeys">The filter keys to disable.</param>
    /// <returns>A builder that ignores the named filters.</returns>
    public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys)
    {
        var scope = QueryFilterScope.FromKeys(filterKeys);
        if (scope.IsEmpty)
            return this;

        var b = Clone();
        b._filterScope = b._filterScope.Union(scope);
        return b;
    }
    /// <summary>
    /// Disables the named global query filters identified by <paramref name="filterKeys"/> only on the
    /// given <paramref name="entityTypes"/> (the intersection of keys and types); an empty
    /// <paramref name="entityTypes"/> means any entity type. A subsequent call accumulates with the
    /// union. The key list is the gate: an empty or <see langword="null"/>
    /// <paramref name="filterKeys"/> disables nothing even when entity types are supplied. The current
    /// builder is unchanged.
    /// </summary>
    /// <param name="filterKeys">The filter keys to disable.</param>
    /// <param name="entityTypes">The entity types the disable is scoped to; empty means any entity type.</param>
    /// <returns>A builder that ignores the given named filters on the given entity types.</returns>
    public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes)
    {
        var scope = QueryFilterScope.FromKeysAndTypes(filterKeys, entityTypes);
        if (scope.IsEmpty)
            return this;

        var b = Clone();
        b._filterScope = b._filterScope.Union(scope);
        return b;
    }
    /// <summary>
    /// Declares a split-query eager load for <paramref name="collection"/>: after the parent query is
    /// materialized, one extra child query per key chunk is executed to fetch the children, and they are
    /// stitched onto the parents in memory (two round trips, never N+1). Only the stitching terminals
    /// (<c>ToList</c>/<c>ToListAsync</c> and <c>ToArray</c>/<c>ToArrayAsync</c>) honor the declaration;
    /// <c>ToHashSet</c>, <c>ToDictionary</c>, <c>First</c>, <c>FirstOrDefault</c>, <c>Single*</c>,
    /// <c>ToEnumerable</c>, <c>ToAsyncEnumerable</c> and <see cref="ToCommand"/> run the parent only and
    /// do not load the collection, and <c>Count</c>/<c>Any</c> stay scalar without extra load queries.
    /// <para>
    /// The parent's current collection value is cleared and refilled when it is non-null; a null value is
    /// assigned a fresh list when the member is settable. A read-only member whose value is null is
    /// rejected with <see cref="NotSupportedException"/>. Every target is validated before the parents are
    /// mutated, so the rejection leaves no parent partially populated. The child order follows the child
    /// query's order.
    /// </para>
    /// <para>
    /// A collection member may be declared only once: calling <c>LoadWith</c> twice for the same member
    /// throws <see cref="InvalidOperationException"/>. Declare distinct members on the same builder to
    /// load several collections.
    /// </para>
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <typeparam name="TKey">
    /// The non-nullable key type shared by the parent and child selectors. A null key value observed at
    /// runtime is skipped and never matches.
    /// </typeparam>
    /// <param name="collection">The parent-side collection member to fill.</param>
    /// <param name="childQuery">Builds the child query from the data context that owns the parent query.</param>
    /// <param name="parentKey">Selects the parent key used to match children.</param>
    /// <param name="childKey">Selects the child key used to match parents.</param>
    /// <returns>A copy of this builder carrying the eager-load declaration.</returns>
    /// <exception cref="InvalidOperationException">
    /// The collection member already carries an eager-load declaration on this builder.
    /// </exception>
    public EntityBuilder<TEntity> LoadWith<TChild, TKey>(
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Func<IDataContext, EntityBuilder<TChild>> childQuery,
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(childQuery);
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(childKey);

        var spec = new EagerLoadSpec<TEntity, TChild, TKey>(collection, childQuery, parentKey, childKey);

        if (spec.CollectionMember is { } member && _loadSpecs is not null)
        {
            foreach (var registered in _loadSpecs)
            {
                if (registered.CollectionMember is { } existing && existing.Equals(member))
                    throw new InvalidOperationException(
                        $"The collection member '{member.Name}' on '{typeof(TEntity).Name}' already has an " +
                        "eager-load declaration; remove the earlier LoadWith call or load a different collection.");
            }
        }

        var b = Clone();
        b._loadSpecs = _loadSpecs is null ? [spec] : [.. _loadSpecs, spec];

        return b;
    }
    /// <summary>
    /// Switches the collections declared with <see cref="LoadWith{TChild, TKey}"/> to single-query eager
    /// loading: one denormalized <c>LEFT JOIN</c> command fetches the parents and every declared
    /// collection, and the rows are stitched in memory. Every terminal that stitches
    /// (<c>ToList</c>/<c>ToListAsync</c>, <c>ToArray</c>/<c>ToArrayAsync</c>) uses this one command; the
    /// other terminals see the parent only. The returned builder is a copy; the current builder is
    /// unchanged.
    /// <para>
    /// Unlike split loading, single-query loading never chunks the parent keys: any number of parents is
    /// fetched by the same one command, so there is no chunked <c>IN</c> list and no silent fallback to
    /// split. The child query's own <c>Where</c> condition is merged into the join <c>ON</c> predicate, so
    /// it filters the children without dropping childless parents.
    /// </para>
    /// <para>
    /// Split (two round trips) remains the default; call this only when a single round trip matters more
    /// than the smaller, chunked child result. A collection member may still be declared only once.
    /// </para>
    /// </summary>
    /// <returns>A copy of this builder set to single-query eager loading.</returns>
    public EntityBuilder<TEntity> AsSingleQuery()
    {
        var b = Clone();
        b._singleQuery = true;
        return b;
    }
    /// <summary>
    /// Declares a <c>LEFT JOIN</c> to <paramref name="child"/> that fills the <paramref name="collection"/>
    /// member of every parent when the query is enumerated by a list terminal. The returned builder is a
    /// copy; the current builder is unchanged.
    /// <para>
    /// The join is part of the query: <see cref="ToCommand"/> emits
    /// <c>... from Parent as t1 left join Child as t2 on &lt;predicate&gt;</c> while still materializing
    /// <typeparamref name="TEntity"/> rows, so a bare <see cref="ToCommand"/> may repeat a parent once
    /// per matching child. The parent/child keys are resolved from the declared relationship metadata (or
    /// from a local <see cref="JoinOptions"/> configuration, which fully replaces the metadata for this
    /// call); a relationship that is not declared, is many-to-many without a junction, or uses a composite
    /// key is rejected with <see cref="NotSupportedException"/> — use the explicit-key overload instead.
    /// The list terminals
    /// execute one denormalized command and stitch the children onto the deduplicated parents; the
    /// non-list terminals exclude this join and evaluate the parent only.
    /// </para>
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate over the parent and child.</param>
    /// <param name="collection">The parent-side collection member the joined children fill.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A copy of this builder carrying the join declaration.</returns>
    /// <exception cref="NotSupportedException">The join kind or relationship is not supported.</exception>
    public EntityBuilder<TEntity> JoinInto<TChild>(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Action<JoinOptions>? options = null)
        => JoinInto(child, predicate, collection, JoinType.Left, options);
    /// <summary>
    /// Declares a typed join to <paramref name="child"/> (see
    /// <see cref="JoinInto{TChild}(EntityBuilder{TChild}, Expression{Func{TEntity, TChild, bool}}, Expression{Func{TEntity, ICollection{TChild}}}, Action{JoinOptions}?)"/>)
    /// that fills the <paramref name="collection"/> member. Only <see cref="JoinType.Inner"/> and
    /// <see cref="JoinType.Left"/> are accepted; any other kind throws <see cref="NotSupportedException"/>.
    /// The returned builder is a copy; the current builder is unchanged.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate over the parent and child.</param>
    /// <param name="collection">The parent-side collection member the joined children fill.</param>
    /// <param name="joinType">The join kind; <see cref="JoinType.Inner"/> or <see cref="JoinType.Left"/>.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A copy of this builder carrying the join declaration.</returns>
    /// <exception cref="NotSupportedException">The join kind or relationship is not supported.</exception>
    public EntityBuilder<TEntity> JoinInto<TChild>(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        JoinType joinType,
        Action<JoinOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(collection);
        EnsureJoinIntoSupported(joinType);
        EnsureJoinIntoSource(nameof(JoinInto));

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var relationship = ResolveJoinIntoRelationship(collection, opts);
        if (relationship.Kind is RelationshipKind.ManyToMany)
        {
            var (manyToMany, linkJoin, childJoin) = JoinIntoManyToManySpec<TEntity, TChild>.Create(
                child, predicate, collection, relationship, joinType, opts, _dataProvider);
            return AddJoinInto(manyToMany, linkJoin, childJoin);
        }

        var spec = new JoinIntoSpec<TEntity, TChild>(child, predicate, collection, relationship, joinType);
        return AddJoinInto(spec, new JoinExpression(predicate, joinType)
        {
            From = GetJoinSource(child),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints,
            SuppressCartesianWarning = opts.CartesianWarningSuppressed
        });
    }
    /// <summary>
    /// Declares a <c>LEFT JOIN</c> to <paramref name="child"/> that assigns the single joined child to the
    /// one-to-one <paramref name="navigation"/> reference of every parent when the query is enumerated by a
    /// list terminal. The returned builder is a copy; the current builder is unchanged.
    /// <para>
    /// The relationship must be declared as one-to-one (<c>HasOneToOne</c>) so the principal/foreign keys
    /// are known. At most one child is assigned per parent; a <c>LEFT</c> join leaves the reference
    /// <see langword="null"/> when no child matches, while an <c>INNER</c> join excludes the parent. A
    /// parent that matches more than one distinct child throws <see cref="InvalidOperationException"/> at
    /// materialization. Cartesian repeats of the same child caused by a neighbouring join are tolerated.
    /// </para>
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate over the parent and child.</param>
    /// <param name="navigation">The parent-side reference navigation the joined child fills.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A copy of this builder carrying the join declaration.</returns>
    /// <exception cref="NotSupportedException">The join kind, relationship kind or keys are not supported.</exception>
    public EntityBuilder<TEntity> JoinInto<TChild>(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, TChild?>> navigation,
        Action<JoinOptions>? options = null)
        where TChild : class
        => JoinInto(child, predicate, navigation, JoinType.Left, options);
    /// <summary>
    /// Declares a typed join to <paramref name="child"/> (see
    /// <see cref="JoinInto{TChild}(EntityBuilder{TChild}, Expression{Func{TEntity, TChild, bool}}, Expression{Func{TEntity, TChild}}, Action{JoinOptions}?)"/>)
    /// that assigns the single joined child to the one-to-one <paramref name="navigation"/> reference.
    /// Only <see cref="JoinType.Inner"/> and <see cref="JoinType.Left"/> are accepted; any other kind
    /// throws <see cref="NotSupportedException"/>. The returned builder is a copy; the current builder is
    /// unchanged.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate over the parent and child.</param>
    /// <param name="navigation">The parent-side reference navigation the joined child fills.</param>
    /// <param name="joinType">The join kind; <see cref="JoinType.Inner"/> or <see cref="JoinType.Left"/>.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A copy of this builder carrying the join declaration.</returns>
    /// <exception cref="NotSupportedException">The join kind, relationship kind or keys are not supported.</exception>
    public EntityBuilder<TEntity> JoinInto<TChild>(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, TChild?>> navigation,
        JoinType joinType,
        Action<JoinOptions>? options = null)
        where TChild : class
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(navigation);
        EnsureJoinIntoSupported(joinType);
        EnsureJoinIntoSource(nameof(JoinInto));

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var relationship = ResolveJoinIntoReferenceRelationship(navigation, opts);
        var spec = new JoinIntoReferenceSpec<TEntity, TChild>(child, predicate, navigation, relationship, joinType);
        return AddJoinInto(spec, new JoinExpression(predicate, joinType)
        {
            From = GetJoinSource(child),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints,
            SuppressCartesianWarning = opts.CartesianWarningSuppressed
        });
    }
    /// <summary>
    /// Declares a <c>LEFT JOIN</c> to <paramref name="child"/> that fills the <paramref name="collection"/>
    /// member, using the explicit <paramref name="parentKey"/>/<paramref name="childKey"/> selectors
    /// instead of declared relationship metadata (symmetrical to the <c>LoadWith</c> fallback). The
    /// returned builder is a copy; the current builder is unchanged.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <typeparam name="TKey">The non-nullable key type shared by the two selectors.</typeparam>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate over the parent and child.</param>
    /// <param name="collection">The parent-side collection member the joined children fill.</param>
    /// <param name="parentKey">Selects the parent key used to group the rows.</param>
    /// <param name="childKey">Selects the child key used to group the rows.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A copy of this builder carrying the join declaration.</returns>
    /// <exception cref="NotSupportedException">The key selectors select different types.</exception>
    public EntityBuilder<TEntity> JoinInto<TChild, TKey>(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey,
        Action<JoinOptions>? options = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(childKey);
        EnsureJoinIntoSource(nameof(JoinInto));
        ValidateJoinIntoKeys(parentKey, childKey);

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var spec = new JoinIntoSpec<TEntity, TChild, TKey>(child, predicate, collection, parentKey, childKey, JoinType.Left);
        return AddJoinInto(spec, new JoinExpression(predicate, JoinType.Left)
        {
            From = GetJoinSource(child),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints,
            SuppressCartesianWarning = opts.CartesianWarningSuppressed
        });
    }
    /// <summary>Validates the <c>JoinInto</c> join kind: only inner and left edges are supported.</summary>
    private static void EnsureJoinIntoSupported(JoinType joinType)
    {
        if (joinType is not (JoinType.Inner or JoinType.Left))
            throw new NotSupportedException(
                $"JoinInto supports only {nameof(JoinType.Inner)} and {nameof(JoinType.Left)} joins, not {joinType}.");
    }
    /// <summary>Rejects <c>JoinInto</c> over a derived (<c>As</c>) or joined projection source.</summary>
    private void EnsureJoinIntoSource(string method)
    {
        if (_query is not null || typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                $"{method} cannot be applied to a derived (As) or joined projection source.");
    }
    /// <summary>
    /// Finds the declared relationship whose navigation is <paramref name="collection"/> on the parent,
    /// rejecting the unsupported cardinalities/keys and an undeclared relationship. A relationship
    /// configured locally through <paramref name="options"/> fully replaces the metadata lookup.
    /// </summary>
    private IRelationshipMetadata ResolveJoinIntoRelationship<TChild>(
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        JoinOptions options)
    {
        var property = JoinIntoSpecHelpers.TryResolveCollectionProperty(collection)
            ?? throw new NotSupportedException(
                "JoinInto requires the collection selector to be a property access so the declared relationship can be resolved.");

        if (options.Relationship is { } local)
            return ValidateLocalCollectionRelationship<TChild>(local, property);

        var metadata = DataContextExtensions.ResolveMetadata<TEntity>(_dataProvider, null);
        foreach (var relationship in metadata.Relationships)
        {
            if (relationship.Navigation is null || !relationship.Navigation.Equals(property))
                continue;

            // Many-to-many needs a junction descriptor; a model-only declaration without one (or a
            // locally configured relationship without a junction) is reported instead of silently
            // producing a direct join between the two principal sides.
            if (relationship.Kind is RelationshipKind.ManyToMany)
            {
                if (relationship.Junction is null)
                    throw new NotSupportedException(
                        $"The many-to-many relationship declared on '{typeof(TEntity).Name}.{property.Name}' has no junction metadata; " +
                        "declare it with HasManyThrough, or pass a local ManyToMany configuration through the JoinInto options.");

                if (relationship.RelatedType != typeof(TChild))
                    throw new NotSupportedException(
                        $"The relationship declared on '{typeof(TEntity).Name}.{property.Name}' points to '{relationship.RelatedType.Name}', not '{typeof(TChild).Name}'.");

                if (relationship.PrincipalKey.Count != 1 || relationship.ForeignKey.Count != 1)
                    throw new NotSupportedException(
                        $"JoinInto does not support a composite key on the relationship declared on '{typeof(TEntity).Name}.{property.Name}'.");

                // The junction carries its own four key lists; validate them independently of the
                // principal/child key pair so a composite junction is rejected explicitly.
                if (relationship.Junction is { } junction &&
                    (junction.ParentKey.Count != 1 || junction.ChildKey.Count != 1 ||
                     junction.JunctionParentForeignKey.Count != 1 || junction.JunctionChildForeignKey.Count != 1))
                    throw new NotSupportedException(
                        $"JoinInto does not support a composite junction key on the relationship declared on '{typeof(TEntity).Name}.{property.Name}'.");

                return relationship;
            }

            // OneToOne is lowered by the reference-navigation overload; the collection overload only
            // accepts a collection navigation (OneToMany).
            if (relationship.Kind is not RelationshipKind.OneToMany)
                throw new NotSupportedException(
                    $"JoinInto on the collection '{typeof(TEntity).Name}.{property.Name}' expects a OneToMany relationship, " +
                    $"but the declared kind is {relationship.Kind}; use the reference-navigation JoinInto overload for a one-to-one navigation.");

            if (relationship.RelatedType != typeof(TChild))
                throw new NotSupportedException(
                    $"The relationship declared on '{typeof(TEntity).Name}.{property.Name}' points to '{relationship.RelatedType.Name}', not '{typeof(TChild).Name}'.");

            if (relationship.ForeignKey.Count != 1 || relationship.PrincipalKey.Count != 1)
                throw new NotSupportedException(
                    $"JoinInto does not support a composite key on the relationship declared on '{typeof(TEntity).Name}.{property.Name}'.");

            return relationship;
        }

        throw new NotSupportedException(
            $"JoinInto on '{typeof(TEntity).Name}.{property.Name}' requires an explicitly declared relationship; " +
            "declare it with HasMany/HasOne/HasManyThrough (or [Relationship]) or use the explicit-key JoinInto overload.");
    }

    /// <summary>Validates a locally configured relationship used by a collection <c>JoinInto</c>.</summary>
    private static IRelationshipMetadata ValidateLocalCollectionRelationship<TChild>(IRelationshipMetadata relationship, PropertyInfo property)
    {
        if (relationship.DeclaringType != typeof(TEntity))
            throw new NotSupportedException(
                $"The local relationship configured for '{typeof(TEntity).Name}.{property.Name}' declares '{relationship.DeclaringType.Name}' as the parent, not '{typeof(TEntity).Name}'.");

        if (relationship.Kind is not (RelationshipKind.OneToMany or RelationshipKind.ManyToMany))
            throw new NotSupportedException(
                $"The local relationship configured for the collection '{typeof(TEntity).Name}.{property.Name}' must be OneToMany or ManyToMany, " +
                $"but its kind is {relationship.Kind}; use the reference-navigation JoinInto overload for a one-to-one navigation.");

        if (relationship.Kind is RelationshipKind.ManyToMany && relationship.Junction is null)
            throw new NotSupportedException(
                $"The local many-to-many relationship configured for '{typeof(TEntity).Name}.{property.Name}' has no junction metadata.");

        if (relationship.RelatedType != typeof(TChild))
            throw new NotSupportedException(
                $"The local relationship configured for '{typeof(TEntity).Name}.{property.Name}' points to '{relationship.RelatedType.Name}', not '{typeof(TChild).Name}'.");

        if (relationship.ForeignKey.Count != 1 || relationship.PrincipalKey.Count != 1)
            throw new NotSupportedException(
                $"JoinInto does not support a composite key on the local relationship configured for '{typeof(TEntity).Name}.{property.Name}'.");

        // As in the declared-metadata path, validate the junction's own key lists: a local ManyToMany
        // configuration with a composite junction selector is rejected explicitly.
        if (relationship.Junction is { } junction &&
            (junction.ParentKey.Count != 1 || junction.ChildKey.Count != 1 ||
             junction.JunctionParentForeignKey.Count != 1 || junction.JunctionChildForeignKey.Count != 1))
            throw new NotSupportedException(
                $"JoinInto does not support a composite junction key on the local relationship configured for '{typeof(TEntity).Name}.{property.Name}'.");

        return relationship;
    }

    /// <summary>
    /// Finds the declared one-to-one relationship whose reference navigation is <paramref name="navigation"/>,
    /// rejecting another relationship kind, an undeclared relationship, a mismatched child type, a
    /// composite key and a read-only navigation. A relationship configured locally through
    /// <paramref name="options"/> fully replaces the metadata lookup.
    /// </summary>
    private IRelationshipMetadata ResolveJoinIntoReferenceRelationship<TChild>(Expression<Func<TEntity, TChild?>> navigation, JoinOptions options)
        where TChild : class
    {
        var property = JoinIntoSpecHelpers.TryResolveNavigationProperty(navigation)
            ?? throw new NotSupportedException(
                "JoinInto requires the navigation selector to be a property access so the declared relationship can be resolved.");

        if (options.Relationship is { } local)
        {
            if (local.DeclaringType != typeof(TEntity) || local.RelatedType != typeof(TChild))
                throw new NotSupportedException(
                    $"The local relationship configured for '{typeof(TEntity).Name}.{property.Name}' must relate '{typeof(TEntity).Name}' to '{typeof(TChild).Name}'.");

            if (local.Kind is not RelationshipKind.OneToOne)
                throw new NotSupportedException(
                    $"The local relationship configured for the reference navigation '{typeof(TEntity).Name}.{property.Name}' must be OneToOne, " +
                    $"but its kind is {local.Kind}; use the collection JoinInto overload for a collection navigation.");

            if (property.SetMethod is null)
                throw new NotSupportedException(
                    $"JoinInto cannot assign the one-to-one navigation '{typeof(TEntity).Name}.{property.Name}' because it has no setter.");

            return local;
        }

        var metadata = DataContextExtensions.ResolveMetadata<TEntity>(_dataProvider, null);
        foreach (var relationship in metadata.Relationships)
        {
            if (relationship.Navigation is null || !relationship.Navigation.Equals(property))
                continue;

            if (relationship.Kind is not RelationshipKind.OneToOne)
                throw new NotSupportedException(
                    $"JoinInto with a reference navigation supports only a OneToOne relationship, but " +
                    $"'{typeof(TEntity).Name}.{property.Name}' declares {relationship.Kind}; use the collection JoinInto overload.");

            if (relationship.RelatedType != typeof(TChild))
                throw new NotSupportedException(
                    $"The relationship declared on '{typeof(TEntity).Name}.{property.Name}' points to '{relationship.RelatedType.Name}', not '{typeof(TChild).Name}'.");

            if (relationship.ForeignKey.Count != 1 || relationship.PrincipalKey.Count != 1)
                throw new NotSupportedException(
                    $"JoinInto does not support a composite key on the relationship declared on '{typeof(TEntity).Name}.{property.Name}'.");

            if (property.SetMethod is null)
                throw new NotSupportedException(
                    $"JoinInto cannot assign the one-to-one navigation '{typeof(TEntity).Name}.{property.Name}' because it has no setter.");

            return relationship;
        }

        throw new NotSupportedException(
            $"JoinInto on '{typeof(TEntity).Name}.{property.Name}' requires an explicitly declared one-to-one relationship; " +
            "declare it with HasOneToOne (or [Relationship]) or use the collection JoinInto overload.");
    }
    /// <summary>Rejects explicit key selectors whose selected property types do not agree, or that are not member accesses.</summary>
    private static void ValidateJoinIntoKeys<TChild, TKey>(
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey)
        where TKey : notnull
    {
        // A computed key has no stable plan identity, so two distinct selectors would share a cached
        // plan; reject it rather than fold both into the same key.
        JoinIntoSpecHelpers.RequireMemberName(parentKey);
        JoinIntoSpecHelpers.RequireMemberName(childKey);

        var parentType = JoinIntoSpecHelpers.TryResolveKeyType(parentKey);
        var childType = JoinIntoSpecHelpers.TryResolveKeyType(childKey);
        if (parentType is null || childType is null)
            return;

        var keyType = JoinIntoSpecHelpers.Unwrap(typeof(TKey));
        if (parentType != childType || parentType != keyType)
            throw new NotSupportedException(
                $"The JoinInto key selectors must select the same property type ({nameof(TKey)}): " +
                $"the parent key selects '{parentType.Name}' and the child key selects '{childType.Name}'.");
    }
    /// <summary>
    /// Returns a copy of this builder carrying the join declaration: the join is appended to the rendered
    /// join list and the spec is appended to the stitching metadata list. Copying keeps the source
    /// builder unchanged and lets a following <c>Where</c> keep the join.
    /// </summary>
    private EntityBuilder<TEntity> AddJoinInto(IJoinIntoSpec<TEntity> spec, params JoinExpression[] joins)
    {
        if (_joins is { } existing && existing.Exists(j => !j.IsJoinInto))
            throw new NotSupportedException(
                "JoinInto cannot be combined with other joins on the same builder; declare JoinInto on a plain entity source only.");

        if (joins.Length == 0)
            throw new ArgumentException("A JoinInto declaration must contribute at least one join.", nameof(joins));

        foreach (var join in joins)
        {
            join.IsJoinInto = true;
            join.JoinIntoIdentity = spec.Identity;
        }

        // A plain JoinInto child that made no selective filter decision leaves the scope null so it keeps
        // inheriting the parent's whole scope (including a parent IgnoreFilters()); one that disabled
        // specific filters carries its own scope so the selective disable reaches the child join. Only the
        // child edge takes the scope: the many-to-many link edge is a derived junction source with no
        // child filters of its own.
        var childJoin = joins[^1];
        childJoin.FilterScope = spec.ChildFilterScope.IsEmpty ? null : spec.ChildFilterScope;

        var b = Clone();
        b._joinIntos = _joinIntos is null ? [spec] : [.. _joinIntos, spec];
        b._joins = _joins is null ? [.. joins] : [.. _joins, .. joins];
        return b;
    }
    /// <summary>
    /// Rejects projecting a builder that carries a <c>JoinInto</c>. <c>Select</c> keeps the denormalized
    /// joins without stitching (repeating parents, never grouping children), and <c>As</c> would drop the
    /// stitching metadata altogether, so both silently produce the wrong shape; only the list terminals
    /// stitch and they are the supported way to materialize <c>JoinInto</c> collections.
    /// </summary>
    private void EnsureNoJoinIntos(string method)
    {
        if (_joinIntos is { Count: > 0 })
            throw new NotSupportedException(
                $"JoinInto cannot be combined with {method}; declare JoinInto on a plain entity source and materialize with ToList/ToListAsync.");
    }
    /// <summary>
    /// Rejects a projection or join over a builder that carries eager-load state, because that state
    /// cannot survive the composition: a <c>Select</c>/<c>As</c> command has no loader, and a
    /// <c>Join</c>/<c>Apply</c> produces a <c>JoinedEntityBuilder</c> that carries neither the load
    /// specifications nor the single-query mode, so the collections would silently stay empty.
    /// </summary>
    /// <param name="method">The composition method being rejected.</param>
    private void EnsureNoEagerLoadState(string method)
    {
        if (_loadSpecs is { Count: > 0 })
            throw new NotSupportedException(
                $"LoadWith cannot be combined with {method}; materialize the eager-loaded query with " +
                "ToList/ToListAsync (or ToArray/ToArrayAsync) before applying the modifier.");

        if (_singleQuery)
            throw new NotSupportedException(
                $"AsSingleQuery cannot be combined with {method}; apply AsSingleQuery only to the final " +
                "eager-loaded query materialized with ToList/ToListAsync.");
    }
    /// <summary>
    /// Names the child-query shapes that single-query eager loading cannot fold into the join predicate.
    /// Only the child's own <c>Where</c> is supported; every other shape (ordering, paging, distinct,
    /// grouping, joins, table modifiers, ...) is reported so the single-query path can reject it instead
    /// of silently dropping it. A derived (<c>As</c>) child source is reported too: its condition/filters
    /// live inside the subquery, so the join cannot fold them in.
    /// </summary>
    /// <returns>The unsupported shape names; empty when the child query carries only a <c>Where</c>.</returns>
    internal IReadOnlyList<string> UnsupportedSingleQueryChildShapes()
    {
        var shapes = new List<string>();
        if (_sorting is { Count: > 0 })
            shapes.Add("OrderBy");

        if (!Paging.IsEmpty)
            shapes.Add("Limit/Offset/Page");

        if (IsDistinct)
            shapes.Add("Distinct");

        if (_group is not null)
            shapes.Add("GroupBy");

        if (_having is not null)
            shapes.Add("Having");

        if (_distinctOn is not null)
            shapes.Add("DistinctOn");

        if (_extremeRow is not null)
            shapes.Add("SelectWhereExtreme");

        if (_limitBy is not null)
            shapes.Add("LimitBy");

        if (_joins is { Count: > 0 })
            shapes.Add("Join");

        if (IsFinal)
            shapes.Add("Final");

        if (_preWhere is not null)
            shapes.Add("PreWhere");

        if (_arrayJoins is { Count: > 0 })
            shapes.Add("ArrayJoin");

        if (_tablesample is not null)
            shapes.Add("TableSample");

        if (SampleRatio is not null || SampleOffset != 0)
            shapes.Add("Sample");

        if (_temporal is not null)
            shapes.Add("ForSystemTime");

        if (_rowLock is not null)
            shapes.Add("ForUpdate/ForShare");

        if (_windows is { Count: > 0 })
            shapes.Add("Window");

        if (_settings is { Count: > 0 })
            shapes.Add("Settings");

        if (Ctes is { Count: > 0 })
            shapes.Add("Cte");

        if (TableHints is { Count: > 0 })
            shapes.Add("TableHint");

        if (IndexHints is not null)
            shapes.Add("IndexHint");

        if (TablesInScopeHints is { Count: > 0 })
            shapes.Add("TablesInScopeHint");

        if (_query is not null || _from?.SubQuery is not null)
            shapes.Add("derived (As) source");

        return shapes;
    }
    /// <summary>
    /// Returns a copy of this builder without the <c>JoinInto</c> joins (and their stitching metadata),
    /// so the non-list terminals evaluate the parent only. The source builder is unchanged.
    /// </summary>
    internal EntityBuilder<TEntity> WithoutJoinIntos()
    {
        if (_joinIntos is not { Count: > 0 } || _joins is null)
            return this;

        var b = Clone();
        var kept = new List<JoinExpression>(_joins.Count);
        foreach (var join in _joins)
        {
            if (!join.IsJoinInto)
                kept.Add(join);
        }

        b._joins = kept.Count == 0 ? null : kept;
        b._joinIntos = null;
        return b;
    }
    /// <summary>Builds the parent-only command for the non-list terminals (excludes the <c>JoinInto</c> joins).</summary>
    internal QueryCommand<TEntity> ToParentCommand() => WithoutJoinIntos().ToCommand();
    /// <summary>Builds a parent-only projection command for the aggregate terminals (excludes the <c>JoinInto</c> joins).</summary>
    internal QueryCommand<TResult> SelectParent<TResult>(Expression<Func<TEntity, TResult>> exp) => WithoutJoinIntos().SelectCore(exp);
    /// <summary>
    /// Materializes the single-query stitching metadata for <see cref="AsSingleQuery"/>: the existing
    /// <c>JoinInto</c> declarations (if any) followed by one explicit-key join per
    /// <see cref="LoadWith{TChild, TKey}"/> specification, together with the join clauses that render
    /// them. The child query's own <c>Where</c> condition is folded into each synthesized <c>ON</c>
    /// predicate by the eager-load specification, so it filters the children without dropping childless
    /// parents.
    /// </summary>
    /// <returns>The specs and joins in declaration order.</returns>
    internal (List<IJoinIntoSpec<TEntity>> Specs, List<JoinExpression> Joins) BuildSingleQueryJoins()
    {
        if (_joinIntos is not { Count: > 0 } && _joins is { } regular && regular.Exists(j => !j.IsJoinInto))
            throw new NotSupportedException(
                "AsSingleQuery single-query loading cannot be combined with other joins on the same builder; declare LoadWith on a plain entity source only.");

        // A many-to-many declaration contributes two joins while the single-query mapping assumes one join
        // per declaration; reject it before any join/spec index pairing can overflow.
        if (_joinIntos is { Count: > 0 } manyToManyIntos)
        {
            for (var i = 0; i < manyToManyIntos.Count; i++)
            {
                if (manyToManyIntos[i].IsManyToMany)
                    throw new NotSupportedException(
                        "AsSingleQuery single-query loading does not support a many-to-many JoinInto declaration; materialize the many-to-many JoinInto separately, or use split-query loading.");
            }
        }

        var specs = new List<IJoinIntoSpec<TEntity>>();
        var joins = new List<JoinExpression>();

        if (_joinIntos is { Count: > 0 })
        {
            specs.AddRange(_joinIntos);
            if (_joins is { } existingJoins)
            {
                // Pair each declaration with its own joins: a many-to-many declaration contributes a link
                // edge followed by a child edge, and only the child edge carries the child's selective
                // filter scope. Indexing by join position would hand a link edge the child scope and shift
                // every later declaration.
                var joinCursor = 0;
                for (var i = 0; i < _joinIntos.Count; i++)
                {
                    var spec = _joinIntos[i];
                    var linkEdges = spec.ItemTypes.Count - 1;

                    for (var j = 0; j < linkEdges && joinCursor < existingJoins.Count; j++)
                        joins.Add(existingJoins[joinCursor++]);

                    if (joinCursor < existingJoins.Count)
                    {
                        // The child's own selective filter scope, independent of the parent's scope. An
                        // empty child scope is normalized to null: null and empty both mean "inherit", so
                        // the join then carries the command's whole scope (including a parent
                        // IgnoreFilters()) instead of an explicit empty scope that would read as a
                        // selective decision.
                        var childScope = spec.ChildFilterScope;
                        joins.Add(WithChildFilterScope(existingJoins[joinCursor++], childScope.IsEmpty ? null : childScope));
                    }
                }

                for (; joinCursor < existingJoins.Count; joinCursor++)
                    joins.Add(existingJoins[joinCursor]);
            }
        }

        if (_loadSpecs is { Count: > 0 })
        {
            foreach (var load in _loadSpecs)
            {
                var spec = load.ToJoinIntoSpec(_dataProvider, out var childSource);
                specs.Add(spec);
                joins.Add(new JoinExpression(spec.Predicate, spec.JoinType)
                {
                    From = childSource,
                    IsJoinInto = true,
                    JoinIntoIdentity = spec.Identity,
                    // The child query's own selective filter scope, independent of the parent's scope;
                    // an empty scope is normalized to null (both mean "inherit").
                    FilterScope = spec.ChildFilterScope.IsEmpty ? null : spec.ChildFilterScope,
                });
            }
        }

        return (specs, joins);
    }

    /// <summary>
    /// Returns a copy of <paramref name="source"/> carrying the child side's own selective filter scope,
    /// leaving the stored join (used by the plain <c>JoinInto</c> path, which inherits the parent's scope)
    /// untouched.
    /// </summary>
    /// <param name="source">The stored join to copy.</param>
    /// <param name="childFilterScope">The child side's selective global-query-filter scope, or <see langword="null"/> to inherit.</param>
    /// <returns>A copy of the join with an explicit child filter scope.</returns>
    private static JoinExpression WithChildFilterScope(JoinExpression source, QueryFilterScope? childFilterScope)
        => new(source.JoinCondition, source.JoinType)
        {
            From = source.From,
            EntityType = source.EntityType,
            Strictness = source.Strictness,
            IsGlobal = source.IsGlobal,
            JoinHint = source.JoinHint,
            TableHints = source.TableHints,
            ApplySource = source.ApplySource,
            OriginalJoinCondition = source.OriginalJoinCondition,
            IsJoinInto = source.IsJoinInto,
            JoinIntoIdentity = source.JoinIntoIdentity,
            SuppressCartesianWarning = source.SuppressCartesianWarning,
            FilterScope = childFilterScope,
        };

    /// <summary>
    /// Builds the single denormalized command the list terminals execute: one row per
    /// <c>(parent, child…)</c> pair, materialized through the entity-projection capability. When the
    /// builder pages parents on a SQL provider the parent subquery carries the limit/offset so the join
    /// wraps around it; the in-memory provider pages the deduplicated parents instead.
    /// </summary>
    internal QueryCommand CreateJoinIntoPairCommand() => CreatePairCommand(_joinIntos!, _joins);

    /// <summary>
    /// Builds the single denormalized command for the given stitching specifications and joins. Shared by
    /// the <c>JoinInto</c> terminals (which pass the builder's stored declarations) and the single-query
    /// <c>LoadWith</c> terminals (which synthesize an explicit-key join per load specification).
    /// </summary>
    /// <param name="specs">The stitching specifications, in declaration order.</param>
    /// <param name="joins">The join clauses that render the specifications, or <c>null</c> when there are none.</param>
    /// <returns>The command producing one row per <c>(parent, child…)</c> pair.</returns>
    internal QueryCommand CreatePairCommand(IReadOnlyList<IJoinIntoSpec<TEntity>> specs, List<JoinExpression>? joins)
    {
        var isSql = _dataProvider.NeedMapping;

        var itemTypesByDeclaration = new IReadOnlyList<Type>[specs.Count];
        for (var i = 0; i < specs.Count; i++)
        {
            // A many-to-many declaration projects a different link item per path: the synthetic link the
            // derived row_number() source produces on SQL, or the junction entity the in-memory engine
            // joins directly.
            itemTypesByDeclaration[i] = specs[i] is IJoinIntoManyToManySpec manyToMany
                ? manyToMany.GetItemTypes(isSql)
                : specs[i].ItemTypes;

            // Re-publish the synthetic link mapping (and the junction it is derived from) before
            // preparation so a DataContextCache.Clear() between declaration and execution cannot leave
            // the derived junction link unregistered. SQL-only: the in-memory path reads the junction
            // entity and never touches the link type.
            if (isSql && specs[i] is IJoinIntoManyToManySpec manyToManySpec)
            {
                JunctionLinkSourceFactory.EnsureRegistered(specs[i].ItemTypes[0]);
                JunctionLinkSourceFactory.EnsureJunctionRegistered(manyToManySpec.JunctionEntityType);
            }
        }

        var projectionType = JoinIntoProjectionFactory.Create(typeof(TEntity), itemTypesByDeclaration);

        var sourceType = isSql ? _sourceEntityType ?? typeof(TEntity) : projectionType;
        LambdaExpression? condition = _condition;
        var sorting = _sorting?.ToArray();
        LambdaExpression? group = _group;
        LambdaExpression? having = _having;
        FromExpression? from = null;

        // The in-memory provider executes joins through its projection engine, so its command source
        // type is the projection; a parent-typed WHERE/ORDER BY/GROUP BY/HAVING must be re-rooted onto
        // the parent item. The SQL provider keeps the parent source type and expands the projection
        // from metadata instead.
        if (!isSql)
        {
            var projectionParameter = Expression.Parameter(projectionType, "p");
            var parentItem = Expression.Property(projectionParameter, "Item1");

            if (condition is not null)
            {
                var body = new ReplaceTargetParameterVisitor(condition.Parameters[0], parentItem).Visit(condition.Body);
                condition = Expression.Lambda(body, projectionParameter);
            }

            if (sorting is not null)
            {
                for (var i = 0; i < sorting.Length; i++)
                {
                    if (sorting[i].SortExpression is not LambdaExpression { Parameters.Count: 1 } sort)
                        continue;

                    var body = new ReplaceTargetParameterVisitor(sort.Parameters[0], parentItem).Visit(sort.Body);
                    sorting[i] = new Sorting(Expression.Lambda(body, projectionParameter)) { Direction = sorting[i].Direction };
                }
            }

            if (group is not null)
            {
                var body = new ReplaceTargetParameterVisitor(group.Parameters[0], parentItem).Visit(group.Body);
                group = Expression.Lambda(body, projectionParameter);
            }

            if (having is not null)
            {
                var body = new ReplaceTargetParameterVisitor(having.Parameters[0], parentItem).Visit(having.Body);
                having = Expression.Lambda(body, projectionParameter);
            }
        }

        // Parent paging applies exactly once. On a SQL provider the join wraps around a paged parent
        // subquery; the in-memory provider pages the deduplicated parents in the stitcher. The pair
        // command itself must not carry the limit/offset, or the denormalized rows (not parents) page.
        if (isSql && !Paging.IsEmpty)
        {
            from = new FromExpression(ToParentCommand());
            condition = null;
        }

        var cmd = _dataProvider.CreateCommand(projectionType, new QueryDefinition
        {
            SrcType = sourceType,
            // Set on both providers: the SQL path uses it to expand the projection's entity items, the
            // in-memory path uses it to resolve the parent entity behind the Projection<…> source so the
            // parent's global query filters still apply (QueryPreparer.InjectMainSourceFilters).
            ProjectionType = projectionType,
            Condition = condition,
            FilterScope = _filterScope,
            Joins = isSql ? joins?.ToArray() : BuildInMemoryJoinIntos(specs, joins),
            Paging = default,
            Sorting = sorting,
            Group = group,
            Having = having,
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

        cmd.From = from ?? ResolveSource();

        if (Ctes is not null)
            cmd.Ctes = Ctes;

        ApplyCommandOptions(cmd);

        // A query with two or more collection navigations renders the cartesian product of the parent
        // rows; warn once per preparation unless a declaration suppressed it. The count excludes
        // one-to-one references (IsCollection false), and this flag is command state, not plan-key state.
        var collectionCount = 0;
        for (var i = 0; i < specs.Count; i++)
        {
            if (specs[i].IsCollection)
                collectionCount++;
        }

        var suppressed = false;
        if (joins is not null)
        {
            for (var i = 0; i < joins.Count; i++)
            {
                if (joins[i].SuppressCartesianWarning)
                {
                    suppressed = true;
                    break;
                }
            }
        }

        cmd.PendingJoinIntoCartesianWarning = collectionCount >= 2 && !suppressed;

        return cmd;
    }

    /// <summary>
    /// Rewrites the <c>JoinInto</c> join conditions for the in-memory provider. Its join engine threads
    /// an accumulated projection through the joins, so the second and later conditions must read the
    /// parent through <c>Item1</c> of that projection rather than through the bare parent parameter. A
    /// many-to-many declaration is rebuilt entirely: its stored link edge joins the derived
    /// <c>row_number()</c> source (SQL-only), so in memory the junction entity is joined directly and the
    /// junction instance is the link item, whose projection slot the child edge reads.
    /// </summary>
    /// <param name="specs">The stitching specifications, in declaration order.</param>
    /// <param name="source">The stored SQL joins, in declaration order, or <c>null</c> when there are none.</param>
    /// <returns>The joins the in-memory engine executes, or <c>null</c> when there are none.</returns>
    private JoinExpression[]? BuildInMemoryJoinIntos(IReadOnlyList<IJoinIntoSpec<TEntity>> specs, List<JoinExpression>? source)
    {
        if (source is null)
            return null;

        // By contract every preceding join is a JoinInto: mixing regular joins with JoinIntos is
        // rejected at declaration time (see AddJoinInto/AddSemiAntiJoin/CreateJoined). The in-memory
        // join engine threads an accumulated Projection<TEntity, …> through every join, and each
        // declaration's joins append their items in turn.
        var joins = new List<JoinExpression>(source.Count);
        var precedingTypes = new List<Type>(source.Count);
        var cursor = 0;

        for (var i = 0; i < specs.Count && cursor < source.Count; i++)
        {
            var spec = specs[i];

            if (spec is IJoinIntoManyToManySpec manyToMany)
            {
                if (cursor + 1 >= source.Count)
                    throw new InvalidOperationException(
                        "A many-to-many JoinInto declaration requires a link join followed by a child join.");

                joins.Add(BuildInMemoryManyToManyLinkJoin(manyToMany, source[cursor++], precedingTypes));
                precedingTypes.Add(manyToMany.JunctionEntityType);

                joins.Add(BuildInMemoryManyToManyChildJoin(spec, manyToMany, source[cursor++], precedingTypes));
                precedingTypes.Add(spec.ChildEntityType);
                continue;
            }

            var join = source[cursor++];

            // The first join already starts from the bare parent (arity 1); every later JoinInto reads
            // the parent through Item1 of the projection accumulated so far.
            if (join.IsJoinInto && joins.Count > 0 && join.JoinCondition is { Parameters.Count: 2 } condition)
            {
                var leftType = JoinIntoProjectionFactory.Create(
                    typeof(TEntity),
                    precedingTypes.Select(static type => (IReadOnlyList<Type>)new[] { type }).ToArray());
                var leftParameter = Expression.Parameter(leftType, "l");
                var rewritten = ReRootJoinCondition(condition, leftParameter);
                // Re-root the original condition too: the preparer injects the joined entity's global
                // filters from this field and installs the result as the join condition, so a bare-parent
                // original would overwrite the re-rooted condition below with the wrong parameter.
                var original = join.OriginalJoinCondition is { Parameters.Count: 2 } originalCondition
                    ? ReRootJoinCondition(originalCondition, leftParameter)
                    : join.OriginalJoinCondition;

                joins.Add(new JoinExpression(rewritten, join.JoinType)
                {
                    From = join.From,
                    EntityType = join.EntityType,
                    Strictness = join.Strictness,
                    IsGlobal = join.IsGlobal,
                    JoinHint = join.JoinHint,
                    TableHints = join.TableHints,
                    OriginalJoinCondition = original,
                    IsJoinInto = true,
                    JoinIntoIdentity = join.JoinIntoIdentity,
                    SuppressCartesianWarning = join.SuppressCartesianWarning,
                    // Preserve the child's own filter scope; dropping it would make the 2nd+ spec
                    // inherit the parent's IgnoreFilters() and silently apply the wrong child filters.
                    FilterScope = join.FilterScope,
                });
            }
            else
            {
                joins.Add(join);
            }

            precedingTypes.Add(JoinSecondType(join));
        }

        // Defensive: the declaration and join lists are paired by construction, but any unpaired trailing
        // join is kept rather than silently dropped.
        while (cursor < source.Count)
            joins.Add(source[cursor++]);

        return joins.ToArray();
    }

    /// <summary>
    /// Builds the in-memory parent-to-junction edge of a many-to-many declaration: the junction entity is
    /// joined directly (there is no derived <c>row_number()</c> source in memory), so the link item is
    /// the junction instance. The condition is <c>(p, j) =&gt; p.principalKey == j.parentForeignKey</c> and
    /// is re-rooted onto the accumulated projection for any declaration after the first.
    /// </summary>
    private JoinExpression BuildInMemoryManyToManyLinkJoin(
        IJoinIntoManyToManySpec spec, JoinExpression stored, List<Type> precedingTypes)
    {
        var parentParameter = Expression.Parameter(typeof(TEntity), "p");
        var junctionParameter = Expression.Parameter(spec.JunctionEntityType, "j");
        var equality = JoinIntoSpecHelpers.BuildKeyEquality(
            Expression.Property(parentParameter, spec.ParentPrincipalKey),
            Expression.Property(junctionParameter, spec.JunctionParentForeignKey),
            spec.ParentPrincipalKey.PropertyType,
            "JoinInto");
        LambdaExpression condition = Expression.Lambda(equality, parentParameter, junctionParameter);

        if (precedingTypes.Count > 0)
        {
            var leftType = JoinIntoProjectionFactory.Create(
                typeof(TEntity),
                precedingTypes.Select(static type => (IReadOnlyList<Type>)new[] { type }).ToArray());
            condition = ReRootJoinCondition(condition, Expression.Parameter(leftType, "l"));
        }

        return new JoinExpression(condition, stored.JoinType)
        {
            From = new FromExpression(spec.JunctionEntityType),
            EntityType = spec.JunctionEntityType,
            Strictness = stored.Strictness,
            IsGlobal = stored.IsGlobal,
            JoinHint = stored.JoinHint,
            TableHints = stored.TableHints,
            OriginalJoinCondition = condition,
            IsJoinInto = true,
            JoinIntoIdentity = stored.JoinIntoIdentity,
            SuppressCartesianWarning = stored.SuppressCartesianWarning,
        };
    }

    /// <summary>
    /// Builds the in-memory junction-to-child edge of a many-to-many declaration: the condition is
    /// <c>(l, c) =&gt; c.childKey == l.ItemN.childForeignKey &amp;&amp; &lt;user predicate over Item1 and c&gt;</c>,
    /// where <c>ItemN</c> is the junction slot the link edge just appended. Unlike a direct declaration
    /// the left parameter is the accumulated projection, so the condition is <b>not</b> re-rooted again.
    /// </summary>
    private JoinExpression BuildInMemoryManyToManyChildJoin(
        IJoinIntoSpec<TEntity> spec, IJoinIntoManyToManySpec manyToMany, JoinExpression stored, List<Type> precedingTypes)
    {
        // The link edge appended the junction as the last item; its 1-based Item slot is parent + the
        // accumulated items, the junction being the last of them.
        var leftType = JoinIntoProjectionFactory.Create(
            typeof(TEntity),
            precedingTypes.Select(static type => (IReadOnlyList<Type>)new[] { type }).ToArray());

        var predicate = spec.Predicate;
        var leftParameter = Expression.Parameter(leftType, "l");
        var childParameter = predicate.Parameters[1];
        // The link edge is an outer join, so a parent without a junction row reaches this edge as a
        // projection whose junction item is null. The junction accessor must be guarded before it is
        // dereferenced, exactly as SQL's NULL-propagating ON predicate would be (a null junction matches
        // no child, it does not throw).
        var junctionItem = Expression.Property(leftParameter, $"Item{precedingTypes.Count + 1}");
        var equality = JoinIntoSpecHelpers.BuildKeyEquality(
            Expression.Property(childParameter, manyToMany.ChildKey),
            Expression.Property(junctionItem, manyToMany.JunctionChildForeignKey),
            manyToMany.ChildKey.PropertyType,
            "JoinInto");
        var predicateBody = JoinIntoSpecHelpers.ReplaceParameter(
            predicate.Body, predicate.Parameters[0], Expression.Property(leftParameter, "Item1"));
        var body = Expression.AndAlso(equality, predicateBody);
        if (!manyToMany.JunctionEntityType.IsValueType)
        {
            var junctionGuard = Expression.NotEqual(
                junctionItem, Expression.Constant(null, manyToMany.JunctionEntityType));
            body = Expression.AndAlso(junctionGuard, body);
        }

        var condition = Expression.Lambda(body, leftParameter, childParameter);

        return new JoinExpression(condition, stored.JoinType)
        {
            From = stored.From,
            EntityType = stored.EntityType,
            Strictness = stored.Strictness,
            IsGlobal = stored.IsGlobal,
            JoinHint = stored.JoinHint,
            TableHints = stored.TableHints,
            OriginalJoinCondition = condition,
            IsJoinInto = true,
            JoinIntoIdentity = stored.JoinIntoIdentity,
            SuppressCartesianWarning = stored.SuppressCartesianWarning,
            FilterScope = stored.FilterScope,
        };
    }

    /// <summary>
    /// Re-roots a two-parameter join condition onto the projection the in-memory join engine has
    /// accumulated so far: the left-hand (parent) parameter reads through <c>Item1</c>, exactly like the
    /// rewritten <see cref="JoinExpression.JoinCondition"/>. Applied to the original condition as well,
    /// because the preparer injects the joined entity's global filters from that field and installs the
    /// result as the condition — a bare-parent original would silently overwrite the re-rooted condition.
    /// </summary>
    private static LambdaExpression ReRootJoinCondition(LambdaExpression condition, ParameterExpression leftParameter)
    {
        var body = new ReplaceTargetParameterVisitor(condition.Parameters[0], Expression.Property(leftParameter, "Item1"))
            .Visit(condition.Body);
        return Expression.Lambda(body, leftParameter, condition.Parameters[1]);
    }

    /// <summary>
    /// The type the in-memory engine appends to the accumulated projection for <paramref name="join"/>:
    /// the joined entity from the condition's second parameter, or <see cref="JoinExpression.EntityType"/>
    /// for a conditionless join.
    /// </summary>
    private static Type JoinSecondType(JoinExpression join)
        => join.JoinCondition is { Parameters.Count: 2 } condition
            ? condition.Parameters[1].Type
            : join.EntityType ?? typeof(object);
    /// <summary>
    /// Adds the ClickHouse <c>FINAL</c> modifier to the primary <c>FROM</c> table (a forced merge of a
    /// ReplacingMergeTree/CollapsingMergeTree before the read). Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsFinal"/>).
    /// </summary>
    internal EntityBuilder<TEntity> Final()
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
    internal EntityBuilder<TEntity> Settings(params (string Key, string Value)[] settings)
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
    internal EntityBuilder<TEntity> PreWhere(Expression<Func<TEntity, bool>> condition)
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
    internal EntityBuilder<TEntity> ArrayJoin<TArray>(Expression<Func<TEntity, TArray>> array)
        => AddArrayJoin(array, ArrayJoinKind.Inner);

    /// <summary>
    /// Adds the ClickHouse <c>LEFT ARRAY JOIN</c> clause: like <see cref="ArrayJoin{TArray}"/> but a row
    /// whose array is empty is kept (with the array column at its default). Requires a dialect that
    /// supports it (see <see cref="ISqlDialect.ArrayJoinClause"/>).
    /// </summary>
    internal EntityBuilder<TEntity> LeftArrayJoin<TArray>(Expression<Func<TEntity, TArray>> array)
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
    internal EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> array)
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
    internal EntityBuilder<ArrayJoinProjection<TEntity, TElement>> LeftArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> array)
        => ToArrayJoinElement(array, ArrayJoinKind.Left);

    private EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ToArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>> array, ArrayJoinKind kind)
    {
        ArgumentNullException.ThrowIfNull(array);
        EnsureNoEagerLoadState(kind == ArrayJoinKind.Left ? nameof(LeftArrayJoinElement) : nameof(ArrayJoinElement));

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
    /// Selects the row(s) with the maximum <paramref name="valueSelector"/> value, optionally per
    /// <paramref name="groupBy"/> group. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsSelectWhereMinMax"/>).
    /// </summary>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <param name="valueSelector">The value whose maximum is compared.</param>
    /// <param name="ties">Whether to keep a single row or every tied row.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole result.</param>
    public EntityBuilder<TEntity> SelectWhereMax<TValue>(
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => SelectWhereExtreme(ExtremeKind.Max, valueSelector, ties, groupBy);

    /// <summary>
    /// Selects the row(s) with the minimum <paramref name="valueSelector"/> value, optionally per
    /// <paramref name="groupBy"/> group. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsSelectWhereMinMax"/>).
    /// </summary>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <param name="valueSelector">The value whose minimum is compared.</param>
    /// <param name="ties">Whether to keep a single row or every tied row.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole result.</param>
    public EntityBuilder<TEntity> SelectWhereMin<TValue>(
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => SelectWhereExtreme(ExtremeKind.Min, valueSelector, ties, groupBy);

    /// <summary>
    /// Selects the row(s) with the maximum <paramref name="valueSelector"/> value and projects each with
    /// <paramref name="projection"/>, optionally per <paramref name="groupBy"/> group. Requires a dialect
    /// that supports it (see <see cref="ISqlDialect.SupportsSelectWhereMinMax"/>).
    /// </summary>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <typeparam name="TResult">The projection type.</typeparam>
    /// <param name="valueSelector">The value whose maximum is compared.</param>
    /// <param name="projection">The projection applied to the surviving row(s).</param>
    /// <param name="ties">Whether to keep a single row or every tied row.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole result.</param>
    public QueryCommand<TResult> SelectWhereMax<TValue, TResult>(
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => SelectWhereExtreme(ExtremeKind.Max, valueSelector, projection, ties, groupBy);

    /// <summary>
    /// Selects the row(s) with the minimum <paramref name="valueSelector"/> value and projects each with
    /// <paramref name="projection"/>, optionally per <paramref name="groupBy"/> group. Requires a dialect
    /// that supports it (see <see cref="ISqlDialect.SupportsSelectWhereMinMax"/>).
    /// </summary>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <typeparam name="TResult">The projection type.</typeparam>
    /// <param name="valueSelector">The value whose minimum is compared.</param>
    /// <param name="projection">The projection applied to the surviving row(s).</param>
    /// <param name="ties">Whether to keep a single row or every tied row.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole result.</param>
    public QueryCommand<TResult> SelectWhereMin<TValue, TResult>(
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => SelectWhereExtreme(ExtremeKind.Min, valueSelector, projection, ties, groupBy);

    private EntityBuilder<TEntity> SelectWhereExtreme<TValue>(
        ExtremeKind kind,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties,
        Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        var b = Clone();
        b._extremeRow = new ExtremeRowClause(kind, valueSelector, ties, groupBy, null);

        return b;
    }

    private QueryCommand<TResult> SelectWhereExtreme<TValue, TResult>(
        ExtremeKind kind,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties,
        Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(projection);

        var b = Clone();
        b._extremeRow = new ExtremeRowClause(kind, valueSelector, ties, groupBy, projection);

        // Build the projection command over the same source so the command's select list is the
        // user projection and the extreme-row clause rides on that same command. Wrapping it in a
        // derived query would leave the outer command with a null select list, which the preparer
        // rejects ("Select must return new anonymous type").
        return b.Select(projection);
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
        EnsureNoEagerLoadState(nameof(Pivot));

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
        EnsureNoEagerLoadState(nameof(Unpivot));

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
            || _distinctOn is not null || _extremeRow is not null || _windows is { Count: > 0 } || IsFinal || SampleRatio is not null
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
    /// (see <see cref="ISqlDialect.SupportsWithTies"/>). Cannot be combined with <see cref="Distinct"/>
    /// or <see cref="DistinctOn{TResult}"/>: this combination is rejected during SQL generation with
    /// <see cref="BuildSqlCommandException"/>; validation is not performed by the <c>WithTies</c>
    /// fluent call itself.
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
    internal EntityBuilder<TEntity> LimitBy<TResult>(int limit, Expression<Func<TEntity, TResult>> exp)
        => LimitBy(limit, 0, exp);
    /// <summary>
    /// Adds a <c>LIMIT offset, n BY expr</c> clause (ClickHouse): skips <paramref name="offset"/> rows
    /// and then returns at most <paramref name="limit"/> rows per distinct key. <paramref name="limit"/>
    /// must be positive and <paramref name="offset"/> non-negative. Requires a dialect that supports it
    /// (see <see cref="ISqlDialect.LimitBy"/>).
    /// </summary>
    internal EntityBuilder<TEntity> LimitBy<TResult>(int limit, int offset, Expression<Func<TEntity, TResult>> exp)
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
        dst._filterScope = _filterScope;
        dst._having = _having;
        dst._arrayJoins = _arrayJoins is null ? null : [.. _arrayJoins];
        dst._windows = _windows is null ? null : [.. _windows];
        dst._arrayJoinKind = _arrayJoinKind;
        dst._sourceEntityType = _sourceEntityType;
        dst._bindArrayJoinElement = _bindArrayJoinElement;
        dst._loadSpecs = _loadSpecs is null ? null : [.. _loadSpecs];
        dst._joinIntos = _joinIntos is null ? null : [.. _joinIntos];
        dst._joins = _joins is null ? null : [.. _joins];
        dst._singleQuery = _singleQuery;
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
        dst._tableNameOverride = _tableNameOverride;
        dst._schemaOverride = _schemaOverride;
        dst._databaseOverride = _databaseOverride;
        dst._serverOverride = _serverOverride;
        dst._tableExpression = _tableExpression;
        dst.IsDistinct = IsDistinct;
        dst.GroupingType = GroupingType;
        dst.GroupingSets = GroupingSets;
        dst.GroupByWithTotals = GroupByWithTotals;
        dst.LimitByClause = LimitByClause;
        dst.DistinctOnClause = DistinctOnClause;
        dst.ExtremeRowClause = ExtremeRowClause;
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
        dst.TablesInScopeHints = TablesInScopeHints;
        dst._subQueryHint = _subQueryHint;
        dst.QuoteIdentifiers = QuoteIdentifiers;
        dst.NamingConvention = NamingConvention;
        dst.KeywordCase = KeywordCase;
        dst.Tag = Tag;
        dst.CommandTimeout = CommandTimeout;
        dst.Ctes = Ctes;
    }
    /// <summary>
    /// Creates a typed copy of this raw-source builder with <paramref name="binding"/> attached to the
    /// source, enforcing the <c>BindEntity</c> guards. Used by
    /// <see cref="EntityBuilderExtensions.BindEntity{TEntity}(EntityBuilder{TableAlias}, IReadOnlyCollection{string})"/>.
    /// </summary>
    /// <typeparam name="TResult">The bound entity type.</typeparam>
    /// <param name="binding">The binding to attach.</param>
    /// <returns>A new typed builder; the original is not mutated.</returns>
    /// <exception cref="NotSupportedException">The receiver is not a direct <c>FromSql</c>/<c>From(string)</c> source, or <typeparamref name="TResult"/> is exactly <see cref="TableAlias"/> (a mapped subclass is accepted).</exception>
    /// <exception cref="InvalidOperationException">The source has already been composed (predicate, projection, join or another query operator); a CTE declaration or a sub-query hint is not composition and does not reject the call.</exception>
    internal EntityBuilder<TResult> BindEntitySource<TResult>(FromExpression.EntityBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (typeof(TResult) == typeof(TableAlias))
            throw new NotSupportedException("An entity binding cannot target TableAlias; bind a mapped entity type instead.");

        var raw = _from;
        var isRawSql = raw?.RawSqlSource is not null;
        var isNamedTable = raw is null && !string.IsNullOrEmpty(_table);
        if (!isRawSql && !isNamedTable)
            throw new NotSupportedException("BindEntity can only be applied to a direct FromSql or From(string) source.");

        if (HasCompositionState)
            throw new InvalidOperationException("BindEntity must be called immediately after FromSql/From(string), before any predicate, projection, join or other query operator.");

        var bound = isRawSql
            ? raw!.WithEntityBinding(binding)
            : new FromExpression(_table!).WithEntityBinding(binding);

        var dst = new EntityBuilder<TResult>(_dataProvider) { Logger = Logger };
        CopyProjectionIndependentStateTo(dst);
        dst._from = bound;
        dst._table = null;
        return dst;
    }
    /// <summary>
    /// Whether the builder has been composed past its source (filter, projection, join, grouping,
    /// paging or another query operator), which makes a later <c>BindEntity</c> invalid.
    /// </summary>
    private bool HasCompositionState =>
        _condition is not null
        || _query is not null
        || _joins is { Count: > 0 }
        || _group is not null
        || _having is not null
        || _sorting is { Count: > 0 }
        || _joinIntos is { Count: > 0 }
        || _loadSpecs is { Count: > 0 }
        || _limitBy is not null
        || _distinctOn is not null
        || _extremeRow is not null
        || _preWhere is not null
        || _arrayJoins is { Count: > 0 }
        || _windows is { Count: > 0 }
        || _tableNameOverride is not null
        || _schemaOverride is not null
        || _databaseOverride is not null
        || _serverOverride is not null
        || _tableExpression is not null
        || IsDistinct
        || _singleQuery
        || !_filterScope.IsEmpty
        || _tablesample is not null
        || _temporal is not null
        || _rowLock is not null
        || TableHints is { Count: > 0 }
        || IndexHints is { Count: > 0 }
        || SettingsList is { Count: > 0 }
        || GroupingType != NextORM.Core.GroupingType.None
        || GroupingSets is { Count: > 0 }
        || GroupByWithTotals
        || IsFinal
        || SampleRatio is not null
        || SampleOffset != 0
        || TablesInScopeHints is { Count: > 0 }
        || _sourceEntityType is not null
        || _bindArrayJoinElement
        || !Paging.IsEmpty;
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
        EnsureNoEagerLoadState(nameof(SelectMany));

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
        EnsureNoEagerLoadState(nameof(SelectMany));

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
        EnsureNoEagerLoadState(nameof(GroupJoin));

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
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>
    /// Adds a left outer join: every left-hand row is kept, and right-hand columns are <c>NULL</c>
    /// when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>
    /// Adds a right outer join: every right-hand row is kept, and left-hand columns are <c>NULL</c>
    /// when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>
    /// Adds a full outer join: unmatched rows from both sides are kept, with the other side's columns
    /// set to <c>NULL</c>.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>
    /// Adds a cross join producing the Cartesian product of the two sources; there is no <c>ON</c>
    /// condition.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>. A cross join rejects every modifier at render time.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row (<c>CROSS APPLY</c> / <c>CROSS JOIN LATERAL</c>).
    /// There is no <c>ON</c> condition.
    /// </summary>
    /// <param name="_">The builder for the applied entity; only its source is used.</param>
    /// <param name="options">Optional per-join configuration; an APPLY rejects strictness/GLOBAL/table hints at render time but folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the applied projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(EntityBuilder<TJoinEntity> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>
    /// Applies <paramref name="_"/> to every left-hand row, preserving left-hand rows with an empty
    /// result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). There is no <c>ON</c> condition.
    /// </summary>
    /// <param name="_">The builder for the applied entity; only its source is used.</param>
    /// <param name="options">Optional per-join configuration; see <see cref="CrossApply{TJoinEntity}(EntityBuilder{TJoinEntity}, Action{JoinOptions}?)"/>.</param>
    /// <returns>A builder over the applied projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(EntityBuilder<TJoinEntity> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    // -----------------------------------------------------------------------------------------------
    // #159 direct Cte<T> overloads. The descriptor is converted with THIS builder's context
    // (DataProvider.From(cte)) and routed through the existing EntityBuilder-source overload, so CTE
    // declaration propagation, source resolution, filters, state and caching match ctx.From(cte).
    // -----------------------------------------------------------------------------------------------

    /// <summary>Adds an inner join over the typed CTE <paramref name="cte"/>; identical to <see cref="Join{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}}, Action{JoinOptions}?)"/> with this builder's context.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => Join(cte, joinCondition, null);
    /// <summary>Adds an inner join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return Join(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a left outer join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => LeftJoin(cte, joinCondition, null);
    /// <summary>Adds a left outer join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return LeftJoin(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a right outer join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => RightJoin(cte, joinCondition, null);
    /// <summary>Adds a right outer join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return RightJoin(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a full outer join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => FullJoin(cte, joinCondition, null);
    /// <summary>Adds a full outer join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return FullJoin(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a cross join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(Cte<TJoinEntity> cte)
        => CrossJoin(cte, null);
    /// <summary>Adds a cross join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(Cte<TJoinEntity> cte, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        return CrossJoin(_dataProvider.From(cte), options);
    }

    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>CROSS APPLY</c>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Cte<TJoinEntity> cte)
        => CrossApply(cte, null);
    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>CROSS APPLY</c> and per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Cte<TJoinEntity> cte, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        return CrossApply(_dataProvider.From(cte), options);
    }

    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>OUTER APPLY</c>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Cte<TJoinEntity> cte)
        => OuterApply(cte, null);
    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>OUTER APPLY</c> and per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Cte<TJoinEntity> cte, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        return OuterApply(_dataProvider.From(cte), options);
    }

    /// <summary>
    /// Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/> and returns this builder
    /// unchanged in shape: only the left-hand columns survive, and a left-hand row is kept once when at
    /// least one right-hand row matches. Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsSemiAntiJoin"/>).
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the left-hand columns, carrying the semi join.</returns>
    internal EntityBuilder<TEntity> SemiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(GetJoinSource(_), joinCondition, JoinType.Semi, options);
    /// <summary>
    /// Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>: only the left-hand columns
    /// survive, and a left-hand row is kept when no right-hand row matches (the complement of
    /// <see cref="SemiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}}, Action{JoinOptions})"/>).
    /// Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsSemiAntiJoin"/>).
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the left-hand columns, carrying the anti join.</returns>
    internal EntityBuilder<TEntity> AntiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(GetJoinSource(_), joinCondition, JoinType.Anti, options);
    /// <summary>
    /// Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>: the two sources are paired by row
    /// position with no <c>ON</c> condition, and the projection exposes both sides (as many rows as the
    /// shorter side). Requires a dialect that supports it (see <see cref="ISqlDialect.SupportsPasteJoin"/>).
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    internal JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <inheritdoc cref="SemiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}}, Action{JoinOptions})"/>
    internal EntityBuilder<TEntity> SemiJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(new FromExpression(query), joinCondition, JoinType.Semi, options);
    /// <inheritdoc cref="AntiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}}, Action{JoinOptions})"/>
    internal EntityBuilder<TEntity> AntiJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(new FromExpression(query), joinCondition, JoinType.Anti, options);
    /// <inheritdoc cref="PasteJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Action{JoinOptions})"/>
    internal JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.Paste, null, options);
    private EntityBuilder<TEntity> AddSemiAntiJoin(FromExpression rightSource, LambdaExpression joinCondition, JoinType joinType, Action<JoinOptions>? options = null)
    {
        if (_joinIntos is { Count: > 0 })
            throw new NotSupportedException(
                "JoinInto cannot be combined with other joins on the same builder; declare JoinInto on a plain entity source only.");

        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var b = Clone();
        var joins = b._joins;
        if (joins is null)
            b._joins = joins = _joins is null ? [] : [.. _joins];

        joins.Add(new JoinExpression(joinCondition, joinType)
        {
            From = rightSource,
            EntityType = null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });

        return b;
    }
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        // A TVF (or other explicit source) on either side is carried as a FromExpression: the right
        // side keeps the joined entity's own source, the left side keeps the one propagated below.
        var joined = CreateJoined<TJoinEntity>(new JoinExpression(joinCondition, joinType)
        {
            From = GetJoinSource(_),
            EntityType = joinCondition is null ? typeof(TJoinEntity) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        }, ResolveJoinBase());
        joined.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        ApplyWhereToJoined(joined);
        return joined;
    }

    /// <summary>
    /// Seam for generated alias-join builders (issue #113): adds <paramref name="_"/> as a join and
    /// returns the caller-created builder of a different projection shape, carrying this builder's join
    /// chain. The generated extension method supplies the target type through <paramref name="create"/>
    /// so the projection can expose lexical alias properties (for example <c>p.Buyer</c>) whose
    /// <see cref="JoinSlotAttribute"/> maps them to the right joined table. This overload serves the
    /// conditional operators (<c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/<c>FullJoin</c>) and delegates
    /// to the same construction path as their positional counterparts, parameterized by
    /// <paramref name="joinType"/>; the in-memory provider refuses it explicitly at construction.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="TJoinEntity">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    /// <exception cref="NotSupportedException">The in-memory provider cannot project named aliases.</exception>
    public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        EntityBuilder<TJoinEntity> _,
        Expression<Func<TEntity, TJoinEntity, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(_);
        ArgumentNullException.ThrowIfNull(joinCondition);

        return JoinAliasEntity<TNext, TNextEntity, TJoinEntity>(create, _, joinCondition, joinType, options);
    }

    /// <summary>
    /// Alias-join seam for the conditionless operators (<c>CrossJoin</c>/<c>CrossApply</c>/
    /// <c>OuterApply</c>): mirrors the positional conditionless overloads, so the resulting
    /// <see cref="JoinExpression"/> carries no <c>ON</c> predicate and an explicit
    /// <see cref="JoinExpression.EntityType"/>.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="TJoinEntity">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinType">The conditionless join kind to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    /// <exception cref="NotSupportedException">The in-memory provider cannot project named aliases.</exception>
    public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        EntityBuilder<TJoinEntity> _,
        JoinType joinType,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(_);

        return JoinAliasEntity<TNext, TNextEntity, TJoinEntity>(create, _, null, joinType, options);
    }

    /// <summary>
    /// Alias-join seam over a typed CTE for the conditional operators. The descriptor is converted with
    /// this builder's context and forwarded to
    /// <see cref="JoinAlias{TNext, TNextEntity, TJoinEntity}(Func{IDataContext, TNext}, EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}}, JoinType, Action{JoinOptions}?)"/>,
    /// so the CTE declaration set and the join source match the converted form.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="create"/>, <paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The in-memory provider cannot project named aliases.</exception>
    public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        Cte<TJoinEntity> cte,
        Expression<Func<TEntity, TJoinEntity, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);

        return JoinAliasEntity<TNext, TNextEntity, TJoinEntity>(create, _dataProvider.From(cte), joinCondition, joinType, options);
    }

    /// <summary>Alias-join seam over a typed CTE for the conditionless operators; see the conditional counterpart.</summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinType">The conditionless join kind to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> or <paramref name="cte"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The in-memory provider cannot project named aliases.</exception>
    public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        Cte<TJoinEntity> cte,
        JoinType joinType,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(cte);

        return JoinAliasEntity<TNext, TNextEntity, TJoinEntity>(create, _dataProvider.From(cte), null, joinType, options);
    }

    /// <summary>
    /// Correlated alias <c>CROSS APPLY</c>/<c>OUTER APPLY</c>: mirrors the positional
    /// <see cref="CrossApply{TJoinEntity}(Expression{Func{TEntity, EntityBuilder{TJoinEntity}}}, Action{JoinOptions})"/>
    /// overload and carries the source through
    /// <see cref="JoinExpression.ApplySource"/> instead of a plain <c>ON</c> condition.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="TJoinEntity">The entity type yielded by the applied source.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="source">A lambda receiving the left-hand row and returning the applied builder.</param>
    /// <param name="joinType">The APPLY kind to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the applied source.</returns>
    public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source,
        JoinType joinType,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => JoinAliasApply<TNext, TNextEntity, TJoinEntity>(create, source, joinType, options);

    /// <summary>
    /// Correlated alias <c>CROSS APPLY</c>/<c>OUTER APPLY</c> over a derived query; mirrors the
    /// positional <see cref="CrossApply{TJoinEntity}(Expression{Func{TEntity, QueryCommand{TJoinEntity}}}, Action{JoinOptions})"/>
    /// overload.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="TJoinEntity">The entity type yielded by the applied query.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="source">A lambda receiving the left-hand row and returning the applied query.</param>
    /// <param name="joinType">The APPLY kind to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the applied query.</returns>
    public TNext JoinAlias<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source,
        JoinType joinType,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => JoinAliasApply<TNext, TNextEntity, TJoinEntity>(create, source, joinType, options);

    /// <summary>
    /// Builds an alias join over a table-shaped <paramref name="_"/>, sharing the positional join
    /// construction path (<see cref="CreateAliasJoined{TNext,TNextEntity}"/> mirrors
    /// <see cref="CreateJoined{TJoinEntity}"/>), including <c>SourceFrom</c>/CTE propagation and a
    /// pre-join <c>Where</c> being moved onto the alias projection.
    /// </summary>
    private TNext JoinAliasEntity<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        EntityBuilder<TJoinEntity> _,
        LambdaExpression? joinCondition,
        JoinType joinType,
        Action<JoinOptions>? options)
        where TNext : EntityBuilder<TNextEntity>
    {
        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "Named alias join projections are not supported by the in-memory provider; run the query against a SQL provider.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var join = new JoinExpression(joinCondition, joinType)
        {
            From = GetJoinSource(_),
            EntityType = joinCondition is null ? typeof(TJoinEntity) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        };

        return CreateAliasJoined<TNext, TNextEntity>(create, join, _.Ctes, ResolveAliasJoinBase());
    }

    /// <summary>
    /// Builds a correlated alias APPLY, sharing the positional correlated-apply path
    /// (<see cref="JoinApply{TJoinEntity}"/>): the source is converted to a
    /// <see cref="QueryCommand"/> lambda and stored as <see cref="JoinExpression.ApplySource"/>.
    /// </summary>
    private TNext JoinAliasApply<TNext, TNextEntity, TJoinEntity>(
        Func<IDataContext, TNext> create,
        LambdaExpression source,
        JoinType joinType,
        Action<JoinOptions>? options)
        where TNext : EntityBuilder<TNextEntity>
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(source);

        if (typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                "A correlated CROSS/OUTER APPLY source cannot reference a join projection; apply it to a single-entity source instead.");

        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "A correlated CROSS/OUTER APPLY source is not supported by the in-memory provider; run the query against a SQL provider.");

        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var body = source.Body.Type == typeof(QueryCommand) ? source.Body : Expression.Convert(source.Body, typeof(QueryCommand));
        var applySource = Expression.Lambda(body, source.Parameters);

        var join = new JoinExpression(null, joinType)
        {
            From = new FromExpression(typeof(TJoinEntity)),
            EntityType = typeof(TJoinEntity),
            ApplySource = applySource,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        };

        return CreateAliasJoined<TNext, TNextEntity>(create, join, null, ResolveAliasJoinBase());
    }

    /// <summary>
    /// Wraps an alias join into the caller-created <typeparamref name="TNext"/>, carrying this
    /// builder's query state (via <see cref="ApplyJoinStateTo{TOther}"/>), join chain, CTE declarations
    /// and a pre-join <c>Where</c> onto the alias projection. The generic analogue of
    /// <see cref="CreateJoined{TJoinEntity}"/>.
    /// </summary>
    private TNext CreateAliasJoined<TNext, TNextEntity>(
        Func<IDataContext, TNext> create,
        JoinExpression join,
        IReadOnlyList<CteDefinition>? rightCtes,
        QueryCommand? query)
        where TNext : EntityBuilder<TNextEntity>
    {
        if (_joinIntos is { Count: > 0 })
            throw new NotSupportedException(
                "JoinInto cannot be combined with other joins on the same builder; declare JoinInto on a plain entity source only.");

        EnsureNoEagerLoadState("Join/Apply");

        var joined = create(_dataProvider);
        ApplyJoinStateTo(joined, query);

        var joins = _joins is null ? new List<JoinExpression>() : new List<JoinExpression>(_joins);
        joins.Add(join);
        joined.Joins = joins;

        joined.Ctes = CteMerge.Merge(Ctes, rightCtes);
        ApplyWhereToAliasJoined(joined);
        return joined;
    }

    /// <summary>
    /// Root-alias seam (issue #160, Phase 2): re-roots a plain source builder into a caller-created
    /// builder whose projection shape names the root slot 1 lexically (for example
    /// <c>AliasProjection_A1_Order&lt;Order&gt;</c>), preserving the source's query state (physical table
    /// or derived source, CTE declarations, overrides, source options and hints). The generated
    /// <c>.WithAlias(Alias.X)</c> extension supplies <paramref name="create"/>. It is root-only: a
    /// builder that already carries a join, <c>JoinInto</c> or a projection shape is rejected, and the
    /// in-memory provider fails closed because it cannot project named aliases.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the root alias.</typeparam>
    /// <typeparam name="TNextEntity">The root-alias projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's source state and no joins.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The in-memory provider cannot project named aliases, or the receiver is not the root source.</exception>
    public TNext AliasRoot<TNext, TNextEntity>(Func<IDataContext, TNext> create)
        where TNext : EntityBuilder<TNextEntity>
    {
        ArgumentNullException.ThrowIfNull(create);

        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "Named alias projections are not supported by the in-memory provider; run the query against a SQL provider.");

        if (_joins is { Count: > 0 } || _joinIntos is { Count: > 0 })
            throw new NotSupportedException(
                "WithAlias names the root source (slot 1) and must be applied before any Join/JoinInto.");

        if (typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                "WithAlias must be applied to a plain root source, not to a join or alias projection.");

        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        EnsureNoEagerLoadState("WithAlias");

        var rooted = create(_dataProvider);
        ApplyJoinStateTo(rooted, Query);

        // A mapped-entity root carries no explicit FROM source (it is resolved from entity metadata by
        // the planner from the query's source type). After re-rooting, that source type is the projection,
        // which is not a registered entity, so the projection's slot 1 could not be resolved. Materialize
        // the mapped root's physical source once, without touching the planner: every other root kind
        // already carries an explicit source (table name, FromExpression or derived Query).
        if (rooted.SourceFrom is null && rooted.Query is null && string.IsNullOrEmpty(rooted.Table))
        {
            rooted.SourceFrom = _dataProvider.GetFrom(_sourceEntityType ?? typeof(TEntity), null);
        }
        // A derived root source (From(builder) / From(QueryCommand<T>)) is carried as _query. After
        // re-rooting, the projection type would make ResolveJoinBase treat it as a joined projection that
        // cannot be joined again, even though the physical source is a derived query. Materialize it once
        // as an explicit derived-table FromExpression and clear _query, so the derived root is preserved
        // and aliased 't1', exactly like FromSql. The projection guard in ResolveJoinBase is not touched.
        else if (rooted.Query is not null)
        {
            rooted.SourceFrom = new FromExpression(rooted.Query);
            rooted.Query = null;
        }

        // A Where written before WithAlias is over the plain root entity; move it onto the projection's
        // Item1 so it keeps pointing at the same table after re-rooting.
        if (_condition is not null)
        {
            var sourceParameter = _condition.Parameters[0];
            var param = Expression.Parameter(typeof(TNextEntity), sourceParameter.Name);
            var item1 = typeof(TNextEntity).GetProperty(nameof(Projection<TEntity, object>.Item1), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new NotSupportedException(
                    $"WithAlias requires the root projection '{typeof(TNextEntity)}' to derive from a Projection type exposing Item1.");
            var item1Access = Expression.Property(param, item1);
            var body = new ReplaceTargetParameterVisitor(sourceParameter, item1Access).Visit(_condition.Body);
            rooted.Condition = Expression.Lambda<Func<TNextEntity, bool>>(body, param);
        }

        return rooted;
    }

    /// <summary>
    /// Copies the query state a join builder carries onto <paramref name="target"/>, which may have a
    /// different projection type. Shared by the positional <see cref="CreateJoined{TJoinEntity}"/> and
    /// the alias <see cref="CreateAliasJoined{TNext,TNextEntity}"/> so both observe the same
    /// <c>_query</c>/<c>SourceFrom</c>/modifier propagation. Overrides are copied only when the source
    /// stayed unresolved for the planner (<paramref name="query"/> is <see langword="null"/>), mirroring
    /// the positional path.
    /// </summary>
    private void ApplyJoinStateTo<TOther>(EntityBuilder<TOther> target, QueryCommand? query)
    {
        target.Logger = Logger;
        target.Table = Table;
        target.Query = query;
        target.IsDistinct = IsDistinct;
        target.GroupingType = GroupingType;
        target.GroupingSets = GroupingSets;
        target.GroupByWithTotals = GroupByWithTotals;
        target.LimitByClause = LimitByClause;
        target.DistinctOnClause = DistinctOnClause;
        target.TableSampleClause = TableSampleClause;
        target.TemporalClause = TemporalClause;
        target.RowLockClause = RowLockClause;
        target.IsFinal = IsFinal;
        target.SampleRatio = SampleRatio;
        target.SampleOffset = SampleOffset;
        target.SettingsList = SettingsList;
        target.PreWhereCondition = PreWhereCondition;
        target.ArrayJoins = ArrayJoins;
        target.ArrayJoinKind = ArrayJoinKind;
        target.TableHints = TableHints;
        target.IndexHints = IndexHints;
        target.IndexHintKind = IndexHintKind;
        target.TablesInScopeHints = TablesInScopeHints;
        target.Ctes = Ctes;
        target.QuoteIdentifiers = QuoteIdentifiers;
        target.NamingConvention = NamingConvention;
        target.KeywordCase = KeywordCase;
        target.SourceFrom = SourceFrom;
        target.FilterScope = _filterScope;

        if (query is null)
        {
            target._tableNameOverride = _tableNameOverride;
            target._schemaOverride = _schemaOverride;
            target._databaseOverride = _databaseOverride;
            target._serverOverride = _serverOverride;
            target._tableExpression = _tableExpression;
        }
    }

    /// <summary>
    /// Moves a <c>Where</c> written before an alias join onto the alias projection. Two shapes are
    /// handled: a <c>Where</c> over a derived query source is re-rooted onto the projection's
    /// <c>Item1</c> (mirroring <see cref="ApplyWhereToJoined{TJoinEntity}"/>), while a <c>Where</c>
    /// written between two alias joins is over the previous alias projection and each of its members is
    /// mapped by slot onto the extended projection (<c>Item1</c>-&gt;<c>Item1</c>,
    /// <c>Item2</c>/<c>Buyer</c>-&gt;<c>Item2</c>), so the predicate keeps pointing at the table it named.
    /// </summary>
    private void ApplyWhereToAliasJoined<TNextEntity>(EntityBuilder<TNextEntity> joined)
    {
        if (_condition is null)
            return;

        var sourceParameter = _condition.Parameters[0];
        var param = Expression.Parameter(typeof(TNextEntity), sourceParameter.Name);

        if (sourceParameter.Type.TryGetProjectionDimension(out _))
        {
            var rebased = new RebaseAliasProjectionVisitor(sourceParameter, param, typeof(TNextEntity)).Visit(_condition.Body);
            joined.Condition = Expression.Lambda<Func<TNextEntity, bool>>(rebased, param);
            return;
        }

        // A plain entity source turned the pre-join Where into the join's base subquery; it must not be
        // applied twice. Only a genuine derived query source (already a QueryCommand) is re-rooted here.
        if (_query is null)
            return;

        var item1 = typeof(TNextEntity).GetProperty(nameof(Projection<TEntity, object>.Item1), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new NotSupportedException(
                $"An alias join over a derived source requires the alias projection '{typeof(TNextEntity)}' to derive from a Projection type exposing Item1.");

        var item1Access = Expression.Property(param, item1);
        var body = new ReplaceTargetParameterVisitor(sourceParameter, item1Access).Visit(_condition.Body);
        joined.Condition = Expression.Lambda<Func<TNextEntity, bool>>(body, param);
    }

    /// <summary>
    /// Maps member accesses on a previous alias projection onto the extended projection by slot. A
    /// member's slot comes from <see cref="ProjectionAliasCache.GetMemberPosition"/> (the digits of
    /// <c>ItemN</c> or the <see cref="JoinSlotAttribute"/> of a generated alias), and the extended
    /// projection retains the same slot as <c>ItemN</c>.
    /// </summary>
    private sealed class RebaseAliasProjectionVisitor(ParameterExpression source, ParameterExpression target, Type targetType) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => ReferenceEquals(node, source) ? target : base.VisitParameter(node);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (ReferenceEquals(node.Expression, source))
            {
                var position = ProjectionAliasCache.GetMemberPosition(node.Member);
                if (position >= 0)
                {
                    var itemName = "Item" + (position + 1).ToString(CultureInfo.InvariantCulture);
                    var targetMember = targetType.GetProperty(itemName, BindingFlags.Public | BindingFlags.Instance);
                    if (targetMember is not null && targetMember.PropertyType == node.Type)
                        return Expression.Property(target, targetMember);
                }
            }

            return base.VisitMember(node);
        }
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
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.Inner, joinCondition, options);
    /// <summary>
    /// Adds a left outer join to the derived <paramref name="query"/>: every left-hand row is kept,
    /// and right-hand columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.Left, joinCondition, options);
    /// <summary>
    /// Adds a right outer join to the derived <paramref name="query"/>: every right-hand row is kept,
    /// and left-hand columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.Right, joinCondition, options);
    /// <summary>
    /// Adds a full outer join to the derived <paramref name="query"/>: unmatched rows from both sides
    /// are kept, with the other side's columns set to <c>NULL</c>.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="joinCondition">The join predicate over the two entities.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.Full, joinCondition, options);
    /// <summary>
    /// Adds a cross join to the derived <paramref name="query"/>, producing the Cartesian product of
    /// the two sources; there is no <c>ON</c> condition.
    /// </summary>
    /// <param name="query">The derived query to join.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(QueryCommand<TJoinEntity> query, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.Cross, null, options);
    /// <summary>Applies a derived query to every left-hand row (<c>CROSS APPLY</c>).</summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(QueryCommand<TJoinEntity> query, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.CrossApply, null, options);
    /// <summary>
    /// Applies a derived query to every left-hand row, preserving left-hand rows with an empty result
    /// (<c>OUTER APPLY</c>).
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(QueryCommand<TJoinEntity> query, Action<JoinOptions>? options = null)
        => JoinCore(query, JoinType.OuterApply, null, options);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row (<c>CROSS APPLY</c> /
    /// <c>CROSS JOIN LATERAL</c>). <paramref name="source"/> receives the left-hand row, so the query
    /// it builds may reference its columns; there is no <c>ON</c> condition.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source, Action<JoinOptions>? options = null)
        => JoinApply<TJoinEntity>(source, JoinType.CrossApply, options);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row, preserving left-hand rows with an
    /// empty result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). <paramref name="source"/>
    /// receives the left-hand row, so the query it builds may reference its columns.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source, Action<JoinOptions>? options = null)
        => JoinApply<TJoinEntity>(source, JoinType.OuterApply, options);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row (<c>CROSS APPLY</c> /
    /// <c>CROSS JOIN LATERAL</c>). <paramref name="source"/> receives the left-hand row, so the query
    /// it builds may reference its columns; there is no <c>ON</c> condition.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source, Action<JoinOptions>? options = null)
        => JoinApply<TJoinEntity>(source, JoinType.CrossApply, options);
    /// <summary>
    /// Applies a correlated derived query to every left-hand row, preserving left-hand rows with an
    /// empty result (<c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>). <paramref name="source"/>
    /// receives the left-hand row, so the query it builds may reference its columns.
    /// <typeparam name="TJoinEntity">The element type yielded by the applied query.</typeparam>
    /// </summary>
    public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source, Action<JoinOptions>? options = null)
        => JoinApply<TJoinEntity>(source, JoinType.OuterApply, options);
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinApply<TJoinEntity>(LambdaExpression source, JoinType joinType, Action<JoinOptions>? options = null)
    {
        if (typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                "A correlated CROSS/OUTER APPLY source cannot reference a join projection; apply it to a single-entity source instead.");

        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "A correlated CROSS/OUTER APPLY source is not supported by the in-memory provider; run the query against a SQL provider.");

        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        QueryCommand? queryBase = ResolveJoinBase();

        var body = source.Body.Type == typeof(QueryCommand) ? source.Body : Expression.Convert(source.Body, typeof(QueryCommand));
        var applySource = Expression.Lambda(body, source.Parameters);

        var joined = CreateJoined<TJoinEntity>(new JoinExpression(null, joinType)
        {
            From = new FromExpression(typeof(TJoinEntity)),
            EntityType = typeof(TJoinEntity),
            ApplySource = applySource,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
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
        if (_joinIntos is { Count: > 0 })
            throw new NotSupportedException(
                "JoinInto cannot be combined with other joins on the same builder; declare JoinInto on a plain entity source only.");

        // A positional Join/Apply applied to a projection-shaped builder (a generated alias-join
        // builder, or any other source whose entity is a join projection) would nest that projection
        // as the result's Item1. The planner resolves the physical primary table from Item1, so a
        // nested projection cannot be mapped to a table and only fails later with a confusing
        // BuildSqlCommandException. Fail closed here, at construction, with an actionable message.
        // A derived query (query is not null) keeps its own resolution path and is unaffected.
        if (query is null && typeof(TEntity).TryGetProjectionDimension(out _))
            throw new NotSupportedException(
                "A positional Join/Apply cannot follow an alias Join: the alias projection cannot be nested as the joined source. Keep the chain alias-based, or use positional joins without aliases.");

        EnsureNoEagerLoadState("Join/Apply");

        var cb = new JoinedEntityBuilder<TEntity, TJoinEntity>(_dataProvider, join);
        ApplyJoinStateTo(cb, query);
        return cb;
    }
    private JoinedEntityBuilder<TEntity, TJoinEntity> JoinCore<TJoinEntity>(QueryCommand<TJoinEntity> query, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        if (_windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        QueryCommand? queryBase = ResolveJoinBase();

        var joined = CreateJoined<TJoinEntity>(new JoinExpression(joinCondition, joinType)
        {
            From = new FromExpression(query),
            EntityType = joinCondition is null ? typeof(TJoinEntity) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        }, queryBase);
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

        // A projection source can be joined only once when it is the primary FROM source (its shape
        // cannot be reconstructed). An alias join chain already carries its joins and reconstructs the
        // extended projection explicitly, so a further alias join over it is valid.
        if (typeof(TEntity).TryGetProjectionDimension(out _) && _joins is not { Count: > 0 })
            throw new NotSupportedException(
                "A derived query as the primary FROM source can be joined only once; add further joins to the derived query instead.");

        if (HasNoNonWhereModifiers())
            return _query;

        throw new NotSupportedException(
            "A derived query as the primary FROM source supports only a Where clause before Join; put OrderBy/GroupBy/Distinct and other modifiers into the derived query.");
    }

    /// <summary>
    /// Resolves the base command for an alias join. A <c>Where</c> written between two alias joins is
    /// over the previous alias projection and is moved onto the extended projection by
    /// <see cref="ApplyWhereToAliasJoined{TNextEntity}"/>; wrapping it into a derived subquery would make
    /// the second join's condition reference the previous projection shape instead. Everything else
    /// keeps the positional behavior.
    /// </summary>
    private QueryCommand? ResolveAliasJoinBase()
    {
        if (_query is null && _condition is not null && _condition.Parameters[0].Type.TryGetProjectionDimension(out _))
            return null;

        return ResolveJoinBase();
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
    internal EntityBuilder<TEntity> WithTotals()
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
    /// Attaches hints to every physical table in the query's scope: on SQL Server each table (the
    /// primary source and every joined table) gets a <c>WITH (hint, ...)</c> suffix; on
    /// PostgreSQL/MySQL/MariaDB the hints are folded into the statement-level <c>/*+ ... */</c> comment.
    /// A dialect that supports neither rejects the command with <see cref="NotSupportedException"/>.
    /// The hints are emitted verbatim, so only use trusted values.
    /// </summary>
    /// <param name="hints">The hint texts; at least one non-empty.</param>
    /// <returns>A builder with the tables-in-scope hints applied.</returns>
    /// <exception cref="ArgumentException">No non-empty hint was supplied.</exception>
    public EntityBuilder<TEntity> WithTablesInScopeHint(params string[] hints)
    {
        var names = hints is { Length: > 0 }
            ? hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : [];

        if (names.Length == 0)
            throw new ArgumentException("A tables-in-scope hint must be a non-empty string.", nameof(hints));

        var b = Clone();
        b.TablesInScopeHints = names;
        return b;
    }
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
    /// Overrides the command timeout in seconds for the commands this builder creates: it takes
    /// precedence over the context default set with <c>DataContextBuilder.UseCommandTimeout</c>. A value
    /// of zero or less means the provider default. The override is part of the plan-cache key, so two
    /// otherwise identical queries with different timeouts do not share a cached command.
    /// </summary>
    /// <param name="seconds">The command timeout in seconds; zero or less uses the provider default.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithCommandTimeout(int seconds)
    {
        var b = Clone();

        b.CommandTimeout = seconds > 0 ? seconds : null;

        return b;
    }
    /// <summary>
    /// Overrides the physical table name of the primary source for this query only, for example
    /// <c>From&lt;IOrder&gt;().WithTableName("orders_archive")</c>. The mapped entity (and its columns) is
    /// unchanged, and other queries are unaffected. The name is emitted verbatim, or quoted when
    /// identifier quoting is enabled (see <see cref="WithQuotedIdentifiers"/>).
    /// </summary>
    /// <param name="name">The table name to render instead of the mapped one.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithTableName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureSqlSourceOverride();

        var b = Clone();
        b._tableNameOverride = name;
        return b;
    }
    /// <summary>
    /// Qualifies the primary table with <paramref name="schema"/> for this query only, for example
    /// <c>From&lt;IOrder&gt;().WithSchema("sales")</c> rendering <c>sales.orders</c>. Requires a provider
    /// that can render a schema qualifier (see <see cref="ISqlDialect.MakeQualifiedTableName"/>).
    /// </summary>
    /// <param name="schema">The schema name to render before the table.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithSchema(string schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        EnsureSqlSourceOverride();

        var b = Clone();
        b._schemaOverride = schema;
        return b;
    }
    /// <summary>
    /// Qualifies the primary table with <paramref name="database"/> for this query only
    /// (<c>database.schema.table</c> on SQL Server, <c>database.table</c> on MySQL/MariaDB/ClickHouse,
    /// an attached database on SQLite). Requires a provider that opted into
    /// <see cref="ISqlDialect.SupportsCrossDatabase"/>.
    /// </summary>
    /// <param name="database">The database name to render before the table.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithDatabase(string database)
    {
        ArgumentException.ThrowIfNullOrEmpty(database);
        EnsureSqlSourceOverride();

        var b = Clone();
        b._databaseOverride = database;
        return b;
    }
    /// <summary>
    /// Qualifies the primary table with a linked <paramref name="server"/> for this query only
    /// (<c>server.database.schema.table</c>). Only SQL Server opted into
    /// <see cref="ISqlDialect.SupportsLinkedServer"/>; every other provider rejects the override with
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="server">The linked server name to render before the table.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder<TEntity> WithServer(string server)
    {
        ArgumentException.ThrowIfNullOrEmpty(server);
        EnsureSqlSourceOverride();

        var b = Clone();
        b._serverOverride = server;
        return b;
    }
    /// <summary>
    /// Replaces the primary table access with the raw SQL <paramref name="sql"/> rendered as a derived
    /// source (<c>(sql) AS alias</c>) for this query only. The mapped entity's columns are read from the
    /// expression's result set. The fragment is emitted verbatim, so pass only trusted SQL (the caller
    /// owns validity and injection safety, as with <c>WithSql</c>). Cannot be combined with a
    /// table-name/schema/database/server override. SQL providers only.
    /// </summary>
    /// <param name="sql">The raw SQL expression to use as the table source.</param>
    /// <returns>A builder with the source replaced.</returns>
    public EntityBuilder<TEntity> WithTableExpression(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        EnsureSqlSourceOverride();

        var b = Clone();
        b._tableExpression = sql;
        return b;
    }

    /// <summary>
    /// Whether any per-query source override has been set on this builder.
    /// </summary>
    private bool HasSourceOverride => _tableNameOverride is not null || _schemaOverride is not null
        || _databaseOverride is not null || _serverOverride is not null || _tableExpression is not null;

    /// <summary>
    /// Rejects a source override eagerly on the in-memory provider, which has no SQL source to rewrite.
    /// The source-kind check (a physical table/entity is required) happens later in
    /// <see cref="ResolvePhysicalSource"/>.
    /// </summary>
    private void EnsureSqlSourceOverride()
    {
        if (_dataProvider is InMemoryDataContext)
            throw new NotSupportedException("Per-query source overrides are not supported by the in-memory provider; run the query against a SQL provider.");
    }

    /// <summary>
    /// Resolves the command's primary source, applying the per-query overrides when any are set. Without
    /// an override the resolution is unchanged: an explicit source is used as-is, otherwise the command
    /// leaves <c>From</c> for the planner to resolve from entity metadata.
    /// </summary>
    internal FromExpression? ResolveSource()
    {
        if (!HasSourceOverride)
        {
            if (_query is not null) return new FromExpression(_query) { SubQueryHint = _subQueryHint };
            if (_from is not null)
                return _subQueryHint is null || _from.SubQuery is null
                    ? _from
                    : new FromExpression(_from.SubQuery) { SubQueryHint = _subQueryHint };
            return !string.IsNullOrEmpty(_table) ? new FromExpression(_table) : null;
        }

        if (_tableExpression is not null)
        {
            if (_tableNameOverride is not null || _schemaOverride is not null || _databaseOverride is not null || _serverOverride is not null)
                throw new InvalidOperationException("WithTableExpression cannot be combined with a table-name, schema, database or server override.");

            return ResolvePhysicalSource().WithOverrides(null, null, null, null, _tableExpression);
        }

        return ResolvePhysicalSource().WithOverrides(_tableNameOverride, _schemaOverride, _databaseOverride, _serverOverride, null);
    }

    /// <summary>
    /// Resolves the physical-table base a qualification/table-expression override applies to. Only a
    /// mapped entity, an explicit table name or an explicit physical <see cref="FromExpression"/> can
    /// carry one; a derived query, subquery, table-valued function or pivot is rejected.
    /// </summary>
    private FromExpression ResolvePhysicalSource()
    {
        if (_query is not null || (_from is not null && string.IsNullOrEmpty(_from.Table)))
            throw new NotSupportedException("A per-query source override can only be applied to a physical table or mapped-entity source, not a derived query, subquery, table-valued function or pivot.");

        if (_from is not null)
            return _from;

        if (!string.IsNullOrEmpty(_table))
            return new FromExpression(_table);

        var t = _sourceEntityType ?? typeof(TEntity);
        if (DataContextCache.Metadata.TryGetValue(t, out var entity) && !string.IsNullOrEmpty(entity.TableName))
            return new FromExpression(entity.TableName, entity.IsTableNameAuto, t.IsInterface);

        throw new BuildSqlCommandException(
            $"Table name is not registered for type {t}. Materialize the entity first (for example with {nameof(DataContextExtensions.From)}<{t.Name}>()) so its metadata is registered.");
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
    /// Per-query override of the command timeout in seconds (<c>null</c> inherits the context
    /// default). A value of zero or less means the provider default. Set through
    /// <see cref="WithCommandTimeout"/>.
    /// </summary>
    internal int? CommandTimeout { get; set; }
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
        cmd.CommandTimeout = CommandTimeout;

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
        dst.CommandTimeout = CommandTimeout;
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
    /// Overrides the command timeout in seconds for the commands this builder creates
    /// (see <see cref="EntityBuilder{TEntity}.WithCommandTimeout"/>): it takes precedence over the
    /// context default set with <c>DataContextBuilder.UseCommandTimeout</c>, and a value of zero or less
    /// means the provider default. The override is part of the plan-cache key.
    /// </summary>
    /// <param name="seconds">The command timeout in seconds; zero or less uses the provider default.</param>
    /// <returns>A builder with the override applied.</returns>
    public EntityBuilder WithCommandTimeout(int seconds)
    {
        var b = Clone();

        b.CommandTimeout = seconds > 0 ? seconds : null;

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
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> Join(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(from, JoinType.Inner, joinCondition, options);
    /// <summary>
    /// Adds a left outer join to <paramref name="from"/>: every left-hand row is kept, and right-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> LeftJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(from, JoinType.Left, joinCondition, options);
    /// <summary>
    /// Adds a right outer join to <paramref name="from"/>: every right-hand row is kept, and left-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> RightJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(from, JoinType.Right, joinCondition, options);
    /// <summary>
    /// Adds a full outer join to <paramref name="from"/>: unmatched rows from both sides are kept,
    /// with the other side's columns set to <c>NULL</c>.
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TableAlias> FullJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(from, JoinType.Full, joinCondition, options);
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
    /// <see cref="EntityBuilder{TEntity}.SemiJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Expression{Func{TEntity, TJoinEntity, bool}}, Action{JoinOptions})"/>).
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="joinCondition">The join predicate over the two table aliases.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the left-hand columns, carrying the semi join.</returns>
    internal EntityBuilder SemiJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Semi)
        {
            From = new FromExpression(from._table!),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }
    /// <inheritdoc cref="SemiJoin(EntityBuilder, Expression{Func{TableAlias, TableAlias, bool}}, Action{JoinOptions})"/>
    internal EntityBuilder AntiJoin(EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Anti)
        {
            From = new FromExpression(from._table!),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }
    /// <summary>
    /// Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="from"/> (see
    /// <see cref="EntityBuilder{TEntity}.PasteJoin{TJoinEntity}(EntityBuilder{TJoinEntity}, Action{JoinOptions})"/>).
    /// </summary>
    /// <param name="from">The named-table builder whose source is joined.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    internal JoinedEntityBuilder<TableAlias, TableAlias> PasteJoin(EntityBuilder from, Action<JoinOptions>? options = null)
        => JoinCore(from, JoinType.Paste, null, options);
    private EntityBuilder AddSemiAntiJoin(JoinExpression join)
    {
        var b = Clone();
        var joins = b._joins;
        if (joins is null)
            b._joins = joins = _joins is null ? [] : [.. _joins];

        joins.Add(join);

        return b;
    }
    private JoinedEntityBuilder<TableAlias, TableAlias> JoinCore(EntityBuilder from, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<TableAlias, TableAlias>(_dataProvider, new JoinExpression(joinCondition, joinType)
        {
            From = new FromExpression(from._table!),
            EntityType = joinCondition is null ? typeof(TableAlias) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        }) { Logger = Logger, Table = _table, Ctes = CteMerge.Merge(Ctes, from.Ctes), QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        return cb;
    }
    /// <summary>
    /// Adds an inner join to <paramref name="_"/>, using <paramref name="joinCondition"/> as the
    /// <c>ON</c> predicate; only matching pairs survive.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>
    /// Adds a left outer join to <paramref name="_"/>: every left-hand row is kept, and right-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>
    /// Adds a right outer join to <paramref name="_"/>: every right-hand row is kept, and left-hand
    /// columns are <c>NULL</c> when there is no match.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>
    /// Adds a full outer join to <paramref name="_"/>: unmatched rows from both sides are kept, with
    /// the other side's columns set to <c>NULL</c>.
    /// </summary>
    /// <param name="_">The builder for the joined entity; only its source is used.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
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
    // #159 direct Cte<T> overloads for the named-table (TableAlias) receiver. The descriptor is
    // converted with this builder's context and routed through the generic EntityBuilder-source
    // overloads, so the source resolution and CTE propagation match ctx.From(cte).

    /// <summary>Adds an inner join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => Join(cte, joinCondition, null);
    /// <summary>Adds an inner join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return Join(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a left outer join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => LeftJoin(cte, joinCondition, null);
    /// <summary>Adds a left outer join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return LeftJoin(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a right outer join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => RightJoin(cte, joinCondition, null);
    /// <summary>Adds a right outer join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return RightJoin(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a full outer join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => FullJoin(cte, joinCondition, null);
    /// <summary>Adds a full outer join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="joinCondition">The join predicate over the table alias and the joined entity.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(Cte<TJoinEntity> cte, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(joinCondition);
        return FullJoin(_dataProvider.From(cte), joinCondition, options);
    }

    /// <summary>Adds a cross join over the typed CTE <paramref name="cte"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossJoin<TJoinEntity>(Cte<TJoinEntity> cte)
        => CrossJoin(cte, null);
    /// <summary>Adds a cross join over the typed CTE <paramref name="cte"/> with per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossJoin<TJoinEntity>(Cte<TJoinEntity> cte, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        return JoinCore(_dataProvider.From(cte), JoinType.Cross, null, options);
    }

    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>CROSS APPLY</c>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossApply<TJoinEntity>(Cte<TJoinEntity> cte)
        => CrossApply(cte, null);
    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>CROSS APPLY</c> and per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> CrossApply<TJoinEntity>(Cte<TJoinEntity> cte, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        return JoinCore(_dataProvider.From(cte), JoinType.CrossApply, null, options);
    }

    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>OUTER APPLY</c>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> OuterApply<TJoinEntity>(Cte<TJoinEntity> cte)
        => OuterApply(cte, null);
    /// <summary>Applies the typed CTE <paramref name="cte"/> with <c>OUTER APPLY</c> and per-join <paramref name="options"/>.</summary>
    /// <typeparam name="TJoinEntity">The CTE's projection type.</typeparam>
    /// <param name="cte">The CTE descriptor, converted with this builder's context.</param>
    /// <param name="options">Per-join configuration.</param>
    /// <returns>A builder over the applied projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cte"/> is <see langword="null"/>.</exception>
    public JoinedEntityBuilder<TableAlias, TJoinEntity> OuterApply<TJoinEntity>(Cte<TJoinEntity> cte, Action<JoinOptions>? options)
    {
        ArgumentNullException.ThrowIfNull(cte);
        return JoinCore(_dataProvider.From(cte), JoinType.OuterApply, null, options);
    }
    /// <inheritdoc cref="SemiJoin(EntityBuilder, Expression{Func{TableAlias, TableAlias, bool}}, Action{JoinOptions})"/>
    internal EntityBuilder SemiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Semi)
        {
            From = _dataProvider.GetFrom(typeof(TJoinEntity), null)!,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }
    /// <inheritdoc cref="SemiJoin(EntityBuilder, Expression{Func{TableAlias, TableAlias, bool}}, Action{JoinOptions})"/>
    internal EntityBuilder AntiJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(new JoinExpression(joinCondition, JoinType.Anti)
        {
            From = _dataProvider.GetFrom(typeof(TJoinEntity), null)!,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }
    /// <inheritdoc cref="PasteJoin(EntityBuilder, Action{JoinOptions})"/>
    internal JoinedEntityBuilder<TableAlias, TJoinEntity> PasteJoin<TJoinEntity>(EntityBuilder<TJoinEntity> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    private JoinedEntityBuilder<TableAlias, TJoinEntity> JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<TableAlias, TJoinEntity>(_dataProvider, new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(TJoinEntity) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        }) { Logger = Logger, Table = _table, Ctes = CteMerge.Merge(Ctes, _.Ctes), QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        return cb;
    }
}
