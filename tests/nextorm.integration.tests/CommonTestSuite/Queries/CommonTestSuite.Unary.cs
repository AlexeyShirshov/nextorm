using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Not_InWhere_ShouldFilterNegatedRows()
    {
        var ids = _sut.ComplexEntity
            .Where(e => !e.Boolean!.Value)
            .Select(e => e.Id)
            .ToList();

        // rows 2 and 3 have b = false.
        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void Not_WhenProjected_ShouldReturnNegatedBoolean()
    {
        var values = _sut.ComplexEntity
            .Where(e => e.Id <= 2)
            .Select(e => new { e.Id, NotB = !e.Boolean!.Value })
            .ToList();

        // row 1 has b = true, row 2 has b = false.
        values.Single(x => x.Id == 1).NotB.Should().BeFalse();
        values.Single(x => x.Id == 2).NotB.Should().BeTrue();
    }

    [Fact]
    public void Negate_ShouldReturnNegatedValue()
    {
        var values = _sut.ComplexEntity
            .Select(e => -e.Id)
            .ToList();

        // ids 1, 2 and 3 become -1, -2 and -3.
        values.OrderBy(x => x).Should().Equal(-3L, -2L, -1L);
    }

    [Fact]
    public void OnesComplement_ShouldReturnBitwiseComplement()
    {
        var values = _sut.ComplexEntity
            .Where(e => e.Int != null)
            .Select(e => ~e.Int!.Value)
            .ToList();

        // nullableint is 1 for rows 2 and 3, and ~1 == -2.
        values.Should().AllBeEquivalentTo(-2);
    }
}
