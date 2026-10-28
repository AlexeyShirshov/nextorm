namespace NextORM.Core;

/// <summary>
/// MySQL/MariaDB-only SQL surface: the native string/conditional idioms (<c>find_in_set</c>,
/// <c>field</c>, <c>elt</c>, <c>substring_index</c>, <c>format</c>), the <c>%</c>-templated date
/// conversion and Unix-epoch functions, the hexadecimal hash functions, the IPv4 conversion pair, the
/// JSON mutation family, the binary UUID pair and the MariaDB-only names (extended regexp,
/// <c>nvl</c>/<c>nvl2</c>, the Oracle-compatible date/number conversions, <c>kdf</c>, <c>xxh3</c>/
/// <c>xxh32</c>, <c>json_detailed</c>/<c>json_compact</c>, the JSON canonicalisation pair
/// <c>json_equals</c>/<c>json_normalize</c> and sequence access). It also carries the MySQL 8.4
/// JSON constructor/query catalogue and the <c>-&gt;</c>/<c>-&gt;&gt;</c>/<c>MEMBER OF</c> operators
/// (with the <c>JSON_TABLE</c> shape refusal), the
/// string, numeric and bit catalogue: <c>bin</c>, <c>export_set</c>, <c>hex</c>, <c>make_set</c>,
/// <c>oct</c>, <c>ord</c>, <c>quote</c>, <c>soundex</c>, <c>strcmp</c>, the Base64/hex codecs,
/// <c>weight_string</c>, <c>conv</c>, <c>crc32</c>, <c>log2</c>, <c>rand</c>, <c>truncate</c>,
/// <c>bit_count</c> and the <c>bit_and</c>/<c>bit_or</c>/<c>bit_xor</c> aggregates, and the
/// <c>date</c>/<c>time</c> catalogue (<c>adddate</c>/<c>subdate</c>, <c>addtime</c>/<c>subtime</c>,
/// <c>convert_tz</c>, <c>get_format</c>, <c>time_to_sec</c> and the remaining temporal accessors).
/// <para>
/// Exposed through <see cref="SqlFunctions.MySql"/>; every member is gated per name by the dialect's
/// <see cref="IMySqlFunctions"/> capability object and rejected by providers that do not opt in.
/// MariaDB supports most of this surface through its composed MySQL renderer, excludes the names it
/// does not implement and adds the MariaDB-only names; MySQL rejects those names with a clear message.
/// </para>
/// </summary>
public class MySqlFunctions : CommonFunctions
{
    /// <summary>
    /// <c>find_in_set(value, set)</c>: the 1-based position of <paramref name="value"/> in the
    /// comma-separated <paramref name="set"/>, or <c>0</c> when it is absent (and <c>null</c> when
    /// either argument is null).
    /// </summary>
    public int? find_in_set(string? value, string? set) => default!;

    /// <summary>
    /// <c>field(value, values...)</c>: the 1-based index of <paramref name="value"/> in the
    /// <paramref name="values"/> list, or <c>0</c> when it is absent.
    /// </summary>
    public int? field<T>(T? value, params T?[] values) => default!;

    /// <summary>
    /// <c>elt(index, values...)</c>: the <paramref name="index"/>-th (1-based) element of the
    /// <paramref name="values"/> list, or <c>null</c> when the index is out of range.
    /// </summary>
    public T? elt<T>(int index, params T?[] values) => default!;

    /// <summary>
    /// <c>substring_index(value, delimiter, count)</c>: the substring before the
    /// <paramref name="count"/>-th occurrence of <paramref name="delimiter"/> (counting from the left
    /// when <paramref name="count"/> is positive, from the right when it is negative).
    /// </summary>
    public string? substring_index(string? value, string? delimiter, int count) => default!;

    /// <summary>
    /// <c>format(value, decimals)</c>: formats the number with a thousands separator and
    /// <paramref name="decimals"/> fractional digits (returns a string).
    /// </summary>
    public string? format(decimal? value, int decimals) => default!;

    /// <summary>
    /// <c>str_to_date(value, format)</c>: parses <paramref name="value"/> using a MySQL
    /// <c>%</c>-style <paramref name="format"/> string.
    /// </summary>
    public DateTime? str_to_date(string? value, string? format) => default!;

    /// <summary>
    /// <c>date_format(value, format)</c>: formats the date/time using a MySQL <c>%</c>-style
    /// <paramref name="format"/> string.
    /// </summary>
    public string? date_format(DateTime? value, string? format) => default!;

    /// <summary>
    /// <c>from_unixtime(unixTimestamp)</c>: converts a Unix epoch (seconds) to a date/time in the
    /// session time zone.
    /// </summary>
    public DateTime? from_unixtime(long? unixTimestamp) => default!;

    /// <summary>
    /// <c>unix_timestamp(value)</c>: converts the date/time to Unix epoch seconds (or the current
    /// epoch when called without an argument).
    /// </summary>
    public long? unix_timestamp(DateTime? value) => default!;

    /// <summary><c>md5(value)</c>: the MD5 hash of <paramref name="value"/> as a 32-character hexadecimal string.</summary>
    public string? md5(string? value) => default!;

    /// <summary><c>sha1(value)</c>: the SHA-1 hash of <paramref name="value"/> as a 40-character hexadecimal string.</summary>
    public string? sha1(string? value) => default!;

    /// <summary>
    /// <c>sha2(value, hashLength)</c>: the SHA-2 hash of <paramref name="value"/> with the digest length
    /// <paramref name="hashLength"/> (224, 256, 384 or 512 bits).
    /// </summary>
    public string? sha2(string? value, int hashLength) => default!;

    /// <summary>
    /// <c>inet_aton(value)</c>: the numeric (network-order) representation of the dotted-quad IPv4
    /// address <paramref name="value"/>.
    /// </summary>
    public long? inet_aton(string? value) => default!;

    /// <summary>
    /// <c>inet_ntoa(value)</c>: the dotted-quad IPv4 address for the numeric
    /// <paramref name="value"/>.
    /// </summary>
    public string? inet_ntoa(long? value) => default!;

    /// <summary>
    /// <c>json_array(values...)</c>: a JSON array of the arguments (each argument is a SQL value, so
    /// strings become JSON strings). MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_array(params object?[] values) => default!;

    /// <summary>
    /// <c>json_arrayagg(value)</c>: aggregates the group's values into a single JSON array. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    public string? json_arrayagg(object? value) => default!;

    /// <summary><c>json_array_append(json, path, value)</c>: appends the value to the array at <paramref name="path"/>.</summary>
    public string? json_array_append(string? json, string? path, string? value) => default!;

    /// <summary><c>json_array_insert(json, path, value)</c>: inserts the value into the array at <paramref name="path"/> without replacing an existing element.</summary>
    public string? json_array_insert(string? json, string? path, string? value) => default!;

    /// <summary>
    /// <c>column -&gt; path</c>: the value at the JSON <paramref name="path"/> of the
    /// <paramref name="column"/>. MySQL requires a column identifier on the left and a string literal
    /// on the right, so <paramref name="column"/> must be a mapped column and <paramref name="path"/> a
    /// compile-time string; any other shape is refused before SQL is emitted. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    public string? json_arrow(string? column, string path) => default!;

    /// <summary>
    /// <c>column -&gt;&gt; path</c>: the unquoted value at the JSON <paramref name="path"/> of the
    /// <paramref name="column"/> (equivalent to <c>JSON_UNQUOTE(JSON_EXTRACT(column, path))</c>). The
    /// same column/literal restrictions as <see cref="json_arrow"/> apply. MySQL-only; MariaDB rejects
    /// the call.
    /// </summary>
    public string? json_arrow_text(string? column, string path) => default!;

    /// <summary>
    /// <c>json_contains(target, candidate)</c>: <c>1</c> when <paramref name="candidate"/> is contained
    /// in <paramref name="target"/>, otherwise <c>0</c>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? json_contains(string? target, string? candidate) => default!;

    /// <summary>
    /// <c>json_contains(target, candidate, path)</c>: <c>1</c> when <paramref name="candidate"/> is
    /// contained in the value at <paramref name="path"/> of <paramref name="target"/>, otherwise
    /// <c>0</c>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? json_contains(string? target, string? candidate, string? path) => default!;

