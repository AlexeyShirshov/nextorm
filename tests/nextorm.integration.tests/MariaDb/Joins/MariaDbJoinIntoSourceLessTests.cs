using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// D8 (#330) MariaDB adapter: MariaDB has no <see cref="CommonTestSuite"/>-based integration class (its
/// provider class is not derived from the shared suite), so the live equivalence of the source-less
/// <c>JoinInto</c> forms against the explicit bare <c>_ctx.From&lt;TChild&gt;()</c> twin is re-pinned here
/// against a real server with its own tables, following the existing MariaDB <c>JoinInto</c> harness
/// (<see cref="MariaDbJoinIntoRemainderIntegrationTests"/>).
/// <para>
/// Every scenario runs both calls on the same rows and compares the full projection, so a duplicated
/// denormalized row, a lost child or a wrong join kind cannot pass. The fixture is asserted first
/// (exactly one childless parent, exactly one parent with two or more children, exactly one parent with a
/// single child), so the comparison cannot become vacuous on a degenerate seed.
/// </para>
/// <para>
/// The <see cref="Assert.SkipUnless(bool, string)"/> guard in the constructor is a container-readiness
/// guard, not a capability exclusion: under a leased <c>DOCKER_HOST</c> <see cref="MariaDbContainer.IsAvailable"/>
/// is <c>true</c> and all scenarios execute. MySQL execution is never presented as MariaDB evidence.
/// </para>
/// </summary>
public sealed class MariaDbJoinIntoSourceLessTests : IDisposable
{
    // Fixture ids. "many" carries two children (row-multiplication detection), "one" exactly one child
    // (the one-to-one reference), "none" is childless (unmatched parent).
    private const int ManyParent = 1;
    private const int OneParent = 2;
    private const int NoneParent = 3;

    private const int ManyChildA = 101;
    private const int ManyChildB = 102;
    private const int OneChild = 103;

    private const int ManyNoteA = 201;
    private const int ManyNoteB = 202;
    private const int OneNote = 203;

    private readonly MariaDbDataContext _ctx;

    public MariaDbJoinIntoSourceLessTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinIntoSourceLess_CollectionLeft_ShouldMatchTheExplicitBareSource()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        AssertFixtureHasUnmatchedSingleAndDuplicateShapes(explicitBare);

