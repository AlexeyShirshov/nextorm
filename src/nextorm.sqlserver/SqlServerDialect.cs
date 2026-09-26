using System.Text;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// SQL Server dialect: bracket-quoted identifiers, <c>@name</c> parameters, <c>offset/fetch</c> paging
/// (which requires an ORDER BY), <c>count_big</c> and a boolean-less rendering of subquery predicates.
/// </summary>
public sealed class SqlServerDialect : SqlDialectBase
{
    /// <summary>The shared SQL Server dialect instance.</summary>
    public static readonly SqlServerDialect Instance = new();

    private readonly IPivotRenderer _pivot;

    /// <summary>Creates the dialect and its capability renderers.</summary>
    public SqlServerDialect() => _pivot = new SqlServerPivotRenderer(this);

    /// <summary>Renders a positional parameter as <c>@name</c>.</summary>
    public override string MakeParam(string name) => $"@{name}";

    /// <summary>
    /// SQL Server returns the generated identity through the <c>OUTPUT inserted.&lt;column&gt;</c>
    /// clause of the insert (preferred over the session-scoped <c>SCOPE_IDENTITY()</c>).
    /// </summary>
    public override bool SupportsOutput => true;

    /// <inheritdoc/>
    public override bool SupportsRangeColumns => true;

    /// <summary>
    /// SQL Server writes the modified rows into an existing table through the
    /// <c>OUTPUT ... INTO &lt;target&gt;(columns)</c> form of a data-modifying statement.
    /// </summary>
    public override bool SupportsOutputInto => true;

    /// <summary>
    /// SQL Server reads the last generated identity through the batch-scoped <c>SCOPE_IDENTITY()</c>.
    /// The identity-function terminal appends it to the insert as a single batch, which keeps
    /// <c>SCOPE_IDENTITY()</c> in scope and unaffected by triggers.
    /// </summary>
    public override bool SupportsIdentityFunction => true;

    /// <summary>SQL Server supports <c>INSERT ... DEFAULT VALUES</c> for an all-defaults row.</summary>
    public override bool SupportsDefaultValues => true;

    /// <summary>SQL Server accepts <c>DEFAULT</c> as a value in the <c>VALUES</c> list.</summary>
    public override bool SupportsColumnDefault => true;

    /// <summary>SQL Server has a native bulk path (<c>SqlBulkCopy</c>).</summary>
    public override bool SupportsBulkCopy => true;

    /// <summary>SQL Server batches through <c>SqlBatch</c>.</summary>
    public override bool SupportsBatch => true;

    /// <summary><c>SqlBatch</c> runs each command in its own scope, so a <c>#temp</c> created by one command is not visible to the next; the joined command keeps them in one batch scope.</summary>
    public override bool BatchUsesJoinedCommand => true;

    /// <summary>SQL Server writes explicit identity values only inside <c>SET IDENTITY_INSERT ... ON/OFF</c>.</summary>
    public override bool RequiresIdentityInsertToggle => true;

    /// <summary>SQL Server has a native <c>TRUNCATE TABLE</c>.</summary>
    public override bool SupportsTruncate => true;

    /// <summary>SQL Server deletes rows based on a join through the <c>DELETE &lt;alias&gt; FROM ... JOIN</c> extension.</summary>
    public override bool SupportsDeleteJoin => true;

    /// <summary>SQL Server updates rows based on a join through the <c>UPDATE &lt;alias&gt; ... FROM ... JOIN</c> extension.</summary>
    public override bool SupportsUpdateJoin => true;

    /// <summary>SQL Server materialises a query into a table through <c>SELECT ... INTO</c>.</summary>
    public override bool SupportsCreateTableAsSelect => true;

    /// <summary>SQL Server has no <c>CREATE TEMPORARY TABLE ... AS SELECT</c>; a session-scoped table is <c>ToTable("#name")</c>.</summary>
    public override bool SupportsTemporaryCreateTableAsSelect => false;

    /// <summary>SQL Server's <c>SELECT ... INTO</c> has no <c>IF NOT EXISTS</c> clause.</summary>
    public override bool SupportsCreateTableAsSelectIfNotExists => false;

    /// <summary>SQL Server renders the materialisation as <c>SELECT ... INTO</c> rather than <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool CreateTableAsSelectUsesSelectInto => true;

    /// <summary>Renders the SQL Server <c>INTO &lt;table&gt;</c> clause inserted into the top-level select list.</summary>
    public override string MakeCreateTableAsSelectInto(CreateTableAsClause clause, KeywordCase keywordCase = KeywordCase.Lower)
        => Kw(keywordCase, " into ") + clause.Table;

    /// <summary>
    /// Renders the SQL Server multi-table update: the <c>SET</c> list is qualified by the target alias and
    /// the target is named again (with its joins) in the <c>FROM</c> clause.
    /// </summary>
    public override string MakeUpdateJoin(
        string target,
        string targetAlias,
        string assignments,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var sql = Kw(keywordCase, "update ") + targetAlias + Kw(keywordCase, " set ") + assignments + Kw(keywordCase, " from ") + fromAndJoins;
        return string.IsNullOrEmpty(whereSql) ? sql : sql + Kw(keywordCase, " where ") + whereSql;
    }

    /// <summary>SQL Server expresses a key upsert as a <c>MERGE</c> over a <c>VALUES</c> derived source.</summary>
    public override bool SupportsMerge => true;

    /// <summary>SQL Server renders a general, multi-branch <c>MERGE</c>.</summary>
    public override bool SupportsMergeStatement => true;

    /// <summary>SQL Server <c>MERGE</c> supports a <c>WHEN MATCHED THEN DELETE</c> branch.</summary>
    public override bool SupportsMergeDelete => true;

    /// <summary>SQL Server is the only supported provider with <c>WHEN NOT MATCHED BY SOURCE</c>.</summary>
    public override bool SupportsMergeBySourceDelete => true;

    /// <summary>SQL Server <c>MERGE</c> supports an explicit <c>ON &lt;condition&gt;</c> and <c>WHEN ... AND &lt;condition&gt;</c>.</summary>
    public override bool SupportsMergeConditionalBranches => true;

