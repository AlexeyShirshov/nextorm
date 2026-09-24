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
    /// <summary>Gets the shared MySQL dialect instance.</summary>
    public static readonly MySqlDialect Instance = new();

    /// <summary>A MySQL derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    /// <summary>
    /// MySQL has no <c>RETURNING</c>; the generated identity is read back with a separate
    /// <c>SELECT LAST_INSERT_ID()</c>.
    /// </summary>
    public override bool SupportsLastInsertId => true;
    /// <inheritdoc/>
    public override string MakeLastInsertId(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "select last_insert_id()");

    // MySQL/MariaDB insert an all-defaults row with the empty-column-list form and accept DEFAULT as a
    // value in the VALUES list.
    /// <inheritdoc/>
    public override bool SupportsDefaultValues => true;
    /// <inheritdoc/>
    public override string MakeDefaultValues(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " () values ()");
    /// <inheritdoc/>
    public override bool SupportsColumnDefault => true;

    /// <summary>
    /// MySQL is not marked as having a native bulk path: <c>MySqlBulkCopy</c> goes through
    /// <c>LOAD DATA LOCAL INFILE</c>, which requires <c>AllowLoadLocalInfile=true</c> on the connection
    /// and a matching server setting, so the portable <c>INSERT ... VALUES</c> path is used instead (it
    /// also returns keys and supports <c>INSERT IGNORE</c>).
    /// </summary>
    public override bool SupportsBulkCopy => false;

    /// <summary>MySQL skips conflicting rows with the <c>INSERT IGNORE</c> head.</summary>
    public override bool SupportsInsertIgnore => true;

    /// <inheritdoc/>
    public override string MakeInsertIgnoreInto(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "insert ignore into ");

    /// <summary>MySQL and MariaDB have a native <c>TRUNCATE TABLE</c>.</summary>
    public override bool SupportsTruncate => true;

    /// <summary>MySQL and MariaDB delete rows based on a join through the multi-table <c>DELETE &lt;alias&gt; FROM ... JOIN</c> form.</summary>
    public override bool SupportsDeleteJoin => true;

    /// <summary>MySQL and MariaDB update rows based on a join through the multi-table <c>UPDATE ... JOIN ... SET</c> form.</summary>
    public override bool SupportsUpdateJoin => true;

    /// <summary>
    /// Renders the MySQL/MariaDB multi-table update: the join specification sits between <c>UPDATE</c> and
    /// <c>SET</c>, and any filter follows the <c>SET</c> list. The <c>SET</c> list names the target through
    /// its alias.
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
        var sql = Kw(keywordCase, "update ") + fromAndJoins + Kw(keywordCase, " set ") + assignments;
        return string.IsNullOrEmpty(whereSql) ? sql : sql + Kw(keywordCase, " where ") + whereSql;
    }

    // MySQL/MariaDB express a key upsert as INSERT ... ON DUPLICATE KEY UPDATE, assigning the incoming
    // value through the VALUES(<column>) function (MariaDB has no `AS new` row alias).
    /// <inheritdoc/>
    public override bool SupportsOnDuplicateKey => true;
    /// <inheritdoc/>
    public override string MakeUpsertValueReference(string column, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "values(") + column + ")";

    // MySQL 8.0.14+ (and MariaDB 10.3+) spell the APPLY surface as an ANSI lateral join, which is
    // the SqlDialectBase default.
    /// <inheritdoc/>
    public override bool SupportsApply => true;

    /// <summary>MySQL/MariaDB support a raw SQL derived table (<c>FROM (&lt;sql&gt;) AS alias</c>).</summary>
    public override bool SupportsRawSqlSource => true;

    /// <summary>MySQL 5.7+ (and MariaDB 10.2+) render statement-level hints as inline optimizer hints.</summary>
    public override bool SupportsQueryHints => true;

    /// <summary>
    /// Renders the statement-level hints as a <c>/*+ ... */</c> optimizer-hint comment immediately after
    /// the top-level <c>select</c> (the position MySQL and MariaDB require; a <c>WITH</c> prefix and
    /// subqueries are skipped). Neither has a <c>maxrecursion</c> option, so
    /// <paramref name="maxRecursionOption"/> is ignored.
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

    // MySQL/MariaDB have a right join but no full join.
    /// <inheritdoc/>
    public override bool SupportsFullJoin => false;

    // MySQL/MariaDB render greatest(...)/least(...) (NULL when any argument is NULL).
    /// <inheritdoc/>
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
    /// <inheritdoc/>
    public override bool SupportsFullText => true;

    /// <inheritdoc/>
    public override string MakeFullText(string functionName, string column, string search) =>
        functionName == "contains"
            ? $"(match({column}) against({search} in boolean mode) > 0)"
            : $"(match({column}) against({search}) > 0)";

    // MySQL/MariaDB store JSON in text columns; the SQL/JSON-shaped surface is expressed through the
    // JSON_EXTRACT/JSON_SET family. json_value unpacks a scalar (JSON_UNQUOTE), json_query keeps the
    // fragment, json_modify maps onto JSON_SET (insert-or-replace, matching JSON_MODIFY's lax path).
    /// <inheritdoc/>
    public override bool SupportsTextJson => true;

    /// <summary>MySQL 5.7+/MariaDB render the arbitrary-value aggregate as <c>ANY_VALUE(x)</c>.</summary>
    public override bool SupportsAnyValueAggregate => true;

    /// <summary>MySQL/MariaDB render the whole session/information family.</summary>
    public override ISessionInfoFunctions SessionInfoFunctions => MySqlSessionInfoFunctions.Instance;

    /// <inheritdoc/>
    public override string MakeTextJsonFunction(string name, IReadOnlyList<string> args) => name switch
    {
        "json_value" => $"json_unquote(json_extract({args[0]}, {args[1]}))",
        "json_query" => $"json_extract({args[0]}, {args[1]})",
        "json_modify" => $"json_set({args[0]}, {args[1]}, {args[2]})",
        _ => base.MakeTextJsonFunction(name, args)
    };

    // JSON_VALID returns 1/0; a predicate compares it with 1, a value context keeps the 0/1 integer.
    /// <inheritdoc/>
    public override string MakeIsJson(string value, bool asPredicate) =>
        asPredicate ? $"(json_valid({value})) = 1" : $"json_valid({value})";

    // MySQL/MariaDB aggregate strings through group_concat (there is no array_agg).
    /// <inheritdoc/>
    public override bool SupportsStringAgg => true;

    /// <inheritdoc/>
    public override string MakeStringAgg(string value, string delimiter) =>
        $"group_concat({value} separator {delimiter})";

    // MySQL/MariaDB express interval arithmetic through date_add/date_sub and timestampdiff.
    /// <inheritdoc/>
    public override bool SupportsDateArithmetic => true;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override string MakeEndOfMonth(string value) => $"last_day({value})";

    /// <inheritdoc/>
    public override string MakeDateFromParts(string year, string month, string day) =>
        $"str_to_date(concat_ws('-', {year}, {month}, {day}), '%Y-%m-%d')";

    // MySQL/MariaDB spell the super-aggregate as a trailing modifier (GROUP BY a, b WITH ROLLUP) and
    // have no CUBE.
    /// <inheritdoc/>
    public override bool SupportsRollup => true;

    /// <inheritdoc/>
    public override string MakeGrouping(string columns, GroupingType groupingType, KeywordCase keywordCase = KeywordCase.Lower) => groupingType switch
    {
        GroupingType.Rollup => columns + Kw(keywordCase, " with rollup"),
        _ => columns
    };

    // MySQL's infix || is a logical OR unless PIPES_AS_CONCAT is set, so concatenation is rendered
    // as the concat function instead of an operator.
    /// <inheritdoc/>
    public override string MakeConcat(IReadOnlyList<string> parts) => $"concat({string.Join(", ", parts)})";

    // MySQL quotes identifiers with backticks; single-quoted aliases are syntax errors.
    /// <inheritdoc/>
    public override string Escape(string keyword) => "`" + keyword + "`";

    /// <summary>MySQL quotes a physical identifier with backticks, doubling an embedded backtick.</summary>
    public override string QuoteIdentifier(string name) => "`" + name.Replace("`", "``") + "`";

    /// <inheritdoc/>
    public override string MakeColumnReference(string name) => Escape(name);

    // MySQL's CAST target has its own type names (there is no cast(... as bigint/integer)); the CLR
    // numeric types map onto the signed/unsigned integer targets instead.
    /// <inheritdoc/>
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
        _ when type == typeof(DateTimeOffset) => "datetime",
        _ when type == typeof(TimeSpan) => "time",
        _ => type.Name
    };

    /// <summary>MySQL has a native duration/time-of-day type (<c>TIME</c>), so a <see cref="TimeSpan"/> is stored natively.</summary>
    public override bool SupportsNativeDuration => true;

    /// <summary>MySQL's native duration type is <c>TIME</c>, optionally with fractional-second precision.</summary>
    public override string MakeDurationType(DurationUnit? unit, int precision = 0)
        => precision > 0 ? $"time({precision})" : "time";

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    // length() counts bytes in MySQL; char_length() counts characters, matching string.Length.
    /// <inheritdoc/>
    public override string MakeStringLength(string value) => $"char_length({value})";

    /// <inheritdoc/>
    protected override string MakeStringPosition(string value, string substring) =>
        $"instr({value}, {substring})";

    /// <inheritdoc/>
    protected override string MakeStringPosition(string value, string substring, string start) =>
        $"locate({substring}, {value}, {start} + 1)";

    /// <inheritdoc/>
    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    /// <inheritdoc/>
    protected override string MakeStringReverse(string value) => $"reverse({value})";

    /// <summary>MySQL/MariaDB render CLR format specifiers through <c>FORMAT</c>/<c>DATE_FORMAT</c>.</summary>
    public override IStringFormatFunctions? StringFormats => MySqlStringFormats.Instance;

    /// <summary>MySQL/MariaDB support a per-expression <c>COLLATE</c> clause.</summary>
    public override bool SupportsCollation => true;

    /// <inheritdoc/>
    public override string MakeCollate(string value, string collation, KeywordCase keywordCase = KeywordCase.Lower) =>
        value + Kw(keywordCase, " collate ") + collation;

    /// <summary>MySQL/MariaDB can express ordinal comparison through the <c>utf8mb4_bin</c> collation.</summary>
    public override bool SupportsOrdinalComparison => true;

    /// <inheritdoc/>
    public override string MakeOrdinal(string value, bool ignoreCase)
    {
        const string Binary = "utf8mb4_bin";
        return ignoreCase ? $"lower({value}) collate {Binary}" : $"{value} collate {Binary}";
    }

    /// <summary>MySQL 8.0.4+ matches with <c>REGEXP_LIKE</c> and replaces with <c>REGEXP_REPLACE</c>.</summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"regexp_like({value}, {QuoteStringLiteral(pattern, escapeBackslash: true)}, {(ignoreCase ? "'i'" : "'c'")})";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"regexp_replace({value}, {QuoteStringLiteral(pattern, escapeBackslash: true)}, {QuoteStringLiteral(replacement, escapeBackslash: true)}, 1, 0, {(ignoreCase ? "'i'" : "'c'")})";

    /// <inheritdoc/>
    public override string MakeStuff(string value, string start, string? count, string newValue) =>
        count is null
            ? $"substring({value}, 1, {start})"
            : $"insert({value}, {start} + 1, {count}, {newValue})";

    // now() is the session time zone; utc_timestamp() is UTC regardless of it.
    /// <inheritdoc/>
    public override string MakeNow(bool utc) => utc ? "utc_timestamp()" : "now()";

    /// <inheritdoc/>
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
    /// <inheritdoc/>
    public override string MakeLikeEscape(string escapeChar, KeywordCase keywordCase = KeywordCase.Lower) =>
        Kw(keywordCase, " escape '") + escapeChar.Replace("\\", "\\\\").Replace("'", "''") + "'";

    // MySQL's ~ yields an unsigned 64-bit value, which overflows the signed CLR integer the
    // projection expects; -(x) - 1 keeps the two's-complement result signed.
    /// <inheritdoc/>
    public override string MakeOnesComplement(string operand) => $"(-({operand}) - 1)";

    /// <inheritdoc/>
    public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
    {
        // MySQL requires LIMIT before OFFSET, and OFFSET is only valid together with LIMIT, so an
        // offset-only page uses the maximum unsigned bigint as the limit.
        if (paging.Limit > 0)
            sqlBuilder.Append(Kw(keywordCase, "limit ")).Append(paging.Limit);
        else if (paging.Offset > 0)
            sqlBuilder.Append(Kw(keywordCase, "limit 18446744073709551615"));

        if (paging.Offset > 0)
            sqlBuilder.Append(Kw(keywordCase, " offset ")).Append(paging.Offset);
    }

    /// <summary>MySQL/MariaDB support <c>USE INDEX</c>/<c>FORCE INDEX</c>/<c>IGNORE INDEX</c>.</summary>
    public override IIndexHintRenderer? IndexHints => MySqlIndexHintRenderer.Instance;

    /// <summary>MySQL/MariaDB support the trailing row-locking clause.</summary>
    public override ILockRenderer Lock => MySqlLockRenderer.Instance;

    /// <summary>MySQL/MariaDB support <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelect => true;

    /// <summary>MySQL/MariaDB accept <c>IF NOT EXISTS</c> on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectIfNotExists => true;

    /// <summary>MySQL/MariaDB accept a column list on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectColumnList => true;
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

    public string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower) =>
        Render(mode, LockWaitMode.Wait, keywordCase);

    public string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase = KeywordCase.Lower) =>
        SqlKeywords.Of(keywordCase, MySqlLockRenderer.RenderToken(mode, wait));

    private static string RenderToken(LockMode mode, LockWaitMode wait)
    {
        if (mode == LockMode.Share)
        {
            if (wait == LockWaitMode.Wait)
                return " lock in share mode";

            return " for share" + LockWaitSuffix(wait);
        }

        return " for update" + LockWaitSuffix(wait);
    }

    private static string LockWaitSuffix(LockWaitMode wait) => wait switch
    {
        LockWaitMode.Wait => "",
        LockWaitMode.NoWait => " nowait",
        LockWaitMode.SkipLocked => " skip locked",
        _ => throw new ArgumentOutOfRangeException(nameof(wait), wait, "Unknown locking wait mode.")
    };
}

