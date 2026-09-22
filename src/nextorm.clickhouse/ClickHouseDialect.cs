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

    private readonly IMultiIfRenderer _multiIf;
    private readonly IUniqAggregateRenderer _uniqAggregates;
    private readonly IQuantileAggregateRenderer _quantileAggregates;
    private readonly ITopKAggregateRenderer _topKAggregates;

    /// <summary>Creates the dialect and its capability renderers.</summary>
    public ClickHouseDialect()
    {
        _multiIf = new ClickHouseMultiIfRenderer(this);
        _uniqAggregates = new ClickHouseUniqAggregateRenderer(this);
        _quantileAggregates = new ClickHouseQuantileAggregateRenderer(this);
        _topKAggregates = new ClickHouseTopKAggregateRenderer(this);
    }

    /// <summary>A ClickHouse derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    /// <summary>ClickHouse supports a raw SQL derived table (<c>FROM (&lt;sql&gt;) AS alias</c>).</summary>
    public override bool SupportsRawSqlSource => true;

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

    /// <summary>
    /// ClickHouse implements the higher-order (lambda) array functions (<c>arrayMap</c>,
    /// <c>arrayFilter</c>, <c>arrayExists</c>, <c>arrayAll</c>, <c>arrayCount</c>,
    /// <c>arrayFirst*</c>, <c>arrayLast*</c>).
    /// </summary>
    public override bool SupportsHigherOrderArrayFunctions => true;

    /// <summary>ClickHouse implements <c>arrayJoin(array)</c>, which expands one row per element.</summary>
    public override bool SupportsArrayJoin => true;

    /// <summary>ClickHouse renders <c>string.Split</c> as <c>splitByChar(separator, value)</c>.</summary>
    public override IStringSplitRenderer StringSplit => ClickHouseStringSplitRenderer.Instance;

    /// <summary>
    /// <c>length</c>, <c>indexOf</c>, <c>arrayCount</c>, <c>arrayFirstIndex</c> and
    /// <c>arrayLastIndex</c> return <c>UInt64</c>/<c>UInt32</c> natively; cast them to <c>Int64</c> so
    /// the result matches the declared CLR <c>long</c>.
    /// </summary>
    public override string MakeArrayFunction(string name, string call) =>
        name is "length" or "indexOf" or "arrayCount" or "arrayFirstIndex" or "arrayLastIndex" ? $"toInt64({call})" : call;

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
    public override IIifRenderer Iif => ClickHouseIifRenderer.Instance;

    /// <summary>ClickHouse implements the multi-branch conditional <c>multiIf(cond1, then1, ..., else)</c>.</summary>
    public override IMultiIfRenderer MultiIf => _multiIf;

    // ClickHouse has dateTrunc(unit, datetime) and the addYears/.../addSeconds family plus
    // toLastDayOfMonth(date).
    public override bool SupportsDateTrunc => true;
    public override bool SupportsDateArithmetic => true;

    /// <summary>
    /// ClickHouse exposes the native date conversion/truncation surface (<c>toDate</c>/<c>toDateTime</c>/
    /// <c>toDate32</c>, the <c>toYear</c>/... part accessors, <c>toStartOf*</c>, <c>toMonday</c>,
    /// <c>toYYYYMM</c>/<c>toYYYYMMDD</c>, <c>toUnixTimestamp</c>).
    /// </summary>
    public override IDateConversionRenderer DateConversion => ClickHouseDateConversionRenderer.Instance;

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
    /// <summary>ClickHouse implements the distinct-count <c>uniq*</c> aggregates.</summary>
    public override IUniqAggregateRenderer UniqAggregates => _uniqAggregates;

    /// <summary>ClickHouse implements the parameterised <c>quantile(level)(value)</c> family and <c>median</c>.</summary>
    public override IQuantileAggregateRenderer QuantileAggregates => _quantileAggregates;

    /// <summary>ClickHouse implements the parameterised <c>topK(N)(value)</c>/<c>topKWeighted(N)(value, weight)</c> aggregates.</summary>
    public override ITopKAggregateRenderer TopKAggregates => _topKAggregates;

    /// <summary>ClickHouse implements the <c>any</c>/<c>anyLast</c> row-picking aggregates.</summary>
    public override bool SupportsAnyAggregates => true;

    /// <summary>ClickHouse renders the arbitrary-value aggregate as <c>any(x)</c>.</summary>
    public override bool SupportsAnyValueAggregate => true;

    /// <summary>ClickHouse implements the <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>ClickHouse implements the <c>nth_value(value, n)</c> window function.</summary>
    public override bool SupportsNthValue => true;

    /// <summary>
    /// ClickHouse implements the frame-respecting offset window functions <c>lagInFrame</c>/<c>leadInFrame</c>
    /// (unlike the standard <c>lag</c>/<c>lead</c>, which ignore the window frame).
    /// </summary>
    public override bool SupportsInFrameWindowFunctions => true;

    /// <summary>ClickHouse declares named windows (<c>WINDOW w AS (...)</c>) and references them with <c>OVER w</c>.</summary>
    public override bool SupportsNamedWindows => true;

    /// <summary>ClickHouse supports the <c>GROUPS</c> window frame unit.</summary>
    public override bool SupportsWindowFrameGroups => true;

    /// <summary>
    /// ClickHouse implements the <c>JSONExtract*</c>/<c>JSONHas</c>/<c>visitParamExtract*</c> string-JSON
    /// family and the JSONPath scalars <c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>.
    /// </summary>
    public override bool SupportsJsonExtract => true;

    /// <summary>
    /// ClickHouse spells the string-JSON extractors in camel case and the native-JSON functions as
    /// <c>JSONAllPaths</c>/<c>JSONAllPathsWithTypes</c>/<c>toJSONString</c>; <c>JSONLength</c> returns
    /// <c>UInt64</c>, which the row reader cannot materialise, so it is cast to <c>Int64</c>.
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
            "json_all_paths" => "JSONAllPaths",
            "json_all_paths_with_types" => "JSONAllPathsWithTypes",
            "to_json_string" => "toJSONString",
            _ => name
        };

        var call = $"{function}({string.Join(", ", args)})";
        return name == "json_length" ? $"toInt64({call})" : call;
    }

    /// <summary>
    /// ClickHouse renders <c>currentUser()</c>, <c>currentDatabase()</c> and <c>version()</c>; it has no
    /// session-user or schema concept, so <c>session_user</c>/<c>current_schema</c> stay unavailable.
    /// </summary>
    public override ISessionInfoFunctions SessionInfoFunctions => ClickHouseSessionInfoFunctions.Instance;

    /// <summary>ClickHouse renders both UUID generators.</summary>
    public override IUuidGenerators UuidGenerators => ClickHouseUuidGenerators.Instance;

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

    // count()/countIf() also return UInt64; cast to the CLR type the function declares (int vs long).
    public override bool WrapsCountResult => true;

    public override string WrapCount(string countExpression, bool big) =>
        big ? $"toInt64({countExpression})" : $"toInt32({countExpression})";

    /// <summary>ClickHouse implements the <c>windowFunnel</c>/<c>retention</c>/<c>sequenceMatch</c> aggregates.</summary>
    public override ISequenceAggregateRenderer SequenceAggregates => ClickHouseSequenceAggregateRenderer.Instance;

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

    /// <summary>
    /// ClickHouse provides the <c>numbers</c>/<c>numbers_mt</c>, <c>zeros</c>/<c>zeros_mt</c> and
    /// <c>generateRandom</c> table functions plus the server/cluster table functions <c>url</c>,
    /// <c>s3</c>, <c>file</c>, <c>remote</c>, <c>remoteSecure</c>, <c>cluster</c> and
    /// <c>clusterAllReplicas</c>.
    /// </summary>
    public override bool SupportsTableFunction(string name) =>
        name is "numbers" or "numbers_mt" or "zeros" or "zeros_mt" or "generateRandom"
            or "url" or "s3" or "file" or "remote" or "remoteSecure" or "cluster" or "clusterAllReplicas";

    // The fixed structure the built-in generate_random()/generate_random(seed) map to
    // (ClickHouse's own no-argument generateRandom has a dynamic, random schema).
    private const string GenerateRandomStructure = "'id UInt64, value Float64, name String'";

    /// <summary>
    /// <c>numbers</c>/<c>numbers_mt</c> expose an unsigned <c>UInt64 number</c> column; cast it to
    /// <c>Int64</c> through a wrapping subquery to match <c>INumbersRow.Value</c>. The built-in
    /// <c>generateRandom</c> gets its fixed structure injected here and its <c>id</c> column cast the
    /// same way.
    /// </summary>
    public override string WrapTableFunction(string name, string call)
    {
        if (name is "numbers" or "numbers_mt")
            return $"(select toInt64(number) as number from {call})";

        if (name == "generateRandom")
        {
            var inner = call["generateRandom(".Length..^1];
            var args = inner.Length == 0 ? GenerateRandomStructure : $"{GenerateRandomStructure}, {inner}";
            return $"(select toInt64(id) as id, value, name from generateRandom({args}))";
        }

        return call;
    }

    /// <summary>ClickHouse implements the distributed <c>GLOBAL IN</c> predicate.</summary>
    public override bool SupportsGlobalPredicates => true;

    /// <summary>ClickHouse implements <c>LIMIT n BY expr</c>.</summary>
    public override ILimitByRenderer LimitBy => ClickHouseLimitByRenderer.Instance;

    /// <summary>ClickHouse implements the <c>FINAL</c> table modifier.</summary>
    public override bool SupportsFinal => true;

    /// <summary>ClickHouse implements the <c>SAMPLE</c> table modifier.</summary>
    public override bool SupportsSample => true;

    /// <summary>ClickHouse implements the <c>PREWHERE</c> clause.</summary>
    public override bool SupportsPreWhere => true;

    /// <summary>ClickHouse implements the <c>ARRAY JOIN</c> clause.</summary>
    public override IArrayJoinRenderer ArrayJoinClause => ClickHouseArrayJoinRenderer.Instance;

    /// <summary>ClickHouse implements the trailing <c>SETTINGS</c> clause.</summary>
    public override bool SupportsSettings => true;

    public override string MakeGrouping(string columns, GroupingType groupingType) => groupingType switch
    {
        GroupingType.Rollup => $"{columns} with rollup",
        GroupingType.Cube => $"{columns} with cube",
        _ => columns
    };

    // ClickHouse quotes identifiers with backticks; single-quoted aliases are syntax errors.
    public override string Escape(string keyword) => "`" + keyword + "`";

    /// <summary>ClickHouse quotes a physical identifier with backticks, doubling an embedded backtick.</summary>
    public override string QuoteIdentifier(string name) => "`" + name.Replace("`", "``") + "`";

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
    /// ClickHouse spells the date parts through the <c>toXxx</c> family; the accessors return
    /// UInt8/UInt16, which the row reader cannot read through GetInt32, so they are cast. The
    /// normalised parts follow the cross-provider <c>extract</c> contract: <c>week</c> is ISO 8601,
    /// <c>dow</c> is 0=Sunday..6=Saturday (native <c>toDayOfWeek</c> is ISO 1..7, hence <c>% 7</c>) and
    /// <c>isodow</c> is 1=Monday..7=Sunday.
    /// </summary>
    public override string MakeDatePart(string part, string value) => part switch
    {
        "year" => $"toInt32(toYear({value}))",
        "quarter" => $"toInt32(toQuarter({value}))",
        "month" => $"toInt32(toMonth({value}))",
        "day" => $"toInt32(toDayOfMonth({value}))",
        "dow" => $"toInt32(toDayOfWeek({value}) % 7)",
        "isodow" => $"toInt32(toDayOfWeek({value}))",
        "doy" => $"toInt32(toDayOfYear({value}))",
        "week" => $"toInt32(toISOWeek({value}))",
        "hour" => $"toInt32(toHour({value}))",
        "minute" => $"toInt32(toMinute({value}))",
        "second" => $"toInt32(toSecond({value}))",
        "epoch" => $"toFloat64(toUnixTimestamp({value}))",
        _ => base.MakeDatePart(part, value)
    };

    /// <summary>ClickHouse additionally accepts the normalised weekdays and epoch.</summary>
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
        "top_k" => "topK",
        "top_k_weighted" => "topKWeighted",
        "any_agg" => "any",
        "any_last" => "anyLast",
        "group_array" => "groupArray",
        "group_uniq_array" => "groupUniqArray",
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

