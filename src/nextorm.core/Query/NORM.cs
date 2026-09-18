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
public static class NORM
{
    public static NORM_SQL SQL => default!;
    public static T Param<T>(int idx) => default!;

    /// <summary>
    /// Row shape produced by <see cref="NORM_SQL.generate_series(long, long)"/>: a single column
    /// named <c>generate_series</c> holding the generated number.
    /// </summary>
    public interface IGenerateSeriesRow
    {
        [Column("generate_series")]
        long Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="NORM_SQL.unnest{T}(T[])"/>: a single column named
    /// <c>unnest</c> holding the array element.
    /// </summary>
    public interface IUnnestRow<T>
    {
        [Column("unnest")]
        T Value { get; set; }
    }

    /// <summary>
    /// Row shape produced by <see cref="NORM_SQL.string_split(string?, string?)"/>: a single column
    /// named <c>value</c> holding one fragment (SQL Server <c>string_split</c>).
    /// </summary>
    public interface IStringSplitRow
    {
        [Column("value")]
        string? Value { get; set; }
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
        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;
        public bool @in<T>(T column, IEnumerable<T> values) => default!;
        public bool @in<T>(T column, params T[] values) => default!;
        public T any<T>(QueryCommand<T> cmd) => default!;
        public T all<T>(QueryCommand<T> cmd) => default!;

        #region Arrays (PostgreSQL)

        /// <summary>
        /// <c>column = any(array)</c>: true when <paramref name="column"/> equals any element of
        /// <paramref name="values"/>. The whole array is bound as a single parameter, so the SQL text
        /// does not depend on the number of elements.
        /// </summary>
        public bool any<T>(T column, T[] values) => default!;

        /// <summary><c>column = all(array)</c>: true when <paramref name="column"/> equals every element of <paramref name="values"/>.</summary>
        public bool all<T>(T column, T[] values) => default!;

        /// <summary>
        /// The <c>any(array)</c> quantifier as the right-hand side of a comparison:
        /// <c>x.Id == NORM.SQL.any(values)</c> renders <c>id = any(@values)</c>.
        /// </summary>
        public T any<T>(T[] values) => default!;

        /// <summary>The <c>all(array)</c> quantifier as the right-hand side of a comparison.</summary>
        public T all<T>(T[] values) => default!;

        /// <summary>Number of elements in <paramref name="array"/> (<c>cardinality</c>).</summary>
        public int cardinality<T>(T[] array) => default!;

        /// <summary>Length of the <paramref name="dimension"/> of <paramref name="array"/> (1-based), or null.</summary>
        public int? array_length<T>(T[] array, int dimension) => default!;

        /// <summary>Number of dimensions of <paramref name="array"/>, or null for an empty array.</summary>
        public int? array_ndims<T>(T[] array) => default!;

        /// <summary>Lower bound of the <paramref name="dimension"/> of <paramref name="array"/> (1-based).</summary>
        public int? array_lower<T>(T[] array, int dimension) => default!;

        /// <summary>Upper bound of the <paramref name="dimension"/> of <paramref name="array"/> (1-based).</summary>
        public int? array_upper<T>(T[] array, int dimension) => default!;

        /// <summary>One-based position of <paramref name="element"/> in <paramref name="array"/>, or null.</summary>
        public int? array_position<T>(T[] array, T element) => default!;

        /// <summary><c>array @&gt; other</c>: true when <paramref name="array"/> contains every element of <paramref name="other"/>.</summary>
        public bool array_contains<T>(T[] array, T[] other) => default!;

        /// <summary><c>array &amp;&amp; other</c>: true when <paramref name="array"/> and <paramref name="other"/> share at least one element.</summary>
        public bool array_overlaps<T>(T[] array, T[] other) => default!;

        /// <summary>Joins the elements of <paramref name="array"/> using <paramref name="delimiter"/>.</summary>
        public string? array_to_string<T>(T[] array, string delimiter) => default!;

        /// <summary>Appends <paramref name="element"/> to <paramref name="array"/> (<c>array_append</c>).</summary>
        public T[] array_append<T>(T[] array, T element) => default!;

        /// <summary>Prepends <paramref name="element"/> to <paramref name="array"/> (<c>array_prepend</c>).</summary>
        public T[] array_prepend<T>(T element, T[] array) => default!;

