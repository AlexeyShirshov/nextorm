using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Core.Tests.PreparedCommands;

namespace NextORM.Core.Tests;

public class ExecuteSqlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteSql_ForwardsArgumentsAndAwaitsAsyncDisposalWithoutReading(bool failDisposal)
    {
        const string sql = " SELECT 1; SELECT 2; ";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var context = new ProbeHarnessContext();
        using var table = new DataTable();
        table.Columns.Add("value", typeof(int));
        table.Rows.Add(1);
        var reader = new ProbeDbDataReader(table.CreateDataReader());
        var failure = failDisposal ? new InvalidOperationException("Disposal failed.") : null;
        var command = new DisposalProbeCommand(failure);
        using var result = new ProcedureResult(context, new CommandReaderOwner(command, reader, null));
        var executor = new RawExecutor((text, parameters, token) =>
        {
            text.Should().Be(sql);
            parameters.Should().BeEmpty();
            token.Should().Be(cancellation.Token);
            return Task.FromResult(result);
        });

        var execution = executor.ExecuteSql(sql, cancellation.Token);
        try
        {
            var first = await Task.WhenAny(command.DisposalStarted.Task, execution)
                .WaitAsync(TestContext.Current.CancellationToken);
            first.Should().BeSameAs(command.DisposalStarted.Task);
            execution.IsCompleted.Should().BeFalse();
            reader.ReadCount.Should().Be(0);
            reader.IsClosed.Should().BeTrue();
        }
        finally
        {
            command.ReleaseDisposal.SetResult();
        }

        if (failure is not null)
        {
            var act = () => execution;
            var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
            thrown.Which.Should().BeSameAs(failure);
        }
        else
        {
            await execution;
        }
    }

    [Fact]
    public async Task ExecuteSql_WhenExecutionFails_PropagatesTheOriginalException()
    {
        var failure = new InvalidOperationException("Execution failed.");
        var executor = new RawExecutor((_, _, _) => Task.FromException<ProcedureResult>(failure));

        var act = () => executor.ExecuteSql("SELECT 1", TestContext.Current.CancellationToken);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(failure);
    }

    private sealed class RawExecutor(
        Func<string, IReadOnlyList<ProcedureParameter>, CancellationToken, Task<ProcedureResult>> execute) : IRawCommandExecutor
    {
        public Task<ProcedureResult> ExecuteRawAsync(
            string sql, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken)
            => execute(sql, parameters, cancellationToken);
    }

    private sealed class DisposalProbeCommand(Exception? failure) : SqliteCommand
    {
        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask DisposeAsync()
        {
            DisposalStarted.SetResult();
            await ReleaseDisposal.Task.ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
            if (failure is not null)
                throw failure;
        }
    }
}
