using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Predicate half of a command's raw description: the disabled selective query-filter scope. The
/// <c>WHERE</c>/<c>GROUP BY</c>/<c>HAVING</c>/<c>PREWHERE</c> lambdas are node-owned fields, not
/// description state. Copied on write; the block is shared between command nodes only at
/// clone/CTE-construction edges and is never mutated in place while shared.
/// </summary>
internal sealed class QueryPredicates
{
    internal static readonly QueryPredicates Empty = new() { Shared = true };

    internal bool Shared;
    internal QueryFilterScope FilterScope = QueryFilterScope.None;

    internal QueryPredicates()
    {
    }

    internal QueryPredicates(QueryPredicates other)
    {
        FilterScope = other.FilterScope;
    }
}

/// <summary>
/// Relation half of a command's raw description: joins, CTE declarations, correlated referenced queries,
/// outer references, the registry they resolve against and the set-operation operand.
/// </summary>
internal sealed class QueryRelations
{
    internal static readonly QueryRelations Empty = new() { Shared = true };

    internal bool Shared;
    internal JoinExpression[]? Joins;
    internal IReadOnlyList<CteDefinition>? Ctes;
    internal List<QueryCommand>? ReferencedQueries;
    internal List<Expression>? OuterRefs;
    internal IQueryRegistry? OuterRegistry;
    internal QueryCommand? Union;
    internal UnionType UnionType;

    internal QueryRelations()
    {
    }

    internal QueryRelations(QueryRelations other)
    {
        Joins = other.Joins;
        Ctes = other.Ctes;
        ReferencedQueries = other.ReferencedQueries;
        OuterRefs = other.OuterRefs;
        OuterRegistry = other.OuterRegistry;
        Union = other.Union;
        UnionType = other.UnionType;
    }
}

/// <summary>
/// Clause half of a command's raw description: the provider clause objects, ClickHouse modifiers, trailing
/// clauses, grouping modifiers and navigation-expansion metadata. Copied on write.
/// </summary>
internal sealed class QueryClauses
{
    internal static readonly QueryClauses Empty = new() { Shared = true };

    internal bool Shared;
    internal LimitByClause? LimitBy;
    internal DistinctOnClause? DistinctOn;
    internal ExtremeRowClause? ExtremeRow;
    internal TableSampleClause? TableSample;
    internal TemporalClause? Temporal;
    internal LockClause? RowLock;
    internal LockClause? SourceLockClause;
    internal double? SampleRatio;
    internal double SampleOffset;
    internal IReadOnlyList<KeyValuePair<string, string>>? Settings;
    internal IReadOnlyList<WindowDefinition>? Windows;
    internal ArrayJoinKind ArrayJoinKind;
    internal GroupingType GroupingType;
    internal IReadOnlyList<int[]>? GroupingSets;
    internal ForJsonClause? ForJsonClause;
    internal ForXmlClause? ForXmlClause;
    internal IReadOnlyDictionary<ParameterExpression, string>? NavigationPaths;

    internal QueryClauses()
    {
    }

    internal QueryClauses(QueryClauses other)
    {
        LimitBy = other.LimitBy;
        DistinctOn = other.DistinctOn;
        ExtremeRow = other.ExtremeRow;
        TableSample = other.TableSample;
        Temporal = other.Temporal;
        RowLock = other.RowLock;
        SourceLockClause = other.SourceLockClause;
        SampleRatio = other.SampleRatio;
        SampleOffset = other.SampleOffset;
        Settings = other.Settings;
        Windows = other.Windows;
        ArrayJoinKind = other.ArrayJoinKind;
        GroupingType = other.GroupingType;
        GroupingSets = other.GroupingSets;
        ForJsonClause = other.ForJsonClause;
        ForXmlClause = other.ForXmlClause;
        NavigationPaths = other.NavigationPaths;
    }
}

