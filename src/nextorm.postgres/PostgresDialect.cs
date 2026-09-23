using System.Text;
using NextORM.Core;

namespace NextORM.Postgres;

/// <summary>
/// PostgreSQL dialect: <c>||</c> concatenation, <c>@name</c> parameters, double-quoted identifiers,
/// <c>limit/offset</c> paging and the aggregate name mapping (<c>stdev</c> -> <c>stddev</c>, ...).
/// </summary>
public sealed class PostgresDialect : SqlDialectBase
{
    /// <summary>Gets the shared PostgreSQL dialect instance.</summary>
    public static readonly PostgresDialect Instance = new();

    /// <inheritdoc/>
    public override string ConcatStringOperator => "||";

    /// <summary>PostgreSQL has a native row-value type: <c>ROW(a, b)</c> with <c>(row).fN</c> access.</summary>
    public override ITupleRenderer? Tuple => PostgresTupleRenderer.Instance;

    /// <summary>PostgreSQL supports <c>INSERT ... RETURNING &lt;column&gt;</c>.</summary>
    public override bool SupportsReturning => true;

    /// <summary>PostgreSQL allows a data-modifying statement (<c>INSERT ... RETURNING</c>) as a CTE body.</summary>
    public override bool SupportsDataModifyingCtes => true;

    /// <summary>PostgreSQL expresses a key upsert as <c>INSERT ... ON CONFLICT (&lt;keys&gt;) DO UPDATE SET ...</c> (9.5+).</summary>
    public override bool SupportsOnConflict => true;

    /// <summary>PostgreSQL reads the last generated identity of the session through the <c>lastval()</c> function.</summary>
    public override bool SupportsIdentityFunction => true;

    /// <summary>PostgreSQL supports <c>INSERT ... DEFAULT VALUES</c> for an all-defaults row.</summary>
    public override bool SupportsDefaultValues => true;

    /// <summary>PostgreSQL accepts <c>DEFAULT</c> as a value in the <c>VALUES</c> list.</summary>
    public override bool SupportsColumnDefault => true;

    /// <summary>Renders the identity-function query <c>select lastval()</c>.</summary>
    public override string MakeIdentityFunction(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "select lastval()");

    /// <inheritdoc/>
    public override string MakeParam(string name) => $"@{name}";

    /// <inheritdoc/>
    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    /// <inheritdoc/>
    public override string MakeBool(bool v) => v ? "true" : "false";

    /// <summary>PostgreSQL's text type is <c>text</c> (the base maps <see cref="string"/> to the CLR name).</summary>
    public override string MakeTypeName(Type type) => type == typeof(string) ? "text" : base.MakeTypeName(type);

    // PostgreSQL only accepts double-quoted identifiers; single-quoted aliases are a syntax error.
    /// <inheritdoc/>
    public override string Escape(string keyword) => "\"" + keyword + "\"";

    /// <inheritdoc/>
    public override string MakeColumnReference(string name) => Escape(name);

    /// <inheritdoc/>
    public override bool RequireSubqueryAlias => true;

    // PostgreSQL spells the APPLY surface as CROSS JOIN LATERAL / LEFT JOIN LATERAL ... ON true,
    // which is exactly the SqlDialectBase default.
    /// <inheritdoc/>
    public override bool SupportsApply => true;

    /// <summary>PostgreSQL supports a raw SQL derived table (<c>FROM (&lt;sql&gt;) AS alias</c>).</summary>
    public override bool SupportsRawSqlSource => true;

    /// <summary>
    /// PostgreSQL has no statement hints in the core server; the optional <c>pg_hint_plan</c> extension
    /// reads an inline <c>/*+ ... */</c> comment. nextorm renders that comment unconditionally, because
    /// on a server without the extension it is an ordinary comment.
    /// </summary>
    public override bool SupportsQueryHints => true;

