using System.Text;
using NextORM.Core;

namespace NextORM.MySql;

/// <summary>
/// MySQL dialect: backtick-quoted identifiers, <c>@name</c> parameters, <c>concat(...)</c>
/// concatenation (the <c>||</c> operator is logical OR by default in MySQL), <c>limit/offset</c>
/// paging and the standard-deviation/variance aggregate name mapping.
/// <para>
/// Declared non-sealed so that <c>NextORM.MariaDb</c> can derive its dialect from it.
/// </para>
/// </summary>
public class MySqlDialect : SqlDialectBase
{
    public static readonly MySqlDialect Instance = new();

    /// <summary>A MySQL derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    // MySQL 8.0.14+ (and MariaDB 10.3+) spell the APPLY surface as an ANSI lateral join, which is
    // the SqlDialectBase default.
    public override bool SupportsApply => true;

    // MySQL/MariaDB have a right join but no full join.
    public override bool SupportsFullJoin => false;

    // MySQL/MariaDB render greatest(...)/least(...) (NULL when any argument is NULL).
    public override bool SupportsGreatestLeast => true;

    /// <summary>MySQL renders the portable <c>iif</c> as <c>if(condition, whenTrue, whenFalse)</c>.</summary>
    public override IIifRenderer Iif => MySqlIifRenderer.Instance;

    /// <summary>MySQL 8.0+ (and MariaDB 10.2+) support the ANSI <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>MySQL 8.0+ supports <c>nth_value(value, n)</c> as a window function.</summary>
    public override bool SupportsNthValue => true;

    /// <summary>MySQL 8.0+ declares named windows (the <c>WINDOW</c> clause) and references them with <c>OVER w</c>.</summary>
    public override bool SupportsNamedWindows => true;

    // MySQL/MariaDB full-text search matches against a FULLTEXT index; contains uses boolean mode,
    // freetext natural-language mode. The relevance score is turned into a boolean.
    public override bool SupportsFullText => true;

    public override string MakeFullText(string functionName, string column, string search) =>
        functionName == "contains"
            ? $"(match({column}) against({search} in boolean mode) > 0)"
            : $"(match({column}) against({search}) > 0)";

    // MySQL/MariaDB store JSON in text columns; the SQL/JSON-shaped surface is expressed through the
    // JSON_EXTRACT/JSON_SET family. json_value unpacks a scalar (JSON_UNQUOTE), json_query keeps the
    // fragment, json_modify maps onto JSON_SET (insert-or-replace, matching JSON_MODIFY's lax path).
    public override bool SupportsTextJson => true;

    /// <summary>MySQL 5.7+/MariaDB render the arbitrary-value aggregate as <c>ANY_VALUE(x)</c>.</summary>
    public override bool SupportsAnyValueAggregate => true;

    /// <summary>MySQL/MariaDB render the whole session/information family.</summary>
    public override ISessionInfoFunctions SessionInfoFunctions => MySqlSessionInfoFunctions.Instance;

    public override string MakeTextJsonFunction(string name, IReadOnlyList<string> args) => name switch
    {
        "json_value" => $"json_unquote(json_extract({args[0]}, {args[1]}))",
        "json_query" => $"json_extract({args[0]}, {args[1]})",
        "json_modify" => $"json_set({args[0]}, {args[1]}, {args[2]})",
        _ => base.MakeTextJsonFunction(name, args)
    };

    // JSON_VALID returns 1/0; a predicate compares it with 1, a value context keeps the 0/1 integer.
    public override string MakeIsJson(string value, bool asPredicate) =>
        asPredicate ? $"(json_valid({value})) = 1" : $"json_valid({value})";

    // MySQL/MariaDB aggregate strings through group_concat (there is no array_agg).
    public override bool SupportsStringAgg => true;

    public override string MakeStringAgg(string value, string delimiter) =>
        $"group_concat({value} separator {delimiter})";

    // MySQL/MariaDB express interval arithmetic through date_add/date_sub and timestampdiff.
    public override bool SupportsDateArithmetic => true;

