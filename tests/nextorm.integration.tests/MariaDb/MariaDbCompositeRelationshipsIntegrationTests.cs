using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T251 D251-05 live MariaDB row: MariaDB has no <see cref="CommonTestSuite"/>-based integration class,
/// so the declared composite surface (1:N, N:1, 1:1, composite-junction M:N and the per-position null
/// foreign key) is re-pinned here against a real server with its own tables. The relationships are
/// declared through the public fluent A–J overloads, never injected. A skip is never a pass; a MySQL
/// result is not MariaDB evidence.
/// </summary>
public sealed class MariaDbCompositeRelationshipsIntegrationTests : IDisposable
{
    private readonly MariaDbDataContext _ctx;

    public MariaDbCompositeRelationshipsIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T251_DeclaredCompositeOneToMany_ShouldMatchEveryComponentOnMariaDb()
    {
        var terminals = _ctx.From<MdbT251Parent>()
            .Where(p => p.RowId == OneToManyBase || p.RowId == OneToManyBase + 1 || p.RowId == OneToManyBase + 2)
            .OrderBy(p => p.RowId)
            .Select(p => new { p.RowId, Has = p.Children.Any(), Count = p.Children.Count() })
            .ToList();

        terminals.Select(r => r.RowId).Should().Equal(OneToManyBase, OneToManyBase + 1, OneToManyBase + 2);
        terminals[0].Count.Should().Be(2);
        terminals[1].Count.Should().Be(1);
        terminals[2].Count.Should().Be(1);
        terminals.Should().OnlyContain(r => r.Has == (r.Count > 0));

        var stitched = _ctx.From<MdbT251Parent>()
            .Where(p => p.RowId == OneToManyBase || p.RowId == OneToManyBase + 1 || p.RowId == OneToManyBase + 2)
            .JoinInto(
                _ctx.From<MdbT251Child>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        stitched[0].Children.Select(ch => ch.Id).Should().BeEquivalentTo([OneToManyBase + 100, OneToManyBase + 101]);
        stitched[1].Children.Select(ch => ch.Id).Should().Equal(OneToManyBase + 102);
        stitched[2].Children.Select(ch => ch.Id).Should().Equal(OneToManyBase + 103);
        stitched.SelectMany(p => p.Children).Select(ch => ch.Id)
            .Should().NotContain(OneToManyBase + 104)
            .And.NotContain(OneToManyBase + 105);
    }

    [Fact]
    public void T251_DeclaredCompositeManyToOne_ShouldResolveReferenceAndRejectTenantCollision()
    {
        var rows = _ctx.From<MdbT251Child>()
            .Where(ch => ch.Id == OneToManyBase + 100 || ch.Id == OneToManyBase + 103 || ch.Id == OneToManyBase + 105)
            .OrderBy(ch => ch.Id)
            .Select(ch => new { ch.Id, ParentRow = (int?)ch.Parent!.RowId })
            .ToList();

        rows.Single(r => r.Id == OneToManyBase + 100).ParentRow.Should().Be(OneToManyBase);
        rows.Single(r => r.Id == OneToManyBase + 103).ParentRow.Should().Be(OneToManyBase + 2,
            "the tenant component rejects the (t1, l1) parent and resolves the (t2, l1) one");
        rows.Single(r => r.Id == OneToManyBase + 105).ParentRow.Should().BeNull("a dangling key has no principal");
    }

    [Fact]
    public void T251_DeclaredCompositeOneToOne_ShouldAssignByEveryComponentOnMariaDb()
    {
        var parents = _ctx.From<MdbT251Parent>()
            .Where(p => p.RowId == OneToOneBase || p.RowId == OneToOneBase + 1 || p.RowId == OneToOneBase + 2)
            .JoinInto(
                _ctx.From<MdbT251Primary>(),
                (p, ch) => p.Tenant == ch.OwnerTenant && p.LinkId == ch.ParentLink,
                p => p.Primary)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(OneToOneBase, OneToOneBase + 1, OneToOneBase + 2);
        parents[0].Primary!.Id.Should().Be(OneToOneBase + 10);
        parents[1].Primary!.Id.Should().Be(OneToOneBase + 11);
        parents[2].Primary.Should().BeNull();
    }

    [Fact]
    public void T251_DeclaredCompositeJunction_ShouldPreserveOccurrencesOnMariaDb()
    {
        var counted = _ctx.From<MdbT251JunctionParent>()
            .Where(p => p.RowId == JunctionBase || p.RowId == JunctionBase + 1)
            .OrderBy(p => p.RowId)
            .Select(p => new { p.RowId, Count = p.Children.Count() })
            .ToList();

        counted.Select(r => r.RowId).Should().Equal(JunctionBase, JunctionBase + 1);
        counted[0].Count.Should().Be(2, "the duplicate link is preserved and the dangling link is excluded");
        counted[1].Count.Should().Be(1);

        var stitched = _ctx.From<MdbT251JunctionParent>()
            .Where(p => p.RowId == JunctionBase || p.RowId == JunctionBase + 1)
            .JoinInto(_ctx.From<MdbT251JunctionChild>(), (p, ch) => true, p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        stitched[0].Children.Should().HaveCount(2);
        stitched[0].Children.Select(ch => ch.Id).Should().OnlyContain(id => id == JunctionBase + 10);
        stitched[0].Children.Should().NotContainNulls();
        stitched[1].Children.Select(ch => ch.Id).Should().Equal(JunctionBase + 11);
    }

    [Fact]
    public void T251_NullForeignKeyComponents_ShouldYieldNoRelationshipOnMariaDb()
    {
        var row = _ctx.From<MdbT251Parent>()
            .Where(p => p.RowId == NullableBase)
            .Select(p => new { Has = p.NullableChildren.Any(), Count = p.NullableChildren.Count() })
            .ToList()
            .Single();

        row.Has.Should().BeTrue("the fully non-null child is linked");
        row.Count.Should().Be(1,
            "the first-null, last-null, all-null and dangling rows must each contribute nothing");
    }

    private const int OneToManyBase = -680_000_000;
    private const int OneToOneBase = OneToManyBase + 200;
    private const int NullableBase = OneToManyBase + 300;
    private const int JunctionBase = OneToManyBase + 400;

    private void Seed()
    {
        Execute("drop table if exists mdb_t251_child");
        Execute("drop table if exists mdb_t251_parent");
        Execute("drop table if exists mdb_t251_j_link");
        Execute("drop table if exists mdb_t251_j_child");
        Execute("drop table if exists mdb_t251_j_parent");
        Execute("create table mdb_t251_parent (row_id int not null primary key, tenant int not null, link_id int not null)");
        Execute("create table mdb_t251_child (id int not null primary key, tenant int null, parent_id int null, sort_key int not null default 0)");
        Execute("create table mdb_t251_j_parent (row_id int not null primary key, tenant int not null, link_id int not null)");
        Execute("create table mdb_t251_j_child (id int not null primary key)");
        Execute("create table mdb_t251_j_link (parent_tenant int not null, parent_link_id int not null, child_id int not null)");

        Register();

        var t1 = OneToManyBase;
        var t2 = OneToManyBase + 1;
        var l1 = OneToManyBase + 10;
        var l2 = OneToManyBase + 20;

        _ctx.CreateInsertBuilder<MdbT251Parent>().Values([
            new MdbT251Parent { RowId = OneToManyBase, Tenant = t1, LinkId = l1 },
            new MdbT251Parent { RowId = OneToManyBase + 1, Tenant = t1, LinkId = l2 },
            new MdbT251Parent { RowId = OneToManyBase + 2, Tenant = t2, LinkId = l1 },
            new MdbT251Parent { RowId = OneToOneBase, Tenant = OneToOneBase + 50, LinkId = OneToOneBase + 10 },
            new MdbT251Parent { RowId = OneToOneBase + 1, Tenant = OneToOneBase + 50, LinkId = OneToOneBase + 20 },
            new MdbT251Parent { RowId = OneToOneBase + 2, Tenant = OneToOneBase + 50, LinkId = OneToOneBase + 30 },
            new MdbT251Parent { RowId = NullableBase, Tenant = NullableBase + 1, LinkId = NullableBase + 2 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbT251Child>().Values([
            new MdbT251Child { Id = OneToManyBase + 100, Tenant = t1, ParentId = l1, SortKey = 1 },
            new MdbT251Child { Id = OneToManyBase + 101, Tenant = t1, ParentId = l1, SortKey = 2 },
            new MdbT251Child { Id = OneToManyBase + 102, Tenant = t1, ParentId = l2, SortKey = 3 },
            new MdbT251Child { Id = OneToManyBase + 103, Tenant = t2, ParentId = l1, SortKey = 4 },
            new MdbT251Child { Id = OneToManyBase + 104, Tenant = t2, ParentId = l2, SortKey = 5 },
            new MdbT251Child { Id = OneToManyBase + 105, Tenant = t1, ParentId = OneToManyBase + 99, SortKey = 6 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbT251NullableChild>().Values([
            // Null components of the composite foreign key, one row per position, plus a dangling row.
            new MdbT251NullableChild { Id = OneToManyBase + 120, Tenant = NullableBase + 1, ParentId = NullableBase + 2, SortKey = 7 },
            new MdbT251NullableChild { Id = OneToManyBase + 121, Tenant = null, ParentId = NullableBase + 2, SortKey = 8 },
            new MdbT251NullableChild { Id = OneToManyBase + 122, Tenant = NullableBase + 1, ParentId = null, SortKey = 9 },
            new MdbT251NullableChild { Id = OneToManyBase + 123, Tenant = null, ParentId = null, SortKey = 10 },
            new MdbT251NullableChild { Id = OneToManyBase + 124, Tenant = NullableBase + 1, ParentId = NullableBase + 99, SortKey = 11 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbT251Primary>().Values([
            new MdbT251Primary { Id = OneToOneBase + 10, OwnerTenant = OneToOneBase + 50, ParentLink = OneToOneBase + 10 },
            new MdbT251Primary { Id = OneToOneBase + 11, OwnerTenant = OneToOneBase + 50, ParentLink = OneToOneBase + 20 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbT251JunctionParent>().Values([
            new MdbT251JunctionParent { RowId = JunctionBase, Tenant = JunctionBase, LinkId = JunctionBase + 1 },
            new MdbT251JunctionParent { RowId = JunctionBase + 1, Tenant = JunctionBase, LinkId = JunctionBase + 2 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbT251JunctionChild>().Values([
            new MdbT251JunctionChild { Id = JunctionBase + 10 },
            new MdbT251JunctionChild { Id = JunctionBase + 11 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbT251JunctionLink>().Values([
            new MdbT251JunctionLink { ParentTenant = JunctionBase, ParentLinkId = JunctionBase + 1, ChildId = JunctionBase + 10 },
            new MdbT251JunctionLink { ParentTenant = JunctionBase, ParentLinkId = JunctionBase + 2, ChildId = JunctionBase + 11 },
            new MdbT251JunctionLink { ParentTenant = JunctionBase, ParentLinkId = JunctionBase + 1, ChildId = JunctionBase + 10 },
            new MdbT251JunctionLink { ParentTenant = JunctionBase, ParentLinkId = JunctionBase + 1, ChildId = JunctionBase + 99 },
        ]).Insert();
    }

    private void Register()
    {
        _ctx.From<MdbT251Child>((EntityMetadataBuilder<MdbT251Child> b) => b.HasOne(
            c => c.Parent,
            c => new { c.Tenant, c.ParentId },
            p => new { p.Tenant, p.LinkId }));
        _ctx.From<MdbT251Primary>();
        _ctx.From<MdbT251NullableChild>();
        _ctx.From<MdbT251Parent>((EntityMetadataBuilder<MdbT251Parent> b) => b
            .HasMany(p => p.Children, c => new { c.Tenant, c.ParentId }, p => new { p.Tenant, p.LinkId })
            .HasMany(p => p.NullableChildren, c => new { c.Tenant, c.ParentId }, p => new { p.Tenant, p.LinkId })
            .HasOneToOne(p => p.Primary, p => new { p.Tenant, p.LinkId }, c => new { c.OwnerTenant, c.ParentLink }));

        _ctx.From<MdbT251JunctionChild>();
        _ctx.From<MdbT251JunctionLink>();
        _ctx.From<MdbT251JunctionParent>((EntityMetadataBuilder<MdbT251JunctionParent> b) => b.HasManyThrough<MdbT251JunctionChild, MdbT251JunctionLink>(
            p => p.Children,
            p => new { p.Tenant, p.LinkId },
            l => new { l.ParentTenant, l.ParentLinkId },
            c => new { c.Id },
            l => new { l.ChildId }));
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("mdb_t251_parent")]
internal sealed class MdbT251Parent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public ICollection<MdbT251Child> Children { get; set; } = new List<MdbT251Child>();

    public ICollection<MdbT251NullableChild> NullableChildren { get; set; } = new List<MdbT251NullableChild>();

    public MdbT251Primary? Primary { get; set; }
}

[SqlTable("mdb_t251_child")]
internal sealed class MdbT251Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    public MdbT251Parent? Parent { get; set; }
}

[SqlTable("mdb_t251_child")]
internal sealed class MdbT251NullableChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int? Tenant { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}

[SqlTable("mdb_t251_child")]
internal sealed class MdbT251Primary
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int OwnerTenant { get; set; }

    [Column("parent_id")]
    public int ParentLink { get; set; }
}

[SqlTable("mdb_t251_j_parent")]
internal sealed class MdbT251JunctionParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public ICollection<MdbT251JunctionChild> Children { get; set; } = new List<MdbT251JunctionChild>();
}

[SqlTable("mdb_t251_j_child")]
internal sealed class MdbT251JunctionChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("mdb_t251_j_link")]
internal sealed class MdbT251JunctionLink
{
    [Column("parent_tenant")]
    public int ParentTenant { get; set; }

    [Column("parent_link_id")]
    public int ParentLinkId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
