using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC08 — the command-reuse policy travels as an invocation parameter and never mutates the shared
/// <see cref="QueryCommand.Cache"/> flag: a <c>None</c> preparation neither enters the plan store nor
/// replaces a cached <c>Connection</c> preparation, and a shared <c>.Any()</c>/<c>.Count()</c> command
/// keeps working across preparations of both policies.
/// </summary>
[Trait("PDCA", "T333")]
public class PreparePolicyCacheSeparationTests
{
    [SqlTable("pdca_t333_rows")]
    internal sealed class ConnectionThenNoneRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("pdca_t333_rows")]
    internal sealed class NoneThenConnectionRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("pdca_t333_rows")]
    internal sealed class SharedAnyRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    [Fact]
    public void ConnectionThenNone_KeepsTheCommandCacheFlag_AndThePlanCacheUsable()
    {
        WithContext((ctx, _) =>
        {
            var command = ctx.From<ConnectionThenNoneRow>().OrderBy(x => x.Id).Select(x => x.Id);
            command.Cache.Should().BeTrue("a new command caches by default");

            var reuse = ctx.GetPreparedQueryCommand(command, false, true, PreparedCommandCache.Connection, TestContext.Current.CancellationToken);
            ((DbPreparedQueryCommand<int>)reuse).CachePolicy.Should().Be(PreparedCommandCache.Connection);
            command.Cache.Should().BeTrue("a Connection preparation never disables the shared flag");

            var fresh = ctx.GetPreparedQueryCommand(command, false, true, PreparedCommandCache.None, TestContext.Current.CancellationToken);
            ((DbPreparedQueryCommand<int>)fresh).CachePolicy.Should().Be(PreparedCommandCache.None);
            command.Cache.Should().BeTrue("a None preparation must not stickily disable the shared command");
            fresh.Should().NotBeSameAs(reuse);

            AssertPlanCacheStillHits(ctx);
        });
    }

    [Fact]
    public void NoneThenConnection_KeepsTheCommandCacheFlag_AndThePlanCacheUsable()
    {
        WithContext((ctx, _) =>
        {
            var command = ctx.From<NoneThenConnectionRow>().OrderBy(x => x.Id).Select(x => x.Id);
            command.Cache.Should().BeTrue();

            var fresh = ctx.GetPreparedQueryCommand(command, false, true, PreparedCommandCache.None, TestContext.Current.CancellationToken);
            ((DbPreparedQueryCommand<int>)fresh).CachePolicy.Should().Be(PreparedCommandCache.None);
            command.Cache.Should().BeTrue("the None policy is call-local, not a property of the shared command");

            var reuse = ctx.GetPreparedQueryCommand(command, false, true, PreparedCommandCache.Connection, TestContext.Current.CancellationToken);
            ((DbPreparedQueryCommand<int>)reuse).CachePolicy.Should().Be(PreparedCommandCache.Connection);
            command.Cache.Should().BeTrue();

            AssertPlanCacheStillHits(ctx);
        });
    }

    [Fact]
    public void NonePreparation_DoesNotReplaceTheCachedConnectionPreparation()
    {
        WithContext((ctx, _) =>
        {
            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
            var cancellationToken = TestContext.Current.CancellationToken;

            var firstConnection = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
                command, false, true, PreparedCommandCache.Connection, cancellationToken);
            firstConnection.CachePolicy.Should().Be(PreparedCommandCache.Connection);

            var none = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
                command, false, true, PreparedCommandCache.None, cancellationToken);
            none.CachePolicy.Should().Be(PreparedCommandCache.None, "each returned object carries its own request");
            none.Should().NotBeSameAs(firstConnection, "None is never served from the plan store");
            none.LiveStateCount.Should().Be(0, "a preparation creates no per-connection state");

            var secondConnection = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
                command, false, true, PreparedCommandCache.Connection, cancellationToken);
            secondConnection.Should().BeSameAs(firstConnection, "None must not evict or replace the cached plan");
            secondConnection.CachePolicy.Should().Be(PreparedCommandCache.Connection);
        });
    }

    [Fact]
    public void SharedAnyCommand_KeepsWorkingAcrossPreparationsOfBothPolicies()
    {
        WithContext((ctx, _) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var command = ctx.From<SharedAnyRow>().Where(x => x.Id > 0).ToParentCommand();
            var any = EntityBuilderExtensions.GetAnyCommand(ctx, command);
            any.Cache.Should().BeTrue("the shared terminal command caches by default");

            var none = (DbPreparedQueryCommand<bool>)any.Prepare(
                new PrepareOptions { Cache = PreparedCommandCache.None }, cancellationToken);
            none.CachePolicy.Should().Be(PreparedCommandCache.None);
            any.Cache.Should().BeTrue("preparing the shared Any command under None must not disable its cache");

            var again = EntityBuilderExtensions.GetAnyCommand(ctx, ctx.From<SharedAnyRow>().Where(x => x.Id > 0).ToParentCommand());
            again.Should().BeSameAs(any, "the context keeps one shared Any command per context");
            again.Cache.Should().BeTrue();

            ctx.From<SharedAnyRow>().Where(x => x.Id > 0).Any().Should().BeTrue();
            ctx.From<SharedAnyRow>().Where(x => x.Id > 0).Any().Should().BeTrue("the shared command still executes after both policies");
            any.Cache.Should().BeTrue();
        });
    }

    private static void AssertPlanCacheStillHits(SqliteDataContext ctx)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstCommand = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
        var first = ctx.GetPreparedQueryCommand(firstCommand, false, true, PreparedCommandCache.Connection, cancellationToken);
        var secondCommand = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
        var second = ctx.GetPreparedQueryCommand(secondCommand, false, true, PreparedCommandCache.Connection, cancellationToken);

        second.Should().BeSameAs(first, "the plan cache still serves a repeated normal query");
    }

    private static void WithContext(Action<SqliteDataContext, ProbeSqliteConnection> body)
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            body(ctx, connection);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
