using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the cross-provider scalar functions against ClickHouse. ClickHouse does not derive the
/// shared <see cref="CommonTestSuite"/> (its divergences live in <see cref="ClickHouseIntegrationTests"/>),
/// so the same shared assertions are pinned here for the container-backed provider.
/// </summary>
public sealed class ClickHouseScalarFunctionsTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

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
}
