using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory parity for the dynamic-columns store: the in-memory provider materialises the source row
/// as-is, so the store dictionary of the registered row is the one returned by the query.
/// </summary>
public class DynamicColumnsInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public DynamicColumnsInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
    }

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_ShouldReturnTheStoredDictionary()
    {
        var entity = _sut.DataProvider.From<DynamicInMemoryEntity>();
        entity.WithData([new DynamicInMemoryEntity { Id = 1, Extra = { ["x"] = 5, ["y"] = "z" } }]);

        var rows = entity.ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(1);
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
