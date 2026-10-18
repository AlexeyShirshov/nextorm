using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// A stack-only carrier for the projection-shape fields a fluent builder transfers into a new
/// <see cref="QueryCommand"/>. It exists so the builder-to-command hot path does not materialise a
/// <see cref="QueryDefinition"/> record (a heap allocation) just for the constructor to copy it out
/// again. Value type by design: it never escapes the construction call and is never boxed.
/// </summary>
internal readonly struct QueryShape
{
    /// <summary>Projection lambda of the query, or <c>null</c> when <see cref="SrcType"/> is used.</summary>
    public LambdaExpression? Exp { get; init; }
    /// <summary>Entity type of the query when there is no projection lambda.</summary>
    public Type? SrcType { get; init; }
    /// <summary>Optional predicate applied as a <c>WHERE</c> clause.</summary>
    public LambdaExpression? Condition { get; init; }
    /// <summary>The selective query-filter scope disabled for this command, or <c>null</c> for none.</summary>
    public QueryFilterScope? FilterScope { get; init; }
    /// <summary>Joins accumulated by the builder.</summary>
    public JoinExpression[]? Joins { get; init; }
    /// <summary>Paging (limit/offset) applied to the query.</summary>
    public Paging Paging { get; init; }
    /// <summary>Ordering accumulated by the builder.</summary>
    public Sorting[]? Sorting { get; init; }
    /// <summary>Optional grouping lambda.</summary>
    public LambdaExpression? Group { get; init; }
    /// <summary>Optional <c>HAVING</c> lambda.</summary>
    public LambdaExpression? Having { get; init; }
    /// <summary>Optional logger used by the command.</summary>
    public ILogger? Logger { get; init; }
    /// <summary>Whether the query was marked <c>DISTINCT</c>.</summary>
    public bool IsDistinct { get; init; }
    /// <summary>Whether the query carries the ClickHouse <c>FINAL</c> modifier.</summary>
    public bool Final { get; init; }
    /// <summary>The ClickHouse <c>SAMPLE</c> ratio, or <c>null</c> when the modifier is absent.</summary>
    public double? SampleRatio { get; init; }
    /// <summary>The ClickHouse <c>SAMPLE ... OFFSET</c> value; zero when absent.</summary>
    public double SampleOffset { get; init; }
    /// <summary>The trailing ClickHouse <c>SETTINGS</c> entries, or <c>null</c> when there are none.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Settings { get; init; }
    /// <summary>Optional ClickHouse <c>PREWHERE</c> predicate.</summary>
    public LambdaExpression? PreWhere { get; init; }
    /// <summary>Optional ClickHouse <c>ARRAY JOIN</c> expressions, or <c>null</c> when absent.</summary>
    public IReadOnlyList<LambdaExpression>? ArrayJoins { get; init; }
    /// <summary>Named window definitions declared for the query, or <c>null</c> when there are none.</summary>
    public IReadOnlyList<WindowDefinition>? Windows { get; init; }
    /// <summary>The <c>ARRAY JOIN</c> kind (plain or <c>LEFT</c>).</summary>
    public ArrayJoinKind ArrayJoinKind { get; init; }
    /// <summary>Whether the last <c>ARRAY JOIN</c> expression is aliased as the element.</summary>
    public bool BindArrayJoinElement { get; init; }
}

public partial class QueryCommand : IQueryRegistry, ICloneable
{

    private object? _customData;
    /// <summary>The context that executes the command, or <c>null</c> for an unbound cached clone.</summary>
    protected IDataContext? _dataContext;
    /// <summary>
    /// The join-projection result type a <c>JoinInto</c> pair command materializes while its
    /// <see cref="_srcType"/> stays the parent entity type, or <c>null</c> for an ordinary command.
    /// </summary>
    internal Type? ProjectionType
    {
        get => _description?.ProjectionType;
        init
        {
            if (ReferenceEquals(value, _description?.ProjectionType)) return;
            WriteDescription().ProjectionType = value;
        }
    }
    /// <summary>
    /// True for a prepared identity (whole-projection) CTE shape: its flattened item columns are tagged
    /// with the deterministic, collision-free per-slot aliases the mutation's <c>RETURNING</c> list
    /// emits. Set before preparation so the alias is folded into the column plan hash.
    /// </summary>
    internal bool IdentitySlotAliases
    {
        get => GetFlag(QueryDescription.FlagIdentitySlotAliases);
        set => SetFlag(QueryDescription.FlagIdentitySlotAliases, value);
    }
    /// <summary>The paging state (<c>LIMIT</c>/<c>OFFSET</c>) applied to the query.</summary>
    public Paging Paging;

    /// <summary>
    /// Initializes a command from a query shape, copying each collaborator and leaving the command
    /// unprepared. The shape's arrays are defensively copied: a definition can be supplied by a caller
    /// (the public <c>CreateCommand</c>) and its <see cref="QueryDefinition.Joins"/> array is
    /// caller-owned.
    /// </summary>
    /// <param name="dataProvider">The context that executes the command, or <c>null</c> for an unbound clone.</param>
    /// <param name="definition">The shape to copy from.</param>
    protected QueryCommand(IDataContext? dataProvider, QueryDefinition definition)
        : this(dataProvider, definition, ownJoins: false)
    {
    }

