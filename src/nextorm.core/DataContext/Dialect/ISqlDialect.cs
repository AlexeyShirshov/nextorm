using System.Text;

namespace nextorm.core;

/// <summary>
/// Rendering contract of a SQL dialect: everything that differs between providers when SQL text is
/// produced. Kept separate from <see cref="DbContext"/> so that SQL generation does not depend on the
/// whole execution pipeline (connection lifecycle, plan cache, materialization) — see
/// <see cref="SqlBuilder"/> and the expression visitors, which depend on this interface only.
/// <para>
/// Connection/parameter creation (<c>CreateConnection</c>/<c>CreateParam</c>) and column mapping
/// (<c>MapColumnExpression</c>) are deliberately not part of this contract: they are separate axes.
/// </para>
/// </summary>
public interface ISqlDialect
{
    /// <summary>String concatenation operator, e.g. <c>+</c> or <c>||</c>.</summary>
    string ConcatStringOperator { get; }
    /// <summary>SQL literal for an empty string.</summary>
    string EmptyString { get; }
    /// <summary>True for providers that require a derived table (subquery in FROM) to have an alias.</summary>
    bool RequireSubqueryAlias { get; }
    /// <summary>
    /// True when the provider can render <c>right join</c> and <c>full join</c>. SQLite before 3.39
    /// cannot; this is expressed as a capability instead of being special-cased in the SQL builder.
    /// </summary>
    bool SupportsRightFullJoin { get; }
    /// <summary>
    /// True when the provider can render the <c>* ALL</c> variants of INTERSECT and EXCEPT
    /// (<c>intersect all</c> / <c>except all</c>). UNION ALL is not covered by this flag because it is
    /// universally supported. SQL Server does not support either variant, and SQLite has no
    /// <c>intersect all</c>/<c>except all</c> at all, so the safe default is <c>false</c>.
    /// </summary>
    bool SupportsIntersectExceptAll { get; }

    /// <summary>
    /// Renders the opening keyword of a common table expression list (<c>with</c>). Dialects that
    /// support and require the <c>recursive</c> modifier for recursive CTEs (SQLite, PostgreSQL)
    /// emit <c>with recursive</c>; SQL Server declares a recursive CTE with <c>with</c> alone, so the
    /// flag is ignored there. Keeping this on the dialect avoids provider names in <see cref="SqlBuilder"/>.
    /// </summary>
    string MakeWith(bool recursive);
    /// <summary>
    /// Renders the statement-level option that raises the recursion limit (SQL Server
    /// <c>option (maxrecursion n)</c>), or <c>null</c> when the dialect has no such option and relies
    /// on its own default (SQLite, PostgreSQL).
    /// </summary>
    string? MakeMaxRecursion(int maxRecursion);

