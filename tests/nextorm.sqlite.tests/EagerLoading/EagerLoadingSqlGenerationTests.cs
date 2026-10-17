using System.Data.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Split-query eager loading (<c>LoadWith</c>, #95) against a real SQLite database: the child statement
/// is a separate <c>WHERE ... IN (...)</c> command, the two statements are issued back to back (two round
/// trips for at most one key chunk) and the results are stitched onto the parents.
/// </summary>
public class EagerLoadingSqlGenerationTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Statements.Add(command.CommandText);
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-eager-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table eager_parent (id integer primary key, name text);" +
                "create table eager_child (id integer primary key, parent_id int not null, name text);" +
                "insert into eager_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'zero');" +
                "insert into eager_child (id, parent_id, name) values (10, 1, 'x'), (11, 1, 'y'), (12, 2, 'z');";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void LoadWith_ChildStatement_UsesInListInASeparateRoundTrip()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .Where(p => p.Id == 1 || p.Id == 2 || p.Id == 3)
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[2].Children.Should().BeEmpty();

            // One parent statement plus one child statement: two round trips, no N+1.
            interceptor.Statements.Should().HaveCount(2);

            var parentSql = interceptor.Statements[0].ToLowerInvariant();
            var childSql = interceptor.Statements[1].ToLowerInvariant();

            parentSql.Should().Contain("eager_parent").And.NotContain("join").And.NotContain(" in (");
            childSql.Should().Contain("eager_child").And.Contain(" in (").And.NotContain("join");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_IssuesExactlyOneCommandWithTheChildFilterInTheJoin()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().Where(x => x.Name != "y"), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            // The child's own Where must be merged into the ON predicate (control: dropping it would
            // bring child 11 back).
            parents[0].Children.Select(c => c.Id).Should().Equal(10);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[2].Children.Should().BeEmpty();

            // One denormalized command: a single round trip, no chunked IN list, no fallback to split.
            interceptor.Statements.Should().HaveCount(1);
            var sql = interceptor.Statements[0].ToLowerInvariant();
            sql.Should().Contain("join").And.Contain("eager_child").And.NotContain(" in (");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
            count++;

        return count;
    }

    /// <summary>
    /// Creates a fresh SQLite database with the <c>eager_parent</c>/<c>eager_child</c> schema and
    /// <paramref name="count"/> parents, each with exactly one child, inserted in small batches.
    /// </summary>
    private static (SqliteDataContext Ctx, string Path) CreateBulkDb(int count)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-eager-bulk-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table eager_parent (id integer primary key, name text);" +
                "create table eager_child (id integer primary key, parent_id int not null, name text);";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        const int batchSize = 250;
        for (var offset = 1; offset <= count; offset += batchSize)
        {
            var parents = new List<EagerParent>();
            var children = new List<EagerChild>();
            for (var id = offset; id < Math.Min(offset + batchSize, count + 1); id++)
            {
                parents.Add(new EagerParent { Id = id, Name = "p" + id });
                children.Add(new EagerChild { Id = id * 10, ParentId = id, Name = "c" + id });
            }

            ctx.CreateInsertBuilder<EagerParent>().Values(parents).Insert();
            ctx.CreateInsertBuilder<EagerChild>().Values(children).Insert();
        }

        return (ctx, path);
    }

    [Fact]
    public void Split_1001Keys_ChunksIntoParentPlusTwoChildCommands()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateBulkDb(1001);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Should().HaveCount(1001);
            parents.Should().OnlyContain(p => p.Children.Count == 1);

            // One parent statement plus two child statements: the 1001 distinct keys split into chunks of
            // 1000 + 1. Never a single 1001-parameter IN list, and never N+1.
            interceptor.Statements.Should().HaveCount(3);
            var childStatements = interceptor.Statements.Where(s => s.Contains(" in (", StringComparison.OrdinalIgnoreCase)).ToArray();
            childStatements.Should().HaveCount(2);
            foreach (var childSql in childStatements)
                CountOccurrences(childSql, "$p").Should().BeLessThanOrEqualTo(1000, "the chunked IN list must stay within the 1000-key chunk size");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_1001Keys_IssuesExactlyOneCommandWithoutAChunkedInList()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateBulkDb(1001);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Should().HaveCount(1001);
            parents.Should().OnlyContain(p => p.Children.Count == 1);

            // Exactly one denormalized command even beyond the 1000-key chunk boundary: no chunked IN
            // list and no silent fallback to split.
            interceptor.Statements.Should().HaveCount(1);
            interceptor.Statements[0].Should().Contain(" join ").And.NotContain(" in (");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_TwoSpecs_IssueOneCommandJoiningBothChildren()
    {
        var interceptor = new SqlRecordingInterceptor();
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-eager-two-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var seed = conn.CreateCommand();
            seed.CommandText =
                "create table eager_two_parent (id integer primary key, name text);" +
                "create table eager_two_child (id integer primary key, parent_id int not null);" +
                "create table eager_two_note (id integer primary key, parent_id int not null);" +
                "insert into eager_two_parent (id, name) values (1, 'a'), (2, 'b');" +
                "insert into eager_two_child (id, parent_id) values (10, 1), (11, 2);" +
                "insert into eager_two_note (id, parent_id) values (100, 1);";
            seed.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerTwoParent>()
                .LoadWith(p => p.Children, c => c.From<EagerTwoChild>(), p => p.Id, c => c.ParentId)
                .LoadWith(p => p.Notes, c => c.From<EagerTwoNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Should().HaveCount(2);
            parents[0].Children.Select(c => c.Id).Should().Equal(10);
            parents[0].Notes.Select(n => n.Id).Should().Equal(100);
            parents[1].Children.Select(c => c.Id).Should().Equal(11);
            parents[1].Notes.Should().BeEmpty();

            interceptor.Statements.Should().HaveCount(1, "both declared collections stitch from one denormalized command");
            var sql = interceptor.Statements[0].ToLowerInvariant();
            sql.Should().Contain("join").And.Contain("eager_two_child").And.Contain("eager_two_note");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- D209.5: context-default precedence (V02/V03), explicit/Auto dispatch (V10), zero-SQL rejection
    // --- (V05/V09/V10) and the fixed-Single JoinInto under every context default (V11).

    private static (SqliteDataContext Ctx, string Path) CreateDbWithEagerMode(EagerLoadMode contextMode)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-eager-mode-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table eager_parent (id integer primary key, name text);" +
                "create table eager_child (id integer primary key, parent_id int not null, name text);" +
                "insert into eager_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'zero');" +
                "insert into eager_child (id, parent_id, name) values (10, 1, 'x'), (11, 1, 'y'), (12, 2, 'z');";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder().UseEagerLoadMode(contextMode)), path);
    }

    [Fact]
    public void ContextDefaultSingleQuery_MakesAnUnqualifiedLoadWithUseOneJoinCommand()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SingleQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            interceptor.Statements.Should().HaveCount(1, "the context default SingleQuery dispatches the load to the single-query path");
            interceptor.Statements[0].ToLowerInvariant().Should().Contain(" join ");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextDefaultSplitQuery_ExplicitSingleQueryOverrideWins()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            interceptor.Statements.Should().HaveCount(1, "the explicit SingleQuery override wins over the SplitQuery context default");
            interceptor.Statements[0].ToLowerInvariant().Should().Contain(" join ");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextDefaultSingleQuery_ExplicitSplitOverrideWins_AndLaterQueryStillInheritsTheDefault()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SingleQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var explicitSplit = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();
            explicitSplit[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            interceptor.Statements.Should().HaveCount(2, "the explicit SplitQuery override wins over the SingleQuery context default");
            interceptor.Statements.Should().OnlyContain(s => !s.Contains(" join ", StringComparison.OrdinalIgnoreCase));

            // A later query that declares no mode must still inherit the unchanged context default.
            interceptor.Statements.Clear();
            var inherited = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            inherited[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            interceptor.Statements.Should().HaveCount(1, "one query's explicit override must not change the context default for later queries");
            interceptor.Statements[0].ToLowerInvariant().Should().Contain(" join ");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextDefaultAuto_DispatchesToTheSplitPath()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.Auto);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            interceptor.Statements.Should().HaveCount(2, "the conservative Auto rule selects the split path");
            interceptor.Statements.Should().OnlyContain(s => !s.Contains(" join ", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExplicitCorrelated_IsRejectedWithZeroSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Correlated)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");
            interceptor.Statements.Should().BeEmpty("an unsupported strategy must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextDefaultCorrelated_UnqualifiedLoadWith_IsRejectedWithZeroSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.Correlated);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");
            interceptor.Statements.Should().BeEmpty("the Correlated context default must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_PerParentTopN_IsAppliedPerParentWithPartitionedRanking()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // #211: the child Limit/Offset/Page is no longer a blanket rejection on the split path. It is
            // applied independently to each parent through a ranked child source, so a globally limited
            // child query can no longer starve later parents.
            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal([10], "each parent gets its own lowest child");
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[2].Children.Should().BeEmpty("a childless parent is not dropped by the ranked child source");

            // One parent statement plus one batched child statement: still two round trips, no N+1.
            interceptor.Statements.Should().HaveCount(2);
            interceptor.Statements[0].ToLowerInvariant().Should().Contain("eager_parent").And.NotContain("join");
            var childSql = interceptor.Statements[1].ToLowerInvariant();
            childSql.Should().Contain("eager_child").And.Contain("row_number()").And.Contain("over (").And.Contain("partition by");
            childSql.Should().Contain("__nextorm_rn").And.Contain("<= ", "the per-partition page bound is applied inside the ranked child source");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextAuto_PerParentTopN_IsAppliedPerParentViaSplit()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.Auto);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // The conservative Auto rule dispatches ordinary loads to Split; a per-parent top-N is honored
            // there (ranked child source) instead of being rejected or silently reinterpreted as a global
            // child limit.
            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents[0].Children.Select(c => c.Id).Should().Equal(10);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[2].Children.Should().BeEmpty();
            interceptor.Statements.Should().HaveCount(2, "Auto still dispatches the paged load to the split path");
            interceptor.Statements.Should().OnlyContain(s => !s.Contains(" join ", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextCorrelated_PerParentTopN_IsRejectedBeforeSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.Correlated);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");
            interceptor.Statements.Should().BeEmpty("the Correlated context default must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Split_PerParentTopN_Async_IsAppliedPerParent()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // The async split path applies the child page per parent through the same ranked child
            // source, batched into one child statement instead of rejecting the per-parent top-N.
            var parents = await ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            parents[0].Children.Select(c => c.Id).Should().Equal(10);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[2].Children.Should().BeEmpty();
            interceptor.Statements.Should().HaveCount(2, "the async split path applies the per-parent page in one batched child statement");
            interceptor.Statements[1].ToLowerInvariant().Should().Contain("row_number()").And.Contain("partition by");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void PerParentTopN_PopulatesThePlanCacheAndLaterQueriesStillCache()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // A supported per-parent paged preparation is cached; it no longer relies on a rejection.
            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            parents[0].Children.Select(c => c.Id).Should().Equal([10]);

            var planCount = QueryPlanStore.Values.Count();
            planCount.Should().BeGreaterThan(0, "a supported per-parent paged preparation must insert a plan-cache entry");

            // The identical paged shape re-emits identical SQL: the ranked bounds are rebound, not
            // accumulated or leaked across preparations.
            var firstRun = interceptor.Statements.ToArray();
            interceptor.Statements.Clear();
            _ = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            interceptor.Statements.Should().Equal(firstRun, "the identical paged shape must rebind to the same deterministic SQL");

            // The sticky cache flag is not mutated and the plan cache still serves ordinary queries.
            var command = ctx.From<EagerParent>().Select(p => p.Id);
            command.Cache.Should().BeTrue("a supported eager preparation must not poison the sticky cache flag");
            var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
            var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
            second.Should().BeSameAs(first, "subsequent normal queries must still reuse the plan cache");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_PerParentTopN_IsAppliedPerParentWithOneSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // #211: the child's own Limit is honored per parent inside the single-query join (a ranked
            // derived source partitioned by the child key), instead of the previously rejected blanket.
            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal(10);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[2].Children.Should().BeEmpty();

            // One denormalized command: the ranked per-parent paging stays a single round trip.
            interceptor.Statements.Should().HaveCount(1, "single-query per-parent paging stays one command");
            var sql = interceptor.Statements[0].ToLowerInvariant();
            sql.Should().Contain("row_number()").And.Contain("partition by").And.Contain("join");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void SingleQuery_PerParentTopN_OrdersOuterPairByReboundChildKeyWithLegalNestedSource()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            _ = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            interceptor.Statements.Should().HaveCount(1);
            var sql = interceptor.Statements[0].ToLowerInvariant();

            // The ranking window keeps the child's own ordering.
            sql.Should().Contain("over (partition by parent_id order by id)");

            // The nested ranked derived source carries no statement-level ORDER BY (SQL Server rejects one
            // in a derived table); the order moves to the outer pair command, which rebinds the child key
            // to the join alias after the parent key.
            var lastRankReference = sql.LastIndexOf("__nextorm_rn", StringComparison.Ordinal);
            lastRankReference.Should().BeGreaterThanOrEqualTo(0);
            var derivedClose = sql.IndexOf(") as '", lastRankReference, StringComparison.Ordinal);
            derivedClose.Should().BeGreaterThan(lastRankReference);
            sql[lastRankReference..derivedClose].Should().NotContain("order by");
            sql.Should().Contain("order by t1.id, t3.id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_UnsupportedChildShape_IsRejectedWithZeroSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*");
            interceptor.Statements.Should().BeEmpty("an unsupported single-query child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Distinct")]
    [InlineData("GroupBy")]
    [InlineData("Having")]
    public void SingleQuery_PerParentTopN_UnsupportedChildShape_IsRejectedWithZeroSql(string shape)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // Retain the fail-closed guard for the child shapes the ranked per-parent paging source cannot
            // fold in (aggregate/grouped/distinct): ordering and paging are consumed by the window, but
            // every other unsupported shape is still rejected before any SQL is generated.
            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => PagedUnsupportedChild(c, shape), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage($"*{shape}*");
            interceptor.Statements.Should().BeEmpty("an unsupported paged child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Distinct")]
    [InlineData("GroupBy")]
    [InlineData("Having")]
    public void Split_PerParentTopN_UnsupportedChildShape_IsRejectedWithZeroSql(string shape)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // The split loader lowers a paged child to the same ranked derived source the single-query
            // path uses. It must apply the same fail-closed shape guard: a shape the ranked source cannot
            // fold in (Distinct/GroupBy/Having) is rejected before any SQL instead of being silently
            // dropped into wrong rows.
            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => PagedUnsupportedChild(c, shape), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage($"*{shape}*");
            interceptor.Statements.Should().BeEmpty("an unsupported paged child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_PerParentTopN_NoOrder_IsRejectedWithZeroSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDbWithEagerMode(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            // A paged child without an ordering cannot lower to a valid window (SQL Server rejects a
            // row_number() with no ORDER BY). It is rejected at preparation, before the parent statement.
            var act = () => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().Limit(1), p => p.Id, c => c.ParentId)
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

    private static EntityBuilder<EagerChild> PagedUnsupportedChild(IDataContext ctx, string shape) => shape switch
    {
        "Distinct" => ctx.From<EagerChild>().Distinct().OrderBy(x => x.Id).Limit(1),
        "GroupBy" => ctx.From<EagerChild>().GroupBy(x => x.ParentId).OrderBy(x => x.Id).Limit(1),
        "Having" => ctx.From<EagerChild>().GroupBy(x => x.ParentId).Having(x => x.ParentId > 0).OrderBy(x => x.Id).Limit(1),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown child shape"),
    };

    [Fact]
    public void ContextBuilder_RejectsDefaultAndUndefinedEagerModes()
    {
        Action @default = () => new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.Default);
        @default.Should().Throw<ArgumentOutOfRangeException>();

        Action undefined = () => new DataContextBuilder().UseEagerLoadMode((EagerLoadMode)999);
        undefined.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(EagerLoadMode.SplitQuery)]
    [InlineData(EagerLoadMode.SingleQuery)]
    [InlineData(EagerLoadMode.Auto)]
    [InlineData(EagerLoadMode.Correlated)]
    public void JoinInto_IsFixedSingleUnderEveryContextDefault(EagerLoadMode contextMode)
    {
        var interceptor = new SqlRecordingInterceptor();
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-joininto-mode-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var seed = conn.CreateCommand();
            seed.CommandText =
                "create table strategy_parent (id integer primary key, name text);" +
                "create table strategy_child (id integer primary key, parent_id int not null, name text);" +
                "insert into strategy_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3');" +
                "insert into strategy_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');";
            seed.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().UseEagerLoadMode(contextMode));
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<StrategyParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
                .JoinInto(ctx.From<StrategyChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            parents[2].Children.Should().BeEmpty();
            interceptor.Statements.Should().HaveCount(1, "JoinInto stays the fixed Single operation regardless of the context default");
            interceptor.Statements[0].ToLowerInvariant().Should().Contain(" join ");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- D209 r=1 n=2 (F14): JoinInto + LoadWith(Default) keeps the baseline pair+split dispatch under
    // --- every context default; only an explicit SingleQuery merges the JoinInto and the load.

    private static (SqliteDataContext Ctx, string Path) CreateMixedDb(EagerLoadMode contextMode)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-mixed-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var seed = conn.CreateCommand();
            seed.CommandText =
                "create table mixed_parent (id integer primary key, name text);" +
                "create table mixed_child (id integer primary key, parent_id int not null, name text);" +
                "create table mixed_note (id integer primary key, parent_id int not null, name text);" +
                "insert into mixed_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3');" +
                "insert into mixed_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');" +
                "insert into mixed_note (id, parent_id, name) values (100, 1, 'n1'), (101, 2, 'n2');";
            seed.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder().UseEagerLoadMode(contextMode)), path);
    }

    [Theory]
    [InlineData(EagerLoadMode.SplitQuery)]
    [InlineData(EagerLoadMode.SingleQuery)]
    [InlineData(EagerLoadMode.Auto)]
    [InlineData(EagerLoadMode.Correlated)]
    public void JoinInto_WithLoadWithDefault_KeepsTheBaselinePairAndSplitDispatch(EagerLoadMode contextMode)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(contextMode);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<MixedParent>(b => b.HasMany(p => p.Children, c => c.ParentId).HasMany(p => p.Notes, n => n.ParentId))
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => c.From<MixedNote>(), p => p.Id, n => n.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            parents[0].Notes.Select(n => n.Id).Should().Equal(100);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[1].Notes.Select(n => n.Id).Should().Equal(101);
            parents[2].Children.Should().BeEmpty("a childless parent survives the Left Join");

            // Baseline mixed dispatch under every context default: the JoinInto pair command (one join
            // statement) plus one split child statement for Notes. The context default must not reroute the
            // combination into the merged LoadWith path and must not throw for Correlated.
            interceptor.Statements.Should().HaveCount(2,
                "the JoinInto pair command and the split LoadWith child must both run under every context default");
            interceptor.Statements.Should().ContainSingle(s => s.Contains(" join ", StringComparison.OrdinalIgnoreCase));
            interceptor.Statements.Should().ContainSingle(s => s.Contains(" in (", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void JoinInto_WithExplicitSingleQueryLoadWith_IssuesOneMergedCommand()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<MixedParent>(b => b.HasMany(p => p.Children, c => c.ParentId).HasMany(p => p.Notes, n => n.ParentId))
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => c.From<MixedNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
            parents[0].Notes.Select(n => n.Id).Should().Equal(100);
            parents[1].Children.Select(c => c.Id).Should().Equal(12);
            parents[1].Notes.Select(n => n.Id).Should().Equal(101);

            interceptor.Statements.Should().HaveCount(1,
                "an explicit SingleQuery LoadWith merges the JoinInto and the load into one denormalized command");
            var sql = interceptor.Statements[0].ToLowerInvariant();
            sql.Should().Contain(" join ").And.Contain("mixed_child").And.Contain("mixed_note");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- D209 r=1 n=2 (escalation): the JoinInto entry guard validates the LoadWith set before any SQL,
    // --- so a per-parent top-N (Limit/Offset/Page, empty or non-empty parent) or an explicit Correlated
    // --- fails closed with zero statements and no plan-cache growth, instead of running the pair SQL and
    // --- failing in the child loader (or being silently reinterpreted).

    private static EntityBuilder<MixedNote> PagedNotes(EntityBuilder<MixedNote> child, string paging) => paging switch
    {
        "Limit" => child.Limit(1),
        "Offset" => child.Offset(1),
        "Page" => child.Page(1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(paging), paging, "unknown paging kind"),
    };

    private static EntityBuilder<MixedParent> MixedRoot(SqliteDataContext ctx)
        => ctx.From<MixedParent>(b => b.HasMany(p => p.Children, c => c.ParentId).HasMany(p => p.Notes, n => n.ParentId));

    [Theory]
    [InlineData("Limit", false)]
    [InlineData("Offset", false)]
    [InlineData("Page", false)]
    [InlineData("Limit", true)]
    [InlineData("Offset", true)]
    [InlineData("Page", true)]
    public void JoinInto_WithPagedLoadWith_IsRejectedBeforeAnySql(string paging, bool emptyParent)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            QueryPlanStore.Values.Should().BeEmpty("the purge must leave a clean plan cache for the no-entry assertion");

            var root = MixedRoot(ctx);
            var parents = emptyParent ? root.Where(p => p.Id == -1) : root;

            var act = () => parents
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => PagedNotes(c.From<MixedNote>(), paging), p => p.Id, n => n.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
            interceptor.Statements.Should().BeEmpty(
                "the JoinInto entry guard must reject the per-parent top-N before the pair statement runs");
            QueryPlanStore.Values.Should().BeEmpty("a rejected JoinInto preparation must not insert a plan-cache entry");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Limit", false)]
    [InlineData("Offset", false)]
    [InlineData("Page", false)]
    [InlineData("Limit", true)]
    [InlineData("Offset", true)]
    [InlineData("Page", true)]
    public async Task JoinInto_WithPagedLoadWith_Async_IsRejectedBeforeAnySql(string paging, bool emptyParent)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            QueryPlanStore.Values.Should().BeEmpty("the purge must leave a clean plan cache for the no-entry assertion");

            var root = MixedRoot(ctx);
            var parents = emptyParent ? root.Where(p => p.Id == -1) : root;

            var act = () => parents
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => PagedNotes(c.From<MixedNote>(), paging), p => p.Id, n => n.ParentId)
                .OrderBy(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
            interceptor.Statements.Should().BeEmpty(
                "the async JoinInto entry guard must reject the per-parent top-N before the first SQL await");
            QueryPlanStore.Values.Should().BeEmpty("a rejected JoinInto preparation must not insert a plan-cache entry");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void JoinInto_WithExplicitCorrelatedLoadWith_IsRejectedBeforeAnySql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            QueryPlanStore.Values.Should().BeEmpty("the purge must leave a clean plan cache for the no-entry assertion");

            var act = () => MixedRoot(ctx)
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => c.From<MixedNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Correlated)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");
            interceptor.Statements.Should().BeEmpty("an explicit Correlated LoadWith must fail before any SQL");
            QueryPlanStore.Values.Should().BeEmpty("a rejected JoinInto preparation must not insert a plan-cache entry");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task JoinInto_WithExplicitCorrelatedLoadWith_Async_IsRejectedBeforeAnySql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(EagerLoadMode.SplitQuery);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            QueryPlanStore.Values.Should().BeEmpty("the purge must leave a clean plan cache for the no-entry assertion");

            var act = () => MixedRoot(ctx)
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => c.From<MixedNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Correlated)
                .OrderBy(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*Correlated*");
            interceptor.Statements.Should().BeEmpty("an explicit Correlated LoadWith must fail before the first SQL await");
            QueryPlanStore.Values.Should().BeEmpty("a rejected JoinInto preparation must not insert a plan-cache entry");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void JoinInto_WithDefaultLoadWith_UnderContextCorrelated_KeepsPairAndSplit()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateMixedDb(EagerLoadMode.Correlated);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = MixedRoot(ctx)
                .JoinInto(ctx.From<MixedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .LoadWith(p => p.Notes, c => c.From<MixedNote>(), p => p.Id, n => n.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            interceptor.Statements.Should().HaveCount(2,
                "an inherited Correlated context default must not reject the JoinInto + Default combination");
            interceptor.Statements.Should().ContainSingle(s => s.Contains(" join ", StringComparison.OrdinalIgnoreCase));
            interceptor.Statements.Should().ContainSingle(s => s.Contains(" in (", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- D14a: split vs single eager-load filter-scope inheritance. The alias types below map to the
    // --- same eager_parent/eager_child tables but declare a named filter, so no other test's entity
    // --- metadata is affected (the first registration per type wins process-wide).

    private static void RegisterFilteredEagerFilters(SqliteDataContext ctx)
    {
        ctx.From<FilteredSqlEagerParent>(b => b.HasQueryFilter("soft", (p, _) => p.Name != "zero"));
        ctx.From<FilteredSqlEagerChild>(b => b.HasQueryFilter("soft", (c, _) => c.Name != "y"));
    }

    private static string Signature(IReadOnlyList<FilteredSqlEagerParent> parents)
        => string.Join(";", parents.Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c => c.Id).OrderBy(id => id))));

    [Fact]
    public void Split_vs_Single_FilterScopeMatrix_ShouldProduceSameRows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            RegisterFilteredEagerFilters(ctx);

            string Run(bool single, bool parentIgnore, bool childIgnore)
            {
                var builder = ctx.From<FilteredSqlEagerParent>()
                    .LoadWith(
                        p => p.Children,
                        c => childIgnore ? c.From<FilteredSqlEagerChild>().IgnoreFilters(["soft"]) : c.From<FilteredSqlEagerChild>(),
                        p => p.Id,
                        c => c.ParentId,
                        single ? EagerLoadMode.SingleQuery : EagerLoadMode.SplitQuery);
                if (parentIgnore)
                    builder = builder.IgnoreFilters(["soft"]);

                var parents = builder.OrderBy(p => p.Id).ToList();
                return Signature(parents);
            }

            // Explicit expectations first (so the equivalence below is not vacuous), then split == single.
            Run(single: false, parentIgnore: false, childIgnore: false).Should().Be("1:10;2:12");
            Run(single: false, parentIgnore: true, childIgnore: false).Should().Be("1:10|11;2:12;3:", "the parent's key-selective scope also names the child's soft filter (union)");
            Run(single: false, parentIgnore: false, childIgnore: true).Should().Be("1:10|11;2:12", "the child's own ignore is local to the child side");
            Run(single: false, parentIgnore: true, childIgnore: true).Should().Be("1:10|11;2:12;3:");

            for (var parentIgnore = 0; parentIgnore < 2; parentIgnore++)
            {
                for (var childIgnore = 0; childIgnore < 2; childIgnore++)
                {
                    var split = Run(false, parentIgnore == 1, childIgnore == 1);
                    var single = Run(true, parentIgnore == 1, childIgnore == 1);
                    single.Should().Be(split, "the split and single paths must apply the same parent/child scope union");
                }
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_ParentIgnoreFilters_ChildStatementDropsTheChildFilter()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            RegisterFilteredEagerFilters(ctx);

            var parents = ctx.From<FilteredSqlEagerParent>()
                .LoadWith(p => p.Children, c => c.From<FilteredSqlEagerChild>(), p => p.Id, c => c.ParentId)
                .IgnoreFilters()
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);

            var childSql = interceptor.Statements.Single(s => s.Contains(" in (", StringComparison.OrdinalIgnoreCase)).ToLowerInvariant();
            childSql.Should().Contain("eager_child").And.NotContain("'y'", "the parent IgnoreFilters() must drop the child's soft filter too");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_ChildIgnoreFilters_KeepsTheParentFilterInTheParentStatement()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            RegisterFilteredEagerFilters(ctx);

            var parents = ctx.From<FilteredSqlEagerParent>()
                .LoadWith(p => p.Children, c => c.From<FilteredSqlEagerChild>().IgnoreFilters(["soft"]), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(new[] { 1, 2 }, "the parent's own soft filter still applies");
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(new[] { 10, 11 }, "the child's soft filter is disabled on the child side only");

            var parentSql = interceptor.Statements[0].ToLowerInvariant();
            parentSql.Should().Contain("eager_parent").And.Contain("'zero'", "the parent filter is untouched");
            var childSql = interceptor.Statements.Single(s => s.Contains(" in (", StringComparison.OrdinalIgnoreCase)).ToLowerInvariant();
            childSql.Should().NotContain("'y'", "the child's own ignore drops its soft filter from the split child statement");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- T212-D4: a nested (level 2+) LoadWith on the single-query path renders one join chain and issues
    // --- exactly one denormalized command; an unexpressible nested child shape fails closed before any SQL.

    private static (SqliteDataContext Ctx, string Path) CreateNestedDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-eager-nested-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table eager_nested_parent (id integer primary key);" +
                "create table eager_nested_mid (id integer primary key, parent_id int not null);" +
                "create table eager_nested_leaf (id integer primary key, mid_id int not null);" +
                "insert into eager_nested_parent (id) values (1), (2), (3);" +
                "insert into eager_nested_mid (id, parent_id) values (10, 1), (11, 1), (12, 2);" +
                "insert into eager_nested_leaf (id, mid_id) values (100, 10), (101, 10), (110, 11), (120, 12);";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void SingleQuery_Nested_JoinsEveryLevelIntoOneCommand()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateNestedDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerNestedParent>()
                .LoadWith(
                    p => p.Mids,
                    c => c.From<EagerNestedMid>().LoadWith(m => m.Leaves, l => l.From<EagerNestedLeaf>(), m => m.Id, l => l.MidId),
                    p => p.Id,
                    m => m.ParentId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents[0].Mids.Select(m => m.Id).Should().Equal(10, 11);
            parents[2].Mids.Should().BeEmpty("a childless parent survives the nested Left Join chain");
            parents.Single(p => p.Id == 1).Mids.Single(m => m.Id == 10).Leaves.Select(l => l.Id).Should().Equal(100, 101);
            parents.Single(p => p.Id == 2).Mids.Single().Leaves.Select(l => l.Id).Should().Equal(120);

            // One denormalized command carrying the whole nested graph: no chunked IN list, no split fallback.
            interceptor.Statements.Should().HaveCount(1, "the nested graph must stitch from one command");
            var sql = interceptor.Statements[0].ToLowerInvariant();
            sql.Should().Contain("eager_nested_parent").And.Contain("eager_nested_mid").And.Contain("eager_nested_leaf");
            CountOccurrences(sql, " join ").Should().Be(2, "one join per nested level");
            sql.Should().NotContain(" in (");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_NestedInexpressibleChildShape_IsRejectedWithZeroSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateNestedDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var act = () => ctx.From<EagerNestedParent>()
                .LoadWith(
                    p => p.Mids,
                    c => c.From<EagerNestedMid>().LoadWith(m => m.Leaves, l => l.From<EagerNestedLeaf>().OrderBy(x => x.Id), m => m.Id, l => l.MidId),
                    p => p.Id,
                    m => m.ParentId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*");
            interceptor.Statements.Should().BeEmpty("an unexpressible nested child shape must fail before any SQL is generated");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

[SqlTable("eager_parent")]
public sealed class FilteredSqlEagerParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<FilteredSqlEagerChild> Children { get; } = new List<FilteredSqlEagerChild>();
}

[SqlTable("eager_child")]
public sealed class FilteredSqlEagerChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("strategy_parent")]
public sealed class StrategyParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<StrategyChild> Children { get; set; } = new List<StrategyChild>();
}

[SqlTable("strategy_child")]
public sealed class StrategyChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("mixed_parent")]
public sealed class MixedParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MixedChild> Children { get; set; } = new List<MixedChild>();
    public ICollection<MixedNote> Notes { get; set; } = new List<MixedNote>();
}

[SqlTable("mixed_child")]
public sealed class MixedChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("mixed_note")]
public sealed class MixedNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
