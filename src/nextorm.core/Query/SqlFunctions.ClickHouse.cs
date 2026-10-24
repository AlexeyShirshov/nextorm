using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// ClickHouse-only SQL surface: the <c>argMin</c>/<c>argMax</c> aggregates, the distinct-count
/// <c>uniq</c>/<c>uniqExact</c>/<c>uniqCombined</c>/<c>uniqHLL12</c> aggregates, the parameterised
/// <c>quantile(level)(value)</c>/<c>quantiles(level...)(value)</c> family with <c>median</c>, the
/// <c>topK(k)(value)</c>/<c>topKWeighted(k)(value, weight)</c> aggregates, the <c>anyLast</c> row-picking
/// aggregate, the sequence/funnel aggregates <c>windowFunnel</c>/<c>retention</c>/<c>sequenceMatch</c>,
/// the frame-respecting <c>lagInFrame</c>/<c>leadInFrame</c> window functions, the multi-branch
/// <c>multiIf</c> conditional (through <see cref="when{T}(bool, T)"/>/<see cref="otherwise{T}(T)"/>),
/// the string-JSON <c>JSONExtract*</c>/<c>JSONHas</c> and <c>visitParamExtract*</c> families
/// plus the JSONPath <c>json_value</c>/<c>json_query</c>/<c>json_exists</c> scalars and the native-JSON
/// <c>JSONAllPaths</c>/<c>JSONAllPathsWithTypes</c>/<c>toJSONString</c> functions, the merge-patch,
/// validation and typed-extraction additions (<c>JSONMergePatch</c>, <c>JSONArrayLength</c>,
/// <c>JSONDynamicPaths</c>/<c>JSONSharedDataPaths</c> with their <c>WithTypes</c> siblings,
/// <c>JSONKey</c>, <c>JSONExtractKeysAndValuesRaw</c>, <c>JSONExtractUInt</c>, <c>isValidJSON</c> and
/// the <c>simpleJSONExtract*</c> family),
/// the dictionary functions, the <c>-If</c> aggregate combinator, the distributed <c>global_in</c>
/// predicate, the <c>numbers</c>/<c>numbers_mt</c> and <c>zeros</c>/<c>zeros_mt</c> table functions
/// (plus <c>generateRandom</c>) and the server/cluster table functions
/// (<c>url</c>/<c>s3</c>/<c>file</c>/<c>remote</c>/<c>remoteSecure</c>/<c>cluster</c>/<c>clusterAllReplicas</c>),
/// the date conversion/part surface (<c>toDate</c>/<c>toDateTime</c>/<c>toDate32</c>, the
/// <c>toYear</c>/... accessors, <c>toStartOf*</c>, <c>toMonday</c>, <c>toYYYYMM</c>/<c>toYYYYMMDD</c>,
/// <c>toUnixTimestamp</c>),
/// and the array functions over array columns and expressions (<c>arrayJoin</c>, <c>length</c>,
/// <c>has</c>, <c>indexOf</c>, <c>hasAny</c>/<c>hasAll</c>, <c>startsWith</c>/<c>endsWith</c>/<c>hasSubstr</c>,
/// <c>arrayStringConcat</c>,
/// <c>splitByChar</c>, <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>, <c>range</c>,
/// <c>arrayEnumerate</c>, <c>arrayCumSum</c>, <c>arraySlice</c>, <c>arrayPushBack</c>), the
/// higher-order (lambda) array functions (<c>arrayMap</c>, <c>arrayFilter</c>, <c>arrayExists</c>,
/// <c>arrayAll</c>, <c>arrayCount</c>, <c>arrayFirst*</c>, <c>arrayLast*</c>) plus the
/// array-returning aggregates <c>groupArray</c>/<c>groupUniqArray</c>, the string search/edit-distance
/// surface (<c>overlay</c>, <c>initcap</c>, <c>soundex</c>, the <c>base64Encode</c>/<c>base64Decode</c>
/// and <c>hex</c>/<c>unhex</c> pairs, <c>editDistance</c>/<c>jaroSimilarity</c>/<c>byteHammingDistance</c>,
/// <c>substringIndex</c>, <c>countSubstrings</c>, <c>extractGroups</c> and the four
/// <c>normalizeUTF8*</c> forms), and the numeric functions
/// <c>log2</c>/<c>cbrt</c>/<c>exp2</c>/<c>exp10</c>, the special-function surface
/// <c>erf</c>/<c>erfc</c>/<c>lgamma</c>/<c>tgamma</c>/<c>sigmoid</c>/<c>sqr</c>/<c>hypot</c>, the
/// integral helpers <c>gcd</c>/<c>lcm</c>/<c>intDiv</c>/<c>intExp2</c>/<c>intExp10</c> and the
/// <c>isFinite</c>/<c>isInfinite</c>/<c>isNaN</c> predicates, and the rounding family
/// (<c>roundBankers</c>, <c>roundDown</c>, <c>roundDuration</c>, <c>roundAge</c>, <c>roundToExp2</c>)
/// with its vendor bucket tables and half-to-even rule, and the date/time family
/// (<c>makeDate</c>/<c>makeDate32</c>/<c>makeDateTime</c>, <c>now64</c>/<c>nowInBlock</c>,
/// <c>timeSlot</c>/<c>timeSlots</c>, the sub-hour <c>toStartOf*</c> truncations and
/// <c>toStartOfInterval</c>, the ISO/week/year-week accessors, <c>toModifiedJulianDay</c>,
/// <c>age</c>/<c>dateName</c>, <c>serverTimezone</c>/<c>timezoneOf</c>/<c>timezoneOffset</c>,
/// <c>toTime</c>, <c>timestamp</c> and the <c>fromUnixTimestamp*</c> forms), with three tested
/// CLR-shape refusals (<c>fromUTCTimestamp</c>, <c>toTime64</c>, <c>fromUnixTimestamp64Nano</c>).
/// The catalog's numeric operator forms
/// (<c>plus</c>/<c>minus</c>/<c>multiply</c>/<c>divide</c>/<c>negate</c>) and the constant <c>e</c> are
/// deliberately *not* members: they are the portable CLR operators and the <see cref="Math.E"/>
/// constant, which the shared translator renders as the identical ClickHouse function/expression.
/// Exposed through <see cref="SqlFunctions.ClickHouse"/>; every member is gated by a capability flag
/// and rejected by providers that do not opt in.
/// </summary>
    public class ClickHouseFunctions : CommonFunctions
    {
        /// <summary>
        /// The host name of the server that answered the query (<c>hostName()</c>). Use <see cref="tcp_port"/>
        /// for the listening port. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SessionInfoFunctions"/>; ClickHouse).
        /// </summary>
        public string host_name() => default!;

        /// <summary>
        /// The TCP port the server is listening on (<c>CAST(tcpPort() AS Int32)</c>). Requires a provider
        /// that supports it (see <see cref="ISqlDialect.SessionInfoFunctions"/>; ClickHouse).
        /// </summary>
        public int tcp_port() => default!;

        /// <summary>
        /// <c>argMin(value, by)</c>: the <paramref name="value"/> of the row where <paramref name="by"/>
        /// is the smallest. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsArgMinMax"/>; ClickHouse).
        /// </summary>
        public TValue? arg_min<TValue, TBy>(TValue? value, TBy? by) => default!;

        /// <summary><c>argMax(value, by)</c>: the <paramref name="value"/> of the row where <paramref name="by"/> is the largest.</summary>
        public TValue? arg_max<TValue, TBy>(TValue? value, TBy? by) => default!;

        /// <summary>
        /// <c>uniq(value)</c>: the approximate number of distinct values. Requires a provider that
        /// supports the distinct-count family (see <see cref="ISqlDialect.UniqAggregates"/>; ClickHouse).
        /// </summary>
        public long uniq<T>(T? value) => default!;

        /// <summary><c>uniqExact(value)</c>: the exact number of distinct values.</summary>
        public long uniq_exact<T>(T? value) => default!;

        /// <summary><c>uniqCombined(value)</c>: the approximate distinct count (combined algorithm).</summary>
        public long uniq_combined<T>(T? value) => default!;

        /// <summary><c>uniqHLL12(value)</c>: the approximate distinct count using HLL(12).</summary>
        public long uniq_hll12<T>(T? value) => default!;

        /// <summary>
        /// <c>quantile(level)(value)</c>: the approximate <paramref name="level"/> quantile
        /// (0..1). Requires a provider that supports the quantile family (see
        /// <see cref="ISqlDialect.QuantileAggregates"/>; ClickHouse).
        /// </summary>
        public double? quantile<T>(double level, T? value) => default!;

        /// <summary><c>quantileExact(level)(value)</c>: the exact <paramref name="level"/> quantile.</summary>
        public double? quantile_exact<T>(double level, T? value) => default!;

        /// <summary><c>quantileTiming(level)(value)</c>: the approximate quantile optimised for timing data.</summary>
        public double? quantile_timing<T>(double level, T? value) => default!;

        /// <summary><c>median(value)</c>: the median (the <c>0.5</c> quantile).</summary>
        public double? median<T>(T? value) => default!;

        /// <summary>
        /// <c>quantiles(level1, level2, ...)(value)</c>: the approximate quantiles at every
        /// <paramref name="levels"/> value in one pass. The native result is <c>Array(Float64)</c>,
        /// surfaced as <c>double[]</c> and projected directly. Requires a provider that supports the
        /// quantile family (see <see cref="ISqlDialect.QuantileAggregates"/>; ClickHouse). The levels
        /// must be an inline array; a captured array is rejected.
        /// </summary>
        public double[] quantiles<T>(double[] levels, T? value) => default!;

        /// <summary>
        /// <c>topK(k)(value)</c>: the approximately most frequent values, sorted by descending
        /// approximate frequency. The native result is <c>Array(T)</c>, surfaced as <c>T[]</c> and
        /// projected directly (or used as the operand of an array function such as
        /// <see cref="array_sort{T}(T[])"/>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.TopKAggregates"/>; ClickHouse). The result is approximate.
        /// </summary>
        public T[] top_k<T>(long k, T? value) => default!;

        /// <summary>
        /// <c>topKWeighted(k)(value, weight)</c>: the values with the largest approximate sum of
        /// <paramref name="weight"/>, surfaced as <c>T[]</c>. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.TopKAggregates"/>; ClickHouse). The result is approximate.
        /// </summary>
        public T[] top_k_weighted<T, TWeight>(long k, T? value, TWeight? weight) => default!;

        /// <summary>
        /// <c>anyLast(value)</c>: the <paramref name="value"/> of an arbitrary last row. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsAnyAggregates"/>; ClickHouse).
        /// The arbitrary-value aggregate without the last-row restriction is the cross-provider
        /// <see cref="CommonFunctions.any_agg{T}"/>.
        /// </summary>
        public T? any_last<T>(T? value) => default!;

        /// <summary>
        /// <c>windowFunnel(window)(timestamp, cond1, cond2, ...)</c>: the maximum number of consecutive
        /// conditions satisfied within the sliding <paramref name="window"/> (in units of
        /// <paramref name="timestamp"/>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SequenceAggregates"/>; ClickHouse). The conditions must be
        /// inline expressions; a captured condition array is rejected.
        /// </summary>
        public int window_funnel<TTime>(long window, TTime? timestamp, params bool[] conditions) => default!;

        /// <summary>
        /// <c>sequenceMatch(pattern)(timestamp, cond1, cond2, ...)</c>: <c>1</c> when the event chain
        /// matches the <paramref name="pattern"/> (for example <c>"(?1).*(?2)"</c>), otherwise <c>0</c>.
        /// Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SequenceAggregates"/>; ClickHouse). The conditions must be
        /// inline expressions.
        /// </summary>
        public int sequence_match<TTime>(string? pattern, TTime? timestamp, params bool[] conditions) => default!;

        /// <summary>
        /// <c>retention(cond1, cond2, ...)</c>: the 1/0 condition mask (the first condition, then the
        /// first-and-second, and so on). Returns an array, so it can be projected directly or used as
        /// the operand of another array function (for example <see cref="length{T}(T[])"/> or
        /// <see cref="array_string_concat{T}(T[], string?)"/>). Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SequenceAggregates"/>; ClickHouse). The conditions must be
        /// inline expressions.
        /// </summary>
        public int[] retention(params bool[] conditions) => default!;

        /// <summary>
        /// <c>groupArray(value)</c>: aggregates the values of a group into an array. The native result
        /// is <c>Array(T)</c>, surfaced as <c>T[]</c> and projected directly (or used as the operand of
        /// an array function such as <see cref="array_sort{T}(T[])"/>). Requires a provider that
        /// supports the array surface (see <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// The element order is unspecified; sort or concatenate in SQL for a stable result.
        /// </summary>
        public T[] group_array<T>(T? value) => default!;

        /// <summary>
        /// <c>groupUniqArray(value)</c>: aggregates the distinct values of a group into an array. The
        /// native result is <c>Array(T)</c>, surfaced as <c>T[]</c> and projected directly. Requires a
        /// provider that supports the array surface (see
        /// <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse). The element order is
        /// unspecified.
        /// </summary>
        public T[] group_uniq_array<T>(T? value) => default!;

        /// <summary>
        /// <c>JSONExtractString(json, path)</c>: the string at <paramref name="path"/>. JSON is stored in
        /// a text column; requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string? json_extract_string(string? json, string? path) => default!;

        /// <summary><c>JSONExtractInt(json, path)</c>: the 64-bit integer at <paramref name="path"/>.</summary>
        public long json_extract_int(string? json, string? path) => default!;

        /// <summary><c>JSONExtractFloat(json, path)</c>: the double at <paramref name="path"/>.</summary>
        public double json_extract_float(string? json, string? path) => default!;

        /// <summary><c>JSONExtractBool(json, path)</c>: the boolean at <paramref name="path"/>.</summary>
        public bool json_extract_bool(string? json, string? path) => default!;

        /// <summary><c>JSONExtractRaw(json, path)</c>: the raw JSON fragment (object/array) at <paramref name="path"/>.</summary>
        public string? json_extract_raw(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSONExtractKeys(json)</c>: the object keys, surfaced as <c>string[]</c> and projected
        /// directly (or used as the operand of another array function). Requires a provider that
        /// supports the string-JSON family (see <see cref="ISqlDialect.SupportsJsonExtract"/>;
        /// ClickHouse).
        /// </summary>
        public string[] json_extract_keys(string? json) => default!;

        /// <summary><c>JSONExtractKeys(json, path)</c>: as <see cref="json_extract_keys(string?)"/> at <paramref name="path"/>.</summary>
        public string[] json_extract_keys(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSONExtractArrayRaw(json)</c>: the array elements, each as raw JSON text, surfaced as
        /// <c>string[]</c> and projected directly. Requires a provider that supports the string-JSON
        /// family (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string[] json_extract_array_raw(string? json) => default!;

        /// <summary><c>JSONExtractArrayRaw(json, path)</c>: as <see cref="json_extract_array_raw(string?)"/> at <paramref name="path"/>.</summary>
        public string[] json_extract_array_raw(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSONExtractKeysAndValues(json, value_type)</c>: the object key/value pairs, surfaced as
        /// <c>Tuple&lt;string, T&gt;[]</c> and projected directly. Requires a provider that supports the
        /// string-JSON family (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        /// <typeparam name="T">The ClickHouse value type, named in SQL through the dialect type map; it
        /// must be a non-nullable CLR type.</typeparam>
        public Tuple<string, T>[] json_extract_keys_and_values<T>(string? json) => default!;

        /// <summary><c>JSONExtractKeysAndValues(json, path, value_type)</c>: as <see cref="json_extract_keys_and_values{T}(string?)"/> at <paramref name="path"/>.</summary>
        /// <typeparam name="T">The ClickHouse value type; it must be a non-nullable CLR type.</typeparam>
        public Tuple<string, T>[] json_extract_keys_and_values<T>(string? json, string? path) => default!;

        /// <summary><c>JSONHas(json, path)</c>: true when <paramref name="path"/> exists.</summary>
        public bool json_has(string? json, string? path) => default!;

        /// <summary><c>JSONLength(json, path)</c>: the length of the array/object at <paramref name="path"/>.</summary>
        public long json_length(string? json, string? path) => default!;

        /// <summary><c>JSONType(json, path)</c>: the type name of the value at <paramref name="path"/>.</summary>
        public string? json_type(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSON_VALUE(json, path)</c>: the scalar value at the JSONPath <paramref name="path"/>
        /// (for example <c>$.a[0]</c>). Requires a provider that supports the string-JSON family (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string? json_value(string? json, string? path) => default!;

        /// <summary><c>JSON_QUERY(json, path)</c>: the JSON object/array at the JSONPath <paramref name="path"/> as text.</summary>
        public string? json_query(string? json, string? path) => default!;

        /// <summary><c>JSON_EXISTS(json, path)</c>: true when the JSONPath <paramref name="path"/> exists.</summary>
        public bool json_exists(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSONAllPaths(json)</c>: the list of paths stored in the native ClickHouse <c>JSON</c>
        /// value. The native result is <c>Array(String)</c>, surfaced as <c>string[]</c> and projected
        /// directly (or used as the operand of an array function such as
        /// <see cref="length{T}(T[])"/>). <paramref name="json"/> must be a native ClickHouse <c>JSON</c>
        /// value; a <c>String</c> column needs <c>CAST(col AS JSON)</c> first. Requires a provider that
        /// supports the JSON functions (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string[] json_all_paths(string? json) => default!;

        /// <summary>
        /// <c>JSONAllPathsWithTypes(json)</c>: the paths stored in the native ClickHouse <c>JSON</c>
        /// value together with their data types. The native result is <c>Map(String, String)</c>,
        /// surfaced as <c>Dictionary&lt;string, string&gt;</c> and projected directly; use ClickHouse
        /// <c>mapKeys</c>/<c>mapValues</c> to turn it into a collection. <paramref name="json"/> must be
        /// a native ClickHouse <c>JSON</c> value; a <c>String</c> column needs <c>CAST(col AS JSON)</c>
        /// first. Requires a provider that supports the JSON functions (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public Dictionary<string, string> json_all_paths_with_types(string? json) => default!;

        /// <summary>
        /// <c>toJSONString(value)</c>: serialises <paramref name="value"/> to its JSON text
        /// representation. Requires a provider that supports the JSON functions (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string? to_json_string<T>(T? value) => default!;

        /// <summary>
        /// <c>JSONMergePatch(json1, json2)</c>: merges the RFC 7396 merge patch
        /// <paramref name="json2"/> into <paramref name="json1"/> and returns the merged JSON object as
        /// text (for example <c>{"a":1}</c> patched with <c>{"a":2,"b":3}</c> gives
        /// <c>{"a":2,"b":3}</c>). Both arguments must be valid JSON object strings: a malformed
        /// argument or a SQL <c>NULL</c> is a server error (<c>BAD_ARGUMENTS</c>), never an empty
        /// document. Requires a provider that supports the string-JSON family (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string json_merge_patch(string? json1, string? json2) => default!;

        /// <summary>
        /// <c>JSONMergePatch(json1, json2, json3)</c>: merges three JSON objects left to right (the
        /// vendor accepts one or more operands; the surface exposes the two- and three-operand forms).
        /// A malformed or SQL <c>NULL</c> operand is a server error (<c>BAD_ARGUMENTS</c>).
        /// </summary>
        public string json_merge_patch(string? json1, string? json2, string? json3) => default!;

        /// <summary>
        /// <c>JSONArrayLength(json)</c>: the number of elements in the outermost JSON array, or
        /// <see langword="null"/> when <paramref name="json"/> is not a valid JSON array — an object,
        /// a scalar, empty text, malformed JSON, or SQL <c>NULL</c> all yield <c>NULL</c>. The native
        /// result is <c>Nullable(UInt64)</c>, lowered to <c>Nullable(Int64)</c> so it maps to
        /// <see cref="long"/>? — the null is preserved and is never replaced by <c>0</c>. Requires a
        /// provider that supports the string-JSON family (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public long? json_array_length(string? json) => default!;

        /// <summary>
        /// <c>JSONDynamicPaths(json)</c>: the paths stored as separate subcolumns of a native
        /// ClickHouse <c>JSON</c> value, as <c>Array(String)</c> surfaced as <c>string[]</c>.
        /// <paramref name="json"/> must be a native <c>JSON</c>-typed expression: a <c>JSON</c> column
        /// (whether mapped as a string or as a JSON DOM) is read directly, and a text value needs
        /// <c>CAST(text AS JSON)</c>. A plain <c>String</c> literal or parameter is rejected by the
        /// server (<c>NUMBER_OF_ARGUMENTS_DOESNT_MATCH</c>), a <c>Nullable(JSON)</c> column is
        /// rejected because <c>Array(String)</c> cannot be nested in <c>Nullable</c>
        /// (<c>ILLEGAL_TYPE_OF_ARGUMENT</c>), and a SQL <c>NULL</c> JSON value yields <c>NULL</c>.
        /// Requires a provider that supports the JSON functions (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string[] json_dynamic_paths(string? json) => default!;

        /// <summary>
        /// <c>JSONDynamicPathsWithTypes(json)</c>: the dynamic paths of a native ClickHouse <c>JSON</c>
        /// value together with their data types, as <c>Map(String, String)</c> surfaced as
        /// <see cref="Dictionary{TKey,TValue}"/>. The argument rules are those of
        /// <see cref="json_dynamic_paths(string?)"/>; an empty map means the row stores no dynamic
        /// path. Requires a provider that supports the JSON functions (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public Dictionary<string, string> json_dynamic_paths_with_types(string? json) => default!;

        /// <summary>
        /// <c>JSONSharedDataPaths(json)</c>: the paths stored in the shared data structure of a native
        /// ClickHouse <c>JSON</c> value, as <c>Array(String)</c> surfaced as <c>string[]</c>. The
        /// argument rules and refusals are those of <see cref="json_dynamic_paths(string?)"/>. Requires
        /// a provider that supports the JSON functions (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string[] json_shared_data_paths(string? json) => default!;

        /// <summary>
        /// <c>JSONSharedDataPathsWithTypes(json)</c>: the shared-data paths of a native ClickHouse
        /// <c>JSON</c> value together with their data types, as <c>Map(String, String)</c> surfaced as
        /// <see cref="Dictionary{TKey,TValue}"/>. The argument rules and refusals are those of
        /// <see cref="json_dynamic_paths(string?)"/>. Requires a provider that supports the JSON
        /// functions (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public Dictionary<string, string> json_shared_data_paths_with_types(string? json) => default!;

        /// <summary>
        /// <c>JSONKey(json)</c>: the name of a key in a JSON object, or an empty string when the value
        /// has no key. The argument may be a text JSON value or a native <c>JSON</c> value; SQL
        /// <c>NULL</c> yields <c>NULL</c>. Requires a provider that supports the string-JSON family
        /// (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string? json_key(string? json) => default!;

        /// <summary>
        /// <c>JSONKey(json, key)</c>: the key name reached through the single object key
        /// <paramref name="key"/>, or an empty string when it does not exist. Use
        /// <see cref="json_key(string?, int)"/> to address an array position or object member by its
        /// 1-based index.
        /// </summary>
        public string? json_key(string? json, string? key) => default!;

        /// <summary>
        /// <c>JSONKey(json, index)</c>: the key name at the 1-based <paramref name="index"/>; indices
        /// outside the object/array yield an empty string, not a default key name. SQL <c>NULL</c>
        /// yields <c>NULL</c>.
        /// </summary>
        public string? json_key(string? json, int index) => default!;

        /// <summary>
        /// <c>JSONExtractKeysAndValuesRaw(json)</c>: the object key/value pairs of the whole document,
        /// with every value kept as unparsed JSON text, surfaced as
        /// <c>Tuple&lt;string, string&gt;[]</c>. Malformed JSON or a non-object document yields an
        /// empty array; SQL <c>NULL</c> yields <c>NULL</c>. Requires a provider that supports the
        /// string-JSON family (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public Tuple<string, string>[] json_extract_keys_and_values_raw(string? json) => default!;

        /// <summary>
        /// <c>JSONExtractKeysAndValuesRaw(json, path)</c>: as
        /// <see cref="json_extract_keys_and_values_raw(string?)"/> at <paramref name="path"/>.
        /// </summary>
        public Tuple<string, string>[] json_extract_keys_and_values_raw(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSONExtractUInt(json, path)</c>: the unsigned 64-bit integer at
        /// <paramref name="path"/>, or <c>0</c> when the path is missing, the value has another type,
        /// or the document is malformed — the vendor default, asserted rather than replaced by the
        /// library. SQL <c>NULL</c> yields <c>NULL</c>, never <c>0</c>, so the result is
        /// <see cref="ulong"/>?. Requires a provider that supports the string-JSON family (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public ulong? json_extract_uint(string? json, string? path) => default!;

        /// <summary>
        /// <c>JSONExtractUIntOrDefault(json, path, default)</c> is refused: ClickHouse 25.8.33.6 has
        /// no such function (it is absent from <c>system.functions</c> and the name fails with
        /// <c>UNKNOWN_FUNCTION</c>), so no SQL that executes on the pinned server can be emitted.
        /// <see cref="json_extract_uint(string?, string?)"/> already returns the vendor default
        /// <c>0</c> for a missing path, a wrong value type, or malformed JSON and <c>NULL</c> for a
        /// SQL <c>NULL</c>; a default argument would invent a semantic the server does not define.
        /// </summary>
        public ulong? json_extract_uint_or_default(string? json, string? path, ulong defaultValue) =>
            throw new NotSupportedException(
                "The json_extract_uint_or_default function is refused: no JSONExtractUIntOrDefault function exists in ClickHouse 25.8.33.6.");

        /// <summary>
        /// <c>isValidJSON(json)</c>: <see langword="true"/> for well-formed JSON and
        /// <see langword="false"/> for malformed text (including the empty string); SQL <c>NULL</c>
        /// yields <c>NULL</c>, never <see langword="false"/>. The native <c>UInt8</c> result is
        /// surfaced as <c>Nullable(UInt8)</c> → <see cref="bool"/>. Requires a provider that supports
        /// the string-JSON family (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public bool? is_valid_json(string? json) => default!;

        /// <summary>
        /// <c>simpleJSONExtractRaw(json, field)</c>: the raw JSON fragment of the flat
        /// <paramref name="field"/> key, kept as unparsed text. The simple-JSON contract is the
        /// vendor fast path: the field name is a constant, fields are searched at any nesting level,
        /// and the first match wins. A missing field, a wrong value type or malformed input yields an
        /// empty string; SQL <c>NULL</c> yields <c>NULL</c>. Requires a provider that supports the
        /// string-JSON family (see <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string? simple_json_extract_raw(string? json, string? field) => default!;

        /// <summary>
        /// <c>simpleJSONExtractString(json, field)</c>: the string value of the flat
        /// <paramref name="field"/> key, or an empty string when it is missing, has another type, or
        /// the input is malformed; SQL <c>NULL</c> yields <c>NULL</c>. The vendor fast-path
        /// assumptions of <see cref="simple_json_extract_raw(string?, string?)"/> apply.
        /// </summary>
        public string? simple_json_extract_string(string? json, string? field) => default!;

        /// <summary>
        /// <c>simpleJSONExtractInt(json, field)</c>: the signed 64-bit integer of the flat
        /// <paramref name="field"/> key, or <c>0</c> when it is missing, has another type, or the
        /// input is malformed; SQL <c>NULL</c> yields <c>NULL</c>. The vendor fast-path assumptions
        /// of <see cref="simple_json_extract_raw(string?, string?)"/> apply.
        /// </summary>
        public long? simple_json_extract_int(string? json, string? field) => default!;

        /// <summary>
        /// <c>simpleJSONExtractUInt(json, field)</c>: the unsigned 64-bit integer of the flat
        /// <paramref name="field"/> key, or <c>0</c> when it is missing, negative, has another type,
        /// or the input is malformed; SQL <c>NULL</c> yields <c>NULL</c>. The vendor fast-path
        /// assumptions of <see cref="simple_json_extract_raw(string?, string?)"/> apply.
        /// </summary>
        public ulong? simple_json_extract_uint(string? json, string? field) => default!;

        /// <summary>
        /// <c>simpleJSONExtractFloat(json, field)</c>: the <c>Float64</c> value of the flat
        /// <paramref name="field"/> key, or <c>0</c> when it is missing, has another type, or the
        /// input is malformed; SQL <c>NULL</c> yields <c>NULL</c>. The vendor fast-path assumptions
        /// of <see cref="simple_json_extract_raw(string?, string?)"/> apply.
        /// </summary>
        public double? simple_json_extract_float(string? json, string? field) => default!;

        /// <summary>
        /// <c>simpleJSONExtractBool(json, field)</c>: the boolean value of the flat
        /// <paramref name="field"/> key, or <see langword="false"/> when it is missing, has another
        /// type, or the input is malformed; SQL <c>NULL</c> yields <c>NULL</c>, never
        /// <see langword="false"/>. The vendor fast-path assumptions of
        /// <see cref="simple_json_extract_raw(string?, string?)"/> apply.
        /// </summary>
        public bool? simple_json_extract_bool(string? json, string? field) => default!;

        /// <summary>
        /// <c>prettyPrintJSON(json)</c> is refused: the function was introduced in ClickHouse 26.4 and
        /// does not exist on the pinned 25.8.33.6 server (it is absent from <c>system.functions</c> and
        /// the name fails with <c>UNKNOWN_FUNCTION</c>), so any SQL emitted for it would be rejected by
        /// the target server and no documented equivalent composition exists.
        /// </summary>
        public string? pretty_print_json(string? json) =>
            throw new NotSupportedException(
                "The pretty_print_json function is refused: prettyPrintJSON was introduced in ClickHouse 26.4 and does not exist on the pinned 25.8.33.6 server.");

        /// <summary>
        /// <c>visitParamExtractString(json, name)</c>: the string value of the flat <paramref name="name"/>
        /// key. Requires a provider that supports the string-JSON family (see
        /// <see cref="ISqlDialect.SupportsJsonExtract"/>; ClickHouse).
        /// </summary>
        public string? visit_param_extract_string(string? json, string? name) => default!;

        /// <summary><c>visitParamExtractInt(json, name)</c>: the 64-bit integer of the flat <paramref name="name"/> key.</summary>
        public long visit_param_extract_int(string? json, string? name) => default!;

        /// <summary><c>visitParamExtractFloat(json, name)</c>: the double of the flat <paramref name="name"/> key.</summary>
        public double visit_param_extract_float(string? json, string? name) => default!;

        /// <summary><c>visitParamExtractBool(json, name)</c>: the boolean of the flat <paramref name="name"/> key.</summary>
        public bool visit_param_extract_bool(string? json, string? name) => default!;

        /// <summary><c>visitParamExtractRaw(json, name)</c>: the raw fragment of the flat <paramref name="name"/> key.</summary>
        public string? visit_param_extract_raw(string? json, string? name) => default!;

        /// <summary>
        /// <c>dictGet('dict', 'attribute', id)</c>: the attribute value for <paramref name="id"/> from the
        /// dictionary. Requires a provider that supports dictionaries (see
        /// <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse). The value type is the dictionary
        /// attribute type, specified by the caller.
        /// </summary>
        public TValue? dict_get<TValue, TKey>(string? dict, string? attribute, TKey? id) => default!;

        /// <summary><c>dictGetOrDefault('dict', 'attribute', id, default)</c>: as <c>dict_get</c> but falls back to <paramref name="defaultValue"/>.</summary>
        public TValue? dict_get_or_default<TValue, TKey>(string? dict, string? attribute, TKey? id, TValue? defaultValue) => default!;

        /// <summary><c>dictHas('dict', id)</c>: true when the dictionary contains <paramref name="id"/>.</summary>
        public bool dict_has<TKey>(string? dict, TKey? id) => default!;

        /// <summary>
        /// <c>dictGetHierarchy('dict', key)</c>: the key and all its parents in a hierarchical dictionary,
        /// from the key up to the root. Requires a provider that supports dictionaries (see
        /// <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public ulong[] dict_get_hierarchy<TKey>(string? dict, TKey? id) => default!;

        /// <summary>
        /// <c>dictGetChildren('dict', key)</c>: the first-level children of <paramref name="id"/> in a
        /// hierarchical dictionary. Requires a provider that supports dictionaries (see
        /// <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public ulong[] dict_get_children<TKey>(string? dict, TKey? id) => default!;

        /// <summary>
        /// <c>dictIsIn('dict', child, ancestor)</c>: true when <paramref name="childId"/> is
        /// <paramref name="ancestorId"/> or a descendant of it in a hierarchical dictionary. Requires a
        /// provider that supports dictionaries (see <see cref="ISqlDialect.SupportsDictionaries"/>;
        /// ClickHouse).
        /// </summary>
        public bool dict_is_in<TKey>(string? dict, TKey? childId, TKey? ancestorId) => default!;

        /// <summary>
        /// <c>dictGetOrNull('dict', 'attribute', id)</c>: as <see cref="dict_get{TValue, TKey}"/> but
        /// returns <c>NULL</c> (a null <typeparamref name="TValue"/>) for an absent key instead of the
        /// attribute's configured default value. For a value-type attribute pass the nullable CLR type
        /// (for example <c>ulong?</c>) as <typeparamref name="TValue"/>, so an absent key materialises
        /// as <see langword="null"/> rather than the value type's zero. Requires a provider that
        /// supports dictionaries (see <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public TValue? dict_get_or_null<TValue, TKey>(string? dict, string? attribute, TKey? id) => default!;

        /// <summary>
        /// <c>dictGetAll('dict', 'attribute', id)</c>: every attribute value matching <paramref name="id"/>
        /// in a <c>REGEXP_TREE</c> dictionary, as the declared <typeparamref name="TValue"/> array. An
        /// absent key yields an empty array, never a substituted default. Requires a provider that
        /// supports dictionaries (see <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public TValue[] dict_get_all<TValue, TKey>(string? dict, string? attribute, TKey? id) => default!;

        /// <summary>
        /// <c>dictGetAll('dict', 'attribute', id, limit)</c>: as
        /// <see cref="dict_get_all{TValue, TKey}(string, string, TKey)"/> truncated to
        /// <paramref name="limit"/> values (the vendor gives child matches precedence when truncating).
        /// </summary>
        public TValue[] dict_get_all<TValue, TKey>(string? dict, string? attribute, TKey? id, ulong limit) => default!;

        /// <summary>
        /// <c>dictGetDescendants('dict', key)</c>: every descendant of <paramref name="id"/> in a
        /// hierarchical dictionary, at any depth. Requires a provider that supports dictionaries (see
        /// <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public ulong[] dict_get_descendants<TKey>(string? dict, TKey? id) => default!;

        /// <summary>
        /// <c>dictGetDescendants('dict', key, level)</c>: as
        /// <see cref="dict_get_descendants{TKey}(string, TKey)"/> limited to <paramref name="level"/>
        /// recursive steps; <c>0</c> means all descendants to the end.
        /// </summary>
        public ulong[] dict_get_descendants<TKey>(string? dict, TKey? id, byte level) => default!;

        /// <summary>
        /// <c>dictGetKeys('dict', 'attribute', value)</c> is refused: the reverse-lookup function was
        /// introduced in ClickHouse 25.12 and is absent from the pinned 25.8.33.6 server (it fails with
        /// <c>UNKNOWN_FUNCTION</c>), so no SQL that executes on the target server can be emitted.
        /// </summary>
        public TKey[] dict_get_keys<TKey, TValue>(string? dict, string? attribute, TValue? value) =>
            throw new NotSupportedException(
                "The dict_get_keys function is refused: dictGetKeys was introduced in ClickHouse 25.12 " +
                "and does not exist on the pinned 25.8.33.6 server (absent from system.functions; the name " +
                "is rejected with UNKNOWN_FUNCTION). No documented equivalent reverse lookup exists for " +
                "this server version.");

        /// <summary>
        /// <c>dictGetRoot('dict', key)</c> is refused: the root accessor was introduced in ClickHouse
        /// 26.7 and is absent from the pinned 25.8.33.6 server (it fails with <c>UNKNOWN_FUNCTION</c>);
        /// use <see cref="dict_get_hierarchy{TKey}(string, TKey)"/> and take its last element.
        /// </summary>
        public ulong dict_get_root<TKey>(string? dict, TKey? id) =>
            throw new NotSupportedException(
                "The dict_get_root function is refused: dictGetRoot was introduced in ClickHouse 26.7 and " +
                "does not exist on the pinned 25.8.33.6 server (absent from system.functions; the name is " +
                "rejected with UNKNOWN_FUNCTION). Use dict_get_hierarchy and take its last element instead.");

        /// <summary>
        /// <c>regionToName(id[, lang])</c>: the name of the region in the requested language (or the
        /// server's default geobase language). A missing region yields an empty string, as the vendor
        /// defines; an unsupported language is rejected by the server. Requires the embedded geobase
        /// dictionaries to be configured on the server (see
        /// <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public string region_to_name(uint id) => default!;

        /// <summary><c>regionToName(id, lang)</c>: as <see cref="region_to_name(uint)"/> with an explicit geobase language.</summary>
        public string region_to_name(uint id, string lang) => default!;

        /// <summary><c>regionToCity(id)</c>: the id of the city that contains the region, or <c>0</c> when there is none.</summary>
        public uint region_to_city(uint id) => default!;

        /// <summary><c>regionToCity(id, geobase)</c>: as <see cref="region_to_city(uint)"/> for the named geobase.</summary>
        public uint region_to_city(uint id, string geobase) => default!;

        /// <summary><c>regionToArea(id)</c>: the id of the area (vendor region type 5) that contains the region, or <c>0</c>.</summary>
        public uint region_to_area(uint id) => default!;

        /// <summary><c>regionToArea(id, geobase)</c>: as <see cref="region_to_area(uint)"/> for the named geobase.</summary>
        public uint region_to_area(uint id, string geobase) => default!;

        /// <summary><c>regionToDistrict(id)</c>: the id of the federal district (vendor region type 4) that contains the region, or <c>0</c>.</summary>
        public uint region_to_district(uint id) => default!;

        /// <summary><c>regionToDistrict(id, geobase)</c>: as <see cref="region_to_district(uint)"/> for the named geobase.</summary>
        public uint region_to_district(uint id, string geobase) => default!;

        /// <summary><c>regionToCountry(id)</c>: the id of the country (vendor region type 3) that contains the region, or <c>0</c>.</summary>
        public uint region_to_country(uint id) => default!;

        /// <summary><c>regionToCountry(id, geobase)</c>: as <see cref="region_to_country(uint)"/> for the named geobase.</summary>
        public uint region_to_country(uint id, string geobase) => default!;

        /// <summary><c>regionToContinent(id)</c>: the id of the nearest continent (vendor region type 1) ancestor of the region, or <c>0</c>.</summary>
        public uint region_to_continent(uint id) => default!;

        /// <summary><c>regionToContinent(id, geobase)</c>: as <see cref="region_to_continent(uint)"/> for the named geobase.</summary>
        public uint region_to_continent(uint id, string geobase) => default!;

        /// <summary><c>regionToTopContinent(id)</c>: the id of the highest continent ancestor of the region, or <c>0</c>.</summary>
        public uint region_to_top_continent(uint id) => default!;

        /// <summary><c>regionToTopContinent(id, geobase)</c>: as <see cref="region_to_top_continent(uint)"/> for the named geobase.</summary>
        public uint region_to_top_continent(uint id, string geobase) => default!;

        /// <summary><c>regionToPopulation(id)</c>: the population recorded for the region in the geobase, or <c>0</c> when it is not recorded.</summary>
        public uint region_to_population(uint id) => default!;

        /// <summary><c>regionToPopulation(id, geobase)</c>: as <see cref="region_to_population(uint)"/> for the named geobase.</summary>
        public uint region_to_population(uint id, string geobase) => default!;

        /// <summary>
        /// <c>regionHierarchy(id)</c>: the region and all its parents down to the root, as region ids.
        /// Requires the embedded geobase dictionaries (see <see cref="ISqlDialect.SupportsDictionaries"/>;
        /// ClickHouse).
        /// </summary>
        public uint[] region_hierarchy(uint id) => default!;

        /// <summary><c>regionHierarchy(id, geobase)</c>: as <see cref="region_hierarchy(uint)"/> for the named geobase.</summary>
        public uint[] region_hierarchy(uint id, string geobase) => default!;

        /// <summary>
        /// <c>regionIn(lhs, rhs)</c>: <c>true</c> when the <paramref name="lhs"/> region is
        /// <paramref name="rhs"/> or one of its descendants (the relationship is reflexive). Requires the
        /// embedded geobase dictionaries (see <see cref="ISqlDialect.SupportsDictionaries"/>; ClickHouse).
        /// </summary>
        public bool region_in(uint lhs, uint rhs) => default!;

        /// <summary><c>regionIn(lhs, rhs, geobase)</c>: as <see cref="region_in(uint, uint)"/> for the named geobase.</summary>
        public bool region_in(uint lhs, uint rhs, string geobase) => default!;

        /// <summary>
        /// <c>regionToParent(id)</c> is refused: no such function exists in ClickHouse 25.8.33.6 (it
        /// fails with <c>UNKNOWN_FUNCTION</c>); use <see cref="region_hierarchy(uint)"/> and skip the
        /// first element.
        /// </summary>
        public uint region_to_parent(uint id) =>
            throw new NotSupportedException(
                "The region_to_parent function is refused: ClickHouse 25.8.33.6 has no regionToParent " +
                "function (absent from system.functions; the name is rejected with UNKNOWN_FUNCTION). " +
                "Use region_hierarchy and skip the first element instead.");

        /// <summary>
        /// <c>regionToParents(id)</c> is refused: no such function exists in ClickHouse 25.8.33.6 (it
        /// fails with <c>UNKNOWN_FUNCTION</c>); use <see cref="region_hierarchy(uint)"/> and skip the
        /// first element.
        /// </summary>
        public uint[] region_to_parents(uint id) =>
            throw new NotSupportedException(
                "The region_to_parents function is refused: ClickHouse 25.8.33.6 has no regionToParents " +
                "function (absent from system.functions; the name is rejected with UNKNOWN_FUNCTION). " +
                "Use region_hierarchy and skip the first element instead.");

        /// <summary>
        /// <c>column GLOBAL IN (subquery)</c>: like <c>IN</c>, but the right-hand result is sent to every
        /// node of a distributed cluster. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsGlobalPredicates"/>; ClickHouse). Negate with C# <c>!</c> for
        /// <c>GLOBAL NOT IN</c>.
        /// </summary>
        public bool global_in<T>(T column, QueryCommand<T> cmd) => default!;

        /// <summary><c>column GLOBAL IN (values)</c>: the value-list form of <see cref="global_in{T}(T, QueryCommand{T})"/>.</summary>
        public bool global_in<T>(T column, IEnumerable<T> values) => default!;

        /// <summary><c>column GLOBAL IN (values)</c>: the value-list form of <see cref="global_in{T}(T, QueryCommand{T})"/>.</summary>
        public bool global_in<T>(T column, params T[] values) => default!;

        /// <summary>
        /// <c>numbers(count)</c> as a FROM source; select <see cref="SqlFunctions.INumbersRow.Value"/>. Yields
        /// <paramref name="count"/> consecutive integers starting at zero. Requires a provider that supports
        /// table functions (see <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse). Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{T}}})"/>.
        /// </summary>
        [SqlTableFunction("numbers")]
        public IQueryable<SqlFunctions.INumbersRow> numbers(long count) => throw new NotSupportedException();

        /// <summary><c>numbers(start, stop)</c> as a FROM source: integers in <c>[start, stop)</c>.</summary>
        [SqlTableFunction("numbers")]
        public IQueryable<SqlFunctions.INumbersRow> numbers(long start, long stop) => throw new NotSupportedException();

        /// <summary><c>numbers(start, stop, step)</c> as a FROM source.</summary>
        [SqlTableFunction("numbers")]
        public IQueryable<SqlFunctions.INumbersRow> numbers(long start, long stop, long step) => throw new NotSupportedException();

        /// <summary>
        /// <c>numbers_mt(count)</c> as a FROM source: as <see cref="numbers(long)"/> but evaluated with
        /// multiple threads.
        /// </summary>
        [SqlTableFunction("numbers_mt")]
        public IQueryable<SqlFunctions.INumbersRow> numbers_mt(long count) => throw new NotSupportedException();

        /// <summary>
        /// <c>zeros(count)</c> as a FROM source; select <see cref="SqlFunctions.IZerosRow.Value"/>. Yields
        /// <paramref name="count"/> rows whose single <c>zero UInt8</c> column is <c>0</c>. Requires a
        /// provider that supports table functions (see <see cref="ISqlDialect.SupportsTableFunction"/>;
        /// ClickHouse). Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{T}}})"/>.
        /// </summary>
        [SqlTableFunction("zeros")]
        public IQueryable<SqlFunctions.IZerosRow> zeros(long count) => throw new NotSupportedException();

        /// <summary>
        /// <c>zeros_mt(count)</c> as a FROM source: as <see cref="zeros(long)"/> but evaluated with
        /// multiple threads.
        /// </summary>
        [SqlTableFunction("zeros_mt")]
        public IQueryable<SqlFunctions.IZerosRow> zeros_mt(long count) => throw new NotSupportedException();

        /// <summary>
        /// <c>generateRandom(...)</c> as a FROM source; select
        /// <see cref="SqlFunctions.IGenerateRandomRow"/>. Yields an unbounded stream of random rows with
        /// the fixed structure <c>id UInt64, value Float64, name String</c>, so apply a page/limit.
        /// Requires a provider that supports table functions (see
        /// <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse). Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{T}}})"/>.
        /// </summary>
        [SqlTableFunction("generateRandom")]
        public IQueryable<SqlFunctions.IGenerateRandomRow> generate_random() => throw new NotSupportedException();

        /// <summary>
        /// <c>generateRandom(structure, seed)</c> as a FROM source: as
        /// <see cref="generate_random()"/> but with a fixed <paramref name="seed"/> so the generated
        /// values are reproducible.
        /// </summary>
        [SqlTableFunction("generateRandom")]
        public IQueryable<SqlFunctions.IGenerateRandomRow> generate_random(long seed) => throw new NotSupportedException();

        /// <summary>
        /// <c>values(structure, values...)</c> as a FROM source: a temporary table whose result schema is
        /// derived from <typeparamref name="TRow"/>. The structure string (for example
        /// <c>'a UInt8, b String'</c>) is rendered from <typeparamref name="TRow"/>'s mapped properties
        /// and types; <paramref name="tuples"/> is the verbatim row list, for example
        /// <c>"(1, 'x'), (2, 'y')"</c>, emitted after it. Only trusted developer-authored SQL may be
        /// passed as <paramref name="tuples"/>. Requires a provider that supports a typed result schema
        /// (see <see cref="ISqlDialect.SupportsResultSchema(TableFunctionSchema)"/>; ClickHouse). Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{T}}})"/>.
        /// </summary>
        /// <typeparam name="TRow">The caller-declared row shape; its <c>[Column]</c> names/types form the structure.</typeparam>
        /// <param name="tuples">The verbatim tuple list, for example <c>"(1, 'x'), (2, 'y')"</c>.</param>
        [SqlTableFunction("values", ResultSchema = TableFunctionSchema.LeadingArgument, VerbatimArguments = new[] { 0 })]
        public IQueryable<TRow> values<TRow>(string tuples) => throw new NotSupportedException();

        /// <summary>
        /// <c>url(url, format, structure)</c> as a FROM source: reads the resource at
        /// <paramref name="url"/> in <paramref name="format"/> with the column layout
        /// <paramref name="structure"/> (for example <c>'id UInt64, name String'</c>). The row shape is
        /// declared by the caller through <typeparamref name="TRow"/>, whose <c>[Column]</c> names must
        /// match <paramref name="structure"/>. Requires a provider that supports table functions (see
        /// <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse). Use through
        /// <see cref="DataContextExtensions.FromTableFunction{T}(IDataContext, System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{T}}})"/>.
        /// The URL selects the backend: an HTTP(S) URL is fetched directly, a recognised non-HTTP
        /// scheme (<c>file://</c>, <c>s3://</c>, …) is delegated to the matching function.
        /// </summary>
        [SqlTableFunction("url")]
        public IQueryable<TRow> url<TRow>(string url, string format, string structure) => throw new NotSupportedException();

        /// <summary>
        /// <c>s3(url, format, structure)</c> as a FROM source: reads an object from Amazon S3 or Google
        /// Cloud Storage in the given <paramref name="format"/> and column layout; the row shape is
        /// declared by the caller through <typeparamref name="TRow"/>. Requires a provider that supports
        /// table functions (see <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse). Credentials
        /// are taken from the server configuration or a named collection; do not pass secrets in the URL.
        /// </summary>
        [SqlTableFunction("s3")]
        public IQueryable<TRow> s3<TRow>(string url, string format, string structure) => throw new NotSupportedException();

        /// <summary>
        /// <c>file(path, format, structure)</c> as a FROM source: reads a file under the server's
        /// <c>user_files_path</c> in the given <paramref name="format"/> and column layout; the row shape
        /// is declared by the caller through <typeparamref name="TRow"/>. Requires a provider that
        /// supports table functions (see <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse).
        /// The path is resolved by the server and is not a client filesystem path.
        /// </summary>
        [SqlTableFunction("file")]
        public IQueryable<TRow> file<TRow>(string path, string format, string structure) => throw new NotSupportedException();

        /// <summary>
        /// <c>remote(addresses, database, table)</c> as a FROM source: reads <paramref name="table"/> from
        /// the ClickHouse server(s) at <paramref name="addresses"/> (comma-separated <c>host[:port]</c>
        /// list) without creating a distributed table. The row shape is declared by the caller through
        /// <typeparamref name="TRow"/> and must match the remote table. Requires a provider that supports
        /// table functions (see <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse). Credentials
        /// come from the server's <c>remote_servers</c> configuration, never from query arguments.
        /// </summary>
        [SqlTableFunction("remote")]
        public IQueryable<TRow> remote<TRow>(string addresses, string database, string table) => throw new NotSupportedException();

        /// <summary>
        /// <c>remoteSecure(addresses, database, table)</c> as a FROM source: the TLS counterpart of
        /// <see cref="remote{TRow}(string, string, string)"/> (default secure port 9440).
        /// </summary>
        [SqlTableFunction("remoteSecure")]
        public IQueryable<TRow> remote_secure<TRow>(string addresses, string database, string table) => throw new NotSupportedException();

        /// <summary>
        /// <c>cluster(cluster, database, table)</c> as a FROM source: reads <paramref name="table"/> from
        /// one replica of each shard of the configured <paramref name="cluster"/>. The row shape is
        /// declared by the caller through <typeparamref name="TRow"/>. Requires a provider that supports
        /// table functions (see <see cref="ISqlDialect.SupportsTableFunction"/>; ClickHouse). Connection
        /// settings and credentials come from the server's <c>remote_servers</c> configuration.
        /// </summary>
        [SqlTableFunction("cluster")]
        public IQueryable<TRow> cluster<TRow>(string cluster, string database, string table) => throw new NotSupportedException();

        /// <summary>
        /// <c>clusterAllReplicas(cluster, database, table)</c> as a FROM source: like
        /// <see cref="cluster{TRow}(string, string, string)"/> but queries every replica of every shard as
        /// a separate connection.
        /// </summary>
        [SqlTableFunction("clusterAllReplicas")]
        public IQueryable<TRow> cluster_all_replicas<TRow>(string cluster, string database, string table) => throw new NotSupportedException();

        /// <summary>
        /// <c>arrayJoin(array)</c>: expands the array into one row per element. Requires a provider that
        /// supports it (see <see cref="ISqlDialect.SupportsArrayJoin"/>; ClickHouse). The expanded value
        /// can be projected and filtered like a scalar column.
        /// </summary>
        public T array_join<T>(T[] array) => default!;

        /// <summary>
        /// <c>length(array)</c>: the number of elements. Requires a provider that supports array
        /// functions (see <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public long length<T>(T[] array) => default!;

        /// <summary><c>has(array, element)</c>: true when <paramref name="element"/> is in <paramref name="array"/>.</summary>
        public bool has<T>(T[] array, T element) => default!;

        /// <summary><c>indexOf(array, element)</c>: the one-based position of the first occurrence, or <c>0</c> when absent.</summary>
        public long index_of<T>(T[] array, T element) => default!;

        /// <summary><c>hasAny(array, other)</c>: true when the arrays have at least one common element.</summary>
        public bool has_any<T>(T[] array, T[] other) => default!;

        /// <summary><c>hasAll(array, other)</c>: true when every element of <paramref name="other"/> is in <paramref name="array"/>.</summary>
        public bool has_all<T>(T[] array, T[] other) => default!;

        /// <summary>
        /// <c>startsWith(array, prefix)</c>: true when <paramref name="array"/> begins with the
        /// <paramref name="prefix"/> elements, in order. Requires a provider that supports array
        /// functions (see <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public bool starts_with<T>(T[] array, T[] prefix) => default!;

        /// <summary><c>endsWith(array, suffix)</c>: true when <paramref name="array"/> ends with the <paramref name="suffix"/> elements, in order.</summary>
        public bool ends_with<T>(T[] array, T[] suffix) => default!;

        /// <summary>
        /// <c>hasSubstr(array, other)</c>: true when the elements of <paramref name="other"/> appear in
        /// <paramref name="array"/> contiguously and in the same order (an empty <paramref name="other"/>
        /// is always contained). Requires a provider that supports array functions (see
        /// <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public bool has_substr<T>(T[] array, T[] other) => default!;

        /// <summary><c>arrayStringConcat(array, delimiter)</c>: joins the elements into one string (default separator is the empty string).</summary>
        public string array_string_concat<T>(T[] array, string? delimiter = null) => default!;

        /// <summary><c>splitByChar(separator, value)</c>: splits the string by a single-character separator. Returns an array, so it can be projected directly or used as the operand of another array function.</summary>
        public string[] split_by_char(string? separator, string? value) => default!;

        /// <summary><c>arraySort(array)</c>: the elements in ascending order. Returns an array, so it can be projected directly or used as the operand of another array function.</summary>
        public T[] array_sort<T>(T[] array) => default!;

        /// <summary><c>arrayReverse(array)</c>: the elements in reverse order. Returns an array, so it can be projected directly or used as the operand of another array function.</summary>
        public T[] array_reverse<T>(T[] array) => default!;

        /// <summary><c>arrayDistinct(array)</c>: the distinct elements. Returns an array, so it can be projected directly or used as the operand of another array function.</summary>
        public T[] array_distinct<T>(T[] array) => default!;

        /// <summary>
        /// <c>range(end)</c>: the integers from <c>0</c> up to (but excluding) <paramref name="end"/>.
        /// Returns an array, so it can be projected directly or used as the operand of another array
        /// function (for
        /// example <see cref="length{T}(T[])"/> or <see cref="array_string_concat{T}(T[], string?)"/>).
        /// Requires a provider that supports array functions (see
        /// <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public long[] range(long end) => default!;

        /// <summary><c>range(start, end)</c>: the integers in <c>[start, end)</c>. Returns an array, so it can be projected directly or nested.</summary>
        public long[] range(long start, long end) => default!;

        /// <summary><c>range(start, end, step)</c>: the integers in <c>[start, end)</c> with the given step. Returns an array, so it can be projected directly or nested.</summary>
        public long[] range(long start, long end, long step) => default!;

        /// <summary>
        /// <c>arrayEnumerate(array)</c>: the one-based positions <c>[1, 2, ..., length(array)]</c>.
        /// Returns an array, so it can be projected directly or used as the operand of another array function.
        /// </summary>
        public long[] array_enumerate<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayCumSum(array)</c>: the running sums of the elements. Returns an array, so it can be
        /// projected directly or used as the operand of another array function.
        /// </summary>
        public T[] array_cum_sum<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayCumSumNonNegative(array)</c>: the running sums of the elements, with every negative
        /// running sum replaced by zero (<c>[1, -2, 3, -4, 5]</c> becomes <c>[1, 0, 3, 0, 5]</c>). The
        /// vendor widens the accumulation (<c>Int32</c> input → <c>Array(Int64)</c>,
        /// <c>UInt8</c> input → <c>Array(UInt64)</c>); the declared CLR result is <see cref="long"/>
        /// and only integral element types except <see cref="ulong"/> are accepted — a floating-point,
        /// decimal or unsigned-64-bit element type is refused at translation time rather than
        /// truncated into the declared <see cref="long"/>. An empty array yields an empty array, and a
        /// <c>Nullable</c>-element array is rejected by the server (no nullable overload). Requires a
        /// provider that supports array functions (ClickHouse).
        /// </summary>
        public long[] array_cum_sum_non_negative<T>(T[] array) => default!;

        /// <summary>
        /// <c>arraySlice(array, offset)</c>: the elements from the one-based <paramref name="offset"/>
        /// to the end (a negative offset counts from the end). Returns an array, so it can be projected
        /// directly or used as the operand of another array function.
        /// </summary>
        public T[] array_slice<T>(T[] array, long offset) => default!;

        /// <summary><c>arraySlice(array, offset, length)</c>: as <see cref="array_slice{T}(T[], long)"/> but limited to <paramref name="length"/> elements.</summary>
        public T[] array_slice<T>(T[] array, long offset, long length) => default!;

        /// <summary>
        /// <c>arrayPushBack(array, element)</c>: the array with <paramref name="element"/> appended.
        /// Returns an array, so it can be projected directly or used as the operand of another array function.
        /// </summary>
        public T[] array_push_back<T>(T[] array, T element) => default!;

        /// <summary>
        /// <c>arrayMap(function, array)</c>: the array of the lambda results. Requires a provider that
        /// translates the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse). The lambda body may
        /// use its parameter in operators and function calls; member access on the parameter is not
        /// supported.
        /// </summary>
        public TOut[] array_map<TIn, TOut>(Expression<Func<TIn, TOut>> function, TIn[] array) => default!;

        /// <summary>
        /// <c>arrayMap(function, first, second)</c>: the array of the lambda results, with the lambda
        /// receiving one element from <paramref name="first"/> and one from <paramref name="second"/>.
        /// Both arrays must have the same length (the server rejects a length mismatch). This is the
        /// documented index-lambda idiom: pass an index array as the first operand —
        /// <c>array_map((i, x) =&gt; i + x, array_enumerate(array), array)</c>. The lambda has exactly
        /// one parameter per array argument, and ClickHouse has no separate index parameter. Requires a
        /// provider that supports the higher-order array functions (ClickHouse).
        /// </summary>
        public TOut[] array_map<TIn1, TIn2, TOut>(
            Expression<Func<TIn1, TIn2, TOut>> function, TIn1[] first, TIn2[] second) => default!;

        /// <summary>
        /// <c>arrayFilter(predicate, array)</c>: the elements for which the lambda returns true.
        /// Requires a provider that supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T[] array_filter<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayFilter(predicate, first, second)</c>: the elements of <paramref name="first"/> for
        /// which the two-parameter lambda returns true, with the second parameter bound to the matching
        /// element of <paramref name="second"/>. Both arrays must have the same length. The index-lambda
        /// idiom is <c>array_filter((x, i) =&gt; i % 2 = 0, array, array_enumerate(array))</c>: the
        /// source array comes first, so the result is a subset of the source and not of the index array.
        /// The lambda has exactly one parameter per array argument. Requires a provider that supports the
        /// higher-order array functions (ClickHouse).
        /// </summary>
        public TIn1[] array_filter<TIn1, TIn2>(
            Expression<Func<TIn1, TIn2, bool>> predicate, TIn1[] first, TIn2[] second) => default!;

        /// <summary>
        /// <c>arrayExists(predicate, array)</c>: true when the lambda returns true for at least one
        /// element. Requires a provider that supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public bool array_exists<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayAll(predicate, array)</c>: true when the lambda returns true for every element.
        /// Requires a provider that supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public bool array_all<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayCount(predicate, array)</c>: the number of elements for which the lambda returns
        /// true. Requires a provider that supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public long array_count<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayFirst(predicate, array)</c>: the first element for which the lambda returns true,
        /// or the default value of <typeparamref name="T"/> when there is none. Requires a provider
        /// that supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T? array_first<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayFirstIndex(predicate, array)</c>: the one-based index of the first element for
        /// which the lambda returns true, or <c>0</c> when there is none. Requires a provider that
        /// supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public long array_first_index<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayLast(predicate, array)</c>: the last element for which the lambda returns true, or
        /// the default value of <typeparamref name="T"/> when there is none. Requires a provider that
        /// supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T? array_last<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayLastIndex(predicate, array)</c>: the one-based index of the last element for which
        /// the lambda returns true, or <c>0</c> when there is none. Requires a provider that supports
        /// the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public long array_last_index<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>toDate(value)</c>: converts a string or date/time value to a <c>Date</c>. Requires a
        /// provider that supports the date conversion surface (see
        /// <see cref="ISqlDialect.DateConversion"/>; ClickHouse).
        /// </summary>
        public DateTime? to_date<T>(T? value) => default!;

        /// <summary><c>toDateTime(value)</c>: converts a string or date value to a <c>DateTime</c>.</summary>
        public DateTime? to_date_time<T>(T? value) => default!;

        /// <summary><c>toDate32(value)</c>: converts a value to ClickHouse's extended-range <c>Date32</c>.</summary>
        public DateTime? to_date32<T>(T? value) => default!;

        /// <summary><c>toYear(value)</c>: the year part.</summary>
        public int to_year<T>(T? value) => default!;

        /// <summary><c>toQuarter(value)</c>: the quarter (1..4).</summary>
        public int to_quarter<T>(T? value) => default!;

        /// <summary><c>toMonth(value)</c>: the month (1..12).</summary>
        public int to_month<T>(T? value) => default!;

        /// <summary><c>toDayOfMonth(value)</c>: the day of the month (1..31).</summary>
        public int to_day_of_month<T>(T? value) => default!;

        /// <summary><c>toDayOfWeek(value)</c>: the day of the week (Monday is 1, Sunday is 7).</summary>
        public int to_day_of_week<T>(T? value) => default!;

        /// <summary><c>toDayOfYear(value)</c>: the day of the year (1..366).</summary>
        public int to_day_of_year<T>(T? value) => default!;

        /// <summary><c>toHour(value)</c>: the hour (0..23).</summary>
        public int to_hour<T>(T? value) => default!;

        /// <summary><c>toMinute(value)</c>: the minute (0..59).</summary>
        public int to_minute<T>(T? value) => default!;

        /// <summary><c>toSecond(value)</c>: the second (0..59).</summary>
        public int to_second<T>(T? value) => default!;

        /// <summary><c>toStartOfYear(value)</c>: the first day of the year at 00:00:00.</summary>
        public DateTime? to_start_of_year<T>(T? value) => default!;

        /// <summary><c>toStartOfQuarter(value)</c>: the first day of the quarter at 00:00:00.</summary>
        public DateTime? to_start_of_quarter<T>(T? value) => default!;

        /// <summary><c>toStartOfMonth(value)</c>: the first day of the month at 00:00:00.</summary>
        public DateTime? to_start_of_month<T>(T? value) => default!;

        /// <summary>
        /// <c>toStartOfWeek(value)</c>: the start of the week at 00:00:00. ClickHouse starts the week on
        /// Sunday by default (unlike ISO weeks); use <see cref="to_monday{T}"/> for the Monday start.
        /// </summary>
        public DateTime? to_start_of_week<T>(T? value) => default!;

        /// <summary><c>toStartOfDay(value)</c>: midnight of the day.</summary>
        public DateTime? to_start_of_day<T>(T? value) => default!;

        /// <summary><c>toStartOfHour(value)</c>: the start of the hour.</summary>
        public DateTime? to_start_of_hour<T>(T? value) => default!;

        /// <summary><c>toStartOfMinute(value)</c>: the start of the minute.</summary>
        public DateTime? to_start_of_minute<T>(T? value) => default!;

        /// <summary><c>toStartOfSecond(value)</c>: the start of the second.</summary>
        public DateTime? to_start_of_second<T>(T? value) => default!;

        /// <summary><c>toMonday(value)</c>: the Monday of the ISO week at 00:00:00.</summary>
        public DateTime? to_monday<T>(T? value) => default!;

        /// <summary><c>toYYYYMM(value)</c>: the year and month packed as a <c>YYYYMM</c> integer.</summary>
        public int to_yyyymm<T>(T? value) => default!;

        /// <summary><c>toYYYYMMDD(value)</c>: the date packed as a <c>YYYYMMDD</c> integer.</summary>
        public int to_yyyymmdd<T>(T? value) => default!;

        /// <summary><c>toUnixTimestamp(value)</c>: the Unix timestamp in seconds.</summary>
        public long to_unix_timestamp<T>(T? value) => default!;

        /// <summary>
        /// A single branch of a <see cref="multi_if{TResult}(MultiIfBranch{TResult}[])"/> call: a
        /// condition with its result (<see cref="when{T}(bool, T)"/>) or the final else value
        /// (<see cref="otherwise{T}(T)"/>). Only ever produced by the compiler while building an
        /// expression tree; the dialect renders it as an argument of <c>multiIf</c>.
        /// </summary>
        public sealed class MultiIfBranch<T>
        {
            private MultiIfBranch()
            {
            }
        }

        /// <summary>
        /// A <c>multiIf</c> branch: when <paramref name="condition"/> is true, the result is
        /// <paramref name="value"/>. Requires a provider that supports <c>multiIf</c> (see
        /// <see cref="ISqlDialect.MultiIf"/>; ClickHouse).
        /// </summary>
        public MultiIfBranch<T> when<T>(bool condition, T? value) => default!;

        /// <summary>
        /// The final <c>multiIf</c> else value, used when every preceding <see cref="when{T}(bool, T)"/>
        /// condition is false. Must be the last branch.
        /// </summary>
        public MultiIfBranch<T> otherwise<T>(T? value) => default!;

        /// <summary>
        /// <c>multiIf(cond1, then1, cond2, then2, ..., else)</c>: the value of the first true branch, or
        /// the trailing else value. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.MultiIf"/>; ClickHouse). Each branch is built with
        /// <see cref="when{T}(bool, T)"/> and the final else with <see cref="otherwise{T}(T)"/>; the
        /// conditions and values must be inline expressions (a captured branch array is rejected). A
        /// numeric <typeparamref name="TResult"/> is cast to its ClickHouse type so it materialises;
        /// otherwise the result must match the common supertype ClickHouse infers for the branches.
        /// </summary>
        public TResult? multi_if<TResult>(params MultiIfBranch<TResult>[] branches) => default!;

        /// <summary>
        /// <c>lagInFrame(value[, offset[, default]])</c>: like <c>CommonFunctions.lag</c> but evaluated
        /// within the ordered window frame. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsInFrameWindowFunctions"/>; ClickHouse). The standard <c>lag</c>
        /// looks at the whole partition and ignores the frame, so the two differ on a partial frame.
        /// </summary>
        public WindowFunction<T?> lag_in_frame<T>(T? value) => default!;

        /// <summary><c>lagInFrame(value, offset)</c>: as <c>lag_in_frame</c> with an explicit offset.</summary>
        public WindowFunction<T?> lag_in_frame<T>(T? value, int offset) => default!;

        /// <summary><c>lagInFrame(value, offset, default)</c>: as <c>lag_in_frame</c> with a fallback value.</summary>
        public WindowFunction<T?> lag_in_frame<T>(T? value, int offset, T? defaultValue) => default!;

        /// <summary>
        /// <c>leadInFrame(value[, offset[, default]])</c>: like <c>CommonFunctions.lead</c> but evaluated
        /// within the ordered window frame. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsInFrameWindowFunctions"/>; ClickHouse). The standard <c>lead</c>
        /// looks at the whole partition and ignores the frame, so the two differ on a partial frame.
        /// </summary>
        public WindowFunction<T?> lead_in_frame<T>(T? value) => default!;

        /// <summary><c>leadInFrame(value, offset)</c>: as <c>lead_in_frame</c> with an explicit offset.</summary>
        public WindowFunction<T?> lead_in_frame<T>(T? value, int offset) => default!;

        /// <summary><c>leadInFrame(value, offset, default)</c>: as <c>lead_in_frame</c> with a fallback value.</summary>
        public WindowFunction<T?> lead_in_frame<T>(T? value, int offset, T? defaultValue) => default!;

        /// <summary>
        /// <c>nonNegativeDerivative(value, time)</c> over an ordered window: the non-negative rate of
        /// change of <paramref name="value"/> with respect to <paramref name="time"/>, as
        /// <see cref="double"/> (<c>Float64</c>). A negative or zero change yields 0, and the first
        /// row of a partition yields 0. The vendor accepts this function only as a window function:
        /// an aggregate call is rejected, and this surface refuses a bare call, so it must be
        /// completed with <c>Over(...)</c>. Both arguments must be non-nullable expressions
        /// (<c>Nullable</c> inputs are rejected by the server with <c>BAD_ARGUMENTS</c>), and the
        /// timestamp must be a <see cref="DateTime"/>. The optional third vendor argument is an
        /// <c>INTERVAL</c> and is not part of the typed surface. Requires a provider that supports it
        /// (ClickHouse).
        /// </summary>
        public WindowFunction<double> non_negative_derivative<T>(T value, DateTime time) where T : struct => default!;

        /// <summary>
        /// <c>lowerUTF8(value)</c>: lowercases character-wise (UTF-8 aware), unlike the byte-wise
        /// <c>lower</c> behind <see cref="string.ToLowerInvariant"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? lower_utf8(string? value) => default!;

        /// <summary><c>upperUTF8(value)</c>: uppercases character-wise (UTF-8 aware).</summary>
        public string? upper_utf8(string? value) => default!;

        /// <summary>
        /// <c>trimLeft(value)</c> or <c>trimLeft(value, characters)</c>: removes the leading whitespace,
        /// or every leading character contained in <paramref name="characters"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public string? trim_left(string? value, string? characters = null) => default!;

        /// <summary><c>trimRight(value[, characters])</c>: as <see cref="trim_left(string?, string?)"/> from the end.</summary>
        public string? trim_right(string? value, string? characters = null) => default!;

        /// <summary><c>trimBoth(value[, characters])</c>: as <see cref="trim_left(string?, string?)"/> from both ends.</summary>
        public string? trim_both(string? value, string? characters = null) => default!;

        /// <summary>
        /// <c>replaceRegexpOne(value, pattern, replacement)</c>: replaces the first RE2 match of
        /// <paramref name="pattern"/> (the replacement may reference capture groups with <c>\1</c>).
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? replace_regexp_one(string? value, string? pattern, string? replacement) => default!;

        /// <summary><c>replaceRegexpAll(value, pattern, replacement)</c>: replaces every RE2 match.</summary>
        public string? replace_regexp_all(string? value, string? pattern, string? replacement) => default!;

        /// <summary>
        /// <c>match(value, pattern)</c>: true when the RE2 <paramref name="pattern"/> matches
        /// <paramref name="value"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public bool match(string? value, string? pattern) => default!;

        /// <summary>
        /// <c>extract(value, pattern)</c>: the first fragment of <paramref name="value"/> captured by the
        /// RE2 <paramref name="pattern"/>, or the empty string. Requires a provider that supports it
        /// (ClickHouse).
        /// </summary>
        public string? extract(string? value, string? pattern) => default!;

        /// <summary>
        /// <c>extractAll(value, pattern)</c>: every fragment of <paramref name="value"/> captured by the
        /// RE2 <paramref name="pattern"/>. The native result is <c>Array(String)</c>, surfaced as
        /// <c>string[]</c> and projected directly or used as the operand of another array function.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string[] extract_all(string? value, string? pattern) => default!;

        /// <summary>
        /// <c>splitByString(separator, value)</c>: splits <paramref name="value"/> by a multi-character
        /// separator. Returns an array, so it can be projected directly or nested. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public string[] split_by_string(string? separator, string? value) => default!;

        /// <summary><c>splitByRegexp(pattern, value)</c>: splits by an RE2 pattern. Returns an array, so it can be projected directly or nested.</summary>
        public string[] split_by_regexp(string? pattern, string? value) => default!;

        /// <summary><c>splitByWhitespace(value)</c>: splits on runs of whitespace. Returns an array, so it can be projected directly or nested.</summary>
        public string[] split_by_whitespace(string? value) => default!;

        /// <summary>
        /// <c>overlay(value, replacement, offset)</c> or <c>overlay(value, replacement, offset, length)</c>:
        /// replaces <paramref name="length"/> characters (by default, the whole
        /// <paramref name="replacement"/>) starting at the 1-based <paramref name="offset"/> of
        /// <paramref name="value"/>. A zero <paramref name="length"/> inserts without removing anything; an
        /// <paramref name="offset"/> past the end, like a negative one, appends. A <see langword="null"/>
        /// in any argument yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? overlay(string? value, string? replacement, int offset, int? length = null) => default!;

        /// <summary>
        /// <c>initcap(value)</c>: upper-cases the first letter of every word and lower-cases the remaining
        /// ones. The case mapping is ASCII-only, so non-ASCII letters keep their original case. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? initcap(string? value) => default!;

        /// <summary>
        /// <c>soundex(value)</c>: the four-character Soundex code of <paramref name="value"/>. An input
        /// without an ASCII letter (empty, digits only or non-ASCII) yields <c>0000</c>. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? soundex(string? value) => default!;

        /// <summary>
        /// <c>base64Encode(value)</c>: the standard Base64 text of the UTF-8 bytes of
        /// <paramref name="value"/>; an empty input yields the empty string. A <see langword="null"/>
        /// input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? base64_encode(string? value) => default!;

        /// <summary>
        /// <c>base64Decode(value)</c>: the bytes encoded by the Base64 <paramref name="value"/>, surfaced
        /// as a string and therefore meaningful for UTF-8 text rather than for arbitrary binary (use the
        /// <c>hex</c>/<c>unhex</c> pair to round-trip binary data). Malformed Base64 is rejected by the
        /// server; an empty input yields the empty string and a <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? base64_decode(string? value) => default!;

        /// <summary>
        /// <c>hex(value)</c>: the upper-case hexadecimal rendering of the UTF-8 bytes of
        /// <paramref name="value"/>; an empty input yields the empty string. A <see langword="null"/>
        /// input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? hex(string? value) => default!;

        /// <summary>
        /// <c>unhex(value)</c>: the string whose UTF-8 bytes are the hexadecimal digits of
        /// <paramref name="value"/>; an odd number of digits is left-padded with a zero. The server does
        /// not validate the digits, so only <c>hex</c>-produced text round-trips. An empty input yields the
        /// empty string and a <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public string? unhex(string? value) => default!;

        // -------------------------------------------------------------------------------------------------
        // Encoding family (T286b F11). Binary results are carried as an explicit hexadecimal string
        // (the upper-case `hex(...)` of the server bytes) instead of a CLR string that would decode
        // arbitrary bytes as UTF-8; textual results stay `string`. A text-taking encoder consumes the
        // UTF-8 bytes of its argument, so `unhex(...)` can supply arbitrary binary input.
        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// <c>bin(value)</c>: the binary representation of the unsigned 64-bit
        /// <paramref name="value"/>, most significant bit first and leading zero digits preserved
        /// only to the value's minimal byte width: the vendor emits one character per bit of the
        /// value's significant bytes (at least eight characters), so <c>42</c> is
        /// <c>'00101010'</c> and <see cref="ulong.MaxValue"/> is 64 ones. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? bin(ulong? value) => default!;

        /// <summary>
        /// <c>unbin(bits)</c>: the inverse of <see cref="bin(ulong?)"/>. The server returns the decoded
        /// value as a binary string (a BLOB), so the member surfaces those bytes as an upper-case
        /// hexadecimal string rather than as a CLR string that would decode arbitrary bytes as UTF-8:
        /// <c>unbin(bin(42))</c> is <c>'2A'</c>. The bits are consumed from the right, an odd-length
        /// input is left-padded with a zero, and the server does not reject characters other than
        /// <c>'0'</c>/<c>'1'</c>. A <see langword="null"/> input yields <see langword="null"/> and an
        /// empty input yields the empty string. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? unbin(string? bits) => default!;

        /// <summary>
        /// <c>base32Encode(value)</c>: the RFC 4648 Base32 text of the UTF-8 bytes of
        /// <paramref name="value"/>, upper-case and <c>'='</c>-padded to a multiple of eight
        /// characters. An empty input yields the empty string; a <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? base32_encode(string? value) => default!;

        /// <summary>
        /// <c>base32Decode(value)</c>: decodes the RFC 4648 Base32 <paramref name="value"/> (either
        /// case is accepted). The decoded bytes are surfaced as an upper-case hexadecimal string, so
        /// arbitrary binary round-trips without UTF-8 loss:
        /// <c>base32_decode(base32_encode(x))</c> is <c>hex(x)</c>. An input whose length is not a
        /// multiple of eight or that contains a character outside the Base32 alphabet raises the
        /// server's <c>INCORRECT_DATA</c> error. An empty input yields the empty string and a
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? base32_decode(string? value) => default!;

        /// <summary>
        /// <c>base58Encode(value)</c>: the Bitcoin Base58 text of the UTF-8 bytes of
        /// <paramref name="value"/> (the alphabet excludes <c>0</c>, <c>O</c>, <c>I</c> and <c>l</c>,
        /// and no padding is emitted). An empty input yields the empty string; a
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? base58_encode(string? value) => default!;

        /// <summary>
        /// <c>base58Decode(value)</c>: decodes the Bitcoin Base58 <paramref name="value"/> and
        /// surfaces the bytes as an upper-case hexadecimal string (a leading <c>'1'</c> is a zero
        /// byte, so <c>base58Decode('11')</c> is <c>'0000'</c>). A character outside the alphabet
        /// raises the server's <c>INCORRECT_DATA</c> error. An empty input yields the empty string
        /// and a <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? base58_decode(string? value) => default!;

        /// <summary>
        /// <c>base64URLEncode(value)</c>: the URL-safe Base64 text of the UTF-8 bytes of
        /// <paramref name="value"/> (<c>'-'</c>/<c>'_'</c> alphabet, no <c>'='</c> padding). An empty
        /// input yields the empty string; a <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? base64_url_encode(string? value) => default!;

        /// <summary>
        /// <c>base64URLDecode(value)</c>: decodes the URL-safe Base64 <paramref name="value"/>; both
        /// the padded and the unpadded form are accepted and both alphabets are read. The decoded bytes
        /// are surfaced as an upper-case hexadecimal string. An invalid alphabet raises the server's
        /// <c>INCORRECT_DATA</c> error. An empty input yields the empty string and a
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? base64_url_decode(string? value) => default!;

        /// <summary>
        /// <c>bech32Encode(hrp, data[, witver])</c>: a Bech32/Bech32m address. <paramref name="hrp"/>
        /// is the 1–83-character lower-case human-readable part (usually <c>'bc'</c> or <c>'tb'</c>)
        /// and <paramref name="data"/> is binary data — pass text for its UTF-8 bytes or
        /// <c>unhex(...)</c> for arbitrary bytes. <paramref name="witver"/> is optional:
        /// <c>0</c> selects Bech32 and <c>1</c> (the server default) or greater selects Bech32m. The
        /// vendor returns an empty string when the input cannot form a valid address, and a
        /// <see langword="null"/> argument yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? bech32_encode(string? hrp, string? data, int? witver = null) => default!;

        /// <summary>
        /// <c>bech32Decode(address)</c>: the <c>(hrp, data)</c> pair of a Bech32 address, with the
        /// binary data surfaced as an upper-case hexadecimal string. The server does not validate the
        /// checksum: an address it cannot decode yields <c>('', '')</c> rather than raising, so the
        /// empty pair is the tested malformed-input outcome. A <see langword="null"/> address is
        /// rejected by the server (a tuple cannot be inside <c>Nullable</c>), hence the non-nullable
        /// parameter. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Tuple<string, string> bech32_decode(string address) => default!;

        /// <summary>
        /// <c>bitmaskToArray(value)</c>: the ascending powers of two that sum to
        /// <paramref name="value"/>, as <c>Array(UInt64)</c>; <c>0</c> yields an empty array. The
        /// argument is rendered through <c>toUInt64</c>, because the vendor result element type
        /// follows the input width (<c>Array(UInt8)</c> for a small literal) and the declared CLR
        /// shape is the unsigned 64-bit array. The server rejects a <c>Nullable</c> argument because
        /// an array cannot be inside <c>Nullable</c>, hence the non-nullable parameter. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public ulong[] bitmask_to_array(ulong value) => default!;

        /// <summary>
        /// <c>bitmaskToList(value)</c>: the same powers of two as a comma-separated string
        /// (for example <c>'2,8'</c>); <c>0</c> yields the empty string. A <see langword="null"/>
        /// input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? bitmask_to_list(ulong? value) => default!;

        /// <summary>
        /// <c>bitPositionsToArray(value)</c>: the ascending zero-based positions of the set bits of
        /// <paramref name="value"/> (for example <c>[1, 3]</c> for 10); <c>0</c> yields an empty array.
        /// The server rejects a <c>Nullable</c> argument, hence the non-nullable parameter. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public ulong[] bit_positions_to_array(ulong value) => default!;

        /// <summary>
        /// <c>sqidEncode(value, ...)</c>: encodes one or more unsigned integers as a Sqid string
        /// (for example <c>sqidEncode(1, 2, 3)</c> is <c>'86Rf07'</c>). At least one value is
        /// required: the server rejects the empty call with <c>NUMBER_OF_ARGUMENTS_DOESNT_MATCH</c>,
        /// so an empty call is refused at translation time. The values must be inline expressions,
        /// not a captured array. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string sqid_encode(params ulong[] values) => default!;

        /// <summary>
        /// <c>sqidDecode(sqid)</c>: the numbers encoded by the Sqid string as <c>Array(UInt64)</c>;
        /// an empty string yields an empty array and an arbitrary string decodes deterministically to
        /// the number its characters represent. The server rejects a <c>Nullable</c> argument because
        /// an array cannot be inside <c>Nullable</c>, hence the non-nullable parameter. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public ulong[] sqid_decode(string value) => default!;

        /// <summary>
        /// <c>hilbertEncode(value)</c>: the Hilbert curve code of the single unsigned coordinate
        /// (<c>UInt64</c>). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong hilbert_encode(ulong value) => default!;

        /// <summary>
        /// <c>hilbertEncode(x, y)</c>: the Hilbert curve code of the two unsigned coordinates
        /// (<c>UInt64</c>). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong hilbert_encode(ulong x, ulong y) => default!;

        /// <summary>
        /// <c>hilbertDecode(code)</c>: the <c>(x, y)</c> coordinates of a two-dimensional Hilbert
        /// code, rendered as the native <c>hilbertDecode(2, code)</c> whose <c>Tuple(UInt64, UInt64)</c>
        /// result is the declared pair. The vendor result arity follows a runtime size argument, so
        /// the fixed two-component shape is the one a CLR type can name; the one-dimensional form is
        /// reachable through raw SQL. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Tuple<ulong, ulong> hilbert_decode(ulong code) => default!;

        /// <summary>
        /// <c>mortonEncode(x, y)</c>: the Morton (Z-order) code of the two unsigned coordinates
        /// (<c>UInt64</c>). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong morton_encode(ulong x, ulong y) => default!;

        /// <summary>
        /// <c>mortonDecode(code)</c>: the <c>(x, y)</c> coordinates of a two-dimensional Morton code,
        /// rendered as the native <c>mortonDecode(2, code)</c> whose <c>Tuple(UInt64, UInt64)</c>
        /// result is the declared pair. The vendor result arity follows a runtime size argument (up to
        /// eight), so the fixed two-component shape is the one a CLR type can name; other arities are
        /// reachable through raw SQL. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Tuple<ulong, ulong> morton_decode(ulong code) => default!;

        // -------------------------------------------------------------------------------------------------
        // UUID/ULID/snowflake family (T286b F12). The native UUID type is exactly System.Guid; the
        // vendor's 16-byte binary UUID values are surfaced as an explicit 32-hex-digit string rather
        // than as a Guid (whose byte order differs) or as a UTF-8-decoded string.
        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// <c>UUIDNumToString(num)</c>: the lower-case hyphenated textual UUID of the 16-byte binary
        /// value given as 32 hexadecimal digits — the form <see cref="uuid_string_to_num(string?)"/>
        /// returns, so the pair round-trips:
        /// <c>uuid_num_to_string(uuid_string_to_num(s))</c> is <c>s</c>. A shorter input is padded
        /// with zero bytes by the server's <c>toFixedString</c>; a longer one raises
        /// <c>TOO_LARGE_STRING_SIZE</c>. A <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? uuid_num_to_string(string? num) => default!;

        /// <summary>
        /// <c>UUIDStringToNum(value)</c>: the 16-byte binary UUID as 32 upper-case hexadecimal
        /// digits in textual (big-endian) order, the vendor default variant. The server returns
        /// sixteen zero bytes for a string it cannot parse, so malformed input is a deterministic
        /// value rather than an exception. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? uuid_string_to_num(string? value) => default!;

        /// <summary>
        /// <c>UUIDToNum(value)</c>: the binary form of a <see cref="Guid"/> as 32 upper-case
        /// hexadecimal digits (textual/big-endian order, the vendor default variant). A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? uuid_to_num(Guid? value) => default!;

        /// <summary>
        /// <c>UUIDv7ToDateTime(value)</c>: the millisecond timestamp component of a version-7 UUID as
        /// <c>DateTime64(3)</c>. The vendor does not validate the version: it decodes the first 48
        /// bits, so the nil UUID and a non-v7 value decode to the epoch (1970-01-01) instead of
        /// raising. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public DateTime? uuid_v7_to_datetime(Guid? value) => default!;

        /// <summary>
        /// <c>toUUID(value)</c>: parses the textual UUID into a <see cref="Guid"/>. Upper-case input is
        /// accepted; a malformed string raises the server's <c>CANNOT_PARSE_UUID</c> error. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public Guid? to_uuid(string? value) => default!;

        /// <summary>
        /// <c>toUUIDOrDefault(value, defaultValue)</c>: the parsed <see cref="Guid"/>, or
        /// <paramref name="defaultValue"/> when the string cannot be parsed or is
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Guid? to_uuid_or_default(string? value, Guid defaultValue) => default!;

        /// <summary>
        /// <c>toUUIDOrNull(value)</c>: the parsed <see cref="Guid"/>, or <see langword="null"/> when
        /// the string cannot be parsed or is <see langword="null"/>; unlike
        /// <see cref="to_uuid(string?)"/> a malformed value is not an error. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public Guid? to_uuid_or_null(string? value) => default!;

        /// <summary>
        /// <c>ULIDStringToDateTime(value)</c>: the millisecond timestamp component of the 26-character
        /// Crockford Base32 ULID as <c>DateTime64(3)</c>; either case is accepted and the nil ULID
        /// decodes to the epoch. A value that is not 26 characters raises the server's
        /// <c>ILLEGAL_COLUMN</c> error. The 48-bit ULID timestamp range reaches year 10889, beyond
        /// the <c>DateTime64(3)</c> range: such values saturate server-side and cannot be
        /// materialized as <see cref="DateTime"/>, so the driver raises instead of returning a wrong
        /// instant. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public DateTime? ulid_string_to_datetime(string? value) => default!;

        /// <summary>
        /// <c>generateSnowflakeID()</c>: a new unsigned 64-bit Snowflake identifier. The vendor takes
        /// the current millisecond timestamp shifted by 22 bits, so two calls in the same query
        /// differ. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong generate_snowflake_id() => default!;

        /// <summary>
        /// <c>dateTimeToSnowflakeID(value)</c>: the Snowflake id of a <see cref="DateTime"/> — the
        /// millisecond timestamp shifted by 22 bits. The server accepts <c>DateTime</c> only (not
        /// <c>DateTime64</c>); an instant before the epoch is clamped to zero. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public ulong? date_time_to_snowflake_id(DateTime? value) => default!;

        /// <summary>
        /// <c>dateTime64ToSnowflakeID(value)</c>: the Snowflake id of a <see cref="DateTime"/>, encoded
        /// as <c>dateTime64ToSnowflakeID(toDateTime64(value, 3))</c> because the server requires
        /// <c>DateTime64</c>. The vendor format carries milliseconds, so sub-millisecond digits of the
        /// CLR value are truncated by the function itself (123456 µs and 123 ms produce the identical
        /// id). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public ulong? date_time64_to_snowflake_id(DateTime? value) => default!;

        /// <summary>
        /// <c>dateTimeToUUIDv7(value)</c>: a version-7 UUID whose timestamp component is the
        /// millisecond timestamp of <paramref name="value"/>. The server requires a non-nullable
        /// <c>DateTime</c> (a <c>DateTime64</c> or <c>Nullable</c> argument is rejected), hence the
        /// non-nullable parameter; use <see cref="uuid_v7_to_datetime(Guid?)"/> for the inverse.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Guid date_time_to_uuid_v7(DateTime value) => default!;

        /// <summary>
        /// <c>snowflakeIDToDateTime(value)</c>: the millisecond timestamp component as the server's
        /// 32-bit <c>DateTime</c>. Ids whose timestamp is beyond 2106 (the 32-bit <c>DateTime</c>
        /// limit) wrap instead of raising, so <c>UInt64.MaxValue</c> decodes to 1973-04-08; use
        /// <see cref="snowflake_id_to_date_time64(ulong?)"/> for the exact value of the full range. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public DateTime? snowflake_id_to_date_time(ulong? value) => default!;

        /// <summary>
        /// <c>snowflakeIDToDateTime64(value)</c>: the millisecond timestamp component as
        /// <c>DateTime64(3)</c>, exact for the whole <see cref="ulong"/> id range (the maximum decodes
        /// to 2109-05-15). A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public DateTime? snowflake_id_to_date_time64(ulong? value) => default!;

        // -------------------------------------------------------------------------------------------------
        // URL/IP family (T286b F13). The URL parser is the vendor's fast non-RFC one: it is not RFC 3986
        // and rejects a userinfo component, an IPv6 literal host and a trailing-dot host. The IP surface
        // keeps the native IPv4/IPv6 CLR mapping (System.Net.IPAddress) and surfaces the 16-byte binary
        // IPv6 form as 32 upper-case hexadecimal digits, the same explicit-hex convention the binary
        // UUID forms use, because the driver maps FixedString(16) to a raw byte-valued string.
        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// <c>domain(url)</c>: the host name of a URL, with or without a scheme. The vendor uses its fast
        /// non-RFC parser, so a URL carrying userinfo (<c>https://user:pw@example.com</c>), an IPv6
        /// literal host or a trailing-dot host yields an empty string rather than the host; an
        /// unparsable value also yields an empty string. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? domain(string? url) => default!;

        /// <summary>
        /// <c>protocol(url)</c>: the scheme of a URL without <c>://</c> (for example <c>https</c>),
        /// case preserved (<c>HTTPS://example.com</c> yields <c>HTTPS</c>). A scheme-less value, a
        /// protocol-relative value (<c>//example.com</c>) and an empty value yield an empty string. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? protocol(string? url) => default!;

        /// <summary>
        /// <c>path(url)</c>: the path without the query string and the fragment (the leading solidus is
        /// included, repeated separators are preserved). A URL without a path yields an empty string. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? path(string? url) => default!;

        /// <summary>
        /// <c>queryString(url)</c>: the query string without the leading <c>?</c> and without the
        /// fragment. A URL without a query string yields an empty string. A <see langword="null"/> input
        /// yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? query_string(string? url) => default!;

        /// <summary>
        /// <c>fragment(url)</c>: the fragment identifier without the leading <c>#</c>; a later <c>#</c>
        /// stays part of the fragment. A URL without a fragment yields an empty string. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? fragment(string? url) => default!;

        /// <summary>
        /// <c>port(url)</c>: the port of a URL (the server's <c>UInt16</c> widened to
        /// <see cref="int"/> with <c>toInt32</c>, an exact widening because the value never exceeds
        /// 65535), or <c>0</c> when the URL carries no port, cannot be parsed, carries userinfo or names
        /// a port above 65535. A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public int? port(string? url) => default!;

        /// <summary>
        /// <c>port(url, defaultPort)</c>: the port of a URL, or <paramref name="defaultPort"/> (a
        /// <c>UInt16</c>, 0..65535) when the URL carries no port or cannot be parsed (the vendor returns
        /// the default for a userinfo URL too). A <see langword="null"/> URL still yields
        /// <see langword="null"/>, never the default. Requires a provider that supports it
        /// (ClickHouse).
        /// </summary>
        public int? port(string? url, ushort defaultPort) => default!;

        /// <summary>
        /// <c>cutURLParameter(url, name)</c>: the URL with the first <c>name=value</c> occurrence of
        /// <paramref name="name"/> removed; an unmatched name leaves the URL unchanged, a value-less flag
        /// parameter (<c>?name</c>) is not removed, and names are compared without decoding (so
        /// <c>Client ID</c> and <c>Client%20ID</c> differ). A <see langword="null"/> argument yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? cut_url_parameter(string? url, string? name) => default!;

        /// <summary>
        /// <c>topLevelDomain(url)</c>: the last label of the host name (for example <c>com</c>), with or
        /// without a scheme. An unparsable value, a trailing-dot host and an address-only host yield an
        /// empty string. A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public string? top_level_domain(string? url) => default!;

        /// <summary>
        /// <c>encodeURLComponent(value)</c>: percent-encodes every reserved character of
        /// <paramref name="value"/> (RFC 3986 component encoding; a space becomes <c>%20</c>). A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? encode_url_component(string? value) => default!;

        /// <summary>
        /// <c>decodeURLComponent(value)</c>: decodes percent-encoded escapes; a <c>+</c> is left as a
        /// literal plus (that is the form-encoding rule, not this function). A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? decode_url_component(string? value) => default!;

        /// <summary>
        /// <c>encodeURLFormComponent(value)</c>: form (RFC 1866) encoding, where a space becomes a
        /// <c>+</c> and the remaining reserved characters are percent-encoded. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? encode_url_form_component(string? value) => default!;

        /// <summary>
        /// <c>decodeURLFormComponent(value)</c>: form (RFC 1866) decoding, where a <c>+</c> becomes a
        /// space and percent escapes are decoded. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? decode_url_form_component(string? value) => default!;

        /// <summary>
        /// <c>domainWithoutWWW(url)</c>: the host name with a leading <c>www.</c> removed (the non-RFC
        /// parser rules of <see cref="domain(string?)"/> apply). A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? domain_without_www(string? url) => default!;

        /// <summary>
        /// <c>firstSignificantSubdomain(url)</c>: the first significant subdomain under the built-in
        /// list (a second-level label for <c>com</c>/<c>net</c>/<c>org</c>/<c>co</c>, otherwise a
        /// third-level label; <c>https://news.clickhouse.com.tr/</c> yields <c>clickhouse</c>). A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? first_significant_subdomain(string? url) => default!;

        /// <summary>
        /// <c>firstSignificantSubdomainCustom(url, tldList)</c>: the first significant subdomain under a
        /// custom top-level-domain list configured on the server (<c>&lt;top_level_domains_lists&gt;</c>).
        /// The list name is an ordinary argument; on a server without that configuration the vendor
        /// raises <c>TLD_LIST_NOT_FOUND</c>. A <see langword="null"/> argument yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? first_significant_subdomain_custom(string? url, string? tldList) => default!;

        /// <summary>
        /// <c>pathFull(url)</c>: the path including the query string and the fragment. A URL without a
        /// path yields an empty string. A <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? path_full(string? url) => default!;

        /// <summary>
        /// <c>queryStringAndFragment(url)</c>: the query string and the fragment joined by the vendor's
        /// separators (for example <c>b=1#c</c>), without a leading <c>?</c>. A URL with neither yields
        /// an empty string. A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public string? query_string_and_fragment(string? url) => default!;

        /// <summary>
        /// <c>cutWWW(url)</c>: the URL with a leading <c>www.</c> removed from the host when the value
        /// has a scheme (<c>https://www.example.com/x</c> becomes <c>https://example.com/x</c>); a bare
        /// <c>www.example.com</c> is returned unchanged by the vendor's parser. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? cut_www(string? url) => default!;

        /// <summary>
        /// <c>cutQueryString(url)</c>: the URL without the query string (the <c>?</c> included); the
        /// fragment stays. A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public string? cut_query_string(string? url) => default!;

        /// <summary>
        /// <c>cutFragment(url)</c>: the URL without the fragment (the <c>#</c> included); the query
        /// string stays. A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public string? cut_fragment(string? url) => default!;

        /// <summary>
        /// <c>cutQueryStringAndFragment(url)</c>: the URL without the query string and the fragment. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? cut_query_string_and_fragment(string? url) => default!;

        /// <summary>
        /// <c>cutToFirstSignificantSubdomain(url)</c>: the host truncated to the first significant
        /// subdomain (<c>https://www.example.co.uk/x</c> yields <c>example.co.uk</c>); an input that
        /// cannot be parsed, such as a bare top-level domain or a userinfo URL, yields an empty string. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? cut_to_first_significant_subdomain(string? url) => default!;

        /// <summary>
        /// <c>cutToFirstSignificantSubdomainWithWWW(url)</c>: the variant that keeps a leading
        /// <c>www.</c>. The pinned 25.8 server returns the same value as
        /// <see cref="cut_to_first_significant_subdomain(string?)"/> (the prefix is not retained), which
        /// is the vendor behaviour this member exposes rather than a CLR-side rewrite. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? cut_to_first_significant_subdomain_with_www(string? url) => default!;

        /// <summary>
        /// <c>cutToFirstSignificantSubdomainCustom(url, tldList)</c>: the custom-list variant of
        /// <see cref="cut_to_first_significant_subdomain(string?)"/>; the list must be configured on the
        /// server, otherwise the vendor raises <c>TLD_LIST_NOT_FOUND</c>. A <see langword="null"/>
        /// argument yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? cut_to_first_significant_subdomain_custom(string? url, string? tldList) => default!;

        /// <summary>
        /// <c>cutToFirstSignificantSubdomainCustomWithWWW(url, tldList)</c>: the custom-list variant that
        /// keeps a leading <c>www.</c>; the list must be configured on the server, otherwise the vendor
        /// raises <c>TLD_LIST_NOT_FOUND</c>. A <see langword="null"/> argument yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? cut_to_first_significant_subdomain_custom_with_www(string? url, string? tldList) => default!;

        /// <summary>
        /// <c>URLHierarchy(url)</c>: the progressively longer URLs of a value, truncated at <c>/</c>,
        /// <c>?</c> and <c>#</c> (<c>https://x/a/b?c=1</c> yields four elements). The server returns
        /// <c>Array(String)</c>; the argument cannot be <c>Nullable</c> (an Array cannot be inside a
        /// Nullable type), hence the non-nullable parameter. Requires a provider that supports it
        /// (ClickHouse).
        /// </summary>
        public string[] url_hierarchy(string url) => default!;

        /// <summary>
        /// <c>URLPathHierarchy(url)</c>: the path-only hierarchy of a URL, without the protocol and host
        /// (<c>https://x/a/b?c=1</c> yields <c>/a/</c>, <c>/a/b?</c>, <c>/a/b?c=1</c>). The argument
        /// cannot be <c>Nullable</c>, hence the non-nullable parameter. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string[] url_path_hierarchy(string url) => default!;

        /// <summary>
        /// <c>extractURLParameter(url, name)</c>: the value of the first <c>name</c> parameter, or an
        /// empty string when it is absent. The vendor requires the query part to start with <c>?</c>
        /// (a bare <c>a=1&amp;b=2</c> yields an empty string), returns the value undecoded, matches names
        /// case-sensitively and treats <c>;</c> as an ordinary value character. A
        /// <see langword="null"/> argument yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? extract_url_parameter(string? url, string? name) => default!;

        /// <summary>
        /// <c>extractURLParameterNames(url)</c>: the parameter names as <c>Array(String)</c>, in order
        /// and undecoded (a value-less parameter contributes its name). The argument cannot be
        /// <c>Nullable</c>, hence the non-nullable parameter. Requires a provider that supports it
        /// (ClickHouse).
        /// </summary>
        public string[] extract_url_parameter_names(string url) => default!;

        /// <summary>
        /// <c>IPv4NumToString(num)</c>: the dotted-quad text of a <c>UInt32</c> address
        /// (<c>16909060</c> becomes <c>1.2.3.4</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? ipv4_num_to_string(uint? value) => default!;

        /// <summary>
        /// <c>IPv4NumToStringClassC(num)</c>: the class-C textual form, where the last octet is the
        /// literal <c>xxx</c> (<c>192.168.1.xxx</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? ipv4_num_to_string_class_c(uint? value) => default!;

        /// <summary>
        /// <c>IPv4StringToNum(value)</c>: the <c>UInt32</c> of a dotted-quad address. A malformed value
        /// (missing or out-of-range octet, leading zero, surrounding space, empty string) raises the
        /// vendor's <c>CANNOT_PARSE_IPV4</c> error; use
        /// <see cref="ipv4_string_to_num_or_null(string?)"/> for a NULL result. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public uint? ipv4_string_to_num(string? value) => default!;

        /// <summary>
        /// <c>IPv4StringToNumOrDefault(value)</c>: the <c>UInt32</c> of a dotted-quad address, or
        /// <c>0</c> for a malformed value; unlike the <c>OrNull</c> sibling a NULL input still yields
        /// NULL. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? ipv4_string_to_num_or_default(string? value) => default!;

        /// <summary>
        /// <c>IPv4StringToNumOrNull(value)</c>: the <c>UInt32</c> of a dotted-quad address, or
        /// <see langword="null"/> for a malformed or <see langword="null"/> input instead of raising.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? ipv4_string_to_num_or_null(string? value) => default!;

        /// <summary>
        /// <c>IPv6NumToString(num)</c>: the compressed textual IPv6 form of the 16-byte binary value
        /// given as 32 hexadecimal digits — the form <see cref="ipv6_string_to_num(string?)"/> returns,
        /// so the pair round-trips. A shorter input is zero-padded by <c>toFixedString</c>, a longer one
        /// is rejected by the server. A <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? ipv6_num_to_string(string? value) => default!;

        /// <summary>
        /// <c>IPv6StringToNum(value)</c>: the 16-byte <c>FixedString(16)</c> binary address as 32
        /// upper-case hexadecimal digits (the explicit hex convention shared with the binary UUID
        /// forms, because the driver does not expose the raw bytes usefully). A malformed value raises
        /// the vendor's <c>CANNOT_PARSE_IPV6</c> error; use
        /// <see cref="ipv6_string_to_num_or_null(string?)"/> for a NULL result. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? ipv6_string_to_num(string? value) => default!;

        /// <summary>
        /// <c>IPv6StringToNumOrDefault(value)</c>: the 16-byte binary address as 32 upper-case
        /// hexadecimal digits, or thirty-two zeros for a malformed value; a NULL input still yields
        /// NULL. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? ipv6_string_to_num_or_default(string? value) => default!;

        /// <summary>
        /// <c>IPv6StringToNumOrNull(value)</c>: the 16-byte binary address as 32 upper-case hexadecimal
        /// digits, or <see langword="null"/> for a malformed or <see langword="null"/> input instead of
        /// raising. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? ipv6_string_to_num_or_null(string? value) => default!;

        /// <summary>
        /// <c>cutIPv6(num, bytesToCutForIPv6, zeroBytesForIPv4)</c>: the binary 16-byte IPv6 value given
        /// as 32 hexadecimal digits, truncated to the first
        /// <paramref name="bytesToCutForIPv6"/> significant bytes (zeroed tails are omitted). The
        /// position arguments are <c>UInt8</c>; <paramref name="zeroBytesForIPv4"/> applies when the
        /// address is IPv4-mapped. A <see langword="null"/> value yields <see langword="null"/>. Requires
        /// a provider that supports it (ClickHouse).
        /// </summary>
        public string? cut_ipv6(string? value, byte bytesToCutForIPv6, byte zeroBytesForIPv4) => default!;

        /// <summary>
        /// <c>isIPv4String(value)</c>: <see langword="true"/> when the whole string is a dotted-quad IPv4
        /// address. The server returns <c>UInt8</c>, surfaced as <see cref="bool"/>; the NULL of a
        /// nullable argument stays <see langword="null"/>, never <see langword="false"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public bool? is_ipv4_string(string? value) => default!;

        /// <summary>
        /// <c>isIPv6String(value)</c>: <see langword="true"/> when the whole string is an IPv6 address
        /// (including the IPv4-mapped form). The server returns <c>UInt8</c>, surfaced as
        /// <see cref="bool"/>; a NULL input stays <see langword="null"/>, never
        /// <see langword="false"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public bool? is_ipv6_string(string? value) => default!;

        /// <summary>
        /// <c>isIPAddressInRange(address, prefix)</c>: <see langword="true"/> when the address lies in
        /// the CIDR range named by <paramref name="prefix"/> (<c>1.2.3.0/24</c>). The vendor returns
        /// <c>UInt8</c> and does NOT propagate NULL: a <see langword="null"/> address yields
        /// <see langword="false"/>, which is why the result is non-nullable; a malformed address or
        /// range raises <c>CANNOT_PARSE_TEXT</c>, and the prefix argument cannot be NULL. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public bool is_ip_address_in_range(string? address, string prefix) => default!;

        /// <summary>
        /// <c>toIPv4(value)</c>: parses the dotted-quad text into the native <c>IPv4</c> type and renders
        /// the address back as its canonical dotted-quad text (<c>toString</c>). The library's row
        /// materializer has no address type, so the exact canonical text is the CLR shape; the server
        /// still produces and consumes the native <c>IPv4</c> value, and
        /// <see cref="ipv4_string_to_num(string?)"/> exposes its <c>UInt32</c> storage form. A malformed
        /// value raises the vendor's <c>CANNOT_PARSE_IPV4</c> error; a <see langword="null"/> input
        /// yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? to_ipv4(string? value) => default!;

        /// <summary>
        /// <c>toIPv4(value)</c> from a <c>UInt32</c> address, rendered as the canonical dotted-quad text
        /// (the vendor converts the numeric form to the native <c>IPv4</c> value first). Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public string to_ipv4(uint value) => default!;

        /// <summary>
        /// <c>toIPv4(value)</c> from a nullable <c>UInt32</c>: the canonical dotted-quad text, or
        /// <see langword="null"/> (<c>Nullable(IPv4)</c>). Requires a provider that supports it
        /// (ClickHouse).
        /// </summary>
        public string? to_ipv4(uint? value) => default!;

        /// <summary>
        /// <c>toIPv6(value)</c>: parses the textual IPv6 address into the native <c>IPv6</c> type and
        /// renders it back as its canonical compressed text (<c>toString</c>); an IPv4 text is accepted
        /// as its IPv4-mapped form (<c>1.2.3.4</c> becomes <c>::ffff:1.2.3.4</c>). The library's row
        /// materializer has no address type, so the canonical text is the CLR shape, while
        /// <see cref="ipv6_string_to_num(string?)"/> exposes the <c>FixedString(16)</c> storage form as
        /// hex. A malformed value raises <c>CANNOT_PARSE_IPV6</c>; a <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? to_ipv6(string? value) => default!;

        /// <summary>
        /// <c>IPv4ToIPv6(address)</c>: the IPv4-mapped IPv6 form of a dotted-quad address
        /// (<c>1.2.3.4</c> becomes <c>::ffff:1.2.3.4</c>), rendered as canonical text. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse).
        /// </summary>
        public string? ipv4_to_ipv6(string? address) => default!;

        /// <summary>
        /// <c>IPv4CIDRToRange(address, prefix)</c>: the first and last address of the IPv4 CIDR block as
        /// <c>Tuple(IPv4, IPv4)</c> — a two-component <see cref="Tuple{T1,T2}"/> of canonical
        /// dotted-quad strings. The prefix is <c>UInt8</c>; a prefix above 32 is accepted by the vendor
        /// and clamped to 32 (a single address), and a negative prefix is rejected by type. The native
        /// <c>IPv4</c> argument cannot be <c>Nullable</c> (a Tuple cannot be inside a Nullable type),
        /// hence the non-nullable parameter. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Tuple<string, string> ipv4_cidr_to_range(string address, byte prefix) => default!;

        /// <summary>
        /// <c>IPv6CIDRToRange(address, prefix)</c>: the first and last address of the IPv6 CIDR block as
        /// <c>Tuple(IPv6, IPv6)</c> — a two-component <see cref="Tuple{T1,T2}"/> of canonical text. The
        /// prefix is <c>UInt8</c>; a prefix above 128 is accepted by the vendor and clamped to 128 (a
        /// single address). The argument cannot be <c>Nullable</c>, hence the non-nullable parameter.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Tuple<string, string> ipv6_cidr_to_range(string address, byte prefix) => default!;

        /// <summary>
        /// <c>bitAnd(left, right)</c>: the bitwise AND of two unsigned integers, surfaced as
        /// <see cref="ulong"/>. The vendor keeps the first argument's width (UInt8/16/32/64); this
        /// surface fixes the result at UInt64 and widens every unsigned input exactly with
        /// <c>toUInt64</c>, which is lossless and cannot change a bitwise AND (the operands are
        /// zero-extended). A <see langword="null"/> operand yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? bit_and(ulong? left, ulong? right) => default!;

        /// <summary>
        /// <c>bitOr(left, right)</c>: the bitwise OR of two unsigned integers, surfaced as
        /// <see cref="ulong"/>. As <see cref="bit_and(ulong?, ulong?)"/>, the inputs are widened
        /// exactly to UInt64; zero extension cannot change a bitwise OR. A
        /// <see langword="null"/> operand yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public ulong? bit_or(ulong? left, ulong? right) => default!;

        /// <summary>
        /// <c>bitXor(left, right)</c>: the bitwise XOR of two unsigned integers, surfaced as
        /// <see cref="ulong"/>. As <see cref="bit_and(ulong?, ulong?)"/>, the inputs are widened
        /// exactly to UInt64; zero extension cannot change a bitwise XOR. A
        /// <see langword="null"/> operand yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public ulong? bit_xor(ulong? left, ulong? right) => default!;

        /// <summary>
        /// <c>bitNot(value)</c> over a <c>UInt8</c>: the one's complement within the 8-bit width,
        /// <see cref="byte"/>. The width is part of the function's semantics, so each width has its
        /// own overload. A <see langword="null"/> input yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public byte? bit_not(byte? value) => default!;

        /// <summary>
        /// <c>bitNot(value)</c> over a <c>UInt16</c>. The vendor returns <c>UInt16</c>, which the
        /// shared row reader cannot materialize; the result is widened exactly with <c>toInt32</c>
        /// (0..65535 is lossless) and surfaced as <see cref="int"/>. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public int? bit_not(ushort? value) => default!;

        /// <summary>
        /// <c>bitNot(value)</c> over a <c>UInt32</c>: the one's complement within the 32-bit width,
        /// <see cref="uint"/>. A <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? bit_not(uint? value) => default!;

        /// <summary>
        /// <c>bitNot(value)</c> over a <c>UInt64</c>: the one's complement within the 64-bit width,
        /// <see cref="ulong"/>. A <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? bit_not(ulong? value) => default!;

        /// <summary>
        /// <c>bitShiftLeft(value, shift)</c> over a <c>UInt8</c>, <see cref="byte"/>. The shift count
        /// is the vendor's <c>UInt8</c>; on 25.8.33.6 a count of 0..7 shifts normally, a count equal
        /// to the width returns 0 and any larger count returns 0 as well. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public byte? bit_shift_left(byte? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftLeft(value, shift)</c> over a <c>UInt16</c>, widened exactly to
        /// <see cref="int"/> with <c>toInt32</c>. The shift-count semantics follow the vendor
        /// (0..15 shift, a count of 16 or more yields 0). A <see langword="null"/> value yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? bit_shift_left(ushort? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftLeft(value, shift)</c> over a <c>UInt32</c>, <see cref="uint"/>. The vendor
        /// returns 0 for a count of 33 or more; on the pinned 25.8.33.6 server a count equal to 32
        /// returns the value unchanged (a vendor quirk, pinned by the live tests). A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public uint? bit_shift_left(uint? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftLeft(value, shift)</c> over a <c>UInt64</c>, <see cref="ulong"/>. The vendor
        /// returns 0 for a count of 65 or more; on the pinned 25.8.33.6 server a count equal to 64
        /// returns the value unchanged (a vendor quirk, pinned by the live tests). A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public ulong? bit_shift_left(ulong? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftRight(value, shift)</c> over a <c>UInt8</c>, <see cref="byte"/>. Counts
        /// 0..7 shift normally; a count of 8 or more yields 0. A <see langword="null"/> value
        /// yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? bit_shift_right(byte? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftRight(value, shift)</c> over a <c>UInt16</c>, widened exactly to
        /// <see cref="int"/>. Counts 0..15 shift normally; a count of 16 or more yields 0. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public int? bit_shift_right(ushort? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftRight(value, shift)</c> over a <c>UInt32</c>, <see cref="uint"/>. On the
        /// pinned 25.8.33.6 server a count equal to 32 returns the value unchanged and 33 or more
        /// yields 0 (a vendor quirk). A <see langword="null"/> value yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? bit_shift_right(uint? value, byte shift) => default!;

        /// <summary>
        /// <c>bitShiftRight(value, shift)</c> over a <c>UInt64</c>, <see cref="ulong"/>. On the
        /// pinned 25.8.33.6 server a count equal to 64 returns the value unchanged and 65 or more
        /// yields 0 (a vendor quirk). A <see langword="null"/> value yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? bit_shift_right(ulong? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateLeft(value, shift)</c> over a <c>UInt8</c>, <see cref="byte"/>. The rotation
        /// is modulo the 8-bit width, so counts 0, 8 and 255 are equivalent. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public byte? bit_rotate_left(byte? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateLeft(value, shift)</c> over a <c>UInt16</c>, widened exactly to
        /// <see cref="int"/>. The rotation is modulo the 16-bit width. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public int? bit_rotate_left(ushort? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateLeft(value, shift)</c> over a <c>UInt32</c>, <see cref="uint"/>. The
        /// rotation is modulo the 32-bit width. A <see langword="null"/> value yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? bit_rotate_left(uint? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateLeft(value, shift)</c> over a <c>UInt64</c>, <see cref="ulong"/>. The
        /// rotation is modulo the 64-bit width. A <see langword="null"/> value yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? bit_rotate_left(ulong? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateRight(value, shift)</c> over a <c>UInt8</c>, <see cref="byte"/>. The rotation
        /// is modulo the 8-bit width. A <see langword="null"/> value yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? bit_rotate_right(byte? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateRight(value, shift)</c> over a <c>UInt16</c>, widened exactly to
        /// <see cref="int"/>. The rotation is modulo the 16-bit width. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public int? bit_rotate_right(ushort? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateRight(value, shift)</c> over a <c>UInt32</c>, <see cref="uint"/>. The
        /// rotation is modulo the 32-bit width. A <see langword="null"/> value yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? bit_rotate_right(uint? value, byte shift) => default!;

        /// <summary>
        /// <c>bitRotateRight(value, shift)</c> over a <c>UInt64</c>, <see cref="ulong"/>. The
        /// rotation is modulo the 64-bit width. A <see langword="null"/> value yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? bit_rotate_right(ulong? value, byte shift) => default!;

        /// <summary>
        /// <c>bitCount(value)</c>: the number of set bits (popcount) of an unsigned integer. The
        /// vendor returns <c>UInt8</c> (0..64), surfaced as <see cref="byte"/>; the input is widened
        /// exactly to <c>UInt64</c>, which cannot change the popcount. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public byte? bit_count(ulong? value) => default!;

        /// <summary>
        /// <c>bitTest(value, bitPosition)</c> over a <c>UInt8</c>: whether the zero-based bit is
        /// set, surfaced as <see cref="bool"/> (the vendor returns <c>UInt8</c> 0/1). A position
        /// outside 0..7 is rejected by the server (<c>PARAMETER_OUT_OF_BOUND</c>), never silently
        /// read as zero. A <see langword="null"/> value yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public bool? bit_test(byte? value, byte bitPosition) => default!;

        /// <summary>
        /// <c>bitTest(value, bitPosition)</c> over a <c>UInt16</c>: whether the zero-based bit is
        /// set, surfaced as <see cref="bool"/>. A position outside 0..15 is rejected by the server.
        /// A <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test(ushort? value, byte bitPosition) => default!;

        /// <summary>
        /// <c>bitTest(value, bitPosition)</c> over a <c>UInt32</c>: whether the zero-based bit is
        /// set, surfaced as <see cref="bool"/>. A position outside 0..31 is rejected by the server.
        /// A <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test(uint? value, byte bitPosition) => default!;

        /// <summary>
        /// <c>bitTest(value, bitPosition)</c> over a <c>UInt64</c>: whether the zero-based bit is
        /// set, surfaced as <see cref="bool"/>. A position outside 0..63 is rejected by the server.
        /// A <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test(ulong? value, byte bitPosition) => default!;

        /// <summary>
        /// <c>bitTestAll(value, bit1, bit2, ...)</c> over a <c>UInt8</c>: whether every named bit is
        /// set, surfaced as <see cref="bool"/>. The vendor requires the value plus at least one bit
        /// position; an empty position list is rejected by the translator. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_all(byte? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAll(value, bit1, bit2, ...)</c> over a <c>UInt16</c>: whether every named bit
        /// is set, surfaced as <see cref="bool"/>. Requires at least one bit position. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_all(ushort? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAll(value, bit1, bit2, ...)</c> over a <c>UInt32</c>: whether every named bit
        /// is set, surfaced as <see cref="bool"/>. Requires at least one bit position. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_all(uint? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAll(value, bit1, bit2, ...)</c> over a <c>UInt64</c>: whether every named bit
        /// is set, surfaced as <see cref="bool"/>. Requires at least one bit position. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_all(ulong? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAny(value, bit1, bit2, ...)</c> over a <c>UInt8</c>: whether any named bit is
        /// set, surfaced as <see cref="bool"/>. The vendor requires the value plus at least one bit
        /// position; an empty position list is rejected by the translator. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_any(byte? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAny(value, bit1, bit2, ...)</c> over a <c>UInt16</c>: whether any named bit is
        /// set, surfaced as <see cref="bool"/>. Requires at least one bit position. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_any(ushort? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAny(value, bit1, bit2, ...)</c> over a <c>UInt32</c>: whether any named bit is
        /// set, surfaced as <see cref="bool"/>. Requires at least one bit position. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_any(uint? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitTestAny(value, bit1, bit2, ...)</c> over a <c>UInt64</c>: whether any named bit is
        /// set, surfaced as <see cref="bool"/>. Requires at least one bit position. A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? bit_test_any(ulong? value, params byte[] bitPositions) => default!;

        /// <summary>
        /// <c>bitHammingDistance(left, right)</c> over two unsigned integers: the number of differing
        /// bits, surfaced as <see cref="byte"/> (the vendor returns <c>UInt8</c>, 0..64). The
        /// operands are widened exactly to <c>UInt64</c>; zero extension cannot change the distance.
        /// A <see langword="null"/> operand yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public byte? bit_hamming_distance(ulong? left, ulong? right) => default!;

        /// <summary>
        /// <c>bitHammingDistance(left, right)</c> over two strings: the number of differing bits
        /// over the UTF-8 bytes, the shorter input zero-padded. The vendor returns <c>UInt64</c> for
        /// the string overload (unlike the <c>UInt8</c> integer overload), surfaced as
        /// <see cref="ulong"/>. A <see langword="null"/> operand yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? bit_hamming_distance(string? left, string? right) => default!;

        /// <summary>
        /// <c>editDistance(left, right)</c>: the Levenshtein distance between the two strings measured
        /// over their UTF-8 bytes, so a multi-byte character counts per byte (<c>'ёж'</c> versus
        /// <c>'еж'</c> is 2). The server returns <c>UInt64</c>; it is surfaced as <see cref="long"/>
        /// (<c>toInt64</c>). A <see langword="null"/> argument yields <see langword="null"/>. Requires a
        /// provider that supports it (ClickHouse).
        /// </summary>
        public long? edit_distance(string? left, string? right) => default!;

        /// <summary>
        /// <c>jaroSimilarity(left, right)</c>: the Jaro similarity of the two strings as <c>Float64</c>,
        /// from 0 (no common characters) to 1 (equal strings; two empty strings are 0). A
        /// <see langword="null"/> argument yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public double? jaro_similarity(string? left, string? right) => default!;

        /// <summary>
        /// <c>byteHammingDistance(left, right)</c>: the number of differing bytes; the shorter input is
        /// zero-padded to the longer one. The server returns <c>UInt64</c>; it is surfaced as
        /// <see cref="long"/> (<c>toInt64</c>). A <see langword="null"/> argument yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? byte_hamming_distance(string? left, string? right) => default!;

        /// <summary>
        /// <c>substringIndex(value, delimiter, count)</c>: the substring of <paramref name="value"/>
        /// before the <paramref name="count"/>-th occurrence of the single-character
        /// <paramref name="delimiter"/>; a negative <paramref name="count"/> counts from the end and a
        /// zero count yields the empty string. A multi-character delimiter is rejected by the server. A
        /// <see langword="null"/> argument yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? substring_index(string? value, string? delimiter, int count) => default!;

        /// <summary>
        /// <c>countSubstrings(value, substring)</c> or <c>countSubstrings(value, substring, start)</c>:
        /// the number of non-overlapping occurrences of <paramref name="substring"/>, optionally counted
        /// from the 1-based position <paramref name="start"/>. An empty
        /// <paramref name="substring"/> counts zero. The server returns <c>UInt64</c>; it is surfaced as
        /// <see cref="long"/> (<c>toInt64</c>). A <see langword="null"/> argument yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? count_substrings(string? value, string? substring, int? start = null) => default!;

        /// <summary>
        /// <c>extractGroups(value, pattern)</c>: the capture groups of the first RE2 match as
        /// <c>Array(String)</c>, surfaced as <c>string[]</c>; a pattern that does not match yields an empty
        /// array. ClickHouse cannot materialise a <c>Nullable(Array(String))</c> result
        /// (<c>Nested type Array(String) cannot be inside Nullable type</c>, Code 43), so both arguments
        /// must be non-nullable expressions: a nullable column or a <see langword="null"/> literal is
        /// rejected by the server rather than silently treated as an empty array. An empty pattern is
        /// rejected too. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string[] extract_groups(string? value, string? pattern) => default!;

        /// <summary>
        /// <c>normalizeUTF8NFC(value)</c>: the Unicode NFC (canonical composition) normalization of
        /// <paramref name="value"/>. An empty input yields the empty string and a <see langword="null"/>
        /// input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? normalize_utf8_nfc(string? value) => default!;

        /// <summary>
        /// <c>normalizeUTF8NFD(value)</c>: the Unicode NFD (canonical decomposition) normalization of
        /// <paramref name="value"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? normalize_utf8_nfd(string? value) => default!;

        /// <summary>
        /// <c>normalizeUTF8NFKC(value)</c>: the Unicode NFKC (compatibility composition) normalization of
        /// <paramref name="value"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? normalize_utf8_nfkc(string? value) => default!;

        /// <summary>
        /// <c>normalizeUTF8NFKD(value)</c>: the Unicode NFKD (compatibility decomposition) normalization
        /// of <paramref name="value"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? normalize_utf8_nfkd(string? value) => default!;

        /// <summary>
        /// <c>formatDateTime(value, format)</c> or <c>formatDateTime(value, format, timezone)</c>:
        /// formats <paramref name="value"/> with a ClickHouse format string. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? format_date_time(DateTime? value, string? format, string? timezone = null) => default!;

        /// <summary>
        /// <c>parseDateTime(value, format)</c>: parses <paramref name="value"/> with the ClickHouse
        /// <paramref name="format"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public DateTime? parse_date_time(string? value, string? format) => default!;

        /// <summary><c>parseDateTimeBestEffort(value[, timezone])</c>: parses a date/time in any of the supported formats.</summary>
        public DateTime? parse_date_time_best_effort(string? value, string? timezone = null) => default!;

        /// <summary><c>now()</c>: the current date and time in the server time zone. Requires a provider that supports it (ClickHouse).</summary>
        public DateTime? now() => default!;

        /// <summary><c>today()</c>: the current date in the server time zone.</summary>
        public DateTime? today() => default!;

        /// <summary><c>yesterday()</c>: the date one day before <see cref="today()"/>.</summary>
        public DateTime? yesterday() => default!;

        /// <summary>
        /// <c>arrayConcat(arrays...)</c>: concatenates the arrays into one. Returns an array, so it can
        /// be projected directly or used as the operand of another array function. Requires a provider
        /// that supports array functions (see <see cref="ISqlDialect.SupportsArrayFunctions"/>;
        /// ClickHouse).
        /// </summary>
        public T[] array_concat<T>(params T[][] arrays) => default!;

        /// <summary>
        /// <c>arrayFlatten(array)</c>: flattens one level of nesting of an <c>Array(Array(T))</c>.
        /// Returns an array, so it can be projected directly or nested. Requires a provider that
        /// supports array functions (see <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T[] array_flatten<T>(T[][] arrays) => default!;

        /// <summary>
        /// <c>arrayUniq(array)</c>: the number of distinct elements. Requires a provider that supports
        /// array functions (see <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public long array_uniq<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayIntersect(arrays...)</c>: the elements common to all the arrays. Returns an array, so
        /// it can be projected directly or nested. Requires a provider that supports array functions
        /// (see <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T[] array_intersect<T>(params T[][] arrays) => default!;

        /// <summary><c>arrayUnion(arrays...)</c>: the union of the arrays. Returns an array, so it can be projected directly or nested.</summary>
        public T[] array_union<T>(params T[][] arrays) => default!;

        /// <summary>
        /// <c>arrayExcept(arrays...)</c>: the elements of the first array that are not in the others.
        /// The native name exists only from ClickHouse 25.9; the current provider has no server-version
        /// awareness, so on the pinned 25.8 baseline the call is refused with
        /// <see cref="NotSupportedException"/> at translation/preparation time and no SQL is emitted.
        /// The signature is retained for API compatibility; it is not executable here.
        /// </summary>
        public T[] array_except<T>(params T[][] arrays) => default!;

        /// <summary><c>arraySymmetricDifference(arrays...)</c>: the elements that occur in an odd number of the arrays. Returns an array, so it can be projected directly or nested.</summary>
        public T[] array_symmetric_difference<T>(params T[][] arrays) => default!;

        // ---------------------------------------------------------------------------------------
        // Array family (F05). Every member is translated by ArraySqlTranslator and gated by
        // ISqlDialect.SupportsArrayFunctions, exactly like the array surface above; the version-gap
        // members are rendered from the vendor definition on the pinned 25.8 server.
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// <c>arrayElement(array, index)</c>: the element at the one-based <paramref name="index"/>.
        /// Arrays are one-based and a negative index counts from the end (<c>-1</c> is the last
        /// element); an index outside the bounds returns the element type's default instead of an
        /// error, which is the vendor contract rather than a client-side substitution. A NULL element
        /// is returned as NULL (<c>Nullable(T)</c>), and a nullable element array keeps its positions.
        /// Requires a provider that supports array functions (see
        /// <see cref="ISqlDialect.SupportsArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T array_element<T>(T[] array, long index) => default!;

        /// <summary>
        /// <c>arrayEnumerateDense(array)</c>: for each element, the one-based number of the position at
        /// which its value first appears (<c>[10, 20, 10, 30]</c> is <c>[1, 2, 1, 3]</c>). The server
        /// returns <c>Array(UInt32)</c>; the arm casts it to <c>Array(Int64)</c>, so the CLR result is
        /// <c>long[]</c>. Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public long[] array_enumerate_dense<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayZip(array1, array2)</c>: the pairwise tuples of two arrays of the <b>same</b>
        /// length (the server rejects mismatched sizes rather than truncating). The result is an
        /// <c>Array(Tuple(T1, T2))</c>, surfaced as <see cref="Tuple{T1,T2}"/>[]. Requires a provider
        /// that supports array functions (ClickHouse).
        /// </summary>
        public Tuple<T1, T2>[] array_zip<T1, T2>(T1[] array1, T2[] array2) => default!;

        /// <summary>
        /// <c>arrayDifference(array)</c>: the differences between adjacent elements, with a leading
        /// <c>0</c> (<c>[1, 2, 3, 4]</c> is <c>[0, 1, 1, 1]</c>). The vendor widens the subtraction
        /// result (<c>Int16</c> input → <c>Array(Int32)</c>, <c>Int32</c>/<c>Int64</c> input →
        /// <c>Array(Int64)</c>); the arm casts to <c>Array(Int64)</c>, so the CLR result is
        /// <c>long[]</c> and only integral element types are accepted — a floating-point or decimal
        /// element type is refused at translation time because its fractional differences cannot be
        /// carried by the declared <c>long[]</c>. Requires a provider that supports array functions
        /// (ClickHouse).
        /// </summary>
        public long[] array_difference<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayAvg(array)</c>: the average of the elements as <c>Float64</c>; an empty array yields
        /// <c>0</c>. A <c>Nullable</c>-element array is rejected by the server ("array aggregation
        /// function cannot be performed on type Nullable(...)") because no nullable overload exists.
        /// Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public double array_avg<T>(T[] array) => default!;

        /// <summary>
        /// <c>arraySum(array)</c>: the sum of the elements. The vendor widens the result
        /// (<c>Int32</c> input → <c>Int64</c>, <c>Float32</c> → <c>Float64</c>,
        /// <c>Decimal</c> → <c>Decimal(38, s)</c>); the declared CLR result is <see cref="long"/> and
        /// only integral element types except <see cref="ulong"/> are accepted — a floating-point,
        /// decimal or unsigned-64-bit element type is refused at translation time rather than
        /// truncated into the declared <see cref="long"/>. An empty array yields <c>0</c>, and a
        /// <c>Nullable</c>-element array is rejected by the server. Requires a provider that supports
        /// array functions (ClickHouse).
        /// </summary>
        public long array_sum<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayMin(array)</c>: the minimum element, in the element's own type (an empty array
        /// yields the type's default). A <c>Nullable</c>-element array is rejected by the server.
        /// Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T array_min<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayMax(array)</c>: the maximum element, in the element's own type (an empty array
        /// yields the type's default). A <c>Nullable</c>-element array is rejected by the server.
        /// Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T array_max<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayProduct(array)</c>: the product of the elements as <c>Float64</c> (an empty array
        /// yields <c>0</c>). A <c>Nullable</c>-element array is rejected by the server. Requires a
        /// provider that supports array functions (ClickHouse).
        /// </summary>
        public double array_product<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayTopK(array, k)</c>: up to <paramref name="k"/> largest elements, sorted in
        /// descending order, with NULL elements skipped. The native name arrived in ClickHouse 26.6
        /// and is absent on the pinned 25.8 server, so the arm renders the vendor definition
        /// <c>arraySlice(arrayReverseSort(arrayFilter(x -&gt; isNotNull(x), array)), 1, k)</c>, which
        /// has the same sorted-non-null contract and is independent of the server version. Requires a
        /// provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_top_k<T>(T[] array, long k) => default!;

        /// <summary>
        /// <c>arrayBottomK(array, k)</c>: up to <paramref name="k"/> smallest elements, sorted in
        /// ascending order, with NULL elements skipped. The native name arrived in ClickHouse 26.6 and
        /// is absent on the pinned 25.8 server, so the arm renders the vendor definition
        /// <c>arraySlice(arraySort(arrayFilter(x -&gt; isNotNull(x), array)), 1, k)</c>. Requires a
        /// provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_bottom_k<T>(T[] array, long k) => default!;

        /// <summary>
        /// <c>arrayRandomSample(array, samples)</c>: a random sample of at most
        /// <paramref name="samples"/> elements; a larger request returns every element in an
        /// unspecified order and zero returns an empty array. The function is non-deterministic, so
        /// only the sample size and membership are stable. Requires a provider that supports array
        /// functions (ClickHouse).
        /// </summary>
        public T[] array_random_sample<T>(T[] array, long samples) => default!;

        /// <summary>
        /// <c>arrayCompact(array)</c>: removes <b>consecutive</b> duplicates, NULLs included
        /// (<c>[1, 1, NULL, NULL, 2]</c> is <c>[1, NULL, 2]</c>); the order is preserved. Requires a
        /// provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_compact<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayPopBack(array)</c>: the array without its last element (an empty array stays
        /// empty). Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_pop_back<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayPopFront(array)</c>: the array without its first element (an empty array stays
        /// empty). Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_pop_front<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayPushFront(array, element)</c>: the array with <paramref name="element"/> prepended.
        /// A NULL element is appended as NULL and widens the element type to <c>Nullable(T)</c>; it
        /// is never replaced by a default. Requires a provider that supports array functions
        /// (ClickHouse).
        /// </summary>
        public T[] array_push_front<T>(T[] array, T element) => default!;

        /// <summary>
        /// <c>arrayResize(array, size)</c>: changes the length, truncating from the right or extending
        /// with the element type's default. A negative <paramref name="size"/> keeps the last
        /// <c>|size|</c> elements (the vendor rule, not a client-side clamp) and zero yields an empty
        /// array. Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_resize<T>(T[] array, long size) => default!;

        /// <summary>
        /// <c>arrayResize(array, size, extender)</c>: as <see cref="array_resize{T}(T[], long)"/> but
        /// extension uses <paramref name="extender"/>; a NULL extender fills with NULL (it widens the
        /// element type to <c>Nullable(T)</c>) rather than the element default. Requires a provider
        /// that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_resize<T>(T[] array, long size, T extender) => default!;

        /// <summary>
        /// <c>arrayRemove(array, element)</c>: removes every element equal to
        /// <paramref name="element"/>, with NULLs treated as equal to NULL (unlike a plain
        /// <c>arrayFilter</c>, whose NULL comparison would keep them). The native name arrived in
        /// ClickHouse 25.11 and is absent on the pinned 25.8 server, so the arm renders the equivalent
        /// <c>arrayFilter(x -&gt; not coalesce(x = element, isNull(x) AND isNull(element)), array)</c>.
        /// Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_remove<T>(T[] array, T element) => default!;

        /// <summary>
        /// <c>arrayRotateLeft(array, n)</c>: rotates the elements left by <paramref name="n"/>
        /// positions (a negative <paramref name="n"/> rotates right; an <paramref name="n"/> larger
        /// than the length wraps). No elements are added or removed. Requires a provider that supports
        /// array functions (ClickHouse).
        /// </summary>
        public T[] array_rotate_left<T>(T[] array, long n) => default!;

        /// <summary>
        /// <c>arrayRotateRight(array, n)</c>: rotates the elements right by <paramref name="n"/>
        /// positions (a negative <paramref name="n"/> rotates left). Requires a provider that supports
        /// array functions (ClickHouse).
        /// </summary>
        public T[] array_rotate_right<T>(T[] array, long n) => default!;

        /// <summary>
        /// <c>arrayShiftLeft(array, n)</c>: shifts the elements left by <paramref name="n"/> positions,
        /// filling the vacated positions with the element type's default; a negative
        /// <paramref name="n"/> shifts right. Requires a provider that supports array functions
        /// (ClickHouse).
        /// </summary>
        public T[] array_shift_left<T>(T[] array, long n) => default!;

        /// <summary>
        /// <c>arrayShiftLeft(array, n, default)</c>: as <see cref="array_shift_left{T}(T[], long)"/>
        /// but the vacated positions are filled with <paramref name="default"/>. On the pinned server a
        /// NULL fill value makes the whole result NULL (the vendor returns <c>Nullable(Nothing)</c>)
        /// instead of filling with NULLs; the arm passes it through rather than substituting a default.
        /// Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T[] array_shift_left<T>(T[] array, long n, T @default) => default!;

        /// <summary>
        /// <c>arrayShiftRight(array, n)</c>: shifts the elements right by <paramref name="n"/>
        /// positions, filling the vacated positions with the element type's default; a negative
        /// <paramref name="n"/> shifts left. Requires a provider that supports array functions
        /// (ClickHouse).
        /// </summary>
        public T[] array_shift_right<T>(T[] array, long n) => default!;

        /// <summary>
        /// <c>arrayShiftRight(array, n, default)</c>: as
        /// <see cref="array_shift_right{T}(T[], long)"/> but the vacated positions are filled with
        /// <paramref name="default"/>; a NULL fill value makes the whole result NULL on the pinned
        /// server, as with <see cref="array_shift_left{T}(T[], long, T)"/>. Requires a provider that
        /// supports array functions (ClickHouse).
        /// </summary>
        public T[] array_shift_right<T>(T[] array, long n, T @default) => default!;

        /// <summary>
        /// <c>arrayReduce(aggregate, array)</c> — <b>refused</b>. The aggregate is named by a runtime
        /// SQL string (<c>'sum'</c>, <c>'uniqUpTo(6)'</c>), so there is no typed aggregate contract
        /// that a signature could close: any string reaching the server is an opaque SQL fragment.
        /// The call is rejected deterministically at translation/preparation time, before any SQL is
        /// emitted, on every provider; use the typed array functions (<see cref="array_sum{T}(T[])"/>,
        /// <see cref="array_min{T}(T[])"/>, <see cref="array_max{T}(T[])"/>,
        /// <see cref="array_avg{T}(T[])"/>, <see cref="array_product{T}(T[])"/>) for the common
        /// aggregates.
        /// </summary>
        public T array_reduce<T>(string aggregate, T[] array) => throw new NotSupportedException(
            "The array_reduce function is refused: the aggregate is named by a runtime SQL string, so there is no typed aggregate contract that can be closed by a signature.");

        /// <summary>
        /// <c>arrayReduceInRanges(aggregate, ranges, array)</c> — <b>refused</b>. As
        /// <see cref="array_reduce{T}(string, T[])"/>, the aggregate is named by a runtime SQL string
        /// (<c>'sum'</c>, <c>'uniqUpTo(6)'</c>), so no typed aggregate contract can close it and the
        /// string would reach the server as an opaque SQL fragment; the <c>(start, end)</c> pairs only
        /// select the ranges and do not remove that contract. The call is rejected deterministically at
        /// translation/preparation time, before any SQL is emitted, on every provider; use the typed
        /// array functions over <c>array_slice</c> for the common aggregates.
        /// </summary>
        public T array_reduce_in_ranges<T>(
            string aggregate, (long Start, long End)[] ranges, T[] array) => throw new NotSupportedException(
            "The array_reduce_in_ranges function is refused: the aggregate is named by a runtime SQL string, so there is no typed aggregate contract that can be closed by a signature.");

        /// <summary>
        /// <c>arrayFold(lambda, array, accumulator)</c>: folds the array into the accumulator, calling
        /// <c>lambda(acc, element)</c> for each element in order. The lambda must have exactly two
        /// parameters and its result type must equal the accumulator type (the server rejects a
        /// mismatch with <c>TYPE_MISMATCH</c>). The lambda may reference the enclosing query's columns
        /// and captured parameters. Requires a provider that supports the higher-order array functions
        /// (see <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public TAccumulator array_fold<T, TAccumulator>(
            Expression<Func<TAccumulator, T, TAccumulator>> function, T[] array, TAccumulator accumulator) => default!;

        /// <summary>
        /// <c>arrayFill(lambda, array)</c>: scans the array from the first element, replacing an
        /// element with the previous <b>current</b> element when the predicate is false; the first
        /// element is always preserved (<c>arrayFill(x -&gt; not isNull(x), [1, NULL, 2, NULL])</c> is
        /// <c>[1, 1, 2, 2]</c>). The lambda has exactly one parameter. Requires a provider that
        /// supports the higher-order array functions (ClickHouse).
        /// </summary>
        public T[] array_fill<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayReverseFill(lambda, array)</c>: as <see cref="array_fill{T}"/> but scanned from the
        /// last element, whose value is always preserved. Requires a provider that supports the
        /// higher-order array functions (ClickHouse).
        /// </summary>
        public T[] array_reverse_fill<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arraySplit(lambda, array)</c>: splits the array so that a new sub-array <b>starts</b> at
        /// every element for which the predicate is true (<c>arraySplit(x -&gt; x &gt; 1, [1, 2, 3])</c>
        /// is <c>[[1], [2], [3]]</c>); the element belongs to the new sub-array. An
        /// <c>Array(Array(T))</c> is surfaced as a jagged <c>T[][]</c>. Requires a provider that
        /// supports the higher-order array functions (ClickHouse).
        /// </summary>
        public T[][] array_split<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayReverseSplit(lambda, array)</c>: the reverse split — a sub-array <b>ends after</b>
        /// every element for which the predicate is true (<c>arrayReverseSplit(x -&gt; x &gt; 1,
        /// [1, 2, 3])</c> is <c>[[1, 2], [3]]</c>); a true at the last position adds no trailing empty
        /// sub-array. Returns an <c>Array(Array(T))</c>, surfaced as <c>T[][]</c>. Requires a provider
        /// that supports the higher-order array functions (ClickHouse).
        /// </summary>
        public T[][] array_reverse_split<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

        /// <summary>
        /// <c>arrayReverseSort(array)</c>: the elements in descending order; NULL elements sort last,
        /// which is the vendor order (asserted rather than corrected). Requires a provider that
        /// supports array functions (ClickHouse).
        /// </summary>
        public T[] array_reverse_sort<T>(T[] array) => default!;

        /// <summary>
        /// <c>arrayPartialSort(limit, array)</c>: the same-size array whose first
        /// <paramref name="limit"/> elements are sorted ascending; the remaining elements are in an
        /// unspecified order and the function is non-deterministic. A <paramref name="limit"/> larger
        /// than the array sorts the whole array. Requires a provider that supports array functions
        /// (ClickHouse).
        /// </summary>
        public T[] array_partial_sort<T>(T[] array, long limit) => default!;

        /// <summary>
        /// <c>arrayShingles(array, length)</c>: the consecutive sub-arrays of
        /// <paramref name="length"/> elements (<c>[[1, 2, 3], [2, 3, 4]]</c> for
        /// <c>[1, 2, 3, 4]</c> and <c>3</c>). A length greater than the array is rejected by the
        /// server. Returns an <c>Array(Array(T))</c>, surfaced as <c>T[][]</c>. Requires a provider
        /// that supports array functions (ClickHouse).
        /// </summary>
        public T[][] array_shingles<T>(T[] array, long length) => default!;

        /// <summary>
        /// <c>arrayTranspose(array)</c>: the transpose of a two-dimensional array, where
        /// <c>result[i][j]</c> equals <c>array[j][i]</c>; every inner array must have the same length.
        /// The native name arrived in ClickHouse 26.4 and is absent on the pinned 25.8 server, so the
        /// arm renders the definition <c>arrayMap(i -&gt; arrayMap(row -&gt; row[i], array), range(1,
        /// length(array[1]) + 1))</c>, which is the transposition itself and is
        /// version-independent. Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public T[][] array_transpose<T>(T[][] array) => default!;

        /// <summary>
        /// <c>countEqual(array, element)</c>: the number of elements equal to
        /// <paramref name="element"/> (NULL counts as equal to NULL). The server returns
        /// <c>UInt64</c>; the arm lowers it with <c>toInt64</c>, so the CLR result is
        /// <see cref="long"/>. Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public long count_equal<T>(T[] array, T element) => default!;

        /// <summary>
        /// <c>arrayWithConstant(length, value)</c>: an array of <paramref name="length"/> copies of
        /// <paramref name="value"/>. A NULL value produces an array of NULLs
        /// (<c>Array(Nullable(T))</c>) rather than a default. Requires a provider that supports array
        /// functions (ClickHouse).
        /// </summary>
        public T[] array_with_constant<T>(long length, T value) => default!;

        /// <summary>
        /// <c>arrayJaccardIndex(array1, array2)</c>: the vendor's Jaccard index of the two arrays, as
        /// <c>Float64</c>: the number of distinct common elements divided by
        /// <c>length(array1) + length(array2) - common</c>. The classic set formula holds only for
        /// unique elements — <c>[1, 1, 2]</c> against <c>[1, 2, 3]</c> is <c>0.5</c>, while
        /// <c>[1, 2, 3, 2]</c> against itself is <c>0.6</c> because the raw lengths still count the
        /// duplicates. Two empty arrays are rejected by the server. Requires a provider that supports
        /// array functions (ClickHouse).
        /// </summary>
        public double array_jaccard_index<T>(T[] array1, T[] array2) => default!;

        /// <summary>
        /// <c>arraySimilarity(from, to, fromWeights, toWeights)</c>: the weighted Levenshtein
        /// similarity between <c>0</c> and <c>1</c> as <c>Float64</c>. On the pinned 25.8 server the
        /// four-argument form is the only one (the two-argument shortcut is rejected with a
        /// number-of-arguments error). Requires a provider that supports array functions (ClickHouse).
        /// </summary>
        public double array_similarity<T>(
            T[] from, T[] to, double[] fromWeights, double[] toWeights) => default!;

        /// <summary>
        /// <c>map(key1, value1, key2, value2, ...)</c>: builds a ClickHouse <c>Map</c> from the given
        /// key/value pairs, surfaced as <see cref="IDictionary{TKey,TValue}"/>. The pairs must be inline
        /// tuples. Requires a provider that supports the map surface (ClickHouse).
        /// </summary>
        public IDictionary<TKey, TValue> map<TKey, TValue>(params Tuple<TKey, TValue>[] pairs) => default!;

        /// <summary><c>mapKeys(map)</c>: the keys of the map, surfaced as <c>TKey[]</c>. Requires a provider that supports the map surface (ClickHouse).</summary>
        public TKey[] map_keys<TKey, TValue>(IDictionary<TKey, TValue>? map) => default!;

        /// <summary><c>mapValues(map)</c>: the values of the map, surfaced as <c>TValue[]</c>.</summary>
        public TValue[] map_values<TKey, TValue>(IDictionary<TKey, TValue>? map) => default!;

        /// <summary><c>mapContainsKey(map, key)</c>: true when the map has the key.</summary>
        public bool map_contains_key<TKey, TValue>(IDictionary<TKey, TValue>? map, TKey? key) => default!;

        /// <summary><c>mapContainsValue(map, value)</c>: true when the map has the value.</summary>
        public bool map_contains_value<TKey, TValue>(IDictionary<TKey, TValue>? map, TValue? value) => default!;

        /// <summary><c>mapAdd(map1, map2)</c>: the union of the maps, summing the values of repeated keys.</summary>
        public IDictionary<TKey, TValue>? map_add<TKey, TValue>(IDictionary<TKey, TValue>? map1, IDictionary<TKey, TValue>? map2) => default!;

        /// <summary><c>mapConcat(maps...)</c>: the union of the maps; on a repeated key the value of the last map wins.</summary>
        public IDictionary<TKey, TValue>? map_concat<TKey, TValue>(params IDictionary<TKey, TValue>[] maps) => default!;

        /// <summary>
        /// The entries of the map for which the lambda returns true, surfaced as exactly
        /// <see cref="Tuple{TKey,TValue}"/>[]. ClickHouse has no native function with this result
        /// shape, so the call is lowered to
        /// <c>arrayFilter(_t -&gt; predicate, arrayZip(mapKeys(map), mapValues(map)))</c>; the lambda's
        /// key and value are rebound to the tuple fields (<c>_t.1</c>/<c>_t.2</c>) of one generated
        /// identifier, and the completed lowering is wrapped in
        /// <c>cast(... as Array(Tuple(TKey, TValue)))</c>, the declared-type normalisation.
        /// <para>
        /// This is a documented result-shape break: earlier releases surfaced
        /// <see cref="IDictionary{TKey,TValue}"/>, which no provider can materialize from a projected
        /// expression. Read the returned tuples with LINQ once the array is materialized:
        /// <c>filtered.Select(t =&gt; t.Item2)</c> for the values, or
        /// <c>filtered.ToDictionary(t =&gt; t.Item1, t =&gt; t.Item2)</c> for a client-side dictionary.
        /// There is no public path back to a server-side map; use
        /// <see cref="map_apply{TKey,TValue,TResult}"/> to project a different value.
        /// </para>
        /// </summary>
        /// <returns>The matching entries as a one-dimensional <see cref="Tuple{TKey,TValue}"/> array, in map order.</returns>
        public Tuple<TKey, TValue>[] map_filter<TKey, TValue>(Expression<Func<TKey, TValue, bool>> predicate, IDictionary<TKey, TValue>? map) => default!;

        /// <summary>
        /// The lambda results of the map entries, one per entry in map order, surfaced as exactly
        /// <c>TResult[]</c>. Lowered to
        /// <c>arrayMap((k, v) -&gt; expression, mapKeys(map), mapValues(map))</c> and wrapped in
        /// <c>cast(... as Array(TResult))</c>, so the lambda may return any scalar value the declared
        /// result admits, including a conditional with a <see langword="null"/> branch. A reference-type
        /// <c>TResult</c> is cast as <c>Array(Nullable(TResult))</c> (for example
        /// <c>Array(Nullable(String))</c>), because the bare ClickHouse leaf type does not admit null;
        /// a value-type <c>TResult</c> stays a bare leaf unless it is itself <c>Nullable&lt;T&gt;</c>.
        /// </summary>
        /// <returns>One element per map entry, in map order.</returns>
        public TResult[] map_apply<TKey, TValue, TResult>(Expression<Func<TKey, TValue, TResult>> function, IDictionary<TKey, TValue>? map) => default!;

        /// <summary><c>mapAll((k, v) -&gt; predicate, map)</c>: true when the lambda returns true for every entry. Kept native (lambda first, then the map) and surfaced as <c>bool</c>, not lowered.</summary>
        public bool map_all<TKey, TValue>(Expression<Func<TKey, TValue, bool>> predicate, IDictionary<TKey, TValue>? map) => default!;

        /// <summary><c>mapExists((k, v) -&gt; predicate, map)</c>: true when the lambda returns true for at least one entry. Kept native (lambda first, then the map) and surfaced as <c>bool</c>, not lowered.</summary>
        public bool map_exists<TKey, TValue>(Expression<Func<TKey, TValue, bool>> predicate, IDictionary<TKey, TValue>? map) => default!;

        /// <summary>
        /// The map entries sorted by key, surfaced as exactly <see cref="Tuple{TKey,TValue}"/>[].
        /// Lowered to <c>arraySort(_t -&gt; _t.1, arrayZip(mapKeys(map), mapValues(map)))</c> and wrapped
        /// in <c>cast(... as Array(Tuple(TKey, TValue)))</c>: the native <c>mapSort</c> returns a map,
        /// which cannot be projected as the tuple array the declaration promises.
        /// </summary>
        /// <returns>The entries ordered by key as a one-dimensional <see cref="Tuple{TKey,TValue}"/> array.</returns>
        public Tuple<TKey, TValue>[] map_sort<TKey, TValue>(IDictionary<TKey, TValue>? map) => default!;

        /// <summary>
        /// <c>groupBitmap(value)</c>: the bitmap of the distinct unsigned integer values of a group,
        /// surfaced as the opaque <c>UInt64</c> bitmap state. Combine with the bitmap functions to count
        /// or merge. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? group_bitmap<T>(T? value) => default!;

        /// <summary><c>groupBitmapAnd(bitmap)</c>: the intersection of the group's bitmap states.</summary>
        public ulong? group_bitmap_and<T>(T? value) => default!;

        /// <summary><c>groupBitmapOr(bitmap)</c>: the union of the group's bitmap states.</summary>
        public ulong? group_bitmap_or<T>(T? value) => default!;

        /// <summary><c>groupBitmapXor(bitmap)</c>: the symmetric difference of the group's bitmap states.</summary>
        public ulong? group_bitmap_xor<T>(T? value) => default!;

        /// <summary>
        /// <c>sumMap(key, value)</c>: aggregates the values per key, surfaced as a ClickHouse <c>Map</c>
        /// (<see cref="IDictionary{TKey,TValue}"/>). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public IDictionary<TKey, TValue>? sum_map<TKey, TValue>(TKey? key, TValue? value) => default!;

        /// <summary>
        /// <c>sumMapFiltered(keys)(key, value)</c>: as <see cref="sum_map{TKey,TValue}(TKey, TValue)"/>
        /// but only for the <paramref name="keysToKeep"/> keys. The keys must be an inline array.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public IDictionary<TKey, TValue>? sum_map_filtered<TKey, TValue>(TKey[] keysToKeep, TKey? key, TValue? value) => default!;

        // ----------------------------------------------------------------------------------------
        // Tuple family (T286b F06). Every delivered result is a System.Tuple: the ClickHouse driver
        // materialises Tuple(Int32, String) as Tuple<int, string> (and nested tuples recursively),
        // while ValueTuple result types are not supported by the raw-row mapper. The rendered call is
        // wrapped in cast(... as the declared tuple type), exactly like the array and map facades, so
        // the server returns the element types the CLR declaration promises (tuplePlus promotes Int32
        // to Int64, for example).
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// <c>tupleConcat(first, second)</c>: the elements of both tuples, concatenated. The two- and
        /// three-component forms cover the documented arities that a typed CLR tuple can name; the
        /// result is cast to the declared <see cref="Tuple{T1,T2,T3,T4}"/> so the element types match.
        /// Requires a provider that supports the ClickHouse tuple surface.
        /// </summary>
        public Tuple<T1, T2, T3, T4> tuple_concat<T1, T2, T3, T4>(
            Tuple<T1, T2> first, Tuple<T3, T4> second) => default!;

        /// <inheritdoc cref="tuple_concat{T1,T2,T3,T4}(Tuple{T1,T2},Tuple{T3,T4})"/>
        public Tuple<T1, T2, T3, T4, T5, T6> tuple_concat<T1, T2, T3, T4, T5, T6>(
            Tuple<T1, T2, T3> first, Tuple<T4, T5, T6> second) => default!;

        /// <summary>
        /// <c>tupleNames(tuple)</c>: the element names of a named tuple as a <c>String</c> array. For
        /// an unnamed tuple the server substitutes the one-based indices as strings (verified on
        /// 25.8: <c>tupleNames(tuple(1, 2))</c> is <c>['1', '2']</c>). The result is
        /// <c>Array(String)</c> and never depends on the element types.
        /// </summary>
        public string[] tuple_names<T1, T2>(Tuple<T1, T2> tuple) => default!;

        /// <inheritdoc cref="tuple_names{T1,T2}(Tuple{T1,T2})"/>
        public string[] tuple_names<T1, T2, T3>(Tuple<T1, T2, T3> tuple) => default!;

        /// <summary>
        /// <c>untuple(tuple)</c> is a documented deterministic refusal. The function performs a
        /// syntactic substitution of the tuple elements <b>in the call location</b>: one projected
        /// expression becomes a dynamic set of columns whose count and types follow the tuple and
        /// whose names the vendor declares implementation-specific. NextORM maps one projection
        /// expression to exactly one result column, so no CLR return type can name or declare that
        /// shape; the declared <see cref="object"/> is never materialized and the translation raises
        /// <see cref="NotSupportedException"/> with this reason before any SQL is emitted. Project the
        /// components explicitly (<c>tuple.Item1</c>/<c>tuple.Item2</c>) instead.
        /// </summary>
        public object? untuple<T1, T2>(Tuple<T1, T2> tuple) => default!;

        /// <summary>
        /// <c>flattenTuple(tuple)</c>: the named nested tuple flattened depth-first, one level deep.
        /// The server accepts named tuples only; a named tuple column is flattened directly, while an
        /// inline tuple constructor is first cast to a synthetic named type built from the declared
        /// components (<c>Tuple(f1 T1, f2 Tuple(f3 T2, f4 T3))</c>). A cast of a nested tuple
        /// <b>column</b> to another named type corrupts the values on 25.8 (verified), so that path is
        /// deliberately not used. The result is cast to the declared flat
        /// <see cref="Tuple{T1,T2,T3}"/>. Requires a provider that supports the ClickHouse tuple
        /// surface.
        /// </summary>
        public Tuple<T1, T2, T3> flatten_tuple<T1, T2, T3>(Tuple<T1, Tuple<T2, T3>> tuple) => default!;

        /// <inheritdoc cref="flatten_tuple{T1,T2,T3}(Tuple{T1,Tuple{T2,T3}})"/>
        public Tuple<T1, T2, T3, T4> flatten_tuple<T1, T2, T3, T4>(
            Tuple<T1, Tuple<T2, Tuple<T3, T4>>> tuple) => default!;

        /// <summary>
        /// <c>tupleToNameValuePairs(tuple)</c>: the tuple as <c>(name, value)</c> pairs. The server
        /// requires every tuple element to have <b>the same type</b> (a heterogeneous tuple raises
        /// Code 43 on 25.8), so the declared <typeparamref name="T1"/> and <typeparamref name="T2"/>
        /// must be identical after type inference; a heterogeneous tuple is refused at translation
        /// with that precise reason instead of an untyped <c>object[]</c> shape. The pair name is the
        /// element name for a named tuple and the one-based index string for an unnamed tuple.
        /// </summary>
        public Tuple<string, T1>[] tuple_to_name_value_pairs<T1, T2>(Tuple<T1, T2> tuple) => default!;

        /// <inheritdoc cref="tuple_to_name_value_pairs{T1,T2}(Tuple{T1,T2})"/>
        public Tuple<string, T1>[] tuple_to_name_value_pairs<T1, T2, T3>(Tuple<T1, T2, T3> tuple) => default!;

        /// <summary>
        /// <c>tuplePlus(first, second)</c> (the <c>tuple+</c> operator): the element-wise sum of the
        /// two tuples, cast to the declared component types. ClickHouse widens integral components
        /// (Int32 + Int32 is computed as Int64), so the cast is what keeps the declared
        /// <see cref="Tuple{T1,T2}"/> exact; a value outside the declared component type wraps on
        /// the server cast exactly as the ClickHouse <c>Int32</c> cast does. Numeric components only:
        /// a string or decimal component is refused at translation.
        /// </summary>
        public Tuple<T1, T2> tuple_plus<T1, T2>(Tuple<T1, T2> first, Tuple<T1, T2> second) => default!;

        /// <summary><c>tupleMinus</c> (the <c>tuple-</c> operator): the element-wise difference, with the same component-type and cast rules as <see cref="tuple_plus{T1,T2}"/>.</summary>
        public Tuple<T1, T2> tuple_minus<T1, T2>(Tuple<T1, T2> first, Tuple<T1, T2> second) => default!;

        /// <summary><c>tupleMultiply</c> (the <c>tuple*</c> operator): the element-wise product, with the same component-type and cast rules as <see cref="tuple_plus{T1,T2}"/>.</summary>
        public Tuple<T1, T2> tuple_multiply<T1, T2>(Tuple<T1, T2> first, Tuple<T1, T2> second) => default!;

        /// <summary>
        /// <c>tupleDivide</c> (the <c>tuple/</c> operator): the element-wise quotient, always
        /// <c>Float64</c> regardless of the component types (verified on 25.8: Int32 / Int32 is
        /// Float64), cast to the declared <see cref="Tuple{T1,T2}"/> of doubles. Division by zero
        /// yields the IEEE <c>inf</c>/<c>nan</c>, never an exception. Numeric components only.
        /// </summary>
        public Tuple<double, double> tuple_divide<T1, T2>(Tuple<T1, T2> first, Tuple<T1, T2> second) => default!;

        /// <summary>
        /// <c>tupleHammingDistance(first, second)</c>: the number of positions at which the two tuples
        /// differ, as <c>long</c> (the native result is UInt8/16/32/64 by arity and is lowered through
        /// <c>toInt64</c>). Elements are compared for equality, so any component type the server can
        /// compare is accepted (strings included); both tuples must have the same arity, which the
        /// signature enforces.
        /// </summary>
        public long tuple_hamming_distance<T1, T2>(Tuple<T1, T2> first, Tuple<T1, T2> second) => default!;

        /// <inheritdoc cref="tuple_hamming_distance{T1,T2}(Tuple{T1,T2},Tuple{T1,T2})"/>
        public long tuple_hamming_distance<T1, T2, T3>(
            Tuple<T1, T2, T3> first, Tuple<T1, T2, T3> second) => default!;

        /// <summary>
        /// <c>dotProduct(first, second)</c>: the sum of the element-wise products, as <c>long</c> for
        /// integral components (the server computes Int64/UInt64 and the call is lowered with a cast
        /// to Int64). Decimal components would lose precision in the declared <c>long</c> and are
        /// refused at translation. A <see cref="Tuple{T1,T2}"/> of <c>double</c> or <c>float</c>
        /// selects the overload below, which keeps a floating-point result.
        /// </summary>
        public long dot_product<T1, T2>(Tuple<T1, T2> first, Tuple<T1, T2> second) => default!;

        /// <summary>
        /// <c>dotProduct(first, second)</c> for <c>Double</c> components: the result is
        /// <c>Float64</c> and is cast to <c>double</c>, so no integer truncation happens.
        /// </summary>
        public double dot_product(Tuple<double, double> first, Tuple<double, double> second) => default!;

        /// <summary>
        /// <c>dotProduct(first, second)</c> for <c>Single</c> components: the server promotes the
        /// <c>Float32</c> product to <c>Float64</c>, which the declared <c>double</c> keeps exactly.
        /// </summary>
        public double dot_product(Tuple<float, float> first, Tuple<float, float> second) => default!;

        // ----------------------------------------------------------------------------------------
        // Map family (T286b F07). A ClickHouse Map cannot be projected directly as a
        // dictionary (the row mapper rejects IDictionary result columns, exactly the T257 constraint
        // that moved map_filter/map_sort to Tuple<TKey,TValue>[]), so every result is surfaced as the
        // projectable array of key/value tuples produced by
        // arrayZip(mapKeys(<map>), mapValues(<map>)) and cast to the declared Array(Tuple(K, V)).
        // Inputs stay typed: a map operand is an IDictionary (an inline map(...) constructor, a
        // captured dictionary or a map column), and mapFromArrays takes two typed arrays. Duplicate
        // keys are preserved in the tuple array (a ClickHouse Map and its key/value arrays keep them),
        // so duplicate-key semantics are observable rather than silently collapsed.
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// <c>mapSubtract(left, right)</c>: the keys of both maps, with the values of
        /// <paramref name="right"/> subtracted from the matching values of
        /// <paramref name="left"/>; keys present in only one map keep their value (a missing key
        /// behaves as zero). The server returns a <c>Map</c>, which is surfaced as the projectable
        /// <c>Tuple&lt;TKey, TValue&gt;[]</c> of <c>arrayZip(mapKeys(...), mapValues(...))</c> cast to
        /// the declared element types; the native Int64 promotion is normalised by that cast. A
        /// Nullable value component is refused because the server cannot promote a Nullable Map value
        /// against a non-nullable operand. Requires a provider that supports the map surface
        /// (ClickHouse).
        /// </summary>
        public Tuple<TKey, TValue>[] map_subtract<TKey, TValue>(
            IDictionary<TKey, TValue>? left, IDictionary<TKey, TValue>? right) => default!;

        /// <summary>
        /// <c>mapUpdate(left, right)</c>: <paramref name="left"/> with the values of the keys present
        /// in <paramref name="right"/> replaced; keys only in <paramref name="right"/> are added. Both
        /// maps must have the same key and value types (the server rejects a mismatch with Code 43),
        /// which the identical type parameters enforce. Surfaced as the same projectable key/value
        /// tuple array as <see cref="map_subtract{TKey,TValue}"/>; duplicate keys stay in the array.
        /// Requires a provider that supports the map surface (ClickHouse).
        /// </summary>
        public Tuple<TKey, TValue>[] map_update<TKey, TValue>(
            IDictionary<TKey, TValue>? left, IDictionary<TKey, TValue>? right) => default!;

        /// <summary>
        /// <c>mapReverseSort(map)</c>: the entries sorted by key in descending order, surfaced as the
        /// projectable <see cref="Tuple{TKey,TValue}"/> array (duplicates keep their server order).
        /// Requires a provider that supports the map surface.
        /// </summary>
        public Tuple<TKey, TValue>[] map_reverse_sort<TKey, TValue>(IDictionary<TKey, TValue>? map) => default!;

        /// <summary>
        /// <c>mapPartialSort(limit, map)</c>: the map partially sorted by key in ascending order,
        /// with <paramref name="limit"/> bounding the sorted prefix. The argument order follows the
        /// vendor syntax (<c>limit</c> first); the pinned 25.8 server returns the key-sorted map for
        /// every accepted limit including <c>0</c> (verified live). Surfaced as the projectable
        /// <see cref="Tuple{TKey,TValue}"/> array. Requires a provider that supports the map surface.
        /// </summary>
        public Tuple<TKey, TValue>[] map_partial_sort<TKey, TValue>(
            long limit, IDictionary<TKey, TValue>? map) => default!;

        /// <summary>
        /// <c>mapFromArrays(keys, values)</c>: a <c>Map(K, V)</c> built from two equal-length arrays,
        /// surfaced as the projectable <see cref="Tuple{TKey,TValue}"/> array. A captured or inline
        /// array is bound as one array parameter, an array column stays a column; a length mismatch
        /// and a NULL key element are server errors (Code 190/36). Duplicate keys are preserved in the
        /// tuple array. Requires a provider that supports the map surface.
        /// </summary>
        public Tuple<TKey, TValue>[] map_from_arrays<TKey, TValue>(TKey[]? keys, TValue[]? values) => default!;

        /// <summary>
        /// <c>mapPopulateSeries(map)</c>: fills the missing integer keys of the map with the value
        /// type default, from the smallest to the largest existing key, surfaced as the projectable
        /// <see cref="Tuple{TKey,TValue}"/> array in key order. The key type must be an integer type
        /// (the server rejects Float/String keys with Code 43), which the translation enforces. When
        /// keys repeat, the first value in order of appearance is kept. Requires a provider that
        /// supports the map surface.
        /// </summary>
        public Tuple<TKey, TValue>[] map_populate_series<TKey, TValue>(IDictionary<TKey, TValue>? map) => default!;

        /// <summary>
        /// <c>mapPopulateSeries(map, max)</c>: as <see cref="map_populate_series{TKey,TValue}(IDictionary{TKey,TValue})"/>,
        /// extended to <paramref name="max"/>; a <paramref name="max"/> below the largest key truncates
        /// the series (verified live: <c>max = 3</c> over keys 1 and 5 yields 1..3). Requires a
        /// provider that supports the map surface.
        /// </summary>
        public Tuple<TKey, TValue>[] map_populate_series<TKey, TValue>(
            IDictionary<TKey, TValue>? map, long max) => default!;

        /// <summary>
        /// <c>extractKeyValuePairs(data)</c>: the key/value pairs parsed from free-form text with the
        /// vendor defaults (key/value delimiter <c>:</c>, pair delimiters space, <c>,</c> and
        /// <c>;</c>), surfaced as the projectable <see cref="Tuple{TKey,TValue}"/> array of
        /// <c>(String, String)</c> pairs. Duplicate keys are preserved (the server Map keeps them);
        /// a NULL input is refused at translation because a <c>Map</c> cannot be inside
        /// <c>Nullable</c>. Requires a provider that supports the map surface.
        /// </summary>
        public Tuple<string, string>[] extract_key_value_pairs(string? data) => default!;

        /// <summary>
        /// <c>extractKeyValuePairs(data, keyValueDelimiter, pairDelimiter)</c>: the explicit-delimiter
        /// form. Both delimiters are compile-time or bound strings; the server requires the exact
        /// characters it is given (the defaults are <c>:</c> and the set space/<c>,</c>/<c>;</c>).
        /// A NULL input is refused at translation.
        /// </summary>
        public Tuple<string, string>[] extract_key_value_pairs(
            string? data, string keyValueDelimiter, string pairDelimiter) => default!;

        /// <summary>
        /// <c>MD5(value)</c>: the 16-byte MD5 digest rendered as 32 lowercase hexadecimal characters
        /// (<c>lower(hex(MD5(value)))</c>); a <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? md5(string? value) => default!;

        /// <summary>
        /// <c>SHA1(value)</c>: the 20-byte SHA-1 digest rendered as 40 lowercase hexadecimal characters.
        /// A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public string? sha1(string? value) => default!;

        /// <summary>
        /// <c>SHA256(value)</c>: the 32-byte SHA-256 digest rendered as 64 lowercase hexadecimal
        /// characters. A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public string? sha256(string? value) => default!;

        /// <summary>
        /// <c>SHA512(value)</c>: the 64-byte SHA-512 digest rendered as 128 lowercase hexadecimal
        /// characters. A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public string? sha512(string? value) => default!;

        /// <summary><c>xxHash32(value)</c>: the 32-bit xxHash as a signed integer. Requires a provider that supports it (ClickHouse).</summary>
        public int xx_hash32(string? value) => default!;

        /// <summary><c>xxHash64(value)</c>: the 64-bit xxHash as a signed integer.</summary>
        public long xx_hash64(string? value) => default!;

        /// <summary><c>xxh3(value)</c>: the 64-bit XXH3 hash as a signed integer.</summary>
        public long xxh3(string? value) => default!;

        /// <summary><c>cityHash64(value)</c>: the 64-bit CityHash as a signed integer.</summary>
        public long city_hash64(string? value) => default!;

        /// <summary><c>sipHash64(value)</c>: the 64-bit SipHash as a signed integer.</summary>
        public long sip_hash64(string? value) => default!;

        /// <summary>
        /// <c>sipHash128(value)</c>: the 128-bit SipHash rendered as 32 lowercase hexadecimal characters
        /// (<c>lower(hex(sipHash128(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? sip_hash128(string? value) => default!;

        /// <summary><c>murmurHash2_32(value)</c>: the 32-bit MurmurHash2 as a signed integer.</summary>
        public int murmur_hash2_32(string? value) => default!;

        /// <summary><c>murmurHash2_64(value)</c>: the 64-bit MurmurHash2 as a signed integer.</summary>
        public long murmur_hash2_64(string? value) => default!;

        /// <summary><c>murmurHash3_32(value)</c>: the 32-bit MurmurHash3 as a signed integer.</summary>
        public int murmur_hash3_32(string? value) => default!;

        /// <summary><c>murmurHash3_64(value)</c>: the 64-bit MurmurHash3 as a signed integer.</summary>
        public long murmur_hash3_64(string? value) => default!;

        /// <summary>
        /// <c>murmurHash3_128(value)</c>: the 128-bit MurmurHash3 rendered as 32 lowercase hexadecimal
        /// characters (<c>lower(hex(murmurHash3_128(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? murmur_hash3_128(string? value) => default!;

        // ----------------------------------------------------------------------------------------
        // Hash family (T286b F10). The digest members keep the existing md5/sha family contract:
        // the server returns FixedString, so the arm renders lower(hex(...)) and the CLR shape is a
        // lowercase hexadecimal string (leading zeros preserved). The non-cryptographic hashes keep
        // the server's own width and signedness (UInt64 -> ulong, UInt32 -> uint, Int32 -> int)
        // instead of the toInt64 lowering used by the older shipped members, and every member that
        // can see a NULL argument is nullable so the server NULL is preserved, never zeroed.
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// <c>SHA224(value)</c>: the 28-byte SHA-224 digest rendered as 56 lowercase hexadecimal
        /// characters (<c>lower(hex(SHA224(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? sha224(string? value) => default!;

        /// <summary>
        /// <c>SHA384(value)</c>: the 48-byte SHA-384 digest rendered as 96 lowercase hexadecimal
        /// characters (<c>lower(hex(SHA384(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? sha384(string? value) => default!;

        /// <summary>
        /// <c>SHA512_256(value)</c>: the 32-byte SHA-512/256 digest rendered as 64 lowercase
        /// hexadecimal characters (<c>lower(hex(SHA512_256(value)))</c>). A <see langword="null"/>
        /// input yields <see langword="null"/>.
        /// </summary>
        public string? sha512_256(string? value) => default!;

        /// <summary>
        /// <c>BLAKE3(value)</c>: the 32-byte BLAKE3 digest rendered as 64 lowercase hexadecimal
        /// characters (<c>lower(hex(BLAKE3(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? blake3(string? value) => default!;

        /// <summary>
        /// <c>MD4(value)</c>: the 16-byte MD4 digest rendered as 32 lowercase hexadecimal characters
        /// (<c>lower(hex(MD4(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? md4(string? value) => default!;

        /// <summary>
        /// <c>RIPEMD160(value)</c>: the 20-byte RIPEMD-160 digest rendered as 40 lowercase hexadecimal
        /// characters (<c>lower(hex(RIPEMD160(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? ripemd160(string? value) => default!;

        /// <summary>
        /// <c>keccak256(value)</c>: the 32-byte Keccak-256 digest rendered as 64 lowercase hexadecimal
        /// characters (<c>lower(hex(keccak256(value)))</c>). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public string? keccak256(string? value) => default!;

        /// <summary>
        /// <c>farmHash64(value)</c>: the 64-bit FarmHash as the server's unsigned <c>UInt64</c>
        /// (<see cref="ulong"/>), so no value is reinterpreted as negative. A <see langword="null"/>
        /// input yields <see langword="null"/>.
        /// </summary>
        public ulong? farm_hash64(string? value) => default!;

        /// <summary>
        /// <c>metroHash64(value)</c>: the 64-bit MetroHash as the server's unsigned <c>UInt64</c>.
        /// A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? metro_hash64(string? value) => default!;

        /// <summary>
        /// <c>javaHash(value)</c>: the Java <c>String.hashCode</c> value as the server's signed
        /// <c>Int32</c> (it can be negative). A <see langword="null"/> input yields
        /// <see langword="null"/>.
        /// </summary>
        public int? java_hash(string? value) => default!;

        /// <summary>
        /// <c>hiveHash(value)</c>: JavaHash with the sign bit cleared, as the server's signed
        /// <c>Int32</c> (the vendor type; the value is non-negative). A <see langword="null"/> input
        /// yields <see langword="null"/>.
        /// </summary>
        public int? hive_hash(string? value) => default!;

        /// <summary>
        /// <c>halfMD5(value)</c>: the first 8 bytes of the MD5 digest as the server's unsigned
        /// <c>UInt64</c> (big-endian). A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? half_md5(string? value) => default!;

        /// <summary>
        /// <c>URLHash(value)</c>: the normalized URL hash as the server's unsigned <c>UInt64</c>. One
        /// trailing <c>/</c>, <c>?</c> or <c>#</c> is ignored, as the vendor defines. A
        /// <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? url_hash(string? value) => default!;

        /// <summary>
        /// <c>URLHash(value, level)</c>: the hash of the URL truncated to the given hierarchy level
        /// (0 is the whole URL), rendered as <c>toUInt8(level)</c> because the vendor level parameter
        /// is a <c>UInt8</c>. A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? url_hash(string? value, byte level) => default!;

        /// <summary>
        /// <c>wyHash64(value)</c>: the 64-bit wyHash as the server's unsigned <c>UInt64</c>. A
        /// <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? wy_hash64(string? value) => default!;

        /// <summary>
        /// <c>intHash32(value)</c>: the 32-bit integer hash as the server's unsigned <c>UInt32</c>
        /// (<see cref="uint"/>), never reinterpreted as negative. A <see langword="null"/> input
        /// yields <see langword="null"/>.
        /// </summary>
        public uint? int_hash32(long? value) => default!;

        /// <summary>
        /// <c>intHash64(value)</c>: the 64-bit integer hash as the server's unsigned <c>UInt64</c>.
        /// A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? int_hash64(long? value) => default!;

        /// <summary>
        /// <c>ngramMinHash(value[, ngramsize])</c>: the minimum and maximum of the n-gram hashes,
        /// surfaced as the declared <see cref="Tuple{T1,T2}"/> of two unsigned 64-bit hashes
        /// (<c>Tuple(UInt64, UInt64)</c>). <paramref name="ngramsize"/> is a <c>UInt8</c> from 1 to 25
        /// and defaults to the vendor's 3 when omitted. The vendor's third parameter <c>hashnum</c> is
        /// fixed at its default 6: it is the arity of the arg-returning sibling only and does not
        /// change this two-hash result. A nullable argument column is rejected by the server because a
        /// <c>Tuple</c> cannot be inside <c>Nullable</c>; pass a non-nullable column or
        /// <c>assumeNotNull(...)</c>.
        /// </summary>
        public Tuple<ulong, ulong> ngram_min_hash(string? value, byte? ngramsize = null) => default!;

        /// <summary>
        /// <c>ngramMinHashArg(value[, ngramsize])</c>: the n-grams with the minimum and maximum hashes,
        /// surfaced as the vendor's <c>Tuple(Tuple(String, ... six), Tuple(String, ... six))</c> — the
        /// declared <see cref="Tuple{T1,T2}"/> of two six-component string tuples. The arity is the
        /// vendor default <c>hashnum = 6</c>; <c>hashnum</c> is deliberately not a parameter because the
        /// result's tuple arity would follow it and no CLR type can name a variable-arity tuple. Short
        /// inputs are padded with empty strings, exactly as the server returns them. A nullable
        /// argument column is rejected by the server (a <c>Tuple</c> cannot be inside <c>Nullable</c>).
        /// </summary>
        public Tuple<
            Tuple<string, string, string, string, string, string>,
            Tuple<string, string, string, string, string, string>> ngram_min_hash_arg(
            string? value, byte? ngramsize = null) => default!;

        /// <summary>
        /// <c>ngramSimHash(value[, ngramsize])</c>: the n-gram SimHash as the server's unsigned
        /// <c>UInt64</c>; close inputs produce close hashes (compare with <c>bitHammingDistance</c>).
        /// <paramref name="ngramsize"/> is a <c>UInt8</c> from 1 to 25 (vendor default 3). A
        /// <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? ngram_sim_hash(string? value, byte? ngramsize = null) => default!;

        /// <summary>
        /// <c>wordShingleMinHash(value[, shinglesize])</c>: the minimum and maximum of the word-shingle
        /// hashes as the declared <see cref="Tuple{T1,T2}"/> of two unsigned 64-bit hashes.
        /// <paramref name="shinglesize"/> is a <c>UInt8</c> from 1 to 25 (vendor default 3); the vendor
        /// <c>hashnum</c> is fixed at its default 6 for the same arity reason as
        /// <see cref="ngram_min_hash_arg"/>. A nullable argument column is rejected by the server
        /// (a <c>Tuple</c> cannot be inside <c>Nullable</c>).
        /// </summary>
        public Tuple<ulong, ulong> word_shingle_min_hash(string? value, byte? shinglesize = null) => default!;

        /// <summary>
        /// <c>wordShingleMinHashArg(value[, shinglesize])</c>: the word shingles with the minimum and
        /// maximum hashes as two six-component string tuples (the vendor <c>hashnum = 6</c> arity).
        /// Short inputs are padded with empty strings. A nullable argument column is rejected by the
        /// server (a <c>Tuple</c> cannot be inside <c>Nullable</c>).
        /// </summary>
        public Tuple<
            Tuple<string, string, string, string, string, string>,
            Tuple<string, string, string, string, string, string>> word_shingle_min_hash_arg(
            string? value, byte? shinglesize = null) => default!;

        /// <summary>
        /// <c>wordShingleSimHash(value[, shinglesize])</c>: the word-shingle SimHash as the server's
        /// unsigned <c>UInt64</c>. <paramref name="shinglesize"/> is a <c>UInt8</c> from 1 to 25
        /// (vendor default 3). A <see langword="null"/> input yields <see langword="null"/>.
        /// </summary>
        public ulong? word_shingle_sim_hash(string? value, byte? shinglesize = null) => default!;

        /// <summary>
        /// <c>ngramSimHashArg(value[, ngramsize])</c>: refused. ClickHouse has no arg-returning SimHash
        /// sibling — <c>ngramSimHash</c> is a single <c>UInt64</c> aggregate hash and the only
        /// arg-returning n-gram function is <c>ngramMinHashArg</c>. The name is absent from
        /// <c>system.functions</c> on the pinned 25.8.33.6 server (and from the vendor hash-function
        /// reference) and fails with <c>UNKNOWN_FUNCTION</c>, so no version gap or rendering can close
        /// it. This is a non-existent-member refusal, not a CLR-shape limitation.
        /// </summary>
        public Tuple<
            Tuple<string, string, string, string, string, string>,
            Tuple<string, string, string, string, string, string>> ngram_sim_hash_arg(
            string? value, byte? ngramsize = null) =>
            throw new NotSupportedException(
                "The ngram_sim_hash_arg function is refused: ClickHouse has no ngramSimHashArg " +
                "function — ngramSimHash is a single UInt64 aggregate hash and ngramMinHashArg is the " +
                "only arg-returning n-gram function. The name is absent from system.functions on " +
                "25.8.33.6 and from the vendor hash-function reference, and fails with " +
                "UNKNOWN_FUNCTION.");

        /// <summary>
        /// <c>wordShingleSimHashArg(value[, shinglesize])</c>: refused for the same documented reason
        /// as <see cref="ngram_sim_hash_arg"/> — the vendor defines no arg-returning word-shingle
        /// SimHash sibling (<c>wordShingleMinHashArg</c> is the only Arg form), so the expansion member
        /// names a function that exists in no ClickHouse version and fails with
        /// <c>UNKNOWN_FUNCTION</c>.
        /// </summary>
        public Tuple<
            Tuple<string, string, string, string, string, string>,
            Tuple<string, string, string, string, string, string>> word_shingle_sim_hash_arg(
            string? value, byte? shinglesize = null) =>
            throw new NotSupportedException(
                "The word_shingle_sim_hash_arg function is refused: ClickHouse has no " +
                "wordShingleSimHashArg function — wordShingleSimHash is a single UInt64 aggregate " +
                "hash and wordShingleMinHashArg is the only arg-returning word-shingle function. The " +
                "name is absent from system.functions on 25.8.33.6 and from the vendor " +
                "hash-function reference, and fails with UNKNOWN_FUNCTION.");

        /// <summary>
        /// <c>generateULID()</c>: a new ULID (a lexicographically sortable 26-character identifier).
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? generate_ulid() => default!;

        /// <summary>
        /// <c>log2(x)</c>: the base-2 logarithm of <paramref name="x"/> (<c>Float64</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider reject
        /// the call.
        /// </summary>
        public double log2(double x) => throw new NotSupportedException("The log2 function is not supported by this provider.");

        /// <summary>
        /// <c>log2(x)</c> over a nullable argument: the base-2 logarithm, or <see langword="null"/> when
        /// <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? log2(double? x) => throw new NotSupportedException("The log2 function is not supported by this provider.");

        /// <summary>
        /// <c>cbrt(x)</c>: the cube root of <paramref name="x"/> (<c>Float64</c>). Requires a provider that
        /// supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double cbrt(double x) => throw new NotSupportedException("The cbrt function is not supported by this provider.");

        /// <summary>
        /// <c>cbrt(x)</c> over a nullable argument: the cube root, or <see langword="null"/> when
        /// <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? cbrt(double? x) => throw new NotSupportedException("The cbrt function is not supported by this provider.");

        /// <summary>
        /// <c>exp2(x)</c>: two raised to the power <paramref name="x"/> (<c>Float64</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider reject
        /// the call.
        /// </summary>
        public double exp2(double x) => throw new NotSupportedException("The exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>exp2(x)</c> over a nullable argument: two raised to the power <paramref name="x"/>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(Float64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public double? exp2(double? x) => throw new NotSupportedException("The exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>exp10(x)</c>: ten raised to the power <paramref name="x"/> (<c>Float64</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider reject
        /// the call.
        /// </summary>
        public double exp10(double x) => throw new NotSupportedException("The exp10 function is not supported by this provider.");

        /// <summary>
        /// <c>exp10(x)</c> over a nullable argument: ten raised to the power <paramref name="x"/>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(Float64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public double? exp10(double? x) => throw new NotSupportedException("The exp10 function is not supported by this provider.");

        /// <summary>
        /// <c>erf(x)</c>: the Gauss error function of <paramref name="x"/> (<c>Float64</c>). It is odd
        /// (<c>erf(-x) = -erf(x)</c>), tends to <c>±1</c> at the infinities and is <c>nan</c> for
        /// <c>nan</c>. Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public double erf(double x) => throw new NotSupportedException("The erf function is not supported by this provider.");

        /// <summary>
        /// <c>erf(x)</c> over a nullable argument: the Gauss error function, or <see langword="null"/>
        /// when <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider reject
        /// the call.
        /// </summary>
        public double? erf(double? x) => throw new NotSupportedException("The erf function is not supported by this provider.");

        /// <summary>
        /// <c>erfc(x)</c>: the complementary error function <c>1 - erf(x)</c> of <paramref name="x"/>
        /// (<c>Float64</c>), which keeps its precision in the far tail where <c>1 - erf(x)</c> would lose
        /// it. <c>erfc(inf) = 0</c>, <c>erfc(-inf) = 2</c> and <c>erfc(nan) = nan</c>. Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double erfc(double x) => throw new NotSupportedException("The erfc function is not supported by this provider.");

        /// <summary>
        /// <c>erfc(x)</c> over a nullable argument: the complementary error function, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(Float64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public double? erfc(double? x) => throw new NotSupportedException("The erfc function is not supported by this provider.");

        /// <summary>
        /// <c>lgamma(x)</c>: the natural logarithm of the absolute value of the gamma function of
        /// <paramref name="x"/> (<c>Float64</c>). <c>lgamma(1) = 0</c>; at a pole (<c>0</c> or another
        /// non-positive integer) the server returns <c>inf</c> and <c>nan</c> respectively, never an
        /// exception. Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public double lgamma(double x) => throw new NotSupportedException("The lgamma function is not supported by this provider.");

        /// <summary>
        /// <c>lgamma(x)</c> over a nullable argument: the log-gamma value, or <see langword="null"/> when
        /// <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? lgamma(double? x) => throw new NotSupportedException("The lgamma function is not supported by this provider.");

        /// <summary>
        /// <c>tgamma(x)</c>: the gamma function of <paramref name="x"/> (<c>Float64</c>).
        /// <c>tgamma(0)</c> is <c>inf</c> and a negative integer argument is <c>nan</c> (a pole), not an
        /// exception. Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public double tgamma(double x) => throw new NotSupportedException("The tgamma function is not supported by this provider.");

        /// <summary>
        /// <c>tgamma(x)</c> over a nullable argument: the gamma value, or <see langword="null"/> when
        /// <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? tgamma(double? x) => throw new NotSupportedException("The tgamma function is not supported by this provider.");

        /// <summary>
        /// <c>gcd(a, b)</c>: the greatest common divisor of two integral values. The result is
        /// non-negative regardless of the operand signs (<c>gcd(-12, 18) = 6</c>), the operands are widened
        /// to <c>Int64</c> (<c>gcd(Int32, Int32)</c> already returns <c>Int64</c>) and <c>Int64</c>
        /// overflow for <c>Int64.MinValue</c> is a server error. A zero operand is *not* identity: the
        /// server raises <c>ILLEGAL_DIVISION</c> (division by zero) for <c>gcd(0, b)</c> and
        /// <c>gcd(a, 0)</c>, and that error is surfaced rather than replaced with a default.
        /// <para>
        /// There is deliberately no nullable overload: the server adapts the function to
        /// <c>Nullable(Int64)</c> by evaluating it over the underlying column, where a NULL is its zero
        /// default, so a nullable column that contains a NULL hits the same division by zero instead of
        /// producing NULL. A nullable operand must be filtered or coalesced by the caller; no silent
        /// <c>coalesce</c> is emitted.
        /// </para>
        /// Requires a provider that supports it (ClickHouse); every other provider and the in-memory
        /// provider reject the call.
        /// </summary>
        public long gcd(long a, long b) => throw new NotSupportedException("The gcd function is not supported by this provider.");

        /// <summary>
        /// <c>lcm(a, b)</c>: the least common multiple of two integral values, always non-negative
        /// (<c>lcm(-4, 6) = 12</c>). Like <see cref="gcd(long, long)"/>, a zero operand is a server-side
        /// <c>ILLEGAL_DIVISION</c> error rather than <c>0</c>, and no nullable overload is offered for the
        /// same reason (a NULL in a nullable column reaches the zero-operand error). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public long lcm(long a, long b) => throw new NotSupportedException("The lcm function is not supported by this provider.");

        /// <summary>
        /// <c>isFinite(x)</c>: <see langword="true"/> when <paramref name="x"/> is neither infinite nor
        /// <c>nan</c>. The server returns <c>UInt8</c>, surfaced as <see cref="bool"/>. A
        /// <see langword="null"/> argument yields <see langword="null"/>, never <see langword="false"/>.
        /// Requires a provider that supports it (ClickHouse); every other provider and the in-memory
        /// provider reject the call.
        /// </summary>
        public bool is_finite(double x) => throw new NotSupportedException("The is_finite function is not supported by this provider.");

        /// <summary>
        /// <c>isFinite(x)</c> over a nullable argument: <see langword="true"/>/<see langword="false"/>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(UInt8)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public bool? is_finite(double? x) => throw new NotSupportedException("The is_finite function is not supported by this provider.");

        /// <summary>
        /// <c>isInfinite(x)</c>: <see langword="true"/> for <c>±inf</c> only (<c>nan</c> is not infinite).
        /// The server returns <c>UInt8</c>, surfaced as <see cref="bool"/>; a <see langword="null"/>
        /// argument yields <see langword="null"/>. Requires a provider that supports it (ClickHouse);
        /// every other provider and the in-memory provider reject the call.
        /// </summary>
        public bool is_infinite(double x) => throw new NotSupportedException("The is_infinite function is not supported by this provider.");

        /// <summary>
        /// <c>isInfinite(x)</c> over a nullable argument: <see langword="true"/>/<see langword="false"/>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(UInt8)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public bool? is_infinite(double? x) => throw new NotSupportedException("The is_infinite function is not supported by this provider.");

        /// <summary>
        /// <c>isNaN(x)</c>: <see langword="true"/> only for <c>nan</c> (<c>inf</c> is not <c>nan</c>). The
        /// server returns <c>UInt8</c>, surfaced as <see cref="bool"/>; a <see langword="null"/> argument
        /// yields <see langword="null"/>, never <see langword="false"/>. Requires a provider that supports
        /// it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public bool is_nan(double x) => throw new NotSupportedException("The is_nan function is not supported by this provider.");

        /// <summary>
        /// <c>isNaN(x)</c> over a nullable argument: <see langword="true"/>/<see langword="false"/>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(UInt8)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public bool? is_nan(double? x) => throw new NotSupportedException("The is_nan function is not supported by this provider.");

        /// <summary>
        /// <c>hypot(a, b)</c>: the length of the hypotenuse <c>sqrt(a² + b²)</c> computed without
        /// intermediate overflow (<c>Float64</c>). An infinite operand yields <c>inf</c>; a <c>nan</c>
        /// operand with a finite one yields <c>nan</c>. Requires a provider that supports it (ClickHouse);
        /// every other provider and the in-memory provider reject the call.
        /// </summary>
        public double hypot(double a, double b) => throw new NotSupportedException("The hypot function is not supported by this provider.");

        /// <summary>
        /// <c>hypot(a, b)</c> over nullable operands: the hypotenuse length, or <see langword="null"/>
        /// when either operand is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? hypot(double? a, double? b) => throw new NotSupportedException("The hypot function is not supported by this provider.");

        /// <summary>
        /// <c>intDiv(a, b)</c>: integer division of <paramref name="a"/> by <paramref name="b"/> truncated
        /// toward zero (<c>intDiv(-7, 2) = -3</c>). The operands are widened to <c>Int64</c>, so the result
        /// is an <c>Int64</c>. A zero divisor is a server-side <c>ILLEGAL_DIVISION</c> error, not
        /// <c>0</c> and not the <c>inf</c> of the floating-point <c>divide</c>. Distinct from the CLR
        /// <c>/</c> operator, which is the catalog's <c>divide</c> form and is floating-point division on
        /// ClickHouse. Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public long int_div(long a, long b) => throw new NotSupportedException("The int_div function is not supported by this provider.");

        /// <summary>
        /// <c>intDiv(a, b)</c> over nullable operands: the truncated integer quotient, or
        /// <see langword="null"/> when either operand is <see langword="null"/>
        /// (<c>Nullable(Int64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public long? int_div(long? a, long? b) => throw new NotSupportedException("The int_div function is not supported by this provider.");

        /// <summary>
        /// <c>intExp2(x)</c>: <c>2^x</c> as the unsigned 64-bit integer <c>UInt64</c>. A negative exponent
        /// yields <c>0</c>, and an exponent above 63 saturates at <c>UInt64.MaxValue</c> (<c>intExp2(64)</c>
        /// is <c>18446744073709551615</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public ulong int_exp2(long x) => throw new NotSupportedException("The int_exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>intExp2(x)</c> over a nullable argument: <c>2^x</c> as <c>UInt64</c>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(UInt64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public ulong? int_exp2(long? x) => throw new NotSupportedException("The int_exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>intExp10(x)</c>: <c>10^x</c> as the unsigned 64-bit integer <c>UInt64</c>. A negative
        /// exponent yields <c>0</c>, and an exponent above 19 saturates at <c>UInt64.MaxValue</c>
        /// (<c>intExp10(20)</c> is <c>18446744073709551615</c>). Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public ulong int_exp10(long x) => throw new NotSupportedException("The int_exp10 function is not supported by this provider.");

        /// <summary>
        /// <c>intExp10(x)</c> over a nullable argument: <c>10^x</c> as <c>UInt64</c>, or
        /// <see langword="null"/> when <paramref name="x"/> is <see langword="null"/>
        /// (<c>Nullable(UInt64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public ulong? int_exp10(long? x) => throw new NotSupportedException("The int_exp10 function is not supported by this provider.");

        /// <summary>
        /// <c>sigmoid(x)</c>: the logistic function <c>1 / (1 + exp(-x))</c> (<c>Float64</c>). The server
        /// pins <c>sigmoid(0) = 0.5</c>, <c>sigmoid(inf) = 1</c>, <c>sigmoid(-inf) = 0</c> and — unlike
        /// the CLR formula — <c>sigmoid(nan) = 0</c>. Requires a provider that supports it (ClickHouse);
        /// every other provider and the in-memory provider reject the call.
        /// </summary>
        public double sigmoid(double x) => throw new NotSupportedException("The sigmoid function is not supported by this provider.");

        /// <summary>
        /// <c>sigmoid(x)</c> over a nullable argument: the logistic value, or <see langword="null"/> when
        /// <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? sigmoid(double? x) => throw new NotSupportedException("The sigmoid function is not supported by this provider.");

        /// <summary>
        /// <c>sqr(x)</c>: the square of <paramref name="x"/> (<c>Float64</c>). ClickHouse introduced the
        /// <c>sqr</c> spelling in 26.7 and defines it as "the product of <c>x</c> multiplied by itself"
        /// with the multiplication operator's return type, so on the pinned 25.8 server — which rejects the
        /// unknown name — the dialect renders exactly that product (<c>(x * x)</c>) instead of an
        /// unavailable function. Requires a provider that supports it (ClickHouse); every other provider
        /// and the in-memory provider reject the call.
        /// </summary>
        public double sqr(double x) => throw new NotSupportedException("The sqr function is not supported by this provider.");

        /// <summary>
        /// <c>sqr(x)</c> over a nullable argument: the square, or <see langword="null"/> when
        /// <paramref name="x"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double? sqr(double? x) => throw new NotSupportedException("The sqr function is not supported by this provider.");

        /// <summary>
        /// <c>roundAge(age)</c>: buckets a human age onto the vendor table — <c>0</c> for an age below
        /// one (negative ages included), <c>17</c> for 1–17, <c>18</c> for 18–24, <c>25</c> for 25–34,
        /// <c>35</c> for 35–44, <c>45</c> for 45–54 and <c>55</c> for 55 and above. The server returns
        /// <c>UInt8</c>; the arm lowers it with <c>toInt64</c>, so the CLR result is a
        /// <see cref="long"/>. The buckets are the server's own table (verified on 25.8.33.6), not the
        /// catalog's rounded description. Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public long round_age(long age) => throw new NotSupportedException("The round_age function is not supported by this provider.");

        /// <summary>
        /// <c>roundAge(age)</c> over a nullable argument: the age bucket, or <see langword="null"/> when
        /// <paramref name="age"/> is <see langword="null"/> (<c>Nullable(UInt8)</c> lowered to
        /// <c>Nullable(Int64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public long? round_age(long? age) => throw new NotSupportedException("The round_age function is not supported by this provider.");

        /// <summary>
        /// <c>roundBankers(x)</c>: banker's rounding (half to even) of a floating-point value to the
        /// nearest integer, so a tie goes to the nearest even digit — <c>roundBankers(2.5) = 2</c>,
        /// <c>roundBankers(3.5) = 4</c> — and a negative tie keeps the negative sign
        /// (<c>roundBankers(-0.5)</c> is negative zero). <c>nan</c> and <c>±inf</c> propagate unchanged.
        /// This member is ClickHouse-only rather than a portable <see cref="Math.Round(double)"/>
        /// representation: the portable call renders the server's <c>round</c>, which agrees for
        /// <c>Float64</c> but rounds <em>away from zero</em> for <c>Decimal</c> inputs (verified:
        /// <c>round(toDecimal64('2.5', 1)) = 3</c> while <c>roundBankers(...) = 2</c>), and the portable
        /// API has no integral or negative-scale form. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double round_bankers(double value) => throw new NotSupportedException("The round_bankers function is not supported by this provider.");

        /// <summary>
        /// <c>roundBankers(x, N)</c>: banker's rounding of a floating-point value to <paramref name="scale"/>
        /// decimal places — <paramref name="scale"/> &gt; 0 rounds to the right of the decimal point,
        /// &lt; 0 to the left and 0 to the integer (<c>roundBankers(1234.5, -2) = 1200</c>). The server
        /// rounds the decimal representation, so <c>roundBankers(2.675, 2) = 2.68</c>; the tie rule still
        /// decides (<c>roundBankers(2.65, 1) = 2.6</c>, <c>roundBankers(2.55, 1) = 2.6</c>). A
        /// <see langword="null"/> value yields <see langword="null"/>. Requires a provider that supports
        /// it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double round_bankers(double value, int scale) => throw new NotSupportedException("The round_bankers function is not supported by this provider.");

        /// <summary>
        /// <c>roundBankers(x)</c> over a nullable argument: the half-to-even integer, or
        /// <see langword="null"/> when <paramref name="value"/> is <see langword="null"/>
        /// (<c>Nullable(Float64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public double? round_bankers(double? value) => throw new NotSupportedException("The round_bankers function is not supported by this provider.");

        /// <summary>
        /// <c>roundBankers(x, N)</c> over a nullable argument: the half-to-even value at
        /// <paramref name="scale"/> decimal places, or <see langword="null"/> when
        /// <paramref name="value"/> is <see langword="null"/> (<c>Nullable(Float64)</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public double? round_bankers(double? value, int scale) => throw new NotSupportedException("The round_bankers function is not supported by this provider.");

        /// <summary>
        /// <c>roundBankers(x, N)</c> over an integral value: the same half-to-even rule on the integer
        /// domain, where a negative <paramref name="scale"/> rounds to the left of the decimal point
        /// (<c>roundBankers(45, -1) = 40</c>, <c>roundBankers(1350, -2) = 1400</c>). The server returns
        /// the argument type (<c>Int64</c> here). Requires a provider that supports it (ClickHouse);
        /// every other provider and the in-memory provider reject the call.
        /// </summary>
        public long round_bankers(long value, int scale) => throw new NotSupportedException("The round_bankers function is not supported by this provider.");

        /// <summary>
        /// <c>roundBankers(x, N)</c> over a nullable integral value: the half-to-even integral value at
        /// <paramref name="scale"/> decimal places, or <see langword="null"/> when
        /// <paramref name="value"/> is <see langword="null"/> (<c>Nullable(Int64)</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public long? round_bankers(long? value, int scale) => throw new NotSupportedException("The round_bankers function is not supported by this provider.");

        /// <summary>
        /// <c>roundDown(x, boundaries)</c>: the greatest boundary that is not above
        /// <paramref name="value"/>, clamped up to the smallest boundary when the value lies below every
        /// entry (<c>roundDown(-3.5, [1, 2, 5, 10]) = 1</c>) and down to the largest one when it lies
        /// above them. The boundaries need not be ordered. They are bound as one
        /// <c>Array(Float64)</c> parameter, which the server accepts because parameters are substituted
        /// as constants — it rejects a non-constant array column and an empty array outright rather than
        /// substituting a default. Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public double round_down(double value, params double[] boundaries) => throw new NotSupportedException("The round_down function is not supported by this provider.");

        /// <summary>
        /// <c>roundDown(x, boundaries)</c> over a nullable value: the rounded value, or
        /// <see langword="null"/> when <paramref name="value"/> is <see langword="null"/>
        /// (<c>Nullable(Float64)</c>). The boundaries are still mandatory and constant. Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public double? round_down(double? value, params double[] boundaries) => throw new NotSupportedException("The round_down function is not supported by this provider.");

        /// <summary>
        /// <c>roundDuration(duration)</c>: floors a duration in seconds onto the vendor table
        /// <c>1, 10, 30, 60, 120, 180, 240, 300, 600, 1200, 1800, 3600, 7200, 18000, 36000</c>. A value
        /// below one — zero and every negative duration — yields <c>0</c>, and the top entry saturates:
        /// <c>roundDuration(36001) = 36000</c>. The server returns <c>UInt16</c>; the arm lowers it with
        /// <c>toInt64</c>, so the CLR result is a <see cref="long"/>. Requires a provider that supports
        /// it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public long round_duration(long duration) => throw new NotSupportedException("The round_duration function is not supported by this provider.");

        /// <summary>
        /// <c>roundDuration(duration)</c> over a nullable argument: the duration bucket, or
        /// <see langword="null"/> when <paramref name="duration"/> is <see langword="null"/>
        /// (<c>Nullable(UInt16)</c> lowered to <c>Nullable(Int64)</c>). Requires a provider that
        /// supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public long? round_duration(long? duration) => throw new NotSupportedException("The round_duration function is not supported by this provider.");

        /// <summary>
        /// <c>roundToExp2(x)</c> over an integral value: the largest power of two that is not above
        /// <paramref name="value"/> (<c>roundToExp2(100) = 64</c>). A value below one — including every
        /// negative integer — yields <c>0</c>, and the result saturates at the largest power of two of
        /// the argument width (<c>roundToExp2(Int64.MaxValue) = 2^62</c>). The server returns the
        /// argument type, so an <see cref="long"/> argument returns <c>Int64</c>. Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the
        /// call.
        /// </summary>
        public long round_to_exp2(long value) => throw new NotSupportedException("The round_to_exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>roundToExp2(x)</c> over a nullable integral value: the power-of-two floor, or
        /// <see langword="null"/> when <paramref name="value"/> is <see langword="null"/>
        /// (<c>Nullable(Int64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public long? round_to_exp2(long? value) => throw new NotSupportedException("The round_to_exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>roundToExp2(x)</c> over a <see cref="double"/>: the mantissa is truncated, so the value is
        /// floored onto a — possibly fractional — power of two and the sign is preserved
        /// (<c>roundToExp2(0.7) = 0.5</c>, <c>roundToExp2(-3.7) = -2</c>). Unlike the integral form,
        /// values between zero and one are <em>not</em> collapsed to zero. <c>±inf</c> stay infinite; a
        /// <c>nan</c> argument yields <c>+inf</c> on the pinned 25.8 server, which is asserted as the
        /// server's behaviour rather than corrected client-side. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public double round_to_exp2(double value) => throw new NotSupportedException("The round_to_exp2 function is not supported by this provider.");

        /// <summary>
        /// <c>roundToExp2(x)</c> over a nullable <see cref="double"/>: the power-of-two floor, or
        /// <see langword="null"/> when <paramref name="value"/> is <see langword="null"/>
        /// (<c>Nullable(Float64)</c>). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public double? round_to_exp2(double? value) => throw new NotSupportedException("The round_to_exp2 function is not supported by this provider.");

        // ---------------------------------------------------------------------------------------
        // Date/time family (F04). The Date/DateTime results are surfaced as System.DateTime; a
        // value whose zone is part of the ClickHouse column type (DateTime('Zone')) loses the zone
        // identifier on materialization, which is why the members whose *result* attaches a zone are
        // refused with a deterministic reason instead of silently dropping it.
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// <c>makeDate(year, month, day)</c>: builds a <c>Date</c> from its components. The server
        /// normalizes an out-of-range day into the next month (<c>makeDate(2024, 2, 31)</c> is
        /// <c>2024-03-02</c>) and yields <c>1970-01-01</c> when the date falls outside the
        /// <c>Date</c> range (<c>1970-01-01</c>..<c>2149-06-06</c>, so a year below 1970 or above
        /// 2149 is clamped to the epoch). That substitution is the server's, not the client's, and is
        /// asserted in the live tests. Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime make_date(int year, int month, int day) => throw new NotSupportedException("The make_date function is not supported by this provider.");

        /// <summary>
        /// <c>makeDate(year, day_of_year)</c>: the <c>Date</c> for a year and a 1-based day of that
        /// year (<c>makeDate(2024, 60)</c> is <c>2024-02-29</c>). A day outside the year's length
        /// yields <c>1970-01-01</c> on the server (it is not clamped to the last day). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public DateTime make_date(int year, int dayOfYear) => throw new NotSupportedException("The make_date function is not supported by this provider.");

        /// <summary>
        /// The nullable form of <see cref="make_date(int, int, int)"/>: a NULL component makes the
        /// whole result NULL (<c>Nullable(Date)</c>), never the epoch.
        /// </summary>
        public DateTime? make_date(int? year, int? month, int? day) => throw new NotSupportedException("The make_date function is not supported by this provider.");

        /// <summary>
        /// The nullable form of <see cref="make_date(int, int)"/>: a NULL component makes the result
        /// NULL (<c>Nullable(Date)</c>), never the epoch.
        /// </summary>
        public DateTime? make_date(int? year, int? dayOfYear) => throw new NotSupportedException("The make_date function is not supported by this provider.");

        /// <summary>
        /// <c>makeDate32(year, month, day)</c>: builds a <c>Date32</c> from its components. The
        /// <c>Date32</c> range is <c>1900-01-01</c>..<c>2299-12-31</c>; a date outside it yields
        /// <c>1970-01-01</c> on the server. Requires a provider that supports it (ClickHouse); every
        /// other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime make_date32(int year, int month, int day) => throw new NotSupportedException("The make_date32 function is not supported by this provider.");

        /// <summary>
        /// <c>makeDate32(year, day_of_year)</c>: the <c>Date32</c> for a year and a 1-based day of
        /// that year. Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public DateTime make_date32(int year, int dayOfYear) => throw new NotSupportedException("The make_date32 function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="make_date32(int, int, int)"/>: NULL stays NULL (<c>Nullable(Date32)</c>).</summary>
        public DateTime? make_date32(int? year, int? month, int? day) => throw new NotSupportedException("The make_date32 function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="make_date32(int, int)"/>: NULL stays NULL (<c>Nullable(Date32)</c>).</summary>
        public DateTime? make_date32(int? year, int? dayOfYear) => throw new NotSupportedException("The make_date32 function is not supported by this provider.");

        /// <summary>
        /// <c>makeDateTime(year, month, day, hour, minute, second)</c>: builds a <c>DateTime</c> in the
        /// server time zone. The <c>DateTime</c> range is <c>1970-01-01 00:00:00</c>..
        /// <c>2106-02-07 06:28:15</c>; an out-of-range second is normalized
        /// (<c>makeDateTime(2024, 1, 1, 0, 0, 60)</c> is <c>2024-01-01 00:01:00</c>) and an
        /// out-of-range date yields the epoch on the server. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime make_date_time(int year, int month, int day, int hour, int minute, int second) => throw new NotSupportedException("The make_date_time function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="make_date_time(int, int, int, int, int, int)"/>: a NULL component yields NULL (<c>Nullable(DateTime)</c>).</summary>
        public DateTime? make_date_time(int? year, int? month, int? day, int? hour, int? minute, int? second) => throw new NotSupportedException("The make_date_time function is not supported by this provider.");

        /// <summary>
        /// <c>now64()</c>: the current date and time at millisecond precision
        /// (<c>DateTime64(3)</c>, the vendor default scale). The function is non-deterministic and is
        /// evaluated once per query analysis; the live tests assert freshness and precision only.
        /// Requires a provider that supports it (ClickHouse); every other provider and the in-memory
        /// provider reject the call.
        /// </summary>
        public DateTime now64() => throw new NotSupportedException("The now64 function is not supported by this provider.");

        /// <summary>
        /// <c>now64(scale)</c>: the current date and time at <c>DateTime64(scale)</c>. The scale must
        /// be a compile-time constant and is limited to <c>0..7</c>: scale 8 and 9 carry
        /// sub-100-nanosecond digits that <see cref="DateTime"/> cannot represent (its tick is 100 ns),
        /// so those scales are refused with a deterministic reason rather than truncated. Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public DateTime now64(int scale) => throw new NotSupportedException("The now64 function is not supported by this provider.");

        /// <summary>
        /// <c>nowInBlock()</c>: the current date and time at the moment each block is processed
        /// (<c>DateTime</c>). Unlike <see cref="now64()"/> it is not a constant expression: inside one
        /// block it is fixed, across blocks of a long query it advances. Requires a provider that
        /// supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime now_in_block() => throw new NotSupportedException("The now_in_block function is not supported by this provider.");

        /// <summary>
        /// <c>timeSlot(value)</c>: rounds a <c>DateTime</c> down to the start of its half-hour
        /// interval (<c>03:44:59</c> becomes <c>03:30:00</c>). Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime time_slot(DateTime value) => throw new NotSupportedException("The time_slot function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="time_slot(DateTime)"/>: NULL stays NULL (<c>Nullable(DateTime)</c>).</summary>
        public DateTime? time_slot(DateTime? value) => throw new NotSupportedException("The time_slot function is not supported by this provider.");

        /// <summary>
        /// <c>timeSlots(startTime, duration)</c>: the 30-minute slot boundaries covering
        /// <paramref name="durationSeconds"/> from <paramref name="startTime"/> (the first slot is the
        /// start's own half-hour). The start is cast to <c>DateTime</c> (a <c>DateTime64</c> column is
        /// truncated to whole seconds — the slot boundaries carry no sub-second part) and the duration
        /// is sent as <c>UInt32</c>: the server requires exactly that pair (a <c>UInt32</c> duration is
        /// rejected for a <c>DateTime64</c> start, which needs <c>Decimal64</c>). There is deliberately
        /// no nullable overload: a NULL start makes the server's
        /// result <c>Nullable(Array(DateTime))</c>, which ClickHouse rejects ("Nested type
        /// Array(DateTime) cannot be inside Nullable type"), so a NULL must be filtered by the caller
        /// instead of silently becoming an empty array. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime[] time_slots(DateTime startTime, uint durationSeconds) => throw new NotSupportedException("The time_slots function is not supported by this provider.");

        /// <summary>
        /// <c>toStartOfFifteenMinutes(value)</c>: truncates a <c>DateTime</c> to the start of its
        /// fifteen-minute interval. The argument must be a date with time (a <c>Date</c> is rejected
        /// by the server). Requires a provider that supports it (ClickHouse); every other provider and
        /// the in-memory provider reject the call.
        /// </summary>
        public DateTime to_start_of_fifteen_minutes(DateTime value) => throw new NotSupportedException("The to_start_of_fifteen_minutes function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_start_of_fifteen_minutes(DateTime)"/>: NULL stays NULL.</summary>
        public DateTime? to_start_of_fifteen_minutes(DateTime? value) => throw new NotSupportedException("The to_start_of_fifteen_minutes function is not supported by this provider.");

        /// <summary>
        /// <c>toStartOfFiveMinutes(value)</c>: truncates a <c>DateTime</c> to the start of its
        /// five-minute interval. Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime to_start_of_five_minutes(DateTime value) => throw new NotSupportedException("The to_start_of_five_minutes function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_start_of_five_minutes(DateTime)"/>: NULL stays NULL.</summary>
        public DateTime? to_start_of_five_minutes(DateTime? value) => throw new NotSupportedException("The to_start_of_five_minutes function is not supported by this provider.");

        /// <summary>
        /// <c>toStartOfTenMinutes(value)</c>: truncates a <c>DateTime</c> to the start of its
        /// ten-minute interval. Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime to_start_of_ten_minutes(DateTime value) => throw new NotSupportedException("The to_start_of_ten_minutes function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_start_of_ten_minutes(DateTime)"/>: NULL stays NULL.</summary>
        public DateTime? to_start_of_ten_minutes(DateTime? value) => throw new NotSupportedException("The to_start_of_ten_minutes function is not supported by this provider.");

        /// <summary>
        /// <c>toStartOfInterval(value, INTERVAL amount unit)</c>: the general truncation over a
        /// calendar grid anchored at the vendor's fixed points (year 0, 1900 for quarters, the epoch
        /// for day and below; weeks start on Monday). <paramref name="unit"/> must be a compile-time
        /// string literal naming a whole-second-or-larger unit — <c>second</c>, <c>minute</c>,
        /// <c>hour</c>, <c>day</c>, <c>week</c>, <c>month</c>, <c>quarter</c> or <c>year</c>; the
        /// sub-second units are refused because a <see cref="DateTime"/> argument binds a
        /// <c>DateTime</c> and the server rejects them for that type. The amount must be positive
        /// (zero or negative is refused by the server and surfaced, never substituted).
        /// Requires a provider that supports it (ClickHouse); every other provider and the in-memory
        /// provider reject the call.
        /// </summary>
        public DateTime to_start_of_interval(DateTime value, long amount, string unit) => throw new NotSupportedException("The to_start_of_interval function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_start_of_interval(DateTime, long, string)"/>: NULL stays NULL (<c>Nullable(DateTime)</c>).</summary>
        public DateTime? to_start_of_interval(DateTime? value, long amount, string unit) => throw new NotSupportedException("The to_start_of_interval function is not supported by this provider.");

        /// <summary>
        /// <c>toStartOfInterval(value, INTERVAL amount unit, origin)</c>: the TimescaleDB
        /// <c>time_bucket()</c> form, where the grid is anchored at <paramref name="origin"/> instead
        /// of the vendor's fixed point. The value and the origin are both cast to <c>DateTime</c> (the
        /// server requires the same type for them). The origin must not be after the value (the
        /// server raises <c>BAD_ARGUMENTS</c> otherwise). Requires a provider that supports it (ClickHouse); every
        /// other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime to_start_of_interval(DateTime value, long amount, string unit, DateTime origin) => throw new NotSupportedException("The to_start_of_interval function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_start_of_interval(DateTime, long, string, DateTime)"/>: NULL stays NULL.</summary>
        public DateTime? to_start_of_interval(DateTime? value, long amount, string unit, DateTime origin) => throw new NotSupportedException("The to_start_of_interval function is not supported by this provider.");

        /// <summary>
        /// <c>toISOYear(value)</c>: the ISO-8601 week-based year, which differs from the calendar year
        /// around New Year (<c>toISOYear(2024-01-01)</c> is 2024, <c>toISOYear(2021-01-01)</c> is
        /// 2020). The server returns <c>UInt16</c>, surfaced as <see cref="int"/>. Requires a provider
        /// that supports it (ClickHouse); every other provider and the in-memory provider reject the
        /// call.
        /// </summary>
        public int to_iso_year(DateTime value) => throw new NotSupportedException("The to_iso_year function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_iso_year(DateTime)"/>: NULL stays NULL (<c>Nullable(UInt16)</c>).</summary>
        public int? to_iso_year(DateTime? value) => throw new NotSupportedException("The to_iso_year function is not supported by this provider.");

        /// <summary>
        /// <c>toWeek(value)</c>: the week number in mode 0 (weeks start on Sunday, range 0..53, the
        /// first week is the one containing a Sunday of this year). The server returns <c>UInt8</c>,
        /// surfaced as <see cref="int"/>. Requires a provider that supports it (ClickHouse); every
        /// other provider and the in-memory provider reject the call.
        /// </summary>
        public int to_week(DateTime value) => throw new NotSupportedException("The to_week function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_week(DateTime)"/>: NULL stays NULL (<c>Nullable(UInt8)</c>).</summary>
        public int? to_week(DateTime? value) => throw new NotSupportedException("The to_week function is not supported by this provider.");

        /// <summary>
        /// <c>toWeek(value, mode)</c>: the week number under one of the vendor's ten modes
        /// (<c>0..9</c>, the MySQL <c>WEEK()</c> table: mode 0/1 range 0..53, 2..9 range 1..53; even
        /// modes start on Sunday, odd modes on Monday; mode 3 is the ISO-8601 week). A mode outside
        /// <c>0..9</c> is refused by the server and surfaced. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public int to_week(DateTime value, int mode) => throw new NotSupportedException("The to_week function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_week(DateTime, int)"/>: NULL stays NULL.</summary>
        public int? to_week(DateTime? value, int? mode) => throw new NotSupportedException("The to_week function is not supported by this provider.");

        /// <summary>
        /// <c>toYearWeek(value)</c>: the combined year and week (<c>year * 100 + week</c>) in mode 0.
        /// Unlike <see cref="to_week(DateTime)"/> the year component follows the week into the
        /// neighbouring year, so <c>toYearWeek(2023-12-31)</c> is <c>202353</c> while
        /// <c>toWeek(2023-12-31)</c> is 53. The server returns <c>UInt32</c>, lowered to
        /// <see cref="int"/>. Requires a provider that supports it (ClickHouse); every other provider
        /// and the in-memory provider reject the call.
        /// </summary>
        public int to_year_week(DateTime value) => throw new NotSupportedException("The to_year_week function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_year_week(DateTime)"/>: NULL stays NULL (<c>Nullable(UInt32)</c>).</summary>
        public int? to_year_week(DateTime? value) => throw new NotSupportedException("The to_year_week function is not supported by this provider.");

        /// <summary>
        /// <c>toYearWeek(value, mode)</c>: the combined year and week under the <c>toWeek</c> modes
        /// <c>0..9</c>. Requires a provider that supports it (ClickHouse); every other provider and
        /// the in-memory provider reject the call.
        /// </summary>
        public int to_year_week(DateTime value, int mode) => throw new NotSupportedException("The to_year_week function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_year_week(DateTime, int)"/>: NULL stays NULL.</summary>
        public int? to_year_week(DateTime? value, int? mode) => throw new NotSupportedException("The to_year_week function is not supported by this provider.");

        /// <summary>
        /// <c>toLastDayOfWeek(value)</c>: rounds a date up to the nearest Saturday (mode 0, the
        /// default). The result is a <c>Date</c>. Requires a provider that supports it (ClickHouse);
        /// every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime to_last_day_of_week(DateTime value) => throw new NotSupportedException("The to_last_day_of_week function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_last_day_of_week(DateTime)"/>: NULL stays NULL (<c>Nullable(Date)</c>).</summary>
        public DateTime? to_last_day_of_week(DateTime? value) => throw new NotSupportedException("The to_last_day_of_week function is not supported by this provider.");

        /// <summary>
        /// <c>toLastDayOfWeek(value, mode)</c>: rounds up to the last day of the week selected by the
        /// <c>toWeek</c> mode (<c>0..9</c>): even modes end on Saturday, odd modes on Sunday. Requires
        /// a provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public DateTime to_last_day_of_week(DateTime value, int mode) => throw new NotSupportedException("The to_last_day_of_week function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_last_day_of_week(DateTime, int)"/>: NULL stays NULL.</summary>
        public DateTime? to_last_day_of_week(DateTime? value, int? mode) => throw new NotSupportedException("The to_last_day_of_week function is not supported by this provider.");

        /// <summary>
        /// <c>toDaysInMonth(value)</c>: the number of days in the month (28..31, leap years
        /// included). The name was introduced in ClickHouse 26.3, so the arm renders the vendor
        /// definition <c>toDayOfMonth(toLastDayOfMonth(value))</c> — the same value with the same
        /// <c>UInt8</c> type, lowered to <see cref="int"/>. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public int to_days_in_month(DateTime value) => throw new NotSupportedException("The to_days_in_month function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_days_in_month(DateTime)"/>: NULL stays NULL (<c>Nullable(UInt8)</c>).</summary>
        public int? to_days_in_month(DateTime? value) => throw new NotSupportedException("The to_days_in_month function is not supported by this provider.");

        /// <summary>
        /// <c>fromUTCTimestamp(value, timeZone)</c> — <b>refused</b>. The vendor result carries no
        /// zone in its type on the pinned server (<c>toTypeName</c> is <c>DateTime</c> and
        /// <c>timezoneOf</c> is <c>UTC</c>), yet its wall clock is the input reinterpreted in the
        /// requested zone, so its underlying instant is shifted by that zone's offset (03:04:05 UTC
        /// becomes 06:04:05 for <c>Europe/Moscow</c>). A <see cref="DateTime"/> can hold that wall
        /// clock but cannot carry the zone that gives it meaning, and reading it as a UTC instant
        /// silently yields a different instant; the (wall clock, zone) pair is therefore not
        /// representable and the member is refused with a deterministic, tested reason. Use
        /// <see cref="timezone_offset(DateTime)"/> or <see cref="timezone_of(DateTime)"/> to inspect
        /// zones.
        /// </summary>
        public DateTime from_utc_timestamp(DateTime value, string timeZone) => throw new NotSupportedException("The from_utc_timestamp function is refused: fromUTCTimestamp returns a plain DateTime without a zone in its type whose wall clock is the target-zone time and whose underlying instant is shifted by the zone offset, and System.DateTime cannot carry the target zone that gives the value meaning.");

        /// <summary>
        /// <c>toModifiedJulianDay(date)</c>: the Modified Julian Day number (<c>Int32</c>) of a
        /// proleptic Gregorian date given in text form <c>YYYY-MM-DD</c>. The accepted range is
        /// <c>0000-01-01</c>..<c>9999-12-31</c> — wider than <see cref="DateTime"/> in the lower
        /// direction, which is why the argument is a <see cref="string"/> and the result an
        /// <see cref="int"/>: nothing is narrowed to a CLR date. An unparsable or invalid date (a
        /// 30 February, a five-digit year) raises the server's parse error, surfaced rather than
        /// defaulted, and a NULL argument yields NULL (<c>Nullable(Int32)</c>) without parsing.
        /// Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public int? to_modified_julian_day(string? date) => throw new NotSupportedException("The to_modified_julian_day function is not supported by this provider.");

        /// <summary>
        /// <c>age(unit, start, end)</c>: the number of whole <paramref name="unit"/> intervals
        /// between two dates, at nanosecond precision. <paramref name="unit"/> is one of
        /// <c>year</c>, <c>quarter</c>, <c>month</c>, <c>week</c>, <c>day</c>, <c>hour</c>,
        /// <c>minute</c>, <c>second</c> (singular, plural and vendor aliases are accepted by the
        /// server). The unit must be a constant expression (a literal or a bound parameter): the
        /// server rejects a column in that position, and the error is surfaced. The result is signed: an <paramref name="end"/> before
        /// <paramref name="start"/> is negative, never clamped. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public long age(string unit, DateTime start, DateTime end) => throw new NotSupportedException("The age function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="age(string, DateTime, DateTime)"/>: a NULL argument yields NULL (<c>Nullable(Int64)</c>).</summary>
        public long? age(string? unit, DateTime? start, DateTime? end) => throw new NotSupportedException("The age function is not supported by this provider.");

        /// <summary>
        /// <c>dateName(unit, value)</c>: the textual component of a date. <paramref name="unit"/> is
        /// one of <c>year</c>, <c>quarter</c>, <c>month</c> (English month name), <c>week</c>,
        /// <c>dayofyear</c>, <c>day</c>, <c>weekday</c> (English weekday name), <c>hour</c>,
        /// <c>minute</c>, <c>second</c>; an unsupported unit is refused by the server. The unit must
        /// be a constant expression (a literal or a bound parameter); a column in that position is
        /// rejected by the server. Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public string date_name(string unit, DateTime value) => throw new NotSupportedException("The date_name function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="date_name(string, DateTime)"/>: NULL stays NULL (<c>Nullable(String)</c>).</summary>
        public string? date_name(string? unit, DateTime? value) => throw new NotSupportedException("The date_name function is not supported by this provider.");

        /// <summary>
        /// <c>serverTimezone()</c>: the server's <c>timezone</c> setting as a name (constant unless the
        /// query spans shards). Requires a provider that supports it (ClickHouse); every other
        /// provider and the in-memory provider reject the call.
        /// </summary>
        public string server_timezone() => throw new NotSupportedException("The server_timezone function is not supported by this provider.");

        /// <summary>
        /// <c>timezoneOf(value)</c>: the zone name of a date with time (<c>UTC</c> for a value that
        /// carries none). Requires a provider that supports it (ClickHouse); every other provider and
        /// the in-memory provider reject the call.
        /// </summary>
        public string timezone_of(DateTime value) => throw new NotSupportedException("The timezone_of function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="timezone_of(DateTime)"/>: a NULL argument yields NULL.</summary>
        public string? timezone_of(DateTime? value) => throw new NotSupportedException("The timezone_of function is not supported by this provider.");

        /// <summary>
        /// <c>timezoneOffset(value)</c>: the zone's offset from UTC in seconds at that instant,
        /// daylight-saving time included (<c>Europe/Berlin</c> is 3600 in January and 7200 in July).
        /// Requires a provider that supports it (ClickHouse); every other provider and the in-memory
        /// provider reject the call.
        /// </summary>
        public int timezone_offset(DateTime value) => throw new NotSupportedException("The timezone_offset function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="timezone_offset(DateTime)"/>: NULL stays NULL (<c>Nullable(Int32)</c>).</summary>
        public int? timezone_offset(DateTime? value) => throw new NotSupportedException("The timezone_offset function is not supported by this provider.");

        /// <summary>
        /// <c>toTime(value)</c>: the time component of a date with time, represented as a
        /// <see cref="DateTime"/> on the vendor's fixed date (currently <c>1970-01-02</c>; the vendor
        /// documents that the exact date is an implementation detail and that the value is meant for
        /// differences, not standalone display). The arm renders the stable vendor name
        /// <c>toTimeWithFixedDate</c>, which is the legacy <c>toTime</c> on 25.8 (the name
        /// <c>toTime</c> was reassigned in 26.7); the two are asserted equal on the pinned server.
        /// Sub-second digits are dropped by the server. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime to_time(DateTime value) => throw new NotSupportedException("The to_time function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="to_time(DateTime)"/>: NULL stays NULL (<c>Nullable(DateTime)</c>).</summary>
        public DateTime? to_time(DateTime? value) => throw new NotSupportedException("The to_time function is not supported by this provider.");

        /// <summary>
        /// <c>toTime64(value, scale)</c> — <b>refused</b>. The function returns the ClickHouse
        /// <c>Time64(scale)</c> type; the provider maps <see cref="TimeSpan"/> to the Int64 duration
        /// storage policy (<see cref="ISqlDialect.SupportsNativeDuration"/> is false for ClickHouse),
        /// so a typed <see cref="TimeSpan"/> projection of a <c>Time64</c> value cannot be
        /// materialized and no other CLR value type carries a time of day with the declared scale.
        /// The refusal is deterministic and tested; read the value through <c>ExecuteRaw</c> with a
        /// <see cref="TimeSpan"/> projection if a raw read is what you need.
        /// </summary>
        public TimeSpan to_time64(DateTime value, int scale) => throw new NotSupportedException("The to_time64 function is refused: toTime64 returns the ClickHouse Time64 type, which System.TimeSpan cannot materialize through the ClickHouse provider's Int64 duration mapping (SupportsNativeDuration is false).");

        /// <summary>
        /// <c>timestamp(value)</c>: converts a date/time string to <c>DateTime64(6)</c>, i.e. a
        /// <see cref="DateTime"/> with microsecond precision. An unparsable value raises the server's
        /// parse error, and a NULL argument stays NULL (<c>Nullable(DateTime64(6))</c>). Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public DateTime? timestamp(string? value) => throw new NotSupportedException("The timestamp function is not supported by this provider.");

        /// <summary>
        /// <c>timestamp(value, time)</c>: converts the date/time string and adds the second
        /// <paramref name="time"/> string to it (<c>timestamp('2023-12-31', '12:00:00.11')</c> is
        /// <c>2023-12-31 12:00:00.110000</c>); an hour beyond 23 rolls into the next day on the
        /// server, and a NULL argument yields NULL. On the pinned 25.8 server this two-argument form
        /// is evaluated for constant arguments only: with a table column as
        /// <paramref name="value"/> the server raises <c>ILLEGAL_COLUMN</c> ("Illegal column
        /// Const(String) of 2nd argument"), which is surfaced as-is rather than simulated with date
        /// arithmetic; use <see cref="timestamp(string)"/> for a column. Requires a provider that
        /// supports it (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime? timestamp(string? value, string? time) => throw new NotSupportedException("The timestamp function is not supported by this provider.");

        /// <summary>
        /// <c>fromUnixTimestamp(timestamp)</c>: the <c>DateTime</c> of a Unix epoch second. The
        /// parameter is <see cref="uint"/> because the vendor domain is <c>UInt32</c> (1970..2106),
        /// so a negative epoch cannot be expressed and silently clamped to the epoch. Requires a
        /// provider that supports it (ClickHouse); every other provider and the in-memory provider
        /// reject the call.
        /// </summary>
        public DateTime from_unix_timestamp(uint timestamp) => throw new NotSupportedException("The from_unix_timestamp function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="from_unix_timestamp(uint)"/>: NULL stays NULL (<c>Nullable(DateTime)</c>).</summary>
        public DateTime? from_unix_timestamp(uint? timestamp) => throw new NotSupportedException("The from_unix_timestamp function is not supported by this provider.");

        /// <summary>
        /// <c>fromUnixTimestamp(timestamp, format)</c>: the MySQL-compatibility formatting form,
        /// which returns a <see cref="string"/> using MySQL <c>DATE_FORMAT</c> specifiers (for
        /// example <c>%i</c> for minutes). The format is passed as an ordinary argument, never
        /// inlined. Requires a provider that supports it (ClickHouse); every other provider and the
        /// in-memory provider reject the call.
        /// </summary>
        public string from_unix_timestamp(uint timestamp, string format) => throw new NotSupportedException("The from_unix_timestamp function is not supported by this provider.");

        /// <summary>
        /// <c>fromUnixTimestamp(timestamp, format, timeZone)</c>: the formatting form evaluated in
        /// <paramref name="timeZone"/>. Requires a provider that supports it (ClickHouse); every
        /// other provider and the in-memory provider reject the call.
        /// </summary>
        public string from_unix_timestamp(uint timestamp, string format, string? timeZone = null) => throw new NotSupportedException("The from_unix_timestamp function is not supported by this provider.");

        /// <summary>The nullable form of the formatting <c>fromUnixTimestamp</c>: a NULL argument yields NULL (<c>Nullable(String)</c>).</summary>
        public string? from_unix_timestamp(uint? timestamp, string? format, string? timeZone) => throw new NotSupportedException("The from_unix_timestamp function is not supported by this provider.");

        /// <summary>
        /// <c>fromUnixTimestamp64Milli(value)</c>: the <c>DateTime64(3)</c> of a Unix epoch in
        /// milliseconds. The full Int64 domain (1900..2299) is accepted, negatives included, so
        /// pre-epoch instants are produced rather than clamped. Requires a provider that supports it
        /// (ClickHouse); every other provider and the in-memory provider reject the call.
        /// </summary>
        public DateTime from_unix_timestamp64_milli(long value) => throw new NotSupportedException("The from_unix_timestamp64_milli function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="from_unix_timestamp64_milli(long)"/>: NULL stays NULL (<c>Nullable(DateTime64(3))</c>).</summary>
        public DateTime? from_unix_timestamp64_milli(long? value) => throw new NotSupportedException("The from_unix_timestamp64_milli function is not supported by this provider.");

        /// <summary>
        /// <c>fromUnixTimestamp64Micro(value)</c>: the <c>DateTime64(6)</c> of a Unix epoch in
        /// microseconds. Requires a provider that supports it (ClickHouse); every other provider and
        /// the in-memory provider reject the call.
        /// </summary>
        public DateTime from_unix_timestamp64_micro(long value) => throw new NotSupportedException("The from_unix_timestamp64_micro function is not supported by this provider.");

        /// <summary>The nullable form of <see cref="from_unix_timestamp64_micro(long)"/>: NULL stays NULL (<c>Nullable(DateTime64(6))</c>).</summary>
        public DateTime? from_unix_timestamp64_micro(long? value) => throw new NotSupportedException("The from_unix_timestamp64_micro function is not supported by this provider.");

        /// <summary>
        /// <c>fromUnixTimestamp64Nano(value)</c> — <b>refused</b>. The function returns
        /// <c>DateTime64(9)</c>, whose nanosecond digits cannot be represented by
        /// <see cref="DateTime"/> (tick resolution is 100 ns), so materializing it would silently
        /// truncate the value. The refusal is deterministic and tested; use
        /// <see cref="from_unix_timestamp64_micro(long)"/> and keep the submicrosecond remainder, or
        /// read the column through <c>ExecuteRaw</c>.
        /// </summary>
        public DateTime from_unix_timestamp64_nano(long value) => throw new NotSupportedException("The from_unix_timestamp64_nano function is refused: fromUnixTimestamp64Nano returns DateTime64(9), whose nanosecond digits cannot be represented by System.DateTime (100 ns tick resolution), so the value would be silently truncated.");
        // -----------------------------------------------------------------------------------
        // Type-conversion family (T286b F15): the toInt*/toUInt*/toFloat*/toDecimal* widths with
        // their OrNull/OrDefault variants, toString/toBool/toFixedString/toLowCardinality, the
        // checked accurateCast* forms and the bit-pattern reinterpretAs* forms. 128/256-bit
        // integers, Decimal128/256 and the Interval results are deterministic refusals.
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// <c>toInt8(value)</c>: converts a numeric or textual value to the vendor <c>Int8</c>. Numeric narrowing wraps within the target width (two's complement), for example 128 becomes -128, and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int8<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt8OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int8_or_null(string? value) => default!;

        /// <summary>
        /// <c>toInt8OrDefault(value)</c>: the vendor form that returns the Int8 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted). The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int8_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt8OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toInt8(defaultValue)</c> so its vendor type matches the Int8 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int8_or_default<TValue>(TValue? value, short defaultValue) => default!;

        /// <summary>
        /// <c>toInt16(value)</c>: converts a numeric or textual value to the vendor <c>Int16</c>. Numeric narrowing wraps within the target width (two's complement), and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int16<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt16OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int16_or_null(string? value) => default!;

        /// <summary>
        /// <c>toInt16OrDefault(value)</c>: the vendor form that returns the Int16 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted).  A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int16_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt16OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toInt16(defaultValue)</c> so its vendor type matches the Int16 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? to_int16_or_default<TValue>(TValue? value, short defaultValue) => default!;

        /// <summary>
        /// <c>toInt32(value)</c>: converts a numeric or textual value to the vendor <c>Int32</c>. Numeric narrowing wraps within the target width (two's complement), and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_int32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt32OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_int32_or_null(string? value) => default!;

        /// <summary>
        /// <c>toInt32OrDefault(value)</c>: the vendor form that returns the Int32 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted).  A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_int32_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt32OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toInt32(defaultValue)</c> so its vendor type matches the Int32 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_int32_or_default<TValue>(TValue? value, int defaultValue) => default!;

        /// <summary>
        /// <c>toInt64(value)</c>: converts a numeric or textual value to the vendor <c>Int64</c>. Numeric narrowing wraps within the target width (two's complement), and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? to_int64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt64OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? to_int64_or_null(string? value) => default!;

        /// <summary>
        /// <c>toInt64OrDefault(value)</c>: the vendor form that returns the Int64 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted).  A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? to_int64_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt64OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toInt64(defaultValue)</c> so its vendor type matches the Int64 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? to_int64_or_default<TValue>(TValue? value, long defaultValue) => default!;

        /// <summary>
        /// <c>toUInt8(value)</c>: converts a numeric or textual value to the vendor <c>UInt8</c>. Numeric narrowing wraps modulo the target width, for example -1 becomes 255, and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? to_uint8<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt8OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? to_uint8_or_null(string? value) => default!;

        /// <summary>
        /// <c>toUInt8OrDefault(value)</c>: the vendor form that returns the UInt8 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted).  A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? to_uint8_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt8OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toUInt8(defaultValue)</c> so its vendor type matches the UInt8 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? to_uint8_or_default<TValue>(TValue? value, byte defaultValue) => default!;

        /// <summary>
        /// <c>toUInt16(value)</c>: converts a numeric or textual value to the vendor <c>UInt16</c>. Numeric narrowing wraps modulo the target width, and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_uint16<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt16OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_uint16_or_null(string? value) => default!;

        /// <summary>
        /// <c>toUInt16OrDefault(value)</c>: the vendor form that returns the UInt16 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted). The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_uint16_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt16OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toUInt16(defaultValue)</c> so its vendor type matches the UInt16 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? to_uint16_or_default<TValue>(TValue? value, int defaultValue) => default!;

        /// <summary>
        /// <c>toUInt32(value)</c>: converts a numeric or textual value to the vendor <c>UInt32</c>. Numeric narrowing wraps modulo the target width, and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? to_uint32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt32OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? to_uint32_or_null(string? value) => default!;

        /// <summary>
        /// <c>toUInt32OrDefault(value)</c>: the vendor form that returns the UInt32 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted).  A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? to_uint32_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt32OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toUInt32(defaultValue)</c> so its vendor type matches the UInt32 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? to_uint32_or_default<TValue>(TValue? value, uint defaultValue) => default!;

        /// <summary>
        /// <c>toUInt64(value)</c>: converts a numeric or textual value to the vendor <c>UInt64</c>. Numeric narrowing wraps modulo the target width, and a float is truncated toward zero; an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>ATTEMPT_TO_READ_AFTER_EOF</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? to_uint64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt64OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable or out-of-range value (the vendor accepts only a String argument, so a numeric input is rejected by the server). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? to_uint64_or_null(string? value) => default!;

        /// <summary>
        /// <c>toUInt64OrDefault(value)</c>: the vendor form that returns the UInt64 zero instead of raising on an unparsable or out-of-range value (numeric and textual inputs are accepted).  A <see langword="null"/> input yields the zero, not NULL (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? to_uint64_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt64OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toUInt64(defaultValue)</c> so its vendor type matches the UInt64 target; a constant default outside the target range is refused at translation, while a non-constant one is converted by the server with the vendor's narrowing rule. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? to_uint64_or_default<TValue>(TValue? value, ulong defaultValue) => default!;

        /// <summary>
        /// <c>toFloat32(value)</c>: converts a numeric or textual value to the vendor <c>Float32</c>. The text forms accept <c>nan</c>, <c>inf</c> and <c>-inf</c>; a numeric value outside the target range becomes +/-Infinity (the vendor does not raise), while an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>CANNOT_PARSE_NUMBER</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? to_float32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toFloat32OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable value (the vendor accepts only a String argument). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? to_float32_or_null(string? value) => default!;

        /// <summary>
        /// <c>toFloat32OrDefault(value)</c>: the vendor form that returns the Float32 zero instead of raising on an unparsable value (numeric and textual inputs are accepted). A <see langword="null"/> input yields the zero, not NULL. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? to_float32_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toFloat32OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toFloat32(defaultValue)</c> so its vendor type matches the Float32 target. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? to_float32_or_default<TValue>(TValue? value, float defaultValue) => default!;

        /// <summary>
        /// <c>toFloat64(value)</c>: converts a numeric or textual value to the vendor <c>Float64</c>. The text forms accept <c>nan</c>, <c>inf</c> and <c>-inf</c>; a numeric value outside the target range becomes +/-Infinity (the vendor does not raise), while an unparsable string raises <c>CANNOT_PARSE_TEXT</c> and an empty string raises <c>CANNOT_PARSE_NUMBER</c>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? to_float64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toFloat64OrNull(value)</c>: the vendor form that returns <c>NULL</c> instead of raising on an unparsable value (the vendor accepts only a String argument). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? to_float64_or_null(string? value) => default!;

        /// <summary>
        /// <c>toFloat64OrDefault(value)</c>: the vendor form that returns the Float64 zero instead of raising on an unparsable value (numeric and textual inputs are accepted). A <see langword="null"/> input yields the zero, not NULL. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? to_float64_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toFloat64OrDefault(value, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toFloat64(defaultValue)</c> so its vendor type matches the Float64 target. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? to_float64_or_default<TValue>(TValue? value, double defaultValue) => default!;

        /// <summary>
        /// <c>toDecimal32(value, scale)</c>: converts a numeric or textual value to the vendor <c>Decimal(9, scale)</c>. The scale is a compile-time constant 0..9 and is rendered as the vendor scale; fractional digits beyond it are truncated (not rounded), and a value with more than 9 significant digits raises <c>ARGUMENT_OUT_OF_BOUND</c>. A non-constant scale is refused with a precise reason. The result fits <see cref="decimal"/> exactly (9/18 digits are below the 28-29 digits of the 96-bit mantissa). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal32<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal32OrNull(value, scale)</c>: the vendor form that returns <c>NULL</c> instead of raising on a value that cannot be parsed or does not fit Decimal(9, scale) (the vendor accepts only a String argument). The scale is a compile-time constant 0..9. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal32_or_null(string? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal32OrDefault(value, scale)</c>: the vendor form that returns the Decimal(9, scale) zero instead of raising on an unparsable value or a value that does not fit. The scale is a compile-time constant 0..9. A <see langword="null"/> input yields the zero (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal32_or_default<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal32OrDefault(value, scale, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toDecimal32(defaultValue, scale)</c> so its vendor type matches the target, and the scale is a compile-time constant 0..9. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal32_or_default<TValue>(TValue? value, int scale, decimal defaultValue) => default!;

        /// <summary>
        /// <c>toDecimal64(value, scale)</c>: converts a numeric or textual value to the vendor <c>Decimal(18, scale)</c>. The scale is a compile-time constant 0..18 and is rendered as the vendor scale; fractional digits beyond it are truncated (not rounded), and a value with more than 18 significant digits raises <c>ARGUMENT_OUT_OF_BOUND</c>. A non-constant scale is refused with a precise reason. The result fits <see cref="decimal"/> exactly (9/18 digits are below the 28-29 digits of the 96-bit mantissa). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal64<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal64OrNull(value, scale)</c>: the vendor form that returns <c>NULL</c> instead of raising on a value that cannot be parsed or does not fit Decimal(18, scale) (the vendor accepts only a String argument). The scale is a compile-time constant 0..18. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal64_or_null(string? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal64OrDefault(value, scale)</c>: the vendor form that returns the Decimal(18, scale) zero instead of raising on an unparsable value or a value that does not fit. The scale is a compile-time constant 0..18. A <see langword="null"/> input yields the zero (the vendor does not propagate NULL through the fallback). Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal64_or_default<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal64OrDefault(value, scale, defaultValue)</c>: the vendor form that returns <paramref name="defaultValue"/> instead of raising. The default is rendered as <c>toDecimal64(defaultValue, scale)</c> so its vendor type matches the target, and the scale is a compile-time constant 0..18. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? to_decimal64_or_default<TValue>(TValue? value, int scale, decimal defaultValue) => default!;

        /// <summary>
        /// <c>toString(value)</c>: the vendor string representation of a scalar value. Float
        /// formatting is the shortest representation that round-trips (<c>0.1</c> for a
        /// Float32), DateTime is formatted in the server time zone (<c>yyyy-MM-dd HH:mm:ss</c>,
        /// with the fractional part for DateTime64), Bool is <c>true</c>/<c>false</c>, Decimal
        /// drops trailing zeros and UUID is the canonical hyphenated text. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? to_string<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toBool(value)</c>: converts a numeric or textual value to the vendor <c>Bool</c>.
        /// Numeric 0 is false and every non-zero value (including <c>0.5</c> and <c>-1</c>) is
        /// true; the text forms accept <c>true</c>/<c>false</c>, <c>T</c>/<c>F</c>, <c>Y</c>/<c>N</c>,
        /// <c>yes</c>/<c>no</c>, <c>on</c>/<c>off</c>, <c>enable</c>/<c>disable</c>,
        /// <c>enabled</c>/<c>disabled</c> and <c>1</c>/<c>0</c> (the server's
        /// <c>bool_true_representation</c>/<c>bool_false_representation</c> settings control the
        /// exact spellings). An unparsable or empty string raises <c>CANNOT_PARSE_BOOL</c>. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public bool? to_bool<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toFixedString(value, length)</c>: renders the string as the vendor <c>FixedString(N)</c>.
        /// A shorter value is padded with zero bytes and a longer one raises
        /// <c>TOO_LARGE_STRING_SIZE</c> (it is never truncated). The length is a compile-time
        /// constant 1..255 and is rendered inline because the vendor rejects a column or parameter in
        /// that position; the result is the exact byte sequence, including the trailing NULs, held
        /// by <see cref="string"/>. A <see langword="null"/> input yields <see langword="null"/>.
        /// Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? to_fixed_string<TValue>(TValue? value, byte length) => default!;

        /// <summary>
        /// <c>toLowCardinality(value)</c>: wraps the value in the vendor <c>LowCardinality(T)</c>
        /// storage type. The wrapper has no value-level effect, so the result is the underlying CLR
        /// value; a Nullable column becomes <c>LowCardinality(Nullable(T))</c>. The vendor rejects a
        /// bare <c>NULL</c> literal because its type is <c>Nullable(Nothing)</c> (a tested
        /// expected-negative), while a nullable column and a NULL value inside it are accepted. A
        /// <see langword="null"/> input yields <see langword="null"/>. Requires a provider that
        /// supports it (ClickHouse).
        /// </summary>
        public string? to_low_cardinality<TValue>(TValue? value) => default!;

        // accurateCast: the checked conversion. Unlike toInt8, accurateCast(300, 'Int8') raises
        // CANNOT_CONVERT_TYPE instead of wrapping, and accurateCast(12.7, 'Int8') raises instead
        // of truncating; an integral float (12.0) is exact and accepted. Each delivered target has
        // its own member so the result stays typed; the target type is a fixed literal in the arm.

        /// <summary>
        /// <c>accurateCast(value, 'Int8')</c>: the checked conversion to <c>Int8</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>. The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public short? accurate_cast_int8<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Int8')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? accurate_cast_int8_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Int8')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed. The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? accurate_cast_int8_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Int16')</c>: the checked conversion to <c>Int16</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public short? accurate_cast_int16<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Int16')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? accurate_cast_int16_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Int16')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? accurate_cast_int16_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Int32')</c>: the checked conversion to <c>Int32</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public int? accurate_cast_int32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Int32')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? accurate_cast_int32_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Int32')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? accurate_cast_int32_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Int64')</c>: the checked conversion to <c>Int64</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public long? accurate_cast_int64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Int64')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? accurate_cast_int64_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Int64')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? accurate_cast_int64_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'UInt8')</c>: the checked conversion to <c>UInt8</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public byte? accurate_cast_uint8<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'UInt8')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? accurate_cast_uint8_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'UInt8')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? accurate_cast_uint8_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'UInt16')</c>: the checked conversion to <c>UInt16</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>. The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public int? accurate_cast_uint16<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'UInt16')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? accurate_cast_uint16_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'UInt16')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed. The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? accurate_cast_uint16_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'UInt32')</c>: the checked conversion to <c>UInt32</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public uint? accurate_cast_uint32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'UInt32')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? accurate_cast_uint32_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'UInt32')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? accurate_cast_uint32_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'UInt64')</c>: the checked conversion to <c>UInt64</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public ulong? accurate_cast_uint64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'UInt64')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? accurate_cast_uint64_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'UInt64')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? accurate_cast_uint64_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Float32')</c>: the checked conversion to <c>Float32</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public float? accurate_cast_float32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Float32')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? accurate_cast_float32_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Float32')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? accurate_cast_float32_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Float64')</c>: the checked conversion to <c>Float64</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public double? accurate_cast_float64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Float64')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? accurate_cast_float64_or_null<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Float64')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? accurate_cast_float64_or_default<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Decimal(9, scale)')</c>: the checked conversion to <c>Decimal(9, S)</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>. The scale is a compile-time constant 0..9 and forms the target type <c>Decimal(9, scale)</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public decimal? accurate_cast_decimal32<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Decimal(9, scale)')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. The scale is a compile-time constant 0..9 and forms the target type <c>Decimal(9, scale)</c>. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? accurate_cast_decimal32_or_null<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Decimal(9, scale)')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed. The scale is a compile-time constant 0..9 and forms the target type <c>Decimal(9, scale)</c>.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? accurate_cast_decimal32_or_default<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>accurateCast(value, 'Decimal(18, scale)')</c>: the checked conversion to <c>Decimal(18, S)</c>.
        /// Unlike the <c>to*</c> member it raises <c>CANNOT_CONVERT_TYPE</c> instead of
        /// wrapping or truncating when the value is not exactly representable (300 to Int8,
        /// 12.7 to Int8, 12.34 to Float32), while an exact value such as 12.0 to Int8 is
        /// accepted; an unparsable string still raises <c>CANNOT_PARSE_TEXT</c>. The scale is a compile-time constant 0..18 and forms the target type <c>Decimal(18, scale)</c>.  A <see langword="null"/> input yields <see langword="null"/>. Requires a provider
        /// that supports it (ClickHouse).
        /// </summary>
        public decimal? accurate_cast_decimal64<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>accurateCastOrNull(value, 'Decimal(18, scale)')</c>: the checked conversion that
        /// returns <c>NULL</c> instead of raising when the value is not exactly representable
        /// or cannot be parsed. The scale is a compile-time constant 0..18 and forms the target type <c>Decimal(18, scale)</c>. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? accurate_cast_decimal64_or_null<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>accurateCastOrDefault(value, 'Decimal(18, scale)')</c>: the checked conversion that
        /// returns the target zero instead of raising when the value is not exactly
        /// representable or cannot be parsed. The scale is a compile-time constant 0..18 and forms the target type <c>Decimal(18, scale)</c>.  A <see langword="null"/>
        /// input yields the zero. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public decimal? accurate_cast_decimal64_or_default<TValue>(TValue? value, int scale) => default!;

        // reinterpretAs: the bit-pattern reinterpretation, never a numeric conversion. The
        // source bytes are read as the target width in the server's byte order (little-endian on
        // the pinned 25.8.33.6 server); a narrower source is zero-padded to the target width.

        /// <summary>
        /// <c>reinterpretAsUInt8(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>UInt8</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public byte? reinterpret_as_uint8<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsUInt16(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>UInt16</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. The vendor returns <c>UInt16</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt32</c> and surfaced as <see cref="int"/> (0..65535 is lossless). A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? reinterpret_as_uint16<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsUInt32(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>UInt32</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public uint? reinterpret_as_uint32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsUInt64(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>UInt64</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public ulong? reinterpret_as_uint64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsInt8(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Int8</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. The vendor returns <c>Int8</c>, which the shared row reader cannot materialize, so the result is widened exactly with <c>toInt16</c> and surfaced as <see cref="short"/>. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? reinterpret_as_int8<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsInt16(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Int16</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public short? reinterpret_as_int16<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsInt32(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Int32</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public int? reinterpret_as_int32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsInt64(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Int64</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public long? reinterpret_as_int64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsFloat32(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Float32</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public float? reinterpret_as_float32<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsFloat64(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Float64</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public double? reinterpret_as_float64<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsDate(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>Date</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public DateTime? reinterpret_as_date<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsDateTime(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>DateTime</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public DateTime? reinterpret_as_date_time<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsString(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>String</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? reinterpret_as_string<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsFixedString(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>FixedString</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public string? reinterpret_as_fixed_string<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsUUID(value)</c>: reinterprets the value's bytes as the vendor
        /// <c>UUID</c> type without any numeric conversion. The bytes are
        /// read in the server's byte order (little-endian on the pinned server), so
        /// <c>reinterpretAsUInt32('abcd')</c> is <c>0x64636261</c>; a narrower source is zero-padded
        /// to the target width. A <see langword="null"/> input yields
        /// <see langword="null"/>. Requires a provider that supports it (ClickHouse).
        /// </summary>
        public Guid? reinterpret_as_uuid<TValue>(TValue? value) => default!;

        // Deterministic CLR-shape refusals (T286b F15). The translator raises the precise reason
        // before the provider gate, so every provider fails closed with the same message.

        /// <summary>
        /// <c>toInt128(value)</c> — <b>refused</b>. ClickHouse <c>Int128</c> has no exact fixed-width CLR integer type; the driver surfaces it as <see cref="System.Numerics.BigInteger"/>, but the shared row materializer has no BigInteger accessor (<c>SelectExpression.GetDataRecordMethod</c>), so the declared shape cannot be materialized and adding one is a shared-contract change outside this unit.
        /// </summary>
        public object? to_int128<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt128(value)</c> — <b>refused</b>. ClickHouse <c>UInt128</c> has no exact fixed-width CLR integer type; the driver surfaces it as <see cref="System.Numerics.BigInteger"/>, but the shared row materializer has no BigInteger accessor (<c>SelectExpression.GetDataRecordMethod</c>), so the declared shape cannot be materialized and adding one is a shared-contract change outside this unit.
        /// </summary>
        public object? to_uint128<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toInt256(value)</c> — <b>refused</b>. ClickHouse <c>Int256</c> has no exact fixed-width CLR integer type; the driver surfaces it as <see cref="System.Numerics.BigInteger"/>, but the shared row materializer has no BigInteger accessor (<c>SelectExpression.GetDataRecordMethod</c>), so the declared shape cannot be materialized and adding one is a shared-contract change outside this unit.
        /// </summary>
        public object? to_int256<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toUInt256(value)</c> — <b>refused</b>. ClickHouse <c>UInt256</c> has no exact fixed-width CLR integer type; the driver surfaces it as <see cref="System.Numerics.BigInteger"/>, but the shared row materializer has no BigInteger accessor (<c>SelectExpression.GetDataRecordMethod</c>), so the declared shape cannot be materialized and adding one is a shared-contract change outside this unit.
        /// </summary>
        public object? to_uint256<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toDecimal128(value, scale)</c> — <b>refused</b>. Decimal128 carries up to 38 significant decimal digits, while <see cref="decimal"/>'s 128-bit storage has a 96-bit mantissa (28-29 digits); a value above that range cannot be materialized without silent precision loss or <see cref="OverflowException"/>.
        /// </summary>
        public object? to_decimal128<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>toDecimal256(value, scale)</c> — <b>refused</b>. Decimal256 carries up to 76 significant decimal digits, while <see cref="decimal"/>'s 128-bit storage has a 96-bit mantissa (28-29 digits); a value above that range cannot be materialized without silent precision loss or <see cref="OverflowException"/>.
        /// </summary>
        public object? to_decimal256<TValue>(TValue? value, int scale) => default!;

        /// <summary>
        /// <c>toIntervalNanosecond(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalNanosecond</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalNanosecond'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_nanosecond<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalMicrosecond(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalMicrosecond</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalMicrosecond'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_microsecond<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalMillisecond(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalMillisecond</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalMillisecond'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_millisecond<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalSecond(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalSecond</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalSecond'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_second<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalMinute(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalMinute</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalMinute'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_minute<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalHour(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalHour</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalHour'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_hour<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalDay(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalDay</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalDay'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_day<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalWeek(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalWeek</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalWeek'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_week<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalMonth(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalMonth</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalMonth'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_month<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalQuarter(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalQuarter</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalQuarter'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_quarter<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>toIntervalYear(value)</c> — <b>refused</b>. The vendor returns the ClickHouse-only <c>IntervalYear</c> type, which has no CLR representation and is usable only inside date arithmetic; the provider driver itself cannot materialize it (<c>ArgumentException 'Unknown type: IntervalYear'</c> on 25.8.33.6).
        /// </summary>
        public object? to_interval_year<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsUInt128(value)</c> — <b>refused</b>. The vendor returns <c>UInt128</c>, which the driver surfaces as <see cref="System.Numerics.BigInteger"/>; the shared row materializer has no BigInteger accessor, so the bit pattern cannot be materialized into a fixed-width CLR integer.
        /// </summary>
        public object? reinterpret_as_uint128<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsUInt256(value)</c> — <b>refused</b>. The vendor returns <c>UInt256</c>, which the driver surfaces as <see cref="System.Numerics.BigInteger"/>; the shared row materializer has no BigInteger accessor, so the bit pattern cannot be materialized into a fixed-width CLR integer.
        /// </summary>
        public object? reinterpret_as_uint256<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsInt128(value)</c> — <b>refused</b>. The vendor returns <c>Int128</c>, which the driver surfaces as <see cref="System.Numerics.BigInteger"/>; the shared row materializer has no BigInteger accessor, so the bit pattern cannot be materialized into a fixed-width CLR integer.
        /// </summary>
        public object? reinterpret_as_int128<TValue>(TValue? value) => default!;

        /// <summary>
        /// <c>reinterpretAsInt256(value)</c> — <b>refused</b>. The vendor returns <c>Int256</c>, which the driver surfaces as <see cref="System.Numerics.BigInteger"/>; the shared row materializer has no BigInteger accessor, so the bit pattern cannot be materialized into a fixed-width CLR integer.
        /// </summary>
        public object? reinterpret_as_int256<TValue>(TValue? value) => default!;
        // =====================================================================================
        // T286b F16 -- the ClickHouse-only aggregate catalogue.
        // =====================================================================================

        /// <summary>
        /// <c>entropy(value)</c>: the Shannon entropy of the value distribution. Requires a provider
        /// that supports the ClickHouse aggregate catalogue (see
        /// <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse). The result is <c>Float64</c>.
        /// </summary>
        public double entropy<T>(T? value) => default!;

        /// <summary>
        /// <c>first_value(value)</c>: the first encountered value — the vendor alias of <c>any</c>.
        /// This is the aggregate (non-window) form; the window form is the inherited
        /// <see cref="CommonFunctions.first_value"/> completed with <c>Over(...)</c>. The member
        /// deliberately hides the inherited window member so the aggregate result type is <c>T?</c>.
        /// Requires a provider that supports the ClickHouse aggregate catalogue
        /// (see <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public new T? first_value<T>(T? value) => default!;

        /// <summary>
        /// <c>last_value(value)</c>: the last encountered value, the NULL-accepting sibling of
        /// <c>anyLast</c>. This is the aggregate (non-window) form; the window form is the inherited
        /// <see cref="CommonFunctions.last_value"/> completed with <c>Over(...)</c>. The member
        /// deliberately hides the inherited window member so the aggregate result type is <c>T?</c>.
        /// Requires a provider that supports the ClickHouse aggregate catalogue
        /// (see <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public new T? last_value<T>(T? value) => default!;

        /// <summary>
        /// <c>maxIntersections(start, end)</c>: the maximum number of intervals that intersect at one
        /// point. The native <c>UInt64</c> is lowered with <c>toInt64</c>. Requires a provider that
        /// supports the ClickHouse aggregate catalogue (see
        /// <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public long max_intersections<TStart, TEnd>(TStart? start, TEnd? end) => default!;

        /// <summary>
        /// <c>maxIntersectionsPosition(start, end)</c>: the position where the maximum number of
        /// intervals intersects (the <c>Position</c> sibling of
        /// <see cref="max_intersections{TStart,TEnd}"/>). The native integer is narrowed with
        /// <c>toInt32</c>. Requires a provider that supports the ClickHouse aggregate catalogue (see
        /// <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public int max_intersections_position<TStart, TEnd>(TStart? start, TEnd? end) => default!;

        /// <summary>
        /// <c>simpleLinearRegression(x, y)</c>: the slope and intercept of the least-squares line,
        /// surfaced as <c>Tuple(k, b)</c> with both components <c>Float64</c>. Requires a provider that
        /// supports the ClickHouse aggregate catalogue (see
        /// <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public Tuple<double, double> simple_linear_regression<TX, TY>(TX? x, TY? y) => default!;

        /// <summary>
        /// <c>stochasticLinearRegression(...)</c> — <b>refused</b>. The aggregate returns an opaque
        /// <c>AggregateFunction</c> state (a binary serialization) with no typed CLR result; only the
        /// <c>-Merge</c> combinator yields weights, and it requires an <c>AggregateFunction</c>-typed
        /// column the provider cannot read.
        /// </summary>
        public object? stochastic_linear_regression<TValue, TTarget>(TValue? value, TTarget? target) => default!;

        /// <summary>
        /// <c>stochasticLogisticRegression(...)</c> — <b>refused</b>. The aggregate returns an opaque
        /// <c>AggregateFunction</c> state (a binary serialization) with no typed CLR result; only the
        /// <c>-Merge</c> combinator yields weights, and it requires an <c>AggregateFunction</c>-typed
        /// column the provider cannot read.
        /// </summary>
        public object? stochastic_logistic_regression<TValue, TTarget>(TValue? value, TTarget? target) => default!;

        /// <summary>
        /// <c>deltaSum(value)</c>: the sum of the arithmetic differences between consecutive rows; the
        /// result keeps the input type. Requires a provider that supports the ClickHouse aggregate
        /// catalogue (see <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public T? delta_sum<T>(T? value) => default!;

        /// <summary>
        /// <c>deltaSumTimestamp(value, time)</c>: the consecutive-row differences ordered by
        /// <paramref name="time"/>, ignoring negative differences. The native result is the unsigned
        /// counterpart of the input type and is normalised back to the declared type. Requires a
        /// provider that supports the ClickHouse aggregate catalogue (see
        /// <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse).
        /// </summary>
        public TValue? delta_sum_timestamp<TValue, TTime>(TValue? value, TTime? time) => default!;

        /// <summary>
        /// <c>exponentialMovingAverage(...)</c> — <b>refused</b>. The aggregate returns an opaque
        /// <c>AggregateFunction</c> state (a binary serialization) with no typed CLR result; only the
        /// <c>-Merge</c> combinator yields the moving average, and it requires an
        /// <c>AggregateFunction</c>-typed column the provider cannot read.
        /// </summary>
        public object? exponential_moving_average<TValue, TTime>(long halfLife, TValue? value, TTime? time) => default!;

        /// <summary>
        /// <c>gini(value)</c>: the Gini coefficient of the sample. Introduced after the pinned
        /// 25.8.33.6 server, where the native name raises <c>UNKNOWN_FUNCTION</c> (pinned by the live
        /// negative test); the arm renders the vendor name for servers that ship it. Requires a
        /// provider that supports the ClickHouse aggregate catalogue (see
        /// <see cref="ISqlDialect.ClickHouseAggregates"/>; ClickHouse). The result is <c>Float64</c>.
        /// </summary>
        public double gini<T>(T? value) => default!;

        /// <summary><c>skewPop(value)</c>: the population skewness. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double skew_pop<T>(T? value) => default!;

        /// <summary><c>skewSamp(value)</c>: the sample skewness. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double skew_samp<T>(T? value) => default!;

        /// <summary><c>kurtPop(value)</c>: the population kurtosis. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double kurt_pop<T>(T? value) => default!;

        /// <summary><c>kurtSamp(value)</c>: the sample kurtosis. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double kurt_samp<T>(T? value) => default!;

        /// <summary><c>rankCorr(x, y)</c>: the Spearman rank correlation coefficient. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double rank_corr<TX, TY>(TX? x, TY? y) => default!;

        /// <summary><c>contingency(x, y)</c>: the contingency coefficient of two columns. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double contingency<TX, TY>(TX? x, TY? y) => default!;

        /// <summary><c>cramersV(x, y)</c>: Cramer's V association measure. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double cramers_v<TX, TY>(TX? x, TY? y) => default!;

        /// <summary><c>cramersVBiasCorrected(x, y)</c>: the bias-corrected Cramer's V. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double cramers_v_bias_corrected<TX, TY>(TX? x, TY? y) => default!;

        /// <summary><c>theilsU(x, y)</c>: Theil's U uncertainty coefficient. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double theils_u<TX, TY>(TX? x, TY? y) => default!;

        /// <summary>
        /// <c>welchTTest(sample_data, sample_index)</c>: Welch's t-test over the two populations
        /// encoded by <paramref name="sampleIndex"/> (0 = first population, otherwise the second),
        /// surfaced as <c>Tuple(t_statistic, p_value)</c>. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<double, double> welch_t_test<TData, TIndex>(TData? sampleData, TIndex? sampleIndex) => default!;

        /// <summary>
        /// <c>studentTTest(sample_data, sample_index)</c>: Student's t-test over the two populations
        /// encoded by <paramref name="sampleIndex"/> (0 = first population, otherwise the second),
        /// surfaced as <c>Tuple(t_statistic, p_value)</c>. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<double, double> student_t_test<TData, TIndex>(TData? sampleData, TIndex? sampleIndex) => default!;

        /// <summary>
        /// <c>studentTTestOneSample(value, populationMean)</c>: the one-sample Student t-test, surfaced
        /// as <c>Tuple(t_statistic, p_value)</c>. Introduced after the pinned 25.8.33.6 server, where
        /// the native name raises <c>UNKNOWN_FUNCTION</c> (pinned by the live negative test). Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<double, double> student_t_test_one_sample<T>(T? value, double populationMean) => default!;

        /// <summary>
        /// <c>mannWhitneyUTest(sample_data, sample_index)</c>: the Mann-Whitney rank test over the two
        /// non-empty populations encoded by <paramref name="sampleIndex"/>, surfaced as
        /// <c>Tuple(u_statistic, p_value)</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public Tuple<double, double> mann_whitney_u_test<TData, TIndex>(TData? sampleData, TIndex? sampleIndex) => default!;

        /// <summary>
        /// <c>kolmogorovSmirnovTest(sample_data, sample_index)</c>: the Kolmogorov-Smirnov test over the
        /// two non-empty populations encoded by <paramref name="sampleIndex"/>, surfaced as
        /// <c>Tuple(d_statistic, p_value)</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public Tuple<double, double> kolmogorov_smirnov_test<TData, TIndex>(TData? sampleData, TIndex? sampleIndex) => default!;

        /// <summary>
        /// <c>meanZTest(population_variance_x, population_variance_y, confidence_level)(sample_data, sample_index)</c>:
        /// the mean z-test, surfaced as <c>Tuple(z_statistic, p_value, confidence_interval_low,
        /// confidence_interval_high)</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public Tuple<double, double, double, double> mean_z_test<TData, TIndex>(
            double populationVarianceX,
            double populationVarianceY,
            double confidenceLevel,
            TData? sampleData,
            TIndex? sampleIndex) => default!;

        /// <summary>
        /// <c>analysisOfVariance(value, group)</c>: the one-way ANOVA test, surfaced as
        /// <c>Tuple(f_statistic, p_value)</c>. Groups are numbered from 0, at least two groups are
        /// required, and the vendor requires the group argument to be a native unsigned integer (a
        /// signed column is rejected with BAD_ARGUMENTS, pinned live). Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<double, double> analysis_of_variance<TValue, TGroup>(TValue? value, TGroup? group) => default!;

        /// <summary><c>quantileTDigest(level)(value)</c>: the approximate quantile through the t-digest algorithm; the native <c>Float32</c> is widened with <c>toFloat64</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double? quantile_tdigest<T>(double level, T? value) => default!;

        /// <summary>
        /// <c>quantileGK(accuracy, level)(value)</c>: the Greenwald-Khanna quantile; <paramref name="accuracy"/>
        /// is a constant positive integer (100 gives roughly a 1% error). The native result keeps the
        /// input type and is widened with <c>toFloat64</c>. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public double? quantile_gk<T>(long accuracy, double level, T? value) => default!;

        /// <summary>
        /// <c>quantileDD(relative_accuracy, level)(value)</c>: the DD-sketch quantile with a relative
        /// error guarantee; the vendor takes the accuracy first. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public double? quantile_dd<T>(double relativeAccuracy, double level, T? value) => default!;

        /// <summary><c>quantileBFloat16(level)(value)</c>: the approximate quantile over bfloat16 numbers. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse); the result is <c>Float64</c>.</summary>
        public double? quantile_bfloat16<T>(double level, T? value) => default!;

        /// <summary>
        /// <c>quantilePrometheusHistogram(level)(bucket_upper_bound, cumulative_bucket_value)</c>: the
        /// PromQL <c>histogram_quantile</c> interpolation over a classic histogram; the highest bucket
        /// must have an upper bound of <c>+Inf</c>. Introduced in 25.10, so the pinned 25.8.33.6 server
        /// raises <c>UNKNOWN_FUNCTION</c> (pinned by the live negative test); the arm renders the
        /// vendor name for servers that ship it. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public double? quantile_prometheus_histogram<TBound, TCumulative>(
            double level,
            TBound? bucketUpperBound,
            TCumulative? cumulativeBucketValue) => default!;

        /// <summary><c>quantilesExact(level1, level2, ...)(value)</c>: the exact quantiles at every level; the levels must be an inline array and each one is rendered as its own parameter. The native element type follows the input and is normalised to <c>Float64</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] quantiles_exact<T>(double[] levels, T? value) => default!;

        /// <summary><c>quantilesExactLow(level1, level2, ...)(value)</c>: the low-biased exact quantiles. The levels must be an inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] quantiles_exact_low<T>(double[] levels, T? value) => default!;

        /// <summary><c>quantilesExactHigh(level1, level2, ...)(value)</c>: the high-biased exact quantiles. The levels must be an inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] quantiles_exact_high<T>(double[] levels, T? value) => default!;

        /// <summary>
        /// <c>quantilesExactWeighted(level1, level2, ...)(value, weight)</c>: the weighted exact
        /// quantiles. The weight must be an unsigned integer on the server (a signed column is rejected
        /// with <c>ILLEGAL_TYPE_OF_ARGUMENT</c>). The levels must be an inline array. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public double[] quantiles_exact_weighted<T, TWeight>(double[] levels, T? value, TWeight? weight) => default!;

        /// <summary><c>quantilesTiming(level1, level2, ...)(value)</c>: the timing-optimised quantiles; the native <c>Array(Float32)</c> is normalised to <c>double[]</c>. The levels must be an inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] quantiles_timing<T>(double[] levels, T? value) => default!;

        /// <summary><c>quantilesTDigest(level1, level2, ...)(value)</c>: the t-digest quantiles; the native <c>Array(Float32)</c> is normalised to <c>double[]</c>. The levels must be an inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] quantiles_tdigest<T>(double[] levels, T? value) => default!;

        /// <summary><c>quantilesBFloat16(level1, level2, ...)(value)</c>: the bfloat16 quantiles. The levels must be an inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] quantiles_bfloat16<T>(double[] levels, T? value) => default!;

        /// <summary>
        /// <c>quantilesDD(relative_accuracy, level1, level2, ...)(value)</c>: the DD-sketch quantiles;
        /// the vendor takes the accuracy first. The levels must be an inline array. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public double[] quantiles_dd<T>(double relativeAccuracy, double[] levels, T? value) => default!;

        /// <summary>
        /// <c>quantilesGK(accuracy, level1, level2, ...)(value)</c>: the Greenwald-Khanna quantiles;
        /// the vendor takes the integer accuracy first and the levels after it. The levels must be an
        /// inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public double[] quantiles_gk<T>(long accuracy, double[] levels, T? value) => default!;

        /// <summary>
        /// <c>quantilesPrometheusHistogram(level1, level2, ...)(bucket_upper_bound, cumulative_bucket_value)</c>:
        /// the PromQL histogram quantiles over a classic histogram. Introduced in 25.10, so the pinned
        /// 25.8.33.6 server raises <c>UNKNOWN_FUNCTION</c> (pinned by the live negative test). The
        /// levels must be an inline array. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public double[] quantiles_prometheus_histogram<TBound, TCumulative>(
            double[] levels,
            TBound? bucketUpperBound,
            TCumulative? cumulativeBucketValue) => default!;

        /// <summary>
        /// <c>approx_top_k(N)(value)</c>: the approximately most frequent values with their counts and
        /// error bounds, surfaced as <c>Tuple(item, count, error)[]</c> with <c>long</c> counters.
        /// Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<T, long, long>[] approx_top_k<T>(long k, T? value) => default!;

        /// <summary><c>approx_top_sum(N)(value, weight)</c>: the values with the largest approximate weight sum, surfaced as <c>Tuple(item, count, error)[]</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public Tuple<T, long, long>[] approx_top_sum<T, TWeight>(long k, T? value, TWeight? weight) => default!;

        /// <summary><c>anyHeavy(value)</c>: a frequently occurring value selected by the heavy-hitters algorithm (non-deterministic). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public T? any_heavy<T>(T? value) => default!;

        /// <summary>
        /// <c>maxMappedArrays(key, value)</c> (the <c>maxMap</c> form): the maximum value per key,
        /// surfaced as <c>Tuple(keys[], values[])</c> in sorted key order. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<TKey[], TValue[]> max_map<TKey, TValue>(TKey[]? keys, TValue[]? values) => default!;

        /// <summary><c>minMappedArrays(key, value)</c> (the <c>minMap</c> form): the minimum value per key, surfaced as <c>Tuple(keys[], values[])</c> in sorted key order. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public Tuple<TKey[], TValue[]> min_map<TKey, TValue>(TKey[]? keys, TValue[]? values) => default!;

        /// <summary>
        /// <c>sumCount(value)</c>: the sum and the row count in one pass, surfaced as
        /// <c>Tuple(sum, count)</c> with a <c>long</c> count. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<TSum, long> sum_count<TSum>(TSum value) where TSum : struct => default!;

        /// <summary><c>sumKahan(value)</c>: the Kahan-compensated sum as <c>Float64</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double sum_kahan<T>(T? value) => default!;

        /// <summary><c>groupArrayMovingAvg(value)</c>: the running average of the group, surfaced as <c>double[]</c> (native <c>Array(Float64)</c>). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double[] group_array_moving_avg<T>(T? value) => default!;

        /// <summary><c>groupArrayMovingSum(value)</c>: the running sum of the group, surfaced as <c>long[]</c> (native <c>Array(Int64)</c>). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public long[] group_array_moving_sum<T>(T? value) => default!;

        /// <summary><c>groupArraySample(size)(value)</c>: at most <paramref name="size"/> randomly selected values. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public T[] group_array_sample<T>(long size, T? value) => default!;

        /// <summary><c>groupArraySample(size, seed)(value)</c>: the seeded random sample. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public T[] group_array_sample<T>(long size, long seed, T? value) => default!;

        /// <summary><c>groupArraySorted(limit)(value)</c>: the first <paramref name="limit"/> values in ascending order. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public T[] group_array_sorted<T>(long limit, T? value) => default!;

        /// <summary><c>groupArrayLast(limit)(value)</c>: the last <paramref name="limit"/> values. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public T[] group_array_last<T>(long limit, T? value) => default!;

        /// <summary>
        /// <c>groupArrayInsertAt(value, position)</c>: inserts <paramref name="value"/> at the zero-based
        /// <paramref name="position"/>; empty positions are zero-filled (the vendor default). The position
        /// must be an unsigned integer. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public T[] group_array_insert_at<T>(T? value, uint position) => default!;

        /// <summary>
        /// <c>groupArrayInsertAt(default, size)(value, position)</c>: inserts <paramref name="value"/>
        /// at <paramref name="position"/> into an array of <paramref name="size"/> positions filled with
        /// <paramref name="defaultValue"/>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public T[] group_array_insert_at<T>(T? defaultValue, uint size, T? value, uint position) => default!;

        /// <summary><c>groupArrayIntersect(array)</c>: the intersection of the arrays in the group. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public T[] group_array_intersect<T>(T[]? values) => default!;

        /// <summary><c>groupConcat(value)</c>: the concatenation of the group's string values. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public string? group_concat<T>(T? value) => default!;

        /// <summary><c>groupConcat(separator)(value)</c>: the concatenation with an explicit separator. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public string? group_concat<T>(string? separator, T? value) => default!;

        /// <summary><c>groupConcat(separator, max_elements)(value)</c>: the capped concatenation with an explicit separator. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public string? group_concat<T>(string? separator, long maxElements, T? value) => default!;

        /// <summary>
        /// <c>groupFormat(format)(value, ...)</c>: every row of the group rendered through the named
        /// output format (<c>JSONEachRow</c>, <c>CSV</c>, <c>TabSeparated</c>, ...). The format name is
        /// a parameter and must be a constant. Introduced after the pinned 25.8.33.6 server, where the
        /// native name raises <c>UNKNOWN_FUNCTION</c> (pinned by the live negative test). Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public string? group_format(string? format, params object?[] values) => default!;

        /// <summary>
        /// <c>histogram(bins)(value)</c>: an adaptive histogram surfaced as
        /// <c>Tuple(lower, upper, height)[]</c> with all components <c>Float64</c>. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<double, double, double>[] histogram<T>(long bins, T? value) => default!;

        /// <summary>
        /// <c>sparkbar(buckets)(value, weight)</c>: the frequency histogram rendered as a bar string.
        /// The value must be an integer, Date or DateTime column, and the weight carries the frequency.
        /// Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public string? sparkbar<TValue, TWeight>(long buckets, TValue? value, TWeight? weight) => default!;

        /// <summary>
        /// <c>sparkbar(buckets, min_x, max_x)(value, weight)</c>: the frequency histogram over an
        /// explicit interval; the bounds must have the same type as the value. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public string? sparkbar<TValue, TWeight>(long buckets, TValue min, TValue max, TValue? value, TWeight? weight) => default!;

        /// <summary>
        /// <c>uniqUpTo(N)(value)</c>: the exact distinct count while it is at most <paramref name="n"/>,
        /// otherwise <c>N + 1</c> (the maximum accepted N is 100). The native <c>UInt64</c> is lowered
        /// with <c>toInt64</c>. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public long uniq_up_to<T>(long n, T? value) => default!;

        /// <summary>
        /// <c>sequenceCount(pattern)(timestamp, cond1, cond2, ...)</c>: the number of non-overlapping
        /// event chains matching the pattern. The conditions must be inline expressions. The native
        /// <c>UInt64</c> is lowered with <c>toInt64</c>. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public long sequence_count<TTime>(string? pattern, TTime? timestamp, params bool[] conditions) => default!;

        /// <summary>
        /// <c>sequenceMatchEvents(pattern)(timestamp, cond1, cond2, ...)</c>: the timestamps of the
        /// longest matching chain, surfaced as <c>DateTime[]</c> (native <c>Array(DateTime)</c>). The
        /// conditions must be inline expressions. Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public DateTime[] sequence_match_events<TTime>(string? pattern, TTime? timestamp, params bool[] conditions) => default!;

        /// <summary>
        /// <c>sequenceNextNode(direction, base)(timestamp, event_column, base_condition, event1, ...)</c>:
        /// the event value following the matched chain, or <c>null</c>. It is an experimental vendor
        /// aggregate gated by the <c>allow_experimental_funnel_functions</c> setting (without it the
        /// pinned server raises <c>UNKNOWN_AGGREGATE_FUNCTION</c>, pinned by the live negative test).
        /// The events must be inline expressions. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public string? sequence_next_node<TTime, TEvent>(
            string? direction,
            string? basePosition,
            TTime? timestamp,
            TEvent? eventColumn,
            bool baseCondition,
            params bool[] conditions) => default!;

        /// <summary>
        /// <c>timeSeriesGroupSum(timestamp, value)</c>: the per-timestamp sum of a time series. The
        /// aggregate was removed before the pinned 25.8.33.6 server, where the native name raises
        /// <c>UNKNOWN_FUNCTION</c> (pinned by the live negative test; the server points at
        /// <c>timeSeriesGroupArray</c>); the arm renders the vendor name for servers that still ship
        /// it. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<DateTime, double>[] time_series_group_sum<TTime, TValue>(TTime? timestamp, TValue? value) => default!;

        /// <summary>
        /// <c>timeSeriesGroupRateSum(timestamp, value)</c>: the per-timestamp rate sum of a time
        /// series. Removed before the pinned 25.8.33.6 server (the native name raises
        /// <c>UNKNOWN_FUNCTION</c>, pinned live); the arm renders the vendor name for servers that
        /// still ship it. Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public Tuple<DateTime, double>[] time_series_group_rate_sum<TTime, TValue>(TTime? timestamp, TValue? value) => default!;

        /// <summary>
        /// <c>timeSeriesInstantRateToGrid(start, end, step, staleness)(timestamp, value)</c>: the
        /// PromQL <c>irate</c> values on a regular grid, surfaced as <c>double?[]</c> (native
        /// <c>Array(Nullable(Float64))</c>; a grid point without enough samples is <c>null</c>).
        /// Experimental on 25.8: it requires the
        /// <c>allow_experimental_time_series_aggregate_functions</c> setting (the live test sets it
        /// through <c>.Settings(...)</c>). Requires <see cref="ISqlDialect.ClickHouseAggregates"/>
        /// (ClickHouse).
        /// </summary>
        public double?[] time_series_instant_rate_to_grid<TTimestamp, TValue>(
            long startTimestamp,
            long endTimestamp,
            long gridStep,
            long staleness,
            TTimestamp? timestamp,
            TValue? value) => default!;

        /// <summary><c>timeSeriesRateToGrid(start, end, step, staleness)(timestamp, value)</c>: the PromQL <c>rate</c> values on a regular grid, surfaced as <c>double?[]</c>. Experimental on 25.8 (the <c>allow_experimental_time_series_aggregate_functions</c> setting). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double?[] time_series_rate_to_grid<TTimestamp, TValue>(
            long startTimestamp,
            long endTimestamp,
            long gridStep,
            long staleness,
            TTimestamp? timestamp,
            TValue? value) => default!;

        /// <summary><c>timeSeriesResampleToGridWithStaleness(start, end, step, staleness)(timestamp, value)</c>: the time series resampled on a regular grid, surfaced as <c>double?[]</c>. Experimental on 25.8 (the <c>allow_experimental_time_series_aggregate_functions</c> setting). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public double?[] time_series_resample_to_grid_with_staleness<TTimestamp, TValue>(
            long startTimestamp,
            long endTimestamp,
            long gridStep,
            long staleness,
            TTimestamp? timestamp,
            TValue? value) => default!;

        /// <summary>
        /// <c>exponentialTimeDecayedSum(decay)(value, time)</c>: the exponentially smoothed weighted
        /// sum at the last point in time. The vendor accepts it only as a window function (a bare
        /// aggregate call is rejected with <c>BAD_ARGUMENTS</c>, pinned live), so the member returns
        /// <see cref="WindowFunction{T}"/> and must be completed with <c>Over(...)</c>. Requires
        /// <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).
        /// </summary>
        public WindowFunction<double> exponential_time_decayed_sum<TValue>(long decay, TValue value, DateTime time) where TValue : struct => default!;

        /// <summary><c>exponentialTimeDecayedMax(decay)(value, time)</c>: the exponentially smoothed maximum at the last point in time, window-only (must be completed with <c>Over(...)</c>). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public WindowFunction<double> exponential_time_decayed_max<TValue>(long decay, TValue value, DateTime time) where TValue : struct => default!;

        /// <summary><c>exponentialTimeDecayedCount(decay)(time)</c>: the cumulative exponential decay at the last point in time, window-only (must be completed with <c>Over(...)</c>). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public WindowFunction<double> exponential_time_decayed_count(long decay, DateTime time) => default!;

        /// <summary><c>exponentialTimeDecayedAvg(decay)(value, time)</c>: the exponentially smoothed weighted average at the last point in time, window-only (must be completed with <c>Over(...)</c>). Requires <see cref="ISqlDialect.ClickHouseAggregates"/> (ClickHouse).</summary>
        public WindowFunction<double> exponential_time_decayed_avg<TValue>(long decay, TValue value, DateTime time) where TValue : struct => default!;
    }