    /// <summary>
    /// <c>json_contains_path(json, one_or_all, paths...)</c>: <c>1</c> when the document contains the
    /// paths, otherwise <c>0</c>. <paramref name="oneOrAll"/> must be the compile-time literal
    /// <c>"one"</c> or <c>"all"</c> and every path a compile-time string literal; any other shape is
    /// refused before SQL is emitted. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? json_contains_path(string? json, string oneOrAll, params string?[] paths) => default!;

    /// <summary><c>json_depth(json)</c>: the maximum nesting depth of the JSON document.</summary>
    public int? json_depth(string? json) => default!;

    /// <summary><c>json_insert(json, path, value)</c>: inserts the value at <paramref name="path"/>, keeping any existing value.</summary>
    public string? json_insert(string? json, string? path, string? value) => default!;

    /// <summary><c>json_keys(json)</c>: the top-level keys of the JSON object as a JSON array.</summary>
    public string? json_keys(string? json) => default!;

    /// <summary><c>json_length(json)</c>: the number of elements in the top-level JSON array or object.</summary>
    public int? json_length(string? json) => default!;

    /// <summary>
    /// <c>json_merge(first, second, rest...)</c>: merges the JSON documents, preserving duplicate keys.
    /// MySQL deprecates <c>JSON_MERGE()</c> in favour of <c>JSON_MERGE_PATCH()</c> and
    /// <c>JSON_MERGE_PRESERVE()</c>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_merge(string? first, string? second, params string?[] rest) => default!;

    /// <summary><c>json_merge_patch(json, other)</c>: merges two JSON documents using the RFC 7396 merge-patch algorithm.</summary>
    public string? json_merge_patch(string? json, string? other) => default!;

    /// <summary><c>json_merge_preserve(json, other)</c>: merges two JSON documents, preserving duplicate keys.</summary>
    public string? json_merge_preserve(string? json, string? other) => default!;

    /// <summary>
    /// <c>json_object(keyValues...)</c>: a JSON object built from the alternating key/value arguments;
    /// the key positions must be strings and the argument count even, which is validated before any SQL
    /// is emitted. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_object(params object?[] keyValues) => default!;

    /// <summary>
    /// <c>json_objectagg(key, value)</c>: aggregates the group's key/value pairs into a single JSON
    /// object. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_objectagg(string? key, object? value) => default!;

    /// <summary>
    /// <c>json_overlaps(left, right)</c>: <c>1</c> when the two JSON documents have at least one value
    /// in common, otherwise <c>0</c>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? json_overlaps(string? left, string? right) => default!;

    /// <summary>
    /// <c>json_pretty(json)</c>: the JSON document re-indented for readability. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    public string? json_pretty(string? json) => default!;

    /// <summary>
    /// <c>json_quote(value)</c>: the argument as a JSON string literal. MySQL-only; MariaDB rejects the
    /// call.
    /// </summary>
    public string? json_quote(string? value) => default!;

    /// <summary><c>json_remove(json, path)</c>: removes the value at <paramref name="path"/>.</summary>
    public string? json_remove(string? json, string? path) => default!;

    /// <summary><c>json_replace(json, path, value)</c>: replaces an existing value at <paramref name="path"/>.</summary>
    public string? json_replace(string? json, string? path, string? value) => default!;

    /// <summary>
    /// <c>json_schema_valid(schema, document)</c>: <c>1</c> when <paramref name="document"/> validates
    /// against the JSON <paramref name="schema"/>, otherwise <c>0</c>. MySQL-only; MariaDB rejects the
    /// call.
    /// </summary>
    public long? json_schema_valid(string? schema, string? document) => default!;

    /// <summary>
    /// <c>json_schema_validation_report(schema, document)</c>: a JSON report of the validation of
    /// <paramref name="document"/> against the JSON <paramref name="schema"/>. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    public string? json_schema_validation_report(string? schema, string? document) => default!;

    /// <summary>
    /// <c>json_search(json, one_or_all, search)</c>: the path or paths whose string value contains
    /// <paramref name="search"/>, or <c>null</c> when none does. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_search(string? json, string oneOrAll, string? search) => default!;

    /// <summary>
    /// <c>json_search(json, one_or_all, search, escape)</c>: as
    /// <see cref="json_search(string?, string, string?)"/> with the escape character; the escape must be
    /// a compile-time single-character literal. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_search(string? json, string oneOrAll, string? search, string? escape) => default!;

