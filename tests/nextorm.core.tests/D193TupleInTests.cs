using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>Plain in-memory row for the D193 tuple membership semantics.</summary>
public class TupleInMemoryEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? Score { get; set; }
}

/// <summary>
/// D193.3 — core semantics of the tuple value-list <c>IN</c>/<c>Contains</c> path (#193). In-memory
/// execution proves the C# <c>Contains</c> result (match, mismatch, empty, nullable component,
/// negation); direct <see cref="InValues.PartitionTuple(object?, Type)"/> tests pin the fail-closed
/// shape rules (null collection, arity ≥ 8, nested tuple, malformed arity, null tuple entry) that the
/// SQL translators must reject before rendering.
/// </summary>
public class D193TupleInTests
{
    private readonly InMemoryRepository _sut;

    public D193TupleInTests(InMemoryRepository sut) => _sut = sut;

    private EntityBuilder<TupleInMemoryEntity> Source(params TupleInMemoryEntity[] rows)
    {
        var entity = _sut.DataProvider.From<TupleInMemoryEntity>();
        entity.WithData(rows);
        return entity;
    }

    private static TupleInMemoryEntity[] Seed() =>
    [
        new() { Id = 1, Name = "a", Score = 10 },
        new() { Id = 2, Name = "b", Score = 20 },
        new() { Id = 3, Name = "c", Score = 30 },
        new() { Id = 4, Name = null, Score = null },
    ];

    [Fact]
    public void Contains_ValueTupleList_ShouldMatchBothRows()
    {
        var source = Source(Seed());
        var tuples = new List<(int Id, string? Name)> { (1, "a"), (3, "c") };

        var ids = source.Where(x => tuples.Contains(new ValueTuple<int, string?>(x.Id, x.Name))).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void Contains_ValueTupleList_MismatchedComponent_ShouldReturnNoRows()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?)> { (1, "b"), (2, "zzz") };

        var ids = source.Where(x => tuples.Contains(new ValueTuple<int, string?>(x.Id, x.Name))).Select(x => x.Id).ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReferenceTupleList_ShouldMatchRow()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int, string?>> { Tuple.Create(2, (string?)"b") };