internal sealed class MySqlIndexHintRenderer : IIndexHintRenderer
{
    public static readonly MySqlIndexHintRenderer Instance = new();

    public bool MergesWithTableHints => false;

    public string? RenderIndexHint(IReadOnlyList<string> indexes, IndexHintKind kind, KeywordCase keywordCase = KeywordCase.Lower)
    {
        if (indexes.Count == 0)
            throw new NotSupportedException("MySQL/MariaDB index hints require at least one index name.");

        return SqlKeywords.Of(keywordCase, kind switch
        {
            IndexHintKind.Force => " force index (",
            IndexHintKind.Ignore => " ignore index (",
            _ => " use index ("
        }) + string.Join(", ", indexes) + ")";
    }
}

/// <summary>
/// MySQL/MariaDB rendering of the culture-invariant CLR format specifiers: <c>N</c> through
/// <c>format</c> (grouping with the invariant <c>,</c>/<c>.</c> separators), <c>D</c>/<c>X</c> through
/// <c>lpad</c>/<c>hex</c>, and date/time through <c>date_format</c>. The fixed-point <c>F</c> is not
/// offered because MySQL's <c>format</c> always inserts group separators.
/// </summary>
internal sealed class MySqlStringFormats : IStringFormatFunctions
{
    internal static readonly MySqlStringFormats Instance = new();

