using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

/// <summary>
/// Static entry point for SQL constructs that are written directly inside query expressions.
/// </summary>
/// <remarks>
/// The type name is an all-caps abbreviation and the CLR surface of <see cref="NORM_SQL"/> uses
/// snake_case method names and C# keyword identifiers (<c>var</c>, <c>all</c>, <c>any</c>,
/// <c>@in</c>). This mirrors SQL intentionally, but it does not follow the .NET naming guidelines;
/// the recommended shape is a <c>SqlFunctions</c> type with PascalCase methods mapped to SQL tokens.
/// See <c>API-NAMING-REVIEW.md</c> finding P1-10.
/// </remarks>
public static partial class NORM
{
    public static NORM_SQL SQL => default!;

    /// <summary>
    /// Surface of the PostgreSQL-only functions (arrays, native JSON, the extended scalar library,
    /// PG-only aggregates and the <c>generate_series</c>/<c>unnest</c> table functions). Every
    /// member requires <see cref="ISqlDialect.SupportsArrays"/> or the matching capability flag;
    /// other providers reject it with a clear message.
    /// </summary>
    public static PG PG_SQL => default!;

    /// <summary>
    /// Surface of the SQL Server-only functions (the JSON-as-text functions and the
    /// <c>string_split</c>/<c>openjson</c> table functions). Every member requires
    /// <see cref="ISqlDialect.SupportsTextJson"/> or <see cref="ISqlDialect.SupportsTableFunction"/>;
    /// other providers reject it with a clear message.
    /// </summary>
    public static MS MS_SQL => default!;

    /// <summary>
    /// Surface of the ClickHouse-only functions (<c>argMin</c>/<c>argMax</c> and the <c>-If</c>
    /// aggregate combinator). Every member requires <see cref="ISqlDialect.SupportsArgMinMax"/> or
    /// <see cref="ISqlDialect.SupportsIfAggregates"/>; other providers reject it with a clear message.
    /// </summary>
    public static CLK CLK_SQL => default!;

    public static T Param<T>(int idx) => default!;

    /// <summary>
    /// Row shape produced by <see cref="PG.generate_series(long, long)"/>: a single column
    /// named <c>generate_series</c> holding the generated number.
    /// </summary>
    public interface IGenerateSeriesRow
    {
        [Column("generate_series")]
        long Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="PG.unnest{T}(T[])"/>: a single column named
    /// <c>unnest</c> holding the array element.
    /// </summary>
    public interface IUnnestRow<T>
    {
        [Column("unnest")]
        T Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="MS.string_split(string?, string?)"/>: a single column
    /// named <c>value</c> holding one fragment (SQL Server <c>string_split</c>).
    /// </summary>
    public interface IStringSplitRow
    {
        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="MS.openjson(string?)"/>: the <c>key</c>/<c>value</c>/<c>type</c>
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
    /// A window-function call that has not been given its <c>OVER</c> clause yet. The <c>row_number</c>,
    /// <c>rank</c>, <c>lag</c>, <c>sum_over</c>, ... methods of <see cref="NORM_SQL"/> return this
    /// wrapper so that <see cref="Over(Expression{Func{object?}}?, Expression{Func{object?}}?, WindowFrame?)"/>
    /// can be chained and returns the value type that is actually read from the result set.
    /// <para>
    /// The wrapper is only ever produced by the compiler while building an expression tree; none of its
    /// members is executed. The nested <c>partitionBy</c>/<c>orderBy</c> lambdas close over the outer
    /// query parameter, e.g. <c>NORM.SQL.row_number().Over(partitionBy: () =&gt; e.Int, orderBy: () =&gt; e.Id)</c>.
    /// </para>
    /// </summary>
    public sealed class WindowFunction<T>
    {
        /// <summary>
        /// Completes the window function with a partition and/or an ascending <c>ORDER BY</c> plus an
        /// optional frame. <c>Over()</c> renders an empty specification (<c>over ()</c>); trailing
        /// arguments may be omitted.
        /// <para>
        /// Because C# expression trees reject named arguments that skip a preceding defaulted parameter,
        /// an order-only specification must use the <see cref="WindowOrder"/> overloads
        /// (<c>Over(NORM.SQL.asc(() =&gt; e.Id))</c>) rather than <c>Over(orderBy: ...)</c>.
        /// </para>
        /// </summary>
        public T Over(
            Expression<Func<object?>>? partitionBy = null,
            Expression<Func<object?>>? orderBy = null,
            WindowFrame? frame = null) => default!;

