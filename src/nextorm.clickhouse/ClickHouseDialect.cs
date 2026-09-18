using System.Text;
using nextorm.core;

namespace nextorm.clickhouse;

/// <summary>
/// ClickHouse dialect: backtick-quoted identifiers, <c>@name</c> parameters, <c>concat(...)</c>
/// concatenation, <c>limit/offset</c> paging, ClickHouse type names and the camel-case
/// standard-deviation/variance aggregate names. ClickHouse has no recursive CTE support, so the
/// <c>recursive</c> modifier is omitted.
/// </summary>
public sealed class ClickHouseDialect : SqlDialectBase
{
    public static readonly ClickHouseDialect Instance = new();

    /// <summary>A ClickHouse derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    /// <summary>ClickHouse implements the <c>INTERSECT ALL</c>/<c>EXCEPT ALL</c> set-operation variants.</summary>
    public override bool SupportsIntersectExceptAll => true;

    public override string MakeConcat(IReadOnlyList<string> parts) => $"concat({string.Join(", ", parts)})";

    // ClickHouse renders greatest(...)/least(...).
    public override bool SupportsGreatestLeast => true;

    // ClickHouse has dateTrunc(unit, datetime) and the addYears/.../addSeconds family plus
    // toLastDayOfMonth(date).
    public override bool SupportsDateTrunc => true;
    public override bool SupportsDateArithmetic => true;

    // string_agg is rendered as arrayStringConcat(groupArray(value), delimiter). ClickHouse has no
    // array type usable by the row reader, so array_agg stays unavailable.
    public override bool SupportsStringAgg => true;

    // ClickHouse renders the bit aggregates as groupBitAnd/groupBitOr/groupBitXor, the statistical
    // aggregates as corr/covarPop/covarSamp and the filtered aggregates as the -If combinators.
    public override bool SupportsBitAggregates => true;
    public override bool SupportsStatisticalAggregates => true;
    public override bool SupportsArgMinMax => true;
    public override bool SupportsIfAggregates => true;

    // The ClickHouse driver turns CommandBehavior.SingleRow into a trailing LIMIT 1, which would
    // duplicate the limit the dialect already renders for single-row commands.
    public override bool SupportsCommandBehaviorSingleRow => false;

    // ClickHouse spells the super-aggregate as a trailing modifier (GROUP BY a, b WITH ROLLUP/CUBE).
    public override bool SupportsRollup => true;
    public override bool SupportsCube => true;

    public override string MakeGrouping(string columns, GroupingType groupingType) => groupingType switch
    {
        GroupingType.Rollup => $"{columns} with rollup",
        GroupingType.Cube => $"{columns} with cube",
        _ => columns
    };

    // ClickHouse quotes identifiers with backticks; single-quoted aliases are syntax errors.
    public override string Escape(string keyword) => "`" + keyword + "`";

    public override string MakeColumnReference(string name) => Escape(name);

    public override string MakeParam(string name) => $"@{name}";

    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    public override string MakeBool(bool v) => v ? "true" : "false";

    // length() counts bytes in ClickHouse; lengthUTF8() counts characters, matching string.Length.
    public override string MakeStringLength(string value) => $"lengthUTF8({value})";

    public override string MakeTrim(string value, StringTrimKind kind) => kind switch
    {
        StringTrimKind.Start => $"trimLeft({value})",
        StringTrimKind.End => $"trimRight({value})",
        _ => $"trimBoth({value})"
    };

    // now() uses the server time zone; the optional argument selects a zone.
    public override string MakeNow(bool utc) => utc ? "now('UTC')" : "now()";

    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddevSamp",
        "stdevp" => "stddevPop",
        "var" => "varSamp",
        "varp" => "varPop",
        "covar_pop" => "covarPop",
        "covar_samp" => "covarSamp",
        "bit_and" => "groupBitAnd",
        "bit_or" => "groupBitOr",
        "bit_xor" => "groupBitXor",
        "arg_min" => "argMin",
        "arg_max" => "argMax",
        "count_if" => "countIf",
        "sum_if" => "sumIf",
        "avg_if" => "avgIf",
        "min_if" => "minIf",
        "max_if" => "maxIf",
        _ => name
    };

    /// <summary>
    /// ClickHouse truncates through <c>dateTrunc(unit, value)</c>. It accepts <c>second</c>..<c>year</c>
    /// and the sub-second units for <c>DateTime64</c>, but has no <c>decade</c>/<c>century</c>/<c>millennium</c>;
    /// the plural ANSI sub-second names map to the ClickHouse singular ones.
    /// </summary>
    public override string MakeDateTrunc(string field, string value)
    {
        var part = field switch
        {
            "microseconds" => "microsecond",
            "milliseconds" => "millisecond",
            "decade" or "century" or "millennium" =>
                throw new NotSupportedException($"ClickHouse dateTrunc does not support the '{field}' field."),
            _ => field
        };

        return $"dateTrunc('{part}', {value})";
    }

    /// <summary>
    /// ClickHouse adds intervals through the dedicated <c>addYears</c>/<c>addQuarters</c>/.../<c>addSeconds</c>
    /// functions. <c>decade</c>/<c>century</c>/<c>millennium</c> are folded onto a scaled <c>addYears</c>.
    /// </summary>
    public override string MakeDateAdd(string field, string amount, string value)
    {
        var (function, factor) = field switch
        {
            "year" => ("addYears", 1),
            "quarter" => ("addQuarters", 1),
            "month" => ("addMonths", 1),
            "week" => ("addWeeks", 1),
            "day" => ("addDays", 1),
            "hour" => ("addHours", 1),
            "minute" => ("addMinutes", 1),
            "second" => ("addSeconds", 1),
            "milliseconds" => ("addMilliseconds", 1),
            "microseconds" => ("addMicroseconds", 1),
            "decade" => ("addYears", 10),
            "century" => ("addYears", 100),
            "millennium" => ("addYears", 1000),
            _ => throw new NotSupportedException($"ClickHouse date_add does not support the '{field}' field.")
        };

        var scaled = factor == 1 ? amount : $"({amount}) * {factor}";
        return $"{function}({value}, {scaled})";
    }

    public override string MakeEndOfMonth(string value) => $"toLastDayOfMonth({value})";

    public override string MakeStringAgg(string value, string delimiter) =>
        $"arrayStringConcat(groupArray({value}), {delimiter})";

    public override string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "UInt8",
        _ when type == typeof(short) => "Int16",
        _ when type == typeof(int) => "Int32",
        _ when type == typeof(long) => "Int64",
        _ when type == typeof(float) => "Float32",
        _ when type == typeof(double) => "Float64",
        _ when type == typeof(decimal) => "Decimal(38, 10)",
        _ => base.MakeTypeName(type)
    };

    // ClickHouse declares every CTE with `with`; there is no RECURSIVE keyword.
    public override string MakeWith(bool recursive) => "with ";

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        // ClickHouse requires LIMIT before OFFSET, so an offset-only page uses the maximum unsigned
        // bigint as the limit.
        if (paging.Limit > 0)
            sqlBuilder.Append("limit ").Append(paging.Limit);
        else if (paging.Offset > 0)
            sqlBuilder.Append("limit 18446744073709551615");

        if (paging.Offset > 0)
            sqlBuilder.Append(" offset ").Append(paging.Offset);
    }
}
