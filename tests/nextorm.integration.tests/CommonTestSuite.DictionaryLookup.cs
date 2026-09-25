using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Dictionary_IndexedByColumn_ShouldTranslateToCase()
    {
        var lookup = new Dictionary<int, int> { [1] = 10 };

        var ids = _sut.ComplexEntity
            .Where(e => lookup[e.Int!.Value] == 10)
            .Select(e => e.Id)
            .ToList();

        // id 1 has a null nullableint (no CASE branch -> null), ids 2 and 3 have 1.
        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void Dictionary_IndexedByColumn_WithMissingValue_ShouldFilterOut()
    {
        var lookup = new Dictionary<int, int> { [1] = 10 };

        var ids = _sut.ComplexEntity
            .Where(e => lookup[e.Int!.Value] == 99)
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void List_IndexedByColumn_ShouldTranslateToCase()
    {
        var lookup = new List<int> { 10, 20 };

        var ids = _sut.ComplexEntity
            .Where(e => lookup[e.Int!.Value] == 20)
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void Dictionary_IndexedByConstant_ShouldFold()
    {
        var lookup = new Dictionary<int, int> { [5] = 42 };

        var ids = _sut.ComplexEntity
            .Where(e => lookup[5] == 42)
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void ReadOnlyList_IndexedByColumn_ShouldTranslateToCase()
    {
        IReadOnlyList<int> lookup = [10, 20];

        var ids = _sut.ComplexEntity
            .Where(e => lookup[e.Int!.Value] == 20)
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    [Fact]
    public void ReadOnlyDictionary_IndexedByColumn_ShouldTranslateToCase()
    {
        IReadOnlyDictionary<int, int> lookup = new Dictionary<int, int> { [1] = 10 };

        var ids = _sut.ComplexEntity
            .Where(e => lookup[e.Int!.Value] == 10)
            .Select(e => e.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }
}
