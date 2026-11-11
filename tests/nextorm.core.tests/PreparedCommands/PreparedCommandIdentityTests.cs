using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC02 — the default <see cref="PreparedCommandCache.Connection"/> policy reuses exactly one underlying
/// command for successive executions on one connection, while distinct connection objects get their own
/// command even when their connection strings are equal. The negative control is the identity of the two
/// per-connection states: equal strings must never collapse them into one entry.
/// </summary>
[Trait("PDCA", "T333")]
public class PreparedCommandIdentityTests
{
    [Fact]
    public void ConnectionPolicy_ReusesTheCommandPerConnection_AndSeparatesEqualConnectionStrings()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var first = PreparedCommandHarness.OpenProbe(path);
            using var second = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.Connection);

            first.ConnectionString.Should().Be(second.ConnectionString,
                "both connections address the same file, so the connection string is equal");
            first.Should().NotBeSameAs(second, "the state key is the connection object, not its string");

            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(first));

            DbCommand? published = null;
            for (var i = 0; i < 4; i++)
            {
                Execute(executor, prepared).Should().Equal(new[] { 1, 2 }, "every execution returns the seeded rows");
                prepared.CreatedCommandCount.Should().Be(1, "the Connection policy mints a command once per connection");
                var current = prepared.PeekCommand(first);
                current.Should().NotBeNull("the reusable command must stay published for the connection");
                if (published is null)
                    published = current;
                else
                    current.Should().BeSameAs(published, "successive executions must run the identical command");
            }

            prepared.GetLeaseOwnerForTest(first).Should().Be(0, "a completed execution releases its lease");

            // A second connection object with the SAME connection string must not reuse the first state.
            var secondExecutor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(second));
            Execute(secondExecutor, prepared).Should().Equal(new[] { 1, 2 });

            prepared.CreatedCommandCount.Should().Be(2, "the distinct connection object mints its own command");
            prepared.LiveStateCount.Should().Be(2, "two live connection keys mean two independent states");
            first.CreatedCommandCount.Should().Be(1, "the first connection keeps exactly its own command");
            second.CreatedCommandCount.Should().Be(1, "the second connection keeps exactly its own command");
            prepared.PeekCommand(first).Should().BeSameAs(published);
            prepared.PeekCommand(second).Should().NotBeSameAs(published,
                "equal connection strings must never collapse two connection objects into one state");
            prepared.PeekCommand(second).Should().NotBeNull();
            first.DisposedCommandCount.Should().Be(0, "the Connection policy keeps its command alive across executions");
            second.DisposedCommandCount.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static List<int> Execute(QueryExecutor executor, DbPreparedQueryCommand<int> prepared)
    {
        using var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
        var ids = new List<int>();
        while (owner.Reader.Read())
            ids.Add(owner.Reader.GetInt32(0));
        return ids;
    }
}