        /// <summary>Concatenates two arrays (<c>array_cat</c>).</summary>
        public T[] array_cat<T>(T[] array, T[] other) => default!;

        /// <summary>Removes every occurrence of <paramref name="element"/> from <paramref name="array"/>.</summary>
        public T[] array_remove<T>(T[] array, T element) => default!;

        /// <summary>Replaces every occurrence of <paramref name="from"/> in <paramref name="array"/> with <paramref name="to"/>.</summary>
        public T[] array_replace<T>(T[] array, T from, T to) => default!;

        /// <summary>Builds an array filled with <paramref name="value"/> and the given dimensions.</summary>
        public T[] array_fill<T>(T value, params int[] dimensions) => default!;

        /// <summary>A textual representation of the array dimensions (for example <c>[1:3]</c>).</summary>
        public string? array_dims<T>(T[] array) => default!;

        /// <summary>All 1-based positions of <paramref name="element"/> in <paramref name="array"/>.</summary>
        public int[]? array_positions<T>(T[] array, T element) => default!;

        /// <summary>Reverses the elements of <paramref name="array"/>.</summary>
        public T[] array_reverse<T>(T[] array) => default!;

        /// <summary>Sorts the elements of <paramref name="array"/>.</summary>
        public T[] array_sort<T>(T[] array) => default!;

        /// <summary><c>array &lt;@ other</c>: true when every element of <paramref name="array"/> is contained in <paramref name="other"/>.</summary>
        public bool array_contained_by<T>(T[] array, T[] other) => default!;

        /// <summary><c>array || other</c>: concatenates two arrays.</summary>
        public T[] array_concat<T>(T[] array, T[] other) => default!;

        /// <summary>Splits <paramref name="value"/> on <paramref name="delimiter"/> into an array.</summary>
        public string[]? string_to_array(string? value, string? delimiter) => default!;

        #endregion

        #region JSON (PostgreSQL)

        /// <summary>Aggregates the values of <paramref name="property"/> into a JSON array (<c>json_agg</c>).</summary>
        public string? json_agg<T>(T? property) => default!;

        /// <summary>Aggregates the values of <paramref name="property"/> into a JSONB array (<c>jsonb_agg</c>).</summary>
        public string? jsonb_agg<T>(T? property) => default!;

        /// <summary>Aggregates key/value pairs into a JSON object (<c>json_object_agg</c>).</summary>
        public string? json_object_agg<TKey, TValue>(TKey? key, TValue? value) => default!;

        /// <summary>Aggregates key/value pairs into a JSONB object (<c>jsonb_object_agg</c>).</summary>
        public string? jsonb_object_agg<TKey, TValue>(TKey? key, TValue? value) => default!;

        /// <summary>Builds a JSON object from alternating key/value arguments (<c>json_build_object</c>).</summary>
        public string json_build_object(params object?[] args) => default!;

        /// <summary>Builds a JSONB object from alternating key/value arguments (<c>jsonb_build_object</c>).</summary>
        public string jsonb_build_object(params object?[] args) => default!;

        /// <summary>Builds a JSON array from the arguments (<c>json_build_array</c>).</summary>
        public string json_build_array(params object?[] args) => default!;

        /// <summary>Builds a JSONB array from the arguments (<c>jsonb_build_array</c>).</summary>
        public string jsonb_build_array(params object?[] args) => default!;

        /// <summary>Converts a SQL value to <c>json</c> (<c>to_json</c>).</summary>
        public string to_json<T>(T? value) => default!;

        /// <summary>Converts a SQL value to <c>jsonb</c> (<c>to_jsonb</c>).</summary>
        public string to_jsonb<T>(T? value) => default!;

        /// <summary>
        /// Parses a text value (for example a string parameter) as <c>jsonb</c>:
        /// <c>cast(value as jsonb)</c>. Use this when the JSON is supplied as a plain string rather
        /// than as a <c>JsonDocument</c>/<c>JsonElement</c>/<c>JsonNode</c> (which Npgsql already binds
        /// as <c>jsonb</c>).
        /// </summary>
        public string json_cast(object? value) => default!;

