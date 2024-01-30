using System.Linq.Expressions;

namespace nextorm.core;

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