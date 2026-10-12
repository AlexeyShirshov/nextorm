using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #211 per-parent top-N eager loading — edge and variant closure against a real SQLite database. Each
/// test names the frozen rv1 variant row it closes. Non-paged shapes and the pre-existing paging contract
/// are the oracle; the ranked per-parent source must not change either.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingEdgeTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText);
    }

    /// <summary>Cancels the token as soon as the ranked child statement is about to run.</summary>
    private sealed class CancelOnChildInterceptor : IQueryInterceptor
    {
        private readonly CancellationTokenSource _cts;

        public CancelOnChildInterceptor(CancellationTokenSource cts) => _cts = cts;

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
        {
            if (command.CommandText.Contains("row_number", StringComparison.OrdinalIgnoreCase))
                _cts.Cancel();
        }
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-perparent-edge-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table edge_parent (id integer primary key, name text);" +
                "create table edge_child (id integer primary key, parent_id int not null, sort_key int not null, flag int not null);" +
                "create table edge_note (id integer primary key, parent_id int not null, rank int not null);" +
                "create table edge_str_parent (id integer primary key, code text);" +
                "create table edge_str_child (id integer primary key, code text, sort_key int not null);" +
                "create table edge_empty_child (id integer primary key, parent_id int not null, sort_key int not null);" +
                "insert into edge_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'empty');" +
                "insert into edge_child (id, parent_id, sort_key, flag) values" +
                " (11, 1, 40, 1), (12, 1, 10, 1), (13, 1, 30, 1), (14, 1, 20, 0)," +
                " (21, 2, 50, 1), (22, 2, 5, 1);" +
                "insert into edge_note (id, parent_id, rank) values (101, 1, 2), (102, 1, 1), (103, 1, 3), (201, 2, 7);" +
                "insert into edge_str_parent (id, code) values (1, 'a'), (2, 'b'), (3, null);" +
                "insert into edge_str_child (id, code, sort_key) values" +
                " (10, 'a', 1), (11, 'a', 2), (12, 'b', 1), (13, null, 1), (14, null, 2), (15, 'a', 3);";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    // --- variant row :232 — zero/default bounds / negatives / extreme bounds -------------------------

    [Fact]
    public void Split_NegativeLimit_IsRejectedByPagingNormalization()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var act = () => ctx.From<EdgeChild>().Limit(-1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Split_NegativeOffset_IsRejectedByPagingNormalization()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var act = () => ctx.From<EdgeChild>().Offset(-1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Split_ExtremeBounds_DoNotOverflowThePerParentWindow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // int.MaxValue limit: every child survives per parent (the long upper bound must not wrap).
            var all = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(int.MaxValue), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            all[0].Children.Should().HaveCount(4);
            all[1].Children.Should().HaveCount(2);

            // int.MaxValue offset: skip past every child; parents stay, collections empty. Offset+limit
            // computed in long arithmetic keeps this from overflowing into a wrong (non-empty) window.
            var none = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Offset(int.MaxValue).Limit(int.MaxValue), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            none.Select(p => p.Id).Should().Equal(1, 2, 3);
            none.Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_SkipPastEnd_ReturnsEmptyChildrenAndKeepsParents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Offset(100), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_SkipPastEnd_ReturnsEmptyChildrenAndKeepsParents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Offset(100), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_EmptyChildTable_LeavesEveryParentEmpty()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The page never restricts parents to those with a matching child: an empty child source
            // yields empty collections for every parent.
            var parents = ctx.From<EdgeEmptyParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeEmptyChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_EmptyChildTable_LeavesEveryParentEmpty()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EdgeEmptyParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeEmptyChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- variant row :235 — no order / tied order --------------------------------------------------

    [Fact]
    public void Split_TiedOrder_PagedChild_ReturnsMembershipAndCountOnly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Ordering by flag ties three of parent 1's children; the exact surviving row is not promised.
            var parents = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.Flag).Limit(2), p => p.Id, c => c.ParentId)
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

    // --- variant row :242 — two independently paged navigations with more than one row each --------

    [Fact]
    public void SingleQuery_TwoPagedNavigations_WithMultipleRows_ArePagedIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Both navigations page two rows per parent. Each collection keeps its own partitioned page:
            // the page membership is decided inside the child's own partition, never by the sibling join.
            var parents = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .LoadWith(p => p.Notes, c => c.From<EdgeNote>().OrderBy(x => x.Rank).Limit(2), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([12, 14], "the two lowest sort keys of parent 1");
            parents[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([101, 102], "the two lowest ranks of parent 1");
            parents[1].Children.Select(c => c.Id).Should().BeEquivalentTo([21, 22]);
            parents[1].Notes.Select(n => n.Id).Should().BeEquivalentTo([201]);
            parents[2].Children.Should().BeEmpty();
            parents[2].Notes.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_TwoPagedNavigations_KeepEachNavigationsOwnOrder()
    {
        var (ctx, path) = CreateDb();
        var interceptor = new SqlRecordingInterceptor();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // W-2 probe: with >1 row per navigation the outer pair ORDER BY must still let each stitched
            // collection keep its own child order. Children ascending by SortKey, notes ascending by Rank.
            var parents = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(3), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .LoadWith(p => p.Notes, c => c.From<EdgeNote>().OrderBy(x => x.Rank).Limit(3), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.SortKey).Should().Equal(10, 20, 30);
            parents[0].Notes.Select(n => n.Rank).Should().Equal(1, 2, 3);

            // W-2 evidence: the single pair statement orders by the parent key, then every paged child's
            // own keys in join order, so each stitched collection enumerates in its own child order.
            var dir = "/home/alex/sources/nextorm/artifacts/pdca/D211-eager-perparent-topn-1/rv4/check";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "W-2-two-nav-sql.txt"), interceptor.Statements.Single());
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- variant row :237 — nullable/reference/default-value parent-link keys -----------------------

    [Fact]
    public void Split_NullableReferenceKey_DoesNotCrossLeakAcrossParents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The link key is the nullable string code. A null child code and a null parent code are both
            // skipped (documented null-key semantics): no child with a null code lands on any parent, and
            // the null-coded parent matches nothing.
            var parents = ctx.From<EdgeStrParent>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<EdgeStrChild>().OrderBy(x => x.SortKey).Limit(2),
                    p => p.Code!,
                    c => c.Code!)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([10, 11]);
            parents[1].Children.Select(c => c.Id).Should().BeEquivalentTo([12]);
            parents[2].Children.Should().BeEmpty("a null parent key matches no child");
            parents.SelectMany(p => p.Children).Select(c => c.Id).Should().NotContain([13, 14], "a null child key is never attached");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- variant row :234/:233 — empty source beyond the child table --------------------------------

    [Fact]
    public void Split_DefaultValueParentKey_MatchesItsOwnChildren()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "create table edge_zero_parent (id integer primary key, name text);" +
                    "create table edge_zero_child (id integer primary key, parent_id int not null, sort_key int not null);" +
                    "insert into edge_zero_parent (id, name) values (0, 'zero'), (1, 'one');" +
                    "insert into edge_zero_child (id, parent_id, sort_key) values (100, 0, 1), (101, 0, 2), (102, 1, 1);";
                cmd.ExecuteNonQuery();
            }

            // Key value 0 is a valid (default) key, not a sentinel: the zero-keyed parent gets its own page.
            var parents = ctx.From<EdgeZeroParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeZeroChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(0, 1);
            parents[0].Children.Select(c => c.Id).Should().Equal(100);
            parents[1].Children.Select(c => c.Id).Should().Equal(102);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- variant row :247 — sync/async cancellation and resource ownership -------------------------

    [Fact]
    public async Task Split_PreCancelledToken_ThrowsAndLeavesTheContextUsable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = async () => await ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToListAsync(cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();

            // The receiver owns the connection/context, so cancellation must not close or dispose it.
            ctx.From<EdgeParent>().Count().Should().Be(3);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Split_CancelledDuringChildRead_ThrowsAndLeavesTheContextUsable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            using var cts = new CancellationTokenSource();
            ctx.AddInterceptor(new CancelOnChildInterceptor(cts));

            var act = async () => await ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToListAsync(cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();

            ctx.From<EdgeParent>().Count().Should().Be(3, "cancellation must not close the caller-owned context");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- variant row :248 — cached/noncached preparation and same-context interleaving --------------

    [Fact]
    public void Split_PagedAndUnpagedShapes_OnOneContext_DoNotShareAPlan()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Plan-key shape separation: a paged and an unpaged load differ by the child's paging/partition
            // metadata, so they must not collide into one cached plan. Alternate them on one context and
            // assert each keeps its own result.
            for (var run = 0; run < 2; run++)
            {
                var paged = ctx.From<EdgeParent>()
                    .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(2), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id)
                    .ToList();
                paged[0].Children.Select(c => c.SortKey).Should().Equal(10, 20);

                var unpaged = ctx.From<EdgeParent>()
                    .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id)
                    .ToList();
                unpaged[0].Children.Should().HaveCount(4);

                // A different paging shape must not reuse the first paged plan either.
                var wider = ctx.From<EdgeParent>()
                    .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(3), p => p.Id, c => c.ParentId)
                    .OrderBy(p => p.Id)
                    .ToList();
                wider[0].Children.Select(c => c.SortKey).Should().Equal(10, 20, 30);
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_AnyAndCountInterleavedWithAPagedLoad_DoNotPoisonThePlanOrTheSharedCommand()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The shared Any/Count command must not be mutated by a paged eager preparation: interleave
            // the scalar terminals with a paged load on one context and assert both keep working.
            var root = ctx.From<EdgeParent>().Where(p => p.Id > 0);
            root.Any().Should().BeTrue();

            var paged = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            paged[0].Children.Select(c => c.Id).Should().Equal([12]);

            root.Count().Should().Be(3);

            // A second paged load still hits a valid plan (the first preparation must not disable caching).
            var again = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            again[0].Children.Select(c => c.Id).Should().Equal([12]);

            // The sticky flag is untouched: plain commands still cache true.
            ctx.From<EdgeParent>().Select(p => p.Id).Cache.Should().BeTrue();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_NonCachedLocalPreparation_DoesNotInsertAPlanAndLeavesCachingIntact()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // storeInCache:false must prepare a paged child command locally without inserting a plan.
            var builder = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId);
            _ = builder.LoadSpecs![0].ToJoinIntoSpec(ctx, QueryFilterScope.None, out var childSource);
            var child = childSource.SubQuery!;
            string sql = ((dynamic)ctx.GetPreparedQueryCommand((dynamic)child, false, false, CancellationToken.None)).DbCommand.CommandText;
            sql.Should().Contain("row_number()");

            ctx.From<EdgeParent>().Select(p => p.Id).Cache.Should().BeTrue("a local non-cached preparation must not flip the sticky cache flag");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- variant row :238 — entity (reference) projection, rank column never escapes ----------------

    [Fact]
    public void Split_PagedChild_MaterializesTheEntityAndDropsTheRankColumn()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var builder = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2), p => p.Id, c => c.ParentId);
            _ = builder.LoadSpecs![0].ToJoinIntoSpec(ctx, QueryFilterScope.None, out var childSource);
            var child = childSource.SubQuery!;
            string sql = ((dynamic)ctx.GetPreparedQueryCommand((dynamic)child, false, false, CancellationToken.None)).DbCommand.CommandText;
            var lowered = sql.ToLowerInvariant();

            // The outer projection is the full child entity, and the synthetic rank column is not selected.
            lowered.Should().Contain("row_number()");
            lowered.Should().Contain("from (select *");
            var outerProjection = lowered.Split("from (select")[0];
            outerProjection.Should().NotContain("__nextorm_rn");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- security: an untrusted string constant in the child ORDER BY must not alter the SQL ---------

    [Fact]
    public void Split_CapturedRuntimeStringSortKey_IsParameterizedNotInlined()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Security regression: an apostrophe-bearing string that reaches the child sort through a
            // captured runtime value must be bound as a parameter, never inlined as a raw literal that
            // would close the SQL string and alter the statement.
            var marker = "cap'--";

            // Probe A: the eager per-parent ranked child source (the T211 rendering path).
            var captured = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => marker).Limit(1), p => p.Id, c => c.ParentId);
            _ = captured.LoadSpecs![0].ToJoinIntoSpec(ctx, QueryFilterScope.None, out var capturedSource);
            string capturedSql = ((dynamic)ctx.GetPreparedQueryCommand((dynamic)capturedSource.SubQuery!, false, false, CancellationToken.None)).DbCommand.CommandText;

            // Probe B: the ordinary (non-eager) rendering path, to classify whether an unescaped inline is
            // specific to the per-parent path or the shared renderer.
            var plain = ctx.From<EdgeChild>().OrderBy(x => marker).Select(x => x.Id);
            string plainSql = ((dynamic)ctx.GetPreparedQueryCommand((dynamic)plain, false, false, CancellationToken.None)).DbCommand.CommandText;

            // Probe C: a compile-time literal in the sort expression (trusted code, not runtime input).
            var literal = ctx.From<EdgeParent>()
                .LoadWith(p => p.Children, c => c.From<EdgeChild>().OrderBy(x => "o'ops--").Limit(1), p => p.Id, c => c.ParentId);
            _ = literal.LoadSpecs![0].ToJoinIntoSpec(ctx, QueryFilterScope.None, out var literalSource);
            string literalSql = ((dynamic)ctx.GetPreparedQueryCommand((dynamic)literalSource.SubQuery!, false, false, CancellationToken.None)).DbCommand.CommandText;

            var dir = "/home/alex/sources/nextorm/artifacts/pdca/D211-eager-perparent-topn-1/rv4/check";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "d211-security-captured-sql.txt"), capturedSql);
            File.WriteAllText(Path.Combine(dir, "d211-security-plain-sql.txt"), plainSql);
            File.WriteAllText(Path.Combine(dir, "d211-security-literal-sql.txt"), literalSql);

            // The untrusted runtime value is safely parameterized on both paths.
            capturedSql.Should().NotContain("'cap'--'", $"captured sort key leaked raw: {capturedSql}");
            capturedSql.Should().Contain("marker", "the captured value must render as a bound parameter");
            plainSql.Should().NotContain("'cap'--'", $"captured sort key leaked raw on the plain path: {plainSql}");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

[SqlTable("edge_parent")]
public sealed class EdgeParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<EdgeChild> Children { get; } = new List<EdgeChild>();

    public ICollection<EdgeNote> Notes { get; } = new List<EdgeNote>();
}

[SqlTable("edge_child")]
public sealed class EdgeChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    [Column("flag")]
    public int Flag { get; set; }
}

[SqlTable("edge_note")]
public sealed class EdgeNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("rank")]
    public int Rank { get; set; }
}

[SqlTable("edge_str_parent")]
public sealed class EdgeStrParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    public ICollection<EdgeStrChild> Children { get; } = new List<EdgeStrChild>();
}

[SqlTable("edge_str_child")]
public sealed class EdgeStrChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}

[SqlTable("edge_empty_child")]
public sealed class EdgeEmptyChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}

[SqlTable("edge_parent")]
public sealed class EdgeEmptyParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<EdgeEmptyChild> Children { get; } = new List<EdgeEmptyChild>();
}

[SqlTable("edge_zero_parent")]
public sealed class EdgeZeroParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<EdgeZeroChild> Children { get; } = new List<EdgeZeroChild>();
}

[SqlTable("edge_zero_child")]
public sealed class EdgeZeroChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}
