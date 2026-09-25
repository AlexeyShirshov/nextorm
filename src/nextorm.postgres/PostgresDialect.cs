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

    /// <summary>PostgreSQL has a native bulk path (<c>COPY ... FROM STDIN (FORMAT BINARY)</c>).</summary>
    public override bool SupportsBulkCopy => true;

    /// <summary>PostgreSQL batches through <c>NpgsqlBatch</c>, which wraps the commands in an implicit transaction so they share one backend.</summary>
    public override bool SupportsBatch => true;

    /// <summary>PostgreSQL skips conflicting rows with a trailing <c>ON CONFLICT DO NOTHING</c>.</summary>
    public override bool SupportsOnConflictDoNothing => true;

    /// <summary>PostgreSQL writes an explicit value to a <c>GENERATED ALWAYS AS IDENTITY</c> column with <c>OVERRIDING SYSTEM VALUE</c>.</summary>
    public override string MakeOverridingSystemValue(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " overriding system value");

    /// <summary>PostgreSQL 15+ renders a general <c>MERGE</c>; the server version is a documented requirement.</summary>
    public override bool SupportsMergeStatement => true;

    /// <summary>PostgreSQL <c>MERGE</c> supports a <c>WHEN MATCHED THEN DELETE</c> branch.</summary>
    public override bool SupportsMergeDelete => true;

    /// <summary>PostgreSQL <c>MERGE</c> supports a <c>THEN DO NOTHING</c> branch.</summary>
    public override bool SupportsMergeDoNothing => true;

    /// <summary>PostgreSQL <c>MERGE</c> supports an explicit <c>ON &lt;condition&gt;</c> and <c>WHEN ... AND &lt;condition&gt;</c>.</summary>
    public override bool SupportsMergeConditionalBranches => true;

    /// <summary>PostgreSQL <c>MERGE</c> forbids qualifying a target column in the <c>UPDATE SET</c> list.</summary>
    public override bool SupportsMergeTargetQualification => false;

    /// <summary>PostgreSQL <c>MERGE ... RETURNING</c> must qualify target columns, otherwise they are ambiguous with the source.</summary>
    public override string MakeMergeReturning(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower)
        => Kw(keywordCase, " returning ") + string.Join(", ", columns.Select(static c => "target." + c));

    /// <summary>PostgreSQL reads the last generated identity of the session through the <c>lastval()</c> function.</summary>
    public override bool SupportsIdentityFunction => true;

    /// <summary>PostgreSQL supports <c>INSERT ... DEFAULT VALUES</c> for an all-defaults row.</summary>
    public override bool SupportsDefaultValues => true;

    /// <summary>PostgreSQL accepts <c>DEFAULT</c> as a value in the <c>VALUES</c> list.</summary>
    public override bool SupportsColumnDefault => true;

    /// <summary>PostgreSQL has a native <c>TRUNCATE TABLE</c>.</summary>
    public override bool SupportsTruncate => true;

    /// <summary>PostgreSQL deletes rows based on a join through the <c>USING</c> clause.</summary>
    public override bool SupportsDeleteJoin => true;

    /// <summary>PostgreSQL renders the <c>USING</c> spelling of <see cref="MakeDeleteJoin"/>.</summary>
    public override bool DeleteJoinRequiresUsing => true;

    /// <summary>
    /// Renders the PostgreSQL <c>USING</c> form of a multi-table delete. The joined tables are listed in
    /// <c>USING</c> and the join conditions are folded into the <c>WHERE</c>, because a <c>USING</c> join
    /// condition cannot reference the delete target (the target is aliased in <c>DELETE FROM</c>).
    /// </summary>
    public override string MakeDeleteJoin(
        string target,
        string targetAlias,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var where = string.IsNullOrEmpty(joinConditions)
            ? whereSql
            : string.IsNullOrEmpty(whereSql)
                ? joinConditions
                : joinConditions + Kw(keywordCase, " and ") + whereSql;

        var sql = Kw(keywordCase, "delete from ") + target + Kw(keywordCase, " as ") + targetAlias
            + Kw(keywordCase, " using ") + usingSources;

        return string.IsNullOrEmpty(where) ? sql : sql + Kw(keywordCase, " where ") + where;
    }

    /// <summary>PostgreSQL updates rows based on a join through the <c>FROM</c> clause.</summary>
    public override bool SupportsUpdateJoin => true;

    /// <summary>PostgreSQL renders the <c>FROM</c> spelling of the multi-table update (the target stays out of <c>FROM</c>).</summary>
    public override bool UpdateJoinRequiresFrom => true;

    /// <summary>Renders the identity-function query <c>select lastval()</c>.</summary>
    public override string MakeIdentityFunction(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "select lastval()");

    /// <inheritdoc/>
    public override string MakeParam(string name) => $"@{name}";

    /// <inheritdoc/>
    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    /// <inheritdoc/>
    public override string MakeBool(bool v) => v ? "true" : "false";

    /// <summary>PostgreSQL's text type is <c>text</c>, its duration type is <c>interval</c>, a <see cref="Range{T}"/> maps to the native range type selected by its bound type and a <see cref="Range{T}"/> array maps to the matching multirange.</summary>
    /// <param name="type">The CLR type to name.</param>
    /// <returns>The PostgreSQL type name.</returns>
    public override string MakeTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Range<>))
            return PostgresRangeTypes.NameFor(type.GetGenericArguments()[0]);

        if (type.IsArray && type.GetElementType() is { } element
            && element.IsGenericType && element.GetGenericTypeDefinition() == typeof(Range<>))
            return PostgresRangeTypes.MultirangeNameFor(element.GetGenericArguments()[0]);

        return type switch
        {
            _ when type == typeof(string) => "text",
            _ when type == typeof(TimeSpan) => "interval",
            _ => base.MakeTypeName(type)
        };
    }

    /// <summary>PostgreSQL has a native duration type (<c>interval</c>), so a <see cref="TimeSpan"/> is stored natively.</summary>
    public override bool SupportsNativeDuration => true;

    /// <summary>PostgreSQL's native duration type is <c>interval</c>, optionally with fractional-second precision.</summary>
    public override string MakeDurationType(DurationUnit? unit, int precision = 0)
        => precision > 0 ? $"interval({precision})" : "interval";

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

    // PostgreSQL has native range types (int4range/int8range/numrange/tsrange/tstzrange/daterange).
    /// <inheritdoc/>
    public override bool SupportsRanges => true;

    /// <inheritdoc/>
    public override bool SupportsRangeColumns => true;

    /// <inheritdoc/>
    public override bool SupportsTableFunction(string name) =>
        name is "generate_series" or "unnest"
            or "regexp_matches" or "regexp_split_to_table"
            or "jsonb_array_elements" or "jsonb_array_elements_text"
            or "jsonb_each" or "jsonb_each_text" or "jsonb_object_keys"
            or "jsonb_path_query" or "ts_stat"
            or "jsonb_to_record" or "jsonb_to_recordset";

    /// <summary>PostgreSQL renders the row-derived schema as the alias column-definition list of the record function.</summary>
    public override bool SupportsResultSchema(TableFunctionSchema placement) => placement == TableFunctionSchema.AliasColumnList;

    /// <summary>
    /// PostgreSQL's record set-returning functions take their result schema as an alias
    /// column-definition list: <c>jsonb_to_record(json) as "t1"(a integer, b text)</c>.
    /// </summary>
    public override string MakeTableFunctionAlias(string tableAlias, string? columnDefinitionList, KeywordCase keywordCase = KeywordCase.Lower)
        => columnDefinitionList is null
            ? MakeTableAlias(tableAlias, keywordCase)
            : MakeTableAlias(tableAlias, keywordCase) + "(" + columnDefinitionList + ")";

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
    public override AggregateFilterStyle AggregateFilterStyle => AggregateFilterStyle.AnsiFilter;
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

    /// <summary>
    /// The ANSI <c>date_diff</c> casts a sub-day span to <c>integer</c>; the 64-bit variant widens that
    /// cast to <c>bigint</c> so milliseconds/microseconds over a long range do not overflow.
    /// </summary>
    public override string MakeDateDiffBig(string field, string start, string end) =>
        MakeDateDiffCore(field, start, end, big: true);

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

    /// <summary>PostgreSQL renders every cross-provider scalar function natively (<c>space</c> as <c>repeat(' ', n)</c>).</summary>
    public override IScalarFunctions ScalarFunctions => PostgresScalarFunctions.Instance;

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

    /// <summary>PostgreSQL renders CLR format specifiers through <c>to_char</c>/<c>to_hex</c>.</summary>
    public override IStringFormatFunctions? StringFormats => PostgresStringFormats.Instance;

    /// <summary>PostgreSQL supports a per-expression <c>COLLATE</c> clause.</summary>
    public override bool SupportsCollation => true;

    /// <inheritdoc/>
    public override string MakeCollate(string value, string collation, KeywordCase keywordCase = KeywordCase.Lower) =>
        value + Kw(keywordCase, " collate ") + QuoteIdentifier(collation);

    /// <summary>PostgreSQL can express ordinal comparison through the <c>"C"</c> collation.</summary>
    public override bool SupportsOrdinalComparison => true;

    /// <inheritdoc/>
    public override string MakeOrdinal(string value, bool ignoreCase) =>
        MakeCollate(ignoreCase ? $"lower({value})" : value, "C");

    /// <summary>PostgreSQL matches with the <c>~</c>/<c>~*</c> operators and replaces with <c>regexp_replace</c>.</summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"{value} {(ignoreCase ? "~*" : "~")} {QuoteStringLiteral(pattern)}";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"regexp_replace({value}, {QuoteStringLiteral(pattern)}, {QuoteStringLiteral(replacement)}, {(ignoreCase ? "'gi'" : "'g'")})";

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

    /// <summary>PostgreSQL supports <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelect => true;

    /// <summary>PostgreSQL accepts <c>IF NOT EXISTS</c> on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectIfNotExists => true;

    /// <summary>PostgreSQL accepts a column list on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectColumnList => true;

    /// <summary>PostgreSQL supports <c>ON COMMIT { PRESERVE ROWS | DELETE ROWS | DROP }</c> on a temporary table.</summary>
    public override bool SupportsCreateTableAsSelectOnCommit => true;

    /// <summary>PostgreSQL supports <c>WITH [NO] DATA</c> on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectWithNoData => true;

    /// <summary>Adds the PostgreSQL <c>ON COMMIT { PRESERVE ROWS | DELETE ROWS | DROP }</c> clause of a temporary table.</summary>
    protected override string MakeCreateTableAsHead(CreateTableAsClause clause, KeywordCase keywordCase)
    {
        var head = base.MakeCreateTableAsHead(clause, keywordCase);
        if (clause.OnCommit is TempTableOnCommit.PreserveRows)
            return head;

        return head + Kw(keywordCase, " on commit ") + Kw(keywordCase, clause.OnCommit switch
        {
            TempTableOnCommit.DeleteRows => "delete rows",
            TempTableOnCommit.Drop => "drop",
            _ => "preserve rows",
        });
    }

    /// <summary>Appends the PostgreSQL <c>WITH NO DATA</c> clause.</summary>
    protected override string MakeCreateTableAsTail(CreateTableAsClause clause, KeywordCase keywordCase)
        => clause.WithData ? string.Empty : Kw(keywordCase, " with no data");
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
        Render(mode, LockWaitMode.Wait, keywordCase);

    public string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, (mode == LockMode.Share ? " for share" : " for update") + LockWaitSuffix(wait));

    private static string LockWaitSuffix(LockWaitMode wait) => wait switch
    {
        LockWaitMode.Wait => "",
        LockWaitMode.NoWait => " nowait",
        LockWaitMode.SkipLocked => " skip locked",
        _ => throw new ArgumentOutOfRangeException(nameof(wait), wait, "Unknown locking wait mode.")
    };
}

