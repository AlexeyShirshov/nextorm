using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

public static class NORM
{
    public static NORM_SQL SQL => default!;
    public static T Param<T>(int idx) => default!;

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
        /// this shape are supplied through the array overload (see
        /// <see cref="Over(Expression{Func{object?}}[]?, WindowOrder[]?, WindowFrame?)"/>).
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

    public class NORM_SQL
    {
        [Browsable(false)]
        public static readonly MethodInfo ExistsMI = typeof(NORM_SQL).GetMethod(nameof(exists), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo MinMI = typeof(NORM_SQL).GetMethod(nameof(min), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo MaxMI = typeof(NORM_SQL).GetMethod(nameof(max), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo AvgMI = typeof(NORM_SQL).GetMethod(nameof(avg), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo SumMI = typeof(NORM_SQL).GetMethod(nameof(sum), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo StdevMI = typeof(NORM_SQL).GetMethod(nameof(stdev), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo StdevpMI = typeof(NORM_SQL).GetMethod(nameof(stdevp), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo VarMI = typeof(NORM_SQL).GetMethod(nameof(var), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo VarpMI = typeof(NORM_SQL).GetMethod(nameof(varp), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly ConstantExpression SQLExpression = Expression.Constant(new NORM_SQL());
        public bool exists(QueryCommand cmd) => default!;
        public bool like(string? column, string? pattern) => default!;
        public bool like(string? column, string? pattern, string? escapeChar) => default!;
        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;
        public bool @in<T>(T column, IEnumerable<T> values) => default!;
        public bool @in<T>(T column, params T[] values) => default!;
        public T any<T>(QueryCommand<T> cmd) => default!;
        public T all<T>(QueryCommand<T> cmd) => default!;
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

        #region Window functions

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

        #endregion
    }
}