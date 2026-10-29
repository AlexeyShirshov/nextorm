using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the cross-provider scalar functions against ClickHouse. ClickHouse does not derive the
/// shared <see cref="CommonTestSuite"/> (its divergences live in <see cref="ClickHouseIntegrationTests"/>),
/// so the same shared assertions are pinned here for the container-backed provider.
/// The dialect's scalar-function table is also swept through its public surface
/// (<see cref="ClickHouseDialect.ScalarFunctions"/>): every accepted name, every render arm, the
/// negative/edge contracts, and a live execution per function family.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseScalarFunctionsTests : ProviderTestSuite
{
    private static readonly string[] SupportedNames =
    [
        "left", "right", "lpad", "rpad", "repeat", "reverse", "space",
        "concat_ws", "translate", "ascii", "char",
        "lower_utf8", "upper_utf8", "trim_left", "trim_right", "trim_both",
        "replace_regexp_one", "replace_regexp_all", "match", "extract", "extract_all",
        "split_by_string", "split_by_regexp", "split_by_whitespace",
        "overlay", "initcap", "soundex",
        "base64_encode", "base64_decode", "hex", "unhex",
        "edit_distance", "jaro_similarity", "byte_hamming_distance",
        "substring_index", "count_substrings", "extract_groups",
        "normalize_utf8_nfc", "normalize_utf8_nfd", "normalize_utf8_nfkc", "normalize_utf8_nfkd",
        "format_date_time", "parse_date_time", "parse_date_time_best_effort",
        "now", "today", "yesterday",
        "array_concat", "array_flatten", "array_uniq", "array_intersect",
        "array_union", "array_symmetric_difference",
        "map", "map_keys", "map_values", "map_contains_key", "map_contains_value",
        "map_add", "map_concat", "map_filter", "map_apply", "map_all", "map_exists",
        "map_sort",
        "group_bitmap", "group_bitmap_and", "group_bitmap_or", "group_bitmap_xor",
        "sum_map", "sum_map_filtered",
        "md5", "sha1", "sha256", "sha512",
        "xx_hash32", "xx_hash64", "xxh3", "city_hash64",
        "sip_hash64", "sip_hash128",
        "murmur_hash2_32", "murmur_hash2_64", "murmur_hash3_32", "murmur_hash3_64",
        "murmur_hash3_128", "generate_ulid",
        "bit_length", "octet_length", "degrees", "radians", "pi",
        "log2", "cbrt", "exp2", "exp10",
        "erf", "erfc", "lgamma", "tgamma", "gcd", "lcm",
        "is_finite", "is_infinite", "is_nan", "hypot", "int_div",
        "int_exp2", "int_exp10", "sigmoid", "sqr",
        "round_age", "round_bankers", "round_down", "round_duration", "round_to_exp2",
        "make_date", "make_date32", "make_date_time", "now64", "now_in_block",
        "time_slot", "time_slots",
        "to_start_of_fifteen_minutes", "to_start_of_five_minutes", "to_start_of_ten_minutes",
        "to_start_of_interval", "to_iso_year", "to_week", "to_year_week",
        "to_last_day_of_week", "to_days_in_month", "to_modified_julian_day",
        "age", "date_name", "server_timezone", "timezone_of", "timezone_offset",
        "to_time", "timestamp",
        "from_unix_timestamp", "from_unix_timestamp64_milli", "from_unix_timestamp64_micro",
        "tuple_concat", "tuple_names", "flatten_tuple", "tuple_to_name_value_pairs",
        "tuple_plus", "tuple_minus", "tuple_multiply", "tuple_divide",
        "tuple_hamming_distance", "dot_product",
        "map_subtract", "map_update", "map_reverse_sort", "map_partial_sort",
        "map_from_arrays", "map_populate_series", "extract_key_value_pairs",
        "bin", "unbin", "base32_encode", "base32_decode", "base58_encode", "base58_decode",
        "base64_url_encode", "base64_url_decode", "bech32_encode", "bech32_decode",
        "bitmask_to_array", "bitmask_to_list", "bit_positions_to_array",
        "sqid_encode", "sqid_decode",
        "hilbert_encode", "hilbert_decode", "morton_encode", "morton_decode",
        "uuid_num_to_string", "uuid_string_to_num", "uuid_to_num", "uuid_v7_to_datetime",
        "to_uuid", "to_uuid_or_default", "to_uuid_or_null", "ulid_string_to_datetime",
        "generate_snowflake_id", "date_time_to_snowflake_id", "date_time64_to_snowflake_id",
        "date_time_to_uuid_v7", "snowflake_id_to_date_time", "snowflake_id_to_date_time64",
        "domain", "protocol", "path", "query_string", "fragment", "port",
        "cut_url_parameter", "top_level_domain", "encode_url_component", "decode_url_component",
        "encode_url_form_component", "decode_url_form_component", "domain_without_www",
        "first_significant_subdomain", "first_significant_subdomain_custom", "path_full",
        "query_string_and_fragment", "cut_www", "cut_query_string", "cut_fragment",
        "cut_query_string_and_fragment", "cut_to_first_significant_subdomain",
        "cut_to_first_significant_subdomain_with_www", "cut_to_first_significant_subdomain_custom",
        "cut_to_first_significant_subdomain_custom_with_www", "url_hierarchy", "url_path_hierarchy",
        "extract_url_parameter", "extract_url_parameter_names",
        "ipv4_num_to_string", "ipv4_num_to_string_class_c", "ipv4_string_to_num",
        "ipv4_string_to_num_or_default", "ipv4_string_to_num_or_null",
        "ipv6_num_to_string", "ipv6_string_to_num", "ipv6_string_to_num_or_default",
        "ipv6_string_to_num_or_null", "cut_ipv6", "is_ipv4_string", "is_ipv6_string",
        "is_ip_address_in_range", "to_ipv4", "to_ipv6", "ipv4_to_ipv6",
        "ipv4_cidr_to_range", "ipv6_cidr_to_range",
        "bit_and", "bit_or", "bit_xor", "bit_not",
        "bit_shift_left", "bit_shift_right", "bit_rotate_left", "bit_rotate_right",
        "bit_count", "bit_test", "bit_test_all", "bit_test_any", "bit_hamming_distance",
        "non_negative_derivative",
        "to_int8", "to_int8_or_null", "to_int8_or_default", "to_int16", "to_int16_or_null",
        "to_int16_or_default", "to_int32", "to_int32_or_null", "to_int32_or_default", "to_int64",
        "to_int64_or_null", "to_int64_or_default", "to_uint8", "to_uint8_or_null",
        "to_uint8_or_default", "to_uint16", "to_uint16_or_null", "to_uint16_or_default", "to_uint32",
        "to_uint32_or_null", "to_uint32_or_default", "to_uint64", "to_uint64_or_null",
        "to_uint64_or_default", "to_float32", "to_float32_or_null", "to_float32_or_default",
        "to_float64", "to_float64_or_null", "to_float64_or_default", "to_decimal32",
        "to_decimal32_or_null", "to_decimal32_or_default", "to_decimal64", "to_decimal64_or_null",
        "to_decimal64_or_default", "to_string", "to_bool", "to_fixed_string", "to_low_cardinality",
        "accurate_cast_int8", "accurate_cast_int8_or_null", "accurate_cast_int8_or_default",
        "accurate_cast_int16", "accurate_cast_int16_or_null", "accurate_cast_int16_or_default",
        "accurate_cast_int32", "accurate_cast_int32_or_null", "accurate_cast_int32_or_default",
        "accurate_cast_int64", "accurate_cast_int64_or_null", "accurate_cast_int64_or_default",
        "accurate_cast_uint8", "accurate_cast_uint8_or_null", "accurate_cast_uint8_or_default",
        "accurate_cast_uint16", "accurate_cast_uint16_or_null", "accurate_cast_uint16_or_default",
        "accurate_cast_uint32", "accurate_cast_uint32_or_null", "accurate_cast_uint32_or_default",
        "accurate_cast_uint64", "accurate_cast_uint64_or_null", "accurate_cast_uint64_or_default",
        "accurate_cast_float32", "accurate_cast_float32_or_null", "accurate_cast_float32_or_default",
        "accurate_cast_float64", "accurate_cast_float64_or_null", "accurate_cast_float64_or_default",
        "accurate_cast_decimal32", "accurate_cast_decimal32_or_null",
        "accurate_cast_decimal32_or_default", "accurate_cast_decimal64",
        "accurate_cast_decimal64_or_null", "accurate_cast_decimal64_or_default",
        "reinterpret_as_uint8", "reinterpret_as_uint16", "reinterpret_as_uint32",
        "reinterpret_as_uint64", "reinterpret_as_int8", "reinterpret_as_int16",
        "reinterpret_as_int32", "reinterpret_as_int64", "reinterpret_as_float32",
        "reinterpret_as_float64", "reinterpret_as_date", "reinterpret_as_date_time",
        "reinterpret_as_string", "reinterpret_as_fixed_string", "reinterpret_as_uuid"
    ];

    // Every Render arm of ClickHouseScalarFunctions, written out literally from the dialect contract
    // (src/nextorm.clickhouse/ClickHouseDialect.cs): one row per name, plus a second row for the
    // optional pad form of lpad/rpad, the optional length of overlay, the optional start of
    // count_substrings, the scale of roundBankers, the two makeDate forms, the now64 scale, both
    // toStartOfInterval forms, the week-mode forms, the timestamp time form, the three
    // fromUnixTimestamp forms, the two port forms, the three toIPv4 overloads, the 13 bit-family names and the window-only nonNegativeDerivative name. 345 names -> 379
    // arms; array_except is absent from the undeclared (pinned 25.8) table — #252 gates the native
    // arrayExcept behind a context that declares ClickHouse 25.9+, so the shared singleton advertises
    // neither the name nor an arm — and the three date/time CLR-shape refusals (from_utc_timestamp,
    // to_time64, from_unix_timestamp64_nano) have no accepted arm.
    private static readonly (string Name, string[] Args, string Expected)[] RenderArms =
    [
        ("left", ["s", "2"], "leftUTF8(s, 2)"),
        ("right", ["s", "2"], "rightUTF8(s, 2)"),
        ("lpad", ["s", "3", "'0'"], "leftPadUTF8(s, 3, '0')"),
        ("lpad", ["s", "3"], "leftPadUTF8(s, 3, ' ')"),
        ("rpad", ["s", "3", "'0'"], "rightPadUTF8(s, 3, '0')"),
        ("rpad", ["s", "3"], "rightPadUTF8(s, 3, ' ')"),
        ("repeat", ["s", "2"], "repeat(s, 2)"),
        ("reverse", ["s"], "reverseUTF8(s)"),
        ("space", ["2"], "space(2)"),
        ("concat_ws", ["sep", "a", "b"], "concatWithSeparator(sep, a, b)"),
        ("translate", ["s", "from", "to"], "translate(s, from, to)"),
        ("ascii", ["s"], "ascii(s)"),
        ("char", ["65"], "char(65)"),
        ("lower_utf8", ["s"], "lowerUTF8(s)"),
        ("upper_utf8", ["s"], "upperUTF8(s)"),
        ("trim_left", ["s"], "trimLeft(s)"),
        ("trim_right", ["s"], "trimRight(s)"),
        ("trim_both", ["s"], "trimBoth(s)"),
        ("replace_regexp_one", ["s", "pat", "rep"], "replaceRegexpOne(s, pat, rep)"),
        ("replace_regexp_all", ["s", "pat", "rep"], "replaceRegexpAll(s, pat, rep)"),
        ("match", ["s", "pat"], "match(s, pat)"),
        ("extract", ["s", "pat"], "extract(s, pat)"),
        ("extract_all", ["s", "pat"], "extractAll(s, pat)"),
        ("split_by_string", ["sep", "s"], "splitByString(sep, s)"),
        ("split_by_regexp", ["pat", "s"], "splitByRegexp(pat, s)"),
        ("split_by_whitespace", ["s"], "splitByWhitespace(s)"),
        ("overlay", ["s", "r", "2"], "overlay(s, r, 2)"),
        ("overlay", ["s", "r", "2", "3"], "overlay(s, r, 2, 3)"),
        ("initcap", ["s"], "initcap(s)"),
        ("soundex", ["s"], "soundex(s)"),
        ("base64_encode", ["s"], "base64Encode(s)"),
        ("base64_decode", ["s"], "base64Decode(s)"),
        ("hex", ["s"], "hex(s)"),
        ("unhex", ["s"], "unhex(s)"),
        ("edit_distance", ["a", "b"], "toInt64(editDistance(a, b))"),
        ("jaro_similarity", ["a", "b"], "jaroSimilarity(a, b)"),
        ("byte_hamming_distance", ["a", "b"], "toInt64(byteHammingDistance(a, b))"),
        ("substring_index", ["s", "d", "2"], "substringIndex(s, d, 2)"),
        ("count_substrings", ["s", "sub"], "toInt64(countSubstrings(s, sub))"),
        ("count_substrings", ["s", "sub", "3"], "toInt64(countSubstrings(s, sub, 3))"),
        ("extract_groups", ["s", "pat"], "extractGroups(s, pat)"),
        ("normalize_utf8_nfc", ["s"], "normalizeUTF8NFC(s)"),
        ("normalize_utf8_nfd", ["s"], "normalizeUTF8NFD(s)"),
        ("normalize_utf8_nfkc", ["s"], "normalizeUTF8NFKC(s)"),
        ("normalize_utf8_nfkd", ["s"], "normalizeUTF8NFKD(s)"),
        ("format_date_time", ["dt", "fmt"], "formatDateTime(dt, fmt)"),
        ("parse_date_time", ["s", "fmt"], "parseDateTime(s, fmt)"),
        ("parse_date_time_best_effort", ["s"], "parseDateTimeBestEffort(s)"),
        ("now", [], "now()"),
        ("today", [], "today()"),
        ("yesterday", [], "yesterday()"),
        ("array_concat", ["a", "b"], "arrayConcat(a, b)"),
        ("array_flatten", ["a"], "arrayFlatten(a)"),
        ("array_uniq", ["a"], "toInt64(arrayUniq(a))"),
        ("array_intersect", ["a", "b"], "arrayIntersect(a, b)"),
        ("array_union", ["a", "b"], "arrayUnion(a, b)"),
        ("array_symmetric_difference", ["a", "b"], "arraySymmetricDifference(a, b)"),
        ("map", ["k", "v"], "map(k, v)"),
        ("map_keys", ["m"], "mapKeys(m)"),
        ("map_values", ["m"], "mapValues(m)"),
        ("map_contains_key", ["m", "k"], "mapContainsKey(m, k)"),
        ("map_contains_value", ["m", "v"], "mapContainsValue(m, v)"),
        ("map_add", ["a", "b"], "mapAdd(a, b)"),
        ("map_concat", ["a", "b", "c"], "mapConcat(a, b, c)"),
        ("map_filter", ["m", "f"], "mapFilter(m, f)"),
        ("map_apply", ["m", "f"], "mapApply(m, f)"),
        ("map_all", ["m", "f"], "mapAll(m, f)"),
        ("map_exists", ["m", "f"], "mapExists(m, f)"),
        ("map_sort", ["m"], "mapSort(m)"),
        ("group_bitmap", ["a"], "groupBitmap(a)"),
        ("group_bitmap_and", ["a"], "groupBitmapAnd(a)"),
        ("group_bitmap_or", ["a"], "groupBitmapOr(a)"),
        ("group_bitmap_xor", ["a"], "groupBitmapXor(a)"),
        ("sum_map", ["k", "v"], "sumMap(k, v)"),
        ("sum_map_filtered", ["f", "k", "v"], "sumMapFiltered(f)(k, v)"),
        ("md5", ["s"], "lower(hex(MD5(s)))"),
        ("sha1", ["s"], "lower(hex(SHA1(s)))"),
        ("sha256", ["s"], "lower(hex(SHA256(s)))"),
        ("sha512", ["s"], "lower(hex(SHA512(s)))"),
        ("xx_hash32", ["s"], "toInt32(xxHash32(s))"),
        ("xx_hash64", ["s"], "toInt64(xxHash64(s))"),
        ("xxh3", ["s"], "toInt64(xxh3(s))"),
        ("city_hash64", ["s"], "toInt64(cityHash64(s))"),
        ("sip_hash64", ["s"], "toInt64(sipHash64(s))"),
        ("sip_hash128", ["s"], "lower(hex(sipHash128(s)))"),
        ("murmur_hash2_32", ["s"], "toInt32(murmurHash2_32(s))"),
        ("murmur_hash2_64", ["s"], "toInt64(murmurHash2_64(s))"),
        ("murmur_hash3_32", ["s"], "toInt32(murmurHash3_32(s))"),
        ("murmur_hash3_64", ["s"], "toInt64(murmurHash3_64(s))"),
        ("murmur_hash3_128", ["s"], "lower(hex(murmurHash3_128(s)))"),
        ("generate_ulid", [], "generateULID()"),
        ("bit_length", ["s"], "toInt32(length(s) * 8)"),
        ("octet_length", ["s"], "toInt32(length(s))"),
        ("degrees", ["x"], "degrees(x)"),
        ("radians", ["x"], "radians(x)"),
        ("pi", [], "pi()"),
        ("log2", ["x"], "log2(x)"),
        ("cbrt", ["x"], "cbrt(x)"),
        ("exp2", ["x"], "exp2(x)"),
        ("exp10", ["x"], "exp10(x)"),
        ("erf", ["x"], "erf(x)"),
        ("erfc", ["x"], "erfc(x)"),
        ("lgamma", ["x"], "lgamma(x)"),
        ("tgamma", ["x"], "tgamma(x)"),
        ("gcd", ["a", "b"], "gcd(a, b)"),
        ("lcm", ["a", "b"], "lcm(a, b)"),
        ("is_finite", ["x"], "isFinite(x)"),
        ("is_infinite", ["x"], "isInfinite(x)"),
        ("is_nan", ["x"], "isNaN(x)"),
        ("hypot", ["a", "b"], "hypot(a, b)"),
        ("int_div", ["a", "b"], "intDiv(a, b)"),
        ("int_exp2", ["x"], "intExp2(x)"),
        ("int_exp10", ["x"], "intExp10(x)"),
        ("sigmoid", ["x"], "sigmoid(x)"),
        ("sqr", ["x"], "(x * x)"),
        ("round_age", ["age"], "toInt64(roundAge(age))"),
        ("round_bankers", ["x"], "roundBankers(x)"),
        ("round_bankers", ["x", "2"], "roundBankers(x, 2)"),
        ("round_down", ["x", "b"], "roundDown(x, b)"),
        ("round_duration", ["d"], "toInt64(roundDuration(d))"),
        ("round_to_exp2", ["x"], "roundToExp2(x)"),
        ("make_date", ["y", "m", "d"], "makeDate(y, m, d)"),
        ("make_date", ["y", "doy"], "makeDate(y, doy)"),
        ("make_date32", ["y", "m", "d"], "makeDate32(y, m, d)"),
        ("make_date32", ["y", "doy"], "makeDate32(y, doy)"),
        ("make_date_time", ["y", "m", "d", "h", "mi", "s"], "makeDateTime(y, m, d, h, mi, s)"),
        ("now64", [], "now64()"),
        ("now64", ["6"], "now64(6)"),
        ("now_in_block", [], "nowInBlock()"),
        ("time_slot", ["dt"], "timeSlot(dt)"),
        ("time_slots", ["dt", "dur"], "timeSlots(toDateTime(dt), toUInt32(dur))"),
        ("to_start_of_fifteen_minutes", ["dt"], "toStartOfFifteenMinutes(dt)"),
        ("to_start_of_five_minutes", ["dt"], "toStartOfFiveMinutes(dt)"),
        ("to_start_of_ten_minutes", ["dt"], "toStartOfTenMinutes(dt)"),
        ("to_start_of_interval", ["dt", "15", "day"], "toStartOfInterval(dt, INTERVAL 15 DAY)"),
        ("to_start_of_interval", ["dt", "1", "week", "origin"], "toStartOfInterval(toDateTime(dt), INTERVAL 1 WEEK, toDateTime(origin))"),
        ("to_iso_year", ["dt"], "toInt32(toISOYear(dt))"),
        ("to_week", ["dt"], "toInt32(toWeek(dt))"),
        ("to_week", ["dt", "3"], "toInt32(toWeek(dt, toUInt8(3)))"),
        ("to_year_week", ["dt"], "toInt32(toYearWeek(dt))"),
        ("to_year_week", ["dt", "3"], "toInt32(toYearWeek(dt, toUInt8(3)))"),
        ("to_last_day_of_week", ["dt"], "toLastDayOfWeek(dt)"),
        ("to_last_day_of_week", ["dt", "1"], "toLastDayOfWeek(dt, toUInt8(1))"),
        ("to_days_in_month", ["dt"], "toInt32(toDayOfMonth(toLastDayOfMonth(dt)))"),
        ("to_modified_julian_day", ["s"], "toModifiedJulianDay(s)"),
        ("age", ["unit", "start", "end"], "age(unit, start, end)"),
        ("date_name", ["unit", "dt"], "dateName(unit, dt)"),
        ("server_timezone", [], "serverTimezone()"),
        ("timezone_of", ["dt"], "timezoneOf(dt)"),
        ("timezone_offset", ["dt"], "timezoneOffset(dt)"),
        ("to_time", ["dt"], "toTimeWithFixedDate(dt)"),
        ("timestamp", ["s"], "timestamp(s)"),
        ("timestamp", ["s", "t"], "timestamp(s, t)"),
        ("from_unix_timestamp", ["x"], "fromUnixTimestamp(x)"),
        ("from_unix_timestamp", ["x", "fmt"], "fromUnixTimestamp(x, fmt)"),
        ("from_unix_timestamp", ["x", "fmt", "tz"], "fromUnixTimestamp(x, fmt, tz)"),
        ("from_unix_timestamp64_milli", ["x"], "fromUnixTimestamp64Milli(x)"),
        ("from_unix_timestamp64_micro", ["x"], "fromUnixTimestamp64Micro(x)"),
        ("tuple_concat", ["a", "b"], "tupleConcat(a, b)"),
        ("tuple_names", ["t"], "tupleNames(t)"),
        ("flatten_tuple", ["t"], "flattenTuple(t)"),
        ("tuple_to_name_value_pairs", ["t"], "tupleToNameValuePairs(t)"),
        ("tuple_plus", ["a", "b"], "tuplePlus(a, b)"),
        ("tuple_minus", ["a", "b"], "tupleMinus(a, b)"),
        ("tuple_multiply", ["a", "b"], "tupleMultiply(a, b)"),
        ("tuple_divide", ["a", "b"], "tupleDivide(a, b)"),
        ("tuple_hamming_distance", ["a", "b"], "toInt64(tupleHammingDistance(a, b))"),
        ("dot_product", ["a", "b"], "dotProduct(a, b)"),
        ("map_subtract", ["a", "b"], "mapSubtract(a, b)"),
        ("map_update", ["a", "b"], "mapUpdate(a, b)"),
        ("map_reverse_sort", ["m"], "mapReverseSort(m)"),
        ("map_partial_sort", ["2", "m"], "mapPartialSort(2, m)"),
        ("map_from_arrays", ["k", "v"], "mapFromArrays(k, v)"),
        ("map_populate_series", ["m"], "mapPopulateSeries(m)"),
        ("map_populate_series", ["m", "10"], "mapPopulateSeries(m, 10)"),
        ("extract_key_value_pairs", ["s"], "extractKeyValuePairs(s)"),
        ("extract_key_value_pairs", ["s", "'='", "','"], "extractKeyValuePairs(s, '=', ',')"),
        ("bin", ["x"], "bin(x)"),
        ("unbin", ["s"], "hex(unbin(s))"),
        ("base32_encode", ["s"], "base32Encode(s)"),
        ("base32_decode", ["s"], "hex(base32Decode(s))"),
        ("base58_encode", ["s"], "base58Encode(s)"),
        ("base58_decode", ["s"], "hex(base58Decode(s))"),
        ("base64_url_encode", ["s"], "base64URLEncode(s)"),
        ("base64_url_decode", ["s"], "hex(base64URLDecode(s))"),
        ("bech32_encode", ["h", "d"], "bech32Encode(h, d)"),
        ("bech32_encode", ["h", "d", "0"], "bech32Encode(h, d, 0)"),
        ("bech32_decode", ["s"], "tuple(tupleElement(bech32Decode(s), 1), hex(tupleElement(bech32Decode(s), 2)))"),
        ("bitmask_to_array", ["x"], "bitmaskToArray(toUInt64(x))"),
        ("bitmask_to_list", ["x"], "bitmaskToList(x)"),
        ("bit_positions_to_array", ["x"], "bitPositionsToArray(x)"),
        ("sqid_encode", ["a", "b"], "sqidEncode(a, b)"),
        ("sqid_decode", ["s"], "sqidDecode(s)"),
        ("hilbert_encode", ["x"], "hilbertEncode(x)"),
        ("hilbert_encode", ["x", "y"], "hilbertEncode(x, y)"),
        ("hilbert_decode", ["c"], "hilbertDecode(2, c)"),
        ("morton_encode", ["x", "y"], "mortonEncode(x, y)"),
        ("morton_decode", ["c"], "mortonDecode(2, c)"),
        ("uuid_num_to_string", ["s"], "UUIDNumToString(toFixedString(unhex(s), 16))"),
        ("uuid_string_to_num", ["s"], "hex(UUIDStringToNum(s))"),
        ("uuid_to_num", ["u"], "hex(UUIDToNum(u))"),
        ("uuid_v7_to_datetime", ["u"], "UUIDv7ToDateTime(u)"),
        ("to_uuid", ["s"], "toUUID(s)"),
        ("to_uuid_or_default", ["s", "d"], "toUUIDOrDefault(s, d)"),
        ("to_uuid_or_null", ["s"], "toUUIDOrNull(s)"),
        ("ulid_string_to_datetime", ["s"], "ULIDStringToDateTime(s)"),
        ("generate_snowflake_id", [], "generateSnowflakeID()"),
        ("date_time_to_snowflake_id", ["dt"], "dateTimeToSnowflakeID(dt)"),
        ("date_time64_to_snowflake_id", ["dt"], "dateTime64ToSnowflakeID(toDateTime64(dt, 3))"),
        ("date_time_to_uuid_v7", ["dt"], "dateTimeToUUIDv7(dt)"),
        ("snowflake_id_to_date_time", ["x"], "snowflakeIDToDateTime(x)"),
        ("snowflake_id_to_date_time64", ["x"], "snowflakeIDToDateTime64(x)"),
        ("domain", ["s"], "domain(s)"),
        ("protocol", ["s"], "protocol(s)"),
        ("path", ["s"], "path(s)"),
        ("query_string", ["s"], "queryString(s)"),
        ("fragment", ["s"], "fragment(s)"),
        ("port", ["s"], "toInt32(port(s))"),
        ("port", ["s", "d"], "toInt32(port(s, d))"),
        ("cut_url_parameter", ["s", "n"], "cutURLParameter(s, n)"),
        ("top_level_domain", ["s"], "topLevelDomain(s)"),
        ("encode_url_component", ["s"], "encodeURLComponent(s)"),
        ("decode_url_component", ["s"], "decodeURLComponent(s)"),
        ("encode_url_form_component", ["s"], "encodeURLFormComponent(s)"),
        ("decode_url_form_component", ["s"], "decodeURLFormComponent(s)"),
        ("domain_without_www", ["s"], "domainWithoutWWW(s)"),
        ("first_significant_subdomain", ["s"], "firstSignificantSubdomain(s)"),
        ("first_significant_subdomain_custom", ["s", "l"], "firstSignificantSubdomainCustom(s, l)"),
        ("path_full", ["s"], "pathFull(s)"),
        ("query_string_and_fragment", ["s"], "queryStringAndFragment(s)"),
        ("cut_www", ["s"], "cutWWW(s)"),
        ("cut_query_string", ["s"], "cutQueryString(s)"),
        ("cut_fragment", ["s"], "cutFragment(s)"),
        ("cut_query_string_and_fragment", ["s"], "cutQueryStringAndFragment(s)"),
        ("cut_to_first_significant_subdomain", ["s"], "cutToFirstSignificantSubdomain(s)"),
        ("cut_to_first_significant_subdomain_with_www", ["s"], "cutToFirstSignificantSubdomainWithWWW(s)"),
        ("cut_to_first_significant_subdomain_custom", ["s", "l"],
            "cutToFirstSignificantSubdomainCustom(s, l)"),
        ("cut_to_first_significant_subdomain_custom_with_www", ["s", "l"],
            "cutToFirstSignificantSubdomainCustomWithWWW(s, l)"),
        ("url_hierarchy", ["s"], "URLHierarchy(s)"),
        ("url_path_hierarchy", ["s"], "URLPathHierarchy(s)"),
        ("extract_url_parameter", ["s", "n"], "extractURLParameter(s, n)"),
        ("extract_url_parameter_names", ["s"], "extractURLParameterNames(s)"),
        ("ipv4_num_to_string", ["x"], "IPv4NumToString(x)"),
        ("ipv4_num_to_string_class_c", ["x"], "IPv4NumToStringClassC(x)"),
        ("ipv4_string_to_num", ["s"], "IPv4StringToNum(s)"),
        ("ipv4_string_to_num_or_default", ["s"], "IPv4StringToNumOrDefault(s)"),
        ("ipv4_string_to_num_or_null", ["s"], "IPv4StringToNumOrNull(s)"),
        ("ipv6_num_to_string", ["s"], "IPv6NumToString(toFixedString(unhex(s), 16))"),
        ("ipv6_string_to_num", ["s"], "hex(IPv6StringToNum(s))"),
        ("ipv6_string_to_num_or_default", ["s"], "hex(IPv6StringToNumOrDefault(s))"),
        ("ipv6_string_to_num_or_null", ["s"], "hex(IPv6StringToNumOrNull(s))"),
        ("cut_ipv6", ["s", "b", "z"], "cutIPv6(toFixedString(unhex(s), 16), b, z)"),
        ("is_ipv4_string", ["s"], "isIPv4String(s)"),
        ("is_ipv6_string", ["s"], "isIPv6String(s)"),
        ("is_ip_address_in_range", ["s", "r"], "isIPAddressInRange(s, r)"),
        ("to_ipv4", ["s"], "toString(toIPv4(s))"),
        ("to_ipv4", ["x"], "toString(toIPv4(x))"),
        ("to_ipv4", ["n"], "toString(toIPv4(n))"),
        ("to_ipv6", ["s"], "toString(toIPv6(s))"),
        ("ipv4_to_ipv6", ["a"], "toString(IPv4ToIPv6(toIPv4(a)))"),
        ("ipv4_cidr_to_range", ["a", "p"],
            "tuple(toString(tupleElement(IPv4CIDRToRange(toIPv4(a), p), 1)), toString(tupleElement(IPv4CIDRToRange(toIPv4(a), p), 2)))"),
        ("ipv6_cidr_to_range", ["a", "p"],
            "tuple(toString(tupleElement(IPv6CIDRToRange(toIPv6(a), p), 1)), toString(tupleElement(IPv6CIDRToRange(toIPv6(a), p), 2)))"),
        ("bit_and", ["a", "b"], "bitAnd(a, b)"),
        ("bit_or", ["a", "b"], "bitOr(a, b)"),
        ("bit_xor", ["a", "b"], "bitXor(a, b)"),
        ("bit_not", ["a"], "bitNot(a)"),
        ("bit_shift_left", ["a", "n"], "bitShiftLeft(a, n)"),
        ("bit_shift_right", ["a", "n"], "bitShiftRight(a, n)"),
        ("bit_rotate_left", ["a", "n"], "bitRotateLeft(a, n)"),
        ("bit_rotate_right", ["a", "n"], "bitRotateRight(a, n)"),
        ("bit_count", ["a"], "bitCount(a)"),
        ("bit_test", ["a", "n"], "bitTest(a, n)"),
        ("bit_test_all", ["a", "n", "m"], "bitTestAll(a, n, m)"),
        ("bit_test_any", ["a", "n", "m"], "bitTestAny(a, n, m)"),
        ("bit_hamming_distance", ["a", "b"], "bitHammingDistance(a, b)"),
        ("non_negative_derivative", ["v", "t"], "nonNegativeDerivative(v, t)"),
        ("to_int8", ["a"], "toInt16(toInt8(a))"),
        ("to_int8_or_null", ["a"], "toInt16(toInt8OrNull(a))"),
        ("to_int8_or_default", ["a"], "toInt16(toInt8OrDefault(a))"),
        ("to_int8_or_default", ["a", "b"], "toInt16(toInt8OrDefault(a, toInt8(b)))"),
        ("to_int16", ["a"], "toInt16(a)"),
        ("to_int16_or_null", ["a"], "toInt16OrNull(a)"),
        ("to_int16_or_default", ["a"], "toInt16OrDefault(a)"),
        ("to_int16_or_default", ["a", "b"], "toInt16OrDefault(a, toInt16(b))"),
        ("to_int32", ["a"], "toInt32(a)"),
        ("to_int32_or_null", ["a"], "toInt32OrNull(a)"),
        ("to_int32_or_default", ["a"], "toInt32OrDefault(a)"),
        ("to_int32_or_default", ["a", "b"], "toInt32OrDefault(a, toInt32(b))"),
        ("to_int64", ["a"], "toInt64(a)"),
        ("to_int64_or_null", ["a"], "toInt64OrNull(a)"),
        ("to_int64_or_default", ["a"], "toInt64OrDefault(a)"),
        ("to_int64_or_default", ["a", "b"], "toInt64OrDefault(a, toInt64(b))"),
        ("to_uint8", ["a"], "toUInt8(a)"),
        ("to_uint8_or_null", ["a"], "toUInt8OrNull(a)"),
        ("to_uint8_or_default", ["a"], "toUInt8OrDefault(a)"),
        ("to_uint8_or_default", ["a", "b"], "toUInt8OrDefault(a, toUInt8(b))"),
        ("to_uint16", ["a"], "toInt32(toUInt16(a))"),
        ("to_uint16_or_null", ["a"], "toInt32(toUInt16OrNull(a))"),
        ("to_uint16_or_default", ["a"], "toInt32(toUInt16OrDefault(a))"),
        ("to_uint16_or_default", ["a", "b"], "toInt32(toUInt16OrDefault(a, toUInt16(b)))"),
        ("to_uint32", ["a"], "toUInt32(a)"),
        ("to_uint32_or_null", ["a"], "toUInt32OrNull(a)"),
        ("to_uint32_or_default", ["a"], "toUInt32OrDefault(a)"),
        ("to_uint32_or_default", ["a", "b"], "toUInt32OrDefault(a, toUInt32(b))"),
        ("to_uint64", ["a"], "toUInt64(a)"),
        ("to_uint64_or_null", ["a"], "toUInt64OrNull(a)"),
        ("to_uint64_or_default", ["a"], "toUInt64OrDefault(a)"),
        ("to_uint64_or_default", ["a", "b"], "toUInt64OrDefault(a, toUInt64(b))"),
        ("to_float32", ["a"], "toFloat32(a)"),
        ("to_float32_or_null", ["a"], "toFloat32OrNull(a)"),
        ("to_float32_or_default", ["a"], "toFloat32OrDefault(a)"),
        ("to_float32_or_default", ["a", "b"], "toFloat32OrDefault(a, toFloat32(b))"),
        ("to_float64", ["a"], "toFloat64(a)"),
        ("to_float64_or_null", ["a"], "toFloat64OrNull(a)"),
        ("to_float64_or_default", ["a"], "toFloat64OrDefault(a)"),
        ("to_float64_or_default", ["a", "b"], "toFloat64OrDefault(a, toFloat64(b))"),
        ("to_decimal32", ["a", "2"], "toDecimal32(a, 2)"),
        ("to_decimal32_or_null", ["a", "2"], "toDecimal32OrNull(a, 2)"),
        ("to_decimal32_or_default", ["a", "2"], "toDecimal32OrDefault(a, 2)"),
        ("to_decimal32_or_default", ["a", "2", "b"], "toDecimal32OrDefault(a, 2, toDecimal32(b, 2))"),
        ("to_decimal64", ["a", "2"], "toDecimal64(a, 2)"),
        ("to_decimal64_or_null", ["a", "2"], "toDecimal64OrNull(a, 2)"),
        ("to_decimal64_or_default", ["a", "2"], "toDecimal64OrDefault(a, 2)"),
        ("to_decimal64_or_default", ["a", "2", "b"], "toDecimal64OrDefault(a, 2, toDecimal64(b, 2))"),
        ("to_string", ["a"], "toString(a)"),
        ("to_bool", ["a"], "toBool(a)"),
        ("to_fixed_string", ["a", "4"], "toFixedString(a, 4)"),
        ("to_low_cardinality", ["a"], "toLowCardinality(a)"),
        ("accurate_cast_int8", ["a"], "toInt16(accurateCast(a, 'Int8'))"),
        ("accurate_cast_int8_or_null", ["a"], "toInt16(accurateCastOrNull(a, 'Int8'))"),
        ("accurate_cast_int8_or_default", ["a"], "toInt16(accurateCastOrDefault(a, 'Int8'))"),
        ("accurate_cast_int16", ["a"], "accurateCast(a, 'Int16')"),
        ("accurate_cast_int16_or_null", ["a"], "accurateCastOrNull(a, 'Int16')"),
        ("accurate_cast_int16_or_default", ["a"], "accurateCastOrDefault(a, 'Int16')"),
        ("accurate_cast_int32", ["a"], "accurateCast(a, 'Int32')"),
        ("accurate_cast_int32_or_null", ["a"], "accurateCastOrNull(a, 'Int32')"),
        ("accurate_cast_int32_or_default", ["a"], "accurateCastOrDefault(a, 'Int32')"),
        ("accurate_cast_int64", ["a"], "accurateCast(a, 'Int64')"),
        ("accurate_cast_int64_or_null", ["a"], "accurateCastOrNull(a, 'Int64')"),
        ("accurate_cast_int64_or_default", ["a"], "accurateCastOrDefault(a, 'Int64')"),
        ("accurate_cast_uint8", ["a"], "accurateCast(a, 'UInt8')"),
        ("accurate_cast_uint8_or_null", ["a"], "accurateCastOrNull(a, 'UInt8')"),
        ("accurate_cast_uint8_or_default", ["a"], "accurateCastOrDefault(a, 'UInt8')"),
        ("accurate_cast_uint16", ["a"], "toInt32(accurateCast(a, 'UInt16'))"),
        ("accurate_cast_uint16_or_null", ["a"], "toInt32(accurateCastOrNull(a, 'UInt16'))"),
        ("accurate_cast_uint16_or_default", ["a"], "toInt32(accurateCastOrDefault(a, 'UInt16'))"),
        ("accurate_cast_uint32", ["a"], "accurateCast(a, 'UInt32')"),
        ("accurate_cast_uint32_or_null", ["a"], "accurateCastOrNull(a, 'UInt32')"),
        ("accurate_cast_uint32_or_default", ["a"], "accurateCastOrDefault(a, 'UInt32')"),
        ("accurate_cast_uint64", ["a"], "accurateCast(a, 'UInt64')"),
        ("accurate_cast_uint64_or_null", ["a"], "accurateCastOrNull(a, 'UInt64')"),
        ("accurate_cast_uint64_or_default", ["a"], "accurateCastOrDefault(a, 'UInt64')"),
        ("accurate_cast_float32", ["a"], "accurateCast(a, 'Float32')"),
        ("accurate_cast_float32_or_null", ["a"], "accurateCastOrNull(a, 'Float32')"),
        ("accurate_cast_float32_or_default", ["a"], "accurateCastOrDefault(a, 'Float32')"),
        ("accurate_cast_float64", ["a"], "accurateCast(a, 'Float64')"),
        ("accurate_cast_float64_or_null", ["a"], "accurateCastOrNull(a, 'Float64')"),
        ("accurate_cast_float64_or_default", ["a"], "accurateCastOrDefault(a, 'Float64')"),
        ("accurate_cast_decimal32", ["a", "2"], "accurateCast(a, 'Decimal(9, 2)')"),
        ("accurate_cast_decimal32_or_null", ["a", "2"], "accurateCastOrNull(a, 'Decimal(9, 2)')"),
        ("accurate_cast_decimal32_or_default", ["a", "2"], "accurateCastOrDefault(a, 'Decimal(9, 2)')"),
        ("accurate_cast_decimal64", ["a", "2"], "accurateCast(a, 'Decimal(18, 2)')"),
        ("accurate_cast_decimal64_or_null", ["a", "2"], "accurateCastOrNull(a, 'Decimal(18, 2)')"),
        ("accurate_cast_decimal64_or_default", ["a", "2"], "accurateCastOrDefault(a, 'Decimal(18, 2)')"),
        ("reinterpret_as_uint8", ["a"], "reinterpretAsUInt8(a)"),
        ("reinterpret_as_uint16", ["a"], "toInt32(reinterpretAsUInt16(a))"),
        ("reinterpret_as_uint32", ["a"], "reinterpretAsUInt32(a)"),
        ("reinterpret_as_uint64", ["a"], "reinterpretAsUInt64(a)"),
        ("reinterpret_as_int8", ["a"], "toInt16(reinterpretAsInt8(a))"),
        ("reinterpret_as_int16", ["a"], "reinterpretAsInt16(a)"),
        ("reinterpret_as_int32", ["a"], "reinterpretAsInt32(a)"),
        ("reinterpret_as_int64", ["a"], "reinterpretAsInt64(a)"),
        ("reinterpret_as_float32", ["a"], "reinterpretAsFloat32(a)"),
        ("reinterpret_as_float64", ["a"], "reinterpretAsFloat64(a)"),
        ("reinterpret_as_date", ["a"], "reinterpretAsDate(a)"),
        ("reinterpret_as_date_time", ["a"], "reinterpretAsDateTime(a)"),
        ("reinterpret_as_string", ["a"], "reinterpretAsString(a)"),
        ("reinterpret_as_fixed_string", ["a"], "reinterpretAsFixedString(a)"),
        ("reinterpret_as_uuid", ["a"], "reinterpretAsUUID(a)")
    ];

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static IScalarFunctions Functions => ClickHouseDialect.Instance.ScalarFunctions;

    private static string Sql(string name, params string[] args) => Functions.Render(name, args);

    [Fact]
    public void Supports_ShouldAcceptTheCompleteInventoryAndNothingElse()
    {
        SupportedNames.Should().HaveCount(345, "the ClickHouse table accepts 345 scalar-function names");
        SupportedNames.Should().OnlyHaveUniqueItems();

        using (new AssertionScope())
        {
            foreach (var name in SupportedNames)
                Functions.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);
        }

        // The render table and the predicate must agree name-for-name: no accepted name without an
        // arm, no arm for a name the dialect does not accept.
        RenderArms.Select(arm => arm.Name).Distinct().Should().BeEquivalentTo(SupportedNames);
        RenderArms.Should().HaveCount(379, "345 names plus the pad forms of lpad/rpad, the optional forms of overlay, count_substrings, roundBankers, makeDate, makeDate32, now64, toStartOfInterval, the week modes, timestamp, fromUnixTimestamp, mapPopulateSeries/extractKeyValuePairs, bech32Encode(witver), hilbertEncode(2 coordinates), the port default and the three toIPv4 overloads");
    }

    [Fact]
    public void Render_ShouldEmitTheContractSqlForEveryArm()
    {
        using (new AssertionScope())
        {
            foreach (var (name, args, expected) in RenderArms)
                Sql(name, args).Should().Be(
                    expected, "'{0}({1})' is the ClickHouse arm of that name", name, string.Join(", ", args));
        }
    }

    [Fact]
    public void Supports_ShouldRejectUnknownNamesEmptyAndCaseVariants()
    {
        Functions.Supports("bogus").Should().BeFalse("the name is not in the table");
        Functions.Supports("").Should().BeFalse("the empty name is not in the table");
        Functions.Supports(null!).Should().BeFalse("the name pattern never matches a null name");
        Functions.Supports("ABS").Should().BeFalse("names are matched case-sensitively against the CLR names");
    }

    [Fact]
    public void Render_ShouldThrowNotSupportedForUnknownNamesEmptyAndCaseVariants()
    {
        FluentActions.Invoking(() => Sql("bogus", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The bogus function is not supported by ClickHouse.");

        FluentActions.Invoking(() => Sql("", []))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The  function is not supported by ClickHouse.");

        FluentActions.Invoking(() => Sql("ABS", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The ABS function is not supported by ClickHouse.");
    }

    [Fact]
    public void Render_ShouldSurfaceAMissingArgumentWithoutArityValidation()
    {
        // The table indexes its arguments directly and has no arity guard: a short argument list fails
        // on the list indexer of the missing position (the array-backed argument list surfaces it as an
        // argument-out-of-range failure).
        FluentActions.Invoking(() => Sql("translate", new[] { "a", "b" }))
            .Should().Throw<ArgumentOutOfRangeException>("translate reads args[2] and only two were supplied");

        FluentActions.Invoking(() => Sql("bit_length", new[] { "a", "b" }))
            .Should().NotThrow("bit_length reads args[0] and ignores a surplus argument");
    }

    [Fact]
    public void Render_ShouldIgnoreSurplusArgumentsOfFixedArityArms()
    {
        Sql("now", ["x"]).Should().Be("now()", "zero-argument arms never read the list");
        Sql("pi", ["x", "y"]).Should().Be("pi()", "zero-argument arms never read the list");
    }

    [Fact]
    public void Render_ShouldRenderANullArgumentElementAsAnEmptyPosition()
    {
        // The contract has no null guard on the argument elements: they are interpolated into the call,
        // so a null element leaves an empty position rather than throwing.
        Sql("left", [(string)null!, "2"]).Should().Be("leftUTF8(, 2)");
    }

    [Fact]
    public void ScalarFunctions_ShouldExecuteAgainstTheLiveServer()
    {
        using var context = Provider.CreateContext();

        T Scalar<T>(string expression)
        {
            using var result = context.ExecuteRaw($"select {expression} as v");
            return result.Read<T>().Single();
        }

        // Numeric family: pi()/degrees()/radians().
        Scalar<double>(Sql("degrees", Sql("pi", [])))
            .Should().BeApproximately(180d, 1e-9, "degrees(pi()) is 180 (proves degrees + pi)");
        Scalar<double>(Sql("radians", "180"))
            .Should().BeApproximately(Math.PI, 1e-12, "radians(180) is pi");

        // String family: left/right are UTF-8 character based; pads, repeat, reverse, space, concat_ws,
        // translate, case mapping, trims, regexp replace/extract.
        Scalar<string>(Sql("left", "'abcdef'", "3")).Should().Be("abc", "left -> leftUTF8");
        Scalar<string>(Sql("right", "'Привет'", "3")).Should().Be("вет", "right counts characters, not bytes");
        Scalar<string>(Sql("lpad", "'7'", "3", "'0'")).Should().Be("007", "the three-argument lpad arm");
        Scalar<string>(Sql("rpad", "'7'", "3")).Should().Be("7  ", "the two-argument rpad arm pads with a space");
        Scalar<string>(Sql("repeat", "'ab'", "2")).Should().Be("abab", "repeat");
        Scalar<string>(Sql("reverse", "'abc'")).Should().Be("cba", "reverse -> reverseUTF8");
        Scalar<string>(Sql("space", "2")).Should().Be("  ", "space");
        Scalar<string>(Sql("concat_ws", "'-'", "'a'", "'b'", "'c'")).Should().Be("a-b-c", "concat_ws -> concatWithSeparator");
        Scalar<string>(Sql("translate", "'abc'", "'ab'", "'xy'")).Should().Be("xyc", "translate");
        Scalar<string>(Sql("lower_utf8", "'ПРИВЕТ'")).Should().Be("привет", "lower_utf8 -> lowerUTF8");
        Scalar<string>(Sql("upper_utf8", "'привет'")).Should().Be("ПРИВЕТ", "upper_utf8 -> upperUTF8");
        Scalar<string>(Sql("trim_both", "'  x  '")).Should().Be("x", "trim_both -> trimBoth");
        Scalar<string>(Sql("trim_left", "'  x'")).Should().Be("x", "trim_left -> trimLeft");
        Scalar<string>(Sql("trim_right", "'x  '")).Should().Be("x", "trim_right -> trimRight");
        Scalar<string>(Sql("replace_regexp_one", "'a1b2'", "'[0-9]'", "'#'")).Should().Be("a#b2", "replace_regexp_one replaces the first match");
        Scalar<string>(Sql("replace_regexp_all", "'a1b2'", "'[0-9]'", "'#'")).Should().Be("a#b#", "replace_regexp_all replaces every match");
        Scalar<string>(Sql("extract", "'abc123'", "'[0-9]+'")).Should().Be("123", "extract returns the first match");

        // Date/time family: parse + format round-trip, and the best-effort parser.
        Scalar<string>(Sql(
                "format_date_time",
                Sql("parse_date_time", "'2024-01-02 03:04:05'", "'%Y-%m-%d %H:%i:%S'"),
                "'%Y-%m-%d'"))
            .Should().Be("2024-01-02", "parseDateTime + formatDateTime (proves both arms)");
        Scalar<string>(Sql(
                "format_date_time", Sql("parse_date_time_best_effort", "'2024-01-02'"), "'%Y-%m-%d'"))
            .Should().Be("2024-01-02", "parseDateTimeBestEffort");

        // Array family: the distinct-count arm casts UInt64 to Int64; the concat arm feeds it two arrays.
        Scalar<long>(Sql(
                "array_uniq", Sql("array_concat", "[1, 2]", "[2, 3]")))
            .Should().Be(3, "arrayUniq(arrayConcat(...)) counts distinct values (proves both arms)");

        // Conditional/predicate family: match and mapContainsKey return UInt8 truth values.
        Scalar<int>(Sql("match", "'abcdef'", "'^abc'")).Should().Be(1, "match is a predicate");
        Scalar<int>(Sql("map_contains_key", Sql("map", "'a'", "1", "'b'", "2"), "'a'"))
            .Should().Be(1, "mapContainsKey is a predicate over the map arm");

        // Map family: mapKeys feeds the byte-counting length arm.
        Scalar<int>(Sql("octet_length", Sql("map_keys", Sql("map", "'a'", "1", "'b'", "2"))))
            .Should().Be(2, "mapKeys(map(...)) has two keys");

        // Bit family: bit_length is eight times the byte length; octet_length is the byte length.
        Scalar<int>(Sql("bit_length", "'abc'")).Should().Be(24, "bit_length is length * 8");
        Scalar<int>(Sql("octet_length", "'Привет'")).Should().Be(12, "octet_length counts UTF-8 bytes");

        // Type-conversion family: ascii maps a character to its code point, char maps it back.
        Scalar<int>(Sql("ascii", "'A'")).Should().Be(65, "ascii returns the code point");
        Scalar<string>(Sql("char", "65")).Should().Be("A", "char returns the character");

        // Hash/uuid family: the six string-returning digests are materialised as lowercase hexadecimal,
        // so the scalar terminal reads the digest text directly; generateULID is a 26-character
        // Crockford ULID.
        Scalar<string>(Sql("md5", "'abc'")).Should().Be(
            "900150983cd24fb0d6963f7d28e17f72", "MD5('abc') is the 16-byte digest 900150983cd24fb0d6963f7d28e17f72");
        Scalar<string>(Sql("sha256", "'abc'")).Should().Be(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            "SHA256('abc') is the 32-byte digest ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        Scalar<string>(Sql("generate_ulid", []))
            .Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{26}$", "generateULID produces a 26-character ULID");
    }

    [Fact]
    public void ScalarFunctions_ShouldCompute()
    {
        var cyrillicLeft = "Привет";
        var cyrillicRight = "Привет";

        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lru = SqlFunctions.Sql.left(cyrillicLeft, 3),
                Rru = SqlFunctions.Sql.right(cyrillicRight, 3),
                Lp = SqlFunctions.Sql.lpad("7", 3, "0"),
                Rp = SqlFunctions.Sql.rpad("7", 3, "0"),
                Rep = SqlFunctions.Sql.repeat("ab", 2),
                Rev = SqlFunctions.Sql.reverse("abc"),
                Sp = SqlFunctions.Sql.space(2),
                Cw = SqlFunctions.Sql.concat_ws("-", "a", "b", "c"),
                Tr = SqlFunctions.Sql.translate("abc", "ab", "xy"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65)
            })
            .First();

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lru.Should().Be("При");
        r.Rru.Should().Be("вет");
        r.Lp.Should().Be("007");
        r.Rp.Should().Be("700");
        r.Rep.Should().Be("abab");
        r.Rev.Should().Be("cba");
        r.Sp.Should().Be("  ");
        r.Cw.Should().Be("a-b-c");
        r.Tr.Should().Be("xyc");
        r.Asc.Should().Be(65);
        r.Ch.Should().Be("A");
    }

    [Fact]
    public void LengthFunctions_LinqProjection_MaterializesDeclaredInt32()
    {
        // #253: both functions are declared int?, but the dialect let the server return UInt64. The raw
        // scalar path converts on read, so the mismatch only surfaced through the typed reader that a
        // LINQ projection uses: the issue examples must materialise without InvalidCastException.
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Bl = SqlFunctions.Sql.bit_length("abc"),
                Ol = SqlFunctions.Sql.octet_length("Привет")
            })
            .First();

        r.Bl.Should().Be(24, "bit_length('abc') is eight bits for each of its three UTF-8 bytes");
        r.Ol.Should().Be(12, "octet_length('Привет') counts UTF-8 bytes, not characters");
    }

    [Fact]
    public void LengthFunctions_NullArgumentsRemainNull()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Bl = SqlFunctions.Sql.bit_length(null),
                Ol = SqlFunctions.Sql.octet_length(null)
            })
            .First();

        r.Bl.Should().BeNull("bit_length(NULL) is NULL, never zero");
        r.Ol.Should().BeNull("octet_length(NULL) is NULL, never zero");
    }

    [Fact]
    public void LengthFunctions_EmptyStringsReturnZero()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Bl = SqlFunctions.Sql.bit_length(""),
                Ol = SqlFunctions.Sql.octet_length("")
            })
            .First();

        r.Bl.Should().Be(0, "an empty string has no bits");
        r.Ol.Should().Be(0, "an empty string has no bytes");
    }

    [Fact]
    public void LengthFunctions_UseUtf8ByteCounts()
    {
        // "Привет" is 6 characters / 12 UTF-8 bytes; "é" is 1 character / 2 bytes; U+1F600 is a
        // supplementary code point outside the BMP and occupies 4 UTF-8 bytes.
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Cyrillic = SqlFunctions.Sql.octet_length("Привет"),
                Accent = SqlFunctions.Sql.octet_length("é"),
                Emoji = SqlFunctions.Sql.octet_length("😀"),
                EmojiBits = SqlFunctions.Sql.bit_length("😀")
            })
            .First();

        r.Cyrillic.Should().Be(12, "six Cyrillic characters are two UTF-8 bytes each");
        r.Accent.Should().Be(2, "é is a two-byte UTF-8 sequence");
        r.Emoji.Should().Be(4, "the supplementary U+1F600 is a four-byte UTF-8 sequence");
        r.EmojiBits.Should().Be(32, "four UTF-8 bytes are 32 bits");
    }

    [Fact]
    public void LengthFunctions_ScalarAndProjectionAgree()
    {
        using var context = Provider.CreateContext();

        T Scalar<T>(string expression)
        {
            using var result = context.ExecuteRaw($"select {expression} as v");
            return result.Read<T>().Single();
        }

        var projection = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Bl = SqlFunctions.Sql.bit_length("Привет"),
                Ol = SqlFunctions.Sql.octet_length("Привет")
            })
            .First();

        projection.Bl.Should().Be(96, "six Cyrillic characters are 12 bytes = 96 bits");
        projection.Ol.Should().Be(12);
        Scalar<int>(Sql("bit_length", "'Привет'"))
            .Should().Be(projection.Bl, "the raw scalar terminal agrees with the typed projection");
        Scalar<int>(Sql("octet_length", "'Привет'"))
            .Should().Be(projection.Ol, "the raw scalar terminal agrees with the typed projection");
    }

    [Fact]
    public void LengthFunctions_ToInt32OverflowMatchesUncheckedConversion()
    {
        using var context = Provider.CreateContext();

        int Convert(string expression)
        {
            using var result = context.ExecuteRaw($"select {expression} as v");
            return result.Read<int>().Single();
        }

        // The rendered conversion is ClickHouse's unchecked toInt32, not a checked narrowing cast:
        // int.MaxValue + 1 wraps to int.MinValue and a UInt64 product beyond Int32 wraps modulo 2^32.
        Convert("toInt32(toUInt64(2147483648))").Should().Be(int.MinValue);
        Convert("toInt32(toUInt64(2147483648))").Should().Be(unchecked((int)2_147_483_648UL));
        Convert("toInt32(toUInt64(4294967295))").Should().Be(-1);
        Convert("toInt32(toUInt64(4294967295))").Should().Be(unchecked((int)4_294_967_295UL));
        Convert("toInt32(toUInt64(536870912) * 8)").Should().Be(0,
            "536870912 * 8 = 2^32 bytes is zero after the Int32 wrap");
        Convert("toInt32(toUInt64(536870912) * 8)")
            .Should().Be(unchecked((int)(536_870_912UL * 8)));
    }

    [Fact]
    public void ScalarFunctions_ConcatWsNull_ShouldReturnNull()
    {
        // ClickHouse's concatWithSeparator returns NULL when any argument is NULL, unlike the other
        // providers' concat_ws which skips null arguments.
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new { V = SqlFunctions.Sql.concat_ws("-", "a", null, "b") })
            .First();

        r.V.Should().BeNull();
    }

    /// <summary>
    /// Deferred ledger row <c>C8-D3-GROUPING-SETS-WITH-TOTALS</c>: the only public chain that reaches
    /// the <c>GROUPING SETS</c> + <c>WITH TOTALS</c> guard of
    /// <c>src/nextorm.core/DataContext/SqlBuilder.cs:275-276</c> — ClickHouse reports both
    /// <c>SupportsGroupingSets</c> (<c>ClickHouseDialect.cs:424</c>) and <c>SupportsGroupByWithTotals</c>,
    /// and <c>WithTotals</c> is its own public extension
    /// (<c>ClickHouseEntityBuilderExtensions.cs:97</c>). The grouping-set half executes live over the
    /// seeded three-row table (the <c>(tinyval)</c> group and the empty-set grand total); adding
    /// <c>WITH TOTALS</c> is rejected with the documented message. The engine itself rejects the
    /// general multi-set form with <c>NOT_IMPLEMENTED</c> ("WITH TOTALS and GROUPING SETS are not
    /// supported together"), so the guard mirrors the server and is not a product defect.
    /// </summary>
    [Fact]
    public void GroupingSets_WithTotals_ShouldBeRejectedByTheDocumentedCombinationGuard()
    {
        var groupingSets = _sut.ComplexEntity
            .GroupByGroupingSets(x => new { x.TinyInt }, [0], [])
            .Select(g => new { g.TinyInt, Count = SqlFunctions.Sql.count() })
            .ToList();

        groupingSets.Should().HaveCount(2, "one (tinyval) group plus the empty-set grand total");
        groupingSets.Select(r => (r.TinyInt, r.Count)).Should().BeEquivalentTo(
            new[] { ((byte)0, 3), ((byte)2, 3) },
            "all three seeded rows share tinyval 2, so the (tinyval) set counts three and the empty set "
            + "repeats the count over the default key");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .GroupByGroupingSets(x => new { x.TinyInt }, [0], [])
                .WithTotals()
                .Select(g => SqlFunctions.Sql.count())
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("GROUP BY ... WITH TOTALS cannot be combined with GROUPING SETS.");

        // Server-level evidence that the guard mirrors the engine: ClickHouse itself rejects the
        // general multi-set form with NOT_IMPLEMENTED. If the engine ever learns the combination this
        // fact fails, so the guard is revisited instead of silently refusing a supported shape.
        using var context = Provider.CreateContext();
        FluentActions.Invoking(() => context.ExecuteRaw(
                "select number % 2 as a, number % 3 as b, count() as c from numbers(6) "
                + "group by grouping sets ((a), (b)) with totals").Read<int>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*WITH TOTALS and GROUPING SETS are not supported together*");
    }
}
