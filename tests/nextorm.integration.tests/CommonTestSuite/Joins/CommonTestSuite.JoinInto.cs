using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Cross-provider integration coverage for <c>JoinInto</c> (#105 slice B): one denormalized round trip
/// stitched into child collections, LEFT/INNER semantics, parent/child deduplication, WHERE-on-join,
/// parent paging, multiple collections and the parent-only non-list terminals.
/// </summary>
public abstract partial class CommonTestSuite
{
    private static int _joinIntoIdSeed = -3_000_000;

    private static int NextJoinIntoId() => Interlocked.Add(ref _joinIntoIdSeed, -1_000);

    private EntityBuilder<JoinIntoParent> JoinIntoParents()
        => _sut.DataProvider.From<JoinIntoParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId)
            .HasManyThrough<JoinIntoTag, JoinIntoTagLink, int, int>(
                p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId)
            .HasOneToOne(p => p.PrimaryChild, p => p.Id, c => c.ParentId));

    private EntityBuilder<JoinIntoChild> JoinIntoChildren()
        => _sut.DataProvider.From<JoinIntoChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));

    private EntityBuilder<JoinIntoNote> JoinIntoNotes()
        => _sut.DataProvider.From<JoinIntoNote>();

    private EntityBuilder<JoinIntoTag> JoinIntoTags()
        => _sut.DataProvider.From<JoinIntoTag>();

    /// <summary>
    /// Seeds a fresh parent/child/note triple with unique ids: <c>many</c> has two children and two
    /// notes, <c>one</c> has a single child and no note, <c>none</c> is childless.
    /// </summary>
    private (int Many, int One, int None) SeedJoinInto()
    {
        var ctx = _sut.DataProvider;

        // Register the relationship metadata before the first Insert/mapping of these types: the
        // navigation members must be recognized as such (not mapped as columns), and the first
        // registration wins process-wide. The child types are registered first so their navigation
        // is already excluded when the parent-side HasMany resolves them.
        JoinIntoChildren();
        JoinIntoNotes();
        JoinIntoParents();

        var many = NextJoinIntoId();
        var one = many + 1;
        var none = many + 2;

        ctx.CreateInsertBuilder<JoinIntoParent>().Values([
            new JoinIntoParent { Id = many, Name = "many" },
            new JoinIntoParent { Id = one, Name = "one" },
            new JoinIntoParent { Id = none, Name = "none" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoChild>().Values([
            new JoinIntoChild { Id = many + 100, ParentId = many, Name = "many-a" },
            new JoinIntoChild { Id = many + 101, ParentId = many, Name = "many-b" },
            new JoinIntoChild { Id = many + 102, ParentId = one, Name = "one-a" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoNote>().Values([
            new JoinIntoNote { Id = many + 200, ParentId = many, Text = "many-n1" },
            new JoinIntoNote { Id = many + 201, ParentId = many, Text = "many-n2" },
        ]).Insert();

        // Many-to-many fixtures (#135): two tags for "many" plus one for "one", linked through a
        // junction that repeats a (parent, child) pair and carries one dangling child foreign key.
        ctx.CreateInsertBuilder<JoinIntoTag>().Values([
            new JoinIntoTag { Id = many + 300, Name = "many-t1" },
            new JoinIntoTag { Id = many + 301, Name = "many-t2" },
            new JoinIntoTag { Id = many + 302, Name = "one-t1" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoTagLink>().Values([
            new JoinIntoTagLink { Id = many + 400, ParentId = many, ChildId = many + 300 },
            new JoinIntoTagLink { Id = many + 401, ParentId = many, ChildId = many + 301 },
            new JoinIntoTagLink { Id = many + 402, ParentId = many, ChildId = many + 300 },
            new JoinIntoTagLink { Id = many + 403, ParentId = one, ChildId = many + 302 },
            new JoinIntoTagLink { Id = many + 404, ParentId = many, ChildId = many + 399 },
        ]).Insert();

        return (many, one, none);
    }

    /// <summary>
    /// Seeds two parents with exactly one distinct child each. This is the positive per-parent control
    /// for the one-to-one backstop: a global identity comparison would wrongly merge the two children,
    /// so each parent must keep its own child.
    /// </summary>
    private (int First, int Second) SeedJoinIntoTwoParentsOneChild()
    {
        var ctx = _sut.DataProvider;

        JoinIntoChildren();
        JoinIntoNotes();
        JoinIntoParents();

        var first = NextJoinIntoId();
        var second = first + 1;

        ctx.CreateInsertBuilder<JoinIntoParent>().Values([
            new JoinIntoParent { Id = first, Name = "solo-a" },
            new JoinIntoParent { Id = second, Name = "solo-b" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoChild>().Values([
            new JoinIntoChild { Id = first + 100, ParentId = first, Name = "solo-a-child" },
            new JoinIntoChild { Id = second + 100, ParentId = second, Name = "solo-b-child" },
        ]).Insert();

        return (first, second);
    }

    /// <summary>
    /// Seeds a single parent with exactly one physical child and two notes. Joining the notes collection
    /// alongside the one-to-one reference repeats that one child row in the denormalized result while its
    /// identity stays the same — the repeated-identity case for the shared providers, whose tables carry
    /// a primary key so a duplicated physical row cannot be inserted.
    /// </summary>
    private int SeedJoinIntoRepeatedIdentity()
    {
        var ctx = _sut.DataProvider;

        JoinIntoChildren();
        JoinIntoNotes();
        JoinIntoParents();

        var parent = NextJoinIntoId();

        ctx.CreateInsertBuilder<JoinIntoParent>().Values([
            new JoinIntoParent { Id = parent, Name = "repeat" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoChild>().Values([
            new JoinIntoChild { Id = parent + 100, ParentId = parent, Name = "repeat-child" },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoNote>().Values([
            new JoinIntoNote { Id = parent + 200, ParentId = parent, Text = "repeat-n1" },
            new JoinIntoNote { Id = parent + 201, ParentId = parent, Text = "repeat-n2" },
        ]).Insert();

        return parent;
    }

    [Fact]
    public void JoinInto_Left_ShouldKeepChildlessParentAndGroupChildren()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(many, one, none);
        // Child order is not part of the JoinInto contract, so assert membership, not sequence.
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([many + 100, many + 101]);
        parents[1].Children.Select(c => c.Id).Should().BeEquivalentTo([many + 102]);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void JoinInto_Inner_ShouldExcludeChildlessParent()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(many, one);
    }

    [Fact]
    public void JoinInto_ShouldDeduplicateRepeatedParentRows()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3, "one denormalized row per child must collapse to one parent");
        parents.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void JoinInto_MultipleCollections_ShouldDeduplicateEachChildByKey()
    {
        var (many, _, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(JoinIntoNotes(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        parents.Should().ContainSingle("the cartesian rows must collapse to one parent");

        var parent = parents[0];
        parent.Children.Select(c => c.Id).Should().BeEquivalentTo([many + 100, many + 101]);
        parent.Children.Should().OnlyHaveUniqueItems();
        parent.Notes.Select(n => n.Id).Should().BeEquivalentTo([many + 200, many + 201]);
        parent.Notes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void JoinInto_Where_ShouldFilterParentRowsBeforeStitching()
    {
        var (many, _, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(many, none);
        parents[0].Children.Should().HaveCount(2);
        parents[1].Children.Should().BeEmpty();
    }

    [Fact]
    public void JoinInto_ParentPaging_ShouldLimitParentsNotDenormalizedRows()
    {
        var (many, one, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .Limit(1)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Id.Should().Be(many);
        parents[0].Children.Should().HaveCount(2, "paging parents must not truncate the children of the kept parent");
    }

    [Fact]
    public void JoinInto_NonListTerminals_ShouldCountParentsNotJoinedRows()
    {
        var (many, one, none) = SeedJoinInto();

        var filtered = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none);

        var joined = filtered.JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children);

        joined.Count().Should().Be(3, "Count counts parents, not the denormalized rows");
        joined.Any().Should().BeTrue();
        joined.Where(p => p.Id == none).First().Children.Should().BeEmpty("non-list terminals do not stitch");
    }

    [Fact]
    public void JoinInto_ListTerminal_ShouldExecuteOneDenormalizedRoundTrip()
    {
        var (many, one, none) = SeedJoinInto();

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        interceptor.Executing.Should().Be(1, "JoinInto stitches the parent and child collections from one denormalized round trip");
        interceptor.Executed.Should().Be(1);
        parents.Should().HaveCount(3);
        parents[0].Children.Should().HaveCount(2);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void JoinInto_OneToOne_Left_ShouldAssignSingleChildAndLeaveChildlessNull()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(one, none);
        parents[0].PrimaryChild.Should().NotBeNull("the single matching child is assigned to the one-to-one reference");
        parents[0].PrimaryChild!.Id.Should().Be(many + 102);
        parents[0].PrimaryChild!.ParentId.Should().Be(one);
        parents[1].PrimaryChild.Should().BeNull("a LEFT one-to-one join without a matching child leaves the reference null");
    }

    [Fact]
    public void JoinInto_OneToOne_Inner_ShouldExcludeChildlessParent()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([one], "an INNER one-to-one join excludes a parent with no matching child");
        parents[0].PrimaryChild!.Id.Should().Be(many + 102);
    }

    [Fact]
    public void JoinInto_OneToOne_ShouldThrowWhenAParentMatchesMoreThanOneChild()
    {
        var (many, _, _) = SeedJoinInto();

        Action act = () => JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToList();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public async Task JoinInto_OneToOne_ShouldThrowWhenAParentMatchesMoreThanOneChildToListAsync()
    {
        // R170-04 / DA: the shared-provider one-to-one backstop must reject two distinct child
        // identities on the async terminal with the same diagnostic as the sync path.
        var (many, _, _) = SeedJoinInto();

        Func<Task> act = () => JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public void JoinInto_OneToOne_TwoParentsWithDistinctChildren_ShouldAssignEachItsOwnChild()
    {
        // R170-04 / PS: positive per-parent control — two parents, each with its own single child,
        // must load without a throw and each keep its own child identity and payload. This rules out a
        // global identity comparison that would see the two children as a duplicate.
        var (first, second) = SeedJoinIntoTwoParentsOneChild();

        var parents = JoinIntoParents()
            .Where(p => p.Id == first || p.Id == second)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(first, second);
        parents[0].PrimaryChild!.Id.Should().Be(first + 100);
        parents[0].PrimaryChild!.ParentId.Should().Be(first);
        parents[0].PrimaryChild!.Name.Should().Be("solo-a-child");
        parents[1].PrimaryChild!.Id.Should().Be(second + 100);
        parents[1].PrimaryChild!.ParentId.Should().Be(second);
        parents[1].PrimaryChild!.Name.Should().Be("solo-b-child");
    }

    [Fact]
    public async Task JoinInto_OneToOne_TwoParentsWithDistinctChildren_ToListAsync_ShouldAssignEachItsOwnChild()
    {
        // R170-04 / PA: async parity of the positive per-parent control.
        var (first, second) = SeedJoinIntoTwoParentsOneChild();

        var parents = await JoinIntoParents()
            .Where(p => p.Id == first || p.Id == second)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Select(p => p.Id).Should().Equal(first, second);
        parents[0].PrimaryChild!.Id.Should().Be(first + 100);
        parents[0].PrimaryChild!.ParentId.Should().Be(first);
        parents[0].PrimaryChild!.Name.Should().Be("solo-a-child");
        parents[1].PrimaryChild!.Id.Should().Be(second + 100);
        parents[1].PrimaryChild!.ParentId.Should().Be(second);
        parents[1].PrimaryChild!.Name.Should().Be("solo-b-child");
    }

    // case PV(repeated)
    [Fact]
    public void JoinInto_OneToOne_RepeatedSameIdentity_ShouldNotThrowAndKeepOneChild()
    {
        // case PV(repeated): one physical child joined twice through a neighbouring one-to-many collection
        // repeats its rows with an unchanged identity; the one-to-one backstop must tolerate the repeat and
        // assign the single child once.
        var parent = SeedJoinIntoRepeatedIdentity();

        var parents = JoinIntoParents()
            .Where(p => p.Id == parent)
            .JoinInto(JoinIntoNotes(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToList();

        parents.Should().ContainSingle("the repeated one-to-one rows must collapse to one parent");
        parents[0].Notes.Should().HaveCount(2);
        parents[0].PrimaryChild.Should().NotBeNull("a repeated same-identity child is tolerated");
        parents[0].PrimaryChild!.Id.Should().Be(parent + 100);
        parents[0].PrimaryChild!.ParentId.Should().Be(parent);
        parents[0].PrimaryChild!.Name.Should().Be("repeat-child");
    }

    // case PV(repeated)
    [Fact]
    public async Task JoinInto_OneToOne_RepeatedSameIdentity_ToListAsync_ShouldNotThrowAndKeepOneChild()
    {
        // case PV(repeated): async parity of the repeated-identity tolerance.
        var parent = SeedJoinIntoRepeatedIdentity();

        var parents = await JoinIntoParents()
            .Where(p => p.Id == parent)
            .JoinInto(JoinIntoNotes(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().ContainSingle("the repeated one-to-one rows must collapse to one parent");
        parents[0].Notes.Should().HaveCount(2);
        parents[0].PrimaryChild.Should().NotBeNull("a repeated same-identity child is tolerated");
        parents[0].PrimaryChild!.Id.Should().Be(parent + 100);
        parents[0].PrimaryChild!.ParentId.Should().Be(parent);
        parents[0].PrimaryChild!.Name.Should().Be("repeat-child");
    }

    [Fact]
    public void JoinInto_ManyToMany_Left_ShouldKeepParentWithoutTagAndGroupTags()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(JoinIntoTags(), (p, t) => t.Id >= many + 300, p => p.Tags)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(many, one, none);

        // Two distinct tags for "many", one of them linked twice: the duplicate junction row is preserved.
        parents[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([many + 300, many + 301, many + 300]);
        parents[1].Tags.Select(t => t.Id).Should().Equal(many + 302);
        parents[2].Tags.Should().BeEmpty("a LEFT many-to-many join keeps a parent without any junction row");
    }

    [Fact]
    public void JoinInto_ManyToMany_Inner_ShouldExcludeParentWithoutTag()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(JoinIntoTags(), (p, t) => t.Id >= many + 300, p => p.Tags, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([many, one], "an INNER many-to-many join excludes a parent with no junction row");
    }

    [Fact]
    public void JoinInto_ManyToMany_ShouldPreserveDuplicateJunctionRows()
    {
        var (many, _, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoTags(), (p, t) => t.Id >= many + 300, p => p.Tags)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Tags.Count(t => t.Id == many + 300).Should()
            .Be(2, "two junction rows linking the same (parent, child) pair are distinct elements");
    }

    [Fact]
    public void JoinInto_ManyToMany_DanglingChild_ShouldYieldNoElement()
    {
        var (many, _, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoTags(), (p, t) => t.Id >= many + 300, p => p.Tags)
            .ToList();

        parents.Should().ContainSingle();
        // Two tags are linked (one twice); the fourth junction row points at child id many + 399,
        // which no seeded tag has, so it must contribute no element at all.
        parents[0].Tags.Should().HaveCount(3, "the dangling junction row contributes no element");
        parents[0].Tags.Should().NotContainNulls("a junction row whose child does not exist contributes no element");
        parents[0].Tags.Select(t => t.Id).Should().NotContain(many + 399);
    }

    [Fact]
    public void JoinInto_ManyToMany_MixedWithOneToManyAndOneToOne_ShouldStitchEachCollection()
    {
        var (many, one, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == one)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(JoinIntoTags(), (p, t) => t.Id >= many + 300, p => p.Tags)
            .JoinInto(JoinIntoChildren(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .ToList();

        parents.Should().ContainSingle("the 1:N, 1:1 and M:N joins must not multiply the parent");
        parents[0].Children.Should().ContainSingle();
        parents[0].Tags.Should().ContainSingle();
        parents[0].PrimaryChild.Should().NotBeNull("the single matching child fills the one-to-one reference");
    }

    [Fact]
    public void JoinInto_ManyToMany_Predicate_ShouldFilterTheJoinedTags()
    {
        var (many, _, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoTags(), (p, t) => t.Name != "many-t2", p => p.Tags)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Tags.Select(t => t.Id).Should().NotContain(many + 301, "the child predicate is ANDed into the child join");
        parents[0].Tags.Should().OnlyContain(t => t.Name != "many-t2");
    }

    [Fact]
    public void JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchTagsAndNotes()
    {
        // D01 (#213) RED regression: a many-to-many JoinInto declaration composed with a SingleQuery
        // LoadWith is rejected by the unmodified dispatcher; once lifted, one denormalized round trip
        // must stitch the many-to-many tags and the LoadWith notes together.
        var (many, _, _) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many)
            .JoinInto(JoinIntoTags(), (p, t) => t.Id >= many + 300, p => p.Tags)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([many + 300, many + 301, many + 300]);
        parents[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([many + 200, many + 201]);
    }

    [Fact]
    public void JoinInto_CompositeKeyOneToMany_ShouldMatchEveryComponentWithoutBlendingParents()
    {
        // D07 (r=5): a genuine composite relationship key (no composite junction) executed against the
        // real provider. The principal key (tenant, link_id) is not the table key, and the three parents
        // share one component each, so matching on a single component would assign a neighbouring
        // parent's children.
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
        // The orphan child (tenant t2, link l2) matches no parent and must not appear anywhere.
        parents.SelectMany(p => p.Children).Select(ch => ch.Id).Should().NotContain(a + 104);
    }

    [Fact]
    public void JoinInto_CompositeKeyOneToOne_ShouldAssignEachParentItsOwnChildAndLeaveChildlessNull()
    {
        // D07 (r=5): the composite one-to-one. All parents share the tenant and differ only in link_id,
        // so a tenant-only match would pair each parent with both children and trip the one-to-one
        // backstop; one parent has no child and must stay null rather than receive a default instance.
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
        parents[2].Primary.Should().BeNull("a LEFT one-to-one join without a matching composite key leaves the reference null");
    }

    // ---------------------------------------------------------------------------------------------
    // Composite junction M:N (#223): the junction carries two parent foreign-key components. Both the
    // standalone JoinInto pair command and the SingleQuery dispatcher must stitch the composite graph in
    // one round trip, matching every component; the last component is the collision discriminator and the
    // repeated junction occurrence must stay a distinct element.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_CompositeJunction_Standalone_ShouldStitchGraphPreservingOccurrences()
    {
        var (first, second) = JoinIntoCompositeJunctionFixtures.Seed(_sut.DataProvider);

        var interceptor = new JoinIntoQueryCountingInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = _sut.DataProvider.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .JoinInto(_sut.DataProvider.From<JiCompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1, "the composite junction stitches the graph from one denormalized round trip");

        parents.Select(p => p.RowId).Should().Equal(first, second);
        parents[0].Children.Should().HaveCount(2, "two junction occurrences of the same pair remain distinct elements");
        parents[0].Children.Select(c => c.Id).Should().OnlyContain(id => id == first + 100,
            "the junction's last parent component routes child c1 to the first parent only");
        parents[0].Children.Should().NotContainNulls("the dangling child foreign key contributes no element");
        parents[1].Children.Select(c => c.Id).Should().Equal([first + 101],
            "the junction's last parent component routes child c2 to the second parent only");
    }

    [Fact]
    public void JoinInto_CompositeJunction_SingleQuery_ShouldStitchGraphAndRunOneQuery()
    {
        var (first, _) = JoinIntoCompositeJunctionFixtures.Seed(_sut.DataProvider);

        var interceptor = new JoinIntoQueryCountingInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = _sut.DataProvider.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first)
            .JoinInto(_sut.DataProvider.From<JiCompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>(), p => p.RowId, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        interceptor.Executing.Should().Be(1, "SingleQuery stitches the composite graph in one materialization query");

        parents.Should().ContainSingle("the denormalized rows collapse to one parent");
        parents[0].Children.Should().HaveCount(2, "the repeated junction occurrence is preserved in the single query");
        parents[0].Children.Select(c => c.Id).Should().OnlyContain(id => id == first + 100);
        parents[0].Notes.Select(n => n.Id).Should().Equal([first + 200],
            "the SingleQuery LoadWith note is stitched beside the composite junction");
    }
}

[SqlTable("eager_parent")]
public sealed class JoinIntoParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JoinIntoChild> Children { get; set; } = new List<JoinIntoChild>();

    public ICollection<JoinIntoNote> Notes { get; set; } = new List<JoinIntoNote>();

    public ICollection<JoinIntoTag> Tags { get; set; } = new List<JoinIntoTag>();

    public JoinIntoChild? PrimaryChild { get; set; }
}

[SqlTable("eager_child")]
public sealed class JoinIntoChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public JoinIntoParent? Parent { get; set; }
}

[SqlTable("eager_note")]
public sealed class JoinIntoNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}

[SqlTable("eager_tag")]
public sealed class JoinIntoTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("eager_link")]
public sealed class JoinIntoTagLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
