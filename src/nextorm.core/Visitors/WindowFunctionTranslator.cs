using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates <see cref="WindowFunction{T}"/> calls into
/// <c>func(...) over (partition by ... order by ... frame)</c>. Extracted from
/// <see cref="BaseExpressionVisitor"/>; the visitor walk, the emitted SQL and therefore the
/// parameter numbering are unchanged. The function-name map, the partition/order parsing and the
/// frame rendering live in <see cref="WindowSql"/>.
/// </summary>
internal static class WindowFunctionTranslator
{
    /// <summary>
    /// Translates a window function into <c>func(...) over (partition by ... order by ... frame)</c>.
    /// The function is modelled as a call to the <c>Over</c> method on <see cref="WindowFunction{T}"/> whose
    /// target is the <see cref="CommonFunctions"/> function call (<c>row_number</c>, <c>lag</c>, <c>sum_over</c>,
    /// ...); the partition/order keys are nested lambdas that close over the outer query parameter, so
    /// they are rendered with this visitor's own context (the same <see cref="BaseExpressionVisitor.Clone"/>-free path the
    /// rest of the select list uses).
    /// <para>
    /// Limitations: <c>SELECT DISTINCT</c>, <c>GROUP BY</c> and correlated-references contexts are not
    /// adjusted for window functions. They are emitted as written (which the provider may reject)
    /// rather than silently rewritten into different semantics.
    /// </para>
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;
        if (declaringType is null
            || !declaringType.IsGenericType
            || declaringType.GetGenericTypeDefinition() != typeof(WindowFunction<>))
            return false;

        if (node.Object is not MethodCallExpression functionCall
            || (functionCall.Method.DeclaringType != typeof(CommonFunctions)
                && functionCall.Method.DeclaringType != typeof(ClickHouseFunctions))
            || WindowSql.MapWindowFunctionName(functionCall.Method.Name) is not { } functionName)
            return false;

        // The Over overloads differ in which parameters are present (partition-only, order-only, arrays,
        // a named window, frame), so the arguments are mapped by parameter name/type rather than by position.
        WindowSql.SplitWindowArguments(node, out var partitionArgument, out var orderArgument, out var frameArgument, out var namedWindow);

        var partitions = WindowSql.ParseWindowPartitions(partitionArgument);
        var orders = WindowSql.ParseWindowOrders(orderArgument);

        if (namedWindow is not null)
        {
            if (!WindowSql.IsValidWindowName(namedWindow))
                throw new ArgumentException("A window name must be a non-empty SQL identifier.", nameof(node));

            if (!visitor.Dialect.SupportsNamedWindows)
                throw new NotSupportedException("Named windows (the WINDOW clause) are not supported by this provider.");
        }

        if (functionName is "lagInFrame" or "leadInFrame" && !visitor.Dialect.SupportsInFrameWindowFunctions)
            throw new NotSupportedException("The lagInFrame/leadInFrame window functions are not supported by this provider.");

        if (functionName is "percent_rank" or "cume_dist" && !visitor.Dialect.SupportsPercentRankCumeDist)
            throw new NotSupportedException("The percent_rank/cume_dist window functions are not supported by this provider.");

        if (functionName == "nth_value" && !visitor.Dialect.SupportsNthValue)
            throw new NotSupportedException("The nth_value window function is not supported by this provider.");

        var isPercentile = functionName is "percentile_cont" or "percentile_disc";
        if (isPercentile && !visitor.Dialect.SupportsPercentileWindow)
            throw new NotSupportedException("The percentile_cont/percentile_disc window functions are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            // The parameter-extraction pass emits no SQL, but it still has to walk every embedded
            // expression so captured constants become parameters in the same order as the SQL pass.
            // A named-window reference carries no specification of its own (it is declared on the
            // query, where the WINDOW clause walks it).
            for (var (i, cnt) = (0, functionCall.Arguments.Count); i < cnt; i++)
                visitor.Visit(functionCall.Arguments[i]);

            for (var (i, cnt) = (0, partitions.Count); i < cnt; i++)
                visitor.Visit(partitions[i]);

            for (var (i, cnt) = (0, orders.Count); i < cnt; i++)
                visitor.Visit(orders[i].Body);

            return true;
        }

        var frame = EvaluateWindowFrame(visitor, frameArgument);

