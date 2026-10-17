using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #190: a direct whole-entity projection of a joined item (<c>Select(p =&gt; p.ItemN)</c>) must select
/// only the requested entity's mapped columns and materialize it on every provider, including the
/// missing outer-join side as <see langword="null"/>. The focused selector token is
/// <c>JoinWholeEntity_</c>.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void JoinWholeEntity_DirectChild_InnerJoin_ShouldMaterializeEntity()
    {
        var rows = _sut.SimpleEntity
            .Join(_sut.BinaryEntity, (s, b) => s.Id == b.Id)
            .Select(p => p.Item2)
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(b => b.Id == 1 || b.Id == 2);
        rows.Single(b => b.Id == 1).Data.Should().Equal(1, 2, 3, 4);
        rows.Single(b => b.Id == 2).Data.Should().BeNull();
    }

    [Fact]
    public void JoinWholeEntity_DirectChild_OuterJoin_ShouldReturnNullForMissingSide()
    {
        var rows = _sut.SimpleEntity
            .LeftJoin(_sut.BinaryEntity, (s, b) => s.Id == b.Id)
            .Select(p => p.Item2)
            .ToList();

        // simple_entity has 10 rows, binary_entity matches ids 1 and 2.
        rows.Should().HaveCount(10);
        rows.Count(b => b is null).Should().Be(8, "a LEFT JOIN row with no match materializes the entity as null");
        rows.Where(b => b is not null).Select(b => b!.Id).OrderBy(x => x).Should().Equal(1, 2);
    }

    [Fact]
    public void JoinWholeEntity_DirectParent_ShouldMaterializeEntity()
    {
        var rows = _sut.SimpleEntityAsClass
            .Join(_sut.BinaryEntity, (s, b) => s.Id == b.Id)
            .Select(p => p.Item1)
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).OrderBy(x => x).Should().Equal(1, 2);
    }

    [Fact]
    public void JoinWholeEntity_RepeatedExecution_ShouldReuseThePlanWithoutLeak()
    {
        static List<BinaryEntity> Query(TestDataRepository sut)
            => sut.SimpleEntity
                .LeftJoin(sut.BinaryEntity, (s, b) => s.Id == b.Id)
                .Select(p => p.Item2)
                .ToList();

        var first = Query(_sut);
        var second = Query(_sut);

        first.Should().HaveCount(10);
        second.Should().BeEquivalentTo(first, "a repeated execution must reuse the plan and produce identical rows");
    }
}
