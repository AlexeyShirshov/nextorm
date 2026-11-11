using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// #309 live rows for the child-source <c>Where</c> on MariaDB. MariaDB has no
/// <see cref="CommonTestSuite"/> subclass, so the shared live assertions are re-pinned here against a
/// real server (a Testcontainers instance unless <c>NEXTORM_MARIADB_CONNECTION</c> points at an
/// existing one). Every fact reads materialized rows: the defect returns plausible collections, so only
/// values can prove that the child predicate reached the join. When the container is unavailable the
/// constructor reports the reason and the requirement row stays open — a skip is never a pass.
/// </summary>
public sealed class MariaDbJoinIntoChildWhereIntegrationTests : IDisposable
{
    private const string Skip = "skip";

    private readonly IDataContext _ctx;

    public MariaDbJoinIntoChildWhereIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
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

    private EntityBuilder<Mdb309Parent> Parents()
        => _ctx.From<Mdb309Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.PrimaryProfile, p => p.Id, c => c.ParentId));

    private EntityBuilder<Mdb309TagParent> TagParents()
        => _ctx.From<Mdb309TagParent>(b => b.HasManyThrough<Mdb309Tag, Mdb309TagLink, int, int>(
            p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId));

    private EntityBuilder<Mdb309Child> Children() => _ctx.From<Mdb309Child>();

    private EntityBuilder<Mdb309Profile> Profiles() => _ctx.From<Mdb309Profile>();

    private EntityBuilder<Mdb309Tag> Tags() => _ctx.From<Mdb309Tag>();

    private void Seed()
    {
        foreach (var statement in SchemaStatements)
            Execute(statement);

        // Register the relationship metadata before the first insert: child types first, so their
        // navigation members are already recognized when the parent-side relationship resolves.
        Children();
        Profiles();
        Tags();
        Parents();
        TagParents();

        _ctx.CreateInsertBuilder<Mdb309Parent>().Values([
            new Mdb309Parent { Id = 9501, Name = "p9501" },
            new Mdb309Parent { Id = 9502, Name = "p9502" },
            new Mdb309Parent { Id = 9503, Name = "p9503" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb309Child>().Values([
            new Mdb309Child { Id = 9511, ParentId = 9501, Tag = "keep" },
            new Mdb309Child { Id = 9512, ParentId = 9501, Tag = "keep" },
            new Mdb309Child { Id = 9513, ParentId = 9501, Tag = Skip },
            new Mdb309Child { Id = 9521, ParentId = 9502, Tag = "keep" },
            new Mdb309Child { Id = 9522, ParentId = 9502, Tag = Skip },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb309Profile>().Values([
            new Mdb309Profile { Id = 9531, ParentId = 9501, Tag = "keep" },
            new Mdb309Profile { Id = 9532, ParentId = 9502, Tag = Skip },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb309TagParent>().Values([
            new Mdb309TagParent { Id = 9601, Name = "m9601" },
            new Mdb309TagParent { Id = 9602, Name = "m9602" },
            new Mdb309TagParent { Id = 9603, Name = "m9603" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb309Tag>().Values([
            new Mdb309Tag { Id = 9611, Tag = "keep" },
            new Mdb309Tag { Id = 9612, Tag = "keep" },
            new Mdb309Tag { Id = 9613, Tag = Skip },
            new Mdb309Tag { Id = 9621, Tag = "keep" },
            new Mdb309Tag { Id = 9622, Tag = Skip },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb309TagLink>().Values([
            new Mdb309TagLink { Id = 9631, ParentId = 9601, ChildId = 9611 },
            new Mdb309TagLink { Id = 9632, ParentId = 9601, ChildId = 9612 },
            new Mdb309TagLink { Id = 9633, ParentId = 9601, ChildId = 9613 },
            new Mdb309TagLink { Id = 9634, ParentId = 9602, ChildId = 9621 },
            new Mdb309TagLink { Id = 9635, ParentId = 9602, ChildId = 9622 },
        ]).Insert();
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private static readonly string[] SchemaStatements =
    [
        "drop table if exists mdb309_link",
        "drop table if exists mdb309_tag",
        "drop table if exists mdb309_profile",
        "drop table if exists mdb309_child",
        "drop table if exists mdb309_parent",
        "create table mdb309_parent (id int not null primary key, name varchar(100) null)",
        "create table mdb309_child (id int not null primary key, parent_id int not null, tag varchar(100) null)",
        "create table mdb309_profile (id int not null primary key, parent_id int not null, tag varchar(100) null)",
        "create table mdb309_tag (id int not null primary key, tag varchar(100) null)",
        "create table mdb309_link (id int not null primary key, parent_id int not null, child_id int not null)",
    ];
}

[SqlTable("mdb309_parent")]
public sealed class Mdb309Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<Mdb309Child> Children { get; set; } = new List<Mdb309Child>();

    public Mdb309Profile? PrimaryProfile { get; set; }
}

[SqlTable("mdb309_child")]
public sealed class Mdb309Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("mdb309_profile")]
public sealed class Mdb309Profile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("mdb309_parent")]
public sealed class Mdb309TagParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<Mdb309Tag> Tags { get; set; } = new List<Mdb309Tag>();
}

[SqlTable("mdb309_tag")]
public sealed class Mdb309Tag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("mdb309_link")]
public sealed class Mdb309TagLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