        /// <summary><c>json -&gt; key</c>: the JSON element for <paramref name="key"/> (or null).</summary>
        public string? json_get(object? json, string? key) => default!;

        /// <summary><c>json -&gt; index</c>: the JSON element at the given array <paramref name="index"/>.</summary>
        public string? json_get(object? json, int index) => default!;

        /// <summary><c>json -&gt;&gt; key</c>: the JSON element for <paramref name="key"/> as text.</summary>
        public string? json_get_text(object? json, string? key) => default!;

        /// <summary><c>json -&gt;&gt; index</c>: the JSON element at the given array <paramref name="index"/> as text.</summary>
        public string? json_get_text(object? json, int index) => default!;

        /// <summary><c>json #&gt; path</c>: the JSON element at <paramref name="path"/>.</summary>
        public string? json_get_path(object? json, string?[] path) => default!;

        /// <summary><c>json #&gt;&gt; path</c>: the JSON element at <paramref name="path"/> as text.</summary>
        public string? json_get_path_text(object? json, string?[] path) => default!;

        /// <summary><c>json @&gt; other</c>: true when <paramref name="json"/> contains <paramref name="other"/>.</summary>
        public bool json_contains(object? json, object? other) => default!;

        /// <summary><c>json ? key</c>: true when <paramref name="key"/> exists at the top level of <paramref name="json"/>.</summary>
        public bool json_exists(object? json, string? key) => default!;

        /// <summary><c>json ?| keys</c>: true when any of <paramref name="keys"/> exists in <paramref name="json"/>.</summary>
        public bool json_exists_any(object? json, string?[] keys) => default!;

        /// <summary><c>json ?&amp; keys</c>: true when all of <paramref name="keys"/> exist in <paramref name="json"/>.</summary>
        public bool json_exists_all(object? json, string?[] keys) => default!;

        /// <summary>Number of elements in a top-level JSON array (<c>json_array_length</c>).</summary>
        public int? json_array_length(object? json) => default!;

        /// <summary>Number of elements in a top-level JSONB array (<c>jsonb_array_length</c>).</summary>
        public int? jsonb_array_length(object? json) => default!;

        /// <summary>The JSON type of the top-level value (<c>json_typeof</c>).</summary>
        public string? json_typeof(object? json) => default!;

        /// <summary>The JSONB type of the top-level value (<c>jsonb_typeof</c>).</summary>
        public string? jsonb_typeof(object? json) => default!;

        /// <summary>Replaces the value at <paramref name="path"/> (<c>jsonb_set</c>), creating it when missing.</summary>
        public string? jsonb_set(object? json, string?[] path, object? newValue) => default!;

        /// <summary>Replaces the value at <paramref name="path"/>, optionally creating it when missing.</summary>
        public string? jsonb_set(object? json, string?[] path, object? newValue, bool createIfMissing) => default!;

        /// <summary>Inserts <paramref name="newValue"/> at <paramref name="path"/> (<c>jsonb_insert</c>), before the target.</summary>
        public string? jsonb_insert(object? json, string?[] path, object? newValue) => default!;

        /// <summary>Inserts <paramref name="newValue"/> at <paramref name="path"/>, optionally after the target.</summary>
        public string? jsonb_insert(object? json, string?[] path, object? newValue, bool insertAfter) => default!;

        /// <summary>Removes null-valued object fields from <paramref name="json"/>.</summary>
        public string? jsonb_strip_nulls(object? json) => default!;

        /// <summary>Renders <paramref name="json"/> with indentation.</summary>
        public string? jsonb_pretty(object? json) => default!;

        /// <summary>Deletes the object field <paramref name="key"/> from <paramref name="json"/>.</summary>
        public string? jsonb_delete(object? json, string? key) => default!;

        /// <summary>Deletes the array element at <paramref name="index"/> from <paramref name="json"/>.</summary>
        public string? jsonb_delete(object? json, int index) => default!;

        /// <summary>Converts a row value to JSON (<c>row_to_json</c>).</summary>
        public string? row_to_json<T>(T? row) => default!;

        /// <summary>Converts an array to a JSON array (<c>array_to_json</c>).</summary>
        public string? array_to_json<T>(T[] array) => default!;

        /// <summary><c>json || other</c>: concatenates two JSON values.</summary>
        public string? json_concat(object? json, object? other) => default!;

