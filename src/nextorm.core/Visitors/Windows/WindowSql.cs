using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Pure helpers for translating <see cref="WindowFunction{T}"/> calls: the function-name map,
/// parsing of the partition/order lambdas, the <c>Over</c>-overload argument mapping and the SQL
/// rendering of a window frame. Free of visitor state.
/// </summary>
internal static class WindowSql
{
    /// <summary>Maps the <see cref="CommonFunctions"/> method name to the SQL window function name.</summary>
    internal static string? MapWindowFunctionName(string methodName) => methodName switch
    {
        nameof(CommonFunctions.row_number) => "row_number",
        nameof(CommonFunctions.rank) => "rank",
        nameof(CommonFunctions.dense_rank) => "dense_rank",
        nameof(CommonFunctions.ntile) => "ntile",
        nameof(CommonFunctions.lag) => "lag",
        nameof(CommonFunctions.lead) => "lead",
        nameof(ClickHouseFunctions.lag_in_frame) => "lagInFrame",
        nameof(ClickHouseFunctions.lead_in_frame) => "leadInFrame",
        nameof(ClickHouseFunctions.non_negative_derivative) => "nonNegativeDerivative",
        nameof(ClickHouseFunctions.exponential_time_decayed_sum) => "exponentialTimeDecayedSum",
        nameof(ClickHouseFunctions.exponential_time_decayed_max) => "exponentialTimeDecayedMax",
        nameof(ClickHouseFunctions.exponential_time_decayed_count) => "exponentialTimeDecayedCount",
        nameof(ClickHouseFunctions.exponential_time_decayed_avg) => "exponentialTimeDecayedAvg",
        nameof(CommonFunctions.first_value) => "first_value",
        nameof(CommonFunctions.last_value) => "last_value",
        nameof(CommonFunctions.nth_value) => "nth_value",
        nameof(CommonFunctions.percent_rank) => "percent_rank",
        nameof(CommonFunctions.cume_dist) => "cume_dist",
        nameof(CommonFunctions.percentile_cont) => "percentile_cont",
        nameof(CommonFunctions.percentile_disc) => "percentile_disc",
        nameof(CommonFunctions.sum_over) => "sum",
        nameof(CommonFunctions.avg_over) => "avg",
        nameof(CommonFunctions.min_over) => "min",
        nameof(CommonFunctions.max_over) => "max",
        nameof(CommonFunctions.count_over) => "count",
        _ => null
    };

    /// <summary>
    /// Maps a window-function method name to the number of leading <em>parameters</em> rendered in a
    /// first parenthesis pair (<c>name(parameters)(arguments)</c>). Only the ClickHouse
    /// <c>exponentialTimeDecayed*</c> family takes one; every other window function has none.
    /// </summary>
    internal static int MapWindowParameterCount(string methodName) => methodName switch
    {
        nameof(ClickHouseFunctions.exponential_time_decayed_sum) => 1,
        nameof(ClickHouseFunctions.exponential_time_decayed_max) => 1,
        nameof(ClickHouseFunctions.exponential_time_decayed_count) => 1,
        nameof(ClickHouseFunctions.exponential_time_decayed_avg) => 1,
        _ => 0
    };

