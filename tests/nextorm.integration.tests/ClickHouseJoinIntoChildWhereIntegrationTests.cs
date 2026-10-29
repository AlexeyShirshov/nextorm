using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #309 live rows for the child-source <c>Where</c> on ClickHouse. ClickHouse has no
/// <see cref="CommonTestSuite"/> subclass, so the shared live assertions are re-pinned here against a
/// real server with ClickHouse-native DDL (<c>Nullable(String)</c> Memory tables). Every fact reads
/// materialized rows: the defect returns plausible collections, so only values can prove that the child
/// predicate reached the join. When the container is unavailable the constructor reports the reason and
/// the requirement row stays open — a skip is never a pass.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseJoinIntoChildWhereIntegrationTests : IDisposable
{
    private const string Skip = "skip";

    private readonly IDataContext _ctx;

    public ClickHouseJoinIntoChildWhereIntegrationTests()
    {
        Assert.SkipUnless(ClickHouseContainer.IsAvailable, ClickHouseContainer.Failure ?? "ClickHouse is not available.");

        _ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinIntoChildWhere_Left_ShouldFilterChildrenAndKeepEveryParent()
    {
        var parents = Parents()
            .Where(p => p.Id == 9501 || p.Id == 9502 || p.Id == 9503)
            .JoinInto(Children().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(new[] { 9501, 9502, 9503 }, "a LEFT join keeps every parent");
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([9511, 9512], "child 9513 (Tag = skip) is excluded");
        parents[1].Children.Select(c => c.Id).Should().Equal([9521], "child 9522 (Tag = skip) is excluded");
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void JoinIntoChildWhere_Inner_ShouldDropAllFilteredAndChildlessParents()
    {
        var parents = Parents()
            .Where(p => p.Id == 9501 || p.Id == 9502 || p.Id == 9503)
            .JoinInto(
                Children().Where(c => c.Tag != Skip && c.Id != 9521),
                (p, c) => p.Id == c.ParentId,
                p => p.Children,
                JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([9501], "9502 loses its only surviving child and 9503 has none");
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([9511, 9512]);
    }

    [Fact]
    public void JoinIntoChildWhere_Reference_ShouldLeaveNullWhenTheOnlyChildIsFiltered()
    {
        var parents = Parents()
            .Where(p => p.Id == 9501 || p.Id == 9502 || p.Id == 9503)
            .JoinInto(Profiles().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.PrimaryProfile)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].PrimaryProfile!.Id.Should().Be(9531);
        parents[1].PrimaryProfile.Should().BeNull("its only profile (9532) is filtered out");
        parents[2].PrimaryProfile.Should().BeNull();
    }

    [Fact]
    public void JoinIntoChildWhere_ReferenceInner_ShouldExcludeTheFilteredChildParent()
    {
        var parents = Parents()
            .Where(p => p.Id == 9501 || p.Id == 9502 || p.Id == 9503)
            .JoinInto(Profiles().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.PrimaryProfile, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([9501]);
        parents[0].PrimaryProfile!.Id.Should().Be(9531);
    }

    [Fact]
    public void JoinIntoChildWhere_Repeated_ShouldAndComposeBothPredicates()
    {
        var parents = Parents()
            .Where(p => p.Id == 9501 || p.Id == 9502)
            .JoinInto(
                Children().Where(c => c.Tag != Skip).Where(c => c.Id != 9512),
                (p, c) => p.Id == c.ParentId,
                p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal([9511], "9513 fails the first Where and 9512 the second");
        parents[1].Children.Select(c => c.Id).Should().Equal([9521]);
    }

    [Fact]
    public void JoinIntoChildWhere_WithCallerPredicate_ShouldComposeKeysCallerAndChild()
    {
        var parents = Parents()
            .Where(p => p.Id == 9501 || p.Id == 9502 || p.Id == 9503)
            .JoinInto(
                Children().Where(c => c.Tag != Skip),
                (p, c) => p.Id == c.ParentId && c.Id != 9512,
                p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(new[] { 9501, 9502, 9503 });
        parents[0].Children.Select(c => c.Id).Should().Equal([9511]);
        parents[1].Children.Select(c => c.Id).Should().Equal([9521]);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void JoinIntoChildWhere_ManyToMany_Left_ShouldFilterTagsAndKeepEveryParent()
    {
        var parents = TagParents()
            .Where(p => p.Id == 9601 || p.Id == 9602 || p.Id == 9603)
            .JoinInto(Tags().Where(t => t.Tag != Skip), (p, t) => t.Id >= 9600, p => p.Tags)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(new[] { 9601, 9602, 9603 });
        parents[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([9611, 9612], "tag 9613 (Tag = skip) is excluded");
        parents[1].Tags.Select(t => t.Id).Should().Equal([9621], "tag 9622 (Tag = skip) is excluded");
        parents[2].Tags.Should().BeEmpty();
    }

    [Fact]
    public void JoinIntoChildWhere_ManyToMany_Inner_ShouldDropAllFilteredAndUnlinkedParents()
    {
        var parents = TagParents()
            .Where(p => p.Id == 9601 || p.Id == 9602 || p.Id == 9603)
            .JoinInto(
                Tags().Where(t => t.Tag != Skip && t.Id != 9621),
                (p, t) => t.Id >= 9600,
                p => p.Tags,
                JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([9601], "9602 loses its only surviving tag and 9603 has no junction row");
        parents[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([9611, 9612]);
    }

    private EntityBuilder<Ch309Parent> Parents()
        => _ctx.From<Ch309Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.PrimaryProfile, p => p.Id, c => c.ParentId));

    private EntityBuilder<Ch309TagParent> TagParents()
        => _ctx.From<Ch309TagParent>(b => b.HasManyThrough<Ch309Tag, Ch309TagLink, int, int>(
            p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId));

    private EntityBuilder<Ch309Child> Children() => _ctx.From<Ch309Child>();

    private EntityBuilder<Ch309Profile> Profiles() => _ctx.From<Ch309Profile>();

    private EntityBuilder<Ch309Tag> Tags() => _ctx.From<Ch309Tag>();

    private void Seed()
    {
        Execute("drop table if exists ch309_link");
        Execute("drop table if exists ch309_tag");
        Execute("drop table if exists ch309_profile");
        Execute("drop table if exists ch309_child");
        Execute("drop table if exists ch309_parent");

        Execute("create table ch309_parent (id Int32, name Nullable(String)) engine = Memory");
        Execute("create table ch309_child (id Int32, parent_id Int32, tag Nullable(String)) engine = Memory");
        Execute("create table ch309_profile (id Int32, parent_id Int32, tag Nullable(String)) engine = Memory");
        Execute("create table ch309_tag (id Int32, tag Nullable(String)) engine = Memory");
        Execute("create table ch309_link (id Int32, parent_id Int32, child_id Int32) engine = Memory");

        // Register the relationship metadata before the first insert: child types first, so their
        // navigation members are already recognized when the parent-side relationship resolves.
        Children();
        Profiles();
        Tags();
        Parents();
        TagParents();

        _ctx.CreateInsertBuilder<Ch309Parent>().Values([
            new Ch309Parent { Id = 9501, Name = "p9501" },
            new Ch309Parent { Id = 9502, Name = "p9502" },
            new Ch309Parent { Id = 9503, Name = "p9503" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Ch309Child>().Values([
            new Ch309Child { Id = 9511, ParentId = 9501, Tag = "keep" },
            new Ch309Child { Id = 9512, ParentId = 9501, Tag = "keep" },
            new Ch309Child { Id = 9513, ParentId = 9501, Tag = Skip },
            new Ch309Child { Id = 9521, ParentId = 9502, Tag = "keep" },
            new Ch309Child { Id = 9522, ParentId = 9502, Tag = Skip },
        ]).Insert();

        _ctx.CreateInsertBuilder<Ch309Profile>().Values([
            new Ch309Profile { Id = 9531, ParentId = 9501, Tag = "keep" },
            new Ch309Profile { Id = 9532, ParentId = 9502, Tag = Skip },
        ]).Insert();

        _ctx.CreateInsertBuilder<Ch309TagParent>().Values([
            new Ch309TagParent { Id = 9601, Name = "m9601" },
            new Ch309TagParent { Id = 9602, Name = "m9602" },
            new Ch309TagParent { Id = 9603, Name = "m9603" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Ch309Tag>().Values([
            new Ch309Tag { Id = 9611, Tag = "keep" },
            new Ch309Tag { Id = 9612, Tag = "keep" },
            new Ch309Tag { Id = 9613, Tag = Skip },
            new Ch309Tag { Id = 9621, Tag = "keep" },
            new Ch309Tag { Id = 9622, Tag = Skip },
        ]).Insert();

        _ctx.CreateInsertBuilder<Ch309TagLink>().Values([
            new Ch309TagLink { Id = 9631, ParentId = 9601, ChildId = 9611 },
            new Ch309TagLink { Id = 9632, ParentId = 9601, ChildId = 9612 },
            new Ch309TagLink { Id = 9633, ParentId = 9601, ChildId = 9613 },
            new Ch309TagLink { Id = 9634, ParentId = 9602, ChildId = 9621 },
            new Ch309TagLink { Id = 9635, ParentId = 9602, ChildId = 9622 },
        ]).Insert();
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("ch309_parent")]
public sealed class Ch309Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<Ch309Child> Children { get; set; } = new List<Ch309Child>();

    public Ch309Profile? PrimaryProfile { get; set; }
}

[SqlTable("ch309_child")]
public sealed class Ch309Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("ch309_profile")]
public sealed class Ch309Profile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("ch309_parent")]
public sealed class Ch309TagParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<Ch309Tag> Tags { get; set; } = new List<Ch309Tag>();
}

[SqlTable("ch309_tag")]
public sealed class Ch309Tag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("ch309_link")]
public sealed class Ch309TagLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
