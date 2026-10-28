using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T296b (#296, D4): the same live truth tables as the shared suite, executed against a real
/// ClickHouse server. ClickHouse has no <see cref="CommonTestSuite"/> subclass (and this task may not
/// invent one), so the shared assertion bodies are re-pinned here with ClickHouse-native DDL
/// (<c>Nullable(Int32)</c>/<c>Nullable(String)</c>). The RIGHT JOIN form is outside the approved live
/// scope and the shared body records it as not applicable rather than claiming it. The bare-<c>IS NULL</c>
/// <c>JOIN ON</c> shapes are asserted as preparation-time refusals, and one fact proves that refusal
/// precedes any connection or command creation.
/// </summary>
public sealed class T296bNullableComparisonClickHouseTests : IDisposable
{
    private readonly IDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public T296bNullableComparisonClickHouseTests(ITestOutputHelper output)
    {
        _output = output;
        Assert.SkipUnless(ClickHouseContainer.IsAvailable, ClickHouseContainer.Failure ?? "ClickHouse is not available.");

        _ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T296bCapturedNullableComparison_ShouldMatchTheTruthTable()
        => CommonTestSuite.AssertT296bCapturedComparisonTruthTable(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bMappedHasValue_ShouldMatchTheTruthTable()
        => CommonTestSuite.AssertT296bMappedHasValueTruthTable(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bCorrelatedExists_ShouldKeepOuterAssociation()
        => CommonTestSuite.AssertT296bCorrelatedExistsAssociation(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bBooleanSelect_ShouldMaterialiseNonNullableBool()
        => CommonTestSuite.AssertT296bBooleanSelectMaterialisation(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bJoinOn_ShouldKeepMultiplicityAndOuterRows()
        => CommonTestSuite.AssertT296bJoinOnTruthTable(_ctx, "clickhouse", Evidence);

    /// <summary>
    /// Counts the connections the context creates. <c>DataContext.OnConnectionCreated</c> is invoked for
    /// every connection the context starts using and the context builds its own lazily, so a zero count
    /// after a refused preparation proves the refusal preceded any connection.
    /// </summary>
    private sealed class ConnectionCountingContext : ClickHouseDataContext
    {
        private int _connectionsCreated;

        public ConnectionCountingContext(string connectionString)
            : base(connectionString, new DataContextBuilder())
        {
        }

        public int ConnectionsCreated => Volatile.Read(ref _connectionsCreated);

        protected override void OnConnectionCreated(DbConnection connection)
        {
            Interlocked.Increment(ref _connectionsCreated);
            base.OnConnectionCreated(connection);
        }
    }

    /// <summary>Counts every command lifecycle event the context raises.</summary>
    private sealed class CommandCountingInterceptor : IQueryInterceptor
    {
        private int _events;

        public void CommandInitialized(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _events);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _events);

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
            => Interlocked.Increment(ref _events);

        public int Total => Volatile.Read(ref _events);
    }

    /// <summary>
    /// A27–A28 (ClickHouse): preparing a bare-<c>IS NULL</c> <c>JOIN ON</c> shape fails with the guard's
    /// typed refusal, before a connection is created, before a command is created and before anything
    /// reaches the server. The zero counts are backed by a control query on the same instrumented
    /// context, so a silently dead counter cannot pass as evidence.
    /// </summary>
    [Fact]
    public void T296bJoinOnBareNullRefusal_ShouldPrecedeAnyConnectionOrCommand()
    {
        var interceptor = new CommandCountingInterceptor();
        using var probe = new ConnectionCountingContext(ClickHouseContainer.ConnectionString);
        probe.AddInterceptor(interceptor);

        var parents = probe.From<IT296bParent>();
        var children = probe.From<IT296bChild>();

        Action prepare = () =>
        {
            var prepared = ((DataContext)probe).GetPreparedQueryCommand(
                parents.Join(children, (p, c) => p.Id == c.ParentId && c.Amount == (int?)null)
                    .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id }),
                createEnumerator: false,
                storeInCache: false,
                CancellationToken.None);
            _ = prepared;
        };

        prepare.Should().Throw<NotSupportedException>().WithMessage(CommonTestSuite.T296bBareIsNullRefusalMessage);
        probe.ConnectionsCreated.Should().Be(0, "the guard decides on the command tree, before a connection is created");
        interceptor.Total.Should().Be(0, "no command is created for a refused statement");
        Evidence($"provider=clickhouse test=T296b_JoinOn row=A27-A28 scenario=inner nullable literal " +
            $"status=refused connections={probe.ConnectionsCreated} commands={interceptor.Total}");

        // Liveness of the same instrumentation: the control must create the connection and raise the
        // command events the refusal never did, and must still return the matched rows.
        var control = parents.Join(children, (p, c) => p.Id == c.ParentId && c.Amount.HasValue)
            .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id })
            .ToList();

        control.Select(p => $"{p.Parent}:{p.Child}").OrderBy(x => x, StringComparer.Ordinal)
            .Should().Equal("1:12", "2:21", "2:22");
        probe.ConnectionsCreated.Should().BeGreaterThan(0, "the control uses the connection the refusal never created");
        interceptor.Total.Should().BeGreaterThan(0, "the control raises the command events the refusal never raised");
        Evidence($"provider=clickhouse test=T296b_JoinOn row=A27-A28 scenario=inner HasValue control " +
            $"rows=[{string.Join(",", control.Select(p => $"{p.Parent}:{p.Child}"))}] " +
            $"connections={probe.ConnectionsCreated} commands={interceptor.Total}");
    }

    private void Seed()
    {
        Execute("drop table if exists t296b_child");
        Execute("drop table if exists t296b_parent");
        Execute("drop table if exists t296b_rows");

        Execute("create table t296b_rows (id Int64, n Nullable(Int32), s Nullable(String)) engine = Memory");
        Execute(
            "insert into t296b_rows (id, n, s) values " +
            "(1, null, null), (2, 10, 'alpha'), (3, 20, 'beta'), (4, 10, 'alpha'), (5, 30, null), (6, null, 'gamma')");

        Execute("create table t296b_parent (id Int64, label Nullable(String)) engine = Memory");
        Execute("insert into t296b_parent (id, label) values (1, 'one'), (2, 'two'), (3, 'three'), (4, 'four')");

        Execute(
            "create table t296b_child (id Int64, parent_id Int64, amount Nullable(Int32), tag Nullable(String)) " +
            "engine = Memory");
        Execute(
            "insert into t296b_child (id, parent_id, amount, tag) values " +
            "(11, 1, null, null), (12, 1, 5, 'x'), (21, 2, 42, 'y'), (22, 2, 42, 'y'), (31, 3, null, null)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private void Log(string message) => _output.WriteLine(message);

    private Action<string> Evidence => CommonTestSuite.T296bEvidenceLogger(Log);
}