    public bool SupportsNumber(char specifier) => specifier is 'N' or 'D' or 'X';

    public string RenderNumber(string value, char specifier, int precision) => specifier switch
    {
        'N' => $"format({value}, {Math.Max(precision, 0)})",
        'D' => precision <= 0 ? $"cast({value} as char)" : $"lpad({value}, {precision}, '0')",
        'X' => precision <= 0 ? $"hex({value})" : $"lpad(hex({value}), {precision}, '0')",
        _ => throw new NotSupportedException($"The numeric format specifier '{specifier}' is not supported by MySQL/MariaDB.")
    };

    public bool SupportsDateFormat(string clrFormat) => TryMapDate(clrFormat, out _);

    public string RenderDate(string value, string clrFormat) =>
        TryMapDate(clrFormat, out var native)
            ? $"date_format({value}, '{native}')"
            : throw new NotSupportedException($"The date/time format string '{clrFormat}' is not supported by MySQL/MariaDB.");

    private static bool TryMapDate(string format, out string native)
    {
        var sb = new StringBuilder(format.Length + 6);
        var i = 0;

        while (i < format.Length)
        {
            if (Match(format, i, "yyyy")) { sb.Append("%Y"); i += 4; }
            else if (Match(format, i, "yy")) { sb.Append("%y"); i += 2; }
            else if (Match(format, i, "MM")) { sb.Append("%m"); i += 2; }
            else if (Match(format, i, "dd")) { sb.Append("%d"); i += 2; }
            else if (Match(format, i, "HH")) { sb.Append("%H"); i += 2; }
            else if (Match(format, i, "mm")) { sb.Append("%i"); i += 2; }
            else if (Match(format, i, "ss")) { sb.Append("%S"); i += 2; }
            else if (format[i] is '-' or '/' or '.' or ':' or ' ') { sb.Append(format[i]); i++; }
            else { native = string.Empty; return false; }
        }

        native = sb.ToString();
        return true;
    }

    private static bool Match(string value, int index, string token) =>
        index + token.Length <= value.Length && string.CompareOrdinal(value, index, token, 0, token.Length) == 0;
}
