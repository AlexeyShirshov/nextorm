using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.PreparedCommands;

public sealed class BufferedToListAsyncTests
{
    [Fact]
    public async Task CancellingSecondPendingReadCancelsTheListAndReleasesTheLease()
    {
        using var cancellation = new CancellationTokenSource();
        using var cleanup = new CancellationTokenSource();
        var reading = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            connection.ReaderWrapper = inner => new ProbeDbDataReader(inner)
            {
                BeforeReadAsync = (attempt, token) =>
                {
                    if (attempt != 2)
                        return Task.CompletedTask;

                    var pending = release.Task.WaitAsync(token).WaitAsync(cleanup.Token);
                    reading.SetResult(token);
                    return pending;
                },
            };
            var prepared = PreparedCommandHarness.IntPrepared(
                "select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            var operation = executor.ToListAsync(prepared, null, cancellation.Token);
            var timeout = TimeSpan.FromSeconds(5);
            try
            {
                var first = await Task.WhenAny(reading.Task, operation).WaitAsync(timeout, TestContext.Current.CancellationToken);
                first.Should().BeSameAs(reading.Task, "the second async read must start before the list completes");
                (await reading.Task).Should().Be(cancellation.Token);
                operation.IsCompleted.Should().BeFalse("the second read is still pending");
                await cancellation.CancelAsync();

                var act = async () => await operation.WaitAsync(timeout);
                var error = await act.Should().ThrowAsync<OperationCanceledException>();
                error.Which.CancellationToken.Should().Be(cancellation.Token);
                operation.IsCanceled.Should().BeTrue();
                prepared.GetLeaseOwnerForTest(connection).Should().Be(0);
                connection.LastCommand.DisposeCount.Should().Be(1);
            }
            finally
            {
                await cleanup.CancelAsync();
                if (!operation.IsCompleted)
                    await Task.WhenAny(operation).WaitAsync(timeout, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2, 2, 1)]
    public async Task ReturnsAllRowsInQueryOrder(int limit, params int[] expected)
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared(
                $"select id from pdca_t333_rows order by id desc limit {limit}", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            var rows = await executor.ToListAsync(prepared, null, TestContext.Current.CancellationToken);

            rows.Should().Equal(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
