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
/// <c>JSONAllPaths</c>/<c>JSONAllPathsWithTypes</c>/<c>toJSONString</c> functions,
/// the dictionary functions, the <c>-If</c> aggregate combinator, the distributed <c>global_in</c>
/// predicate, the <c>numbers</c>/<c>numbers_mt</c> and <c>zeros</c>/<c>zeros_mt</c> table functions
/// (plus <c>generateRandom</c>) and the server/cluster table functions
/// (<c>url</c>/<c>s3</c>/<c>file</c>/<c>remote</c>/<c>remoteSecure</c>/<c>cluster</c>/<c>clusterAllReplicas</c>),
/// the date conversion/part surface (<c>toDate</c>/<c>toDateTime</c>/<c>toDate32</c>, the
/// <c>toYear</c>/... accessors, <c>toStartOf*</c>, <c>toMonday</c>, <c>toYYYYMM</c>/<c>toYYYYMMDD</c>,
/// <c>toUnixTimestamp</c>),
/// and the array functions over array columns and expressions (<c>arrayJoin</c>, <c>length</c>,
/// <c>has</c>, <c>indexOf</c>, <c>hasAny</c>/<c>hasAll</c>, <c>arrayStringConcat</c>,
/// <c>splitByChar</c>, <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>, <c>range</c>,
/// <c>arrayEnumerate</c>, <c>arrayCumSum</c>, <c>arraySlice</c>, <c>arrayPushBack</c>), the
/// higher-order (lambda) array functions (<c>arrayMap</c>, <c>arrayFilter</c>, <c>arrayExists</c>,
/// <c>arrayAll</c>, <c>arrayCount</c>, <c>arrayFirst*</c>, <c>arrayLast*</c>) plus the
/// array-returning aggregates <c>groupArray</c>/<c>groupUniqArray</c>.
/// Exposed through <see cref="SqlFunctions.ClickHouse"/>; every member is gated by a capability flag
/// and rejected by providers that do not opt in.
/// </summary>
    public class ClickHouseFunctions : CommonFunctions
    {
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
        /// <c>arrayFilter(predicate, array)</c>: the elements for which the lambda returns true.
        /// Requires a provider that supports the higher-order array functions (see
        /// <see cref="ISqlDialect.SupportsHigherOrderArrayFunctions"/>; ClickHouse).
        /// </summary>
        public T[] array_filter<T>(Expression<Func<T, bool>> predicate, T[] array) => default!;

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
    }
