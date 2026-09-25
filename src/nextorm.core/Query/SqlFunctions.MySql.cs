namespace NextORM.Core;

/// <summary>
/// MySQL/MariaDB-only SQL surface: the native string/conditional idioms (<c>find_in_set</c>,
/// <c>field</c>, <c>elt</c>, <c>substring_index</c>, <c>format</c>), the <c>%</c>-templated date
/// conversion and Unix-epoch functions, the hexadecimal hash functions, the IPv4 conversion pair, the
/// JSON mutation family, the binary UUID pair and the MariaDB-only names (extended regexp,
/// <c>nvl</c>/<c>nvl2</c>, the Oracle-compatible date/number conversions, <c>kdf</c>, <c>xxh3</c>/
/// <c>xxh32</c>, <c>json_detailed</c>/<c>json_compact</c> and sequence access).
/// <para>
/// Exposed through <see cref="SqlFunctions.MySql"/>; every member is gated per name by the dialect's
/// <see cref="IMySqlFunctions"/> capability object and rejected by providers that do not opt in.
/// MariaDB supports this surface through its MySQL-derived dialect and adds the MariaDB-only names;
/// MySQL rejects those names with a clear message.
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

    /// <summary><c>json_set(json, path, value)</c>: inserts or updates the value at <paramref name="path"/>.</summary>
    public string? json_set(string? json, string? path, string? value) => default!;

    /// <summary><c>json_insert(json, path, value)</c>: inserts the value at <paramref name="path"/>, keeping any existing value.</summary>
    public string? json_insert(string? json, string? path, string? value) => default!;

    /// <summary><c>json_replace(json, path, value)</c>: replaces an existing value at <paramref name="path"/>.</summary>
    public string? json_replace(string? json, string? path, string? value) => default!;

    /// <summary><c>json_remove(json, path)</c>: removes the value at <paramref name="path"/>.</summary>
    public string? json_remove(string? json, string? path) => default!;

    /// <summary><c>json_merge_patch(json, other)</c>: merges two JSON documents using the RFC 7396 merge-patch algorithm.</summary>
    public string? json_merge_patch(string? json, string? other) => default!;

    /// <summary><c>json_merge_preserve(json, other)</c>: merges two JSON documents, preserving duplicate keys.</summary>
    public string? json_merge_preserve(string? json, string? other) => default!;

    /// <summary><c>json_array_append(json, path, value)</c>: appends the value to the array at <paramref name="path"/>.</summary>
    public string? json_array_append(string? json, string? path, string? value) => default!;

    /// <summary><c>json_array_insert(json, path, value)</c>: inserts the value into the array at <paramref name="path"/> without replacing an existing element.</summary>
    public string? json_array_insert(string? json, string? path, string? value) => default!;

    /// <summary><c>json_depth(json)</c>: the maximum nesting depth of the JSON document.</summary>
    public int? json_depth(string? json) => default!;

    /// <summary><c>json_keys(json)</c>: the top-level keys of the JSON object as a JSON array.</summary>
    public string? json_keys(string? json) => default!;

    /// <summary><c>json_length(json)</c>: the number of elements in the top-level JSON array or object.</summary>
    public int? json_length(string? json) => default!;

    /// <summary><c>json_type(json)</c>: the type name of the top-level JSON value.</summary>
    public string? json_type(string? json) => default!;

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
    /// <paramref name="a"/> is earlier. MariaDB-only.
    /// </summary>
    public double? months_between(DateTime? a, DateTime? b) => default!;

    /// <summary>
    /// <c>to_char(value, format)</c>: formats the date/time using an Oracle-style format string.
    /// MariaDB-only (MariaDB 10.6+).
    /// </summary>
    public string? to_char(DateTime? value, string? format) => default!;

    /// <summary>
    /// <c>to_date(value, format)</c>: parses a date/time from an Oracle-style format string.
    /// MariaDB-only (MariaDB 12.3+).
    /// </summary>
    public DateTime? to_date(string? value, string? format) => default!;

    /// <summary>
    /// <c>to_number(value, format)</c>: parses a number from an Oracle-style format string (the result
    /// is a <c>DOUBLE</c>). MariaDB-only (MariaDB 12.2+).
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
}
