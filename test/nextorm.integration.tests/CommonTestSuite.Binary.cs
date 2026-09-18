using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

public abstract partial class CommonTestSuite
{
    private static readonly byte[] ExpectedData = [1, 2, 3, 4];

    [Fact]
    public void BinaryColumn_ShouldRoundTripIntoEntityProperty()
    {
        var row = _sut.BinaryEntity.Where(it => it.Id == 1).First();

        row.Data.Should().Equal(ExpectedData);
    }

    [Fact]
    public void BinaryColumn_ShouldRoundTripAsScalarProjection()
    {
        var data = _sut.BinaryEntity.Where(it => it.Id == 1).Select(it => it.Data).First();

        data.Should().Equal(ExpectedData);
    }

    [Fact]
    public void BinaryColumn_ShouldRoundTripInsideAnonymousType()
    {
        var row = _sut.BinaryEntity.Where(it => it.Id == 1).Select(it => new { it.Id, it.Data }).First();

        row.Data.Should().Equal(ExpectedData);
    }

    [Fact]
    public void BinaryColumn_ShouldRoundTripAsScalarList()
    {
        var rows = _sut.BinaryEntity.OrderBy(it => it.Id).Select(it => it.Data).ToList();

        rows.Should().HaveCount(2);
        rows[0].Should().Equal(ExpectedData);
        rows[1].Should().BeNull();
    }

    [Fact]
    public void BinaryColumn_ShouldCompareAgainstParameter()
    {
        var row = _sut.BinaryEntity.Where(it => it.Data == NORM.Param<byte[]>(0)).First(ExpectedData);

        row.Data.Should().Equal(ExpectedData);
    }

    [Fact]
    public void BinaryColumn_ShouldReadNull()
    {
        var row = _sut.BinaryEntity.Where(it => it.Id == 2).First();

        row.Data.Should().BeNull();

        var data = _sut.BinaryEntity.Where(it => it.Id == 2).Select(it => it.Data).FirstOrDefault();

        data.Should().BeNull();
    }
}
