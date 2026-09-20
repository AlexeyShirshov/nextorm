using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// ClickHouse-only SQL surface: the <c>argMin</c>/<c>argMax</c> aggregates, the distinct-count
/// <c>uniq</c>/<c>uniqExact</c>/<c>uniqCombined</c>/<c>uniqHLL12</c> aggregates, the parameterised
/// <c>quantile(level)(value)</c> family with <c>median</c>, the <c>anyLast</c> row-picking
/// aggregate, the string-JSON <c>JSONExtract*</c>/<c>JSONHas</c> and <c>visitParamExtract*</c> families
/// plus the JSONPath <c>json_value</c>/<c>json_query</c>/<c>json_exists</c> scalars,
/// the dictionary functions, the <c>-If</c> aggregate combinator, the distributed <c>global_in</c>
/// predicate, the <c>numbers</c>/<c>numbers_mt</c> and <c>zeros</c>/<c>zeros_mt</c> table functions,
/// and the array functions over array columns (<c>arrayJoin</c>, <c>length</c>, <c>has</c>,
/// <c>indexOf</c>, <c>hasAny</c>/<c>hasAll</c>, <c>arrayStringConcat</c>, <c>splitByChar</c>,
/// <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>).
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
        /// supports the distinct-count family (see <see cref="ISqlDialect.SupportsUniqAggregates"/>; ClickHouse).
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
        /// <see cref="ISqlDialect.SupportsQuantileAggregates"/>; ClickHouse).
        /// </summary>
        public double? quantile<T>(double level, T? value) => default!;

        /// <summary><c>quantileExact(level)(value)</c>: the exact <paramref name="level"/> quantile.</summary>
        public double? quantile_exact<T>(double level, T? value) => default!;

        /// <summary><c>quantileTiming(level)(value)</c>: the approximate quantile optimised for timing data.</summary>
        public double? quantile_timing<T>(double level, T? value) => default!;

        /// <summary><c>median(value)</c>: the median (the <c>0.5</c> quantile).</summary>
        public double? median<T>(T? value) => default!;

        /// <summary>
        /// <c>anyLast(value)</c>: the <paramref name="value"/> of an arbitrary last row. Requires a
        /// provider that supports it (see <see cref="ISqlDialect.SupportsAnyAggregates"/>; ClickHouse).
        /// The arbitrary-value aggregate without the last-row restriction is the cross-provider
        /// <see cref="CommonFunctions.any_agg{T}"/>.
        /// </summary>
        public T? any_last<T>(T? value) => default!;

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

        /// <summary><c>splitByChar(separator, value)</c>: splits the string by a single-character separator. Returns an array, so it can only be used as the operand of another array function.</summary>
        public string[] split_by_char(string? separator, string? value) => default!;

        /// <summary><c>arraySort(array)</c>: the elements in ascending order. Returns an array, so it can only be used as the operand of another array function.</summary>
        public T[] array_sort<T>(T[] array) => default!;

        /// <summary><c>arrayReverse(array)</c>: the elements in reverse order. Returns an array, so it can only be used as the operand of another array function.</summary>
        public T[] array_reverse<T>(T[] array) => default!;

        /// <summary><c>arrayDistinct(array)</c>: the distinct elements. Returns an array, so it can only be used as the operand of another array function.</summary>
        public T[] array_distinct<T>(T[] array) => default!;
    }
