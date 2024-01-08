namespace nextorm.core;

public interface IQueryProvider
{
    int AddCommand(QueryCommand cmd);
    IReadOnlyList<QueryCommand> ReferencedQueries { get; }
    QueryPlanEqualityComparer GetQueryPlanEqualityComparer();
    ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer();
    SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer();
    FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer();
    JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer();
    SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer();
    PreciseExpressionEqualityComparer GetPreciseExpressionEqualityComparer();
}