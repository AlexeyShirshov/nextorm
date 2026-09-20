using System.Text;
using NextORM.Core;

namespace NextORM.Postgres;

/// <summary>
/// PostgreSQL dialect: <c>||</c> concatenation, <c>@name</c> parameters, double-quoted identifiers,
/// <c>limit/offset</c> paging and the aggregate name mapping (<c>stdev</c> -> <c>stddev</c>, ...).
/// </summary>
public sealed class PostgresDialect : SqlDialectBase
{
    public static readonly PostgresDialect Instance = new();

    public override string ConcatStringOperator => "||";

    public override string MakeParam(string name) => $"@{name}";

    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    public override string MakeBool(bool v) => v ? "true" : "false";

    /// <summary>PostgreSQL's text type is <c>text</c> (the base maps <see cref="string"/> to the CLR name).</summary>
    public override string MakeTypeName(Type type) => type == typeof(string) ? "text" : base.MakeTypeName(type);

    // PostgreSQL only accepts double-quoted identifiers; single-quoted aliases are a syntax error.
    public override string Escape(string keyword) => "\"" + keyword + "\"";

    public override string MakeColumnReference(string name) => Escape(name);

    public override bool RequireSubqueryAlias => true;

    // PostgreSQL spells the APPLY surface as CROSS JOIN LATERAL / LEFT JOIN LATERAL ... ON true,
    // which is exactly the SqlDialectBase default.
    public override bool SupportsApply => true;

    // PostgreSQL is the only supported provider that implements INTERSECT ALL / EXCEPT ALL.
    public override bool SupportsIntersectExceptAll => true;

    // PostgreSQL renders the ANSI GROUP BY ROLLUP (...)/CUBE (...) form.
    public override bool SupportsRollup => true;
    public override bool SupportsCube => true;
    public override bool SupportsGroupingSets => true;

    // PostgreSQL has native array types and the any/all quantifiers over arrays.
    public override bool SupportsArrays => true;

    public override bool SupportsTableFunction(string name) =>
        name is "generate_series" or "unnest";

    // PostgreSQL has native json/jsonb types and the associated functions/operators.
    public override bool SupportsJson => true;

    // PostgreSQL accepts the FILTER (WHERE ...) aggregate clause, greatest/least, date_trunc and the
    // string_agg/array_agg aggregate surface.
    public override bool SupportsFilter => true;
    public override bool SupportsGreatestLeast => true;

    /// <summary>PostgreSQL has no <c>iif</c>/<c>if</c> function; it renders the conditional as a <c>CASE</c> expression.</summary>
    public override bool SupportsIif => true;

    /// <summary>PostgreSQL has no <c>iif</c>/<c>if</c> function; it renders the conditional as a <c>CASE</c> expression.</summary>
    public override string MakeIif(string condition, string whenTrue, string whenFalse) =>
        $"case when {condition} then {whenTrue} else {whenFalse} end";

    /// <summary>PostgreSQL supports the ANSI <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>PostgreSQL supports <c>nth_value(value, n)</c> as a window function.</summary>
    public override bool SupportsNthValue => true;
    public override bool SupportsDateTrunc => true;
    public override bool SupportsDateArithmetic => true;
    public override bool SupportsStringArrayAggregates => true;

    /// <summary>PostgreSQL renders every date part through <c>extract</c>, including the ISO week and dow forms.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    /// <summary>PostgreSQL's <c>extract(epoch ...)</c> returns numeric, so it is cast to double precision.</summary>
    public override string MakeDatePart(string part, string value) =>
        part == "epoch" ? $"cast(extract(epoch from {value}) as double precision)" : base.MakeDatePart(part, value);

    // PostgreSQL full-text search matches a tsvector against a tsquery; contains/freetext differ in
    // how the search string is parsed (plain terms vs. web-search syntax).
    public override bool SupportsFullText => true;

    public override string MakeFullText(string functionName, string column, string search) =>
        functionName == "freetext"
            ? $"to_tsvector({column}) @@ websearch_to_tsquery({search})"
            : $"to_tsvector({column}) @@ plainto_tsquery({search})";

    /// <summary>PostgreSQL supports the <c>SELECT DISTINCT ON (expr, ...)</c> modifier.</summary>
    public override bool SupportsDistinctOn => true;

    /// <summary>PostgreSQL renders the <c>distinct on (...)</c> prefix in place of a plain <c>distinct</c>.</summary>
    public override string MakeDistinctOn(IReadOnlyList<string> columns) =>
        "distinct on (" + string.Join(", ", columns) + ") ";

    /// <summary>PostgreSQL supports the <c>TABLESAMPLE</c> table modifier.</summary>
    public override bool SupportsTableSample => true;

    /// <summary>PostgreSQL supports both the <c>SYSTEM</c> and <c>BERNOULLI</c> sampling methods.</summary>
    public override bool SupportsTableSampleMethod(TableSampleMethod method) => true;

    /// <summary>PostgreSQL renders <c>tablesample method (percent) [repeatable (seed)]</c>.</summary>
    public override string MakeTableSample(TableSampleMethod method, double percent, double? seed)
    {
        var text = " tablesample " + method.ToString().ToLowerInvariant()
            + " (" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        return seed is { } value
            ? text + " repeatable (" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
            : text;
    }