        /// <summary>Completes the window function with a single ordered key and an optional frame.</summary>
        public T Over(WindowOrder orderBy, WindowFrame? frame = null) => default!;

        /// <summary>
        /// Completes the window function with multiple ordered keys. Use this when a mixed
        /// <c>asc</c>/<c>desc</c> ordering or more than one order key is required; partition keys for
        /// this shape are supplied through the array overload (see the <c>Over</c> overload below).
        /// </summary>
        public T Over(WindowOrder[] orderBy, WindowFrame? frame = null) => default!;

        /// <summary>
        /// Completes the window function with multiple partition and/or ordered expressions, allowing a
        /// mixed <c>asc</c>/<c>desc</c> ordering built with <see cref="NORM_SQL.asc(Expression{Func{object?}})"/>
        /// and <see cref="NORM_SQL.desc(Expression{Func{object?}})"/>. <paramref name="partitionBy"/> is
        /// required here so that <c>Over()</c> still binds unambiguously to the single-expression overload.
        /// </summary>
        public T Over(
            Expression<Func<object?>>[]? partitionBy,
            WindowOrder[]? orderBy = null,
            WindowFrame? frame = null) => default!;
    }

    /// <summary>An <c>ORDER BY</c> expression of a window specification together with its direction.</summary>
    public sealed class WindowOrder
    {
        internal WindowOrder(Expression<Func<object?>> expression, OrderDirection direction)
        {
            Expression = expression;
            Direction = direction;
        }

        /// <summary>The order key expression (a lambda closing over the outer query parameter).</summary>
        public Expression<Func<object?>> Expression { get; }

        /// <summary>Ascending or descending.</summary>
        public OrderDirection Direction { get; }
    }

    /// <summary>Which of the two SQL frame units a <see cref="WindowFrame"/> uses.</summary>
    public enum WindowFrameType
    {
        Rows,
        Range
    }

    /// <summary>The kind of a single frame boundary.</summary>
    public enum WindowFrameBoundKind
    {
        UnboundedPreceding,
        Preceding,
        CurrentRow,
        Following,
        UnboundedFollowing
    }

    /// <summary>A single boundary of a <see cref="WindowFrame"/> (e.g. <c>1 preceding</c>).</summary>
    public sealed class WindowFrameBound
    {
        private WindowFrameBound(WindowFrameBoundKind kind, int offset)
        {
            Kind = kind;
            Offset = offset;
        }

        /// <summary>Boundary kind.</summary>
        public WindowFrameBoundKind Kind { get; }

        /// <summary>The offset of a <c>preceding</c>/<c>following</c> boundary; zero otherwise.</summary>
        public int Offset { get; }

        public static WindowFrameBound UnboundedPreceding => new(WindowFrameBoundKind.UnboundedPreceding, 0);
        public static WindowFrameBound CurrentRow => new(WindowFrameBoundKind.CurrentRow, 0);
        public static WindowFrameBound UnboundedFollowing => new(WindowFrameBoundKind.UnboundedFollowing, 0);

        /// <summary>A boundary <paramref name="rows"/> rows before the current row.</summary>
        public static WindowFrameBound Preceding(int rows) => new(WindowFrameBoundKind.Preceding, rows);

        /// <summary>A boundary <paramref name="rows"/> rows after the current row.</summary>
        public static WindowFrameBound Following(int rows) => new(WindowFrameBoundKind.Following, rows);
    }

    /// <summary>
    /// The optional <c>ROWS</c>/<c>RANGE BETWEEN ... AND ...</c> frame of a window specification.
    /// Both frame units and all standard boundaries are supported by every provider nextorm targets.
    /// </summary>
    public sealed class WindowFrame
    {
        private WindowFrame(WindowFrameType type, WindowFrameBound start, WindowFrameBound end)
        {
            Type = type;
            Start = start;
            End = end;
        }

        /// <summary>Frame unit.</summary>
        public WindowFrameType Type { get; }

        /// <summary>Lower (or, for a reversed frame, first written) boundary.</summary>
        public WindowFrameBound Start { get; }

        /// <summary>Upper (or, for a reversed frame, last written) boundary.</summary>
        public WindowFrameBound End { get; }

