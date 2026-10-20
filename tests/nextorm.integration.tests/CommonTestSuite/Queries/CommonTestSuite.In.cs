using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void In_WithThreeValues_ShouldFilter()
    {
        var ids = _sut.ComplexEntity
            .Where(e => SqlFunctions.Sql.@in(e.Id, new long[] { 1, 3, 10 }))
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1L, 3L);
    }

    [Fact]
    public void In_WithZeroValues_ShouldReturnEmpty()
    {
        var ids = _sut.ComplexEntity
            .Where(e => SqlFunctions.Sql.@in(e.Id, Array.Empty<long>()))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void In_WithOneValue_ShouldFilter()
    {
        var ids = _sut.ComplexEntity
            .Where(e => SqlFunctions.Sql.@in(e.Id, new long[] { 2 }))
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
            .Where(e => SqlFunctions.Sql.@in(e.Int, values))
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
            .Where(e => SqlFunctions.Sql.@in(e.Int, values))
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

    // ------------------------------------------------------------------ D193 tuple value-list IN

    private ISqlDialect TupleInDialect => ((DataContext)_sut.DataProvider).Dialect;

    [Fact]
    public void Contains_TupleIn_CapturedList_ShouldFilter()
    {
        Assert.SkipUnless(TupleInDialect.Tuple is not null, "This provider has no row-value constructor.");

        var tuples = new List<(long Id, int? Int)> { (2, 1) };

        var ids = _sut.ComplexEntity
            .Where(e => tuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(2L);
    }

    [Fact]
    public void Contains_TupleIn_MismatchedComponents_ShouldReturnEmpty()
    {
        Assert.SkipUnless(TupleInDialect.Tuple is not null, "This provider has no row-value constructor.");

        var tuples = new List<(long, int?)> { (1, 1), (2, 2) };

        var ids = _sut.ComplexEntity
            .Where(e => tuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void Contains_TupleIn_Empty_ShouldReturnEmpty()
    {
        Assert.SkipUnless(TupleInDialect.Tuple is not null, "This provider has no row-value constructor.");

        var tuples = new List<(long, int?)>();

        var ids = _sut.ComplexEntity
            .Where(e => tuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void Contains_TupleIn_NullComponent_ShouldMatchNullRow()
    {
        Assert.SkipUnless(TupleInDialect.Tuple is not null, "This provider has no row-value constructor.");

        var tuples = new List<(long, int?)> { (1, null) };

        var ids = _sut.ComplexEntity
            .Where(e => tuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        // id 1 has a null nullableint; a null tuple component must match it deterministically.
        ids.Should().Equal(1L);
    }

    [Fact]
    public void Contains_TupleIn_Negation_ShouldExcludeNullComponentMatch()
    {
        Assert.SkipUnless(TupleInDialect.Tuple is not null, "This provider has no row-value constructor.");

        var tuples = new List<(long, int?)> { (1, null) };

        var ids = _sut.ComplexEntity
            .Where(e => !tuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void Contains_TupleIn_OnProviderWithoutRowConstructor_ShouldReject()
    {
        Assert.SkipUnless(TupleInDialect.Tuple is null, "This provider supports tuple IN/Contains.");

        var tuples = new List<(long, int?)> { (2, 1) };

        var act = () => _sut.ComplexEntity
            .Where(e => tuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }
}
