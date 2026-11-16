using System.Collections.Concurrent;
using System.Globalization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding.FanOut;

public sealed class ShardQueryExecutionTests
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(null, 4, 4)]
    [InlineData(1, 4, 1)]
    [InlineData(99, 4, 4)]
    [InlineData(0, 4, 1)]
    [InlineData(null, 0, 1)]
    public void DegreeOfParallelism_ShouldClampConfiguredAndDefaultValues(
        int? parallelism,
        int shardCount,
        int expected)
    {
        ShardQueryExecution.DegreeOfParallelism(parallelism, shardCount).Should().Be(expected);
    }

    [Fact]
    public async Task RunEach_ShouldPreserveVectorOrder_AndKeepEachOwnerSequential()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [7, 3, 9, 1];
        var owners = new Dictionary<int, string>
        {
            [7] = "owner-a",
            [3] = "owner-b",
            [9] = "owner-a",
            [1] = "owner-a",
        };
        var events = new ConcurrentQueue<string>();
        var activeByOwner = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var maxByOwner = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var firstOwnerBucketEntered = NewCompletionSource<bool>();
        var otherOwnerBucketEntered = NewCompletionSource<bool>();
        var releaseFirstOwnerBucket = NewCompletionSource<bool>();

        var execution = Task.Run(() => ShardQueryExecution.RunEach(
            source,
            buckets,
            owners,
            parallelism: 2,
            query =>
            {
                var bucket = GetBucket(query);
                var owner = owners[bucket];
                var active = activeByOwner.AddOrUpdate(owner, 1, static (_, current) => current + 1);
                maxByOwner.AddOrUpdate(owner, active, (_, current) => Math.Max(current, active));
                events.Enqueue($"start:{bucket}");
                try
                {
                    if (bucket == 7)
                    {
                        firstOwnerBucketEntered.TrySetResult(true);
                        releaseFirstOwnerBucket.Task.GetAwaiter().GetResult();
                    }
                    else if (bucket == 3)
                    {
                        otherOwnerBucketEntered.TrySetResult(true);
                    }

                    return bucket;
                }
                finally
                {
                    events.Enqueue($"end:{bucket}");
                    activeByOwner.AddOrUpdate(owner, 0, static (_, current) => current - 1);
                }
            }));

        try
        {
            await firstOwnerBucketEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            await otherOwnerBucketEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseFirstOwnerBucket.TrySetResult(true);
        }

        var results = await execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        results.Should().Equal(7, 3, 9, 1);
        maxByOwner.Values.Should().OnlyContain(maximum => maximum == 1);

        var executionEvents = events.ToArray();
        Array.IndexOf(executionEvents, "end:7").Should()
            .BeLessThan(Array.IndexOf(executionEvents, "start:9"));
        Array.IndexOf(executionEvents, "end:9").Should()
            .BeLessThan(Array.IndexOf(executionEvents, "start:1"));
    }

    [Fact]
    public async Task RunEach_ShouldBoundCrossOwnerParallelism()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [0, 1, 2, 3, 4, 5];
        var owners = buckets.ToDictionary(static bucket => bucket, static bucket => $"owner-{bucket}");
        var firstTwoEntered = NewCompletionSource<bool>();
        var releaseCallbacks = NewCompletionSource<bool>();
        var current = 0;
        var maximum = 0;

        var execution = Task.Run(() => ShardQueryExecution.RunEach(
            source,
            buckets,
            owners,
            parallelism: 2,
            query =>
            {
                var active = Interlocked.Increment(ref current);
                UpdateMaximum(ref maximum, active);
                if (active == 2)
                    firstTwoEntered.TrySetResult(true);

                try
                {
                    releaseCallbacks.Task.GetAwaiter().GetResult();
                    return GetBucket(query);
                }
                finally
                {
                    Interlocked.Decrement(ref current);
                }
            }));

        try
        {
            await firstTwoEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseCallbacks.TrySetResult(true);
        }

        var results = await execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        results.Should().Equal(buckets);
        maximum.Should().Be(2);
    }

    [Fact]
    public async Task RunEach_ShouldUseOrdinalOwnerIdentity()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [0, 1];
        var owners = new Dictionary<int, string> { [0] = "shard-a", [1] = "SHARD-A" };
        var bothOwnersEntered = NewCompletionSource<bool>();
        var releaseCallbacks = NewCompletionSource<bool>();
        var current = 0;
        var maximum = 0;

        var execution = Task.Run(() => ShardQueryExecution.RunEach(
            source,
            buckets,
            owners,
            parallelism: 2,
            query =>
            {
                var active = Interlocked.Increment(ref current);
                UpdateMaximum(ref maximum, active);
                if (active == 2)
                    bothOwnersEntered.TrySetResult(true);

                try
                {
                    releaseCallbacks.Task.GetAwaiter().GetResult();
                    return GetBucket(query);
                }
                finally
                {
                    Interlocked.Decrement(ref current);
                }
            }));

        try
        {
            await bothOwnersEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseCallbacks.TrySetResult(true);
        }

        (await execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken))
            .Should().Equal(buckets);
        maximum.Should().Be(2);
    }

    [Fact]
    public async Task RunEachAsync_ShouldStoreResultsAtOriginalPositions()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [5, 2, 8, 1];
        var owners = new Dictionary<int, string>
        {
            [5] = "owner-a",
            [2] = "owner-b",
            [8] = "owner-a",
            [1] = "owner-c",
        };
        var expectedToken = TestContext.Current.CancellationToken;
        var receivedTokens = new ConcurrentBag<CancellationToken>();
        var results = new int[buckets.Length];

        await ShardQueryExecution.RunEachAsync(
            source,
            buckets,
            owners,
            parallelism: 3,
            (query, token) =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(GetBucket(query) * 10);
            },
            (position, value) => results[position] = value,
            expectedToken);

        results.Should().Equal(50, 20, 80, 10);
        receivedTokens.Should().HaveCount(buckets.Length)
            .And.OnlyContain(token => token == expectedToken);
    }

    [Fact]
    public async Task RunEachAndRunEachAsync_ShouldHandleEmptyInputWithoutInvokingCallbacks()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        var callbackCount = 0;
        var owners = new Dictionary<int, string>();

        ShardQueryExecution.RunEach(
            source,
            Array.Empty<int>(),
            owners,
            parallelism: null,
            _ =>
            {
                Interlocked.Increment(ref callbackCount);
                return 1;
            }).Should().BeEmpty();

        var results = new List<int>();
        await ShardQueryExecution.RunEachAsync(
            source,
            Array.Empty<int>(),
            owners,
            parallelism: null,
            static (_, _) => Task.FromResult(1),
            (_, value) => results.Add(value),
            TestContext.Current.CancellationToken);

        results.Should().BeEmpty();
        callbackCount.Should().Be(0);
    }

    [Fact]
    public async Task RunEachAsync_ShouldSkipCallbacksForPrecanceledToken()
    {
        using var context = new InMemoryDataContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new EntityBuilder<ProbeRow>(context);
        var calls = 0;
        var owners = new Dictionary<int, string> { [0] = "owner-a", [1] = "owner-b" };

        var act = () => ShardQueryExecution.RunEachAsync(
            source,
            [0, 1],
            owners,
            parallelism: 1,
            (_, _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(1);
            },
            static (_, _) => { },
            cancellation.Token).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

        await FluentActions.Awaiting(act).Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(0);
    }

    [Fact]
    public async Task RunEachAsync_ShouldCancelQueuedWorkAndForwardOriginalToken()
    {
        using var context = new InMemoryDataContext();
        using var cancellation = new CancellationTokenSource();
        var source = new EntityBuilder<ProbeRow>(context);
        var callbackEntered = NewCompletionSource<bool>();
        var releaseCallback = NewCompletionSource<bool>();
        var calls = new ConcurrentBag<int>();
        var receivedTokens = new ConcurrentBag<CancellationToken>();
        var owners = new Dictionary<int, string> { [0] = "owner-a", [1] = "owner-b" };

        var execution = ShardQueryExecution.RunEachAsync(
            source,
            [0, 1],
            owners,
            parallelism: 1,
            async (query, token) =>
            {
                calls.Add(GetBucket(query));
                receivedTokens.Add(token);
                callbackEntered.TrySetResult(true);
                await releaseCallback.Task.ConfigureAwait(false);
                return GetBucket(query);
            },
            static (_, _) => { },
            cancellation.Token);

        try
        {
            await callbackEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            cancellation.Cancel();
        }
        finally
        {
            releaseCallback.TrySetResult(true);
        }

        await FluentActions.Awaiting(() => execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<OperationCanceledException>();
        calls.Should().ContainSingle();
        receivedTokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
    }

    [Fact]
    public void RunEach_ShouldPreserveAggregateExceptionSemantics()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        var expected = new InvalidOperationException("sync failure");
        var owners = new Dictionary<int, string> { [0] = "owner-a", [1] = "owner-b" };

        var exception = FluentActions.Invoking(() => ShardQueryExecution.RunEach(
                source,
                [0, 1],
                owners,
                parallelism: 2,
                query => GetBucket(query) == 0 ? throw expected : 1))
            .Should().Throw<AggregateException>()
            .Which;

        exception.Flatten().InnerExceptions.Should().Contain(expected);
    }

    [Fact]
    public async Task RunEachAsync_ShouldUnwrapFailure_ReleaseGate_AndAwaitOtherShardWork()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        var expected = new InvalidOperationException("async failure");
        var firstCallbackEntered = NewCompletionSource<bool>();
        var failFirstCallback = NewCompletionSource<bool>();
        var secondCallbackEntered = NewCompletionSource<bool>();
        var releaseSecondCallback = NewCompletionSource<bool>();
        var secondCallbackCompleted = NewCompletionSource<bool>();
        var invocationCount = 0;
        var stored = new ConcurrentBag<int>();
        var owners = new Dictionary<int, string> { [0] = "owner-a", [1] = "owner-b" };

        async Task<int> Run(EntityBuilder<ProbeRow> query, CancellationToken _)
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                firstCallbackEntered.TrySetResult(true);
                await failFirstCallback.Task.ConfigureAwait(false);
                throw expected;
            }

            secondCallbackEntered.TrySetResult(true);
            await releaseSecondCallback.Task.ConfigureAwait(false);
            secondCallbackCompleted.TrySetResult(true);
            return GetBucket(query);
        }

        var execution = ShardQueryExecution.RunEachAsync(
            source,
            [0, 1],
            owners,
            parallelism: 1,
            Run,
            (position, value) => stored.Add(position * 10 + value),
            TestContext.Current.CancellationToken);

        try
        {
            await firstCallbackEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            failFirstCallback.TrySetResult(true);
            await secondCallbackEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            execution.IsCompleted.Should().BeFalse("the second shard callback is still suspended");
        }
        finally
        {
            releaseSecondCallback.TrySetResult(true);
        }

        var exception = await FluentActions.Awaiting(() => execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expected);
        secondCallbackCompleted.Task.IsCompleted.Should().BeTrue();
        invocationCount.Should().Be(2);
        stored.Should().ContainSingle();
    }

    [Fact]
    public async Task Facades_ShouldExecuteProjectedAndUnprojectedInMemoryQueries()
    {
        using var context = new InMemoryDataContext();
        ProbeRow[] data = [new() { Id = 4 }, new() { Id = 9 }];
        context.Data[typeof(ProbeRow)] = data;

        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [2, 0, 1];
        var owners = new Dictionary<int, string> { [2] = "owner-a", [0] = "owner-b", [1] = "owner-a" };
        var unprojected = new ShardQuery<ProbeRow>(source, buckets, owners, parallelism: 2);
        var projected = new ShardQuery<ProbeRow, int>(source, static row => row.Id * 10, buckets, owners, parallelism: 2);

        var rows = unprojected.ToList();
        rows.Should().HaveCount(3);
        foreach (var bucketRows in rows)
            bucketRows.Select(static row => row.Id).Should().Equal(4, 9);

        var projectedRows = projected.ToList();
        projectedRows.Should().HaveCount(3);
        foreach (var bucketRows in projectedRows)
            bucketRows.Should().Equal(40, 90);

        var asyncRows = await unprojected.ToListAsync(TestContext.Current.CancellationToken);
        asyncRows.Should().HaveCount(3);
        foreach (var bucketRows in asyncRows)
            bucketRows.Select(static row => row.Id).Should().Equal(4, 9);

        var asyncProjectedRows = await projected.ToListAsync(TestContext.Current.CancellationToken);
        asyncProjectedRows.Should().HaveCount(3);
        foreach (var bucketRows in asyncProjectedRows)
            bucketRows.Should().Equal(40, 90);

        var streamedRows = new List<int[]>();
        await foreach (var bucketRows in unprojected.AsAsyncEnumerable(TestContext.Current.CancellationToken))
            streamedRows.Add(bucketRows.Select(static row => row.Id).ToArray());
        streamedRows.Should().HaveCount(3);
        foreach (var bucketRows in streamedRows)
            bucketRows.Should().Equal(4, 9);

        var streamedProjectedRows = new List<int[]>();
        await foreach (var bucketRows in projected.AsAsyncEnumerable(TestContext.Current.CancellationToken))
            streamedProjectedRows.Add(bucketRows);
        streamedProjectedRows.Should().HaveCount(3);
        foreach (var bucketRows in streamedProjectedRows)
            bucketRows.Should().Equal(40, 90);
    }

    private static int GetBucket(EntityBuilder<ProbeRow> query)
        => int.Parse(query.MappingVariant.Key!, CultureInfo.InvariantCulture);

    private static TaskCompletionSource<T> NewCompletionSource<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void UpdateMaximum(ref int maximum, int value)
    {
        var observed = Volatile.Read(ref maximum);
        while (observed < value)
        {
            var prior = Interlocked.CompareExchange(ref maximum, value, observed);
            if (prior == observed)
                return;
            observed = prior;
        }
    }

    private sealed class ProbeRow
    {
        public int Id { get; set; }
    }
}