internal sealed class ClickHouseIifRenderer : IIifRenderer
{
    public static readonly ClickHouseIifRenderer Instance = new();

    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"if({condition}, {whenTrue}, {whenFalse})";
}

internal sealed class ClickHouseSessionInfoFunctions : ISessionInfoFunctions
{
    public static readonly ClickHouseSessionInfoFunctions Instance = new();

    public bool Supports(string name) => name is "current_user" or "current_database" or "version";

    public string Render(string name) => name switch
    {
        "current_user" => "currentUser()",
        "current_database" => "currentDatabase()",
        "version" => "version()",
        _ => throw new NotSupportedException($"The {name} session information function is not supported by ClickHouse.")
    };
}

internal sealed class ClickHouseUuidGenerators : IUuidGenerators
{
    public static readonly ClickHouseUuidGenerators Instance = new();

    public bool Supports(string name) => name is "gen_random_uuid" or "uuidv7";

    public string Render(string name) => name switch
    {
        "gen_random_uuid" => "generateUUIDv4()",
        "uuidv7" => "generateUUIDv7()",
        _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by ClickHouse.")
    };
}

internal sealed class ClickHouseLimitByRenderer : ILimitByRenderer
{
    public static readonly ClickHouseLimitByRenderer Instance = new();

    public string Render(int limit, int offset, IReadOnlyList<string> columns)
    {
        var prefix = offset > 0 ? $"{offset}, " : string.Empty;
        return $"limit {prefix}{limit} by {string.Join(", ", columns)}";
    }
}