    /// <summary>
    /// Renders the statement-level hints as a <c>/*+ ... */</c> comment immediately after the top-level
    /// <c>select</c> (the position <c>pg_hint_plan</c> reads; a <c>WITH</c> prefix and subqueries are
    /// skipped). PostgreSQL has no <c>maxrecursion</c> option, so <paramref name="maxRecursionOption"/>
    /// is ignored.
    /// </summary>
    public override string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var depth = 0;
        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c is '\'' or '"' or '`')
            {
                var quote = c;
                for (i++; i < sql.Length; i++)
                {
                    if (sql[i] != quote) continue;
                    if (quote == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'') { i++; continue; }
                    break;
                }

                continue;
            }

            if (c == '(') { depth++; continue; }
            if (c == ')') { if (depth > 0) depth--; continue; }
            if (depth != 0 || !MatchesSelect(sql, i)) continue;

            return sql.Insert(i + "select".Length, $" /*+ {string.Join(" ", hints)} */");
        }

        return sql;
    }

    private static bool MatchesSelect(string sql, int index)
    {
        const string keyword = "select";
        if (index + keyword.Length > sql.Length) return false;
        if (string.Compare(sql, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
        if (index > 0 && IsIdentifierChar(sql[index - 1])) return false;
        var end = index + keyword.Length;
        return end >= sql.Length || !IsIdentifierChar(sql[end]);
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // PostgreSQL is the only supported provider that implements INTERSECT ALL / EXCEPT ALL.
    /// <inheritdoc/>
    public override bool SupportsIntersectExceptAll => true;

    // PostgreSQL renders the ANSI GROUP BY ROLLUP (...)/CUBE (...) form.
    /// <inheritdoc/>
    public override bool SupportsRollup => true;
    /// <inheritdoc/>
    public override bool SupportsCube => true;
    /// <inheritdoc/>
    public override bool SupportsGroupingSets => true;

    // PostgreSQL has native array types and the any/all quantifiers over arrays.
    /// <inheritdoc/>
    public override bool SupportsArrays => true;

    /// <inheritdoc/>
    public override bool SupportsTableFunction(string name) =>
        name is "generate_series" or "unnest"
            or "regexp_matches" or "regexp_split_to_table"
            or "jsonb_array_elements" or "jsonb_array_elements_text"
            or "jsonb_each" or "jsonb_each_text" or "jsonb_object_keys"
            or "jsonb_path_query" or "ts_stat";

    /// <summary>
    /// PostgreSQL names the only output column of a scalar set-returning function after the function,
    /// but aliasing the function in <c>FROM</c> renames that column to the alias. nextorm always
    /// aliases a derived source (<see cref="RequireSubqueryAlias"/>), so those functions are wrapped
    /// in a one-column subquery that restores the function-named column
    /// (<c>select generate_series from generate_series(...)</c>). Functions with an explicit output
    /// column (<c>value</c>, <c>key</c>/<c>value</c>, <c>word</c>/<c>ndoc</c>/<c>nentry</c>) keep
    /// their names and are emitted unchanged.
    /// </summary>
    public override string WrapTableFunction(string name, string call) =>
        name is "generate_series" or "unnest" or "regexp_matches" or "regexp_split_to_table"
            or "jsonb_object_keys" or "jsonb_path_query"
            ? $"(select {name} from {call})"
            : call;

    // PostgreSQL has native json/jsonb types and the associated functions/operators.
    /// <inheritdoc/>
    public override bool SupportsJson => true;

    // PostgreSQL accepts the FILTER (WHERE ...) aggregate clause, greatest/least, date_trunc and the
    // string_agg/array_agg aggregate surface.
    /// <inheritdoc/>
    public override bool SupportsFilter => true;
    /// <inheritdoc/>
    public override bool SupportsGreatestLeast => true;

    /// <summary>PostgreSQL has no <c>iif</c>/<c>if</c> function; it renders the conditional as a <c>CASE</c> expression.</summary>
    public override IIifRenderer Iif => PostgresIifRenderer.Instance;

    /// <summary>PostgreSQL supports the ANSI <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>PostgreSQL supports <c>nth_value(value, n)</c> as a window function.</summary>
    public override bool SupportsNthValue => true;

    /// <summary>PostgreSQL declares named windows (<c>WINDOW w AS (...)</c>) and references them with <c>OVER w</c>.</summary>
    public override bool SupportsNamedWindows => true;

    /// <summary>PostgreSQL 11+ supports the <c>GROUPS</c> window frame unit.</summary>
    public override bool SupportsWindowFrameGroups => true;

    /// <summary>PostgreSQL supports the frame <c>EXCLUDE CURRENT ROW</c>/<c>GROUP</c>/<c>TIES</c>/<c>NO OTHERS</c> clause.</summary>
    public override bool SupportsWindowFrameExclusion => true;
    /// <inheritdoc/>
    public override bool SupportsDateTrunc => true;
    /// <inheritdoc/>
    public override bool SupportsDateArithmetic => true;
    /// <inheritdoc/>
    public override bool SupportsStringArrayAggregates => true;

    /// <summary>PostgreSQL renders every date part through <c>extract</c>, including the ISO week and dow forms.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    /// <summary>PostgreSQL's <c>extract(epoch ...)</c> returns numeric, so it is cast to double precision.</summary>
    public override string MakeDatePart(string part, string value) =>
        part == "epoch" ? $"cast(extract(epoch from {value}) as double precision)" : base.MakeDatePart(part, value);

    // PostgreSQL full-text search matches a tsvector against a tsquery; contains/freetext differ in
    // how the search string is parsed (plain terms vs. web-search syntax).
    /// <inheritdoc/>
    public override bool SupportsFullText => true;

    /// <inheritdoc/>
    public override string MakeFullText(string functionName, string column, string search) =>
        functionName == "freetext"
            ? $"to_tsvector({column}) @@ websearch_to_tsquery({search})"
            : $"to_tsvector({column}) @@ plainto_tsquery({search})";

    /// <summary>PostgreSQL supports the <c>SELECT DISTINCT ON (expr, ...)</c> modifier.</summary>
    public override IDistinctOnRenderer DistinctOn => PostgresDistinctOnRenderer.Instance;

    /// <summary>PostgreSQL supports the <c>TABLESAMPLE</c> table modifier (both <c>SYSTEM</c> and <c>BERNOULLI</c>).</summary>
    public override ITableSampleMethods TableSample => PostgresTableSampleMethods.Instance;

    // PostgreSQL is the reference provider for the extended scalar function library and the
    // bool/bit/statistical/ordered-set aggregate surface.
    /// <inheritdoc/>
    public override bool SupportsExtendedScalarFunctions => true;

    /// <summary>PostgreSQL has the standalone session random seed <c>setseed</c>.</summary>
    public override bool SupportsRandomSeed => true;

    /// <summary>PostgreSQL renders the <c>digest</c> (pgcrypto) and <c>sha256</c> (core) hash functions.</summary>
    public override bool SupportsCryptoFunctions => true;

    /// <summary>PostgreSQL is the only provider with the native <c>tsvector</c>/<c>tsquery</c> text-search surface.</summary>
    public override bool SupportsTextSearchFunctions => true;

    /// <summary>PostgreSQL renders the whole session/information family.</summary>
    public override ISessionInfoFunctions SessionInfoFunctions => PostgresSessionInfoFunctions.Instance;

    /// <summary>PostgreSQL renders both UUID generators through the core functions.</summary>
    public override IUuidGenerators UuidGenerators => PostgresUuidGenerators.Instance;

    /// <inheritdoc/>
    public override bool SupportsBooleanAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsBitAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsStatisticalAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsRegressionAggregates => true;
    /// <inheritdoc/>
    public override bool SupportsOrderedAggregates => true;

    /// <inheritdoc/>
    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev",
        "stdevp" => "stddev_pop",
        "var" => "variance",
        "varp" => "var_pop",
        _ => name
    };

    // PostgreSQL's log() is base 10; the natural logarithm (Math.Log) is ln().
    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override string MakeStringPosition(string value, string substring) =>
        $"strpos({value}, {substring})";

    /// <inheritdoc/>
    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    /// <inheritdoc/>
    protected override string MakeStringReverse(string value) => $"reverse({value})";

    /// <inheritdoc/>
    public override string MakeStuff(string value, string start, string? count, string newValue) =>
        count is null
            ? MakeSubstring(value, "0", start)
            : $"overlay({value} placing {newValue} from {start} + 1 for {count})";

    /// <inheritdoc/>
    public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
    {
        // WITH TIES is only expressible through the FETCH form (LIMIT has no WITH TIES variant).
        if (paging.HasWithTies)
        {
            if (paging.Offset > 0)
                sqlBuilder.Append(Kw(keywordCase, "offset ")).Append(paging.Offset).Append(' ');

            sqlBuilder.Append(Kw(keywordCase, "fetch first ")).Append(paging.Limit).Append(Kw(keywordCase, " rows with ties"));
            return;
        }

        // PostgreSQL uses "limit N offset M"; OFFSET may appear on its own, but LIMIT must come first.
        if (paging.Limit > 0)
            sqlBuilder.Append(Kw(keywordCase, "limit ")).Append(paging.Limit);

        if (paging.Offset > 0)
        {
            if (paging.Limit > 0)
                sqlBuilder.Append(' ');

            sqlBuilder.Append(Kw(keywordCase, "offset ")).Append(paging.Offset);
        }
    }

    /// <summary>PostgreSQL supports <c>FETCH FIRST ... WITH TIES</c>.</summary>
    public override bool SupportsWithTies => true;

    /// <summary>PostgreSQL supports the trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause.</summary>
    public override ILockRenderer Lock => PostgresLockRenderer.Instance;
}

internal sealed class PostgresIifRenderer : IIifRenderer
{
    public static readonly PostgresIifRenderer Instance = new();

    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"case when {condition} then {whenTrue} else {whenFalse} end";
}

internal sealed class PostgresSessionInfoFunctions : ISessionInfoFunctions
{
    public static readonly PostgresSessionInfoFunctions Instance = new();

    public bool Supports(string name) =>
        name is "current_user" or "session_user" or "current_schema" or "current_database" or "version";

    public string Render(string name) => name switch
    {
        "current_user" or "session_user" or "current_schema" => name,
        "current_database" => "current_database()",
        "version" => "version()",
        _ => throw new NotSupportedException($"The {name} session information function is not supported by PostgreSQL.")
    };
}

internal sealed class PostgresUuidGenerators : IUuidGenerators
{
    public static readonly PostgresUuidGenerators Instance = new();

    public bool Supports(string name) => name is "gen_random_uuid" or "uuidv7";

    public string Render(string name) => name switch
    {
        "gen_random_uuid" => "gen_random_uuid()",
        "uuidv7" => "uuidv7()",
        _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by PostgreSQL.")
    };
}

internal sealed class PostgresDistinctOnRenderer : IDistinctOnRenderer
{
    public static readonly PostgresDistinctOnRenderer Instance = new();

    public string Render(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, "distinct on (") + string.Join(", ", columns) + ") ";
}

internal sealed class PostgresTableSampleMethods : ITableSampleMethods
{
    public static readonly PostgresTableSampleMethods Instance = new();

    public bool Supports(TableSampleMethod method) => true;

    public string Render(TableSampleMethod method, double percent, double? seed, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var text = SqlKeywords.Of(keywordCase, " tablesample ") + method.ToString().ToLowerInvariant()
            + " (" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        return seed is { } value
            ? text + SqlKeywords.Of(keywordCase, " repeatable (") + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
            : text;
    }
}

internal sealed class PostgresLockRenderer : ILockRenderer
{
    public static readonly PostgresLockRenderer Instance = new();

    public bool UsesTableHints => false;

    public string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, mode == LockMode.Share ? " for share" : " for update");
}

internal sealed class PostgresTupleRenderer : ITupleRenderer
{
    public static readonly PostgresTupleRenderer Instance = new();

    public string RenderConstructor(IReadOnlyList<string> fields) => "ROW(" + string.Join(", ", fields) + ")";

    public string? RenderElement(string row, int oneBasedIndex) => "(" + row + ").f" + oneBasedIndex;
}
