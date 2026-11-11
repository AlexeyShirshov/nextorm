using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// D03 (#223): MariaDB-owned runtime cases for a <b>composite junction</b> many-to-many <c>JoinInto</c>.
/// MariaDB has no <see cref="CommonTestSuite"/>-based integration class, so the shared composite-junction
/// graph/occurrence/null/query-count matrix is re-pinned here against a real server for both the standalone
/// and the SingleQuery dispatcher. The junction carries two parent foreign-key components matched against
/// the full (tenant, link_id) principal key. A skip is never a pass; MySQL is not MariaDB evidence.
/// </summary>
public sealed class MariaDbJoinIntoCompositeJunctionIntegrationTests : IDisposable
{
    private readonly MariaDbDataContext _ctx;

    public MariaDbJoinIntoCompositeJunctionIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinInto_CompositeJunction_Standalone_ShouldStitchGraphPreservingOccurrences()
    {
        var (first, second) = JoinIntoCompositeJunctionFixtures.Seed(_ctx);

        var interceptor = new JoinIntoQueryCountingInterceptor();
        _ctx.AddInterceptor(interceptor);

        var parents = _ctx.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .JoinInto(_ctx.From<JiCompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1, "the composite junction stitches the graph from one denormalized round trip");

        parents.Select(p => p.RowId).Should().Equal([first, second]);
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
        var (first, _) = JoinIntoCompositeJunctionFixtures.Seed(_ctx);

        var interceptor = new JoinIntoQueryCountingInterceptor();
        _ctx.AddInterceptor(interceptor);

        var parents = _ctx.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first)
            .JoinInto(_ctx.From<JiCompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>(), p => p.RowId, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        interceptor.Executing.Should().Be(1, "SingleQuery stitches the composite graph in one materialization query");

        parents.Should().ContainSingle("the denormalized rows collapse to one parent");
        parents[0].Children.Should().HaveCount(2, "the repeated junction occurrence is preserved in the single query");
        parents[0].Children.Select(c => c.Id).Should().OnlyContain(id => id == first + 100);
        parents[0].Notes.Select(n => n.Id).Should().Equal([first + 200],
            "the SingleQuery LoadWith note is stitched beside the composite junction");
    }

    private void Seed()
    {
        Execute("drop table if exists eager_cj_link");
        Execute("drop table if exists eager_cj_child");
        Execute("drop table if exists eager_cj_parent");
        Execute("drop table if exists eager_note");
        Execute("create table eager_cj_parent (row_id int not null primary key, tenant int not null, link_id int not null, name varchar(100) null)");
        Execute("create table eager_cj_child (id int not null primary key, name varchar(100) null)");
        Execute("create table eager_cj_link (parent_tenant int not null, parent_link_id int not null, child_id int not null)");
        Execute("create table eager_note (id int not null primary key, parent_id int not null, text varchar(100) null)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
