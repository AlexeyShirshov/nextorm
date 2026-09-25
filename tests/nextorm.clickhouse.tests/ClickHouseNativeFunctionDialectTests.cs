using FluentAssertions;
using NextORM.Core;
using NextORM.ClickHouse;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Direct assertions on the ClickHouse renderer (<see cref="IScalarFunctions"/>) for the native function
/// gaps (issue #78). The query-level coverage lives in
/// <see cref="ClickHouseFunctionGapSqlGenerationTests"/>.
/// </summary>
public class ClickHouseNativeFunctionDialectTests
{
    private static readonly IScalarFunctions Scalars = ClickHouseDialect.Instance.ScalarFunctions!;

    [Fact]
    public void NativeFunctionNames_ShouldRenderClickHouseForms()
    {
        var cases = new (string Name, string[] Args, string Expected)[]
        {
            ("lower_utf8", ["x"], "lowerUTF8(x)"),
            ("upper_utf8", ["x"], "upperUTF8(x)"),
            ("trim_left", ["x"], "trimLeft(x)"),
            ("trim_left", ["x", "'y'"], "trimLeft(x, 'y')"),
            ("trim_right", ["x"], "trimRight(x)"),
            ("trim_both", ["x"], "trimBoth(x)"),
            ("replace_regexp_one", ["x", "'a'", "'b'"], "replaceRegexpOne(x, 'a', 'b')"),
            ("replace_regexp_all", ["x", "'a'", "'b'"], "replaceRegexpAll(x, 'a', 'b')"),
            ("match", ["x", "'a'"], "match(x, 'a')"),
            ("extract", ["x", "'a'"], "extract(x, 'a')"),
            ("extract_all", ["x", "'a'"], "extractAll(x, 'a')"),
            ("split_by_string", ["','", "x"], "splitByString(',', x)"),
            ("split_by_regexp", ["'a'", "x"], "splitByRegexp('a', x)"),
            ("split_by_whitespace", ["x"], "splitByWhitespace(x)"),
            ("format_date_time", ["dt", "'%Y'"], "formatDateTime(dt, '%Y')"),
            ("format_date_time", ["dt", "'%Y'", "'UTC'"], "formatDateTime(dt, '%Y', 'UTC')"),
            ("parse_date_time", ["s", "'%Y'"], "parseDateTime(s, '%Y')"),
            ("parse_date_time_best_effort", ["s"], "parseDateTimeBestEffort(s)"),
            ("now", [], "now()"),
            ("today", [], "today()"),
            ("yesterday", [], "yesterday()"),
            ("array_concat", ["a", "b"], "arrayConcat(a, b)"),
            ("array_flatten", ["a"], "arrayFlatten(a)"),
            ("array_uniq", ["a"], "toInt64(arrayUniq(a))"),
            ("array_intersect", ["a", "b"], "arrayIntersect(a, b)"),
            ("array_union", ["a", "b"], "arrayUnion(a, b)"),
            ("array_except", ["a", "b"], "arrayExcept(a, b)"),
            ("array_symmetric_difference", ["a", "b"], "arraySymmetricDifference(a, b)"),
            ("map", ["'a'", "1"], "map('a', 1)"),
            ("map_keys", ["m"], "mapKeys(m)"),
            ("map_values", ["m"], "mapValues(m)"),
            ("map_contains_key", ["m", "'a'"], "mapContainsKey(m, 'a')"),
            ("map_contains_value", ["m", "1"], "mapContainsValue(m, 1)"),
            ("map_add", ["m1", "m2"], "mapAdd(m1, m2)"),
            ("map_concat", ["m1", "m2"], "mapConcat(m1, m2)"),
            ("map_filter", ["(k, v) -> x", "m"], "mapFilter((k, v) -> x, m)"),
            ("map_apply", ["(k, v) -> x", "m"], "mapApply((k, v) -> x, m)"),
            ("map_all", ["(k, v) -> x", "m"], "mapAll((k, v) -> x, m)"),
            ("map_exists", ["(k, v) -> x", "m"], "mapExists((k, v) -> x, m)"),
            ("map_sort", ["m"], "mapSort(m)"),
            ("group_bitmap", ["x"], "groupBitmap(x)"),
            ("group_bitmap_and", ["x"], "groupBitmapAnd(x)"),
            ("group_bitmap_or", ["x"], "groupBitmapOr(x)"),
            ("group_bitmap_xor", ["x"], "groupBitmapXor(x)"),
            ("sum_map", ["k", "v"], "sumMap(k, v)"),
            ("sum_map_filtered", ["keys", "k", "v"], "sumMapFiltered(keys)(k, v)"),
            ("md5", ["x"], "MD5(x)"),
            ("sha1", ["x"], "SHA1(x)"),
            ("sha256", ["x"], "SHA256(x)"),
            ("sha512", ["x"], "SHA512(x)"),
            ("xx_hash32", ["x"], "toInt32(xxHash32(x))"),
            ("xx_hash64", ["x"], "toInt64(xxHash64(x))"),
            ("xxh3", ["x"], "toInt64(xxh3(x))"),
            ("city_hash64", ["x"], "toInt64(cityHash64(x))"),
            ("sip_hash64", ["x"], "toInt64(sipHash64(x))"),
            ("sip_hash128", ["x"], "sipHash128(x)"),
            ("murmur_hash2_32", ["x"], "toInt32(murmurHash2_32(x))"),
            ("murmur_hash2_64", ["x"], "toInt64(murmurHash2_64(x))"),
            ("murmur_hash3_32", ["x"], "toInt32(murmurHash3_32(x))"),
            ("murmur_hash3_64", ["x"], "toInt64(murmurHash3_64(x))"),
            ("murmur_hash3_128", ["x"], "murmurHash3_128(x)"),
            ("generate_ulid", [], "generateULID()")
        };

        foreach (var (name, args, expected) in cases)
        {
            Scalars.Supports(name).Should().BeTrue($"ClickHouse supports {name}");
            Scalars.Render(name, args).Should().Be(expected);
        }
    }
}
