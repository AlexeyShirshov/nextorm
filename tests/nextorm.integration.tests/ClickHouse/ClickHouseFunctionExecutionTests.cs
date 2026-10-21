using System.Globalization;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the ClickHouse-native function surface against a live server (issue #78). The
/// SQL-shape assertions live in <c>tests/nextorm.clickhouse.tests/Functions</c>; here every
/// rendered call must actually run and return the documented value, which also pins the
/// result types the row reader materialises.
/// </summary>
public sealed class ClickHouseFunctionExecutionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void Utf8StringFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lru = SqlFunctions.Sql.left("Привет", 3),
                Rru = SqlFunctions.Sql.right("Привет", 3),
                Upper = SqlFunctions.ClickHouse.upper_utf8("привет"),
                Lower = SqlFunctions.ClickHouse.lower_utf8("ПРИВЕТ")
            })
            .First();

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lru.Should().Be("При");
        r.Rru.Should().Be("вет");
        r.Upper.Should().Be("ПРИВЕТ");
        r.Lower.Should().Be("привет");
    }

    [Fact]
    public void TrimFunctions_ShouldExecute_WithAndWithoutCharacters()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                TL = SqlFunctions.ClickHouse.trim_left("  x  "),
                TR = SqlFunctions.ClickHouse.trim_right("  x  "),
                TB = SqlFunctions.ClickHouse.trim_both("  x  "),
                TLC = SqlFunctions.ClickHouse.trim_left("xxabc", "x"),
                TRC = SqlFunctions.ClickHouse.trim_right("abcxx", "x"),
                TBC = SqlFunctions.ClickHouse.trim_both("xxabcxx", "x")
            })
            .First();

        r.TL.Should().Be("x  ");
        r.TR.Should().Be("  x");
        r.TB.Should().Be("x");
        r.TLC.Should().Be("abc");
        r.TRC.Should().Be("abc");
        r.TBC.Should().Be("abc");
    }

    [Fact]
    public void RegexpFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                One = SqlFunctions.ClickHouse.replace_regexp_one("abcabc", "a", "x"),
                All = SqlFunctions.ClickHouse.replace_regexp_all("abcabc", "a", "x"),
                M = SqlFunctions.ClickHouse.match("abc", "b"),
                E = SqlFunctions.ClickHouse.extract("abc123", "[0-9]+"),
                EA = SqlFunctions.ClickHouse.extract_all("a1b2", "[0-9]")
            })
            .First();

        r.One.Should().Be("xbcabc");
        r.All.Should().Be("xbcxbc");
        r.M.Should().BeTrue();
        r.E.Should().Be("123");
        r.EA.Should().HaveCount(2);
    }

    [Fact]
    public void SplitFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.split_by_string(",", "a,b,c"), "|"),
                R = SqlFunctions.ClickHouse.split_by_regexp("[,;]", "a,b;c"),
                W = SqlFunctions.ClickHouse.split_by_whitespace("a b  c"),
                C = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.split_by_string(",", "a,b,c"), string.Empty)
            })
            .First();

        r.S.Should().Be("a|b|c");
        r.R.Should().HaveCount(3);
        r.W.Should().HaveCount(3);
        r.C.Should().Be("abc");
    }

    [Fact]
    public void DateTimeFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.format_date_time(
                    SqlFunctions.ClickHouse.parse_date_time("2023-01-02", "%Y-%m-%d"), "%Y/%m/%d"),
                Ftz = SqlFunctions.ClickHouse.format_date_time(
                    SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-01-02", "UTC"), "%Y"),
                P = SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-01-02"),
                N = SqlFunctions.ClickHouse.now(),
                T = SqlFunctions.ClickHouse.today(),
                Y = SqlFunctions.ClickHouse.yesterday()
            })
            .First();

        r.F.Should().Be("2023/01/02");
        r.Ftz.Should().Be("2023");
        r.P.Should().NotBeNull();
        r.P!.Value.Year.Should().Be(2023);
        r.N.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Y.Should().Be(r.T!.Value.AddDays(-1));
    }

    [Fact]
    public void ArraySetFunctions_ShouldExecute()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Concat = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_concat(x.Nums, x.Nums)),
                Uniq = SqlFunctions.ClickHouse.array_uniq(x.Nums),
                Intersect = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_intersect(x.Nums, x.Nums)),
                Union = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_union(x.Nums, x.Nums)),
                Symmetric = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_symmetric_difference(x.Nums, x.Nums)),
                Flatten = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_flatten(new[]
                {
                    new[] { 1L, 2L },
                    new[] { 3L }
                })),
                Sorted = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_sort(x.Nums), ","),
                Reversed = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_reverse(x.Nums), ","),
                Distinct = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_distinct(x.Nums), ",")
            })
            .First();

        r.Concat.Should().Be(6);
        r.Uniq.Should().Be(3);
        r.Intersect.Should().Be(3);
        r.Union.Should().Be(3);
        r.Symmetric.Should().Be(0);
        r.Flatten.Should().Be(3);
        r.Sorted.Should().Be("1,2,3");
        r.Reversed.Should().Be("2,1,3");
        r.Distinct.Should().Be("3,1,2");
    }

    /// <summary>
    /// The <c>range</c> arity arms of the ClickHouse array table: one argument (<c>0..end</c>), two
    /// (<c>start..end</c>) and three (<c>start..end</c> with a step). Each array is joined to a string
    /// so the assertion pins the element order without depending on the array materialiser.
    /// </summary>
    [Fact]
    public void ArrayRangeArity_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                One = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.range(3), ","),
                Two = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.range(1, 4), ","),
                Three = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.range(1, 7, 2), ",")
            })
            .First();

        r.One.Should().Be("0,1,2");
        r.Two.Should().Be("1,2,3");
        r.Three.Should().Be("1,3,5");
    }

    [Fact]
    public void MapFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Keys = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L), Tuple.Create("b", 2L)))),
                Values = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_values(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L)))),
                ContainsKey = SqlFunctions.ClickHouse.map_contains_key(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L)), "a"),
                ContainsValue = SqlFunctions.ClickHouse.map_contains_value(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L)), 1L),
                Added = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_add(
                    SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)),
                    SqlFunctions.ClickHouse.map(Tuple.Create("b", 2L))))),
                Concatenated = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_concat(
                    SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)),
                    SqlFunctions.ClickHouse.map(Tuple.Create("b", 2L))))),
            })
            .First();

        r.Keys.Should().Be(2);
        r.Values.Should().Be(1);
        r.ContainsKey.Should().BeTrue();
        r.ContainsValue.Should().BeTrue();
        r.Added.Should().Be(2);
        r.Concatenated.Should().Be(2);
    }

    [Fact]
    public void HashFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Md5 = SqlFunctions.ClickHouse.md5("abc"),
                Sha1 = SqlFunctions.ClickHouse.sha1("abc"),
                Sha256 = SqlFunctions.ClickHouse.sha256("abc"),
                Sha512 = SqlFunctions.ClickHouse.sha512("abc"),
                Xx32 = SqlFunctions.ClickHouse.xx_hash32("abc"),
                Xx64 = SqlFunctions.ClickHouse.xx_hash64("abc"),
                Xxh3 = SqlFunctions.ClickHouse.xxh3("abc"),
                City = SqlFunctions.ClickHouse.city_hash64("abc"),
                Sip64 = SqlFunctions.ClickHouse.sip_hash64("abc"),
                Sip128 = SqlFunctions.ClickHouse.sip_hash128("abc"),
                Murmur232 = SqlFunctions.ClickHouse.murmur_hash2_32("abc"),
                Murmur264 = SqlFunctions.ClickHouse.murmur_hash2_64("abc"),
                Murmur332 = SqlFunctions.ClickHouse.murmur_hash3_32("abc"),
                Murmur364 = SqlFunctions.ClickHouse.murmur_hash3_64("abc"),
                Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128("abc")
            })
            .First();

        // ClickHouse returns the digest as FixedString: the raw bytes, not a hex rendering.
        r.Md5.Should().NotBeNullOrEmpty();
        r.Sha1.Should().NotBeNullOrEmpty();
        r.Sha256.Should().NotBeNullOrEmpty();
        r.Sha512.Should().NotBeNullOrEmpty();
        r.Xx32.Should().NotBe(0);
        r.Xx64.Should().NotBe(0);
        r.Xxh3.Should().NotBe(0);
        r.City.Should().NotBe(0);
        r.Sip64.Should().NotBe(0);
        r.Sip128.Should().NotBeNullOrEmpty();
        r.Murmur232.Should().NotBe(0);
        r.Murmur264.Should().NotBe(0);
        r.Murmur332.Should().NotBe(0);
        r.Murmur364.Should().NotBe(0);
        r.Murmur3128.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UlidAndLengthFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ulid = SqlFunctions.ClickHouse.generate_ulid(),
                Degrees = SqlFunctions.Sql.degrees(3.141592653589793),
                Radians = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi()
            })
            .First();

        r.Ulid.Should().HaveLength(26);
        r.Degrees.Should().BeApproximately(180.0, 1e-9);
        r.Radians.Should().BeApproximately(Math.PI, 1e-9);
        r.Pi.Should().BeApproximately(Math.PI, 1e-9);
    }

    /// <summary>
    /// #252: <c>array_except</c> is declared for API compatibility, but the native <c>arrayExcept</c>
    /// exists only from ClickHouse 25.9. On the pinned 25.8 baseline the call must be refused at
    /// translation/preparation time instead of reaching the server as an <c>UNKNOWN_FUNCTION</c>.
    /// </summary>
    [Fact]
    public void ArrayExcept_ShouldRefuseBeforeTouchingTheServer()
    {
        var act = () => _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums)
            })
            .First();

        act.Should().Throw<NotSupportedException>(
                "the pinned 25.8 server has no arrayExcept, so the capability gate must refuse early")
            .WithMessage("The array_except function is not supported by this provider.");
    }

    /// <summary>
    /// D0 live server characterization for #252: the pinned image
    /// <c>clickhouse/clickhouse-server:25.8-alpine</c> reports 25.8.33.6, keeps no
    /// <c>arrayExcept</c> entry in <c>system.functions</c>, and rejects the raw call with Code 46
    /// (UNKNOWN_FUNCTION). Raw ADO.NET keeps this oracle independent of the nextorm translator.
    /// </summary>
    [Fact]
    public async Task ServerCharacterization_ArrayExceptIsAbsentOnThePinnedServer()
    {
        await using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        static async Task<object?> ScalarAsync(ClickHouseConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        var version = await ScalarAsync(connection, "SELECT version()");
        version.Should().Be("25.8.33.6", "the collection pins clickhouse/clickhouse-server:25.8-alpine");

        var functions = await ScalarAsync(
            connection,
            "SELECT count() FROM system.functions WHERE name = 'arrayExcept'");
        Convert.ToInt64(functions, CultureInfo.InvariantCulture)
            .Should().Be(0, "arrayExcept was introduced in server 25.9 and the pinned server is 25.8");

        Func<Task> act = async () => await ScalarAsync(connection, "SELECT arrayExcept([1], [1])");
        var thrown = await act.Should().ThrowAsync<ClickHouseServerException>(
            "the pinned 25.8 server has no arrayExcept function");
        thrown.Which.Message.Should().Contain("Code: 46")
            .And.Contain("arrayExcept", "the server reports the unknown function by name");
    }

}
