using System.Data.Common;
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
    public void AsSingleQuery_IssuesExactlyOneCommandWithTheChildFilterInTheJoin()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>().Where(x => x.Name != "y"), p => p.Id, c => c.ParentId)
                .AsSingleQuery()
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

            ctx.InsertInto<EagerParent>().Values(parents).Insert();
            ctx.InsertInto<EagerChild>().Values(children).Insert();
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
    public void AsSingleQuery_1001Keys_IssuesExactlyOneCommandWithoutAChunkedInList()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateBulkDb(1001);
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();

            var parents = ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
                .AsSingleQuery()
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
    public void AsSingleQuery_TwoSpecs_IssueOneCommandJoiningBothChildren()
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
                .LoadWith(p => p.Notes, c => c.From<EagerTwoNote>(), p => p.Id, n => n.ParentId)
                .AsSingleQuery()
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
}
