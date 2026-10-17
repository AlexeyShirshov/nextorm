using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes a representative subset of the ClickHouse-native function gaps (issue #78) against a real
/// ClickHouse server: the UTF-8/regexp/split strings, the date format/parse helpers, the array set
/// operations, the map family, the hashes and <c>generateULID</c>.
/// </summary>
public sealed class ClickHouseFunctionGapTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void Utf8AndRegexpStrings_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                L = SqlFunctions.ClickHouse.lower_utf8("ПРИВЕТ"),
                U = SqlFunctions.ClickHouse.upper_utf8("привет"),
                T = SqlFunctions.ClickHouse.trim_left("  x  "),
                R = SqlFunctions.ClickHouse.replace_regexp_all("a-b-c", "-", "_"),
                M = SqlFunctions.ClickHouse.match("abc", "b"),
                E = SqlFunctions.ClickHouse.extract("abc123", "[0-9]+"),
                EA = SqlFunctions.ClickHouse.extract_all("a1b2", "[0-9]"),
                S = SqlFunctions.ClickHouse.split_by_string(",", "a,b,c")
            })
            .First();

        r.L.Should().Be("привет");
        r.U.Should().Be("ПРИВЕТ");
        r.T.Should().Be("x  ");
        r.R.Should().Be("a_b_c");
        r.M.Should().BeTrue();
        r.E.Should().Be("123");
        r.EA.Should().Equal("1", "2");
        r.S.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void DateTime_ShouldFormatParseAndCompute()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                F = SqlFunctions.ClickHouse.format_date_time(e.Datetime, "%Y-%m-%d"),
                P = SqlFunctions.ClickHouse.parse_date_time("2023-01-01", "%Y-%m-%d"),
                B = SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-01-01"),
                N = SqlFunctions.ClickHouse.now(),
                T = SqlFunctions.ClickHouse.today()
            })
            .First();

        r.F.Should().Be("2023-01-01");
        r.P.Should().Be(new DateTime(2023, 1, 1));
        r.B.Should().Be(new DateTime(2023, 1, 1));
        r.N.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(1));
        r.T.Should().NotBeNull();
        r.T!.Value.Date.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public void ArraySetOperations_ShouldCompute()
    {
        var r = _sut.ArrayEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                C = SqlFunctions.ClickHouse.array_concat(e.Nums, e.Nums),
                I = SqlFunctions.ClickHouse.array_intersect(e.Nums, e.Nums),
                Q = SqlFunctions.ClickHouse.array_uniq(e.Nums)
            })
            .First();

        r.C.Should().Equal(3, 1, 2, 3, 1, 2);
        r.I.Should().BeEquivalentTo(new[] { 3, 1, 2 });
        r.Q.Should().Be(3);
    }

    [Fact]
    public void MapFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                K = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L), Tuple.Create("b", 2L))),
                Has = SqlFunctions.ClickHouse.map_contains_key(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)), "a")
            })
            .First();

        r.K.Should().Equal("a", "b");
        r.Has.Should().BeTrue();
    }

    [Fact]
    public void HashAndUlidFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                H = SqlFunctions.ClickHouse.xx_hash64("abc"),
                U = SqlFunctions.ClickHouse.generate_ulid()
            })
            .First();

        r.H.Should().NotBe(0);
        r.U.Should().NotBeNullOrEmpty();
        r.U!.Length.Should().Be(26);
    }
}