    /// <summary>SQL Server requires a terminating semicolon after <c>MERGE</c>.</summary>
    public override string MakeMergeStatementTerminator(KeywordCase keywordCase = KeywordCase.Lower) => ";";

    /// <summary>Renders the key-upsert <c>MERGE ... USING (VALUES ...) AS source (...) ON ...</c> statement, optionally followed by an <c>OUTPUT inserted.&lt;column&gt;</c> clause (T-SQL requires the terminating semicolon).</summary>
    public override string MakeMerge(
        string target,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> updateColumns,
        string valuesRows,
        KeywordCase keywordCase = KeywordCase.Lower,
        IReadOnlyList<string>? returningColumns = null)
    {
        var columnList = string.Join(", ", columns);
        var match = string.Join(Kw(keywordCase, " and "), keys.Select(static k => "target." + k + " = source." + k));
        var updates = string.Join(", ", updateColumns.Select(static c => "target." + c + " = source." + c));
        var insertValues = string.Join(", ", columns.Select(static c => "source." + c));
        var output = returningColumns is { Count: > 0 }
            ? MakeOutput(returningColumns, keywordCase)
            : string.Empty;

        return Kw(keywordCase, "merge into ") + target
            + Kw(keywordCase, " as target using (values ") + valuesRows
            + Kw(keywordCase, ") as source (") + columnList
            + Kw(keywordCase, ") on ") + match
            + Kw(keywordCase, " when matched then update set ") + updates
            + Kw(keywordCase, " when not matched then insert (") + columnList
            + Kw(keywordCase, ") values (") + insertValues
            + ")" + output + ";";
    }

    /// <summary>Renders the identity-function query <c>select scope_identity()</c>.</summary>
    public override string MakeIdentityFunction(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "select scope_identity()");

    /// <summary>
    /// SQL Server bracket-quotes identifiers. Single quoted aliases (the base default) are accepted
    /// for columns but produce a syntax error for table and derived table aliases.
    /// </summary>
    public override string Escape(string keyword) => "[" + keyword + "]";

    /// <summary>SQL Server quotes a physical identifier with brackets, doubling an embedded <c>]</c>.</summary>
    public override string QuoteIdentifier(string name) => "[" + name.Replace("]", "]]") + "]";

    /// <summary>
    /// SQL Server uses a bracket-quoted identifier for references as well, so that aliases that
    /// collide with a T-SQL keyword (e.g. "double") stay usable from an outer query.
    /// </summary>
    public override string MakeColumnReference(string name) => Escape(name);

    /// <summary>A SQL Server derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    /// <summary>SQL Server spells a lateral source as <c>CROSS APPLY</c>/<c>OUTER APPLY</c>.</summary>
    public override bool SupportsApply => true;

    /// <summary><c>CROSS APPLY</c>/<c>OUTER APPLY</c> accept a plain table name, unlike a <c>LATERAL</c> reference.</summary>
    public override bool SupportsApplyOnPlainTable => true;

    /// <summary>SQL Server supports <c>GROUP BY ROLLUP (...)</c> and <c>GROUP BY CUBE (...)</c>.</summary>
    public override bool SupportsRollup => true;

    /// <summary>SQL Server supports <c>GROUP BY CUBE (...)</c>.</summary>
    public override bool SupportsCube => true;
    /// <summary>SQL Server supports <c>GROUP BY GROUPING SETS (...)</c>.</summary>
    public override bool SupportsGroupingSets => true;

    /// <summary>SQL Server 2016+ renders the JSON-as-text functions (<c>json_value</c>, ...) over nvarchar.</summary>
    public override bool SupportsTextJson => true;

    /// <summary>T-SQL <c>isjson</c> returns an <c>int</c>: <c>1</c>/<c>0</c>, so a value context casts it to <c>bit</c>.</summary>
    public override string MakeIsJson(string value, bool asPredicate) =>
        asPredicate ? $"(isjson({value})) = 1" : $"cast(isjson({value}) as bit)";

    /// <summary>SQL Server is the only provider with the postfix XML data-type methods.</summary>
    public override IXmlFunctions XmlFunctions => SqlServerXmlFunctions.Instance;

    /// <summary>SQL Server renders the full-text predicates <c>contains</c>/<c>freetext</c>.</summary>
    public override bool SupportsFullText => true;

    /// <summary>Renders the full-text predicate <c>contains(column, search)</c> or <c>freetext(column, search)</c>.</summary>
    public override string MakeFullText(string functionName, string column, string search) =>
        $"{functionName}({column}, {search})";

    /// <summary>SQL Server supports the <c>TABLESAMPLE</c> table modifier; only the <c>SYSTEM</c> method exists.</summary>
    public override ITableSampleMethods TableSample => SqlServerTableSampleMethods.Instance;

    /// <summary>SQL Server supports the <c>FOR SYSTEM_TIME</c> temporal-table clause, including <c>CONTAINED IN</c>.</summary>
    public override bool SupportsTemporalTable => true;

    /// <summary>SQL Server supports every <c>FOR SYSTEM_TIME</c> kind.</summary>
    public override bool SupportsTemporalKind(TemporalKind kind) => true;

    /// <summary>SQL Server supports the native <c>PIVOT</c>/<c>UNPIVOT</c> source constructs.</summary>
    public override IPivotRenderer Pivot => _pivot;

    /// <summary>SQL Server exposes <c>string_split</c>, <c>openjson</c>, <c>containstable</c> and <c>freetexttable</c>.</summary>
    public override bool SupportsTableFunction(string name) =>
        name is "string_split" or "openjson" or "containstable" or "freetexttable";

    /// <summary>SQL Server supports a raw SQL derived table (<c>FROM (&lt;sql&gt;) AS alias</c>).</summary>
    public override bool SupportsRawSqlSource => true;

    /// <summary>SQL Server renders table hints as <c>with (hint, ...)</c> after the table name.</summary>
    public override bool SupportsTableHints => true;

    /// <summary>Renders the hints as a <c>with (hint, ...)</c> suffix.</summary>
    public override string MakeTableHints(IReadOnlyList<string> hints, KeywordCase keywordCase = KeywordCase.Lower)
        => Kw(keywordCase, " with (") + string.Join(", ", hints) + ")";

