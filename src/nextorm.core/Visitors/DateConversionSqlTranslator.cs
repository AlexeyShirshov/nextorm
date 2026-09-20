using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the date conversion/truncation surface of <see cref="ClickHouseFunctions"/>
/// (<c>to_date</c>/<c>to_date_time</c>/<c>to_date32</c>, the <c>to_year</c>/... part accessors,
/// <c>to_start_of_*</c>, <c>to_monday</c>, <c>to_yyyymm</c>/<c>to_yyyymmdd</c> and
/// <c>to_unix_timestamp</c>). The part accessors reuse the existing <see cref="ISqlDialect.MakeDatePart"/>
/// hook; the rest go through <see cref="ISqlDialect.MakeDateConversion"/>. Only a dialect that opts in
/// with <see cref="ISqlDialect.SupportsDateConversionFunctions"/> (ClickHouse) may use these constructs;
/// every other provider rejects them with a clear message.
/// </summary>
internal static class DateConversionSqlTranslator
{
    /// <summary>
    /// Translates a date conversion call. Returns <c>false</c> when the call is not part of this
    /// surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(ClickHouseFunctions))
            return false;

        var part = node.Method.Name switch
        {
            nameof(ClickHouseFunctions.to_year) => "year",
            nameof(ClickHouseFunctions.to_quarter) => "quarter",
            nameof(ClickHouseFunctions.to_month) => "month",
            nameof(ClickHouseFunctions.to_day_of_month) => "day",
            nameof(ClickHouseFunctions.to_day_of_week) => "dow",
            nameof(ClickHouseFunctions.to_day_of_year) => "doy",
            nameof(ClickHouseFunctions.to_hour) => "hour",
            nameof(ClickHouseFunctions.to_minute) => "minute",
            nameof(ClickHouseFunctions.to_second) => "second",
            _ => null
        };

        if (part is not null && node.Arguments.Count == 1)
        {
            EmitPart(visitor, node.Arguments[0], part);
            return true;
        }

        if (node.Arguments.Count == 1 && IsConversion(node.Method.Name))
        {
            EmitConversion(visitor, node, node.Method.Name);
            return true;
        }

        return false;
    }

    private static bool IsConversion(string name) => name is
        nameof(ClickHouseFunctions.to_date) or nameof(ClickHouseFunctions.to_date_time) or
        nameof(ClickHouseFunctions.to_date32) or
        nameof(ClickHouseFunctions.to_start_of_year) or nameof(ClickHouseFunctions.to_start_of_quarter) or
        nameof(ClickHouseFunctions.to_start_of_month) or nameof(ClickHouseFunctions.to_start_of_week) or
        nameof(ClickHouseFunctions.to_start_of_day) or nameof(ClickHouseFunctions.to_start_of_hour) or
        nameof(ClickHouseFunctions.to_start_of_minute) or nameof(ClickHouseFunctions.to_start_of_second) or
        nameof(ClickHouseFunctions.to_monday) or nameof(ClickHouseFunctions.to_yyyymm) or
        nameof(ClickHouseFunctions.to_yyyymmdd) or nameof(ClickHouseFunctions.to_unix_timestamp);

    private static void EmitPart(BaseExpressionVisitor visitor, Expression argument, string part)
    {
        RequireSupport(visitor);

        if (visitor.IsParamMode)
        {
            visitor.Visit(argument);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDatePart(part, visitor.VisitToString(argument)));
    }

    private static void EmitConversion(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        RequireSupport(visitor);

        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeDateConversion(name, rendered));
    }

    private static void RequireSupport(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsDateConversionFunctions)
            throw new NotSupportedException(
                "The date conversion functions (toDate/toDateTime/toDate32/toStartOf*/toMonday/toYYYYMM/toUnixTimestamp) are not supported by this provider.");
    }
}