/// <summary>
/// Option half of a command's raw description: diagnostics/hints, identifier and naming overrides, eager
/// loading strategy, per-parent paging details and in-memory navigation lowering data. Optional: the block
/// stays on the shared empty singleton until a non-default value is written.
/// </summary>
internal sealed class QueryOptions
{
    internal static readonly QueryOptions Empty = new() { Shared = true };

    internal bool Shared;
    internal ILogger? Logger;
    internal string? Tag;
    internal IReadOnlyList<string>? Hints;
    internal IReadOnlyList<string>? TableHints;
    internal IReadOnlyList<string>? IndexHints;
    internal IndexHintKind IndexHintKind;
    internal IReadOnlyList<string>? TablesInScopeHints;
    internal bool? QuoteIdentifiers;
    internal INamingConvention? NamingConvention;
    internal KeywordCase? KeywordCase;
    internal int? CommandTimeout;
    internal EagerLoadMode? EagerLoadMode;
    internal EagerLoadShape EagerLoadShape;
    internal bool HasPerParentEagerTopN;
    internal bool PerParentPaging;
    internal LambdaExpression? PerParentPartition;
    internal bool PerParentPagingEmbedded;
    internal bool IgnoreColumns;
    internal JunctionChildFilter? JunctionChildFilter;
    internal InMemoryNavigationChain? NavigationChain;

    internal QueryOptions()
    {
    }

    internal QueryOptions(QueryOptions other)
    {
        Logger = other.Logger;
        Tag = other.Tag;
        Hints = other.Hints;
        TableHints = other.TableHints;
        IndexHints = other.IndexHints;
        IndexHintKind = other.IndexHintKind;
        TablesInScopeHints = other.TablesInScopeHints;
        QuoteIdentifiers = other.QuoteIdentifiers;
        NamingConvention = other.NamingConvention;
        KeywordCase = other.KeywordCase;
        CommandTimeout = other.CommandTimeout;
        EagerLoadMode = other.EagerLoadMode;
        EagerLoadShape = other.EagerLoadShape;
        HasPerParentEagerTopN = other.HasPerParentEagerTopN;
        PerParentPaging = other.PerParentPaging;
        PerParentPartition = other.PerParentPartition;
        PerParentPagingEmbedded = other.PerParentPagingEmbedded;
        IgnoreColumns = other.IgnoreColumns;
        JunctionChildFilter = other.JunctionChildFilter;
        NavigationChain = other.NavigationChain;
    }
}

/// <summary>
/// Immutable memo of a <see cref="QueryDefinition"/> snapshot together with the node-owned inputs it was
/// built from. The description holds one reference to the memo, so a reader of a shared description can
/// never observe a definition and a snapshot from two different writes.
/// </summary>
internal sealed class DefinitionMemo
{
    internal DefinitionMemo(
        QueryDefinition definition,
        Paging paging,
        LambdaExpression? exp,
        Type? srcType,
        LambdaExpression? condition,
        LambdaExpression? group,
        LambdaExpression? having,
        LambdaExpression? preWhere,
        LambdaExpression[]? arrayJoins,
        Sorting[]? sorting)
    {
        Definition = definition;
        Paging = paging;
        Exp = exp;
        SrcType = srcType;
        Condition = condition;
        Group = group;
        Having = having;
        PreWhere = preWhere;
        ArrayJoins = arrayJoins;
        Sorting = sorting;
    }

    internal QueryDefinition Definition { get; }
    internal Paging Paging { get; }
    internal LambdaExpression? Exp { get; }
    internal Type? SrcType { get; }
    internal LambdaExpression? Condition { get; }
    internal LambdaExpression? Group { get; }
    internal LambdaExpression? Having { get; }
    internal LambdaExpression? PreWhere { get; }
    internal LambdaExpression[]? ArrayJoins { get; }
    internal Sorting[]? Sorting { get; }
}