        /// <summary>True when <paramref name="path"/> yields any item in <paramref name="json"/>.</summary>
        public bool jsonb_path_exists(object? json, string? path) => default!;

        /// <summary>True when <paramref name="path"/> yields a single boolean true in <paramref name="json"/>.</summary>
        public bool jsonb_path_match(object? json, string? path) => default!;

        /// <summary>The first item selected by <paramref name="path"/>, or null.</summary>
        public string? jsonb_path_query_first(object? json, string? path) => default!;

        /// <summary>All items selected by <paramref name="path"/> as a JSON array.</summary>
        public string? jsonb_path_query_array(object? json, string? path) => default!;

        #endregion

        #region JSON as text (SQL Server)

        /// <summary>
        /// <c>json_value(json, path)</c>: the scalar value at <paramref name="path"/>. Here JSON is
        /// stored in a plain text column rather than a native JSON type. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public string? json_value(string? json, string? path) => default!;

        /// <summary>
        /// <c>json_query(json, path)</c>: the JSON fragment (object/array) at <paramref name="path"/>.
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public string? json_query(string? json, string? path) => default!;

        /// <summary>
        /// <c>json_modify(json, path, value)</c>: a copy of <paramref name="json"/> with
        /// <paramref name="path"/> updated. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsTextJson"/>).
        /// </summary>
        public string? json_modify(string? json, string? path, string? value) => default!;

        #endregion

        #region Conditional / date / PostgreSQL scalar functions

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
        /// <c>eomonth(value)</c>: the last day of the month of <paramref name="value"/>. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsDateArithmetic"/>).
        /// </summary>
        public DateTime? end_of_month(DateTime? value) => default!;

        /// <summary>
        /// <c>string_agg(value, delimiter)</c>: concatenates the values of a group. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsStringArrayAggregates"/>).
        /// </summary>
        public string? string_agg<T>(T? value, string delimiter) => default!;

        /// <summary>Filtered <c>string_agg(value, delimiter) filter (where ...)</c>.</summary>
        public string? string_agg<T>(T? value, string delimiter, Expression<Func<bool>> filter) => default!;

        /// <summary>
        /// <c>array_agg(value)</c>: aggregates the values of a group into an array. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsStringArrayAggregates"/>).
        /// </summary>
        public T[]? array_agg<T>(T? value) => default!;

        /// <summary>Filtered <c>array_agg(value) filter (where ...)</c>.</summary>
        public T[]? array_agg<T>(T? value, Expression<Func<bool>> filter) => default!;

        #endregion

        #region Extended scalar functions (PostgreSQL)

        /// <summary>Arcsine. Requires a provider that supports the extended scalar function library.</summary>
        public double? asin(double? x) => default!;

        /// <summary>Arccosine.</summary>
        public double? acos(double? x) => default!;

        /// <summary>Arctangent.</summary>
        public double? atan(double? x) => default!;

        /// <summary>Arctangent of <paramref name="y"/>/<paramref name="x"/>, using the signs of both arguments.</summary>
        public double? atan2(double? y, double? x) => default!;

        /// <summary>Cube root.</summary>
        public double? cbrt(double? x) => default!;

        /// <summary>Hyperbolic sine.</summary>
        public double? sinh(double? x) => default!;

        /// <summary>Hyperbolic cosine.</summary>
        public double? cosh(double? x) => default!;

        /// <summary>Hyperbolic tangent.</summary>
        public double? tanh(double? x) => default!;

        /// <summary>Inverse hyperbolic sine.</summary>
        public double? asinh(double? x) => default!;

        /// <summary>Inverse hyperbolic cosine.</summary>
        public double? acosh(double? x) => default!;

        /// <summary>Inverse hyperbolic tangent.</summary>
        public double? atanh(double? x) => default!;

        /// <summary>Converts radians to degrees.</summary>
        public double? degrees(double? x) => default!;

        /// <summary>Converts degrees to radians.</summary>
        public double? radians(double? x) => default!;

        /// <summary>The constant pi.</summary>
        public double? pi() => default!;

        /// <summary>A pseudo-random value in the range 0.0 &lt;= x &lt; 1.0.</summary>
        public double? random() => default!;

