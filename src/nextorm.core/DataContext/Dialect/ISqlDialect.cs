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
    /// True when the provider can render <c>CROSS APPLY</c>/<c>OUTER APPLY</c> or their lateral
    /// equivalent (<c>CROSS JOIN LATERAL</c> / <c>LEFT JOIN LATERAL ... ON true</c>). SQLite has no
    /// lateral source, so the safe default is <c>false</c>.
    /// </summary>
    bool SupportsApply { get; }
    /// <summary>
    /// Renders a lateral/apply source under <paramref name="applyType"/> (<see cref="JoinType.CrossApply"/>
    /// or <see cref="JoinType.OuterApply"/>) over the already-rendered <paramref name="source"/>. SQL
    /// Server emits <c>cross apply</c>/<c>outer apply</c>; providers that spell it as an ANSI lateral
    /// join emit <c>cross join lateral</c>/<c>left join lateral ... on true</c>.
    /// </summary>
    string MakeApply(JoinType applyType, string source);
    /// <summary>
    /// True when the provider can render the <c>* ALL</c> variants of INTERSECT and EXCEPT
    /// (<c>intersect all</c> / <c>except all</c>). UNION ALL is not covered by this flag because it is
    /// universally supported. SQL Server does not support either variant, and SQLite has no
    /// <c>intersect all</c>/<c>except all</c> at all, so the safe default is <c>false</c>.
    /// </summary>
    bool SupportsIntersectExceptAll { get; }
    /// <summary>
    /// True when the provider can render <c>GROUP BY ROLLUP (...)</c>. The safe default is <c>false</c>;
    /// SQL Server, PostgreSQL, SQLite, MySQL/MariaDB and ClickHouse opt in (the latter group spells it
    /// <c>... WITH ROLLUP</c>).
    /// </summary>
    bool SupportsRollup { get; }
    /// <summary>
    /// True when the provider can render <c>GROUP BY CUBE (...)</c>. The safe default is <c>false</c>;
    /// MySQL/MariaDB have no <c>CUBE</c>, so they leave it off.
    /// </summary>
    bool SupportsCube { get; }
    /// <summary>
    /// Renders a grouping list with the given super-aggregate <paramref name="groupingType"/> over the
    /// already-rendered, comma-separated <paramref name="columns"/>. The default is the ANSI
    /// <c>rollup (columns)</c>/<c>cube (columns)</c> form; a provider that spells it
    /// <c>columns WITH ROLLUP</c> overrides this.
    /// </summary>
    string MakeGrouping(string columns, GroupingType groupingType);
    /// <summary>
    /// True when the provider can render statement-level query hints (see <see cref="QueryCommand.Hints"/>).
    /// The safe default is <c>false</c>; a command that carries hints is rejected by the SQL builder
    /// on a dialect that does not opt in.
    /// </summary>
    bool SupportsQueryHints { get; }
    /// <summary>
    /// True when the provider has an array type and can render the array surface: array-typed
    /// parameters used with the <c>any</c>/<c>all</c> quantifiers (<c>column = any(@array)</c>) and the
    /// array functions in <see cref="NORM.NORM_SQL"/>. The safe default is <c>false</c>; only
    /// PostgreSQL opts in today.
    /// </summary>
    bool SupportsArrays { get; }
    /// <summary>
    /// True when the provider has a JSON type and can render the JSON surface: the <c>json</c>/<c>jsonb</c>
    /// functions and the access/containment operators in <see cref="NORM.NORM_SQL"/>. The safe default is
    /// <c>false</c>; only PostgreSQL opts in today.
    /// </summary>
    bool SupportsJson { get; }
    /// <summary>
    /// True when the provider can render the JSON-as-text functions of <see cref="NORM.NORM_SQL"/>
    /// (<c>json_value</c>, <c>json_query</c>, <c>json_modify</c>), where JSON is stored in a normal
    /// text column rather than a native type. The safe default is <c>false</c>; SQL Server opts in.
    /// </summary>
    bool SupportsTextJson { get; }
    /// <summary>
    /// True when the provider can attach an aggregate filter (<c>FILTER (WHERE ...)</c>). The safe
    /// default is <c>false</c>; the standard clause is rendered by the expression translators and is
    /// accepted by PostgreSQL and SQLite, but not by MySQL/MariaDB or SQL Server.
    /// </summary>
    bool SupportsFilter { get; }
    /// <summary>
    /// True when the provider can render <c>greatest(...)</c>/<c>least(...)</c>. The safe default is
    /// <c>false</c>; PostgreSQL, MySQL/MariaDB, ClickHouse and SQL Server 2022+ opt in, while SQLite
    /// (where the scalar <c>max</c>/<c>min</c> propagate NULL differently) does not.
    /// </summary>
    bool SupportsGreatestLeast { get; }
    /// <summary>
    /// True when the provider can render <c>date_trunc</c> with a date part name. The safe default is
    /// <c>false</c>; PostgreSQL and SQL Server 2022+ (<c>datetrunc</c>) opt in today.
    /// </summary>
    bool SupportsDateTrunc { get; }
    /// <summary>
    /// True when the provider can render the date arithmetic surface of <see cref="NORM.NORM_SQL"/>
    /// (<c>date_add</c> and <c>end_of_month</c>, also used for the <c>DateTime.AddYears</c>/
    /// <c>AddMonths</c>/<c>AddDays</c>/... methods). The safe default is <c>false</c>; PostgreSQL,
    /// SQL Server and ClickHouse opt in today.
    /// </summary>
    bool SupportsDateArithmetic { get; }
    /// <summary>
    /// True when the provider can render the <c>string_agg</c>/<c>array_agg</c> aggregate surface.
    /// The safe default is <c>false</c>; only PostgreSQL opts in today.
    /// <para>
    /// This is the umbrella capability: a dialect that opts into both aggregates sets it, and
    /// <see cref="SupportsStringAgg"/>/<see cref="SupportsArrayAgg"/> default to its value. A provider
    /// that supports only one of the two (SQL Server has <c>string_agg</c> but no array type) overrides
    /// the individual flag instead.
    /// </para>
    /// </summary>
    bool SupportsStringArrayAggregates { get; }
    /// <summary>
    /// True when the provider can render the <c>string_agg(value, delimiter)</c> aggregate. Defaults to
    /// <see cref="SupportsStringArrayAggregates"/>; SQL Server (2017+) opts in on its own.
    /// </summary>
    bool SupportsStringAgg { get; }
    /// <summary>
    /// True when the provider can render the <c>array_agg(value)</c> aggregate. Defaults to
    /// <see cref="SupportsStringArrayAggregates"/>; requires a provider with an array type.
    /// </summary>
    bool SupportsArrayAgg { get; }
    /// <summary>
    /// True when the provider can render the extended scalar function library
    /// (<c>NORM.SQL</c> math/string/date/regular-expression and <c>num_nulls</c>/<c>num_nonnulls</c>
    /// helpers such as <c>asin</c>, <c>split_part</c>, <c>lpad</c>, <c>regexp_replace</c>,
    /// <c>make_date</c>, <c>to_char</c>). The safe default is <c>false</c>; only PostgreSQL opts in
    /// today.
    /// </summary>
    bool SupportsExtendedScalarFunctions { get; }
    /// <summary>
    /// True when the provider can render the boolean aggregates <c>bool_and</c>, <c>bool_or</c> and
    /// <c>every</c>. The safe default is <c>false</c>; only PostgreSQL opts in today.
    /// </summary>
    bool SupportsBooleanAggregates { get; }
    /// <summary>
    /// True when the provider can render the bitwise aggregates <c>bit_and</c>, <c>bit_or</c> and
    /// <c>bit_xor</c>. The safe default is <c>false</c>; PostgreSQL and MySQL/MariaDB support them,
    /// but only PostgreSQL opts in today.
    /// </summary>
    bool SupportsBitAggregates { get; }
    /// <summary>
    /// True when the provider can render the statistical aggregates <c>corr</c>, <c>covar_pop</c> and
    /// <c>covar_samp</c>. The safe default is <c>false</c>; PostgreSQL and ClickHouse opt in today.
    /// </summary>
    bool SupportsStatisticalAggregates { get; }
    /// <summary>
    /// True when the provider can render the regression aggregates of the <c>regr_*</c> family
    /// (<c>regr_slope</c>, <c>regr_intercept</c>, <c>regr_r2</c>, <c>regr_count</c>, <c>regr_avgx</c>,
    /// <c>regr_avgy</c>). The safe default is <c>false</c>; only PostgreSQL opts in today. It is split
    /// from <see cref="SupportsStatisticalAggregates"/> so a provider that has <c>corr</c>/<c>covar_*</c>
    /// but no <c>regr_*</c> (ClickHouse) can opt into one without the other.
    /// </summary>
    bool SupportsRegressionAggregates { get; }
    /// <summary>
    /// True when the provider can render the <c>argMin(value, by)</c>/<c>argMax(value, by)</c>
    /// aggregates. The safe default is <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsArgMinMax { get; }
    /// <summary>
    /// True when the provider renders a filtered aggregate as the ClickHouse combinator
    /// <c>countIf</c>/<c>sumIf</c>/<c>avgIf</c>/<c>minIf</c>/<c>maxIf</c> instead of the ANSI
    /// <c>FILTER (WHERE ...)</c> clause. The safe default is <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsIfAggregates { get; }
    /// <summary>
    /// True when the provider can render the ordered-set aggregates
    /// <c>percentile_cont</c>/<c>percentile_disc</c>/<c>mode</c> with
    /// <c>WITHIN GROUP (ORDER BY ...)</c>. The safe default is <c>false</c>; only PostgreSQL opts in
    /// today.
    /// </summary>
    bool SupportsOrderedAggregates { get; }
    /// <summary>
    /// Applies the statement-level <paramref name="hints"/> to an already-rendered <paramref name="sql"/>
    /// statement. <paramref name="maxRecursionOption"/> is the trailing option produced by
    /// <see cref="MakeMaxRecursion"/> (or <c>null</c>); a dialect that must coalesce it into a single
    /// trailing option clause (SQL Server) uses it so the caller does not append it separately.
    /// <para>
    /// Only called when <see cref="SupportsQueryHints"/> is <c>true</c>; the base implementation returns
    /// the SQL unchanged and is expected to be overridden by an opting-in dialect.
    /// </para>
    /// </summary>
    string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption);

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

    /// <summary>
    /// Renders a string concatenation over the already-rendered operands. Dialects whose infix
    /// operator concatenates (SQLite, PostgreSQL, SQL Server) join the parts with
    /// <see cref="ConcatStringOperator"/>; dialects where that operator means something else or does
    /// not exist (MySQL/MariaDB, ClickHouse) render the <c>concat</c> function instead.
    /// </summary>
    string MakeConcat(IReadOnlyList<string> parts);
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
    /// <summary>Renders <c>nullif(value, other)</c>. ANSI and portable, so every dialect accepts it.</summary>
    string MakeNullIf(string value, string other);
    /// <summary>Renders <c>greatest(...)</c> over the already-rendered arguments.</summary>
    string MakeGreatest(IReadOnlyList<string> args);
    /// <summary>Renders <c>least(...)</c> over the already-rendered arguments.</summary>
    string MakeLeast(IReadOnlyList<string> args);
    /// <summary>Renders <c>date_trunc(field, value)</c>; <paramref name="field"/> is a validated date-part name.</summary>
    string MakeDateTrunc(string field, string value);
    /// <summary>
    /// Renders date addition (<c>value + amount</c> of <paramref name="field"/>) over the already-rendered
    /// arguments; <paramref name="field"/> is a validated date-part name.
    /// </summary>
    string MakeDateAdd(string field, string amount, string value);
    /// <summary>Renders the last day of the month of the already-rendered <paramref name="value"/>.</summary>
    string MakeEndOfMonth(string value);
    /// <summary>Renders the <c>string_agg(value, delimiter)</c> aggregate over the already-rendered arguments.</summary>
    string MakeStringAgg(string value, string delimiter);
    /// <summary>Renders the <c>array_agg(value)</c> aggregate over the already-rendered argument.</summary>
    string MakeArrayAgg(string value);
    /// <summary>
    /// Renders an ordered-set aggregate as <c>&lt;aggregate&gt; within group (order by &lt;orderBy&gt;)</c>.
    /// <paramref name="aggregate"/> is the already-rendered call (for example <c>percentile_cont(0.5)</c>)
    /// and <paramref name="orderBy"/> the already-rendered key list.
    /// </summary>
    string MakeWithinGroup(string aggregate, string orderBy);
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
