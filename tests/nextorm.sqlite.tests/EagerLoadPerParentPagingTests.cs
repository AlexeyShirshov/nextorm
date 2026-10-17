using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Per-parent top-N eager loading (#211): a child query's own <c>Limit</c>/<c>Offset</c>/<c>Page</c> is
/// applied independently to each parent through the split loader. The child source is lowered to a
/// <c>row_number() over (partition by &lt;child key&gt;)</c> derived table whose page bounds filter each
/// partition, so a globally limited child query can no longer starve later parents.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText);
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-perparent-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table per_page_parent (id integer primary key, name text);" +
                "create table per_page_child (id integer primary key, parent_id int not null, sort_key int not null, group_key int not null, flag int not null);" +
                "create table per_page_note (id integer primary key, parent_id int not null, rank int not null);" +
                "insert into per_page_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'empty');" +
                // Parent 1 has four children, parent 2 has two, parent 3 none. group_key deliberately
                // disagrees with sort_key so a two-key ordering is distinguishable from a single key.
                "insert into per_page_child (id, parent_id, sort_key, group_key, flag) values" +
                " (11, 1, 40, 2, 1), (12, 1, 10, 1, 1), (13, 1, 30, 1, 1), (14, 1, 20, 2, 0)," +
                " (21, 2, 50, 1, 1), (22, 2, 5, 2, 1);" +
                "insert into per_page_note (id, parent_id, rank) values" +
                " (101, 1, 2), (102, 1, 1), (103, 1, 3), (201, 2, 7);" +
                // Composite parent-link key fixture: the link key is (tenant, link_id), which is NOT a
                // table key (row_id is). The three parents are (1,10), (1,20), (2,10): each parent shares
                // one component with another, so a partition that used only tenant or only link_id blends
                // two parents. Children are interleaved by sort_key across parents, so a global page or a
                // one-component partition changes every per-parent id set.
                "create table per_page_composite_parent (row_id integer primary key, tenant int not null, link_id int not null, name text);" +
                "create table per_page_composite_child (id integer primary key, tenant int not null, parent_id int not null, sort_key int not null);" +
                "insert into per_page_composite_parent (row_id, tenant, link_id, name) values" +
                " (1, 1, 10, 'p1'), (2, 1, 20, 'p2'), (3, 2, 10, 'p3');" +
                "insert into per_page_composite_child (id, tenant, parent_id, sort_key) values" +
                " (101, 1, 10, 1), (104, 1, 10, 4), (107, 1, 10, 7), (110, 1, 10, 10)," +
                " (202, 1, 20, 2), (205, 1, 20, 5), (208, 1, 20, 8)," +
                " (303, 2, 10, 3), (306, 2, 10, 6), (309, 2, 10, 9);";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void Split_Take_AppliesToEachParentIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            // Parent 1: two lowest sort keys among its four children.
            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            // Parent 2: two lowest (has only two).
            parents[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
            // Parent 3 stays and is empty: no parent disappears for zero qualifying children.
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_SkipAndTake_AppliesToEachParentIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.SortKey).Should().Equal(20);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_SkipOnly_AppliesToEachParentIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Skip with no Take: each parent drops only its own first ranked child; a mutation that
            // collapses the lower-only bound to the `rn >= 1` branch or adds a spurious upper bound
            // (limit >= 0) changes these per-parent results.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.SortKey).Should().Equal(20, 30, 40);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_ChildFilterPrecedesRanking()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            // Parent 1's flag=0 child (sort 20) is filtered out before ranking, so flags {10,30,40} page to {10,30}.
            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 30);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Split_Async_AppliesToEachParentIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = await ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToListAsync();

            parents[0].Children.Select(c => c.SortKey).Should().Equal(10);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(5);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_UnpagedChild_RetainsEveryChild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Should().HaveCount(4);
            parents[1].Children.Should().HaveCount(2);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_PagedSql_UsesPartitionedRowNumberAndBounds()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            _ = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            // One parent statement plus one child statement: still batched, never N+1.
            interceptor.Statements.Should().HaveCount(2);
            var childSql = interceptor.Statements[1].ToLowerInvariant();
            childSql.Should().Contain("row_number()").And.Contain("over (").And.Contain("partition by");
            childSql.Should().Contain("> ").And.Contain("<= ");
            // The rank lives in the inner derived table and is dropped from the outer projection (the
            // outer selects the entity columns explicitly, never `t1.*`).
            childSql.Should().Contain("from (select *");
            childSql.Should().NotContain("select t1.*");
            // The rank appears only in the derived table and its outer filter, never in the outer
            // projection (the part before `from (select`).
            var outerProjection = childSql.Split("from (select")[0];
            outerProjection.Should().NotContain("__nextorm_rn");
            // Two ORDER BY clauses: the window's rank order and the top-level split child's own output
            // order after the derived table. Dropping the outer one silently loses the child's guaranteed
            // order for Take > 1.
            (childSql.Split("order by", StringSplitOptions.None).Length - 1).Should().Be(2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_PagedChild_RespectsRootPagingIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Root Take/Skip selects parents 2 and 3; the child paging still applies per parent.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .Offset(1)
                .Limit(2)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(2, 3);
            parents[0].Children.Select(c => c.SortKey).Should().Equal(5);
            parents[1].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- single-query mode (#211): the same per-parent paging semantics through one ranked derived join ---

    [Fact]
    public void SingleQuery_Take_AppliesToEachParentIndependently()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
            // No parent disappears for zero qualifying children.
            parents[2].Children.Should().BeEmpty();
            interceptor.Statements.Should().HaveCount(1, "single-query paging stays one denormalized command");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_SkipAndTake_AppliesToEachParentIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.SortKey).Should().Equal(20);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_SkipOnly_AppliesToEachParentIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.SortKey).Should().Equal(20, 30, 40);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_PagedChild_WithoutParentOrder_KeepsChildPageOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The parent has no ORDER BY: the embedded child ordering must start from an empty parent
            // ordering and still render each child's own order (the null-parentSorting branch of
            // EntityBuilder.AppendEmbeddedChildOrdering).
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .ToList();

            parents.Should().HaveCount(3);
            var byId = parents.ToDictionary(p => p.Id);
            byId[1].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            byId[2].Children.Select(c => c.SortKey).Should().Equal(5, 50);
            byId[3].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void PerParentPaging_PlanKey_SeparatesPagedPartitionAndEmbeddedShapes()
    {
        using var ctx = SqliteTestContext.Create();

        static QueryCommand Child(IDataContext c, System.Linq.Expressions.Expression<Func<PerPagePageChild, int>> partition, bool paged, bool embedded)
        {
            var cmd = c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2).Select(x => x.Id);
            cmd.PerParentPaging = paged;
            cmd.PerParentPartition = partition;
            cmd.PerParentPagingEmbedded = embedded;
            return cmd;
        }

        var pagedEmbedded = Child(ctx, x => x.ParentId, paged: true, embedded: true);
        var pagedSplit = Child(ctx, x => x.ParentId, paged: true, embedded: false);
        var pagedOtherPartition = Child(ctx, x => x.SortKey, paged: true, embedded: true);
        var unpaged = Child(ctx, x => x.ParentId, paged: false, embedded: false);
        var control = Child(ctx, x => x.ParentId, paged: true, embedded: true);

        var comparer = pagedEmbedded.GetQueryPlanEqualityComparer();

        // REQ-05: the per-parent plan-key separations must not alias a cached plan.
        comparer.Equals(pagedEmbedded, unpaged).Should().BeFalse("a paged child must not reuse an unpaged plan");
        comparer.Equals(pagedEmbedded, pagedOtherPartition).Should().BeFalse("a different partition selector must not share the plan");
        comparer.Equals(pagedEmbedded, pagedSplit).Should().BeFalse("embedded and split render different SQL from the same ranked shape");
        comparer.Equals(pagedEmbedded, control).Should().BeTrue("an identical paged shape must reuse the plan");
        comparer.GetHashCode(pagedEmbedded).Should().Be(comparer.GetHashCode(control));
    }

    [Fact]
    public void SingleQuery_ChildFilterPrecedesRanking()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            // Parent 1's flag=0 child (sort 20) is filtered out before ranking, so flags {10,30,40} page to {10,30}.
            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 30);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_PagedChild_RespectsRootPagingIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .Offset(1)
                .Limit(2)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(2, 3);
            parents[0].Children.Select(c => c.SortKey).Should().Equal(5);
            parents[1].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_And_Split_ProduceSamePerParentResultWithDifferentQueryCounts()
    {
        var splitInterceptor = new SqlRecordingInterceptor();
        var (splitCtx, splitPath) = CreateDb();
        splitCtx.AddInterceptor(splitInterceptor);
        var singleInterceptor = new SqlRecordingInterceptor();
        var (singleCtx, singlePath) = CreateDb();
        singleCtx.AddInterceptor(singleInterceptor);
        try
        {
            splitCtx.PurgeQueryCache();
            singleCtx.PurgeQueryCache();

            var split = splitCtx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            var single = singleCtx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            split.Select(p => p.Id).Should().Equal(single.Select(p => p.Id));
            for (var i = 0; i < split.Count; i++)
            {
                split[i].Children.Select(c => c.SortKey).Should().Equal(single[i].Children.Select(c => c.SortKey));
                split[i].Children.Should().HaveCount(single[i].Children.Count);
            }

            // Split batches (one parent + one child statement); single merges into one denormalized command.
            splitInterceptor.Statements.Should().HaveCount(2);
            singleInterceptor.Statements.Should().HaveCount(1);
        }
        finally
        {
            splitCtx.Dispose();
            singleCtx.Dispose();
            File.Delete(splitPath);
            File.Delete(singlePath);
        }
    }

    [Fact]
    public void SingleQuery_PagedSql_MovesChildOrderOutOfTheNestedDerivedSource()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            _ = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            interceptor.Statements.Should().HaveCount(1);
            var sql = interceptor.Statements[0].ToLowerInvariant();
            sql.Should().Contain("row_number()").And.Contain("over (").And.Contain("partition by");
            sql.Should().Contain("> ").And.Contain("<= ");

            // The ranking window keeps its order by the child key.
            sql.Should().Contain("over (partition by parent_id order by sort_key)");

            // The ranked child is a nested derived source inside the join, not a top-level statement. A
            // nested derived table must not carry the child's output ORDER BY (SQL Server rejects it), so
            // the statement-level ORDER BY moves to the outer pair command. Verify the region after the
            // rank filter up to the derived table's closing parenthesis carries no ORDER BY; the ranking
            // window above is the only remaining "order by" inside the nested source.
            var lastRankReference = sql.LastIndexOf("__nextorm_rn", StringComparison.Ordinal);
            lastRankReference.Should().BeGreaterThanOrEqualTo(0, "the ranked child exposes its partition rank");
            var derivedClose = sql.IndexOf(") as '", lastRankReference, StringComparison.Ordinal);
            derivedClose.Should().BeGreaterThan(lastRankReference, "the ranked child is a derived table");
            sql[lastRankReference..derivedClose].Should().NotContain(
                "order by", "the nested derived table must drop the statement-level child ORDER BY");

            // The outer pair command carries the child key rebound to the join alias, after the parent key.
            sql.Should().Contain("order by t1.id, t3.sortkey");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_OrdinalOrderBy_RendersTheColumnIndexInTheRankingWindow()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            _ = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(1).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            interceptor.Statements.Should().HaveCount(2);
            var childSql = interceptor.Statements[1].ToLowerInvariant();
            // A child ordered by the public OrderBy(int) ordinal carries a column-index Sorting with no
            // prepared expression; the rank window and the outer statement emit that ordinal verbatim.
            childSql.Should().Contain("over (partition by parent_id order by 1)");
            childSql.Should().Contain("order by 1");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static (SqliteDataContext Ctx, string Path) CreateOrdinalDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-perparent-ordinal-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table per_page_parent (id integer primary key, name text);" +
                "create table per_page_child (id integer primary key, parent_id int not null, sort_key int not null, group_key int not null, flag int not null);" +
                "insert into per_page_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'empty');" +
                // Insertion order is deliberately NOT the id order (14,12,11,13 for parent 1), so an
                // incidental/DB order differs from the ordinal (`ORDER BY 1` -> id) order.
                "insert into per_page_child (id, parent_id, sort_key, group_key, flag) values" +
                " (14, 1, 20, 2, 0), (12, 1, 10, 1, 1), (11, 1, 40, 2, 1), (13, 1, 30, 1, 1)," +
                " (22, 2, 5, 2, 1), (21, 2, 50, 1, 1);";
            cmd.ExecuteNonQuery();
        }
        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void Split_OrdinalOrderBy_RetainsEffectivePerParentOrder()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateOrdinalDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(1).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            // Independent expectation: `ORDER BY 1` is the first projected column (id). The fixture's
            // insertion order differs, so a lost ordinal would surface the incidental 14,12 order instead.
            parents[0].Children.Select(c => c.Id).Should().Equal(11, 12);
            parents[1].Children.Select(c => c.Id).Should().Equal(21, 22);

            interceptor.Statements[1].ToLowerInvariant()
                .Should().Contain("over (partition by parent_id order by 1)");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_OrdinalOrderBy_RetainsEffectivePerParentOrder()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateOrdinalDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(1).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.Id).Should().Equal(11, 12);
            parents[1].Children.Select(c => c.Id).Should().Equal(21, 22);

            // Structural: the embedded/single-query window ordering must actually carry the ordinal column
            // (`order by 1`); result equality + the expression control alone cannot exclude incidental DB
            // order. The split path asserts the same window ordering.
            interceptor.Statements.Should().HaveCount(1);
            interceptor.Statements[0].ToLowerInvariant()
                .Should().Contain("over (partition by parent_id order by 1)");

            // Expression-ordering control: the same id order expressed as a lambda must agree.
            var control = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();
            control[0].Children.Select(c => c.Id).Should().Equal(11, 12);
            control[1].Children.Select(c => c.Id).Should().Equal(21, 22);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_PagedChild_RepeatedOnSameContext_StaysCorrectThroughThePlanCache()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The second execution hits the child plan cache. A paged child carries sorting, so it has no
            // fast param recipe and the cache-hit refresh walks the per-parent source in parameter mode.
            for (var run = 0; run < 2; run++)
            {
                var parents = ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id)
                    .ToList();

                parents.Select(p => p.Id).Should().Equal(1, 2, 3);
                parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
                parents[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
                parents[2].Children.Should().BeEmpty();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_DescendingOrder_AppliesToEachParent()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderByDescending(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            // Parent 1 descending: 40, 30, 20, 10 -> the two highest are 11 and 13.
            parents[0].Children.Select(c => c.Id).Should().Equal(11, 13);
            // Parent 2 descending: 50, 5 -> both.
            parents[1].Children.Select(c => c.Id).Should().Equal(21, 22);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_MultipleSortKeys_ApplyInOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Two keys: group first, then sort. Differs from a single sort_key key, proving both bind.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.GroupKey).OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            // Parent 1: (group1,10)=12, (group1,30)=13, (group2,20)=14, (group2,40)=11 -> first two 12, 13.
            parents[0].Children.Select(c => c.Id).Should().Equal(12, 13);
            parents[1].Children.Select(c => c.Id).Should().Equal(21, 22);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_DescendingAndMultipleKeys_MatchSingleQuery()
    {
        var (splitCtx, splitPath) = CreateDb();
        var (singleCtx, singlePath) = CreateDb();
        try
        {
            splitCtx.PurgeQueryCache();
            singleCtx.PurgeQueryCache();

            var split = splitCtx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderByDescending(x => x.GroupKey).OrderByDescending(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            var single = singleCtx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderByDescending(x => x.GroupKey).OrderByDescending(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            // Both modes honor the same per-parent Skip+Take over the two-key descending order.
            split.Select(p => p.Id).Should().Equal(single.Select(p => p.Id));
            for (var i = 0; i < split.Count; i++)
                split[i].Children.Select(c => c.Id).Should().Equal(single[i].Children.Select(c => c.Id));
        }
        finally
        {
            splitCtx.Dispose();
            singleCtx.Dispose();
            File.Delete(splitPath);
            File.Delete(singlePath);
        }
    }

    [Fact]
    public void SingleQuery_NoOrder_PagedChild_IsRejectedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // An actual per-parent page without a window ordering would lower to
            // `row_number() over (partition by ...)` with no ORDER BY: invalid on providers such as
            // SQL Server and without a deterministic per-parent meaning. It fails closed before any SQL
            // rather than emitting the invalid window.
            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*");
            interceptor.Statements.Should().BeEmpty("an unordered per-parent page must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- zero/default page bounds follow the existing Paging contract (#211, variant matrix :232) -----

    [Fact]
    public void Split_LimitZero_DoesNotActivatePagingAndKeepsEveryChild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Paging.Limit == 0 means "no limit" (Paging.IsEmpty), so the child is not paged and no window
            // ordering is required: every child survives and the lack of an OrderBy is not rejected.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Limit(0), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Should().HaveCount(4);
            parents[1].Children.Should().HaveCount(2);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_OffsetZero_PreservesPositiveLimit()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Skip(0) adds no offset and must not erase the positive Take: each parent keeps its two
            // lowest children, exactly as Take-only paging.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(0).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_DefaultBounds_AreUnpaged()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Both bounds default/zero normalize to no paging (Paging.IsEmpty): the child is loaded whole.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Offset(0).Limit(0), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Should().HaveCount(4);
            parents[1].Children.Should().HaveCount(2);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_LimitZeroWithPositiveOffset_SkipsWithoutLimit()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Limit(0) with a positive Offset is skip-only (Paging.IsEmpty is false on the offset): each
            // parent drops its first ranked child and keeps every remaining one, with no upper bound.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(0), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.SortKey).Should().Equal(20, 30, 40);
            parents[1].Children.Select(c => c.SortKey).Should().Equal(50);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_TiedOrder_PagedChild_ReturnsMembershipAndCountOnly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Ordering by flag ties three of parent 1's children, so the surviving row is not promised.
            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.Flag).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Should().HaveCount(2);
            parents[0].Children.Select(c => c.Id).Should().Contain(14, "flag=0 sorts first");
            parents[0].Children.Select(c => c.Id).Should().BeSubsetOf([11, 12, 13, 14]);
            parents[1].Children.Select(c => c.Id).Should().BeEquivalentTo([21, 22]);
            parents[2].Children.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_MultiplePagedNavigations_ArePagedIndependently()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .LoadWith(p => p.Notes, c => c.From<PerPagePageNote>().OrderBy(x => x.Rank).Limit(1), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            // Each navigation ranks within its own partition independently.
            parents[0].Children.Select(c => c.Id).Should().Equal(12);
            parents[0].Notes.Select(n => n.Id).Should().Equal(102);
            parents[1].Children.Select(c => c.Id).Should().Equal(22);
            parents[1].Notes.Select(n => n.Id).Should().Equal(201);
            parents[2].Children.Should().BeEmpty();
            parents[2].Notes.Should().BeEmpty();

            interceptor.Statements.Should().HaveCount(1, "both paged navigations merge into one denormalized command");
            var sql = interceptor.Statements[0].ToLowerInvariant();
            // Two independent partition windows, one per paged navigation.
            sql.Should().Contain("partition by parent_id order by sort_key");
            sql.Should().Contain("partition by parent_id order by rank");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- composite parent-link key (#211, variant matrix :236/:241) ---------------------------------
    // The link key is a ValueTuple built with an explicit constructor (not tuple-literal syntax) so the
    // expression tree carries a ValueTuple NewExpression the partition builder can resolve. The key is
    // (tenant, link_id) and the three parents (1,10), (1,20), (2,10) overlap on one component each: a
    // partition that collapsed to either component, or a global page, changes every asserted id set.

    [Fact]
    public void Split_CompositeKey_Take_PartitionsByBothKeyComponents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPageCompositeParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<PerPageCompositeChild>().OrderBy(x => x.SortKey).Limit(2),
                    p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                    c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
                .OrderBy(p => p.RowId)
                .ToList();

            parents.Select(p => p.RowId).Should().Equal(1, 2, 3);
            // Interleaved sort values across parents: only the complete two-column partition gives these.
            parents[0].Children.Select(c => c.Id).Should().Equal(101, 104);
            parents[1].Children.Select(c => c.Id).Should().Equal(202, 205);
            parents[2].Children.Select(c => c.Id).Should().Equal(303, 306);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_CompositeKey_SkipAndTake_PartitionsByBothKeyComponents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPageCompositeParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<PerPageCompositeChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2),
                    p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                    c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
                .OrderBy(p => p.RowId)
                .ToList();

            parents[0].Children.Select(c => c.Id).Should().Equal(104, 107);
            parents[1].Children.Select(c => c.Id).Should().Equal(205, 208);
            parents[2].Children.Select(c => c.Id).Should().Equal(306, 309);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_CompositeKey_PagedSql_PartitionsByBothColumns()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            _ = ctx.From<PerPageCompositeParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<PerPageCompositeChild>().OrderBy(x => x.SortKey).Limit(2),
                    p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                    c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
                .OrderBy(p => p.RowId)
                .ToList();

            interceptor.Statements.Should().HaveCount(2);
            var childSql = interceptor.Statements[1].ToLowerInvariant();
            // Both link-key components reach the window partition; a single-column partition would drop one.
            childSql.Should().Contain("partition by tenant, parent_id");
            // The split key membership predicate is a two-column row-value list, not a scalar key.
            childSql.Should().Contain("(tenant, parent_id) in");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_CompositeKey_IsRejectedByThePreExistingKeyEqualityGuard()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Single-query mode needs a translatable Equal node for the join predicate. System.ValueTuple
            // defines no equality operator, so the pre-existing JoinIntoSpec guard (not added by #211)
            // rejects the load before any SQL runs. Split mode above is the supported composite path; this
            // is the :241 split/single-query intersection for a composite key (positive / fail-closed).
            var act = () => ctx.From<PerPageCompositeParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<PerPageCompositeChild>().OrderBy(x => x.SortKey).Limit(1),
                    p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                    c => new ValueTuple<int, int>(c.Tenant, c.ParentId),
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.RowId)
                .ToList();

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*no join predicate can be built*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_AnonymousKey_IsRejectedByThePreExistingReferenceEqualityGuard()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // An anonymous key is a reference type with no value-equality operator: Expression.Equal
            // would compare references, so the pre-existing JoinIntoSpec guard (not added by #211) rejects
            // the single-query load before any SQL runs. Split mode keeps working for anonymous keys.
            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(1),
                    p => new { A = p.Id, B = p.Id },
                    c => new { A = c.ParentId, B = c.SortKey },
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*would compare references*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- V9/V11 applicability probe: a derived (As / From(builder)) or set-operation child source is
    // excluded from single-query folding by the `_query is not null` shape check. The split per-parent
    // paging loader consumes the same ranked source, so the reachable split shape must fail closed too. ---

    [Fact]
    public void Split_DerivedChildSource_IsRejectedFailClosedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // V9: c.From(c.From<Child>().Select(...)) makes the child's primary source a derived subquery
            // whose condition/ordering live inside it, so the ranked per-parent paging source cannot fold
            // the child shape in. The split loader must reject before any SQL is built, not drop the
            // derived source or page it globally.
            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From(c.From<PerPagePageChild>().Where(x => x.Flag == 1).Select(x => x)).OrderBy(x => x.SortKey).Limit(2),
                    p => p.Id,
                    c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*derived (As) source*");
            interceptor.Statements.Should().BeEmpty("a derived child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_DerivedChildSource_IsRejectedFailClosedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From(c.From<PerPagePageChild>().Where(x => x.Flag == 1).Select(x => x)).OrderBy(x => x.SortKey).Limit(2),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*derived (As) source*");
            interceptor.Statements.Should().BeEmpty("a derived child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_SetOperationChildSource_IsRejectedFailClosedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // V11: a UNION child source is carried as a derived (_query) builder, so the ranked per-parent
            // paging source cannot fold it. The split loader must reject before SQL.
            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From(
                            c.From<PerPagePageChild>().Where(x => x.Flag == 1).ToCommand()
                                .Union(c.From<PerPagePageChild>().Where(x => x.Flag == 0).ToCommand()))
                        .OrderBy(x => x.SortKey)
                        .Limit(2),
                    p => p.Id,
                    c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*derived (As) source*");
            interceptor.Statements.Should().BeEmpty("a set-operation child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_SetOperationChildSource_IsRejectedFailClosedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From(
                            c.From<PerPagePageChild>().Where(x => x.Flag == 1).ToCommand()
                                .Union(c.From<PerPagePageChild>().Where(x => x.Flag == 0).ToCommand()))
                        .OrderBy(x => x.SortKey)
                        .Limit(2),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*derived (As) source*");
            interceptor.Statements.Should().BeEmpty("a set-operation child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- #211 V17 / EV-06: builder argument form vs `.ToList(@params)` parity, rebinding, re-execution ----

    private static List<PerPagePageParent> LoadPagedByArgument(SqliteDataContext ctx, int minId)
        => ctx.From<PerPagePageParent>()
            .Where(p => p.Id >= minId)
            .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

    private static List<PerPagePageParent> LoadPagedByParams(SqliteDataContext ctx, int minId)
        => ctx.From<PerPagePageParent>()
            .Where(p => p.Id >= SqlFunctions.Parameter<int>(0))
            .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList(minId);

    [Fact]
    public void Split_TopLevelParams_ArgumentForm_And_ParamsForm_Agree()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Argument form: the parent predicate value is a compile-time argument baked into the query.
            var argumentForm = LoadPagedByArgument(ctx, 1);
            // Params form: the same value travels through a positional Parameter placeholder bound by the
            // terminal's params argument — same dataset, same per-parent paging.
            var paramsForm = LoadPagedByParams(ctx, 1);

            paramsForm.Select(p => p.Id).Should().Equal(argumentForm.Select(p => p.Id));
            for (var i = 0; i < argumentForm.Count; i++)
                paramsForm[i].Children.Select(c => c.Id).Should().Equal(argumentForm[i].Children.Select(c => c.Id));

            // Prepared QueryCommand surface parity: the same positional value drives the parent command.
            var commandParents = ctx.From<PerPagePageParent>()
                .Where(p => p.Id >= SqlFunctions.Parameter<int>(0))
                .OrderBy(p => p.Id)
                .ToCommand()
                .ToList(1);
            commandParents.Select(p => p.Id).Should().Equal(argumentForm.Select(p => p.Id));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_TopLevelParams_ChangedValue_RebindsAndReexecutes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Same shape re-executed with a churned runtime value must rebind each time, not retain the
            // first value or a stale plan bound to it; per-parent paging must survive the rebind.
            var all1 = LoadPagedByParams(ctx, 1);
            var only3 = LoadPagedByParams(ctx, 3);
            var all1Again = LoadPagedByParams(ctx, 1);

            all1.Select(p => p.Id).Should().Equal(1, 2, 3);
            only3.Select(p => p.Id).Should().Equal(3);
            only3[0].Children.Should().BeEmpty("parent 3 has no children");
            all1Again.Select(p => p.Id).Should().Equal(1, 2, 3);
            for (var i = 0; i < all1.Count; i++)
                all1Again[i].Children.Select(c => c.Id).Should().Equal(all1[i].Children.Select(c => c.Id));

            all1[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            all1[1].Children.Select(c => c.SortKey).Should().Equal(5, 50);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- #211 A01 / EV-R4-A01: WithTies + per-parent paging is rejected during preparation ----------

    [Fact]
    public void Split_WithTiesPagedChild_IsRejectedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var limitOnly = () => ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2).WithTies(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            limitOnly.Should().Throw<NotSupportedException>()
                .WithMessage("LoadWith cannot apply Limit/Offset/Page to a child query that also uses WithTies.");

            var withOffset = () => ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2).WithTies(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            withOffset.Should().Throw<NotSupportedException>()
                .WithMessage("*also uses WithTies.");

            interceptor.Statements.Should().BeEmpty("the WithTies guard must fire before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_WithTiesPagedChild_IsRejectedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(1).WithTies(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>()
                .WithMessage("LoadWith cannot apply Limit/Offset/Page to a child query that also uses WithTies.");
            interceptor.Statements.Should().BeEmpty("the shared guard must fire before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_WithTiesPagedChild_WarmCache_StillRejectedAndDoesNotContaminate()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // Positive control: the equivalent paging without WithTies executes and warms the plan cache.
            var supported = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            supported[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            var statementsAfterWarm = interceptor.Statements.Count;

            // A warm cache must not let the unsupported WithTies shape through.
            var act = () => ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2).WithTies(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            act.Should().Throw<NotSupportedException>()
                .WithMessage("*also uses WithTies.");
            interceptor.Statements.Count.Should().Be(statementsAfterWarm, "a rejected shape must not insert a plan or emit SQL");

            // No cache contamination: the supported paged shape still resolves to the same per-parent result.
            var supportedAgain = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            supportedAgain[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
    // --- #211 A08 / EV-R4-A08: child-factory resolution and per-context memoization ------------------

    [Fact]
    public void NonListTerminal_InvokesChildFactoryForValidationWithoutLoadingChildren()
    {
        var calls = 0;
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parent = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => { calls++; return c.From<PerPagePageChild>().OrderBy(x => x.SortKey); }, p => p.Id, c => c.ParentId)
                .Where(p => p.Id == 1)
                .FirstOrDefault();

            parent.Should().NotBeNull();
            calls.Should().Be(1, "the non-list terminal resolves the child factory during validation");
            parent!.Children.Should().BeEmpty("a non-list terminal never runs the split loader");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SplitListTerminal_ResolvesChildFactoryOncePerExecution()
    {
        var calls = 0;
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => { calls++; return c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2); }, p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);
            calls.Should().Be(1, "validation and execution must share one resolved child for the same (spec, context)");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SameBuilder_RepeatedTerminal_ReusesTheResolvedChild()
    {
        var calls = 0;
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var query = ctx.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => { calls++; return c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2); }, p => p.Id, c => c.ParentId);

            query.OrderBy(p => p.Id).ToList();
            calls.Should().Be(1);
            query.OrderBy(p => p.Id).ToList();
            calls.Should().Be(1, "the per-context memo must survive a repeated resolution of the same spec");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DifferentContext_BuildsItsOwnChild()
    {
        var calls = 0;
        var (ctx1, path1) = CreateDb();
        var (ctx2, path2) = CreateDb();
        try
        {
            ctx1.PurgeQueryCache();
            ctx2.PurgeQueryCache();

            ctx1.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => { calls++; return c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2); }, p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            calls.Should().Be(1);
            ctx2.From<PerPagePageParent>()
                .LoadWith(p => p.Children, c => { calls++; return c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2); }, p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            calls.Should().Be(2, "the per-context memo must not be inherited by another context");
        }
        finally
        {
            ctx1.Dispose();
            ctx2.Dispose();
            File.Delete(path1);
            File.Delete(path2);
        }
    }
}


/// <summary>
/// #211 EV-02 / REQ-01 — independent <c>EagerPerParentPagingReference-v1</c>. The expected per-parent
/// result is computed from the raw child rows (read back over a plain ADO connection, never through the
/// ORM/SQL/window paging path) with an ordinary in-process filter -> order -> Skip -> Take pipeline, and
/// compared against both eager execution modes. An adversarial case proves a naive global limit would
/// produce a different per-parent partition. The comparison triple (inputs / expected / actual) is
/// exported to <c>artifacts/pdca/D211-eager-perparent-topn-1/rv4/oracle/reference-v1.json</c>, which
/// <c>probe.py oracle</c> checks.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingOracleTests
{
    private sealed record RawChild(int Id, int ParentId, int SortKey, int GroupKey, int Flag);

    private static readonly int[] ParentIds = [1, 2, 3];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "nextorm.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-perparent-oracle-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table per_page_parent (id integer primary key, name text);" +
                "create table per_page_child (id integer primary key, parent_id int not null, sort_key int not null, group_key int not null, flag int not null);" +
                "insert into per_page_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'empty');" +
                "insert into per_page_child (id, parent_id, sort_key, group_key, flag) values" +
                " (11, 1, 40, 2, 1), (12, 1, 10, 1, 1), (13, 1, 30, 1, 1), (14, 1, 20, 2, 0)," +
                " (21, 2, 50, 1, 1), (22, 2, 5, 2, 1);";
            cmd.ExecuteNonQuery();
        }
        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    /// <summary>Reads the child table over a plain ADO connection — no ORM, no SQL builder, no window.</summary>
    private static List<RawChild> ReadRawChildren(string dbPath)
    {
        var rows = new List<RawChild>();
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "select id, parent_id, sort_key, group_key, flag from per_page_child order by id";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(new RawChild(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)));
        return rows;
    }

    /// <summary>Independent reference: filter -> order -> Skip -> Take, computed per parent in-process.</summary>
    private static Dictionary<int, List<int>> ReferencePage(
        IEnumerable<RawChild> raw, Func<RawChild, bool>? filter, int? skip, int? take)
    {
        var rows = raw.ToList();
        var result = new Dictionary<int, List<int>>();
        foreach (var pid in ParentIds)
        {
            IEnumerable<RawChild> q = rows.Where(c => c.ParentId == pid);
            if (filter is not null)
                q = q.Where(filter);
            q = q.OrderBy(c => c.SortKey).ThenBy(c => c.Id);
            if (skip is int s)
                q = q.Skip(s);
            if (take is int t)
                q = q.Take(t);
            result[pid] = q.Select(c => c.Id).ToList();
        }
        return result;
    }

    /// <summary>What a naive global (non-partitioned) limit would return.</summary>
    private static Dictionary<int, List<int>> GlobalLimit(
        IEnumerable<RawChild> raw, Func<RawChild, bool>? filter, int? skip, int? take)
    {
        var rows = raw.ToList();
        IEnumerable<RawChild> q = rows.Where(c => filter is null || filter(c));
        q = q.OrderBy(c => c.SortKey).ThenBy(c => c.Id);
        if (skip is int s)
            q = q.Skip(s);
        if (take is int t)
            q = q.Take(t);
        var ids = q.Select(c => c.Id).ToList();
        return ParentIds.ToDictionary(pid => pid, pid => ids.Where(id => rows.First(r => r.Id == id).ParentId == pid).ToList());
    }

    private static Dictionary<int, List<int>> Actual(IEnumerable<PerPagePageParent> parents)
        => parents.ToDictionary(p => p.Id, p => p.Children.Select(c => c.Id).ToList());

    [Fact]
    public void IndependentReference_MatchesBothModes_AndGlobalLimitDiffers()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var raw = ReadRawChildren(path);
            var scenarios = new List<object>();
            var allMatch = true;
            var adversarialDiffers = false;

            void Check(
                string name, int? filterFlag, int? skip, int? take,
                List<PerPagePageParent> split, List<PerPagePageParent> single)
            {
                Func<RawChild, bool>? filter = filterFlag is int f ? c => c.Flag == f : null;
                var expected = ReferencePage(raw, filter, skip, take);
                var splitActual = Actual(split);
                var singleActual = Actual(single);
                var global = GlobalLimit(raw, filter, skip, take);

                var splitMatch = true;
                var singleMatch = true;
                var differs = false;
                foreach (var pid in ParentIds)
                {
                    splitActual[pid].Should().Equal(expected[pid], $"split scenario {name} parent {pid}");
                    singleActual[pid].Should().Equal(expected[pid], $"single scenario {name} parent {pid}");
                    splitMatch &= splitActual[pid].SequenceEqual(expected[pid]);
                    singleMatch &= singleActual[pid].SequenceEqual(expected[pid]);
                    differs |= !global[pid].SequenceEqual(expected[pid]);
                }
                allMatch &= splitMatch && singleMatch;
                adversarialDiffers |= differs;

                scenarios.Add(new
                {
                    name,
                    filter_flag = filterFlag,
                    skip,
                    take,
                    expected,
                    split_actual = splitActual,
                    single_actual = singleActual,
                    split_match = splitMatch,
                    single_match = singleMatch,
                    adversarial_global = global,
                    adversarial_differs = differs,
                });
            }

            // A: Take only (no filter).
            Check("take2", null, null, 2,
                ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id).ToList(),
                ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                    .OrderBy(p => p.Id).ToList());

            // B: Skip + Take.
            Check("skip1_take1", null, 1, 1,
                ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(1), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id).ToList(),
                ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().OrderBy(x => x.SortKey).Offset(1).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                    .OrderBy(p => p.Id).ToList());

            // C: child filter precedes ranking.
            Check("filtered_take2", 1, null, 2,
                ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id).ToList(),
                ctx.From<PerPagePageParent>()
                    .LoadWith(p => p.Children, c => c.From<PerPagePageChild>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                    .OrderBy(p => p.Id).ToList());

            allMatch.Should().BeTrue("the independent reference must agree with both eager modes");
            adversarialDiffers.Should().BeTrue("a naive global limit must differ from per-parent paging on this fixture");

            var export = new Dictionary<string, object?>
            {
                ["reference"] = "EagerPerParentPagingReference-v1",
                ["generated_utc"] = DateTime.UtcNow.ToString("o"),
                ["generated_from"] = "tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs",
                ["fixture"] = "per_page_parent/per_page_child (SQLite)",
                ["raw_children"] = raw.Select(c => new { id = c.Id, parent_id = c.ParentId, sort_key = c.SortKey, group_key = c.GroupKey, flag = c.Flag }).ToList(),
                ["scenarios"] = scenarios,
                ["all_match"] = allMatch,
                ["adversarial_case_differs"] = adversarialDiffers,
            };
            var dir = Path.Combine(RepoRoot(), "artifacts", "pdca", "D211-eager-perparent-topn-1", "rv4", "oracle");
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "reference-v1.json"),
                JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

[SqlTable("per_page_parent")]
public sealed class PerPagePageParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<PerPagePageChild> Children { get; } = new List<PerPagePageChild>();

    public ICollection<PerPagePageNote> Notes { get; } = new List<PerPagePageNote>();
}

[SqlTable("per_page_child")]
public sealed class PerPagePageChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    [Column("group_key")]
    public int GroupKey { get; set; }

    [Column("flag")]
    public int Flag { get; set; }
}

[SqlTable("per_page_note")]
public sealed class PerPagePageNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("rank")]
    public int Rank { get; set; }
}

/// <summary>
/// Parent whose eager link key is the composite <c>(Tenant, LinkId)</c>, deliberately not the table key
/// (<see cref="RowId"/>). The variant-matrix :236 fixture: <c>(1,10)</c>, <c>(1,20)</c> and <c>(2,10)</c>
/// overlap on one component each, so a partition on a single component blends two parents.
/// </summary>
[SqlTable("per_page_composite_parent")]
public sealed class PerPageCompositeParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<PerPageCompositeChild> Children { get; } = new List<PerPageCompositeChild>();
}

/// <summary>Child keyed to its parent by the same composite <c>(Tenant, ParentId)</c> link key.</summary>
[SqlTable("per_page_composite_child")]
public sealed class PerPageCompositeChild
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
}