/// <summary>
/// Immutable-by-contract raw description of a <see cref="QueryCommand"/>: the five grouped raw blocks
/// and a flag word for the remaining raw booleans. A node owns at most one description; a description is
/// shared with other nodes only on copy-on-write terms (every mutation replaces the command's own
/// description and the touched block).
/// </summary>
internal sealed class QueryDescription
{
    internal const ulong FlagIdentitySlotAliases = 1UL << 0;
    internal const ulong FlagIsDistinct = 1UL << 1;
    internal const ulong FlagGroupByWithTotals = 1UL << 2;
    internal const ulong FlagFinal = 1UL << 3;
    internal const ulong FlagBindArrayJoinElement = 1UL << 4;
    internal const ulong FlagIsWideNavigationCount = 1UL << 5;
    internal const ulong FlagSingleRow = 1UL << 6;
    internal const ulong FlagOneColumn = 1UL << 7;
    internal const ulong FlagDocumentMode = 1UL << 8;
    internal const ulong FlagNeedsOuterJoinNullSettings = 1UL << 9;

    internal bool Shared;
    internal Type? ProjectionType;
    internal ulong Flags;
    internal QueryPredicates Predicates = QueryPredicates.Empty;
    internal QueryRelations Relations = QueryRelations.Empty;
    internal QueryClauses Clauses = QueryClauses.Empty;
    internal QueryOptions Options = QueryOptions.Empty;
    /// <summary>
    /// The memoised <see cref="QueryDefinition"/> snapshot together with the paging it was built from.
    /// One immutable object per memo keeps the pair consistent under concurrent reads of a shared
    /// description: a single reference write publishes both values, never a torn <see cref="Paging"/>.
    /// </summary>
    internal DefinitionMemo? DefinitionCache;

    /// <summary>Marks this description and every block it references as shared by more than one node.</summary>
    internal void MarkShared()
    {
        Shared = true;
        Predicates.Shared = true;
        Relations.Shared = true;
        Clauses.Shared = true;
        Options.Shared = true;
    }

    /// <summary>
    /// Returns a description shell this command can mutate. The source shell and its blocks are marked
    /// shared, so the returned copy owns only the shell until it replaces a block.
    /// </summary>
    internal QueryDescription CloneShellForWrite()
    {
        MarkShared();
        var clone = (QueryDescription)MemberwiseClone();
        clone.Shared = false;
        clone.DefinitionCache = null;
        return clone;
    }
}

public partial class QueryCommand
{
    private QueryDescription? _description;

    private QueryDescription Description => _description ??= new QueryDescription();

    /// <summary>Returns the command's mutable description, splitting it from any node that shares it.</summary>
    private QueryDescription WriteDescription()
    {
        var description = _description ??= new QueryDescription();
        description.DefinitionCache = null;
        if (description.Shared)
        {
            description = description.CloneShellForWrite();
            _description = description;
        }

        return description;
    }

    private QueryPredicates WritePredicates()
    {
        var description = WriteDescription();
        var block = description.Predicates;
        if (block.Shared)
        {
            block = new QueryPredicates(block);
            description.Predicates = block;
        }

        return block;
    }

    private QueryRelations WriteRelations()
    {
        var description = WriteDescription();
        var block = description.Relations;
        if (block.Shared)
        {
            block = new QueryRelations(block);
            description.Relations = block;
        }

        return block;
    }

    private QueryClauses WriteClauses()
    {
        var description = WriteDescription();
        var block = description.Clauses;
        if (block.Shared)
        {
            block = new QueryClauses(block);
            description.Clauses = block;
        }

        return block;
    }

    private QueryOptions WriteOptions()
    {
        var description = WriteDescription();
        var block = description.Options;
        if (block.Shared)
        {
            block = new QueryOptions(block);
            description.Options = block;
        }

        return block;
    }

    private bool GetFlag(ulong flag) => _description is { } description && (description.Flags & flag) != 0;

