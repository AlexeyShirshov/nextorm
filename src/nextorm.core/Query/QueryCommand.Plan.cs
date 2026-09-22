namespace NextORM.Core;

public partial class QueryCommand
{

    /// <summary>
    /// Re-evaluates the value-list shape of an already-prepared command. The implementation lives in
    /// <see cref="QueryPreparer"/>, next to the rest of the preparation pipeline.
    /// </summary>
    internal void RefreshInValuesShape() => QueryPreparer.RefreshInValuesShape(this);

    /// <summary>Returns the comparer used to key whole query plans, creating it on first use.</summary>
    /// <returns>The query-plan comparer.</returns>
    public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => _queryPlanComparer ??= new QueryPlanEqualityComparer(this);
    /// <summary>Returns the comparer used to compare expression plans, creating it on first use.</summary>
    /// <returns>The expression-plan comparer.</returns>
    public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => _expressionPlanComparer ??= new ExpressionPlanEqualityComparer(this);
    /// <summary>Returns the comparer used to compare select-list plans, creating it on first use.</summary>
    /// <returns>The select-list comparer.</returns>
    public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => _selectExpressionPlanComparer ??= new SelectExpressionPlanEqualityComparer(this);
    /// <summary>Returns the comparer used to compare FROM-source plans, creating it on first use.</summary>
    /// <returns>The FROM-source comparer.</returns>
    public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => _fromExpressionPlanComparer ??= new FromExpressionPlanEqualityComparer(this);
    /// <summary>Returns the comparer used to compare join-expression plans, creating it on first use.</summary>
    /// <returns>The join-expression comparer.</returns>
    public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => _joinExpressionPlanComparer ??= new JoinExpressionPlanEqualityComparer(this);
    /// <summary>Returns the comparer used to compare sorting-expression plans, creating it on first use.</summary>
    /// <returns>The sorting-expression comparer.</returns>
    public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => _sortingExpressionPlanComparer ??= new SortingExpressionPlanEqualityComparer(this);

}
