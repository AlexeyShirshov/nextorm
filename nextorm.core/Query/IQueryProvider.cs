namespace nextorm.core;

public interface IQueryProvider
{
    int AddCommand(QueryCommand cmd);
    IReadOnlyList<QueryCommand> ReferencedQueries { get; }
    QueryPlanEqualityComparer GetQueryPlanEqualityComparer();
}