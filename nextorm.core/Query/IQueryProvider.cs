using nextorm.core;

public interface IQueryProvider
{
    int AddCommand(QueryCommand cmd);
    IReadOnlyList<QueryCommand> ReferencedColumns { get; }
}