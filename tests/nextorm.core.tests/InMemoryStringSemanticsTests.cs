using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider executes the CLR expressions natively, so the string-semantics surface must
/// produce the native C# results (and <c>collate</c> is the ordinal identity).
/// </summary>
public class InMemoryStringSemanticsTests
{
    private readonly InMemoryRepository _sut;

    public InMemoryStringSemanticsTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void ToStringFormat_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity.Select(it => new { F = it.Id.ToString("F2") }).ToList();

        r.Should().Contain(x => x.F == "1.00");
        r.Should().Contain(x => x.F == "2.00");
    }

    [Fact]
    public void InterpolationFormat_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity.Select(it => new { F = $"{it.Id:D4}" }).ToList();

        r.Should().Contain(x => x.F == "0001");
        r.Should().Contain(x => x.F == "0002");
    }

    [Fact]
    public void Collate_ShouldBeTheOrdinalIdentity()
    {
        var r = _sut.SimpleEntity.Select(it => new { F = SqlFunctions.Sql.collate(it.Id.ToString(), "C") }).ToList();

        r.Should().Contain(x => x.F == "1");
        r.Should().Contain(x => x.F == "2");
    }

    [Fact]
    public void CompareOrdinal_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity
            .Where(it => string.CompareOrdinal(it.Id.ToString(), "1") == 0)
            .Select(it => new { it.Id })
            .ToList();

        r.Should().ContainSingle();
        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void ToUpperInvariant_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity.Select(it => new { F = it.Id.ToString().ToUpperInvariant() }).ToList();

        r.Should().Contain(x => x.F == "1");
    }
}
