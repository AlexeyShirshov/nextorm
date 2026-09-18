using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Tests that assert MySQL specific behaviour, backed by a Testcontainers instance unless
/// NEXTORM_MYSQL_CONNECTION points at an existing server.
/// </summary>
public sealed class MySqlSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    [Fact]
    public void Stdev_ShouldMatchSampleStandardDeviation()
    {
        _sut.SimpleEntity
            .Select(x => NORM.SQL.stdev((double)x.Id))
            .First()
            .Should().BeApproximately(3.0276503540974917, 1e-12);
    }

    [Fact]
    public void StringConcat_ShouldUseConcatFunction()
    {
        // MySQL's infix || is a logical OR, so the provider must render concat(...).
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => x.String + "/" + x.RequiredString)
            .First();

        value.Should().Be("dadfasd/sdf");
    }

    [Fact]
    public void OrderByAscending_NullsFirst_ShouldSortData()
    {
        // MySQL sorts NULL first ascending; the row with the NULL nullableint is id 1.
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void Offset_WithoutLimit_ShouldReturnRemainingRows()
    {
        var ids = _sut.SimpleEntity.Offset(8).Select(it => it.Id).ToList();

        ids.Should().Equal(9, 10);
    }

    [Fact]
    public void QueryHint_ShouldThrowBecauseMySqlHasNoQueryHints()
    {
        var query = _sut.SimpleEntity.Select(it => it.Id).Hint("recompile");

        var act = () => query.ToList();

        act.Should().Throw<NotSupportedException>();
    }
}
