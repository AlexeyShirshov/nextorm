using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the cross-provider scalar functions every SQL provider can express
/// (<c>left</c>/<c>right</c>, <c>concat_ws</c>, <c>ascii</c>/<c>char</c>, <c>mod</c>, <c>log10</c>,
/// <c>power</c>) against the seeded <c>ComplexEntity</c>. The functions that only some providers have
/// (<c>lpad</c>/<c>rpad</c>, <c>repeat</c>, <c>reverse</c>, <c>space</c>, <c>translate</c>) are covered
/// per provider by the SQL-generation tests and, for PostgreSQL, by <see cref="PostgresFunctionsTests"/>.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void CrossProviderScalarFunctions_ShouldCompute()
    {
        // A captured variable is bound as a string parameter, so SQL Server's non-Unicode inline
        // literal handling does not mangle the multi-byte value.
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
                Cw = SqlFunctions.Sql.concat_ws("-", "a", "b", "c"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65)
            })
            .First();

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lru.Should().Be("При");
        r.Rru.Should().Be("вет");
        r.Cw.Should().Be("a-b-c");
        r.Asc.Should().Be(65);
        r.Ch.Should().Be("A");
    }

    [Fact]
    public void CrossProviderScalarFunctions_ConcatWsNull_ShouldSkipNulls()
    {
        // On every provider except ClickHouse, concat_ws skips NULL arguments (ClickHouse's
        // concatWithSeparator returns NULL instead; pinned in ClickHouseScalarFunctionsTests).
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => SqlFunctions.Sql.concat_ws("-", "a", null, "b"))
            .First();

        r.Should().Be("a-b");
    }

    [Fact]
    public void CrossProviderScalarFunctions_NumericAndLength_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                Bl = SqlFunctions.Sql.bit_length("abc"),
                Ol = SqlFunctions.Sql.octet_length("abc"),
                Deg = SqlFunctions.Sql.degrees(Math.PI),
                Rad = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi(),
                Acos = Math.Acos(1.0),
                Atan2 = Math.Atan2(1.0, 1.0)
            })
            .First();

        r.Bl.Should().Be(24);
        r.Ol.Should().Be(3);
        ((double?)r.Deg).GetValueOrDefault().Should().BeApproximately(180.0, 1e-6);
        ((double?)r.Rad).GetValueOrDefault().Should().BeApproximately(Math.PI, 1e-6);
        ((double?)r.Pi).GetValueOrDefault().Should().BeApproximately(Math.PI, 1e-6);
        ((double?)r.Acos).GetValueOrDefault().Should().BeApproximately(0.0, 1e-6);
        ((double?)r.Atan2).GetValueOrDefault().Should().BeApproximately(Math.PI / 4, 1e-6);
    }
}
