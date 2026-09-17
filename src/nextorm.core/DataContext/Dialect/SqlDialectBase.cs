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

    public abstract string MakeParam(string name);
    public abstract void MakePage(Paging paging, StringBuilder sqlBuilder);

    // ANSI/SQLite/PostgreSQL form: the RECURSIVE modifier is part of the WITH keyword. SQL Server
    // overrides MakeWith to drop it, and MakeMaxRecursion to expose its depth option.
    public virtual string MakeWith(bool recursive) => recursive ? "with recursive " : "with ";
    public virtual string? MakeMaxRecursion(int maxRecursion) => null;

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

    // A user-defined function name is emitted verbatim by default; a dialect that quotes or remaps
    // identifiers overrides this.
    public virtual string MakeFunction(string name, string? schema)
        => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    public virtual string MakeCount(bool distinct, bool big) => distinct ? "count(distinct " : "count(";

    public virtual string MakeSubqueryPredicate(string keyword, string query, bool asPredicate) => $"{keyword}({query})";

    public virtual bool MakeTop(int limit, out string? topStmt)
    {
        topStmt = null;
        return false;
    }

    public virtual string? GetPagingOrderBy(QueryCommand queryCommand) => null;
}
