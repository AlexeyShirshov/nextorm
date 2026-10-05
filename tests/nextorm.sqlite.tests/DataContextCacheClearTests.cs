using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Serializes the tests that clear the process-wide <see cref="DataContextCache"/> with the rest of
/// the suite.
/// </summary>
[CollectionDefinition("DataContextCache clear", DisableParallelization = true)]
public sealed class DataContextCacheClearCollection;

/// <summary>
/// Verifies that <see cref="DataContextCache.Clear"/> invalidates the thread-local plan cache, so a
/// later prepared query rebuilds instead of returning the stale cached plan.
/// </summary>
[Collection("DataContextCache clear")]
public class DataContextCacheClearTests
{
    [Fact]
    public void Clear_Should_Invalidate_Implicit_Plan_Cache()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-clear-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table simple_entity (id integer primary key);" +
                              "insert into simple_entity (id) values (42);";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
            ctx.PurgeQueryCache();

            var command = ctx.From<ISimpleEntity>().Select(x => x.Id);
            var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            second.Should().BeSameAs(first);

            DataContextCache.Clear();

            var third = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            third.Should().NotBeSameAs(first);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Cache-clear lifetime characterization (Stage A, task #183 row 7): after a process-wide clear, the
    /// same closure shape is rebuilt and re-binds the current captured value instead of serving the
    /// stale plan. The B2 refresh/lifetime work must keep this cut valid.
    /// </summary>
    [Fact]
    public void Clear_Should_Rebuild_And_Rebind_The_Captured_Value()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-clear-rebind-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table simple_entity (id integer primary key);" +
                              "insert into simple_entity (id) values (42);";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
            ctx.PurgeQueryCache();

            var threshold = 0;
            var first = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
                ctx.From<ISimpleEntity>().Where(x => x.Id > threshold).Select(x => x.Id),
                createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            first.DbCommandParams[0].Value.Should().Be(0);

            DataContextCache.Clear();

            threshold = 100;
            var second = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
                ctx.From<ISimpleEntity>().Where(x => x.Id > threshold).Select(x => x.Id),
                createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

            second.Should().NotBeSameAs(first, "the cleared plan must be rebuilt");
            second.DbCommandParams[0].Value.Should().Be(100, "the rebuilt plan binds the current capture");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
