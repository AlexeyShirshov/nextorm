using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider has no collations; a collation declared on a mapped column is ignored and the
/// native (ordinal) CLR comparison is used.
/// </summary>
public class InMemoryColumnCollationTests
{
    private readonly InMemoryRepository _sut;

    public InMemoryColumnCollationTests(InMemoryRepository sut)
    {
        _sut = sut;
    }

    [Fact]
    public void CollatedColumn_ShouldBeIgnored_AndCompareOrdinally()
    {
        var e = _sut.DataProvider.From<CollatedEntity>();
        e.WithData(new[]
        {
            new CollatedEntity { Id = 1, Name = "a" },
            new CollatedEntity { Id = 2, Name = "B" },
        });

        var r = e.Where(x => x.Name == "a").Select(x => new { x.Id }).ToList();

        r.Should().ContainSingle();
        r[0].Id.Should().Be(1);
    }
}

public class CollatedEntity
{
    public int Id { get; set; }

    [Collation("C")]
    public string? Name { get; set; }
}
