using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Translates the less common aggregate families written through <see cref="NORM.NORM_SQL"/>: the
/// boolean aggregates (<c>bool_and</c>/<c>bool_or</c>/<c>every</c>), the bitwise aggregates
/// (<c>bit_and</c>/<c>bit_or</c>/<c>bit_xor</c>), the statistical aggregates (<c>corr</c>,
/// <c>covar_*</c>, <c>regr_*</c>) and the ordered-set aggregates
/// (<c>percentile_cont</c>/<c>percentile_disc</c>/<c>mode</c> with
/// <c>WITHIN GROUP (ORDER BY ...)</c>).
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
            case nameof(NORM.NORM_SQL.bool_and) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bool_and", () => visitor.Dialect.SupportsBooleanAggregates, "bool_and/bool_or/every");
                return true;
            case nameof(NORM.NORM_SQL.bool_or) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bool_or", () => visitor.Dialect.SupportsBooleanAggregates, "bool_and/bool_or/every");
                return true;
            case nameof(NORM.NORM_SQL.every) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "every", () => visitor.Dialect.SupportsBooleanAggregates, "bool_and/bool_or/every");
                return true;

            case nameof(NORM.NORM_SQL.bit_and) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bit_and", () => visitor.Dialect.SupportsBitAggregates, "bit_and/bit_or/bit_xor");
                return true;
            case nameof(NORM.NORM_SQL.bit_or) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bit_or", () => visitor.Dialect.SupportsBitAggregates, "bit_and/bit_or/bit_xor");
                return true;
            case nameof(NORM.NORM_SQL.bit_xor) when node.Arguments.Count == 1:
                EmitSimple(visitor, node, "bit_xor", () => visitor.Dialect.SupportsBitAggregates, "bit_and/bit_or/bit_xor");
                return true;

            case nameof(NORM.NORM_SQL.corr) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "corr", () => visitor.Dialect.SupportsStatisticalAggregates, "statistical (corr/covar)");
                return true;
            case nameof(NORM.NORM_SQL.covar_pop) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "covar_pop", () => visitor.Dialect.SupportsStatisticalAggregates, "statistical (corr/covar)");
                return true;
            case nameof(NORM.NORM_SQL.covar_samp) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "covar_samp", () => visitor.Dialect.SupportsStatisticalAggregates, "statistical (corr/covar)");
                return true;
            case nameof(NORM.NORM_SQL.regr_slope) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_slope", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(NORM.NORM_SQL.regr_intercept) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_intercept", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(NORM.NORM_SQL.regr_r2) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_r2", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(NORM.NORM_SQL.regr_count) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_count", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(NORM.NORM_SQL.regr_avgx) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_avgx", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;
            case nameof(NORM.NORM_SQL.regr_avgy) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "regr_avgy", () => visitor.Dialect.SupportsRegressionAggregates, "regr_*");
                return true;

            case nameof(NORM.NORM_SQL.arg_min) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "arg_min", () => visitor.Dialect.SupportsArgMinMax, "argMin/argMax");
                return true;
            case nameof(NORM.NORM_SQL.arg_max) when node.Arguments.Count == 2:
                EmitBinary(visitor, node, "arg_max", () => visitor.Dialect.SupportsArgMinMax, "argMin/argMax");
                return true;

            case nameof(NORM.NORM_SQL.percentile_cont) when node.Arguments.Count == 2:
                EmitOrdered(visitor, node, hasFraction: true);
                return true;
            case nameof(NORM.NORM_SQL.percentile_disc) when node.Arguments.Count == 2:
                EmitOrdered(visitor, node, hasFraction: true);
                return true;
            case nameof(NORM.NORM_SQL.mode) when node.Arguments.Count == 1:
                EmitOrdered(visitor, node, hasFraction: false);
                return true;

            case nameof(NORM.NORM_SQL.count_if) when node.Arguments.Count == 1:
                EmitIfAggregate(visitor, node, "count_if", valueArgument: null);
                return true;
            case nameof(NORM.NORM_SQL.sum_if) when node.Arguments.Count == 2:
                EmitIfAggregate(visitor, node, "sum_if", node.Arguments[0]);
                return true;
            case nameof(NORM.NORM_SQL.avg_if) when node.Arguments.Count == 2:
                EmitIfAggregate(visitor, node, "avg_if", node.Arguments[0]);
                return true;
            case nameof(NORM.NORM_SQL.min_if) when node.Arguments.Count == 2:
                EmitIfAggregate(visitor, node, "min_if", node.Arguments[0]);
                return true;
            case nameof(NORM.NORM_SQL.max_if) when node.Arguments.Count == 2:
                EmitIfAggregate(visitor, node, "max_if", node.Arguments[0]);
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
    /// Renders a ClickHouse-style filtered aggregate combinator,
    /// <c>countIf(predicate)</c>/<c>sumIf(value, predicate)</c>/<c>avgIf(...)</c>/<c>minIf(...)</c>/<c>maxIf(...)</c>.
    /// The aggregate name is mapped by <see cref="ISqlDialect.MakeAggregate"/>; the predicate is
    /// rendered without the ANSI <c>filter (where ...)</c> wrapper.
    /// </summary>
    private static void EmitIfAggregate(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        string name,
        Expression? valueArgument)
    {
        if (!visitor.Dialect.SupportsIfAggregates)
            throw new NotSupportedException("The countIf/sumIf/avgIf/minIf/maxIf aggregates are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            if (valueArgument is not null)
                visitor.Visit(valueArgument);

            AggregateFilter.AppendPredicate(visitor, node.Arguments[^1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeAggregate(name)).Append('(');

        if (valueArgument is not null)
            visitor.Builder!.Append(visitor.VisitToString(valueArgument)).Append(", ");

        AggregateFilter.AppendPredicate(visitor, node.Arguments[^1]);
        visitor.Builder!.Append(')');
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

        visitor.Builder!.Append(visitor.Dialect.MakeWithinGroup(aggregate, orderBy));
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
