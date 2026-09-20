using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Static entry point for SQL constructs that are written directly inside query expressions.
/// </summary>
/// <remarks>
/// Renamed from <c>NORM</c> (all-caps abbreviation). The nested function surfaces were renamed too:
/// <c>NORM_SQL</c> → <see cref="CommonFunctions"/>, <c>PG</c>/<c>MS</c>/<c>CLK</c> →
/// <see cref="PostgresFunctions"/>/<see cref="SqlServerFunctions"/>/<see cref="ClickHouseFunctions"/>.
/// Method names intentionally mirror SQL tokens (<c>count_big</c>, <c>@in</c>, ...), which the .NET
/// guidelines would not; see <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-10.
/// </remarks>
public static partial class SqlFunctions
{
    /// <summary>Cross-provider SQL function surface.</summary>
    public static CommonFunctions Sql => default!;

    /// <summary>
    /// Surface of the PostgreSQL-only functions (arrays, native JSON, the extended scalar library,
    /// PG-only aggregates and the <c>generate_series</c>/<c>unnest</c> table functions). Every
    /// member requires <see cref="ISqlDialect.SupportsArrays"/> or the matching capability flag;
    /// other providers reject it with a clear message.
    /// </summary>
    public static PostgresFunctions Postgres => default!;

    /// <summary>
    /// Surface of the text-JSON functions (SQL Server and MySQL/MariaDB), the SQL Server-only
    /// <c>choose</c> conditional function (the portable <c>iif</c> is on <see cref="CommonFunctions"/>)
    /// and the SQL Server <c>string_split</c>/<c>openjson</c> table functions. Every member requires
    /// <see cref="ISqlDialect.SupportsTextJson"/>, <see cref="ISqlDialect.SupportsChoose"/> or
    /// <see cref="ISqlDialect.SupportsTableFunction"/>; other providers reject it with a clear message.
    /// </summary>
    public static SqlServerFunctions SqlServer => default!;

    /// <summary>
    /// Surface of the ClickHouse-only functions (the <c>argMin</c>/<c>argMax</c>, <c>uniq*</c>,
    /// parameterised <c>quantile*</c>/<c>median</c> and the <c>anyLast</c> aggregate, the
    /// string-JSON <c>JSONExtract*</c>/<c>visitParamExtract*</c> family plus the JSONPath
    /// <c>json_value</c>/<c>json_query</c>/<c>json_exists</c> scalars, the dictionary functions, the
    /// <c>-If</c> combinator, the distributed <c>global_in</c> predicate and the <c>numbers</c>/<c>numbers_mt</c>
    /// and <c>zeros</c>/<c>zeros_mt</c> table functions, as well as the array functions over array
    /// columns (<c>arrayJoin</c>, <c>length</c>, <c>has</c>, <c>indexOf</c>, <c>hasAny</c>/<c>hasAll</c>,
    /// <c>arrayStringConcat</c>, <c>splitByChar</c>, <c>arraySort</c>, <c>arrayReverse</c>,
    /// <c>arrayDistinct</c>). Every member is
    /// gated by a capability flag
    /// (<see cref="ISqlDialect.SupportsArgMinMax"/>, <see cref="ISqlDialect.SupportsIfAggregates"/>, …);
    /// other providers reject it with a clear message.
    /// </summary>
    public static ClickHouseFunctions ClickHouse => default!;