    /// <summary>SQL Server renders a join hint inside the join clause, between the join kind and <c>JOIN</c>.</summary>
    public override bool SupportsJoinHints => true;

    /// <summary>
    /// Inserts the join <paramref name="hint"/> before the <c>JOIN</c> keyword of the already-rendered
    /// keyword, e.g. <c>inner join</c> with <c>loop</c> becomes <c>inner loop join</c>. A <c>null</c>
    /// hint delegates to the plain keyword.
    /// </summary>
    public override string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal, string? hint, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var keyword = MakeJoinKeyword(joinType, strictness, isGlobal, keywordCase);

        if (hint is null)
            return keyword;

        // The ANSI form omits INNER; the hinted form spells it out (INNER LOOP JOIN).
        if (joinType == JoinType.Inner)
            keyword = Kw(keywordCase, " inner join ");

        var index = keyword.LastIndexOf("join", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            throw new NotSupportedException($"A join hint cannot be applied to the {joinType} join");

        return keyword[..index] + hint + " " + keyword[index..];
    }

    /// <summary>SQL Server renders tables-in-scope hints as a <c>WITH (...)</c> table hint on every table.</summary>
    public override bool SupportsTablesInScopeHints => true;

    /// <summary>Renders the tables-in-scope hints as a <c>with (hint, ...)</c> suffix on a table.</summary>
    public override string MakeTablesInScopeHints(IReadOnlyList<string> hints, KeywordCase keywordCase = KeywordCase.Lower)
        => Kw(keywordCase, " with (") + string.Join(", ", hints) + ")";

    /// <summary>SQL Server renders index hints as a <c>WITH (INDEX(...))</c> table hint.</summary>
    public override IIndexHintRenderer? IndexHints => SqlServerIndexHintRenderer.Instance;

    /// <summary>SQL Server supports row locking through the <c>updlock</c>/<c>holdlock</c> table hints.</summary>
    public override ILockRenderer Lock => SqlServerLockRenderer.Instance;

    /// <summary>SQL Server renders the result set as JSON through a trailing <c>FOR JSON</c> clause.</summary>
    public override bool SupportsForJson => true;

    /// <summary>Renders the trailing <c>FOR JSON</c> clause with its mode, root and null-value options.</summary>
    public override string MakeForJson(ForJsonClause clause, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var sqlBuilder = new StringBuilder(Kw(keywordCase, "for json "));
        sqlBuilder.Append(Kw(keywordCase, clause.Mode == ForJsonMode.Auto ? "auto" : "path"));

        if (!string.IsNullOrEmpty(clause.Root))
            sqlBuilder.Append(Kw(keywordCase, ", root('")).Append(clause.Root.Replace("'", "''")).Append("')");

        if (clause.IncludeNullValues)
            sqlBuilder.Append(Kw(keywordCase, ", include_null_values"));

        return sqlBuilder.ToString();
    }

    /// <summary>SQL Server renders the result set as XML through a trailing <c>FOR XML</c> clause.</summary>
    public override bool SupportsForXml => true;

    /// <summary>Renders the trailing <c>FOR XML</c> clause with its mode, element name, root and elements options.</summary>
    public override string MakeForXml(ForXmlClause clause, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var sqlBuilder = new StringBuilder(Kw(keywordCase, "for xml "));
        sqlBuilder.Append(Kw(keywordCase, clause.Mode switch
        {
            ForXmlMode.Raw => "raw",
            ForXmlMode.Auto => "auto",
            ForXmlMode.Explicit => "explicit",
            _ => "path"
        }));

        if (!string.IsNullOrEmpty(clause.ElementName))
            sqlBuilder.Append("('").Append(clause.ElementName.Replace("'", "''")).Append("')");

        if (!string.IsNullOrEmpty(clause.Root))
            sqlBuilder.Append(Kw(keywordCase, ", root('")).Append(clause.Root.Replace("'", "''")).Append("')");

        if (clause.Elements)
            sqlBuilder.Append(Kw(keywordCase, ", elements"));

        return sqlBuilder.ToString();
    }

    /// <summary>
    /// SQL Server 2022 introduced <c>greatest()</c>/<c>least()</c> with the standard syntax, so the
    /// base rendering applies. The functions are unavailable on older servers, but the dialect cannot
    /// detect the server version at SQL-generation time.
    /// </summary>
    public override bool SupportsGreatestLeast => true;

    // SQL Server 2012+ supports the ANSI percent_rank()/cume_dist() window functions.
    /// <summary>SQL Server 2012+ implements the <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>SQL Server renders percentiles as the <c>PERCENTILE_CONT</c>/<c>PERCENTILE_DISC</c> analytic functions (no exact ordered-set aggregate form).</summary>
    public override bool SupportsPercentileWindow => true;

    /// <summary>SQL Server 2017 introduced <c>string_agg</c>; it has no array type, so <c>array_agg</c> stays unavailable.</summary>
    public override bool SupportsStringAgg => true;

    /// <summary>Renders a lateral source as <c>CROSS APPLY</c> or <c>OUTER APPLY</c>.</summary>
    public override string MakeApply(JoinType applyType, string source, KeywordCase keywordCase = KeywordCase.Lower) => applyType switch
    {
        JoinType.CrossApply => Kw(keywordCase, " cross apply ") + source,
        JoinType.OuterApply => Kw(keywordCase, " outer apply ") + source,
        _ => base.MakeApply(applyType, source, keywordCase)
    };

