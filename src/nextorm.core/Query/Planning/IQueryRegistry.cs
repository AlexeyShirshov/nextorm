using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Coordinates query registration and outer-reference capture within a query scope. It also provides
/// the comparers used to key query plans and their expression components.
/// </summary>
public interface IQueryRegistry
{
    /// <summary>
    /// Registers <paramref name="cmd"/> as a referenced query and returns its zero-based index within
    /// <see cref="ReferencedQueries"/>.
    /// </summary>
    /// <param name="cmd">The command to reference.</param>
    int AddCommand(QueryCommand cmd);
    /// <summary>The queries referenced by the current query, indexed by the reference markers in its expressions.</summary>
    IReadOnlyList<QueryCommand> ReferencedQueries { get; }
    /// <summary>The outer expressions captured by the current query, or <c>null</c> when there are none.</summary>
    IReadOnlyList<Expression>? OuterReferences { get; }

    /// <summary>Returns the comparer used to key whole query plans in the plan cache.</summary>
    QueryPlanEqualityComparer GetQueryPlanEqualityComparer();
    /// <summary>Returns the comparer used to compare expression plans.</summary>
    ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer();
    /// <summary>Returns the comparer used to compare select-list plans.</summary>
    SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer();
    /// <summary>Returns the comparer used to compare FROM-source plans.</summary>
    FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer();
    /// <summary>Returns the comparer used to compare join-expression plans.</summary>
    JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer();
    /// <summary>Returns the comparer used to compare sorting-expression plans.</summary>
    SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer();
    /// <summary>Registers an outer expression and returns its zero-based index within <see cref="OuterReferences"/>.</summary>
    /// <param name="node">The outer expression to capture.</param>
    int AddOuterReference(Expression node);
    // PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer();
}