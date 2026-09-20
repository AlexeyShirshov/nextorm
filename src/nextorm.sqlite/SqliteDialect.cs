using System.Text;
using NextORM.Core;

namespace NextORM.Sqlite;

/// <summary>SQLite dialect: <c>||</c> concatenation, <c>$name</c> parameters, <c>limit/offset</c> paging.</summary>
public sealed class SqliteDialect : SqlDialectBase
{
    public static readonly SqliteDialect Instance = new();

    public override string ConcatStringOperator => "||";

    // SQLite silently returns the first row of a scalar subquery that produces several rows, so
    // Single/SingleOrDefault inside a scalar subquery cannot be enforced by the engine.
    public override bool EnforcesScalarSubqueryCardinality => false;

    // SQLite 3.30+ accepts the FILTER (WHERE ...) aggregate clause.
    public override bool SupportsFilter => true;

    // SQLite 3.33+ accepts the ANSI GROUP BY ROLLUP (...)/CUBE (...) form.
    public override bool SupportsRollup => true;
    public override bool SupportsCube => true;
    public override bool SupportsGroupingSets => true;

    // SQLite aggregates strings through group_concat (there is no array_agg).
    public override bool SupportsStringAgg => true;

    public override string MakeStringAgg(string value, string delimiter) =>
        $"group_concat({value}, {delimiter})";

    /// <summary>SQLite exposes only the library version from the session/information family.</summary>
    public override bool SupportsSessionInfoFunctions => true;

    /// <summary>SQLite has no session/user/schema/database information; only the library version is exposed.</summary>
    public override bool SupportsSessionInfoFunction(string name) => name is "version";

    /// <summary>SQLite renders <c>version()</c> as <c>sqlite_version()</c>.</summary>
    public override string MakeSessionInfoFunction(string name) =>
        name == "version" ? "sqlite_version()" : base.MakeSessionInfoFunction(name);

    public override bool SupportsGreatestLeast => true;

    /// <summary>SQLite 3.32+ renders the portable <c>iif</c> as <c>iif(condition, whenTrue, whenFalse)</c>.</summary>
    public override bool SupportsIif => true;

    /// <summary>SQLite 3.32+ renders the portable <c>iif</c> as <c>iif(condition, whenTrue, whenFalse)</c>.</summary>
    public override string MakeIif(string condition, string whenTrue, string whenFalse) =>
        $"iif({condition}, {whenTrue}, {whenFalse})";

    /// <summary>SQLite 3.25+ supports the ANSI <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>SQLite 3.25+ supports <c>nth_value(value, n)</c> as a window function.</summary>
    public override bool SupportsNthValue => true;

    public override string MakeGreatest(IReadOnlyList<string> args) =>
        args.Count == 1 ? $"({args[0]})" : $"max({string.Join(", ", args)})";

    public override string MakeLeast(IReadOnlyList<string> args) =>
        args.Count == 1 ? $"({args[0]})" : $"min({string.Join(", ", args)})";

    // instr() is the one-based position primitive; SQLite has no reverse(), so string.LastIndexOf is
    // not available (the base throws a clear message).
    protected override string MakeStringPosition(string value, string substring) =>
        $"instr({value}, {substring})";

    // There is no repeat(); a run of the (single-character) value is produced by replacing every '00'
    // of a zero blob's hex representation, whose length in bytes is its character count.
    public override string MakeRepeat(string value, string count) =>
        $"replace(hex(zeroblob({count})), '00', {value})";

    // SQLite has no dateadd/datediff; it adjusts a date through a modifier string and measures
    // differences in seconds (or months for calendar parts).
    public override bool SupportsDateArithmetic => true;