    /// <summary>Maps the CLR type to its SQL Server column type (<c>tinyint</c>, <c>smallint</c>, <c>int</c>, <c>bigint</c>, <c>real</c>, <c>float</c>, <c>decimal</c>).</summary>
    public override string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "tinyint",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "bigint",
        _ when type == typeof(float) => "real",
        _ when type == typeof(double) => "float",
        _ when type == typeof(decimal) => "decimal(38, 10)",
        _ when type == typeof(DateTimeOffset) => "datetimeoffset",
        _ => base.MakeTypeName(type)
    };

    // SQL Server has no length(); its equivalent is len().
    /// <summary>SQL Server has no <c>length</c>; it renders <c>len(value)</c>.</summary>
    public override string MakeStringLength(string value) => $"len({value})";

    /// <summary>Renders the one-based position through <c>charindex(substring, value)</c> (0 when absent).</summary>
    protected override string MakeStringPosition(string value, string substring) =>
        $"charindex({substring}, {value})";

    /// <summary>Renders the one-based position from a zero-based start through <c>charindex(substring, value, start + 1)</c>.</summary>
    protected override string MakeStringPosition(string value, string substring, string start) =>
        $"charindex({substring}, {value}, {start} + 1)";

    /// <summary>Renders <c>replicate(value, count)</c>.</summary>
    public override string MakeRepeat(string value, string count) => $"replicate({value}, {count})";

    /// <summary>Renders <c>reverse(value)</c>.</summary>
    protected override string MakeStringReverse(string value) => $"reverse({value})";

    /// <summary>SQL Server renders CLR format specifiers through <c>FORMAT</c> (a .NET formatter).</summary>
    public override IStringFormatFunctions? StringFormats => SqlServerStringFormats.Instance;

    /// <summary>SQL Server supports a per-expression <c>COLLATE</c> clause.</summary>
    public override bool SupportsCollation => true;

    /// <inheritdoc/>
    public override string MakeCollate(string value, string collation, KeywordCase keywordCase = KeywordCase.Lower) =>
        value + Kw(keywordCase, " collate ") + collation;

    /// <summary>SQL Server can express ordinal comparison through the <c>Latin1_General_100_BIN2</c> collation.</summary>
    public override bool SupportsOrdinalComparison => true;

    /// <inheritdoc/>
    public override string MakeOrdinal(string value, bool ignoreCase)
    {
        const string Binary = "Latin1_General_100_BIN2";
        return ignoreCase ? $"lower({value}) collate {Binary}" : $"{value} collate {Binary}";
    }

    /// <summary>
    /// SQL Server 2025 matches with the RE2-based <c>REGEXP_LIKE</c>/<c>REGEXP_REPLACE</c>. The capability
    /// is unconditional: the static dialect cannot see the server version or
    /// <c>sys.databases.compatibility_level</c>, so translation targets 2025+ and SQL Server 2019/2022
    /// rejects the emitted call at execution. On 2025 the match additionally requires database
    /// compatibility level 170, while <c>REGEXP_REPLACE</c> is available at every level. Flags: <c>c</c>
    /// (case-sensitive, the default) or <c>i</c>.
    /// </summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"regexp_like({value}, {QuoteStringLiteral(pattern)}, {(ignoreCase ? "'i'" : "'c'")})";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"regexp_replace({value}, {QuoteStringLiteral(pattern)}, {QuoteStringLiteral(replacement)}, 1, 0, {(ignoreCase ? "'i'" : "'c'")})";

    /// <summary>Renders <c>stuff(value, start + 1, count, newValue)</c>, or the truncated prefix when the count is <c>null</c>.</summary>
    public override string MakeStuff(string value, string start, string? count, string newValue) =>
        count is null
            ? $"substring({value}, 1, {start})"
            : $"stuff({value}, {start} + 1, {count}, {newValue})";

    /// <summary>
    /// SQL Server extracts date parts through <c>datepart(part, value)</c>; T-SQL spells day-of-year
    /// as <c>dayofyear</c>. The ISO week is <c>isowk</c>; dow/isodow are normalised against the
    /// session DATEFIRST through <c>datepart(weekday)</c> and <c>@@datefirst</c>.
    /// </summary>
    public override string MakeDatePart(string part, string value) => part switch
    {
        "doy" => $"datepart(dayofyear, {value})",
        "week" => $"datepart(isowk, {value})",
        "dow" => $"((datepart(weekday, {value}) + @@datefirst - 1) % 7)",
        "isodow" => $"(((datepart(weekday, {value}) + @@datefirst - 2) % 7) + 1)",
        "epoch" => $"cast(datediff_big(millisecond, '19700101', {value}) as float) / 1000.0",
        _ => $"datepart({part}, {value})"
    };

    /// <summary>SQL Server additionally accepts the normalised weekday and epoch date parts.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    /// <summary>
    /// SQL Server 2022 introduced <c>datetrunc(datepart, date)</c>. It takes an unquoted part name and
    /// uses singular spellings (the ANSI/PostgreSQL <c>microseconds</c>/<c>milliseconds</c> are mapped);
    /// <c>decade</c>/<c>century</c>/<c>millennium</c> have no T-SQL equivalent.
    /// </summary>
    public override bool SupportsDateTrunc => true;

    /// <summary>Accepts the date-trunc fields except <c>decade</c>, <c>century</c> and <c>millennium</c>, which T-SQL <c>datetrunc</c> lacks.</summary>
    public override bool SupportsDateTruncField(string field) =>
        field is not ("decade" or "century" or "millennium") && base.SupportsDateTruncField(field);

    /// <summary>Renders <c>datetrunc(part, value)</c>, mapping the plural ANSI sub-second names to the singular T-SQL ones.</summary>
    public override string MakeDateTrunc(string field, string value)
    {
        var part = field switch
        {
            "microseconds" => "microsecond",
            "milliseconds" => "millisecond",
            "decade" or "century" or "millennium" =>
                throw new NotSupportedException($"SQL Server datetrunc does not support the '{field}' field."),
            _ => field
        };

        return $"datetrunc({part}, {value})";
    }

    /// <summary>
    /// SQL Server has no interval arithmetic; addition is exposed through <c>dateadd</c> and the last
    /// day of the month through <c>eomonth</c>.
    /// </summary>
    public override bool SupportsDateArithmetic => true;

    /// <summary>Renders <c>dateadd(part, amount, value)</c>, folding <c>decade</c>/<c>century</c>/<c>millennium</c> onto a scaled <c>year</c>.</summary>
    public override string MakeDateAdd(string field, string amount, string value)
    {
        // T-SQL dateadd has no decade/century/millennium parts (datepart has them, dateadd does not),
        // so they are folded into a scaled year add; plural ANSI parts map to the singular T-SQL names.
        var (part, factor) = field switch
        {
            "microseconds" => ("microsecond", 1),
            "milliseconds" => ("millisecond", 1),
            "decade" => ("year", 10),
            "century" => ("year", 100),
            "millennium" => ("year", 1000),
            _ => (field, 1)
        };

        var scaled = factor == 1 ? amount : $"({amount}) * {factor}";

        return $"dateadd({part}, {scaled}, {value})";
    }

    /// <summary>Renders <c>datediff(part, start, end)</c> with the singular T-SQL part names.</summary>
    public override string MakeDateDiff(string field, string start, string end) =>
        $"datediff({TSqlDatePart(field)}, {start}, {end})";

    /// <summary>Renders the 64-bit <c>datediff_big(part, start, end)</c> with the singular T-SQL part names.</summary>
    public override string MakeDateDiffBig(string field, string start, string end) =>
        $"datediff_big({TSqlDatePart(field)}, {start}, {end})";

    // T-SQL datediff uses the singular part names; plural ANSI parts map onto them.
    private static string TSqlDatePart(string field) => field switch
    {
        "microseconds" => "microsecond",
        "milliseconds" => "millisecond",
        _ => field
    };

    /// <summary>
    /// T-SQL <c>dateadd</c> on a <c>date</c> operand for a sub-day part drops the time of day (and
    /// <c>datediff</c> cannot see it), so a sub-day operand is promoted to <c>datetime2</c>.
    /// </summary>
    public override string PromoteDateOperand(string field, string value) =>
        IsSubDayField(field) ? $"cast({value} as datetime2)" : value;

    /// <summary>Renders <c>eomonth(value)</c>.</summary>
    public override string MakeEndOfMonth(string value) => $"eomonth({value})";

    /// <summary>Renders <c>datefromparts(year, month, day)</c>.</summary>
    public override string MakeDateFromParts(string year, string month, string day) =>
        $"datefromparts({year}, {month}, {day})";

    /// <summary>Renders <c>getutcdate()</c> for UTC or <c>getdate()</c> for local time.</summary>
    public override string MakeNow(bool utc) => utc ? "getutcdate()" : "getdate()";

    // T-SQL has no trunc; the 3-argument round(number, length, function) truncates when function is
    // non-zero. Its round() also requires the length argument, unlike the ANSI/Math single-argument form.
    /// <summary>Renders <c>round(number, 0, 1)</c> for <c>trunc</c> and <c>round(number, 0)</c> for the one-argument <c>round</c>.</summary>
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) => (name, args.Count) switch
    {
        ("trunc", 1) => $"round({args[0]}, 0, 1)",
        ("round", 1) => $"round({args[0]}, 0)",
        // T-SQL has no POW; Math.Pow must render as POWER.
        ("pow", 2) => $"power({args[0]}, {args[1]})",
        // T-SQL names the two-argument arctangent ATN2, not ATAN2.
        ("atan2", 2) => $"atn2({args[0]}, {args[1]})",
        _ => base.MakeMathFunction(name, args)
    };

    /// <summary>T-SQL has no boolean type: a scalar boolean is materialised as <c>cast(case when ... then 1 else 0 end as bit)</c>.</summary>
    public override string MakeBooleanPredicate(string predicate, bool asPredicate)
    {
        // T-SQL has no boolean type: a predicate is valid only as a condition, so a scalar use has to
        // be materialised as a bit value (which keeps GetBoolean working).
        return asPredicate
            ? predicate
            : $"cast(case when {predicate} then 1 else 0 end as bit)";
    }

    /// <summary>A bit value is not a valid T-SQL predicate, so it is compared with its true literal (<c>(value) = 1</c>).</summary>
    public override string MakeBooleanValuePredicate(string value)
    {
        // A bit value is not a valid predicate in T-SQL, so it has to be compared with its true
        // literal before it can be used in a condition context (WHERE, CASE test, AND/OR, NOT).
        return $"({value}) = 1";
    }

    /// <summary>T-SQL has no boolean type: <c>exists</c>/<c>any</c>/<c>all</c> are materialised as a bit scalar in a projection.</summary>
    public override string MakeSubqueryPredicate(string keyword, string query, bool asPredicate)
    {
        // SQL Server has no boolean type: EXISTS/ANY/ALL are valid only as a predicate, while a
        // scalar projection needs a CASE (a comparison is not a valid select list entry in T-SQL).
        if (keyword is "exists" or "any" or "all")
            return asPredicate
                ? base.MakeSubqueryPredicate(keyword, query, true)
                // The CASE literals are ints, so it is cast to bit to keep GetBoolean working.
                : $"cast(case when {base.MakeSubqueryPredicate(keyword, query, false)} then 1 else 0 end as bit)";

        return base.MakeSubqueryPredicate(keyword, query, asPredicate);
    }

    // T-SQL has both isnull and coalesce; isnull is kept as the historical rendering.
    /// <summary>T-SQL has both <c>isnull</c> and <c>coalesce</c>; this renders the historical <c>isnull(v1, v2)</c>.</summary>
    public override string MakeCoalesce(string v1, string v2) => $"isnull({v1},{v2})";

    /// <summary>Renders the boolean coalesce as <c>(isnull(v1, v2)) = 1</c> so it stays usable as a predicate.</summary>
    public override string MakeBoolCoalesce(string v1, string v2)
    {
        // A bit expression is not a valid predicate in T-SQL, so compare it with 1; the result is
        // still a bit value and therefore remains usable as a projection.
        return $"({MakeCoalesce(v1, v2)}) = 1";
    }

    /// <summary>Materialises a boolean-valued CASE as a <c>bit</c> scalar, compared with <c>1</c> in a predicate context.</summary>
    public override string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate, KeywordCase keywordCase = KeywordCase.Lower)
    {
        if (!isBooleanResult)
            return caseExpression;

        // T-SQL has no boolean type: a boolean-valued CASE has to be materialised as a bit scalar.
        var bitValue = Kw(keywordCase, "cast(") + caseExpression + Kw(keywordCase, " as bit)");

        // A bit scalar is not a valid predicate, so a condition context compares it with 1. The
        // result of that comparison is boolean, which is exactly what the predicate needs.
        return asPredicate ? bitValue + " = 1" : bitValue;
    }

    /// <summary>Renders <c>count_big(...)</c> for the large form (<c>count</c> otherwise), preserving the <c>distinct</c> modifier.</summary>
    public override string MakeCount(bool distinct, bool big)
    {
        // count_big returns bigint while count returns int; the SQL Server provider can express both.
        if (big)
            return distinct ? "count_big(distinct " : "count_big(";

        return base.MakeCount(distinct, false);
    }

    /// <summary>Renders <c>offset n rows</c> and an optional <c>fetch next m rows only|with ties</c>.</summary>
    public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
    {
        sqlBuilder.Append(Kw(keywordCase, "offset ")).Append(paging.Offset).Append(Kw(keywordCase, " rows"));

        if (paging.Limit > 0)
            sqlBuilder.AppendLine().Append(Kw(keywordCase, "fetch next ")).Append(paging.Limit)
                .Append(Kw(keywordCase, paging.HasWithTies ? " rows with ties" : " rows only"));
    }

    /// <summary>SQL Server supports <c>TOP(n) WITH TIES</c> and <c>FETCH NEXT ... WITH TIES</c>.</summary>
    public override bool SupportsWithTies => true;

    // SQL Server rejects OFFSET/FETCH without ORDER BY, so a constant sort has to be injected.
    /// <summary>Injects a constant sort because SQL Server rejects <c>OFFSET</c>/<c>FETCH</c> without an <c>ORDER BY</c>.</summary>
    public override string? GetPagingOrderBy(QueryCommand queryCommand)
        => queryCommand.Paging.IsEmpty ? null : "(select null as anyorder)";

    /// <summary>SQL Server renders the inline limit as <c>top(n)</c>, or <c>top(n) with ties</c>.</summary>
    public override bool MakeTop(int limit, bool withTies, out string? topStmt, KeywordCase keywordCase = KeywordCase.Lower)
    {
        topStmt = Kw(keywordCase, withTies ? $"top({limit}) with ties" : $"top({limit})");
        return true;
    }

    // T-SQL has no RECURSIVE keyword: a recursive CTE is declared with `with` alone, so the flag is
    // intentionally ignored.
    /// <summary>T-SQL has no <c>RECURSIVE</c> keyword, so the flag is ignored and <c>with</c> is always rendered.</summary>
    public override string MakeWith(bool recursive, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "with ");

    // MAXRECURSION overrides the 100-level default. The option is appended at the end of the
    // statement; the builder supplies the depth requested by the CTE declaration.
    /// <summary>Renders the trailing <c>option (maxrecursion n)</c> clause.</summary>
    public override string? MakeMaxRecursion(int maxRecursion, KeywordCase keywordCase = KeywordCase.Lower) =>
        Kw(keywordCase, "option (maxrecursion ") + maxRecursion + ")";

    /// <summary>SQL Server renders statement-level hints as a trailing <c>OPTION (...)</c> clause.</summary>
    public override bool SupportsQueryHints => true;

    /// <summary>SQL Server implements the <c>iif</c> conditional function and the <c>choose</c> value picker.</summary>
    public override IIifRenderer Iif => SqlServerIifRenderer.Instance;

    /// <summary>SQL Server is the only provider with the <c>choose</c> value-picker function.</summary>
    public override bool SupportsChoose => true;

    /// <summary>
    /// SQL Server renders <c>current_user</c>/<c>session_user</c> as the ANSI keywords and the
    /// database/schema/version information through <c>db_name()</c>/<c>schema_name()</c>/<c>@@version</c>.
    /// </summary>
    public override ISessionInfoFunctions SessionInfoFunctions => SqlServerSessionInfoFunctions.Instance;

    /// <summary>SQL Server can generate a random UUID through <c>newid()</c>; it has no v7 generator.</summary>
    public override IUuidGenerators UuidGenerators => SqlServerUuidGenerators.Instance;

    /// <summary>SQL Server renders every cross-provider scalar function (<c>lpad</c>/<c>rpad</c> through <c>replicate</c>).</summary>
    public override IScalarFunctions ScalarFunctions => SqlServerScalarFunctions.Instance;

    /// <summary>SQL Server renders the T-SQL-only scalar functions (<c>patindex</c>, <c>quotename</c>, the trigonometric functions, the SQL/JSON constructors and aggregates, …).</summary>
    public override ISqlServerFunctions? SqlServerFunctions => SqlServerSpecificFunctions.Instance;

    /// <summary>Appends the hints as an <c>option (...)</c> clause, folding an existing <c>maxrecursion</c> option into it.</summary>
    public override string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption, KeywordCase keywordCase = KeywordCase.Lower)
    {
        // T-SQL allows only one OPTION clause per statement. When the query also declared a CTE
        // maxrecursion option, fold it into the same clause instead of emitting a second one. The
        // option text is produced by MakeMaxRecursion, so extracting its body stays local to this
        // dialect and the caller does not append it separately.
        var option = Kw(keywordCase, " option (");
        if (maxRecursionOption is not null)
        {
            var open = maxRecursionOption.IndexOf('(');
            var body = maxRecursionOption[(open + 1)..^1];
            return sql + option + body + ", " + string.Join(", ", hints) + ")";
        }

        return sql + option + string.Join(", ", hints) + ")";
    }
}

