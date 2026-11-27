using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public void ReenteredColdMiss_KeepsTheFirstPublishedPlanAndItsRawAliases()
    {
        var token = TestContext.Current.CancellationToken;
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var firstFilter = FilterFor(new WhereOwner { Target = 1 });
        var secondFilter = FilterFor(new WhereOwner { Target = 2 });
        var secondQuery = Build(second, secondFilter);
        DbPreparedQueryCommand<int>? published = null;
        var gate = new ReenterPlanMiss(() =>
        {
            var nested = second.GetPreparedQueryCommand(secondQuery, false, true, token);
            nested.ToList(second).Should().Equal(2);
            published = BoundQueryInvocation<int>.GetPlan(nested);
        });
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(gate));
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder()
            .UseLoggerFactory(loggerFactory).AddInterceptor(firstTap));
        var outer = first.GetPreparedQueryCommand(Build(first, firstFilter), false, true, token);
        outer.ToList(first).Should().Equal(1);
        published.Should().NotBeNull("the inner call publishes while the original miss is in progress");
        BoundQueryInvocation<int>.GetPlan(outer).Should().BeSameAs(published);
        firstTap.LastParameters.Should().Equal((0, "Target", 1));
        secondTap.LastParameters.Should().Equal((0, "Target", 2));

        CheckRawAlias(first, firstFilter);
        CheckRawAlias(second, secondFilter);
        published!.PeekCommand(firstConnection).Should().NotBeNull().And.NotBeSameAs(published.PeekCommand(secondConnection));

        void CheckRawAlias(SqliteDataContext context, Expression<Func<BindingRow, bool>> filter)
        {
            var command = Build(context, filter);
            var shape = QueryCommand.CaptureRawShape(command)!;
            QueryPlanStore.TryGetRaw(context.GetType(), shape, command, out var alias).Should().BeTrue();
            alias!.Holder.Should().BeSameAs(published);
        }

        static Expression<Func<BindingRow, bool>> FilterFor(WhereOwner owner) => row => row.Id == owner.Target;

        static QueryCommand<int> Build(SqliteDataContext context, Expression<Func<BindingRow, bool>> filter)
            => context.From<BindingRow>().WithTag("issue16 reentered cold publication")
                .Where(filter).Select(row => row.Id);
    }

    private sealed class ReenterPlanMiss(Action onMiss) : ILoggerProvider, ILogger
    {
        private Action? _onMiss = onMiss;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Debug
                && formatter(state, exception).StartsWith("Query plan cache miss with hash:", StringComparison.Ordinal))
                Interlocked.Exchange(ref _onMiss, null)?.Invoke();
        }
    }

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
