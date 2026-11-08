using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Immutable read-only view of an unprepared <see cref="QueryCommand"/> exposed to a query component
/// during <see cref="IQueryComponent.Prepare"/>. It never mutates the command and is safe to capture.
/// </summary>
public interface IQueryPreparationContext
{
    /// <summary>The command being prepared.</summary>
    QueryCommand Command { get; }

    /// <summary>The source entity type.</summary>
    Type SourceType { get; }

    /// <summary>The raw projection expression, if any.</summary>
    LambdaExpression? Projection { get; }

    /// <summary>The effective source condition (WHERE), if any.</summary>
    LambdaExpression? Where { get; }

    /// <summary>The HAVING expression, if any.</summary>
    LambdaExpression? Having { get; }

    /// <summary>The sortings, if any.</summary>
    IReadOnlyList<Sorting>? Sorting { get; }

    /// <summary>The grouping expression, if any.</summary>
    LambdaExpression? GroupBy { get; }

    /// <summary>Whether grouping includes totals.</summary>
    bool GroupByWithTotals { get; }

    /// <summary>The grouping type (None/Rollup/Cube/GroupingSets), if applicable.</summary>
    NextORM.Core.GroupingType GroupingType { get; }

    /// <summary>The grouping sets, if any.</summary>
    IReadOnlyList<int[]>? GroupingSets { get; }
}

/// <summary>
/// Default immutable implementation of <see cref="IQueryPreparationContext"/> over a command.
/// </summary>
public sealed class QueryPreparationContext : IQueryPreparationContext
{
    /// <summary>Creates a context over <paramref name="command"/>.</summary>
    public QueryPreparationContext(QueryCommand command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        SourceType = command.Definition.SrcType ?? command.GetType().GenericTypeArguments.FirstOrDefault() ?? typeof(object);
        Projection = command.ProjectionExpression;
        Where = command.Condition as LambdaExpression;
        Having = command.Having;
        Sorting = command.Sorting;
        GroupBy = command.GroupBy;
        GroupByWithTotals = command.GroupByWithTotals;
        GroupingType = command.GroupingType;
        GroupingSets = command.GroupingSets;
    }

    /// <inheritdoc />
    public QueryCommand Command { get; }

    /// <inheritdoc />
    public Type SourceType { get; }

    /// <inheritdoc />
    public LambdaExpression? Projection { get; }

    /// <inheritdoc />
    public LambdaExpression? Where { get; }

    /// <inheritdoc />
    public LambdaExpression? Having { get; }

    /// <inheritdoc />
    public IReadOnlyList<Sorting>? Sorting { get; }

    /// <inheritdoc />
    public LambdaExpression? GroupBy { get; }

    /// <inheritdoc />
    public bool GroupByWithTotals { get; }

    /// <summary>The grouping type.</summary>
    public NextORM.Core.GroupingType GroupingType { get; }

    /// <summary>The grouping sets.</summary>
    public IReadOnlyList<int[]>? GroupingSets { get; }
}