internal sealed class SqlServerIifRenderer : IIifRenderer
{
    public static readonly SqlServerIifRenderer Instance = new();

    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"iif({condition}, {whenTrue}, {whenFalse})";
}

internal sealed class SqlServerSessionInfoFunctions : ISessionInfoFunctions
{
    public static readonly SqlServerSessionInfoFunctions Instance = new();

    public bool Supports(string name) =>
        name is "current_user" or "session_user" or "current_schema" or "current_database" or "version";

    public string Render(string name) => name switch
    {
        "current_user" => "current_user",
        "session_user" => "session_user",
        "current_schema" => "schema_name()",
        "current_database" => "db_name()",
        "version" => "@@version",
        _ => throw new NotSupportedException($"The {name} session information function is not supported by SQL Server.")
    };
}

internal sealed class SqlServerUuidGenerators : IUuidGenerators
{
    public static readonly SqlServerUuidGenerators Instance = new();

    public bool Supports(string name) => name is "gen_random_uuid";

    public string Render(string name) => name switch
    {
        "gen_random_uuid" => "newid()",
        _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by SQL Server.")
    };
}

internal sealed class SqlServerXmlFunctions : IXmlFunctions
{
    public static readonly SqlServerXmlFunctions Instance = new();

    public bool Supports(string name) => name is "value" or "query" or "exist" or "nodes";