internal sealed class PostgresTupleRenderer : ITupleRenderer
{
    public static readonly PostgresTupleRenderer Instance = new();

    public string RenderConstructor(IReadOnlyList<string> fields) => "ROW(" + string.Join(", ", fields) + ")";

    public string? RenderElement(string row, int oneBasedIndex) => "(" + row + ").f" + oneBasedIndex;
}

/// <summary>
/// PostgreSQL rendering of the culture-invariant CLR format specifiers: numbers through
/// <c>to_char</c> masks (and <c>to_hex</c> for <c>X</c>), date/time through a <c>to_char</c> mask.
/// The group separator (<c>N</c>) is not offered because PostgreSQL's <c>to_char</c> grouping is
/// locale-dependent.
/// </summary>
internal sealed class PostgresStringFormats : IStringFormatFunctions
{
    internal static readonly PostgresStringFormats Instance = new();

    public bool SupportsNumber(char specifier) => specifier is 'F' or 'D' or 'X';

    public string RenderNumber(string value, char specifier, int precision) => specifier switch
    {
        'F' => precision <= 0
            ? $"to_char({value}, 'FM9999999999999990')"
            : $"to_char({value}, 'FM9999999999999990.' || repeat('0', {precision}))",
        'D' => precision <= 0
            ? $"to_char({value}, 'FM9999999999999990')"
            : $"to_char({value}, 'FM' || repeat('0', {precision}))",
        'X' => precision <= 0
            ? $"upper(to_hex({value}))"
            : $"lpad(upper(to_hex({value})), {precision}, '0')",
        _ => throw new NotSupportedException($"The numeric format specifier '{specifier}' is not supported by PostgreSQL.")
    };

