using System.Linq;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// PostgreSQL-only SQL surface: arrays, native JSON, the extended scalar library, the
/// PostgreSQL-only aggregates and the <c>generate_series</c>/<c>unnest</c> table functions.
/// Exposed through <see cref="SqlFunctions.Postgres"/>; every member is gated by a capability flag and
/// rejected by providers that do not opt in.
/// </summary>
    public class PostgresFunctions : CommonFunctions
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

        /// <summary>Reverses the elements of <paramref name="array"/> (<c>array_reverse</c>, PostgreSQL 18+).</summary>
        public T[] array_reverse<T>(T[] array) => default!;

        /// <summary>Sorts the elements of <paramref name="array"/> (<c>array_sort</c>, PostgreSQL 18+).</summary>
        public T[] array_sort<T>(T[] array) => default!;

        /// <summary>Returns a randomly shuffled copy of <paramref name="array"/> (<c>array_shuffle</c>, PostgreSQL 16+).</summary>
        public T[] array_shuffle<T>(T[] array) => default!;

        /// <summary>Returns <paramref name="n"/> randomly selected elements of <paramref name="array"/> (<c>array_sample</c>, PostgreSQL 16+).</summary>
        public T[] array_sample<T>(T[] array, int n) => default!;

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

        /// <summary>Converts radians to degrees.</summary>
        public double? degrees(double? x) => default!;

        /// <summary>Converts degrees to radians.</summary>
        public double? radians(double? x) => default!;

        /// <summary>The constant pi.</summary>
        public double? pi() => default!;

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

        /// <summary>Builds an interval from its parts.</summary>
        public TimeSpan? make_interval(int years, int months, int days, int hours, int minutes, double seconds) => default!;

        /// <summary>
        /// Builds a <c>time</c> from its parts (<c>make_time</c>). Use the cross-provider
        /// <see cref="CommonFunctions.date_from_parts"/> for <c>make_date</c>.
        /// </summary>
        public TimeSpan? make_time(int hour, int minute, double second) => default!;

        /// <summary>Builds a timestamp from its parts (<c>make_timestamp</c>).</summary>
        public DateTime? make_timestamp(int year, int month, int day, int hour, int minute, double second) => default!;

        /// <summary>Adjusts an interval so that 30-day units are represented as months.</summary>
        public TimeSpan? justify_days(TimeSpan? interval) => default!;

        /// <summary>Adjusts an interval so that 24-hour units are represented as days.</summary>
        public TimeSpan? justify_hours(TimeSpan? interval) => default!;

        /// <summary>
        /// The interval between two timestamps, subtracting <paramref name="b"/> from
        /// <paramref name="a"/> (<c>age</c>). PostgreSQL's <c>age</c> returns an <c>interval</c> that can
        /// carry whole months, which a <see cref="TimeSpan"/> cannot represent; PostgreSQL treats a
        /// month as 30 days when the value is read back as a <see cref="TimeSpan"/>.
        /// </summary>
        public TimeSpan? age(DateTime? a, DateTime? b) => default!;

        /// <summary>
        /// Binaries <paramref name="source"/> into buckets of <paramref name="stride"/> aligned to
        /// <paramref name="origin"/> (<c>date_bin</c>). The stride is an interval literal such as
        /// <c>"15 minutes"</c>; it is cast to <c>interval</c> in SQL.
        /// </summary>
        public DateTime? date_bin(string? stride, DateTime? source, DateTime? origin) => default!;

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
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, Expression{Func{IQueryable{T}}})"/>.
        /// </summary>
        [SqlTableFunction("generate_series")]
        public IQueryable<SqlFunctions.IGenerateSeriesRow> generate_series(long start, long stop) => throw new NotSupportedException();

        /// <summary><c>generate_series(start, stop, step)</c> as a FROM source.</summary>
        [SqlTableFunction("generate_series")]
        public IQueryable<SqlFunctions.IGenerateSeriesRow> generate_series(long start, long stop, long step) => throw new NotSupportedException();

        /// <summary>
        /// <c>unnest(array)</c> as a FROM source; select <see cref="SqlFunctions.IUnnestRow{T}.Value"/>. The
        /// array is normally supplied through <c>SqlFunctions.Parameter&lt;T[]&gt;(idx)</c>.
        /// </summary>
        [SqlTableFunction("unnest")]
        public IQueryable<SqlFunctions.IUnnestRow<T>> unnest<T>(T[] array) => throw new NotSupportedException();

        /// <summary>
        /// <c>regexp_matches(source, pattern)</c> as a FROM source: one row per match, the
        /// <c>regexp_matches</c> column is the <c>text[]</c> array of captured groups (see
        /// <see cref="SqlFunctions.IRegexpMatchesRow"/>).
        /// </summary>
        [SqlTableFunction("regexp_matches")]
        public IQueryable<SqlFunctions.IRegexpMatchesRow> regexp_matches(string? source, string? pattern) => throw new NotSupportedException();

        /// <summary><c>regexp_matches(source, pattern, flags)</c> as a FROM source, with the <c>g</c>/<c>i</c>/... flags.</summary>
        [SqlTableFunction("regexp_matches")]
        public IQueryable<SqlFunctions.IRegexpMatchesRow> regexp_matches(string? source, string? pattern, string? flags) => throw new NotSupportedException();

        /// <summary>
        /// <c>regexp_split_to_table(source, pattern)</c> as a FROM source: one row per fragment (see
        /// <see cref="SqlFunctions.IRegexpSplitToTableRow"/>).
        /// </summary>
        [SqlTableFunction("regexp_split_to_table")]
        public IQueryable<SqlFunctions.IRegexpSplitToTableRow> regexp_split_to_table(string? source, string? pattern) => throw new NotSupportedException();

        /// <summary><c>regexp_split_to_table(source, pattern, flags)</c> as a FROM source.</summary>
        [SqlTableFunction("regexp_split_to_table")]
        public IQueryable<SqlFunctions.IRegexpSplitToTableRow> regexp_split_to_table(string? source, string? pattern, string? flags) => throw new NotSupportedException();

        /// <summary>
        /// <c>jsonb_array_elements(json)</c> as a FROM source: one row per array element, the
        /// <c>value</c> column is the element as a jsonb document (see
        /// <see cref="SqlFunctions.IJsonArrayElementsRow"/>).
        /// </summary>
        [SqlTableFunction("jsonb_array_elements")]
        public IQueryable<SqlFunctions.IJsonArrayElementsRow> jsonb_array_elements(object? json) => throw new NotSupportedException();

        /// <summary><c>jsonb_array_elements_text(json)</c> as a FROM source: one row per array element, the <c>value</c> column is text.</summary>
        [SqlTableFunction("jsonb_array_elements_text")]
        public IQueryable<SqlFunctions.IJsonArrayElementsRow> jsonb_array_elements_text(object? json) => throw new NotSupportedException();

        /// <summary>
        /// <c>jsonb_each(json)</c> as a FROM source: one row per top-level object property, the
        /// <c>key</c>/<c>value</c> columns are text/jsonb (see <see cref="SqlFunctions.IJsonbEachRow"/>).
        /// </summary>
        [SqlTableFunction("jsonb_each")]
        public IQueryable<SqlFunctions.IJsonbEachRow> jsonb_each(object? json) => throw new NotSupportedException();

        /// <summary><c>jsonb_each_text(json)</c> as a FROM source: one row per property, <c>key</c>/<c>value</c> are text.</summary>
        [SqlTableFunction("jsonb_each_text")]
        public IQueryable<SqlFunctions.IJsonbEachRow> jsonb_each_text(object? json) => throw new NotSupportedException();

        /// <summary>
        /// <c>jsonb_object_keys(json)</c> as a FROM source: one row per top-level object key (see
        /// <see cref="SqlFunctions.IJsonObjectKeysRow"/>).
        /// </summary>
        [SqlTableFunction("jsonb_object_keys")]
        public IQueryable<SqlFunctions.IJsonObjectKeysRow> jsonb_object_keys(object? json) => throw new NotSupportedException();

        /// <summary>
        /// <c>jsonb_path_query(json, path)</c> as a FROM source: one row per JSONPath match. Cast a text
        /// path with <see cref="jsonpath(string?)"/> (<see cref="SqlFunctions.IJsonPathQueryRow"/>).
        /// </summary>
        [SqlTableFunction("jsonb_path_query")]
        public IQueryable<SqlFunctions.IJsonPathQueryRow> jsonb_path_query(object? json, string? path) => throw new NotSupportedException();

        /// <summary>
        /// <c>ts_stat(query)</c> as a FROM source: one row per lexeme of a <c>tsvector</c> query, the
        /// <c>word</c>/<c>ndoc</c>/<c>nentry</c> columns (see <see cref="SqlFunctions.ITsStatRow"/>).
        /// </summary>
        [SqlTableFunction("ts_stat")]
        public IQueryable<SqlFunctions.ITsStatRow> ts_stat(string? query) => throw new NotSupportedException();
    }