    public static T Parameter<T>(int idx) => default!;

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.generate_series(long, long)"/>: a single column
    /// named <c>generate_series</c> holding the generated number.
    /// </summary>
    public interface IGenerateSeriesRow
    {
        [Column("generate_series")]
        long Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PostgresFunctions.unnest{T}(T[])"/>: a single column named
    /// <c>unnest</c> holding the array element.
    /// </summary>
    public interface IUnnestRow<T>
    {
        [Column("unnest")]
        T Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.string_split(string?, string?)"/>: a single column
    /// named <c>value</c> holding one fragment (SQL Server <c>string_split</c>).
    /// </summary>
    public interface IStringSplitRow
    {
        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="SqlServerFunctions.openjson(string?)"/>: the <c>key</c>/<c>value</c>/<c>type</c>
    /// columns of SQL Server's <c>openjson</c> (the default schema, i.e. object properties or array elements).
    /// </summary>
    public interface IOpenJsonRow
    {
        // KEY is a reserved word in T-SQL, so the physical name is quoted here. The openjson helper
        // is gated to SQL Server (see ISqlDialect.SupportsTableFunction), so the quoting is safe.
        [Column("[key]")]
        string? Key { get; set; }
        [Column("value")]
        string? Value { get; set; }
        [Column("type")]
        int Type { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="ClickHouseFunctions.numbers(long)"/> and
    /// <see cref="ClickHouseFunctions.numbers_mt(long)"/>: a single column named <c>number</c> holding the
    /// generated value.
    /// </summary>
    public interface INumbersRow
    {
        [Column("number")]
        long Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="ClickHouseFunctions.zeros(long)"/> and
    /// <see cref="ClickHouseFunctions.zeros_mt(long)"/>: a single column named <c>zero</c> holding the
    /// byte <c>0</c> (ClickHouse <c>UInt8</c>).
    /// </summary>
    public interface IZerosRow
    {
        [Column("zero")]
        byte Value { get; set; }
    }

}

/// <summary>
/// Surface of SQL functions and predicates that can be used inside query expressions. The members
    /// are only ever evaluated by the expression translator, never at runtime.
    /// </summary>
    /// <remarks>
    /// Renamed from <c>NORM_SQL</c>. The <see cref="System.Reflection.MethodInfo"/> fields and
    /// <c>SQLExpression</c> are implementation details and are now <c>internal</c>.
    /// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-10.
    /// </remarks>
    public class CommonFunctions
        // Common (cross-provider) surface.
    {
        [Browsable(false)]
        internal static readonly MethodInfo ExistsMI = typeof(CommonFunctions).GetMethod(nameof(exists), BindingFlags.Public | BindingFlags.Instance)!;

        [Browsable(false)]
        internal static readonly MethodInfo MinMI = AggregateMethod(nameof(min));

        [Browsable(false)]
        internal static readonly MethodInfo MaxMI = AggregateMethod(nameof(max));

        [Browsable(false)]
        internal static readonly MethodInfo AvgMI = AggregateMethod(nameof(avg));

        [Browsable(false)]
        internal static readonly MethodInfo SumMI = AggregateMethod(nameof(sum));

        [Browsable(false)]
        internal static readonly MethodInfo StdevMI = AggregateMethod(nameof(stdev));

        [Browsable(false)]
        internal static readonly MethodInfo StdevpMI = AggregateMethod(nameof(stdevp));

        [Browsable(false)]
        internal static readonly MethodInfo VarMI = AggregateMethod(nameof(var));

        [Browsable(false)]
        internal static readonly MethodInfo VarpMI = AggregateMethod(nameof(varp));

        [Browsable(false)]
        internal static readonly ConstantExpression SQLExpression = Expression.Constant(new CommonFunctions());

        /// <summary>
        /// Resolves the single-value overload of an aggregate method. Several aggregates
        /// (<c>min</c>, <c>max</c>, <c>avg</c>, <c>sum</c>) also have an <c>(value, filter)</c>
        /// overload, so a name-only <c>GetMethod</c> lookup is ambiguous; binding by parameter count
        /// selects the plain aggregate.
        /// </summary>
        private static MethodInfo AggregateMethod(string name) =>
            typeof(CommonFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == name && m.GetParameters().Length == 1);

        public bool exists(QueryCommand cmd) => default!;

        public bool like(string? column, string? pattern) => default!;

        public bool like(string? column, string? pattern, string? escapeChar) => default!;

        /// <summary>
        /// <c>contains(column, search)</c>: true when the full-text-indexed <paramref name="column"/>
        /// contains <paramref name="search"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsFullText"/>).
        /// </summary>
        public bool contains<T>(T? column, string? search) => default!;

        /// <summary>
        /// <c>freetext(column, search)</c>: true when the full-text-indexed <paramref name="column"/>
        /// matches the meaning (not just the words) of <paramref name="search"/>. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsFullText"/>).
        /// </summary>
        public bool freetext<T>(T? column, string? search) => default!;

        /// <summary>
        /// The name of the current user (<c>current_user</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsSessionInfoFunctions"/> and
        /// <see cref="ISqlDialect.SupportsSessionInfoFunction(string)"/>).
        /// </summary>
        public string? current_user() => default!;

        /// <summary>
        /// The name of the session user (<c>session_user</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsSessionInfoFunctions"/> and
        /// <see cref="ISqlDialect.SupportsSessionInfoFunction(string)"/>).
        /// </summary>
        public string? session_user() => default!;

        /// <summary>
        /// The current schema (<c>current_schema</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsSessionInfoFunctions"/> and
        /// <see cref="ISqlDialect.SupportsSessionInfoFunction(string)"/>).
        /// </summary>
        public string? current_schema() => default!;

        /// <summary>
        /// The current database name (<c>current_database()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsSessionInfoFunctions"/> and
        /// <see cref="ISqlDialect.SupportsSessionInfoFunction(string)"/>).
        /// </summary>
        public string? current_database() => default!;

        /// <summary>
        /// The provider version string (<c>version()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsSessionInfoFunctions"/> and
        /// <see cref="ISqlDialect.SupportsSessionInfoFunction(string)"/>).
        /// </summary>
        public string? version() => default!;

        /// <summary>
        /// Generates a random UUID v4 (<c>gen_random_uuid()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsUuidGenerators"/> and
        /// <see cref="ISqlDialect.SupportsUuidGenerator(string)"/>): PostgreSQL 13+, SQL Server
        /// (<c>newid()</c>), MariaDB (<c>uuid_v4()</c>) and ClickHouse (<c>generateUUIDv4()</c>).
        /// </summary>
        public Guid? gen_random_uuid() => default!;

        /// <summary>
        /// Generates a UUID v7 (<c>uuidv7()</c>). Requires a provider that supports it
        /// (see <see cref="ISqlDialect.SupportsUuidGenerators"/> and
        /// <see cref="ISqlDialect.SupportsUuidGenerator(string)"/>): PostgreSQL 18+, MariaDB 11.7+
        /// (<c>uuid_v7()</c>) and ClickHouse (<c>generateUUIDv7()</c>).
        /// </summary>
        public Guid? uuidv7() => default!;

        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;

        public bool @in<T>(T column, IEnumerable<T> values) => default!;

        public bool @in<T>(T column, params T[] values) => default!;

        public T any<T>(QueryCommand<T> cmd) => default!;

        public T all<T>(QueryCommand<T> cmd) => default!;

        /// <summary><c>nullif(value, other)</c>: null when the two arguments are equal. ANSI and portable.</summary>
        public T? nullif<T>(T? value, T? other) => default!;

        /// <summary>
        /// <c>iif(condition, whenTrue, whenFalse)</c>: the conditional expression, rendered as <c>iif</c>
        /// (SQL Server, SQLite), <c>if</c> (MySQL/MariaDB, ClickHouse) or <c>case when ... then ... else ... end</c>
        /// (PostgreSQL). Requires a provider that supports it (see <see cref="ISqlDialect.SupportsIif"/>).
        /// </summary>
        public TResult? iif<TResult>(bool condition, TResult? whenTrue, TResult? whenFalse) => default!;

        /// <summary>
        /// <c>greatest(...)</c>: the largest of the arguments. NULL handling is provider-specific:
        /// PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments, while MySQL/MariaDB
        /// and SQLite return NULL when any argument is NULL.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsGreatestLeast"/>).
        /// </summary>
        public T? greatest<T>(params T?[] values) => default!;

        /// <summary>
        /// <c>least(...)</c>: the smallest of the arguments. NULL handling is provider-specific:
        /// PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments, while MySQL/MariaDB
        /// and SQLite return NULL when any argument is NULL.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsGreatestLeast"/>).
        /// </summary>
        public T? least<T>(params T?[] values) => default!;

        /// <summary>
        /// <c>date_trunc(field, value)</c>: truncates a timestamp to <paramref name="field"/>
        /// (for example <c>"month"</c>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsDateTrunc"/>).
        /// </summary>
        public DateTime? date_trunc(string field, DateTime? value) => default!;

        /// <summary>
        /// <c>dateadd(field, amount, value)</c>: adds <paramref name="amount"/> units of
        /// <paramref name="field"/> (for example <c>"day"</c>) to a date/time. The field must be a
        /// constant string. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? date_add(string field, int amount, DateTime? value) => default!;

        /// <summary>
        /// <c>datediff(field, start, end)</c>: the number of <paramref name="field"/> boundaries
        /// between two timestamps (SQL Server semantics; a provider without a native <c>datediff</c>
        /// may count whole units instead, which can differ). The field must be a constant string and
        /// cannot be <c>decade</c>/<c>century</c>/<c>millennium</c>, which T-SQL <c>datediff</c> has no
        /// part for. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public int? date_diff(string field, DateTime? start, DateTime? end) => default!;

        /// <summary>
        /// <c>eomonth(value)</c>: the last day of the month of <paramref name="value"/>. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? end_of_month(DateTime? value) => default!;

        /// <summary>
        /// <c>datefromparts(year, month, day)</c>: builds a date from its numeric parts. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? date_from_parts(int year, int month, int day) => default!;

        /// <summary>
        /// <c>extract(part, value)</c>: the integer <paramref name="part"/> of a date/time. Supports
        /// <c>year</c>, <c>quarter</c>, <c>month</c>, <c>week</c> (ISO 8601), <c>day</c>, <c>doy</c>,
        /// <c>dow</c> (0=Sunday..6=Saturday), <c>isodow</c> (1=Monday..7=Sunday), <c>hour</c>,
        /// <c>minute</c> and <c>second</c>. The part must be a constant string. Use
        /// <see cref="date_part"/> for <c>epoch</c>. Requires a provider that supports the part (see
        /// <see cref="ISqlDialect.SupportsDatePart(string)"/>).
        /// </summary>
        public int? extract(string part, DateTime? value) => default!;

        /// <summary>
        /// <c>date_part(part, value)</c>: the numeric <paramref name="part"/> of a date/time.
        /// Currently supports <c>epoch</c> (the number of seconds since 1970-01-01 00:00:00, including
        /// any fraction). The part must be a constant string. Use <see cref="extract"/> for the
        /// integer parts. Requires a provider that supports the part (see
        /// <see cref="ISqlDialect.SupportsDatePart(string)"/>).
        /// </summary>
        /// <remarks>
        /// This is not PostgreSQL's general <c>date_part</c> synonym: only the numeric-valued part
        /// <c>epoch</c> is provided here, while the integer parts (including PostgreSQL's
        /// <c>date_part('year', x)</c>) go through <see cref="extract"/>.
        /// </remarks>
        public double? date_part(string part, DateTime? value) => default!;

        /// <summary>
        /// <c>string_agg(value, delimiter)</c>: concatenates the values of a group. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsStringArrayAggregates"/>).
        /// </summary>
        public string? string_agg<T>(T? value, string delimiter) => default!;

        /// <summary>Filtered <c>string_agg(value, delimiter) filter (where ...)</c>.</summary>
        public string? string_agg<T>(T? value, string delimiter, Expression<Func<bool>> filter) => default!;

        public int count(params object?[] properties) => default!;

        public long count_big(params object?[] properties) => default!;

        public int count_distinct(params object?[] properties) => default!;

        public long count_big_distinct(params object?[] properties) => default!;

        public T? min<T>(T? property) => default!;

        public T? max<T>(T? property) => default!;

        public T? avg<T>(T? property) => default!;

        public T? avg_distinct<T>(T? property) => default!;

        public T? sum<T>(T? property) => default!;

        public T? sum_distinct<T>(T? property) => default!;

        public T? stdev<T>(T? property) => default!;

        public T? stdev_distinct<T>(T? property) => default!;

        public T? stdevp<T>(T? property) => default!;

        public T? stdevp_distinct<T>(T? property) => default!;

        public T? var<T>(T? property) => default!;

        public T? var_distinct<T>(T? property) => default!;

        public T? varp<T>(T? property) => default!;

        public T? varp_distinct<T>(T? property) => default!;

        /// <summary>
        /// <c>any_value(property)</c>: an arbitrary value from the group. Distinct from the
        /// <see cref="any{T}(QueryCommand{T})"/> subquery quantifier, hence the name. Rendered as
        /// <c>ANY_VALUE(x)</c> by MySQL and as <c>any(x)</c> by ClickHouse; MariaDB has no
        /// <c>ANY_VALUE</c> yet, so it is gated off. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsAnyValueAggregate"/>).
        /// </summary>
        public T? any_agg<T>(T? property) => default!;

        /// <summary>
        /// Filtered <c>count(*) filter (where ...)</c>. Requires a provider that supports the FILTER
        /// clause (see <see cref="ISqlDialect.SupportsFilter"/>).
        /// </summary>
        public int count(Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered 64-bit <c>count(*) filter (where ...)</c>.</summary>
        public long count_big(Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>min(property) filter (where ...)</c>.</summary>
        public T? min<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>max(property) filter (where ...)</c>.</summary>
        public T? max<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>avg(property) filter (where ...)</c>.</summary>
        public T? avg<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary>Filtered <c>sum(property) filter (where ...)</c>.</summary>
        public T? sum<T>(T? property, Expression<Func<bool>> filter) => default!;

        /// <summary><c>corr(Y, X)</c>: the correlation coefficient of a set of (Y, X) pairs.</summary>
        public double? corr<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>covar_pop(Y, X)</c>: the population covariance.</summary>
        public double? covar_pop<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>covar_samp(Y, X)</c>: the sample covariance.</summary>
        public double? covar_samp<TY, TX>(TY? y, TX? x) => default!;

        /// <summary>Order key of a window specification rendered as <c>asc</c>.</summary>
        public WindowOrder asc(Expression<Func<object?>> expression) => default!;

        /// <summary>Order key of a window specification rendered as <c>desc</c>.</summary>
        public WindowOrder desc(Expression<Func<object?>> expression) => default!;

        /// <summary>Sequential row number within the window partition.</summary>
        public WindowFunction<int> row_number() => default!;

        /// <summary>Rank with gaps within the window partition.</summary>
        public WindowFunction<int> rank() => default!;

        /// <summary>Rank without gaps within the window partition.</summary>
        public WindowFunction<int> dense_rank() => default!;

        /// <summary>Splits the partition into <paramref name="buckets"/> roughly equal groups.</summary>
        public WindowFunction<int> ntile(int buckets) => default!;

        /// <summary>Value of <paramref name="property"/> from the previous row (offset 1).</summary>
        public WindowFunction<T?> lag<T>(T? property) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows back.</summary>
        public WindowFunction<T?> lag<T>(T? property, int offset) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows back, or <paramref name="defaultValue"/>.</summary>
        public WindowFunction<T?> lag<T>(T? property, int offset, T? defaultValue) => default!;

        /// <summary>Value of <paramref name="property"/> from the next row (offset 1).</summary>
        public WindowFunction<T?> lead<T>(T? property) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows ahead.</summary>
        public WindowFunction<T?> lead<T>(T? property, int offset) => default!;

        /// <summary>Value of <paramref name="property"/> from the row <paramref name="offset"/> rows ahead, or <paramref name="defaultValue"/>.</summary>
        public WindowFunction<T?> lead<T>(T? property, int offset, T? defaultValue) => default!;

        /// <summary>First value of <paramref name="property"/> in the window frame.</summary>
        public WindowFunction<T?> first_value<T>(T? property) => default!;

        /// <summary>Last value of <paramref name="property"/> in the window frame.</summary>
        public WindowFunction<T?> last_value<T>(T? property) => default!;

        /// <summary>
        /// <c>nth_value(property, n)</c>: the value of <paramref name="property"/> at the 1-based
        /// <paramref name="n"/>-th row of the window frame. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsNthValue"/>); SQL Server has no <c>NTH_VALUE</c>.
        /// </summary>
        public WindowFunction<T?> nth_value<T>(T? property, int n) => default!;

        /// <summary>Relative rank of the current row: <c>(rank - 1) / (rows - 1)</c>, in <c>[0, 1]</c>.</summary>
        public WindowFunction<double> percent_rank() => default!;

        /// <summary>Cumulative distribution of the current row within the partition, in <c>(0, 1]</c>.</summary>
        public WindowFunction<double> cume_dist() => default!;

        /// <summary>
        /// Windowed <c>percentile_cont(fraction)</c>: the interpolated value at
        /// <paramref name="fraction"/> in the window. Rendered as
        /// <c>percentile_cont(fraction) within group (order by value) over (...)</c>; requires a provider
        /// that supports the window percentile form (see
        /// <see cref="ISqlDialect.SupportsPercentileWindow"/>; SQL Server and MariaDB). PostgreSQL
        /// expresses percentiles as an ordered-set aggregate instead
        /// (<see cref="PostgresFunctions.percentile_cont"/>), which is not a window function.
        /// </summary>
        public WindowFunction<T?> percentile_cont<T>(double fraction, T? value) => default!;

        /// <summary>Windowed <c>percentile_disc(fraction)</c>: the first value whose position reaches the fraction.</summary>
        public WindowFunction<T?> percentile_disc<T>(double fraction, T? value) => default!;

        /// <summary>Windowed <c>sum</c>. The <c>_over</c> suffix avoids clashing with the scalar aggregate.</summary>
        public WindowFunction<T?> sum_over<T>(T? property) => default!;

        /// <summary>Windowed <c>avg</c>.</summary>
        public WindowFunction<T?> avg_over<T>(T? property) => default!;

        /// <summary>Windowed <c>min</c>.</summary>
        public WindowFunction<T?> min_over<T>(T? property) => default!;

        /// <summary>Windowed <c>max</c>.</summary>
        public WindowFunction<T?> max_over<T>(T? property) => default!;

        /// <summary>Windowed <c>count(*)</c>.</summary>
        public WindowFunction<int> count_over() => default!;

        /// <summary>Windowed <c>count(property)</c>.</summary>
        public WindowFunction<int> count_over<T>(T? property) => default!;
    }
