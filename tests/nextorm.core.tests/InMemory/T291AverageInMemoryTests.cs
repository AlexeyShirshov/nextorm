using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T291 (#291) in-memory arm: the in-memory provider folds the mean as a fraction
/// (<c>sum / count</c>) and then converts it back to the declared result type. When that declared type
/// came from the integer selector, the conversion silently truncated; these facts pin the mapped
/// nullable-double result for every integral width, the DISTINCT form, the nullable input and both
/// window shapes.
/// </summary>
public class T291AverageInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public T291AverageInMemoryTests(InMemoryRepository sut) => _sut = sut;

    private static void ShouldBeDouble(object? value, double expected)
    {
        Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(expected, 1e-12);
        value.Should().BeOfType<double>();
    }

    [Fact]
    public void OrdinaryAverage_ShouldKeepFraction()
    {
        _sut.AvgFixture.WithData(new[]
        {
            new AvgFixture { Id = 1, B = 1, S = 1, I = 1, L = 1 },
            new AvgFixture { Id = 2, B = 2, S = 2, I = 2, L = 2 },
        });

        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.B)).First(), 1.5);
        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.S)).First(), 1.5);
        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1.5);
        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.L)).First(), 1.5);
        ShouldBeDouble(_sut.AvgFixture.Avg(x => x.I), 1.5);
    }

    [Fact]
    public void DistinctAverage_ShouldKeepFraction()
    {
        _sut.AvgFixture.WithData(new[]
        {
            new AvgFixture { Id = 1, I = 1 },
            new AvgFixture { Id = 2, I = 2 },
            new AvgFixture { Id = 3, I = 1 },
        });

        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg_distinct(x.I)).First(), 1.5);
        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 4d / 3d);
    }

    [Fact]
    public void NullableAverage_ShouldIgnoreNulls()
    {
        _sut.AvgFixture.WithData(new[]
        {
            new AvgFixture { Id = 1, NI = null },
            new AvgFixture { Id = 2, NI = 1 },
            new AvgFixture { Id = 3, NI = 2 },
        });

        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.NI)).First(), 1.5);
    }

    [Fact]
    public void OneRowAverage_ShouldBeOne()
    {
        _sut.AvgFixture.WithData(new[] { new AvgFixture { Id = 1, I = 1 } });

        ShouldBeDouble(_sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1d);
    }

    [Fact]
    public void EmptyAverage_ShouldBeNull()
    {
        _sut.AvgFixture.WithData(Array.Empty<AvgFixture>());

        object? value = _sut.AvgFixture.Select(x => SqlFunctions.Sql.avg(x.I)).First();

        value.Should().BeNull();
    }
}