    public string Render(string name, string operand, IReadOnlyList<string> args) =>
        $"{operand}.{name}({string.Join(", ", args)})";
}

internal sealed class SqlServerTableSampleMethods : ITableSampleMethods
{
    public static readonly SqlServerTableSampleMethods Instance = new();

    public bool Supports(TableSampleMethod method) => method == TableSampleMethod.System;

    public string Render(TableSampleMethod method, double percent, double? seed, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var text = SqlKeywords.Of(keywordCase, " tablesample (") + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + SqlKeywords.Of(keywordCase, " percent)");
        return seed is { } value
            ? text + SqlKeywords.Of(keywordCase, " repeatable (") + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
            : text;
    }
}

internal sealed class SqlServerPivotRenderer(SqlServerDialect dialect) : IPivotRenderer
{
    public string RenderPivot(PivotExpression pivot, string source, string aggregateColumn, string forColumn, string alias, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var sb = new StringBuilder(source);
        sb.Append(SqlKeywords.Of(keywordCase, " pivot (")).Append(pivot.Aggregate.ToString().ToLowerInvariant()).Append('(')
          .Append(aggregateColumn).Append(SqlKeywords.Of(keywordCase, ") for ")).Append(forColumn).Append(SqlKeywords.Of(keywordCase, " in ("));

        for (var i = 0; i < pivot.Values.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(dialect.Escape(pivot.Values[i].Value));
        }

        sb.Append("))").Append(dialect.MakeTableAlias(alias, keywordCase));
        return sb.ToString();
    }