    /// <summary>
    /// Initializes a command from a query shape, optionally taking ownership of the shape's joins.
    /// </summary>
    /// <param name="dataProvider">The context that executes the command, or <c>null</c> for an unbound clone.</param>
    /// <param name="definition">The shape to copy from.</param>
    /// <param name="ownJoins">
    /// When <c>true</c>, <see cref="QueryDefinition.Joins"/> was produced by this command's own
    /// construction path (the self-clone path and the fluent builders) and is not reachable from any
    /// live command, so it is installed directly instead of being copied again. Every definition supplied
    /// from outside — in particular the public <c>CreateCommand</c> — passes <c>false</c>, keeping a
    /// caller-owned array isolated from the command.
    /// </param>
    internal QueryCommand(IDataContext? dataProvider, QueryDefinition definition, bool ownJoins)
    {
        _dataContext = dataProvider;
        _exp = definition.Exp;
        _srcType = definition.SrcType;
        ProjectionType = definition.ProjectionType;
        IdentitySlotAliases = definition.IdentitySlotAliases;
        _condition = definition.Condition;
        _filterScope = definition.FilterScope ?? (definition.IgnoreFilters ? QueryFilterScope.AllFilters : QueryFilterScope.None);
        // Own a private array: the DML/paging clones and the external definition callers hand the
        // source's _joins here, and preparation writes the prepared copy back into the array
        // (PrepareJoin). Sharing the array would let one command's preparation rewrite a source's or
        // sibling's joins while it is already prepared, so a later re-render (storeInCache:false) read
        // the sibling's injected conditions. The elements stay shared by design: they are immutable apart
        // from preparation, which replaces the element with a copy instead of mutating it. ownJoins is
        // set only where the array was just produced for this command and no other command can reach it:
        // the fluent builders (EntityBuilder._joins?.ToArray()); every definition supplied from outside
        // keeps the defensive copy.
        _joins = definition.Joins is { } joins
            ? (ownJoins ? joins : (JoinExpression[])joins.Clone())
            : null;
        Paging = definition.Paging;
        _sorting = definition.Sorting;
        _groupExp = definition.Group;
        _having = definition.Having;
        SetLogger(definition.Logger);
        IsDistinct = definition.IsDistinct;
        GroupByWithTotals = definition.GroupByWithTotals;
        LimitBy = definition.LimitBy;
        DistinctOn = definition.DistinctOn;
        ExtremeRow = definition.ExtremeRow;
        TableSample = definition.TableSample;
        Temporal = definition.Temporal;
        RowLock = definition.RowLock;
        SourceLockClause = definition.PrimarySourceLock;
        Final = definition.Final;
        SampleRatio = definition.SampleRatio;
        SampleOffset = definition.SampleOffset;
        Settings = definition.Settings;
        NavigationPaths = definition.NavigationPaths;
        _preWhere = definition.PreWhere;
        _arrayJoins = definition.ArrayJoins?.ToArray();
        _windows = definition.Windows;
        ArrayJoinKind = definition.ArrayJoinKind;
        BindArrayJoinElement = definition.BindArrayJoinElement;
    }
    /// <summary>
    /// Initializes a command by transferring the shape of an existing command. This is the copy
    /// constructor the internal clone and derivation paths use instead of materialising an intermediate
    /// <see cref="QueryDefinition"/> record and copying it field by field: the fields are read straight
    /// from <paramref name="source"/>, so a fluent step allocates one command and no shape record.
    /// </summary>
    /// <param name="dataProvider">The context that executes the command, or <c>null</c> for an unbound clone.</param>
    /// <param name="source">The command whose shape is transferred.</param>
    /// <param name="forCache">
    /// When <c>true</c>, the clone is the plan-cache key: the raw projection, condition and grouping are
    /// cleared and the joins are deep-cloned, exactly as the removed <c>CacheCloneDefinition</c> did.
    /// When <c>false</c>, the raw shape travels and the joins get a private array.
    /// </param>
    internal QueryCommand(IDataContext? dataProvider, QueryCommand source, bool forCache)
    {
        _dataContext = dataProvider;
        Paging = source.Paging;

        // A live clone that does not need a private join/array-join array shares the source's immutable
        // description: the very first raw write on either side splits it (copy-on-write). This is the
        // CTE-construction edge the plan targets: UnionAll/Clone/Cte bodies stop re-allocating every raw
        // field of the node.
        if (!forCache && source._joins is null && source._arrayJoins is null)
        {
            var shared = source._description;
            if (shared is null)
            {
                _description = new QueryDescription();
                SetLogger(source.Logger);
                ProjectionType = source.ProjectionType;
                IdentitySlotAliases = source.IdentitySlotAliases;
                return;
            }

            shared.MarkShared();
            _description = shared;
            return;
        }

        // The cache clone (and any live clone that must own a private join/array-join array) builds an
        // owned description from the source's blocks.
        var description = source._description?.CloneShellForWrite() ?? new QueryDescription();
        if (forCache)
        {
            description.Exp = null;
            description.Predicates = new QueryPredicates(description.Predicates)
            {
                Condition = null,
                Group = null,
            };
            description.Relations = new QueryRelations(description.Relations)
            {
                Joins = CloneForCache(description.Relations.Joins),
            };
        }
        else
        {
            description.Relations = new QueryRelations(description.Relations)
            {
                Joins = CloneJoinArray(description.Relations.Joins),
            };
        }

        description.Clauses = new QueryClauses(description.Clauses)
        {
            ArrayJoins = CloneJoinArray(description.Clauses.ArrayJoins),
        };
        description.DefinitionCache = null;
        _description = description;
    }

    /// <summary>
    /// Initializes a command from a stack-only <see cref="QueryShape"/>. The fluent builders pass their
    /// fields this way so a builder-to-command step allocates the command but no <see cref="QueryDefinition"/>
    /// record; the public <c>CreateCommand</c> definition overload keeps the caller-owned-array defence.
    /// </summary>
    /// <param name="dataProvider">The context that executes the command, or <c>null</c> for an unbound clone.</param>
    /// <param name="shape">The shape transferred from the builder.</param>
    /// <param name="ownJoins">
    /// When <c>true</c>, <see cref="QueryShape.Joins"/> is a fresh array no other command can reach
    /// (the builder's <c>_joins?.ToArray()</c>), so it is installed directly instead of being copied.
    /// </param>
    internal QueryCommand(IDataContext? dataProvider, QueryShape shape, bool ownJoins)
    {
        _dataContext = dataProvider;
        _exp = shape.Exp;
        _srcType = shape.SrcType;
        _condition = shape.Condition;
        _filterScope = shape.FilterScope ?? QueryFilterScope.None;
        _joins = shape.Joins is { } joins
            ? (ownJoins ? joins : CloneJoinArray(joins)!)
            : null;
        Paging = shape.Paging;
        _sorting = shape.Sorting;
        _groupExp = shape.Group;
        _having = shape.Having;
        SetLogger(shape.Logger);
        IsDistinct = shape.IsDistinct;
        Final = shape.Final;
        SampleRatio = shape.SampleRatio;
        SampleOffset = shape.SampleOffset;
        Settings = shape.Settings;
        _preWhere = shape.PreWhere;
        _arrayJoins = shape.ArrayJoins?.ToArray();
        _windows = shape.Windows;
        ArrayJoinKind = shape.ArrayJoinKind;
        BindArrayJoinElement = shape.BindArrayJoinElement;
    }

    /// <summary>Shallow-clones a collaborator array with a private copy, or returns <c>null</c>.</summary>
    /// <typeparam name="T">The array element type.</typeparam>
    /// <param name="items">The array to copy, or <c>null</c>.</param>
    /// <returns>A private copy of <paramref name="items"/>, or <c>null</c>.</returns>
    private static T[]? CloneJoinArray<T>(T[]? items) => items is null ? null : (T[])items.Clone();

