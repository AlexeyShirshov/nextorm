using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC04 — the preparation options surface: <c>default(PrepareOptions)</c> means buffered execution with
/// per-connection command reuse, and an undefined cache policy is rejected before any resource is
/// allocated (no connection command, no per-connection state). The absence of the removed public
/// prepared members (<c>DbCommand</c>, <c>LastRowCount</c>, <c>Enumerator</c>, the boolean overloads) is
/// proven separately by <c>.pdca/T333/rv1/results/AC04-surface.json</c> and is not duplicated here.
/// </summary>
[Trait("PDCA", "T333")]
public class PrepareOptionsTests
{
    [Fact]
    public void DefaultOptions_MeanBufferedConnectionReuse()
    {
        var byDefault = default(PrepareOptions);
        var constructed = new PrepareOptions();

        byDefault.Cache.Should().Be(PreparedCommandCache.Connection);
        byDefault.Streaming.Should().BeFalse("the default preparation is buffered");
        constructed.Should().Be(byDefault, "the parameterless struct is the documented default value");
        ((int)PreparedCommandCache.Connection).Should().Be(0, "the enum default must be connection reuse");
        ((int)PreparedCommandCache.None).Should().Be(1);
    }

    [Fact]
    public void UndefinedPolicy_ThrowsBeforeAnyCommandIsCreated()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
            var createdBefore = connection.CreatedCommandCount;

            var act = () => command.Prepare(new PrepareOptions { Cache = (PreparedCommandCache)42 });

            var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;
            exception.ParamName.Should().Be("options");
            exception.ActualValue.Should().Be((PreparedCommandCache)42);
            exception.Message.Should().Contain(nameof(PreparedCommandCache));
            connection.CreatedCommandCount.Should().Be(createdBefore,
                "the guard runs before the planner mints any command, so no ADO.NET command exists to leak");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UndefinedPolicy_IsRejectedBeforeTheContextIsDereferenced()
    {
        // The command has no context at all: only a guard that runs before preparation can throw the
        // policy error here instead of a NullReferenceException.
        var command = new QueryCommand<int>(null, new QueryDefinition());

        var act = () => command.Prepare(new PrepareOptions { Cache = (PreparedCommandCache)42 });

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("options");
    }

    [Fact]
    public void BuilderPrepare_UndefinedPolicy_ThrowsTheSameGuardBeforePreparing()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var builder = ctx.From<PreparedCommandHarness.PreparedProbeRow>();

        var act = () => builder.Prepare(new PrepareOptions { Cache = (PreparedCommandCache)42 });

        var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("options");
        exception.ActualValue.Should().Be((PreparedCommandCache)42);
    }

    [Fact]
    public void ExplicitPolicies_CreateThePreparedCommandWithThatPolicy()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);

            var reuse = ((DbPreparedQueryCommand<int>)command.Prepare(
                new PrepareOptions { Cache = PreparedCommandCache.Connection }, TestContext.Current.CancellationToken));
            var fresh = ((DbPreparedQueryCommand<int>)command.Prepare(
                new PrepareOptions { Cache = PreparedCommandCache.None }, TestContext.Current.CancellationToken));

            reuse.CachePolicy.Should().Be(PreparedCommandCache.Connection);
            fresh.CachePolicy.Should().Be(PreparedCommandCache.None);
            reuse.SqlStmt.Should().Be(fresh.SqlStmt, "the policy never changes the prepared SQL");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
