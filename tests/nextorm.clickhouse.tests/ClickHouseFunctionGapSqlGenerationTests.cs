using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL-generation coverage for the ClickHouse-native function gaps (issue #78): the UTF-8/regexp/split
/// strings, the date format/parse/current-date functions, the array set operations, the map and bitmap
/// families, the hash functions and <c>generateULID</c>. These tests never open a connection.
/// </summary>
public class ClickHouseFunctionGapSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Utf8StringFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.ClickHouse.lower_utf8(x.String),
            U = SqlFunctions.ClickHouse.upper_utf8(x.String),
            TL = SqlFunctions.ClickHouse.trim_left(x.String),
            TR = SqlFunctions.ClickHouse.trim_right(x.String),
            TB = SqlFunctions.ClickHouse.trim_both(x.String),
            TLC = SqlFunctions.ClickHouse.trim_left(x.String, "x")
        }));

        sql.Should().Contain("lowerUTF8(somestring)");
        sql.Should().Contain("upperUTF8(somestring)");
        sql.Should().Contain("trimLeft(somestring)");
        sql.Should().Contain("trimRight(somestring)");
        sql.Should().Contain("trimBoth(somestring)");
        sql.Should().Contain("trimLeft(somestring, 'x')");
    }

    [Fact]
    public void RegexpStringFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R1 = SqlFunctions.ClickHouse.replace_regexp_one(x.String, "a", "b"),
            RA = SqlFunctions.ClickHouse.replace_regexp_all(x.String, "a", "b"),
            M = SqlFunctions.ClickHouse.match(x.String, "a"),
            E = SqlFunctions.ClickHouse.extract(x.String, "a"),
            EA = SqlFunctions.ClickHouse.extract_all(x.String, "a")
        }));

        sql.Should().Contain("replaceRegexpOne(somestring, 'a', 'b')");
        sql.Should().Contain("replaceRegexpAll(somestring, 'a', 'b')");
        sql.Should().Contain("match(somestring, 'a')");
        sql.Should().Contain("extract(somestring, 'a')");
        sql.Should().Contain("extractAll(somestring, 'a')");
    }

    [Fact]
    public void SplitStringFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.split_by_string(",", x.String),
            R = SqlFunctions.ClickHouse.split_by_regexp("[']", x.String),
            W = SqlFunctions.ClickHouse.split_by_whitespace(x.String)
        }));

        sql.Should().Contain("splitByString(',', somestring)");
        sql.Should().Contain("splitByRegexp('[']', somestring)");
        sql.Should().Contain("splitByWhitespace(somestring)");
    }

    [Fact]
    public void DateTimeFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.format_date_time(x.Datetime, "%Y-%m-%d"),
            FT = SqlFunctions.ClickHouse.format_date_time(x.Datetime, "%Y", "UTC"),
            P = SqlFunctions.ClickHouse.parse_date_time(x.String, "%Y-%m-%d"),
            PB = SqlFunctions.ClickHouse.parse_date_time_best_effort(x.String),
            N = SqlFunctions.ClickHouse.now(),
            T = SqlFunctions.ClickHouse.today(),
            Y = SqlFunctions.ClickHouse.yesterday()
        }));

        sql.Should().Contain("formatDateTime(dt, '%Y-%m-%d')");
        sql.Should().Contain("formatDateTime(dt, '%Y', 'UTC')");
        sql.Should().Contain("parseDateTime(somestring, '%Y-%m-%d')");
        sql.Should().Contain("parseDateTimeBestEffort(somestring)");
        sql.Should().Contain("now()");
        sql.Should().Contain("today()");
        sql.Should().Contain("yesterday()");
    }

    [Fact]
    public void ArraySetFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();
        var nested = new[] { new long[] { 1, 2 }, new long[] { 3 } };

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.ClickHouse.array_concat(x.Nums, x.Nums),
            I = SqlFunctions.ClickHouse.array_intersect(x.Nums, x.Nums),
            U = SqlFunctions.ClickHouse.array_union(x.Nums, x.Nums),
            E = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums),
            S = SqlFunctions.ClickHouse.array_symmetric_difference(x.Nums, x.Nums),
            Q = SqlFunctions.ClickHouse.array_uniq(x.Nums),
            F = SqlFunctions.ClickHouse.array_flatten(nested)
        }));

        sql.Should().Contain("arrayConcat(nums, nums)");
        sql.Should().Contain("arrayIntersect(nums, nums)");
        sql.Should().Contain("arrayUnion(nums, nums)");
        sql.Should().Contain("arrayExcept(nums, nums)");
        sql.Should().Contain("arraySymmetricDifference(nums, nums)");
        sql.Should().Contain("toInt64(arrayUniq(nums))");
        sql.Should().Contain("arrayFlatten(@");
    }

    [Fact]
    public void MapFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L), Tuple.Create("b", 2L)))),
            K = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            V = SqlFunctions.ClickHouse.map_values(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            CK = SqlFunctions.ClickHouse.map_contains_key(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)), "a"),
            CV = SqlFunctions.ClickHouse.map_contains_value(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)), 1L),
            A = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_add(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)), SqlFunctions.ClickHouse.map(Tuple.Create("b", 2L))))),
            C = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_concat(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)), SqlFunctions.ClickHouse.map(Tuple.Create("b", 2L))))),
            S = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))))
        }));

        sql.Should().Contain("map('a', 1, 'b', 2)");
        sql.Should().Contain("mapKeys(map('a', 1))");
        sql.Should().Contain("mapValues(map('a', 1))");
        sql.Should().Contain("mapContainsKey(map('a', 1), 'a')");
        sql.Should().Contain("mapContainsValue(map('a', 1), 1)");
        sql.Should().Contain("mapAdd(map('a', 1), map('b', 2))");
        sql.Should().Contain("mapConcat(map('a', 1), map('b', 2))");
        sql.Should().Contain("mapSort(map('a', 1))");
    }

    [Fact]
    public void MapLambdaFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))))),
            A = SqlFunctions.ClickHouse.map_apply((k, v) => k, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            X = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))
        }));

        sql.Should().Contain("mapFilter((k, v) -> (v > 0), map('a', 1))");
        sql.Should().Contain("mapApply((k, v) -> k, map('a', 1))");
        sql.Should().Contain("mapAll((k, v) -> (v > 0), map('a', 1))");
        sql.Should().Contain("mapExists((k, v) -> (v > 0), map('a', 1))");
    }

    [Fact]
    public void BitmapAndSumMapAggregates_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            GB = SqlFunctions.ClickHouse.group_bitmap(x.Id),
            GA = SqlFunctions.ClickHouse.group_bitmap_and(x.Id),
            GO = SqlFunctions.ClickHouse.group_bitmap_or(x.Id),
            GX = SqlFunctions.ClickHouse.group_bitmap_xor(x.Id),
            SM = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.sum_map(x.String, x.Int))),
            SMF = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.sum_map_filtered(new[] { "a" }, x.String, x.Int)))
        }));

        sql.Should().Contain("groupBitmap(id)");
        sql.Should().Contain("groupBitmapAnd(id)");
        sql.Should().Contain("groupBitmapOr(id)");
        sql.Should().Contain("groupBitmapXor(id)");
        sql.Should().Contain("sumMap(somestring, nullableint)");
        sql.Should().Contain("sumMapFiltered(@");
    }

    [Fact]
    public void HashFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M5 = SqlFunctions.ClickHouse.md5(x.String),
            S1 = SqlFunctions.ClickHouse.sha1(x.String),
            S256 = SqlFunctions.ClickHouse.sha256(x.String),
            S512 = SqlFunctions.ClickHouse.sha512(x.String),
            X32 = SqlFunctions.ClickHouse.xx_hash32(x.String),
            X64 = SqlFunctions.ClickHouse.xx_hash64(x.String),
            X3 = SqlFunctions.ClickHouse.xxh3(x.String),
            C64 = SqlFunctions.ClickHouse.city_hash64(x.String),
            P64 = SqlFunctions.ClickHouse.sip_hash64(x.String),
            P128 = SqlFunctions.ClickHouse.sip_hash128(x.String),
            M232 = SqlFunctions.ClickHouse.murmur_hash2_32(x.String),
            M264 = SqlFunctions.ClickHouse.murmur_hash2_64(x.String),
            M332 = SqlFunctions.ClickHouse.murmur_hash3_32(x.String),
            M364 = SqlFunctions.ClickHouse.murmur_hash3_64(x.String),
            M3128 = SqlFunctions.ClickHouse.murmur_hash3_128(x.String)
        }));

        sql.Should().Contain("MD5(somestring)");
        sql.Should().Contain("SHA1(somestring)");
        sql.Should().Contain("SHA256(somestring)");
        sql.Should().Contain("SHA512(somestring)");
        sql.Should().Contain("toInt32(xxHash32(somestring))");
        sql.Should().Contain("toInt64(xxHash64(somestring))");
        sql.Should().Contain("toInt64(xxh3(somestring))");
        sql.Should().Contain("toInt64(cityHash64(somestring))");
        sql.Should().Contain("toInt64(sipHash64(somestring))");
        sql.Should().Contain("sipHash128(somestring)");
        sql.Should().Contain("toInt32(murmurHash2_32(somestring))");
        sql.Should().Contain("toInt64(murmurHash2_64(somestring))");
        sql.Should().Contain("toInt32(murmurHash3_32(somestring))");
        sql.Should().Contain("toInt64(murmurHash3_64(somestring))");
        sql.Should().Contain("murmurHash3_128(somestring)");
    }

    [Fact]
    public void GenerateUlid_ShouldUseClickHouseName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { U = SqlFunctions.ClickHouse.generate_ulid() }))
            .Should().Contain("generateULID()");
    }
}
