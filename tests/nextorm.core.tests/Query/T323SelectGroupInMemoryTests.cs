using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>A row wide enough for the keyed/global/constant <c>SelectGroup</c> execution matrix.</summary>
public class T323InMemoryRow
{
    public int Id { get; set; }
    public string? Category { get; set; }
    public int? Bucket { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// A custom scalar function: its CLR body throws on purpose, so a test that reaches
/// <see cref="InvalidOperationException"/> proves the provider executed it instead of rejecting it.
/// </summary>
public static class T323InMemoryUdf
{
    /// <summary>The placeholder the SQL vendors would call as <c>t323_mem_udf</c>.</summary>
    [SqlFunction("t323_mem_udf")]
    public static decimal T323MemScalar(decimal value)
        => throw new InvalidOperationException("t323_mem_udf must never be evaluated in process.");
}

/// <summary>
/// T323 (#323) D4a: <c>SelectGroup</c> executes against the in-memory provider in all three resolved
/// modes. Where an explicit <c>GroupBy(...).Select(...)</c> equivalent exists, the two executions of the
/// same context are compared; the key-less modes assert their rows directly or against the explicit
/// <c>Where(...).Select(...).Distinct()</c> form. The rejection cases prove a provider-only function is
/// never executed in process: the custom function's CLR body would throw a different exception if it
/// were.
/// </summary>
public class T323SelectGroupInMemoryTests
{
    private readonly IDataContext _context;

    public T323SelectGroupInMemoryTests(InMemoryRepository sut)
    {
        _context = sut.DataProvider;
    }

    private EntityBuilder<T323InMemoryRow> Rows(params T323InMemoryRow[] rows)
        => _context.From<T323InMemoryRow>().WithData(rows);

    private static T323InMemoryRow Row(int id, string? category, int? bucket, decimal amount)
        => new() { Id = id, Category = category, Bucket = bucket, Amount = amount };

    [Fact]
    public void Keyed_SingleKeyWithCount_ShouldMatchExplicitGroupBy()
    {
        var e = Rows(
            Row(1, "a", null, 10m),
            Row(2, "a", null, 20m),
            Row(3, "b", null, 30m),
            Row(4, "c", null, 40m));

        var inferred = e.SelectGroup(x => new { x.Category, C = SqlFunctions.Sql.count() }).ToList();
        var explicitRows = e.GroupBy(x => x.Category)
            .Select(x => new { x.Category, C = SqlFunctions.Sql.count() })
            .ToList();

        inferred.Should().HaveCount(3);
        inferred.Should().BeEquivalentTo(explicitRows);
    }

    [Fact]
    public void Keyed_TwoKeysIncludingNull_ShouldGroupNullOnce()
    {
        var e = Rows(
            Row(1, "a", null, 1m),
            Row(2, "a", null, 2m),
            Row(3, "a", 7, 4m),
            Row(4, "b", 7, 8m));

        var inferred = e.SelectGroup(x => new { x.Category, x.Bucket, C = SqlFunctions.Sql.count() }).ToList();
        var explicitRows = e.GroupBy(x => new { x.Category, x.Bucket })
            .Select(x => new { x.Category, x.Bucket, C = SqlFunctions.Sql.count() })
            .ToList();

        inferred.Should().HaveCount(3);
        inferred.Count(g => g.Bucket is null).Should().Be(1, "a null key value forms exactly one group");
        inferred.Single(g => g.Bucket is null).C.Should().Be(2);
        inferred.Should().BeEquivalentTo(explicitRows);
    }

    [Fact]
    public void Keyed_SumAndCount_ShouldMatchExplicit()
    {
        var e = Rows(
            Row(1, "a", null, 1.5m),
            Row(2, "a", null, 2.5m),
            Row(3, "b", null, 7m),
            Row(4, "b", null, 3m));

        var inferred = e.SelectGroup(x => new
            {
                x.Category,
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();
        var explicitRows = e.GroupBy(x => x.Category)
            .Select(x => new
            {
                x.Category,
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        inferred.Should().BeEquivalentTo(explicitRows);
        inferred.Single(g => g.Category == "a").S.Should().Be(4.0m);
        inferred.Single(g => g.Category == "b").S.Should().Be(10.0m);
    }

    [Fact]
    public void GlobalAggregate_ShouldReturnOneRow()
    {
        var e = Rows(
            Row(1, "a", null, 1m),
            Row(2, "b", null, 2m),
            Row(3, "c", null, 3m));

        var rows = e.SelectGroup(x => new
            {
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        rows.Should().ContainSingle("a global aggregate folds the whole source into one row");
        rows[0].S.Should().Be(6.0m);
        rows[0].C.Should().Be(3);
    }

    [Fact]
    public void GlobalAggregate_EmptySource_ShouldReturnExistingEmptySemantics()
    {
        var e = Rows();

        var emptySum = e.Sum(x => x.Amount);
        var emptyCount = e.Count();

        var rows = e.SelectGroup(x => new
            {
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        rows.Should().ContainSingle("a global aggregate produces its single row even over no rows");
        rows[0].S.Should().Be(emptySum, "sum over no rows keeps the provider's existing value");
        rows[0].C.Should().Be(emptyCount, "count over no rows keeps the provider's existing value");
        emptySum.Should().Be(0m);
        emptyCount.Should().Be(0);
    }

    [Fact]
    public void GlobalAggregate_Having_ShouldFilterTheSingleRow()
    {
        var e = Rows(
            Row(1, "a", null, 1m),
            Row(2, "b", null, 2m));
        decimal threshold = 2m;

        e.Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) })
            .ToList()
            .Should().ContainSingle("the folded total exceeds the captured threshold");

        e.Having(x => SqlFunctions.Sql.sum(x.Amount) > 100m)
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) })
            .ToList()
            .Should().BeEmpty("the single aggregate row is filtered out");
    }

    [Fact]
    public void ConstantProjection_ShouldCollapseDuplicates()
    {
        var e = Rows(
            Row(1, "a", null, 1m),
            Row(2, "a", null, 1m),
            Row(3, "b", null, 2m),
            Row(4, "b", null, 2m));

        var inferred = e.SelectGroup(x => new { V = 1 }).ToList();
        var explicitRows = e.Select(x => new { V = 1 }).Distinct().ToList();

        inferred.Should().ContainSingle("a row-independent projection collapses to one distinct row");
        inferred.Should().BeEquivalentTo(explicitRows);
    }

    [Fact]
    public void ConstantProjection_EmptySource_ShouldReturnNoRows()
    {
        var e = Rows();

        e.SelectGroup(x => new { V = 1 }).ToList().Should().BeEmpty();
        e.Select(x => new { V = 1 }).Distinct().ToList().Should().BeEmpty();
    }

    [Fact]
    public void ConstantProjection_ConstantHaving_ShouldFilterRows()
    {
        var e = Rows(
            Row(1, "a", null, 1m),
            Row(2, "b", null, 2m));

        var inferred = e.Having(x => 1 > 0).SelectGroup(x => new { V = 1 }).ToList();
        var explicitRows = e.Where(x => 1 > 0).Select(x => new { V = 1 }).Distinct().ToList();

        inferred.Should().ContainSingle("the row-independent predicate keeps the source non-empty");
        inferred.Should().BeEquivalentTo(explicitRows);

        e.Having(x => 1 > 2)
            .SelectGroup(x => new { V = 1 })
            .ToList()
            .Should().BeEmpty("the row-independent predicate removes every source row");
    }

    [Fact]
    public void FilteredAggregate_ShouldRejectExplicitly()
    {
        var e = Rows(Row(1, "a", null, 1m));

        var act = () => e.SelectGroup(x => new
            {
                S = SqlFunctions.Sql.sum(x.Amount, () => x.Id > 0),
            })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Filtered aggregates*")
            .WithMessage("*in-memory provider*");
    }

    [Fact]
    public void CustomSqlFunction_ShouldRejectExplicitly()
    {
        var e = Rows(Row(1, "a", null, 1m));

        var act = () => e.SelectGroup(x => new
            {
                x.Id,
                V = T323InMemoryUdf.T323MemScalar(x.Amount),
            })
            .ToList();

        // The CLR body of T323MemScalar throws InvalidOperationException: only the provider rejection
        // can satisfy this assertion, so the method was never invoked.
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*T323MemScalar*")
            .WithMessage("*in-memory provider*");
    }

    [Fact]
    public void OrderBy_ShouldApplyToProjectedRows()
    {
        var e = Rows(
            Row(1, "a", null, 1m),
            Row(2, "b", null, 2m),
            Row(3, "b", null, 3m),
            Row(4, "b", null, 4m),
            Row(5, "c", null, 5m),
            Row(6, "c", null, 6m));

        // Column 2 is the projected count, the grouped path's supported ordinal ORDER BY form.
        var rows = e.OrderBy(2, OrderDirection.Asc)
            .SelectGroup(x => new { x.Category, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Select(r => r.Category).Should().ContainInOrder("a", "c", "b");
        rows.Select(r => r.C).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void InvalidHaving_ShouldBeRejectedAtPreparation()
    {
        var e = Rows(Row(1, "a", null, 1m));

        var act = () => e
            .Having(x => x.Category != null)
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) })
            .ToList();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*not covered by inferred keys*");
    }
}
