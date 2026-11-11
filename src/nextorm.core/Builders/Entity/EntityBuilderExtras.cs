using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Rarely used fluent options of an <see cref="EntityBuilder{TEntity}"/>, held behind one reference.
/// </summary>
/// <remarks>
/// A builder is created and copied by every fluent step, and the common query never sets any of these
/// options. Keeping them out of the builder object means the common builder stays small; the payload is
/// allocated only when an option is actually set (issue #319: the raw state covers the builder lifetime).
/// Writes of a default value clear the member in place and never allocate.
/// </remarks>
/// <typeparam name="TEntity">The mapped entity type of the builder.</typeparam>
internal sealed class EntityBuilderExtras<TEntity>
{
    /// <summary>The <c>LIMIT BY</c> clause.</summary>
    public LimitByClause? LimitBy;
    /// <summary>The <c>DISTINCT ON</c> clause.</summary>
    public DistinctOnClause? DistinctOn;
    /// <summary>The extreme-row (<c>SelectWhereMin/Max</c>) clause.</summary>
    public ExtremeRowClause? ExtremeRow;
    /// <summary>The attached query-component state (one component or a list).</summary>
    public object? ComponentState;
    /// <summary>The table-sample clause.</summary>
    public TableSampleClause? Tablesample;
    /// <summary>The temporal-table clause.</summary>
    public TemporalClause? Temporal;
    /// <summary>The result-row lock clause.</summary>
    public LockClause? RowLock;
    /// <summary>The source lock clause.</summary>
    public LockClause? SourceLock;
    /// <summary>The ClickHouse <c>PREWHERE</c> predicate.</summary>
    public LambdaExpression? PreWhere;
    /// <summary>The ClickHouse <c>ARRAY JOIN</c> expressions.</summary>
    public List<LambdaExpression>? ArrayJoins;
    /// <summary>The <c>ARRAY JOIN</c> kind.</summary>
    public ArrayJoinKind ArrayJoinKind;
    /// <summary>The named window definitions.</summary>
    public List<WindowDefinition>? Windows;
    /// <summary>The ClickHouse <c>SETTINGS</c> entries.</summary>
    public List<KeyValuePair<string, string>>? Settings;
    /// <summary>The eager-load specifications.</summary>
    public List<IEagerLoadSpec<TEntity>>? LoadSpecs;
    /// <summary>The <c>JoinInto</c> specifications.</summary>
    public List<IJoinIntoSpec<TEntity>>? JoinIntos;
    /// <summary>The provider hint attached to a derived-table source.</summary>
    public string? SubQueryHint;
    /// <summary>The physical table-name override.</summary>
    public string? TableNameOverride;
    /// <summary>The schema override.</summary>
    public string? SchemaOverride;
    /// <summary>The database override.</summary>
    public string? DatabaseOverride;
    /// <summary>The server override.</summary>
    public string? ServerOverride;
    /// <summary>The raw table expression override.</summary>
    public string? TableExpression;
}
