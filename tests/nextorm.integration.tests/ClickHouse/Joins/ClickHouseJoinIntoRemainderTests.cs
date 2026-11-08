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
[Collection("ClickHouse")]
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

    [Fact]
    public void JoinInto_CompositeKeyOneToMany_ShouldMatchEveryComponentWithoutBlendingParents()
    {
        // D07 (r=5): the shared composite relationship-key case against a real ClickHouse server.
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedOneToMany(_sut.DataProvider);

        var parents = _sut.DataProvider.From<JiCompositeOneToManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .JoinInto(
                _sut.DataProvider.From<JiCompositeKeyChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(a, b, c);
        parents[0].Children.Select(ch => ch.Id).Should().BeEquivalentTo([a + 100, a + 101]);
        parents[1].Children.Select(ch => ch.Id).Should().Equal(a + 102);
        parents[2].Children.Select(ch => ch.Id).Should().Equal(a + 103);
    }

    [Fact]
    public void JoinInto_CompositeKeyOneToOne_ShouldAssignEachParentItsOwnChildAndLeaveChildlessNull()
    {
        // D07 (r=5): the shared composite one-to-one case against a real ClickHouse server.
        var (withChild, otherChild, childless) = JoinIntoCompositeKeyFixtures.SeedOneToOne(_sut.DataProvider);

        var parents = _sut.DataProvider.From<JiCompositeOneToOneParent>()
            .Where(p => p.RowId == withChild || p.RowId == otherChild || p.RowId == childless)
            .JoinInto(
                _sut.DataProvider.From<JiCompositeKeyChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Primary)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(withChild, otherChild, childless);
        parents[0].Primary!.Id.Should().Be(withChild + 110);
        parents[1].Primary!.Id.Should().Be(otherChild + 110);
        parents[2].Primary.Should().BeNull();
    }
}
