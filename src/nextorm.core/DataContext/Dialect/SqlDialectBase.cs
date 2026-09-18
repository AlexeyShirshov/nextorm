using System.Text;

namespace nextorm.core;

/// <summary>
/// Default implementations of the dialect contract. Only the parts that genuinely differ between
/// dialects are abstract (<see cref="MakeParam"/>, <see cref="MakePage"/>); everything else has a
/// working generic-SQL default, so a dialect overrides just what is different. Because every member is
/// either abstract or has a real body, a dialect can never inherit a placeholder that throws at runtime.
/// </summary>
public abstract class SqlDialectBase : ISqlDialect
{
    public virtual string ConcatStringOperator => "+";
    public virtual string EmptyString => "''";
    public virtual bool RequireSubqueryAlias => false;
    public virtual bool SupportsRightFullJoin => true;
    public virtual bool SupportsIntersectExceptAll => false;
    public virtual bool SupportsRollup => false;
    public virtual bool SupportsCube => false;
    public virtual bool SupportsApply => false;
    public virtual bool SupportsQueryHints => false;
    public virtual bool SupportsArrays => false;
    public virtual bool SupportsJson => false;
    public virtual bool SupportsTextJson => false;
    public virtual bool SupportsFilter => false;
    public virtual bool SupportsGreatestLeast => false;
    public virtual bool SupportsDateTrunc => false;
    public virtual bool SupportsDateArithmetic => false;
    public virtual bool SupportsStringArrayAggregates => false;
    // The umbrella flag seeds the individual capabilities; a dialect opts out of one of them by
    // overriding it (SQL Server has string_agg but no array_agg).
    public virtual bool SupportsStringAgg => SupportsStringArrayAggregates;
    public virtual bool SupportsArrayAgg => SupportsStringArrayAggregates;
    public virtual bool SupportsExtendedScalarFunctions => false;
    public virtual bool SupportsBooleanAggregates => false;
    public virtual bool SupportsBitAggregates => false;
    public virtual bool SupportsStatisticalAggregates => false;
    public virtual bool SupportsRegressionAggregates => false;
    public virtual bool SupportsArgMinMax => false;
    public virtual bool SupportsIfAggregates => false;
    public virtual bool SupportsOrderedAggregates => false;

    public abstract string MakeParam(string name);
    public abstract void MakePage(Paging paging, StringBuilder sqlBuilder);

    // ANSI super-aggregate form. A provider that spells ROLLUP/CUBE as a trailing modifier
    // (MySQL/MariaDB, ClickHouse) overrides this; only reached through a dialect that opted in.
    public virtual string MakeGrouping(string columns, GroupingType groupingType) => groupingType switch
    {
        GroupingType.Rollup => $"rollup ({columns})",
        GroupingType.Cube => $"cube ({columns})",
        _ => columns
    };

    // ANSI lateral form. A provider whose surface is literally CROSS/OUTER APPLY (SQL Server)
    // overrides this; the base body is only reached through a dialect that opted in with
    // SupportsApply, so it never runs for a provider that cannot express a lateral source.
    public virtual string MakeApply(JoinType applyType, string source) => applyType switch
    {
        JoinType.CrossApply => $" cross join lateral {source}",
        JoinType.OuterApply => $" left join lateral {source} on true",
        _ => throw new ArgumentOutOfRangeException(nameof(applyType), applyType, "Not an APPLY join type")
    };

    // ANSI/SQLite/PostgreSQL form: the RECURSIVE modifier is part of the WITH keyword. SQL Server
    // overrides MakeWith to drop it, and MakeMaxRecursion to expose its depth option.
    public virtual string MakeWith(bool recursive) => recursive ? "with recursive " : "with ";
    public virtual string? MakeMaxRecursion(int maxRecursion) => null;

    // A dialect with a concatenation operator joins the operands with it; a dialect where the
    // operator is not a concatenation overrides this with the concat function.
    public virtual string MakeConcat(IReadOnlyList<string> parts) => string.Join(ConcatStringOperator, parts);

    public virtual string Escape(string keyword) => "'" + keyword + "'";
    public virtual string MakeColumnReference(string name) => name;
    public virtual string MakeTableAlias(string tableAlias) => " as " + Escape(tableAlias);
    public virtual string MakeColumnAlias(string? colAlias) => string.IsNullOrEmpty(colAlias)
        ? string.Empty
        : " as " + Escape(colAlias);