    /// <summary>
    /// <c>json_search(json, one_or_all, search, escape, paths...)</c>: as above restricted to the given
    /// paths. <paramref name="oneOrAll"/> must be the compile-time literal <c>"one"</c> or <c>"all"</c>
    /// and every path a compile-time string literal; any other shape is refused before SQL is emitted.
    /// MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? json_search(
        string? json, string oneOrAll, string? search, string? escape, params string?[] paths) => default!;

    /// <summary><c>json_set(json, path, value)</c>: inserts or updates the value at <paramref name="path"/>.</summary>
    public string? json_set(string? json, string? path, string? value) => default!;

    /// <summary>
    /// <c>json_storage_free(json)</c>: the bytes freed in the binary representation of a MySQL
    /// <c>JSON</c> column value after it was updated in place by <c>JSON_SET</c>, <c>JSON_REPLACE</c> or
    /// <c>JSON_REMOVE</c>. Returns <c>0</c> for a literal, a captured value or a column whose update
    /// freed nothing, and <c>null</c> when <paramref name="json"/> is SQL NULL; an argument that is not
    /// valid JSON fails on the server. Server-side only: the count describes MySQL's binary JSON
    /// storage, not a portable JSON-text length, and providers that cannot express the operation reject
    /// the call with <see cref="NotSupportedException"/>.
    /// </summary>
    public long? json_storage_free(string? json) => default!;

    /// <summary>
    /// <c>json_storage_size(json)</c>: the bytes used to store the JSON document (the length of its
    /// binary representation for a column). MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? json_storage_size(string? json) => default!;

    /// <summary><c>json_type(json)</c>: the type name of the top-level JSON value.</summary>
    public string? json_type(string? json) => default!;

    /// <summary>
    /// <c>JSON_TABLE(...)</c> is a table-producing construct, not a scalar function: it requires a
    /// <c>FROM</c>/join position and a <c>COLUMNS</c> definition list, so no scalar expression can
    /// reference it. The member exists only to carry the refusal; calling it throws
    /// <see cref="NotSupportedException"/> in the body as well as when the expression is translated,
    /// so a projection the planner folds client-side cannot silently evaluate to <c>null</c>.
    /// </summary>
    public static object? json_table() => throw new NotSupportedException(JsonTableRefusal);

    /// <summary>
    /// <c>value MEMBER OF (jsonArray)</c>: <c>1</c> when <paramref name="value"/> is an element of the
    /// JSON array literal <paramref name="jsonArray"/>, otherwise <c>0</c>. <paramref name="jsonArray"/>
    /// must be a compile-time string literal; any other shape is refused before SQL is emitted.
    /// MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? member_of(object? value, string jsonArray) => default!;

    /// <summary>
    /// <c>uuid_to_bin(uuid)</c>: converts the textual UUID to its 16-byte binary representation.
    /// </summary>
    public byte[]? uuid_to_bin(string? uuid) => default!;

    /// <summary>
    /// <c>bin_to_uuid(binary)</c>: converts the 16-byte binary UUID to its textual representation.
    /// </summary>
    public string? bin_to_uuid(byte[]? binary) => default!;

    /// <summary>
    /// <c>regexp_substr(subject, pattern)</c>: the part of <paramref name="value"/> matching the regular
    /// expression, or an empty string when it does not match. MariaDB-only (MySQL has no
    /// <c>REGEXP_SUBSTR</c>).
    /// </summary>
    public string? regexp_substr(string? value, string? pattern) => default!;

    /// <summary>
    /// <c>regexp_instr(subject, pattern)</c>: the 1-based position of the regular-expression match in
    /// <paramref name="value"/>, or <c>0</c> when there is none. MariaDB-only.
    /// </summary>
    public int? regexp_instr(string? value, string? pattern) => default!;

    /// <summary>
    /// <c>regexp_replace(subject, pattern, replace)</c>: <paramref name="value"/> with every
    /// regular-expression match replaced. MariaDB-only.
    /// </summary>
    public string? regexp_replace(string? value, string? pattern, string? replacement) => default!;

    /// <summary>
    /// <c>nvl(value, fallback)</c>: <paramref name="value"/> when it is not null, otherwise
    /// <paramref name="fallback"/> (a synonym of <c>IFNULL</c>). MariaDB-only.
    /// </summary>
    public T? nvl<T>(T? value, T? fallback) => default!;

    /// <summary>
    /// <c>nvl2(value, whenNotNull, whenNull)</c>: <paramref name="whenNotNull"/> when
    /// <paramref name="value"/> is not null, otherwise <paramref name="whenNull"/>. MariaDB-only.
    /// </summary>
    public T? nvl2<T>(T? value, T? whenNotNull, T? whenNull) => default!;

    /// <summary>
    /// <c>add_months(date, months)</c>: <paramref name="date"/> shifted by <paramref name="months"/>,
    /// clamped to the last day of a shorter month. MariaDB-only (MariaDB 10.6.1+).
    /// </summary>
    public DateTime? add_months(DateTime? date, int months) => default!;

    /// <summary>
    /// <c>months_between(a, b)</c>: the (fractional) number of months between two dates, negative when
    /// <paramref name="a"/> is earlier. MariaDB-only (MariaDB 12.2.1+): an unversioned or older MariaDB
    /// dialect rejects it with <see cref="NotSupportedException"/> instead of substituting
    /// <c>timestampdiff</c> (integer truncation, no fractional months).
    /// </summary>
    public double? months_between(DateTime? a, DateTime? b) => default!;

    /// <summary>
    /// <c>to_char(value, format)</c>: formats the date/time using an Oracle-style format string.
    /// MariaDB-only (MariaDB 10.6+).
    /// </summary>
    public string? to_char(DateTime? value, string? format) => default!;

    /// <summary>
    /// <c>to_date(value, format)</c>: parses a date/time from an Oracle-style format string.
    /// MariaDB-only (MariaDB 12.3+): an unversioned or older MariaDB dialect rejects it with
    /// <see cref="NotSupportedException"/> instead of substituting <c>str_to_date</c> (<c>%</c> masks,
    /// not Oracle masks).
    /// </summary>
    public DateTime? to_date(string? value, string? format) => default!;

    /// <summary>
    /// <c>to_number(value, format)</c>: parses a number from an Oracle-style format string (the result
    /// is a <c>DOUBLE</c>). MariaDB-only (MariaDB 12.2 family; the enforced minimum is 12.2.1): an
    /// unversioned or older MariaDB dialect rejects it with <see cref="NotSupportedException"/> instead
    /// of substituting a decimal cast, which has no Oracle format model.
    /// </summary>
    public double? to_number(string? value, string? format) => default!;

    /// <summary>
    /// <c>kdf(password, salt, info, kdf_name)</c>: derives a key with the named key-derivation function.
    /// MariaDB-only (MariaDB 11.3+).
    /// </summary>
    public byte[]? kdf(string? password, string? salt, string? info, string? kdfName) => default!;

    /// <summary>
    /// <c>xxh3(value)</c>: the fast 64-bit xxHash of a text value. MariaDB-only (MariaDB 13.1+).
    /// </summary>
    public ulong? xxh3(string? value) => default!;

    /// <summary>
    /// <c>xxh32(value)</c>: the fast 32-bit xxHash of a text value, widened to a <see cref="long"/>
    /// (the unsigned 32-bit result always fits). MariaDB-only (MariaDB 13.1+).
    /// </summary>
    public long? xxh32(string? value) => default!;

    /// <summary>
    /// <c>json_detailed(json)</c>: the JSON document re-indented for readability. MariaDB-only.
    /// </summary>
    public string? json_detailed(string? json) => default!;

    /// <summary>
    /// <c>json_compact(json)</c>: the JSON document with all unnecessary whitespace removed.
    /// MariaDB-only.
    /// </summary>
    public string? json_compact(string? json) => default!;

    /// <summary>
    /// <c>json_equals(json1, json2)</c>: <c>1</c> when the two JSON documents are equal (object key
    /// order does not matter), <c>0</c> when they differ, and <c>null</c> when either argument is SQL
    /// NULL. MariaDB-only.
    /// </summary>
    public int? json_equals(string? json1, string? json2) => default!;

    /// <summary>
    /// <c>json_normalize(json)</c>: the canonical text of the JSON document with sorted object keys and
    /// no unnecessary whitespace (idempotent), or <c>null</c> when <paramref name="json"/> is SQL NULL.
    /// MariaDB-only.
    /// </summary>
    public string? json_normalize(string? json) => default!;

    /// <summary>
    /// <c>next value for sequence</c>: the next value of the named sequence. MariaDB-only; the
    /// <paramref name="sequence"/> name is emitted as an identifier.
    /// </summary>
    public long? next_value_for(string? sequence) => default!;

    /// <summary>
    /// <c>nextval(sequence)</c>: the next value of the named sequence. MariaDB-only; the
    /// <paramref name="sequence"/> name is emitted as an identifier.
    /// </summary>
    public long? nextval(string? sequence) => default!;

    /// <summary>
    /// <c>setval(sequence, value)</c>: sets the next value to be returned for the named sequence.
    /// MariaDB-only; the <paramref name="sequence"/> name is emitted as an identifier.
    /// </summary>
    public long? setval(string? sequence, long value) => default!;

    /// <summary>
    /// <c>lastval(sequence)</c>: the last value generated by the named sequence for the current
    /// connection. MariaDB-only; the <paramref name="sequence"/> name is emitted as an identifier.
    /// </summary>
    public long? lastval(string? sequence) => default!;

    /// <summary>
    /// <c>previous value for sequence</c>: the value the current connection's last
    /// <c>NEXT VALUE FOR</c> produced for the named sequence. MariaDB-only; the
    /// <paramref name="sequence"/> name is emitted as an identifier.
    /// </summary>
    public long? previous_value_for(string? sequence) => default!;

    /// <summary>
    /// <c>ROWNUM()</c>: the 1-based position of the row in the result. MariaDB-only, and only the
    /// zero-argument function form — the bare <c>ROWNUM</c> pseudo-column needs <c>sql_mode=ORACLE</c> on
    /// the verified 11.4 baseline and is not exposed.
    /// </summary>
    public ulong? rownum() => default!;

    /// <summary>
    /// <c>binlog_gtid_pos(file, position)</c>: the GTID position of a binary-log file/offset pair, as
    /// <c>domain-server-sequence</c> text. MariaDB-only; the server reports error 1381 when binary
    /// logging is off.
    /// </summary>
    /// <param name="file">The binary-log file name.</param>
    /// <param name="position">The offset in that file.</param>
    public string? binlog_gtid_pos(string? file, ulong position) => default!;

    /// <summary>
    /// <c>sformat(format, values...)</c>: the arguments formatted with an fmtlib-style <c>{}</c> format
    /// string. MariaDB-only.
    /// </summary>
    public string? sformat(string? format, params object?[] values) => default!;

    /// <summary>
    /// <c>trim_oracle(value)</c>: Oracle-compatible <c>TRIM</c>, which removes the spaces both leading and
    /// trailing. MariaDB-only.
    /// </summary>
    public string? trim_oracle(string? value) => default!;

    /// <summary>
    /// <c>natural_sort_key(value)</c>: a sort key that orders embedded digit runs numerically rather than
    /// lexically. MariaDB-only.
    /// </summary>
    public string? natural_sort_key(string? value) => default!;

    /// <summary>
    /// <c>lengthb(value)</c>: the length of <paramref name="value"/> in bytes. MariaDB-only.
    /// </summary>
    public int? lengthb(string? value) => default!;

    /// <summary>
    /// <c>chr(code)</c>: the character with the given code point. MariaDB-only (MySQL spells the same
    /// conversion <c>CHAR()</c>).
    /// </summary>
    public string? chr(int? code) => default!;

    /// <summary>
    /// <c>json_loose(json)</c>: the JSON document re-formatted with readable whitespace but without the
    /// strict validity check. MariaDB-only.
    /// </summary>
    public string? json_loose(string? json) => default!;

    /// <summary>
    /// <c>json_array_intersect(json, json, ...)</c>: the array of elements common to every argument array.
    /// MariaDB-only (MariaDB 11.2+).
    /// </summary>
    public string? json_array_intersect(string? first, string? second, params string?[] rest) => default!;

    /// <summary>
    /// <c>json_key_value(json, path)</c>: the key/value pairs of the object selected by the JSON
    /// <paramref name="path"/>, as a JSON array. MariaDB-only (MariaDB 11.2+).
    /// </summary>
    public string? json_key_value(string? json, string? path) => default!;

    /// <summary>
    /// <c>json_object_filter_keys(json, keys...)</c>: the object reduced to the named keys, or an empty
    /// object when none is present. MariaDB-only (MariaDB 11.2+).
    /// </summary>
    public byte[]? json_object_filter_keys(string? json, params string?[] keys) => default!;

    /// <summary>
    /// <c>json_object_to_array(json)</c>: the object as an array of <c>[key, value]</c> pairs.
    /// MariaDB-only (MariaDB 11.2+).
    /// </summary>
    public byte[]? json_object_to_array(string? json) => default!;

    /// <summary>
    /// <c>encode(value, password)</c>: the symmetric ciphertext of <paramref name="value"/> under
    /// <paramref name="password"/>; the historical pair of <c>decode</c>. MariaDB-only.
    /// </summary>
    public byte[]? encode(string? value, string? password) => default!;

    /// <summary>
    /// <c>decode(value, password)</c>: the plaintext of the <paramref name="value"/> ciphertext under
    /// <paramref name="password"/>; the pair of <c>encode</c>. MariaDB-only.
    /// </summary>
    public byte[]? decode(byte[]? value, string? password) => default!;

    /// <summary>
    /// <c>encrypt(value)</c>: the Unix <c>crypt()</c> hash of <paramref name="value"/> with a random
    /// two-character salt. MariaDB-only.
    /// </summary>
    public byte[]? encrypt(string? value) => default!;

    /// <summary>
    /// <c>encrypt(value, salt)</c>: the Unix <c>crypt()</c> hash of <paramref name="value"/> with the
    /// two-character <paramref name="salt"/>, which makes the result deterministic. MariaDB-only.
    /// </summary>
    public byte[]? encrypt(string? value, string? salt) => default!;

    /// <summary>
    /// <c>des_encrypt(value)</c>: the DES ciphertext under the first key of the server's DES key file.
    /// MariaDB-only; removed in MariaDB 13.0.
    /// </summary>
    public byte[]? des_encrypt(string? value) => default!;

    /// <summary>
    /// <c>des_encrypt(value, key)</c>: the DES ciphertext under the literal <paramref name="key"/>.
    /// MariaDB-only; removed in MariaDB 13.0.
    /// </summary>
    public byte[]? des_encrypt(string? value, string? key) => default!;

    /// <summary>
    /// <c>des_decrypt(value)</c>: the plaintext of the <paramref name="value"/> DES ciphertext under the
    /// first key of the server's DES key file. MariaDB-only; removed in MariaDB 13.0.
    /// </summary>
    public byte[]? des_decrypt(byte[]? value) => default!;

    /// <summary>
    /// <c>des_decrypt(value, key)</c>: the plaintext of the <paramref name="value"/> DES ciphertext under
    /// the literal <paramref name="key"/>. MariaDB-only; removed in MariaDB 13.0.
    /// </summary>
    public byte[]? des_decrypt(byte[]? value, string? key) => default!;

    /// <summary>
    /// <c>old_password(value)</c>: the pre-4.1 (16-character) MySQL password hash. MariaDB-only.
    /// </summary>
    public string? old_password(string? value) => default!;

    /// <summary>
    /// <c>bin(value)</c>: the binary representation of <paramref name="value"/> as an unsigned 64-bit
    /// two's-complement value, without a <c>0b</c> prefix. MySQL-only.
    /// </summary>
    public string? bin(ulong? value) => default!;

    /// <summary>
    /// <c>export_set(bits, on, off)</c>: builds a string by reading the bits of <paramref name="bits"/>
    /// from the least significant upward, appending <paramref name="on"/> for a set bit and
    /// <paramref name="off"/> for a clear one, joined by a comma for 64 bits. MySQL-only; the
    /// separator and count are supplied by the longer overloads.
    /// </summary>
    public string? export_set(ulong? bits, string? on, string? off) => default!;

    /// <summary>
    /// <c>export_set(bits, on, off, separator)</c>: the same bit-string construction with an explicit
    /// separator and the default 64-bit count. MySQL-only.
    /// </summary>
    public string? export_set(ulong? bits, string? on, string? off, string? separator) => default!;

    /// <summary>
    /// <c>export_set(bits, on, off, separator, count)</c>: the same bit-string construction for
    /// <paramref name="count"/> bits. MySQL-only; a constant <paramref name="count"/> outside
    /// <c>[0, 64]</c> is refused before SQL is generated.
    /// </summary>
    public string? export_set(ulong? bits, string? on, string? off, string? separator, int? count) => default!;

    /// <summary><c>hex(value)</c>: the hexadecimal representation of the binary value. MySQL-only.</summary>
    public string? hex(byte[]? value) => default!;

    /// <summary><c>hex(value)</c>: the hexadecimal representation of the text's bytes. MySQL-only.</summary>
    public string? hex(string? value) => default!;

    /// <summary><c>hex(value)</c>: the 16-digit hexadecimal representation of a 64-bit integer. MySQL-only.</summary>
    public string? hex(ulong? value) => default!;

    /// <summary>
    /// <c>make_set(bits, values...)</c>: a comma-separated list of the values whose 1-based position
    /// corresponds to a set bit of <paramref name="bits"/> (the first value for bit 0), or an empty
    /// string when no bit is set (<c>null</c> when <paramref name="bits"/> is SQL NULL). MySQL-only.
    /// </summary>
    public string? make_set(ulong? bits, params string?[] values) => default!;

    /// <summary>
    /// <c>oct(value)</c>: the octal representation of <paramref name="value"/> as an unsigned 64-bit
    /// value. MySQL-only.
    /// </summary>
    public string? oct(ulong? value) => default!;

    /// <summary>
    /// <c>ord(value)</c>: the numeric code of the leftmost character of <paramref name="value"/>, or
    /// <c>0</c> for an empty string and <c>null</c> for SQL NULL. A multi-byte character yields the
    /// value MySQL computes from its bytes. MySQL-only.
    /// </summary>
    public long? ord(string? value) => default!;

    /// <summary>
    /// <c>quote(value)</c>: <paramref name="value"/> as a single-quoted SQL literal with embedded
    /// quotes and backslashes escaped, or the four-character word <c>NULL</c> when it is SQL NULL.
    /// MySQL-only.
    /// </summary>
    public string? quote(string? value) => default!;

    /// <summary>
    /// <c>soundex(value)</c>: the Soundex code of <paramref name="value"/> (a four-character string such
    /// as <c>R163</c>), or an empty string when the value contains no alphabetic characters. MySQL-only.
    /// </summary>
    public string? soundex(string? value) => default!;

    /// <summary>
    /// <c>strcmp(left, right)</c>: <c>-1</c>, <c>0</c> or <c>1</c> as <paramref name="left"/> sorts
    /// before, equal to or after <paramref name="right"/> in the collation, or <c>null</c> when either
    /// argument is SQL NULL. MySQL-only.
    /// </summary>
    public long? strcmp(string? left, string? right) => default!;

    /// <summary>
    /// <c>to_base64(value)</c>: the Base64 encoding of the binary value, wrapped at 76 characters per
    /// line. MySQL-only.
    /// </summary>
    public string? to_base64(byte[]? value) => default!;

    /// <summary>
    /// <c>from_base64(value)</c>: the binary value decoded from the Base64 text, or <c>null</c> when the
    /// text is not valid Base64. MySQL-only.
    /// </summary>
    public byte[]? from_base64(string? value) => default!;

    /// <summary>
    /// <c>unhex(value)</c>: the binary value decoded from a pair of hexadecimal digits per byte, or
    /// <c>null</c> when <paramref name="value"/> has an odd length or a non-hexadecimal character.
    /// MySQL-only.
    /// </summary>
    public byte[]? unhex(string? value) => default!;

    /// <summary>
    /// <c>weight_string(value)</c>: the weight of <paramref name="value"/> as a binary string. MySQL-only;
    /// MariaDB has no <c>WEIGHT_STRING</c> and rejects the call.
    /// </summary>
    public byte[]? weight_string(string? value) => default!;

    /// <summary>
    /// <c>weight_string(value as {char|binary}(length))</c>: the weight of <paramref name="value"/> after
    /// padding it (or truncating it) to <paramref name="length"/> characters with spaces
    /// (<see cref="MySqlWeightStringCast.Char"/>) or bytes with <c>0x00</c>
    /// (<see cref="MySqlWeightStringCast.Binary"/>). Both modifiers must be compile-time constants.
    /// MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="value">The string whose weight is computed.</param>
    /// <param name="cast">The padding/truncation unit.</param>
    /// <param name="length">The number of characters or bytes; at least <c>1</c>.</param>
    public byte[]? weight_string(string? value, MySqlWeightStringCast cast, int length) => default!;

    /// <summary>
    /// <c>conv(value, fromBase, toBase)</c>: <paramref name="value"/> re-expressed as a string in the
    /// target base (bases <c>2</c>–<c>36</c>, and <c>-1</c> for a signed interpretation). MySQL-only.
    /// </summary>
    public string? conv(string? value, int? fromBase, int? toBase) => default!;

    /// <summary>
    /// <c>crc32(value)</c>: the unsigned 32-bit CRC-32 checksum of the binary value, widened to
    /// <see cref="ulong"/> (the value always fits), or <c>null</c> for SQL NULL. MySQL-only.
    /// </summary>
    public ulong? crc32(byte[]? value) => default!;

    /// <summary><c>crc32(value)</c>: the unsigned 32-bit CRC-32 checksum of the text's bytes. MySQL-only.</summary>
    public ulong? crc32(string? value) => default!;

    /// <summary><c>log2(value)</c>: the base-2 logarithm of <paramref name="value"/>. MySQL-only.</summary>
    public double? log2(double? value) => default!;

    /// <summary>
    /// <c>rand()</c>: a random <see cref="double"/> in <c>[0, 1)</c> for the current session. MySQL-only.
    /// </summary>
    public double? rand() => default!;

    /// <summary>
    /// <c>rand(seed)</c>: a random <see cref="double"/> in <c>[0, 1)</c> from the given
    /// <paramref name="seed"/>; the same seed reproduces the same sequence within a session. MySQL-only.
    /// </summary>
    public double? rand(long? seed) => default!;

    /// <summary>
    /// <c>truncate(value, places)</c>: <paramref name="value"/> truncated toward zero to
    /// <paramref name="places"/> decimal places (a negative count zeroes the integer part on the left).
    /// MySQL-only.
    /// </summary>
    public decimal? truncate(decimal? value, int? places) => default!;

    /// <summary><c>truncate(value, places)</c>: the same truncation for a floating-point value. MySQL-only.</summary>
    public double? truncate(double? value, int? places) => default!;

    /// <summary>
    /// <c>bit_count(value)</c>: the number of set bits in the 64-bit representation of
    /// <paramref name="value"/>. MySQL-only.
    /// </summary>
    public long? bit_count(ulong? value) => default!;

    /// <summary><c>bit_count(value)</c>: the number of set bits in the binary value. MySQL-only.</summary>
    public long? bit_count(byte[]? value) => default!;

    /// <summary>
    /// <c>bit_and(value)</c>: the bitwise AND of every non-null value in the group, or <c>null</c> when
    /// the group has no rows. MySQL-only.
    /// </summary>
    public ulong? bit_and(ulong? value) => default!;

    /// <summary><c>bit_and(value)</c>: the bitwise AND of every binary value in the group. MySQL-only.</summary>
    public byte[]? bit_and(byte[]? value) => default!;

    /// <summary>
    /// <c>bit_or(value)</c>: the bitwise OR of every non-null value in the group, or <c>null</c> when
    /// the group has no rows. MySQL-only.
    /// </summary>
    public ulong? bit_or(ulong? value) => default!;

    /// <summary><c>bit_or(value)</c>: the bitwise OR of every binary value in the group. MySQL-only.</summary>
    public byte[]? bit_or(byte[]? value) => default!;

    /// <summary>
    /// <c>bit_xor(value)</c>: the bitwise XOR of every non-null value in the group, or <c>null</c> when
    /// the group has no rows. MySQL-only.
    /// </summary>
    public ulong? bit_xor(ulong? value) => default!;

    /// <summary><c>bit_xor(value)</c>: the bitwise XOR of every binary value in the group. MySQL-only.</summary>
    public byte[]? bit_xor(byte[]? value) => default!;

    /// <summary>
    /// <c>adddate(date, days)</c>: <paramref name="date"/> plus <paramref name="days"/> calendar days.
    /// This is the numeric-days form only; the <c>INTERVAL</c> unit form is not modelled. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    public DateTime? adddate(DateTime? date, int? days) => default!;

    /// <summary>
    /// <c>addtime(datetime, time)</c>: <paramref name="datetime"/> plus the duration
    /// <paramref name="time"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public DateTime? addtime(DateTime? datetime, TimeSpan? time) => default!;

    /// <summary>
    /// <c>addtime(time1, time2)</c>: the sum of two durations. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public TimeSpan? addtime(TimeSpan? time1, TimeSpan? time2) => default!;

    /// <summary>
    /// <c>convert_tz(datetime, fromTimeZone, toTimeZone)</c>: <paramref name="datetime"/> converted from
    /// the <paramref name="fromTimeZone"/> to the <paramref name="toTimeZone"/> IANA zone, or
    /// <c>null</c> when either zone is unknown to the server. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public DateTime? convert_tz(DateTime? datetime, string? fromTimeZone, string? toTimeZone) => default!;

    /// <summary>
    /// <c>dayname(date)</c>: the weekday name of <paramref name="date"/> (for example
    /// <c>Wednesday</c>). MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? dayname(DateTime? date) => default!;

    /// <summary>
    /// <c>from_days(days)</c>: the date corresponding to the day number
    /// <paramref name="days"/> (days since year 0). MySQL-only; MariaDB rejects the call.
    /// </summary>
    public DateTime? from_days(long? days) => default!;

    /// <summary>
    /// <c>get_format(kind, format)</c>: the MySQL <c>%</c>-style format string for the platform format
    /// named <paramref name="format"/> (<c>USA</c>, <c>JIS</c>, <c>ISO</c>, <c>EUR</c> or
    /// <c>INTERNAL</c>) of the <paramref name="kind"/> family. Both arguments must be compile-time
    /// constants; an invalid pair is refused before SQL is generated. MySQL-only; MariaDB rejects the
    /// call.
    /// </summary>
    /// <param name="kind">The format family: <see cref="MySqlFormatKind.Date"/>,
    /// <see cref="MySqlFormatKind.Time"/> or <see cref="MySqlFormatKind.DateTime"/>.</param>
    /// <param name="format">The platform format name, as a compile-time literal.</param>
    public string? get_format(MySqlFormatKind kind, string? format) => default!;

    /// <summary>
    /// <c>makedate(year, dayOfYear)</c>: the date built from <paramref name="year"/> and the 1-based
    /// <paramref name="dayOfYear"/>, or <c>null</c> when the day is outside the year. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    public DateTime? makedate(int? year, int? dayOfYear) => default!;

    /// <summary>
    /// <c>maketime(hour, minute, second)</c>: the duration built from the parts, or <c>null</c> when a
    /// part is out of range. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public TimeSpan? maketime(int? hour, int? minute, decimal? second) => default!;

    /// <summary>
    /// <c>microsecond(datetime)</c>: the microseconds part of <paramref name="datetime"/>
    /// (<c>0</c>–<c>999999</c>). MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? microsecond(DateTime? datetime) => default!;

    /// <summary>
    /// <c>microsecond(time)</c>: the microseconds part of the duration. MySQL-only; MariaDB rejects
    /// the call.
    /// </summary>
    public long? microsecond(TimeSpan? time) => default!;

    /// <summary>
    /// <c>monthname(date)</c>: the month name of <paramref name="date"/> (for example <c>May</c>).
    /// MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? monthname(DateTime? date) => default!;

    /// <summary>
    /// <c>period_add(period, months)</c>: the <c>YYMM</c>/<c>YYYYMM</c> period
    /// <paramref name="period"/> shifted by <paramref name="months"/>. MySQL-only; MariaDB rejects the
    /// call.
    /// </summary>
    public long? period_add(long? period, long? months) => default!;

    /// <summary>
    /// <c>period_diff(period1, period2)</c>: the number of months from <paramref name="period2"/> to
    /// <paramref name="period1"/>, both in <c>YYMM</c>/<c>YYYYMM</c> form. MySQL-only; MariaDB rejects
    /// the call.
    /// </summary>
    public long? period_diff(long? period1, long? period2) => default!;

    /// <summary>
    /// <c>sec_to_time(seconds)</c>: the duration represented by <paramref name="seconds"/>, with a
    /// fractional part when the input has one. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public TimeSpan? sec_to_time(decimal? seconds) => default!;

    /// <summary>
    /// <c>subdate(date, days)</c>: <paramref name="date"/> minus <paramref name="days"/> calendar days.
    /// This is the numeric-days form only; the <c>INTERVAL</c> unit form is not modelled. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    public DateTime? subdate(DateTime? date, int? days) => default!;

    /// <summary>
    /// <c>subtime(datetime, time)</c>: <paramref name="datetime"/> minus the duration
    /// <paramref name="time"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public DateTime? subtime(DateTime? datetime, TimeSpan? time) => default!;

    /// <summary>
    /// <c>subtime(time1, time2)</c>: the difference of two durations. MySQL-only; MariaDB rejects the
    /// call.
    /// </summary>
    public TimeSpan? subtime(TimeSpan? time1, TimeSpan? time2) => default!;

    /// <summary>
    /// <c>time(datetime)</c>: the time-of-day part of <paramref name="datetime"/>. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    public TimeSpan? time(DateTime? datetime) => default!;

    /// <summary>
    /// <c>time(time)</c>: the duration unchanged, rounded to whole seconds. MySQL-only; MariaDB rejects
    /// the call.
    /// </summary>
    public TimeSpan? time(TimeSpan? time) => default!;

    /// <summary>
    /// <c>time_format(time, format)</c>: the duration formatted with a MySQL <c>%</c>-style
    /// <paramref name="format"/> string. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? time_format(TimeSpan? time, string? format) => default!;

    /// <summary>
    /// <c>time_to_sec(time)</c>: the duration converted to whole seconds. MySQL 8.4 returns an integer
    /// number of seconds, so any sub-second component of the argument is discarded by the server. The
    /// argument is the duration to convert; a SQL NULL argument yields SQL NULL. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    public long? time_to_sec(TimeSpan? time) => default!;

    /// <summary>
    /// <c>timediff(datetime1, datetime2)</c>: the duration from <paramref name="datetime2"/> to
    /// <paramref name="datetime1"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public TimeSpan? timediff(DateTime? datetime1, DateTime? datetime2) => default!;

    /// <summary>
    /// <c>timediff(time1, time2)</c>: the duration from <paramref name="time2"/> to
    /// <paramref name="time1"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public TimeSpan? timediff(TimeSpan? time1, TimeSpan? time2) => default!;

    /// <summary>
    /// <c>timestamp(datetime)</c>: <paramref name="datetime"/> as a <c>DATETIME</c>. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    public DateTime? timestamp(DateTime? datetime) => default!;

    /// <summary>
    /// <c>timestamp(datetime, time)</c>: <paramref name="datetime"/> plus the duration
    /// <paramref name="time"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public DateTime? timestamp(DateTime? datetime, TimeSpan? time) => default!;

    /// <summary>
    /// <c>to_days(date)</c>: the day number of <paramref name="date"/> (days since year 0). MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    public long? to_days(DateTime? date) => default!;

    /// <summary>
    /// <c>to_seconds(date)</c>: the second number of <paramref name="date"/> (seconds since year 0).
    /// MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? to_seconds(DateTime? date) => default!;

    /// <summary>
    /// <c>yearweek(date)</c>: the year and week of <paramref name="date"/> under the default week
    /// mode. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? yearweek(DateTime? date) => default!;

    /// <summary>
    /// <c>yearweek(date, mode)</c>: the year and week of <paramref name="date"/> under the given
    /// week <paramref name="mode"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? yearweek(DateTime? date, int? mode) => default!;

    /// <summary>
    /// <c>percentile_cont(0.5) within group (order by value) over (...)</c>: the median as the native
    /// analytic composition (neither MySQL nor MariaDB has a <c>median</c> function). Complete the call
    /// with <c>Over(...)</c>: a partition-only window (or an empty <c>over ()</c>) is supported, while a
    /// window <c>ORDER BY</c> or a frame is refused. The result keeps one row per input row, unlike an
    /// aggregate. MariaDB supports the form (MariaDB 10.3+, <c>double(18,10)</c>); MySQL has neither the
    /// function nor the <c>WITHIN GROUP</c> grammar and refuses the call with
    /// <see cref="NotSupportedException"/> naming MySQL. Requires
    /// <see cref="ISqlDialect.SupportsPercentileWindow"/>.
    /// </summary>
    /// <typeparam name="T">The value expression's type.</typeparam>
    /// <param name="value">The value to take the median of.</param>
    /// <returns>The interpolated median of the window, or SQL NULL when the window has no non-null values.</returns>
    public WindowFunction<double?> median<T>(T? value) => default!;

    /// <summary>
    /// <c>percentile</c>: not supported by MySQL or MariaDB. The native spellings are the window
    /// <c>CommonFunctions.percentile_cont</c> (interpolated) and
    /// <c>CommonFunctions.percentile_disc</c> (an input value) completed with <c>Over(...)</c>, which
    /// differ in whether the result is interpolated; a generic alias would hide that difference.
    /// Referencing this member throws <see cref="NotSupportedException"/> at translation time, naming the
    /// actual provider. The <paramref name="fraction"/> is a fraction in <c>[0, 1]</c>, not a percentage.
    /// </summary>
    /// <typeparam name="T">The value expression's type.</typeparam>
    /// <param name="fraction">The percentile fraction, in <c>[0, 1]</c>.</param>
    /// <param name="value">The value to take the percentile of.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public WindowFunction<double?> percentile<T>(double fraction, T? value) => default!;

    /// <summary>
    /// <c>aes_encrypt(data, key)</c>: <paramref name="data"/> encrypted with the AES algorithm and
    /// <paramref name="key"/>. MySQL 8.4 uses <c>aes-128-ecb</c> (ECB) with a 128-bit key by default.
    /// Returns <c>null</c> when either argument is SQL NULL. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    public byte[]? aes_encrypt(byte[]? data, byte[]? key) => default!;

    /// <summary>
    /// <c>aes_encrypt(data, key, iv)</c>: as the two-argument form, with <paramref name="iv"/> selecting
    /// the CBC mode prescribed by the vendor manual for the three-argument form. MySQL-only.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    public byte[]? aes_encrypt(byte[]? data, byte[]? key, byte[]? iv) => default!;

    /// <summary>
    /// <c>aes_encrypt(data, key, iv, kdf)</c>: as the three-argument form, with
    /// <paramref name="kdf"/> choosing the key-derivation function. Which names the server accepts is
    /// validated by the server; this surface only forwards the literal. MySQL-only.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    public byte[]? aes_encrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf) => default!;

    /// <summary>
    /// <c>aes_encrypt(data, key, iv, kdf, salt)</c>: as the four-argument form, adding the
    /// <paramref name="salt"/> the derived-key forms require. Which KDF names are valid is validated by
    /// the server. MySQL-only.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    /// <param name="salt">The salt for the derived-key forms.</param>
    public byte[]? aes_encrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf, byte[]? salt) => default!;

    /// <summary>
    /// <c>aes_encrypt(data, key, iv, kdf, salt, info)</c>: the derived-key form with the
    /// <paramref name="info"/> context value. Which KDF names are valid is validated by the server.
    /// MySQL-only.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    /// <param name="salt">The salt for the derived-key form.</param>
    /// <param name="info">The context info for the derived-key form.</param>
    public byte[]? aes_encrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf, byte[]? salt, string? info) => default!;

    /// <summary>
    /// <c>aes_encrypt(data, key, iv, kdf, salt, iterations)</c>: the derived-key form with the
    /// <paramref name="iterations"/> count. Which KDF names are valid is validated by the server.
    /// MySQL-only.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    /// <param name="salt">The salt for the derived-key form.</param>
    /// <param name="iterations">The iteration count for the derived-key form.</param>
    public byte[]? aes_encrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf, byte[]? salt, int? iterations) => default!;

    /// <summary>
    /// <c>aes_decrypt(data, key)</c>: <paramref name="data"/> decrypted with the AES algorithm and
    /// <paramref name="key"/>. MySQL 8.4 uses <c>aes-128-ecb</c> (ECB) with a 128-bit key by default.
    /// Returns <c>null</c> when either argument is SQL NULL, or when the input is invalid or padded
    /// incorrectly. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The encryption key.</param>
    public byte[]? aes_decrypt(byte[]? data, byte[]? key) => default!;

    /// <summary>
    /// <c>aes_decrypt(data, key, iv)</c>: as the two-argument form, with <paramref name="iv"/> selecting
    /// the CBC mode prescribed by the vendor manual for the three-argument form. MySQL-only.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    public byte[]? aes_decrypt(byte[]? data, byte[]? key, byte[]? iv) => default!;

    /// <summary>
    /// <c>aes_decrypt(data, key, iv, kdf)</c>: as the three-argument form, with
    /// <paramref name="kdf"/> choosing the key-derivation function. Which names the server accepts is
    /// validated by the server; this surface only forwards the literal. MySQL-only.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    public byte[]? aes_decrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf) => default!;

    /// <summary>
    /// <c>aes_decrypt(data, key, iv, kdf, salt)</c>: as the four-argument form, adding the
    /// <paramref name="salt"/> the derived-key forms require. Which KDF names are valid is validated by
    /// the server. MySQL-only.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    /// <param name="salt">The salt for the derived-key forms.</param>
    public byte[]? aes_decrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf, byte[]? salt) => default!;

    /// <summary>
    /// <c>aes_decrypt(data, key, iv, kdf, salt, info)</c>: the derived-key form with the
    /// <paramref name="info"/> context value. Which KDF names are valid is validated by the server.
    /// MySQL-only.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    /// <param name="salt">The salt for the derived-key form.</param>
    /// <param name="info">The context info for the derived-key form.</param>
    public byte[]? aes_decrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf, byte[]? salt, string? info) => default!;

    /// <summary>
    /// <c>aes_decrypt(data, key, iv, kdf, salt, iterations)</c>: the derived-key form with the
    /// <paramref name="iterations"/> count. Which KDF names are valid is validated by the server.
    /// MySQL-only.
    /// </summary>
    /// <param name="data">The data to decrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="kdf">The key-derivation function name.</param>
    /// <param name="salt">The salt for the derived-key form.</param>
    /// <param name="iterations">The iteration count for the derived-key form.</param>
    public byte[]? aes_decrypt(byte[]? data, byte[]? key, byte[]? iv, string? kdf, byte[]? salt, int? iterations) => default!;

    /// <summary>
    /// <c>compress(value)</c>: <paramref name="value"/> compressed with the zlib algorithm, prefixed by
    /// its uncompressed length, or <c>null</c> when the argument is SQL NULL. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    /// <param name="value">The value to compress.</param>
    public byte[]? compress(byte[]? value) => default!;

    /// <summary>
    /// <c>uncompress(value)</c>: the value decompressed from the <c>COMPRESS</c> format, or <c>null</c>
    /// when the argument is SQL NULL or is not a compressed value. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="value">The compressed value.</param>
    public byte[]? uncompress(byte[]? value) => default!;

    /// <summary>
    /// <c>uncompressed_length(value)</c>: the uncompressed length recorded in the <c>COMPRESS</c> header,
    /// or <c>null</c> when the argument is SQL NULL or is not a compressed value. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    /// <param name="value">The compressed value.</param>
    public long? uncompressed_length(byte[]? value) => default!;

    /// <summary>
    /// <c>random_bytes(length)</c>: <paramref name="length"/> cryptographically random bytes, or
    /// <c>null</c> when the argument is SQL NULL. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="length">The number of bytes to generate.</param>
    public byte[]? random_bytes(int? length) => default!;

    /// <summary>
    /// <c>validate_password_strength(password)</c>: the server's password-strength grade for
    /// <paramref name="password"/>. Meaningful grading depends on the server's password-validation
    /// configuration: on a server without the <c>validate_password</c> component the call still
    /// executes, but <c>VALIDATE_PASSWORD_STRENGTH('abc')</c> evaluates to <c>0</c> and the value
    /// carries no active policy. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="password">The password to grade.</param>
    public long? validate_password_strength(string? password) => default!;

    /// <summary>
    /// <c>statement_digest(statement)</c>: the SHA-256 digest of the normalised statement text, or
    /// <c>null</c> when the argument is SQL NULL. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="statement">The SQL statement text.</param>
    public string? statement_digest(string? statement) => default!;

    /// <summary>
    /// <c>statement_digest_text(statement)</c>: the normalised form of the statement text, or
    /// <c>null</c> when the argument is SQL NULL. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="statement">The SQL statement text.</param>
    public string? statement_digest_text(string? statement) => default!;

    /// <summary>
    /// <c>found_rows()</c>: the row count the last <c>SELECT</c> would have returned without its
    /// <c>LIMIT</c>. The value depends on the session that ran that statement. MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    public long? found_rows() => default!;

    /// <summary>
    /// <c>last_insert_id()</c>: the first automatically generated value produced by the last statement
    /// that inserted into an <c>AUTO_INCREMENT</c> column on this connection. MySQL-only; MariaDB rejects
    /// the call.
    /// </summary>
    public ulong? last_insert_id() => default!;

    /// <summary>
    /// <c>last_insert_id(value)</c>: sets the session's last-insert identifier to
    /// <paramref name="value"/> and returns it. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="value">The identifier to record for the session.</param>
    public ulong? last_insert_id(ulong? value) => default!;

    /// <summary>
    /// <c>row_count()</c>: the number of rows affected by the last statement on this connection.
    /// MySQL-only; MariaDB rejects the call.
    /// </summary>
    public long? row_count() => default!;

    /// <summary>
    /// <c>current_role()</c>: the active roles of the current session, or <c>NONE</c> when no role is
    /// active. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? current_role() => default!;

    /// <summary>
    /// <c>charset(expression)</c>: the character set of <paramref name="expression"/>. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    /// <param name="expression">The expression whose character set is reported.</param>
    public string? charset(object? expression) => default!;

    /// <summary>
    /// <c>collation(expression)</c>: the collation of <paramref name="expression"/>. MySQL-only;
    /// MariaDB rejects the call.
    /// </summary>
    /// <param name="expression">The expression whose collation is reported.</param>
    public string? collation(object? expression) => default!;

    /// <summary>
    /// <c>coercibility(expression)</c>: the coercibility value MySQL uses to resolve collation conflicts
    /// for <paramref name="expression"/>. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="expression">The expression whose coercibility is reported.</param>
    public long? coercibility(object? expression) => default!;

    /// <summary>
    /// <c>benchmark(count, expression)</c>: evaluates <paramref name="expression"/>
    /// <paramref name="count"/> times and returns <c>0</c>. The server executes the expression for its
    /// side effects only, so this is a timing probe, not a value function. MySQL-only; MariaDB rejects
    /// the call.
    /// </summary>
    /// <param name="count">The number of evaluations.</param>
    /// <param name="expression">The expression to evaluate.</param>
    public long? benchmark(long? count, object? expression) => default!;

    /// <summary>
    /// <c>icu_version()</c>: the ICU library version the server was built with, or an empty string when
    /// it was built without ICU. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public string? icu_version() => default!;

    /// <summary>
    /// <c>uuid_short()</c>: a 64-bit unsigned identifier unique to this server instance, derived from
    /// the server start time and a monotonic counter. MySQL-only; MariaDB rejects the call.
    /// </summary>
    public ulong? uuid_short() => default!;

    /// <summary>
    /// <c>is_uuid(value)</c>: <c>1</c> when <paramref name="value"/> is a syntactically valid string
    /// UUID, otherwise <c>0</c> (and <c>null</c> when the argument is SQL NULL). MySQL-only; MariaDB
    /// rejects the call.
    /// </summary>
    /// <param name="value">The string to validate as a UUID.</param>
    public long? is_uuid(string? value) => default!;

    /// <summary>
    /// <c>sleep(seconds)</c>: pauses the statement for <paramref name="seconds"/> and returns <c>0</c>,
    /// or <c>1</c> when the sleep was interrupted. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="seconds">The number of seconds to pause, possibly fractional.</param>
    public long? sleep(double? seconds) => default!;

    /// <summary>
    /// <c>grouping(expressions...)</c>: the rollup grouping bitmap for the given expressions, for use
    /// with <c>GROUP BY ... WITH ROLLUP</c>. Every argument must exactly match an expression of the
    /// <c>GROUP BY</c> clause; that rule is a caller obligation the expression translator cannot check
    /// because it never sees the grouping list. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <param name="expressions">The grouped expressions, in the <c>GROUP BY</c> order.</param>
    public long? grouping(params object?[] expressions) => default!;

    /// <summary>
    /// <c>name_const(name, value)</c>: returns <paramref name="value"/> under the column label
    /// <paramref name="name"/>. The name must be a compile-time string literal; a non-constant or null
    /// name is refused before SQL is emitted. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <typeparam name="TValue">The value expression's type.</typeparam>
    /// <param name="name">The column label, as a compile-time literal.</param>
    /// <param name="value">The value to return.</param>
    public TValue name_const<TValue>(string? name, TValue value) => default!;

    /// <summary>
    /// <c>default(column)</c>: the default value of the named column. The argument must be a column
    /// reference; constants, parameters, casts and computed expressions are refused before SQL is
    /// emitted. MySQL-only; MariaDB rejects the call.
    /// </summary>
    /// <typeparam name="TValue">The column's value type.</typeparam>
    /// <param name="column">The column whose default is returned.</param>
    public TValue column_default<TValue>(TValue column) => default!;

    /// <summary>
    /// <c>uuid()</c> is refused: MySQL generates UUID version 1, while this API's UUID contract is v4
    /// (<see cref="CommonFunctions.gen_random_uuid"/>) or v7 (<see cref="CommonFunctions.uuidv7"/>), and
    /// a v1 value is never substituted for it. Referencing this member throws
    /// <see cref="NotSupportedException"/> at translation time. Use <c>uuidv7()</c> to generate an
    /// identifier, or <c>UUID()</c> through raw SQL when v1 is genuinely intended.
    /// </summary>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public Guid? uuid() => throw new NotSupportedException(UuidRefusal);

    /// <summary>
    /// <c>values(column)</c> is refused: the name is deprecated in MySQL 8.4 and has no
    /// scalar-expression contract — it refers to the new row only inside the upsert mutation builder.
    /// Referencing this member throws <see cref="NotSupportedException"/> at translation time; use the
    /// mutation builder's upsert API instead.
    /// </summary>
    /// <typeparam name="TValue">The column's value type.</typeparam>
    /// <param name="column">The column to refer to; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public TValue values<TValue>(TValue column) => throw new NotSupportedException(ValuesRefusal);

    /// <summary>
    /// <c>COLUMN_GET(dynamic_column, 'name' AS type)</c> is refused: the <c>AS</c> type clause is
    /// mandatory and selects the CLR type at parse time, so the clause has no safe compile-time
    /// representation on a scalar function surface. Referencing this member throws
    /// <see cref="NotSupportedException"/> in the body as well as when the expression is translated.
    /// </summary>
    /// <param name="dynamicColumn">The dynamic-columns blob; never read.</param>
    /// <param name="name">The column name; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public object? column_get(object? dynamicColumn, string name) =>
        throw new NotSupportedException(ColumnGetRefusal);

    /// <summary>
    /// <c>TRUNC(...)</c> is refused: the verified MariaDB 11.4 baseline does not implement the name in
    /// default mode or under <c>sql_mode=ORACLE</c>. Referencing this member throws
    /// <see cref="NotSupportedException"/> in the body as well as when the expression is translated; use
    /// <c>truncate</c> for numeric truncation.
    /// </summary>
    /// <param name="value">The value to truncate; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public object? trunc(object? value) => throw new NotSupportedException(TruncRefusal);

    /// <summary>
    /// <c>_rowid</c> is refused: the pseudo-column needs a source table with exactly one integer
    /// primary-key column, a table-dependent requirement the function surface cannot validate. On any
    /// other table the server reports error 1054. Referencing this member throws
    /// <see cref="NotSupportedException"/> in the body as well as when the expression is translated.
    /// </summary>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public int? _rowid() => throw new NotSupportedException(RowIdRefusal);

    /// <summary>
    /// <c>VEC_FromText(text)</c> is refused: the vector functions arrived in MariaDB 11.7 and the verified
    /// baseline is 11.4. Referencing this member throws <see cref="NotSupportedException"/> at
    /// translation time; the dialect never switches to a newer server.
    /// </summary>
    /// <param name="text">The vector text; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public object? vec_from_text(string? text) => throw new NotSupportedException(VectorRefusal);

    /// <summary>
    /// <c>VEC_ToText(vector)</c> is refused: the vector functions arrived in MariaDB 11.7 and the verified
    /// baseline is 11.4. Referencing this member throws <see cref="NotSupportedException"/> at
    /// translation time.
    /// </summary>
    /// <param name="vector">The vector value; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public string? vec_to_text(object? vector) => throw new NotSupportedException(VectorRefusal);

    /// <summary>
    /// <c>VEC_DISTANCE_EUCLIDEAN(left, right)</c> is refused: the vector functions arrived in MariaDB
    /// 11.7 and the verified baseline is 11.4. Referencing this member throws
    /// <see cref="NotSupportedException"/> at translation time.
    /// </summary>
    /// <param name="left">The left vector; never read.</param>
    /// <param name="right">The right vector; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public double? vec_distance_euclidean(object? left, object? right) =>
        throw new NotSupportedException(VectorRefusal);

    /// <summary>
    /// <c>VEC_DISTANCE_COSINE(left, right)</c> is refused: the vector functions arrived in MariaDB 11.7
    /// and the verified baseline is 11.4. Referencing this member throws
    /// <see cref="NotSupportedException"/> at translation time.
    /// </summary>
    /// <param name="left">The left vector; never read.</param>
    /// <param name="right">The right vector; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public double? vec_distance_cosine(object? left, object? right) =>
        throw new NotSupportedException(VectorRefusal);

    /// <summary>
    /// <c>VEC_DISTANCE(left, right)</c> is refused: the vector functions arrived in MariaDB 11.7 and the
    /// verified baseline is 11.4. Referencing this member throws <see cref="NotSupportedException"/> at
    /// translation time.
    /// </summary>
    /// <param name="left">The left vector; never read.</param>
    /// <param name="right">The right vector; never read.</param>
    /// <returns>Never returns; the call is refused when the query is translated.</returns>
    public double? vec_distance(object? left, object? right) => throw new NotSupportedException(VectorRefusal);

    /// <summary>The exact refusal text for <c>json_table()</c>, shared by the member body and the translator.</summary>
    internal const string JsonTableRefusal =
        "MySQL JSON_TABLE is a table-producing construct that requires a FROM/join position and a " +
        "COLUMNS definition list; it is not a scalar function and cannot be expressed on " +
        "SqlFunctions.MySql.";

    /// <summary>The exact refusal text for <c>uuid()</c>, shared by the member body and the translator.</summary>
    internal const string UuidRefusal =
        "uuid is refused: MySQL UUID() generates UUID version 1; this API's UUID contract is v4 " +
        "(gen_random_uuid) or v7 (uuidv7), and a v1 value is never substituted for it. Use uuidv7() " +
        "to generate an identifier, or UUID() through raw SQL when v1 is genuinely intended.";

    /// <summary>The exact refusal text for <c>values(column)</c>, shared by the member body and the translator.</summary>
    internal const string ValuesRefusal =
        "MySQL VALUES(column) cannot be translated as a scalar query function; use the mutation " +
        "builder's upsert API.";

    /// <summary>The exact refusal text for <c>column_get(...)</c>, shared by the member body and the translator.</summary>
    internal const string ColumnGetRefusal =
        "COLUMN_GET requires a compile-time AS type clause; the supported function surface cannot " +
        "represent that clause safely.";

    /// <summary>The exact refusal text for <c>trunc(...)</c>, shared by the member body and the translator.</summary>
    internal const string TruncRefusal =
        "TRUNC is unavailable on the verified MariaDB 11.4 baseline, including ORACLE mode; " +
        "use TRUNCATE for numeric truncation.";

    /// <summary>The exact refusal text for <c>_rowid</c>, shared by the member body and the translator.</summary>
    internal const string RowIdRefusal =
        "_rowid requires a source table with one integer primary-key column; this function surface " +
        "cannot validate that table-dependent requirement.";

    /// <summary>The exact refusal text for the five <c>VEC_*</c> members, shared by every body and the translator.</summary>
    internal const string VectorRefusal =
        "Requires MariaDB 11.7 or later; the verified baseline is MariaDB 11.4.";
}