        /// <summary>The logarithm of <paramref name="x"/> to the given base (two-argument <c>log</c>).</summary>
        public double? log(double? baseValue, double? x) => default!;

        /// <summary>The remainder of <c>a / b</c>.</summary>
        public T? mod<T>(T? a, T? b) => default!;

        /// <summary>The greatest common divisor.</summary>
        public T? gcd<T>(T? a, T? b) => default!;

        /// <summary>The least common multiple.</summary>
        public T? lcm<T>(T? a, T? b) => default!;

        /// <summary>The factorial.</summary>
        public T? factorial<T>(T? n) => default!;

        /// <summary>The bucket number for <paramref name="operand"/> in <paramref name="count"/> equal-width buckets.</summary>
        public int width_bucket(double? operand, double? low, double? high, int count) => default!;

        /// <summary>Splits <paramref name="value"/> on <paramref name="delimiter"/> and returns the <paramref name="field"/>-th part (1-based).</summary>
        public string? split_part(string? value, string? delimiter, int field) => default!;

        /// <summary>The 1-based position of <paramref name="substring"/> in <paramref name="value"/>.</summary>
        public int? strpos(string? value, string? substring) => default!;

        /// <summary>The first <paramref name="n"/> characters of <paramref name="value"/>.</summary>
        public string? left(string? value, int n) => default!;

        /// <summary>The last <paramref name="n"/> characters of <paramref name="value"/>.</summary>
        public string? right(string? value, int n) => default!;

        /// <summary>Left-pads <paramref name="value"/> to <paramref name="length"/> with <paramref name="fill"/>.</summary>
        public string? lpad(string? value, int length, string? fill) => default!;

        /// <summary>Right-pads <paramref name="value"/> to <paramref name="length"/> with <paramref name="fill"/>.</summary>
        public string? rpad(string? value, int length, string? fill) => default!;

        /// <summary>Repeats <paramref name="value"/> <paramref name="n"/> times.</summary>
        public string? repeat(string? value, int n) => default!;

        /// <summary>Reverses the characters of <paramref name="value"/>.</summary>
        public string? reverse(string? value) => default!;

        /// <summary>Capitalises the first letter of each word.</summary>
        public string? initcap(string? value) => default!;

        /// <summary>Replaces every character of <paramref name="from"/> in <paramref name="value"/> with the matching character of <paramref name="to"/>.</summary>
        public string? translate(string? value, string? from, string? to) => default!;

        /// <summary>Overlays <paramref name="value"/> with <paramref name="placing"/> starting at <paramref name="from"/> for <paramref name="count"/> characters.</summary>
        public string? overlay(string? value, string? placing, int from, int count) => default!;

        /// <summary>Concatenates the values with <paramref name="separator"/>, skipping nulls.</summary>
        public string? concat_ws(string? separator, params object?[] values) => default!;

        /// <summary>Formats the arguments with a printf-style format string.</summary>
        public string? format(string? formatString, params object?[] args) => default!;

        /// <summary>The MD5 hash of <paramref name="value"/> as a hexadecimal string.</summary>
        public string? md5(string? value) => default!;

        /// <summary>Replaces the matches of a POSIX regular expression.</summary>
        public string? regexp_replace(string? value, string? pattern, string? replacement) => default!;

        /// <summary>Replaces the matches of a POSIX regular expression with flags.</summary>
        public string? regexp_replace(string? value, string? pattern, string? replacement, string? flags) => default!;

        /// <summary>True when <paramref name="value"/> matches the POSIX regular expression.</summary>
        public bool regexp_like(string? value, string? pattern) => default!;

        /// <summary>True when <paramref name="value"/> matches the POSIX regular expression with flags.</summary>
        public bool regexp_like(string? value, string? pattern, string? flags) => default!;

        /// <summary>Splits <paramref name="value"/> on a POSIX regular expression into an array.</summary>
        public string[]? regexp_split_to_array(string? value, string? pattern) => default!;

        /// <summary>The number of matches of a POSIX regular expression in <paramref name="value"/>.</summary>
        public int? regexp_count(string? value, string? pattern) => default!;

        /// <summary>The 1-based position of the first match of a POSIX regular expression in <paramref name="value"/>.</summary>
        public int? regexp_instr(string? value, string? pattern) => default!;

