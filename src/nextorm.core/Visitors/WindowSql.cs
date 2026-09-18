using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Pure helpers for translating <see cref="NORM.WindowFunction{T}"/> calls: the function-name map,
/// parsing of the partition/order lambdas, the <c>Over</c>-overload argument mapping and the SQL
/// rendering of a window frame. Free of visitor state.
/// </summary>
internal static class WindowSql
{
    /// <summary>Maps the <see cref="NORM.NORM_SQL"/> method name to the SQL window function name.</summary>
    internal static string? MapWindowFunctionName(string methodName) => methodName switch
    {
        nameof(NORM.NORM_SQL.row_number) => "row_number",
        nameof(NORM.NORM_SQL.rank) => "rank",
        nameof(NORM.NORM_SQL.dense_rank) => "dense_rank",
        nameof(NORM.NORM_SQL.ntile) => "ntile",
        nameof(NORM.NORM_SQL.lag) => "lag",
        nameof(NORM.NORM_SQL.lead) => "lead",
        nameof(NORM.NORM_SQL.first_value) => "first_value",
        nameof(NORM.NORM_SQL.last_value) => "last_value",
        nameof(NORM.NORM_SQL.sum_over) => "sum",
        nameof(NORM.NORM_SQL.avg_over) => "avg",
        nameof(NORM.NORM_SQL.min_over) => "min",
        nameof(NORM.NORM_SQL.max_over) => "max",
        nameof(NORM.NORM_SQL.count_over) => "count",
        _ => null
    };

    internal static List<Expression> ParseWindowPartitions(Expression? expression)
    {
        var result = new List<Expression>();
        if (expression is null or ConstantExpression { Value: null })
            return result;

        if (expression is NewArrayExpression array)
        {
            for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                AddWindowLambda(array.Expressions[i], result);
        }
        else
            AddWindowLambda(expression, result);

        return result;
    }

    private static void AddWindowLambda(Expression expression, List<Expression> result)
    {
        if (UnwrapWindowLambda(expression) is { } body)
            result.Add(body);
    }

    internal static List<(Expression Body, OrderDirection Direction)> ParseWindowOrders(Expression? expression)
    {
        var result = new List<(Expression, OrderDirection)>();
        if (expression is null or ConstantExpression { Value: null })
            return result;

        if (expression is NewArrayExpression array)
        {
            for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                AddWindowOrder(array.Expressions[i], result);
        }
        else
            AddWindowOrder(expression, result);

        return result;
    }

    private static void AddWindowOrder(Expression expression, List<(Expression, OrderDirection)> result)
    {
        // A WindowOrder (NORM.SQL.asc/desc) carries an explicit direction; a bare lambda is ascending.
        if (expression is MethodCallExpression call
            && call.Method.DeclaringType == typeof(NORM.NORM_SQL)
            && call.Method.Name is nameof(NORM.NORM_SQL.asc) or nameof(NORM.NORM_SQL.desc))
        {
            if (UnwrapWindowLambda(call.Arguments[0]) is { } body)
            {
                var direction = call.Method.Name == nameof(NORM.NORM_SQL.desc) ? OrderDirection.Desc : OrderDirection.Asc;
                result.Add((body, direction));
            }

            return;
        }

        if (UnwrapWindowLambda(expression) is { } plainBody)
            result.Add((plainBody, OrderDirection.Asc));
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
    /// in which slots exist (partition-only, order-only, arrays), so this is driven by the parameter
    /// names of the resolved method rather than by position.
    /// </summary>
    internal static void SplitWindowArguments(
        MethodCallExpression node,
        out Expression? partitionArgument,
        out Expression? orderArgument,
        out Expression? frameArgument)
    {
        partitionArgument = null;
        orderArgument = null;
        frameArgument = null;

        var parameters = node.Method.GetParameters();
        var args = node.Arguments;
        var index = 0;

        if (parameters.Length > 0 && parameters[0].Name == "partitionBy")
            partitionArgument = args[index++];

        if (index < parameters.Length && parameters[index].Name == "orderBy")
            orderArgument = args[index++];

        if (index < parameters.Length && parameters[index].ParameterType == typeof(NORM.WindowFrame))
            frameArgument = args[index];
    }

    internal static string RenderWindowFrame(NORM.WindowFrame frame)
    {
        var unit = frame.Type == NORM.WindowFrameType.Rows ? "rows" : "range";
        return $"{unit} between {RenderWindowFrameBound(frame.Start)} and {RenderWindowFrameBound(frame.End)}";
    }

    private static string RenderWindowFrameBound(NORM.WindowFrameBound bound) => bound.Kind switch
    {
        NORM.WindowFrameBoundKind.UnboundedPreceding => "unbounded preceding",
        NORM.WindowFrameBoundKind.Preceding => $"{bound.Offset} preceding",
        NORM.WindowFrameBoundKind.CurrentRow => "current row",
        NORM.WindowFrameBoundKind.Following => $"{bound.Offset} following",
        NORM.WindowFrameBoundKind.UnboundedFollowing => "unbounded following",
        _ => throw new NotSupportedException(bound.Kind.ToString())
    };
}
