using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Coverage for the per-context (<c>UseCommandTimeout</c>) and per-query (<c>WithCommandTimeout</c>)
/// command timeout. A prepared command is created without opening the connection, so the value baked
/// into its <c>DbCommand.CommandTimeout</c> can be asserted directly; the mutation path runs against a
/// temp file database observed through an interceptor.
/// </summary>
public class CommandTimeoutTests
{
    private static IDataContext Create(int? seconds = null)
    {
        var builder = new DataContextBuilder();
        if (seconds is int value)
            builder = builder.UseCommandTimeout(value);

        var ctx = new SqliteDataContext("Data Source=:memory:", builder);
        ctx.PurgeQueryCache();
        return ctx;
    }

    private static DbCommand Prepare(IDataContext ctx, QueryCommand<int> command)
    {
        var prepared = ctx.GetPreparedQueryCommand(command, false, true, TestContext.Current.CancellationToken);
        return ((DbPreparedQueryCommand<int>)prepared).DbCommand;
    }

    private static int ProviderDefault()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var command = connection.CreateCommand();
        return command.CommandTimeout;
    }

    [Fact]
    public void ContextTimeout_ShouldApplyToPreparedCommand()
    {
        using var ctx = Create(17);

        Prepare(ctx, ctx.From<ISimpleEntity>().Select(x => x.Id)).CommandTimeout.Should().Be(17);
    }

    [Fact]
    public void NoTimeout_ShouldKeepProviderDefault()
    {
        using var ctx = Create();

        Prepare(ctx, ctx.From<ISimpleEntity>().Select(x => x.Id)).CommandTimeout.Should().Be(ProviderDefault());
    }

    [Fact]
    public void PerQueryTimeout_ShouldOverrideContext()
    {
        using var ctx = Create(17);

        var command = ctx.From<ISimpleEntity>().WithCommandTimeout(5).Select(x => x.Id);
        Prepare(ctx, command).CommandTimeout.Should().Be(5);
    }

    [Fact]
    public void NonGenericBuilder_WithCommandTimeout_ShouldApply()
    {
        using var ctx = Create(17);

        var command = ctx.From("simple_entity").WithCommandTimeout(8).Select(x => x["id"].AsInt);
        Prepare(ctx, command).CommandTimeout.Should().Be(8);
    }

    [Fact]
    public void QueryCommand_WithCommandTimeout_ShouldApply()
    {
        using var ctx = Create(17);

        var command = ctx.From<ISimpleEntity>().Select(x => x.Id).WithCommandTimeout(9);
        Prepare(ctx, command).CommandTimeout.Should().Be(9);
    }

    [Fact]
    public void DifferentPerQueryTimeouts_ShouldNotShareCachedPlanAndShouldNotLeak()
    {
        using var ctx = Create(17);

        var shortCommand = Prepare(ctx, ctx.From<ISimpleEntity>().WithCommandTimeout(11).Select(x => x.Id));
        shortCommand.CommandTimeout.Should().Be(11);

        var longCommand = Prepare(ctx, ctx.From<ISimpleEntity>().WithCommandTimeout(22).Select(x => x.Id));
        longCommand.CommandTimeout.Should().Be(22);
        ReferenceEquals(shortCommand, longCommand).Should().BeFalse("a different timeout must not reuse the cached command");

        var shortAgain = Prepare(ctx, ctx.From<ISimpleEntity>().WithCommandTimeout(11).Select(x => x.Id));
        ReferenceEquals(shortCommand, shortAgain).Should().BeTrue("an equal timeout must reuse the cached plan");
        shortAgain.CommandTimeout.Should().Be(11);
    }

    [Fact]
    public void PerQueryTimeout_ShouldNotLeakToALaterPlainQuery()
    {
        using var ctx = Create(17);

        Prepare(ctx, ctx.From<ISimpleEntity>().WithCommandTimeout(5).Select(x => x.Id)).CommandTimeout.Should().Be(5);

        Prepare(ctx, ctx.From<ISimpleEntity>().Select(x => x.Id)).CommandTimeout.Should().Be(17);
    }

    [Fact]
    public void ContextsWithDifferentTimeouts_ShouldNotShareCachedPlan()
    {
        using var first = Create(17);
        using var second = Create(40);

        Prepare(first, first.From<ISimpleEntity>().Select(x => x.Id)).CommandTimeout.Should().Be(17);
        Prepare(second, second.From<ISimpleEntity>().Select(x => x.Id)).CommandTimeout.Should().Be(40);
    }

    [Fact]
    public void ZeroTimeout_WithoutContextTimeout_ShouldKeepProviderDefault()
    {
        using var ctx = Create();

        var command = ctx.From<ISimpleEntity>().WithCommandTimeout(0).Select(x => x.Id);
        Prepare(ctx, command).CommandTimeout.Should().Be(ProviderDefault());
    }

    [Fact]
    public void ContextTimeout_ShouldApplyToMutationCommand()
    {
        var interceptor = new CapturingInterceptor();
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-timeout-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var setup = connection.CreateCommand();
            setup.CommandText = "create table simple_entity (id integer primary key);";
            setup.ExecuteNonQuery();
        }

        try
        {
            using var ctx = new SqliteDataContext(
                $"Data Source={path}",
                new DataContextBuilder().UseCommandTimeout(13).AddInterceptor(interceptor));

            ctx.DeleteFrom<ISimpleEntity>().All().Delete();

            interceptor.LastCommand.Should().NotBeNull();
            interceptor.LastCommand!.CommandTimeout.Should().Be(13);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CapturingInterceptor : IQueryInterceptor
    {
        public DbCommand? LastCommand { get; private set; }

        public void CommandInitialized(CommandEventData eventData, DbCommand command) => LastCommand = command;

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => LastCommand = command;

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        {
        }

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        {
        }
    }
}