        /// <summary>The interval between two timestamps (<c>age</c>).</summary>
        public TimeSpan? age(DateTime? a, DateTime? b) => default!;

        /// <summary>Truncates <paramref name="source"/> to the given <paramref name="stride"/> relative to <paramref name="origin"/>.</summary>
        public DateTime? date_bin(TimeSpan? stride, DateTime? source, DateTime? origin) => default!;

        /// <summary>Builds a date from its parts.</summary>
        public DateTime? make_date(int year, int month, int day) => default!;

        /// <summary>Builds an interval from its parts.</summary>
        public TimeSpan? make_interval(int years, int months, int days, int hours, int minutes, double seconds) => default!;

        /// <summary>Adjusts an interval so that 30-day units are represented as months.</summary>
        public TimeSpan? justify_days(TimeSpan? interval) => default!;

        /// <summary>Adjusts an interval so that 24-hour units are represented as days.</summary>
        public TimeSpan? justify_hours(TimeSpan? interval) => default!;

        /// <summary>Formats a timestamp with a template.</summary>
        public string? to_char(DateTime? value, string? formatString) => default!;

        /// <summary>Formats a number with a template.</summary>
        public string? to_char(decimal? value, string? formatString) => default!;

        /// <summary>Parses a date from text using a template.</summary>
        public DateTime? to_date(string? text, string? formatString) => default!;

        /// <summary>Parses a number from text using a template.</summary>
        public decimal? to_number(string? text, string? formatString) => default!;

        /// <summary>Converts a Unix epoch to a timestamp.</summary>
        public DateTime? to_timestamp(double? epoch) => default!;

        /// <summary>Parses a timestamp from text using a template.</summary>
        public DateTime? to_timestamp(string? text, string? formatString) => default!;

        /// <summary>Converts <paramref name="value"/> to the given time zone.</summary>
        public DateTime? timezone(string? zone, DateTime? value) => default!;

        /// <summary>Extracts a date/time field (<c>quarter</c>, <c>week</c>, <c>epoch</c>, <c>dow</c>, <c>doy</c>, <c>isodow</c>, ...).</summary>
        public double? extract(string field, DateTime? value) => default!;

        /// <summary>The current date.</summary>
        public DateTime? current_date() => default!;

        /// <summary>The current time of day with time zone.</summary>
        public TimeSpan? current_time() => default!;

        /// <summary>The current time of day without time zone.</summary>
        public TimeSpan? localtime() => default!;

        /// <summary>The current timestamp without time zone.</summary>
        public DateTime? localtimestamp() => default!;

        /// <summary>The number of null arguments (<c>num_nulls</c>).</summary>
        public int num_nulls(params object?[] values) => default!;

        /// <summary>The number of non-null arguments (<c>num_nonnulls</c>).</summary>
        public int num_nonnulls(params object?[] values) => default!;

        #endregion

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

        #region Boolean / bitwise / statistical / ordered-set aggregates (PostgreSQL)

        /// <summary><c>bool_and(property)</c>: true when every non-null value is true. Requires a provider that supports it.</summary>
        public T? bool_and<T>(T? property) => default!;

        /// <summary><c>bool_or(property)</c>: true when at least one non-null value is true.</summary>
        public T? bool_or<T>(T? property) => default!;

        /// <summary><c>every(property)</c>: a synonym of <c>bool_and</c>.</summary>
        public T? every<T>(T? property) => default!;

        /// <summary><c>bit_and(property)</c>: the bitwise AND of all non-null values.</summary>
        public T? bit_and<T>(T? property) => default!;

        /// <summary><c>bit_or(property)</c>: the bitwise OR of all non-null values.</summary>
        public T? bit_or<T>(T? property) => default!;

        /// <summary><c>bit_xor(property)</c>: the bitwise XOR of all non-null values.</summary>
        public T? bit_xor<T>(T? property) => default!;

