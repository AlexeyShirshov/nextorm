using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC01 — one <c>static readonly</c> prepared command executes concurrently through two contexts on two
/// different live SQLite connections, with different runtime parameter values and independent results.
/// The prepared command is built once by a throwaway context and then used by both executing contexts,
/// which is exactly the cross-context reuse the prepared object promises; the barrier-coordinated loops
/// make parameter bleed or reader substitution observable.
/// </summary>
[Trait("PDCA", "T333")]
public class PreparedCommandConcurrencyTests
{
    [SqlTable("pdca_t333_rows")]
    internal sealed class ConcurrentRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    private static readonly IPreparedQueryCommand<int> Prepared = CreatePrepared();

    private static IPreparedQueryCommand<int> CreatePrepared()
    {
        // Preparation needs the dialect only: no connection is opened, and the prepared command keeps
        // no reference to this context (it holds the immutable factory and the compiled mapper).
        using var preparingContext = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var command = preparingContext.From<ConcurrentRow>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .Select(x => x.Id);
        return command.Prepare(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task OneStaticPreparedCommand_TwoContexts_TwoConnections_DistinctParameters()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var firstConnection = PreparedCommandHarness.OpenProbe(path);
            using var secondConnection = PreparedCommandHarness.OpenProbe(path);
            using var firstContext = new SqliteDataContext(firstConnection, new DataContextBuilder());
            using var secondContext = new SqliteDataContext(secondConnection, new DataContextBuilder());
            using var gate = new Barrier(2);
            var cancellationToken = TestContext.Current.CancellationToken;

            async Task RunAsync(DataContext context, object? parameter, int expected)
            {
                for (var i = 0; i < 25; i++)
                {
                    gate.SignalAndWait(TimeSpan.FromSeconds(30)).Should().BeTrue(
                        "both contexts must reach the execution boundary together to expose interleaving");
                    var rows = context.ToList(Prepared, new object?[] { parameter });
                    rows.Should().Equal(new[] { expected },
                        "each connection must bind its own parameter value and read its own reader");
                }
            }

            await Task.WhenAll(
                Task.Run(() => RunAsync(firstContext, 1, 1), cancellationToken),
                Task.Run(() => RunAsync(secondContext, 2, 2), cancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