    public override string MakeDateAdd(string field, string amount, string value)
    {
        // The modifier number carries its own sign, so the amount expression is concatenated into
        // the modifier string; there is no millisecond/month-quarter unit, so those fold.
        var (unit, factor, subSecond) = field switch
        {
            "microseconds" => ("seconds", 1_000_000, true),
            "milliseconds" => ("seconds", 1_000, true),
            "second" => ("seconds", 1, false),
            "minute" => ("minutes", 1, false),
            "hour" => ("hours", 1, false),
            "day" => ("days", 1, false),
            "week" => ("days", 7, false),
            "month" => ("months", 1, false),
            "quarter" => ("months", 3, false),
            "year" => ("years", 1, false),
            "decade" => ("years", 10, false),
            "century" => ("years", 100, false),
            "millennium" => ("years", 1000, false),
            _ => throw new NotSupportedException($"SQLite date_add does not support the '{field}' field.")
        };

        var scaled = subSecond
            ? $"({amount}) / {factor}.0"
            : factor == 1 ? amount : $"({amount}) * {factor}";

        var modifier = $"({scaled}) || ' {unit}'";

        // A sub-second shift goes through strftime to keep the fractional seconds.
        return subSecond
            ? $"strftime('%Y-%m-%d %H:%M:%f', {value}, {modifier})"
            : $"datetime({value}, {modifier})";
    }

    public override string MakeDateDiff(string field, string start, string end)
    {
        var seconds = $"(strftime('%s', {end}) - strftime('%s', {start}))";

        return field switch
        {
            "microseconds" => $"({seconds} * 1000000)",
            "milliseconds" => $"({seconds} * 1000)",
            "second" => seconds,
            "minute" => $"({seconds} / 60)",
            "hour" => $"({seconds} / 3600)",
            "day" => $"({seconds} / 86400)",
            "week" => $"({seconds} / 604800)",
            "month" => MonthsDiff(start, end),
            "quarter" => $"({MonthsDiff(start, end)} / 3)",
            "year" => $"({MonthsDiff(start, end)} / 12)",
            _ => throw new NotSupportedException($"SQLite date_diff does not support the '{field}' field.")
        };
    }

    private static string MonthsDiff(string start, string end) =>
        $"((cast(strftime('%Y', {end}) as integer) * 12 + cast(strftime('%m', {end}) as integer)) - "
        + $"(cast(strftime('%Y', {start}) as integer) * 12 + cast(strftime('%m', {start}) as integer)))";

    public override string MakeEndOfMonth(string value) =>
        $"date({value}, 'start of month', '+1 month', '-1 day')";

    public override string MakeDateFromParts(string year, string month, string day) =>
        $"date(printf('%04d-%02d-%02d', {year}, {month}, {day}))";

    public override string MakeCoalesce(string v1, string v2) => $"ifnull({v1}, {v2})";

    public override string MakeParam(string name) => $"${name}";

    // SQLite has no now(); datetime('now') is UTC and is used for both local and UTC requests.
    public override string MakeNow(bool utc) => "datetime('now')";

    // SQLite extracts date parts through strftime; the result is cast back to an integer so that it
    // materialises like the C# DateTime.Year/Month/... int properties.
    public override string MakeDatePart(string part, string value) => part switch
    {
        "year" => $"cast(strftime('%Y', {value}) as integer)",
        "month" => $"cast(strftime('%m', {value}) as integer)",
        "day" => $"cast(strftime('%d', {value}) as integer)",
        "hour" => $"cast(strftime('%H', {value}) as integer)",
        "minute" => $"cast(strftime('%M', {value}) as integer)",
        "second" => $"cast(strftime('%S', {value}) as integer)",
        "doy" => $"cast(strftime('%j', {value}) as integer)",
        "quarter" => $"cast((cast(strftime('%m', {value}) as integer) + 2) / 3 as integer)",
        // ISO week via the Thursday of the ISO week; %W has a week-0 and is not ISO 8601.
        "week" => $"cast((cast(strftime('%j', date({value}, '-3 days', 'weekday 4')) as integer) + 6) / 7 as integer)",
        "dow" => $"cast(strftime('%w', {value}) as integer)",
        "isodow" => $"((cast(strftime('%w', {value}) as integer) + 6) % 7 + 1)",
        "epoch" => $"((julianday({value}) - 2440587.5) * 86400.0)",
        _ => base.MakeDatePart(part, value)
    };

    /// <summary>SQLite additionally accepts the ISO week, the normalised weekdays and epoch.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    // SQLite's log() is base 10; the natural logarithm (Math.Log) is ln().
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        name == "log" && args.Count == 1
            ? $"ln({args[0]})"
            : base.MakeMathFunction(name, args);

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        sqlBuilder.Append("limit ").Append(paging.Limit > 0
            ? paging.Limit
            : -1);

        if (paging.Offset > 0)
            sqlBuilder.Append(" offset ").Append(paging.Offset);
    }
}
