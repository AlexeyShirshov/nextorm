using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// Translates the ClickHouse-native functions declared on <see cref="ClickHouseFunctions"/> that have no
/// cross-provider spelling: the UTF-8 case, the RE2 replace/search/split family, the map and bitmap
/// families, the hash functions and <c>generateULID</c>. The native name is produced by the dialect's
/// <see cref="IScalarFunctions"/> renderer, so a provider that does not report the name as supported
/// rejects the call with <see cref="NotSupportedException"/> instead of emitting invalid SQL.
/// <para>
/// The array arguments are rendered through <see cref="SqlOperandTranslator.AppendArrayOrColumn"/>, so an
/// array column stays a column and a captured array is bound as a single parameter; the <c>params</c>
/// overloads flatten the compiler-generated argument array into positional arguments.
/// </para>
/// </summary>
internal static class ClickHouseNativeFunctionTranslator
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.lower_utf8), nameof(ClickHouseFunctions.upper_utf8),
        nameof(ClickHouseFunctions.trim_left), nameof(ClickHouseFunctions.trim_right),
        nameof(ClickHouseFunctions.trim_both),
        nameof(ClickHouseFunctions.replace_regexp_one), nameof(ClickHouseFunctions.replace_regexp_all),
        nameof(ClickHouseFunctions.match), nameof(ClickHouseFunctions.extract),
        nameof(ClickHouseFunctions.extract_all),
        nameof(ClickHouseFunctions.split_by_string), nameof(ClickHouseFunctions.split_by_regexp),
        nameof(ClickHouseFunctions.split_by_whitespace),
        nameof(ClickHouseFunctions.overlay), nameof(ClickHouseFunctions.initcap),
        nameof(ClickHouseFunctions.soundex),
        nameof(ClickHouseFunctions.base64_encode), nameof(ClickHouseFunctions.base64_decode),
        nameof(ClickHouseFunctions.hex), nameof(ClickHouseFunctions.unhex),
        nameof(ClickHouseFunctions.edit_distance), nameof(ClickHouseFunctions.jaro_similarity),
        nameof(ClickHouseFunctions.byte_hamming_distance),
        nameof(ClickHouseFunctions.substring_index), nameof(ClickHouseFunctions.count_substrings),
        nameof(ClickHouseFunctions.extract_groups),
        nameof(ClickHouseFunctions.normalize_utf8_nfc), nameof(ClickHouseFunctions.normalize_utf8_nfd),
        nameof(ClickHouseFunctions.normalize_utf8_nfkc), nameof(ClickHouseFunctions.normalize_utf8_nfkd),
        nameof(ClickHouseFunctions.format_date_time), nameof(ClickHouseFunctions.parse_date_time),
        nameof(ClickHouseFunctions.parse_date_time_best_effort),
        nameof(ClickHouseFunctions.now), nameof(ClickHouseFunctions.today),
        nameof(ClickHouseFunctions.yesterday),
        nameof(ClickHouseFunctions.array_concat), nameof(ClickHouseFunctions.array_flatten),
        nameof(ClickHouseFunctions.array_uniq), nameof(ClickHouseFunctions.array_intersect),
        nameof(ClickHouseFunctions.array_union), nameof(ClickHouseFunctions.array_except),
        nameof(ClickHouseFunctions.array_symmetric_difference),
        nameof(ClickHouseFunctions.array_reduce),
        nameof(ClickHouseFunctions.array_reduce_in_ranges),
        nameof(ClickHouseFunctions.map), nameof(ClickHouseFunctions.map_keys),
        nameof(ClickHouseFunctions.map_values), nameof(ClickHouseFunctions.map_contains_key),
        nameof(ClickHouseFunctions.map_contains_value), nameof(ClickHouseFunctions.map_add),
        nameof(ClickHouseFunctions.map_concat), nameof(ClickHouseFunctions.map_filter),
        nameof(ClickHouseFunctions.map_apply), nameof(ClickHouseFunctions.map_all),
        nameof(ClickHouseFunctions.map_exists), nameof(ClickHouseFunctions.map_sort),
        nameof(ClickHouseFunctions.group_bitmap), nameof(ClickHouseFunctions.group_bitmap_and),
        nameof(ClickHouseFunctions.group_bitmap_or), nameof(ClickHouseFunctions.group_bitmap_xor),
        nameof(ClickHouseFunctions.sum_map), nameof(ClickHouseFunctions.sum_map_filtered),
        nameof(ClickHouseFunctions.tuple_concat), nameof(ClickHouseFunctions.tuple_names),
        nameof(ClickHouseFunctions.untuple), nameof(ClickHouseFunctions.flatten_tuple),
        nameof(ClickHouseFunctions.tuple_to_name_value_pairs),
        nameof(ClickHouseFunctions.tuple_plus), nameof(ClickHouseFunctions.tuple_minus),
        nameof(ClickHouseFunctions.tuple_multiply), nameof(ClickHouseFunctions.tuple_divide),
        nameof(ClickHouseFunctions.tuple_hamming_distance), nameof(ClickHouseFunctions.dot_product),
        nameof(ClickHouseFunctions.map_subtract), nameof(ClickHouseFunctions.map_update),
        nameof(ClickHouseFunctions.map_reverse_sort), nameof(ClickHouseFunctions.map_partial_sort),
        nameof(ClickHouseFunctions.map_from_arrays), nameof(ClickHouseFunctions.map_populate_series),
        nameof(ClickHouseFunctions.extract_key_value_pairs),
        nameof(ClickHouseFunctions.md5), nameof(ClickHouseFunctions.sha1),
        nameof(ClickHouseFunctions.sha256), nameof(ClickHouseFunctions.sha512),
        nameof(ClickHouseFunctions.xx_hash32), nameof(ClickHouseFunctions.xx_hash64),
        nameof(ClickHouseFunctions.xxh3), nameof(ClickHouseFunctions.city_hash64),
        nameof(ClickHouseFunctions.sip_hash64), nameof(ClickHouseFunctions.sip_hash128),
        nameof(ClickHouseFunctions.murmur_hash2_32), nameof(ClickHouseFunctions.murmur_hash2_64),
        nameof(ClickHouseFunctions.murmur_hash3_32), nameof(ClickHouseFunctions.murmur_hash3_64),
        nameof(ClickHouseFunctions.murmur_hash3_128),
        nameof(ClickHouseFunctions.sha224), nameof(ClickHouseFunctions.sha384),
        nameof(ClickHouseFunctions.sha512_256), nameof(ClickHouseFunctions.blake3),
        nameof(ClickHouseFunctions.md4), nameof(ClickHouseFunctions.ripemd160),
        nameof(ClickHouseFunctions.keccak256),
        nameof(ClickHouseFunctions.farm_hash64), nameof(ClickHouseFunctions.metro_hash64),
        nameof(ClickHouseFunctions.java_hash), nameof(ClickHouseFunctions.hive_hash),
        nameof(ClickHouseFunctions.half_md5), nameof(ClickHouseFunctions.url_hash),
        nameof(ClickHouseFunctions.wy_hash64),
        nameof(ClickHouseFunctions.int_hash32), nameof(ClickHouseFunctions.int_hash64),
        nameof(ClickHouseFunctions.ngram_min_hash),
        nameof(ClickHouseFunctions.ngram_min_hash_arg),
        nameof(ClickHouseFunctions.ngram_sim_hash),
        nameof(ClickHouseFunctions.word_shingle_min_hash),
        nameof(ClickHouseFunctions.word_shingle_min_hash_arg),
        nameof(ClickHouseFunctions.word_shingle_sim_hash),
        nameof(ClickHouseFunctions.ngram_sim_hash_arg),
        nameof(ClickHouseFunctions.word_shingle_sim_hash_arg),
        nameof(ClickHouseFunctions.generate_ulid),
        // Encoding family (T286b F11).
        nameof(ClickHouseFunctions.bin), nameof(ClickHouseFunctions.unbin),
        nameof(ClickHouseFunctions.base32_encode), nameof(ClickHouseFunctions.base32_decode),
        nameof(ClickHouseFunctions.base58_encode), nameof(ClickHouseFunctions.base58_decode),
        nameof(ClickHouseFunctions.base64_url_encode), nameof(ClickHouseFunctions.base64_url_decode),
        nameof(ClickHouseFunctions.bech32_encode), nameof(ClickHouseFunctions.bech32_decode),
        nameof(ClickHouseFunctions.bitmask_to_array), nameof(ClickHouseFunctions.bitmask_to_list),
        nameof(ClickHouseFunctions.bit_positions_to_array),
        nameof(ClickHouseFunctions.sqid_encode), nameof(ClickHouseFunctions.sqid_decode),
        nameof(ClickHouseFunctions.hilbert_encode), nameof(ClickHouseFunctions.hilbert_decode),
        nameof(ClickHouseFunctions.morton_encode), nameof(ClickHouseFunctions.morton_decode),
        // UUID/ULID/snowflake family (T286b F12).
        nameof(ClickHouseFunctions.uuid_num_to_string), nameof(ClickHouseFunctions.uuid_string_to_num),
        nameof(ClickHouseFunctions.uuid_to_num), nameof(ClickHouseFunctions.uuid_v7_to_datetime),
        nameof(ClickHouseFunctions.to_uuid), nameof(ClickHouseFunctions.to_uuid_or_default),
        nameof(ClickHouseFunctions.to_uuid_or_null), nameof(ClickHouseFunctions.ulid_string_to_datetime),
        nameof(ClickHouseFunctions.generate_snowflake_id),
        nameof(ClickHouseFunctions.date_time_to_snowflake_id),
        nameof(ClickHouseFunctions.date_time64_to_snowflake_id),
        nameof(ClickHouseFunctions.date_time_to_uuid_v7),
        nameof(ClickHouseFunctions.snowflake_id_to_date_time),
        nameof(ClickHouseFunctions.snowflake_id_to_date_time64),
        // URL/IP family (T286b F13). Every member is a plain scalar call; the IPv6 binary forms are
        // rendered through the dialect's hex/toFixedString arms, and the URL/IP names use the vendor's
        // exact casing (URLHierarchy/URLPathHierarchy), not the catalogue's lower-camel spelling.
        nameof(ClickHouseFunctions.domain), nameof(ClickHouseFunctions.protocol),
        nameof(ClickHouseFunctions.path), nameof(ClickHouseFunctions.query_string),
        nameof(ClickHouseFunctions.fragment), nameof(ClickHouseFunctions.port),
        nameof(ClickHouseFunctions.cut_url_parameter), nameof(ClickHouseFunctions.top_level_domain),
        nameof(ClickHouseFunctions.encode_url_component), nameof(ClickHouseFunctions.decode_url_component),
        nameof(ClickHouseFunctions.encode_url_form_component),
        nameof(ClickHouseFunctions.decode_url_form_component),
        nameof(ClickHouseFunctions.domain_without_www),
        nameof(ClickHouseFunctions.first_significant_subdomain),
        nameof(ClickHouseFunctions.first_significant_subdomain_custom),
        nameof(ClickHouseFunctions.path_full), nameof(ClickHouseFunctions.query_string_and_fragment),
        nameof(ClickHouseFunctions.cut_www), nameof(ClickHouseFunctions.cut_query_string),
        nameof(ClickHouseFunctions.cut_fragment),
        nameof(ClickHouseFunctions.cut_query_string_and_fragment),
        nameof(ClickHouseFunctions.cut_to_first_significant_subdomain),
        nameof(ClickHouseFunctions.cut_to_first_significant_subdomain_with_www),
        nameof(ClickHouseFunctions.cut_to_first_significant_subdomain_custom),
        nameof(ClickHouseFunctions.cut_to_first_significant_subdomain_custom_with_www),
        nameof(ClickHouseFunctions.url_hierarchy), nameof(ClickHouseFunctions.url_path_hierarchy),
        nameof(ClickHouseFunctions.extract_url_parameter),
        nameof(ClickHouseFunctions.extract_url_parameter_names),
        nameof(ClickHouseFunctions.ipv4_num_to_string),
        nameof(ClickHouseFunctions.ipv4_num_to_string_class_c),
        nameof(ClickHouseFunctions.ipv4_string_to_num),
        nameof(ClickHouseFunctions.ipv4_string_to_num_or_default),
        nameof(ClickHouseFunctions.ipv4_string_to_num_or_null),
        nameof(ClickHouseFunctions.ipv6_num_to_string), nameof(ClickHouseFunctions.ipv6_string_to_num),
        nameof(ClickHouseFunctions.ipv6_string_to_num_or_default),
        nameof(ClickHouseFunctions.ipv6_string_to_num_or_null),
        nameof(ClickHouseFunctions.cut_ipv6),
        nameof(ClickHouseFunctions.is_ipv4_string), nameof(ClickHouseFunctions.is_ipv6_string),
        nameof(ClickHouseFunctions.is_ip_address_in_range),
        nameof(ClickHouseFunctions.to_ipv4), nameof(ClickHouseFunctions.to_ipv6),
        nameof(ClickHouseFunctions.ipv4_to_ipv6),
        nameof(ClickHouseFunctions.ipv4_cidr_to_range),
        nameof(ClickHouseFunctions.ipv6_cidr_to_range),
        // Bit family (T286b F14). The rendering rules are positional (the two-operand bitwise
        // functions and bitCount/hamming widen their operands to UInt64 exactly, while the
        // width-preserving members keep the caller's width and widen a UInt16 result with toInt32).
        nameof(ClickHouseFunctions.bit_and), nameof(ClickHouseFunctions.bit_or),
        nameof(ClickHouseFunctions.bit_xor), nameof(ClickHouseFunctions.bit_not),
        nameof(ClickHouseFunctions.bit_shift_left), nameof(ClickHouseFunctions.bit_shift_right),
        nameof(ClickHouseFunctions.bit_rotate_left), nameof(ClickHouseFunctions.bit_rotate_right),
        nameof(ClickHouseFunctions.bit_count), nameof(ClickHouseFunctions.bit_test),
        nameof(ClickHouseFunctions.bit_test_all), nameof(ClickHouseFunctions.bit_test_any),
        nameof(ClickHouseFunctions.bit_hamming_distance),
        nameof(ClickHouseFunctions.log2), nameof(ClickHouseFunctions.cbrt),
        nameof(ClickHouseFunctions.exp2), nameof(ClickHouseFunctions.exp10),
        nameof(ClickHouseFunctions.erf), nameof(ClickHouseFunctions.erfc),
        nameof(ClickHouseFunctions.lgamma), nameof(ClickHouseFunctions.tgamma),
        nameof(ClickHouseFunctions.gcd), nameof(ClickHouseFunctions.lcm),
        nameof(ClickHouseFunctions.is_finite), nameof(ClickHouseFunctions.is_infinite),
        nameof(ClickHouseFunctions.is_nan),
        nameof(ClickHouseFunctions.hypot), nameof(ClickHouseFunctions.int_div),
        nameof(ClickHouseFunctions.int_exp2), nameof(ClickHouseFunctions.int_exp10),
        nameof(ClickHouseFunctions.sigmoid), nameof(ClickHouseFunctions.sqr),
        nameof(ClickHouseFunctions.round_age), nameof(ClickHouseFunctions.round_bankers),
        nameof(ClickHouseFunctions.round_down), nameof(ClickHouseFunctions.round_duration),
        nameof(ClickHouseFunctions.round_to_exp2),
        nameof(ClickHouseFunctions.make_date), nameof(ClickHouseFunctions.make_date32),
        nameof(ClickHouseFunctions.make_date_time),
        nameof(ClickHouseFunctions.now64), nameof(ClickHouseFunctions.now_in_block),
        nameof(ClickHouseFunctions.time_slot), nameof(ClickHouseFunctions.time_slots),
        nameof(ClickHouseFunctions.to_start_of_fifteen_minutes),
        nameof(ClickHouseFunctions.to_start_of_five_minutes),
        nameof(ClickHouseFunctions.to_start_of_ten_minutes),
        nameof(ClickHouseFunctions.to_start_of_interval),
        nameof(ClickHouseFunctions.to_iso_year), nameof(ClickHouseFunctions.to_week),
        nameof(ClickHouseFunctions.to_year_week), nameof(ClickHouseFunctions.to_last_day_of_week),
        nameof(ClickHouseFunctions.to_days_in_month),
        nameof(ClickHouseFunctions.to_modified_julian_day),
        nameof(ClickHouseFunctions.age), nameof(ClickHouseFunctions.date_name),
        nameof(ClickHouseFunctions.server_timezone), nameof(ClickHouseFunctions.timezone_of),
        nameof(ClickHouseFunctions.timezone_offset), nameof(ClickHouseFunctions.to_time),
        nameof(ClickHouseFunctions.timestamp),
        nameof(ClickHouseFunctions.from_unix_timestamp),
        nameof(ClickHouseFunctions.from_unix_timestamp64_milli),
        nameof(ClickHouseFunctions.from_unix_timestamp64_micro),
        nameof(ClickHouseFunctions.from_utc_timestamp),
        nameof(ClickHouseFunctions.to_time64),
        nameof(ClickHouseFunctions.from_unix_timestamp64_nano),
        // Type-conversion family (T286b F15): the 12 widths with their OrNull/OrDefault
        // variants, the four scalar forms, the checked accurateCast targets and the
        // bit-pattern reinterpretAs members, plus the deterministic refusals.
        nameof(ClickHouseFunctions.to_int8),
        nameof(ClickHouseFunctions.to_int8_or_null),
        nameof(ClickHouseFunctions.to_int8_or_default),
        nameof(ClickHouseFunctions.to_int16),
        nameof(ClickHouseFunctions.to_int16_or_null),
        nameof(ClickHouseFunctions.to_int16_or_default),
        nameof(ClickHouseFunctions.to_int32),
        nameof(ClickHouseFunctions.to_int32_or_null),
        nameof(ClickHouseFunctions.to_int32_or_default),
        nameof(ClickHouseFunctions.to_int64),
        nameof(ClickHouseFunctions.to_int64_or_null),
        nameof(ClickHouseFunctions.to_int64_or_default),
        nameof(ClickHouseFunctions.to_uint8),
        nameof(ClickHouseFunctions.to_uint8_or_null),
        nameof(ClickHouseFunctions.to_uint8_or_default),
        nameof(ClickHouseFunctions.to_uint16),
        nameof(ClickHouseFunctions.to_uint16_or_null),
        nameof(ClickHouseFunctions.to_uint16_or_default),
        nameof(ClickHouseFunctions.to_uint32),
        nameof(ClickHouseFunctions.to_uint32_or_null),
        nameof(ClickHouseFunctions.to_uint32_or_default),
        nameof(ClickHouseFunctions.to_uint64),
        nameof(ClickHouseFunctions.to_uint64_or_null),
        nameof(ClickHouseFunctions.to_uint64_or_default),
        nameof(ClickHouseFunctions.to_float32),
        nameof(ClickHouseFunctions.to_float32_or_null),
        nameof(ClickHouseFunctions.to_float32_or_default),
        nameof(ClickHouseFunctions.to_float64),
        nameof(ClickHouseFunctions.to_float64_or_null),
        nameof(ClickHouseFunctions.to_float64_or_default),
        nameof(ClickHouseFunctions.to_decimal32),
        nameof(ClickHouseFunctions.to_decimal32_or_null),
        nameof(ClickHouseFunctions.to_decimal32_or_default),
        nameof(ClickHouseFunctions.to_decimal64),
        nameof(ClickHouseFunctions.to_decimal64_or_null),
        nameof(ClickHouseFunctions.to_decimal64_or_default),
        nameof(ClickHouseFunctions.to_string),
        nameof(ClickHouseFunctions.to_bool),
        nameof(ClickHouseFunctions.to_fixed_string),
        nameof(ClickHouseFunctions.to_low_cardinality),
        nameof(ClickHouseFunctions.accurate_cast_int8),
        nameof(ClickHouseFunctions.accurate_cast_int8_or_null),
        nameof(ClickHouseFunctions.accurate_cast_int8_or_default),
        nameof(ClickHouseFunctions.accurate_cast_int16),
        nameof(ClickHouseFunctions.accurate_cast_int16_or_null),
        nameof(ClickHouseFunctions.accurate_cast_int16_or_default),
        nameof(ClickHouseFunctions.accurate_cast_int32),
        nameof(ClickHouseFunctions.accurate_cast_int32_or_null),
        nameof(ClickHouseFunctions.accurate_cast_int32_or_default),
        nameof(ClickHouseFunctions.accurate_cast_int64),
        nameof(ClickHouseFunctions.accurate_cast_int64_or_null),
        nameof(ClickHouseFunctions.accurate_cast_int64_or_default),
        nameof(ClickHouseFunctions.accurate_cast_uint8),
        nameof(ClickHouseFunctions.accurate_cast_uint8_or_null),
        nameof(ClickHouseFunctions.accurate_cast_uint8_or_default),
        nameof(ClickHouseFunctions.accurate_cast_uint16),
        nameof(ClickHouseFunctions.accurate_cast_uint16_or_null),
        nameof(ClickHouseFunctions.accurate_cast_uint16_or_default),
        nameof(ClickHouseFunctions.accurate_cast_uint32),
        nameof(ClickHouseFunctions.accurate_cast_uint32_or_null),
        nameof(ClickHouseFunctions.accurate_cast_uint32_or_default),
        nameof(ClickHouseFunctions.accurate_cast_uint64),
        nameof(ClickHouseFunctions.accurate_cast_uint64_or_null),
        nameof(ClickHouseFunctions.accurate_cast_uint64_or_default),
        nameof(ClickHouseFunctions.accurate_cast_float32),
        nameof(ClickHouseFunctions.accurate_cast_float32_or_null),
        nameof(ClickHouseFunctions.accurate_cast_float32_or_default),
        nameof(ClickHouseFunctions.accurate_cast_float64),
        nameof(ClickHouseFunctions.accurate_cast_float64_or_null),
        nameof(ClickHouseFunctions.accurate_cast_float64_or_default),
        nameof(ClickHouseFunctions.accurate_cast_decimal32),
        nameof(ClickHouseFunctions.accurate_cast_decimal32_or_null),
        nameof(ClickHouseFunctions.accurate_cast_decimal32_or_default),
        nameof(ClickHouseFunctions.accurate_cast_decimal64),
        nameof(ClickHouseFunctions.accurate_cast_decimal64_or_null),
        nameof(ClickHouseFunctions.accurate_cast_decimal64_or_default),
        nameof(ClickHouseFunctions.reinterpret_as_uint8),
        nameof(ClickHouseFunctions.reinterpret_as_uint16),
        nameof(ClickHouseFunctions.reinterpret_as_uint32),
        nameof(ClickHouseFunctions.reinterpret_as_uint64),
        nameof(ClickHouseFunctions.reinterpret_as_int8),
        nameof(ClickHouseFunctions.reinterpret_as_int16),
        nameof(ClickHouseFunctions.reinterpret_as_int32),
        nameof(ClickHouseFunctions.reinterpret_as_int64),
        nameof(ClickHouseFunctions.reinterpret_as_float32),
        nameof(ClickHouseFunctions.reinterpret_as_float64),
        nameof(ClickHouseFunctions.reinterpret_as_date),
        nameof(ClickHouseFunctions.reinterpret_as_date_time),
        nameof(ClickHouseFunctions.reinterpret_as_string),
        nameof(ClickHouseFunctions.reinterpret_as_fixed_string),
        nameof(ClickHouseFunctions.reinterpret_as_uuid),
        nameof(ClickHouseFunctions.to_int128),
        nameof(ClickHouseFunctions.to_uint128),
        nameof(ClickHouseFunctions.to_int256),
        nameof(ClickHouseFunctions.to_uint256),
        nameof(ClickHouseFunctions.to_decimal128),
        nameof(ClickHouseFunctions.to_decimal256),
        nameof(ClickHouseFunctions.to_interval_nanosecond),
        nameof(ClickHouseFunctions.to_interval_microsecond),
        nameof(ClickHouseFunctions.to_interval_millisecond),
        nameof(ClickHouseFunctions.to_interval_second),
        nameof(ClickHouseFunctions.to_interval_minute),
        nameof(ClickHouseFunctions.to_interval_hour),
        nameof(ClickHouseFunctions.to_interval_day),
        nameof(ClickHouseFunctions.to_interval_week),
        nameof(ClickHouseFunctions.to_interval_month),
        nameof(ClickHouseFunctions.to_interval_quarter),
        nameof(ClickHouseFunctions.to_interval_year),
        nameof(ClickHouseFunctions.reinterpret_as_uint128),
        nameof(ClickHouseFunctions.reinterpret_as_uint256),
        nameof(ClickHouseFunctions.reinterpret_as_int128),
        nameof(ClickHouseFunctions.reinterpret_as_int256),
        nameof(ClickHouseFunctions.from_unix_timestamp64_nano)
    };

    /// <summary>
    /// The conversion members whose scale argument selects the vendor target type: the value is the
    /// maximum scale of <c>Decimal32</c> (9) or <c>Decimal64</c> (18). The scale must be a compile-time
    /// constant so the declared result shape is known before execution.
    /// </summary>
    private static readonly Dictionary<string, int> ConversionScales = new(StringComparer.Ordinal)
    {
        [nameof(ClickHouseFunctions.to_decimal32)] = 9,
        [nameof(ClickHouseFunctions.to_decimal32_or_null)] = 9,
        [nameof(ClickHouseFunctions.to_decimal32_or_default)] = 9,
        [nameof(ClickHouseFunctions.to_decimal64)] = 18,
        [nameof(ClickHouseFunctions.to_decimal64_or_null)] = 18,
        [nameof(ClickHouseFunctions.to_decimal64_or_default)] = 18,
        [nameof(ClickHouseFunctions.accurate_cast_decimal32)] = 9,
        [nameof(ClickHouseFunctions.accurate_cast_decimal32_or_null)] = 9,
        [nameof(ClickHouseFunctions.accurate_cast_decimal32_or_default)] = 9,
        [nameof(ClickHouseFunctions.accurate_cast_decimal64)] = 18,
        [nameof(ClickHouseFunctions.accurate_cast_decimal64_or_null)] = 18,
        [nameof(ClickHouseFunctions.accurate_cast_decimal64_or_default)] = 18
    };

    /// <summary>
    /// The conversion members whose optional explicit default is typed wider than the vendor target:
    /// the constant is range-checked so an out-of-range default is refused instead of being narrowed
    /// silently by the server.
    /// </summary>
    private static readonly HashSet<string> NarrowDefaultConversions = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.to_int8_or_default),
        nameof(ClickHouseFunctions.to_uint16_or_default)
    };

    /// <summary>
    /// Members refused for a demonstrated CLR-shape limitation. The refusal is raised before the
    /// dialect gate so the caller always gets the precise technical reason, on every provider.
    /// </summary>
    private static readonly Dictionary<string, string> Refused = new(StringComparer.Ordinal)
    {
        [nameof(ClickHouseFunctions.from_utc_timestamp)] =
            "The from_utc_timestamp function is refused: fromUTCTimestamp returns a plain DateTime " +
            "without a zone in its type whose wall clock is the target-zone time and whose underlying " +
            "instant is shifted by the zone offset, and System.DateTime cannot carry the target zone " +
            "that gives the value meaning.",
        [nameof(ClickHouseFunctions.to_time64)] =
            "The to_time64 function is refused: toTime64 returns the ClickHouse Time64 type, which " +
            "System.TimeSpan cannot materialize through the ClickHouse provider's Int64 duration " +
            "mapping (SupportsNativeDuration is false).",
        [nameof(ClickHouseFunctions.from_unix_timestamp64_nano)] =
            "The from_unix_timestamp64_nano function is refused: fromUnixTimestamp64Nano returns " +
            "DateTime64(9), whose nanosecond digits cannot be represented by System.DateTime " +
            "(100 ns tick resolution), so the value would be silently truncated.",
        [nameof(ClickHouseFunctions.array_reduce)] =
            "The array_reduce function is refused: the aggregate is named by a runtime SQL string " +
            "(for example 'sum' or 'uniqUpTo(6)'), so there is no typed aggregate contract that can be " +
            "closed by a signature and the string would reach the server as an opaque SQL fragment. " +
            "Use the typed array functions (array_sum, array_min, array_max, array_avg, array_product).",
        [nameof(ClickHouseFunctions.array_reduce_in_ranges)] =
            "The array_reduce_in_ranges function is refused: the aggregate is named by a runtime SQL " +
            "string (for example 'sum' or 'uniqUpTo(6)'), so there is no typed aggregate contract that " +
            "can be closed by a signature and the string would reach the server as an opaque SQL " +
            "fragment; the (start, end) range pairs do not remove that contract. Use the typed array " +
            "functions over array_slice for the common aggregates.",
        [nameof(ClickHouseFunctions.ngram_sim_hash_arg)] =
            "The ngram_sim_hash_arg function is refused: ClickHouse has no ngramSimHashArg " +
            "function — ngramSimHash is a single UInt64 aggregate hash and ngramMinHashArg is the " +
            "only arg-returning n-gram function. The name is absent from system.functions on " +
            "25.8.33.6 and from the vendor hash-function reference, and fails with UNKNOWN_FUNCTION.",
        [nameof(ClickHouseFunctions.word_shingle_sim_hash_arg)] =
            "The word_shingle_sim_hash_arg function is refused: ClickHouse has no " +
            "wordShingleSimHashArg function — wordShingleSimHash is a single UInt64 aggregate hash " +
            "and wordShingleMinHashArg is the only arg-returning word-shingle function. The name is " +
            "absent from system.functions on 25.8.33.6 and from the vendor hash-function reference, " +
            "and fails with UNKNOWN_FUNCTION.",
        [nameof(ClickHouseFunctions.untuple)] =
            "The untuple function is refused: untuple performs a syntactic substitution of the tuple " +
            "elements in the call location, so one projected expression becomes a dynamic set of " +
            "columns whose count and types follow the tuple and whose names the vendor declares " +
            "implementation-specific. NextORM maps one projection expression to exactly one result " +
            "column, so no CLR return type can name or declare that shape. Project the components " +
            "explicitly (tuple.Item1/tuple.Item2) instead.",
        [nameof(ClickHouseFunctions.to_int128)] =
            "The to_int128 function is refused: ClickHouse Int128 has no exact fixed-width CLR integer type; " +
            "the driver surfaces it as System.Numerics.BigInteger, but the shared row materializer has no " +
            "BigInteger accessor (SelectExpression.GetDataRecordMethod), so the declared shape cannot be " +
            "materialized and adding one is a shared-contract change outside this unit.",

        [nameof(ClickHouseFunctions.to_uint128)] =
            "The to_uint128 function is refused: ClickHouse UInt128 has no exact fixed-width CLR integer type; " +
            "the driver surfaces it as System.Numerics.BigInteger, but the shared row materializer has no " +
            "BigInteger accessor (SelectExpression.GetDataRecordMethod), so the declared shape cannot be " +
            "materialized and adding one is a shared-contract change outside this unit.",

        [nameof(ClickHouseFunctions.to_int256)] =
            "The to_int256 function is refused: ClickHouse Int256 has no exact fixed-width CLR integer type; " +
            "the driver surfaces it as System.Numerics.BigInteger, but the shared row materializer has no " +
            "BigInteger accessor (SelectExpression.GetDataRecordMethod), so the declared shape cannot be " +
            "materialized and adding one is a shared-contract change outside this unit.",

        [nameof(ClickHouseFunctions.to_uint256)] =
            "The to_uint256 function is refused: ClickHouse UInt256 has no exact fixed-width CLR integer type; " +
            "the driver surfaces it as System.Numerics.BigInteger, but the shared row materializer has no " +
            "BigInteger accessor (SelectExpression.GetDataRecordMethod), so the declared shape cannot be " +
            "materialized and adding one is a shared-contract change outside this unit.",

        [nameof(ClickHouseFunctions.to_decimal128)] =
            "The to_decimal128 function is refused: Decimal128 carries up to 38 significant decimal digits, " +
            "while System.Decimal's 128-bit storage has a 96-bit mantissa (28-29 digits); a value above that " +
            "range cannot be materialized without silent precision loss or OverflowException.",

        [nameof(ClickHouseFunctions.to_decimal256)] =
            "The to_decimal256 function is refused: Decimal256 carries up to 76 significant decimal digits, " +
            "while System.Decimal's 128-bit storage has a 96-bit mantissa (28-29 digits); a value above that " +
            "range cannot be materialized without silent precision loss or OverflowException.",

        [nameof(ClickHouseFunctions.to_interval_nanosecond)] =
            "The to_interval_nanosecond function is refused: The vendor returns the ClickHouse-only " +
            "IntervalNanosecond type, which has no CLR representation and is usable only inside date " +
            "arithmetic; the provider driver itself cannot materialize it (ArgumentException 'Unknown type: " +
            "IntervalNanosecond' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_microsecond)] =
            "The to_interval_microsecond function is refused: The vendor returns the ClickHouse-only " +
            "IntervalMicrosecond type, which has no CLR representation and is usable only inside date " +
            "arithmetic; the provider driver itself cannot materialize it (ArgumentException 'Unknown type: " +
            "IntervalMicrosecond' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_millisecond)] =
            "The to_interval_millisecond function is refused: The vendor returns the ClickHouse-only " +
            "IntervalMillisecond type, which has no CLR representation and is usable only inside date " +
            "arithmetic; the provider driver itself cannot materialize it (ArgumentException 'Unknown type: " +
            "IntervalMillisecond' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_second)] =
            "The to_interval_second function is refused: The vendor returns the ClickHouse-only IntervalSecond " +
            "type, which has no CLR representation and is usable only inside date arithmetic; the provider " +
            "driver itself cannot materialize it (ArgumentException 'Unknown type: IntervalSecond' on " +
            "25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_minute)] =
            "The to_interval_minute function is refused: The vendor returns the ClickHouse-only IntervalMinute " +
            "type, which has no CLR representation and is usable only inside date arithmetic; the provider " +
            "driver itself cannot materialize it (ArgumentException 'Unknown type: IntervalMinute' on " +
            "25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_hour)] =
            "The to_interval_hour function is refused: The vendor returns the ClickHouse-only IntervalHour " +
            "type, which has no CLR representation and is usable only inside date arithmetic; the provider " +
            "driver itself cannot materialize it (ArgumentException 'Unknown type: IntervalHour' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_day)] =
            "The to_interval_day function is refused: The vendor returns the ClickHouse-only IntervalDay type, " +
            "which has no CLR representation and is usable only inside date arithmetic; the provider driver " +
            "itself cannot materialize it (ArgumentException 'Unknown type: IntervalDay' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_week)] =
            "The to_interval_week function is refused: The vendor returns the ClickHouse-only IntervalWeek " +
            "type, which has no CLR representation and is usable only inside date arithmetic; the provider " +
            "driver itself cannot materialize it (ArgumentException 'Unknown type: IntervalWeek' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_month)] =
            "The to_interval_month function is refused: The vendor returns the ClickHouse-only IntervalMonth " +
            "type, which has no CLR representation and is usable only inside date arithmetic; the provider " +
            "driver itself cannot materialize it (ArgumentException 'Unknown type: IntervalMonth' on " +
            "25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_quarter)] =
            "The to_interval_quarter function is refused: The vendor returns the ClickHouse-only " +
            "IntervalQuarter type, which has no CLR representation and is usable only inside date arithmetic; " +
            "the provider driver itself cannot materialize it (ArgumentException 'Unknown type: " +
            "IntervalQuarter' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.to_interval_year)] =
            "The to_interval_year function is refused: The vendor returns the ClickHouse-only IntervalYear " +
            "type, which has no CLR representation and is usable only inside date arithmetic; the provider " +
            "driver itself cannot materialize it (ArgumentException 'Unknown type: IntervalYear' on 25.8.33.6).",

        [nameof(ClickHouseFunctions.reinterpret_as_uint128)] =
            "The reinterpret_as_uint128 function is refused: The vendor returns UInt128, which the driver " +
            "surfaces as System.Numerics.BigInteger; the shared row materializer has no BigInteger accessor, so " +
            "the bit pattern cannot be materialized into a fixed-width CLR integer.",

        [nameof(ClickHouseFunctions.reinterpret_as_uint256)] =
            "The reinterpret_as_uint256 function is refused: The vendor returns UInt256, which the driver " +
            "surfaces as System.Numerics.BigInteger; the shared row materializer has no BigInteger accessor, so " +
            "the bit pattern cannot be materialized into a fixed-width CLR integer.",

        [nameof(ClickHouseFunctions.reinterpret_as_int128)] =
            "The reinterpret_as_int128 function is refused: The vendor returns Int128, which the driver " +
            "surfaces as System.Numerics.BigInteger; the shared row materializer has no BigInteger accessor, so " +
            "the bit pattern cannot be materialized into a fixed-width CLR integer.",

        [nameof(ClickHouseFunctions.reinterpret_as_int256)] =
            "The reinterpret_as_int256 function is refused: The vendor returns Int256, which the driver " +
            "surfaces as System.Numerics.BigInteger; the shared row materializer has no BigInteger accessor, so " +
            "the bit pattern cannot be materialized into a fixed-width CLR integer."
    };

    /// <summary>
    /// The interval units <c>toStartOfInterval</c> accepts for a <see cref="DateTime"/> argument.
    /// Sub-second units are not in the set: the server rejects them for a <c>DateTime</c> column.
    /// </summary>
    private static readonly Dictionary<string, string> IntervalUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["second"] = "SECOND",
        ["minute"] = "MINUTE",
        ["hour"] = "HOUR",
        ["day"] = "DAY",
        ["week"] = "WEEK",
        ["month"] = "MONTH",
        ["quarter"] = "QUARTER",
        ["year"] = "YEAR"
    };

    private static readonly HashSet<string> ParamsArrayFunctions = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.array_concat), nameof(ClickHouseFunctions.array_intersect),
        nameof(ClickHouseFunctions.array_union), nameof(ClickHouseFunctions.array_except),
        nameof(ClickHouseFunctions.array_symmetric_difference), nameof(ClickHouseFunctions.map_concat),
        nameof(ClickHouseFunctions.sqid_encode)
    };

    private static readonly HashSet<string> MapLambdaFunctions = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.map_filter), nameof(ClickHouseFunctions.map_apply),
        nameof(ClickHouseFunctions.map_all), nameof(ClickHouseFunctions.map_exists)
    };

    /// <summary>
    /// The tuple-family members whose result is a typed <see cref="Tuple"/> (or an array of typed
    /// tuples): every one of them is normalised with a <c>cast(... as the declared CLR shape)</c>,
    /// because ClickHouse promotes tuple arithmetic elements (Int32 + Int32 is Int64) and the
    /// declared result has to stay exact. <c>flatten_tuple</c> additionally needs the synthetic named
    /// tuple input cast; <c>untuple</c> is in <see cref="Refused"/> and never reaches this set.
    /// </summary>
    private static readonly HashSet<string> TupleMembers = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.tuple_concat), nameof(ClickHouseFunctions.tuple_names),
        nameof(ClickHouseFunctions.flatten_tuple),
        nameof(ClickHouseFunctions.tuple_to_name_value_pairs),
        nameof(ClickHouseFunctions.tuple_plus), nameof(ClickHouseFunctions.tuple_minus),
        nameof(ClickHouseFunctions.tuple_multiply), nameof(ClickHouseFunctions.tuple_divide),
        nameof(ClickHouseFunctions.tuple_hamming_distance), nameof(ClickHouseFunctions.dot_product)
    };

    /// <summary>
    /// The map-family members delivered by T286b F07. Each result is a <c>Map(K, V)</c> lowered to
    /// the projectable <c>arrayZip(mapKeys(...), mapValues(...))</c> key/value array (the T257
    /// map_filter/map_sort shape) and cast to the declared <see cref="Tuple{TKey,TValue}"/> array, and
    /// each map operand goes through <see cref="RenderMapOperand"/>, so an inline <c>map(...)</c>
    /// constructor, a captured dictionary and a map column all work with the same code path as the
    /// existing map helpers.
    /// </summary>
    private static readonly HashSet<string> MapMembers = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.map_subtract), nameof(ClickHouseFunctions.map_update),
        nameof(ClickHouseFunctions.map_reverse_sort), nameof(ClickHouseFunctions.map_partial_sort),
        nameof(ClickHouseFunctions.map_from_arrays), nameof(ClickHouseFunctions.map_populate_series),
        nameof(ClickHouseFunctions.extract_key_value_pairs)
    };

    /// <summary>The integral CLR types the server accepts as a <c>mapPopulateSeries</c> key.</summary>
    private static readonly HashSet<Type> IntegralKeyTypes =
    [
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong)
    ];

    /// <summary>
    /// The CLR scalars the ClickHouse dialect maps to a name identical to the CLR name (<c>Int16</c>,
    /// <c>Int32</c>, <c>UInt32</c>, <c>Int64</c> and <c>DateTime</c>): the mapping is real, not the
    /// dialect's generic <c>Type.Name</c> fallback for an unmapped type, so the resulting string alone
    /// cannot tell them apart and they are admitted by type. <c>float</c> is deliberately absent: the
    /// dialect maps it to <c>Float32</c>, which differs from the CLR name <c>Single</c> and is therefore
    /// already distinguishable from the fallback.
    /// </summary>
    private static readonly HashSet<Type> IdentityMappedTypes =
    [
        typeof(short), typeof(int), typeof(uint), typeof(long), typeof(DateTime)
    ];

    /// <summary>The inline <c>map(key, value, ...)</c> constructor, the only inline map operand source.</summary>
    private const string MapConstructorName = nameof(ClickHouseFunctions.map);

    /// <summary>The map-only sort arm, lowered onto the tuple-array representation.</summary>
    private const string MapSortName = nameof(ClickHouseFunctions.map_sort);

    /// <summary>Base name of the generated tuple lambda identifier of the <c>arrayZip</c> arms.</summary>
    private const string TupleIdentifierBase = "_t";

    /// <summary>Translates a ClickHouse-native call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(ClickHouseFunctions) || !Names.Contains(node.Method.Name))
            return false;

        var name = node.Method.Name;

        if (Refused.TryGetValue(name, out var refusal))
            throw new NotSupportedException(refusal);
        if (ConversionScales.TryGetValue(name, out var maxScale))
            RequireConversionScale(node, name, maxScale);

        if (name == nameof(ClickHouseFunctions.to_fixed_string))
            RequireFixedStringLength(node);

        if (NarrowDefaultConversions.Contains(name) && node.Arguments.Count == 2)
            RequireNarrowDefault(node, name);


        if (name == nameof(ClickHouseFunctions.now64) && node.Arguments.Count == 1)
            RequireRepresentableScale(node.Arguments[0]);

        // The two library-side refusals of the encoding family: sqidEncode() has no typed result for
        // the empty call, and a bech32 witness version is a compile-time 5-bit constant.
        if (name == nameof(ClickHouseFunctions.sqid_encode)
            && node.Arguments is [NewArrayExpression { Expressions.Count: 0 }])
            throw new NotSupportedException(
                "The sqid_encode function requires at least one value: ClickHouse rejects sqidEncode() " +
                "with NUMBER_OF_ARGUMENTS_DOESNT_MATCH, so the empty call has no result to declare.");

        if (name == nameof(ClickHouseFunctions.bech32_encode) && node.Arguments.Count == 3
            && TypeFacts.UnwrapConvert(node.Arguments[2]) is not ConstantExpression { Value: null })
            RequireWitnessVersion(node.Arguments[2]);

        // The tuple and map type/lambda contracts are validated before the provider gate, so the
        // precise technical reason wins on every provider (the F04 refusal precedent).
        if (TupleMembers.Contains(name))
            ValidateTupleMember(node, name);
        else if (MapMembers.Contains(name))
            ValidateMapMember(node, name);

        if (visitor.Dialect.ScalarFunctions is not { } scalars || !scalars.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (name == nameof(ClickHouseFunctions.to_start_of_interval))
        {
            EmitStartOfInterval(visitor, node, scalars);
            return true;
        }

        if (ParamsArrayFunctions.Contains(name))
        {
            EmitParamsArray(visitor, node, scalars, name);
            return true;
        }

        if (name is nameof(ClickHouseFunctions.array_flatten) or nameof(ClickHouseFunctions.array_uniq))
        {
            EmitSingleArray(visitor, node, scalars, name);
            return true;
        }

        if (MapLambdaFunctions.Contains(name) || name == MapSortName)
        {
            EmitMapFunction(visitor, node, scalars, name);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.map))
        {
            EmitMapConstructor(visitor, node, scalars, name);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.sum_map_filtered))
        {
            EmitSumMapFiltered(visitor, node, scalars, name);
            return true;
        }

        if (TupleMembers.Contains(name))
        {
            EmitTupleMember(visitor, node, scalars, name);
            return true;
        }

        if (MapMembers.Contains(name))
        {
            EmitMapMember(visitor, node, scalars, name);
            return true;
        }

        // roundDown takes a constant boundary array as its second argument: a captured/inline array is
        // bound as one Array parameter (the server substitutes it as a constant), while an array column
        // or other SQL expression is rendered as SQL.
        if (name == nameof(ClickHouseFunctions.round_down))
        {
            EmitScalarWithTrailingArray(visitor, node, scalars, name);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.bech32_encode))
        {
            EmitBech32Encode(visitor, node, scalars, name);
            return true;
        }

        if (BitFunctions.Contains(name))
        {
            EmitBitMember(visitor, node, scalars, name);
            return true;
        }

        EmitScalar(visitor, node, scalars, name);
        return true;
    }

    /// <summary>
    /// The bit-family members and the overload-dependent rendering they need. Kept together so the
    /// width rules (exact <c>toUInt64</c> widening for the width-insensitive operations, plain
    /// rendering plus a <c>toInt32</c> result widening for the UInt16 width-preserving overloads) are
    /// in one place instead of being spread over the generic scalar path.
    /// </summary>
    private static readonly HashSet<string> BitFunctions = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.bit_and), nameof(ClickHouseFunctions.bit_or),
        nameof(ClickHouseFunctions.bit_xor), nameof(ClickHouseFunctions.bit_not),
        nameof(ClickHouseFunctions.bit_shift_left), nameof(ClickHouseFunctions.bit_shift_right),
        nameof(ClickHouseFunctions.bit_rotate_left), nameof(ClickHouseFunctions.bit_rotate_right),
        nameof(ClickHouseFunctions.bit_count), nameof(ClickHouseFunctions.bit_test),
        nameof(ClickHouseFunctions.bit_test_all), nameof(ClickHouseFunctions.bit_test_any),
        nameof(ClickHouseFunctions.bit_hamming_distance)
    };

    /// <summary>
    /// Renders a bit-family call; the overload signature carries the width decision:
    /// <list type="bullet">
    /// <item><c>bitAnd</c>/<c>bitOr</c>/<c>bitXor</c>, <c>bitCount</c> and the integer
    /// <c>bitHammingDistance</c> are width-insensitive above the input's width, so every operand is
    /// widened exactly to <c>UInt64</c> and the declared CLR result is exact (the zero extension
    /// cannot change a bitwise result or a popcount);</item>
    /// <item><c>bitNot</c>/<c>bitShift*</c>/<c>bitRotate*</c> over a <c>UInt16</c> are rendered
    /// natively and the whole call is wrapped in <c>toInt32</c>, an exact widening of the
    /// 0..65535 result the shared row reader cannot otherwise materialize;</item>
    /// <item><c>bitTest*</c> renders natively (the <c>UInt8</c> 0/1 result is surfaced as
    /// <see cref="bool"/>).</item>
    /// </list>
    /// </summary>
    private static void EmitBitMember(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        if (name is nameof(ClickHouseFunctions.bit_test_all) or nameof(ClickHouseFunctions.bit_test_any))
        {
            EmitBitTestFamily(visitor, node, scalars, name);
            return;
        }

        var arguments = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
                visitor.Visit(arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[arguments.Count];
        for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(arguments[i]);

        if (name is nameof(ClickHouseFunctions.bit_and)
            or nameof(ClickHouseFunctions.bit_or)
            or nameof(ClickHouseFunctions.bit_xor))
        {
            for (var i = 0; i < rendered.Length; i++)
                rendered[i] = $"toUInt64({rendered[i]})";
        }
        else if (name == nameof(ClickHouseFunctions.bit_count))
        {
            rendered[0] = $"toUInt64({rendered[0]})";
        }
        else if (name == nameof(ClickHouseFunctions.bit_hamming_distance)
                 && node.Method.GetParameters()[0].ParameterType != typeof(string))
        {
            rendered[0] = $"toUInt64({rendered[0]})";
            rendered[1] = $"toUInt64({rendered[1]})";
        }

        var call = scalars.Render(name, rendered);

        // The UInt16 overloads keep the vendor's width semantics (bitNot/shift/rotate) but declare an
        // int result because the shared materializer has no ushort reader; the widening is exact.
        if ((name is nameof(ClickHouseFunctions.bit_not)
                or nameof(ClickHouseFunctions.bit_shift_left)
                or nameof(ClickHouseFunctions.bit_shift_right)
                or nameof(ClickHouseFunctions.bit_rotate_left)
                or nameof(ClickHouseFunctions.bit_rotate_right))
            && Nullable.GetUnderlyingType(node.Method.GetParameters()[0].ParameterType) == typeof(ushort))
        {
            call = $"toInt32({call})";
        }

        visitor.Builder!.Append(call);
    }

    /// <summary>
    /// Renders <c>bitTestAll</c>/<c>bitTestAny</c>: the value followed by the inline
    /// <c>params</c> bit positions. The vendor needs at least one position, so an empty inline list
    /// and a captured (non-inline) array are refused before the provider gate with the precise
    /// technical reason instead of emitting a call the server cannot accept.
    /// </summary>
    private static void EmitBitTestFamily(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        if (node.Arguments.Count != 2
            || node.Arguments[1] is not NewArrayExpression { Expressions: var positions })
            throw new NotSupportedException(
                $"The {name} bit positions must be inline constants, not a captured array.");

        if (positions.Count == 0)
            throw new NotSupportedException(
                $"The {name} function requires at least one bit position: ClickHouse rejects the " +
                "value-only call with TOO_FEW_ARGUMENTS_FOR_FUNCTION.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            for (var (i, cnt) = (0, positions.Count); i < cnt; i++)
                visitor.Visit(positions[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[positions.Count + 1];
        rendered[0] = visitor.VisitToString(node.Arguments[0]);
        for (var (i, cnt) = (0, positions.Count); i < cnt; i++)
            rendered[i + 1] = visitor.VisitToString(positions[i]);

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }

    /// <summary>
    /// <c>bech32Encode(hrp, data[, witver])</c>: the optional witness version is a compile-time 5-bit
    /// constant, so it is validated and inlined as a literal instead of being bound as a parameter the
    /// server may refuse in a constant position.
    /// </summary>
    private static void EmitBech32Encode(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var arguments = node.Arguments;
        var hasWitnessVersion = arguments.Count == 3
            && TypeFacts.UnwrapConvert(arguments[2]) is not ConstantExpression { Value: null };

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
                visitor.Visit(arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var rendered = new string[hasWitnessVersion ? 3 : 2];
        rendered[0] = visitor.VisitToString(arguments[0]);
        rendered[1] = visitor.VisitToString(arguments[1]);

        if (hasWitnessVersion)
            rendered[2] = RequireWitnessVersion(arguments[2]).ToString(CultureInfo.InvariantCulture);

        builder.Append(scalars.Render(name, rendered));
    }

    /// <summary>
    /// The bech32 witness version is a 5-bit value: 0 selects Bech32, 1 or greater selects Bech32m.
    /// A non-constant or out-of-range value is refused before any SQL is composed.
    /// </summary>
    private static int RequireWitnessVersion(Expression version)
    {
        if (TypeFacts.UnwrapConvert(version) is not ConstantExpression { Value: int value } || value is < 0 or > 31)
            throw new NotSupportedException(
                "The bech32_encode witness version must be a compile-time constant in 0..31: 0 selects " +
                "Bech32 and 1..31 select Bech32m.");

        return value;
    }

    /// <summary>
    /// Renders a function whose last argument is an array operand: every scalar argument is rendered by
    /// the visitor, and the array argument is handled by
    /// <see cref="SqlOperandTranslator.AppendArrayOrColumn"/> so a captured or inline array becomes one
    /// parameter instead of a per-element list.
    /// </summary>
    private static void EmitScalarWithTrailingArray(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var arguments = node.Arguments;
        var last = arguments.Count - 1;
        var rendered = new string[arguments.Count];

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            {
                if (i == last)
                    SqlOperandTranslator.AppendArrayOrColumn(visitor, arguments[i]);
                else
                    visitor.Visit(arguments[i]);
            }

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;

        for (var i = 0; i < last; i++)
            rendered[i] = visitor.VisitToString(arguments[i]);

        var (arrayText, arrayFragment) = Capture(visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, arguments[last]));
        rendered[last] = arrayText;

        var fragments = arrayFragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(arrayText, null, arrayFragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        SqlParameterFragmentRelocation.AppendComposed(builder, visitor.Recorder, scalars.Render(name, rendered), fragments);
    }

    /// <summary>
    /// <c>now64(scale)</c> is representable only up to tick precision: <see cref="DateTime"/> resolves
    /// 100 ns, while <c>DateTime64(8)</c>/<c>DateTime64(9)</c> carry digits below that.
    /// </summary>
    private static void RequireRepresentableScale(Expression scale)
    {
        if (scale is not ConstantExpression { Value: int value })
            throw new NotSupportedException(
                "The now64 scale must be a compile-time constant so the result precision is known: a " +
                "DateTime64(8)/DateTime64(9) result carries sub-100 ns digits System.DateTime cannot represent.");

        if (value is < 0 or > 7)
            throw new NotSupportedException(
                $"The now64 function is refused for scale {value}: the scale must be 0..7, because scale " +
                "8/9 carries sub-100 ns digits that System.DateTime cannot represent (100 ns tick resolution).");
    }

    /// <summary>
    /// Renders <c>toStartOfInterval(value, INTERVAL amount unit[, origin])</c>. The unit is a SQL
    /// keyword, so it cannot be a query parameter: it must be a compile-time literal from the
    /// supported set and is inlined (validated, upper-cased). The amount and the origin are ordinary
    /// operands (parameters are fine).
    /// </summary>
    private static void EmitStartOfInterval(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars)
    {
        var arguments = node.Arguments;
        if (!SqlLiteral.TryGetConstantString(arguments[2], out var unit)
            || !IntervalUnits.TryGetValue(unit, out var keyword))
            throw new NotSupportedException(
                "The to_start_of_interval unit must be a compile-time string literal naming second, " +
                "minute, hour, day, week, month, quarter or year; ClickHouse rejects sub-second units " +
                "for a DateTime argument.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(arguments[0]);
            visitor.Visit(arguments[1]);
            if (arguments.Count == 4)
                visitor.Visit(arguments[3]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[arguments.Count];
        rendered[0] = visitor.VisitToString(arguments[0]);
        rendered[1] = visitor.VisitToString(arguments[1]);
        rendered[2] = keyword;
        if (arguments.Count == 4)
            rendered[3] = visitor.VisitToString(arguments[3]);

        visitor.Builder!.Append(scalars.Render(nameof(ClickHouseFunctions.to_start_of_interval), rendered));
    }

    /// <summary>Renders a function over scalar (non-array) arguments.</summary>
    private static void EmitScalar(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var args = DropTrailingNulls(node);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }

    /// <summary>Renders a <c>params</c> array function, flattening the argument array into positional arguments.</summary>
    private static void EmitParamsArray(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var items = FlattenParams(node.Arguments);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArrayOrColumn(visitor, items[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var rendered = new string[items.Count];
        var fragments = new List<SqlParameterTrackedFragment>(items.Count);

        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
        {
            var (text, fragment) = Capture(visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, items[i]));
            rendered[i] = text;
            if (fragment is { OccurrenceCount: > 0 })
                fragments.Add(new SqlParameterTrackedFragment(text, null, fragment));
        }

        SqlParameterFragmentRelocation.AppendComposed(builder, visitor.Recorder, scalars.Render(name, rendered), fragments);
    }

    /// <summary>Renders a function with a single array argument (a column or a captured array).</summary>
    private static void EmitSingleArray(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var argument = node.Arguments[0];

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArrayOrColumn(visitor, argument);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var (rendered, fragment) = Capture(visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, argument));
        var fragments = fragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(rendered, null, fragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        SqlParameterFragmentRelocation.AppendComposed(builder, visitor.Recorder, scalars.Render(name, [rendered]), fragments);
    }

    /// <summary>Renders the <c>map(key, value, ...)</c> constructor from inline key/value tuples.</summary>
    private static void EmitMapConstructor(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var pairs = FlattenTuplePairs(node.Arguments);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, pairs.Count); i < cnt; i++)
            {
                var pair = pairs[i];
                for (var (j, jcnt) = (0, pair.Count); j < jcnt; j++)
                    visitor.Visit(pair[j]);
            }

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new List<string>(pairs.Count * 2);
        for (var (i, cnt) = (0, pairs.Count); i < cnt; i++)
        {
            var pair = pairs[i];
            for (var (j, jcnt) = (0, pair.Count); j < jcnt; j++)
                rendered.Add(visitor.VisitToString(pair[j]));
        }

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }

    /// <summary>
    /// Renders the map helpers, which have no native server form with the CLR-shaped result the public
    /// surface declares:
    /// <c>map_filter</c> and <c>map_sort</c> iterate a zipped <c>(key, value)</c> array,
    /// <c>map_apply</c> maps the key and value arrays, and the native <c>mapAll</c>/<c>mapExists</c>
    /// predicates keep their own spelling. The map operand is exactly one operand: an inline
    /// <c>map(...)</c> constructor rendered through the pair path, or one captured dictionary bound as a
    /// single map parameter. A null operand is refused before any SQL reaches the server.
    /// </summary>
    private static void EmitMapFunction(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        // map_sort has no lambda; its ordering lambda is generated over the zipped pair tuple.
        if (name == MapSortName)
        {
            var sortMap = RenderMapOperand(visitor, node.Arguments[0]);

            if (visitor.IsParamMode)
                return;

            // The generated identifier must not shadow one the operand already renders (an outer lambda
            // parameter called `_t`, for example), so the operand is scanned along with the (absent) body.
            var tuple = FreshIdentifier(null, node.Arguments[0]);
            var sortLowered = $"arraySort({tuple} -> {tuple}.1, {ZipMapOperand(sortMap)})";

            // The cast composes the declared result type and can refuse the call; the alias flag is set
            // only after that succeeded, so a refusal leaves no sticky flag behind.
            var sortSql = CastMapResult(visitor.Dialect, node, name, sortLowered);

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(sortSql);
            return;
        }

        var lambda = ExtractLambda(node.Arguments[0], name);

        if (lambda.Parameters.Count != 2)
            throw new NotSupportedException($"The {name} lambda must have exactly two parameters (key, value).");

        var first = lambda.Parameters[0];
        var second = lambda.Parameters[1];

        // map_filter iterates a zipped (key, value) tuple, so its two parameters are rendered as the
        // tuple fields of one fresh lambda identifier. The rebinding is carried by the parameter
        // identity: the body's ParameterExpression nodes are mapped, never its identifier text, so
        // nested scopes and every other identifier stay untouched.
        var isFilter = name == nameof(ClickHouseFunctions.map_filter);
        var filterTuple = isFilter ? FreshIdentifier(lambda.Body, node.Arguments[1]) : null;

        // The body may reference an enclosing higher-order lambda's parameter (a nested
        // array_map(x => ... map_filter((k, v) => v > x, m))); the outer bindings are carried into the
        // body visitor so that reference still renders. An inner parameter of the same name keeps
        // winning through SQL scoping, because both are emitted as their bare identifier.
        var parameters = new Dictionary<ParameterExpression, string>();
        if (visitor.LambdaParameters is { } outer)
        {
            foreach (var entry in outer)
                parameters[entry.Key] = entry.Value;
        }

        parameters[first] = filterTuple is null ? IdentifierOr(first, "k") : filterTuple + ".1";
        parameters[second] = filterTuple is null ? IdentifierOr(second, "v") : filterTuple + ".2";

        using var bodyVisitor = new HigherOrderLambdaVisitor(visitor.Options with { DontNeedAlias = false }, parameters);
        var body = RenderLambdaBody(bodyVisitor, lambda.Body);

        var lambdaSql = filterTuple is null
            ? $"({parameters[first]}, {parameters[second]}) -> {body}"
            : $"{filterTuple} -> {body}";

        // The lambda precedes the map in the SQL, so it is rendered first and the captured-value
        // parameter order of the statement stays aligned. The parameter-only pass relies on the same
        // order to mint identical parameter names (see RenderLambdaBody).
        var map = RenderMapOperand(visitor, node.Arguments[1]);

        if (visitor.IsParamMode)
            return;

        // The two predicates accept the map itself and keep their native spelling.
        if (name is nameof(ClickHouseFunctions.map_all) or nameof(ClickHouseFunctions.map_exists))
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(scalars.Render(name, [lambdaSql, map]));
            return;
        }

        var lowered = name == nameof(ClickHouseFunctions.map_apply)
            ? $"arrayMap({lambdaSql}, mapKeys({map}), mapValues({map}))"
            : $"arrayFilter({lambdaSql}, {ZipMapOperand(map)})";

        // Compose the cast before touching the alias flag, for the same no-sticky-flag reason as map_sort.
        var mapSql = CastMapResult(visitor.Dialect, node, name, lowered);

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(mapSql);
    }

    /// <summary>
    /// Wraps a completed <c>map_filter</c>/<c>map_sort</c>/<c>map_apply</c> lowering in the typed cast of
    /// its declared result, so the server returns exactly the CLR collection element type the
    /// declaration promises. The lowering expression itself is emitted unchanged.
    /// </summary>
    private static string CastMapResult(ISqlDialect dialect, MethodCallExpression node, string name, string lowered)
    {
        var element = node.Method.ReturnType.GetElementType()
            ?? throw new NotSupportedException($"The {name} result must be a one-dimensional array (T257).");

        if (name != nameof(ClickHouseFunctions.map_apply)
            && (!TypeFacts.IsTupleLike(element) || TypeFacts.TupleArity(element) != 2))
        {
            throw new NotSupportedException(
                $"The {name} result element {element} must be a key/value tuple (T257).");
        }

        return $"cast({lowered} as {MakeMapResultTypeName(dialect, node.Method.ReturnType, resultPosition: true)})";
    }

    /// <summary>
    /// Composes the type name of a <c>map_*</c> result from its declared CLR type: the structural
    /// wrappers (array, <see cref="Nullable{T}"/>, key/value tuple) are spelled here, while every scalar
    /// leaf is named by the provider's own <see cref="ISqlDialect.MakeTypeName"/>, so no second
    /// CLR-to-provider type table exists. A leaf the provider does not map is refused before any SQL is
    /// emitted.
    /// <para>
    /// <paramref name="resultPosition"/> distinguishes the two halves of a map result. An
    /// <c>arrayMap</c> element is the declared CLR <c>TResult</c>, where a reference type admits null,
    /// while the provider's bare name (ClickHouse <c>String</c>) does not carry it, so the leaf is
    /// spelled <c>Nullable(...)</c>; this is the declared-type mapping only and does not change the
    /// materialized CLR type (<c>string?[]</c> is <c>string[]</c> at runtime). A filter/sort key or
    /// value tuple component keeps the bare name unless the CLR component is
    /// <see cref="Nullable{T}"/>, because the map keys and values themselves are non-nullable.
    /// </para>
    /// </summary>
    private static string MakeMapResultTypeName(ISqlDialect dialect, Type type, bool resultPosition)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return $"Nullable({MakeMapResultTypeName(dialect, underlying, resultPosition: false)})";

        if (type.IsArray && type.GetElementType() is { } element)
            return $"Array({MakeMapResultTypeName(dialect, element, resultPosition)})";

        if (TypeFacts.IsTupleLike(type))
        {
            if (TypeFacts.TupleArity(type) != 2)
                throw new NotSupportedException($"The map result tuple {type} must have exactly two components (T257).");

            var components = type.GetGenericArguments();
            return $"Tuple({MakeMapResultTypeName(dialect, components[0], resultPosition: false)}, {MakeMapResultTypeName(dialect, components[1], resultPosition: false)})";
        }

        var leaf = MakeMapLeafTypeName(dialect, type);

        return resultPosition && !type.IsValueType ? $"Nullable({leaf})" : leaf;
    }

    /// <summary>
    /// Names a scalar result element through the provider mapping. <c>string</c> is the one admitted
    /// reference-type leaf, and the CLR scalars whose own name already is the provider spelling are
    /// identity mappings, indistinguishable from the dialect's generic <c>Type.Name</c> fallback by the
    /// resulting string, so they are admitted by type. A type the provider maps to a name other than its
    /// CLR name (for example <c>float</c> to <c>Float32</c>) is admitted by that differing mapping. Any
    /// other leaf that only reaches the generic fallback is refused.
    /// </summary>
    private static string MakeMapLeafTypeName(ISqlDialect dialect, Type type)
    {
        var name = dialect.MakeTypeName(type);

        if (type == typeof(string) || name != type.Name || IdentityMappedTypes.Contains(type))
            return name;

        throw new NotSupportedException(
            $"The map result element type {type} is not mapped to a ClickHouse type name (T257); the cast would emit an invalid element type.");
    }

    /// <summary>
    /// Renders the map operand of a map helper. An inline <c>map(...)</c> constructor keeps the existing
    /// pair path (a captured or null pair collection is refused there); a captured dictionary is
    /// evaluated once and bound as a single map-valued parameter; anything else is rendered as an
    /// operand, so a map column keeps its column reference. A captured value that is not a dictionary is
    /// refused instead of being emitted as <c>mapKeys(&lt;scalar&gt;)</c>.
    /// </summary>
    private static string RenderMapOperand(BaseExpressionVisitor visitor, Expression expression)
    {
        var operand = TypeFacts.UnwrapConvert(expression);

        if (operand is MethodCallExpression call
            && call.Method.DeclaringType == typeof(ClickHouseFunctions)
            && call.Method.Name == MapConstructorName)
        {
            if (visitor.IsParamMode)
            {
                visitor.Visit(operand);
                return string.Empty;
            }

            return visitor.VisitToString(operand);
        }

        if (!SqlOperandTranslator.IsCapturedArray(operand))
        {
            if (visitor.IsParamMode)
            {
                visitor.Visit(operand);
                return string.Empty;
            }

            return visitor.VisitToString(operand);
        }

        // A captured dictionary is a map value, not a pair collection: it is evaluated exactly once and
        // bound as one parameter (the driver maps Dictionary<K, V> to Map(K, V)).
        var value = InValuesEvaluator.Evaluate(operand, visitor.QueryProvider);

        if (value is null)
            throw new NotSupportedException(
                $"The {TypeFacts.UnwrapConvert(expression).Type.Name} map operand must not be null; the map helpers require a map value or an inline map(...) constructor.");

        if (!IsMapValue(value))
            throw new NotSupportedException(
                $"The {value.GetType().Name} map operand must be a dictionary; the map helpers require a map value or an inline map(...) constructor.");

        var name = visitor.ParameterProvider.GetParamName();
        visitor.TryAddCapturedParameter(name, value, operand);

        return visitor.IsParamMode ? string.Empty : visitor.Dialect.MakeParam(name);
    }

    /// <summary>
    /// True when a captured value is a map: its type implements the generic
    /// <see cref="IDictionary{TKey,TValue}"/> or <see cref="IReadOnlyDictionary{TKey,TValue}"/> contract
    /// (which every <see cref="Dictionary{TKey,TValue}"/> does). A captured value of any other type is
    /// not a map and must not be rendered as one.
    /// </summary>
    private static bool IsMapValue(object value)
    {
        foreach (var contract in value.GetType().GetInterfaces())
        {
            if (!contract.IsGenericType)
                continue;

            var definition = contract.GetGenericTypeDefinition();
            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Renders a higher-order lambda body. A conditional is composed here with the mapping-aware body
    /// visitor: the shared conditional renderer builds its test through a fresh predicate visitor that
    /// does not carry the lambda parameters, which would emit <c>case when ( &gt; 0) ...</c> and fail on
    /// the server. Any conditional in a position this arm cannot compose is refused instead of being
    /// rendered into invalid SQL.
    /// <para>
    /// Invariant: the parameter-collection pass visits exactly what the SQL pass visits, in the same
    /// order (map body first, then the map operand), so both passes mint identical parameter names. It
    /// therefore walks the body on the mapping-aware visitor itself and returns no SQL: cloning is
    /// refused in parameter mode, and this pass has no SQL to produce.
    /// </para>
    /// </summary>
    private static string RenderLambdaBody(BaseExpressionVisitor visitor, Expression body)
    {
        if (visitor.IsParamMode)
        {
            // The fail-closed guard stays in force on this pass: a genuinely unsupported shape must be
            // refused here too, never silently accepted because the pass emits no SQL.
            if (TypeFacts.UnwrapConvert(body) is not ConditionalExpression && body.Has<ConditionalExpression>())
            {
                throw new NotSupportedException(
                    "A conditional inside a higher-order map lambda is supported only as the lambda body or a branch value.");
            }

            visitor.Visit(body);
            return string.Empty;
        }

        if (TypeFacts.UnwrapConvert(body) is ConditionalExpression conditional)
        {
            var test = visitor.VisitToString(conditional.Test);
            var whenTrue = RenderLambdaBody(visitor, conditional.IfTrue);
            var whenFalse = RenderLambdaBody(visitor, conditional.IfFalse);
            var caseSql = $"{visitor.Kw("case when ")}{test}{visitor.Kw(" then ")}{whenTrue}" +
                          $"{visitor.Kw(" else ")}{whenFalse}{visitor.Kw(" end")}";

            return visitor.Dialect.MakeCase(
                caseSql, TypeFacts.IsBoolean(body.Type), visitor.IsPredicateContext, visitor.KeywordCase);
        }

        if (body.Has<ConditionalExpression>())
            throw new NotSupportedException(
                "A conditional inside a higher-order map lambda is supported only as the lambda body or a branch value.");

        return visitor.VisitToString(body);
    }

    /// <summary>Renders the zipped <c>(key, value)</c> array of the map operand.</summary>
    private static string ZipMapOperand(string map) => $"arrayZip(mapKeys({map}), mapValues({map}))";

    /// <summary>
    /// Returns a tuple lambda identifier that does not occur as an identifier in the rendered body or in
    /// the map operand, so the generated <c>.1</c>/<c>.2</c> accesses can never shadow a column, a
    /// parameter or an enclosing lambda parameter.
    /// </summary>
    private static string FreshIdentifier(Expression? body, Expression? operand = null)
    {
        var candidate = TupleIdentifierBase;
        var suffix = 1;

        while (UsesIdentifier(body, candidate) || UsesIdentifier(operand, candidate))
            candidate = TupleIdentifierBase + (++suffix).ToString(CultureInfo.InvariantCulture);

        return candidate;
    }

    private static bool UsesIdentifier(Expression? body, string name)
    {
        if (body is null)
            return false;

        var collector = new IdentifierCollector(name);
        collector.Visit(body);
        return collector.Found;
    }

    /// <summary>Records whether an identifier (a parameter or a member name) occurs in an expression.</summary>
    private sealed class IdentifierCollector(string name) : ExpressionVisitor
    {
        internal bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name == name)
                Found = true;

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.Name == name)
                Found = true;

            return base.VisitMember(node);
        }
    }

    private static string IdentifierOr(ParameterExpression parameter, string fallback)
        => string.IsNullOrEmpty(parameter.Name) ? fallback : parameter.Name!;


    /// <summary>
    /// Validates the CLR-side contract of a tuple-family member before any SQL is composed or any
    /// provider gate runs: the operator/dot-product components must be numeric, and
    /// <c>tupleToNameValuePairs</c> requires every element to have the same type (the server rejects a
    /// heterogeneous tuple and there is no typed common-supertype result).
    /// </summary>
    private static void ValidateTupleMember(MethodCallExpression node, string name)
    {
        if (name == nameof(ClickHouseFunctions.tuple_to_name_value_pairs))
        {
            var components = node.Method.GetGenericArguments();
            for (var i = 1; i < components.Length; i++)
            {
                if (components[i] != components[0])
                    throw new NotSupportedException(
                        $"The tuple_to_name_value_pairs function is refused for the tuple {node.Method.ReturnType}: " +
                        "the server requires every tuple element to have just one type (a heterogeneous tuple " +
                        "raises ILLEGAL_TYPE_OF_ARGUMENT), and there is no typed common-supertype CLR pair to " +
                        "declare. Use the same declared element type for every component.");
            }

            return;
        }

        if (name == nameof(ClickHouseFunctions.tuple_hamming_distance))
        {
            foreach (var component in node.Method.GetGenericArguments())
            {
                if (Nullable.GetUnderlyingType(component) is not null)
                    throw new NotSupportedException(
                        $"The {name} function is refused for the component type {component.Name}: the server " +
                        "returns a Nullable result when any component is NULL, and the declared non-nullable " +
                        "CLR result cannot represent it (toInt64(NULL) would silently become 0).");
            }

            return;
        }

        if (name is nameof(ClickHouseFunctions.tuple_plus) or nameof(ClickHouseFunctions.tuple_minus)
            or nameof(ClickHouseFunctions.tuple_multiply) or nameof(ClickHouseFunctions.tuple_divide)
            or nameof(ClickHouseFunctions.dot_product))
        {
            foreach (var component in node.Method.GetGenericArguments())
            {
                var leaf = Nullable.GetUnderlyingType(component) ?? component;

                if (name == nameof(ClickHouseFunctions.dot_product)
                    && Nullable.GetUnderlyingType(component) is not null)
                {
                    throw new NotSupportedException(
                        $"The {name} function is refused for the component type {component.Name}: the server " +
                        "returns a Nullable result when any component is NULL, and the declared non-nullable " +
                        "CLR result cannot represent it (toInt64(NULL) would silently become 0).");
                }

                if (leaf == typeof(byte) || leaf == typeof(sbyte) || leaf == typeof(short)
                    || leaf == typeof(ushort) || leaf == typeof(int) || leaf == typeof(uint)
                    || leaf == typeof(long) || leaf == typeof(ulong) || leaf == typeof(float)
                    || leaf == typeof(double))
                {
                    continue;
                }

                throw new NotSupportedException(
                    $"The {name} function is refused for the component type {component.Name}: ClickHouse " +
                    "defines the element-wise arithmetic only for numeric components, and a decimal " +
                    "component would lose precision in the declared integer/floating-point tuple.");
            }
        }
    }

    /// <summary>
    /// Validates the CLR-side contract of a map-family member before any SQL is composed: a Map cannot
    /// have a Nullable key, a Nullable value cannot be promoted against a non-nullable operand, a
    /// <c>mapPopulateSeries</c> key must be an integral type, and a NULL map/array/string operand is
    /// refused rather than rendered as SQL the server would reject with an unrelated error.
    /// </summary>
    private static void ValidateMapMember(MethodCallExpression node, string name)
    {
        Type[] generics = node.Method.IsGenericMethod ? node.Method.GetGenericArguments() : [];
        if (generics.Length == 2)
        {
            var key = generics[0];
            var value = generics[1];

            if (key.IsValueType && Nullable.GetUnderlyingType(key) is not null)
                throw new NotSupportedException(
                    $"The {name} function is refused for the key type {key.Name}: ClickHouse cannot have a " +
                    "Nullable(.) key in a Map (BAD_ARGUMENTS), so the result type cannot be declared.");

            if (value.IsValueType && Nullable.GetUnderlyingType(value) is not null
                && (name is nameof(ClickHouseFunctions.map_subtract)
                    or nameof(ClickHouseFunctions.map_update)
                    or nameof(ClickHouseFunctions.map_populate_series)))
                throw new NotSupportedException(
                    $"The {name} function is refused for the value type {value.Name}: a Nullable Map value " +
                    "cannot be promoted against a non-nullable operand (DATA_TYPE_CANNOT_BE_PROMOTED), so the " +
                    "declared key/value array cannot be produced from the server Map.");

            if (name == nameof(ClickHouseFunctions.map_populate_series)
                && !IntegralKeyTypes.Contains(Nullable.GetUnderlyingType(key) ?? key))
            {
                throw new NotSupportedException(
                    $"The map_populate_series function is refused for the key type {key.Name}: the server " +
                    "requires a signed or unsigned integer key (ILLEGAL_TYPE_OF_ARGUMENT).");
            }
        }

        switch (name)
        {
            case nameof(ClickHouseFunctions.map_subtract):
            case nameof(ClickHouseFunctions.map_update):
                RefuseNullOperand(node.Arguments[0], name);
                RefuseNullOperand(node.Arguments[1], name);
                break;
            case nameof(ClickHouseFunctions.map_reverse_sort):
                RefuseNullOperand(node.Arguments[0], name);
                break;
            case nameof(ClickHouseFunctions.map_partial_sort):
                RefuseNullOperand(node.Arguments[1], name);
                break;
            case nameof(ClickHouseFunctions.map_populate_series):
                RefuseNullOperand(node.Arguments[0], name);
                break;
            case nameof(ClickHouseFunctions.map_from_arrays):
                RefuseNullOperand(node.Arguments[0], name);
                RefuseNullOperand(node.Arguments[1], name);
                break;
            case nameof(ClickHouseFunctions.extract_key_value_pairs):
                RefuseNullOperand(node.Arguments[0], name);
                break;
        }
    }

    private static void RefuseNullOperand(Expression expression, string name)
    {
        if (TypeFacts.UnwrapConvert(expression) is ConstantExpression { Value: null })
            throw new NotSupportedException(
                $"The {name} function is refused for a null operand: a ClickHouse Map, Array or data string " +
                "is not NULL-able in that position, and the server rejects the call instead of returning NULL.");
    }

    /// <summary>
    /// Renders a tuple-family member and normalises the completed call to the declared CLR shape.
    /// <c>flatten_tuple</c> needs the synthetic named input cast that the server requires; every other
    /// member is a plain function call wrapped in <c>cast(... as the declared type)</c>.
    /// </summary>
    private static void EmitTupleMember(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return;
        }

        var builder = visitor.Builder!;

        if (name == nameof(ClickHouseFunctions.flatten_tuple))
        {
            var operandExpression = node.Arguments[0];
            var operand = visitor.VisitToString(operandExpression);
            var resultType = MakeDeclaredTypeName(visitor.Dialect, node.Method.ReturnType);

            // A named tuple column already carries server-side names, and casting a nested tuple
            // column to another named type corrupts the values on the pinned 25.8 server (verified:
            // CAST(nested AS Tuple(f1 ..., f2 Tuple(...))) yields zeros), so a column is flattened
            // directly. An inline tuple(...) constructor is unnamed and would be rejected, so only
            // that shape receives the synthetic named input cast (which is value-safe for a
            // constructor).
            string flattened;
            if (TupleSqlTranslator.IsDirectTupleConstructor(operandExpression))
            {
                var field = 0;
                var namedInput = MakeNamedNestedTupleType(visitor.Dialect, operandExpression.Type, ref field);
                flattened = $"cast(flattenTuple(cast({operand} as {namedInput})) as {resultType})";
            }
            else
            {
                flattened = $"cast(flattenTuple({operand}) as {resultType})";
            }

            visitor.NeedAliasForColumn = true;
            builder.Append(flattened);
            return;
        }

        var rendered = new string[node.Arguments.Count];
        for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(node.Arguments[i]);

        var call = scalars.Render(name, rendered);
        var sql = $"cast({call} as {MakeDeclaredTypeName(visitor.Dialect, node.Method.ReturnType)})";

        visitor.NeedAliasForColumn = true;
        builder.Append(sql);
    }

    /// <summary>
    /// Renders a map-family member, routing each map operand through
    /// <see cref="RenderMapOperand"/> (the existing map-helper operand path) and each array operand
    /// through <see cref="SqlOperandTranslator.AppendArrayOrColumn"/>, then normalising the completed
    /// call to the declared <c>Map(K, V)</c>.
    /// </summary>
    private static void EmitMapMember(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var arguments = node.Arguments;

        if (visitor.IsParamMode)
        {
            switch (name)
            {
                case nameof(ClickHouseFunctions.map_from_arrays):
                    SqlOperandTranslator.AppendArrayOrColumn(visitor, arguments[0]);
                    SqlOperandTranslator.AppendArrayOrColumn(visitor, arguments[1]);
                    return;
                case nameof(ClickHouseFunctions.map_partial_sort):
                    visitor.Visit(arguments[0]);
                    RenderMapOperand(visitor, arguments[1]);
                    return;
                case nameof(ClickHouseFunctions.map_populate_series):
                    RenderMapOperand(visitor, arguments[0]);
                    if (arguments.Count == 2)
                        visitor.Visit(arguments[1]);
                    return;
                case nameof(ClickHouseFunctions.extract_key_value_pairs):
                    for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
                        visitor.Visit(arguments[i]);
                    return;
                default:
                    for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
                        RenderMapOperand(visitor, arguments[i]);
                    return;
            }
        }

        var builder = visitor.Builder!;
        string[] rendered;
        var fragments = new List<SqlParameterTrackedFragment>(2);

        switch (name)
        {
            case nameof(ClickHouseFunctions.map_from_arrays):
                var (keys, keysFragment) = Capture(visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, arguments[0]));
                var (values, valuesFragment) = Capture(visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, arguments[1]));
                rendered = [keys, values];
                if (keysFragment is { OccurrenceCount: > 0 })
                    fragments.Add(new SqlParameterTrackedFragment(keys, null, keysFragment));
                if (valuesFragment is { OccurrenceCount: > 0 })
                    fragments.Add(new SqlParameterTrackedFragment(values, null, valuesFragment));
                break;
            case nameof(ClickHouseFunctions.map_partial_sort):
                rendered = [visitor.VisitToString(arguments[0]), RenderMapOperand(visitor, arguments[1])];
                break;
            case nameof(ClickHouseFunctions.map_populate_series):
                rendered = arguments.Count == 2
                    ? [RenderMapOperand(visitor, arguments[0]), visitor.VisitToString(arguments[1])]
                    : [RenderMapOperand(visitor, arguments[0])];
                break;
            case nameof(ClickHouseFunctions.extract_key_value_pairs):
                rendered = new string[arguments.Count];
                for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
                    rendered[i] = visitor.VisitToString(arguments[i]);
                break;
            default:
                rendered = new string[arguments.Count];
                for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
                    rendered[i] = RenderMapOperand(visitor, arguments[i]);
                break;
        }

        var call = scalars.Render(name, rendered);
        var sql = $"cast({ZipMapOperand(call)} as {MakeDeclaredTypeName(visitor.Dialect, node.Method.ReturnType)})";

        visitor.NeedAliasForColumn = true;
        SqlParameterFragmentRelocation.AppendComposed(builder, visitor.Recorder, sql, fragments);
    }

    /// <summary>
    /// Composes the declared CLR type as a ClickHouse type name: the structural wrappers
    /// (<see cref="Nullable{T}"/>, arrays and tuples) are spelled here, while each scalar leaf is named
    /// by the provider's own <see cref="ISqlDialect.MakeTypeName"/> through the existing map-leaf gate.
    /// </summary>
    private static string MakeDeclaredTypeName(ISqlDialect dialect, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return $"Nullable({MakeDeclaredTypeName(dialect, underlying)})";

        if (type.IsArray && type.GetElementType() is { } element)
            return $"Array({MakeDeclaredTypeName(dialect, element)})";

        if (TypeFacts.IsTupleLike(type))
            return $"Tuple({string.Join(", ", type.GetGenericArguments().Select(component => MakeDeclaredTypeName(dialect, component)))})";

        return MakeMapLeafTypeName(dialect, type);
    }

    /// <summary>
    /// Composes a synthetic <b>named</b> tuple type for the <c>flattenTuple</c> input, which the server
    /// accepts only as a named tuple. Field names are generated positionally (<c>f1</c>, <c>f2</c>, …)
    /// and exist only inside the cast: they label the flattened output and never leak into the declared
    /// result, which is cast back to the flat CLR tuple.
    /// </summary>
    private static string MakeNamedNestedTupleType(ISqlDialect dialect, Type type, ref int field)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return $"Nullable({MakeNamedNestedTupleType(dialect, underlying, ref field)})";

        if (TypeFacts.IsTupleLike(type))
        {
            var components = type.GetGenericArguments();
            var parts = new string[components.Length];
            for (var i = 0; i < components.Length; i++)
            {
                var name = "f" + (++field).ToString(CultureInfo.InvariantCulture);
                parts[i] = name + " " + MakeNamedNestedTupleType(dialect, components[i], ref field);
            }

            return $"Tuple({string.Join(", ", parts)})";
        }

        return MakeMapLeafTypeName(dialect, type);
    }

    /// <summary>Renders <c>sumMapFiltered(keys)(key, value)</c> with double parentheses.</summary>
    private static void EmitSumMapFiltered(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var (keys, keysFragment) = Capture(visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, node.Arguments[0]));
        var key = visitor.VisitToString(node.Arguments[1]);
        var value = visitor.VisitToString(node.Arguments[2]);
        var fragments = keysFragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(keys, null, keysFragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        SqlParameterFragmentRelocation.AppendComposed(builder, visitor.Recorder, scalars.Render(name, [keys, key, value]), fragments);
    }

    /// <summary>
    /// Renders <paramref name="emit"/> into the current builder, rewinds both the builder and the
    /// parameter recorder to the pre-render position, and returns the emitted text together with a
    /// detached recorder that owns its occurrences rebased to zero. The text is not part of the builder
    /// until the caller appends the composed wrapper through
    /// <see cref="SqlParameterFragmentRelocation.AppendComposed"/>, which relocates those occurrences to
    /// their real offsets inside the wrapper.
    /// </summary>
    private static (string Text, SqlParameterTemplateRecorder? Fragment) Capture(
        BaseExpressionVisitor visitor, StringBuilder builder, Action emit)
    {
        var recorder = visitor.Recorder;
        var start = builder.Length;
        var mark = recorder?.MarkAt(start);
        emit();
        var text = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        var fragment = mark is { } checkpoint ? recorder!.RewindTo(checkpoint) : null;
        return (text, fragment);
    }

    /// <summary>
    /// Flattens the compiler-generated <c>params</c> array of an array function into its element
    /// expressions. A non-inline (captured) argument array is rejected.
    /// </summary>
    private static IReadOnlyList<Expression> FlattenParams(IReadOnlyList<Expression> args)
    {
        if (args.Count == 1 && args[0] is NewArrayExpression { Expressions: var expressions })
            return expressions;

        throw new NotSupportedException("The array arguments must be inline expressions, not a captured array.");
    }

    /// <summary>
    /// Flattens the compiler-generated <c>params</c> tuple array of <c>map</c> into its key/value
    /// elements. Every pair is an inline <see cref="Tuple{T1,T2}"/> construction (<c>Tuple.Create</c>);
    /// the declaring type is matched structurally, so an unrelated <c>Create</c> overload cannot be
    /// mistaken for a pair.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Expression>> FlattenTuplePairs(IReadOnlyList<Expression> args)
    {
        if (args.Count != 1 || args[0] is not NewArrayExpression { Expressions: var pairs })
            throw new NotSupportedException("The map key/value pairs must be inline tuples, not a captured array.");

        var result = new List<IReadOnlyList<Expression>>(pairs.Count);
        for (var (i, cnt) = (0, pairs.Count); i < cnt; i++)
        {
            result.Add(pairs[i] switch
            {
                MethodCallExpression pair
                    when pair.Method.DeclaringType == typeof(Tuple) && pair.Arguments.Count == 2 => pair.Arguments,
                _ => throw new NotSupportedException("The map key/value pairs must be inline tuples, not a captured array.")
            });
        }

        return result;
    }

    /// <summary>
    /// Drops trailing <c>null</c> literals that stand for an omitted *optional* argument, i.e. only when
    /// the corresponding parameter declares a default value. A <c>null</c> passed to a required parameter
    /// is a real NULL operand: dropping it would render a call with the wrong arity (<c>gcd(a, NULL)</c>
    /// would become <c>gcd(a)</c>, and a one-argument function called with <c>null</c> —
    /// <c>initcap(NULL)</c>, <c>lower_utf8(NULL)</c> — would render an empty call). The first argument is
    /// always kept.
    /// </summary>
    /// <summary>
    /// A decimal conversion's scale selects the vendor target type (<c>Decimal(9|18, scale)</c>), so it
    /// must be a compile-time constant in range: a runtime scale would leave the result shape unknown.
    /// </summary>
    private static void RequireConversionScale(MethodCallExpression node, string name, int maxScale)
    {
        var scale = TypeFacts.UnwrapConvert(node.Arguments[1]);
        if (scale is not ConstantExpression { Value: int value } || value < 0 || value > maxScale)
            throw new NotSupportedException(
                $"The {name} scale must be a compile-time constant in 0..{maxScale}: the vendor target " +
                "type is chosen by the scale, so a runtime scale cannot declare the result shape.");
    }

    /// <summary>
    /// <c>toFixedString</c>'s length is the vendor <c>FixedString(N)</c> size: ClickHouse rejects a
    /// column or parameter in that position (<c>ILLEGAL_COLUMN</c>), so the length is a compile-time
    /// constant 1..255 (a zero size is rejected by the server as well).
    /// </summary>
    private static void RequireFixedStringLength(MethodCallExpression node)
    {
        var length = TypeFacts.UnwrapConvert(node.Arguments[1]);
        if (length is not ConstantExpression { Value: byte or int } constant)
            throw new NotSupportedException(
                "The to_fixed_string length must be a compile-time constant: ClickHouse rejects a column " +
                "or parameter in that position with ILLEGAL_COLUMN.");

        var value = Convert.ToInt32(constant.Value, CultureInfo.InvariantCulture);
        if (value is < 1 or > 255)
            throw new NotSupportedException(
                $"The to_fixed_string length must be 1..255: ClickHouse rejects FixedString({value}) with " +
                "ARGUMENT_OUT_OF_BOUND (the size must be positive).");
    }

    /// <summary>
    /// The explicit default of <c>toInt8OrDefault</c>/<c>toUInt16OrDefault</c> is declared in the wider
    /// CLR result type (Int16/Int32) while the vendor parameter is Int8/UInt16; a constant outside the
    /// target range would be narrowed by the server before it becomes the fallback, so it is refused.
    /// </summary>
    private static void RequireNarrowDefault(MethodCallExpression node, string name)
    {
        if (TypeFacts.UnwrapConvert(node.Arguments[1]) is not ConstantExpression constant)
            return;

        var (min, max, target) = name == nameof(ClickHouseFunctions.to_int8_or_default)
            ? (-128L, 127L, "Int8")
            : (0L, 65535L, "UInt16");
        var value = Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture);
        if (value < min || value > max)
            throw new NotSupportedException(
                $"The {name} default must fit the vendor {target} target ({min}..{max}): a constant outside " +
                "that range would be narrowed by the server before it becomes the default.");
    }


    private static IReadOnlyList<Expression> DropTrailingNulls(MethodCallExpression node)
    {
        var args = node.Arguments;
        var parameters = node.Method.GetParameters();
        var last = args.Count;
        while (last > 1
               && args[last - 1] is ConstantExpression { Value: null }
               && parameters.Length >= last
               && parameters[last - 1].HasDefaultValue)
        {
            last--;
        }

        if (last == args.Count)
            return args;

        var result = new Expression[last];
        for (var i = 0; i < last; i++)
            result[i] = args[i];

        return result;
    }

    private static LambdaExpression ExtractLambda(Expression expression, string facadeName)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda })
            return lambda;

        throw new NotSupportedException(
            $"The {facadeName} function requires an inline lambda argument: a lambda held in a variable "
            + "or a delegate is not part of the query expression tree, so its body cannot be translated.");
    }
}