internal sealed class ClickHouseStringSplitRenderer : IStringSplitRenderer
{
    public static readonly ClickHouseStringSplitRenderer Instance = new();

    public string Render(string separator, string value) =>
        $"splitByChar({separator}, {value})";
}

internal sealed class ClickHouseMultiIfRenderer(ClickHouseDialect dialect) : IMultiIfRenderer
{
    public string Render(IReadOnlyList<string> arguments, Type resultType)
    {
        var call = $"multiIf({string.Join(", ", arguments)})";

        return resultType == typeof(byte) || resultType == typeof(short) || resultType == typeof(int)
            || resultType == typeof(long) || resultType == typeof(float) || resultType == typeof(double)
            || resultType == typeof(decimal)
            ? $"cast({call} as {dialect.MakeTypeName(resultType)})"
            : call;
    }
}

internal sealed class ClickHouseUniqAggregateRenderer(ClickHouseDialect dialect) : IUniqAggregateRenderer
{
    // The uniq* aggregates return UInt64; cast to Int64 to match the declared long.
    public string Render(string name, string argument) =>
        $"toInt64({dialect.MakeAggregate(name)}({argument}))";
}

internal sealed class ClickHouseQuantileAggregateRenderer(ClickHouseDialect dialect) : IQuantileAggregateRenderer
{
    // quantileTiming returns Float32 and quantileExact keeps the input type; toFloat64 pins every
    // variant to the CLR double the methods declare.
    public string Render(string name, string level, string value) =>
        $"toFloat64({dialect.MakeAggregate(name)}({level})({value}))";

