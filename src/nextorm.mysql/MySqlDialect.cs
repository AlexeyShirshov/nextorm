using System.Text;
using nextorm.core;

namespace nextorm.mysql;

/// <summary>
/// MySQL dialect: backtick-quoted identifiers, <c>@name</c> parameters, <c>concat(...)</c>
/// concatenation (the <c>||</c> operator is logical OR by default in MySQL), <c>limit/offset</c>
/// paging and the standard-deviation/variance aggregate name mapping.
/// <para>
/// Declared non-sealed so that <c>nextorm.mariadb</c> can derive its dialect from it.
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

    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    // length() counts bytes in MySQL; char_length() counts characters, matching string.Length.
    public override string MakeStringLength(string value) => $"char_length({value})";

    // now() is the session time zone; utc_timestamp() is UTC regardless of it.
    public override string MakeNow(bool utc) => utc ? "utc_timestamp()" : "now()";

    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev_samp",
        "stdevp" => "stddev_pop",
        "var" => "var_samp",
        "varp" => "var_pop",
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
}
