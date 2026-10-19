using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Immutable memo of a <see cref="QueryDefinition"/> snapshot together with the node-owned inputs it was
/// built from. The memo belongs to one <see cref="QueryCommand"/> node, never to a shared description:
/// two commands that share a description each keep their own memo, so a reader can never observe a
/// definition published by a sibling. Value type by design: a definition read publishes the memo with a
/// single value write and allocates nothing on the heap.
/// </summary>
internal readonly struct DefinitionMemo
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
        Sorting[]? sorting,
        JoinExpression[]? joins,
        IReadOnlyList<CteDefinition>? ctes,
        QueryFilterScope filterScope,
        IReadOnlyDictionary<ParameterExpression, string>? navigationPaths)
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
        Joins = joins;
        Ctes = ctes;
        FilterScope = filterScope;
        NavigationPaths = navigationPaths;
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
    internal JoinExpression[]? Joins { get; }
    internal IReadOnlyList<CteDefinition>? Ctes { get; }
    internal QueryFilterScope FilterScope { get; }
    internal IReadOnlyDictionary<ParameterExpression, string>? NavigationPaths { get; }
}

/// <summary>
/// Immutable-by-contract raw description of a <see cref="QueryCommand"/>: every raw slot (the former
/// predicate/relation/clause/option blocks flattened into this single root) and a flag word for the
/// remaining raw booleans. A node owns at most one description; a description is shared with other nodes
/// only on copy-on-write terms (a write replaces the command's own description once).
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
    /// <summary>
    /// True while <see cref="ReferencedQueries"/> and <see cref="OuterRefs"/> may still be referenced by
    /// another node. Those two lists are appended in place, so their owner must copy them before the first
    /// append; this mirrors the sharing flag of the former relation block.
    /// </summary>
    internal bool SharedRelations;
    internal Type? ProjectionType;
    internal ulong Flags;

    internal QueryFilterScope FilterScope = QueryFilterScope.None;

    internal JoinExpression[]? Joins;
    internal IReadOnlyList<CteDefinition>? Ctes;
    internal List<QueryCommand>? ReferencedQueries;
    internal List<Expression>? OuterRefs;
    internal IQueryRegistry? OuterRegistry;
    internal QueryCommand? Union;
    internal UnionType UnionType;

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

    /// <summary>Marks this description, including its in-place collections, as shared by more than one node.</summary>
    internal void MarkShared()
    {
        Shared = true;
        SharedRelations = true;
    }

    /// <summary>
    /// Returns a description shell this command can mutate. The source shell is marked shared, so the
    /// returned copy owns only the shell until it replaces a slot.
    /// </summary>
    internal QueryDescription CloneShellForWrite()
    {
        MarkShared();
        var clone = (QueryDescription)MemberwiseClone();
        clone.Shared = false;
        return clone;
    }
}

public partial class QueryCommand
{
    private QueryDescription? _description;
    /// <summary>
    /// Node-owned memo of the last <see cref="Definition"/> snapshot, stored as a value-type field so
    /// publishing it is one value write with no heap allocation. It is deliberately not stored on the
    /// description: two commands that share a description must not observe each other's memo.
    /// </summary>
    private DefinitionMemo? _definitionMemo;

    /// <summary>Returns the command's mutable description, splitting it from any node that shares it.</summary>
    private QueryDescription WriteDescription()
    {
        // The definition memo is node-owned and depends on every raw write of this node: a writer that
        // arrives here (before installing the new value or the split description) invalidates it.
        _definitionMemo = null;
        var description = _description ??= new QueryDescription();
        if (description.Shared)
        {
            description = description.CloneShellForWrite();
            _description = description;
        }

        return description;
    }

    /// <summary>
    /// Returns the command's mutable description before a relation write and drops the relation-collection
    /// sharing condition, mirroring the former relation block: once a relation slot is written this node's
    /// relation state owns its field slots, so a later append may reuse the collections.
    /// </summary>
    private QueryDescription WriteRelations()
    {
        var description = WriteDescription();
        description.SharedRelations = false;
        return description;
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
        get => _description?.FilterScope ?? QueryFilterScope.None;
        set
        {
            var current = _description?.FilterScope ?? QueryFilterScope.None;
            if (ReferenceEquals(value, current) || value.Equals(current)) return;
            WriteDescription().FilterScope = value;
        }
    }

    private JoinExpression[]? _joins
    {
        get => _description?.Joins;
        set
        {
            if (ReferenceEquals(value, _description?.Joins)) return;
            WriteRelations().Joins = value;
        }
    }

    private IReadOnlyList<CteDefinition>? _ctes
    {
        get => _description?.Ctes;
        set
        {
            if (ReferenceEquals(value, _description?.Ctes)) return;
            WriteRelations().Ctes = value;
        }
    }

    private IReadOnlyList<string>? _hints
    {
        get => _description?.Hints;
        set
        {
            if (ReferenceEquals(value, _description?.Hints)) return;
            WriteDescription().Hints = value;
        }
    }

    private List<QueryCommand>? _referencedQueries
    {
        get => _description?.ReferencedQueries;
        set
        {
            if (ReferenceEquals(value, _description?.ReferencedQueries)) return;
            WriteRelations().ReferencedQueries = value;
        }
    }

    private List<Expression>? _outerRefs
    {
        get => _description?.OuterRefs;
        set
        {
            if (ReferenceEquals(value, _description?.OuterRefs)) return;
            WriteRelations().OuterRefs = value;
        }
    }

    private QueryCommand? _union
    {
        get => _description?.Union;
        set
        {
            if (ReferenceEquals(value, _description?.Union)) return;
            WriteRelations().Union = value;
        }
    }

    private UnionType _unionType
    {
        get => _description?.UnionType ?? default;
        set
        {
            if (value == (_description?.UnionType ?? default)) return;
            WriteRelations().UnionType = value;
        }
    }

    /// <summary>The ClickHouse <c>ARRAY JOIN</c> expressions before preparation, or <c>null</c> when the clause is absent.</summary>
    protected LambdaExpression[]? _arrayJoins;

    private IReadOnlyList<WindowDefinition>? _windows
    {
        get => _description?.Windows;
        set
        {
            if (ReferenceEquals(value, _description?.Windows)) return;
            WriteDescription().Windows = value;
        }
    }

    /// <summary>The sort columns before preparation, or <c>null</c> when the query has no <c>ORDER BY</c>.</summary>
    protected Sorting[]? _sorting;

    private void SetLogger(ILogger? value)
    {
        // A null logger must overwrite an inherited one: baseline assigned the definition's logger
        // unconditionally, so a derivation created without logging cannot keep the source's logger.
        if (ReferenceEquals(value, _description?.Logger)) return;
        WriteDescription().Logger = value;
    }
}
