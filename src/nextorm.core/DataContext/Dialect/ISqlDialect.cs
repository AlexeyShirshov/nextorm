using System.Text;

namespace NextORM.Core;

/// <summary>
/// Rendering contract of a SQL dialect: everything that differs between providers when SQL text is
/// produced. Kept separate from <see cref="DataContext"/> so that SQL generation does not depend on the
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
    /// True when a scalar subquery used as an expression raises an error if it returns more than one
    /// row. PostgreSQL, SQL Server, MySQL, MariaDB and ClickHouse do; SQLite silently takes the first
    /// row, so <c>Single</c>/<c>SingleOrDefault</c> cannot rely on the engine there.
    /// </summary>
    bool EnforcesScalarSubqueryCardinality { get; }
    /// <summary>
    /// True when the provider can render <c>right join</c>. A full join additionally requires
    /// <see cref="SupportsFullJoin"/>; they are separate because MySQL/MariaDB have one but not the
    /// other. SQLite before 3.39 cannot render either; this is expressed as a capability instead of
    /// being special-cased in the SQL builder.
    /// </summary>
    bool SupportsRightFullJoin { get; }
    /// <summary>
    /// True when the provider can render <c>full join</c>. MySQL/MariaDB implement <c>right join</c>
    /// but not <c>full join</c>, so the two capabilities are separate.
    /// </summary>
    bool SupportsFullJoin { get; }
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
    /// True when the provider understands the join strictness/kind modifiers in
    /// <see cref="JoinStrictness"/> (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>). The safe default is
    /// <c>false</c>; only ClickHouse opts in.
    /// </summary>
    bool SupportsJoinStrictness { get; }
    /// <summary>
    /// True when the provider understands the ClickHouse <c>GLOBAL</c> join modifier (the right-hand
    /// side is resolved once and broadcast). The safe default is <c>false</c>; only ClickHouse opts in.
    /// </summary>
    bool SupportsGlobalJoin { get; }
    /// <summary>
    /// Renders the join keyword for <paramref name="joinType"/> with an optional
    /// <see cref="JoinStrictness"/> modifier and/or the <c>GLOBAL</c> modifier. A dialect that did not
    /// opt in with <see cref="SupportsJoinStrictness"/>/<see cref="SupportsGlobalJoin"/> is only ever
    /// asked for <see cref="JoinStrictness.Default"/> and <c>false</c>.
    /// </summary>
    string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal);
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
    /// True when the provider can render an explicit <c>GROUP BY GROUPING SETS (...)</c> list. The safe
    /// default is <c>false</c>; MySQL/MariaDB have no grouping sets, so they leave it off.
    /// </summary>
    bool SupportsGroupingSets { get; }
    /// <summary>
    /// Renders a grouping list with the given super-aggregate <paramref name="groupingType"/> over the
    /// already-rendered, comma-separated <paramref name="columns"/>. The default is the ANSI
    /// <c>rollup (columns)</c>/<c>cube (columns)</c> form; a provider that spells it
    /// <c>columns WITH ROLLUP</c> overrides this.
    /// </summary>
    string MakeGrouping(string columns, GroupingType groupingType);
    /// <summary>
    /// Renders <c>GROUPING SETS (...)</c> over the already-rendered, parenthesised
    /// <paramref name="groupingSets"/> (each entry is a returned <c>(a, b)</c>/<c>()</c> string).
    /// </summary>
    string MakeGroupingSets(IReadOnlyList<string> groupingSets);
    /// <summary>
    /// True when the provider can render statement-level query hints (see <see cref="QueryCommand.Hints"/>).
    /// The safe default is <c>false</c>; a command that carries hints is rejected by the SQL builder
    /// on a dialect that does not opt in.
    /// </summary>
    bool SupportsQueryHints { get; }
    /// <summary>
    /// True when the provider can render table-level hints on a physical <c>FROM</c> source
    /// (<c>WITH (...)</c>). The safe default is <c>false</c>; a command that carries table hints is
    /// rejected by the SQL builder on a dialect that does not opt in.
    /// </summary>
    bool SupportsTableHints { get; }
    /// <summary>
    /// True when the provider can render a trailing <c>FOR JSON</c> clause (SQL Server). The safe
    /// default is <c>false</c>; a command that carries one is rejected on a dialect that does not opt in.
    /// </summary>
    bool SupportsForJson { get; }
    /// <summary>
    /// True when the provider can render a trailing <c>FOR XML</c> clause (SQL Server). The safe
    /// default is <c>false</c>.
    /// </summary>
    bool SupportsForXml { get; }
    /// <summary>
    /// True when the provider has an array type and can render the array surface: array-typed
    /// parameters used with the <c>any</c>/<c>all</c> quantifiers (<c>column = any(@array)</c>) and the
    /// array functions in <see cref="CommonFunctions"/>. The safe default is <c>false</c>; only
    /// PostgreSQL opts in today.
    /// </summary>
    bool SupportsArrays { get; }
    /// <summary>
    /// True when the provider has an array type and can render the array functions of
    /// <see cref="ClickHouseFunctions"/> over array <em>columns</em> (for example <c>length</c>,
    /// <c>has</c>, <c>indexOf</c>, <c>arrayStringConcat</c>, <c>hasAny</c>/<c>hasAll</c>,
    /// <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>). The safe default is
    /// <c>false</c>; only ClickHouse opts in today.
    /// </summary>
    bool SupportsArrayFunctions { get; }
    /// <summary>
    /// True when the provider can render the CLR <c>string.Split</c> call as a scalar array via
    /// <see cref="MakeStringSplit"/> (<c>splitByChar(separator, value)</c>). The safe default is
    /// <c>false</c>; only ClickHouse opts in today. PostgreSQL handles <c>string.Split</c> as an array
    /// operand through its own <c>string_to_array</c> path instead.
    /// </summary>
    bool SupportsStringSplit { get; }
    /// <summary>
    /// True when the provider can render <c>arrayJoin(array)</c>, which expands one row per array
    /// element. The safe default is <c>false</c>; only ClickHouse opts in today. See
    /// <see cref="ClickHouseFunctions.array_join{T}(T[])"/>.
    /// </summary>
    bool SupportsArrayJoin { get; }
    /// <summary>
    /// True when the provider has a JSON type and can render the JSON surface: the <c>json</c>/<c>jsonb</c>
    /// functions and the access/containment operators in <see cref="CommonFunctions"/>. The safe default is
    /// <c>false</c>; only PostgreSQL opts in today.
    /// </summary>
    bool SupportsJson { get; }
    /// <summary>
    /// True when the provider can render the JSON-as-text functions of <see cref="CommonFunctions"/>
    /// (<c>json_value</c>, <c>json_query</c>, <c>json_modify</c>, <c>isjson</c>), where JSON is stored in
    /// a normal text column rather than a native type. The safe default is <c>false</c>; SQL Server and
    /// MySQL/MariaDB opt in.
    /// </summary>
    bool SupportsTextJson { get; }
    /// <summary>
    /// True when the provider can render the full-text predicates <c>contains</c> and <c>freetext</c>
    /// (<see cref="CommonFunctions.contains{T}"/>). The safe default is <c>false</c>; SQL Server opts in.
    /// </summary>
    bool SupportsFullText { get; }
    /// <summary>
    /// True when the provider can attach an aggregate filter (<c>FILTER (WHERE ...)</c>). The safe
    /// default is <c>false</c>; the standard clause is rendered by the expression translators and is
    /// accepted by PostgreSQL and SQLite, but not by MySQL/MariaDB or SQL Server.
    /// </summary>
    bool SupportsFilter { get; }
    /// <summary>
    /// True when the provider can render <c>greatest(...)</c>/<c>least(...)</c>. The safe default is
    /// <c>false</c>; PostgreSQL, MySQL/MariaDB, ClickHouse, SQL Server 2022+ and SQLite (through the
    /// scalar <c>max</c>/<c>min</c>) opt in.
    /// </summary>
    bool SupportsGreatestLeast { get; }

    /// <summary>
    /// True when the provider can render the window functions <c>percent_rank()</c> and <c>cume_dist()</c>.
    /// The safe default is <c>false</c>; PostgreSQL, SQL Server, MySQL/MariaDB, SQLite and ClickHouse
    /// opt in.
    /// </summary>
    bool SupportsPercentRankCumeDist { get; }
    /// <summary>
    /// True when the provider can render the window function <c>nth_value(value, n)</c>. The safe default
    /// is <c>false</c>; PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse opt in, while SQL Server has no
    /// <c>NTH_VALUE</c>.
    /// </summary>
    bool SupportsNthValue { get; }
    /// <summary>
    /// True when the provider can render the window (analytic) percentile functions
    /// <c>percentile_cont(fraction)</c>/<c>percentile_disc(fraction)</c> as
    /// <c>name(f) within group (order by value) over (...)</c>. The safe default is <c>false</c>;
    /// SQL Server and MariaDB opt in. PostgreSQL expresses percentiles as an ordered-set aggregate instead
    /// (<see cref="SupportsOrderedAggregates"/>, <c>PostgresFunctions.percentile_cont</c>), and
    /// MySQL/SQLite/ClickHouse cannot express the window form.
    /// </summary>
    bool SupportsPercentileWindow { get; }
    /// <summary>
    /// True when the provider can render <c>date_trunc</c> with a date part name. The safe default is
    /// <c>false</c>; PostgreSQL and SQL Server 2022+ (<c>datetrunc</c>) opt in today.
    /// </summary>
    bool SupportsDateTrunc { get; }
    /// <summary>
    /// True when the provider can render the date arithmetic surface of <see cref="CommonFunctions"/>
    /// (<c>date_add</c> and <c>end_of_month</c>, also used for the <c>DateTime.AddYears</c>/
    /// <c>AddMonths</c>/<c>AddDays</c>/... methods). The safe default is <c>false</c>; PostgreSQL,
    /// SQL Server and ClickHouse opt in today.
    /// </summary>
    bool SupportsDateArithmetic { get; }
    /// <summary>
    /// True when the provider can render the ClickHouse-style date conversion surface of
    /// <see cref="ClickHouseFunctions"/> (<c>toDate</c>/<c>toDateTime</c>/<c>toDate32</c>, the
    /// <c>toYear</c>/... part accessors, <c>toStartOf*</c>, <c>toMonday</c>, <c>toYYYYMM</c>/
    /// <c>toYYYYMMDD</c> and <c>toUnixTimestamp</c>). The safe default is <c>false</c>; only
    /// ClickHouse opts in today. The part accessors render through <see cref="MakeDatePart"/>.
    /// </summary>
    bool SupportsDateConversionFunctions { get; }
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
    /// (<c>SqlFunctions.Sql</c> math/string/date/regular-expression and <c>num_nulls</c>/<c>num_nonnulls</c>
    /// helpers such as <c>asin</c>, <c>split_part</c>, <c>lpad</c>, <c>regexp_replace</c>,
    /// <c>make_date</c>, <c>to_char</c>). The safe default is <c>false</c>; only PostgreSQL opts in
    /// today.
    /// </summary>
    bool SupportsExtendedScalarFunctions { get; }
    /// <summary>
    /// True when the provider can set the seed of its session random generator as a separate side
    /// effect (<see cref="PostgresFunctions.setseed(double?)"/>). The safe default is <c>false</c>;
    /// only PostgreSQL has a standalone <c>setseed</c> (MySQL/MariaDB and SQL Server combine seeding
    /// with the value-returning <c>RAND</c>).
    /// </summary>
    bool SupportsRandomSeed { get; }
    /// <summary>
    /// True when the provider can render the cryptographic hash functions
    /// (<see cref="PostgresFunctions.digest(string?, string?)"/> and
    /// <see cref="PostgresFunctions.sha256(byte[])"/>). The safe default is <c>false</c>; only
    /// PostgreSQL opts in. <c>digest</c> additionally requires the <c>pgcrypto</c> extension to be
    /// installed on the server; <c>sha256</c> is a core binary-string function.
    /// </summary>
    bool SupportsCryptoFunctions { get; }
    /// <summary>
    /// True when the provider can render the native text-search scalar surface of
    /// <see cref="PostgresFunctions"/> (<c>to_tsvector</c>, <c>to_tsquery</c>, <c>plainto_tsquery</c>,
    /// <c>phraseto_tsquery</c>, <c>websearch_to_tsquery</c>, <c>ts_rank</c>, <c>ts_headline</c> and the
    /// <c>@@</c> match operator). The safe default is <c>false</c>; only PostgreSQL opts in. The
    /// cross-provider <c>contains</c>/<c>freetext</c> predicates are gated separately by
    /// <see cref="SupportsFullText"/>.
    /// </summary>
    bool SupportsTextSearchFunctions { get; }
    /// <summary>
    /// True when the provider can render the session/information functions of
    /// <see cref="CommonFunctions"/> (<c>current_user</c>, <c>session_user</c>, <c>current_schema</c>,
    /// <c>current_database</c>, <c>version</c>). The safe default is <c>false</c>; a provider opts in
    /// and lists the individual functions it can express through
    /// <see cref="SupportsSessionInfoFunction(string)"/>.
    /// </summary>
    bool SupportsSessionInfoFunctions { get; }
    /// <summary>
    /// True when the provider can render the session/information function <paramref name="name"/>
    /// (<c>current_user</c>, <c>session_user</c>, <c>current_schema</c>, <c>current_database</c> or
    /// <c>version</c>). The safe default is <c>false</c>; only consulted when
    /// <see cref="SupportsSessionInfoFunctions"/> is <c>true</c>.
    /// </summary>
    bool SupportsSessionInfoFunction(string name);
    /// <summary>
    /// Renders the session/information function <paramref name="name"/>. Only called when
    /// <see cref="SupportsSessionInfoFunction(string)"/> returned <c>true</c> for it. The base
    /// implementation fails with a clear message because the spelling differs per provider.
    /// </summary>
    string MakeSessionInfoFunction(string name);
    /// <summary>
    /// True when the provider can render the UUID generator functions of <see cref="CommonFunctions"/>
    /// (<c>gen_random_uuid</c>, <c>uuidv7</c>). The safe default is <c>false</c>; a provider opts in and
    /// lists the individual generators it can express through <see cref="SupportsUuidGenerator(string)"/>.
    /// </summary>
    bool SupportsUuidGenerators { get; }
    /// <summary>
    /// True when the provider can render the UUID generator <paramref name="name"/>
    /// (<c>gen_random_uuid</c> or <c>uuidv7</c>). The safe default is <c>false</c>; only consulted when
    /// <see cref="SupportsUuidGenerators"/> is <c>true</c>.
    /// </summary>
    bool SupportsUuidGenerator(string name);
    /// <summary>
    /// Renders the UUID generator <paramref name="name"/>. Only called when
    /// <see cref="SupportsUuidGenerator(string)"/> returned <c>true</c> for it. The base implementation
    /// fails with a clear message because the spelling differs per provider.
    /// </summary>
    string MakeUuidGenerator(string name);
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
    /// True when the provider can render the ClickHouse distinct-count aggregates
    /// <c>uniq</c>/<c>uniqExact</c>/<c>uniqCombined</c>/<c>uniqHLL12</c>. The safe default is
    /// <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsUniqAggregates { get; }

    /// <summary>
    /// Renders a distinct-count aggregate (<c>uniq</c>/<c>uniqExact</c>/<c>uniqCombined</c>/<c>uniqHLL12</c>)
    /// over the already-rendered <paramref name="argument"/>. Only called when
    /// <see cref="SupportsUniqAggregates"/> is <c>true</c>. The default renders
    /// <c>MakeAggregate(name)(argument)</c>; ClickHouse wraps it in <c>toInt64(...)</c> because the native
    /// result is <c>UInt64</c>, which the row reader cannot materialise into a CLR integer.
    /// </summary>
    string MakeUniqAggregate(string name, string argument);
    /// <summary>
    /// True when the provider can render the parameterised quantile aggregates
    /// <c>quantile(level)(value)</c> (ClickHouse). The safe default is <c>false</c>; ClickHouse opts in
    /// today. The ordered-set <c>percentile_cont</c> family is a separate capability
    /// (<see cref="SupportsOrderedAggregates"/>).
    /// </summary>
    bool SupportsQuantileAggregates { get; }

    /// <summary>
    /// Renders a parameterised quantile aggregate (<c>quantile(level)(value)</c>) over the already-rendered
    /// <paramref name="level"/> and <paramref name="value"/>. Only called when
    /// <see cref="SupportsQuantileAggregates"/> is <c>true</c>. The default renders
    /// <c>MakeAggregate(name)(level)(value)</c>; ClickHouse wraps it in <c>toFloat64(...)</c> so every
    /// variant materialises as a CLR <see cref="double"/>.
    /// </summary>
    string MakeQuantile(string name, string level, string value);

    /// <summary>
    /// Renders the <c>median</c> aggregate over the already-rendered <paramref name="value"/>. Only
    /// called when <see cref="SupportsQuantileAggregates"/> is <c>true</c>. The default renders
    /// <c>MakeAggregate("median")(value)</c>; ClickHouse wraps it in <c>toFloat64(...)</c> so it
    /// materialises as a CLR <see cref="double"/> for any input type.
    /// </summary>
    string MakeMedian(string value);
    /// <summary>
    /// True when the provider can render the <c>anyLast</c> row-picking aggregate (the value of an
    /// arbitrary last row; <c>ClickHouseFunctions.any_last</c>). The safe default is <c>false</c>;
    /// ClickHouse opts in today. The arbitrary-value aggregate without the last-row restriction is
    /// <see cref="SupportsAnyValueAggregate"/>.
    /// </summary>
    bool SupportsAnyAggregates { get; }

    /// <summary>
    /// True when the provider can render the arbitrary-value aggregate
    /// (<c>CommonFunctions.any_agg</c>): <c>ANY_VALUE(x)</c> on MySQL/MariaDB, <c>any(x)</c>
    /// on ClickHouse. The safe default is <c>false</c>.
    /// </summary>
    bool SupportsAnyValueAggregate { get; }

    /// <summary>
    /// True when the provider can render the string-JSON functions over JSON stored in a text column:
    /// the <c>JSONExtract*</c>/<c>JSONHas</c> and <c>visitParamExtract*</c> families
    /// (<c>ClickHouseFunctions.json_extract_*</c>/<c>visit_param_extract_*</c>) and the JSONPath scalars
    /// <c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>
    /// (<c>ClickHouseFunctions.json_value</c>/<c>json_query</c>/<c>json_exists</c>). This is distinct from
    /// the PostgreSQL JSON type (<see cref="SupportsJson"/>) and the SQL Server/MySQL text-JSON functions
    /// (<see cref="SupportsTextJson"/>). The safe default is <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsJsonExtract { get; }
    /// <summary>
    /// Renders a ClickHouse string-JSON function over the already-rendered <paramref name="args"/>: the
    /// <c>JSONExtract*</c>/<c>visitParamExtract*</c> family and the JSONPath scalars
    /// (<c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>). Only called when
    /// <see cref="SupportsJsonExtract"/> is <c>true</c>. The default renders
    /// <c>name(arg1, arg2, ...)</c>; ClickHouse maps the snake_case name to its native spelling and
    /// casts the unsigned results it cannot materialise.
    /// </summary>
    string MakeJsonExtract(string name, IReadOnlyList<string> args);

    /// <summary>
    /// True when the provider can render the <c>GROUP BY ... WITH TOTALS</c> modifier (ClickHouse): a
    /// grouped query that additionally returns one row with the totals over all groups. The safe default
    /// is <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsGroupByWithTotals { get; }
    /// <summary>
    /// Appends the <c>WITH TOTALS</c> modifier to an already-rendered grouping clause. Only called when
    /// <see cref="SupportsGroupByWithTotals"/> is <c>true</c>. The default returns
    /// <paramref name="grouping"/> unchanged; ClickHouse appends <c> with totals</c>.
    /// </summary>
    string MakeGroupByTotals(string grouping);

    /// <summary>
    /// True when the provider can render the conditional <c>iif(condition, whenTrue, whenFalse)</c>
    /// (<c>CommonFunctions.iif</c>). Every SQL provider opts in with its native spelling
    /// (<c>iif</c>/<c>if</c>/<c>case when</c>); the safe default is <c>false</c>.
    /// </summary>
    bool SupportsIif { get; }

    /// <summary>
    /// True when the provider can render the SQL Server-only <c>choose(index, value, ...)</c>
    /// (<c>SqlServerFunctions.choose</c>). The safe default is <c>false</c>; only SQL Server opts in.
    /// </summary>
    bool SupportsChoose { get; }

    /// <summary>
    /// True when the provider can render the ClickHouse dictionary functions
    /// (<c>dictGet</c>/<c>dictGetOrDefault</c>/<c>dictHas</c>). The safe default is <c>false</c>;
    /// ClickHouse opts in today.
    /// </summary>
    bool SupportsDictionaries { get; }
    /// <summary>
    /// Renders a ClickHouse dictionary function over the already-rendered <paramref name="args"/>. Only
    /// called when <see cref="SupportsDictionaries"/> is <c>true</c>. The default renders
    /// <c>name(arg1, arg2, ...)</c>; ClickHouse maps the snake_case name to its camel-case spelling.
    /// </summary>
    string MakeDictionaryFunction(string name, IReadOnlyList<string> args);

    /// <summary>
    /// True when the provider can render the ordered-set aggregates
    /// <c>percentile_cont</c>/<c>percentile_disc</c>/<c>mode</c> with
    /// <c>WITHIN GROUP (ORDER BY ...)</c>. The safe default is <c>false</c>; only PostgreSQL opts in
    /// today.
    /// </summary>
    bool SupportsOrderedAggregates { get; }
    /// <summary>
    /// True when passing <see cref="System.Data.CommandBehavior.SingleRow"/> to the ADO.NET provider is
    /// safe. The ClickHouse driver translates that hint into an extra <c>LIMIT 1</c> appended to the
    /// statement, which conflicts with the <c>limit</c> the dialect already renders for a single-row
    /// query, so ClickHouse opts out. The default is <c>true</c>.
    /// </summary>
    bool SupportsCommandBehaviorSingleRow { get; }
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
    /// Renders the table-level hint clause appended to a physical table name, or an empty string when
    /// the dialect has no table hints. Only called when <see cref="SupportsTableHints"/> is <c>true</c>
    /// and <paramref name="hints"/> is non-empty.
    /// </summary>
    string MakeTableHints(IReadOnlyList<string> hints);

    /// <summary>
    /// Renders the trailing <c>FOR JSON</c> clause for <paramref name="clause"/>. Only called when
    /// <see cref="SupportsForJson"/> is <c>true</c>.
    /// </summary>
    string MakeForJson(ForJsonClause clause);

    /// <summary>
    /// Renders the trailing <c>FOR XML</c> clause for <paramref name="clause"/>. Only called when
    /// <see cref="SupportsForXml"/> is <c>true</c>.
    /// </summary>
    string MakeForXml(ForXmlClause clause);

    /// <summary>
    /// Renders the full-text predicate <paramref name="functionName"/> (<c>contains</c>/<c>freetext</c>)
    /// over the already-rendered <paramref name="column"/> and <paramref name="search"/> operands. Only
    /// called when <see cref="SupportsFullText"/> is <c>true</c>.
    /// </summary>
    string MakeFullText(string functionName, string column, string search);

    /// <summary>
    /// Renders the text-JSON validity test over the already-rendered <paramref name="value"/>. When
    /// <paramref name="asPredicate"/> is <c>true</c> the result is a boolean predicate, otherwise a
    /// value expression. Only called when <see cref="SupportsTextJson"/> is <c>true</c>.
    /// </summary>
    string MakeIsJson(string value, bool asPredicate);

    /// <summary>
    /// Maps a text-JSON function name (<c>json_value</c>/<c>json_query</c>/<c>json_modify</c>) to the
    /// provider's spelling. Only called when <see cref="SupportsTextJson"/> is <c>true</c>.
    /// </summary>
    string MakeTextJsonFunction(string name);

    /// <summary>
    /// Renders a text-JSON function over the already-rendered <paramref name="args"/>. The default
    /// implementation wraps <see cref="MakeTextJsonFunction(string)"/>, which is enough when the
    /// provider's spelling is a plain function call; a provider whose form nests functions (MySQL
    /// renders <c>json_value</c> as <c>json_unquote(json_extract(...))</c>) overrides this. Only called
    /// when <see cref="SupportsTextJson"/> is <c>true</c>.
    /// </summary>
    string MakeTextJsonFunction(string name, IReadOnlyList<string> args);

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
    /// Renders <paramref name="value"/> repeated <paramref name="count"/> times. The primitive behind
    /// the <c>new string(char, n)</c> translation and <see cref="MakePad"/>.
    /// </summary>
    string MakeRepeat(string value, string count);
    /// <summary>
    /// Renders a left/right pad of <paramref name="value"/> to <paramref name="length"/> with the
    /// single-character literal <paramref name="pad"/>. Matches <see cref="string.PadLeft(int)"/>:
    /// a value that is already at least <paramref name="length"/> long is returned unchanged (the
    /// SQL <c>lpad</c>/<c>rpad</c> family truncates, so the length is guarded).
    /// </summary>
    string MakePad(string value, string length, string pad, bool left);
    /// <summary>
    /// Renders the zero-based index of the first occurrence of <paramref name="substring"/> in
    /// <paramref name="value"/>, or <c>-1</c> when it is absent, matching
    /// <see cref="string.IndexOf(string)"/>. <paramref name="start"/>, when not null, is the
    /// already-rendered zero-based index the search starts at.
    /// </summary>
    string MakeStringIndexOf(string value, string substring, string? start);
    /// <summary>
    /// Renders the zero-based index of the last occurrence of <paramref name="substring"/> in
    /// <paramref name="value"/>, or <c>-1</c> when it is absent, matching
    /// <see cref="string.LastIndexOf(string)"/>. A provider without a character-wise reversal fails
    /// with a clear message.
    /// </summary>
    string MakeStringLastIndexOf(string value, string substring);
    /// <summary>
    /// Renders <see cref="string.Remove(int)"/>/<see cref="string.Remove(int, int)"/> and
    /// <see cref="string.Insert(int, string)"/> as one <c>stuff</c>/<c>overlay</c>-style expression over
    /// the already-rendered zero-based <paramref name="start"/>. A <paramref name="count"/> of null
    /// removes through the end; <paramref name="newValue"/> is spliced in at start otherwise (the
    /// <c>Insert</c> translation passes the string <c>"0"</c> so nothing is removed).
    /// </summary>
    string MakeStuff(string value, string start, string? count, string newValue);
    /// <summary>
    /// Renders the <c>escape</c> clause of a LIKE predicate for <paramref name="escapeChar"/>. The
    /// literal form differs per provider (MySQL requires a doubled backslash to escape the string
    /// delimiter), so the escaping is delegated to the dialect.
    /// </summary>
    string MakeLikeEscape(string escapeChar);
    /// <summary>
    /// Renders the integer ones-complement (<c>~</c>) over the already-rendered <paramref name="operand"/>.
    /// MySQL's <c>~</c> yields an unsigned 64-bit result, so a dialect whose integer complement must
    /// stay signed overrides this.
    /// </summary>
    string MakeOnesComplement(string operand);
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
    /// <summary>
    /// True when the provider accepts <paramref name="part"/> as an <c>extract</c>/<c>date_part</c>
    /// date part. The ANSI parts are accepted by default; a dialect adds the parts it can express
    /// natively (<c>quarter</c>, <c>week</c>, <c>dow</c>, <c>isodow</c>, <c>epoch</c>) by overriding
    /// this together with <see cref="MakeDatePart"/>.
    /// </summary>
    bool SupportsDatePart(string part);
    /// <summary>Renders the current local or UTC date/time.</summary>
    string MakeNow(bool utc);
    /// <summary>Renders a math function call with the given already-rendered arguments.</summary>
    string MakeMathFunction(string name, IReadOnlyList<string> args);
    /// <summary>
    /// Renders a math function call together with the static CLR types of the arguments. A dialect
    /// whose native function has a more specific overload set than ANSI uses the types to pick the
    /// right form (PostgreSQL's two-argument <c>round</c> only accepts <c>numeric</c>, so a
    /// <see cref="double"/>/<see cref="float"/> argument has to be cast). The default ignores the
    /// types and delegates to <see cref="MakeMathFunction(string, IReadOnlyList{string})"/>.
    /// </summary>
    /// <remarks>
    /// Declared as a default interface method so that existing external <see cref="ISqlDialect"/>
    /// implementations that do not render SQL themselves keep compiling.
    /// </remarks>
    string MakeMathFunction(string name, IReadOnlyList<string> args, IReadOnlyList<Type> argTypes) =>
        MakeMathFunction(name, args);
    /// <summary>Renders <c>nullif(value, other)</c>. ANSI and portable, so every dialect accepts it.</summary>
    string MakeNullIf(string value, string other);
    /// <summary>Renders <c>greatest(...)</c> over the already-rendered arguments.</summary>
    string MakeGreatest(IReadOnlyList<string> args);
    /// <summary>Renders <c>least(...)</c> over the already-rendered arguments.</summary>
    string MakeLeast(IReadOnlyList<string> args);
    /// <summary>
    /// Renders <c>iif(condition, whenTrue, whenFalse)</c> over the already-rendered arguments. Only
    /// called when <see cref="SupportsIif"/> is <c>true</c>; each dialect uses its native spelling.
    /// </summary>
    /// <remarks>
    /// Declared as a default interface method so that existing external <see cref="ISqlDialect"/>
    /// implementations that do not render SQL themselves keep compiling. The default body throws:
    /// a provider that reports <see cref="SupportsIif"/> as <c>true</c> must supply its own rendering.
    /// </remarks>
    string MakeIif(string condition, string whenTrue, string whenFalse) =>
        throw new NotSupportedException("The iif conditional function is not supported by this provider.");
    /// <summary>Renders <c>date_trunc(field, value)</c>; <paramref name="field"/> is a validated date-part name.</summary>
    string MakeDateTrunc(string field, string value);
    /// <summary>
    /// Renders date addition (<c>value + amount</c> of <paramref name="field"/>) over the already-rendered
    /// arguments; <paramref name="field"/> is a validated date-part name.
    /// </summary>
    string MakeDateAdd(string field, string amount, string value);
    /// <summary>
    /// Renders the difference between two already-rendered timestamps in <paramref name="field"/> units;
    /// <paramref name="field"/> is a validated date-part name.
    /// </summary>
    string MakeDateDiff(string field, string start, string end);
    /// <summary>Renders the last day of the month of the already-rendered <paramref name="value"/>.</summary>
    string MakeEndOfMonth(string value);
    /// <summary>
    /// Renders a date built from its already-rendered <paramref name="year"/>/<paramref name="month"/>/
    /// <paramref name="day"/> parts.
    /// </summary>
    string MakeDateFromParts(string year, string month, string day);
    /// <summary>
    /// Renders a ClickHouse-style date conversion/truncation function over the already-rendered
    /// arguments (<see cref="ClickHouseFunctions"/>). <paramref name="name"/> is the snake-case method
    /// name (<c>to_date</c>, <c>to_start_of_month</c>, <c>to_monday</c>, <c>to_yyyymm</c>,
    /// <c>to_unix_timestamp</c>, ...); only called when <see cref="SupportsDateConversionFunctions"/>
    /// is <c>true</c>. The part accessors (<c>toYear</c>/...) use <see cref="MakeDatePart"/> instead.
    /// </summary>
    string MakeDateConversion(string name, IReadOnlyList<string> args) =>
        throw new NotSupportedException("The date conversion functions (toDate/toDateTime/toStartOf*/toYYYYMM/toUnixTimestamp) are not supported by this provider.");
    /// <summary>True when the provider accepts <paramref name="field"/> as a <c>date_trunc</c> part.</summary>
    bool SupportsDateTruncField(string field);
    /// <summary>True when the provider accepts <paramref name="field"/> as a <c>date_add</c> part.</summary>
    bool SupportsDateAddField(string field);
    /// <summary>True when the provider accepts <paramref name="field"/> as a <c>date_diff</c> part.</summary>
    bool SupportsDateDiffField(string field);
    /// <summary>Renders the <c>string_agg(value, delimiter)</c> aggregate over the already-rendered arguments.</summary>
    string MakeStringAgg(string value, string delimiter);
    /// <summary>Renders the <c>array_agg(value)</c> aggregate over the already-rendered argument.</summary>
    string MakeArrayAgg(string value);
    /// <summary>
    /// Renders an array function call (<see cref="ClickHouseFunctions"/>) over the already-rendered
    /// <paramref name="call"/> (for example <c>length(tags)</c>). The default returns the call
    /// verbatim; ClickHouse wraps <c>length</c>/<c>indexOf</c> in <c>toInt64(...)</c> because their
    /// native result is <c>UInt64</c>, which the row reader cannot materialise as the declared CLR
    /// integer.
    /// </summary>
    string MakeArrayFunction(string name, string call) => call;
    /// <summary>
    /// Renders the CLR <c>string.Split</c> call as a scalar array over the already-rendered
    /// <paramref name="separator"/> (a one-character SQL string literal) and <paramref name="value"/>
    /// (<c>splitByChar(separator, value)</c> on ClickHouse). Only called when
    /// <see cref="SupportsStringSplit"/> is <c>true</c>; a provider that cannot express a scalar
    /// <c>string.Split</c> never renders it.
    /// </summary>
    string MakeStringSplit(string separator, string value) =>
        throw new NotSupportedException("string.Split is not supported by this provider.");
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
    /// True when the provider supports the built-in table-valued function
    /// <paramref name="name"/> declared in <see cref="CommonFunctions"/> (for example
    /// <c>generate_series</c>, <c>unnest</c>, <c>string_split</c>, <c>openjson</c>,
    /// <c>numbers</c>/<c>numbers_mt</c>, <c>zeros</c>/<c>zeros_mt</c>). User-defined
    /// <see cref="SqlTableFunctionAttribute"/> functions are not gated by this and are emitted
    /// verbatim on every provider.
    /// </summary>
    bool SupportsTableFunction(string name);
    /// <summary>
    /// Renders the opening fragment of a count aggregate. <paramref name="big"/> requests a 64-bit
    /// count; dialects where <c>count</c> already returns a 64-bit integer ignore the flag.
    /// </summary>
    string MakeCount(bool distinct, bool big);
    /// <summary>
    /// True when the provider's count aggregates return a type the row reader cannot materialise as a
    /// CLR integer, so the rendered <c>count(...)</c>/<c>countIf(...)</c> must be wrapped. Defaults to
    /// <c>false</c>; ClickHouse opts in because its count aggregates return <c>UInt64</c>.
    /// </summary>
    bool WrapsCountResult { get; }
    /// <summary>
    /// Wraps a fully rendered <c>count(...)</c> or <c>countIf(...)</c> expression, or returns it
    /// unchanged. Only called when <see cref="WrapsCountResult"/> is <c>true</c>; <paramref name="big"/>
    /// is <c>true</c> for the 64-bit variants (<c>count_big</c>/<c>count_big_distinct</c>). ClickHouse
    /// casts the unsigned result to <c>Int32</c>/<c>Int64</c> so the row reader can materialise it into
    /// the CLR integer the function declares.
    /// </summary>
    string WrapCount(string countExpression, bool big);
    /// <summary>
    /// Renders a subquery predicate (exists/any/all). <paramref name="asPredicate"/> is true when the
    /// expression is used as a condition (WHERE/HAVING) rather than as a projected value; a dialect
    /// without a boolean type (SQL Server) has to render the two forms differently.
    /// </summary>
    string MakeSubqueryPredicate(string keyword, string query, bool asPredicate);
    /// <summary>
    /// True when the provider supports the distributed <c>GLOBAL IN</c> predicate
    /// (<c>SqlFunctions.ClickHouse.global_in</c>). Defaults to <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsGlobalPredicates { get; }

    void MakePage(Paging paging, StringBuilder sqlBuilder);
    /// <summary>
    /// Whether the dialect supports <c>LIMIT n BY expr</c> (ClickHouse). When <c>false</c>, a command that
    /// carries a <see cref="LimitByClause"/> is rejected when its SQL is built.
    /// </summary>
    bool SupportsLimitBy { get; }
    /// <summary>
    /// Renders a <c>LIMIT [offset,] n BY columns</c> clause. Only reached through a dialect that set
    /// <see cref="SupportsLimitBy"/>.
    /// </summary>
    void MakeLimitBy(int limit, int offset, IReadOnlyList<string> columns, StringBuilder sqlBuilder);
    /// <summary>
    /// Whether the dialect supports <c>DISTINCT ON (expr, ...)</c> (PostgreSQL). When <c>false</c>, a
    /// command that carries a <see cref="DistinctOnClause"/> is rejected when its SQL is built.
    /// </summary>
    bool SupportsDistinctOn { get; }
    /// <summary>
    /// Renders the <c>distinct on (columns)</c> prefix (including the trailing space) that replaces a
    /// plain <c>distinct</c>. Only reached through a dialect that set <see cref="SupportsDistinctOn"/>.
    /// </summary>
    string MakeDistinctOn(IReadOnlyList<string> columns);
    /// <summary>
    /// Wraps the rendered table-function call, or returns it unchanged. ClickHouse uses it to cast the
    /// unsigned <c>numbers</c> column to a type the row reader supports.
    /// </summary>
    string WrapTableFunction(string name, string call);
    /// <summary>
    /// Whether the dialect supports the <c>FINAL</c> table modifier (ClickHouse). When <c>false</c>, a
    /// command that carries it is rejected when its SQL is built.
    /// </summary>
    bool SupportsFinal { get; }
    /// <summary>Renders the <c>FINAL</c> modifier. Only reached through a dialect that set <see cref="SupportsFinal"/>.</summary>
    string MakeFinal();
    /// <summary>
    /// Whether the dialect supports the <c>SAMPLE ratio [OFFSET offset]</c> table modifier (ClickHouse).
    /// </summary>
    bool SupportsSample { get; }
    /// <summary>Renders the <c>SAMPLE</c> modifier. Only reached through a dialect that set <see cref="SupportsSample"/>.</summary>
    string MakeSample(double ratio, double offset);
    /// <summary>
    /// Whether the dialect supports the <c>TABLESAMPLE</c> table modifier. When <c>false</c>, a command
    /// that carries a <see cref="TableSampleClause"/> is rejected when its SQL is built.
    /// </summary>
    bool SupportsTableSample { get; }
    /// <summary>
    /// Whether the dialect supports the given <c>TABLESAMPLE</c> sampling method. PostgreSQL supports
    /// both <c>SYSTEM</c> and <c>BERNOULLI</c>; SQL Server only <c>SYSTEM</c>. Only reached through a
    /// dialect that set <see cref="SupportsTableSample"/>.
    /// </summary>
    bool SupportsTableSampleMethod(TableSampleMethod method);
    /// <summary>
    /// Renders the <c>TABLESAMPLE</c> modifier. Only reached through a dialect that set
    /// <see cref="SupportsTableSample"/>.
    /// </summary>
    string MakeTableSample(TableSampleMethod method, double percent, double? seed);
    /// <summary>
    /// Whether the dialect supports the <c>FOR SYSTEM_TIME</c> temporal-table clause. When <c>false</c>,
    /// a command that carries a <see cref="TemporalClause"/> is rejected when its SQL is built. SQL
    /// Server and MariaDB opt in.
    /// </summary>
    bool SupportsTemporalTable { get; }
    /// <summary>
    /// Whether the dialect supports the given <c>FOR SYSTEM_TIME</c> kind. SQL Server supports every
    /// kind; MariaDB has no <c>CONTAINED IN</c>. Only reached through a dialect that set
    /// <see cref="SupportsTemporalTable"/>.
    /// </summary>
    bool SupportsTemporalKind(TemporalKind kind);
    /// <summary>
    /// Renders the <c>FOR SYSTEM_TIME</c> clause. The standard SQL:2011 syntax is shared by SQL Server
    /// and MariaDB; only reached through a dialect that set <see cref="SupportsTemporalTable"/>.
    /// </summary>
    string MakeTemporalTable(TemporalClause clause);
    /// <summary>
    /// Whether the dialect supports the <c>PREWHERE</c> clause (ClickHouse). When <c>false</c>, a command
    /// that carries one is rejected when its SQL is built.
    /// </summary>
    bool SupportsPreWhere { get; }
    /// <summary>
    /// Whether the dialect supports the <c>ARRAY JOIN</c> clause (ClickHouse), which expands one row per
    /// array element. When <c>false</c>, a command that carries one is rejected when its SQL is built.
    /// </summary>
    bool SupportsArrayJoinClause { get; }
    /// <summary>
    /// Renders the <c>[LEFT ]ARRAY JOIN expr, ...</c> clause over the already-rendered array
    /// <paramref name="expressions"/>. Only reached through a dialect that set
    /// <see cref="SupportsArrayJoinClause"/>.
    /// </summary>
    string MakeArrayJoin(ArrayJoinKind kind, IReadOnlyList<string> expressions);
    /// <summary>
    /// Whether the dialect supports the trailing <c>SETTINGS</c> clause (ClickHouse). When <c>false</c>, a
    /// command that carries settings is rejected when its SQL is built.
    /// </summary>
    bool SupportsSettings { get; }
    /// <summary>Renders the trailing <c>SETTINGS</c> clause. Only reached through a dialect that set <see cref="SupportsSettings"/>.</summary>
    string MakeSettings(IReadOnlyList<KeyValuePair<string, string>> settings);
    /// <summary>
    /// Renders a <c>TOP(n)</c>-style limit clause. Returns false when the dialect cannot express the
    /// limit inline and paging must be rendered by <see cref="MakePage"/> instead.
    /// </summary>
    /// <param name="limit">The maximum number of rows to return.</param>
    /// <param name="withTies">Requests the <c>WITH TIES</c> variant where the dialect supports it.</param>
    /// <param name="topStmt">The inline limit fragment, or <c>null</c> when the dialect returns false.</param>
    /// <returns><c>true</c> when <paramref name="topStmt"/> carries the limit; otherwise <c>false</c>.</returns>
    bool MakeTop(int limit, bool withTies, out string? topStmt);
    /// <summary>
    /// Whether the dialect supports a trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause.
    /// When <c>false</c>, a command that carries a <see cref="LockClause"/> is rejected when its SQL is
    /// built. PostgreSQL, MySQL and MariaDB opt in.
    /// </summary>
    bool SupportsLocking { get; }
    /// <summary>Renders the trailing row-locking clause. Only reached through a dialect that set <see cref="SupportsLocking"/>.</summary>
    string MakeLock(LockMode mode);
    /// <summary>
    /// Whether the dialect supports <c>WITH TIES</c> on a page request (<c>FETCH ... WITH TIES</c>,
    /// <c>TOP(n) WITH TIES</c>). When <c>false</c>, a command whose <see cref="Paging.HasWithTies"/> is set
    /// is rejected when its SQL is built. PostgreSQL and SQL Server opt in.
    /// </summary>
    bool SupportsWithTies { get; }
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