    public string RenderUnpivot(PivotExpression pivot, string source, string alias, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var sb = new StringBuilder(source);
        sb.Append(SqlKeywords.Of(keywordCase, " unpivot (")).Append(dialect.Escape(pivot.UnpivotValueColumn!)).Append(SqlKeywords.Of(keywordCase, " for "))
          .Append(dialect.Escape(pivot.UnpivotNameColumn!)).Append(SqlKeywords.Of(keywordCase, " in ("));

        for (var i = 0; i < pivot.Columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(dialect.Escape(pivot.Columns[i].Column));
        }

        sb.Append("))").Append(dialect.MakeTableAlias(alias, keywordCase));
        return sb.ToString();
    }
}

internal sealed class SqlServerLockRenderer : ILockRenderer
{
    public static readonly SqlServerLockRenderer Instance = new();

    public bool UsesTableHints => true;

    public string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower) =>
        Render(mode, LockWaitMode.Wait, keywordCase);

    public string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, (mode == LockMode.Share ? "holdlock" : "updlock") + wait switch
        {
            LockWaitMode.Wait => "",
            LockWaitMode.NoWait => ", nowait",
            LockWaitMode.SkipLocked => ", readpast",
            _ => throw new ArgumentOutOfRangeException(nameof(wait), wait, "Unknown locking wait mode.")
        });
}

internal sealed class SqlServerIndexHintRenderer : IIndexHintRenderer
{
    public static readonly SqlServerIndexHintRenderer Instance = new();

    public bool MergesWithTableHints => true;

    public string? RenderIndexHint(IReadOnlyList<string> indexes, IndexHintKind kind, KeywordCase keywordCase = KeywordCase.Lower)
    {
        if (kind == IndexHintKind.Ignore)
            throw new NotSupportedException("SQL Server has no index-ignore hint; only INDEX(...) is supported.");

        if (indexes.Count == 0)
            throw new NotSupportedException("SQL Server index hints require at least one index name.");

        return SqlKeywords.Of(keywordCase, "index(") + string.Join(", ", indexes) + ")";
    }
}

/// <summary>
/// SQL Server rendering of the culture-invariant CLR format specifiers through the CLR-based
/// <c>FORMAT</c> function, which accepts the same <c>N</c>/<c>F</c>/<c>D</c>/<c>X</c> specifiers and
/// custom date/time format strings. The current language is assumed to be invariant (see the
/// limitations guide); only the portable token subset is validated.
/// </summary>
internal sealed class SqlServerStringFormats : IStringFormatFunctions
{
    internal static readonly SqlServerStringFormats Instance = new();

    public bool SupportsNumber(char specifier) => specifier is 'N' or 'F' or 'D' or 'X';

    public string RenderNumber(string value, char specifier, int precision)
    {
        var clr = precision < 0 ? specifier.ToString() : $"{specifier}{precision}";
        return $"format({value}, '{clr}')";
    }

    public bool SupportsDateFormat(string clrFormat) => TryMapDate(clrFormat, out _);

    public string RenderDate(string value, string clrFormat) =>
        TryMapDate(clrFormat, out var native)
            ? $"format({value}, '{native}')"
            : throw new NotSupportedException($"The date/time format string '{clrFormat}' is not supported by SQL Server.");

    private static bool TryMapDate(string format, out string native)
    {
        var sb = new StringBuilder(format.Length);
        var i = 0;

        while (i < format.Length)
        {
            if (Match(format, i, "yyyy")) { sb.Append("yyyy"); i += 4; }
            else if (Match(format, i, "yy")) { sb.Append("yy"); i += 2; }
            else if (Match(format, i, "MM")) { sb.Append("MM"); i += 2; }
            else if (Match(format, i, "dd")) { sb.Append("dd"); i += 2; }
            else if (Match(format, i, "HH")) { sb.Append("HH"); i += 2; }
            else if (Match(format, i, "mm")) { sb.Append("mm"); i += 2; }
            else if (Match(format, i, "ss")) { sb.Append("ss"); i += 2; }
            else if (format[i] is '-' or '/' or '.' or ':' or ' ') { sb.Append(format[i]); i++; }
            else { native = string.Empty; return false; }
        }

        native = sb.ToString();
        return true;
    }

    private static bool Match(string value, int index, string token) =>
        index + token.Length <= value.Length && string.CompareOrdinal(value, index, token, 0, token.Length) == 0;
}