    public bool SupportsDateFormat(string clrFormat) => TryMapDate(clrFormat, out _);

    public string RenderDate(string value, string clrFormat) =>
        TryMapDate(clrFormat, out var mask)
            ? $"to_char({value}, '{mask}')"
            : throw new NotSupportedException($"The date/time format string '{clrFormat}' is not supported by PostgreSQL.");

    private static bool TryMapDate(string format, out string mask)
    {
        var sb = new StringBuilder(format.Length + 4);
        var i = 0;

        while (i < format.Length)
        {
            if (Match(format, i, "yyyy")) { sb.Append("YYYY"); i += 4; }
            else if (Match(format, i, "yy")) { sb.Append("YY"); i += 2; }
            else if (Match(format, i, "MM")) { sb.Append("MM"); i += 2; }
            else if (Match(format, i, "dd")) { sb.Append("DD"); i += 2; }
            else if (Match(format, i, "HH")) { sb.Append("HH24"); i += 2; }
            else if (Match(format, i, "mm")) { sb.Append("MI"); i += 2; }
            else if (Match(format, i, "ss")) { sb.Append("SS"); i += 2; }
            else if (format[i] is '-' or '/' or '.' or ':' or ' ') { sb.Append(format[i]); i++; }
            else { mask = string.Empty; return false; }
        }

        mask = sb.ToString();
        return true;
    }

