using System.Text;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// SQL Server dialect: bracket-quoted identifiers, <c>@name</c> parameters, <c>offset/fetch</c> paging
/// (which requires an ORDER BY), <c>count_big</c> and a boolean-less rendering of subquery predicates.
/// </summary>
public sealed class SqlServerDialect : SqlDialectBase
{
    public static readonly SqlServerDialect Instance = new();

    public override string MakeParam(string name) => $"@{name}";

    /// <summary>
    /// SQL Server bracket-quotes identifiers. Single quoted aliases (the base default) are accepted
    /// for columns but produce a syntax error for table and derived table aliases.
    /// </summary>
    public override string Escape(string keyword) => "[" + keyword + "]";

    /// <summary>
    /// SQL Server uses a bracket-quoted identifier for references as well, so that aliases that
    /// collide with a T-SQL keyword (e.g. "double") stay usable from an outer query.
    /// </summary>
    public override string MakeColumnReference(string name) => Escape(name);

    /// <summary>A SQL Server derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    /// <summary>SQL Server spells a lateral source as <c>CROSS APPLY</c>/<c>OUTER APPLY</c>.</summary>
    public override bool SupportsApply => true;

    /// <summary>SQL Server supports <c>GROUP BY ROLLUP (...)</c> and <c>GROUP BY CUBE (...)</c>.</summary>
    public override bool SupportsRollup => true;

    public override bool SupportsCube => true;
    public override bool SupportsGroupingSets => true;

    /// <summary>SQL Server 2016+ renders the JSON-as-text functions (<c>json_value</c>, ...) over nvarchar.</summary>
    public override bool SupportsTextJson => true;

    /// <summary>T-SQL <c>isjson</c> returns an <c>int</c>: <c>1</c>/<c>0</c>, so a value context casts it to <c>bit</c>.</summary>
    public override string MakeIsJson(string value, bool asPredicate) =>
        asPredicate ? $"(isjson({value})) = 1" : $"cast(isjson({value}) as bit)";

    /// <summary>SQL Server renders the full-text predicates <c>contains</c>/<c>freetext</c>.</summary>
    public override bool SupportsFullText => true;

    public override string MakeFullText(string functionName, string column, string search) =>
        $"{functionName}({column}, {search})";

    /// <summary>SQL Server supports the <c>TABLESAMPLE</c> table modifier.</summary>
    public override bool SupportsTableSample => true;

    /// <summary>SQL Server supports only the <c>SYSTEM</c> sampling method (there is no <c>BERNOULLI</c>).</summary>
    public override bool SupportsTableSampleMethod(TableSampleMethod method) => method == TableSampleMethod.System;

    /// <summary>SQL Server renders <c>tablesample (percent percent) [repeatable (seed)]</c>.</summary>
    public override string MakeTableSample(TableSampleMethod method, double percent, double? seed)
    {
        var text = " tablesample (" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + " percent)";
        return seed is { } value
            ? text + " repeatable (" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
            : text;
    }

    /// <summary>SQL Server supports the <c>FOR SYSTEM_TIME</c> temporal-table clause, including <c>CONTAINED IN</c>.</summary>
    public override bool SupportsTemporalTable => true;

    /// <summary>SQL Server supports every <c>FOR SYSTEM_TIME</c> kind.</summary>
    public override bool SupportsTemporalKind(TemporalKind kind) => true;

    public override bool SupportsTableFunction(string name) =>
        name is "string_split" or "openjson";

    /// <summary>SQL Server renders table hints as <c>with (hint, ...)</c> after the table name.</summary>
    public override bool SupportsTableHints => true;

    public override string MakeTableHints(IReadOnlyList<string> hints) => $" with ({string.Join(", ", hints)})";

    /// <summary>SQL Server renders the result set as JSON through a trailing <c>FOR JSON</c> clause.</summary>
    public override bool SupportsForJson => true;

    public override string MakeForJson(ForJsonClause clause)
    {
        var sqlBuilder = new StringBuilder("for json ");
        sqlBuilder.Append(clause.Mode == ForJsonMode.Auto ? "auto" : "path");

        if (!string.IsNullOrEmpty(clause.Root))
            sqlBuilder.Append(", root('").Append(clause.Root.Replace("'", "''")).Append("')");

        if (clause.IncludeNullValues)
            sqlBuilder.Append(", include_null_values");

        return sqlBuilder.ToString();
    }

    /// <summary>SQL Server renders the result set as XML through a trailing <c>FOR XML</c> clause.</summary>
    public override bool SupportsForXml => true;