        /// <summary><c>corr(Y, X)</c>: the correlation coefficient of a set of (Y, X) pairs.</summary>
        public double? corr<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>covar_pop(Y, X)</c>: the population covariance.</summary>
        public double? covar_pop<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>covar_samp(Y, X)</c>: the sample covariance.</summary>
        public double? covar_samp<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>regr_slope(Y, X)</c>: the slope of the least-squares fit.</summary>
        public double? regr_slope<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>regr_intercept(Y, X)</c>: the y-intercept of the least-squares fit.</summary>
        public double? regr_intercept<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>regr_r2(Y, X)</c>: the coefficient of determination.</summary>
        public double? regr_r2<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>regr_count(Y, X)</c>: the number of input rows where both arguments are non-null.</summary>
        public double? regr_count<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>regr_avgx(Y, X)</c>: the average of the independent variable.</summary>
        public double? regr_avgx<TY, TX>(TY? y, TX? x) => default!;

        /// <summary><c>regr_avgy(Y, X)</c>: the average of the dependent variable.</summary>
        public double? regr_avgy<TY, TX>(TY? y, TX? x) => default!;

        /// <summary>
        /// <c>percentile_cont(fraction) within group (order by key)</c>: the interpolated value at
        /// the given fraction. Requires a provider that supports ordered-set aggregates.
        /// </summary>
        public T? percentile_cont<T>(double fraction, Expression<Func<T>> orderBy) => default!;

        /// <summary><c>percentile_disc(fraction) within group (order by key)</c>: the first value whose position reaches the fraction.</summary>
        public T? percentile_disc<T>(double fraction, Expression<Func<T>> orderBy) => default!;

        /// <summary><c>mode() within group (order by key)</c>: the most frequent value.</summary>
        public T? mode<T>(Expression<Func<T>> orderBy) => default!;

        /// <summary>
        /// <c>argMin(value, by)</c>: the <paramref name="value"/> of the row where <paramref name="by"/>
        /// is the smallest. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsArgMinMax"/>; ClickHouse).
        /// </summary>
        public TValue? arg_min<TValue, TBy>(TValue? value, TBy? by) => default!;

        /// <summary><c>argMax(value, by)</c>: the <paramref name="value"/> of the row where <paramref name="by"/> is the largest.</summary>
        public TValue? arg_max<TValue, TBy>(TValue? value, TBy? by) => default!;

        /// <summary>
        /// <c>countIf(predicate)</c>: the number of rows for which the predicate is true. Requires a
        /// provider that renders the <c>-If</c> combinator (see
        /// <see cref="ISqlDialect.SupportsIfAggregates"/>; ClickHouse).
        /// </summary>
        public int count_if(Expression<Func<bool>> filter) => default!;

        /// <summary><c>sumIf(value, predicate)</c>: the sum of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? sum_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        /// <summary><c>avgIf(value, predicate)</c>: the average of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? avg_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        /// <summary><c>minIf(value, predicate)</c>: the minimum of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? min_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        /// <summary><c>maxIf(value, predicate)</c>: the maximum of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? max_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        #endregion

        #region Table functions

        /// <summary>
        /// <c>generate_series(start, stop)</c> as a FROM source; select
        /// <see cref="NORM.IGenerateSeriesRow.Value"/>. Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// </summary>
        [SqlTableFunction("generate_series")]
        public IQueryable<IGenerateSeriesRow> generate_series(long start, long stop) => throw new NotSupportedException();

        /// <summary><c>generate_series(start, stop, step)</c> as a FROM source.</summary>
        [SqlTableFunction("generate_series")]
        public IQueryable<IGenerateSeriesRow> generate_series(long start, long stop, long step) => throw new NotSupportedException();

        /// <summary>
        /// <c>unnest(array)</c> as a FROM source; select <see cref="NORM.IUnnestRow{T}.Value"/>. The
        /// array is normally supplied through <c>NORM.Param&lt;T[]&gt;(idx)</c>.
        /// </summary>
        [SqlTableFunction("unnest")]
        public IQueryable<IUnnestRow<T>> unnest<T>(T[] array) => throw new NotSupportedException();

        /// <summary>
        /// <c>string_split(value, separator)</c> as a FROM source (SQL Server 2016+); select
        /// <see cref="NORM.IStringSplitRow.Value"/>. Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// The fragments are not guaranteed to be ordered; add an <c>order by</c> on the caller side if
        /// the input order matters.
        /// </summary>
        [SqlTableFunction("string_split")]
        public IQueryable<IStringSplitRow> string_split(string? value, string? separator) => throw new NotSupportedException();

        #endregion

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