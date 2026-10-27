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

        if (node.Object is not MethodCallExpression functionCall)
            return false;

        // The residual percentile members live on a provider surface rather than on CommonFunctions and
        // are analytic-only: the median is the native composition percentile_cont(0.5), and the generic
        // percentile is refused. The refusal must fire before any fraction inspection, and it names the
        // actual dialect because one CLR member serves both MySQL (no percentile at all) and MariaDB
        // (native percentile_cont/percentile_disc window forms).
        var functionDeclaringType = functionCall.Method.DeclaringType;
        var provider = AdvancedAggregateTranslator.ProviderLabel(visitor.Dialect);
        var isResidualMember = functionDeclaringType == typeof(SqlServerFunctions)
            || functionDeclaringType == typeof(MySqlFunctions);

        var isResidualMedian = isResidualMember
            && functionCall.Method.Name == nameof(SqlServerFunctions.median);
        var isResidualPercentile = isResidualMember
            && functionCall.Method.Name == nameof(SqlServerFunctions.percentile);

        if (isResidualPercentile)
            throw new NotSupportedException($"The percentile function is not supported by {provider}.");

        // The median has no spelling of its own on MySQL, so the capability default (false) is reported
        // with the median's name rather than as a generic percentile refusal.
        if (isResidualMedian && !visitor.Dialect.SupportsPercentileWindow)
            throw new NotSupportedException($"The median function is not supported by {provider}.");

        string functionName;
        if (isResidualMedian)
        {
            functionName = "percentile_cont";
        }
        else if ((functionCall.Method.DeclaringType == typeof(CommonFunctions)
                || functionCall.Method.DeclaringType == typeof(ClickHouseFunctions))
            && WindowSql.MapWindowFunctionName(functionCall.Method.Name) is { } mapped)
        {
            functionName = mapped;
        }
        else
        {
            return false;
        }

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

        // nonNegativeDerivative is a ClickHouse window-only aggregate. The provider gate reuses the
        // dialect's name-keyed scalar table (the ClickHouse table carries the name) so no new
        // ISqlDialect member is added; every other provider fails closed before emitting invalid SQL.
        if (functionName == "nonNegativeDerivative"
            && (visitor.Dialect.ScalarFunctions is not { } windowScalars
                || !windowScalars.Supports(nameof(ClickHouseFunctions.non_negative_derivative))))
            throw new NotSupportedException("The nonNegativeDerivative window function is not supported by this provider.");

        // The exponentialTimeDecayed* aggregates are ClickHouse window-only functions; the capability
        // object's presence is the provider gate (no other dialect carries it).
        var parameterCount = WindowSql.MapWindowParameterCount(functionCall.Method.Name);
        if (parameterCount > 0
            && (visitor.Dialect.ClickHouseAggregates is not { } decayedAggregates
                || !decayedAggregates.Supports(functionCall.Method.Name)))
            throw new NotSupportedException(
                $"The {functionName} window function is not supported by this provider.");

        if (functionName is "percent_rank" or "cume_dist" && !visitor.Dialect.SupportsPercentRankCumeDist)
            throw new NotSupportedException("The percent_rank/cume_dist window functions are not supported by this provider.");

        if (functionName == "nth_value" && !visitor.Dialect.SupportsNthValue)
            throw new NotSupportedException("The nth_value window function is not supported by this provider.");

        var isPercentile = isResidualMedian || functionName is "percentile_cont" or "percentile_disc";
        if (isPercentile && !visitor.Dialect.SupportsPercentileWindow)
            throw new NotSupportedException($"The {functionName} function is not supported by {provider}.");

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

        // The median composition takes its ordering from the value argument, so a window ORDER BY or a
        // frame would silently change the statistic instead of the window shape; refuse it.
        if (isResidualMedian && (orders.Count > 0 || frame is not null))
            throw new NotSupportedException(
                "The median window function supports a partition-only OVER clause; a window ORDER BY or a frame is not supported.");

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
        var windowMark = visitor.Recorder?.MarkAt(windowStart);

        // The aggregate functions keep their dialect mapping (e.g. stdev -> stddev) and count() keeps
        // its dialect spelling (the opening parenthesis is part of MakeCount); the ranking/value
        // functions are ANSI and shared by every provider.
        var functionArgs = functionCall.Arguments;
        string? averageArgument = null;
        var overStart = -1;
        if (isPercentile)
        {
            visitor.Builder!.Append(functionName).Append('(');

            if (isResidualMedian)
            {
                // median == percentile_cont(0.5); the fraction is fixed, so there is no user input to
                // validate and no literal interpolation of a caller value.
                visitor.Builder!.Append("0.5");
            }
            else
            {
                AdvancedAggregateTranslator.ValidateFraction(
                    functionArgs[0],
                    functionName,
                    AdvancedAggregateTranslator.ProviderLabel(visitor.Dialect));
                visitor.Visit(functionArgs[0]);
            }

            visitor.Builder!.Append(visitor.Kw(") within group (order by "));
            visitor.Visit(functionArgs[isResidualMedian ? 0 : 1]);
            visitor.Builder!.Append(named ? visitor.Kw(") over ") : visitor.Kw(") over ("));
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

                // A parameterised window aggregate renders the vendor's two-parenthesis grammar
                // name(parameters)(arguments): the leading ClickHouse exponentialTimeDecayed* parameter
                // (the decay) is the only such family.
                if (parameterCount > 0)
                {
                    visitor.Builder!.Append('(');
                    for (var (i, cnt) = (0, parameterCount); i < cnt; i++)
                    {
                        if (i > 0) visitor.Builder!.Append(", ");
                        visitor.Visit(functionArgs[i]);
                    }

                    visitor.Builder!.Append(')');
                }

                visitor.Builder!.Append('(');
            }

            if (functionName == "count" && functionArgs.Count == 0)
            {
                visitor.Builder!.Append('*');
            }
            else
            {
                for (var (i, cnt) = (parameterCount, functionArgs.Count); i < cnt; i++)
                {
                    if (i > parameterCount) visitor.Builder!.Append(", ");

                    if (functionName == "avg")
                    {
                        // Window AVG shares the ordinary argument policy (SQL Server widens an integral
                        // argument inside the aggregate); the rendered fragment is composed so any
                        // placeholder inside it keeps its real offset.
                        var rendered = visitor.VisitToString(functionArgs[i]);
                        averageArgument = visitor.Dialect.MakeAverageArgument(rendered, functionArgs[i].Type);
                        visitor.AppendTransformed(averageArgument, rendered);
                    }
                    else
                    {
                        visitor.Visit(functionArgs[i]);
                    }
                }
            }

            overStart = visitor.Builder!.Length;
            visitor.Builder!.Append(named ? visitor.Kw(") over ") : visitor.Kw(") over ("));
        }

        if (named)
        {
            visitor.Builder!.Append(namedWindow);
        }
        else
        {
            if (partitions.Count > 0)
            {
                visitor.Builder!.Append(visitor.Kw("partition by "));
                for (var (i, cnt) = (0, partitions.Count); i < cnt; i++)
                {
                    if (i > 0) visitor.Builder!.Append(", ");
                    visitor.Visit(partitions[i]);
                }
            }

            if (orders.Count > 0)
            {
                if (partitions.Count > 0) visitor.Builder!.Append(' ');

                visitor.Builder!.Append(visitor.Kw("order by "));
                for (var (i, cnt) = (0, orders.Count); i < cnt; i++)
                {
                    if (i > 0) visitor.Builder!.Append(", ");
                    visitor.Visit(orders[i].Body);
                    if (orders[i].Direction == OrderDirection.Desc)
                        visitor.Builder!.Append(visitor.Kw(" desc"));
                }
            }

            if (frame is not null)
            {
                if (partitions.Count > 0 || orders.Count > 0) visitor.Builder!.Append(' ');
                visitor.Builder!.Append(WindowSql.RenderWindowFrame(frame, visitor.KeywordCase));
            }

            visitor.Builder!.Append(')');
        }

        // ClickHouse returns nan for avg over an empty window frame, so the whole window expression is
        // wrapped in an eligibility check that repeats the identical window clause for count().
        if (functionName == "avg"
            && averageArgument is not null
            && overStart >= 0
            && visitor.Dialect.WrapsAverageResult)
        {
            var windowSuffix = visitor.Builder!.ToString(overStart + 1, visitor.Builder!.Length - overStart - 1);
            AppendWrapped(visitor, windowStart, windowMark,
                rendered => visitor.Dialect.MakeAverageResult(rendered, averageArgument, windowClause: windowSuffix));
        }

        if (functionName == "count" && visitor.Dialect.WrapsCountResult)
        {
            AppendWrapped(visitor, windowStart, windowMark, rendered => visitor.Dialect.WrapCount(rendered, big: false));
        }
        else if (declaringType.GetGenericArguments()[0] == typeof(int))
        {
            // The ranking window functions (row_number/rank/dense_rank/ntile) are declared as
            // WindowFunction<int>, but some providers return an unsigned 64-bit value (ClickHouse
            // returns UInt64) that the row reader cannot materialise into the declared int. The
            // dialect decides whether the rendered expression needs a narrowing cast.
            AppendWrapped(visitor, windowStart, windowMark, visitor.Dialect.WrapIntegerWindowResult);
        }

        return true;
    }

    /// <summary>
    /// Replaces the window expression rendered after <paramref name="start"/> with
    /// <paramref name="wrap"/> applied to it; the wrapper embeds the rendered window expression
    /// unchanged. The parameter recorder is rewound to <paramref name="mark"/> and the detached
    /// occurrences are relocated behind the wrapper prefix, so a cast around a window function that
    /// contains a placeholder keeps the placeholder at its real offset instead of leaving the recorder
    /// ahead of the builder.
    /// </summary>
    private static void AppendWrapped(
        BaseExpressionVisitor visitor,
        int start,
        SqlParameterMark? mark,
        Func<string, string> wrap)
    {
        var builder = visitor.Builder!;
        var rendered = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        var fragment = mark is { } checkpoint ? visitor.Recorder!.RewindTo(checkpoint) : null;
        var fragments = fragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(rendered, null, fragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        SqlParameterFragmentRelocation.AppendComposed(builder, visitor.Recorder, wrap(rendered), fragments);
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
