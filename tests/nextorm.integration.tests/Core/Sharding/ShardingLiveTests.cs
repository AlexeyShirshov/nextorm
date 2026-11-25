using System.Globalization;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live sharding over one database server per shard. SQLite stands in for the servers with one throwaway
/// database file per shard, so a routed statement must reach the file its bucket belongs to.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteShardingLiveTests : IDisposable
{
    private const int BucketCount = 8;
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-sharding-live-" + Guid.NewGuid().ToString("N"));

    public SqliteShardingLiveTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void RoutedInsertPointReadUpdateDelete_ShouldTargetTheBucketTable()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenant1, bucket1) = FindTenant(topology, "shard-1");
        var (tenant2, bucket2) = FindTenant(topology, "shard-2");
        var (tenant3, bucket3) = FindTenant(topology, "shard-3");

        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenant1, Name = "one" }).Insert().Should().Be(1);
        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 2, Tenant = tenant2, Name = "two" }).Insert().Should().Be(1);
        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 3, Tenant = tenant3, Name = "three" }).Insert().Should().Be(1);

        context.From<ShardedRow>().Where(row => row.Tenant == tenant1).Select(row => row.Name).ToList()
            .Should().Equal("one");
        context.From<ShardedRow>().Where(row => row.Tenant == tenant2).Select(row => row.Name).ToList()
            .Should().Equal("two");

        context.CreateUpdateBuilder<ShardedRow>()
            .Set(row => row.Name, "two-updated")
            .Where(row => row.Tenant == tenant2)
            .Update().Should().Be(1);
        context.From<ShardedRow>().Where(row => row.Tenant == tenant2).Select(row => row.Name).ToList()
            .Should().Equal("two-updated");

        context.CreateDeleteBuilder<ShardedRow>()
            .Where(row => row.Tenant == tenant3)
            .Delete().Should().Be(1);

        // Each write reached the file of the shard that owns the bucket: no other shard holds a copy.
        CountRows(topology, "shard-3", $"SELECT COUNT(*) FROM ShardedRow_{bucket3}").Should().Be(0L);
        CountRows(topology, "shard-1", $"SELECT COUNT(*) FROM ShardedRow_{bucket1}").Should().Be(1L);
        CountRows(topology, "shard-2", $"SELECT COUNT(*) FROM ShardedRow_{bucket2}").Should().Be(1L);
    }

    [Fact]
    public void FluentShardKey_ShouldRouteInsertAndPointReadsToTheBucketTable()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenant, bucket) = FindTenant(topology, "shard-2");
        var router = context.GetShardRouter();
        router.IsSharded(typeof(FluentRow)).Should().BeFalse();

        var query = context.From<FluentRow>(map => map.Table(nameof(ShardedRow)).HasShardKey(x => x.Tenant));
        var row = new FluentRow { Id = 1, Tenant = tenant, Name = "fluent" };

        router.GetBucket(row).Should().Be(bucket);
        context.CreateInsertBuilder<FluentRow>().Values(row).Insert().Should().Be(1);
        query.Where(x => x.Tenant == tenant).Select(x => x.Name).ToList().Should().Equal("fluent");
        context.From<FluentRow>().WithShardKey(tenant).Select(x => x.Name).ToList().Should().Equal("fluent");
        CountRows(topology, "shard-2", $"SELECT COUNT(*) FROM ShardedRow_{bucket}").Should().Be(1L);
        FluentActions.Invoking(() => context.From<FluentRow>().Select(x => x.Name).ToList())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void AddingAShard_ShouldKeepTheBucketTableName()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenant, bucket) = FindTenant(topology, "shard-1");
        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenant, Name = "kept" }).Insert();

        topology.AddShard("shard-4", ConnectionString("shard-4"));
        Seed(topology, "shard-4");

        // A new server changes placement, never the unit of placement: the tenant keeps its bucket, so its
        // data keeps living under the same table name on the old server until the user moves it.
        ResolveBucket(topology, tenant).Should().Be(bucket);
        CountRows(topology, "shard-1", $"SELECT COUNT(*) FROM ShardedRow_{bucket}").Should().Be(1L);
    }

    [Fact]
    public void AcrossBuckets_ShouldReturnOneVectorPerBucket()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var tenants = new[] { FindTenant(topology, "shard-1"), FindTenant(topology, "shard-2"), FindTenant(topology, "shard-3") };
        for (var index = 0; index < tenants.Length; index++)
        {
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = index + 1, Tenant = tenants[index].Tenant, Name = $"row-{index + 1}" })
                .Insert();
        }

        var all = context.From<ShardedRow>().AcrossBuckets().Where(row => row.Id > 0).Select(row => row.Name).ToList();

        all.Should().HaveCount(BucketCount);
        for (var index = 0; index < tenants.Length; index++)
            all[tenants[index].Bucket].Should().Equal($"row-{index + 1}");

        var empty = all.Where((_, bucket) => tenants.All(tenant => tenant.Bucket != bucket));
        empty.Should().OnlyContain(vector => vector.Count == 0);
    }

    [Fact]
    public async Task AcrossBucketsAsync_ShouldStreamOneVectorPerBucket()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenant, bucket) = FindTenant(topology, "shard-1");
        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenant, Name = "row-1" }).Insert();

        var vectors = new List<string[]>();
        await foreach (var vector in context.From<ShardedRow>()
                           .AcrossBuckets()
                           .Where(row => row.Id > 0)
                           .Select(row => row.Name)
                           .AsAsyncEnumerable(TestContext.Current.CancellationToken))
        {
            vectors.Add([.. vector]);
        }

        vectors.Should().HaveCount(BucketCount);
        vectors[bucket].Should().Equal("row-1");
    }

    [Fact]
    public void ScalarFanOut_ShouldReturnOneValuePerBucket()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        var (tenant, bucket) = FindTenant(topology, "shard-2");
        context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenant, Name = "counted" }).Insert();

        var counts = context.From<ShardedRow>().AcrossBuckets().Count();
        var any = context.From<ShardedRow>().AcrossBuckets().Any();

        counts.Should().HaveCount(BucketCount);
        counts.Sum().Should().Be(1);
        counts[bucket].Should().Be(1);
        any.Should().HaveCount(BucketCount);
        any[bucket].Should().BeTrue();
        any.Count(static value => value).Should().Be(1);
    }

    [Fact]
    public void UnroutedAccess_ShouldFailBeforeSql()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        Seed(topology);

        FluentActions.Invoking(() => context.From<ShardedRow>().Where(row => row.Id > 0).Select(row => row.Id).ToList())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
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

    private SqliteDataContext CreateContext(ShardTopology topology)
    {
        var context = new SqliteDataContext(new DataContextBuilder()
            .UseSharding(topology)
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));
        context.EnsureConnectionOpen();
        return context;
    }

    private ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(BucketCount, BucketPlacementMode.TableSuffix);
        for (var shard = 1; shard <= 3; shard++)
            topology.AddShard($"shard-{shard}", ConnectionString($"shard-{shard}"));

        return topology;
    }

    private string ConnectionString(string shardId) => $"Data Source={Path.Combine(_directory, shardId + ".db")}";

    private static int ResolveBucket(ShardTopology topology, string tenant)
        => int.Parse(
            new ShardRouter(topology).ResolveShard(typeof(ShardedRow), [tenant]).Key!,
            CultureInfo.InvariantCulture);

    private static (string Tenant, int Bucket) FindTenant(ShardTopology topology, string desiredShard)
    {
        var router = new ShardRouter(topology);
        for (var index = 0; index < 10_000; index++)
        {
            var tenant = $"tenant-{index}";
            var bucket = int.Parse(
                router.ResolveShard(typeof(ShardedRow), [tenant]).Key!,
                CultureInfo.InvariantCulture);
            if (router.GetShard(bucket) == desiredShard)
                return (tenant, bucket);
        }

        throw new InvalidOperationException($"No tenant mapped to shard '{desiredShard}'.");
    }

    private void Seed(ShardTopology topology, params string[] shardIds)
    {
        var targets = shardIds.Length == 0 ? topology.ShardIds : shardIds;
        var router = new ShardRouter(topology);
        foreach (var shardId in targets)
        {
            using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
            context.EnsureConnectionOpen();
            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                if (router.GetShard(bucket) != shardId)
                    continue;

                using var command = context.CreateCommand(
                    $"DROP TABLE IF EXISTS ShardedRow_{bucket}; " +
                    $"CREATE TABLE ShardedRow_{bucket} (Id INTEGER PRIMARY KEY, Tenant TEXT NOT NULL, Name TEXT NOT NULL);");
                command.ExecuteNonQuery();
            }
        }
    }

    private static object? CountRows(ShardTopology topology, string shardId, string sql)
    {
        using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        return command.ExecuteScalar();
    }

    private sealed class FluentRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}

[ShardKey(nameof(Tenant))]
public sealed class ShardedRow
{
    public int Id { get; set; }
    public string Tenant { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
