namespace NextORM.Core;

public partial class QueryCommand
{

    /// <summary>
    /// Re-evaluates the value-list shape of an already-prepared command. The implementation lives in
    /// <see cref="QueryPreparer"/>, next to the rest of the preparation pipeline.
    /// </summary>
    internal void RefreshInValuesShape() => QueryPreparer.RefreshInValuesShape(this);

    // The plan-cache key of a prepared command is structural and expensive to build (a deep walk of
    // the expression tree). A command that is executed repeatedly (Any/Count/All share one command per
    // context, and a caller can reuse a command explicitly) would otherwise rebuild the key and
    // re-compare it structurally on every execution. The key is therefore memoized here and reused
    // while the command is prepared and its re-evaluated value-list shape is unchanged; it is dropped
    // when the command is prepared/reset. Reusing the same key instance lets the plan store match it
    // by reference (see QueryPlan.Equals), so a warm repeated execution does no structural comparison.
    private QueryPlan? _planKey;
    private string? _planKeySql;
    private int _planKeyInValuesShapeHash;
    private int _planKeyPreWhereShapeHash;

    /// <summary>
    /// Returns this command's plan-cache key, reusing the memoized one when the command is prepared
    /// and its value-list shape has not changed since the key was built.
    /// </summary>
    /// <param name="sql">The raw-SQL override of the command, or <see langword="null"/>.</param>
    /// <returns>The plan key to look up or store.</returns>
    internal QueryPlan GetOrCreatePlanKey(string? sql)
    {
        if (_planKey is { } plan
            && _planKeySql == sql
            && _planKeyInValuesShapeHash == InValuesShapeHash
            && _planKeyPreWhereShapeHash == PreWhereShapeHash)
            return plan;

        var created = new QueryPlan(this, sql);
        _planKey = created;
        _planKeySql = sql;
        _planKeyInValuesShapeHash = InValuesShapeHash;
        _planKeyPreWhereShapeHash = PreWhereShapeHash;
        return created;
    }

    /// <summary>
    /// Memoizes the plan key instance that is actually stored in the plan cache (returned by a cache
    /// hit), so the next lookup of this command matches it by reference instead of structurally.
    /// </summary>
    /// <param name="plan">The stored plan key.</param>
    /// <param name="sql">The raw-SQL override the key was looked up with, or <see langword="null"/>.</param>
    internal void CacheStoredPlanKey(QueryPlan plan, string? sql)
    {
        _planKey = plan;
        _planKeySql = sql;
        _planKeyInValuesShapeHash = InValuesShapeHash;
        _planKeyPreWhereShapeHash = PreWhereShapeHash;
    }

    /// <summary>
    /// Drops the memoized plan-cache key. Called when the command is prepared again or reset, so a
    /// key built for the previous query shape can never be reused.
    /// </summary>
    internal void InvalidatePlanKey()
    {
        _planKey = null;
        _planKeySql = null;
    }

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