    public override string MakeDateAdd(string field, string amount, string value)
    {
        // There is no millisecond unit, so it is expressed in microseconds; decade/century/millennium
        // have no unit either and fold onto a scaled year.
        var (unit, factor) = field switch
        {
            "microseconds" => ("microsecond", 1),
            "milliseconds" => ("microsecond", 1000),
            "second" => ("second", 1),
            "minute" => ("minute", 1),
            "hour" => ("hour", 1),
            "day" => ("day", 1),
            "week" => ("week", 1),
            "month" => ("month", 1),
            "quarter" => ("quarter", 1),
            "year" => ("year", 1),
            "decade" => ("year", 10),
            "century" => ("year", 100),
            "millennium" => ("year", 1000),
            _ => throw new NotSupportedException($"MySQL date_add does not support the '{field}' field.")
        };

        var scaled = factor == 1 ? amount : $"({amount}) * {factor}";

        return $"date_add({value}, interval {scaled} {unit})";
    }

    public override string MakeDateDiff(string field, string start, string end)
    {
        var (unit, factor) = field switch
        {
            "microseconds" => ("microsecond", 1),
            "milliseconds" => ("microsecond", 1000),
            "second" => ("second", 1),
            "minute" => ("minute", 1),
            "hour" => ("hour", 1),
            "day" => ("day", 1),
            "week" => ("week", 1),
            "month" => ("month", 1),
            "quarter" => ("quarter", 1),
            "year" => ("year", 1),
            _ => throw new NotSupportedException($"MySQL date_diff does not support the '{field}' field.")
        };

        var diff = $"timestampdiff({unit}, {start}, {end})";

        return factor == 1 ? diff : $"cast(({diff} / {factor}) as signed)";
    }

    public override string MakeEndOfMonth(string value) => $"last_day({value})";

    public override string MakeDateFromParts(string year, string month, string day) =>
        $"str_to_date(concat_ws('-', {year}, {month}, {day}), '%Y-%m-%d')";

    // MySQL/MariaDB spell the super-aggregate as a trailing modifier (GROUP BY a, b WITH ROLLUP) and
    // have no CUBE.
    public override bool SupportsRollup => true;

    public override string MakeGrouping(string columns, GroupingType groupingType) => groupingType switch
    {
        GroupingType.Rollup => $"{columns} with rollup",
        _ => columns
    };

    // MySQL's infix || is a logical OR unless PIPES_AS_CONCAT is set, so concatenation is rendered
    // as the concat function instead of an operator.
    public override string MakeConcat(IReadOnlyList<string> parts) => $"concat({string.Join(", ", parts)})";

    // MySQL quotes identifiers with backticks; single-quoted aliases are syntax errors.
    public override string Escape(string keyword) => "`" + keyword + "`";

    /// <summary>MySQL quotes a physical identifier with backticks, doubling an embedded backtick.</summary>
    public override string QuoteIdentifier(string name) => "`" + name.Replace("`", "``") + "`";

    public override string MakeColumnReference(string name) => Escape(name);

