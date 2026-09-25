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

    [Fact]
    public void RangeOverlaps_ShouldUsePostgresSemantics()
    {
        var a = new Range<int>(10, 20);
        var overlapping = new Range<int>(15, 25);
        var touchingAtExclusiveBound = new Range<int>(20, 30);

        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.overlaps(a, overlapping)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.overlaps(a, touchingAtExclusiveBound)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.overlaps(a, Range<int>.Empty)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void RangeContains_ShouldUsePostgresSemantics()
    {
        var range = new Range<int>(10, 20);
        var inner = new Range<int>(12, 18);
        var overlappingOutside = new Range<int>(18, 25);

        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contains(range, 15)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contains(range, 20)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contains(range, inner)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contained_by(inner, range)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contained_by(overlappingOutside, range)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void RangeInspectionFunctions_ShouldUsePostgresSemantics()
    {
        var range = new Range<int>(10, 20);
        var unbounded = new Range<int>(10, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true);

        var result = _sut.SimpleEntity.Select(it => new
        {
            L = SqlFunctions.Postgres.lower(range),
            U = SqlFunctions.Postgres.upper(range),
            E = SqlFunctions.Postgres.isempty(Range<int>.Empty),
            Li = SqlFunctions.Postgres.lower_inc(range),
            Ui = SqlFunctions.Postgres.upper_inc(range),
            Lf = SqlFunctions.Postgres.lower_inf(unbounded),
            Uf = SqlFunctions.Postgres.upper_inf(unbounded),
            Elf = SqlFunctions.Postgres.lower_inf(Range<int>.Empty),
            Euf = SqlFunctions.Postgres.upper_inf(Range<int>.Empty)
        }).ToList();

        result.Should().OnlyContain(x => x.L == 10 && x.U == 20 && x.E && x.Li && !x.Ui && !x.Lf && x.Uf && !x.Elf && !x.Euf);
    }

    [Fact]
    public void RangeOperatorFunctions_ShouldThrowBecauseNotSupportedInMemory()
    {
        var a = new Range<int>(1, 10);
        var b = new Range<int>(5, 20);

        var act = () => _sut.SimpleEntity
            .Where(it => SqlFunctions.Postgres.range_adjacent(a, b))
            .Select(it => it.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*range_adjacent*not supported*");
    }
}