        var ids = source.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Name))).Select(x => x.Id).ToList();

        ids.Should().Equal(2);
    }

    [Fact]
    public void Contains_ValueTupleArityOne_ShouldMatchBothRows()
    {
        var source = Source(Seed());
        var tuples = new List<ValueTuple<int>> { new(1), new(3) };

        var ids = source.Where(x => tuples.Contains(new ValueTuple<int>(x.Id))).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void Contains_ValueTupleAritySeven_ShouldMatchBothRows()
    {
        var source = Source(Seed());
        var tuples = new List<(int, int, int, int, int, int, int)>
        {
            (1, 2, 2, 3, 4, 5, 6),
            (3, 4, 2, 3, 4, 5, 6),
        };

        var ids = source
            .Where(x => tuples.Contains(new ValueTuple<int, int, int, int, int, int, int>(x.Id, x.Id + 1, 2, 3, 4, 5, 6)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void Contains_ReferenceTupleArityThree_ShouldMatchBothRows()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int, string?, int?>>
        {
            Tuple.Create(1, (string?)"a", (int?)10),
            Tuple.Create(3, (string?)"c", (int?)30),
        };

        var ids = source.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Name, x.Score))).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void Contains_ValueTupleArityThree_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?, int?)> { (1, "a", 10), (3, "c", 30) };

        source.Where(x => tuples.Contains(new ValueTuple<int, string?, int?>(x.Id, x.Name, x.Score))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<(int, string?, int?)> { (1, "a", 999) };
        source.Where(x => mismatch.Contains(new ValueTuple<int, string?, int?>(x.Id, x.Name, x.Score))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ValueTupleArityFour_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?, int?, int)> { (1, "a", 10, 4), (3, "c", 30, 4) };

        source.Where(x => tuples.Contains(new ValueTuple<int, string?, int?, int>(x.Id, x.Name, x.Score, 4))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<(int, string?, int?, int)> { (1, "a", 10, 99) };
        source.Where(x => mismatch.Contains(new ValueTuple<int, string?, int?, int>(x.Id, x.Name, x.Score, 4))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ValueTupleArityFive_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?, int?, int, int)> { (1, "a", 10, 4, 5), (3, "c", 30, 4, 5) };

        source.Where(x => tuples.Contains(new ValueTuple<int, string?, int?, int, int>(x.Id, x.Name, x.Score, 4, 5))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<(int, string?, int?, int, int)> { (1, "a", 10, 4, 99) };
        source.Where(x => mismatch.Contains(new ValueTuple<int, string?, int?, int, int>(x.Id, x.Name, x.Score, 4, 5))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ValueTupleAritySix_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?, int?, int, int, int)> { (1, "a", 10, 4, 5, 6), (3, "c", 30, 4, 5, 6) };

        source.Where(x => tuples.Contains(new ValueTuple<int, string?, int?, int, int, int>(x.Id, x.Name, x.Score, 4, 5, 6))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<(int, string?, int?, int, int, int)> { (1, "a", 10, 4, 5, 99) };
        source.Where(x => mismatch.Contains(new ValueTuple<int, string?, int?, int, int, int>(x.Id, x.Name, x.Score, 4, 5, 6))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReferenceTupleArityOne_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int>> { Tuple.Create(1), Tuple.Create(3) };

        source.Where(x => tuples.Contains(Tuple.Create(x.Id))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<Tuple<int>> { Tuple.Create(99) };
        source.Where(x => mismatch.Contains(Tuple.Create(x.Id))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReferenceTupleArityFour_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int, string?, int?, int>>
        {
            Tuple.Create(1, (string?)"a", (int?)10, 4),
            Tuple.Create(3, (string?)"c", (int?)30, 4),
        };

        source.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<Tuple<int, string?, int?, int>> { Tuple.Create(1, (string?)"a", (int?)10, 99) };
        source.Where(x => mismatch.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReferenceTupleArityFive_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int, string?, int?, int, int>>
        {
            Tuple.Create(1, (string?)"a", (int?)10, 4, 5),
            Tuple.Create(3, (string?)"c", (int?)30, 4, 5),
        };

        source.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4, 5))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<Tuple<int, string?, int?, int, int>> { Tuple.Create(1, (string?)"a", (int?)10, 4, 99) };
        source.Where(x => mismatch.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4, 5))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReferenceTupleAritySix_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int, string?, int?, int, int, int>>
        {
            Tuple.Create(1, (string?)"a", (int?)10, 4, 5, 6),
            Tuple.Create(3, (string?)"c", (int?)30, 4, 5, 6),
        };

        source.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4, 5, 6))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<Tuple<int, string?, int?, int, int, int>> { Tuple.Create(1, (string?)"a", (int?)10, 4, 5, 99) };
        source.Where(x => mismatch.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4, 5, 6))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReferenceTupleAritySeven_ShouldMatchAndRejectMismatch()
    {
        var source = Source(Seed());
        var tuples = new List<Tuple<int, string?, int?, int, int, int, int>>
        {
            Tuple.Create(1, (string?)"a", (int?)10, 4, 5, 6, 7),
            Tuple.Create(3, (string?)"c", (int?)30, 4, 5, 6, 7),
        };

        source.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4, 5, 6, 7))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 3]);

        var mismatch = new List<Tuple<int, string?, int?, int, int, int, int>> { Tuple.Create(1, (string?)"a", (int?)10, 4, 5, 6, 99) };
        source.Where(x => mismatch.Contains(Tuple.Create(x.Id, x.Name, x.Score, 4, 5, 6, 7))).Select(x => x.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void Contains_DefaultValueTupleEntry_ShouldMatchDefaultsRow()
    {
        // `default` of a value tuple is an ordinary (0, null) row, not an all-null rejection.
        var source = Source(new TupleInMemoryEntity { Id = 0, Name = "z", Score = null });
        var tuples = new List<(int, int?)> { default };

        var ids = source.Where(x => tuples.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Select(x => x.Id).ToList();

        ids.Should().Equal(0);
    }

    [Fact]
    public void Contains_EmptyList_ShouldReturnNoRows()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?)>();

        var ids = source.Where(x => tuples.Contains(new ValueTuple<int, string?>(x.Id, x.Name))).Select(x => x.Id).ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void Contains_NullableComponent_ShouldMatchNullTupleRow()
    {
        var source = Source(Seed());
        var tuples = new List<(int, int?)> { (4, null) };

        var ids = source.Where(x => tuples.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Select(x => x.Id).ToList();

        ids.Should().Equal(4);
    }

    [Fact]
    public void Contains_NullableComponent_Negation_ShouldExcludeNullTupleRow()
    {
        var source = Source(Seed());
        var tuples = new List<(int, int?)> { (4, null) };

        var ids = source.Where(x => !tuples.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void Contains_NegatedTupleList_ShouldExcludeMatch()
    {
        var source = Source(Seed());
        var tuples = new List<(int, string?)> { (1, "a") };

        var ids = source.Where(x => !tuples.Contains(new ValueTuple<int, string?>(x.Id, x.Name))).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([2, 3, 4]);
    }

    [Fact]
    public void Contains_NullableComponent_ValueInNullableSlot_ShouldKeepNonNullAndNullMatches()
    {
        var source = Source(Seed());
        var tuples = new List<(int, int?)> { (1, 10), (4, null) };

        source.Where(x => tuples.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([1, 4]);

        source.Where(x => !tuples.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void TupleContains_TwoShapesOnSharedAnyCommand_ShouldRebindAndKeepCacheable()
    {
        var source = Source(Seed());
        var match = new List<(int, int?)> { (1, 10) };
        var mismatch = new List<(int, int?)> { (99, 1) };

        source.Where(x => match.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Any().Should().BeTrue();
        source.Where(x => mismatch.Contains(new ValueTuple<int, int?>(x.Id, x.Score))).Any().Should().BeFalse();

        _sut.DataProvider.AnyCommand.Should().NotBeNull();
        _sut.DataProvider.AnyCommand!.Value.Cache.Should().BeTrue(
            "a tuple IN/Contains inside the shared Any command must not set the sticky Cache=false flag");
    }

    [Fact]
    public void PartitionTuple_NonEnumerableValue_ShouldThrowNotSupported()
    {
        var act = () => InValues.PartitionTuple(42, typeof((int, int)));

        act.Should().Throw<NotSupportedException>().WithMessage("*enumerable collection*");
    }

    [Fact]
    public void PartitionTuple_NonCollectionEnumerable_ShouldStillPartitionRows()
    {
        // A LINQ iterator is IEnumerable but not ICollection, so the pre-sizing branch is skipped.
        var value = Enumerable.Range(0, 2).Select(i => (i, i * 2));

        var partition = InValues.PartitionTuple(value, typeof((int, int)));

        partition.Tuple.Should().NotBeNull();
        partition.Tuple!.Value.Rows.Should().HaveCount(2);
        partition.Tuple!.Value.Rows[1][1].Should().Be(2);
    }

    [Fact]
    public void PartitionTuple_NonTupleEntry_ShouldThrowNotSupported()
    {
        object value = new List<object> { 42 };

        var act = () => InValues.PartitionTuple(value, typeof((int, int)));

        act.Should().Throw<NotSupportedException>().WithMessage("*same arity*");
    }

    [Fact]
    public void PartitionTuple_NullCollection_ShouldThrowArgumentNullException()
    {
        var act = () => InValues.PartitionTuple(null, typeof((int, string)));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PartitionTuple_ArityEight_ShouldThrowNotSupported()
    {
        var value = new List<object>();

        var act = () => InValues.PartitionTuple(value, typeof((int, int, int, int, int, int, int, int)));

        act.Should().Throw<NotSupportedException>().WithMessage("*arity 1..7*");
    }

    [Fact]
    public void PartitionTuple_NestedTuple_ShouldThrowNotSupported()
    {
        var value = new List<(int, (int, int))>();

        var act = () => InValues.PartitionTuple(value, typeof((int, (int, int))));

        act.Should().Throw<NotSupportedException>().WithMessage("*nested*");
    }

    [Fact]
    public void PartitionTuple_MismatchedArity_ShouldThrowNotSupported()
    {
        object value = new List<(int, int, int)> { (1, 2, 3) };

        var act = () => InValues.PartitionTuple(value, typeof((int, int)));

        act.Should().Throw<NotSupportedException>().WithMessage("*same arity*");
    }

    [Fact]
    public void PartitionTuple_NullTupleEntry_ShouldThrowNotSupported()
    {
        object value = new List<(int, int)?> { null };

        var act = () => InValues.PartitionTuple(value, typeof((int, int)));

        act.Should().Throw<NotSupportedException>().WithMessage("*null tuple entry*");
    }

    [Fact]
    public void PartitionTuple_NullableValueTupleElement_ShouldThrowNotSupported()
    {
        // W2: Nullable<ValueTuple<...>> is recognised as a tuple family, but it has no null-safe row
        // shape; the routing must reject it explicitly instead of silently treating it as a scalar.
        object value = new List<(int, int)?>();

        var act = () => InValues.PartitionTuple(value, typeof((int, int)?));

        act.Should().Throw<NotSupportedException>().WithMessage("*nullable tuple*");
    }

    [Fact]
    public void PartitionTuple_ValidRows_ShouldCaptureArityAndComponents()
    {
        object value = new List<(int, string)> { (1, "a"), (2, "b") };

        var partition = InValues.PartitionTuple(value, typeof((int, string)));

        partition.Tuple.Should().NotBeNull();
        var tuple = partition.Tuple!.Value;
        tuple.Arity.Should().Be(2);
        tuple.Rows.Should().HaveCount(2);
        tuple.Rows[0][0].Should().Be(1);
        tuple.Rows[0][1].Should().Be("a");
        tuple.Rows[1][0].Should().Be(2);
        tuple.Rows[1][1].Should().Be("b");
    }
}
