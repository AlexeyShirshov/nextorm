using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory parity for the dynamic-columns store. The in-memory provider has no result-set schema, so
/// there is no "unmapped column" concept: the whole-entity read is an identity materializer and returns
/// the registered row as-is, store property included.
/// </summary>
public class DynamicColumnsInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public DynamicColumnsInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
    }

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_ShouldReturnTheRegisteredRowAsIs()
    {
        // The in-memory provider short-circuits the whole-entity materializer, so the dynamic-columns
        // reader is never involved: the registered instance comes back unchanged, dictionary included.
        var stored = new DynamicInMemoryEntity { Id = 1, Extra = { ["x"] = 5, ["y"] = "z" } };
        var entity = _sut.DataProvider.From<DynamicInMemoryEntity>();
        entity.WithData([stored]);

        var rows = entity.ToList();

        rows.Should().ContainSingle();
        rows[0].Should().BeSameAs(stored);
        rows[0].Extra.Should().ContainKey("x").WhoseValue.Should().Be(5);
        rows[0].Extra.Should().ContainKey("y").WhoseValue.Should().Be("z");
    }
}

public class DynamicInMemoryEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
