using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

/// <summary>
/// Proves the fan-out scheduling contract: buckets that share a shard run one after another on that
/// shard's connection, while different shards run at the same time, up to the configured parallelism.
/// Two SQLite files stand in for two servers; a probe interceptor simulates a slow server so overlapping
/// executions are observable.
/// </summary>
public sealed class ShardParallelismTests : IDisposable
{
    private const int BucketCount = 8;
    private static readonly TimeSpan FakeServerLatency = TimeSpan.FromMilliseconds(25);
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-sharding-parallel-" + Guid.NewGuid().ToString("N"));

    public ShardParallelismTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void FanOut_ShouldRunDifferentShardsInParallel()
    {
        var topology = CreateTopology();
        var probe = new ConcurrencyProbe(topology);
        using var context = CreateContext(topology, probe);
        Seed(topology);

        var counts = context.From<ShardedRow>().AcrossBuckets().Count();

        counts.Should().HaveCount(BucketCount);
        probe.MaxOverall.Should().BeGreaterThan(1, "different shards are queried at the same time");
        probe.MaxOverall.Should().BeLessThanOrEqualTo(topology.ShardIds.Count);
    }

    [Fact]
    public void FanOut_ShouldRunBucketsOfOneShardSequentially()
    {
        var topology = CreateTopology();
        var probe = new ConcurrencyProbe(topology);
        using var context = CreateContext(topology, probe);
        Seed(topology);

        _ = context.From<ShardedRow>().AcrossBuckets().Count();

        probe.MaxPerShard.Should().NotBeEmpty();
        probe.MaxPerShard.Values.Should().OnlyContain(max => max == 1,
            "buckets that share a shard run sequentially on that shard's single connection");
    }

    [Fact]
    public void FanOut_ShouldHonourSequentialParallelismSetting()
    {
        var topology = CreateTopology();
        var probe = new ConcurrencyProbe(topology);
        using var context = CreateContext(topology, probe);
        Seed(topology);

        _ = context.From<ShardedRow>().AcrossBuckets(parallelism: 1).Count();

        probe.MaxOverall.Should().Be(1, "parallelism 1 serializes the whole fan-out");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A pooled handle may still hold a file on this platform; the OS temp cleaner owns the rest.
        }
    }

    /// <summary>Four shards, each one a separate SQLite file standing in for its own server.</summary>
    private ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(BucketCount);
        for (var shard = 1; shard <= 4; shard++)
            topology.AddShard($"shard-{shard}", ConnectionString(shard.ToString(CultureInfo.InvariantCulture)));

        return topology;
    }

    private string ConnectionString(string name) => $"Data Source={Path.Combine(_directory, name + ".db")}";

    private static SqliteDataContext CreateContext(ShardTopology topology, IQueryInterceptor probe)
        => new("Data Source=:memory:", new DataContextBuilder().UseSharding(topology).AddInterceptor(probe));

    private void Seed(ShardTopology topology)
    {
        foreach (var shardId in topology.ShardIds)
        {
            using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
            context.EnsureConnectionOpen();
            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                using var command = context.CreateCommand(
                    $"DROP TABLE IF EXISTS ShardedRow_{bucket}; " +
                    $"CREATE TABLE ShardedRow_{bucket} (Id INTEGER PRIMARY KEY);");
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Records how many fan-out commands a shard executes at the same time. The latency is spent inside
    /// <see cref="IQueryInterceptor.CommandExecuting"/>, so a sequential schedule can never overlap while a
    /// parallel one does.
    /// </summary>
    private sealed class ConcurrencyProbe(ShardTopology topology) : IQueryInterceptor
    {
        private static readonly Regex BucketPattern = new(@"ShardedRow_(\d+)", RegexOptions.CultureInvariant);
        private readonly ShardRouter _router = new(topology);
        private readonly object _gate = new();
        private readonly Dictionary<string, int> _currentPerShard = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _maxPerShard = new(StringComparer.Ordinal);
        private int _current;
        private int _maxOverall;

        public int MaxOverall => Volatile.Read(ref _maxOverall);

        public IReadOnlyDictionary<string, int> MaxPerShard
        {
            get
            {
                lock (_gate)
                    return new Dictionary<string, int>(_maxPerShard, StringComparer.Ordinal);
            }
        }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            if (!TryResolveShard(command.CommandText, out var shardId))
                return;

            lock (_gate)
            {
                _maxOverall = Math.Max(_maxOverall, ++_current);
                _currentPerShard.TryGetValue(shardId, out var current);
                _currentPerShard[shardId] = ++current;
                _maxPerShard.TryGetValue(shardId, out var max);
                _maxPerShard[shardId] = Math.Max(max, current);
            }

            try
            {
                Thread.Sleep(FakeServerLatency);
            }
            finally
            {
                lock (_gate)
                {
                    _current--;
                    _currentPerShard[shardId]--;
                }
            }
        }

        private bool TryResolveShard(string? sql, out string shardId)
        {
            shardId = string.Empty;
            if (sql is null)
                return false;

            var match = BucketPattern.Match(sql);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var bucket))
                return false;

            shardId = _router.GetShard(bucket);
            return true;
        }
    }

    private sealed class ShardedRow
    {
        public int Id { get; set; }
    }
}
