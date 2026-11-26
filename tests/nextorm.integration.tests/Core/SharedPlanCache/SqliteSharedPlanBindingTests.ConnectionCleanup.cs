using System.Data;
using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public async Task DisposeOnAnotherThread_DropsOnlyTheExactConnectionsCachedCommand()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder());
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder());
        var token = TestContext.Current.CancellationToken;
        var preparingThread = Environment.CurrentManagedThreadId;

        var firstInvocation = first.GetStreamingRowsQueryCommand(Query(first, 1), token);
        var secondInvocation = second.GetStreamingRowsQueryCommand(Query(second, 2), token);
        firstInvocation.ToList(first).Should().Equal(1);
        secondInvocation.ToList(second).Should().Equal(2);
        var holder = BoundQueryInvocation<int>.GetPlan(firstInvocation)!;
        BoundQueryInvocation<int>.GetPlan(secondInvocation).Should().BeSameAs(holder);
        var firstCommand = holder.PeekCommand(firstConnection);
        var secondCommand = holder.PeekCommand(secondConnection);
        firstCommand.Should().NotBeNull();
        secondCommand.Should().NotBeNull().And.NotBeSameAs(firstCommand);
        firstInvocation.ToList(first).Should().Equal(1);
        holder.PeekCommand(firstConnection).Should().BeSameAs(firstCommand);

        var disposedThread = await Task.Factory.StartNew(() =>
        {
            first.Dispose();
            return Environment.CurrentManagedThreadId;
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(10), token);
        disposedThread.Should().NotBe(preparingThread);

        firstConnection.State.Should().Be(ConnectionState.Open, "the connection is caller-owned");
        holder.PeekCommand(firstConnection).Should().BeNull("disposing the context must forget its execution state on any thread");
        holder.PeekCommand(secondConnection).Should().BeSameAs(secondCommand);
        secondInvocation.ToList(second).Should().Equal(2);
        holder.PeekCommand(secondConnection).Should().BeSameAs(secondCommand);
    }
}
