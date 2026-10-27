namespace NextORM.Core;

/// <summary>
/// MySQL/MariaDB-only SQL surface: the native string/conditional idioms (<c>find_in_set</c>,
/// <c>field</c>, <c>elt</c>, <c>substring_index</c>, <c>format</c>), the <c>%</c>-templated date
/// conversion and Unix-epoch functions, the hexadecimal hash functions, the IPv4 conversion pair, the
/// JSON mutation family, the binary UUID pair and the MariaDB-only names (extended regexp,
/// <c>nvl</c>/<c>nvl2</c>, the Oracle-compatible date/number conversions, <c>kdf</c>, <c>xxh3</c>/
/// <c>xxh32</c>, <c>json_detailed</c>/<c>json_compact</c>, the JSON canonicalisation pair
/// <c>json_equals</c>/<c>json_normalize</c> and sequence access). It also carries the MySQL 8.4
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
    /// <c>time_to_sec(time)</c>: the duration converted to whole seconds.
    /// For a parameterized <see cref="TimeSpan"/> argument, the current parameter binding truncates any
    /// non-zero sub-second component before <c>TIME_TO_SEC</c> executes. MySQL 8.4 returns a
    /// <c>BIGINT</c>, so sub-second precision is discarded even when the value reaches the server. The
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
}
