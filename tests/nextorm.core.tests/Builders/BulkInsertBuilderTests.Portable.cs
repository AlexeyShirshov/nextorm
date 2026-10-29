using FluentAssertions;
using System.Data.Common;

// These acceptance tests own the tokens they assert on (cancellation identity, deadline races); the
// runner token would mask the identity under test, so xUnit1051 is not applicable here.
#pragma warning disable xUnit1051

namespace NextORM.Core.Tests;

/// <summary>
/// D2 acceptance for the portable bulk-insert path: destination qualification (A04), local temporary
/// targets (A05), the whole-invocation deadline (A06), the exact batch limits (A11) and the progress /
/// cancellation contract (A12). The fake mutation executor renders through the real insert renderer, so
/// the assertions cover the SQL the provider would receive without opening a connection.
/// </summary>
public partial class BulkInsertBuilderTests
{
    // ---------------------------------------------------------------- A06 whole-invocation deadline

    [Fact]
    public void Timeout_ExpiredWhilePullingTheSource_ShouldThrowTimeoutException()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { Timeout = TimeSpan.FromMilliseconds(20) }, SlowRows(5, TimeSpan.FromMilliseconds(20)));

        var act = () => new PortableBulkInsertExecutor(executor).BulkInsert(command);

        act.Should().Throw<TimeoutException>();
        executor.WriteCount.Should().Be(0);
    }

    [Fact]
    public void Timeout_ExpiredDuringAWrite_ShouldThrowTimeoutExceptionAndKeepTheWrittenRows()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance) { WriteDelay = TimeSpan.FromMilliseconds(60) };
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { Timeout = TimeSpan.FromMilliseconds(5) }, Rows(1));

        var act = () => new PortableBulkInsertExecutor(executor).BulkInsert(command);

        act.Should().Throw<TimeoutException>();
        executor.WriteCount.Should().Be(1);
    }

    [Fact]
    public async Task Timeout_ShouldBeOneBudgetForTheWholeCall_NotResetPerBatch()
    {
        using var ctx = new PortableOnlyContext();
        var options = new BulkInsertOptions { Timeout = TimeSpan.FromMilliseconds(400), MaxBatchSize = 1 };
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance) { WriteDelay = TimeSpan.FromMilliseconds(120) };
        var command = BuildPortableCommand(ctx, options, Rows(5));

        var act = () => new PortableBulkInsertExecutor(executor).BulkInsertAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        // Five 120 ms writes fit in a 400 ms budget only if the budget restarted per batch; one monotonic
        // budget cancels the write that would cross the deadline, so the last batches are never written.
        executor.WriteCount.Should().BeLessThan(5).And.BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Timeout_NonPositive_ShouldBeRejectedAtValidation(int milliseconds)
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkPortableRow>(o => o.Timeout(TimeSpan.FromMilliseconds(milliseconds)));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Cancellation_WithAnExhaustedDeadline_ShouldSurfaceOperationCanceledWithTheCallerToken()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance) { WriteDelay = TimeSpan.FromMilliseconds(50) };
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { Timeout = TimeSpan.FromMilliseconds(1) }, Rows(3));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => new PortableBulkInsertExecutor(executor).BulkInsertAsync(command, cancellation.Token);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task Cancellation_DuringAWrite_ShouldStopBeforeTheNextBatch()
    {
        using var ctx = new PortableOnlyContext();
        using var cancellation = new CancellationTokenSource();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance) { AfterWrite = _ => cancellation.Cancel() };
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { MaxBatchSize = 1 }, Rows(3));

        var act = () => new PortableBulkInsertExecutor(executor).BulkInsertAsync(command, cancellation.Token);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cancellation.Token);
        executor.WriteCount.Should().Be(1);
    }

    // ---------------------------------------------------------------- A04 destination qualification

    [Fact]
    public void Portable_SchemaNameOnly_ShouldQualifyTheMappedTable()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { SchemaName = "staging" }, Rows(1));

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(1);

        executor.Statements.Should().ContainSingle()
            .Which.Should().StartWith("insert into staging.BulkPortableRow (");
    }

    [Fact]
    public void Portable_DatabaseName_ShouldQualifyTheTarget()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { DatabaseName = "other" }, Rows(1));

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(1);

        executor.Statements.Should().ContainSingle()
            .Which.Should().StartWith("insert into other.BulkPortableRow (");
    }

    [Fact]
    public void Portable_ConflictingSchemaAliases_ShouldThrowBeforeTheSourceIsRead()
    {
        using var ctx = new InMemoryDataContext();
        var pulled = false;

        var act = () => ctx.CreateBulkInsertBuilder<BulkPortableRow>(new BulkInsertOptions { TableName = "bulk_target", TableSchema = "staging", SchemaName = "archive" })
            .Values(ThrowingEntities(() => pulled = true))
            .BulkInsert();

        act.Should().Throw<ArgumentException>().WithMessage("*TableSchema*SchemaName*");
        pulled.Should().BeFalse();
    }

    [Fact]
    public void Portable_DatabaseAndSchemaOnASingleQualifierDialect_ShouldBeRefusedBeforeTheSource()
    {
        // SqliteDialect treats the database and the schema as one level, so both together cannot be expressed.
        using var ctx = new SqlitePortableContext();
        var pulled = false;
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { DatabaseName = "other", SchemaName = "staging" }, TrackingRows(() => pulled = true));

        var act = () => new PortableBulkInsertExecutor(ctx).BulkInsert(command);

        act.Should().Throw<NotSupportedException>().WithMessage("*same qualifier*");
        pulled.Should().BeFalse();
    }

    // ---------------------------------------------------------------- A05 local temporary target

    [Fact]
    public void Portable_LocalTemporary_ShouldAddressTheSameConnectionWithoutDdl()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { TargetKind = BulkInsertTargetKind.LocalTemporary }, Rows(2));

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(2);

        executor.WriteCount.Should().Be(1);
        executor.Statements.Should().ContainSingle()
            .Which.Should().Be("insert into BulkPortableRow (Code, Quantity) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void Portable_LocalTemporaryWithDatabaseName_ShouldBeRefusedByNameBeforeTheSource()
    {
        using var ctx = new PortableOnlyContext();
        var pulled = false;
        var command = BuildPortableCommand(
            ctx,
            new BulkInsertOptions { TargetKind = BulkInsertTargetKind.LocalTemporary, DatabaseName = "other" },
            TrackingRows(() => pulled = true));

        var act = () => PortableBulkInsertExecutor.EnsurePortableOptionsSupported(command);

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
        pulled.Should().BeFalse();
    }

    // ---------------------------------------------------------------- A11 exact limits

    [Fact]
    public void MaxBatchSize_ShouldSplitIntoEqualBatches()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { MaxBatchSize = 2 }, Rows(5));

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(5);

        executor.BatchRowCounts.Should().Equal(2, 2, 1);
    }

    [Fact]
    public void MaxParameters_ShouldSplitAtTheExactParameterCount()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { MaxParameters = 5 }, Rows(5));
        command.Columns.Should().HaveCount(2);

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(5);

        // Five parameters over two columns per row bind exactly two rows per statement.
        executor.BatchRowCounts.Should().Equal(2, 2, 1);
    }

    [Fact]
    public void MaxSqlLength_ShouldSplitAtTheExactRenderedLength()
    {
        using var ctx = new PortableOnlyContext();
        var rows = Rows(5).ToArray();
        var probe = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var probeCommand = BuildPortableCommand(ctx, new BulkInsertOptions(), []);

        // The limit is derived from the SQL two rows actually render: an estimate cannot reproduce it.
        var twoRowLength = probe.Render(PortableBulkInsertExecutor.BuildBatch(probeCommand, [rows[0], rows[1]])).Length;
        var threeRowLength = probe.Render(PortableBulkInsertExecutor.BuildBatch(probeCommand, [rows[0], rows[1], rows[2]])).Length;
        threeRowLength.Should().BeGreaterThan(twoRowLength);

        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { MaxSqlLength = twoRowLength }, rows);

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(5);

        executor.BatchRowCounts.Should().Equal(2, 2, 1);
        executor.Statements.Should().OnlyContain(sql => sql.Length <= twoRowLength);
    }

    [Fact]
    public void MaxSqlLength_SingleInexpressibleRow_ShouldThrowBeforeSending()
    {
        using var ctx = new PortableOnlyContext();
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, new BulkInsertOptions { MaxSqlLength = 10 }, Rows(2));

        var act = () => new PortableBulkInsertExecutor(executor).BulkInsert(command);

        act.Should().Throw<NotSupportedException>().WithMessage("*MaxSqlLength*");
        executor.WriteCount.Should().Be(0);
    }

    // ---------------------------------------------------------------- A12 progress / cancellation

    [Fact]
    public void Progress_ShouldReportCumulativeCountsOnlyAfterCompletedBatches()
    {
        using var ctx = new PortableOnlyContext();
        var reported = new List<int>();
        var options = new BulkInsertOptions
        {
            MaxBatchSize = 2,
            Progress = (total, _) => reported.Add(total),
        };
        var executor = new RecordingMutationExecutor(PortableOnlyDialect.Instance);
        var command = BuildPortableCommand(ctx, options, Rows(5));

        new PortableBulkInsertExecutor(executor).BulkInsert(command).Should().Be(5);

        reported.Should().Equal(2, 4, 5);
    }

    // ---------------------------------------------------------------- helpers

    private static BulkInsertCommand BuildPortableCommand(DataContext ctx, BulkInsertOptions options, IEnumerable<object?[]> rows)
        => BuildContext(ctx, options).BuildCommand(rows, null);

    private static BulkInsertBuilder<BulkPortableRow> BuildContext(DataContext ctx, BulkInsertOptions options)
        => ctx.CreateBulkInsertBuilder<BulkPortableRow>(options);

    private static IEnumerable<object?[]> Rows(int count, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return [$"code-{i}", i];
        }
    }

    private static IEnumerable<object?[]> SlowRows(int count, TimeSpan delay)
    {
        for (var i = 0; i < count; i++)
        {
            Thread.Sleep(delay);
            yield return [$"code-{i}", i];
        }
    }

    private static IEnumerable<object?[]> TrackingRows(Action onPulled)
    {
        onPulled();
        yield return ["code", 1];
    }

    private static IEnumerable<BulkPortableRow> ThrowingEntities(Action onPulled)
    {
        onPulled();
        throw new InvalidOperationException("the source must not be enumerated");
#pragma warning disable CS0162 // Unreachable code: the throw documents that no row may be produced.
        yield return new BulkPortableRow();
#pragma warning restore CS0162
    }

    /// <summary>
    /// Renders and records every portable batch through the real insert renderer, with no database: it
    /// stands in for the execution role so batching, deadlines and progress are observable.
    /// </summary>
    private sealed class RecordingMutationExecutor(ISqlDialect dialect) : IMutationExecutor
    {
        public List<string> Statements { get; } = [];

        public List<int> BatchRowCounts { get; } = [];

        public TimeSpan WriteDelay { get; set; }

        public Action<int>? AfterWrite { get; set; }

        public int WriteCount => Statements.Count;

        public bool SupportsGeneratedColumns => false;

        public string Render(MutationCommand command)
        {
            var insert = command as InsertCommand
                ?? throw new NotSupportedException($"The portable path only renders inserts, not {command.GetType().Name}.");

            return SqlMutationBuilder.MakeInsert(
                dialect,
                quoteIdentifiers: false,
                namingConvention: null,
                insert,
                parameterProvider: new DefaultParameterProvider(),
                renderKind: MutationRenderKind.AdHoc).Sql;
        }

        public int Execute(MutationCommand command)
        {
            var insert = Record(command);
            if (WriteDelay > TimeSpan.Zero)
                Thread.Sleep(WriteDelay);

            AfterWrite?.Invoke(WriteCount);
            return insert.RowCount;
        }

        public async Task<int> Execute(MutationCommand command, CancellationToken cancellationToken)
        {
            var insert = command as InsertCommand
                ?? throw new NotSupportedException($"The portable path only executes inserts, not {command.GetType().Name}.");

            if (WriteDelay > TimeSpan.Zero)
                await Task.Delay(WriteDelay, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Recorded only once the write completed, so a cancelled write is not counted.
            Statements.Add(Render(insert));
            BatchRowCounts.Add(insert.RowCount);
            AfterWrite?.Invoke(WriteCount);
            return insert.RowCount;
        }

        private InsertCommand Record(MutationCommand command)
        {
            var insert = command as InsertCommand
                ?? throw new NotSupportedException($"The portable path only executes inserts, not {command.GetType().Name}.");

            Statements.Add(Render(insert));
            BatchRowCounts.Add(insert.RowCount);
            return insert;
        }

        public string RenderIdentityFunction(MutationCommand command) => throw new NotSupportedException("t305-fake");

        public object? ExecuteIdentity(MutationCommand command) => throw new NotSupportedException("t305-fake");

        public Task<object?> ExecuteIdentity(MutationCommand command, CancellationToken cancellationToken) => throw new NotSupportedException("t305-fake");

        public object? ExecuteIdentityFunction(MutationCommand command) => throw new NotSupportedException("t305-fake");

        public Task<object?> ExecuteIdentityFunction(MutationCommand command, CancellationToken cancellationToken) => throw new NotSupportedException("t305-fake");

        public IReadOnlyList<TResult> ExecuteReturning<TResult>(MutationCommand command, SelectExpression[] selectList, bool oneColumn)
            => throw new NotSupportedException("t305-fake");

        public Task<IReadOnlyList<TResult>> ExecuteReturning<TResult>(MutationCommand command, SelectExpression[] selectList, bool oneColumn, CancellationToken cancellationToken)
            => throw new NotSupportedException("t305-fake");
    }

    /// <summary>
    /// A context whose dialect is SQLite, used to prove a dialect-level destination refusal happens before
    /// any connection or source access (the connection factory therefore throws).
    /// </summary>
    private sealed class SqlitePortableContext : DataContext
    {
        public SqlitePortableContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => NextORM.Sqlite.SqliteDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException("t305-fake-param");

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException("t305-fake-connection");
    }
}

/// <summary>A plain two-column entity with no identity column, so every written column binds a parameter.</summary>
public class BulkPortableRow
{
    public string Code { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
