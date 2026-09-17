using System.Text;
using nextorm.core;

namespace nextorm.sqlserver;

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

    // SQL Server extracts date parts through datepart(part, value).
    public override string MakeDatePart(string part, string value) => $"datepart({part}, {value})";

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
            sqlBuilder.AppendLine().Append("fetch next ").Append(paging.Limit).Append(" rows only");
    }

    // SQL Server rejects OFFSET/FETCH without ORDER BY, so a constant sort has to be injected.
    public override string? GetPagingOrderBy(QueryCommand queryCommand)
        => queryCommand.Paging.IsEmpty ? null : "(select null as anyorder)";

    public override bool MakeTop(int limit, out string? topStmt)
    {
        topStmt = $"top({limit})";
        return true;
    }

    // T-SQL has no RECURSIVE keyword: a recursive CTE is declared with `with` alone, so the flag is
    // intentionally ignored.
    public override string MakeWith(bool recursive) => "with ";

    // MAXRECURSION overrides the 100-level default. The option is appended at the end of the
    // statement; the builder supplies the depth requested by the CTE declaration.
    public override string? MakeMaxRecursion(int maxRecursion) => $"option (maxrecursion {maxRecursion})";
}
