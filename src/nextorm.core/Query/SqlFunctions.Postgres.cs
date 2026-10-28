using System.Linq;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// PostgreSQL-only SQL surface: arrays, native JSON, the extended scalar library, the
/// PostgreSQL-only aggregates and the <c>generate_series</c>/<c>unnest</c> table functions.
/// Exposed through <see cref="SqlFunctions.Postgres"/>; every member is gated by a capability flag and
/// rejected by providers that do not opt in.
/// </summary>
    public sealed class PostgresFunctions : CommonFunctions
    {
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
        /// <c>x.Id == SqlFunctions.Sql.any(values)</c> renders <c>id = any(@values)</c>.
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

        /// <summary>
        /// Reverses the elements of <paramref name="array"/>. No current PostgreSQL release provides
        /// <c>array_reverse</c>, so the PostgreSQL provider refuses the call at preparation with
        /// <see cref="NotSupportedException"/> and never emits the function; no fallback (a
        /// <see langword="null"/> array or an unchanged array) is substituted.
        /// </summary>
        public T[] array_reverse<T>(T[] array) => default!;

        /// <summary>
        /// Sorts the elements of <paramref name="array"/> (<c>array_sort</c>, PostgreSQL 18+). The
        /// PostgreSQL provider renders it only for an explicit server version 18 or later; a version
        /// below 18 and an unset version refuse the call at preparation with
        /// <see cref="NotSupportedException"/>.
        /// </summary>
        public T[] array_sort<T>(T[] array) => default!;

        /// <summary>Returns a randomly shuffled copy of <paramref name="array"/> (<c>array_shuffle</c>, PostgreSQL 16+).</summary>
        public T[] array_shuffle<T>(T[] array) => default!;

        /// <summary>Returns <paramref name="n"/> randomly selected elements of <paramref name="array"/> (<c>array_sample</c>, PostgreSQL 16+).</summary>
        public T[] array_sample<T>(T[] array, int n) => default!;

        /// <summary>
        /// Removes the last <paramref name="n"/> elements of a one-dimensional <paramref name="array"/>
        /// (<c>trim_array</c>), keeping the element type. PostgreSQL raises an error when
        /// <paramref name="n"/> is greater than the array length, and only one-dimensional arrays are
        /// accepted; a SQL-NULL array stays NULL.
        /// </summary>
        public T[] trim_array<T>(T[] array, int? n) => default!;

        /// <summary><c>array &lt;@ other</c>: true when every element of <paramref name="array"/> is contained in <paramref name="other"/>.</summary>
        public bool array_contained_by<T>(T[] array, T[] other) => default!;

        /// <summary><c>array || other</c>: concatenates two arrays.</summary>
        public T[] array_concat<T>(T[] array, T[] other) => default!;

        /// <summary>Splits <paramref name="value"/> on <paramref name="delimiter"/> into an array.</summary>
        public string[]? string_to_array(string? value, string? delimiter) => default!;

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

        /// <summary>Builds a JSON array with the SQL/JSON constructor (<c>json_array</c>).</summary>
        public string json_array(params object?[] values) => default!;

        /// <summary>
        /// Builds a JSONB array with the SQL/JSON constructor
        /// (<c>json_array(... returning jsonb)</c>, exposed as <c>jsonb_array</c>).
        /// </summary>
        public string jsonb_array(params object?[] values) => default!;

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

        /// <summary>
        /// SQL/JSON <c>json(expression FORMAT JSON)</c>: parses <paramref name="value"/>'s text as a JSON
        /// document and returns it as <c>json</c>. The argument is rendered as text
        /// (<c>cast(value as text)</c>) and <c>FORMAT JSON</c> declares it to be JSON text, so an invalid
        /// document is rejected by the server. Distinct from <see cref="json_scalar{T}(T)"/>, which
        /// converts a value to JSON (a string becomes a JSON string, not a parsed document).
        /// A PostgreSQL 17 SQL/JSON constructor: it is grammar, not a <c>pg_proc</c> function.
        /// </summary>
        public string? json<T>(T? value) => default!;

        /// <summary>
        /// SQL/JSON <c>json_scalar(value)</c>: converts <paramref name="value"/> to a <c>json</c> scalar
        /// (a string becomes a JSON string, a number a JSON number, SQL NULL stays NULL).
        /// A PostgreSQL 17 SQL/JSON constructor: grammar, not a <c>pg_proc</c> function.
        /// </summary>
        public string? json_scalar<T>(T? value) => default!;

        /// <summary>
        /// SQL/JSON <c>json_serialize(expression FORMAT JSON RETURNING text)</c>: parses
        /// <paramref name="value"/>'s text as JSON and returns its serialized <c>text</c>. The result is
        /// text, not <c>json</c> — that is the difference from <see cref="json{T}(T)"/>.
        /// A PostgreSQL 17 SQL/JSON constructor: grammar, not a <c>pg_proc</c> function.
        /// </summary>
        public string? json_serialize<T>(T? value) => default!;

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

        /// <summary>
        /// <c>json_extract_path(json, path)</c>: the <c>json</c> element at the key/index
        /// <paramref name="path"/>. The document is a JSON <em>text</em> (the argument is cast to
        /// <c>json</c> in the emitted SQL); the path is passed to the variadic parameter as one
        /// <c>text[]</c>. An absent path, a JSON <c>null</c> element and a SQL NULL path all yield SQL
        /// NULL; an empty path returns the document itself.
        /// </summary>
        public string? json_extract_path(string? json, string?[] path) => default!;

        /// <summary><c>json_extract_path_text(json, path)</c>: the element at <paramref name="path"/> as text (SQL NULL when absent).</summary>
        public string? json_extract_path_text(string? json, string?[] path) => default!;

        /// <summary><c>jsonb_extract_path(json, path)</c>: the <c>jsonb</c> element at <paramref name="path"/> (SQL NULL when absent).</summary>
        public string? jsonb_extract_path(string? json, string?[] path) => default!;

        /// <summary><c>jsonb_extract_path_text(json, path)</c>: the <c>jsonb</c> element at <paramref name="path"/> as text (SQL NULL when absent).</summary>
        public string? jsonb_extract_path_text(string? json, string?[] path) => default!;

        /// <summary>
        /// <c>jsonb_set_lax(json, path, new_value, create_if_missing, null_value_treatment)</c>: replaces
        /// the value at <paramref name="path"/>, with the SQL-NULL <paramref name="newValue"/> handled by
        /// <paramref name="nullValueTreatment"/> (<c>use_json_null</c>, <c>delete_key</c>,
        /// <c>return_target</c> or <c>raise_exception</c>). Both PostgreSQL defaults are exposed as
        /// explicit arguments (<paramref name="createIfMissing"/> = <c>true</c>,
        /// <paramref name="nullValueTreatment"/> = <c>use_json_null</c>) instead of a nullable overload.
        /// </summary>
        public string? jsonb_set_lax(string? json, string?[] path, string? newValue, bool? createIfMissing, string? nullValueTreatment) => default!;

        /// <summary>
        /// <c>jsonb_path_exists_tz(json, path, vars, silent)</c>: true when <paramref name="path"/> selects
        /// any item. The <c>_tz</c> form is required for paths that depend on the session time zone (for
        /// example the <c>.datetime()</c> method); the non-<c>_tz</c> sibling raises an error for them.
        /// </summary>
        public bool? jsonb_path_exists_tz(string? json, string? path, string? vars, bool? silent) => default!;

        /// <summary><c>jsonb_path_match_tz(json, path, vars, silent)</c>: true when <paramref name="path"/> selects a single true (time-zone-aware).</summary>
        public bool? jsonb_path_match_tz(string? json, string? path, string? vars, bool? silent) => default!;

        /// <summary><c>jsonb_path_query_array_tz(json, path, vars, silent)</c>: all matches as a <c>jsonb</c> array (time-zone-aware).</summary>
        public string? jsonb_path_query_array_tz(string? json, string? path, string? vars, bool? silent) => default!;

        /// <summary><c>jsonb_path_query_first_tz(json, path, vars, silent)</c>: the first match, or SQL NULL (time-zone-aware).</summary>
        public string? jsonb_path_query_first_tz(string? json, string? path, string? vars, bool? silent) => default!;

        /// <summary>
        /// Casts <paramref name="path"/> to the PostgreSQL <c>jsonpath</c> type
        /// (<c>cast(path as jsonpath)</c>). Use it for the path operand of the JSONPath functions and
        /// the <see cref="jsonb_path_query(object?, string?)"/> table function, because a text
        /// parameter is not implicitly coerced to <c>jsonpath</c>.
        /// </summary>
        public string? jsonpath(string? path) => default!;

        /// <summary>
        /// SQL/JSON <c>json_value(json, jsonpath)</c>: the scalar value selected by <paramref name="path"/>.
        /// The path is cast to <c>jsonpath</c>. This is the SQL/JSON query function, not the
        /// <c>-&gt;&gt;</c> operator (<see cref="json_get_text(object?, string?)"/>).
        /// </summary>
        public string? json_value(object? json, string? path) => default!;

        /// <summary>
        /// SQL/JSON <c>json_query(json, jsonpath)</c>: the JSON value selected by <paramref name="path"/>.
        /// The path is cast to <c>jsonpath</c>. This is the SQL/JSON query function, not the
        /// <c>-&gt;</c> operator (<see cref="json_get(object?, string?)"/>).
        /// </summary>
        public string? json_query(object? json, string? path) => default!;

        /// <summary>
        /// SQL/JSON <c>json_exists(json, jsonpath)</c>: true when <paramref name="path"/> yields at
        /// least one item. The path is cast to <c>jsonpath</c>. The two-argument
        /// <see cref="json_exists(object?, string?)"/> is the top-level <c>?</c> operator, so the
        /// SQL/JSON overload carries an extra discriminator that is not rendered into SQL.
        /// </summary>
        /// <param name="json">The JSON document.</param>
        /// <param name="path">The SQL/JSON path expression.</param>
        /// <param name="fromJsonPath">Selects the SQL/JSON overload; it is not rendered into SQL.</param>
        public bool json_exists(object? json, string? path, bool fromJsonPath) => default!;

        /// <summary>
        /// <c>array_agg(value)</c>: aggregates the values of a group into an array. Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SupportsStringArrayAggregates"/>).
        /// </summary>
        public T[]? array_agg<T>(T? value) => default!;

        /// <summary>Filtered <c>array_agg(value) filter (where ...)</c>.</summary>
        public T[]? array_agg<T>(T? value, Expression<Func<bool>> filter) => default!;

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

        /// <summary>A pseudo-random value in the range 0.0 &lt;= x &lt; 1.0.</summary>
        public double? random() => default!;

        /// <summary>
        /// Sets the seed for subsequent <see cref="random"/> calls in the session (<c>setseed</c>).
        /// Requires a provider that supports it (see <see cref="ISqlDialect.SupportsRandomSeed"/>;
        /// PostgreSQL only). PostgreSQL's <c>setseed</c> returns <c>void</c>, so the projected value is
        /// always <c>null</c>; the call is made for its side effect.
        /// </summary>
        /// <returns>Always <c>null</c>, because PostgreSQL's <c>setseed</c> returns <c>void</c>.</returns>
        public double? setseed(double? seed) => default!;

        /// <summary>The logarithm of <paramref name="x"/> to the given base (two-argument <c>log</c>).</summary>
        public double? log(double? baseValue, double? x) => default!;

        /// <summary>The greatest common divisor.</summary>
        public T? gcd<T>(T? a, T? b) => default!;

        /// <summary>The least common multiple.</summary>
        public T? lcm<T>(T? a, T? b) => default!;

        /// <summary>The factorial.</summary>
        public T? factorial<T>(T? n) => default!;

        /// <summary>The bucket number for <paramref name="operand"/> in <paramref name="count"/> equal-width buckets.</summary>
        public int width_bucket(double? operand, double? low, double? high, int count) => default!;

        /// <summary>
        /// Integer division truncating toward zero (<c>div(y, x)</c>) on <c>numeric</c> operands, so
        /// <c>div(7, 2)</c> is <c>3</c> and <c>div(-7, 2)</c> is <c>-3</c>. Division by zero raises on
        /// the server. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public decimal? div(decimal? y, decimal? x) => default!;

        /// <summary>
        /// The error function <c>erf(x)</c> of <paramref name="x"/>, in the range -1..1. A NULL argument
        /// yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public double? erf(double? x) => default!;

        /// <summary>
        /// The complementary error function <c>erfc(x)</c> = 1 - <c>erf(x)</c>, in the range 0..2. A
        /// NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public double? erfc(double? x) => default!;

        /// <summary>
        /// A normally distributed random value with the given <paramref name="mean"/> and
        /// <paramref name="stddev"/> (<c>random_normal</c>); the result is volatile, so the same query
        /// returns a different value on every execution. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public double? random_normal(double? mean, double? stddev) => default!;

        /// <summary>Arccosine in degrees (<c>acosd</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? acosd(double? x) => default!;

        /// <summary>Arcsine in degrees (<c>asind</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? asind(double? x) => default!;

        /// <summary>Arctangent in degrees (<c>atand</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? atand(double? x) => default!;

        /// <summary>Arctangent of <paramref name="y"/>/<paramref name="x"/> in degrees, using the signs of both arguments (<c>atan2d</c>). A NULL argument yields NULL.</summary>
        public double? atan2d(double? y, double? x) => default!;

        /// <summary>Cosine in degrees (<c>cosd</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? cosd(double? x) => default!;

        /// <summary>Cotangent in degrees (<c>cotd</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? cotd(double? x) => default!;

        /// <summary>Sine in degrees (<c>sind</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? sind(double? x) => default!;

        /// <summary>Tangent in degrees (<c>tand</c>). A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.</summary>
        public double? tand(double? x) => default!;

        /// <summary>
        /// <c>gamma(x)</c> is refused: it does not exist on the pinned PostgreSQL 17.11 server (absent
        /// from <c>pg_proc</c>, absent from the PG17 mathematical-functions table, and the name fails
        /// with <c>42883</c>), so any SQL emitted for it would be rejected by the target server.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public double? gamma(double? x) =>
            throw new NotSupportedException(
                "The gamma function is refused: it does not exist on the pinned PostgreSQL 17.11 server.");

        /// <summary>
        /// <c>tgamma(x)</c> is refused: it does not exist on the pinned PostgreSQL 17.11 server (absent
        /// from <c>pg_proc</c> and the name fails with <c>42883</c>), so any SQL emitted for it would be
        /// rejected by the target server.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public double? tgamma(double? x) =>
            throw new NotSupportedException(
                "The tgamma function is refused: it does not exist on the pinned PostgreSQL 17.11 server.");

        /// <summary>
        /// <c>lgamma(x)</c> is refused: it does not exist on the pinned PostgreSQL 17.11 server (absent
        /// from <c>pg_proc</c> and the name fails with <c>42883</c>), so any SQL emitted for it would be
        /// rejected by the target server.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public double? lgamma(double? x) =>
            throw new NotSupportedException(
                "The lgamma function is refused: it does not exist on the pinned PostgreSQL 17.11 server.");

        /// <summary>
        /// The number of decimal digits in the fractional part of <paramref name="value"/>
        /// (<c>scale</c>, PostgreSQL). Trailing zeroes count, so <c>12.3400</c> has scale 4. A NULL
        /// argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int? scale(decimal? value) => default!;

        /// <summary>
        /// The number of fractional digits that remain after the trailing zeroes of
        /// <paramref name="value"/> are removed (<c>min_scale</c>, PostgreSQL 13+); <c>12.3400</c>
        /// reports 2. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int? min_scale(decimal? value) => default!;

        /// <summary>
        /// <paramref name="value"/> with its trailing fractional zeroes removed (<c>trim_scale</c>,
        /// PostgreSQL 13+): <c>12.3400</c> becomes <c>12.34</c>. Only zeroes are removed, so this is
        /// not rounding and an input without removable zeroes is returned unchanged. A NULL argument
        /// yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public decimal? trim_scale(decimal? value) => default!;

        /// <summary>Splits <paramref name="value"/> on <paramref name="delimiter"/> and returns the <paramref name="field"/>-th part (1-based).</summary>
        public string? split_part(string? value, string? delimiter, int field) => default!;

        /// <summary>The 1-based position of <paramref name="substring"/> in <paramref name="value"/>.</summary>
        public int? strpos(string? value, string? substring) => default!;

        /// <summary>Capitalises the first letter of each word.</summary>
        public string? initcap(string? value) => default!;

        /// <summary>Overlays <paramref name="value"/> with <paramref name="placing"/> starting at <paramref name="from"/> for <paramref name="count"/> characters.</summary>
        public string? overlay(string? value, string? placing, int from, int count) => default!;

        /// <summary>Formats the arguments with a printf-style format string.</summary>
        public string? format(string? formatString, params object?[] args) => default!;

        /// <summary>The MD5 hash of <paramref name="value"/> as a hexadecimal string.</summary>
        public string? md5(string? value) => default!;

        /// <summary>
        /// The binary hash of <paramref name="data"/> using the algorithm named by
        /// <paramref name="type"/> (<c>md5</c>, <c>sha1</c>, <c>sha224</c>, <c>sha256</c>,
        /// <c>sha384</c>, <c>sha512</c>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsCryptoFunctions"/>; PostgreSQL) and the <c>pgcrypto</c>
        /// extension installed on the server.
        /// </summary>
        public byte[]? digest(string? data, string? type) => default!;

        /// <summary>
        /// The binary hash of the <paramref name="data"/> bytes using the algorithm named by
        /// <paramref name="type"/>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsCryptoFunctions"/>; PostgreSQL) and the <c>pgcrypto</c>
        /// extension installed on the server.
        /// </summary>
        public byte[]? digest(byte[]? data, string? type) => default!;

        /// <summary>
        /// The SHA-256 hash of <paramref name="data"/> as a <c>bytea</c> (<c>sha256</c>). Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsCryptoFunctions"/>;
        /// PostgreSQL, where this is a core binary-string function).
        /// </summary>
        public byte[]? sha256(byte[]? data) => default!;

        /// <summary>
        /// The SHA-224 hash of <paramref name="data"/> as a <c>bytea</c> (<c>sha224</c>). Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsCryptoFunctions"/>;
        /// PostgreSQL, where this is a core binary-string function).
        /// </summary>
        public byte[]? sha224(byte[]? data) => default!;

        /// <summary>
        /// The SHA-384 hash of <paramref name="data"/> as a <c>bytea</c> (<c>sha384</c>). Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsCryptoFunctions"/>;
        /// PostgreSQL, where this is a core binary-string function).
        /// </summary>
        public byte[]? sha384(byte[]? data) => default!;

        /// <summary>
        /// The SHA-512 hash of <paramref name="data"/> as a <c>bytea</c> (<c>sha512</c>). Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsCryptoFunctions"/>;
        /// PostgreSQL, where this is a core binary-string function).
        /// </summary>
        public byte[]? sha512(byte[]? data) => default!;

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

        /// <summary>The substring matched by the first POSIX regular expression match in <paramref name="value"/> (<c>regexp_substr</c>).</summary>
        public string? regexp_substr(string? value, string? pattern) => default!;

        /// <summary>The substring matched by the first POSIX regular expression match in <paramref name="value"/> with <paramref name="flags"/>.</summary>
        public string? regexp_substr(string? value, string? pattern, string? flags) => default!;

        /// <summary>
        /// The first POSIX regular-expression match of <paramref name="pattern"/> in
        /// <paramref name="value"/> as a <c>text[]</c> of its captured groups — the whole match when the
        /// pattern has no groups. A pattern with no match returns <see langword="null"/> (not an empty
        /// array), and a NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string?[] regexp_match(string? value, string? pattern) => default!;

        /// <summary>
        /// <c>regexp_match(value, pattern, flags)</c>: the first match with the given
        /// <c>g</c>/<c>i</c>/<c>n</c>/<c>p</c>/<c>w</c>/<c>x</c> flags. A pattern with no match returns
        /// <see langword="null"/>, and a NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string?[] regexp_match(string? value, string? pattern, string? flags) => default!;

        /// <summary>
        /// Removes the longest string containing only whitespace from both ends of
        /// <paramref name="value"/> (<c>btrim</c>, the one-argument form). A NULL argument yields NULL.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? btrim(string? value) => default!;

        /// <summary>
        /// Removes the longest string containing only the characters of <paramref name="characters"/>
        /// from both ends of <paramref name="value"/> (<c>btrim</c>, two-argument form). The second
        /// argument is a character set, not a substring. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? btrim(string? value, string? characters) => default!;

        /// <summary>
        /// The <c>bytea</c> form of <see cref="btrim(string?, string?)"/>: removes the leading and
        /// trailing bytes that occur in <paramref name="bytesToTrim"/>. A NULL argument yields NULL.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? btrim(byte[]? value, byte[]? bytesToTrim) => default!;

        /// <summary>
        /// Removes the longest string containing only the characters of <paramref name="characters"/>
        /// from the start of <paramref name="value"/> (<c>ltrim</c>, two-argument form). The
        /// one-argument form is the built-in <see cref="string.TrimStart()"/> translation. A NULL
        /// argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? ltrim(string? value, string? characters) => default!;

        /// <summary>
        /// The <c>bytea</c> form of <see cref="ltrim(string?, string?)"/>: removes the leading bytes
        /// that occur in <paramref name="bytesToTrim"/>. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? ltrim(byte[]? value, byte[]? bytesToTrim) => default!;

        /// <summary>
        /// Removes the longest string containing only the characters of <paramref name="characters"/>
        /// from the end of <paramref name="value"/> (<c>rtrim</c>, two-argument form). The
        /// one-argument form is the built-in <see cref="string.TrimEnd()"/> translation. A NULL
        /// argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? rtrim(string? value, string? characters) => default!;

        /// <summary>
        /// The <c>bytea</c> form of <see cref="rtrim(string?, string?)"/>: removes the trailing bytes
        /// that occur in <paramref name="bytesToTrim"/>. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? rtrim(byte[]? value, byte[]? bytesToTrim) => default!;

        /// <summary>
        /// True when <paramref name="value"/> begins with <paramref name="prefix"/> (<c>starts_with</c>,
        /// collation-aware, case-sensitive unless the collation says otherwise). A NULL argument yields
        /// NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? starts_with(string? value, string? prefix) => default!;

        /// <summary>
        /// Splits a qualified identifier into its dot-separated parts (<c>parse_ident</c>), removing the
        /// quoting of each part. With <paramref name="strict"/> true an invalid identifier raises
        /// instead of being truncated after the first error. Returns <c>text[]</c>; a NULL argument
        /// yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string?[] parse_ident(string? value, bool? strict) => default!;

        /// <summary>
        /// Quotes <paramref name="value"/> as an SQL identifier, adding double quotes only when required
        /// (<c>quote_ident</c>): <c>a b</c> becomes <c>"a b"</c>, <c>test</c> stays unquoted. A NULL
        /// argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? quote_ident(string? value) => default!;

        /// <summary>
        /// Quotes <paramref name="value"/> as an SQL string literal, doubling embedded single quotes and
        /// prefixing a backslash-containing value with <c>E</c> (<c>quote_literal</c>, the <c>text</c>
        /// overload). A NULL argument yields NULL — use
        /// <see cref="quote_nullable(string?)"/> to obtain the <c>NULL</c> keyword. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? quote_literal(string? value) => default!;

        /// <summary>
        /// Like <see cref="quote_literal(string?)"/>, but a NULL argument yields the four-character text
        /// <c>NULL</c> instead of NULL (<c>quote_nullable</c>, the <c>text</c> overload). Gated per name
        /// by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? quote_nullable(string? value) => default!;

        /// <summary>
        /// The one-argument <c>to_ascii(text)</c> is refused: it takes the source encoding from the
        /// database encoding, and PostgreSQL 17 supports the conversion only from <c>LATIN1</c>,
        /// <c>LATIN2</c>, <c>LATIN9</c> and <c>WIN1250</c>. The pinned fixture database is UTF8, so the
        /// form raises <c>0A000</c> even for ASCII-only input. Use
        /// <see cref="to_ascii(string?, string?)"/> or <see cref="to_ascii(string?, int)"/> with an
        /// explicit encoding instead.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public string? to_ascii(string? value) =>
            throw new NotSupportedException(
                "The one-argument to_ascii function is refused: PostgreSQL 17 supports the conversion only " +
                "from LATIN1, LATIN2, LATIN9 and WIN1250, and the pinned fixture database is UTF8, so the " +
                "form raises 0A000; use to_ascii(value, encoding).");

        /// <summary>
        /// <c>to_ascii(value, encoding)</c> with the encoding named as text (for example
        /// <c>LATIN1</c>). A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? to_ascii(string? value, string? encoding) => default!;

        /// <summary>
        /// <c>to_ascii(value, encoding)</c> with the encoding given as its numeric code — the
        /// identifier reported by <c>pg_encoding_to_char</c> (<c>8</c> = <c>LATIN1</c>, <c>9</c> =
        /// <c>LATIN2</c>, <c>16</c> = <c>LATIN9</c>, <c>29</c> = <c>WIN1250</c>). This is the documented
        /// <c>to_ascii(text, integer)</c> overload. The code is validated by the server when the command
        /// runs, so an unknown code raises <c>42704</c> instead of being refused while the query is
        /// prepared. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? to_ascii(string? value, int encoding) => default!;

        /// <summary>
        /// The hexadecimal representation of <paramref name="value"/> (<c>to_hex</c>); negative values
        /// keep the two's-complement sign, so <c>-1</c> is <c>ffffffff</c>. A NULL argument yields NULL.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? to_hex(int? value) => default!;

        /// <summary>The hexadecimal representation of the 64-bit <paramref name="value"/> (<c>to_hex</c>). A NULL argument yields NULL.</summary>
        public string? to_hex(long? value) => default!;

        /// <summary>The octal representation of <paramref name="value"/> (<c>to_oct</c>). A NULL argument yields NULL.</summary>
        public string? to_oct(int? value) => default!;

        /// <summary>The octal representation of the 64-bit <paramref name="value"/> (<c>to_oct</c>). A NULL argument yields NULL.</summary>
        public string? to_oct(long? value) => default!;

        /// <summary>The binary representation of <paramref name="value"/> (<c>to_bin</c>). A NULL argument yields NULL.</summary>
        public string? to_bin(int? value) => default!;

        /// <summary>The binary representation of the 64-bit <paramref name="value"/> (<c>to_bin</c>). A NULL argument yields NULL.</summary>
        public string? to_bin(long? value) => default!;

        /// <summary>
        /// Converts <paramref name="bytes"/> from the named encoding into the database encoding
        /// (<c>convert_from</c>). A value that is not valid in that encoding raises on the server rather
        /// than being replaced. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? convert_from(byte[]? bytes, string? encoding) => default!;

        /// <summary>
        /// Converts <paramref name="value"/> from the database encoding into the named encoding
        /// (<c>convert_to</c>), returning <c>bytea</c>. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? convert_to(string? value, string? encoding) => default!;

        /// <summary>
        /// Encodes <paramref name="bytes"/> into a textual form named by <paramref name="format"/>
        /// (<c>encode</c>): <c>base64</c>, <c>hex</c> or <c>escape</c>. A NULL argument yields NULL.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? encode(byte[]? bytes, string? format) => default!;

        /// <summary>
        /// Decodes the textual <paramref name="value"/> written in <paramref name="format"/>
        /// (<c>decode</c>: <c>base64</c>, <c>hex</c> or <c>escape</c>) back into <c>bytea</c>. Malformed
        /// input raises on the server. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? decode(string? value, string? format) => default!;

        /// <summary>
        /// The <paramref name="index"/>-th bit of the <c>bytea</c> <paramref name="bytes"/> (0 = least
        /// significant bit of the first byte), as 0 or 1 (<c>get_bit(bytea, bigint)</c>). A NULL or
        /// out-of-range argument raises on the server. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int? get_bit(byte[]? bytes, long? index) => default!;

        /// <summary>
        /// <paramref name="bytes"/> with its <paramref name="index"/>-th bit set to
        /// <paramref name="value"/> (0 or 1), returning <c>bytea</c>
        /// (<c>set_bit(bytea, bigint, integer)</c>). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? set_bit(byte[]? bytes, long? index, int? value) => default!;

        /// <summary>
        /// The <paramref name="index"/>-th byte of <paramref name="bytes"/> (0-based), as an integer
        /// (<c>get_byte(bytea, integer)</c>). A NULL or out-of-range argument raises on the server. Gated
        /// per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int? get_byte(byte[]? bytes, int? index) => default!;

        /// <summary>
        /// <paramref name="bytes"/> with its <paramref name="index"/>-th byte replaced by
        /// <paramref name="value"/>, returning <c>bytea</c> (<c>set_byte(bytea, integer, integer)</c>).
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public byte[]? set_byte(byte[]? bytes, int? index, int? value) => default!;

        /// <summary>
        /// The number of set bits in <paramref name="bytes"/> (<c>bit_count(bytea)</c>), as a
        /// <c>bigint</c>. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public long? bit_count(byte[]? bytes) => default!;

        /// <summary>
        /// <c>crc32(bytea)</c> is refused: it was introduced in PostgreSQL 18 and does not exist on the
        /// pinned 17.11 server (absent from <c>pg_proc</c>; the name fails with <c>42883</c>), so no SQL
        /// that executes on the target server can be emitted.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public int? crc32(byte[]? bytes) =>
            throw new NotSupportedException(
                "The crc32 function is refused: it was introduced in PostgreSQL 18 and does not exist on " +
                "the pinned 17.11 server (absent from pg_proc; the name fails with 42883), so no SQL that " +
                "executes on the target server can be emitted.");

        /// <summary>
        /// <c>crc32c(bytea)</c> is refused for the same reason as <see cref="crc32(byte[])"/>: it is a
        /// PostgreSQL 18 function absent from the pinned 17.11 server.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public int? crc32c(byte[]? bytes) =>
            throw new NotSupportedException(
                "The crc32c function is refused: it was introduced in PostgreSQL 18 and does not exist on " +
                "the pinned 17.11 server (absent from pg_proc; the name fails with 42883), so no SQL that " +
                "executes on the target server can be emitted.");

        /// <summary>
        /// The Unicode normalization of <paramref name="value"/> in the form named by
        /// <paramref name="form"/> (<c>normalize</c>): <c>NFC</c>, <c>NFD</c>, <c>NFKC</c> or
        /// <c>NFKD</c>. The form is a grammar keyword in PostgreSQL, not a string value, so it must be a
        /// constant and is rendered unquoted. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? normalize(string? value, string? form) => default!;

        /// <summary>
        /// Interprets backslash escapes (<c>\XXXX</c>, <c>\+XXXXXX</c>, <c>\uXXXX</c>,
        /// <c>\UXXXXXXXX</c>) and doubled backslashes in <paramref name="value"/> as Unicode code
        /// points (<c>unistr</c>). A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? unistr(string? value) => default!;

        /// <summary>
        /// Splits <paramref name="value"/> on the <paramref name="delimiter"/> string and returns one
        /// row per fragment (<c>string_to_table</c>), select
        /// <see cref="SqlFunctions.IStringToTableRow.Value"/>. Use through <c>ctx.From(() =&gt; ...)</c>.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IStringToTableRow> string_to_table(string? value, string? delimiter) => new("string_to_table");

        /// <summary>
        /// <c>string_to_table(value, delimiter, null_string)</c>: fragments equal to
        /// <paramref name="nullString"/> become NULL rows; the other fragments are unchanged. Use through
        /// <c>ctx.From(() =&gt; ...)</c>.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IStringToTableRow> string_to_table(string? value, string? delimiter, string? nullString) => new("string_to_table");

        /// <summary>
        /// <c>casefold(text)</c> is refused: it was introduced in PostgreSQL 18 and does not exist on
        /// the pinned 17.11 server (absent from <c>pg_proc</c>; the name fails with <c>42883</c>), so
        /// any SQL emitted for it would be rejected by the target server.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public string? casefold(string? value) =>
            throw new NotSupportedException(
                "The casefold function is refused: it was introduced in PostgreSQL 18 and does not exist on the pinned 17.11 server.");

        /// <summary>
        /// Builds an interval from its parts. A mapped <see cref="TimeSpan"/> result is supported only
        /// for zero-month values: the mapper rejects a returned interval with a non-zero month component
        /// (either sign) through <see cref="InvalidCastException"/> and converts the zero-month days and
        /// microseconds exactly.
        /// </summary>
        public TimeSpan? make_interval(int years, int months, int days, int hours, int minutes, double seconds) => default!;

        /// <summary>
        /// Builds a <c>time</c> from its parts (<c>make_time</c>). Use the cross-provider
        /// <see cref="CommonFunctions.date_from_parts"/> for <c>make_date</c>.
        /// </summary>
        public TimeSpan? make_time(int hour, int minute, double second) => default!;

        /// <summary>Builds a timestamp from its parts (<c>make_timestamp</c>).</summary>
        public DateTime? make_timestamp(int year, int month, int day, int hour, int minute, double second) => default!;

        /// <summary>
        /// Adjusts an interval so that 30-day units are represented as months, so the returned value can
        /// carry a month component: the mapper rejects a non-zero month component (either sign) through
        /// <see cref="InvalidCastException"/> and converts a zero-month result exactly.
        /// </summary>
        public TimeSpan? justify_days(TimeSpan? interval) => default!;

        /// <summary>
        /// Adjusts an interval so that 24-hour units are represented as days. The returned interval is
        /// subject to the same mapper rule: a non-zero month component (either sign) is rejected through
        /// <see cref="InvalidCastException"/> and a zero-month value converts exactly.
        /// </summary>
        public TimeSpan? justify_hours(TimeSpan? interval) => default!;

        /// <summary>
        /// The interval between two timestamps, subtracting <paramref name="b"/> from
        /// <paramref name="a"/> (<c>age</c>). PostgreSQL's <c>age</c> returns an <c>interval</c> that can
        /// carry whole months, which a <see cref="TimeSpan"/> cannot represent: on the mapped-column path
        /// the mapper rejects a returned interval with a non-zero month component (either sign) through
        /// <see cref="InvalidCastException"/> naming calendar months, and a month is never read back as 30
        /// days. A zero-month value converts exactly. Other materialisation paths are not covered.
        /// </summary>
        public TimeSpan? age(DateTime? a, DateTime? b) => default!;

        /// <summary>
        /// Binaries <paramref name="source"/> into buckets of <paramref name="stride"/> aligned to
        /// <paramref name="origin"/> (<c>date_bin</c>). The stride is an interval literal such as
        /// <c>"15 minutes"</c>; it is cast to <c>interval</c> in SQL.
        /// </summary>
        public DateTime? date_bin(string? stride, DateTime? source, DateTime? origin) => default!;

        /// <summary>
        /// Subtracts <paramref name="interval"/> from <paramref name="value"/> (<c>date_subtract</c> with
        /// <c>timestamp with time zone</c>). The result is a <c>timestamp with time zone</c>; a NULL
        /// argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? date_subtract(DateTime? value, TimeSpan? interval) => default!;

        /// <summary>
        /// Subtracts <paramref name="interval"/> from <paramref name="value"/> in the named
        /// <paramref name="zone"/> (<c>date_subtract(timestamp with time zone, interval, text)</c>). A
        /// NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? date_subtract(DateTime? value, TimeSpan? interval, string? zone) => default!;

        /// <summary>
        /// Adjusts an interval so that 24-hour units are represented as days and 30-day units as months
        /// (<c>justify_interval</c>). The returned interval can therefore carry a month component: an
        /// interval with a non-zero month component (either sign) is rejected through
        /// <see cref="InvalidCastException"/> when materialised as a <see cref="TimeSpan"/>, and a
        /// zero-month value converts exactly. A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public TimeSpan? justify_interval(TimeSpan? interval) => default!;

        /// <summary>
        /// Builds a <c>timestamp with time zone</c> from its parts in the session time zone
        /// (<c>make_timestamptz</c>). A NULL argument yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? make_timestamptz(int? year, int? month, int? day, int? hour, int? minute, double? second) => default!;

        /// <summary>
        /// Builds a <c>timestamp with time zone</c> from its parts in the named
        /// <paramref name="zone"/> (<c>make_timestamptz(..., timezone text)</c>). A NULL argument yields
        /// NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? make_timestamptz(int? year, int? month, int? day, int? hour, int? minute, double? second, string? zone) => default!;

        /// <summary>
        /// The actual current time at the instant of the call (<c>clock_timestamp</c>), as a
        /// <c>timestamp with time zone</c>. Unlike <c>statement_timestamp</c> and
        /// <c>transaction_timestamp</c> it advances within a statement. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? clock_timestamp() => default!;

        /// <summary>
        /// The time at which the current statement started (<c>statement_timestamp</c>), as a
        /// <c>timestamp with time zone</c>. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? statement_timestamp() => default!;

        /// <summary>
        /// The time at which the current transaction started (<c>transaction_timestamp</c>), as a
        /// <c>timestamp with time zone</c>. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? transaction_timestamp() => default!;

        /// <summary>
        /// The current date and time as text, including the time zone abbreviation
        /// (<c>timeofday</c>, for example <c>Tue Oct 27 22:21:04.335170 2026 UTC</c>). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? timeofday() => default!;

        /// <summary>
        /// True when the <c>date</c> <paramref name="value"/> is finite — that is, not
        /// <c>infinity</c>/<c>-infinity</c> (<c>isfinite(date)</c>). A NULL argument yields NULL. Gated
        /// per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? isfinite(DateOnly? value) => default!;

        /// <summary>
        /// True when the <c>timestamp</c> <paramref name="value"/> is finite — that is, not
        /// <c>infinity</c>/<c>-infinity</c> (<c>isfinite(timestamp)</c>). A NULL argument yields NULL.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? isfinite(DateTime? value) => default!;

        /// <summary>
        /// True when the <c>interval</c> <paramref name="value"/> is finite (<c>isfinite(interval)</c>).
        /// A NULL argument yields NULL. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? isfinite(TimeSpan? value) => default!;

        /// <summary>
        /// <c>pg_sleep(seconds)</c> is refused: it returns <c>void</c>, and the expression model has no
        /// void-typed projection, so no faithful result type exists. Declaring any CLR result would
        /// return a value the server never sends; the call is refused while the query is translated and
        /// no SQL is emitted.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public void pg_sleep(double? seconds) =>
            throw new NotSupportedException(
                "The pg_sleep function is refused: it returns void, and the expression model has no " +
                "void-typed projection, so no faithful CLR result exists; a fabricated value is never " +
                "returned.");

        /// <summary>
        /// <c>pg_sleep_for(interval)</c> is refused for the same reason as <see cref="pg_sleep(double?)"/>:
        /// it returns <c>void</c>, so there is no faithful expression result.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public void pg_sleep_for(TimeSpan? interval) =>
            throw new NotSupportedException(
                "The pg_sleep_for function is refused: it returns void, and the expression model has no " +
                "void-typed projection, so no faithful CLR result exists; a fabricated value is never " +
                "returned.");

        /// <summary>
        /// <c>pg_sleep_until(timestamp with time zone)</c> is refused for the same reason as
        /// <see cref="pg_sleep(double?)"/>: it returns <c>void</c>, so there is no faithful expression
        /// result.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public void pg_sleep_until(DateTime? target) =>
            throw new NotSupportedException(
                "The pg_sleep_until function is refused: it returns void, and the expression model has no " +
                "void-typed projection, so no faithful CLR result exists; a fabricated value is never " +
                "returned.");

        /// <summary>
        /// Extracts the <c>timestamp with time zone</c> embedded in a version 1 (or version 7) UUID
        /// (<c>uuid_extract_timestamp</c>); UUIDs of other versions and the nil UUID yield NULL. Gated
        /// per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? uuid_extract_timestamp(Guid? value) => default!;

        /// <summary>
        /// The version number embedded in <paramref name="value"/> (<c>uuid_extract_version</c>), for
        /// example <c>1</c> or <c>4</c>; the nil UUID yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public short? uuid_extract_version(Guid? value) => default!;

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

        /// <summary>The current date.</summary>
        public DateTime? current_date() => default!;

        /// <summary>The current time of day with time zone.</summary>
        public TimeSpan? current_time() => default!;

        /// <summary>The current time of day without time zone.</summary>
        public TimeSpan? localtime() => default!;

        /// <summary>The current timestamp without time zone.</summary>
        public DateTime? localtimestamp() => default!;

        /// <summary>The type name of <paramref name="value"/> (<c>pg_typeof</c>).</summary>
        public string? pg_typeof(object? value) => default!;

        /// <summary>
        /// The names of the schemas on the search path (<c>current_schemas(include_implicit)</c>), most
        /// significant first; with <paramref name="includeImplicit"/> true the implicit system schemas are
        /// included. The result is a <c>name[]</c>, read as a <see cref="string"/> array. Gated per name
        /// by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string?[] current_schemas(bool? includeImplicit) => default!;

        /// <summary>
        /// The time the postmaster started (<c>pg_postmaster_start_time()</c>), as
        /// <c>timestamp with time zone</c>. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? pg_postmaster_start_time() => default!;

        /// <summary>
        /// The path of the current log file for the default (stderr) log format
        /// (<c>pg_current_logfile()</c>), or NULL when the server does not collect logs. Gated per name
        /// by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_current_logfile() => default!;

        /// <summary>
        /// The path of the current log file for the log format named <paramref name="format"/>
        /// (<c>pg_current_logfile(format)</c>): <c>stderr</c>, <c>csvlog</c> or <c>jsonlog</c>; any other
        /// format raises an error and an uncollected format yields NULL. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_current_logfile(string? format) => default!;

        /// <summary>
        /// The time the configuration files were last loaded (<c>pg_conf_load_time()</c>). Gated per name
        /// by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? pg_conf_load_time() => default!;

        /// <summary>
        /// Whether JIT compilation is available in this server process (<c>pg_jit_available()</c>).
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_jit_available() => default!;

        /// <summary>
        /// The OID of this session's temporary schema, or <c>0</c> when none was created
        /// (<c>pg_my_temp_schema()</c>); the OID is read as its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_my_temp_schema() => default!;

        /// <summary>
        /// Whether the temporary schema identified by <paramref name="schemaOid"/> belongs to another
        /// session (<c>pg_is_other_temp_schema(oid)</c>). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_is_other_temp_schema(string? schemaOid) => default!;

        /// <summary>
        /// The fraction of the asynchronous notification queue currently in use, between 0 and 1
        /// (<c>pg_notification_queue_usage()</c>). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public double? pg_notification_queue_usage() => default!;

        /// <summary>
        /// The process IDs of the sessions holding a lock that blocks the session
        /// <paramref name="pid"/> (<c>pg_blocking_pids(pid)</c>); an empty array when nothing blocks it.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int[]? pg_blocking_pids(int? pid) => default!;

        /// <summary>
        /// The process IDs of the sessions holding a safe-snapshot conflict that blocks the session
        /// <paramref name="pid"/> (<c>pg_safe_snapshot_blocking_pids(pid)</c>). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int[]? pg_safe_snapshot_blocking_pids(int? pid) => default!;

        /// <summary>
        /// The current nesting depth of trigger execution (<c>pg_trigger_depth()</c>), zero outside a
        /// trigger. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int? pg_trigger_depth() => default!;

        /// <summary>
        /// Whether the collation with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_collation_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_collation_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the encoding conversion with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_conversion_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_conversion_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the function or procedure with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_function_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_function_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the operator class with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_opclass_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_opclass_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the operator with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_operator_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_operator_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the operator family with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_opfamily_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_opfamily_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the extended statistics object with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_statistics_obj_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_statistics_obj_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the table, view or other relation with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_table_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_table_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the text-search configuration with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_ts_config_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_ts_config_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the text-search dictionary with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_ts_dict_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_ts_dict_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the text-search parser with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_ts_parser_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_ts_parser_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the text-search template with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_ts_template_is_visible(oid)</c>); NULL when the OID names no such object. The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_ts_template_is_visible(string? oid) => default!;

        /// <summary>
        /// Whether the data type with the given <paramref name="oid"/> is visible in the current search
        /// path (<c>pg_type_is_visible(oid)</c>); NULL when the OID names no type. The OID is passed in
        /// its text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_type_is_visible(string? oid) => default!;

        /// <summary>
        /// The numeric code of the encoding called <paramref name="name"/>
        /// (<c>pg_char_to_encoding(name)</c>), or <c>-1</c> when the name is not a known encoding.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public int? pg_char_to_encoding(string? name) => default!;

        /// <summary>
        /// The canonical name of the encoding with the given <paramref name="code"/>
        /// (<c>pg_encoding_to_char(code)</c>); code <c>0</c> is <c>SQL_ASCII</c>, and an unknown code
        /// yields the empty string. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_encoding_to_char(int? code) => default!;

        /// <summary>
        /// The base type of <paramref name="type"/> (<c>pg_basetype(regtype)</c>) in its text form, e.g.
        /// <c>integer</c> for <c>integer</c> and <c>integer[]</c> for an integer array; an unknown type
        /// name raises a server error. The <c>regtype</c> result is cast to text because the driver
        /// cannot read a <c>regtype</c>. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_basetype(string? type) => default!;

        /// <summary>
        /// A human-readable description of a catalog object
        /// (<c>pg_describe_object(classid, objid, objsubid)</c>), e.g. <c>table pg_class</c>; NULL when
        /// the OIDs name no object. Both OIDs are passed in their text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_describe_object(string? classOid, string? objectOid, int? objectSubId) => default!;

        /// <summary>
        /// The file-system location of the tablespace with the given <paramref name="oid"/>
        /// (<c>pg_tablespace_location(oid)</c>); the empty string for the default tablespace. The OID is
        /// passed in its text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_tablespace_location(string? oid) => default!;

        /// <summary>
        /// The flags of the run-time setting <paramref name="setting"/>
        /// (<c>pg_settings_get_flags(setting)</c>), e.g. <c>EXPLAIN</c>; an empty array when it has none
        /// and NULL for an unknown setting. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string?[] pg_settings_get_flags(string? setting) => default!;

        /// <summary>
        /// Whether the index has the named property (<c>pg_index_has_property(index, property)</c>), e.g.
        /// <c>clusterable</c>; NULL when the property is not recognised. The index is a <c>regclass</c>
        /// name or a numeric OID in text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_index_has_property(string? index, string? property) => default!;

        /// <summary>
        /// Whether the index access method has the named property
        /// (<c>pg_indexam_has_property(amoid, property)</c>), e.g. <c>can_order</c>; NULL when the
        /// property is not recognised. The access-method OID is passed in its text form. Gated per name
        /// by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_indexam_has_property(string? accessMethodOid, string? property) => default!;

        /// <summary>
        /// Whether the index column has the named property
        /// (<c>pg_index_column_has_property(index, column, property)</c>), e.g. <c>orderable</c>; NULL
        /// when the property is not recognised. The index is a <c>regclass</c> name or a numeric OID in
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_index_column_has_property(string? index, int? columnNumber, string? property) => default!;

        /// <summary>
        /// Whether <paramref name="value"/> is valid input for the type <paramref name="typeName"/>
        /// (<c>pg_input_is_valid(value, type)</c>), without raising an error; an unknown type name is
        /// still an error. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_input_is_valid(string? value, string? typeName) => default!;

        /// <summary>
        /// The sequence owned by a serial or identity column
        /// (<c>pg_get_serial_sequence(table, column)</c>), or NULL when the column has none; an unknown
        /// column is an error. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_serial_sequence(string? table, string? column) => default!;

        /// <summary>
        /// The definition of the constraint with the given <paramref name="constraintOid"/>
        /// (<c>pg_get_constraintdef(oid)</c>). Refused together with <see cref="pg_get_expr(object?, string?)"/>:
        /// both render a stored node tree as SQL text, and the node-tree argument of <c>pg_get_expr</c>
        /// cannot be called at all, because PostgreSQL requires a <c>pg_node_tree</c> argument and a value
        /// of that type cannot be produced from CLR (<c>cannot accept a value of type pg_node_tree</c>).
        /// Read the constraint text from <c>pg_constraint.conbin</c> through a raw command instead.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public string? pg_get_constraintdef(string? constraintOid) =>
            throw new NotSupportedException(GetConstraintdefRefusal);

        /// <summary>
        /// The definition of the constraint with the given <paramref name="constraintOid"/>, pretty-printed
        /// when <paramref name="pretty"/> is true (<c>pg_get_constraintdef(oid, boolean)</c>). Refused with
        /// <see cref="pg_get_constraintdef(string?)"/>.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public string? pg_get_constraintdef(string? constraintOid, bool? pretty) =>
            throw new NotSupportedException(GetConstraintdefRefusal);

        /// <summary>
        /// The expression stored in a <c>pg_node_tree</c>
        /// (<c>pg_get_expr(pg_node_tree, oid)</c>). Refused: PostgreSQL requires a <c>pg_node_tree</c>
        /// argument and a value of that type cannot be produced from CLR — the pinned 17.11 server answers
        /// <c>cannot accept a value of type pg_node_tree</c>. There is no CLR type that can carry the
        /// stored tree, so no form of this function can ever be prepared.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public string? pg_get_expr(object? nodeTree, string? relationOid) =>
            throw new NotSupportedException(GetExprRefusal);

        /// <summary>
        /// The expression stored in a <c>pg_node_tree</c>, pretty-printed when
        /// <paramref name="pretty"/> is true (<c>pg_get_expr(pg_node_tree, oid, boolean)</c>). Refused with
        /// <see cref="pg_get_expr(object?, string?)"/>.
        /// </summary>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public string? pg_get_expr(object? nodeTree, string? relationOid, bool? pretty) =>
            throw new NotSupportedException(GetExprRefusal);

        /// <summary>
        /// The argument list of the function with the given <paramref name="functionOid"/>
        /// (<c>pg_get_function_arguments(oid)</c>), with argument names and defaults; the empty string for a
        /// function without arguments. The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_function_arguments(string? functionOid) => default!;

        /// <summary>
        /// The identity argument list of the function with the given <paramref name="functionOid"/>
        /// (<c>pg_get_function_identity_arguments(oid)</c>), without names and defaults; the form used to
        /// identify the function in <c>ALTER</c>/<c>DROP</c>. The OID is passed in its text form. Gated per
        /// name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_function_identity_arguments(string? functionOid) => default!;

        /// <summary>
        /// The result type of the function with the given <paramref name="functionOid"/>
        /// (<c>pg_get_function_result(oid)</c>), e.g. <c>integer</c> or <c>TABLE(...)</c>. The OID is passed
        /// in its text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_function_result(string? functionOid) => default!;

        /// <summary>
        /// The complete <c>CREATE OR REPLACE FUNCTION</c> statement of the function with the given
        /// <paramref name="functionOid"/> (<c>pg_get_functiondef(oid)</c>); an aggregate or a window
        /// function is an error. The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_functiondef(string? functionOid) => default!;

        /// <summary>
        /// The <c>CREATE INDEX</c> statement of the index with the given <paramref name="indexOid"/>
        /// (<c>pg_get_indexdef(oid)</c>). The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_indexdef(string? indexOid) => default!;

        /// <summary>
        /// The definition of one index column of the index with the given <paramref name="indexOid"/>
        /// (<c>pg_get_indexdef(oid, integer, boolean)</c>); an out-of-range column is an error. The OID is
        /// passed in its text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_indexdef(string? indexOid, int? columnNumber, bool? pretty) => default!;

        /// <summary>
        /// The <c>FOR VALUES</c> clause of the partition bound of the relation with the given
        /// <paramref name="relationOid"/> (<c>pg_get_partition_constraintdef(oid)</c>); an error when the
        /// relation is not a partition. The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_partition_constraintdef(string? relationOid) => default!;

        /// <summary>
        /// The partition key of the partitioned table with the given <paramref name="relationOid"/>
        /// (<c>pg_get_partkeydef(oid)</c>), e.g. <c>RANGE (id)</c>; an error when the relation is not
        /// partitioned. The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_partkeydef(string? relationOid) => default!;

        /// <summary>
        /// The <c>CREATE RULE</c> statement of the rule with the given <paramref name="ruleOid"/>
        /// (<c>pg_get_ruledef(oid)</c>). The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_ruledef(string? ruleOid) => default!;

        /// <summary>
        /// The <c>CREATE RULE</c> statement of the rule with the given <paramref name="ruleOid"/>,
        /// pretty-printed when <paramref name="pretty"/> is true (<c>pg_get_ruledef(oid, boolean)</c>). The
        /// OID is passed in its text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_ruledef(string? ruleOid, bool? pretty) => default!;

        /// <summary>
        /// The <c>CREATE STATISTICS</c> statement of the extended statistics object with the given
        /// <paramref name="statisticsOid"/> (<c>pg_get_statisticsobjdef(oid)</c>). The OID is passed in its
        /// text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_statisticsobjdef(string? statisticsOid) => default!;

        /// <summary>
        /// The <c>CREATE TRIGGER</c> statement of the trigger with the given <paramref name="triggerOid"/>
        /// (<c>pg_get_triggerdef(oid)</c>). The OID is passed in its text form. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_triggerdef(string? triggerOid) => default!;

        /// <summary>
        /// The <c>CREATE TRIGGER</c> statement of the trigger with the given <paramref name="triggerOid"/>,
        /// pretty-printed when <paramref name="pretty"/> is true (<c>pg_get_triggerdef(oid, boolean)</c>).
        /// The OID is passed in its text form. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_triggerdef(string? triggerOid, bool? pretty) => default!;

        /// <summary>
        /// The name of the role with the given <paramref name="roleOid"/> (<c>pg_get_userbyid(oid)</c>), or
        /// <c>unknown (OID=N)</c> when no role has that OID. The OID is passed in its text form. Gated per
        /// name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_userbyid(string? roleOid) => default!;

        /// <summary>
        /// The reconstructed <c>SELECT</c> of the view named <paramref name="view"/>
        /// (<c>pg_get_viewdef(text)</c>), where the argument is a view name or a numeric OID in text form
        /// (rendered through a <c>to_regclass</c> cast; an unknown name yields NULL). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_viewdef(string? view) => default!;

        /// <summary>
        /// The reconstructed <c>SELECT</c> of the view named <paramref name="view"/>, pretty-printed when
        /// <paramref name="pretty"/> is true (<c>pg_get_viewdef(text, boolean)</c>); the name or OID goes
        /// through a <c>to_regclass</c> cast. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_viewdef(string? view, bool? pretty) => default!;

        /// <summary>
        /// The reconstructed <c>SELECT</c> of the view with the given <paramref name="viewOid"/>
        /// (<c>pg_get_viewdef(oid, integer)</c>), pretty-printed when <paramref name="pretty"/> is true.
        /// The OID is passed in its text form (<c>to_regclass</c>). Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_get_viewdef(string? viewOid, int? pretty) => default!;

        /// <summary>
        /// Whether the current user has the privilege <paramref name="privilege"/> on the role
        /// <paramref name="role"/> (<c>pg_has_role(name, text)</c>); the role may also be given as a numeric
        /// OID in text form (<c>pg_has_role(oid, text)</c>), since it is rendered through a <c>regrole</c>
        /// cast. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_has_role(string? role, string? privilege) => default!;

        /// <summary>
        /// Whether the current user has <paramref name="privilege"/> on <paramref name="role"/>, with
        /// <paramref name="membership"/> naming the membership to check
        /// (<c>pg_has_role(name, name, text)</c>); either role may be a numeric OID in text form, since both
        /// are rendered through a <c>regrole</c> cast. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_has_role(string? role, string? membership, string? privilege) => default!;

        /// <summary>
        /// The current transaction snapshot (<c>pg_current_snapshot()</c>) in its textual form, e.g.
        /// <c>100:104:100,102</c>; read as text because the driver cannot read a <c>pg_snapshot</c>. The
        /// value can be passed back to <c>pg_snapshot_xmax</c>, <c>pg_snapshot_xmin</c> and
        /// <c>pg_visible_in_snapshot</c>, which accept this textual form.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_current_snapshot() => default!;

        /// <summary>
        /// The current transaction id (<c>pg_current_xact_id()</c>) in its textual form; read as text
        /// because the driver cannot read an <c>xid8</c>. A write transaction is assigned one lazily, so
        /// this form assigns one. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_current_xact_id() => default!;

        /// <summary>
        /// The transaction id already assigned to the current transaction, or NULL when none is
        /// (<c>pg_current_xact_id_if_assigned()</c>), in its textual form; read as text because the driver
        /// cannot read an <c>xid8</c>. Unlike <see cref="pg_current_xact_id()"/> it assigns nothing. Gated
        /// per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_current_xact_id_if_assigned() => default!;

        /// <summary>
        /// The highest transaction id of the snapshot <paramref name="snapshot"/>
        /// (<c>pg_snapshot_xmax(pg_snapshot)</c>) in its textual form; the snapshot is the text produced by
        /// <see cref="pg_current_snapshot()"/>. The <c>xid8</c> result is read as text because the driver
        /// cannot read it. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_snapshot_xmax(string? snapshot) => default!;

        /// <summary>
        /// The lowest transaction id still in progress in the snapshot <paramref name="snapshot"/>
        /// (<c>pg_snapshot_xmin(pg_snapshot)</c>) in its textual form; the <c>xid8</c> result is read as
        /// text because the driver cannot read it. Gated per name by
        /// <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_snapshot_xmin(string? snapshot) => default!;

        /// <summary>
        /// Whether the transaction <paramref name="xid"/> is visible in <paramref name="snapshot"/>
        /// (<c>pg_visible_in_snapshot(xid8, pg_snapshot)</c>); both arguments are the textual forms.
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public bool? pg_visible_in_snapshot(string? xid, string? snapshot) => default!;

        /// <summary>
        /// The commit time of the transaction <paramref name="xid"/> (<c>pg_xact_commit_timestamp(xid)</c>).
        /// The time exists only for a transaction that committed while <c>track_commit_timestamp</c> was
        /// enabled (a restart-only setting); otherwise the server raises SQLSTATE 55000
        /// (<c>could not get commit timestamp data</c>). The <c>xid</c> argument is passed in its numeric
        /// text form (the driver cannot bind an <c>xid</c>, and PostgreSQL has no integer-to-xid cast).
        /// Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public DateTime? pg_xact_commit_timestamp(string? xid) => default!;

        /// <summary>
        /// The status of the transaction <paramref name="xid"/>
        /// (<c>pg_xact_status(xid8)</c>): <c>in progress</c>, <c>committed</c>, <c>aborted</c> or
        /// <c>subcommitted</c>. Gated per name by <see cref="IScalarFunctions.Supports"/>.
        /// </summary>
        public string? pg_xact_status(string? xid) => default!;

        private const string GetConstraintdefRefusal =
            "The pg_get_constraintdef function is refused: it renders a stored constraint node tree as " +
            "SQL text, and its pg_get_expr sibling cannot be called at all because PostgreSQL requires a " +
            "pg_node_tree argument and a value of that type cannot be produced from CLR (cannot accept a " +
            "value of type pg_node_tree); the pair is refused rather than half-exposed.";

        private const string GetExprRefusal =
            "The pg_get_expr function is refused: PostgreSQL requires a pg_node_tree argument and a value " +
            "of that type cannot be produced from CLR; the pinned 17.11 server answers \"cannot accept a " +
            "value of type pg_node_tree\".";

        /// <summary>The current value of the run-time setting <paramref name="name"/> (<c>current_setting</c>).</summary>
        public string? current_setting(string? name) => default!;

        /// <summary>
        /// The current value of the run-time setting <paramref name="name"/>, or null when it does not
        /// exist if <paramref name="missingOk"/> is true (<c>current_setting(name, missing_ok)</c>).
        /// </summary>
        public string? current_setting(string? name, bool missingOk) => default!;

        /// <summary>Sets the run-time setting <paramref name="name"/> to <paramref name="value"/> (<c>set_config</c>).</summary>
        public string? set_config(string? name, string? value, bool isLocal) => default!;

        /// <summary>The number of null arguments (<c>num_nulls</c>).</summary>
        public int num_nulls(params object?[] values) => default!;

        /// <summary>The number of non-null arguments (<c>num_nonnulls</c>).</summary>
        public int num_nonnulls(params object?[] values) => default!;

        /// <summary>
        /// Converts <paramref name="document"/> to a <c>tsvector</c> (<c>to_tsvector</c>). Part of the
        /// PostgreSQL native text-search surface (see <see cref="ISqlDialect.SupportsTextSearchFunctions"/>).
        /// Both <c>tsvector</c> and <c>tsquery</c> are represented as <see cref="string"/> on the CLR side.
        /// </summary>
        /// <param name="document">The text to convert.</param>
        public string? to_tsvector(string document) => default!;

        /// <summary>
        /// Parses <paramref name="query"/> into a <c>tsquery</c> (<c>to_tsquery</c>); the terms must
        /// already be joined by the <c>&amp;</c>/<c>|</c>/<c>!</c> operators.
        /// </summary>
        /// <param name="query">The query text, with explicit operators.</param>
        public string? to_tsquery(string query) => default!;

        /// <summary>Parses <paramref name="query"/> into a <c>tsquery</c>, treating it as plain text (<c>plainto_tsquery</c>).</summary>
        /// <param name="query">The plain-text query.</param>
        public string? plainto_tsquery(string query) => default!;

        /// <summary>Parses <paramref name="query"/> into a <c>tsquery</c>, keeping phrase order (<c>phraseto_tsquery</c>).</summary>
        /// <param name="query">The phrase query.</param>
        public string? phraseto_tsquery(string query) => default!;

        /// <summary>Parses <paramref name="query"/> into a <c>tsquery</c> using web-search syntax (<c>websearch_to_tsquery</c>).</summary>
        /// <param name="query">The web-search query.</param>
        public string? websearch_to_tsquery(string query) => default!;

        /// <summary>The <c>@@</c> match operator: true when <paramref name="tsvector"/> matches <paramref name="tsquery"/>.</summary>
        /// <param name="tsvector">The <c>tsvector</c> to match.</param>
        /// <param name="tsquery">The <c>tsquery</c> to match against.</param>
        public bool ts_match(string? tsvector, string? tsquery) => default!;

        /// <summary>Relevance score of a <c>tsvector</c> against a <c>tsquery</c> (<c>ts_rank</c>).</summary>
        /// <param name="tsvector">The <c>tsvector</c> to rank.</param>
        /// <param name="tsquery">The <c>tsquery</c> to rank against.</param>
        public double? ts_rank(string? tsvector, string? tsquery) => default!;

        /// <summary>Cover-density relevance score of a <c>tsvector</c> against a <c>tsquery</c> (<c>ts_rank_cd</c>).</summary>
        /// <param name="tsvector">The <c>tsvector</c> to rank.</param>
        /// <param name="tsquery">The <c>tsquery</c> to rank against.</param>
        public double? ts_rank_cd(string? tsvector, string? tsquery) => default!;

        /// <summary>A highlighted excerpt of <paramref name="document"/> for <paramref name="tsquery"/> (<c>ts_headline</c>).</summary>
        /// <param name="document">The source document.</param>
        /// <param name="tsquery">The <c>tsquery</c> whose matches are highlighted.</param>
        public string? ts_headline(string document, string? tsquery) => default!;

        /// <summary>
        /// Builds a <c>tsvector</c> from <paramref name="words"/> (<c>array_to_tsvector</c>). The result is
        /// a <c>tsvector</c>, which the driver reads as text. PostgreSQL refuses an array holding a NULL,
        /// so every element must be a non-NULL lexeme; positions are not assigned.
        /// </summary>
        /// <param name="words">The lexemes; an empty array yields an empty <c>tsvector</c>.</param>
        public string? array_to_tsvector(string?[] words) => default!;

        /// <summary>
        /// Builds a <c>tsvector</c> from the string and numeric values of the <c>json</c> document that the
        /// <paramref name="filter"/> selects (<c>json_to_tsvector</c>). The filter is a <c>jsonb</c> array
        /// of the strings <c>"string"</c>, <c>"numeric"</c>, <c>"boolean"</c>, <c>"key"</c> and
        /// <c>"all"</c>.
        /// </summary>
        /// <param name="json">The JSON document, as text.</param>
        /// <param name="filter">The <c>jsonb</c> filter array, as text.</param>
        public string? json_to_tsvector(string? json, string? filter) => default!;

        /// <summary>
        /// Builds a <c>tsvector</c> from a <c>json</c> document with an explicit text-search
        /// <paramref name="config"/> (for example <c>english</c>).
        /// </summary>
        /// <param name="config">The text-search configuration name (<c>regconfig</c>).</param>
        /// <param name="json">The JSON document, as text.</param>
        /// <param name="filter">The <c>jsonb</c> filter array, as text.</param>
        public string? json_to_tsvector(string? config, string? json, string? filter) => default!;

        /// <summary>Builds a <c>tsvector</c> from a <c>jsonb</c> document (<c>jsonb_to_tsvector</c>).</summary>
        /// <param name="json">The JSON document, as text.</param>
        /// <param name="filter">The <c>jsonb</c> filter array, as text.</param>
        public string? jsonb_to_tsvector(string? json, string? filter) => default!;

        /// <summary>Builds a <c>tsvector</c> from a <c>jsonb</c> document with an explicit text-search config.</summary>
        /// <param name="config">The text-search configuration name (<c>regconfig</c>).</param>
        /// <param name="json">The JSON document, as text.</param>
        /// <param name="filter">The <c>jsonb</c> filter array, as text.</param>
        public string? jsonb_to_tsvector(string? config, string? json, string? filter) => default!;

        /// <summary>
        /// The text-search configuration the current session resolves by default
        /// (<c>get_current_ts_config</c>), e.g. <c>english</c>. The server returns a <c>regconfig</c>,
        /// which the driver reads as text.
        /// </summary>
        public string? get_current_ts_config() => default!;

        /// <summary>
        /// The number of lexemes and operators in <paramref name="tsquery"/> (<c>numnode</c>).
        /// </summary>
        /// <param name="tsquery">The <c>tsquery</c> text, for example <c>a &amp; b</c>.</param>
        public int? numnode(string? tsquery) => default!;

        /// <summary>
        /// The query tree of <paramref name="tsquery"/> as text (<c>querytree</c>), with the operators and
        /// the grouping the parser produced, for example <c>'b' &amp; 'a'</c>.
        /// </summary>
        /// <param name="tsquery">The <c>tsquery</c> text.</param>
        public string? querytree(string? tsquery) => default!;

        /// <summary>
        /// Assigns the weight <paramref name="weight"/> to every lexeme of <paramref name="tsvector"/>
        /// (<c>setweight</c>). The result is a <c>tsvector</c>, which the driver reads as text.
        /// </summary>
        /// <param name="tsvector">The <c>tsvector</c> text.</param>
        /// <param name="weight">One of <c>A</c>, <c>B</c>, <c>C</c> or <c>D</c>; the server rejects anything else.</param>
        public string? setweight(string? tsvector, string? weight) => default!;

        /// <summary>
        /// Assigns the weight <paramref name="weight"/> only to the lexemes listed in
        /// <paramref name="lexemes"/> (<c>setweight</c>); the remaining lexemes keep their weights.
        /// </summary>
        /// <param name="tsvector">The <c>tsvector</c> text.</param>
        /// <param name="weight">One of <c>A</c>, <c>B</c>, <c>C</c> or <c>D</c>.</param>
        /// <param name="lexemes">The lexemes whose weight changes; a NULL element, an empty array and a
        /// lexeme absent from <paramref name="tsvector"/> all leave the vector unchanged (measured on 17.11).</param>
        public string? setweight(string? tsvector, string? weight, string?[] lexemes) => default!;

        /// <summary>
        /// Removes the positions and weights of <paramref name="tsvector"/>, leaving only the lexemes
        /// (<c>strip</c>).
        /// </summary>
        /// <param name="tsvector">The <c>tsvector</c> text.</param>
        public string? strip(string? tsvector) => default!;

        /// <summary>
        /// Removes <paramref name="lexeme"/> from <paramref name="tsvector"/>, NULLing the positions it
        /// occupied (<c>ts_delete</c>). A missing lexeme leaves the vector unchanged.
        /// </summary>
        /// <param name="tsvector">The <c>tsvector</c> text.</param>
        /// <param name="lexeme">The lexeme to delete.</param>
        public string? ts_delete(string? tsvector, string? lexeme) => default!;

        /// <summary>
        /// Removes every lexeme listed in <paramref name="lexemes"/> from <paramref name="tsvector"/>
        /// (<c>ts_delete</c>). A lexeme that does not match is ignored; a NULL element or an empty array
        /// leaves the vector unchanged, and a SQL-NULL array yields NULL (measured on 17.11).
        /// </summary>
        /// <param name="tsvector">The <c>tsvector</c> text.</param>
        /// <param name="lexemes">The lexemes to delete.</param>
        public string? ts_delete(string? tsvector, string?[] lexemes) => default!;

        /// <summary>
        /// Keeps only the lexemes of <paramref name="tsvector"/> whose weight is one of
        /// <paramref name="weights"/> (<c>ts_filter</c>).
        /// </summary>
        /// <param name="tsvector">The <c>tsvector</c> text.</param>
        /// <param name="weights">The accepted weights, each one of <c>A</c>, <c>B</c>, <c>C</c> or <c>D</c>.</param>
        public string? ts_filter(string? tsvector, string?[] weights) => default!;

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
        /// the given fraction. Requires a provider that supports ordered-set aggregates. This is the
        /// ordered-set aggregate; <paramref name="orderBy"/> must be a lambda. The window form is the
        /// separate <c>CommonFunctions.percentile_cont</c>.
        /// </summary>
        public T? percentile_cont<T>(double fraction, Expression<Func<T>> orderBy) => default!;

        /// <summary><c>percentile_disc(fraction) within group (order by key)</c>: the first value whose position reaches the fraction (ordered-set aggregate; the window form is <c>CommonFunctions.percentile_disc</c>).</summary>
        public T? percentile_disc<T>(double fraction, Expression<Func<T>> orderBy) => default!;

        /// <summary>
        /// <c>percentile_cont(0.5) within group (order by key)</c>: the median as the native ordered-set
        /// aggregate (PostgreSQL has no <c>median</c> function of its own). Requires the ordered-set
        /// percentile capability (see <see cref="ISqlDialect.SupportsOrderedPercentiles"/>; PostgreSQL) and
        /// a floating-point <paramref name="orderBy"/> expression, because the continuous percentile
        /// interpolates and returns <c>double precision</c>. This is an aggregate: it produces one row per
        /// group, not one value per input row.
        /// </summary>
        /// <typeparam name="T">The sort expression's type; <see cref="double"/> or <see cref="float"/>.</typeparam>
        /// <param name="orderBy">The ordering key, written as a lambda closing over the query parameter.</param>
        /// <returns>The interpolated median, or SQL NULL when the group has no non-null values.</returns>
        public double? median<T>(Expression<Func<T>> orderBy) => default!;

        /// <summary>
        /// <c>percentile</c>: not supported by PostgreSQL. Its native spellings are the typed
        /// <see cref="percentile_cont{T}(double, Expression{Func{T}})"/> and
        /// <see cref="percentile_disc{T}(double, Expression{Func{T}})"/> ordered-set aggregates, which
        /// differ in whether the result is interpolated or an input value; a generic alias would hide that
        /// difference. Referencing this member throws <see cref="NotSupportedException"/> at translation time.
        /// </summary>
        /// <typeparam name="T">The sort expression's type.</typeparam>
        /// <param name="fraction">The percentile fraction, in <c>[0, 1]</c>.</param>
        /// <param name="orderBy">The ordering key, written as a lambda closing over the query parameter.</param>
        /// <returns>Never returns; the call is refused when the query is translated.</returns>
        public double? percentile<T>(double fraction, Expression<Func<T>> orderBy) => default!;

        /// <summary><c>mode() within group (order by key)</c>: the most frequent value.</summary>
        public T? mode<T>(Expression<Func<T>> orderBy) => default!;

        /// <summary>Advances <paramref name="sequence"/> and returns its next value (<c>nextval</c>).</summary>
        public long? nextval(string? sequence) => default!;

        /// <summary>Sets the current value of <paramref name="sequence"/> (<c>setval</c>).</summary>
        public long? setval(string? sequence, long value) => default!;

        /// <summary>The current value of <paramref name="sequence"/> for this session (<c>currval</c>).</summary>
        public long? currval(string? sequence) => default!;

        /// <summary>The value most recently returned by <c>nextval</c> in this session (<c>lastval</c>).</summary>
        public long? lastval() => default!;

        /// <summary>
        /// <c>generate_series(start, stop)</c> as a FROM source; select
        /// <see cref="SqlFunctions.IGenerateSeriesRow.Value"/>. Use through
        /// <c>ctx.From(() =&gt; ...)</c>.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IGenerateSeriesRow> generate_series(long start, long stop) => new("generate_series");

        /// <summary><c>generate_series(start, stop, step)</c> as a FROM source.</summary>
        public TvfEntityBuilder<SqlFunctions.IGenerateSeriesRow> generate_series(long start, long stop, long step) => new("generate_series");

        /// <summary>
        /// <c>unnest(array)</c> as a FROM source; select <see cref="SqlFunctions.IUnnestRow{T}.Value"/>. The
        /// array is normally supplied through <c>SqlFunctions.Parameter&lt;T[]&gt;(idx)</c>.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IUnnestRow<T>> unnest<T>(T[] array) => new("unnest");

        /// <summary>
        /// <c>unnest(array1, array2)</c> as a FROM source: one row per position of the longest array, with
        /// <see cref="SqlFunctions.IUnnestMultiRow{T1,T2}.Value1"/> and
        /// <see cref="SqlFunctions.IUnnestMultiRow{T1,T2}.Value2"/> taking the elements of the two arrays and
        /// the shorter array padded with <see langword="null"/>. The multi-array form is FROM-clause
        /// grammar, not a <c>pg_proc</c> function: PostgreSQL rejects a column definition list on it and
        /// points at <c>ROWS FROM</c>, which the provider renders. The admitted arities are one, two and
        /// three arrays — there is no variadic claim.
        /// </summary>
        /// <typeparam name="T1">The element type of the first array.</typeparam>
        /// <typeparam name="T2">The element type of the second array.</typeparam>
        /// <param name="first">The first array.</param>
        /// <param name="second">The second array; the shorter array is padded with <see langword="null"/>.</param>
        public TvfEntityBuilder<SqlFunctions.IUnnestMultiRow<T1, T2>> unnest<T1, T2>(T1[] first, T2[] second) => new("unnest");

        /// <summary>
        /// <c>unnest(array1, array2, array3)</c> as a FROM source: the three admitted array positions, all
        /// padded with <see langword="null"/> to the longest array (see
        /// <see cref="SqlFunctions.IUnnestMultiRow{T1,T2,T3}"/>).
        /// </summary>
        /// <typeparam name="T1">The element type of the first array.</typeparam>
        /// <typeparam name="T2">The element type of the second array.</typeparam>
        /// <typeparam name="T3">The element type of the third array.</typeparam>
        /// <param name="first">The first array.</param>
        /// <param name="second">The second array.</param>
        /// <param name="third">The third array.</param>
        public TvfEntityBuilder<SqlFunctions.IUnnestMultiRow<T1, T2, T3>> unnest<T1, T2, T3>(T1[] first, T2[] second, T3[] third) => new("unnest");

        /// <summary>
        /// <c>unnest(four or more arrays)</c> is refused: PostgreSQL's multi-array <c>unnest</c> is
        /// FROM-clause grammar with no variadic arity, and this library admits exactly one, two and three
        /// arrays as typed overloads. Claiming an arbitrary arity through one overload would misrepresent
        /// the grammar, so the call fails while the source is composed and no SQL is emitted.
        /// </summary>
        /// <typeparam name="TRow">The row shape the caller hoped to project; never produced.</typeparam>
        /// <param name="first">The first array.</param>
        /// <param name="second">The second array.</param>
        /// <param name="third">The third array.</param>
        /// <param name="fourth">The fourth array, which exceeds the admitted arities.</param>
        /// <returns>Never returns; the call is refused when the source is composed.</returns>
        public TvfEntityBuilder<TRow> unnest<TRow>(object? first, object? second, object? third, object? fourth) =>
            throw new NotSupportedException(
                "The unnest function is refused for four or more arrays: multi-array unnest is FROM-clause " +
                "grammar with no variadic arity, and this library admits exactly one, two and three arrays " +
                "as typed overloads.");

        /// <summary>
        /// <c>unnest(array) WITH ORDINALITY</c> as a FROM source: the element and its one-based position
        /// (see <see cref="SqlFunctions.IUnnestOrdinalityRow{T}"/>). <c>WITH ORDINALITY</c> is FROM-clause
        /// grammar, and the ordinality column is <c>bigint</c>, so it is <see cref="long"/> here.
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="array">The array to expand.</param>
        public TvfEntityBuilder<SqlFunctions.IUnnestOrdinalityRow<T>> unnest_with_ordinality<T>(T[] array) => new("unnest_with_ordinality");

        /// <summary>
        /// <c>unnest(array1, array2) WITH ORDINALITY</c> as a FROM source: both admitted array positions
        /// plus the shared one-based position; the shorter array is padded with <see langword="null"/> (see
        /// <see cref="SqlFunctions.IUnnestOrdinalityMultiRow{T1,T2}"/>).
        /// </summary>
        /// <typeparam name="T1">The element type of the first array.</typeparam>
        /// <typeparam name="T2">The element type of the second array.</typeparam>
        /// <param name="first">The first array.</param>
        /// <param name="second">The second array; the shorter array is padded with <see langword="null"/>.</param>
        public TvfEntityBuilder<SqlFunctions.IUnnestOrdinalityMultiRow<T1, T2>> unnest_with_ordinality<T1, T2>(T1[] first, T2[] second) => new("unnest_with_ordinality");

        /// <summary>
        /// <c>generate_subscripts(array, dimension)</c> as a FROM source: one row per valid subscript of
        /// the requested dimension (see <see cref="SqlFunctions.IGenerateSubscriptsRow"/>). PostgreSQL
        /// numbers dimensions from one, and an empty or SQL-NULL array yields no rows.
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="array">The array whose subscripts are generated.</param>
        /// <param name="dimension">The one-based array dimension.</param>
        public TvfEntityBuilder<SqlFunctions.IGenerateSubscriptsRow> generate_subscripts<T>(T[] array, int? dimension) => new("generate_subscripts");

        /// <summary>
        /// <c>generate_subscripts(array, dimension, reverse)</c> as a FROM source: the same subscripts in
        /// descending order when <paramref name="reverse"/> is <see langword="true"/>.
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="array">The array whose subscripts are generated.</param>
        /// <param name="dimension">The one-based array dimension.</param>
        /// <param name="reverse">Whether to generate the subscripts in descending order; <see langword="null"/> is not reversed.</param>
        public TvfEntityBuilder<SqlFunctions.IGenerateSubscriptsRow> generate_subscripts<T>(T[] array, int? dimension, bool? reverse) => new("generate_subscripts");

        /// <summary>
        /// <c>regexp_matches(source, pattern)</c> as a FROM source: one row per match, the
        /// <c>regexp_matches</c> column is the <c>text[]</c> array of captured groups (see
        /// <see cref="SqlFunctions.IRegexpMatchesRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IRegexpMatchesRow> regexp_matches(string? source, string? pattern) => new("regexp_matches");

        /// <summary><c>regexp_matches(source, pattern, flags)</c> as a FROM source, with the <c>g</c>/<c>i</c>/... flags.</summary>
        public TvfEntityBuilder<SqlFunctions.IRegexpMatchesRow> regexp_matches(string? source, string? pattern, string? flags) => new("regexp_matches");

        /// <summary>
        /// <c>regexp_split_to_table(source, pattern)</c> as a FROM source: one row per fragment (see
        /// <see cref="SqlFunctions.IRegexpSplitToTableRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IRegexpSplitToTableRow> regexp_split_to_table(string? source, string? pattern) => new("regexp_split_to_table");

        /// <summary><c>regexp_split_to_table(source, pattern, flags)</c> as a FROM source.</summary>
        public TvfEntityBuilder<SqlFunctions.IRegexpSplitToTableRow> regexp_split_to_table(string? source, string? pattern, string? flags) => new("regexp_split_to_table");

        /// <summary>
        /// <c>jsonb_array_elements(json)</c> as a FROM source: one row per array element, the
        /// <c>value</c> column is the element as a jsonb document (see
        /// <see cref="SqlFunctions.IJsonArrayElementsRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonArrayElementsRow> jsonb_array_elements(object? json) => new("jsonb_array_elements");

        /// <summary><c>jsonb_array_elements_text(json)</c> as a FROM source: one row per array element, the <c>value</c> column is text.</summary>
        public TvfEntityBuilder<SqlFunctions.IJsonArrayElementsRow> jsonb_array_elements_text(object? json) => new("jsonb_array_elements_text");

        /// <summary>
        /// <c>jsonb_each(json)</c> as a FROM source: one row per top-level object property, the
        /// <c>key</c>/<c>value</c> columns are text/jsonb (see <see cref="SqlFunctions.IJsonbEachRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonbEachRow> jsonb_each(object? json) => new("jsonb_each");

        /// <summary><c>jsonb_each_text(json)</c> as a FROM source: one row per property, <c>key</c>/<c>value</c> are text.</summary>
        public TvfEntityBuilder<SqlFunctions.IJsonbEachRow> jsonb_each_text(object? json) => new("jsonb_each_text");

        /// <summary>
        /// <c>json_array_elements(json)</c> as a FROM source: one row per array element of the <c>json</c>
        /// document, the single <c>value</c> column is the element as a <c>json</c> value (see
        /// <see cref="SqlFunctions.IJsonArrayElementsRow"/>). The document is a JSON <em>text</em>: the
        /// argument is cast to <c>json</c> in the emitted SQL, because a text parameter does not resolve to
        /// the function. An empty array yields no rows.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonArrayElementsRow> json_array_elements(string? json) => new("json_array_elements");

        /// <summary>
        /// <c>json_array_elements_text(json)</c> as a FROM source: one row per array element, the
        /// <c>value</c> column is text (<c>null</c> for a JSON <c>null</c> element).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonArrayElementsRow> json_array_elements_text(string? json) => new("json_array_elements_text");

        /// <summary>
        /// <c>json_each(json)</c> as a FROM source: one row per top-level object property, the
        /// <c>key</c> (text) and <c>value</c> (a <c>json</c> value) columns (see
        /// <see cref="SqlFunctions.IJsonbEachRow"/>; PostgreSQL 17 returns exactly two columns). An empty
        /// object yields no rows.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonbEachRow> json_each(string? json) => new("json_each");

        /// <summary><c>json_each_text(json)</c> as a FROM source: one row per property, <c>key</c>/<c>value</c> are text.</summary>
        public TvfEntityBuilder<SqlFunctions.IJsonbEachRow> json_each_text(string? json) => new("json_each_text");

        /// <summary>
        /// <c>jsonb_path_query_tz(json, path, vars, silent)</c> as a FROM source: one row per JSONPath
        /// match, the single <c>jsonb_path_query_tz</c> column holds the match as text (see
        /// <see cref="SqlFunctions.IJsonPathQueryTzRow"/>). The <c>_tz</c> form is required for paths that
        /// depend on the session time zone (for example the <c>.datetime()</c> method); the non-<c>_tz</c>
        /// sibling <see cref="jsonb_path_query(object?, string?)"/> raises an error for them. No match
        /// yields no rows, and a SQL-NULL document yields no rows.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonPathQueryTzRow> jsonb_path_query_tz(string? json, string? path, string? vars, bool? silent) => new("jsonb_path_query_tz");

        /// <summary>
        /// <c>jsonb_object_keys(json)</c> as a FROM source: one row per top-level object key (see
        /// <see cref="SqlFunctions.IJsonObjectKeysRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonObjectKeysRow> jsonb_object_keys(object? json) => new("jsonb_object_keys");

        /// <summary>
        /// <c>jsonb_path_query(json, path)</c> as a FROM source: one row per JSONPath match. Cast a text
        /// path with <see cref="jsonpath(string?)"/> (<see cref="SqlFunctions.IJsonPathQueryRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IJsonPathQueryRow> jsonb_path_query(object? json, string? path) => new("jsonb_path_query");

        /// <summary>
        /// <c>jsonb_to_record(json) AS x(col type, ...)</c> as a FROM source: one row from a single JSON
        /// object, with the result schema derived from <typeparamref name="TRow"/>'s mapped properties
        /// and rendered as the alias column-definition list. Requires a provider that supports a typed
        /// result schema (see <see cref="ISqlDialect.SupportsResultSchema(TableFunctionSchema)"/>; PostgreSQL). Use
        /// through
        /// <c>ctx.From(() =&gt; ...)</c>.
        /// </summary>
        /// <typeparam name="TRow">The caller-declared row shape; its <c>[Column]</c> names/types form the column list.</typeparam>
        /// <param name="json">The JSON object to expand.</param>
        public TvfEntityBuilder<TRow> jsonb_to_record<TRow>(object? json) => new("jsonb_to_record", null, null, null, [], TableFunctionSchema.AliasColumnList);

        /// <summary>
        /// <c>jsonb_to_recordset(json) AS x(col type, ...)</c> as a FROM source: one row per element of a
        /// JSON array, with the result schema derived from <typeparamref name="TRow"/>'s mapped
        /// properties. This is the set form of <see cref="jsonb_to_record{TRow}(object?)"/>. Requires a
        /// provider that supports a typed result schema (see
        /// <see cref="ISqlDialect.SupportsResultSchema(TableFunctionSchema)"/>; PostgreSQL).
        /// </summary>
        /// <typeparam name="TRow">The caller-declared row shape; its <c>[Column]</c> names/types form the column list.</typeparam>
        /// <param name="json">The JSON array to expand.</param>
        public TvfEntityBuilder<TRow> jsonb_to_recordset<TRow>(object? json) => new("jsonb_to_recordset", null, null, null, [], TableFunctionSchema.AliasColumnList);

        /// <summary>
        /// <c>json_to_record(json) AS x(col type, ...)</c> as a FROM source: one row from a single
        /// <c>json</c> object, with the result schema derived from <typeparamref name="TRow"/>'s mapped
        /// properties and rendered as the alias column-definition list. The document is a JSON
        /// <em>text</em> (the argument is cast to <c>json</c> in the emitted SQL). Requires a provider that
        /// supports a typed result schema (see <see cref="ISqlDialect.SupportsResultSchema(TableFunctionSchema)"/>;
        /// PostgreSQL). Use through <c>ctx.From(() =&gt; ...)</c>.
        /// </summary>
        /// <typeparam name="TRow">The caller-declared row shape; its <c>[Column]</c> names/types form the column list.</typeparam>
        /// <param name="json">The JSON object to expand.</param>
        public TvfEntityBuilder<TRow> json_to_record<TRow>(string? json) => new("json_to_record", null, null, null, [], TableFunctionSchema.AliasColumnList);

        /// <summary>
        /// <c>json_to_recordset(json) AS x(col type, ...)</c> as a FROM source: one row per element of a
        /// <c>json</c> array, with the result schema derived from <typeparamref name="TRow"/>'s mapped
        /// properties. This is the set form of <see cref="json_to_record{TRow}(string?)"/>; an empty array
        /// yields no rows.
        /// </summary>
        /// <typeparam name="TRow">The caller-declared row shape; its <c>[Column]</c> names/types form the column list.</typeparam>
        /// <param name="json">The JSON array to expand.</param>
        public TvfEntityBuilder<TRow> json_to_recordset<TRow>(string? json) => new("json_to_recordset", null, null, null, [], TableFunctionSchema.AliasColumnList);

        /// <summary>
        /// <c>json_populate_record(base, json, use_json_as_text)</c> is refused: the first argument is an
        /// <c>anyelement</c> base <em>row</em> whose composite type a CLR type cannot name, so no faithful
        /// call can be built (the column-definition list is available but does not replace the base row).
        /// Referencing this member fails while the query is prepared.
        /// </summary>
        /// <typeparam name="TRow">The base row type the server would have to infer from the base value.</typeparam>
        /// <param name="baseRecord">The base row value; a CLR type cannot carry the <c>anyelement</c> base.</param>
        /// <param name="json">The JSON document to expand.</param>
        /// <param name="useJsonAsText">Whether JSON strings are read as text values.</param>
        /// <returns>Never returns; the call is refused when the source is composed.</returns>
        public TvfEntityBuilder<TRow> json_populate_record<TRow>(object? baseRecord, string? json, bool? useJsonAsText) =>
            throw new NotSupportedException(
                "The json_populate_record function is refused: its first argument is an anyelement base row, " +
                "and neither a named composite type nor the pseudo-type record can be named from a CLR type, " +
                "so no faithful call can be emitted.");

        /// <summary>
        /// <c>json_populate_recordset(base, json, use_json_as_text)</c> is refused for the same reason as
        /// <see cref="json_populate_record{TRow}(object?, string?, bool?)"/>: the <c>anyelement</c> base row
        /// cannot be supplied from a CLR type.
        /// </summary>
        /// <typeparam name="TRow">The base row type the server would have to infer from the base value.</typeparam>
        /// <param name="baseRecord">The base row value; a CLR type cannot carry the <c>anyelement</c> base.</param>
        /// <param name="json">The JSON array to expand.</param>
        /// <param name="useJsonAsText">Whether JSON strings are read as text values.</param>
        /// <returns>Never returns; the call is refused when the source is composed.</returns>
        public TvfEntityBuilder<TRow> json_populate_recordset<TRow>(object? baseRecord, string? json, bool? useJsonAsText) =>
            throw new NotSupportedException(
                "The json_populate_recordset function is refused: its first argument is an anyelement base row, " +
                "and neither a named composite type nor the pseudo-type record can be named from a CLR type, " +
                "so no faithful call can be emitted.");

        /// <summary>
        /// <c>jsonb_populate_record(base, json)</c> is refused: the <c>anyelement</c> base row's composite
        /// type is what the result row carries, and no CLR type can name it.
        /// </summary>
        /// <typeparam name="TRow">The base row type the server would have to infer from the base value.</typeparam>
        /// <param name="baseRecord">The base row value; a CLR type cannot carry the <c>anyelement</c> base.</param>
        /// <param name="json">The JSON document to expand.</param>
        /// <returns>Never returns; the call is refused when the source is composed.</returns>
        public TvfEntityBuilder<TRow> jsonb_populate_record<TRow>(object? baseRecord, string? json) =>
            throw new NotSupportedException(
                "The jsonb_populate_record function is refused: its first argument is an anyelement base row, " +
                "and the result row type is that base row's composite type, which no CLR type can name.");

        /// <summary>
        /// <c>jsonb_populate_recordset(base, json)</c> is refused for the same reason as
        /// <see cref="jsonb_populate_record{TRow}(object?, string?)"/>: the <c>anyelement</c> base row cannot
        /// be supplied from a CLR type.
        /// </summary>
        /// <typeparam name="TRow">The base row type the server would have to infer from the base value.</typeparam>
        /// <param name="baseRecord">The base row value; a CLR type cannot carry the <c>anyelement</c> base.</param>
        /// <param name="json">The JSON array to expand.</param>
        /// <returns>Never returns; the call is refused when the source is composed.</returns>
        public TvfEntityBuilder<TRow> jsonb_populate_recordset<TRow>(object? baseRecord, string? json) =>
            throw new NotSupportedException(
                "The jsonb_populate_recordset function is refused: its first argument is an anyelement base row, " +
                "and the result row type is that base row's composite type, which no CLR type can name.");

        /// <summary>
        /// <c>jsonb_populate_record_valid(base, json)</c> is refused: the <c>anyelement</c> base row cannot be
        /// supplied from a CLR type, and without a base row the server cannot determine the row type
        /// (<c>0A000</c>). Referencing this member fails while the query is prepared, before any SQL is emitted.
        /// </summary>
        /// <param name="baseRecord">The base row value; a CLR type cannot carry the <c>anyelement</c> base.</param>
        /// <param name="json">The JSON document to validate.</param>
        /// <returns>Never returns; the call is refused when the query is prepared.</returns>
        public bool? jsonb_populate_record_valid(object? baseRecord, string? json) =>
            throw new NotSupportedException(
                "The jsonb_populate_record_valid function is refused: its first argument is an anyelement base " +
                "row that a CLR type cannot name, and without a base row the server raises 0A000 (could not " +
                "determine row type).");

        /// <summary>
        /// <c>json_table(json, path COLUMNS (...))</c> is refused: the SQL/JSON table function needs its
        /// per-call <c>COLUMNS (...)</c> clause inside the call parentheses, and the expression model only
        /// renders a caller-declared schema as an alias column-definition list after the alias. The construct
        /// itself works on PostgreSQL 17; only the rendering model cannot express it.
        /// </summary>
        /// <typeparam name="TRow">The row type the <c>COLUMNS</c> clause would declare.</typeparam>
        /// <param name="json">The JSON document to read.</param>
        /// <param name="path">The JSONPath that selects the rows.</param>
        /// <returns>Never returns; the call is refused when the source is composed.</returns>
        public TvfEntityBuilder<TRow> json_table<TRow>(string? json, string? path) =>
            throw new NotSupportedException(
                "The json_table function is refused: it needs a per-call COLUMNS (...) clause inside the call " +
                "parentheses, which the expression model cannot render (a caller-declared schema is only " +
                "rendered as an alias column-definition list after the alias).");

        /// <summary>
        /// <c>ts_stat(query)</c> as a FROM source: one row per lexeme of a <c>tsvector</c> query, the
        /// <c>word</c>/<c>ndoc</c>/<c>nentry</c> columns (see <see cref="SqlFunctions.ITsStatRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.ITsStatRow> ts_stat(string? query) => new("ts_stat");

        /// <summary>
        /// <c>pg_control_system()</c> as a FROM source: the single control-file row (see
        /// <see cref="SqlFunctions.IPgControlSystemRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgControlSystemRow> pg_control_system() => new("pg_control_system");

        /// <summary>
        /// <c>pg_control_init()</c> as a FROM source: the single row of compile-time and control-file
        /// constants (see <see cref="SqlFunctions.IPgControlInitRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgControlInitRow> pg_control_init() => new("pg_control_init");

        /// <summary>
        /// <c>pg_control_recovery()</c> as a FROM source: the single recovery-state row; the
        /// <c>pg_lsn</c> columns are text (see <see cref="SqlFunctions.IPgControlRecoveryRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgControlRecoveryRow> pg_control_recovery() => new("pg_control_recovery");

        /// <summary>
        /// <c>pg_control_checkpoint()</c> as a FROM source: the single checkpoint-state row; the
        /// <c>pg_lsn</c>/<c>oid</c> columns are text and the <c>xid</c> columns are <see cref="uint"/>
        /// (see <see cref="SqlFunctions.IPgControlCheckpointRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgControlCheckpointRow> pg_control_checkpoint() => new("pg_control_checkpoint");

        /// <summary>
        /// <c>pg_get_keywords()</c> as a FROM source: one row per SQL keyword or keyword-like token;
        /// the <c>"char"</c> category code is read as text (see
        /// <see cref="SqlFunctions.IPgKeywordRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgKeywordRow> pg_get_keywords() => new("pg_get_keywords");

        /// <summary>
        /// <c>pg_listening_channels()</c> as a FROM source: one row per channel this session is
        /// listening on (see <see cref="SqlFunctions.IListeningChannelRow"/>). The set is empty until a
        /// <c>LISTEN</c> is issued on the same connection.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IListeningChannelRow> pg_listening_channels() => new("pg_listening_channels");

        /// <summary>
        /// <c>pg_snapshot_xip(snapshot)</c> as a FROM source: one row per in-progress transaction id of
        /// the snapshot, read as text (see <see cref="SqlFunctions.ISnapshotXipRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.ISnapshotXipRow> pg_snapshot_xip(string? snapshot) => new("pg_snapshot_xip");

        /// <summary>
        /// <c>pg_tablespace_databases(oid)</c> as a FROM source: one row per database that has files in
        /// the tablespace, with the database OID read as text (see
        /// <see cref="SqlFunctions.ITablespaceDatabaseRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.ITablespaceDatabaseRow> pg_tablespace_databases(string? tablespaceOid) => new("pg_tablespace_databases");

        /// <summary>
        /// <c>pg_get_catalog_foreign_keys()</c> as a FROM source: one row per catalog foreign-key
        /// relationship, with both <c>regclass</c> columns read as text and both column lists as
        /// <c>text[]</c> (see <see cref="SqlFunctions.IPgCatalogForeignKeyRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgCatalogForeignKeyRow> pg_get_catalog_foreign_keys() => new("pg_get_catalog_foreign_keys");

        /// <summary>
        /// <c>pg_options_to_table(options)</c> as a FROM source: one row per <c>name=value</c> storage
        /// option, split into <c>option_name</c>/<c>option_value</c> (see
        /// <see cref="SqlFunctions.IPgOptionRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgOptionRow> pg_options_to_table(string?[] options) => new("pg_options_to_table");

        /// <summary>
        /// <c>pg_available_wal_summaries()</c> as a FROM source: one row per WAL summary file on disk,
        /// with the <c>pg_lsn</c> bounds read as text (see
        /// <see cref="SqlFunctions.IPgAvailableWalSummaryRow"/>). Empty while
        /// <c>summarize_wal</c> is off.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgAvailableWalSummaryRow> pg_available_wal_summaries() => new("pg_available_wal_summaries");

        /// <summary>
        /// <c>pg_get_multixact_members(multixactid)</c> as a FROM source: one row per transaction that
        /// is a member of the multixact, with its lock mode (see
        /// <see cref="SqlFunctions.IPgMultixactMemberRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgMultixactMemberRow> pg_get_multixact_members(uint? multixactId) => new("pg_get_multixact_members");

        /// <summary>
        /// <c>pg_get_object_address(type, object_names, object_args)</c> as a FROM source: the catalog
        /// coordinates of the described object, with both OIDs read as text (see
        /// <see cref="SqlFunctions.IPgObjectAddressRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgObjectAddressRow> pg_get_object_address(string? type, string?[] objectNames, string?[] objectArgs) => new("pg_get_object_address");

        /// <summary>
        /// <c>pg_get_wal_summarizer_state()</c> as a FROM source: the single WAL summarizer state row,
        /// with the <c>pg_lsn</c> columns read as text (see
        /// <see cref="SqlFunctions.IPgWalSummarizerStateRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgWalSummarizerStateRow> pg_get_wal_summarizer_state() => new("pg_get_wal_summarizer_state");

        /// <summary>
        /// <c>pg_last_committed_xact()</c> as a FROM source: the last transaction with a commit
        /// timestamp, or no row when none is recorded (see
        /// <see cref="SqlFunctions.IPgCommittedXactRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgCommittedXactRow> pg_last_committed_xact() => new("pg_last_committed_xact");

        /// <summary>
        /// <c>pg_identify_object(classid, objid, objsubid)</c> as a FROM source: the type, schema, name
        /// and identity of one catalog object (see <see cref="SqlFunctions.IPgIdentifyObjectRow"/>). An
        /// object that does not exist yields one all-NULL row.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgIdentifyObjectRow> pg_identify_object(string? classId, string? objectId, int? objectSubId) => new("pg_identify_object");

        /// <summary>
        /// <c>pg_identify_object_as_address(classid, objid, objsubid)</c> as a FROM source: the
        /// machine-readable address of one catalog object, both components as <c>text[]</c> (see
        /// <see cref="SqlFunctions.IPgIdentifyObjectAddressRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgIdentifyObjectAddressRow> pg_identify_object_as_address(string? classId, string? objectId, int? objectSubId) => new("pg_identify_object_as_address");

        /// <summary>
        /// <c>pg_input_error_info(value, type_name)</c> as a FROM source: the soft error report of parsing
        /// <paramref name="value"/> as <paramref name="typeName"/>. A valid value yields one all-NULL row;
        /// an invalid value yields the message and SQLSTATE (see
        /// <see cref="SqlFunctions.IPgInputErrorInfoRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgInputErrorInfoRow> pg_input_error_info(string? value, string? typeName) => new("pg_input_error_info");

        /// <summary>
        /// <c>pg_wal_summary_contents(tli, start_lsn, end_lsn)</c> as a FROM source: one block range of the
        /// WAL summary file that covers the requested range, with the <c>oid</c> columns read as text (see
        /// <see cref="SqlFunctions.IPgWalSummaryContentRow"/>).
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgWalSummaryContentRow> pg_wal_summary_contents(long? timelineId, string? startLsn, string? endLsn) => new("pg_wal_summary_contents");

        /// <summary>
        /// <c>pg_xact_commit_timestamp_origin(xid)</c> as a FROM source: the commit timestamp of one
        /// transaction and its replication origin, with <c>roident</c> read as text (see
        /// <see cref="SqlFunctions.IPgXactCommitOriginRow"/>). Requires <c>track_commit_timestamp</c>.
        /// </summary>
        public TvfEntityBuilder<SqlFunctions.IPgXactCommitOriginRow> pg_xact_commit_timestamp_origin(uint? xid) => new("pg_xact_commit_timestamp_origin");

        /// <summary><c>a &amp;&amp; b</c>: true when the two ranges overlap (share at least one point).</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns><see langword="true"/> when the ranges overlap.</returns>
        public bool overlaps<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>range @&gt; value</c>: true when <paramref name="value"/> is inside <paramref name="range"/>.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to test.</param>
        /// <param name="value">The point to look for.</param>
        /// <returns><see langword="true"/> when the range contains the value.</returns>
        public bool range_contains<T>(Range<T> range, T value) where T : struct, IComparable<T> => default!;

        /// <summary><c>outer @&gt; inner</c>: true when <paramref name="outer"/> contains every point of <paramref name="inner"/>.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="outer">The containing range.</param>
        /// <param name="inner">The contained range.</param>
        /// <returns><see langword="true"/> when the outer range contains the inner one.</returns>
        public bool range_contains<T>(Range<T> outer, Range<T> inner) where T : struct, IComparable<T> => default!;

        /// <summary><c>inner &lt;@ outer</c>: true when every point of <paramref name="inner"/> is inside <paramref name="outer"/>.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="inner">The contained range.</param>
        /// <param name="outer">The containing range.</param>
        /// <returns><see langword="true"/> when the inner range is contained by the outer one.</returns>
        public bool range_contained_by<T>(Range<T> inner, Range<T> outer) where T : struct, IComparable<T> => default!;

        /// <summary><c>a + b</c>: the smallest range covering both operands (they must overlap or be adjacent).</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns>The union range.</returns>
        public Range<T> range_union<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a * b</c>: the common part of the two ranges, or the empty range.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns>The intersection range.</returns>
        public Range<T> range_intersection<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a - b</c>: the part of <paramref name="a"/> not covered by <paramref name="b"/>.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The range to subtract from.</param>
        /// <param name="b">The range to subtract.</param>
        /// <returns>The difference range.</returns>
        public Range<T> range_difference<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a -|- b</c>: true when the ranges are adjacent (they touch but do not overlap).</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns><see langword="true"/> when the ranges are adjacent.</returns>
        public bool range_adjacent<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &lt;&lt; b</c>: true when <paramref name="a"/> ends before <paramref name="b"/> starts.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns><see langword="true"/> when the left range is strictly to the left.</returns>
        public bool range_strictly_left_of<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &gt;&gt; b</c>: true when <paramref name="a"/> starts after <paramref name="b"/> ends.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns><see langword="true"/> when the left range is strictly to the right.</returns>
        public bool range_strictly_right_of<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &amp;&lt; b</c>: true when <paramref name="a"/> does not extend to the right of <paramref name="b"/>.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns><see langword="true"/> when <paramref name="a"/> does not extend past the right end of <paramref name="b"/>.</returns>
        public bool range_not_extend_right_of<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &amp;&gt; b</c>: true when <paramref name="a"/> does not extend to the left of <paramref name="b"/>.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns><see langword="true"/> when <paramref name="a"/> does not extend past the left end of <paramref name="b"/>.</returns>
        public bool range_not_extend_left_of<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>isempty(range)</c>: true when the range contains no point.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to test.</param>
        /// <returns><see langword="true"/> when the range is empty.</returns>
        public bool isempty<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>lower(range)</c>: the lower bound, or <see langword="null"/> when it is unbounded.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to inspect.</param>
        /// <returns>The lower bound, or <see langword="null"/>.</returns>
        public T? lower<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>upper(range)</c>: the upper bound, or <see langword="null"/> when it is unbounded.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to inspect.</param>
        /// <returns>The upper bound, or <see langword="null"/>.</returns>
        public T? upper<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>lower_inc(range)</c>: true when the lower bound is inclusive.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to inspect.</param>
        /// <returns><see langword="true"/> when the lower bound is inclusive.</returns>
        public bool lower_inc<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>upper_inc(range)</c>: true when the upper bound is inclusive.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to inspect.</param>
        /// <returns><see langword="true"/> when the upper bound is inclusive.</returns>
        public bool upper_inc<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>lower_inf(range)</c>: true when the lower side is unbounded.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to inspect.</param>
        /// <returns><see langword="true"/> when the lower side is unbounded.</returns>
        public bool lower_inf<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>upper_inf(range)</c>: true when the upper side is unbounded.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to inspect.</param>
        /// <returns><see langword="true"/> when the upper side is unbounded.</returns>
        public bool upper_inf<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary>
        /// The empty range of <typeparamref name="T"/>, rendered as the PostgreSQL
        /// <c>'empty'::&lt;range type&gt;</c> literal. When the value is supplied as a parameter, use
        /// <see cref="Range{T}.Empty"/> instead.
        /// </summary>
        /// <typeparam name="T">The bound type that selects the PostgreSQL range type.</typeparam>
        /// <returns>The empty range.</returns>
        public Range<T> empty_range<T>() where T : struct, IComparable<T> => default!;

        /// <summary>Builds <c>int4range(lower, upper)</c> with the default <c>[)</c> bounds.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <returns>The <c>int4range</c> value.</returns>
        public Range<int> int4range(int? lower, int? upper) => default!;

        /// <summary>Builds <c>int4range(lower, upper, bounds)</c>.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="bounds">The bound characters, for example <c>[)</c>, <c>[]</c>, <c>()</c> or <c>(]</c>.</param>
        /// <returns>The <c>int4range</c> value.</returns>
        public Range<int> int4range(int? lower, int? upper, string bounds) => default!;

        /// <summary>Builds <c>int8range(lower, upper)</c> with the default <c>[)</c> bounds.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <returns>The <c>int8range</c> value.</returns>
        public Range<long> int8range(long? lower, long? upper) => default!;

        /// <summary>Builds <c>int8range(lower, upper, bounds)</c>.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="bounds">The bound characters, for example <c>[)</c>, <c>[]</c>, <c>()</c> or <c>(]</c>.</param>
        /// <returns>The <c>int8range</c> value.</returns>
        public Range<long> int8range(long? lower, long? upper, string bounds) => default!;

        /// <summary>Builds <c>numrange(lower, upper)</c> with the default <c>[)</c> bounds.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <returns>The <c>numrange</c> value.</returns>
        public Range<decimal> numrange(decimal? lower, decimal? upper) => default!;

        /// <summary>Builds <c>numrange(lower, upper, bounds)</c>.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="bounds">The bound characters, for example <c>[)</c>, <c>[]</c>, <c>()</c> or <c>(]</c>.</param>
        /// <returns>The <c>numrange</c> value.</returns>
        public Range<decimal> numrange(decimal? lower, decimal? upper, string bounds) => default!;

        /// <summary>Builds <c>tsrange(lower, upper)</c> with the default <c>[)</c> bounds.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <returns>The <c>tsrange</c> value.</returns>
        public Range<DateTime> tsrange(DateTime? lower, DateTime? upper) => default!;

        /// <summary>Builds <c>tsrange(lower, upper, bounds)</c>.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="bounds">The bound characters, for example <c>[)</c>, <c>[]</c>, <c>()</c> or <c>(]</c>.</param>
        /// <returns>The <c>tsrange</c> value.</returns>
        public Range<DateTime> tsrange(DateTime? lower, DateTime? upper, string bounds) => default!;

        /// <summary>Builds <c>tstzrange(lower, upper)</c> with the default <c>[)</c> bounds.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <returns>The <c>tstzrange</c> value.</returns>
        public Range<DateTimeOffset> tstzrange(DateTimeOffset? lower, DateTimeOffset? upper) => default!;

        /// <summary>Builds <c>tstzrange(lower, upper, bounds)</c>.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="bounds">The bound characters, for example <c>[)</c>, <c>[]</c>, <c>()</c> or <c>(]</c>.</param>
        /// <returns>The <c>tstzrange</c> value.</returns>
        public Range<DateTimeOffset> tstzrange(DateTimeOffset? lower, DateTimeOffset? upper, string bounds) => default!;

        /// <summary>Builds <c>daterange(lower, upper)</c> with the default <c>[)</c> bounds.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <returns>The <c>daterange</c> value.</returns>
        public Range<DateOnly> daterange(DateOnly? lower, DateOnly? upper) => default!;

        /// <summary>Builds <c>daterange(lower, upper, bounds)</c>.</summary>
        /// <param name="lower">The lower bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="upper">The upper bound, or <see langword="null"/> for an unbounded side.</param>
        /// <param name="bounds">The bound characters, for example <c>[)</c>, <c>[]</c>, <c>()</c> or <c>(]</c>.</param>
        /// <returns>The <c>daterange</c> value.</returns>
        public Range<DateOnly> daterange(DateOnly? lower, DateOnly? upper, string bounds) => default!;

        /// <summary><c>multirange(range)</c>: a multirange containing just the given range.</summary>
        /// <typeparam name="T">The bound type of the range.</typeparam>
        /// <param name="range">The range to wrap.</param>
        /// <returns>The multirange with a single element.</returns>
        public Range<T>[] multirange<T>(Range<T> range) where T : struct, IComparable<T> => default!;

        /// <summary><c>range_merge(a, b)</c>: the smallest range that includes both ranges.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left range.</param>
        /// <param name="b">The right range.</param>
        /// <returns>The smallest range covering both operands.</returns>
        public Range<T> range_merge<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>range_merge(multirange)</c>: the smallest range that includes the whole multirange.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to merge.</param>
        /// <returns>The smallest range covering the multirange.</returns>
        public Range<T> range_merge<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>range_agg(value)</c>: the multirange of all ranges in the group.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="value">The range to aggregate.</param>
        /// <returns>The aggregated multirange.</returns>
        public Range<T>[] range_agg<T>(Range<T> value) where T : struct, IComparable<T> => default!;

        /// <summary><c>range_intersect_agg(value)</c>: the intersection of all ranges in the group.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="value">The range to aggregate.</param>
        /// <returns>The intersection of every range in the group.</returns>
        public Range<T> range_intersect_agg<T>(Range<T> value) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &amp;&amp; b</c>: true when the two multiranges share at least one point.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns><see langword="true"/> when the multiranges overlap.</returns>
        public bool overlaps<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>multirange &amp;&amp; range</c>: true when the multirange overlaps the range.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The multirange.</param>
        /// <param name="b">The range.</param>
        /// <returns><see langword="true"/> when the multirange overlaps the range.</returns>
        public bool overlaps<T>(Range<T>[] a, Range<T> b) where T : struct, IComparable<T> => default!;

        /// <summary><c>range &amp;&amp; multirange</c>: true when the range overlaps the multirange.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The range.</param>
        /// <param name="b">The multirange.</param>
        /// <returns><see langword="true"/> when the range overlaps the multirange.</returns>
        public bool overlaps<T>(Range<T> a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>multirange @&gt; multirange</c>: true when the first multirange contains the second.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="outer">The containing multirange.</param>
        /// <param name="inner">The contained multirange.</param>
        /// <returns><see langword="true"/> when the outer multirange contains the inner one.</returns>
        public bool range_contains<T>(Range<T>[] outer, Range<T>[] inner) where T : struct, IComparable<T> => default!;

        /// <summary><c>multirange @&gt; range</c>: true when the multirange contains the range.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="outer">The containing multirange.</param>
        /// <param name="inner">The contained range.</param>
        /// <returns><see langword="true"/> when the multirange contains the range.</returns>
        public bool range_contains<T>(Range<T>[] outer, Range<T> inner) where T : struct, IComparable<T> => default!;

        /// <summary><c>range @&gt; multirange</c>: true when the range contains the multirange.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="outer">The containing range.</param>
        /// <param name="inner">The contained multirange.</param>
        /// <returns><see langword="true"/> when the range contains the multirange.</returns>
        public bool range_contains<T>(Range<T> outer, Range<T>[] inner) where T : struct, IComparable<T> => default!;

        /// <summary><c>multirange @&gt; value</c>: true when the multirange contains the element.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="multirange">The multirange to test.</param>
        /// <param name="value">The point to look for.</param>
        /// <returns><see langword="true"/> when the multirange contains the value.</returns>
        public bool range_contains<T>(Range<T>[] multirange, T value) where T : struct, IComparable<T> => default!;

        /// <summary><c>multirange &lt;@ multirange</c>: true when the first multirange is contained by the second.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="inner">The contained multirange.</param>
        /// <param name="outer">The containing multirange.</param>
        /// <returns><see langword="true"/> when the inner multirange is contained by the outer one.</returns>
        public bool range_contained_by<T>(Range<T>[] inner, Range<T>[] outer) where T : struct, IComparable<T> => default!;

        /// <summary><c>multirange &lt;@ range</c>: true when the multirange is contained by the range.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="inner">The contained multirange.</param>
        /// <param name="outer">The containing range.</param>
        /// <returns><see langword="true"/> when the multirange is contained by the range.</returns>
        public bool range_contained_by<T>(Range<T>[] inner, Range<T> outer) where T : struct, IComparable<T> => default!;

        /// <summary><c>range &lt;@ multirange</c>: true when the range is contained by the multirange.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="inner">The contained range.</param>
        /// <param name="outer">The containing multirange.</param>
        /// <returns><see langword="true"/> when the range is contained by the multirange.</returns>
        public bool range_contained_by<T>(Range<T> inner, Range<T>[] outer) where T : struct, IComparable<T> => default!;

        /// <summary><c>a + b</c>: the multirange union (the operands need not overlap).</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns>The union multirange.</returns>
        public Range<T>[] range_union<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a * b</c>: the multirange intersection.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns>The intersection multirange.</returns>
        public Range<T>[] range_intersection<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a - b</c>: the multirange difference.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The multirange to subtract from.</param>
        /// <param name="b">The multirange to subtract.</param>
        /// <returns>The difference multirange.</returns>
        public Range<T>[] range_difference<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &lt;&lt; b</c>: true when the first multirange is strictly left of the second.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns><see langword="true"/> when the first multirange is strictly to the left.</returns>
        public bool range_strictly_left_of<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &gt;&gt; b</c>: true when the first multirange is strictly right of the second.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns><see langword="true"/> when the first multirange is strictly to the right.</returns>
        public bool range_strictly_right_of<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &amp;&lt; b</c>: true when the first multirange does not extend to the right of the second.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns><see langword="true"/> when the first multirange does not extend past the right end of the second.</returns>
        public bool range_not_extend_right_of<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a &amp;&gt; b</c>: true when the first multirange does not extend to the left of the second.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns><see langword="true"/> when the first multirange does not extend past the left end of the second.</returns>
        public bool range_not_extend_left_of<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>a -|- b</c>: true when the multiranges are adjacent.</summary>
        /// <typeparam name="T">The bound type of the ranges.</typeparam>
        /// <param name="a">The left multirange.</param>
        /// <param name="b">The right multirange.</param>
        /// <returns><see langword="true"/> when the multiranges are adjacent.</returns>
        public bool range_adjacent<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T> => default!;

        /// <summary><c>isempty(multirange)</c>: true when the multirange contains no range.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to test.</param>
        /// <returns><see langword="true"/> when the multirange is empty.</returns>
        public bool isempty<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>lower(multirange)</c>: the lower bound, or <see langword="null"/> when it is unbounded.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to inspect.</param>
        /// <returns>The lower bound, or <see langword="null"/>.</returns>
        public T? lower<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>upper(multirange)</c>: the upper bound, or <see langword="null"/> when it is unbounded.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to inspect.</param>
        /// <returns>The upper bound, or <see langword="null"/>.</returns>
        public T? upper<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>lower_inc(multirange)</c>: true when the lower bound is inclusive.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to inspect.</param>
        /// <returns><see langword="true"/> when the lower bound is inclusive.</returns>
        public bool lower_inc<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>upper_inc(multirange)</c>: true when the upper bound is inclusive.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to inspect.</param>
        /// <returns><see langword="true"/> when the upper bound is inclusive.</returns>
        public bool upper_inc<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>lower_inf(multirange)</c>: true when the lower side is unbounded.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to inspect.</param>
        /// <returns><see langword="true"/> when the lower side is unbounded.</returns>
        public bool lower_inf<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;

        /// <summary><c>upper_inf(multirange)</c>: true when the upper side is unbounded.</summary>
        /// <typeparam name="T">The bound type of the multirange.</typeparam>
        /// <param name="multirange">The multirange to inspect.</param>
        /// <returns><see langword="true"/> when the upper side is unbounded.</returns>
        public bool upper_inf<T>(Range<T>[] multirange) where T : struct, IComparable<T> => default!;
    }