        /// <summary>A <c>rows between</c> frame with explicit boundaries.</summary>
        public static WindowFrame Rows(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Rows, start, end);

        /// <summary>A <c>range between</c> frame with explicit boundaries.</summary>
        public static WindowFrame Range(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Range, start, end);

        /// <summary><c>rows between &lt;preceding&gt; preceding and &lt;following&gt; following</c>.</summary>
        public static WindowFrame Rows(int preceding, int following)
            => new(WindowFrameType.Rows, WindowFrameBound.Preceding(preceding), WindowFrameBound.Following(following));

        /// <summary><c>rows between unbounded preceding and current row</c>, the default running aggregate frame.</summary>
        public static WindowFrame RowsUnboundedPrecedingToCurrentRow
            => new(WindowFrameType.Rows, WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow);

        /// <summary><c>range between unbounded preceding and current row</c>.</summary>
        public static WindowFrame RangeUnboundedPrecedingToCurrentRow
            => new(WindowFrameType.Range, WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow);
    }

    /// <summary>
    /// Surface of SQL functions and predicates that can be used inside query expressions. The members
    /// are only ever evaluated by the expression translator, never at runtime.
    /// </summary>
    /// <remarks>
    /// Nested type with a snake_case name and snake_case methods; see
    /// <see cref="NORM"/> for the recommended <c>SqlFunctions</c> shape. The public
    /// <see cref="System.Reflection.MethodInfo"/> fields and the constant expression are
    /// implementation details that should be <c>internal</c>.
    /// </remarks>
    public class NORM_SQL
        // Common (cross-provider) surface.
    {
        [Browsable(false)]
        public static readonly MethodInfo ExistsMI = typeof(NORM_SQL).GetMethod(nameof(exists), BindingFlags.Public | BindingFlags.Instance)!;

        [Browsable(false)]
        public static readonly MethodInfo MinMI = AggregateMethod(nameof(min));

        [Browsable(false)]
        public static readonly MethodInfo MaxMI = AggregateMethod(nameof(max));

        [Browsable(false)]
        public static readonly MethodInfo AvgMI = AggregateMethod(nameof(avg));

        [Browsable(false)]
        public static readonly MethodInfo SumMI = AggregateMethod(nameof(sum));

        [Browsable(false)]
        public static readonly MethodInfo StdevMI = AggregateMethod(nameof(stdev));

        [Browsable(false)]
        public static readonly MethodInfo StdevpMI = AggregateMethod(nameof(stdevp));

        [Browsable(false)]
        public static readonly MethodInfo VarMI = AggregateMethod(nameof(var));

        [Browsable(false)]
        public static readonly MethodInfo VarpMI = AggregateMethod(nameof(varp));

        [Browsable(false)]
        public static readonly ConstantExpression SQLExpression = Expression.Constant(new NORM_SQL());

        /// <summary>
        /// Resolves the single-value overload of an aggregate method. Several aggregates
        /// (<c>min</c>, <c>max</c>, <c>avg</c>, <c>sum</c>) also have an <c>(value, filter)</c>
        /// overload, so a name-only <c>GetMethod</c> lookup is ambiguous; binding by parameter count
        /// selects the plain aggregate.
        /// </summary>
        private static MethodInfo AggregateMethod(string name) =>
            typeof(NORM_SQL).GetMethods(BindingFlags.Public | BindingFlags.Instance)
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

        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;

        public bool @in<T>(T column, IEnumerable<T> values) => default!;

        public bool @in<T>(T column, params T[] values) => default!;

        public T any<T>(QueryCommand<T> cmd) => default!;

        public T all<T>(QueryCommand<T> cmd) => default!;

        /// <summary><c>nullif(value, other)</c>: null when the two arguments are equal. ANSI and portable.</summary>
        public T? nullif<T>(T? value, T? other) => default!;

        /// <summary>
        /// <c>greatest(...)</c>: the largest of the arguments. NULL handling is provider-specific:
        /// PostgreSQL ignores NULLs, while MySQL/MariaDB return NULL when any argument is NULL.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsGreatestLeast"/>).
        /// </summary>
        public T? greatest<T>(params T?[] values) => default!;

        /// <summary>
        /// <c>least(...)</c>: the smallest of the arguments. NULL handling is provider-specific:
        /// PostgreSQL ignores NULLs, while MySQL/MariaDB return NULL when any argument is NULL.
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
}