        sourceLess.Select(p => p.Id).Should().Equal([ManyParent, OneParent, NoneParent], "a LEFT join keeps every parent");
        sourceLess.Single(p => p.Id == NoneParent).Children.Should()
            .BeEmpty("the childless parent survives with an empty collection");
        sourceLess.Single(p => p.Id == ManyParent).Children.Select(c => c.Id).Should()
            .BeEquivalentTo([ManyChildA, ManyChildB]);
        sourceLess.Single(p => p.Id == ManyParent).Children.Should()
            .OnlyHaveUniqueItems("two child rows must not be multiplied by the denormalized parent row");
        sourceLess.Single(p => p.Id == OneParent).Children.Select(c => c.Id).Should().Equal(OneChild);
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_CollectionLeftExplicitKind_ShouldMatchTheExplicitBareSource()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Left)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Left)
            .OrderBy(p => p.Id)
            .ToList();

        AssertFixtureHasUnmatchedSingleAndDuplicateShapes(explicitBare);

        sourceLess.Select(p => p.Id).Should()
            .Equal([ManyParent, OneParent, NoneParent], "an explicit JoinType.Left is the same kind as the default");
        sourceLess.Single(p => p.Id == NoneParent).Children.Should().BeEmpty();
        sourceLess.Single(p => p.Id == ManyParent).Children.Should().OnlyHaveUniqueItems();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_CollectionInner_ShouldMatchTheExplicitBareSource()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        AssertFixtureHasUnmatchedSingleAndDuplicateShapes(
            Parents()
                .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
                .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .OrderBy(p => p.Id)
                .ToList());

        sourceLess.Select(p => p.Id).Should().Equal([ManyParent, OneParent], "an INNER join drops the childless parent");
        sourceLess.Should().NotContain(p => p.Id == NoneParent);
        sourceLess.Sum(p => p.Children.Count).Should()
            .Be(explicitBare.Sum(p => p.Children.Count), "both calls must keep the same number of children");
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceLeft_ShouldMatchTheExplicitBareSource()
    {
        // "many" has two children and would trip the one-to-one backstop in both calls, so the reference
        // scenarios are scoped to "one" (a single child) and "none" (no child).
        var sourceLess = Parents()
            .Where(p => p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == OneParent || p.Id == NoneParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([OneParent, NoneParent]);
        sourceLess.Single(p => p.Id == OneParent).PrimaryChild.Should().NotBeNull();
        sourceLess.Single(p => p.Id == OneParent).PrimaryChild!.Id.Should()
            .Be(explicitBare.Single(p => p.Id == OneParent).PrimaryChild!.Id);
        sourceLess.Single(p => p.Id == NoneParent).PrimaryChild.Should()
            .BeNull("a LEFT one-to-one join without a matching child keeps the navigation null");
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceLeftExplicitKind_ShouldMatchTheExplicitBareSource()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Left)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == OneParent || p.Id == NoneParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Left)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should()
            .Equal([OneParent, NoneParent], "an explicit JoinType.Left is the same kind as the default");
        sourceLess.Single(p => p.Id == NoneParent).PrimaryChild.Should().BeNull();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceInner_ShouldMatchTheExplicitBareSource()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == OneParent || p.Id == NoneParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        sourceLess.Select(p => p.Id).Should()
            .Equal([OneParent], "an INNER one-to-one join excludes a parent with no matching child");
        sourceLess.Should().NotContain(p => p.Id == NoneParent);
        sourceLess[0].PrimaryChild.Should().NotBeNull();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_KeyForm_ShouldMatchTheExplicitBareSource()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == ManyParent || p.Id == OneParent || p.Id == NoneParent)
            .JoinInto<MdbSlChild, int>(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        AssertFixtureHasUnmatchedSingleAndDuplicateShapes(explicitBare);

        sourceLess.Select(p => p.Id).Should()
            .Equal([ManyParent, OneParent, NoneParent], "the key form exposes only the LEFT mode");
        sourceLess.Single(p => p.Id == NoneParent).Children.Should().BeEmpty();
        sourceLess.Single(p => p.Id == ManyParent).Children.Should().OnlyHaveUniqueItems();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_MultipleCollections_ShouldDeduplicatePerChildKey()
    {
        var sourceLess = Parents()
            .Where(p => p.Id == ManyParent)
            .JoinInto<MdbSlChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto<MdbSlNote>((p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        var explicitBare = Parents()
            .Where(p => p.Id == ManyParent)
            .JoinInto(_ctx.From<MdbSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(_ctx.From<MdbSlNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        sourceLess.Should().ContainSingle("the cartesian rows of two collection joins must collapse to one parent");
        sourceLess[0].Children.Select(c => c.Id).Should().BeEquivalentTo([ManyChildA, ManyChildB]);
        sourceLess[0].Children.Should().OnlyHaveUniqueItems();
        sourceLess[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([ManyNoteA, ManyNoteB]);
        sourceLess[0].Notes.Should().OnlyHaveUniqueItems();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    /// <summary>
    /// The parent builder with the three relationships the source-less forms resolve: two collections and
    /// the one-to-one reference navigation.
    /// </summary>
    private EntityBuilder<MdbSlParent> Parents()
        => _ctx.From<MdbSlParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId)
            .HasOneToOne(p => p.PrimaryChild, p => p.Id, c => c.ParentId));

    /// <summary>
    /// Order-insensitive projection of a stitched parent: child/note id arrays are sorted (the join
    /// contract does not order children) but duplicates are preserved, so row multiplication still fails.
    /// </summary>
    private static object[] Shape(IEnumerable<MdbSlParent> parents)
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
    /// Asserts the seeded fixture really carries the shapes the equivalence depends on: exactly one
    /// childless (unmatched) parent, exactly one with two or more children and exactly one with a single child.
    /// </summary>
    private static void AssertFixtureHasUnmatchedSingleAndDuplicateShapes(IReadOnlyList<MdbSlParent> parents)
    {
        parents.Should().HaveCount(3, "the fixture seeds three scoped parents");
        parents.Count(p => p.Children.Count == 0).Should()
            .Be(1, "the fixture must contain exactly one childless (unmatched) parent");
        parents.Count(p => p.Children.Count >= 2).Should()
            .Be(1, "the fixture must contain exactly one parent with two or more children");
        parents.Count(p => p.Children.Count == 1).Should()
            .Be(1, "the fixture must contain exactly one parent with a single child");
    }

    private void Seed()
    {
        Execute("drop table if exists mdb_sl_note");
        Execute("drop table if exists mdb_sl_child");
        Execute("drop table if exists mdb_sl_parent");
        Execute("create table mdb_sl_parent (id int not null primary key, name varchar(50) null)");
        Execute("create table mdb_sl_child (id int not null primary key, parent_id int not null, name varchar(50) null)");
        Execute("create table mdb_sl_note (id int not null primary key, parent_id int not null, text varchar(50) null)");

        Execute($"insert into mdb_sl_parent (id, name) values ({ManyParent}, 'many'), ({OneParent}, 'one'), ({NoneParent}, 'none')");
        Execute($"insert into mdb_sl_child (id, parent_id, name) values " +
                $"({ManyChildA}, {ManyParent}, 'many-a'), ({ManyChildB}, {ManyParent}, 'many-b'), ({OneChild}, {OneParent}, 'one-a')");
        Execute($"insert into mdb_sl_note (id, parent_id, text) values " +
                $"({ManyNoteA}, {ManyParent}, 'many-n1'), ({ManyNoteB}, {ManyParent}, 'many-n2'), ({OneNote}, {OneParent}, 'one-n1')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("mdb_sl_parent")]
internal sealed class MdbSlParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MdbSlChild> Children { get; set; } = new List<MdbSlChild>();

    public ICollection<MdbSlNote> Notes { get; set; } = new List<MdbSlNote>();

    public MdbSlChild? PrimaryChild { get; set; }
}

[SqlTable("mdb_sl_child")]
internal sealed class MdbSlChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("mdb_sl_note")]
internal sealed class MdbSlNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}
