using System.Collections.Concurrent;
using System.Globalization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding.FanOut;

public sealed class ShardQueryAsyncExecutionTests
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunEachAsync_ShouldKeepSameOwnerBucketsSequentialWhileOwnersOverlap()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [10, 20, 11, 12];
        var owners = new Dictionary<int, string>
        {
            [10] = "owner-a",
            [20] = "owner-b",
            [11] = "owner-a",
            [12] = "owner-a",
        };
        var aFirstEntered = NewSignal();
        var bEntered = NewSignal();
        var aSecondEntered = NewSignal();
        var releaseAFirst = NewSignal();
        var releaseB = NewSignal();
        var releaseASecond = NewSignal();
        var events = new ConcurrentQueue<string>();
        var activeByOwner = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var maxByOwner = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var currentOverall = 0;
        var maxOverall = 0;
        var results = new int[buckets.Length];

        async Task<int> Run(EntityBuilder<ProbeRow> query, CancellationToken _)
        {
            var bucket = GetBucket(query);
            var owner = owners[bucket];
            var activeForOwner = activeByOwner.AddOrUpdate(owner, 1, static (_, current) => current + 1);
            maxByOwner.AddOrUpdate(owner, activeForOwner, (_, current) => Math.Max(current, activeForOwner));
            UpdateMaximum(ref maxOverall, Interlocked.Increment(ref currentOverall));
            events.Enqueue($"start:{bucket}");

            try
            {
                switch (bucket)
                {
                    case 10:
                        aFirstEntered.TrySetResult(true);
                        await releaseAFirst.Task.ConfigureAwait(false);
                        break;
                    case 20:
                        bEntered.TrySetResult(true);
                        await releaseB.Task.ConfigureAwait(false);
                        break;
                    case 11:
                        aSecondEntered.TrySetResult(true);
                        await releaseASecond.Task.ConfigureAwait(false);
                        break;
                }

                return bucket;
            }
            finally
            {
                events.Enqueue($"end:{bucket}");
                activeByOwner.AddOrUpdate(owner, 0, static (_, current) => current - 1);
                Interlocked.Decrement(ref currentOverall);
            }
        }

        var execution = ShardQueryExecution.RunEachAsync(
            source,
            buckets,
            owners,
            parallelism: 2,
            Run,
            (position, value) => results[position] = value,
            TestContext.Current.CancellationToken);

        try
        {
            await aFirstEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            await bEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

            maxOverall.Should().Be(2, "the first owner-a and owner-b callbacks are both gated");
            maxByOwner["owner-a"].Should().Be(1);
            aSecondEntered.Task.IsCompleted.Should().BeFalse();

            releaseAFirst.TrySetResult(true);
            await aSecondEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            releaseASecond.TrySetResult(true);
            releaseB.TrySetResult(true);

            await execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseAFirst.TrySetResult(true);
            releaseB.TrySetResult(true);
            releaseASecond.TrySetResult(true);
        }

        results.Should().Equal(10, 20, 11, 12);
        maxOverall.Should().Be(2);
        maxByOwner.Values.Should().OnlyContain(maximum => maximum == 1);

        var executionEvents = events.ToArray();
        Array.IndexOf(executionEvents, "end:10").Should()
            .BeLessThan(Array.IndexOf(executionEvents, "start:11"));
        Array.IndexOf(executionEvents, "end:11").Should()
            .BeLessThan(Array.IndexOf(executionEvents, "start:12"));
    }

    [Fact]
    public async Task RunEachAsync_ShouldReachConfiguredCrossOwnerConcurrencyAndRespectTheLimit()
    {
        using var context = new InMemoryDataContext();
        var source = new EntityBuilder<ProbeRow>(context);
        int[] buckets = [0, 1, 2, 3, 4];
        var owners = buckets.ToDictionary(static bucket => bucket, static bucket => $"owner-{bucket}");
        var firstTwoEntered = NewSignal();
        var releaseCallbacks = NewSignal();
        var currentOverall = 0;
        var maxOverall = 0;
        var results = new int[buckets.Length];

        async Task<int> Run(EntityBuilder<ProbeRow> query, CancellationToken _)
        {
            var active = Interlocked.Increment(ref currentOverall);
            UpdateMaximum(ref maxOverall, active);
            if (active == 2)
                firstTwoEntered.TrySetResult(true);

            try
            {
                await releaseCallbacks.Task.ConfigureAwait(false);
                return GetBucket(query);
            }
            finally
            {
                Interlocked.Decrement(ref currentOverall);
            }
        }

        var execution = ShardQueryExecution.RunEachAsync(
            source,
            buckets,
            owners,
            parallelism: 2,
            Run,
            (position, value) => results[position] = value,
            TestContext.Current.CancellationToken);

        try
        {
            await firstTwoEntered.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseCallbacks.TrySetResult(true);
        }

        await execution.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

        results.Should().Equal(buckets);
        maxOverall.Should().Be(2);
    }

    private static int GetBucket(EntityBuilder<ProbeRow> query)
        => int.Parse(query.MappingVariant.Key!, CultureInfo.InvariantCulture);

    private static TaskCompletionSource<bool> NewSignal()
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
