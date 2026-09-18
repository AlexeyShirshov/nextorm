using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Registry of the queries, outer references and plan-equality comparers that make up a query scope.
/// </summary>
/// <remarks>
/// Despite the name this is not a LINQ <see cref="System.Linq.IQueryProvider"/>: it coordinates the
/// plan cache (registrations, outer references and comparer instances) rather than building
/// queries. It also collides with <see cref="System.Linq.IQueryProvider"/> when both namespaces are
/// imported. See <c>API-NAMING-REVIEW.md</c> finding P0-2; the recommended name is
/// <c>IQueryRegistry</c>.
/// </remarks>
public interface IQueryProvider
{
    int AddCommand(QueryCommand cmd);
    IReadOnlyList<QueryCommand> ReferencedQueries { get; }
    IReadOnlyList<Expression>? OuterReferences { get; }

    QueryPlanEqualityComparer GetQueryPlanEqualityComparer();
    ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer();
    SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer();
    FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer();
    JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer();
    SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer();
    int AddOuterReference(Expression node);
    // PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer();
}