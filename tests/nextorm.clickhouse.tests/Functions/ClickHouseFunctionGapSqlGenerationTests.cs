using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;
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

    /// <summary>
    /// Renders through the plan cache (<c>storeInCache: true</c>), so the second render of the same
    /// shape is a cache hit whose parameters are collected by the parameter-only pass — the pass that
    /// walks the query tree without emitting SQL.
    /// </summary>
    private static string SqlOfCached<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None))
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
        sql.Should().Contain("splitByRegexp('[\\']', somestring)");
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
            S = SqlFunctions.ClickHouse.array_symmetric_difference(x.Nums, x.Nums),
            Q = SqlFunctions.ClickHouse.array_uniq(x.Nums),
            F = SqlFunctions.ClickHouse.array_flatten(nested)
        }));

        sql.Should().Contain("arrayConcat(nums, nums)");
        sql.Should().Contain("arrayIntersect(nums, nums)");
        sql.Should().Contain("arrayUnion(nums, nums)");
        sql.Should().Contain("arraySymmetricDifference(nums, nums)");
        sql.Should().Contain("toInt64(arrayUniq(nums))");
        sql.Should().Contain("arrayFlatten(@");
    }

    // #252: arrayExcept exists only from ClickHouse 25.9; the pinned 25.8 baseline must refuse the
    // call at translation/preparation time for every form the public signature can express, and the
    // refusal must come from the existing native-function gate, not from a new mechanism.
    [Fact]
    public void ArrayExcept_ShouldBeRefusedAtPreparationForEveryArity()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();
        var absent = (long[])null!;

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Two = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums)
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("The array_except function is not supported by this provider.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Three = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums, x.Nums)
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("The array_except function is not supported by this provider.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Single = SqlFunctions.ClickHouse.array_except(x.Nums)
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("The array_except function is not supported by this provider.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Empty = SqlFunctions.ClickHouse.array_except<long>()
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("The array_except function is not supported by this provider.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Absent = SqlFunctions.ClickHouse.array_except(x.Nums, absent)
        }))).Should().Throw<NotSupportedException>(
                "a null captured array must not surface as a NullReferenceException")
            .WithMessage("The array_except function is not supported by this provider.");
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
        sql.Should().Contain("length(cast(arraySort(_t -> _t.1, arrayZip(mapKeys(map('a', 1)), mapValues(map('a', 1)))) as Array(Tuple(String, Int64))))");
    }

    [Fact]
    public void MapLambdaFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))),
            A = SqlFunctions.ClickHouse.map_apply((k, v) => k, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            X = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))
        }));

        sql.Should().Contain("length(cast(arrayFilter(_t -> (_t.2 > 0), arrayZip(mapKeys(map('a', 1)), mapValues(map('a', 1)))) as Array(Tuple(String, Int64))))");
        sql.Should().Contain("cast(arrayMap((k, v) -> k, mapKeys(map('a', 1)), mapValues(map('a', 1))) as Array(Nullable(String)))");
        sql.Should().Contain("mapAll((k, v) -> (v > 0), map('a', 1))");
        sql.Should().Contain("mapExists((k, v) -> (v > 0), map('a', 1))");
    }

    // T257: the result type of the map_* lowering is composed from the declared CLR element type; the
    // scalar leaves come from the dialect mapping, so a Dictionary<string, int> operand casts to
    // Array(Tuple(String, Int32)) rather than to the operand's provider-inferred width.
    [Fact]
    public void MapFilterAndSort_ShouldCastTheDictionaryElementTypes()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var map = new Dictionary<string, int> { ["a"] = 1 };

        var filterSql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, map))
        }));

        filterSql.Should().Contain("length(cast(arrayFilter(_t -> (_t.2 > 0), arrayZip(mapKeys(@p0), mapValues(@p0))) as Array(Tuple(String, Int32))))");

        var sortSql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_sort(map))
        }));

        sortSql.Should().Contain("length(cast(arraySort(_t -> _t.1, arrayZip(mapKeys(@p0), mapValues(@p0))) as Array(Tuple(String, Int32))))");
    }

    // T257: the apply result element is the declared TResult leaf, not the map value type.
    [Fact]
    public void MapApply_ShouldCastTheLambdaResultType()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.ClickHouse.map_apply((k, v) => v, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            N = SqlFunctions.ClickHouse.map_apply<string, int, int?>((k, v) => v, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1)))
        }));

        sql.Should().Contain("cast(arrayMap((k, v) -> v, mapKeys(map('a', 1)), mapValues(map('a', 1))) as Array(Int64))");
        sql.Should().Contain("cast(arrayMap((k, v) -> v, mapKeys(map('a', 1)), mapValues(map('a', 1))) as Array(Nullable(Int32)))");
    }

    // T257: a reference-type apply result is a nullable leaf — the declared CLR element admits null and
    // ClickHouse String does not, so the cast keeps the nulls of a default(...) branch instead of letting
    // the server reject them (Code: 349).
    [Fact]
    public void MapApply_ShouldCastAReferenceResultAsNullable()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R = SqlFunctions.ClickHouse.map_apply((k, v) => k, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))
        }));

        sql.Should().Contain("cast(arrayMap((k, v) -> k, mapKeys(map('a', 1)), mapValues(map('a', 1))) as Array(Nullable(String)))");
    }

    // T257: a value-type apply result over a captured dictionary stays a bare leaf; only the key/value
    // tuple components of the filter/sort arms follow the same bare spelling.
    [Fact]
    public void MapApply_ShouldCastADictionaryValueResultWithoutNullable()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var map = new Dictionary<string, int> { ["a"] = 1 };

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.map_apply((k, v) => v, map)
        }));

        sql.Should().Contain("cast(arrayMap((k, v) -> v, mapKeys(@p0), mapValues(@p0)) as Array(Int32))");
    }

    // T257: the empty inline constructor still declares the generic arguments the projection carries.
    // Each arm is rendered on its own so the assertion pins that arm's exact lowering and typed wrapper
    // instead of a fragment three arms could all satisfy.
    [Fact]
    public void MapFunctions_ShouldCastTheEmptyInlineMapElementTypes()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, SqlFunctions.ClickHouse.map<string, int>()))
        })).Should().Contain(
            "toInt64(length(cast(arrayFilter(_t -> (_t.2 > 0), arrayZip(mapKeys(map()), mapValues(map()))) " +
            "as Array(Tuple(String, Int32))))) as `F`");

        SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.map_apply((k, v) => v, SqlFunctions.ClickHouse.map<string, int>())
        })).Should().Contain(
            "cast(arrayMap((k, v) -> v, mapKeys(map()), mapValues(map())) as Array(Int32)) as `A`");

        SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map<string, int>()))
        })).Should().Contain(
            "toInt64(length(cast(arraySort(_t -> _t.1, arrayZip(mapKeys(map()), mapValues(map()))) " +
            "as Array(Tuple(String, Int32))))) as `S`");
    }

    // T257 (#7): the result leaf is admitted by the provider mapping. DateTime is an identity mapping in
    // ClickHouse, and float is named Float32 by the dialect, so neither reaches the generic Type.Name
    // fallback that would emit an invalid element type.
    [Fact]
    public void MapApply_ShouldAdmitTheDateTimeAndFloatResultLeaves()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var map = new Dictionary<string, int> { ["a"] = 1 };
        var when = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new
            {
                D = SqlFunctions.ClickHouse.map_apply<string, int, DateTime>((k, v) => when, map)
            })).Should().Contain("as Array(DateTime))");

            SqlOf(ctx, e.Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_apply<string, int, float>((k, v) => v, map)
            })).Should().Contain("as Array(Float32))");
        }
    }

    // T257 (#14): the fail-closed paths that the public typed API can reach. A leaf the provider only
    // reaches through its generic Type.Name fallback would emit invalid ClickHouse SQL, so the call is
    // refused while the cast is composed, i.e. before any statement is rendered.
    [Fact]
    public void MapApply_UnsupportedResultLeaf_ShouldBeRefusedBeforeSql()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.map_apply<string, int, UnsupportedMapLeaf>((k, v) => default!, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1)))
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("*UnsupportedMapLeaf*is not mapped to a ClickHouse type name*");
    }

    // T257 (#14): a conditional is composed only as the whole lambda body or a branch value; one nested
    // inside another expression (here the operand of the comparison) cannot be composed with the
    // lambda-parameter mapping, so it is refused instead of being rendered into invalid SQL.
    [Fact]
    public void MapFilter_NestedConditionalBody_ShouldBeRefused()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var map = new Dictionary<string, int> { ["a"] = 1 };

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter((k, v) => (v > 0 ? true : v < 0) == false, map))
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("A conditional inside a higher-order map lambda is supported only as the lambda body or a branch value.");
    }

    // T257 (#5): a captured map operand whose runtime value is not a dictionary is refused by
    // RenderMapOperand instead of being emitted as mapKeys(<scalar>). That guard is not reachable from
    // the public typed API — every map helper takes IDictionary<TKey,TValue>? — so it is stated here
    // rather than faked with a test that could only bypass the signature.

    // T257 (P1): a captured variable inside the lambda body must not throw on the parameter-only pass
    // of a plan-cache hit. Before the fix that pass cloned the lambda visitor and threw
    // NotSupportedException("Cannot clone in param mode"); now it visits the body in the same order as
    // the SQL pass, so the cached render returns the same SQL and binds the captured value.
    [Fact]
    public void MapLambdaFunctions_ShouldRenderOnAPlanCacheHitWithACapturedVariable()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var threshold = 0L;

        var cold = SqlOfCached(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter((k, v) => v > threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))),
            A = SqlFunctions.ClickHouse.map_apply((k, v) => v + threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            L = SqlFunctions.ClickHouse.map_all((k, v) => v > threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            X = SqlFunctions.ClickHouse.map_exists((k, v) => v > threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))
        }));

        var warm = SqlOfCached(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter((k, v) => v > threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))),
            A = SqlFunctions.ClickHouse.map_apply((k, v) => v + threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            L = SqlFunctions.ClickHouse.map_all((k, v) => v > threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
            X = SqlFunctions.ClickHouse.map_exists((k, v) => v > threshold, SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))
        }));

        using (new AssertionScope())
        {
            warm.Should().Be(cold, "the cached parameter pass must not change the rendered statement");
            warm.Should().Contain("arrayFilter(_t -> (_t.2 > @threshold)");
            warm.Should().Contain("arrayMap((k, v) -> (v + @threshold)");
            warm.Should().Contain("mapAll((k, v) -> (v > @threshold)");
            warm.Should().Contain("mapExists((k, v) -> (v > @threshold)");
        }
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

        sql.Should().Contain("lower(hex(MD5(somestring)))");
        sql.Should().Contain("lower(hex(SHA1(somestring)))");
        sql.Should().Contain("lower(hex(SHA256(somestring)))");
        sql.Should().Contain("lower(hex(SHA512(somestring)))");
        sql.Should().Contain("toInt32(xxHash32(somestring))");
        sql.Should().Contain("toInt64(xxHash64(somestring))");
        sql.Should().Contain("toInt64(xxh3(somestring))");
        sql.Should().Contain("toInt64(cityHash64(somestring))");
        sql.Should().Contain("toInt64(sipHash64(somestring))");
        sql.Should().Contain("lower(hex(sipHash128(somestring)))");
        sql.Should().Contain("toInt32(murmurHash2_32(somestring))");
        sql.Should().Contain("toInt64(murmurHash2_64(somestring))");
        sql.Should().Contain("toInt32(murmurHash3_32(somestring))");
        sql.Should().Contain("toInt64(murmurHash3_64(somestring))");
        sql.Should().Contain("lower(hex(murmurHash3_128(somestring)))");
    }

    [Fact]
    public void GenerateUlid_ShouldUseClickHouseName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { U = SqlFunctions.ClickHouse.generate_ulid() }))
            .Should().Contain("generateULID()");
    }

    /// <summary>
    /// T254: the six string-returning binary digests render as lowercase hexadecimal so the value
    /// survives the driver's string decoder instead of being read as raw <c>FixedString</c> bytes.
    /// The already-rendered argument is preserved verbatim — no pre-encoding, no wrapper.
    /// </summary>
    [Fact]
    public void T254_DigestRenderers_ShouldUseLowercaseHex()
    {
        var scalars = ClickHouseDialect.Instance.ScalarFunctions!;
        var cases = new (string Name, string Expected)[]
        {
            ("md5", "lower(hex(MD5(digest)))"),
            ("sha1", "lower(hex(SHA1(digest)))"),
            ("sha256", "lower(hex(SHA256(digest)))"),
            ("sha512", "lower(hex(SHA512(digest)))"),
            ("sip_hash128", "lower(hex(sipHash128(digest)))"),
            ("murmur_hash3_128", "lower(hex(murmurHash3_128(digest)))")
        };

        using (new AssertionScope())
        {
            foreach (var (name, expected) in cases)
            {
                scalars.Supports(name).Should().BeTrue(
                    "'{0}' stays in the ClickHouse inventory", name);
                scalars.Render(name, ["digest"]).Should().Be(
                    expected, "'{0}' must materialise as lowercase hexadecimal", name);
                scalars.Render(name, ["other"]).Should().Be(
                    expected.Replace("digest", "other", StringComparison.Ordinal),
                    "'{0}' must preserve the already-rendered argument", name);
            }
        }
    }

    /// <summary>
    /// T254: a nullable string column reaches the digest unwrapped. The null travels through the
    /// server expression and is never replaced by a default, an empty string or a coalesce, and the
    /// empty string stays a distinct input with its own digest.
    /// </summary>
    [Fact]
    public void T254_Digests_ShouldTranslateNullableInputsWithoutCoalescing()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M5 = SqlFunctions.ClickHouse.md5(x.String),
            S1 = SqlFunctions.ClickHouse.sha1(x.String),
            S256 = SqlFunctions.ClickHouse.sha256(x.String),
            S512 = SqlFunctions.ClickHouse.sha512(x.String),
            P128 = SqlFunctions.ClickHouse.sip_hash128(x.String),
            M3128 = SqlFunctions.ClickHouse.murmur_hash3_128(x.String)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("lower(hex(MD5(somestring)))");
            sql.Should().Contain("lower(hex(SHA1(somestring)))");
            sql.Should().Contain("lower(hex(SHA256(somestring)))");
            sql.Should().Contain("lower(hex(SHA512(somestring)))");
            sql.Should().Contain("lower(hex(sipHash128(somestring)))");
            sql.Should().Contain("lower(hex(murmurHash3_128(somestring)))");

            sql.Should().NotContain("coalesce", "a null digest input must stay null");
            sql.Should().NotContain("ifNull", "a null digest input must stay null");
            sql.Should().NotContain("assumeNotNull", "the nullable argument must not be unwrapped");
            sql.Should().NotContain("toNotNullable", "the nullable argument must not be re-typed");
        }

        // The empty string is a distinct input and keeps its own digest: it is not folded into NULL.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.md5("") }))
            .Should().Contain("lower(hex(MD5('')))");
    }

    /// <summary>
    /// A result leaf with no ClickHouse mapping: the dialect only reaches its generic <c>Type.Name</c>
    /// fallback for it, so the typed cast of a <c>map_apply</c> result must refuse the call.
    /// </summary>
    private sealed class UnsupportedMapLeaf
    {
        public int Value { get; set; }
    }
}
