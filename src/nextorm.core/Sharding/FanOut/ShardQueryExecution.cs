using System.Globalization;

namespace NextORM.Core;

/// <summary>Shared bucket grouping and execution policy for both shard-query facades.</summary>
internal static class ShardQueryExecution
{
    internal static EntityBuilder<TEntity> Scoped<TEntity>(EntityBuilder<TEntity> source, int bucket)
        => source.CopyWithMappingVariant(new MappingVariant(bucket.ToString(CultureInfo.InvariantCulture)));

    internal static TResult[] RunEach<TEntity, TResult>(
        EntityBuilder<TEntity> source,
        IReadOnlyList<int> buckets,
        IReadOnlyDictionary<int, string> bucketOwners,
        int? parallelism,
        Func<EntityBuilder<TEntity>, TResult> run)
    {
        var results = new TResult[buckets.Count];
        var groups = BucketsByShard(buckets, bucketOwners);
        var options = new ParallelOptions { MaxDegreeOfParallelism = DegreeOfParallelism(parallelism, groups.Count) };
        Parallel.ForEach(groups, options, group =>
        {
            foreach (var (bucket, position) in group)
                results[position] = run(Scoped(source, bucket));
        });

        return results;
    }

    internal static async Task RunEachAsync<TEntity, TResult>(
        EntityBuilder<TEntity> source,
        IReadOnlyList<int> buckets,
        IReadOnlyDictionary<int, string> bucketOwners,
        int? parallelism,
        Func<EntityBuilder<TEntity>, CancellationToken, Task<TResult>> run,
        Action<int, TResult> store,
        CancellationToken cancellationToken)
    {
        var groups = BucketsByShard(buckets, bucketOwners);
        using var gate = new SemaphoreSlim(DegreeOfParallelism(parallelism, groups.Count));
        var tasks = new List<Task>(groups.Count);
        foreach (var group in groups)
        {
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    foreach (var (bucket, position) in group)
                    {
                        var value = await run(Scoped(source, bucket), cancellationToken).ConfigureAwait(false);
                        store(position, value);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    internal static int DegreeOfParallelism(int? parallelism, int shardCount)
        => Math.Clamp(parallelism ?? shardCount, 1, Math.Max(1, shardCount));

    private static List<List<(int Bucket, int Position)>> BucketsByShard(
        IReadOnlyList<int> buckets,
        IReadOnlyDictionary<int, string> bucketOwners)
    {
        var groups = new List<List<(int Bucket, int Position)>>();
        var shardIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var position = 0; position < buckets.Count; position++)
        {
            var bucket = buckets[position];
            var shardId = bucketOwners[bucket];
            if (!shardIndex.TryGetValue(shardId, out var groupIndex))
            {
                groupIndex = groups.Count;
                shardIndex.Add(shardId, groupIndex);
                groups.Add([]);
            }

            groups[groupIndex].Add((bucket, position));
        }

        return groups;
    }
}