    private static bool Match(string value, int index, string token) =>
        index + token.Length <= value.Length && string.CompareOrdinal(value, index, token, 0, token.Length) == 0;
}

/// <summary>
/// Renders the cross-provider scalar functions of <see cref="CommonFunctions"/> on PostgreSQL, where
/// every name has a native spelling (<c>left</c>/<c>right</c>, <c>lpad</c>/<c>rpad</c>, <c>repeat</c>/
/// <c>reverse</c>, <c>concat_ws</c>, <c>translate</c>, <c>ascii</c>, <c>chr</c>). <c>space</c> has no
/// native function and is rendered as <c>repeat(' ', n)</c>.
/// </summary>
internal sealed class PostgresScalarFunctions : IScalarFunctions
{
    internal static readonly PostgresScalarFunctions Instance = new();

    /// <inheritdoc/>
    public bool Supports(string name) => name is
        "left" or "right" or "lpad" or "rpad" or "repeat" or "reverse" or "space" or
        "concat_ws" or "translate" or "ascii" or "char" or
        "bit_length" or "octet_length" or "cot" or "degrees" or "radians" or "pi";

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "left" => $"left({args[0]}, {args[1]})",
        "right" => $"right({args[0]}, {args[1]})",
        "lpad" => $"lpad({args[0]}, {args[1]}, {Pad(args)})",
        "rpad" => $"rpad({args[0]}, {args[1]}, {Pad(args)})",
        "repeat" => $"repeat({args[0]}, {args[1]})",
        "reverse" => $"reverse({args[0]})",
        "space" => $"repeat(' ', {args[0]})",
        "concat_ws" => $"concat_ws({string.Join(", ", args)})",
        "translate" => $"translate({args[0]}, {args[1]}, {args[2]})",
        "ascii" => $"ascii({args[0]})",
        "char" => $"chr({args[0]})",
        "bit_length" => $"bit_length({args[0]})",
        "octet_length" => $"octet_length({args[0]})",
        "cot" => $"cot({args[0]})",
        "degrees" => $"degrees({args[0]})",
        "radians" => $"radians({args[0]})",
        "pi" => "pi()",
        _ => throw new NotSupportedException($"The {name} function is not supported by PostgreSQL.")
    };

    private static string Pad(IReadOnlyList<string> args) => args.Count == 3 ? args[2] : "' '";
}
