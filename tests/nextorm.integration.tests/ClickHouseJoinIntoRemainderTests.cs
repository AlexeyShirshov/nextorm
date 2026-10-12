using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D01 (#213) focused ClickHouse adapter: ClickHouse does not derive <see cref="CommonTestSuite"/>, so
/// the shared many-to-many <c>JoinInto</c> composed with a <see cref="EagerLoadMode.SingleQuery"/>
/// <c>LoadWith</c> is re-pinned here against a real server. The regression is the unmodified dispatcher
/// rejecting the many-to-many declaration; once lifted, one denormalized round trip must stitch the
/// many-to-many tags and the LoadWith notes.
/// </summary>
public sealed class ClickHouseJoinIntoRemainderTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchTagsAndNotes()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);
        _sut.DataProvider.CreateInsertBuilder<JoinIntoNote>().Values([
            new JoinIntoNote { Id = f + 500, ParentId = f, Text = "ch-note" },
        ]).Insert();

        var parents = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f)
            .JoinInto(_sut.DataProvider.From<JoinIntoTag>(), (p, t) => t.Id >= f + 300, p => p.Tags)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([f + 300, f + 301, f + 300]);
        parents[0].Notes.Select(n => n.Id).Should().Contain(f + 500);
    }
}