    /// <summary>
    /// Snapshot of this command's shape as a <see cref="QueryDefinition"/>, used by external readers and
    /// the test surface. The record is materialised once per description and memoised there; any raw
    /// write installs a fresh description (copy-on-write), whose empty cache yields the new value. The
    /// internal clone paths do not read it — they transfer state directly and allocate no record.
    /// </summary>
    internal QueryDefinition Definition => Description.DefinitionCache ??= BuildDefinition();
    /// <summary>Materialises the shape record from the current description and memoizes it there.</summary>
    private QueryDefinition BuildDefinition() => new()
        {
            Exp = _exp,
            SrcType = _srcType,
            ProjectionType = ProjectionType,
            IdentitySlotAliases = IdentitySlotAliases,
            Condition = _condition,
            IgnoreFilters = !_filterScope.IsEmpty,
            FilterScope = _filterScope,
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
            ExtremeRow = ExtremeRow,
            TableSample = TableSample,
            Temporal = Temporal,
            RowLock = RowLock,
            PrimarySourceLock = SourceLockClause,
            Final = Final,
            SampleRatio = SampleRatio,
            SampleOffset = SampleOffset,
            Settings = Settings,
            PreWhere = _preWhere,
            ArrayJoins = _arrayJoins,
            Windows = _windows,
            ArrayJoinKind = ArrayJoinKind,
            BindArrayJoinElement = BindArrayJoinElement,
            NavigationPaths = NavigationPaths,
        };
    /// <summary>
    /// Applies the reference-navigation expansion performed at the start of preparation (#148-B D3):
    /// installs the rewritten lambdas and appends the injected navigation <c>LEFT JOIN</c>s after the
    /// command's declared joins. Preparation-local; the rewrite is idempotent so a re-preparation sees
    /// no navigation access and does not append duplicates.
    /// </summary>
    /// <summary>
    /// The joined parameters injected by a reference-navigation expansion, each mapped to its
    /// human-readable navigation path (#148-B D5). Preparation-local: set by
    /// <see cref="NavigationExpansion"/> and read by the projection-nullability check and the provider
    /// outer-join null-settings injection. Not part of the plan key.
    /// </summary>
    internal IReadOnlyDictionary<ParameterExpression, string>? NavigationPaths
{
    get => _description?.Clauses.NavigationPaths;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.NavigationPaths)) return;
        WriteClauses().NavigationPaths = value;
    }
}

    /// <summary>
    /// #148-B R2.1: the mapped-child-existence leg of a many-to-many collection correlation. Set only
    /// for the in-memory provider, which cannot nest a child-key subquery in a correlated
    /// subcommand's condition and so filters the junction source instead (see
    /// <see cref="InMemoryQueryBuilder"/>). Not part of the plan key; the condition already fixes the
    /// relationship shape.
    /// </summary>
    internal JunctionChildFilter? JunctionChildFilter
{
    get => _description?.Options.JunctionChildFilter;
    set
    {
        if (ReferenceEquals(value, _description?.Options.JunctionChildFilter)) return;
        WriteOptions().JunctionChildFilter = value;
    }
}

    /// <summary>
    /// #148-B R2.3: the in-memory descriptor of a multi-hop reference-navigation chain. When set, the
    /// in-memory correlated evaluator walks the hops directly against the registered datasets instead of
    /// building a nested correlated subquery (which the evaluator cannot bind). Set only for the
    /// in-memory provider and only on the correlated command the chain was lowered to.
    /// </summary>
    internal InMemoryNavigationChain? NavigationChain
{
    get => _description?.Options.NavigationChain;
    set
    {
        if (ReferenceEquals(value, _description?.Options.NavigationChain)) return;
        WriteOptions().NavigationChain = value;
    }
}

    /// <summary>
    /// #148-B r3 A3′: marks the correlated <c>count_big</c> subcommand a navigation <c>Count()</c> /
    /// property <c>Count</c> / <c>LongCount()</c> was lowered to. The SQL renderer uses it as the
    /// provenance of the outer checked <c>long-&gt;int</c> narrowing (so it suppresses the in-database
    /// <c>cast(... as int)</c> for exactly this origin, never by node shape alone); the query preparer
    /// uses it to tag the materialized Int32 column. Preparation-local; carried with clones but not
    /// part of the plan key (the command's own projection/ResultType already describes the shape).
    /// </summary>
    internal bool IsWideNavigationCount
{
    get => GetFlag(QueryDescription.FlagIsWideNavigationCount);
    set => SetFlag(QueryDescription.FlagIsWideNavigationCount, value);
}

    internal void ApplyNavigationExpansion(
        LambdaExpression? projection,
        LambdaExpression? condition,
        LambdaExpression? having,
        LambdaExpression? group,
        LambdaExpression? preWhere,
        Sorting[]? sorting,
        JoinExpression[] navigationJoins)
    {
        ArgumentNullException.ThrowIfNull(navigationJoins);

        if (projection is not null) _exp = projection;
        if (condition is not null) _condition = condition;
        if (having is not null) _having = having;
        if (group is not null) _groupExp = group;
        if (preWhere is not null) _preWhere = preWhere;
        if (sorting is not null) _sorting = sorting;

        _joins = _joins is { Length: > 0 } existing ? [.. existing, .. navigationJoins] : navigationJoins;
    }

    /// <summary>The logger that receives command-preparation diagnostics, or <c>null</c> when logging is disabled.</summary>
    public ILogger? Logger { get => _description?.Options.Logger; }
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
    /// <summary>
    /// Whether the command disables any global query filter declared for its entity type: <see langword="true"/>
    /// both when every filter is disabled (the all-or-nothing form) and when a selective scope disables
    /// only some filters. Setting it is all-or-nothing — <see langword="true"/> disables every filter and
    /// <see langword="false"/> clears the scope — because a <see langword="bool"/> cannot express a
    /// selective disable; use the builder's selective <c>IgnoreFilters</c> overloads for that. The
    /// authoritative state is <see cref="FilterScope"/>. Assigning a different value discards the current
    /// preparation, because the injected filters and the prepared condition change.
    /// </summary>
    public bool IgnoreFilters
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_filterScope.IsEmpty;
        set => SetFilterScope(value ? QueryFilterScope.AllFilters : QueryFilterScope.None);
    }
    /// <summary>
    /// The selective query-filter scope disabled for this command. Part of the command's state (not of
    /// the plan key: the injected condition already captures the effective filter set), so equal scopes
    /// share a cached plan and different scopes do not. Assigning a different scope invalidates the
    /// current preparation, because the injected filters — and therefore the prepared condition and its
    /// plan hashes — change.
    /// </summary>
    internal QueryFilterScope FilterScope
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _filterScope;
        set => SetFilterScope(value);
    }
    /// <summary>
    /// Installs a selective filter scope, discarding the current preparation when it actually changes.
    /// Without the discard an already-prepared command would keep the condition built for the previous
    /// scope (and its memoized plan key), silently reusing the wrong filtered plan.
    /// </summary>
    private void SetFilterScope(QueryFilterScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (ReferenceEquals(_filterScope, scope) || _filterScope.Equals(scope))
            return;

        _filterScope = scope;

        if (_isPrepared)
            ResetPreparation();
    }
    internal QueryCommand? FromQuery => From?.SubQuery;
    internal bool OneColumn
{
    get => GetFlag(QueryDescription.FlagOneColumn);
    set => SetFlag(QueryDescription.FlagOneColumn, value);
}
    /// <summary>
    /// When <c>true</c>, the JSON preparation path captures a recursive JSON shape description (including
    /// nested constructions and expanded entity items) and lowers its scalar descendants into the prepared
    /// select list, instead of collapsing a nested construction into one opaque column. Set only on the
    /// JSON stream clone; ordinary query preparation never sets it.
    /// </summary>
    internal bool JsonShapeMode
    {
        get => _preparation?.JsonShapeMode ?? false;
        set
        {
            if (value == (_preparation?.JsonShapeMode ?? false)) return;
            Preparation.JsonShapeMode = value;
        }
    }
    /// <summary>
    /// The recursively captured JSON projection shape stored by the JSON-only preparer when
    /// <see cref="JsonShapeMode"/> is set; <see langword="null"/> for an ordinary (non-JSON) preparation or a
    /// shape the JSON preparer did not need to take over.
    /// </summary>
    internal JsonShapeNode? JsonShape
    {
        get => _preparation?.JsonShape;
        set
        {
            if (ReferenceEquals(value, _preparation?.JsonShape)) return;
            Preparation.JsonShape = value;
        }
    }
    /// <summary>
    /// When <c>true</c>, the planner compiles no row mapper: the command is read as a single scalar
    /// value. Set by the <c>ForJson</c>/<c>ForXml</c> terminals, whose whole result set collapses into
    /// one document column.
    /// </summary>
    internal bool DocumentMode
{
    get => GetFlag(QueryDescription.FlagDocumentMode);
    set => SetFlag(QueryDescription.FlagDocumentMode, value);
}
    /// <summary>Whether the command is shaped to return at most a single row (set by the <c>First</c>/<c>Single</c> terminals).</summary>
    public bool SingleRow
{
    get => GetFlag(QueryDescription.FlagSingleRow);
    set => SetFlag(QueryDescription.FlagSingleRow, value);
}
    /// <summary>
    /// Whether the command was marked with <c>DISTINCT</c>. Named <c>IsDistinct</c> rather than
    /// <c>Distinct</c> so the fluent <see cref="QueryCommand{TResult}.Distinct"/> method does not hide
    /// the property on the generic command type.
    /// </summary>
    public bool IsDistinct
{
    get => GetFlag(QueryDescription.FlagIsDistinct);
    internal set => SetFlag(QueryDescription.FlagIsDistinct, value);
}
    /// <summary>
    /// Whether the grouping carries the <c>WITH TOTALS</c> modifier (ClickHouse). Only meaningful when
    /// the command has a grouping list; a dialect that does not support it rejects the command when its
    /// SQL is built.
    /// </summary>
    public bool GroupByWithTotals
{
    get => GetFlag(QueryDescription.FlagGroupByWithTotals);
    internal set => SetFlag(QueryDescription.FlagGroupByWithTotals, value);
}
    /// <summary>
    /// The <c>LIMIT n BY expr</c> clause (ClickHouse), or <c>null</c> when the query has none. A dialect
    /// that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal LimitByClause? LimitBy
{
    get => _description?.Clauses.LimitBy;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.LimitBy)) return;
        WriteClauses().LimitBy = value;
    }
}
    /// <summary>The prepared key columns of <see cref="LimitBy"/>, or <c>null</c> when there is none.</summary>
    internal SelectExpression[]? LimitByColumns => _limitByColumns;
    /// <summary>
    /// The <c>DISTINCT ON (expr, ...)</c> clause (PostgreSQL), or <c>null</c> when the query has none. A
    /// dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal DistinctOnClause? DistinctOn
{
    get => _description?.Clauses.DistinctOn;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.DistinctOn)) return;
        WriteClauses().DistinctOn = value;
    }
}
    /// <summary>The prepared key columns of <see cref="DistinctOn"/>, or <c>null</c> when there is none.</summary>
    internal SelectExpression[]? DistinctOnColumns => _distinctOnColumns;
    /// <summary>
    /// The <c>SelectWhereMax</c>/<c>SelectWhereMin</c> row-selection clause, or <c>null</c> when the query
    /// has none. A dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal ExtremeRowClause? ExtremeRow
{
    get => _description?.Clauses.ExtremeRow;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.ExtremeRow)) return;
        WriteClauses().ExtremeRow = value;
    }
}
    /// <summary>The prepared value-selector columns of <see cref="ExtremeRow"/>, or <c>null</c> when there is none.</summary>
    internal SelectExpression[]? ExtremeRowColumns => _extremeRowColumns;
    /// <summary>The prepared group-by columns of <see cref="ExtremeRow"/>, or <c>null</c> when the clause has no group.</summary>
    internal SelectExpression[]? ExtremeRowGroupByColumns => _extremeRowGroupByColumns;
    /// <summary>
    /// Whether this command is the child query of an eager load that carries a per-parent
    /// <c>Limit</c>/<c>Offset</c>/<c>Page</c>. When set, the builder lowers the command to a
    /// <c>row_number() over (partition by &lt;child key&gt; ...)</c> derived table and applies the page
    /// bounds to each partition, so the paging is per parent instead of global. It is an internal
    /// execution detail of the eager loader; a command with no eager load never sets it.
    /// </summary>
    internal bool PerParentPaging
{
    get => _description?.Options.PerParentPaging ?? false;
    set
    {
        if (value == (_description?.Options.PerParentPaging ?? false)) return;
        WriteOptions().PerParentPaging = value;
    }
}
    /// <summary>
    /// The child-side partition selector used by <see cref="PerParentPaging"/> (the load's child key
    /// selector, whose parameter is the child entity type). Prepared into
    /// <see cref="PerParentPartitionColumns"/> during preparation.
    /// </summary>
    internal LambdaExpression? PerParentPartition
{
    get => _description?.Options.PerParentPartition;
    set
    {
        if (ReferenceEquals(value, _description?.Options.PerParentPartition)) return;
        WriteOptions().PerParentPartition = value;
    }
}
    /// <summary>The prepared partition columns of <see cref="PerParentPaging"/>, or <c>null</c> when absent.</summary>
    internal SelectExpression[]? PerParentPartitionColumns => _perParentPartitionColumns;
    /// <summary>
    /// Whether a <see cref="PerParentPaging"/> command is rendered as a nested derived source (the
    /// single-query eager join) rather than as a top-level statement (the split child loader). A nested
    /// derived table must not carry the child's output <c>ORDER BY</c> - it is meaningless inside the
    /// join and is rejected by SQL Server - while the split statement keeps it so the loaded children
    /// preserve the child query's order. Set before preparation; a rendering detail, not query state.
    /// </summary>
    internal bool PerParentPagingEmbedded
{
    get => _description?.Options.PerParentPagingEmbedded ?? false;
    set
    {
        if (value == (_description?.Options.PerParentPagingEmbedded ?? false)) return;
        WriteOptions().PerParentPagingEmbedded = value;
    }
}
    /// <summary>
    /// The <c>TABLESAMPLE</c> table modifier, or <c>null</c> when the query has none. A dialect that does
    /// not support it rejects the command when its SQL is built.
    /// </summary>
    internal TableSampleClause? TableSample
{
    get => _description?.Clauses.TableSample;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.TableSample)) return;
        WriteClauses().TableSample = value;
    }
}
    /// <summary>
    /// The <c>FOR SYSTEM_TIME</c> temporal-table clause, or <c>null</c> when the query has none. A
    /// dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal TemporalClause? Temporal
{
    get => _description?.Clauses.Temporal;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.Temporal)) return;
        WriteClauses().Temporal = value;
    }
}
    /// <summary>
    /// The trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause, or <c>null</c> when the query
    /// has none. A dialect that does not support it rejects the command when its SQL is built.
    /// </summary>
    internal LockClause? RowLock
{
    get => _description?.Clauses.RowLock;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.RowLock)) return;
        WriteClauses().RowLock = value;
    }
}
    /// <summary>
    /// The source-level row lock requested for the primary physical source, or <c>null</c> when none was
    /// requested. During preparation it is transferred to the resolved <c>FromExpression.SourceLock</c>
    /// (and validated); a whole-query <see cref="RowLock"/> cannot be combined with it.
    /// </summary>
    internal LockClause? SourceLockClause
{
    get => _description?.Clauses.SourceLockClause;
    set
    {
        if (ReferenceEquals(value, _description?.Clauses.SourceLockClause)) return;
        WriteClauses().SourceLockClause = value;
    }
}
    /// <summary>Whether the query carries the ClickHouse <c>FINAL</c> modifier.</summary>
    public bool Final
{
    get => GetFlag(QueryDescription.FlagFinal);
    internal set => SetFlag(QueryDescription.FlagFinal, value);
}
    /// <summary>The ClickHouse <c>SAMPLE</c> ratio (a value in <c>[0, 1]</c>), or <c>null</c> when absent.</summary>
    public double? SampleRatio
{
    get => _description?.Clauses.SampleRatio;
    internal set
    {
        if (value == _description?.Clauses.SampleRatio) return;
        WriteClauses().SampleRatio = value;
    }
}
    /// <summary>The ClickHouse <c>SAMPLE ... OFFSET</c> value; zero when absent.</summary>
    public double SampleOffset
{
    get => _description?.Clauses.SampleOffset ?? 0d;
    internal set
    {
        if (value == (_description?.Clauses.SampleOffset ?? 0d)) return;
        WriteClauses().SampleOffset = value;
    }
}
    /// <summary>The trailing ClickHouse <c>SETTINGS</c> entries, or <c>null</c> when there are none.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Settings
{
    get => _description?.Clauses.Settings;
    internal set
    {
        if (ReferenceEquals(value, _description?.Clauses.Settings)) return;
        WriteClauses().Settings = value;
    }
}
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
    public ArrayJoinKind ArrayJoinKind
{
    get => _description?.Clauses.ArrayJoinKind ?? default;
    internal set
    {
        if (value == (_description?.Clauses.ArrayJoinKind ?? default)) return;
        WriteClauses().ArrayJoinKind = value;
    }
}
    /// <summary>
    /// Whether the last <c>ARRAY JOIN</c> expression is aliased as the element referenced by
    /// <see cref="ArrayJoinProjection{TEntity, TElement}.Element"/>.
    /// </summary>
    internal bool BindArrayJoinElement
{
    get => GetFlag(QueryDescription.FlagBindArrayJoinElement);
    set => SetFlag(QueryDescription.FlagBindArrayJoinElement, value);
}
    internal bool IgnoreColumns
{
    get => _description?.Options.IgnoreColumns ?? false;
    set
    {
        if (value == (_description?.Options.IgnoreColumns ?? false)) return;
        WriteOptions().IgnoreColumns = value;
    }
}
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
    /// The typed recursive self-reference for which this command is the anchor, or <c>null</c> when the
    /// command is not the anchor of a typed recursive CTE. A member read over this command must resolve
    /// to the anchor projection's declared output aliases (the recursive CTE's column names) instead of
    /// re-rendering the anchor body: a constant or other non-column anchor projection would otherwise
    /// inline a literal and emit an unaddressable column such as <c>t1.1</c>.
    /// </summary>
    internal CteReference? TypedRecursiveAnchor
    {
        get => _binding?.TypedRecursiveAnchor;
        set
        {
            if (ReferenceEquals(value, _binding?.TypedRecursiveAnchor)) return;
            Binding.TypedRecursiveAnchor = value;
        }
    }

    /// <summary>
    /// True when any common table expression carried by this command, at any declaration depth, has a
    /// data-modifying body (<c>INSERT</c>/<c>UPDATE</c>/<c>DELETE ... RETURNING</c>). Such a statement is
    /// side-effecting and its plan must not be shared; the planner reads this to bypass the plan cache
    /// call-locally instead of clearing the sticky <see cref="Cache"/> flag.
    /// </summary>
    /// <remarks>
    /// The getter first scans the command's own flat declaration list for a data-modifying body. When a
    /// declaration carries further nested <c>Ctes</c>, a prepared command returns immediately: preparation
    /// has already hoisted the transitive declaration tree into that flat list, so the mutation-first scan
    /// is complete and no recursive visited set is needed on the warm path. Only an unprepared
    /// (not-yet-hoisted) command recurses over the declaration tree, with a reference-identity
    /// <see cref="HashSet{T}"/> so a cyclic declaration graph still terminates.
    /// <para>
    /// That recursion follows exactly the tree <see cref="CteHoister.Hoist"/> flattens (a CTE body's own
    /// <c>Ctes</c>), because that is the only tree the renderer can lift into the statement's top-level
    /// <c>WITH</c>. A declaration reachable only through a derived table, join, set-operation branch or
    /// correlated reference is not hoisted and is rejected by
    /// <see cref="CteHoister.EnsureNoUnhoistedCtes"/> during preparation, before the cache lookup/store
    /// gates, so it cannot be cached either.
    /// </para>
    /// </remarks>
    internal bool HasDataModifyingCte
    {
        get
        {
            // Fast path for a prepared/hoisted command: CteHoister flattens the declaration tree into
            // one list, so a data-modifying body is found by scanning that list directly. No visited
            // set and no recursion — the per-lookup walk on the warm read path stays allocation-free.
            if (_ctes is not { Count: > 0 } ctes)
                return false;

            for (var i = 0; i < ctes.Count; i++)
            {
                if (ctes[i].Mutation is not null)
                    return true;
            }

            // No declaration body carries its own CTEs, so there is no deeper body to search: an
            // all-read flat list must not allocate a traversal set.
            var nested = false;
            for (var i = 0; i < ctes.Count; i++)
            {
                if (ctes[i].Query._ctes is { Count: > 0 })
                {
                    nested = true;
                    break;
                }
            }

            if (!nested)
                return false;

            // A prepared command's flat list is already the complete hoisted declaration tree
            // (PrepareCommand -> PrepareCtes), so every reachable mutation is in `ctes` and the flat
            // scan above is conclusive: a nested body's own declarations are present at top level. The
            // recursive probe would find nothing and only allocate its visited set on every warm lookup.
            if (IsPrepared)
                return false;

            // A CTE body declares its own CTEs on an unprepared (not-yet-hoisted) graph: recurse with a
            // reference-identity visited set so a mutation nested deeper is still found and a cyclic
            // declaration graph terminates. The set is allocated only on this cold path.
            var visited = new HashSet<QueryCommand>(ReferenceEqualityComparer.Instance) { this };
            for (var i = 0; i < ctes.Count; i++)
            {
                if (HasDataModifyingCteIn(ctes[i].Query, visited))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Whether this command should emit the <c>JoinInto.MultipleCollections</c> diagnostic on its next
    /// plan preparation — i.e. on the plan-cache miss — because the pair command is built from two or more
    /// collection navigations and no join suppressed it. The diagnostic is best-effort: a plan served from
    /// cache does not re-emit it. Diagnostic-only state: it is deliberately <b>not</b> part of the plan key
    /// and is cleared once the warning has been emitted, so a command warns at most once per preparation,
    /// with suppression applying to the first plan-cache population.
    /// </summary>
    internal bool PendingJoinIntoCartesianWarning
    {
        get => _preparation?.PendingJoinIntoCartesianWarning ?? false;
        set
        {
            if (value == (_preparation?.PendingJoinIntoCartesianWarning ?? false)) return;
            Preparation.PendingJoinIntoCartesianWarning = value;
        }
    }

    /// <summary>
    /// Whether this denormalized pair command (<c>JoinInto</c> or a single-query <c>LoadWith</c>) joins at
    /// least one side with an outer join, so a provider whose outer join fills the unmatched side with
    /// defaults (ClickHouse) must inject its query-local null-producing setting
    /// (<see cref="ISqlDialect.OuterJoinNullSettings"/>). Set by <c>EntityBuilder.CreatePairCommand</c> on
    /// the SQL path only; the provider-neutral injection lives in
    /// <c>NavigationExpansion.ApplyProviderOuterJoinNullSettings</c>, which also honors it for a command
    /// whose joins come from a navigation expansion.
    /// </summary>
    internal bool NeedsOuterJoinNullSettings
{
    get => GetFlag(QueryDescription.FlagNeedsOuterJoinNullSettings);
    set => SetFlag(QueryDescription.FlagNeedsOuterJoinNullSettings, value);
}

    /// <summary>
    /// The raw-source global-filter skips collected during the current preparation, or <see langword="null"/>
    /// when no bound filter was skipped. Emitted by the planner on the next plan-cache miss (and cleared
    /// there) so a skipped filter warns once per completed preparation and never on a cache hit. Not part
    /// of the plan key: it carries only diagnostic data (entity name, filter key, reason, missing column
    /// names), never SQL text, table names, parameters or captured values.
    /// </summary>
    internal List<RawSourceFilterSkip>? PendingRawSourceFilterSkips
    {
        get => _preparation?.PendingRawSourceFilterSkips;
        set
        {
            if (ReferenceEquals(value, _preparation?.PendingRawSourceFilterSkips)) return;
            Preparation.PendingRawSourceFilterSkips = value;
        }
    }

    /// <summary>
    /// The bound raw/named join filters collected during the current preparation whose join has no
    /// <c>ON</c> clause (CROSS/APPLY). They are deferred until the main-source filter pass so they can be
    /// re-rooted onto the projection's join alias and placed in <c>WHERE</c>; a CROSS/APPLY join must never
    /// fabricate an <c>ON</c> clause. Preparation-local scratch: reset at the start of every preparation
    /// and never part of the plan key.
    /// </summary>
    internal List<PendingCrossJoinFilter>? PendingCrossJoinFilters
    {
        get => _preparation?.PendingCrossJoinFilters;
        set
        {
            if (ReferenceEquals(value, _preparation?.PendingCrossJoinFilters)) return;
            Preparation.PendingCrossJoinFilters = value;
        }
    }

    /// <summary>
    /// One bound join filter deferred to the main-source filter pass because its CROSS/APPLY join has no
    /// <c>ON</c> clause. Carries the resolved predicate so a builder-function filter is invoked exactly
    /// once, plus the join's source ordinal for alias re-rooting.
    /// </summary>
    /// <param name="Binding">The binding declared on the joined source.</param>
    /// <param name="SourceOrdinal">The join's source ordinal (zero-based join index plus one).</param>
    /// <param name="Filter">The resolved filter metadata.</param>
    /// <param name="Lambda">The filter predicate, resolved once.</param>
    internal readonly record struct PendingCrossJoinFilter(FromExpression.EntityBinding Binding, int SourceOrdinal, IQueryFilterMetadata Filter, LambdaExpression Lambda);

    /// <summary>
    /// One skipped global query filter on an explicitly bound raw/named source. Carries diagnostic data
    /// only; <see cref="MissingColumns"/> is a comma-joined list of declared column names or <c>null</c>
    /// for the undetermined case.
    /// </summary>
    /// <param name="EntityType">The bound entity type name (no namespace).</param>
    /// <param name="SourceOrdinal">The source ordinal within the command (0 for the main source).</param>
    /// <param name="FilterKey">The filter key, or <c>""</c> for an anonymous filter.</param>
    /// <param name="Reason"><c>MissingColumns</c> or <c>UndeterminedColumns</c>.</param>
    /// <param name="MissingColumns">The required column names absent from the declared shape, if any.</param>
    internal readonly record struct RawSourceFilterSkip(string EntityType, int SourceOrdinal, string FilterKey, string Reason, string? MissingColumns);

    private static bool HasDataModifyingCteIn(QueryCommand command, HashSet<QueryCommand> visited)
    {
        if (command._ctes is not { Count: > 0 } ctes)
            return false;

        if (!visited.Add(command))
            return false;

        for (var i = 0; i < ctes.Count; i++)
        {
            var cte = ctes[i];
            if (cte.Mutation is not null)
                return true;

            if (HasDataModifyingCteIn(cte.Query, visited))
                return true;
        }

        return false;
    }
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
    /// An optional query tag rendered as a block comment (<c>/* tag */</c>) immediately after the
    /// <c>SELECT</c> keyword, so the statement can be identified in logs, profilers and server-side
    /// query stores. Set through <c>WithTag</c>. When <c>null</c> no comment is emitted and the plan
    /// key is unchanged. The tag is part of the plan key, so two commands that differ only in their
    /// tag never share a cached plan.
    /// </summary>
    public string? Tag
{
    get => _description?.Options.Tag;
    internal set
    {
        if (ReferenceEquals(value, _description?.Options.Tag)) return;
        WriteOptions().Tag = value;
    }
}
    /// <summary>
    /// Table-level hints attached to the command's physical <c>FROM</c> table (for example SQL Server
    /// <c>nolock</c>), or <c>null</c> when there are none. How they are rendered is provider specific;
    /// a dialect that does not implement table hints rejects a command that carries them.
    /// </summary>
    public IReadOnlyList<string>? TableHints
{
    get => _description?.Options.TableHints;
    internal set
    {
        if (ReferenceEquals(value, _description?.Options.TableHints)) return;
        WriteOptions().TableHints = value;
    }
}
    /// <summary>
    /// Index hints attached to the command's physical <c>FROM</c> table (MySQL/MariaDB
    /// <c>USE|FORCE|IGNORE INDEX</c>, SQLite <c>INDEXED BY</c>/<c>NOT INDEXED</c>, SQL Server
    /// <c>WITH (INDEX(...))</c>), or <c>null</c> when there are none. A dialect without a native form
    /// (see <see cref="ISqlDialect.IndexHints"/>) rejects a command that carries them.
    /// </summary>
    public IReadOnlyList<string>? IndexHints
{
    get => _description?.Options.IndexHints;
    internal set
    {
        if (ReferenceEquals(value, _description?.Options.IndexHints)) return;
        WriteOptions().IndexHints = value;
    }
}
    /// <summary>The intent of <see cref="IndexHints"/> (<c>USE</c>, <c>FORCE</c> or <c>IGNORE</c>).</summary>
    public IndexHintKind IndexHintKind
{
    get => _description?.Options.IndexHintKind ?? default;
    internal set
    {
        if (value == (_description?.Options.IndexHintKind ?? default)) return;
        WriteOptions().IndexHintKind = value;
    }
}
    /// <summary>
    /// Hints applied to every physical table in this command's scope (for example SQL Server
    /// <c>WITH (...)</c> on the primary and joined tables, or a <c>pg_hint_plan</c>/MySQL optimizer
    /// comment listing the participating aliases), or <c>null</c> when there are none. Set through the
    /// fluent <c>WithTablesInScopeHint</c> modifier. A dialect that supports neither form rejects the
    /// command when its SQL is built.
    /// </summary>
    public IReadOnlyList<string>? TablesInScopeHints
{
    get => _description?.Options.TablesInScopeHints;
    internal set
    {
        if (ReferenceEquals(value, _description?.Options.TablesInScopeHints)) return;
        WriteOptions().TablesInScopeHints = value;
    }
}
    /// <summary>
    /// Per-command override of identifier quoting: <c>true</c> quotes physical table/column names with
    /// the provider's delimiter, <c>false</c> emits them verbatim, and <c>null</c> inherits the
    /// context default (<c>DataContextBuilder.UseQuotedIdentifiers</c>). Set through
    /// <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public bool? QuoteIdentifiers
{
    get => _description?.Options.QuoteIdentifiers;
    internal set
    {
        if (value == _description?.Options.QuoteIdentifiers) return;
        WriteOptions().QuoteIdentifiers = value;
    }
}
    /// <summary>
    /// The identifier-quoting flag resolved for this command during preparation: the command override
    /// when present, otherwise the context default. It is part of the plan key (the inherited value
    /// must not let two contexts with different defaults share a cached plan).
    /// </summary>
    internal bool ResolvedQuoteIdentifiers
    {
        get => _preparation?.ResolvedQuoteIdentifiers ?? false;
        set
        {
            if (value == (_preparation?.ResolvedQuoteIdentifiers ?? false)) return;
            Preparation.ResolvedQuoteIdentifiers = value;
        }
    }
    /// <summary>
    /// Per-command override of the naming convention applied to auto-derived table/column names:
    /// <see langword="null"/> inherits the context default
    /// (<c>DataContextBuilder.UseNamingConvention</c>). Set through <c>WithNamingConvention</c>.
    /// </summary>
    public INamingConvention? NamingConvention
{
    get => _description?.Options.NamingConvention;
    internal set
    {
        if (ReferenceEquals(value, _description?.Options.NamingConvention)) return;
        WriteOptions().NamingConvention = value;
    }
}
    /// <summary>
    /// The naming convention resolved for this command during preparation: the command override when
    /// present, otherwise the context default. It is part of the plan key (the inherited value must
    /// not let two contexts with different defaults share a cached plan).
    /// </summary>
    internal INamingConvention? ResolvedNamingConvention
    {
        get => _preparation?.ResolvedNamingConvention;
        set
        {
            if (ReferenceEquals(value, _preparation?.ResolvedNamingConvention)) return;
            Preparation.ResolvedNamingConvention = value;
        }
    }
    /// <summary>
    /// Per-command override of the letter case in which SQL keywords are emitted: <see langword="null"/>
    /// inherits the context default (<c>DataContextBuilder.UseKeywordCase</c>). Set through
    /// <c>WithKeywordCase</c>.
    /// </summary>
    public KeywordCase? KeywordCase
{
    get => _description?.Options.KeywordCase;
    internal set
    {
        if (value == _description?.Options.KeywordCase) return;
        WriteOptions().KeywordCase = value;
    }
}
    /// <summary>
    /// The keyword case resolved for this command during preparation: the command override when present,
    /// otherwise the context default. It is part of the plan key (the inherited value must not let two
    /// contexts with different defaults share a cached plan).
    /// </summary>
    internal KeywordCase ResolvedKeywordCase
    {
        get => _preparation?.ResolvedKeywordCase ?? default;
        set
        {
            if (value == (_preparation?.ResolvedKeywordCase ?? default)) return;
            Preparation.ResolvedKeywordCase = value;
        }
    }
    /// <summary>
    /// Per-query override of the command timeout in seconds: <see langword="null"/> inherits the
    /// context default (<c>DataContextBuilder.UseCommandTimeout</c>), and a value of zero or less means
    /// the provider default. Set through <c>WithCommandTimeout</c>.
    /// </summary>
    public int? CommandTimeout
{
    get => _description?.Options.CommandTimeout;
    internal set
    {
        if (value == _description?.Options.CommandTimeout) return;
        WriteOptions().CommandTimeout = value;
    }
}
    /// <summary>
    /// The command timeout resolved for this command during preparation: the command override when
    /// present, otherwise the context default; <see langword="null"/> when neither is set and the
    /// provider default applies. It is part of the plan key (the inherited value must not let two
    /// contexts with different defaults share a cached command).
    /// </summary>
    internal int? ResolvedCommandTimeout
    {
        get => _preparation?.ResolvedCommandTimeout;
        set
        {
            if (value == (_preparation?.ResolvedCommandTimeout ?? null)) return;
            Preparation.ResolvedCommandTimeout = value;
        }
    }
    /// <summary>
    /// The trailing <c>FOR JSON</c> clause (SQL Server), or <c>null</c> when the result set is returned
    /// as rows. A dialect that does not implement it rejects a command that carries it.
    /// </summary>
    public ForJsonClause? ForJsonClause
{
    get => _description?.Clauses.ForJsonClause;
    internal set
    {
        if (value == _description?.Clauses.ForJsonClause) return;
        WriteClauses().ForJsonClause = value;
    }
}
    /// <summary>
    /// The trailing <c>FOR XML</c> clause (SQL Server), or <c>null</c> when the result set is returned
    /// as rows. Mutually exclusive with <see cref="ForJsonClause"/>.
    /// </summary>
    public ForXmlClause? ForXmlClause
{
    get => _description?.Clauses.ForXmlClause;
    internal set
    {
        if (value == _description?.Clauses.ForXmlClause) return;
        WriteClauses().ForXmlClause = value;
    }
}
    /// <summary>The <c>GROUP BY</c> key selector, or <c>null</c> when the query has no grouping.</summary>
    public LambdaExpression? GroupBy { get => _groupExp; }
    /// <summary>
    /// The super-aggregate modifier applied to the grouping list (<c>ROLLUP</c>/<c>CUBE</c>), or
    /// <see cref="GroupingType.None"/> for a plain <c>GROUP BY</c>. Only meaningful when a grouping
    /// list is present.
    /// </summary>
    public GroupingType GroupingType
{
    get => _description?.Clauses.GroupingType ?? default;
    internal set
    {
        if (value == (_description?.Clauses.GroupingType ?? default)) return;
        WriteClauses().GroupingType = value;
    }
}
    /// <summary>
    /// The explicit grouping sets (0-based indices into the grouping list) used when
    /// <see cref="GroupingType"/> is <see cref="NextORM.Core.GroupingType.GroupingSets"/>, or
    /// <c>null</c> otherwise. An empty set yields the grand total.
    /// </summary>
    public IReadOnlyList<int[]>? GroupingSets
{
    get => _description?.Clauses.GroupingSets;
    internal set
    {
        if (ReferenceEquals(value, _description?.Clauses.GroupingSets)) return;
        WriteClauses().GroupingSets = value;
    }
}
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
    internal IQueryRegistry? OuterRegistry
{
    get => _description?.Relations.OuterRegistry;
    set
    {
        if (ReferenceEquals(value, _description?.Relations.OuterRegistry)) return;
        WriteRelations().OuterRegistry = value;
    }
}

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
        // The node-owned preparation storage is reset in place; the fields it deliberately keeps
        // (the eleven plan hashes other than ReferencedQueriesPlanHash, the node-owned shape flags and
        // the comparers) survive exactly as they did when they were plain fields.
        _preparation?.Reset();
        // Correlated subqueries and outer references are registered while preparing (#148-B: the
        // navigation collection terminals add one referenced command per terminal). A re-preparation
        // rebuilds them from scratch; keeping the previous lists would append duplicates on every
        // reset/prepare cycle, changing the plan key and growing the registry without bound.
        _referencedQueries = null;
        _outerRefs = null;

        _dataContext?.ResetPreparation(this);
    }

    internal void ReplaceCommand(QueryCommand cmd, int idx)
    {
#if DEBUG
        if (_dataContext != cmd._dataContext)
            throw new InvalidOperationException("Different data context");
#endif

        var referenced = _referencedQueries ?? throw new InvalidOperationException("Referenced queries must be initialized");
        if (_description!.Relations.Shared)
        {
            referenced = [.. referenced];
            WriteRelations().ReferencedQueries = referenced;
        }

        referenced[idx] = cmd;
        // The scalar unkeyed flag was computed against the previous referenced graph. A swapped-in
        // referenced query can add or remove a scalar captured Contains in its WHERE/PREWHERE, which is
        // not re-keyed by the swap, so recompute the flag now to keep the planner's call-local suppression
        // in step with the current graph.
        HasUnkeyedScalarInValues = QueryPreparer.ScanUnkeyedScalarInValues(this);
        // The shared Any/Count command swaps its referenced subquery in place between executions
        // without resetting: the plan key it was prepared under no longer describes the query.
        InvalidatePlanKey();
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

        var referenced = _referencedQueries;
        if (referenced is null || _description!.Relations.Shared)
        {
            referenced = referenced is null ? [] : [.. referenced];
            WriteRelations().ReferencedQueries = referenced;
        }

        var idx = referenced.Count;
        referenced.Add(cmd);
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
            // The tail is shared: a.Union(b) stores the caller's operand b by reference, and
            // CopyTo(copyAll:true) hands the same tail to the next clone. Appending in place would
            // therefore rewrite a live command — the caller's operand b, or the command this clone was
            // derived from — so chaining (a.Union(b).Except(c)) would silently give b an EXCEPT.
            // Own a private copy of the tail before appending; ownership recursion keeps every level
            // private, and the rendered SQL is unchanged.
            _union = _union.Clone();
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

        var outerRefs = _outerRefs;
        if (outerRefs is null || _description!.Relations.Shared)
        {
            outerRefs = outerRefs is null ? [] : [.. outerRefs];
            WriteRelations().OuterRefs = outerRefs;
        }

        var idx = outerRefs.Count;

        outerRefs.Add(node);

        return idx;
    }

    /// <summary>
    /// Executes the command and returns its rows boxed, so a runtime-built projection command can be
    /// stitched by <c>JoinInto</c> without a compile-time result type. Overridden by
    /// <see cref="QueryCommand{TResult}"/>.
    /// </summary>
    /// <param name="params">Positional parameter values.</param>
    /// <returns>The materialized rows, boxed.</returns>
    internal virtual List<object?> ToObjectList(ReadOnlySpan<object?> @params)
        => throw new NotSupportedException("Only a typed query command can be executed for JoinInto stitching.");

    /// <summary>Asynchronous counterpart of <see cref="ToObjectList"/>.</summary>
    /// <param name="params">Positional parameter values.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>A task producing the materialized rows, boxed.</returns>
    internal virtual Task<List<object?>> ToObjectListAsync(object[] @params, CancellationToken cancellationToken)
        => throw new NotSupportedException("Only a typed query command can be executed for JoinInto stitching.");

}
