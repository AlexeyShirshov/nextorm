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
        if (TypeFacts.IsTupleFamily(elementType))
        {
            TranslateTupleInValues(visitor, columnExp, valuesExp, elementType, global);
            return;
        }

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
            // The sticky Cache flag is deliberately NOT set here: mutating a shared command (the
            // context-wide Any/Count command) would disable the plan cache for every later query. A
            // scalar list in a clause whose shape is not folded into the plan key is instead flagged by
            // the preparation scan (HasUnkeyedScalarInValues) and suppressed call-locally by the planner.
            partition = InValues.EvaluatePartition(valuesExp, visitor.QueryProvider);
        }

        var nonNull = partition.NonNull;
        var hasNull = partition.HasNull;
        var nullableAware = !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) is not null;
        var converter = MemberTranslator.ResolveConverter(visitor, columnExp)?.Converter;

        // An inline list (new[] { ... } of constants) is part of the expression shape, so its values
        // are fixed for a cached plan and do not have to be re-extracted on every execution. A captured
        // collection is not: the plan key only sees its shape, not its current contents.
        var stableValues = InValues.IsStableValueExpression(valuesExp);

        if (visitor.IsParamMode)
        {
            visitor.Visit(columnExp);

            for (var i = 0; i < nonNull.Count; i++)
                visitor.Params.Add(new Parameter(visitor.ParameterProvider.GetParamName(), BaseExpressionVisitor.ConvertToProviderValue(converter, nonNull[i])) { Stable = stableValues });

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
                visitor.Params.Add(new Parameter(paramName, BaseExpressionVisitor.ConvertToProviderValue(converter, nonNull[i])) { Stable = stableValues });

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

    /// <summary>
    /// Renders a tuple-valued value-list membership test. The translated predicate is two-valued (no SQL
    /// <c>UNKNOWN</c>), so the C# <c>Contains</c> semantics — including null components and negation —
    /// hold: a null RHS cell becomes <c>LHS IS NULL</c>, a non-null cell becomes
    /// <c>LHS IS NOT NULL AND LHS = @p</c>, and the rows are OR-ed. An all-non-null row group uses the
    /// provider's native row <c>IN</c> list, guarded by <c>IS NOT NULL</c> on every nullable LHS component;
    /// rows that contain a null use explicit OR-of-AND component arms. An empty list is <c>1 = 0</c>. SQL
    /// Server has no row-value form and rejects the predicate before any empty-list folding.
    /// </summary>
    private static void TranslateTupleInValues(BaseExpressionVisitor visitor, Expression columnExp, Expression valuesExp, Type elementType, bool global)
    {
        // SQL Server exposes no ITupleRenderer, so the whole tuple surface is unsupported there. The
        // rejection is deliberately first: it must fire before the empty-list fold below.
        if (visitor.Dialect.Tuple is not { } tupleRenderer)
            throw new NotSupportedException(InValues.SqlServerTupleInNotSupportedMessage);

        // A nullable value tuple is routed here (it is a tuple family), but it has no null-safe row shape.
        // Reject it with the shared pinned message before the arity/LHS checks, matching PartitionTuple.
        if (Nullable.GetUnderlyingType(elementType) is not null)
            throw new NotSupportedException(InValues.NullableTupleElementNotSupportedMessage);

        if (!TypeFacts.IsTupleLike(elementType))
            throw new NotSupportedException(
                $"Tuple IN/Contains supports flat System.Tuple/System.ValueTuple collections of arity 1..7; '{elementType.Name}' is not supported.");

        if (!TupleSqlTranslator.TryGetConstructorArguments(columnExp, out var components)
            || components.Count != TypeFacts.TupleArity(elementType))
            throw new NotSupportedException(
                "Tuple IN/Contains requires an inline System.Tuple/System.ValueTuple constructor on the left-hand side with the same arity as the collection element.");

        var command = visitor.QueryProvider as QueryCommand;
        InValuesPartition partition;
        if (command?.InValuesPartitions is { } partitions && partitions.TryGetValue(valuesExp, out var cachedPartition))
            partition = cachedPartition;
        else
            partition = InValues.EvaluatePartition(valuesExp, visitor.QueryProvider, elementType);

        if (partition.Tuple is not { } tuplePartition)
            throw new NotSupportedException("The tuple IN/Contains collection could not be evaluated.");

        var arity = tuplePartition.Arity;
        var converters = new IPropertyValueConverter?[arity];
        for (var i = 0; i < arity; i++)
            converters[i] = MemberTranslator.ResolveConverter(visitor, components[i])?.Converter;

        var stableValues = InValues.IsStableValueExpression(valuesExp);

        if (visitor.IsParamMode)
        {
            // Walk the LHS so captured values in its components are collected in the same order as the
            // SQL pass, then register the RHS cells in the canonical arm order.
            for (var i = 0; i < arity; i++)
                visitor.Visit(components[i]);

            AddTupleParameters(visitor, tuplePartition, converters, stableValues);
            return;
        }

        var lhs = new string[arity];
        for (var i = 0; i < arity; i++)
            lhs[i] = visitor.VisitToString(components[i]);

        var arms = new List<string>();

        // Non-null rows: one native row-IN list, guarded on every nullable LHS component so a null LHS
        // cell cannot make the predicate UNKNOWN (which would break negation).
        var rhsRows = new List<string>();
        foreach (var row in tuplePartition.Rows)
        {
            if (RowHasNull(row))
                continue;

            var cells = new string[arity];
            for (var i = 0; i < arity; i++)
                cells[i] = AddTupleParameter(visitor, row[i], converters[i], stableValues);

            rhsRows.Add(tupleRenderer.RenderConstructor(cells));
        }

        if (rhsRows.Count > 0)
        {
            var inClause = global ? visitor.Kw(" global in ") : visitor.Kw(" in ");
            var arm = tupleRenderer.RenderConstructor(lhs) + inClause + tupleRenderer.RenderInValues(rhsRows);

            List<string>? guards = null;
            for (var i = 0; i < arity; i++)
            {
                if (IsNullableComponent(components[i].Type))
                    (guards ??= []).Add(lhs[i] + visitor.Kw(" is not null"));
            }

            arms.Add(guards is { Count: > 0 }
                ? "(" + string.Join(visitor.Kw(" and "), guards) + visitor.Kw(" and ") + arm + ")"
                : "(" + arm + ")");
        }

        // Rows with a null component: explicit OR-of-AND arms with the two-valued cell predicate.
        foreach (var row in tuplePartition.Rows)
        {
            if (!RowHasNull(row))
                continue;

            var parts = new string[arity];
            for (var i = 0; i < arity; i++)
            {
                if (row[i] is null)
                {
                    parts[i] = lhs[i] + visitor.Kw(" is null");
                }
                else
                {
                    var param = AddTupleParameter(visitor, row[i], converters[i], stableValues);
                    parts[i] = lhs[i] + visitor.Kw(" is not null") + visitor.Kw(" and ") + lhs[i] + " = " + param;
                }
            }

            arms.Add("(" + string.Join(visitor.Kw(" and "), parts) + ")");
        }

        var predicate = arms.Count switch
        {
            0 => "1 = 0",
            1 => arms[0],
            _ => "(" + string.Join(visitor.Kw(" or "), arms) + ")",
        };

        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(predicate, visitor.IsPredicateContext));
    }

    // Non-null rows first, then rows containing a null, in source order; within a row, component order,
    // skipping null cells (which have no parameter). This mirrors the render arm order exactly so the
    // parameter-only pass and the SQL pass bind the same positions.
    private static void AddTupleParameters(BaseExpressionVisitor visitor, InValuesTuplePartition partition, IPropertyValueConverter?[] converters, bool stable)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            for (var r = 0; r < partition.Rows.Count; r++)
            {
                var row = partition.Rows[r];
                if ((pass == 0) == RowHasNull(row))
                    continue;

                for (var i = 0; i < row.Length; i++)
                {
                    if (row[i] is not null)
                        AddTupleParameter(visitor, row[i], converters[i], stable);
                }
            }
        }
    }

    private static string AddTupleParameter(BaseExpressionVisitor visitor, object? value, IPropertyValueConverter? converter, bool stable)
    {
        var paramName = visitor.ParameterProvider.GetParamName();
        visitor.Params.Add(new Parameter(paramName, BaseExpressionVisitor.ConvertToProviderValue(converter, value)) { Stable = stable });
        return visitor.Dialect.MakeParam(paramName);
    }

    private static bool RowHasNull(object?[] row)
    {
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] is null)
                return true;
        }

        return false;
    }

    private static bool IsNullableComponent(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

}
