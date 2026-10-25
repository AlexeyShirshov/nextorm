using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// T298 (#298): live SQL Server contract for <c>WITH TIES</c> combined with a non-zero <c>OFFSET</c>.
/// T-SQL permits <c>WITH TIES</c> only in <c>TOP(n)</c>; the paged form
/// (<c>OFFSET n ROWS FETCH NEXT m ROWS WITH TIES</c>) is a syntax error. The accepted oracle is
/// fail-closed: a positive-offset <c>WithTies()</c> request must be refused while the command is
/// prepared — before any connection is opened, any statement is sent and any plan is stored — with the
/// pinned <see cref="NotSupportedException"/>. The no-offset <c>TOP … WITH TIES</c> form and ordinary
/// paging stay supported and are the control arms of this class.
/// </summary>
/// <remarks>
/// The refusal arms run against a freshly created context with no open connection, observed by a
/// query/connection interceptor, so "prepared but not executed" is measured rather than assumed. The
/// baseline (pre-fix) red is produced by the same arm: when preparation does not refuse, the prepared
/// statement is sent anyway and SQL Server's own syntax error surfaces in the failure text.
/// Overlap: <c>SqlServerFunctionExecutionTests.PagingWithTies_ShouldExecute</c> owns the executed
/// <c>TOP … WITH TIES</c> case; this class owns the tied fixture, the positive-offset refusal and the
/// paged control.
/// </remarks>
[Collection("SqlServer")]
public sealed class SqlServerOffsetWithTiesTests : ProviderTestSuite
{
    /// <summary>The refusal text pinned by the acceptance oracle; it names the supported alternative.</summary>
    private const string RefusalMessage =
        "SQL Server does not support WITH TIES with a non-zero OFFSET. Use WithTies() without OFFSET.";

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateFixture()
    {
        Execute("drop table if exists ss16_ties");
        Execute("create table ss16_ties (id int not null primary key, score int not null, label nvarchar(50) not null)");
        // score = 20 is a two-row tie group, so top(2) with ties has to expand past the second row and
        // offset 1 / fetch next 2 has a deterministic (score, id) window of ids 2 and 3.
        Execute("insert into ss16_ties (id, score, label) values "
                + "(1, 10, 'a'), (2, 20, 'b'), (3, 20, 'c'), (4, 30, 'd'), (5, 40, 'e'), (6, 50, 'f')");
    }

    private void DropFixture() => Execute("drop table if exists ss16_ties");