    // PostgreSQL is the reference provider for the extended scalar function library and the
    // bool/bit/statistical/ordered-set aggregate surface.
    public override bool SupportsExtendedScalarFunctions => true;

    /// <summary>PostgreSQL has the standalone session random seed <c>setseed</c>.</summary>
    public override bool SupportsRandomSeed => true;

    /// <summary>PostgreSQL renders the <c>digest</c> (pgcrypto) and <c>sha256</c> (core) hash functions.</summary>
    public override bool SupportsCryptoFunctions => true;

    /// <summary>PostgreSQL is the only provider with the native <c>tsvector</c>/<c>tsquery</c> text-search surface.</summary>
    public override bool SupportsTextSearchFunctions => true;

    /// <summary>PostgreSQL renders the whole session/information family.</summary>
    public override bool SupportsSessionInfoFunctions => true;

    /// <summary>PostgreSQL supports all five session/information functions.</summary>
    public override bool SupportsSessionInfoFunction(string name) =>
        name is "current_user" or "session_user" or "current_schema" or "current_database" or "version";

    /// <summary>PostgreSQL uses the keyword forms for the current/session user and schema, and calls for the rest.</summary>
    public override string MakeSessionInfoFunction(string name) => name switch
    {
        "current_user" or "session_user" or "current_schema" => name,
        "current_database" => "current_database()",
        "version" => "version()",
        _ => base.MakeSessionInfoFunction(name)
    };

    /// <summary>PostgreSQL renders both UUID generators through the core functions.</summary>
    public override bool SupportsUuidGenerators => true;

    /// <summary>PostgreSQL supports the random v4 generator (<c>gen_random_uuid</c>, PG13+) and v7 (<c>uuidv7</c>, PG18+).</summary>
    public override bool SupportsUuidGenerator(string name) => name is "gen_random_uuid" or "uuidv7";

    /// <summary>PostgreSQL calls the UUID generators as functions.</summary>
    public override string MakeUuidGenerator(string name) => name switch
    {
        "gen_random_uuid" => "gen_random_uuid()",
        "uuidv7" => "uuidv7()",
        _ => base.MakeUuidGenerator(name)
    };

    public override bool SupportsBooleanAggregates => true;
    public override bool SupportsBitAggregates => true;
    public override bool SupportsStatisticalAggregates => true;
    public override bool SupportsRegressionAggregates => true;
    public override bool SupportsOrderedAggregates => true;

    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev",
        "stdevp" => "stddev_pop",
        "var" => "variance",
        "varp" => "var_pop",
        _ => name
    };

    // PostgreSQL's log() is base 10; the natural logarithm (Math.Log) is ln().
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        name == "log" && args.Count == 1
            ? $"ln({args[0]})"
            : base.MakeMathFunction(name, args);

    /// <summary>
    /// PostgreSQL's two-argument <c>round</c> only accepts <c>numeric</c>, so a double precision/real
    /// first argument is cast; decimal/integer already resolve to <c>round(numeric, integer)</c>.
    /// </summary>
    public override string MakeMathFunction(string name, IReadOnlyList<string> args, IReadOnlyList<Type> argTypes) =>
        name == "round" && args.Count == 2 && IsFloatingPoint(argTypes[0])
            ? $"round(({args[0]})::numeric, {args[1]})"
            : base.MakeMathFunction(name, args, argTypes);

    private static bool IsFloatingPoint(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(double) || underlying == typeof(float);
    }

    protected override string MakeStringPosition(string value, string substring) =>
        $"strpos({value}, {substring})";

    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    protected override string MakeStringReverse(string value) => $"reverse({value})";

    public override string MakeStuff(string value, string start, string? count, string newValue) =>
        count is null
            ? MakeSubstring(value, "0", start)
            : $"overlay({value} placing {newValue} from {start} + 1 for {count})";

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        // WITH TIES is only expressible through the FETCH form (LIMIT has no WITH TIES variant).
        if (paging.HasWithTies)
        {
            if (paging.Offset > 0)
                sqlBuilder.Append("offset ").Append(paging.Offset).Append(' ');

            sqlBuilder.Append("fetch first ").Append(paging.Limit).Append(" rows with ties");
            return;
        }

        // PostgreSQL uses "limit N offset M"; OFFSET may appear on its own, but LIMIT must come first.
        if (paging.Limit > 0)
            sqlBuilder.Append("limit ").Append(paging.Limit);

        if (paging.Offset > 0)
        {
            if (paging.Limit > 0)
                sqlBuilder.Append(' ');

            sqlBuilder.Append("offset ").Append(paging.Offset);
        }
    }

    /// <summary>PostgreSQL supports <c>FETCH FIRST ... WITH TIES</c>.</summary>
    public override bool SupportsWithTies => true;

    /// <summary>PostgreSQL supports the trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause.</summary>
    public override bool SupportsLocking => true;

    /// <summary>PostgreSQL renders <c>for update</c>/<c>for share</c>.</summary>
    public override string MakeLock(LockMode mode) =>
        mode == LockMode.Share ? " for share" : " for update";
}
