namespace NextORM.Core;

public partial class QueryCommand
{

    /// <summary>
    /// Re-evaluates the value-list shape of an already-prepared command. The implementation lives in
    /// <see cref="QueryPreparer"/>, next to the rest of the preparation pipeline.
    /// </summary>
    internal void RefreshInValuesShape() => QueryPreparer.RefreshInValuesShape(this);

    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => _queryPlanComparer ??= new QueryPlanEqualityComparer(this);
    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => _expressionPlanComparer ??= new ExpressionPlanEqualityComparer(this);
    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => _selectExpressionPlanComparer ??= new SelectExpressionPlanEqualityComparer(this);
    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => _fromExpressionPlanComparer ??= new FromExpressionPlanEqualityComparer(this);
    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => _joinExpressionPlanComparer ??= new JoinExpressionPlanEqualityComparer(this);
    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => _sortingExpressionPlanComparer ??= new SortingExpressionPlanEqualityComparer(this);

}
