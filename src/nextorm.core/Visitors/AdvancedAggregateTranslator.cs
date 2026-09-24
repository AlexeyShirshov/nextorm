using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the less common aggregate families written through <see cref="CommonFunctions"/>: the
/// boolean aggregates (<c>bool_and</c>/<c>bool_or</c>/<c>every</c>), the bitwise aggregates
/// (<c>bit_and</c>/<c>bit_or</c>/<c>bit_xor</c>), the statistical aggregates (<c>corr</c>,
/// <c>covar_*</c>, <c>regr_*</c>) and the ordered-set aggregates
/// (<c>percentile_cont</c>/<c>percentile_disc</c>/<c>mode</c> with
/// <c>WITHIN GROUP (ORDER BY ...)</c>) and the ClickHouse parameterised quantile aggregates
/// (<c>quantile(level)(value)</c>/<c>quantiles(...)(value)</c>/<c>median</c>) and the ClickHouse
/// <c>topK</c>/<c>topKWeighted</c> aggregates and the ClickHouse array-returning aggregates
/// <c>groupArray</c>/<c>groupUniqArray</c>.
/// <para>
/// Each family is guarded by its own dialect capability
/// (<see cref="ISqlDialect.SupportsBooleanAggregates"/>,
/// <see cref="ISqlDialect.SupportsBitAggregates"/>,
/// <see cref="ISqlDialect.SupportsStatisticalAggregates"/>,
/// <see cref="ISqlDialect.SupportsOrderedAggregates"/>) so a provider that cannot express the
/// aggregate fails with a clear message instead of emitting invalid SQL.
/// </para>
/// </summary>
internal static class AdvancedAggregateTranslator
{
    /// <summary>Translates an advanced aggregate call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case nameof(PostgresFunctions.bool_and) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bool_and", () => visitor.Dialect.SupportsBooleanAggregates, "bool_and/bool_or/every");
                return true;
            case nameof(PostgresFunctions.bool_or) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bool_or", () => visitor.Dialect.SupportsBooleanAggregates, "bool_and/bool_or/every");
                return true;
            case nameof(PostgresFunctions.every) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "every", () => visitor.Dialect.SupportsBooleanAggregates, "bool_and/bool_or/every");
                return true;

            case nameof(PostgresFunctions.bit_and) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bit_and", () => visitor.Dialect.SupportsBitAggregates, "bit_and/bit_or/bit_xor");
                return true;
            case nameof(PostgresFunctions.bit_or) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bit_or", () => visitor.Dialect.SupportsBitAggregates, "bit_and/bit_or/bit_xor");
                return true;
            case nameof(PostgresFunctions.bit_xor) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bit_xor", () => visitor.Dialect.SupportsBitAggregates, "bit_and/bit_or/bit_xor");
                return true;

            case nameof(CommonFunctions.corr) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "corr", () => visitor.Dialect.SupportsStatisticalAggregates, "statistical (corr/covar)");
                return true;
            case nameof(CommonFunctions.covar_pop) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "covar_pop", () => visitor.Dialect.SupportsStatisticalAggregates, "statistical (corr/covar)");
                return true;
            case nameof(CommonFunctions.covar_samp) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "covar_samp", () => visitor.Dialect.SupportsStatisticalAggregates, "statistical (corr/covar)");
                return true;
            case nameof(PostgresFunctions.regr_slope) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_slope", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(PostgresFunctions.regr_intercept) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_intercept", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(PostgresFunctions.regr_r2) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_r2", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(PostgresFunctions.regr_count) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_count", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(PostgresFunctions.regr_avgx) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_avgx", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(PostgresFunctions.regr_avgy) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_avgy", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;

            case nameof(ClickHouseFunctions.arg_min) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "arg_min", () => visitor.Dialect.SupportsArgMinMax, "argMin/argMax");
                return true;
            case nameof(ClickHouseFunctions.arg_max) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "arg_max", () => visitor.Dialect.SupportsArgMinMax, "argMin/argMax");
                return true;

            case nameof(PostgresFunctions.percentile_cont) when node.Arguments.Count == 2
                && node.Method.DeclaringType == typeof(PostgresFunctions):
                EmitOrdered(visitor, node, hasFraction: true);
                return true;
            case nameof(PostgresFunctions.percentile_disc) when node.Arguments.Count == 2
                && node.Method.DeclaringType == typeof(PostgresFunctions):
                EmitOrdered(visitor, node, hasFraction: true);
                return true;
            case nameof(PostgresFunctions.mode) when node.Arguments.Count == 1:
                EmitOrdered(visitor, node, hasFraction: false);
                return true;

            case nameof(ClickHouseFunctions.uniq) when node.Arguments.Count == 1:
                EmitUniq(visitor, node, "uniq");
                return true;
            case nameof(ClickHouseFunctions.uniq_exact) when node.Arguments.Count == 1:
                EmitUniq(visitor, node, "uniq_exact");
                return true;
            case nameof(ClickHouseFunctions.uniq_combined) when node.Arguments.Count == 1:
                EmitUniq(visitor, node, "uniq_combined");
                return true;
            case nameof(ClickHouseFunctions.uniq_hll12) when node.Arguments.Count == 1:
                EmitUniq(visitor, node, "uniq_hll12");
                return true;

            case nameof(CommonFunctions.any_agg) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "any_agg", () => visitor.Dialect.SupportsAnyValueAggregate, "ANY_VALUE/any");
                return true;
            case nameof(ClickHouseFunctions.any_last) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "any_last", () => visitor.Dialect.SupportsAnyAggregates, "anyLast");
                return true;

            case nameof(ClickHouseFunctions.window_funnel) when node.Arguments.Count == 3:
                EmitSequenceAggregate(visitor, "window_funnel", [node.Arguments[0]], node.Arguments[1], node.Arguments[2]);
                return true;
            case nameof(ClickHouseFunctions.sequence_match) when node.Arguments.Count == 3:
                EmitSequenceAggregate(visitor, "sequence_match", [node.Arguments[0]], node.Arguments[1], node.Arguments[2]);
                return true;
            case nameof(ClickHouseFunctions.retention) when node.Arguments.Count == 1:
                EmitSequenceAggregate(visitor, "retention", [], timestamp: null, node.Arguments[0]);
                return true;

            case nameof(ClickHouseFunctions.group_array) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "group_array", () => visitor.Dialect.SupportsArrayFunctions, "groupArray/groupUniqArray");
                return true;
            case nameof(ClickHouseFunctions.group_uniq_array) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "group_uniq_array", () => visitor.Dialect.SupportsArrayFunctions, "groupArray/groupUniqArray");
                return true;

            case nameof(ClickHouseFunctions.quantile) when node.Arguments.Count == 2:
                EmitQuantile(visitor, node, "quantile");
                return true;
            case nameof(ClickHouseFunctions.quantile_exact) when node.Arguments.Count == 2:
                EmitQuantile(visitor, node, "quantile_exact");
                return true;
            case nameof(ClickHouseFunctions.quantile_timing) when node.Arguments.Count == 2:
                EmitQuantile(visitor, node, "quantile_timing");
                return true;
            case nameof(ClickHouseFunctions.quantiles) when node.Arguments.Count == 2:
                EmitQuantiles(visitor, node, "quantiles");
                return true;
            case nameof(ClickHouseFunctions.top_k) when node.Arguments.Count == 2:
                EmitTopK(visitor, node, "top_k", weighted: false);
                return true;
            case nameof(ClickHouseFunctions.top_k_weighted) when node.Arguments.Count == 3:
                EmitTopK(visitor, node, "top_k_weighted", weighted: true);
                return true;
            case nameof(ClickHouseFunctions.median) when node.Arguments.Count == 1:
                EmitMedian(visitor, node);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Renders a single-argument aggregate gated by a dialect capability flag.</summary>
    private static void EmitSimple(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        string name,
        Func<bool> supported,
        string surface)
    {
        if (!supported())
            throw new NotSupportedException($"The {surface} aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeAggregate(name)).Append('(').Append(visitor.VisitToString(node.Arguments[0])).Append(')');
    }

    /// <summary>
    /// Renders a distinct-count aggregate (<c>uniq</c>/<c>uniqExact</c>/<c>uniqCombined</c>/<c>uniqHLL12</c>)
    /// through <see cref="IUniqAggregateRenderer.Render"/>, which lets a dialect wrap the native
    /// unsigned result in a signed cast.
    /// </summary>
    private static void EmitUniq(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (visitor.Dialect.UniqAggregates is not { } uniqAggregates)
            throw new NotSupportedException("The uniq/uniqExact/uniqCombined/uniqHLL12 aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(uniqAggregates.Render(name, visitor.VisitToString(node.Arguments[0])));
    }

    /// <summary>
    /// Renders a parameterised quantile aggregate (<c>quantile(level)(value)</c>) through
    /// <see cref="IQuantileAggregateRenderer.Render"/>, gated by <see cref="ISqlDialect.QuantileAggregates"/>.
    /// </summary>
    private static void EmitQuantile(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (visitor.Dialect.QuantileAggregates is not { } quantileAggregates)
            throw new NotSupportedException("The quantile/median aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            visitor.Visit(node.Arguments[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(quantileAggregates.Render(
            name,
            visitor.VisitToString(node.Arguments[0]),
            visitor.VisitToString(node.Arguments[1])));
    }

    /// <summary>
    /// Renders the <c>median</c> aggregate through <see cref="IQuantileAggregateRenderer.RenderMedian"/>, gated by
    /// <see cref="ISqlDialect.QuantileAggregates"/>.
    /// </summary>
    private static void EmitMedian(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (visitor.Dialect.QuantileAggregates is not { } quantileAggregates)
            throw new NotSupportedException("The quantile/median aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(quantileAggregates.RenderMedian(visitor.VisitToString(node.Arguments[0])));
    }

    /// <summary>
    /// Renders the multi-level quantile aggregate (<c>quantiles(level1, level2, ...)(value)</c>) through
    /// <see cref="IQuantileAggregateRenderer.RenderLevels"/>, gated by
    /// <see cref="ISqlDialect.QuantileAggregates"/>. The levels must be an inline array; a captured array
    /// is rejected (there is no parameter form for the aggregate's parameter list).
    /// </summary>
    private static void EmitQuantiles(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (visitor.Dialect.QuantileAggregates is not { } quantileAggregates)
            throw new NotSupportedException("The quantile/median aggregates are not supported by this provider.");

        if (node.Arguments[0] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } levels)
            throw new NotSupportedException("The quantiles levels must be inline expressions, not a captured array.");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, levels.Expressions.Count); i < cnt; i++)
                visitor.Visit(levels.Expressions[i]);

            visitor.Visit(node.Arguments[1]);
            return;
        }

        var parts = new List<string>(levels.Expressions.Count);
        for (var (i, cnt) = (0, levels.Expressions.Count); i < cnt; i++)
            parts.Add(visitor.VisitToString(levels.Expressions[i]));

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(quantileAggregates.RenderLevels(
            name,
            string.Join(", ", parts),
            visitor.VisitToString(node.Arguments[1])));
    }

    /// <summary>
    /// Renders the ClickHouse parameterised top-K aggregates
    /// (<c>topK(k)(value)</c>, <c>topKWeighted(k)(value, weight)</c>) through
    /// <see cref="ITopKAggregateRenderer"/>, gated by <see cref="ISqlDialect.TopKAggregates"/>.
    /// </summary>
    private static void EmitTopK(BaseExpressionVisitor visitor, MethodCallExpression node, string name, bool weighted)
    {
        if (visitor.Dialect.TopKAggregates is not { } topKAggregates)
            throw new NotSupportedException("The topK/topKWeighted aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            visitor.Visit(node.Arguments[1]);

            if (weighted)
                visitor.Visit(node.Arguments[2]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var k = visitor.VisitToString(node.Arguments[0]);
        var value = visitor.VisitToString(node.Arguments[1]);

        visitor.Builder!.Append(weighted
            ? topKAggregates.RenderWeighted(name, k, value, visitor.VisitToString(node.Arguments[2]))
            : topKAggregates.Render(name, k, value));
    }

    /// <summary>Renders a two-argument aggregate (for example <c>corr(y, x)</c>) gated by a capability flag.</summary>
    private static void EmitBinary(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        string name,
        Func<bool> supported,
        string surface)
    {
        if (!supported())
            throw new NotSupportedException($"The {surface} aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            visitor.Visit(node.Arguments[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeAggregate(name)).Append('(')
            .Append(visitor.VisitToString(node.Arguments[0])).Append(", ")
            .Append(visitor.VisitToString(node.Arguments[1])).Append(')');
    }

    /// <summary>
    /// Renders a ClickHouse sequence/funnel aggregate
    /// (<c>windowFunnel(window)(timestamp, conds...)</c>, <c>sequenceMatch(pattern)(timestamp, conds...)</c>,
    /// <c>retention(conds...)</c>) through <see cref="ISequenceAggregateRenderer.Render"/>, gated by
    /// <see cref="ISqlDialect.SequenceAggregates"/>. The conditions are an inline
    /// <c>new[]</c> of boolean expressions, so each element is rendered separately.
    /// </summary>
    private static void EmitSequenceAggregate(
        BaseExpressionVisitor visitor,
        string name,
        IReadOnlyList<Expression> parameters,
        Expression? timestamp,
        Expression conditions)
    {
        if (visitor.Dialect.SequenceAggregates is not { } sequenceAggregates)
            throw new NotSupportedException("The windowFunnel/retention/sequenceMatch aggregates are not supported by this provider.");

        if (conditions is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } conditionArray)
            throw new NotSupportedException("The windowFunnel/retention/sequenceMatch conditions must be inline expressions, not a captured array.");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, parameters.Count); i < cnt; i++)
                visitor.Visit(parameters[i]);

            if (timestamp is not null)
                visitor.Visit(timestamp);

            for (var (i, cnt) = (0, conditionArray.Expressions.Count); i < cnt; i++)
                visitor.Visit(conditionArray.Expressions[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        string? renderedParameters = null;
        if (parameters.Count > 0)
        {
            var parts = new List<string>(parameters.Count);
            for (var (i, cnt) = (0, parameters.Count); i < cnt; i++)
                parts.Add(visitor.VisitToString(parameters[i]));

            renderedParameters = string.Join(", ", parts);
        }

        var arguments = new List<string>(conditionArray.Expressions.Count + 1);
        if (timestamp is not null)
            arguments.Add(visitor.VisitToString(timestamp));

        for (var (i, cnt) = (0, conditionArray.Expressions.Count); i < cnt; i++)
            arguments.Add(visitor.VisitToString(conditionArray.Expressions[i]));

        visitor.Builder!.Append(sequenceAggregates.Render(name, renderedParameters, string.Join(", ", arguments)));
    }

    /// <summary>
    /// Renders an ordered-set aggregate as
    /// <c>name(fraction) within group (order by key)</c> (<c>mode</c> omits the fraction).
    /// </summary>
    private static void EmitOrdered(BaseExpressionVisitor visitor, MethodCallExpression node, bool hasFraction)
    {
        if (!visitor.Dialect.SupportsOrderedAggregates)
            throw new NotSupportedException("The ordered-set aggregates (percentile_cont/percentile_disc/mode) are not supported by this provider.");

        var name = node.Method.Name;
        var fractionArg = hasFraction ? node.Arguments[0] : null;
        var orderArg = node.Arguments[hasFraction ? 1 : 0];

        if (visitor.IsParamMode)
        {
            if (fractionArg is not null) visitor.Visit(fractionArg);
            visitor.Visit(UnwrapLambdaBody(orderArg));
            return;
        }

        visitor.NeedAliasForColumn = true;

        var aggregate = fractionArg is null
            ? $"{name}()"
            : $"{name}({visitor.VisitToString(fractionArg)})";

        var orderBy = visitor.VisitToString(UnwrapLambdaBody(orderArg));

        visitor.Builder!.Append(visitor.Dialect.MakeWithinGroup(aggregate, orderBy, visitor.KeywordCase));
    }

    /// <summary>
    /// Extracts the body of a quoted ordering lambda
    /// (<c>Expression&lt;Func&lt;T&gt;&gt;</c>). An expression argument is stored as a quote-wrapped
    /// lambda; the lambda closes over the outer query parameter, so its body is rendered with this
    /// visitor's own context.
    /// </summary>
    private static Expression UnwrapLambdaBody(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            expression = quote.Operand;

        return expression is LambdaExpression lambda ? lambda.Body : expression;
    }
}
