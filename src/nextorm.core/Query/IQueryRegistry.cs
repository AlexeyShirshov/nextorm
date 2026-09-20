using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Registry of the queries, outer references and plan-equality comparers that make up a query scope.
/// </summary>
/// <remarks>
/// It coordinates the plan cache (query registrations, outer references and comparer instances)
/// rather than building queries, so it is deliberately named <c>IQueryRegistry</c> to avoid the
/// collision with <see cref="System.Linq.IQueryProvider"/>.
/// </remarks>
public interface IQueryRegistry
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