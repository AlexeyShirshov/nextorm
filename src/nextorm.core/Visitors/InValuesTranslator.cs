using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Renders value-list membership tests (<c>column in (v1, ...)</c>): a captured
/// <c>Enumerable.Contains</c>/instance <c>Contains</c>, or the value-list
/// <see cref="NORM.NORM_SQL.@in{T}(T, IEnumerable{T})"/> overload. Extracted from
/// <see cref="BaseExpressionVisitor"/>; the visitor walk, the emitted SQL and the parameter order are
/// unchanged.
/// </summary>
internal static class InValuesTranslator
{
    /// <summary>
    /// Translates a captured-collection membership test (<c>Enumerable.Contains</c> or an instance
    /// <c>Contains</c>) into the same <c>in (...)</c> predicate as the value-list
    /// <see cref="NORM.NORM_SQL.@in{T}(T, IEnumerable{T})"/> overload. Only collections that do not
    /// depend on the query parameter (captured locals/fields/constants) are translated.
    /// </summary>
    internal static bool TryTranslateCollectionContains(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (!InValues.TryGetArguments(node, out var columnExp, out var valuesExp, out var elementType))
            return false;

        TranslateInValues(visitor, columnExp, valuesExp, elementType);
        return true;
    }

    /// <summary>
    /// Renders a value-list membership test (<c>column in (v1, ...)</c>). Values become parameters so
    /// the query stays injection safe; an empty list becomes an always-false condition and a list that
    /// can contain null keeps the C# <c>Contains</c> semantics by adding an <c>is null</c> branch.
    /// </summary>
    internal static void TranslateInValues(BaseExpressionVisitor visitor, Expression columnExp, Expression valuesExp, Type elementType)
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

        if (visitor.IsParamMode)
        {
            visitor.Visit(columnExp);

            for (var i = 0; i < nonNull.Count; i++)
                visitor.Params.Add(new Param(visitor.ParamProvider.GetParamName(), nonNull[i]));

            return;
        }

        var column = visitor.VisitToString(columnExp);

        if (nonNull.Count == 0)
        {
            var emptyPredicate = nullableAware && hasNull ? $"{column} is null" : "1 = 0";
            visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(emptyPredicate, visitor.IsPredicateContext));
            return;
        }

        var inBuilder = visitor.BuilderPool.Get();
        try
        {
            if (nullableAware && hasNull)
                inBuilder.Append('(');

            inBuilder.Append(column).Append(" in (");
            for (var i = 0; i < nonNull.Count; i++)
            {
                var paramName = visitor.ParamProvider.GetParamName();
                visitor.Params.Add(new Param(paramName, nonNull[i]));

                if (i > 0)
                    inBuilder.Append(", ");
                inBuilder.Append(visitor.Dialect.MakeParam(paramName));
            }
            inBuilder.Append(')');

            if (nullableAware && hasNull)
                inBuilder.Append(" or ").Append(column).Append(" is null)");

            visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(inBuilder.ToString(), visitor.IsPredicateContext));
        }
        finally
        {
            visitor.BuilderPool.Return(inBuilder);
        }
    }

}