        if (frame is not null)
        {
            if (frame.Type == WindowFrameType.Groups && !visitor.Dialect.SupportsWindowFrameGroups)
                throw new NotSupportedException("The GROUPS window frame unit is not supported by this provider.");

            if (frame.Exclusion is not null && !visitor.Dialect.SupportsWindowFrameExclusion)
                throw new NotSupportedException("The EXCLUDE window frame clause is not supported by this provider.");
        }

        var named = namedWindow is not null;

        visitor.NeedAliasForColumn = true;
        var windowStart = visitor.Builder!.Length;

        // The aggregate functions keep their dialect mapping (e.g. stdev -> stddev) and count() keeps
        // its dialect spelling (the opening parenthesis is part of MakeCount); the ranking/value
        // functions are ANSI and shared by every provider.
        var functionArgs = functionCall.Arguments;
        if (isPercentile)
        {
            visitor.Builder!.Append(functionName).Append('(');
            visitor.Visit(functionArgs[0]);
            visitor.Builder!.Append(") within group (order by ");
            visitor.Visit(functionArgs[1]);
            visitor.Builder!.Append(named ? ") over " : ") over (");
        }
        else
        {
            if (functionName == "count")
            {
                visitor.Builder!.Append(visitor.Dialect.MakeCount(false, false));
            }
            else
            {
                visitor.Builder!.Append(functionName is "sum" or "avg" or "min" or "max"
                    ? visitor.Dialect.MakeAggregate(functionName)
                    : functionName);
                visitor.Builder!.Append('(');
            }

            if (functionName == "count" && functionArgs.Count == 0)
            {
                visitor.Builder!.Append('*');
            }
            else
            {
                for (var (i, cnt) = (0, functionArgs.Count); i < cnt; i++)
                {
                    if (i > 0) visitor.Builder!.Append(", ");
                    visitor.Visit(functionArgs[i]);
                }
            }

            visitor.Builder!.Append(named ? ") over " : ") over (");
        }

        if (named)
        {
            visitor.Builder!.Append(namedWindow);
        }
        else
        {
            if (partitions.Count > 0)
            {
                visitor.Builder!.Append("partition by ");
                for (var (i, cnt) = (0, partitions.Count); i < cnt; i++)
                {
                    if (i > 0) visitor.Builder!.Append(", ");
                    visitor.Visit(partitions[i]);
                }
            }

            if (orders.Count > 0)
            {
                if (partitions.Count > 0) visitor.Builder!.Append(' ');

                visitor.Builder!.Append("order by ");
                for (var (i, cnt) = (0, orders.Count); i < cnt; i++)
                {
                    if (i > 0) visitor.Builder!.Append(", ");
                    visitor.Visit(orders[i].Body);
                    if (orders[i].Direction == OrderDirection.Desc)
                        visitor.Builder!.Append(" desc");
                }
            }

            if (frame is not null)
            {
                if (partitions.Count > 0 || orders.Count > 0) visitor.Builder!.Append(' ');
                visitor.Builder!.Append(WindowSql.RenderWindowFrame(frame));
            }

            visitor.Builder!.Append(')');
        }

        if (functionName == "count" && visitor.Dialect.WrapsCountResult)
        {
            var rendered = visitor.Builder!.ToString(windowStart, visitor.Builder!.Length - windowStart);
            visitor.Builder!.Length = windowStart;
            visitor.Builder!.Append(visitor.Dialect.WrapCount(rendered, big: false));
        }

        return true;
    }

    /// <summary>
    /// Evaluates the <see cref="WindowFrame"/> argument. A frame is constant with respect to the
    /// query (it carries only boundaries), so its expression is compiled and cached rather than
    /// rendered as SQL.
    /// </summary>
    private static WindowFrame? EvaluateWindowFrame(BaseExpressionVisitor visitor, Expression? expression)
    {
        if (expression is null or ConstantExpression { Value: null })
            return null;

        if (expression is ConstantExpression { Value: WindowFrame constant })
            return constant;

        var key = new ExpressionKey(expression, visitor.QueryProvider);
        if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
        {
            del = Expression.Lambda<Func<WindowFrame>>(expression).Compile();
            DataContextCache.ExpressionsCache[key] = del;
        }

        return ((Func<WindowFrame>)del)();
    }
}
