using System.Linq.Expressions;

namespace NextORM.Core.Tests;

/// <summary>
/// Minimal <see cref="IQueryRegistry"/> test double used by the plan-comparer tests: they only need
/// the comparer factory methods and the reference lists, so everything else throws.
/// </summary>
class QueryProvider : IQueryRegistry
{
    public IReadOnlyList<QueryCommand> ReferencedQueries => throw new NotImplementedException();

    public IReadOnlyList<Expression>? OuterReferences => throw new NotImplementedException();

    public int AddCommand(QueryCommand cmd) => throw new NotImplementedException();

    public int AddOuterReference(Expression node) => throw new NotImplementedException();

    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => new(this);

    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => new(this);

    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => new(this);

    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => new(this);

    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => new(this);

    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => new(this);
}
