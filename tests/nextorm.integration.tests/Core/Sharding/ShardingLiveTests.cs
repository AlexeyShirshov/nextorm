using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteShardingLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void RoutedInsertPointReadUpdateDelete_ShouldTargetTheShardTable()
    {
        var topology = CreateTopology();
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(Builder(topology));
        context.EnsureConnectionOpen();
        Seed(context, "s1", "s2", "s3");

        var (tenant1, shard1) = FindTenant(topology, "s1");
        var (tenant2, shard2) = FindTenant(topology, "s2");
        var (tenant3, shard3) = FindTenant(topology, "s3");

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

        CountRows(context, $"SELECT COUNT(*) FROM ShardedRow_{shard3}").Should().Be(0L);
        CountRows(context, $"SELECT COUNT(*) FROM ShardedRow_{shard1}").Should().Be(1L);
    }

    [Fact]
    public void AcrossShards_ShouldMergeRowsFromEverySelectedShard()
    {
        var topology = CreateTopology();
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(Builder(topology));
        context.EnsureConnectionOpen();
        Seed(context, "s1", "s2", "s3");

        var tenants = new[] { FindTenant(topology, "s1"), FindTenant(topology, "s2"), FindTenant(topology, "s3") };
        for (var index = 0; index < tenants.Length; index++)
        {
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = index + 1, Tenant = tenants[index].Tenant, Name = $"row-{index + 1}" })
                .Insert();
        }

        var all = context.From<ShardedRow>().AcrossShards().Where(row => row.Id > 0).Select(row => row.Name).ToList();
        all.Should().BeEquivalentTo("row-1", "row-2", "row-3");

        var subset = context.From<ShardedRow>().AcrossShards("s1", "s3").Select(row => row.Name).ToList();
        subset.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcrossShardsAsyncEnumerable_ShouldStreamEverySelectedShard()
    {
        var topology = CreateTopology();
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(Builder(topology));
        context.EnsureConnectionOpen();
        Seed(context, "s1", "s2", "s3");

        var tenants = new[] { FindTenant(topology, "s1"), FindTenant(topology, "s2"), FindTenant(topology, "s3") };
        for (var index = 0; index < tenants.Length; index++)
        {
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = index + 1, Tenant = tenants[index].Tenant, Name = $"row-{index + 1}" })
                .Insert();
        }

        var names = new List<string>();
        await foreach (var name in context.From<ShardedRow>().AcrossShards().Where(row => row.Id > 0).Select(row => row.Name).AsAsyncEnumerable(TestContext.Current.CancellationToken))
            names.Add(name);

        names.Should().BeEquivalentTo("row-1", "row-2", "row-3");

        var entities = new List<string>();
        await foreach (var row in context.From<ShardedRow>().AcrossShards("s1").AsAsyncEnumerable(TestContext.Current.CancellationToken))
            entities.Add(row.Name);

        entities.Should().Equal("row-1");
    }

    [Fact]
    public void UnroutedAccess_ShouldFailBeforeSql()
    {
        var topology = CreateTopology();
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(Builder(topology));
        context.EnsureConnectionOpen();
        Seed(context, "s1", "s2", "s3");

        FluentActions.Invoking(() => context.From<ShardedRow>().Where(row => row.Id > 0).Select(row => row.Id).ToList())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    private static DataContextBuilder Builder(ShardTopology topology)
        => new DataContextBuilder().UseSharding(topology);

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology();
        topology.AddShard("s1");
        topology.AddShard("s2");
        topology.AddShard("s3");
        return topology;
    }

    private static (string Tenant, string Shard) FindTenant(ShardTopology topology, string desiredShard)
    {
        var router = new ShardRouter(topology);
        for (var index = 0; index < 1000; index++)
        {
            var tenant = $"tenant-{index}";
            if (router.ResolveShard(typeof(ShardedRow), [tenant]).Key == desiredShard)
                return (tenant, desiredShard);
        }

        throw new InvalidOperationException($"No tenant mapped to shard '{desiredShard}'.");
    }

    private static void Seed(DataContext context, params string[] shards)
    {
        foreach (var shard in shards)
        {
            using var setup = context.CreateCommand(
                $"DROP TABLE IF EXISTS ShardedRow_{shard}; CREATE TABLE ShardedRow_{shard} (Id INTEGER PRIMARY KEY, Tenant TEXT NOT NULL, Name TEXT NOT NULL);");
            setup.ExecuteNonQuery();
        }
    }

    private static object? CountRows(DataContext context, string sql)
    {
        using var command = context.CreateCommand(sql);
        return command.ExecuteScalar();
    }
}

public sealed class PostgresShardingLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void AcrossShards_ShouldMergeRowsFromSchemaBasedShards()
    {
        var topology = CreateTopology();
        using var context = (DataContext)PostgresTestProvider.Instance.CreateContext(Builder(topology));
        context.EnsureConnectionOpen();
        Seed(context);

        var tenants = new[] { FindTenant(topology, "s1"), FindTenant(topology, "s2"), FindTenant(topology, "s3") };
        for (var index = 0; index < tenants.Length; index++)
        {
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = index + 1, Tenant = tenants[index].Tenant, Name = $"row-{index + 1}" })
                .Insert();
        }

        var all = context.From<ShardedRow>().AcrossShards().Where(row => row.Id > 0).Select(row => row.Name).ToList();
        all.Should().BeEquivalentTo("row-1", "row-2", "row-3");

        var pointTenant = tenants[1].Tenant;
        var point = context.From<ShardedRow>().Where(row => row.Tenant == pointTenant).Select(row => row.Name).ToList();
        point.Should().Equal("row-2");
    }

    private static DataContextBuilder Builder(ShardTopology topology)
        => new DataContextBuilder().UseSharding(topology);

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology();
        topology.AddShard("s1", "shard_live_s1");
        topology.AddShard("s2", "shard_live_s2");
        topology.AddShard("s3", "shard_live_s3");
        return topology;
    }

    private static (string Tenant, string Shard) FindTenant(ShardTopology topology, string desiredShard)
    {
        var router = new ShardRouter(topology);
        for (var index = 0; index < 1000; index++)
        {
            var tenant = $"tenant-{index}";
            if (router.ResolveShard(typeof(ShardedRow), [tenant]).Key == desiredShard)
                return (tenant, desiredShard);
        }

        throw new InvalidOperationException($"No tenant mapped to shard '{desiredShard}'.");
    }

    private static void Seed(DataContext context)
    {
        using var setup = context.CreateCommand(
            "DROP SCHEMA IF EXISTS shard_live_s1 CASCADE; DROP SCHEMA IF EXISTS shard_live_s2 CASCADE; DROP SCHEMA IF EXISTS shard_live_s3 CASCADE;" +
            "CREATE SCHEMA shard_live_s1; CREATE SCHEMA shard_live_s2; CREATE SCHEMA shard_live_s3;" +
            "CREATE TABLE shard_live_s1.shardedrow_s1 (id INTEGER PRIMARY KEY, tenant TEXT NOT NULL, name TEXT NOT NULL);" +
            "CREATE TABLE shard_live_s2.shardedrow_s2 (id INTEGER PRIMARY KEY, tenant TEXT NOT NULL, name TEXT NOT NULL);" +
            "CREATE TABLE shard_live_s3.shardedrow_s3 (id INTEGER PRIMARY KEY, tenant TEXT NOT NULL, name TEXT NOT NULL);");
        setup.ExecuteNonQuery();
    }
}

[ShardKey(nameof(Tenant))]
public sealed class ShardedRow
{
    public int Id { get; set; }
    public string Tenant { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
