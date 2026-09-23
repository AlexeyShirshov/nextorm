using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Renders value-list membership tests (<c>column in (v1, ...)</c>): a captured
/// <c>Enumerable.Contains</c>/instance <c>Contains</c>, or the value-list
/// <see cref="CommonFunctions.@in{T}(T, IEnumerable{T})"/> overload. Extracted from
/// <see cref="BaseExpressionVisitor"/>; the visitor walk, the emitted SQL and the parameter order are
/// unchanged.
/// </summary>
internal static class InValuesTranslator
{
    /// <summary>
    /// Translates a captured-collection membership test (<c>Enumerable.Contains</c> or an instance
    /// <c>Contains</c>) into the same <c>in (...)</c> predicate as the value-list
    /// <see cref="CommonFunctions.@in{T}(T, IEnumerable{T})"/> overload. Only collections that do not
    /// depend on the query parameter (captured locals/fields/constants) are translated.
    /// </summary>
    internal static bool TryTranslateCollectionContains(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (!InValues.TryGetArguments(node, out var columnExp, out var valuesExp, out var elementType, out var isGlobal)
            || isGlobal)
            return false;

        TranslateInValues(visitor, columnExp, valuesExp, elementType);
        return true;
    }

    /// <summary>
    /// Renders a value-list membership test (<c>column in (v1, ...)</c>). Values become parameters so
    /// the query stays injection safe; an empty list becomes an always-false condition and a list that
    /// can contain null keeps the C# <c>Contains</c> semantics by adding an <c>is null</c> branch.
    /// <paramref name="global"/> renders the ClickHouse distributed <c>GLOBAL IN</c> form instead of
    /// the plain <c>IN</c>.
    /// </summary>
    internal static void TranslateInValues(BaseExpressionVisitor visitor, Expression columnExp, Expression valuesExp, Type elementType, bool global = false)
    {
        // A captured collection is re-read on every execution and the number of parameters (and so
        // the SQL text) depends on its length. When the shape was folded into the plan key while
        // preparing the condition, the evaluated partition is reused here and the plan stays cacheable.
        // In every other context (join/having/select, or a query that is not plan-cached) the shape is
        // not part of any key, so caching must stay disabled.
        var command = visitor.QueryProvider as QueryCommand;
        InValuesPartition partition;
        if (command?.InValuesPartitions is { } partitions && partitions.TryGetValue(valuesExp, out var cachedPartition))
        {
            partition = cachedPartition;
        }
        else
        {
            if (valuesExp is not NewArrayExpression && command is not null)
                command.Cache = false;

            partition = InValues.EvaluatePartition(valuesExp, visitor.QueryProvider);
        }

        var nonNull = partition.NonNull;
        var hasNull = partition.HasNull;
        var nullableAware = !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) is not null;

        // An inline list (new[] { ... } of constants) is part of the expression shape, so its values
        // are fixed for a cached plan and do not have to be re-extracted on every execution. A captured
        // collection is not: the plan key only sees its shape, not its current contents.
        var stableValues = InValues.IsStableValueExpression(valuesExp);

        if (visitor.IsParamMode)
        {
            visitor.Visit(columnExp);

            for (var i = 0; i < nonNull.Count; i++)
                visitor.Params.Add(new Parameter(visitor.ParameterProvider.GetParamName(), nonNull[i]) { Stable = stableValues });

            return;
        }

        var column = visitor.VisitToString(columnExp);

        if (nonNull.Count == 0)
        {
            var emptyPredicate = nullableAware && hasNull ? $"{column} {visitor.Kw("is null")}" : "1 = 0";
            visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(emptyPredicate, visitor.IsPredicateContext));
            return;
        }

        var inBuilder = visitor.BuilderPool.Get();
        try
        {
            if (nullableAware && hasNull)
                inBuilder.Append('(');

            inBuilder.Append(column).Append(global ? visitor.Kw(" global in (") : visitor.Kw(" in ("));
            for (var i = 0; i < nonNull.Count; i++)
            {
                var paramName = visitor.ParameterProvider.GetParamName();
                visitor.Params.Add(new Parameter(paramName, nonNull[i]) { Stable = stableValues });

                if (i > 0)
                    inBuilder.Append(", ");
                inBuilder.Append(visitor.Dialect.MakeParam(paramName));
            }
            inBuilder.Append(')');

            if (nullableAware && hasNull)
                inBuilder.Append(visitor.Kw(" or ")).Append(column).Append(visitor.Kw(" is null)"));

            visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(inBuilder.ToString(), visitor.IsPredicateContext));
        }
        finally
        {
            visitor.BuilderPool.Return(inBuilder);
        }
    }

}
