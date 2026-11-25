using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

/// <summary>
/// Proves that a routed command executes on the connection of the shard that owns the bucket: two
/// separate SQLite databases stand in for two servers (same idea as two databases in one container).
/// </summary>
public sealed class ShardConnectionRoutingTests : IDisposable
{
    private const int BucketCount = 8;
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-sharding-" + Guid.NewGuid().ToString("N"));

    public ShardConnectionRoutingTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void RoutedWrite_ShouldLandInTheOwningShardDatabase()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantA, bucketA) = FindTenant(topology, "server-a");
        var (tenantB, bucketB) = FindTenant(topology, "server-b");

        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenantA, Name = "on-a" }).Insert().Should().Be(1);
        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 2, Tenant = tenantB, Name = "on-b" }).Insert().Should().Be(1);

        CountIn(topology, "server-a", bucketA).Should().Be(1L);
        CountIn(topology, "server-a", bucketB).Should().Be(0L);
        CountIn(topology, "server-b", bucketB).Should().Be(1L);
        CountIn(topology, "server-b", bucketA).Should().Be(0L);
    }

    [Fact]
    public void BulkInsert_WithVariant_ShouldWriteOnlyToTheSelectedShard()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenant, bucket) = FindTenant(topology, "server-b");
        var variant = context.GetShardRouter().GetScope(bucket);
        var rows = new[] { new ShardedRow { Id = 1, Tenant = tenant, Name = "bulk-b" } };

        FluentActions.Invoking(() => context.CreateBulkInsertBuilder<ShardedRow>())
            .Should().Throw<InvalidOperationException>().WithMessage("*bucket mapping variant*");
        context.CreateBulkInsertBuilder<ShardedRow>(new BulkInsertOptions { MappingVariant = variant })
            .Values(rows).BulkInsert().Should().Be(1);

        CountIn(topology, "server-b", bucket).Should().Be(1L);
        CountIn(topology, "server-a", bucket).Should().Be(0L);
    }

    [Fact]
    public void RoutedRead_ShouldUseTheOwningShardDatabase()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantA, bucketA) = FindTenant(topology, "server-a");
        var (tenantB, bucketB) = FindTenant(topology, "server-b");

        InsertRaw(topology, "server-a", bucketA, 1, tenantA, "on-a");
        InsertRaw(topology, "server-b", bucketB, 2, tenantB, "on-b");

        context.From<ShardedRow>().Where(row => row.Tenant == tenantA).Select(row => row.Name).ToList()
            .Should().Equal("on-a");
        context.From<ShardedRow>().Where(row => row.Tenant == tenantB).Select(row => row.Name).ToList()
            .Should().Equal("on-b");

        // The context's own connection (":memory:") holds no data: everything ran on the shard connections.
        CountInMemory(context).Should().Be(0L);
    }

    [Fact]
    public void PinnedHostId_ShouldBeTheShardIdAndOverruleTheBucket()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantB, bucketB) = FindTenant(topology, "server-b");
        // The row physically sits on server-a although its bucket is owned by server-b.
        InsertRaw(topology, "server-a", bucketB, 7, tenantB, "moved-to-a");

        using (context.PinHost(topology.Name, "server-a"))
        {
            context.From<ShardedRow>().Where(row => row.Tenant == tenantB).Select(row => row.Name).ToList()
                .Should().Equal("moved-to-a");
        }

        FluentActions.Invoking(() => context.PinHost(topology.Name, "server-c"))
            .Should().Throw<ArgumentException>().WithMessage("*has no host 'server-c'*");
    }

    [Fact]
    public void MultiRowInsert_ShouldWriteOneBucketPerStatement()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantA, bucketA) = FindTenant(topology, "server-a");
        var (tenantB, _) = FindTenant(topology, "server-b");
        var rows = new[]
        {
            new ShardedRow { Id = 1, Tenant = tenantA, Name = "a-1" },
            new ShardedRow { Id = 2, Tenant = tenantA, Name = "a-2" },
        };

        context.CreateInsertBuilder<ShardedRow>().Values(rows).Insert().Should().Be(2);
        CountIn(topology, "server-a", bucketA).Should().Be(2L);

        var mixed = new[] { rows[0], new ShardedRow { Id = 3, Tenant = tenantB, Name = "b" } };
        FluentActions.Invoking(() => context.CreateInsertBuilder<ShardedRow>().Values(mixed))
            .Should().Throw<InvalidOperationException>().WithMessage("*different buckets*");
    }

    [Fact]
    public void BatchPerBucket_ShouldRunEveryStepOnThatBucketShard()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantA, bucketA) = FindTenant(topology, "server-a");
        var rows = new[] { new ShardedRow { Id = 11, Tenant = tenantA, Name = "batched" } };

        var ids = context.CreateBatchBuilder()
            .Insert(context.CreateInsertBuilder<ShardedRow>().Values(rows))
            .Query(context.From<ShardedRow>(context.GetShardRouter().GetScope(bucketA))
                .Select(row => row.Id))
            .ToList();

        ids.Should().Equal(11);
        CountIn(topology, "server-a", bucketA).Should().Be(1L);
    }

    [Fact]
    public void ScalarFanOut_ShouldReturnOneValuePerBucket()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantA, bucketA) = FindTenant(topology, "server-a");
        var (tenantB, bucketB) = FindTenant(topology, "server-b");
        InsertRaw(topology, "server-a", bucketA, 1, tenantA, "on-a");
        InsertRaw(topology, "server-b", bucketB, 2, tenantB, "on-b");

        var counts = context.From<ShardedRow>().AcrossBuckets().Count();
        var any = context.From<ShardedRow>().AcrossBuckets().Any();

        counts.Should().HaveCount(BucketCount);
        counts.Sum().Should().Be(2);
        counts[bucketA].Should().Be(1);
        counts[bucketB].Should().Be(1);
        counts.Where((_, bucket) => bucket != bucketA && bucket != bucketB).Should().OnlyContain(count => count == 0);

        any.Should().HaveCount(BucketCount);
        any[bucketA].Should().BeTrue();
        any[bucketB].Should().BeTrue();
        any.Where((_, bucket) => bucket != bucketA && bucket != bucketB).Should().OnlyContain(value => !value);
    }

    [Fact]
    public void RemovingShard_ShouldKeepTheSameTables()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenantA, bucketA) = FindTenant(topology, "server-a");
        InsertRaw(topology, "server-a", bucketA, 1, tenantA, "kept");

        topology.AddShard("server-c", ConnectionString("c"));
        Seed(topology, "server-c");

        context.From<ShardedRow>().Where(row => row.Tenant == tenantA).Select(row => row.Name).ToList()
            .Should().Equal("kept");
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

    private ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(BucketCount, BucketPlacementMode.TableSuffix);
        topology.AddShard("server-a", ConnectionString("a"));
        topology.AddShard("server-b", ConnectionString("b"));
        return topology;
    }

    private string ConnectionString(string name) => $"Data Source={Path.Combine(_directory, name + ".db")}";

    private static SqliteDataContext CreateContext(ShardTopology topology)
        => new(new DataContextBuilder()
            .UseSharding(topology)
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));

    private static (string Tenant, int Bucket) FindTenant(ShardTopology topology, string shardId)
    {
        var router = new ShardRouter(topology);
        for (var index = 0; index < 10_000; index++)
        {
            var tenant = $"tenant-{index}";
            var bucket = int.Parse(
                router.ResolveShard(typeof(ShardedRow), [tenant]).Key!,
                System.Globalization.CultureInfo.InvariantCulture);
            if (router.GetShard(bucket) == shardId)
                return (tenant, bucket);
        }

        throw new InvalidOperationException($"No tenant mapped to shard '{shardId}'.");
    }

    private void Seed(ShardTopology topology, params string[] shardIds)
    {
        var targets = shardIds.Length == 0 ? topology.ShardIds : shardIds;
        foreach (var shardId in targets)
        {
            using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
            context.EnsureConnectionOpen();
            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                using var command = context.CreateCommand(
                    $"DROP TABLE IF EXISTS ShardedRow_{bucket}; " +
                    $"CREATE TABLE ShardedRow_{bucket} (Id INTEGER PRIMARY KEY, Tenant TEXT NOT NULL, Name TEXT NOT NULL);");
                command.ExecuteNonQuery();
            }
        }
    }

    private static void InsertRaw(
        ShardTopology topology, string shardId, int bucket, int id, string tenant, string name)
    {
        using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(
            $"INSERT INTO ShardedRow_{bucket} (Id, Tenant, Name) VALUES ({id}, '{tenant}', '{name}')");
        command.ExecuteNonQuery();
    }

    private static object? CountIn(ShardTopology topology, string shardId, int bucket)
    {
        using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand($"SELECT COUNT(*) FROM ShardedRow_{bucket}");
        return command.ExecuteScalar();
    }

    private static object? CountInMemory(SqliteDataContext context)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'ShardedRow_%'");
        return command.ExecuteScalar();
    }

    [ShardKey(nameof(Tenant))]
    private sealed class ShardedRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
