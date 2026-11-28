using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// The parameterless <c>PrepareBuffered()</c> and explicit <c>PrepareBuffered(default)</c> produce an
/// equivalent prepared command: the same SQL, the same reader behavior, the same buffered execution and
/// the same rows. A buffered preparation must not support streaming, which is requested explicitly.
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

            var buffered = command.PrepareBuffered(cancellationToken: cancellationToken);
            var byDefault = (DbPreparedQueryCommand<int>)((PreparedBufferedQueryCommand<int>)buffered).Command;
            var explicitDefault = (DbPreparedQueryCommand<int>)((PreparedBufferedQueryCommand<int>)
                command.PrepareBuffered(default, cancellationToken)).Command;

            byDefault.SqlStmt.Should().NotBeNullOrWhiteSpace();
            byDefault.SqlStmt.Should().Be(explicitDefault.SqlStmt, "both preparation forms compile the same SQL");
            byDefault.Behavior.Should().Be(explicitDefault.Behavior, "both forms request the same reader behavior");
            byDefault.CachePolicy.Should().Be(PreparedCommandCache.Connection);
            explicitDefault.CachePolicy.Should().Be(PreparedCommandCache.Connection);

            byDefault.StreamingEnabled.Should().BeFalse();
            explicitDefault.StreamingEnabled.Should().BeFalse();

            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            FluentActions.Invoking(() => executor.CreateAsyncEnumerator(byDefault, null, cancellationToken))
                .Should().Throw<InvalidOperationException>().WithMessage("*PrepareStreaming()*");
            ExecuteIds(executor, byDefault).Should().Equal(new[] { 1, 2 });
            ExecuteIds(executor, explicitDefault).Should().Equal(new[] { 1, 2 });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StreamingPrepare_EnablesFreshEnumeratorsForEachExecution()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);

            var cancellationToken = TestContext.Current.CancellationToken;
            var streaming = command.PrepareStreaming(cancellationToken: cancellationToken);
            await using var first = streaming.CreateAsyncEnumerator(ctx, cancellationToken);
            await using var second = streaming.CreateAsyncEnumerator(ctx, cancellationToken);
            first.Should().NotBeSameAs(second);
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