    /// <summary>
    /// True when <paramref name="name"/> is a simple ASCII SQL identifier. Window names are inlined
    /// verbatim into the <c>WINDOW</c> clause and the <c>OVER</c> reference, so anything else is
    /// rejected to keep the name from being a SQL-injection vector.
    /// </summary>
    internal static bool IsValidWindowName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var first = name[0];
        if (!((first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z') || first == '_'))
            return false;

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'))
                return false;
        }

        return true;
    }

    internal static bool HasUnkeyedSpecification(MethodCallExpression call)
    {
        var declaringType = call.Method.DeclaringType;
        if (call.Method.Name != nameof(WindowFunction<int>.Over)
            || declaringType is not { IsGenericType: true }
            || declaringType.GetGenericTypeDefinition() != typeof(WindowFunction<>))
            return false;

        foreach (var argument in call.Arguments)
        {
            if (!IsInlineSpecification(argument))
                return true;
        }

        return false;
    }

    private static bool IsInlineSpecification(Expression expression)
    {
        if (UnwrapWindowLambda(expression) is not null)
            return true;

        switch (expression)
        {
            case ConstantExpression { Value: null or string or int or WindowFrame or WindowFrameBound or WindowFrameExclusion }:
            case MemberExpression { Member.DeclaringType: var type, Expression: null }
                when type == typeof(WindowFrame) || type == typeof(WindowFrameBound):
                return true;
            case NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array:
                return array.Expressions.All(IsInlineSpecification);
            case MethodCallExpression call when IsOrderMarker(call):
                return call.Arguments.All(IsInlineSpecification);
            case MethodCallExpression call when call.Method.DeclaringType == typeof(WindowFrame)
                || call.Method.DeclaringType == typeof(WindowFrameBound):
                return call.Arguments.All(IsInlineSpecification) && (call.Object is null || IsInlineSpecification(call.Object));
            default:
                return false;
        }
    }

    internal static List<Expression> ParseWindowPartitions(BaseExpressionVisitor visitor, Expression? expression)
    {
        var result = new List<Expression>();
        if (expression is null or ConstantExpression { Value: null })
            return result;

        if (expression is NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array)
        {
            for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                AddWindowLambda(visitor, array.Expressions[i], result);
        }
        else if (expression.Type.IsArray)
        {
            if (EvaluateSpecification(visitor, expression) is Expression<Func<object?>>[] partitions)
                foreach (var partition in partitions)
                    if (partition is not null)
                        result.Add(partition.Body);
        }
        else
            AddWindowLambda(visitor, expression, result);

        return result;
    }

    private static void AddWindowLambda(BaseExpressionVisitor visitor, Expression expression, List<Expression> result)
    {
        if (ReadWindowLambda(visitor, expression) is { } body)
            result.Add(body);
    }

    internal static List<(Expression Body, OrderDirection Direction)> ParseWindowOrders(BaseExpressionVisitor visitor, Expression? expression)
    {
        var result = new List<(Expression, OrderDirection)>();
        if (expression is null or ConstantExpression { Value: null })
            return result;

        if (expression is NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array)
        {
            for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                AddWindowOrder(visitor, array.Expressions[i], result);
        }
        else if (expression.Type.IsArray)
        {
            if (EvaluateSpecification(visitor, expression) is WindowOrder[] orders)
                foreach (var order in orders)
                    if (order is not null)
                        result.Add((order.Expression.Body, order.Direction));
        }
        else
            AddWindowOrder(visitor, expression, result);

        return result;
    }

    private static bool IsOrderMarker(MethodCallExpression call)
        => call.Method.DeclaringType == typeof(CommonFunctions)
            && call.Method.Name is nameof(CommonFunctions.asc) or nameof(CommonFunctions.desc);

    private static void AddWindowOrder(BaseExpressionVisitor visitor, Expression expression, List<(Expression, OrderDirection)> result)
    {
        // A WindowOrder (SqlFunctions.Sql.asc/desc) carries an explicit direction; a bare lambda is ascending.
        if (expression is MethodCallExpression call && IsOrderMarker(call))
        {
            if (ReadWindowLambda(visitor, call.Arguments[0]) is { } body)
            {
                var direction = call.Method.Name == nameof(CommonFunctions.desc) ? OrderDirection.Desc : OrderDirection.Asc;
                result.Add((body, direction));
            }

            return;
        }

        if (expression.Type == typeof(WindowOrder))
        {
            if (EvaluateSpecification(visitor, expression) is WindowOrder order)
                result.Add((order.Expression.Body, order.Direction));
        }
        else if (ReadWindowLambda(visitor, expression) is { } plainBody)
            result.Add((plainBody, OrderDirection.Asc));
    }

    private static Expression? ReadWindowLambda(BaseExpressionVisitor visitor, Expression expression)
        => UnwrapWindowLambda(expression) ?? (EvaluateSpecification(visitor, expression) as LambdaExpression)?.Body;

    internal static object? EvaluateSpecification(BaseExpressionVisitor visitor, Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        if (expression.Has<ParameterExpression>())
            throw new NotSupportedException("A computed window specification must not depend on a query row.");

        return ValueExpressionAccessor.Evaluate(expression, visitor.QueryProvider, visitor.Logger);
    }

    /// <summary>
    /// Extracts the body of a partition/order lambda. An <c>Expression&lt;TDelegate&gt;</c> argument is
    /// stored in the tree as a <c>Quote</c>-wrapped lambda, so the quote has to be unwrapped first.
    /// </summary>
    private static Expression? UnwrapWindowLambda(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            expression = quote.Operand;

        return expression is LambdaExpression lambda ? lambda.Body : null;
    }

    /// <summary>
    /// Maps the <c>Over</c> overload arguments onto partition/order/frame slots. The overloads differ
    /// in which slots exist (partition-only, order-only, arrays, a named window), so this is driven by
    /// the parameter names of the resolved method rather than by position.
    /// </summary>
    internal static void SplitWindowArguments(
        BaseExpressionVisitor visitor,
        MethodCallExpression node,
        out Expression? partitionArgument,
        out Expression? orderArgument,
        out Expression? frameArgument,
        out string? namedWindow)
    {
        partitionArgument = null;
        orderArgument = null;
        frameArgument = null;
        namedWindow = null;

        var parameters = node.Method.GetParameters();
        var args = node.Arguments;
        var index = 0;

        if (parameters.Length > 0 && parameters[0].Name == "windowName")
        {
            namedWindow = EvaluateSpecification(visitor, args[0]) as string;
            if (!IsValidWindowName(namedWindow))
                throw new ArgumentException("A window name must be a non-empty SQL identifier.", nameof(node));

            return;
        }

        if (parameters.Length > 0 && parameters[0].Name == "partitionBy")
            partitionArgument = args[index++];

        if (index < parameters.Length && parameters[index].Name == "orderBy")
            orderArgument = args[index++];

        if (index < parameters.Length && parameters[index].ParameterType == typeof(WindowFrame))
            frameArgument = args[index];
    }

    internal static string RenderWindowFrame(WindowFrame frame, KeywordCase keywordCase)
    {
        var unit = frame.Type switch
        {
            WindowFrameType.Rows => "rows",
            WindowFrameType.Range => "range",
            WindowFrameType.Groups => "groups",
            _ => throw new NotSupportedException(frame.Type.ToString())
        };

        var result = $"{SqlKeywords.Of(keywordCase, unit)} {SqlKeywords.Of(keywordCase, "between")} {RenderWindowFrameBound(frame.Start, keywordCase)} {SqlKeywords.Of(keywordCase, "and")} {RenderWindowFrameBound(frame.End, keywordCase)}";

        if (frame.Exclusion is { } exclusion)
            result += " " + RenderWindowFrameExclusion(exclusion, keywordCase);

        return result;
    }

    internal static string RenderWindowFrameExclusion(WindowFrameExclusion exclusion, KeywordCase keywordCase) => exclusion switch
    {
        WindowFrameExclusion.NoOthers => SqlKeywords.Of(keywordCase, "exclude no others"),
        WindowFrameExclusion.CurrentRow => SqlKeywords.Of(keywordCase, "exclude current row"),
        WindowFrameExclusion.Group => SqlKeywords.Of(keywordCase, "exclude group"),
        WindowFrameExclusion.Ties => SqlKeywords.Of(keywordCase, "exclude ties"),
        _ => throw new NotSupportedException(exclusion.ToString())
    };

    private static string RenderWindowFrameBound(WindowFrameBound bound, KeywordCase keywordCase) => bound.Kind switch
    {
        WindowFrameBoundKind.UnboundedPreceding => SqlKeywords.Of(keywordCase, "unbounded preceding"),
        WindowFrameBoundKind.Preceding => $"{bound.Offset} {SqlKeywords.Of(keywordCase, "preceding")}",
        WindowFrameBoundKind.CurrentRow => SqlKeywords.Of(keywordCase, "current row"),
        WindowFrameBoundKind.Following => $"{bound.Offset} {SqlKeywords.Of(keywordCase, "following")}",
        WindowFrameBoundKind.UnboundedFollowing => SqlKeywords.Of(keywordCase, "unbounded following"),
        _ => throw new NotSupportedException(bound.Kind.ToString())
    };
}