    /// <summary>Runs only the preparation path: renders, binds and stores the plan, never executes.</summary>
    private static void Prepare<TResult>(DataContext context, QueryCommand<TResult> command)
        => context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);

    /// <summary>
    /// Prepares through the public path and, only if preparation let the statement through, executes it
    /// so the baseline red records the server's own T-SQL rejection instead of a silent pass. A fixed
    /// build throws inside preparation and never reaches the execution or the connection.
    /// </summary>
    private static void PrepareThenExpose<TResult>(DataContext context, QueryCommand<TResult> command)
    {
        var prepared = context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var sql = (prepared as DbPreparedQueryCommand<TResult>)?.DbCommand.CommandText;

        Console.WriteLine($"[T298] preparation did not refuse; statement sent to SQL Server:{Environment.NewLine}{sql}");

        try
        {
            context.ToList(prepared);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[T298] server rejection: {ex.GetType().FullName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>Both call orders of a positive-offset tied page must refuse during preparation.</summary>
    [Fact]
    public void PositiveOffset_ShouldRejectBeforeExecution()
    {
        CreateFixture();
        try
        {
            // Probe: an extreme-row command (SelectWhereMax) rejects paging before any dialect renderer
            // runs, so that dispatch cannot smuggle ties + offset past the pager.
            var extreme = ((DataContext)_sut.DataProvider).From<Ss16Tie>()
                .Offset(1)
                .Limit(2)
                .WithTies()
                .SelectWhereMax(x => x.Score)
                .ToCommand();

            FluentActions.Invoking(() => Prepare((DataContext)_sut.DataProvider, extreme))
                .Should().Throw<BuildSqlCommandException>()
                .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with paging*");

            AssertRefusedAtPreparation(
                "Page(2, 1).WithTies()",
                context => context.From<Ss16Tie>().OrderBy(x => x.Score).Page(2, 1).WithTies().Select(x => x.Id));

            AssertRefusedAtPreparation(
                "Offset(1).Limit(2).WithTies()",
                context => context.From<Ss16Tie>().OrderBy(x => x.Score).Offset(1).Limit(2).WithTies().Select(x => x.Id));
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>Control: the same page request without ties keeps paging normally and returns the second page.</summary>
    [Fact]
    public void OrdinaryPaging_ShouldReturnRequestedRows()
    {
        CreateFixture();
        try
        {
            var rows = _sut.DataProvider.From<Ss16Tie>()
                .OrderBy(x => x.Score)
                .OrderBy(x => x.Id)
                .Page(2, 1)
                .Select(x => x.Id)
                .ToList();

            rows.Should().Equal(new[] { 2, 3 }, "offset 1 / fetch next 2 over (score, id) selects the second page");

            // Control cache seam: an ordinary paged query still publishes and reuses its prepared plan,
            // so the refusal above is a property of the rejected shape, not a disabled plan cache.
            var context = (DataContext)_sut.DataProvider;
            context.PurgeQueryCache();
            var first = PrepareOrdinaryPage(context);
            var second = PrepareOrdinaryPage(context);

            second.Should().BeSameAs(first, "ordinary paging must still cache and reuse its prepared plan");
        }
        finally
        {
            DropFixture();
        }
    }

    private static object PrepareOrdinaryPage(DataContext context)
        => context.GetPreparedQueryCommand(
            context.From<Ss16Tie>().OrderBy(x => x.Score).OrderBy(x => x.Id).Page(2, 1).Select(x => x.Id),
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    /// <summary>Control: a no-offset tied page stays on the TOP path and keeps the whole tie group.</summary>
    [Fact]
    public void TopWithTies_ShouldStillExecute()
    {
        CreateFixture();
        try
        {
            var rows = _sut.DataProvider.From<Ss16Tie>()
                .OrderBy(x => x.Score)
                .Limit(2)
                .WithTies()
                .Select(x => x.Id)
                .ToList();

            rows.OrderBy(x => x).Should().Equal(new[] { 1, 2, 3 }, "top(2) with ties keeps every row tied with the second row (score = 20)");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>
    /// Builds a fresh context with an unopened connection, then runs two attempts of the same request
    /// shape. Both must refuse with the pinned message; the second attempt proves no rejected plan was
    /// stored (a stored plan would be reused instead of re-prepared). Zero commands executed and zero
    /// connection opens prove the refusal is preparation-time.
    /// </summary>
    private void AssertRefusedAtPreparation<TResult>(string because, Func<DataContext, QueryCommand<TResult>> build)
    {
        using var context = (DataContext)Provider.CreateContext();
        var observer = new PreparationObserver();
        context.AddInterceptor((IQueryInterceptor)observer);
        context.AddInterceptor((IConnectionInterceptor)observer);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var command = build(context);

            FluentActions.Invoking(() => PrepareThenExpose(context, command))
                .Should().Throw<NotSupportedException>($"{because}, attempt {attempt}, must refuse during preparation")
                .WithMessage("*non-zero OFFSET*")
                .Which.Message.Should().Be(RefusalMessage, "the refusal text is pinned by the acceptance oracle");
        }

        observer.Executing.Should().Be(0, $"{because}: a refused page request must never be executed");
        observer.Opened.Should().Be(0, $"{because}: a refused page request must not open a connection");
        context.GetConnection().State.Should().Be(ConnectionState.Closed, $"{because}: preparation must not touch the server");

        // Store seam: the normalized plan key the refused shape would be stored under must still be absent
        // after both attempts — a stored holder would have been returned instead of re-preparing.
        var probe = build(context);
        var planKey = probe.GetOrCreatePlanKey(null);
        QueryPlanStore.TryGet(context.GetType(), planKey, out var cachedPlan, out _)
            .Should().BeFalse($"{because}: a refused page request must not store a prepared plan");
        cachedPlan.Should().BeNull();
    }

    /// <summary>Counts executions and connection opens on the context that owns a refusal arm.</summary>
    private sealed class PreparationObserver : IQueryInterceptor, IConnectionInterceptor
    {
        private int _executing;
        private int _opened;

        public int Executing => Volatile.Read(ref _executing);

        public int Opened => Volatile.Read(ref _opened);

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Interlocked.Increment(ref _executing);

        public void ConnectionOpened(ConnectionEventData eventData) => Interlocked.Increment(ref _opened);
    }

    [SqlTable("ss16_ties")]
    public sealed class Ss16Tie
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int Score { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
