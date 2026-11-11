using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D5 (#330): live equivalence of the source-less <c>JoinInto</c> forms against the explicit bare
/// <c>_sut.DataProvider.From&lt;TChild&gt;()</c> twin, on the same seeded data. Every scenario runs both
/// calls and compares the full projection, so a duplicated denormalized row, a lost child or a wrong join
/// kind cannot pass. Each scenario asserts the fixture shape first (exactly one childless parent, exactly
/// one parent with two or more children), so the comparison cannot become vacuous on a degenerate seed.
/// <para>
/// The shared suite deliberately carries no skip or <c>Supports*</c> guard for <c>JoinInto</c> — the
/// existing <see cref="CommonTestSuite"/> <c>JoinInto</c> scenarios do not guard either — so these
/// scenarios inherit the same unconditional execution on SQLite, PostgreSQL, SQL Server and MySQL.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    /// <summary>
    /// Order-insensitive projection of a stitched parent: child/note id arrays are sorted (the join
    /// contract does not order children) but duplicates are preserved, so row multiplication still fails.
    /// </summary>
    private static object[] JoinIntoSourceLessShape(IEnumerable<JoinIntoParent> parents)
        => parents
            .OrderBy(p => p.Id)
            .Select(p => (object)new
            {
                p.Id,
                p.Name,
                Children = p.Children.Select(c => c.Id).OrderBy(id => id).ToArray(),
                Notes = p.Notes.Select(n => n.Id).OrderBy(id => id).ToArray(),
                PrimaryChild = p.PrimaryChild?.Id,
                PrimaryChildName = p.PrimaryChild?.Name,
            })
            .ToArray();

    /// <summary>
    /// Asserts the seeded fixture really carries the unmatched-parent and multi-child shapes the
    /// equivalence depends on: exactly one parent without children and exactly one with two or more.
    /// </summary>
    private static void AssertJoinIntoFixtureHasUnmatchedAndDuplicateShapes(IReadOnlyList<JoinIntoParent> parents)
    {
        parents.Should().HaveCount(3, "the fixture seeds three scoped parents");
        parents.Count(p => p.Children.Count == 0).Should()
            .Be(1, "the fixture must contain exactly one childless (unmatched) parent");
        parents.Count(p => p.Children.Count >= 2).Should()
            .Be(1, "the fixture must contain exactly one parent with two or more children");
    }

    [Fact]
    public void JoinIntoSourceLess_CollectionLeft_ShouldMatchTheExplicitBareSource()
    {
        var (many, one, none) = SeedJoinInto();

        var sourceLess = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        AssertJoinIntoFixtureHasUnmatchedAndDuplicateShapes(explicitBare);
        sourceLess.Select(p => p.Id).Should().Equal([many, one, none], "a LEFT join keeps every parent");
        sourceLess.Single(p => p.Id == none).Children.Should().BeEmpty("the childless parent survives with an empty collection");
        sourceLess.Single(p => p.Id == many).Children.Select(c => c.Id).Should().BeEquivalentTo([many + 100, many + 101]);
        JoinIntoSourceLessShape(sourceLess).Should().BeEquivalentTo(JoinIntoSourceLessShape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_CollectionInner_ShouldMatchTheExplicitBareSource()
    {
        var (many, one, none) = SeedJoinInto();

        var sourceLess = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        AssertJoinIntoFixtureHasUnmatchedAndDuplicateShapes(
            JoinIntoParents()
                .Where(p => p.Id == many || p.Id == one || p.Id == none)
                .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .OrderBy(p => p.Id)
                .ToList());

        sourceLess.Select(p => p.Id).Should().Equal([many, one], "an INNER join drops the childless parent");
        sourceLess.Should().NotContain(p => p.Id == none);
        sourceLess.Sum(p => p.Children.Count).Should()
            .Be(explicitBare.Sum(p => p.Children.Count), "both calls must keep the same number of children");
        JoinIntoSourceLessShape(sourceLess).Should().BeEquivalentTo(JoinIntoSourceLessShape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceLeft_ShouldMatchTheExplicitBareSource()
    {
        // Scope to "one" and "none": "many" has two children and would trip the one-to-one backstop in
        // both calls (the existing CommonTestSuite.JoinInto one-to-one scenarios scope the same way).
        var (_, one, none) = SeedJoinInto();

        var sourceLess = JoinIntoParents()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = JoinIntoParents()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal(one, none);
        sourceLess.Single(p => p.Id == one).PrimaryChild.Should().NotBeNull();
        sourceLess.Single(p => p.Id == one).PrimaryChild!.Id.Should().Be(explicitBare.Single(p => p.Id == one).PrimaryChild!.Id);
        sourceLess.Single(p => p.Id == none).PrimaryChild.Should()
            .BeNull("a LEFT one-to-one join without a matching child keeps the navigation null");
        JoinIntoSourceLessShape(sourceLess).Should().BeEquivalentTo(JoinIntoSourceLessShape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceInner_ShouldMatchTheExplicitBareSource()
    {
        var (_, one, none) = SeedJoinInto();

        var sourceLess = JoinIntoParents()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        var explicitBare = JoinIntoParents()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([one], "an INNER one-to-one join excludes a parent with no matching child");
        sourceLess.Should().NotContain(p => p.Id == none);
        sourceLess[0].PrimaryChild.Should().NotBeNull();
        JoinIntoSourceLessShape(sourceLess).Should().BeEquivalentTo(JoinIntoSourceLessShape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_KeyForm_ShouldMatchTheExplicitBareSource()
    {
        var (many, one, none) = SeedJoinInto();

        var sourceLess = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto<JoinIntoChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto<JoinIntoChild, int>(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        AssertJoinIntoFixtureHasUnmatchedAndDuplicateShapes(explicitBare);
        sourceLess.Select(p => p.Id).Should().Equal([many, one, none], "the key form exposes only the LEFT mode");
        sourceLess.Single(p => p.Id == none).Children.Should().BeEmpty();
        JoinIntoSourceLessShape(sourceLess).Should().BeEquivalentTo(JoinIntoSourceLessShape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_MultipleCollections_ShouldDeduplicatePerChildKey()
    {
        var (many, _, _) = SeedJoinInto();

        var sourceLess = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto<JoinIntoNote>((p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        var explicitBare = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(_sut.DataProvider.From<JoinIntoNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        sourceLess.Should().ContainSingle("the cartesian rows of two collection joins must collapse to one parent");
        sourceLess[0].Children.Select(c => c.Id).Should().BeEquivalentTo([many + 100, many + 101]);
        sourceLess[0].Children.Should().OnlyHaveUniqueItems();
        sourceLess[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([many + 200, many + 201]);
        sourceLess[0].Notes.Should().OnlyHaveUniqueItems();
        JoinIntoSourceLessShape(sourceLess).Should().BeEquivalentTo(JoinIntoSourceLessShape(explicitBare));
    }
}