/// <summary>
/// Renders the cross-provider scalar functions of <see cref="CommonFunctions"/> on SQL Server. The
/// string functions are native (<c>LEFT</c>/<c>RIGHT</c>, <c>REPLICATE</c>, <c>REVERSE</c>,
/// <c>CONCAT_WS</c>, <c>TRANSLATE</c>, <c>ASCII</c>, <c>CHAR</c>, <c>SPACE</c>); <c>lpad</c>/<c>rpad</c>
/// are emulated because T-SQL has no such functions.
/// </summary>
internal sealed class SqlServerScalarFunctions : IScalarFunctions
{
    internal static readonly SqlServerScalarFunctions Instance = new();

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

        // T-SQL has no lpad/rpad: replicate the fill to the requested length, concatenate on the padded
        // side and take that many characters. `right` also truncates an over-long value, matching lpad.
        "lpad" => $"right(replicate({Pad(args)}, {args[1]}) + {args[0]}, {args[1]})",
        "rpad" => $"left({args[0]} + replicate({Pad(args)}, {args[1]}), {args[1]})",
        "repeat" => $"replicate({args[0]}, {args[1]})",
        "reverse" => $"reverse({args[0]})",
        "space" => $"space({args[0]})",
        "concat_ws" => $"concat_ws({string.Join(", ", args)})",
        "translate" => $"translate({args[0]}, {args[1]}, {args[2]})",
        "ascii" => $"ascii({args[0]})",
        "char" => $"char({args[0]})",

        // T-SQL has no bit_length/octet_length; DATALENGTH returns the byte count.
        "bit_length" => $"datalength({args[0]}) * 8",
        "octet_length" => $"datalength({args[0]})",
        "cot" => $"cot({args[0]})",
        // T-SQL DEGREES/RADIANS return the argument's own type, so an integer literal would truncate
        // (radians(180) is 3); cast to float to keep the fractional result.
        "degrees" => $"degrees(cast({args[0]} as float))",
        "radians" => $"radians(cast({args[0]} as float))",
        "pi" => "pi()",
        _ => throw new NotSupportedException($"The {name} function is not supported by SQL Server.")
    };

    private static string Pad(IReadOnlyList<string> args) => args.Count == 3 ? args[2] : "' '";
}

/// <summary>
/// Renders the SQL Server-only T-SQL scalar functions of <see cref="SqlServerFunctions"/>: the string
/// functions (<c>PATINDEX</c>, <c>QUOTENAME</c>, <c>SOUNDEX</c>, <c>DIFFERENCE</c>,
/// <c>STRING_ESCAPE</c>, <c>UNICODE</c>, <c>NCHAR</c>, <c>FORMAT</c>), the trigonometric functions,
/// the date functions (<c>DATENAME</c>, <c>DATE_BUCKET</c>), the binary/system functions
/// (<c>HASHBYTES</c>, <c>NEWSEQUENTIALID</c>) and the SQL/JSON constructors, aggregates and
/// predicates. The date part names are emitted unquoted (the T-SQL convention), the alternating
/// <c>json_object</c>/<c>json_objectagg</c> arguments are joined with the <c>:</c> key separator.
/// </summary>
internal sealed class SqlServerSpecificFunctions : ISqlServerFunctions
{
    internal static readonly SqlServerSpecificFunctions Instance = new();

    /// <inheritdoc/>
    public bool Supports(string name) => name is
        "patindex" or "quotename" or "soundex" or "difference" or "string_escape" or "unicode" or
        "nchar" or "format" or "acos" or "asin" or "atan" or "atn2" or
        "square" or "datename" or "date_bucket" or "hashbytes" or
        "newsequentialid" or "json_array" or "json_object" or "json_arrayagg" or "json_objectagg" or
        "json_contains" or "json_path_exists";

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "patindex" => $"patindex({args[0]}, {args[1]})",
        "quotename" => args.Count == 2 ? $"quotename({args[0]}, {args[1]})" : $"quotename({args[0]})",
        "soundex" => $"soundex({args[0]})",
        "difference" => $"difference({args[0]}, {args[1]})",
        "string_escape" => $"string_escape({args[0]}, {args[1]})",
        "unicode" => $"unicode({args[0]})",
        "nchar" => $"nchar({args[0]})",
        "format" => args.Count == 3
            ? $"format({args[0]}, {args[1]}, {args[2]})"
            : $"format({args[0]}, {args[1]})",
        "acos" or "asin" or "atan" or "square" =>
            $"{name}({args[0]})",
        "atn2" => $"atn2({args[0]}, {args[1]})",
        "datename" => $"datename({DatePart(args[0])}, {args[1]})",
        "date_bucket" => args.Count == 4
            ? $"date_bucket({DatePart(args[0])}, {args[1]}, {args[2]}, {args[3]})"
            : $"date_bucket({DatePart(args[0])}, {args[1]}, {args[2]})",
        "hashbytes" => $"hashbytes({args[0]}, {args[1]})",
        "newsequentialid" => "newsequentialid()",
        "json_array" => $"json_array({string.Join(", ", args)})",
        "json_object" => $"json_object({JsonPairs(args)})",
        "json_arrayagg" => $"json_arrayagg({args[0]})",
        "json_objectagg" => $"json_objectagg({args[0]} : {args[1]})",
        "json_contains" => $"json_contains({args[0]}, {args[1]}, {args[2]})",
        "json_path_exists" => $"json_path_exists({args[0]}, {args[1]})",
        _ => throw new NotSupportedException($"The {name} function is not supported by SQL Server.")
    };

    /// <summary>Strips the surrounding single quotes a constant date-part string is rendered with.</summary>
    private static string DatePart(string value) =>
        value.Length >= 2 && value[0] == '\'' && value[^1] == '\'' ? value[1..^1] : value;

    /// <summary>Renders the alternating key/value arguments of <c>JSON_OBJECT</c> as <c>key : value</c> pairs.</summary>
    private static string JsonPairs(IReadOnlyList<string> args)
    {
        if (args.Count % 2 != 0)
            throw new NotSupportedException("json_object requires an even number of key/value arguments.");

        var parts = new string[args.Count / 2];
        for (var i = 0; i < parts.Length; i++)
            parts[i] = $"{args[i * 2]} : {args[(i * 2) + 1]}";

        return string.Join(", ", parts);
    }
}
