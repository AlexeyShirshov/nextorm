using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public async Task IndependentThreads_CoordinatedColdMissesAndWarmCallUseOnePlan()
    {
        var token = TestContext.Current.CancellationToken;
        QueryPlanStore.Clear();
        using var barrier = new Barrier(2);
        var missGate = new PlanMissGate(barrier, token);
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(missGate));

        var cold = await Task.WhenAll(Start(1, loggerFactory), Start(2, loggerFactory))
            .WaitAsync(TimeSpan.FromSeconds(30), token);
        var warm = await Start(2, null).WaitAsync(TimeSpan.FromSeconds(30), token);

        using var assertions = new AssertionScope();
        missGate.Misses.Should().Be(2, "both cold workers must miss before either builds");
        cold[0].Thread.Should().NotBe(cold[1].Thread);
        warm.Thread.Should().NotBe(cold[0].Thread).And.NotBe(cold[1].Thread);
        cold[0].Rows.Should().Equal(1);
        cold[1].Rows.Should().Equal(2);
        warm.Rows.Should().Equal(2);
        cold[0].Parameters.Should().Equal((0, "target", 1));
        cold[1].Parameters.Should().Equal((0, "target", 2));
        warm.Parameters.Should().Equal((0, "target", 2));
        cold[0].Holder.Should().BeSameAs(warm.Holder);
        cold[1].Holder.Should().BeSameAs(warm.Holder);
        warm.Holder.CreatedCommandCount.Should().Be(3, "each of the three independent connections needs its own command");
        QueryPlanStore.Values.Should().ContainSingle().Which.Should().BeSameAs(warm.Holder);

        QueryPlanStore.Clear();
        QueryPlanStore.Values.Should().BeEmpty();
        var rebuilt = await Start(2, null).WaitAsync(TimeSpan.FromSeconds(30), token);
        rebuilt.Holder.Should().NotBeSameAs(warm.Holder);
        rebuilt.Rows.Should().Equal(2);
        rebuilt.Parameters.Should().Equal((0, "target", 2));

        Task<(DbPreparedQueryCommand<int> Holder, List<int> Rows, (int, string, int)[]? Parameters, int Thread)>
            Start(int target, ILoggerFactory? logger)
        {
            using var flow = ExecutionContext.SuppressFlow();
            return Task.Factory.StartNew(() =>
            {
                using var connection = OpenSeededConnection();
                var tap = new BindingTap();
                var options = new DataContextBuilder().AddInterceptor(tap);
                if (logger is not null)
                    options.UseLoggerFactory(logger);
                using var context = new SqliteDataContext(connection, options);
                var query = Build();
                var invocation = context.GetPreparedQueryCommand(query, false, true, token);
                var holder = BoundQueryInvocation<int>.GetPlan(invocation)!;
                if (logger is not null)
                {
                    var replay = Build();
                    QueryPlanStore.TryGetRaw(context.GetType(), QueryCommand.CaptureRawShape(replay)!,
                        replay, out var alias).Should().BeTrue();
                    alias!.Holder.Should().BeSameAs(holder);
                }
                return (holder, invocation.ToList(context), tap.LastParameters, Environment.CurrentManagedThreadId);

                QueryCommand<int> Build() => context.From<BindingRow>().WithTag("issue16 coordinated cold misses")
                    .Where(row => row.Id == target).Select(row => row.Id);
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    private sealed class PlanMissGate(Barrier barrier, CancellationToken token) : ILoggerProvider, ILogger
    {
        private int _misses;
        internal int Misses => Volatile.Read(ref _misses);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Debug
                && formatter(state, exception).StartsWith("Query plan cache miss with hash:", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _misses);
                barrier.SignalAndWait(TimeSpan.FromSeconds(15), token).Should().BeTrue();
            }
        }
    }

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

    [Fact]
    public async Task NativeListHit_DropsOnlyTheDisposedContextsCommand()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var token = TestContext.Current.CancellationToken;
        var preparingThread = Environment.CurrentManagedThreadId;

        var published = QueryPlanStore.NativePublishes;
        var firstQuery = QueryForNativeList(first, 1);
        firstQuery.ToList().Should().Equal(1);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        QueryPlanStore.NativePublishes.Should().Be(published + 1);
        QueryPlanStore.TryGet(first.GetType(), firstQuery.GetOrCreatePlanKey(null), out var stored, out _).Should().BeTrue();
        var holder = (DbPreparedQueryCommand<int>)stored!;

        var hits = QueryPlanStore.NativeHits;
        var replay = await RunOnDedicatedThread(second, 2).WaitAsync(TimeSpan.FromSeconds(10), token);
        replay.Thread.Should().NotBe(preparingThread);
        replay.Rows.Should().Equal(2);
        secondTap.LastParameters.Should().Equal((0, "target", 2));
        QueryPlanStore.NativeHits.Should().Be(hits + 1);
        var firstCommand = holder.PeekCommand(firstConnection);
        var secondCommand = holder.PeekCommand(secondConnection);
        firstCommand.Should().NotBeNull();
        secondCommand.Should().NotBeNull().And.NotBeSameAs(firstCommand);

        var continuation = await RunOnDedicatedThread(first, 2).WaitAsync(TimeSpan.FromSeconds(10), token);
        continuation.Thread.Should().NotBe(preparingThread);
        continuation.Rows.Should().Equal(2);
        firstTap.LastParameters.Should().Equal((0, "target", 2));
        QueryPlanStore.NativeHits.Should().Be(hits + 2);
        holder.PeekCommand(firstConnection).Should().BeSameAs(firstCommand);

        var disposedThread = await Task.Factory.StartNew(() =>
        {
            first.Dispose();
            return Environment.CurrentManagedThreadId;
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(10), token);
        disposedThread.Should().NotBe(preparingThread);

        firstConnection.State.Should().Be(ConnectionState.Open);
        holder.PeekCommand(firstConnection).Should().BeNull();
        holder.PeekCommand(secondConnection).Should().BeSameAs(secondCommand);

        Task<(int Thread, List<int> Rows)> RunOnDedicatedThread(SqliteDataContext context, int target)
        {
            using var flow = ExecutionContext.SuppressFlow();
            return Task.Factory.StartNew(() => (Environment.CurrentManagedThreadId, QueryForNativeList(context, target).ToList()),
                token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        static QueryCommand<int> QueryForNativeList(SqliteDataContext context, int target)
            => context.From<BindingRow>().Where(row => row.Id <= row.Id && row.Id == target).Select(row => row.Id);
    }
}
