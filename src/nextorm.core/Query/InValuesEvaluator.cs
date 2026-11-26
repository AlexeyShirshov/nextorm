using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Evaluates a value-list or lookup collection. A cached accessor reads the expression's constants
/// from the current invocation; neither its key nor its compiled body embeds those constants.
/// </summary>
internal static class InValuesEvaluator
{
    public static object? Evaluate(Expression valuesExp, IQueryRegistry queryProvider)
        => ValueExpressionAccessor.EvaluateCollection(valuesExp, queryProvider);
}
