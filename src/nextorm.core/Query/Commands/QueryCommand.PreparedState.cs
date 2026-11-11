using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Preparation-only state of a <see cref="QueryCommand"/>, held behind a single reference.
/// </summary>
/// <remarks>
/// A fresh fluent command is created for every call; this payload is created only when the command is
/// actually prepared (a cache miss or an explicit <c>Prepare</c>). Nothing on the warm-hit path writes
/// it, so a hit never pays for it (issue #319: the collected description must stay compact).
/// <para>
/// Reads go through <c>Prep?</c> and never allocate: a collected command has no payload and every
/// member defaults to <c>null</c>/<c>0</c>. Writes go through <c>Prepared</c>, which creates the
/// payload on first use.
/// </para>
/// </remarks>
internal sealed class QueryCommandPreparedState
{
    /// <summary>The prepared grouping columns.</summary>
    public SelectExpression[]? GroupingList;
    /// <summary>The prepared <c>LIMIT BY</c> columns.</summary>
    public SelectExpression[]? LimitByColumns;
    /// <summary>The prepared <c>DISTINCT ON</c> columns.</summary>
    public SelectExpression[]? DistinctOnColumns;
    /// <summary>The prepared extreme-row value columns.</summary>
    public SelectExpression[]? ExtremeRowColumns;
    /// <summary>The prepared extreme-row grouping columns.</summary>
    public SelectExpression[]? ExtremeRowGroupByColumns;
    /// <summary>The prepared per-parent partition columns.</summary>
    public SelectExpression[]? PerParentPartitionColumns;
    /// <summary>The prepared <c>ARRAY JOIN</c> expressions.</summary>
    public Expression[]? PreparedArrayJoin;
    /// <summary>The <c>WHERE</c> predicate after the correlated-query visitor rewriter ran.</summary>
    public Expression? PreparedCondition;
    /// <summary>The <c>HAVING</c> predicate after the correlated-query visitor rewriter ran.</summary>
    public Expression? PreparedHaving;
    /// <summary>The prepared <c>PREWHERE</c> expression.</summary>
    public Expression? PreparedPreWhere;
    /// <summary>Shape hash of any captured value-list in <see cref="PreparedPreWhere"/>.</summary>
    public int PreWhereShapeHash;
    /// <summary>Whether <see cref="PreparedPreWhere"/> carries a top-level value list.</summary>
    public bool HasPreWhereInValues;
    /// <summary>The evaluated value-list partitions keyed by collection expression.</summary>
    public Dictionary<Expression, InValuesPartition>? InValuesPartitions;
    /// <summary>The evaluated lookup partitions keyed by index expression.</summary>
    public Dictionary<Expression, List<LookupEntry>>? LookupPartitions;
    /// <summary>Whether <see cref="PreparedCondition"/> carries a top-level captured-collection node.</summary>
    public bool HasTopLevelInValues;
    /// <summary>Whether the captured-collection shape was folded into the plan key.</summary>
    public bool ShapeScanned;
    /// <summary>Whether every matched collection has a shape-stable value.</summary>
    public bool ShapeValuesStable;
    /// <summary>Whether an unkeyed tuple-valued <c>IN</c> appears in the query.</summary>
    public bool HasUnkeyedTupleInValues;
    /// <summary>Whether an unkeyed scalar <c>IN</c> appears in the query.</summary>
    public bool HasUnkeyedScalarInValues;
    /// <summary>The commands referenced by correlated subqueries, in registration order.</summary>
    public List<QueryCommand>? ReferencedQueries;
    /// <summary>The outer expressions captured by a correlated subquery.</summary>
    public List<Expression>? OuterRefs;
    /// <summary>The occurrence bindings paired with <see cref="OuterRefs"/>.</summary>
    public List<OuterReferenceBinding?>? OuterRefBindings;
    /// <summary>The APPLY left-row owners currently being prepared.</summary>
    public List<(ParameterExpression Owner, int Slots)>? ApplyLeftOwners;
    /// <summary>The runtime result type a boxed wrapper command materializes for.</summary>
    public Type? BoxedResultType;
    /// <summary>The effective grouping keys contributed by a preparation descriptor.</summary>
    public IReadOnlyList<LambdaExpression>? EffectiveGroupingKeys;
    /// <summary>How the effective <c>HAVING</c> is handled.</summary>
    public QueryPreparationHavingMode EffectiveHavingMode;
    /// <summary>The replacement <c>HAVING</c> predicate.</summary>
    public LambdaExpression? EffectiveHaving;
    /// <summary>An additive source predicate AND-ed into the effective condition.</summary>
    public LambdaExpression? EffectiveSourcePredicate;
    /// <summary>Whether a contribution requires <c>DISTINCT</c>.</summary>
    public bool EffectiveRequireDistinct;
    /// <summary>The recursively captured JSON projection shape.</summary>
    public JsonShapeNode? JsonShape;
    /// <summary>Whether the JSON shape mode is active.</summary>
    public bool JsonShapeMode;
    /// <summary>The prepared component descriptors.</summary>
    public IReadOnlyList<IQueryComponentDescriptor>? PreparedComponents;
    /// <summary>The cached read-only view over <see cref="PreparedComponents"/>.</summary>
    public IReadOnlyList<IQueryComponentDescriptor>? PreparedComponentsView;
    /// <summary>The render-scoped component shape.</summary>
    public QueryCommand? ComponentRenderShape;
    /// <summary>The base <c>WHERE</c> plan hash before the value-list shape is folded in.</summary>
    public int WhereBasePlanHash;
}

public partial class QueryCommand
{
    private QueryCommandPreparedState? _preparedState;

    /// <summary>The prepared payload, created on first write.</summary>
    internal QueryCommandPreparedState Prepared => _preparedState ??= new QueryCommandPreparedState();

    /// <summary>The prepared payload without creating it; reads use this and stay allocation-free.</summary>
    internal QueryCommandPreparedState? Prep => _preparedState;
}
