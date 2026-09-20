using System.Text;
using NextORM.Core;

namespace NextORM.ClickHouse;

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

    /// <summary>ClickHouse implements the <c>ANY</c>/<c>ALL</c>/<c>ASOF</c> join modifiers.</summary>
    public override bool SupportsJoinStrictness => true;

    /// <summary>ClickHouse implements the <c>GLOBAL</c> join modifier.</summary>
    public override bool SupportsGlobalJoin => true;

    /// <summary>
    /// ClickHouse has a native <c>Array(T)</c> type and implements the array functions over array
    /// columns (<c>length</c>, <c>has</c>, <c>indexOf</c>, <c>arrayStringConcat</c>, <c>hasAny</c>/<c>hasAll</c>,
    /// <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>, <c>splitByChar</c>).
    /// </summary>
    public override bool SupportsArrayFunctions => true;

    /// <summary>ClickHouse implements <c>arrayJoin(array)</c>, which expands one row per element.</summary>
    public override bool SupportsArrayJoin => true;

    /// <summary>
    /// <c>length</c> and <c>indexOf</c> return <c>UInt64</c> natively; cast them to <c>Int64</c> so the
    /// row reader can materialise the declared CLR integer.
    /// </summary>
    public override string MakeArrayFunction(string name, string call) =>
        name is "length" or "indexOf" ? $"toInt64({call})" : call;

    /// <summary>
    /// Renders <c>[global] [inner|left|right|full|cross] [any|all|asof] join</c>. ClickHouse places the
    /// <c>GLOBAL</c> modifier first and the strictness after the join type, before <c>join</c>.
    /// </summary>
    public override string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal)
    {
        var type = joinType switch
        {
            JoinType.Inner => "",
            JoinType.Left => " left",
            JoinType.Right => " right",
            JoinType.Full => " full",
            JoinType.Cross => " cross",
            JoinType.FullCross => " cross",
            _ => throw new NotSupportedException($"The join modifier cannot be applied to a {joinType} join")
        };

        var modifier = strictness switch
        {
            JoinStrictness.Default => "",
            JoinStrictness.Any => " any",
            JoinStrictness.All => " all",
            JoinStrictness.Asof => " asof",
            _ => throw new NotSupportedException($"The {strictness} join modifier is not supported")
        };

        return (isGlobal ? " global" : "") + type + modifier + " join ";
    }

    // ClickHouse renders greatest(...)/least(...).
    public override bool SupportsGreatestLeast => true;

    /// <summary>ClickHouse renders the portable <c>iif</c> as <c>if(condition, whenTrue, whenFalse)</c>.</summary>
    public override bool SupportsIif => true;

    /// <summary>ClickHouse renders the portable <c>iif</c> as <c>if(condition, whenTrue, whenFalse)</c>.</summary>
    public override string MakeIif(string condition, string whenTrue, string whenFalse) =>
        $"if({condition}, {whenTrue}, {whenFalse})";

    // ClickHouse has dateTrunc(unit, datetime) and the addYears/.../addSeconds family plus
    // toLastDayOfMonth(date).
    public override bool SupportsDateTrunc => true;
    public override bool SupportsDateArithmetic => true;

    public override bool SupportsDateTruncField(string field) =>
        field is not ("decade" or "century" or "millennium") && base.SupportsDateTruncField(field);

    // string_agg is rendered as arrayStringConcat(groupArray(value), delimiter). ClickHouse has no
    // array type usable by the row reader, so array_agg stays unavailable.
    public override bool SupportsStringAgg => true;

    // ClickHouse renders the bit aggregates as groupBitAnd/groupBitOr/groupBitXor, the statistical
    // aggregates as corr/covarPop/covarSamp and the filtered aggregates as the -If combinators.
    public override bool SupportsBitAggregates => true;
    public override bool SupportsStatisticalAggregates => true;
    public override bool SupportsArgMinMax => true;
    public override bool SupportsIfAggregates => true;
    /// <summary>ClickHouse implements the distinct-count <c>uniq*</c> and parameterised <c>quantile*</c> aggregates.</summary>
    public override bool SupportsUniqAggregates => true;

    /// <summary>ClickHouse implements the parameterised <c>quantile(level)(value)</c> family and <c>median</c>.</summary>
    public override bool SupportsQuantileAggregates => true;

    /// <summary>ClickHouse implements the <c>any</c>/<c>anyLast</c> row-picking aggregates.</summary>
    public override bool SupportsAnyAggregates => true;

    /// <summary>ClickHouse renders the arbitrary-value aggregate as <c>any(x)</c>.</summary>
    public override bool SupportsAnyValueAggregate => true;

    /// <summary>ClickHouse implements the <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>ClickHouse implements the <c>nth_value(value, n)</c> window function.</summary>
    public override bool SupportsNthValue => true;

    /// <summary>
    /// ClickHouse implements the <c>JSONExtract*</c>/<c>JSONHas</c>/<c>visitParamExtract*</c> string-JSON
    /// family and the JSONPath scalars <c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>.
    /// </summary>
    public override bool SupportsJsonExtract => true;

    /// <summary>
    /// ClickHouse spells the string-JSON extractors in camel case; <c>JSONLength</c> returns <c>UInt64</c>,
    /// which the row reader cannot materialise, so it is cast to <c>Int64</c>.
    /// </summary>
    public override string MakeJsonExtract(string name, IReadOnlyList<string> args)
    {
        var function = name switch
        {
            "json_extract_string" => "JSONExtractString",
            "json_extract_int" => "JSONExtractInt",
            "json_extract_float" => "JSONExtractFloat",
            "json_extract_bool" => "JSONExtractBool",
            "json_extract_raw" => "JSONExtractRaw",
            "json_has" => "JSONHas",
            "json_length" => "JSONLength",
            "json_type" => "JSONType",
            "visit_param_extract_string" => "visitParamExtractString",
            "visit_param_extract_int" => "visitParamExtractInt",
            "visit_param_extract_float" => "visitParamExtractFloat",
            "visit_param_extract_bool" => "visitParamExtractBool",
            "visit_param_extract_raw" => "visitParamExtractRaw",
            "json_value" => "JSON_VALUE",
            "json_query" => "JSON_QUERY",
            "json_exists" => "JSON_EXISTS",
            _ => name
        };

        var call = $"{function}({string.Join(", ", args)})";
        return name == "json_length" ? $"toInt64({call})" : call;
    }

    /// <summary>
    /// ClickHouse renders <c>currentUser()</c>, <c>currentDatabase()</c> and <c>version()</c>; it has no
    /// session-user or schema concept, so <c>session_user</c>/<c>current_schema</c> stay unavailable.
    /// </summary>
    public override bool SupportsSessionInfoFunctions => true;

    /// <summary>ClickHouse supports <c>current_user</c>/<c>current_database</c>/<c>version</c> only.</summary>
    public override bool SupportsSessionInfoFunction(string name) =>
        name is "current_user" or "current_database" or "version";

    /// <summary>ClickHouse spells the supported functions in camel case.</summary>
    public override string MakeSessionInfoFunction(string name) => name switch
    {
        "current_user" => "currentUser()",
        "current_database" => "currentDatabase()",
        "version" => "version()",
        _ => base.MakeSessionInfoFunction(name)
    };

    /// <summary>ClickHouse renders both UUID generators.</summary>
    public override bool SupportsUuidGenerators => true;

    /// <summary>ClickHouse supports both the random v4 and the v7 generators.</summary>
    public override bool SupportsUuidGenerator(string name) => name is "gen_random_uuid" or "uuidv7";

    /// <summary>ClickHouse spells the UUID generators in camel case.</summary>
    public override string MakeUuidGenerator(string name) => name switch
    {
        "gen_random_uuid" => "generateUUIDv4()",
        "uuidv7" => "generateUUIDv7()",
        _ => base.MakeUuidGenerator(name)
    };

    /// <summary>ClickHouse implements the dictionary functions <c>dictGet</c>/<c>dictGetOrDefault</c>/<c>dictHas</c>.</summary>
    public override bool SupportsDictionaries => true;

    /// <summary>ClickHouse spells the dictionary functions in camel case.</summary>
    public override string MakeDictionaryFunction(string name, IReadOnlyList<string> args)
    {
        var function = name switch
        {
            "dict_get" => "dictGet",
            "dict_get_or_default" => "dictGetOrDefault",
            "dict_has" => "dictHas",
            _ => name
        };

        return $"{function}({string.Join(", ", args)})";
    }

    // The uniq* aggregates return UInt64, which the row reader cannot materialise; cast to Int64.
    public override string MakeUniqAggregate(string name, string argument) =>
        $"toInt64({MakeAggregate(name)}({argument}))";

    // count()/countIf() also return UInt64; cast to the CLR type the function declares (int vs long).
    public override bool WrapsCountResult => true;

    public override string WrapCount(string countExpression, bool big) =>
        big ? $"toInt64({countExpression})" : $"toInt32({countExpression})";

    // quantileTiming returns Float32 and quantileExact keeps the input type; toFloat64 pins every
    // variant to the CLR double the methods declare.
    public override string MakeQuantile(string name, string level, string value) =>
        $"toFloat64({MakeAggregate(name)}({level})({value}))";

    /// <summary>ClickHouse's <c>median</c> is <c>quantile(0.5)</c>; cast it so it materialises as a CLR double.</summary>
    public override string MakeMedian(string value) => $"toFloat64(median({value}))";

    // The ClickHouse driver turns CommandBehavior.SingleRow into a trailing LIMIT 1, which would
    // duplicate the limit the dialect already renders for single-row commands.
    public override bool SupportsCommandBehaviorSingleRow => false;

    // ClickHouse spells the super-aggregate as a trailing modifier (GROUP BY a, b WITH ROLLUP/CUBE).
    public override bool SupportsRollup => true;
    public override bool SupportsCube => true;
    public override bool SupportsGroupingSets => true;

    /// <summary>ClickHouse implements the <c>GROUP BY ... WITH TOTALS</c> modifier.</summary>
    public override bool SupportsGroupByWithTotals => true;

    /// <summary>The super-aggregate <c>WITH TOTALS</c> is a trailing modifier after the grouping list.</summary>
    public override string MakeGroupByTotals(string grouping) => $"{grouping} with totals";

    /// <summary>ClickHouse provides the <c>numbers</c>/<c>numbers_mt</c> and <c>zeros</c>/<c>zeros_mt</c> table functions.</summary>
    public override bool SupportsTableFunction(string name) =>
        name is "numbers" or "numbers_mt" or "zeros" or "zeros_mt";

    /// <summary>
    /// <c>numbers</c>/<c>numbers_mt</c> expose an unsigned <c>UInt64 number</c> column, which the row
    /// reader cannot materialise; cast it to <c>Int64</c> through a wrapping subquery.
    /// </summary>
    public override string WrapTableFunction(string name, string call) =>
        name is "numbers" or "numbers_mt"
            ? $"(select toInt64(number) as number from {call})"
            : call;

    /// <summary>ClickHouse implements the distributed <c>GLOBAL IN</c> predicate.</summary>
    public override bool SupportsGlobalPredicates => true;

    /// <summary>ClickHouse implements <c>LIMIT n BY expr</c>.</summary>
    public override bool SupportsLimitBy => true;

    /// <summary>ClickHouse implements the <c>FINAL</c> table modifier.</summary>
    public override bool SupportsFinal => true;

    /// <summary>ClickHouse implements the <c>SAMPLE</c> table modifier.</summary>
    public override bool SupportsSample => true;

    /// <summary>ClickHouse implements the <c>PREWHERE</c> clause.</summary>
    public override bool SupportsPreWhere => true;

    /// <summary>ClickHouse implements the <c>ARRAY JOIN</c> clause.</summary>
    public override bool SupportsArrayJoinClause => true;

    /// <summary>Renders <c>[left ]array join expr, ...</c>.</summary>
    public override string MakeArrayJoin(ArrayJoinKind kind, IReadOnlyList<string> expressions)
    {
        var keyword = kind == ArrayJoinKind.Left ? " left array join " : " array join ";
        return keyword + string.Join(", ", expressions);
    }

    /// <summary>ClickHouse implements the trailing <c>SETTINGS</c> clause.</summary>
    public override bool SupportsSettings => true;

    /// <summary>Renders <c>limit [offset, ]n by col1, col2</c>; ClickHouse places it before the final LIMIT.</summary>
    public override void MakeLimitBy(int limit, int offset, IReadOnlyList<string> columns, StringBuilder sqlBuilder)
    {
        sqlBuilder.Append("limit ");
        if (offset > 0)
            sqlBuilder.Append(offset).Append(", ");
        sqlBuilder.Append(limit).Append(" by ").Append(string.Join(", ", columns));
    }

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

    // position() works in bytes; positionUTF8() works in characters, matching string.IndexOf.
    protected override string MakeStringPosition(string value, string substring) =>
        $"positionUTF8({value}, {substring})";

    protected override string MakeStringPosition(string value, string substring, string start) =>
        $"positionUTF8({value}, {substring}, {start} + 1)";

    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    protected override string MakeStringReverse(string value) => $"reverseUTF8({value})";

    public override string MakeTrim(string value, StringTrimKind kind) => kind switch
    {
        StringTrimKind.Start => $"trimLeft({value})",
        StringTrimKind.End => $"trimRight({value})",
        _ => $"trimBoth({value})"
    };

    // now() uses the server time zone; the optional argument selects a zone.
    public override string MakeNow(bool utc) => utc ? "now('UTC')" : "now()";

    /// <summary>
    /// ClickHouse spells the day-of-year part as <c>toDayOfYear()</c> and the other non-ANSI parts
    /// through the <c>toXxx</c> family. <c>toDayOfWeek</c> is ISO (1=Monday..7=Sunday), so
    /// <c>dow = toDayOfWeek % 7</c>.
    /// </summary>
    public override string MakeDatePart(string part, string value) => part switch
    {
        "doy" => $"toDayOfYear({value})",
        "quarter" => $"toQuarter({value})",
        "week" => $"toISOWeek({value})",
        "dow" => $"(toDayOfWeek({value}) % 7)",
        "isodow" => $"toDayOfWeek({value})",
        "epoch" => $"toFloat64(toUnixTimestamp({value}))",
        _ => base.MakeDatePart(part, value)
    };

    /// <summary>ClickHouse additionally accepts the ISO week, the normalised weekdays and epoch.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

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
        "uniq_exact" => "uniqExact",
        "uniq_combined" => "uniqCombined",
        "uniq_hll12" => "uniqHLL12",
        "quantile_exact" => "quantileExact",
        "quantile_timing" => "quantileTiming",
        "any_agg" => "any",
        "any_last" => "anyLast",
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

    /// <summary>ClickHouse computes date differences through <c>dateDiff(unit, start, end)</c>.</summary>
    public override string MakeDateDiff(string field, string start, string end)
    {
        var unit = field switch
        {
            "milliseconds" => "millisecond",
            "microseconds" => "microsecond",
            _ => field
        };

        return $"dateDiff('{unit}', {start}, {end})";
    }

    public override string MakeEndOfMonth(string value) => $"toLastDayOfMonth({value})";

    public override string MakeDateFromParts(string year, string month, string day) =>
        $"makeDate({year}, {month}, {day})";

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
