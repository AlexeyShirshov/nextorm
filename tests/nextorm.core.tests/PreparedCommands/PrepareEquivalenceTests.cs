using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC07 — the parameterless <c>Prepare()</c> and the explicit <c>Prepare(default)</c> must produce an
/// equivalent prepared command: the same SQL, the same reader behavior, the same buffered execution and
/// the same rows. A buffered preparation must not build a streaming enumerator (the removed
/// <c>nonStreamUsing</c> boolean used to be inverted; the option now has one positive meaning).
/// </summary>
[Trait("PDCA", "T333")]
public class PrepareEquivalenceTests
{
    [Fact]
    public void ParameterlessPrepare_EqualsExplicitDefault_AndNeverCreatesAnEnumerator()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
            var cancellationToken = TestContext.Current.CancellationToken;

            var byDefault = (DbPreparedQueryCommand<int>)command.Prepare(cancellationToken: cancellationToken);
            var explicitDefault = (DbPreparedQueryCommand<int>)command.Prepare(default, cancellationToken);

            byDefault.SqlStmt.Should().NotBeNullOrWhiteSpace();
            byDefault.SqlStmt.Should().Be(explicitDefault.SqlStmt, "both preparation forms compile the same SQL");
            byDefault.Behavior.Should().Be(explicitDefault.Behavior, "both forms request the same reader behavior");
            byDefault.CachePolicy.Should().Be(PreparedCommandCache.Connection);
            explicitDefault.CachePolicy.Should().Be(PreparedCommandCache.Connection);

            byDefault.StreamingEnabled.Should().BeFalse("a buffered preparation never builds a streaming enumerator");
            explicitDefault.StreamingEnabled.Should().BeFalse();
            byDefault.EnumeratorFactory.Should().BeNull("Prepare() must not create the row enumerator");
            explicitDefault.EnumeratorFactory.Should().BeNull();

            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            ExecuteIds(executor, byDefault).Should().Equal(new[] { 1, 2 });
            ExecuteIds(executor, explicitDefault).Should().Equal(new[] { 1, 2 });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StreamingPrepare_IsTheOnlyFormThatBuildsAnEnumerator()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);

            var streaming = (DbPreparedQueryCommand<int>)command.Prepare(
                new PrepareOptions { Streaming = true },
                TestContext.Current.CancellationToken);

            streaming.StreamingEnabled.Should().BeTrue("the positive Streaming option requests the enumerator");
            streaming.EnumeratorFactory.Should().NotBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static List<int> ExecuteIds(QueryExecutor executor, DbPreparedQueryCommand<int> prepared)
    {
        using var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
        var ids = new List<int>();
        while (owner.Reader.Read())
            ids.Add(owner.Reader.GetInt32(0));
        return ids;
    }
}
