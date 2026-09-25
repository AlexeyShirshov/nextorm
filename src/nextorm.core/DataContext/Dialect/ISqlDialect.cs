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
    string MakeApply(JoinType applyType, string source, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>
    /// True when <see cref="MakeApply"/> is valid over a plain physical table source. PostgreSQL and
    /// MySQL spell an applied source as <c>... LATERAL ...</c>, and <c>LATERAL</c> is only allowed
    /// before a subquery, function or composite expression — not a bare table name — so they return
    /// <see langword="false"/> and a plain table is rendered as an ordinary <c>CROSS</c>/<c>LEFT</c>
    /// join instead. SQL Server spells it <c>CROSS APPLY</c>/<c>OUTER APPLY</c>, which accepts a table
    /// name, so it returns <see langword="true"/>. The default is <see langword="false"/> so a dialect
    /// without an opt-in never emits an invalid lateral reference.
    /// </summary>
    bool SupportsApplyOnPlainTable => false;
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
    /// True when the provider understands the ClickHouse <c>SEMI</c>/<c>ANTI</c> join kinds
    /// (<see cref="JoinType.Semi"/>/<see cref="JoinType.Anti"/>), which return only the left-hand
    /// columns. Declared as a default interface method returning <c>false</c> so existing external
    /// implementations keep compiling; only ClickHouse opts in today.
    /// </summary>
    bool SupportsSemiAntiJoin => false;
    /// <summary>
    /// True when the provider understands the ClickHouse <c>PASTE JOIN</c> kind
    /// (<see cref="JoinType.Paste"/>), a position-based join with no <c>ON</c> condition. Declared as a
    /// default interface method returning <c>false</c> so existing external implementations keep
    /// compiling; only ClickHouse opts in today.
    /// </summary>
    bool SupportsPasteJoin => false;
    /// <summary>
    /// Renders the join keyword for <paramref name="joinType"/> with an optional
    /// <see cref="JoinStrictness"/> modifier and/or the <c>GLOBAL</c> modifier. A dialect that did not
    /// opt in with <see cref="SupportsJoinStrictness"/>/<see cref="SupportsGlobalJoin"/> is only ever
    /// asked for <see cref="JoinStrictness.Default"/> and <c>false</c>. The ClickHouse-only
    /// <see cref="JoinType.Semi"/>/<see cref="JoinType.Anti"/>/<see cref="JoinType.Paste"/> kinds are
    /// only requested from a dialect that opted in with
    /// <see cref="SupportsSemiAntiJoin"/>/<see cref="SupportsPasteJoin"/>.
    /// </summary>
    string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal, KeywordCase keywordCase = KeywordCase.Lower);
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
    string MakeGrouping(string columns, GroupingType groupingType, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>
    /// Renders <c>GROUPING SETS (...)</c> over the already-rendered, parenthesised
    /// <paramref name="groupingSets"/> (each entry is a returned <c>(a, b)</c>/<c>()</c> string).
    /// </summary>
    string MakeGroupingSets(IReadOnlyList<string> groupingSets, KeywordCase keywordCase = KeywordCase.Lower);
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
    /// <c>startsWith</c>/<c>endsWith</c>/<c>hasSubstr</c>,
    /// <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>). The safe default is
    /// <c>false</c>; only ClickHouse opts in today.
    /// </summary>
    bool SupportsArrayFunctions { get; }

    /// <summary>
    /// True when the provider translates the higher-order (lambda) array functions of
    /// <see cref="ClickHouseFunctions"/> (<c>arrayMap</c>, <c>arrayFilter</c>, <c>arrayExists</c>,
    /// <c>arrayAll</c>, <c>arrayCount</c>, <c>arrayFirst*</c>, <c>arrayLast*</c>). The safe default is
    /// <c>false</c>; only ClickHouse opts in today. A provider with plain array functions may still
    /// lack lambda syntax, so this is separate from <see cref="SupportsArrayFunctions"/>.
    /// </summary>
    bool SupportsHigherOrderArrayFunctions { get; }

    /// <summary>
    /// True when the provider exposes the row-value surface: the constructor (PostgreSQL <c>ROW(a, b)</c>,
    /// ClickHouse <c>tuple(a, b)</c>) built from <c>Tuple.Create</c>/<c>new Tuple&lt;...&gt;</c>/
    /// <c>new ValueTuple&lt;...&gt;</c> and element access (from <c>.ItemN</c>). Equivalent to
    /// <see cref="Tuple"/> being non-<see langword="null"/>; declared as a default interface method so
    /// existing external implementations keep compiling.
    /// </summary>
    bool SupportsTupleFunctions => Tuple is not null;

    /// <summary>
    /// The provider's renderer for row values / composite tuples, or <see langword="null"/> when the
    /// provider has no row-value type (SQL Server) or does not expose it yet. Declared as a default
    /// interface method so existing external implementations keep compiling.
    /// </summary>
    ITupleRenderer? Tuple => null;

    /// <summary>
    /// The provider's renderer for a scalar <c>string.Split</c>; <c>null</c> means the provider cannot
    /// express it. Declared as a default interface method so that existing external implementations keep
    /// compiling.
    /// </summary>
    IStringSplitRenderer? StringSplit => null;
    /// <summary>
    /// The provider's renderer for the SQLite-only surface (<see cref="SqlFunctions.Sqlite"/> and
    /// <see cref="SqliteFunctions"/>), or <see langword="null"/> when the provider is not SQLite.
    /// Declared as a default interface method so existing external implementations keep compiling.
    /// </summary>
    ISqliteFunctions? SqliteFunctions => null;
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
    /// How the provider attaches a filter to an aggregate. The safe default is
    /// <see cref="AggregateFilterStyle.None"/>, which rejects the filtering overloads; PostgreSQL and
    /// SQLite return <see cref="AggregateFilterStyle.AnsiFilter"/> and ClickHouse returns
    /// <see cref="AggregateFilterStyle.IfCombinator"/>. Declared as a default interface method so that
    /// existing external implementations keep compiling.
    /// </summary>
    AggregateFilterStyle AggregateFilterStyle => AggregateFilterStyle.None;
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
    /// True when the provider can declare named windows (<c>WINDOW w AS (...)</c>) and reference them
    /// with <c>OVER w</c>. The safe default is <c>false</c>; PostgreSQL, MySQL, MariaDB, ClickHouse and
    /// SQLite opt in, while SQL Server has no <c>WINDOW</c> clause.
    /// </summary>
    bool SupportsNamedWindows { get; }
    /// <summary>
    /// True when the provider can render the <c>GROUPS</c> window frame unit (peer groups rather than
    /// physical rows or ordering values). The safe default is <c>false</c>; PostgreSQL, ClickHouse and
    /// SQLite opt in, while SQL Server, MySQL and MariaDB only support <c>ROWS</c>/<c>RANGE</c>.
    /// </summary>
    bool SupportsWindowFrameGroups { get; }
    /// <summary>
    /// True when the provider can render a window frame exclusion
    /// (<c>EXCLUDE CURRENT ROW</c>/<c>GROUP</c>/<c>TIES</c>/<c>NO OTHERS</c>). The safe default is
    /// <c>false</c>; PostgreSQL and SQLite opt in, while SQL Server, MySQL, MariaDB and ClickHouse do
    /// not implement the clause.
    /// </summary>
    bool SupportsWindowFrameExclusion { get; }
    /// <summary>
    /// True when the provider can render the frame-respecting offset window functions
    /// <c>lagInFrame(value[, offset[, default]])</c>/<c>leadInFrame(value[, offset[, default]])</c>
    /// (<c>ClickHouseFunctions.lag_in_frame</c> and <c>ClickHouseFunctions.lead_in_frame</c>). The safe
    /// default is <c>false</c>; only ClickHouse opts in. The standard <c>lag</c>/<c>lead</c> look at the
    /// whole partition and ignore the window frame; these variants are evaluated within the ordered
    /// frame, so they differ on a partial frame.
    /// </summary>
    bool SupportsInFrameWindowFunctions { get; }
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
    /// True when the provider has a native duration/time-of-day type that the driver exposes as
    /// <see cref="System.TimeSpan"/> (PostgreSQL <c>interval</c>, MySQL/MariaDB <c>TIME</c>). When
    /// <see langword="false"/>, a <see cref="System.TimeSpan"/> column is stored in an integer column
    /// in the unit declared by <see cref="DurationAttribute"/> (<see cref="DurationUnit.Ticks"/> by
    /// default) and read back through a conversion. Declared as a default interface method so that
    /// existing external implementations keep compiling and keep their previous (native) behaviour.
    /// </summary>
    bool SupportsNativeDuration => false;

    /// <summary>
    /// Renders the column type of a <see cref="System.TimeSpan"/> property: the native type when
    /// <see cref="SupportsNativeDuration"/> is true, otherwise an integer type wide enough for
    /// <paramref name="unit"/>. <paramref name="unit"/> is <c>null</c> when the property does not
    /// declare one. Declared as a default interface method so that existing external implementations
    /// keep compiling.
    /// </summary>
    /// <param name="unit">The declared storage unit, or <c>null</c>.</param>
    /// <param name="precision">The fractional-second precision of the native type; zero for the provider default.</param>
    /// <returns>The SQL type name.</returns>
    string MakeDurationType(DurationUnit? unit, int precision = 0) => "bigint";

    /// <summary>
    /// Renders the column type of a <em>nullable</em> <see cref="System.TimeSpan"/> property. The
    /// default returns <see cref="MakeDurationType(DurationUnit?, int)"/> because the native types and
    /// the integer columns of most providers already admit SQL NULL; ClickHouse, whose <c>Int64</c> is
    /// not nullable, overrides it with <c>Nullable(...)</c>. Declared as a default interface method so
    /// that existing external implementations keep compiling.
    /// </summary>
    /// <param name="unit">The declared storage unit, or <c>null</c>.</param>
    /// <param name="precision">The fractional-second precision of the native type; zero for the provider default.</param>
    /// <returns>The SQL type name.</returns>
    string MakeNullableDurationType(DurationUnit? unit, int precision = 0) => MakeDurationType(unit, precision);

    /// <summary>
    /// The provider's renderer for the ClickHouse date-conversion surface; <c>null</c> means the provider
    /// cannot express it. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    IDateConversionRenderer? DateConversion => null;
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
    /// The provider's session/information function surface (<c>current_user</c>, <c>session_user</c>,
    /// <c>current_schema</c>, <c>current_database</c>, <c>version</c>). <c>null</c> means the provider
    /// cannot express the family; the object itself answers per name because ClickHouse and SQLite can
    /// express only part of it. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    ISessionInfoFunctions? SessionInfoFunctions => null;

    /// <summary>
    /// The provider's UUID generator surface (<c>gen_random_uuid</c>, <c>uuidv7</c>). <c>null</c> means
    /// the provider cannot express the family; the object itself answers per name because SQL Server and
    /// MariaDB can express only one of the two. Declared as a default interface method so that existing
    /// external implementations keep compiling.
    /// </summary>
    IUuidGenerators? UuidGenerators => null;

    /// <summary>
    /// The provider's renderer for the cross-provider scalar functions of <see cref="CommonFunctions"/>
    /// (<c>left</c>/<c>right</c>, <c>lpad</c>/<c>rpad</c>, <c>repeat</c>/<c>reverse</c>/<c>space</c>,
    /// <c>concat_ws</c>, <c>translate</c>, <c>ascii</c>/<c>char</c>, <c>mod</c>, <c>log10</c>,
    /// <c>power</c>). <c>null</c> means the provider cannot express the family; the object itself
    /// answers per name because the providers can express different subsets. Declared as a default
    /// interface method so that existing external implementations keep compiling.
    /// </summary>
    IScalarFunctions? ScalarFunctions => null;

    /// <summary>
    /// The provider's renderer for the MySQL/MariaDB-only functions of <see cref="MySqlFunctions"/>
    /// (<c>find_in_set</c>/<c>field</c>/<c>elt</c>, <c>substring_index</c>, <c>format</c>,
    /// <c>str_to_date</c>/<c>date_format</c>, <c>from_unixtime</c>/<c>unix_timestamp</c>, <c>md5</c>/
    /// <c>sha1</c>/<c>sha2</c>, <c>inet_aton</c>/<c>inet_ntoa</c>, the JSON mutation family and
    /// <c>uuid_to_bin</c>/<c>bin_to_uuid</c>). <c>null</c> means the provider cannot express the family;
    /// the object itself answers per name because MySQL and MariaDB can express different subsets.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    IMySqlFunctions? MySqlFunctions => null;

    /// <summary>
    /// The provider's renderer for the SQL Server-only T-SQL scalar functions of
    /// <see cref="SqlServerFunctions"/> (<c>patindex</c>, <c>quotename</c>, the trigonometric functions,
    /// <c>datename</c>/<c>date_bucket</c>, <c>hashbytes</c>, the SQL/JSON constructors and aggregates, …).
    /// <c>null</c> means the provider cannot express the family; the object itself answers per name.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    ISqlServerFunctions? SqlServerFunctions => null;

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
    /// The provider's renderer for the ClickHouse distinct-count family; <c>null</c> means the provider
    /// cannot express it. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    IUniqAggregateRenderer? UniqAggregates => null;

    /// <summary>
    /// The provider's renderer for the parameterised quantile aggregates; <c>null</c> means the provider
    /// cannot express them. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    IQuantileAggregateRenderer? QuantileAggregates => null;

    /// <summary>
    /// The provider's renderer for the ClickHouse parameterised top-K aggregates
    /// (<c>topK(N)(value)</c>, <c>topKWeighted(N)(value, weight)</c>); <c>null</c> means the provider
    /// cannot express them. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    ITopKAggregateRenderer? TopKAggregates => null;

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
    /// The provider's renderer for the ClickHouse sequence/funnel aggregates; <c>null</c> means the
    /// provider cannot express them. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    ISequenceAggregateRenderer? SequenceAggregates => null;

    /// <summary>
    /// True when the provider can render the JSON functions over JSON stored in a text column or a native
    /// JSON column: the <c>JSONExtract*</c>/<c>JSONHas</c> and <c>visitParamExtract*</c> families
    /// (<c>ClickHouseFunctions.json_extract_*</c>/<c>visit_param_extract_*</c>), the JSONPath scalars
    /// <c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>
    /// (<c>ClickHouseFunctions.json_value</c>/<c>json_query</c>/<c>json_exists</c>) and the native-JSON
    /// functions <c>JSONAllPaths</c>/<c>JSONAllPathsWithTypes</c>/<c>toJSONString</c>
    /// (<c>ClickHouseFunctions.json_all_paths</c>/<c>json_all_paths_with_types</c>/<c>to_json_string</c>).
    /// The native-JSON functions require a native <c>JSON</c>-valued argument (a <c>String</c> column needs
    /// <c>CAST(col AS JSON)</c>); <c>JSONAllPathsWithTypes</c> returns <c>Map(String, String)</c>, surfaced
    /// as a <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>.
    /// This is distinct from the PostgreSQL JSON type (<see cref="SupportsJson"/>) and the SQL
    /// Server/MySQL text-JSON functions (<see cref="SupportsTextJson"/>). The safe default is
    /// <c>false</c>; ClickHouse opts in today.
    /// </summary>
    bool SupportsJsonExtract { get; }
    /// <summary>
    /// Renders a ClickHouse JSON function over the already-rendered <paramref name="args"/>: the
    /// <c>JSONExtract*</c>/<c>visitParamExtract*</c> family, the JSONPath scalars
    /// (<c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>) and the native-JSON functions
    /// (<c>JSONAllPaths</c>/<c>JSONAllPathsWithTypes</c>/<c>toJSONString</c>). Only called when
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
    string MakeGroupByTotals(string grouping, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// The provider's renderer for the conditional <c>iif(condition, whenTrue, whenFalse)</c>. <c>null</c>
    /// means the provider cannot express it. Declared as a default interface method so that existing
    /// external implementations keep compiling.
    /// </summary>
    IIifRenderer? Iif => null;

    /// <summary>
    /// True when the provider can render the SQL Server-only <c>choose(index, value, ...)</c>
    /// (<c>SqlServerFunctions.choose</c>). The safe default is <c>false</c>; only SQL Server opts in.
    /// </summary>
    bool SupportsChoose { get; }

    /// <summary>
    /// The provider's renderer for the ClickHouse multi-branch conditional; <c>null</c> means the
    /// provider cannot express it. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    IMultiIfRenderer? MultiIf => null;

    /// <summary>
    /// True when the provider can render the ClickHouse dictionary functions
    /// (<c>dictGet</c>/<c>dictGetOrDefault</c>/<c>dictHas</c>/<c>dictGetHierarchy</c>/<c>dictGetChildren</c>/<c>dictIsIn</c>).
    /// The safe default is <c>false</c>; ClickHouse opts in today.
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
    /// True when the provider supports ADO.NET transactions on its connection
    /// (<c>BEGIN</c>/<c>COMMIT</c>/<c>ROLLBACK</c>). Declared as a default interface method returning
    /// <c>true</c> so existing external implementations keep compiling; only ClickHouse opts out,
    /// because its HTTP protocol has no transaction in the ADO.NET sense.
    /// </summary>
    bool SupportsTransactions => true;
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
    string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// Renders the table-level hint clause appended to a physical table name, or an empty string when
    /// the dialect has no table hints. Only called when <see cref="SupportsTableHints"/> is <c>true</c>
    /// and <paramref name="hints"/> is non-empty.
    /// </summary>
    string MakeTableHints(IReadOnlyList<string> hints, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// The provider's renderer for table index hints, or <see langword="null"/> when the provider has no
    /// native index-hint syntax (PostgreSQL without <c>pg_hint_plan</c>, ClickHouse). Declared as a
    /// default interface method so existing external implementations keep compiling; a dialect that
    /// returns <see langword="null"/> rejects a command carrying an index hint.
    /// </summary>
    IIndexHintRenderer? IndexHints => null;

    /// <summary>
    /// Renders the trailing <c>FOR JSON</c> clause for <paramref name="clause"/>. Only called when
    /// <see cref="SupportsForJson"/> is <c>true</c>.
    /// </summary>
    string MakeForJson(ForJsonClause clause, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// Renders the trailing <c>FOR XML</c> clause for <paramref name="clause"/>. Only called when
    /// <see cref="SupportsForXml"/> is <c>true</c>.
    /// </summary>
    string MakeForXml(ForXmlClause clause, KeywordCase keywordCase = KeywordCase.Lower);

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
    /// The provider's renderer for the postfix XML data-type methods (<c>value</c>/<c>query</c>/
    /// <c>exist</c>). <c>null</c> means the provider cannot express them. Declared as a default
    /// interface method so that existing external implementations keep compiling.
    /// </summary>
    IXmlFunctions? XmlFunctions => null;

    /// <summary>
    /// Renders the opening keyword of a common table expression list (<c>with</c>). Dialects that
    /// support and require the <c>recursive</c> modifier for recursive CTEs (SQLite, PostgreSQL)
    /// emit <c>with recursive</c>; SQL Server declares a recursive CTE with <c>with</c> alone, so the
    /// flag is ignored there. Keeping this on the dialect avoids provider names in <see cref="SqlBuilder"/>.
    /// </summary>
    string MakeWith(bool recursive, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>
    /// Renders the statement-level option that raises the recursion limit (SQL Server
    /// <c>option (maxrecursion n)</c>), or <c>null</c> when the dialect has no such option and relies
    /// on its own default (SQLite, PostgreSQL).
    /// </summary>
    string? MakeMaxRecursion(int maxRecursion, KeywordCase keywordCase = KeywordCase.Lower);

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
    /// Quotes a physical identifier (table or column name) with the provider's delimiter, doubling an
    /// embedded delimiter. Used when identifier quoting is enabled on the context
    /// (<c>DataContextBuilder.UseQuotedIdentifiers</c>) or on a single command
    /// (<c>WithQuotedIdentifiers</c>). The default is the ANSI <c>"name"</c> form; a provider with a
    /// different delimiter (SQL Server brackets, MySQL/MariaDB/ClickHouse backticks) overrides it.
    /// <para>
    /// Distinct from <see cref="Escape(string)"/>, which quotes an alias/keyword for the provider and
    /// is single-quoted on SQLite (a valid alias but not a valid identifier). Declared as a default
    /// interface method so that existing external implementations keep compiling.
    /// </para>
    /// </summary>
    string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    /// <summary>
    /// Quotes a column alias when it is referenced from an outer query. Providers that emit quoted
    /// aliases (so they survive as case-sensitive identifiers) must quote the reference accordingly.
    /// </summary>
    string MakeColumnReference(string name);
    /// <summary>Renders the alias clause for a table source (<c>AS alias</c>).</summary>
    string MakeTableAlias(string tableAlias, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>Renders the alias clause for a projected column (<c>AS alias</c>); a null or empty <paramref name="colAlias"/> renders nothing.</summary>
    string MakeColumnAlias(string? colAlias, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>Renders a parameter placeholder, e.g. <c>@name</c> or <c>$name</c>.</summary>
    string MakeParam(string name);
    /// <summary>SQL type name used when a CLR conversion has to be rendered as a database cast.</summary>
    string MakeTypeName(Type type);
    /// <summary>Renders the SQL literal for a boolean value.</summary>
    string MakeBool(bool v);
    /// <summary>Renders <c>coalesce(v1, v2)</c> over the already-rendered operands.</summary>
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
    string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate, KeywordCase keywordCase = KeywordCase.Lower);
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
    /// True when the provider can translate a <see cref="System.Text.RegularExpressions.Regex"/> call with
    /// a constant pattern into native SQL (<c>Regex.IsMatch</c>/<c>Regex.Replace</c>). The safe default is
    /// <c>false</c>; PostgreSQL, MySQL/MariaDB, ClickHouse and SQLite opt in while SQL Server has no
    /// regular-expression engine and stays off. Declared as a default interface method so that existing
    /// external implementations keep compiling.
    /// <para>
    /// The compiled pattern follows the provider's own engine (RE2 on ClickHouse, POSIX/ARE on
    /// PostgreSQL, ICU on MySQL, PCRE on MariaDB, .NET on SQLite), so a pattern is not portable in
    /// general: lookaround and backreferences are rejected by RE2, and the C# and SQL escaping rules
    /// differ. The CLR syntactic knowledge is not translated, only the operator/function choice.
    /// </para>
    /// </summary>
    bool SupportsRegex => false;
    /// <summary>
    /// Renders a regular-expression match predicate over the already-rendered <paramref name="value"/>.
    /// <paramref name="pattern"/> is the raw pattern text (the dialect renders and escapes the string
    /// literal itself) and <paramref name="ignoreCase"/> selects
    /// <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>. Reached only when
    /// <see cref="SupportsRegex"/> is <c>true</c>.
    /// </summary>
    string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        throw new NotSupportedException("Regular-expression matching is not supported by this SQL dialect.");
    /// <summary>
    /// Renders a regular-expression replace over the already-rendered <paramref name="value"/>,
    /// replacing every match as <see cref="System.Text.RegularExpressions.Regex.Replace(string, string, string)"/>
    /// does. <paramref name="pattern"/> and <paramref name="replacement"/> are raw text (the dialect renders
    /// and escapes the literals). Reached only when <see cref="SupportsRegex"/> is <c>true</c>.
    /// <para>
    /// A replacement backreference uses the provider's group syntax (C# <c>$1</c>, SQL <c>\1</c>); it is
    /// not rewritten, so a replacement with a group reference is not portable as written.
    /// </para>
    /// </summary>
    string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        throw new NotSupportedException("Regular-expression replacement is not supported by this SQL dialect.");
    /// <summary>
    /// The provider's formatting surface for the culture-invariant CLR format specifiers of
    /// <c>string.Format</c>/<c>ToString(format)</c>; <c>null</c> means the provider cannot render them.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    IStringFormatFunctions? StringFormats => null;
    /// <summary>
    /// True when the provider can attach a per-expression <c>COLLATE</c> clause
    /// (<see cref="CommonFunctions.collate"/>). The safe default is <c>false</c>; SQL Server,
    /// PostgreSQL, MySQL/MariaDB and SQLite opt in, while ClickHouse has no <c>COLLATE</c>.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    bool SupportsCollation => false;
    /// <summary>
    /// Renders <c>value COLLATE collation</c> over the already-rendered <paramref name="value"/>.
    /// <paramref name="collation"/> is the provider-native collation name, quoted where the provider
    /// requires it (PostgreSQL). Only called when <see cref="SupportsCollation"/> is <c>true</c>.
    /// </summary>
    string MakeCollate(string value, string collation, KeywordCase keywordCase = KeywordCase.Lower) =>
        throw new NotSupportedException("Per-expression collation is not supported by this SQL dialect.");
    /// <summary>
    /// True when the provider can express an ordinal (byte-order/binary) string comparison. The safe
    /// default is <c>false</c>; SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse opt in.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    bool SupportsOrdinalComparison => false;
    /// <summary>
    /// Renders a string operand normalised for ordinal comparison. A provider with a per-expression
    /// binary collation emits <c>value COLLATE &lt;binary&gt;</c>; a provider whose native <c>String</c>
    /// order is already ordinal (ClickHouse) returns the value unchanged. <paramref name="ignoreCase"/>
    /// additionally case-folds the operand. Only called when <see cref="SupportsOrdinalComparison"/> is
    /// <c>true</c>.
    /// </summary>
    string MakeOrdinal(string value, bool ignoreCase) =>
        throw new NotSupportedException("Ordinal string comparison is not supported by this SQL dialect.");
    /// <summary>
    /// True when the provider can make the case-sensitive ordinal overloads of the LIKE-family string
    /// methods (<c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c> with
    /// <see cref="System.StringComparison.Ordinal"/>) behave byte-wise. It defaults to
    /// <see cref="SupportsOrdinalComparison"/>; SQLite overrides it to <c>false</c> because its
    /// <c>LIKE</c> is always case-insensitive for ASCII regardless of the operand's collation, so a
    /// byte-order match cannot be expressed there and the call is rejected instead of being silently
    /// case-insensitive. The <c>OrdinalIgnoreCase</c> overloads are unaffected (they case-fold both
    /// operands).
    /// </summary>
    bool SupportsOrdinalLike => SupportsOrdinalComparison;
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
    string MakeLikeEscape(string escapeChar, KeywordCase keywordCase = KeywordCase.Lower);
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
    /// <summary>
    /// Renders the same difference as <see cref="MakeDateDiff(string, string, string)"/> through a
    /// 64-bit result, so a <c>millisecond</c>/<c>microsecond</c> span does not overflow (the
    /// <c>date_diff_big</c> function). The default delegates to
    /// <see cref="MakeDateDiff(string, string, string)"/>, whose result is already 64-bit on most
    /// providers; SQL Server overrides it with <c>datediff_big</c>.
    /// </summary>
    string MakeDateDiffBig(string field, string start, string end) => MakeDateDiff(field, start, end);
    /// <summary>
    /// Promotes the already-rendered <paramref name="value"/> operand of a sub-day date function so its
    /// time-of-day part is representable. A provider that applies <c>date_add</c> to a <c>date</c>-only
    /// operand can otherwise truncate or reject <c>hour</c>/<c>minute</c>/<c>second</c>/<c>millisecond</c>/
    /// <c>microsecond</c> arithmetic (SQL Server <c>dateadd</c> on a <c>date</c>, ClickHouse <c>add*</c>
    /// on a <c>Date</c>). The default returns <paramref name="value"/> unchanged, so providers with full
    /// timestamps or interval arithmetic are unaffected. <paramref name="field"/> is a validated part name.
    /// </summary>
    string PromoteDateOperand(string field, string value) => value;
    /// <summary>Renders the last day of the month of the already-rendered <paramref name="value"/>.</summary>
    string MakeEndOfMonth(string value);
    /// <summary>
    /// Renders a date built from its already-rendered <paramref name="year"/>/<paramref name="month"/>/
    /// <paramref name="day"/> parts.
    /// </summary>
    string MakeDateFromParts(string year, string month, string day);

    /// <summary>True when the provider accepts <paramref name="field"/> as a <c>date_trunc</c> part.</summary>
    bool SupportsDateTruncField(string field);
    /// <summary>True when the provider accepts <paramref name="field"/> as a <c>date_add</c> part.</summary>
    bool SupportsDateAddField(string field);
    /// <summary>True when the provider accepts <paramref name="field"/> as a <c>date_diff</c> part.</summary>
    bool SupportsDateDiffField(string field);
    /// <summary>Renders the <c>string_agg(value, delimiter)</c> aggregate over the already-rendered arguments.</summary>
    string MakeStringAgg(string value, string delimiter);
    /// <summary>
    /// Renders a filtered <c>string_agg</c> for a dialect whose <see cref="AggregateFilterStyle"/> is
    /// <see cref="AggregateFilterStyle.IfCombinator"/>, composing the already-rendered
    /// <paramref name="predicate"/> into the aggregate. The default throws; a dialect that expresses
    /// the ANSI clause handles the filter through the shared filter helper instead.
    /// </summary>
    string MakeFilteredStringAgg(string value, string delimiter, string predicate) =>
        throw new NotSupportedException("The filtered string_agg is not supported by this SQL dialect.");
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
    /// Renders an ordered-set aggregate as <c>&lt;aggregate&gt; within group (order by &lt;orderBy&gt;)</c>.
    /// <paramref name="aggregate"/> is the already-rendered call (for example <c>percentile_cont(0.5)</c>)
    /// and <paramref name="orderBy"/> the already-rendered key list.
    /// </summary>
    string MakeWithinGroup(string aggregate, string orderBy, KeywordCase keywordCase = KeywordCase.Lower);
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
    /// True when the provider can use a raw SQL fragment as a composable <c>FROM</c> source rendered as a
    /// derived table (<c>(&lt;sql&gt;) AS alias</c>; see <see cref="DataContextExtensions.FromSql"/>). The
    /// safe default is <c>false</c>; a provider that leaves it <c>false</c> rejects such a source with a
    /// clear <see cref="NotSupportedException"/>.
    /// </summary>
    bool SupportsRawSqlSource { get; }
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

    /// <summary>Appends the provider's paging clause (LIMIT/OFFSET, OFFSET/FETCH or TOP) for <paramref name="paging"/> to <paramref name="sqlBuilder"/>.</summary>
    void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>
    /// The provider's renderer for <c>LIMIT [offset, ]n BY expr</c> (ClickHouse). <c>null</c> means the
    /// modifier is unavailable and a command that carries one is rejected when its SQL is built.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    ILimitByRenderer? LimitBy => null;

    /// <summary>
    /// The provider's renderer for the <c>DISTINCT ON</c> modifier; <c>null</c> means the provider cannot
    /// express it. Declared as a default interface method so that existing external implementations keep
    /// compiling.
    /// </summary>
    IDistinctOnRenderer? DistinctOn => null;

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
    string MakeFinal(KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>
    /// Whether the dialect supports the <c>SAMPLE ratio [OFFSET offset]</c> table modifier (ClickHouse).
    /// </summary>
    bool SupportsSample { get; }
    /// <summary>Renders the <c>SAMPLE</c> modifier. Only reached through a dialect that set <see cref="SupportsSample"/>.</summary>
    string MakeSample(double ratio, double offset, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// The provider's renderer for <c>TABLESAMPLE</c>; <c>null</c> means the provider cannot express it.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    ITableSampleMethods? TableSample => null;

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
    string MakeTemporalTable(TemporalClause clause, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// The provider's renderer for the native <c>PIVOT</c>/<c>UNPIVOT</c> pair; <c>null</c> means the
    /// provider cannot express it. Declared as a default interface method so that existing external
    /// implementations keep compiling.
    /// </summary>
    IPivotRenderer? Pivot => null;

    /// <summary>
    /// Whether the dialect supports the <c>PREWHERE</c> clause (ClickHouse). When <c>false</c>, a command
    /// that carries one is rejected when its SQL is built.
    /// </summary>
    bool SupportsPreWhere { get; }

    /// <summary>
    /// The provider's renderer for the <c>ARRAY JOIN</c> clause; <c>null</c> means the provider cannot
    /// express it. Declared as a default interface method so that existing external implementations keep
    /// compiling.
    /// </summary>
    IArrayJoinRenderer? ArrayJoinClause => null;

    /// <summary>
    /// Whether the dialect supports the trailing <c>SETTINGS</c> clause (ClickHouse). When <c>false</c>, a
    /// command that carries settings is rejected when its SQL is built.
    /// </summary>
    bool SupportsSettings { get; }
    /// <summary>Renders the trailing <c>SETTINGS</c> clause. Only reached through a dialect that set <see cref="SupportsSettings"/>.</summary>
    string MakeSettings(IReadOnlyList<KeyValuePair<string, string>> settings, KeywordCase keywordCase = KeywordCase.Lower);
    /// <summary>
    /// Renders a <c>TOP(n)</c>-style limit clause. Returns false when the dialect cannot express the
    /// limit inline and paging must be rendered by <see cref="MakePage"/> instead.
    /// </summary>
    /// <param name="limit">The maximum number of rows to return.</param>
    /// <param name="withTies">Requests the <c>WITH TIES</c> variant where the dialect supports it.</param>
    /// <param name="topStmt">The inline limit fragment, or <c>null</c> when the dialect returns false.</param>
    /// <param name="keywordCase">The letter case in which the emitted SQL keywords are written.</param>
    /// <returns><c>true</c> when <paramref name="topStmt"/> carries the limit; otherwise <c>false</c>.</returns>
    bool MakeTop(int limit, bool withTies, out string? topStmt, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// The provider's renderer for row locking; <c>null</c> means the provider cannot express it.
    /// Declared as a default interface method so that existing external implementations keep compiling.
    /// </summary>
    ILockRenderer? Lock => null;

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

    /// <summary>
    /// Whether the dialect can append <c>RETURNING &lt;columns&gt;</c> to an <c>INSERT</c> so the
    /// statement returns the generated column (PostgreSQL, SQLite 3.35+). Declared as a default
    /// interface method returning <c>false</c> so existing external implementations keep compiling; a
    /// dialect that opts in also overrides <see cref="MakeReturning"/>.
    /// </summary>
    bool SupportsReturning => false;
    /// <summary>
    /// Whether the dialect can place an <c>OUTPUT inserted.&lt;column&gt;</c> clause on an
    /// <c>INSERT</c> so the statement returns the generated column (SQL Server). Declared as a default
    /// interface method returning <c>false</c> so existing external implementations keep compiling; a
    /// dialect that opts in also overrides <see cref="MakeOutput"/>.
    /// </summary>
    bool SupportsOutput => false;
    /// <summary>
    /// Whether the dialect can return the last generated identity through a separate scalar query
    /// (<c>LAST_INSERT_ID()</c>) rather than a clause on the insert (MySQL, MariaDB). Declared as a
    /// default interface method returning <c>false</c> so existing external implementations keep
    /// compiling; a dialect that opts in also overrides <see cref="MakeLastInsertId"/>.
    /// </summary>
    bool SupportsLastInsertId => false;

    /// <summary>
    /// Whether the dialect can return the last generated identity of the current session through a
    /// separate scalar query (<c>SCOPE_IDENTITY()</c>, <c>lastval()</c>, <c>LAST_INSERT_ID()</c>,
    /// <c>last_insert_rowid()</c>) without naming the identity column. Declared as a default interface
    /// method delegating to <see cref="SupportsLastInsertId"/>, so a dialect that already exposes the
    /// scalar identity form needs no extra override; SQL Server and PostgreSQL opt in explicitly.
    /// </summary>
    bool SupportsIdentityFunction => SupportsLastInsertId;

    /// <summary>
    /// Whether the dialect can place a data-modifying statement as the body of a common table
    /// expression (<c>WITH &lt;name&gt; AS (INSERT ... RETURNING ...) ...</c>, PostgreSQL only). Declared as
    /// a default interface method returning <c>false</c> so existing external implementations keep
    /// compiling: every other provider requires a CTE body to be a <c>SELECT</c>.
    /// </summary>
    bool SupportsDataModifyingCtes => false;

    /// <summary>
    /// Whether the dialect can insert a row that writes only column defaults
    /// (<c>INSERT ... DEFAULT VALUES</c>, or <c>INSERT ... () VALUES ()</c> where the dialect overrides
    /// <see cref="MakeDefaultValues"/>). Declared as a default interface method returning <c>false</c> so
    /// existing external implementations keep compiling.
    /// </summary>
    bool SupportsDefaultValues => false;

    /// <summary>
    /// Renders the all-defaults row form that follows the quoted table name: the ANSI
    /// <c> DEFAULT VALUES</c> by default, or the MySQL/MariaDB <c> () VALUES ()</c> where the dialect
    /// overrides it. Only called when <see cref="SupportsDefaultValues"/> is <c>true</c>.
    /// </summary>
    string MakeDefaultValues(KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, " default values");

    /// <summary>
    /// Whether the dialect can write the <c>DEFAULT</c> keyword as a value in the <c>VALUES</c> list
    /// (PostgreSQL, SQL Server, MySQL, MariaDB), selecting the column default per row. Declared as a
    /// default interface method returning <c>false</c> so existing external implementations keep
    /// compiling.
    /// </summary>
    bool SupportsColumnDefault => false;

    /// <summary>
    /// Renders the <c>DEFAULT</c> keyword as a value in the <c>VALUES</c> list. Only called when
    /// <see cref="SupportsColumnDefault"/> is <c>true</c>.
    /// </summary>
    string MakeColumnDefault(KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "default");

    /// <summary>
    /// Renders the <c>RETURNING</c> clause of an <c>INSERT</c> over the already-rendered
    /// <paramref name="columns"/>. Only called when <see cref="SupportsReturning"/> is <c>true</c>.
    /// </summary>
    string MakeReturning(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, " returning ") + string.Join(", ", columns);
    /// <summary>
    /// Renders the <c>OUTPUT</c> clause of an <c>INSERT</c> over the already-rendered
    /// <paramref name="columns"/>. Only called when <see cref="SupportsOutput"/> is <c>true</c>.
    /// </summary>
    string MakeOutput(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, " output ") + string.Join(", ", columns.Select(static c => "inserted." + c));
    /// <summary>
    /// Renders <c>OUTPUT deleted.&lt;column&gt; ...</c> for a <c>DELETE</c> (SQL Server reads the removed
    /// row through <c>deleted</c>). Only called when <see cref="SupportsOutput"/> is <c>true</c>.
    /// </summary>
    string MakeDeletedOutput(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, " output ") + string.Join(", ", columns.Select(static c => "deleted." + c));
    /// <summary>
    /// Renders the scalar query that returns the last generated identity of the current session. Only
    /// called when <see cref="SupportsLastInsertId"/> is <c>true</c>.
    /// </summary>
    string MakeLastInsertId(KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "select last_insert_rowid()");
    /// <summary>
    /// Renders the scalar query that returns the last generated identity of the current session without
    /// naming the identity column. Only called when <see cref="SupportsIdentityFunction"/> is
    /// <c>true</c>; defaults to <see cref="MakeLastInsertId"/>.
    /// </summary>
    string MakeIdentityFunction(KeywordCase keywordCase = KeywordCase.Lower) => MakeLastInsertId(keywordCase);

    /// <summary>
    /// Whether the provider has a native bulk-copy API (PostgreSQL <c>COPY BINARY</c>, SQL Server
    /// <c>SqlBulkCopy</c>, MySQL/MariaDB <c>MySqlBulkCopy</c>, ClickHouse binary insert). Declared as a
    /// default interface method returning <c>false</c> so existing external implementations keep
    /// compiling; a dialect that opts in is paired with a context overriding the native bulk hook.
    /// </summary>
    bool SupportsBulkCopy => false;

    /// <summary>
    /// Whether the provider can execute several statements as one batch (see
    /// <c>BatchExtensions.Batch</c>): each provider that opts in runs the whole set in a single
    /// protocol exchange on the current connection, so the statements share one server session/backend
    /// — the guarantee a session-scoped temporary table needs under a transaction-mode connection
    /// pooler. PostgreSQL, SQL Server and MySQL/MariaDB use the driver's <see cref="System.Data.Common.DbBatch"/>;
    /// SQLite joins the statements with <c>;</c> into one command. Declared as a default interface
    /// method returning <c>false</c> so existing external implementations keep compiling; ClickHouse
    /// (no multi-statement guarantee) leaves it off.
    /// </summary>
    bool SupportsBatch => false;

    /// <summary>
    /// True when a batch must be sent as one <c>;</c>-joined command even though the connection exposes
    /// a <see cref="System.Data.Common.DbBatch"/>. SQL Server is the case: <c>SqlBatch</c> runs every
    /// command in its own scope, so a session-local <c>#temp</c> table created by one command is not
    /// visible to the next, while the statements of one <c>SqlCommand</c> share a batch scope. Only
    /// meaningful when <see cref="SupportsBatch"/> is <c>true</c>. Declared as a default interface
    /// method returning <c>false</c>.
    /// </summary>
    bool BatchUsesJoinedCommand => false;

    /// <summary>
    /// Whether the dialect can skip conflicting rows with its <c>INSERT OR IGNORE</c>/<c>INSERT IGNORE</c>
    /// head (SQLite, MySQL, MariaDB; ClickHouse opts in as a no-op because it has no uniqueness).
    /// Declared as a default interface method returning <c>false</c> so existing external implementations
    /// keep compiling; a dialect that opts in also overrides <see cref="MakeInsertIgnoreInto"/>.
    /// </summary>
    bool SupportsInsertIgnore => false;

    /// <summary>
    /// Renders the <c>INSERT</c> head that skips conflicting rows: <c>insert or ignore into </c> by
    /// default, overridden with <c>insert ignore into </c> by MySQL/MariaDB and with a plain
    /// <c>insert into </c> by ClickHouse (no uniqueness, so every row is written). Only called when
    /// <see cref="SupportsInsertIgnore"/> is <c>true</c>.
    /// </summary>
    string MakeInsertIgnoreInto(KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "insert or ignore into ");

    /// <summary>
    /// Whether the dialect can skip conflicting rows with a trailing <c>ON CONFLICT DO NOTHING</c>
    /// (PostgreSQL, SQLite 3.24+). Declared as a default interface method returning <c>false</c> so
    /// existing external implementations keep compiling; a dialect that opts in also overrides
    /// <see cref="MakeOnConflictDoNothing"/>.
    /// </summary>
    bool SupportsOnConflictDoNothing => false;

    /// <summary>
    /// Renders the trailing <c> ON CONFLICT DO NOTHING</c> that skips conflicting rows when the dialect
    /// has no <c>INSERT OR IGNORE</c> head. Only called when <see cref="SupportsOnConflictDoNothing"/>
    /// is <c>true</c>.
    /// </summary>
    string MakeOnConflictDoNothing(KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, " on conflict do nothing");

    /// <summary>
    /// Renders the <c> OVERRIDING SYSTEM VALUE</c> clause PostgreSQL needs to write an explicit value to
    /// a <c>GENERATED ALWAYS AS IDENTITY</c> column; the empty string when the dialect needs no clause
    /// (SQLite/MySQL/MariaDB accept explicit identity values as-is, SQL Server uses a session toggle).
    /// Only emitted when the insert writes identity columns.
    /// </summary>
    string MakeOverridingSystemValue(KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;

    /// <summary>
    /// Whether writing explicit identity values requires wrapping the insert with a session toggle
    /// (SQL Server <c>SET IDENTITY_INSERT &lt;table&gt; ON/OFF</c>). Declared as a default interface
    /// method returning <c>false</c> so existing external implementations keep compiling; a dialect that
    /// opts in also overrides <see cref="MakeIdentityInsertOn"/> and <see cref="MakeIdentityInsertOff"/>.
    /// </summary>
    bool RequiresIdentityInsertToggle => false;

    /// <summary>
    /// Renders the statement that enables explicit identity values for <paramref name="table"/>. Only
    /// called when <see cref="RequiresIdentityInsertToggle"/> is <c>true</c>.
    /// </summary>
    string MakeIdentityInsertOn(string table, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "set identity_insert ") + table + SqlKeywords.Of(keywordCase, " on");

    /// <summary>
    /// Renders the statement that disables explicit identity values for <paramref name="table"/>. Only
    /// called when <see cref="RequiresIdentityInsertToggle"/> is <c>true</c>.
    /// </summary>
    string MakeIdentityInsertOff(string table, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "set identity_insert ") + table + SqlKeywords.Of(keywordCase, " off");

    /// <summary>
    /// Whether the dialect expresses a key upsert as
    /// <c>INSERT ... ON CONFLICT (&lt;keys&gt;) DO UPDATE SET ...</c> (PostgreSQL, SQLite 3.24+).
    /// Declared as a default interface method returning <c>false</c> so existing external
    /// implementations keep compiling.
    /// </summary>
    bool SupportsOnConflict => false;

    /// <summary>
    /// Renders the conflict target and the start of the update list:
    /// <c> ON CONFLICT (&lt;keys&gt;) DO UPDATE SET </c>. Only called when
    /// <see cref="SupportsOnConflict"/> is <c>true</c>.
    /// </summary>
    string MakeOnConflict(IReadOnlyList<string> keys, KeywordCase keywordCase = KeywordCase.Lower)
        => SqlKeywords.Of(keywordCase, " on conflict (") + string.Join(", ", keys) + SqlKeywords.Of(keywordCase, ") do update set ");

    /// <summary>
    /// Whether the dialect expresses a key upsert as
    /// <c>INSERT ... ON DUPLICATE KEY UPDATE ...</c> (MySQL, MariaDB). Declared as a default interface
    /// method returning <c>false</c> so existing external implementations keep compiling.
    /// </summary>
    bool SupportsOnDuplicateKey => false;

    /// <summary>
    /// Renders the duplicate-key clause start <c> ON DUPLICATE KEY UPDATE </c>. Only called when
    /// <see cref="SupportsOnDuplicateKey"/> is <c>true</c>.
    /// </summary>
    string MakeOnDuplicateKey(KeywordCase keywordCase = KeywordCase.Lower)
        => SqlKeywords.Of(keywordCase, " on duplicate key update ");

    /// <summary>
    /// Renders the right-hand side of an upsert assignment: the incoming (would-be inserted) value of
    /// <paramref name="column"/> as seen by the matched branch. Defaults to the ANSI
    /// <c>excluded.&lt;column&gt;</c> form used by <c>ON CONFLICT</c>; MySQL and MariaDB override it with
    /// <c>VALUES(&lt;column&gt;)</c>. The <c>excluded</c>/<c>values</c> qualifier is an identifier or
    /// function name, so it is not affected by <paramref name="keywordCase"/>.
    /// </summary>
    string MakeUpsertValueReference(string column, KeywordCase keywordCase = KeywordCase.Lower)
        => "excluded." + column;

    /// <summary>
    /// Whether the dialect expresses a key upsert as a full <c>MERGE</c> statement (SQL Server).
    /// Declared as a default interface method returning <c>false</c> so existing external
    /// implementations keep compiling; a dialect that opts in also overrides <see cref="MakeMerge"/>.
    /// </summary>
    bool SupportsMerge => false;

    /// <summary>
    /// Renders a key-upsert <c>MERGE</c> statement over the already-rendered pieces. Only called when
    /// <see cref="SupportsMerge"/> is <c>true</c>.
    /// </summary>
    /// <param name="target">The quoted target table.</param>
    /// <param name="columns">The quoted written columns, in insert order.</param>
    /// <param name="keys">The quoted match-key columns.</param>
    /// <param name="updateColumns">The quoted non-key columns updated on a match.</param>
    /// <param name="valuesRows">The rendered <c>(&lt;values&gt;), (&lt;values&gt;)</c> rows of the derived source.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered <c>MERGE</c> statement, terminated by a semicolon.</returns>
    string MakeMerge(
        string target,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> updateColumns,
        string valuesRows,
        KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render a MERGE upsert.");

    /// <summary>
    /// Whether the dialect renders a general, multi-branch <c>MERGE</c> statement (SQL Server,
    /// PostgreSQL 15+). Declared as a default interface method returning <c>false</c> so existing
    /// external implementations keep compiling.
    /// </summary>
    bool SupportsMergeStatement => false;

    /// <summary>
    /// Whether the dialect accepts a <c>WHEN MATCHED THEN DELETE</c> branch in a general <c>MERGE</c>
    /// (SQL Server, PostgreSQL). Declared as a default interface method returning <c>false</c>.
    /// </summary>
    bool SupportsMergeDelete => false;

    /// <summary>
    /// Whether the dialect accepts a <c>WHEN NOT MATCHED BY SOURCE</c> branch (SQL Server only).
    /// Declared as a default interface method returning <c>false</c>.
    /// </summary>
    bool SupportsMergeBySourceDelete => false;

    /// <summary>
    /// Whether a general <c>MERGE</c> accepts a <c>THEN DO NOTHING</c> branch (PostgreSQL only; SQL
    /// Server has no <c>DO NOTHING</c> action). Declared as a default interface method returning
    /// <c>false</c>.
    /// </summary>
    bool SupportsMergeDoNothing => false;

    /// <summary>
    /// Whether a general <c>MERGE</c> accepts a search condition: an explicit <c>ON &lt;condition&gt;</c>
    /// and/or a <c>WHEN ... AND &lt;condition&gt;</c> branch condition (SQL Server, PostgreSQL).
    /// Declared as a default interface method returning <c>false</c>.
    /// </summary>
    bool SupportsMergeConditionalBranches => false;

    /// <summary>
    /// Whether a general <c>MERGE</c> qualifies its target columns with the target alias in the
    /// <c>UPDATE SET</c> list. Defaults to <c>true</c> (SQL Server); PostgreSQL overrides it to
    /// <c>false</c> because its <c>MERGE</c> forbids qualifying a target column.
    /// </summary>
    bool SupportsMergeTargetQualification => true;

    /// <summary>
    /// Renders the terminator of a general <c>MERGE</c>. Defaults to none; SQL Server requires a
    /// terminating semicolon.
    /// </summary>
    string MakeMergeStatementTerminator(KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;

    /// <summary>
    /// Renders the <c>RETURNING</c> clause of a general <c>MERGE</c>. Defaults to the ANSI form; PostgreSQL
    /// overrides it to qualify the target columns, whose names would otherwise be ambiguous with the source.
    /// </summary>
    string MakeMergeReturning(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower)
        => MakeReturning(columns, keywordCase);

    /// <summary>
    /// Whether the dialect can execute a <c>DELETE</c> and report the affected-row count. Declared as a
    /// default interface method returning <c>true</c>; the native statement is rendered by
    /// <see cref="MakeDeleteHead"/> (ClickHouse renders its <c>ALTER TABLE ... DELETE</c> mutation).
    /// </summary>
    bool SupportsDelete => true;

    /// <summary>
    /// Whether the dialect has a native <c>TRUNCATE TABLE</c> statement. Declared as a default interface
    /// method returning <c>false</c>; SQLite has no <c>TRUNCATE</c> (use <c>DELETE</c>), so it keeps the
    /// default.
    /// </summary>
    bool SupportsTruncate => false;

    /// <summary>
    /// Renders the native full-table truncation over the already-resolved, quoted target table. Only
    /// called when <see cref="SupportsTruncate"/> is <c>true</c>.
    /// </summary>
    /// <param name="table">The quoted target table.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered <c>TRUNCATE TABLE</c> statement.</returns>
    string MakeTruncate(string table, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "truncate table ") + table;

    /// <summary>
    /// Renders the head of a <c>DELETE</c> statement up to (but not including) its filter, over the
    /// already-resolved, quoted target table. Defaults to the ANSI <c>DELETE FROM &lt;table&gt;</c>;
    /// ClickHouse overrides it with the <c>ALTER TABLE &lt;table&gt; DELETE</c> mutation head.
    /// </summary>
    /// <param name="table">The quoted target table.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered delete head.</returns>
    string MakeDeleteHead(string table, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "delete from ") + table;

    /// <summary>
    /// Whether the dialect requires a <c>WHERE</c> clause on every delete (ClickHouse's
    /// <c>ALTER TABLE ... DELETE</c> does). A delete without a predicate then renders a trivially true
    /// filter (<c>WHERE 1</c>). Defaults to <c>false</c>.
    /// </summary>
    bool DeleteRequiresWhere => false;

    /// <summary>
    /// Renders a dialect suffix appended after the delete's filter (ClickHouse's
    /// <c>SETTINGS mutations_sync = 1</c>), or <see langword="null"/> when there is none. Defaults to
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The suffix, or <see langword="null"/>.</returns>
    string? MakeDeleteSuffix(KeywordCase keywordCase = KeywordCase.Lower) => null;

    /// <summary>
    /// Whether the dialect can delete rows of one table based on a join (a native multi-table
    /// <c>DELETE</c>). Declared as a default interface method returning <c>false</c>; PostgreSQL, SQL
    /// Server, MySQL and MariaDB override it. SQLite has no join-<c>DELETE</c>, and ClickHouse's
    /// <c>ALTER TABLE ... DELETE</c> mutation cannot reference other tables, so both keep the default.
    /// </summary>
    bool SupportsDeleteJoin => false;

    /// <summary>
    /// Renders a multi-table <c>DELETE</c>. Only called when <see cref="SupportsDeleteJoin"/> is
    /// <c>true</c>. The renderer supplies both spellings so the dialect can pick its native form: the
    /// alias style (<c>DELETE &lt;alias&gt; FROM &lt;fromAndJoins&gt;</c>, SQL Server/MySQL/MariaDB) and
    /// the <c>USING</c> style (<c>DELETE FROM ... USING ...</c>, PostgreSQL), where the join conditions
    /// are folded into the <c>WHERE</c> so they can reference the target alias.
    /// </summary>
    /// <param name="target">The target table with literal identifiers (already quoted), without an alias.</param>
    /// <param name="targetAlias">The alias assigned to the target table, already escaped for the dialect.</param>
    /// <param name="fromAndJoins">The alias-style source: <c>&lt;target&gt; AS a JOIN &lt;b&gt; AS b ON ...</c>.</param>
    /// <param name="usingSources">The <c>USING</c>-style source list: <c>&lt;b&gt; AS b[, &lt;c&gt; AS c ...]</c>.</param>
    /// <param name="joinConditions">The rendered join <c>ON</c> conditions, combined with <c>and</c>, without a leading <c>WHERE</c>.</param>
    /// <param name="whereSql">The rendered user <c>WHERE</c> condition, or <see langword="null"/> when there is none.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered multi-table <c>DELETE</c>.</returns>
    /// <exception cref="NotSupportedException">The dialect has no native multi-table <c>DELETE</c>.</exception>
    string MakeDeleteJoin(
        string target,
        string targetAlias,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render a multi-table DELETE.");

    /// <summary>
    /// Whether <see cref="MakeDeleteJoin"/> must be given the <c>USING</c> spelling (PostgreSQL) rather
    /// than the alias spelling. Defaults to <c>false</c>.
    /// </summary>
    bool DeleteJoinRequiresUsing => false;

    /// <summary>
    /// Whether the dialect can execute an <c>UPDATE</c> and report the affected-row count. Declared as a
    /// default interface method returning <c>true</c>; ClickHouse keeps it <c>true</c> but renders its
    /// <c>ALTER TABLE ... UPDATE</c> mutation, which waits for the mutation
    /// (<c>SETTINGS mutations_sync = 1</c>) yet reports no affected-row count. The native head is rendered
    /// by <see cref="MakeUpdateHead"/>.
    /// </summary>
    bool SupportsUpdate => true;

    /// <summary>
    /// Renders the head of an <c>UPDATE</c> statement up to (but not including) its <c>SET</c> list, over
    /// the already-resolved, quoted target table. Defaults to the ANSI <c>UPDATE &lt;table&gt; SET </c>;
    /// ClickHouse overrides it with <c>ALTER TABLE &lt;table&gt; UPDATE </c>. Only reached through a
    /// dialect that kept <see cref="SupportsUpdate"/> <c>true</c>.
    /// </summary>
    /// <param name="table">The quoted target table.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered update head.</returns>
    string MakeUpdateHead(string table, KeywordCase keywordCase = KeywordCase.Lower) => SqlKeywords.Of(keywordCase, "update ") + table + SqlKeywords.Of(keywordCase, " set ");

    /// <summary>
    /// Whether the dialect requires a <c>WHERE</c> clause on every update (ClickHouse's
    /// <c>ALTER TABLE ... UPDATE</c> does). An update without a predicate then renders a trivially true
    /// filter (<c>WHERE 1</c>). Defaults to <c>false</c>.
    /// </summary>
    bool UpdateRequiresWhere => false;

    /// <summary>
    /// Renders a dialect suffix appended after the update's filter (ClickHouse's
    /// <c>SETTINGS mutations_sync = 1</c>), or <see langword="null"/> when there is none. Defaults to
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The suffix, or <see langword="null"/>.</returns>
    string? MakeUpdateSuffix(KeywordCase keywordCase = KeywordCase.Lower) => null;

    /// <summary>
    /// Whether the dialect can update rows of one table based on a join (a native multi-table
    /// <c>UPDATE</c>). Declared as a default interface method returning <c>false</c>; PostgreSQL, SQLite,
    /// SQL Server, MySQL and MariaDB override it. ClickHouse's <c>ALTER TABLE ... UPDATE</c> mutation
    /// cannot reference other tables, so it keeps the default.
    /// </summary>
    bool SupportsUpdateJoin => false;

    /// <summary>
    /// Whether <see cref="MakeUpdateJoin"/> must be given the <c>FROM</c> spelling (PostgreSQL and
    /// SQLite): the target stays out of the source list, the joined tables are listed separately and the
    /// join conditions are folded into the <c>WHERE</c> so they can reference the target alias. When
    /// <c>false</c>, the alias spelling is used (SQL Server <c>UPDATE &lt;alias&gt; ... FROM</c>,
    /// MySQL/MariaDB <c>UPDATE ... JOIN ... SET</c>). Defaults to <c>false</c>.
    /// </summary>
    bool UpdateJoinRequiresFrom => false;

    /// <summary>
    /// Renders a multi-table <c>UPDATE</c>. Only called when <see cref="SupportsUpdateJoin"/> is
    /// <c>true</c>. The renderer supplies both spellings so the dialect can pick its native form: the
    /// alias style (<c>&lt;target&gt; AS a JOIN &lt;b&gt; AS b ON ...</c>, SQL Server/MySQL/MariaDB) and
    /// the <c>FROM</c> style (<c>&lt;b&gt; AS b, ...</c> plus join conditions, PostgreSQL/SQLite). The
    /// <c>SET</c> list is rendered once and references the target through its alias.
    /// </summary>
    /// <param name="target">The target table with literal identifiers (already quoted), without an alias.</param>
    /// <param name="targetAlias">The alias assigned to the target table, already escaped for the dialect.</param>
    /// <param name="assignments">The rendered <c>&lt;column&gt; = &lt;value&gt;</c> list, without the <c>SET</c> keyword.</param>
    /// <param name="fromAndJoins">The alias-style source: <c>&lt;target&gt; AS a JOIN &lt;b&gt; AS b ON ...</c>.</param>
    /// <param name="usingSources">The <c>FROM</c>-style source list: <c>&lt;b&gt; AS b[, &lt;c&gt; AS c ...]</c>.</param>
    /// <param name="joinConditions">The rendered join <c>ON</c> conditions, combined with <c>and</c>, without a leading <c>WHERE</c>.</param>
    /// <param name="whereSql">The rendered user <c>WHERE</c> condition, or <see langword="null"/> when there is none.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered multi-table <c>UPDATE</c>.</returns>
    /// <exception cref="NotSupportedException">The dialect has no native multi-table <c>UPDATE</c>.</exception>
    string MakeUpdateJoin(
        string target,
        string targetAlias,
        string assignments,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render a multi-table UPDATE.");

    /// <summary>
    /// Whether the dialect can materialise a query into a persistent table. Declared as a default
    /// interface method returning <c>false</c> so existing external implementations keep compiling;
    /// PostgreSQL, SQLite, MySQL, MariaDB, ClickHouse and SQL Server opt in. SQL Server uses the
    /// <c>SELECT ... INTO</c> form (see <see cref="CreateTableAsSelectUsesSelectInto"/>), the others
    /// <c>CREATE TABLE ... AS SELECT</c>.
    /// </summary>
    bool SupportsCreateTableAsSelect => false;

    /// <summary>
    /// Whether the dialect can materialise a query into a <em>temporary</em> table (<c>ToTempTable</c>).
    /// Declared as a default interface method returning <see cref="SupportsCreateTableAsSelect"/>;
    /// PostgreSQL, SQLite, MySQL and MariaDB keep it, while SQL Server (a temporary table is
    /// <c>ToTable("#name")</c>) and ClickHouse (a temporary table accepts no <c>AS SELECT</c>) opt out.
    /// </summary>
    bool SupportsTemporaryCreateTableAsSelect => SupportsCreateTableAsSelect;

    /// <summary>
    /// Whether the dialect accepts <c>IF NOT EXISTS</c> on the materialisation. Declared as a default
    /// interface method returning <c>false</c>; PostgreSQL, SQLite, MySQL, MariaDB and ClickHouse opt in,
    /// while SQL Server's <c>SELECT ... INTO</c> cannot express it, so the option is rejected there.
    /// </summary>
    bool SupportsCreateTableAsSelectIfNotExists => false;

    /// <summary>
    /// Whether the dialect renders the materialisation as <c>SELECT ... INTO</c> (SQL Server) rather than
    /// <c>CREATE TABLE ... AS SELECT</c>. Declared as a default interface method returning <c>false</c>.
    /// </summary>
    bool CreateTableAsSelectUsesSelectInto => false;

    /// <summary>
    /// Whether the dialect accepts a column list on <c>CREATE TABLE ... AS SELECT</c> (PostgreSQL,
    /// MySQL, MariaDB). SQLite derives every column from the query and accepts no list; SQL Server takes
    /// the names from the select list; ClickHouse needs <c>name type</c> pairs. All three keep the
    /// default. Declared as a default interface method returning <c>false</c>.
    /// </summary>
    bool SupportsCreateTableAsSelectColumnList => false;

    /// <summary>
    /// Whether the dialect accepts <c>ON COMMIT { PRESERVE ROWS | DELETE ROWS | DROP }</c> on a
    /// temporary table (PostgreSQL only). Declared as a default interface method returning <c>false</c>.
    /// </summary>
    bool SupportsCreateTableAsSelectOnCommit => false;

    /// <summary>
    /// Whether the dialect accepts <c>WITH [NO] DATA</c> after the query of a
    /// <c>CREATE TABLE ... AS SELECT</c> (PostgreSQL only). Declared as a default interface method
    /// returning <c>false</c>.
    /// </summary>
    bool SupportsCreateTableAsSelectWithNoData => false;

    /// <summary>
    /// Renders the clause that introduces the target of a <c>SELECT ... INTO</c> materialisation (for
    /// example <c> into [target]</c>), which the statement builder inserts into the select list. Only
    /// called through a dialect that set <see cref="CreateTableAsSelectUsesSelectInto"/>; the default
    /// throws.
    /// </summary>
    /// <param name="clause">The resolved clause options.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered <c>INTO</c> clause.</returns>
    string MakeCreateTableAsSelectInto(CreateTableAsClause clause, KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render a SELECT ... INTO materialisation.");

    /// <summary>
    /// Renders a <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c> statement over the already-resolved,
    /// optionally quoted <paramref name="clause"/> and the already-rendered <paramref name="selectSql"/>
    /// body. Only called through a dialect that set <see cref="SupportsCreateTableAsSelect"/>; the
    /// optional parts (column list, <c>ON COMMIT</c>, <c>WITH NO DATA</c>) are gated by their own flags
    /// before this is reached.
    /// </summary>
    /// <param name="clause">The resolved clause options.</param>
    /// <param name="selectSql">The rendered body query.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered statement.</returns>
    string MakeCreateTableAsSelect(CreateTableAsClause clause, string selectSql, KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render CREATE TABLE ... AS SELECT.");
}

/// <summary>Which side of a string <see cref="string.Trim()"/> removes whitespace from.</summary>
public enum StringTrimKind
{
    /// <summary>Trim whitespace from both ends.</summary>
    Both,
    /// <summary>Trim leading whitespace only.</summary>
    Start,
    /// <summary>Trim trailing whitespace only.</summary>
    End
}
