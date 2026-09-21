using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Describes the shape of a <see cref="QueryCommand"/>: the projection/entity source, the condition,
/// joins, paging, ordering, grouping and logging. It groups the collaborators that used to travel as
/// a long constructor parameter list through <see cref="QueryCommand"/>,
/// <see cref="QueryCommand{TResult}"/> and <c>CreateCommand</c>.
/// </summary>
/// <remarks>
/// Immutable; use <c>with</c> to derive a variant (for example a re-ordered clone). Exactly one of
/// <see cref="Exp"/> or <see cref="SrcType"/> identifies the source: <see cref="Exp"/> is the
/// projection lambda for the generic command, <see cref="SrcType"/> the entity type when there is no
/// projection.
/// </remarks>
public sealed record QueryDefinition
{
    /// <summary>Projection lambda of the query, or <c>null</c> when <see cref="SrcType"/> is used.</summary>
    public LambdaExpression? Exp { get; init; }
    /// <summary>Entity type of the query when there is no projection lambda.</summary>
    public Type? SrcType { get; init; }
    /// <summary>Optional predicate applied as a <c>WHERE</c> clause.</summary>
    public LambdaExpression? Condition { get; init; }
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
    /// <summary>Whether the grouping carries the <c>WITH TOTALS</c> modifier (ClickHouse).</summary>
    public bool GroupByWithTotals { get; init; }
    /// <summary>Optional <c>LIMIT n BY expr</c> clause (ClickHouse).</summary>
    internal LimitByClause? LimitBy { get; init; }
    /// <summary>Optional <c>DISTINCT ON (expr, ...)</c> clause (PostgreSQL).</summary>
    internal DistinctOnClause? DistinctOn { get; init; }
    /// <summary>Optional <c>TABLESAMPLE</c> table modifier.</summary>
    internal TableSampleClause? TableSample { get; init; }
    /// <summary>Optional <c>FOR SYSTEM_TIME</c> temporal-table clause.</summary>
    internal TemporalClause? Temporal { get; init; }
    /// <summary>Optional trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause.</summary>
    internal LockClause? RowLock { get; init; }
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
    /// <summary>
    /// Named window definitions declared for this query (<c>WINDOW w AS (...)</c>), or <c>null</c> when
    /// the query declares none. A dialect that does not support the clause rejects a query that carries
    /// them.
    /// </summary>
    public IReadOnlyList<WindowDefinition>? Windows { get; init; }
    /// <summary>
    /// Optional ClickHouse <c>ARRAY JOIN</c> expressions (the array lambdas accumulated by the builder),
    /// or <c>null</c> when the clause is absent.
    /// </summary>
    internal IReadOnlyList<LambdaExpression>? ArrayJoins { get; init; }
    /// <summary>The <c>ARRAY JOIN</c> kind (plain or <c>LEFT</c>) shared by <see cref="ArrayJoins"/>.</summary>
    internal ArrayJoinKind ArrayJoinKind { get; init; }
    /// <summary>
    /// Whether the last <c>ARRAY JOIN</c> expression is aliased as the element referenced by
    /// <see cref="ArrayJoinProjection{TEntity, TElement}.Element"/>.
    /// </summary>
    internal bool BindArrayJoinElement { get; init; }

    /// <summary>Creates a definition whose source is the given projection lambda.</summary>
    public static QueryDefinition ForProjection(LambdaExpression exp) => new() { Exp = exp };

    /// <summary>Creates a definition whose source is the given entity type (no projection lambda).</summary>
    public static QueryDefinition ForEntity(Type entityType) => new() { SrcType = entityType };
}