    public virtual string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "smallint",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(int) => "integer",
        _ when type == typeof(long) => "bigint",
        _ when type == typeof(float) => "real",
        _ when type == typeof(double) => "double precision",
        _ when type == typeof(decimal) => "numeric",
        _ => type.Name
    };

    public virtual string MakeBool(bool v) => v ? "1" : "0";
    public virtual string MakeCoalesce(string v1, string v2) => $"isnull({v1},{v2})";
    public virtual string MakeBoolCoalesce(string v1, string v2) => MakeCoalesce(v1, v2);

    // Dialects with a boolean type can return the ANSI CASE unchanged: it is already a valid
    // scalar and (for the boolean case) a valid predicate.
    public virtual string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate) => caseExpression;
    public virtual string MakeAggregate(string name) => name;

    // Scalar function defaults are ANSI/portable; a dialect overrides only where it deviates
    // (SQL Server len/datepart, SQLite strftime/datetime, PostgreSQL/ SQLite natural log, ...).
    public virtual string MakeStringLength(string value) => $"length({value})";
    public virtual string MakeUpper(string value) => $"upper({value})";
    public virtual string MakeLower(string value) => $"lower({value})";
    public virtual string MakeTrim(string value, StringTrimKind kind) => kind switch
    {
        StringTrimKind.Start => $"ltrim({value})",
        StringTrimKind.End => $"rtrim({value})",
        _ => $"trim({value})"
    };
    public virtual string MakeSubstring(string value, string start, string? length) => length is null
        // C# Substring(start) has no SQL equivalent in the ANSI form, so the remaining length is
        // derived from the value: substring(x, start + 1, length(x) - start).
        ? $"substring({value}, {start} + 1, {MakeStringLength(value)} - ({start}))"
        : $"substring({value}, {start} + 1, {length})";
    public virtual string MakeReplace(string value, string oldValue, string newValue) =>
        $"replace({value}, {oldValue}, {newValue})";
    // Dialects with a boolean type can use the predicate unchanged as a scalar.
    public virtual string MakeBooleanPredicate(string predicate, bool asPredicate) => predicate;
    // Dialects with a boolean type can use a boolean value unchanged as a predicate.
    public virtual string MakeBooleanValuePredicate(string value) => value;
    public virtual string MakeDatePart(string part, string value) => $"extract({part} from {value})";
    public virtual string MakeNow(bool utc) => utc ? "now() at time zone 'utc'" : "now()";
    public virtual string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        $"{name}({string.Join(", ", args)})";

    // ANSI/portable defaults. Only reached for the functions their capability flag opts into, so a
    // dialect that does not support one never renders it (the translator rejects the call first).
    public virtual string MakeNullIf(string value, string other) => $"nullif({value}, {other})";
    public virtual string MakeGreatest(IReadOnlyList<string> args) => $"greatest({string.Join(", ", args)})";
    public virtual string MakeLeast(IReadOnlyList<string> args) => $"least({string.Join(", ", args)})";
    public virtual string MakeDateTrunc(string field, string value) => $"date_trunc('{field}', {value})";
    // ANSI/PostgreSQL interval arithmetic; a dialect with a dedicated dateadd-style function overrides
    // this (SQL Server renders dateadd(field, amount, value)).
    public virtual string MakeDateAdd(string field, string amount, string value)
    {
        var unit = field switch
        {
            "quarter" => "3 months",
            "millennium" => "1000 years",
            "century" => "100 years",
            "decade" => "10 years",
            _ => "1 " + field
        };

        return $"{value} + ({amount} * interval '{unit}')";
    }
    public virtual string MakeEndOfMonth(string value) =>
        $"(date_trunc('month', {value}) + interval '1 month - 1 day')";
    public virtual string MakeStringAgg(string value, string delimiter) => $"string_agg({value}, {delimiter})";
    public virtual string MakeArrayAgg(string value) => $"array_agg({value})";
    public virtual string MakeWithinGroup(string aggregate, string orderBy) => $"{aggregate} within group (order by {orderBy})";

    // A user-defined function name is emitted verbatim by default; a dialect that quotes or remaps
    // identifiers overrides this.
    public virtual string MakeFunction(string name, string? schema)
        => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    public virtual string MakeCount(bool distinct, bool big) => distinct ? "count(distinct " : "count(";

    public virtual string MakeSubqueryPredicate(string keyword, string query, bool asPredicate) => $"{keyword}({query})";

    // Reached only through a dialect that set SupportsQueryHints; such a dialect overrides this to
    // place the hints. The base body keeps the contract honest (no throwing placeholder) and lets a
    // provider stage hint support without breaking compilation.
    public virtual string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption)
        => sql;

    public virtual bool MakeTop(int limit, out string? topStmt)
    {
        topStmt = null;
        return false;
    }

    public virtual string? GetPagingOrderBy(QueryCommand queryCommand) => null;
}
