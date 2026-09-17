using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void In_WithThreeValues_ShouldFilter()
    {
        var ids = _sut.ComplexEntity
            .Where(e => NORM.SQL.@in(e.Id, new long[] { 1, 3, 10 }))
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1L, 3L);
    }

    [Fact]
    public void In_WithZeroValues_ShouldReturnEmpty()
    {
        var ids = _sut.ComplexEntity
            .Where(e => NORM.SQL.@in(e.Id, Array.Empty<long>()))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void In_WithOneValue_ShouldFilter()
    {
        var ids = _sut.ComplexEntity
            .Where(e => NORM.SQL.@in(e.Id, new long[] { 2 }))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(2L);
    }

    [Fact]
    public void Contains_CapturedList_ShouldFilter()
    {
        var values = new List<long> { 1, 3 };

        var ids = _sut.ComplexEntity
            .Where(e => values.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1L, 3L);
    }

    [Fact]
    public void Contains_CapturedArray_ShouldFilter()
    {
        var values = new long[] { 2, 3 };

        var ids = _sut.ComplexEntity
            .Where(e => values.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void In_ListContainingNull_ShouldMatchNullColumn()
    {
        var values = new int?[] { 1, null };

        var ids = _sut.ComplexEntity
            .Where(e => NORM.SQL.@in(e.Int, values))
            .Select(e => e.Id)
            .ToList();

        // id 1 has a null nullableint, ids 2 and 3 have 1.
        ids.OrderBy(x => x).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void In_ListWithOnlyNull_ShouldMatchNullColumn()
    {
        var values = new int?[] { null };

        var ids = _sut.ComplexEntity
            .Where(e => NORM.SQL.@in(e.Int, values))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1L);
    }

    [Fact]
    public void Contains_CapturedStringListWithNull_ShouldMatchNullColumn()
    {
        var values = new List<string?> { "xxx", null };

        var ids = _sut.ComplexEntity
            .Where(e => values.Contains(e.String))
            .Select(e => e.Id)
            .ToList();

        // row 2 has "xxx", row 3 has a null string.
        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void Contains_CapturedList_GrownBetweenExecutions_ShouldNotReturnStaleResults()
    {
        var values = new List<long> { 1 };
        var query = _sut.ComplexEntity.Where(e => values.Contains(e.Id)).Select(e => e.Id);

        query.ToList().Should().Equal(1L);

        values.Add(3);

        query.ToList().OrderBy(x => x).Should().Equal(1L, 3L);
    }

    [Fact]
    public void Contains_CapturedArray_ReassignedBetweenExecutions_ShouldNotReturnStaleResults()
    {
        var values = new long[] { 1 };
        var query = _sut.ComplexEntity.Where(e => values.Contains(e.Id)).Select(e => e.Id);

        query.ToList().Should().Equal(1L);

        values = new long[] { 2, 3 };

        query.ToList().OrderBy(x => x).Should().Equal(2L, 3L);
    }
}