    // MySQL's CAST target has its own type names (there is no cast(... as bigint/integer)); the CLR
    // numeric types map onto the signed/unsigned integer targets instead.
    public override string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "unsigned",
        _ when type == typeof(short) => "signed",
        _ when type == typeof(int) => "signed",
        _ when type == typeof(long) => "signed",
        _ when type == typeof(float) => "float",
        _ when type == typeof(double) => "double",
        _ when type == typeof(decimal) => "decimal",
        _ when type == typeof(string) => "char",
        _ when type == typeof(bool) => "signed",
        _ when type == typeof(DateTime) => "datetime",
        _ => type.Name
    };

    public override string MakeParam(string name) => $"@{name}";

    /// <summary>
    /// MySQL has <c>dayofyear()</c>/<c>quarter()</c>/<c>weekofyear()</c>; the generic <c>extract()</c>
    /// spelling is avoided for these parts. <c>weekofyear</c> is ISO 8601 (<c>EXTRACT(WEEK ...)</c>
    /// depends on <c>default_week_format</c>).
    /// </summary>
    public override string MakeDatePart(string part, string value) => part switch
    {
        "doy" => $"dayofyear({value})",
        "quarter" => $"quarter({value})",
        "week" => $"weekofyear({value})",
        "dow" => $"(dayofweek({value}) - 1)",
        "isodow" => $"(weekday({value}) + 1)",
        "epoch" => $"cast(unix_timestamp({value}) as double)",
        _ => base.MakeDatePart(part, value)
    };

    /// <summary>MySQL/MariaDB additionally accept the normalised weekday and epoch date parts.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    // length() counts bytes in MySQL; char_length() counts characters, matching string.Length.
    public override string MakeStringLength(string value) => $"char_length({value})";

    protected override string MakeStringPosition(string value, string substring) =>
        $"instr({value}, {substring})";

    protected override string MakeStringPosition(string value, string substring, string start) =>
        $"locate({substring}, {value}, {start} + 1)";

    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    protected override string MakeStringReverse(string value) => $"reverse({value})";

    public override string MakeStuff(string value, string start, string? count, string newValue) =>
        count is null
            ? $"substring({value}, 1, {start})"
            : $"insert({value}, {start} + 1, {count}, {newValue})";

    // now() is the session time zone; utc_timestamp() is UTC regardless of it.
    public override string MakeNow(bool utc) => utc ? "utc_timestamp()" : "now()";

    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev_samp",
        "stdevp" => "stddev_pop",
        "var" => "var_samp",
        "varp" => "var_pop",
        "any_agg" => "ANY_VALUE",
        _ => name
    };

    // MySQL treats the backslash as a string-literal escape too, so the escape character of a LIKE
    // predicate has to be written as a doubled backslash ('\\' rather than '\').
    public override string MakeLikeEscape(string escapeChar) =>
        " escape '" + escapeChar.Replace("\\", "\\\\").Replace("'", "''") + "'";

    // MySQL's ~ yields an unsigned 64-bit value, which overflows the signed CLR integer the
    // projection expects; -(x) - 1 keeps the two's-complement result signed.
    public override string MakeOnesComplement(string operand) => $"(-({operand}) - 1)";

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        // MySQL requires LIMIT before OFFSET, and OFFSET is only valid together with LIMIT, so an
        // offset-only page uses the maximum unsigned bigint as the limit.
        if (paging.Limit > 0)
            sqlBuilder.Append("limit ").Append(paging.Limit);
        else if (paging.Offset > 0)
            sqlBuilder.Append("limit 18446744073709551615");

        if (paging.Offset > 0)
            sqlBuilder.Append(" offset ").Append(paging.Offset);
    }

    /// <summary>MySQL/MariaDB support the trailing row-locking clause.</summary>
    public override ILockRenderer Lock => MySqlLockRenderer.Instance;
}

internal sealed class MySqlIifRenderer : IIifRenderer
{
    public static readonly MySqlIifRenderer Instance = new();

    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"if({condition}, {whenTrue}, {whenFalse})";
}

internal sealed class MySqlSessionInfoFunctions : ISessionInfoFunctions
{
    public static readonly MySqlSessionInfoFunctions Instance = new();

    public bool Supports(string name) =>
        name is "current_user" or "session_user" or "current_schema" or "current_database" or "version";

    public string Render(string name) => name switch
    {
        "current_user" => "current_user()",
        "session_user" => "session_user()",
        "current_schema" => "schema()",
        "current_database" => "database()",
        "version" => "version()",
        _ => throw new NotSupportedException($"The {name} session information function is not supported by MySQL/MariaDB.")
    };
}

internal sealed class MySqlLockRenderer : ILockRenderer
{
    public static readonly MySqlLockRenderer Instance = new();

    public bool UsesTableHints => false;

    public string Render(LockMode mode) =>
        mode == LockMode.Share ? " lock in share mode" : " for update";
}
