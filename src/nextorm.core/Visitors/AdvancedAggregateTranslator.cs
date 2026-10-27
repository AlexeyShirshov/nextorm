using System.Globalization;
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
        // The ClickHouse-only aggregate catalogue (T286b F16) is probed first: its members live on
        // ClickHouseFunctions, and the state-returning members must be refused on every provider with
        // their precise technical reason before the provider capability gate.
        if (node.Method.DeclaringType == typeof(ClickHouseFunctions))
        {
            if (ClickHouseAggregateRefusals.TryGetValue(node.Method.Name, out var refusal))
                throw new NotSupportedException(refusal);

            if (TryTranslateClickHouseAggregate(visitor, node))
                return true;
        }

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

            case nameof(PostgresFunctions.range_agg) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "range_agg", () => visitor.Dialect.SupportsRanges, "range_agg/range_intersect_agg");
                return true;
            case nameof(PostgresFunctions.range_intersect_agg) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "range_intersect_agg", () => visitor.Dialect.SupportsRanges, "range_agg/range_intersect_agg");
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
            case nameof(PostgresFunctions.median) when node.Arguments.Count == 1
                && node.Method.DeclaringType == typeof(PostgresFunctions):
                EmitOrderedMedian(visitor, node);
                return true;
            case nameof(PostgresFunctions.percentile) when node.Arguments.Count == 2
                && node.Method.DeclaringType == typeof(PostgresFunctions):
                throw new NotSupportedException("The percentile function is not supported by PostgreSQL.");
            case nameof(SqlServerFunctions.percentile) when node.Arguments.Count == 2
                && node.Method.DeclaringType == typeof(SqlServerFunctions):
                throw new NotSupportedException("The percentile function is not supported by SQL Server.");
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

            // SQL Server 2025 PRODUCT aggregate: aggregate context only, rendered through
            // MakeAggregate; no window/OVER expansion is offered. The version-bound SQL Server
            // renderer supplies the exact "requires major version 17" reason.
            case nameof(SqlServerFunctions.product) when node.Method.DeclaringType == typeof(SqlServerFunctions)
                && node.Arguments.Count == 1:
                EmitSimple(
                    visitor,
                    node,
                    "product",
                    () => visitor.Dialect.SupportsProductAggregate,
                    "product",
                    () => visitor.Dialect.SqlServerFunctions?.GetUnsupportedReason("product"));
                return true;
            case nameof(ClickHouseFunctions.any_last) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "any_last", () => visitor.Dialect.SupportsAnyAggregates, "anyLast");
                return true;

            case nameof(ClickHouseFunctions.window_funnel) when node.Arguments.Count == 3:
                EmitSequenceAggregate(visitor, "window_funnel", node.Method.ReturnType, [node.Arguments[0]], node.Arguments[1], node.Arguments[2]);
                return true;
            case nameof(ClickHouseFunctions.sequence_match) when node.Arguments.Count == 3:
                EmitSequenceAggregate(visitor, "sequence_match", node.Method.ReturnType, [node.Arguments[0]], node.Arguments[1], node.Arguments[2]);
                return true;
            case nameof(ClickHouseFunctions.retention) when node.Arguments.Count == 1:
                EmitSequenceAggregate(visitor, "retention", node.Method.ReturnType, [], timestamp: null, node.Arguments[0]);
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
            case nameof(ClickHouseFunctions.median) when node.Arguments.Count == 1
                && node.Method.DeclaringType == typeof(ClickHouseFunctions):
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
        string surface,
        Func<string?>? unsupportedReason = null)
    {
        if (!supported())
            throw new NotSupportedException(
                unsupportedReason?.Invoke()
                ?? $"The {surface} aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var call = $"{visitor.Dialect.MakeAggregate(name)}({visitor.VisitToString(node.Arguments[0])})";
        visitor.Builder!.Append(visitor.Dialect.MakeArrayResult(node.Method.Name, call, node.Method.ReturnType));
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

        var rendered = weighted
            ? topKAggregates.RenderWeighted(name, k, value, visitor.VisitToString(node.Arguments[2]))
            : topKAggregates.Render(name, k, value);

        visitor.Builder!.Append(visitor.Dialect.MakeArrayResult(node.Method.Name, rendered, node.Method.ReturnType));
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
        Type resultType,
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

        var call = sequenceAggregates.Render(name, renderedParameters, string.Join(", ", arguments));
        visitor.Builder!.Append(visitor.Dialect.MakeArrayResult(name, call, resultType));
    }

    /// <summary>
    /// Renders an ordered-set aggregate as
    /// <c>name(fraction) within group (order by key)</c> (<c>mode</c> omits the fraction).
    /// </summary>
    private static void EmitOrdered(BaseExpressionVisitor visitor, MethodCallExpression node, bool hasFraction)
    {
        // A dialect may express ordered-set aggregates without exposing the percentile members
        // (mode is not a percentile), so the percentile forms need both gates.
        if (!visitor.Dialect.SupportsOrderedAggregates
            || (hasFraction && !visitor.Dialect.SupportsOrderedPercentiles))
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

        if (fractionArg is not null)
            ValidateFraction(fractionArg, name, ProviderLabel(visitor.Dialect));

        visitor.NeedAliasForColumn = true;

        var aggregate = fractionArg is null
            ? $"{name}()"
            : $"{name}({visitor.VisitToString(fractionArg)})";

        var orderBy = UnwrapLambdaBody(orderArg);

        if (name == nameof(PostgresFunctions.percentile_cont))
            RequireFloatingContinuousSort(orderBy);

        visitor.Builder!.Append(visitor.Dialect.MakeWithinGroup(aggregate, visitor.VisitToString(orderBy), visitor.KeywordCase));
    }

    /// <summary>
    /// Renders the native median composition <c>percentile_cont(0.5) within group (order by key)</c>
    /// (PostgreSQL): the median is not a server function there, while the interpolated 0.5 percentile is.
    /// </summary>
    private static void EmitOrderedMedian(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (!visitor.Dialect.SupportsOrderedPercentiles)
            throw new NotSupportedException("The ordered-set aggregates (percentile_cont/percentile_disc/mode) are not supported by this provider.");

        var orderArg = node.Arguments[0];

        if (visitor.IsParamMode)
        {
            visitor.Visit(UnwrapLambdaBody(orderArg));
            return;
        }

        var orderBy = UnwrapLambdaBody(orderArg);
        RequireFloatingContinuousSort(orderBy);

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeWithinGroup(
            "percentile_cont(0.5)", visitor.VisitToString(orderBy), visitor.KeywordCase));
    }

    /// <summary>
    /// The continuous percentile interpolates, so PostgreSQL returns <c>double precision</c> and the
    /// library requires the sort expression to be floating-point. An integer/numeric sort would silently
    /// rely on the server's implicit cast, so it is refused with the provider's own restriction text.
    /// </summary>
    internal static void RequireFloatingContinuousSort(Expression orderBy)
    {
        var type = Nullable.GetUnderlyingType(orderBy.Type) ?? orderBy.Type;
        if (type != typeof(double) && type != typeof(float))
            throw new NotSupportedException(
                "The percentile_cont function on PostgreSQL requires a floating-point sort expression.");
    }

    /// <summary>
    /// Validates a percentile fraction at SQL-generation time. The providers disagree on an invalid
    /// fraction (an error on MariaDB/SQL Server, NULL on PostgreSQL, a conversion error on ClickHouse and
    /// nothing at all on SQLite), so an out-of-range or non-row-independent fraction is refused before it
    /// reaches the SQL text. Row-independence is required because the fraction is a direct argument of the
    /// aggregate, not a per-row input.
    /// </summary>
    internal static void ValidateFraction(Expression fraction, string functionName, string provider)
    {
        if (fraction.Has<ParameterExpression>())
            throw new NotSupportedException(
                $"The fraction for {functionName} on {provider} must be a constant or a captured scalar.");

        var value = ReadFraction(fraction, functionName, provider);
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(
                "fraction",
                value,
                $"The fraction for {functionName} on {provider} must be finite and between 0 and 1 inclusive.");
    }

    /// <summary>
    /// Reads a row-independent fraction expression: a literal, a captured closure field/property, or any
    /// other expression that does not reference the query parameter (a computed constant).
    /// </summary>
    private static double ReadFraction(Expression fraction, string functionName, string provider)
    {
        var expression = TypeFacts.UnwrapConvert(fraction);

        switch (expression)
        {
            case ConstantExpression { Value: { } literal }:
                return Convert.ToDouble(literal, CultureInfo.InvariantCulture);
            case MemberExpression { Expression: ConstantExpression closure } member:
                var captured = member.Member switch
                {
                    System.Reflection.FieldInfo field => field.GetValue(closure.Value),
                    System.Reflection.PropertyInfo property => property.GetValue(closure.Value),
                    _ => null
                };
                if (captured is not null)
                    return Convert.ToDouble(captured, CultureInfo.InvariantCulture);
                break;
        }

        try
        {
            return (double)Expression.Lambda(Expression.Convert(expression, typeof(double)))
                .Compile()
                .DynamicInvoke()!;
        }
        catch (Exception ex)
        {
            // A captured value that cannot be read is not a fraction this translator can render; the
            // refusal must name the provider rather than surface a raw reflection/compile error.
            throw new NotSupportedException(
                $"The fraction for {functionName} on {provider} must be a constant or a captured scalar.", ex);
        }
    }

    /// <summary>
    /// The provider label of a percentile message. Core cannot reference the provider assemblies, so the
    /// shipped dialect types are named here; an external dialect falls back to its own type name (the same
    /// convention the base dialect uses for its "cannot render" refusals).
    /// </summary>
    internal static string ProviderLabel(ISqlDialect dialect) => dialect.GetType().Name switch
    {
        "PostgresDialect" => "PostgreSQL",
        "SqlServerDialect" => "SQL Server",
        "MySqlDialect" => "MySQL",
        "MariaDbDialect" => "MariaDB",
        "SqliteDialect" => "SQLite",
        "ClickHouseDialect" => "ClickHouse",
        var other => other
    };


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

    /// <summary>
    /// True when the ClickHouse method is an aggregate that shares its name with a window function
    /// (<c>first_value</c>/<c>last_value</c>): the bare call is the aggregate form, while the same
    /// member completed with <c>Over(...)</c> stays the window form. The window guard in
    /// <see cref="NormSqlTranslator"/> must let the bare aggregate call through.
    /// </summary>
    internal static bool IsAggregateOverWindowName(MethodCallExpression node) =>
        node.Method.DeclaringType == typeof(ClickHouseFunctions)
        && node.Method.Name is nameof(ClickHouseFunctions.first_value) or nameof(ClickHouseFunctions.last_value);

    /// <summary>
    /// The state-returning ClickHouse aggregates refused by T286b F16: they return an opaque
    /// <c>AggregateFunction</c> state (a binary serialization with no CLR representation), so no typed
    /// projection can materialise them. The precise reason is raised on every provider, before the
    /// capability gate, and pinned by a negative test.
    /// </summary>
    private static readonly Dictionary<string, string> ClickHouseAggregateRefusals = new(StringComparer.Ordinal)
    {
        [nameof(ClickHouseFunctions.stochastic_linear_regression)] =
            "The stochastic_linear_regression aggregate returns an AggregateFunction state (an opaque binary " +
            "serialization) and has no typed CLR result; only the -Merge combinator yields weights, and it " +
            "requires an AggregateFunction-typed column the provider cannot read.",
        [nameof(ClickHouseFunctions.stochastic_logistic_regression)] =
            "The stochastic_logistic_regression aggregate returns an AggregateFunction state (an opaque binary " +
            "serialization) and has no typed CLR result; only the -Merge combinator yields weights, and it " +
            "requires an AggregateFunction-typed column the provider cannot read.",
        [nameof(ClickHouseFunctions.exponential_moving_average)] =
            "The exponential_moving_average aggregate returns an AggregateFunction state (an opaque binary " +
            "serialization) and has no typed CLR result; only the -Merge combinator yields the moving average, " +
            "and it requires an AggregateFunction-typed column the provider cannot read."
    };

    /// <summary>The ClickHouse-only aggregate members translated by <see cref="TryTranslateClickHouseAggregate"/>.</summary>
    private static readonly HashSet<string> ClickHouseAggregateNames = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.entropy), nameof(ClickHouseFunctions.first_value),
        nameof(ClickHouseFunctions.last_value), nameof(ClickHouseFunctions.max_intersections),
        nameof(ClickHouseFunctions.max_intersections_position),
        nameof(ClickHouseFunctions.simple_linear_regression),
        nameof(ClickHouseFunctions.delta_sum), nameof(ClickHouseFunctions.delta_sum_timestamp),
        nameof(ClickHouseFunctions.gini), nameof(ClickHouseFunctions.skew_pop),
        nameof(ClickHouseFunctions.skew_samp), nameof(ClickHouseFunctions.kurt_pop),
        nameof(ClickHouseFunctions.kurt_samp), nameof(ClickHouseFunctions.rank_corr),
        nameof(ClickHouseFunctions.contingency), nameof(ClickHouseFunctions.cramers_v),
        nameof(ClickHouseFunctions.cramers_v_bias_corrected), nameof(ClickHouseFunctions.theils_u),
        nameof(ClickHouseFunctions.welch_t_test), nameof(ClickHouseFunctions.student_t_test),
        nameof(ClickHouseFunctions.student_t_test_one_sample),
        nameof(ClickHouseFunctions.mann_whitney_u_test),
        nameof(ClickHouseFunctions.kolmogorov_smirnov_test),
        nameof(ClickHouseFunctions.mean_z_test), nameof(ClickHouseFunctions.analysis_of_variance),
        nameof(ClickHouseFunctions.quantile_tdigest), nameof(ClickHouseFunctions.quantile_gk),
        nameof(ClickHouseFunctions.quantile_dd), nameof(ClickHouseFunctions.quantile_bfloat16),
        nameof(ClickHouseFunctions.quantile_prometheus_histogram),
        nameof(ClickHouseFunctions.quantiles_exact), nameof(ClickHouseFunctions.quantiles_exact_low),
        nameof(ClickHouseFunctions.quantiles_exact_high),
        nameof(ClickHouseFunctions.quantiles_exact_weighted),
        nameof(ClickHouseFunctions.quantiles_timing),
        nameof(ClickHouseFunctions.quantiles_tdigest),
        nameof(ClickHouseFunctions.quantiles_bfloat16), nameof(ClickHouseFunctions.quantiles_dd),
        nameof(ClickHouseFunctions.quantiles_gk),
        nameof(ClickHouseFunctions.quantiles_prometheus_histogram),
        nameof(ClickHouseFunctions.approx_top_k), nameof(ClickHouseFunctions.approx_top_sum),
        nameof(ClickHouseFunctions.any_heavy), nameof(ClickHouseFunctions.max_map),
        nameof(ClickHouseFunctions.min_map), nameof(ClickHouseFunctions.sum_count),
        nameof(ClickHouseFunctions.sum_kahan),
        nameof(ClickHouseFunctions.group_array_moving_avg),
        nameof(ClickHouseFunctions.group_array_moving_sum),
        nameof(ClickHouseFunctions.group_array_sample),
        nameof(ClickHouseFunctions.group_array_sorted),
        nameof(ClickHouseFunctions.group_array_last),
        nameof(ClickHouseFunctions.group_array_insert_at),
        nameof(ClickHouseFunctions.group_array_intersect),
        nameof(ClickHouseFunctions.group_concat), nameof(ClickHouseFunctions.group_format),
        nameof(ClickHouseFunctions.histogram), nameof(ClickHouseFunctions.sparkbar),
        nameof(ClickHouseFunctions.uniq_up_to),
        nameof(ClickHouseFunctions.sequence_count),
        nameof(ClickHouseFunctions.sequence_match_events),
        nameof(ClickHouseFunctions.sequence_next_node),
        nameof(ClickHouseFunctions.time_series_group_sum),
        nameof(ClickHouseFunctions.time_series_group_rate_sum),
        nameof(ClickHouseFunctions.time_series_instant_rate_to_grid),
        nameof(ClickHouseFunctions.time_series_rate_to_grid),
        nameof(ClickHouseFunctions.time_series_resample_to_grid_with_staleness)
    };

    /// <summary>
    /// Translates a member of the ClickHouse-only aggregate catalogue through
    /// <see cref="ISqlDialect.ClickHouseAggregates"/>; returns <c>false</c> when the name is not one
    /// of them. Every other provider fails closed because the capability is <c>null</c> there.
    /// </summary>
    private static bool TryTranslateClickHouseAggregate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name;
        if (!ClickHouseAggregateNames.Contains(name))
            return false;

        if (visitor.Dialect.ClickHouseAggregates is not { } aggregates || !aggregates.Supports(name))
            throw new NotSupportedException($"The {name} aggregate is not supported by this provider.");

        if (name is nameof(ClickHouseFunctions.sequence_count)
            or nameof(ClickHouseFunctions.sequence_match_events))
        {
            EmitClickHouseSequence(visitor, node, name, aggregates);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.sequence_next_node))
        {
            EmitClickHouseSequenceNextNode(visitor, node, aggregates);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.group_format))
        {
            EmitClickHouseGroupFormat(visitor, node, aggregates);
            return true;
        }

        if (ClickHouseQuantilePluralShapes.TryGetValue(name, out var plural))
        {
            EmitClickHouseQuantiles(visitor, node, name, plural.LeadingParameters, plural.TrailingArguments, aggregates);
            return true;
        }

        var parameterCount = ClickHouseParameterCount(name, node.Arguments.Count);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return true;
        }

        visitor.NeedAliasForColumn = true;

        var parameters = new List<string>(parameterCount);
        var arguments = new List<string>(node.Arguments.Count - parameterCount);
        for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
        {
            // The parameter list of a parametric aggregate is type-inferred by the server, so an
            // integral double literal (1.0) must keep its decimal point: a bare 1 binds UInt8 and the
            // aggregate then fails inside the server with BAD_GET "has UInt64, requested Float64".
            var rendered = i < parameterCount
                ? RenderAggregateParameter(visitor, node.Arguments[i])
                : visitor.VisitToString(node.Arguments[i]);
            (i < parameterCount ? parameters : arguments).Add(rendered);
        }

        visitor.Builder!.Append(parameterCount == 0
            ? aggregates.Render(name, arguments, node.Method.ReturnType)
            : aggregates.RenderParameterised(name, parameters, arguments, node.Method.ReturnType));

        return true;
    }

    /// <summary>
    /// Maps a ClickHouse aggregate member and its CLR argument count to the number of leading
    /// parameters (the first parenthesis pair); every remaining argument belongs to the second pair.
    /// </summary>
    private static int ClickHouseParameterCount(string name, int argumentCount) => name switch
    {
        nameof(ClickHouseFunctions.mean_z_test) => 3,
        nameof(ClickHouseFunctions.quantile_tdigest) => 1,
        nameof(ClickHouseFunctions.quantile_gk) => 2,
        nameof(ClickHouseFunctions.quantile_dd) => 2,
        nameof(ClickHouseFunctions.quantile_bfloat16) => 1,
        nameof(ClickHouseFunctions.quantile_prometheus_histogram) => 1,
        nameof(ClickHouseFunctions.approx_top_k) => 1,
        nameof(ClickHouseFunctions.approx_top_sum) => 1,
        nameof(ClickHouseFunctions.group_array_sorted) => 1,
        nameof(ClickHouseFunctions.group_array_last) => 1,
        nameof(ClickHouseFunctions.group_array_sample) => argumentCount - 1,
        nameof(ClickHouseFunctions.group_array_insert_at) => argumentCount == 4 ? 2 : 0,
        nameof(ClickHouseFunctions.group_concat) => argumentCount - 1,
        nameof(ClickHouseFunctions.histogram) => 1,
        nameof(ClickHouseFunctions.sparkbar) => argumentCount == 5 ? 3 : 1,
        nameof(ClickHouseFunctions.uniq_up_to) => 1,
        nameof(ClickHouseFunctions.time_series_group_sum) => 0,
        nameof(ClickHouseFunctions.time_series_group_rate_sum) => 0,
        nameof(ClickHouseFunctions.time_series_instant_rate_to_grid) => 4,
        nameof(ClickHouseFunctions.time_series_rate_to_grid) => 4,
        nameof(ClickHouseFunctions.time_series_resample_to_grid_with_staleness) => 4,
        _ => 0
    };

    /// <summary>
    /// The plural quantile aggregates, whose levels are written as separate scalar parameters (the
    /// vendor rejects an array literal in the parameter list).
    /// </summary>
    private static readonly Dictionary<string, (int LeadingParameters, int TrailingArguments)> ClickHouseQuantilePluralShapes =
        new(StringComparer.Ordinal)
        {
            [nameof(ClickHouseFunctions.quantiles_exact)] = (0, 1),
            [nameof(ClickHouseFunctions.quantiles_exact_low)] = (0, 1),
            [nameof(ClickHouseFunctions.quantiles_exact_high)] = (0, 1),
            [nameof(ClickHouseFunctions.quantiles_exact_weighted)] = (0, 2),
            [nameof(ClickHouseFunctions.quantiles_timing)] = (0, 1),
            [nameof(ClickHouseFunctions.quantiles_tdigest)] = (0, 1),
            [nameof(ClickHouseFunctions.quantiles_bfloat16)] = (0, 1),
            [nameof(ClickHouseFunctions.quantiles_dd)] = (1, 1),
            [nameof(ClickHouseFunctions.quantiles_gk)] = (1, 1),
            [nameof(ClickHouseFunctions.quantiles_prometheus_histogram)] = (0, 2)
        };

    /// <summary>
    /// Renders an aggregate parameter, keeping the decimal point of a whole floating-point literal:
    /// the server infers the parameter type from the literal, and a bare <c>1</c> would bind
    /// <c>UInt8</c> where the vendor requires <c>Float64</c> (BAD_GET at execution).
    /// </summary>
    private static string RenderAggregateParameter(BaseExpressionVisitor visitor, Expression expression)
    {
        var bare = TypeFacts.UnwrapConvert(expression);

        if (bare is ConstantExpression { Value: double value })
            return WithDecimalPoint(value.ToString("R", CultureInfo.InvariantCulture));

        if (bare is ConstantExpression { Value: float single })
            return WithDecimalPoint(single.ToString("R", CultureInfo.InvariantCulture));

        return visitor.VisitToString(expression);
    }

    /// <summary>Appends <c>.0</c> to an integral floating-point literal.</summary>
    private static string WithDecimalPoint(string literal) =>
        literal.Contains('.') || literal.Contains('E') || literal.Contains('e')
            ? literal
            : literal + ".0";

    /// <summary>
    /// Renders a plural quantile aggregate: the leading parameters (the algorithm accuracy), then
    /// every level of the inline levels array as its own parameter, then the trailing aggregate
    /// arguments. A captured levels array is rejected exactly like the shipped <c>quantiles</c>.
    /// </summary>
    private static void EmitClickHouseQuantiles(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        string name,
        int leadingParameters,
        int trailingArguments,
        IClickHouseAggregateRenderer aggregates)
    {
        if (node.Arguments.Count != leadingParameters + 1 + trailingArguments)
            throw new NotSupportedException($"The {name} aggregate has an unexpected argument count.");

        if (node.Arguments[leadingParameters] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } levels)
            throw new NotSupportedException("The quantiles levels must be inline expressions, not a captured array.");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, leadingParameters); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            for (var (i, cnt) = (0, levels.Expressions.Count); i < cnt; i++)
                visitor.Visit(levels.Expressions[i]);

            for (var (i, cnt) = (leadingParameters + 1, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var parameters = new List<string>(leadingParameters + levels.Expressions.Count);
        for (var (i, cnt) = (0, leadingParameters); i < cnt; i++)
            parameters.Add(RenderAggregateParameter(visitor, node.Arguments[i]));

        for (var (i, cnt) = (0, levels.Expressions.Count); i < cnt; i++)
            parameters.Add(RenderAggregateParameter(visitor, levels.Expressions[i]));

        var arguments = new List<string>(trailingArguments);
        for (var (i, cnt) = (leadingParameters + 1, node.Arguments.Count); i < cnt; i++)
            arguments.Add(visitor.VisitToString(node.Arguments[i]));

        visitor.Builder!.Append(aggregates.RenderParameterised(name, parameters, arguments, node.Method.ReturnType));
    }

    /// <summary>
    /// Renders <c>sequenceCount(pattern)(timestamp, cond...)</c> or
    /// <c>sequenceMatchEvents(pattern)(timestamp, cond...)</c>; the conditions are an inline
    /// <c>new[]</c> of boolean expressions, so each element is rendered separately.
    /// </summary>
    private static void EmitClickHouseSequence(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        string name,
        IClickHouseAggregateRenderer aggregates)
    {
        if (node.Arguments.Count != 3
            || node.Arguments[2] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } conditions)
            throw new NotSupportedException($"The {name} conditions must be inline expressions, not a captured array.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            visitor.Visit(node.Arguments[1]);

            for (var (i, cnt) = (0, conditions.Expressions.Count); i < cnt; i++)
                visitor.Visit(conditions.Expressions[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var arguments = new List<string>(conditions.Expressions.Count + 1)
        {
            visitor.VisitToString(node.Arguments[1])
        };
        for (var (i, cnt) = (0, conditions.Expressions.Count); i < cnt; i++)
            arguments.Add(visitor.VisitToString(conditions.Expressions[i]));

        visitor.Builder!.Append(aggregates.RenderParameterised(
            name,
            [visitor.VisitToString(node.Arguments[0])],
            arguments,
            node.Method.ReturnType));
    }

    /// <summary>
    /// Renders <c>sequenceNextNode(direction, base)(timestamp, event_column, base_condition, event...)</c>.
    /// The event chain is an inline <c>new[]</c> of boolean expressions.
    /// </summary>
    private static void EmitClickHouseSequenceNextNode(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        IClickHouseAggregateRenderer aggregates)
    {
        if (node.Arguments.Count != 6
            || node.Arguments[5] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } conditions)
            throw new NotSupportedException(
                "The sequence_next_node events must be inline expressions, not a captured array.");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
            {
                if (i == 5)
                {
                    for (var (j, jcnt) = (0, conditions.Expressions.Count); j < jcnt; j++)
                        visitor.Visit(conditions.Expressions[j]);
                }
                else
                {
                    visitor.Visit(node.Arguments[i]);
                }
            }

            return;
        }

        visitor.NeedAliasForColumn = true;

        var arguments = new List<string>(conditions.Expressions.Count + 3);
        for (var i = 2; i < 5; i++)
            arguments.Add(visitor.VisitToString(node.Arguments[i]));

        for (var (i, cnt) = (0, conditions.Expressions.Count); i < cnt; i++)
            arguments.Add(visitor.VisitToString(conditions.Expressions[i]));

        visitor.Builder!.Append(aggregates.RenderParameterised(
            nameof(ClickHouseFunctions.sequence_next_node),
            [visitor.VisitToString(node.Arguments[0]), visitor.VisitToString(node.Arguments[1])],
            arguments,
            node.Method.ReturnType));
    }

    /// <summary>
    /// Renders <c>groupFormat(format)(x, y, ...)</c>; the params array is flattened into the
    /// aggregate argument list.
    /// </summary>
    private static void EmitClickHouseGroupFormat(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        IClickHouseAggregateRenderer aggregates)
    {
        if (node.Arguments.Count != 2
            || node.Arguments[1] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } values)
            throw new NotSupportedException("The group_format values must be inline expressions, not a captured array.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);

            for (var (i, cnt) = (0, values.Expressions.Count); i < cnt; i++)
                visitor.Visit(values.Expressions[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var arguments = new List<string>(values.Expressions.Count);
        for (var (i, cnt) = (0, values.Expressions.Count); i < cnt; i++)
            arguments.Add(visitor.VisitToString(TypeFacts.UnwrapConvert(values.Expressions[i])));

        visitor.Builder!.Append(aggregates.RenderParameterised(
            nameof(ClickHouseFunctions.group_format),
            [visitor.VisitToString(node.Arguments[0])],
            arguments,
            node.Method.ReturnType));
    }
}