    /// <summary>Quotes an identifier (alias, keyword).</summary>
    string Escape(string keyword);
    /// <summary>
    /// Quotes a column alias when it is referenced from an outer query. Providers that emit quoted
    /// aliases (so they survive as case-sensitive identifiers) must quote the reference accordingly.
    /// </summary>
    string MakeColumnReference(string name);
    string MakeTableAlias(string tableAlias);
    string MakeColumnAlias(string? colAlias);
    /// <summary>Renders a parameter placeholder, e.g. <c>@name</c> or <c>$name</c>.</summary>
    string MakeParam(string name);
    /// <summary>SQL type name used when a CLR conversion has to be rendered as a database cast.</summary>
    string MakeTypeName(Type type);
    string MakeBool(bool v);
    string MakeCoalesce(string v1, string v2);
    /// <summary>
    /// Coalesce over boolean operands. Dialects without a boolean type usable as a predicate
    /// (SQL Server) return an expression that is valid both as a value and as a condition.
    /// </summary>
    string MakeBoolCoalesce(string v1, string v2);
    /// <summary>
    /// Renders a provider-neutral <c>case when ... then ... else ... end</c> expression.
    /// <paramref name="isBooleanResult"/> is true when the CASE yields a boolean value and
    /// <paramref name="asPredicate"/> is true when it is used as a condition (WHERE/HAVING).
    /// Dialects with a real boolean type return the text unchanged; a dialect without one
    /// (SQL Server) has to materialise the integer result as a bit scalar and, in a condition
    /// context, compare it with 1 so that it stays valid both as a value and as a predicate.
    /// </summary>
    string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate);
    /// <summary>
    /// Maps an aggregate function name to the provider specific one, e.g. stdev -> stddev.
    /// </summary>
    string MakeAggregate(string name);
    /// <summary>Renders a string length function over <paramref name="value"/> (SQL Server <c>len</c>, others <c>length</c>).</summary>
    string MakeStringLength(string value);
    /// <summary>Renders an upper case conversion.</summary>
    string MakeUpper(string value);
    /// <summary>Renders a lower case conversion.</summary>
    string MakeLower(string value);
    /// <summary>Renders a leading/trailing whitespace trim.</summary>
    string MakeTrim(string value, StringTrimKind kind);
    /// <summary>
    /// Renders a substring. <paramref name="start"/> is the zero-based C# index; the dialect adds the
    /// one-based offset and, when <paramref name="length"/> is null, derives the remaining length.
    /// </summary>
    string MakeSubstring(string value, string start, string? length);
    /// <summary>Renders a string replace.</summary>
    string MakeReplace(string value, string oldValue, string newValue);
    /// <summary>
    /// Renders a boolean-valued predicate. Dialects without a boolean type (SQL Server) materialise it
    /// as a bit scalar when it is used as a value instead of as a condition.
    /// </summary>
    string MakeBooleanPredicate(string predicate, bool asPredicate);
    /// <summary>
    /// Renders a boolean value as a predicate. Dialects with a boolean type return the value
    /// unchanged; a dialect without one (SQL Server) has to compare it with its true literal, so
    /// that a bare boolean value (e.g. a bit column) can be used as a condition.
    /// </summary>
    string MakeBooleanValuePredicate(string value);
    /// <summary>Renders a date/time part extraction (<c>year</c>, <c>month</c>, <c>day</c>, <c>hour</c>, ...).</summary>
    string MakeDatePart(string part, string value);
    /// <summary>Renders the current local or UTC date/time.</summary>
    string MakeNow(bool utc);
    /// <summary>Renders a math function call with the given already-rendered arguments.</summary>
    string MakeMathFunction(string name, IReadOnlyList<string> args);
    /// <summary>
    /// Renders a user-defined scalar function name (<see cref="SqlFunctionAttribute"/>), optionally
    /// schema/owner qualified. The default is the verbatim name; a dialect may override it to quote or
    /// remap the identifier.
    /// </summary>
    string MakeFunction(string name, string? schema)
        => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";
    /// <summary>
    /// Renders the opening fragment of a count aggregate. <paramref name="big"/> requests a 64-bit
    /// count; dialects where <c>count</c> already returns a 64-bit integer ignore the flag.
    /// </summary>
    string MakeCount(bool distinct, bool big);
    /// <summary>
    /// Renders a subquery predicate (exists/any/all). <paramref name="asPredicate"/> is true when the
    /// expression is used as a condition (WHERE/HAVING) rather than as a projected value; a dialect
    /// without a boolean type (SQL Server) has to render the two forms differently.
    /// </summary>
    string MakeSubqueryPredicate(string keyword, string query, bool asPredicate);

    void MakePage(Paging paging, StringBuilder sqlBuilder);
    /// <summary>
    /// Renders a <c>TOP(n)</c>-style limit clause. Returns false when the dialect cannot express the
    /// limit inline and paging must be rendered by <see cref="MakePage"/> instead.
    /// </summary>
    bool MakeTop(int limit, out string? topStmt);
    /// <summary>
    /// ORDER BY that paging has to inject for dialects that reject a page clause without sorting
    /// (SQL Server), or <c>null</c> when sorting is not required. Encoding the capability as a nullable
    /// result keeps the two pieces of the decision together and avoids a throwing default.
    /// </summary>
    string? GetPagingOrderBy(QueryCommand queryCommand);
}

/// <summary>Which side of a string <see cref="string.Trim()"/> removes whitespace from.</summary>
public enum StringTrimKind
{
    Both,
    Start,
    End
}
