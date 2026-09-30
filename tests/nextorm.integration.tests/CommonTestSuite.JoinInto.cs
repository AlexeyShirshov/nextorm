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
            .HasOneToOne(p => p.PrimaryChild, p => p.Id, c => c.ParentId));

    private EntityBuilder<JoinIntoChild> JoinIntoChildren()
        => _sut.DataProvider.From<JoinIntoChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));

    private EntityBuilder<JoinIntoNote> JoinIntoNotes()
        => _sut.DataProvider.From<JoinIntoNote>();

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

        ctx.InsertInto<JoinIntoParent>().Values([
            new JoinIntoParent { Id = many, Name = "many" },
            new JoinIntoParent { Id = one, Name = "one" },
            new JoinIntoParent { Id = none, Name = "none" },
        ]).Insert();

        ctx.InsertInto<JoinIntoChild>().Values([
            new JoinIntoChild { Id = many + 100, ParentId = many, Name = "many-a" },
            new JoinIntoChild { Id = many + 101, ParentId = many, Name = "many-b" },
            new JoinIntoChild { Id = many + 102, ParentId = one, Name = "one-a" },
        ]).Insert();

        ctx.InsertInto<JoinIntoNote>().Values([
            new JoinIntoNote { Id = many + 200, ParentId = many, Text = "many-n1" },
            new JoinIntoNote { Id = many + 201, ParentId = many, Text = "many-n2" },
        ]).Insert();

        return (many, one, none);
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