    // quantiles returns Array(Float64) for numeric input, which already matches the declared double[].
    public string RenderLevels(string name, string levels, string value) =>
        $"{dialect.MakeAggregate(name)}({levels})({value})";

    // ClickHouse's median is quantile(0.5); cast it so it materialises as a CLR double.
    public string RenderMedian(string value) => $"toFloat64(median({value}))";
}

internal sealed class ClickHouseTopKAggregateRenderer(ClickHouseDialect dialect) : ITopKAggregateRenderer
{
    // topK/topKWeighted return the value type, which the caller declares as T[].
    public string Render(string name, string k, string value) =>
        $"{dialect.MakeAggregate(name)}({k})({value})";

    public string RenderWeighted(string name, string k, string value, string weight) =>
        $"{dialect.MakeAggregate(name)}({k})({value}, {weight})";
}

internal sealed class ClickHouseSequenceAggregateRenderer : ISequenceAggregateRenderer
{
    public static readonly ClickHouseSequenceAggregateRenderer Instance = new();

    public string Render(string name, string? parameters, string arguments)
    {
        var function = name switch
        {
            "window_funnel" => "windowFunnel",
            "sequence_match" => "sequenceMatch",
            "retention" => "retention",
            _ => name
        };

        var call = parameters is null
            ? $"{function}({arguments})"
            : $"{function}({parameters})({arguments})";

        return name is "window_funnel" or "sequence_match" ? $"toInt32({call})" : call;
    }
}

internal sealed class ClickHouseArrayJoinRenderer : IArrayJoinRenderer
{
    public static readonly ClickHouseArrayJoinRenderer Instance = new();

    public string Render(ArrayJoinKind kind, IReadOnlyList<string> expressions)
    {
        var keyword = kind == ArrayJoinKind.Left ? " left array join " : " array join ";
        return keyword + string.Join(", ", expressions);
    }
}

internal sealed class ClickHouseDateConversionRenderer : IDateConversionRenderer
{
    public static readonly ClickHouseDateConversionRenderer Instance = new();

    public string Render(string name, IReadOnlyList<string> args)
    {
        var function = name switch
        {
            "to_date" => "toDate",
            "to_date_time" => "toDateTime",
            "to_date32" => "toDate32",
            "to_day_of_week" => "toDayOfWeek",
            "to_start_of_year" => "toStartOfYear",
            "to_start_of_quarter" => "toStartOfQuarter",
            "to_start_of_month" => "toStartOfMonth",
            "to_start_of_week" => "toStartOfWeek",
            "to_start_of_day" => "toStartOfDay",
            "to_start_of_hour" => "toStartOfHour",
            "to_start_of_minute" => "toStartOfMinute",
            "to_start_of_second" => "toStartOfSecond",
            "to_monday" => "toMonday",
            "to_yyyymm" => "toYYYYMM",
            "to_yyyymmdd" => "toYYYYMMDD",
            "to_unix_timestamp" => "toUnixTimestamp",
            _ => name
        };

        var call = $"{function}({string.Join(", ", args)})";

        return name switch
        {
            "to_day_of_week" or "to_yyyymm" or "to_yyyymmdd" => $"toInt32({call})",
            "to_unix_timestamp" => $"toInt64({call})",
            _ => call
        };
    }
}
