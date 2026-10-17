using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// #211 per-parent top-N eager loading against a real MariaDB server. MariaDB does not derive
/// <see cref="CommonTestSuite"/> and has no shared eager fixture, so this test owns its schema and seed
/// and runs against its own <see cref="MariaDbContainer"/> (the declared <c>mariadb:11.4</c> image, or
/// an external server named by <c>NEXTORM_MARIADB_CONNECTION</c>). A skip is never a pass: the
/// constructor reports the unavailable reason.
/// </summary>
public sealed class MariaDbEagerPagingTests : IDisposable
{
    private const int ParentBase = -550_000_000;

    private readonly MariaDbDataContext _ctx;

    public MariaDbEagerPagingTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private void Seed()
    {
        Execute("drop table if exists d211_maria_child");
        Execute("drop table if exists d211_maria_parent");
        Execute("create table d211_maria_parent (id int not null primary key, name varchar(64))");
        Execute("create table d211_maria_child (id int not null primary key, parent_id int not null, name varchar(64))");

        // Composite parent-link key fixture (#211 variant matrix :236): (tenant, link_id) is not the
        // table key (row_id), so a partition on either component alone blends two parents.
        Execute("drop table if exists eager_composite_child");
        Execute("drop table if exists eager_composite_parent");
        Execute("create table eager_composite_parent (row_id int not null primary key, tenant int not null, link_id int not null)");
        Execute("create table eager_composite_child (id int not null primary key, tenant int not null, parent_id int not null, sort_key int not null)");

        _ctx.CreateInsertBuilder<MariaDbEagerPagingParent>().Values([
            new MariaDbEagerPagingParent { Id = ParentBase, Name = "rich" },
            new MariaDbEagerPagingParent { Id = ParentBase + 1, Name = "few" },
            new MariaDbEagerPagingParent { Id = ParentBase + 2, Name = "empty" },
        ]).Insert();

        _ctx.CreateInsertBuilder<MariaDbEagerPagingChild>().Values([
            new MariaDbEagerPagingChild { Id = ParentBase + 100, ParentId = ParentBase, Name = "r1" },
            new MariaDbEagerPagingChild { Id = ParentBase + 101, ParentId = ParentBase, Name = "r2" },
            new MariaDbEagerPagingChild { Id = ParentBase + 102, ParentId = ParentBase, Name = "r3" },
            new MariaDbEagerPagingChild { Id = ParentBase + 110, ParentId = ParentBase + 1, Name = "f1" },
            new MariaDbEagerPagingChild { Id = ParentBase + 111, ParentId = ParentBase + 1, Name = "f2" },
        ]).Insert();

        var b = ParentBase;
        var tenantA = b;
        var tenantC = b + 1;
        var linkShared = b + 10;
        var linkOther = b + 20;
        _ctx.CreateInsertBuilder<EagerCompositeParent>().Values([
            new EagerCompositeParent { RowId = b, Tenant = tenantA, LinkId = linkShared },
            new EagerCompositeParent { RowId = b + 1, Tenant = tenantA, LinkId = linkOther },
            new EagerCompositeParent { RowId = b + 2, Tenant = tenantC, LinkId = linkShared },
        ]).Insert();
        _ctx.CreateInsertBuilder<EagerCompositeChild>().Values([
            new EagerCompositeChild { Id = b + 100, Tenant = tenantA, ParentId = linkShared, SortKey = 1 },
            new EagerCompositeChild { Id = b + 103, Tenant = tenantA, ParentId = linkShared, SortKey = 4 },
            new EagerCompositeChild { Id = b + 201, Tenant = tenantA, ParentId = linkOther, SortKey = 2 },
            new EagerCompositeChild { Id = b + 204, Tenant = tenantA, ParentId = linkOther, SortKey = 5 },
            new EagerCompositeChild { Id = b + 302, Tenant = tenantC, ParentId = linkShared, SortKey = 3 },
            new EagerCompositeChild { Id = b + 305, Tenant = tenantC, ParentId = linkShared, SortKey = 6 },
        ]).Insert();
    }

    private void Execute(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var cmd = _ctx.CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private EntityBuilder<MariaDbEagerPagingParent> Parents()
        => _ctx.From<MariaDbEagerPagingParent>()
            .Where(p => p.Id == ParentBase || p.Id == ParentBase + 1 || p.Id == ParentBase + 2);

    [Fact]
    [Trait("Feature", "D211")]
    public void MariaDb_Split_Take_AppliesPerParent()
    {
        var parents = Parents()
            .LoadWith(p => p.Children, c => c.From<MariaDbEagerPagingChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(ParentBase, ParentBase + 1, ParentBase + 2);
        parents[0].Children.Select(c => c.Id).Should().Equal(ParentBase + 100, ParentBase + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(ParentBase + 110, ParentBase + 111);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void MariaDb_SingleQuery_SkipAndTake_AppliesPerParent()
    {
        var parents = Parents()
            .LoadWith(p => p.Children, c => c.From<MariaDbEagerPagingChild>().OrderBy(x => x.Id).Offset(1).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(ParentBase + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(ParentBase + 111);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void MariaDb_Split_RootPagingAndChildPagingAreIndependent()
    {
        var parents = Parents()
            .LoadWith(p => p.Children, c => c.From<MariaDbEagerPagingChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .Offset(1)
            .Limit(2)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(ParentBase + 1, ParentBase + 2);
        parents[0].Children.Select(c => c.Id).Should().Equal(ParentBase + 110);
        parents[1].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void MariaDb_CompositeKey_Split_Take_PartitionsByBothComponents()
    {
        var b = ParentBase;
        var parents = _ctx.From<EagerCompositeParent>()
            .Where(p => p.RowId == b || p.RowId == b + 1 || p.RowId == b + 2)
            .LoadWith(
                p => p.Children,
                c => c.From<EagerCompositeChild>().OrderBy(x => x.SortKey).Limit(2),
                p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(b, b + 1, b + 2);
        parents[0].Children.Select(c => c.Id).Should().Equal(b + 100, b + 103);
        parents[1].Children.Select(c => c.Id).Should().Equal(b + 201, b + 204);
        parents[2].Children.Select(c => c.Id).Should().Equal(b + 302, b + 305);
    }
}

[SqlTable("d211_maria_parent")]
public sealed class MariaDbEagerPagingParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MariaDbEagerPagingChild> Children { get; } = new List<MariaDbEagerPagingChild>();
}

[SqlTable("d211_maria_child")]
public sealed class MariaDbEagerPagingChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
