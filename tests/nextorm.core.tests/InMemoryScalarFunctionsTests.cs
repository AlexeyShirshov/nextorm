using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider evaluates the cross-provider scalar functions through
/// <c>InMemoryScalarFunctionRewriter</c>; these tests pin the SQL semantics it reproduces.
/// </summary>
public class InMemoryScalarFunctionsTests
{
    private readonly InMemoryRepository _sut;

    public InMemoryScalarFunctionsTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void StringFunctions_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity
            .Select(it => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lp = SqlFunctions.Sql.lpad("7", 3, "0"),
                Rp = SqlFunctions.Sql.rpad("7", 3, "0"),
                Rep = SqlFunctions.Sql.repeat("ab", 3),
                Rev = SqlFunctions.Sql.reverse("abc"),
                Sp = SqlFunctions.Sql.space(2),
                Tr = SqlFunctions.Sql.translate("abc", "ab", "xy"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65)
            })
            .ToList();

        r.Should().OnlyContain(x =>
            x.L == "abc" && x.R == "def" && x.Lp == "007" && x.Rp == "700" &&
            x.Rep == "ababab" && x.Rev == "cba" && x.Sp == "  " && x.Tr == "xyc" &&
            x.Asc == 65 && x.Ch == "A");
    }

    [Fact]
    public void ConcatWs_ShouldSkipNulls()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { F = SqlFunctions.Sql.concat_ws(",", "a", null, "b") })
            .ToList();

        r.Should().OnlyContain(x => x.F == "a,b");
    }

    [Fact]
    public void SqlServerOnlyFunctions_ShouldThrowBecauseNotSupported()
    {
        var select = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.SqlServer.patindex("%a%", "abc") })
            .ToList();

        var where = () => _sut.SimpleEntity
            .Where(it => SqlFunctions.SqlServer.json_path_exists("{}", "$"))
            .ToList();

        select.Should().Throw<NotSupportedException>().WithMessage("*patindex*not supported*");
        where.Should().Throw<NotSupportedException>().WithMessage("*json_path_exists*not supported*");
    }
}