    public override string MakeForXml(ForXmlClause clause)
    {
        var sqlBuilder = new StringBuilder("for xml ");
        sqlBuilder.Append(clause.Mode switch
        {
            ForXmlMode.Raw => "raw",
            ForXmlMode.Auto => "auto",
            ForXmlMode.Explicit => "explicit",
            _ => "path"
        });

        if (!string.IsNullOrEmpty(clause.ElementName))
            sqlBuilder.Append("('").Append(clause.ElementName.Replace("'", "''")).Append("')");

        if (!string.IsNullOrEmpty(clause.Root))
            sqlBuilder.Append(", root('").Append(clause.Root.Replace("'", "''")).Append("')");

        if (clause.Elements)
            sqlBuilder.Append(", elements");

        return sqlBuilder.ToString();
    }

    /// <summary>
    /// SQL Server 2022 introduced <c>greatest()</c>/<c>least()</c> with the standard syntax, so the
    /// base rendering applies. The functions are unavailable on older servers, but the dialect cannot
    /// detect the server version at SQL-generation time.
    /// </summary>
    public override bool SupportsGreatestLeast => true;

    // SQL Server 2012+ supports the ANSI percent_rank()/cume_dist() window functions.
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>SQL Server renders percentiles as the <c>PERCENTILE_CONT</c>/<c>PERCENTILE_DISC</c> analytic functions (no exact ordered-set aggregate form).</summary>
    public override bool SupportsPercentileWindow => true;

    /// <summary>SQL Server 2017 introduced <c>string_agg</c>; it has no array type, so <c>array_agg</c> stays unavailable.</summary>
    public override bool SupportsStringAgg => true;

    public override string MakeApply(JoinType applyType, string source) => applyType switch
    {
        JoinType.CrossApply => $" cross apply {source}",
        JoinType.OuterApply => $" outer apply {source}",
        _ => base.MakeApply(applyType, source)
    };

