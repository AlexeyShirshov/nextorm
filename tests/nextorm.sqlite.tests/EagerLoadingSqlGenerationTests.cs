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
}