    private void SetFlag(ulong flag, bool value)
    {
        if (value == GetFlag(flag)) return;
        var description = WriteDescription();
        description.Flags = value ? description.Flags | flag : description.Flags & ~flag;
    }

    /// <summary>The original projection lambda, or <c>null</c> for a command without an explicit projection.</summary>
    protected LambdaExpression? _exp;

    /// <summary>The entity type the command reads from, or <c>null</c> for a command without a source type.</summary>
    protected Type? _srcType;

    /// <summary>The <c>WHERE</c> predicate lambda before preparation, or <c>null</c> when there is none.</summary>
    protected LambdaExpression? _condition;

    /// <summary>The <c>GROUP BY</c> key selector before preparation, or <c>null</c> when there is no grouping.</summary>
    protected LambdaExpression? _groupExp;

    /// <summary>The <c>HAVING</c> predicate before preparation, or <c>null</c> when there is none.</summary>
    protected LambdaExpression? _having;

    /// <summary>The ClickHouse <c>PREWHERE</c> predicate before preparation, or <c>null</c> when there is none.</summary>
    protected LambdaExpression? _preWhere;

    /// <summary>The selective query-filter scope disabled for this command.</summary>
    private QueryFilterScope _filterScope
    {
        get => _description?.Predicates.FilterScope ?? QueryFilterScope.None;
        set
        {
            var current = _description?.Predicates.FilterScope ?? QueryFilterScope.None;
            if (ReferenceEquals(value, current) || value.Equals(current)) return;
            WritePredicates().FilterScope = value;
        }
    }

    private JoinExpression[]? _joins
    {
        get => _description?.Relations.Joins;
        set
        {
            if (ReferenceEquals(value, _description?.Relations.Joins)) return;
            WriteRelations().Joins = value;
        }
    }

    private IReadOnlyList<CteDefinition>? _ctes
    {
        get => _description?.Relations.Ctes;
        set
        {
            if (ReferenceEquals(value, _description?.Relations.Ctes)) return;
            WriteRelations().Ctes = value;
        }
    }

    private IReadOnlyList<string>? _hints
    {
        get => _description?.Options.Hints;
        set
        {
            if (ReferenceEquals(value, _description?.Options.Hints)) return;
            WriteOptions().Hints = value;
        }
    }

    private List<QueryCommand>? _referencedQueries
    {
        get => _description?.Relations.ReferencedQueries;
        set
        {
            if (ReferenceEquals(value, _description?.Relations.ReferencedQueries)) return;
            WriteRelations().ReferencedQueries = value;
        }
    }

    private List<Expression>? _outerRefs
    {
        get => _description?.Relations.OuterRefs;
        set
        {
            if (ReferenceEquals(value, _description?.Relations.OuterRefs)) return;
            WriteRelations().OuterRefs = value;
        }
    }

    private QueryCommand? _union
    {
        get => _description?.Relations.Union;
        set
        {
            if (ReferenceEquals(value, _description?.Relations.Union)) return;
            WriteRelations().Union = value;
        }
    }

    private UnionType _unionType
    {
        get => _description?.Relations.UnionType ?? default;
        set
        {
            if (value == (_description?.Relations.UnionType ?? default)) return;
            WriteRelations().UnionType = value;
        }
    }

    /// <summary>The ClickHouse <c>ARRAY JOIN</c> expressions before preparation, or <c>null</c> when the clause is absent.</summary>
    protected LambdaExpression[]? _arrayJoins;

    private IReadOnlyList<WindowDefinition>? _windows
    {
        get => _description?.Clauses.Windows;
        set
        {
            if (ReferenceEquals(value, _description?.Clauses.Windows)) return;
            WriteClauses().Windows = value;
        }
    }

    /// <summary>The sort columns before preparation, or <c>null</c> when the query has no <c>ORDER BY</c>.</summary>
    protected Sorting[]? _sorting;

    private void SetLogger(ILogger? value)
    {
        if (value is null) return;
        WriteOptions().Logger = value;
    }
}