    public override string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "tinyint",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "bigint",
        _ when type == typeof(float) => "real",
        _ when type == typeof(double) => "float",
        _ when type == typeof(decimal) => "decimal(38, 10)",
        _ => base.MakeTypeName(type)
    };

    // SQL Server has no length(); its equivalent is len().
    public override string MakeStringLength(string value) => $"len({value})";

    protected override string MakeStringPosition(string value, string substring) =>
        $"charindex({substring}, {value})";

    protected override string MakeStringPosition(string value, string substring, string start) =>
        $"charindex({substring}, {value}, {start} + 1)";

    public override string MakeRepeat(string value, string count) => $"replicate({value}, {count})";

    protected override string MakeStringReverse(string value) => $"reverse({value})";

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

    public override bool SupportsDateTruncField(string field) =>
        field is not ("decade" or "century" or "millennium") && base.SupportsDateTruncField(field);

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

    public override string MakeDateDiff(string field, string start, string end)
    {
        // T-SQL datediff uses the singular part names; plural ANSI parts map onto them.
        var part = field switch
        {
            "microseconds" => "microsecond",
            "milliseconds" => "millisecond",
            _ => field
        };

        return $"datediff({part}, {start}, {end})";
    }

    public override string MakeEndOfMonth(string value) => $"eomonth({value})";

    public override string MakeDateFromParts(string year, string month, string day) =>
        $"datefromparts({year}, {month}, {day})";

    public override string MakeNow(bool utc) => utc ? "getutcdate()" : "getdate()";

    // T-SQL has no trunc; the 3-argument round(number, length, function) truncates when function is
    // non-zero. Its round() also requires the length argument, unlike the ANSI/Math single-argument form.
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) => (name, args.Count) switch
    {
        ("trunc", 1) => $"round({args[0]}, 0, 1)",
        ("round", 1) => $"round({args[0]}, 0)",
        _ => base.MakeMathFunction(name, args)
    };

    public override string MakeBooleanPredicate(string predicate, bool asPredicate)
    {
        // T-SQL has no boolean type: a predicate is valid only as a condition, so a scalar use has to
        // be materialised as a bit value (which keeps GetBoolean working).
        return asPredicate
            ? predicate
            : $"cast(case when {predicate} then 1 else 0 end as bit)";
    }

    public override string MakeBooleanValuePredicate(string value)
    {
        // A bit value is not a valid predicate in T-SQL, so it has to be compared with its true
        // literal before it can be used in a condition context (WHERE, CASE test, AND/OR, NOT).
        return $"({value}) = 1";
    }

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
    public override string MakeCoalesce(string v1, string v2) => $"isnull({v1},{v2})";

    public override string MakeBoolCoalesce(string v1, string v2)
    {
        // A bit expression is not a valid predicate in T-SQL, so compare it with 1; the result is
        // still a bit value and therefore remains usable as a projection.
        return $"({MakeCoalesce(v1, v2)}) = 1";
    }

    public override string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate)
    {
        if (!isBooleanResult)
            return caseExpression;

        // T-SQL has no boolean type: a boolean-valued CASE has to be materialised as a bit scalar.
        var bitValue = $"cast({caseExpression} as bit)";

        // A bit scalar is not a valid predicate, so a condition context compares it with 1. The
        // result of that comparison is boolean, which is exactly what the predicate needs.
        return asPredicate ? $"{bitValue} = 1" : bitValue;
    }

    public override string MakeCount(bool distinct, bool big)
    {
        // count_big returns bigint while count returns int; the SQL Server provider can express both.
        if (big)
            return distinct ? "count_big(distinct " : "count_big(";

        return base.MakeCount(distinct, false);
    }

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        sqlBuilder.Append("offset ").Append(paging.Offset).Append(" rows");

        if (paging.Limit > 0)
            sqlBuilder.AppendLine().Append("fetch next ").Append(paging.Limit)
                .Append(paging.HasWithTies ? " rows with ties" : " rows only");
    }

    /// <summary>SQL Server supports <c>TOP(n) WITH TIES</c> and <c>FETCH NEXT ... WITH TIES</c>.</summary>
    public override bool SupportsWithTies => true;

    // SQL Server rejects OFFSET/FETCH without ORDER BY, so a constant sort has to be injected.
    public override string? GetPagingOrderBy(QueryCommand queryCommand)
        => queryCommand.Paging.IsEmpty ? null : "(select null as anyorder)";

    /// <summary>SQL Server renders the inline limit as <c>top(n)</c>, or <c>top(n) with ties</c>.</summary>
    public override bool MakeTop(int limit, bool withTies, out string? topStmt)
    {
        topStmt = withTies ? $"top({limit}) with ties" : $"top({limit})";
        return true;
    }

    // T-SQL has no RECURSIVE keyword: a recursive CTE is declared with `with` alone, so the flag is
    // intentionally ignored.
    public override string MakeWith(bool recursive) => "with ";

    // MAXRECURSION overrides the 100-level default. The option is appended at the end of the
    // statement; the builder supplies the depth requested by the CTE declaration.
    public override string? MakeMaxRecursion(int maxRecursion) => $"option (maxrecursion {maxRecursion})";

    /// <summary>SQL Server renders statement-level hints as a trailing <c>OPTION (...)</c> clause.</summary>
    public override bool SupportsQueryHints => true;

    /// <summary>SQL Server implements the <c>iif</c> conditional function and the <c>choose</c> value picker.</summary>
    public override bool SupportsIif => true;

    /// <summary>SQL Server is the only provider with the <c>choose</c> value-picker function.</summary>
    public override bool SupportsChoose => true;

    /// <summary>SQL Server renders the conditional as <c>iif(condition, whenTrue, whenFalse)</c>.</summary>
    public override string MakeIif(string condition, string whenTrue, string whenFalse) =>
        $"iif({condition}, {whenTrue}, {whenFalse})";

    /// <summary>
    /// SQL Server renders <c>current_user</c>/<c>session_user</c> as the ANSI keywords and the
    /// database/schema/version information through <c>db_name()</c>/<c>schema_name()</c>/<c>@@version</c>.
    /// </summary>
    public override bool SupportsSessionInfoFunctions => true;

    /// <summary>SQL Server supports all five session/information functions.</summary>
    public override bool SupportsSessionInfoFunction(string name) =>
        name is "current_user" or "session_user" or "current_schema" or "current_database" or "version";

    /// <summary>SQL Server maps the information functions onto <c>schema_name()</c>/<c>db_name()</c>/<c>@@version</c>.</summary>
    public override string MakeSessionInfoFunction(string name) => name switch
    {
        "current_user" => "current_user",
        "session_user" => "session_user",
        "current_schema" => "schema_name()",
        "current_database" => "db_name()",
        "version" => "@@version",
        _ => base.MakeSessionInfoFunction(name)
    };

    /// <summary>SQL Server can generate a random UUID through <c>newid()</c>; it has no v7 generator.</summary>
    public override bool SupportsUuidGenerators => true;

    /// <summary>SQL Server supports only the random v4 generator (<c>newid()</c>); <c>uuidv7</c> is unavailable.</summary>
    public override bool SupportsUuidGenerator(string name) => name is "gen_random_uuid";

    /// <summary>SQL Server renders the random v4 generator as <c>newid()</c>.</summary>
    public override string MakeUuidGenerator(string name) => name switch
    {
        "gen_random_uuid" => "newid()",
        _ => base.MakeUuidGenerator(name)
    };

    public override string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption)
    {
        // T-SQL allows only one OPTION clause per statement. When the query also declared a CTE
        // maxrecursion option, fold it into the same clause instead of emitting a second one. The
        // option text is produced by MakeMaxRecursion, so extracting its body stays local to this
        // dialect and the caller does not append it separately.
        if (maxRecursionOption is not null)
        {
            var open = maxRecursionOption.IndexOf('(');
            var body = maxRecursionOption[(open + 1)..^1];
            return $"{sql} option ({body}, {string.Join(", ", hints)})";
        }

        return $"{sql} option ({string.Join(", ", hints)})";
    }
}
