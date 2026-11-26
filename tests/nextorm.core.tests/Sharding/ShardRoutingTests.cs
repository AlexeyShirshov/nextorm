using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

/// <summary>
/// Covers routing a sharded entity from a predicate, an instance and an explicit shard key, plus the
/// fail-fast guard that keeps an unrouted sharded access from reaching the default table.
/// </summary>
public sealed class ShardRoutingTests
{
    [Fact]
    public void PredicateEquality_ShouldRouteSelectToTheShardTable()
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "east");
        using var context = CreateContext(topology);

        var sql = PrepareSelect(context, tenant);

        sql.Should().Contain($"ShardedRow_{bucket}");
        sql.Should().Contain($"{bucket}_");
    }

    [Fact]
    public void ExplicitShardKey_ShouldRenderTheShardTable()
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "west");
        using var context = CreateContext(topology);

        var builder = context.From<ShardedRow>().WithShardKey(tenant).Select(row => row.Id);
        var sql = PrepareSelect(context, builder);

        sql.Should().Contain($"ShardedRow_{bucket}");
    }

    [Fact]
    public void ExplicitScope_ShouldNotBeOverriddenByPredicate()
    {
        var topology = CreateTopology();
        var (eastTenant, eastBucket) = FindTenant(topology, "east");
        var (westTenant, westBucket) = FindTenant(topology, "west");
        using var context = CreateContext(topology);

        var builder = context.From<ShardedRow>()
            .WithShardKey(westTenant)
            .Where(row => row.Tenant == eastTenant)
            .Select(row => row.Id);
        var sql = PrepareSelect(context, builder);

        sql.Should().Contain($"ShardedRow_{westBucket}");
        sql.Should().NotContain($"ShardedRow_{eastBucket}");
    }

    [Fact]
    public void SelectWithoutShardKey_ShouldFailBeforeSql()
    {
        using var context = CreateContext(CreateTopology());

        FluentActions.Invoking(() => PrepareSelect(context, context.From<ShardedRow>().Where(row => row.Id > 1).Select(row => row.Id)))
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void InsertInstance_ShouldRouteToTheShardTable()
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "east");
        using var context = CreateContext(topology);

        var sql = context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenant, Name = "row" })
            .ToSql();

        sql.Should().Contain($"ShardedRow_{bucket}");
    }

    [Fact]
    public void InsertMixedBuckets_ShouldThrowBeforeSql()
    {
        var topology = CreateTopology();
        var (eastTenant, _) = FindTenant(topology, "east");
        var (westTenant, _) = FindTenant(topology, "west");
        using var context = CreateContext(topology);
        var rows = new[]
        {
            new ShardedRow { Id = 1, Tenant = eastTenant, Name = "east" },
            new ShardedRow { Id = 2, Tenant = westTenant, Name = "west" },
        };

        FluentActions.Invoking(() => context.CreateInsertBuilder<ShardedRow>().Values(rows))
            .Should().Throw<InvalidOperationException>().WithMessage("*different buckets*");
    }

    [Fact]
    public void InsertWithoutEntityValues_ShouldFailAtBuild()
    {
        using var context = CreateContext(CreateTopology());
        var builder = context.CreateInsertBuilder<ShardedRow>().Value(row => row.Id, 1);

        FluentActions.Invoking(() => builder.ToSql())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void UpdatePredicate_ShouldRouteAndUnroutedUpdateShouldFail()
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "east");
        using var context = CreateContext(topology);

        var sql = context.CreateUpdateBuilder<ShardedRow>()
            .Set(row => row.Name, "updated")
            .Where(row => row.Tenant == tenant)
            .ToSql();
        sql.Should().Contain($"ShardedRow_{bucket}");

        FluentActions.Invoking(() => context.CreateUpdateBuilder<ShardedRow>()
                .Set(row => row.Name, "updated")
                .Where(row => row.Id > 1)
                .ToSql())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void DeleteAll_OnShardedEntity_ShouldFailBeforeSql()
    {
        using var context = CreateContext(CreateTopology());

        FluentActions.Invoking(() => context.CreateDeleteBuilder<ShardedRow>().All().ToSql())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void DeletePredicate_ShouldRouteAndUnroutedDeleteShouldFail()
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "west");
        using var context = CreateContext(topology);

        var sql = context.CreateDeleteBuilder<ShardedRow>()
            .Where(row => row.Tenant == tenant)
            .ToSql();
        sql.Should().Contain($"ShardedRow_{bucket}");

        FluentActions.Invoking(() => context.CreateDeleteBuilder<ShardedRow>()
                .Where(row => row.Id > 1)
                .ToSql())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void TruncateShardedWithoutScope_ShouldFail()
    {
        using var context = CreateContext(CreateTopology());

        FluentActions.Invoking(() => context.CreateTruncateBuilder<ShardedRow>().ToSql())
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Fact]
    public void DifferentShards_ShouldProduceIsolatedSqlAndParameters()
    {
        var topology = CreateTopology();
        var (eastTenant, eastBucket) = FindTenant(topology, "east");
        var (westTenant, westBucket) = FindTenant(topology, "west");
        using var context = CreateContext(topology);

        var eastSql = PrepareSelect(context, eastTenant);
        var westSql = PrepareSelect(context, westTenant);
        var eastAgain = PrepareSelect(context, eastTenant);

        eastSql.Should().NotBe(westSql);
        eastSql.Should().Contain($"ShardedRow_{eastBucket}");
        westSql.Should().Contain($"ShardedRow_{westBucket}");
        eastAgain.Should().Be(eastSql);
    }

    [Fact]
    public void GroupByBucket_ShouldReturnOneGroupPerBucketInBucketOrder()
    {
        var topology = CreateTopology();
        var router = new ShardRouter(topology);
        var (eastTenant, eastBucket) = FindTenant(topology, "east");
        var (westTenant, westBucket) = FindTenant(topology, "west");
        using var context = CreateContext(topology);
        var rows = new[]
        {
            new ShardedRow { Id = 1, Tenant = eastTenant, Name = "east-1" },
            new ShardedRow { Id = 2, Tenant = westTenant, Name = "west-1" },
            new ShardedRow { Id = 3, Tenant = eastTenant, Name = "east-2" },
        };

        var groups = context.GroupByBucket(rows);

        var buckets = new[] { eastBucket, westBucket }.Order().ToArray();
        groups.Select(group => group.Bucket).Should().Equal(buckets);
        groups.Select(group => group.Variant).Should().Equal(buckets.Select(router.GetScope));
        groups.Single(group => group.Bucket == eastBucket).Rows.Select(row => row.Name)
            .Should().Equal("east-1", "east-2");
        groups.Sum(group => group.Rows.Count).Should().Be(rows.Length);
    }

    [Fact]
    public void GroupByBucket_OfNoRows_ShouldReturnNoGroups()
    {
        using var context = CreateContext(CreateTopology());

        context.GroupByBucket(Array.Empty<ShardedRow>()).Should().BeEmpty();
    }

    [Fact]
    public void GroupByBucket_ForAnUnshardedType_ShouldFailFast()
    {
        using var context = CreateContext(CreateTopology());

        FluentActions.Invoking(() => context.GroupByBucket(new[] { new PlainRow() }))
            .Should().Throw<InvalidOperationException>().WithMessage("*shard key*");
    }

    [Fact]
    public void GroupByBucket_WithANullRow_ShouldNameItsIndex()
    {
        using var context = CreateContext(CreateTopology());
        var rows = new ShardedRow?[] { new() { Id = 1, Tenant = "tenant-1" }, null };

        FluentActions.Invoking(() => context.GroupByBucket(rows!))
            .Should().Throw<ArgumentException>().WithMessage("*index 1*");
    }

    [Fact]
    public void GroupByBucket_OfAnUnshardedContext_ShouldFailFast()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        FluentActions.Invoking(() => context.GroupByBucket(new[] { new ShardedRow() }))
            .Should().Throw<InvalidOperationException>().WithMessage("*no shard router*");
    }

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(4096, BucketPlacementMode.TableSuffix);
        topology.AddShard("east", "Server=east");
        topology.AddShard("west", "Server=west");
        return topology;
    }

    private static SqliteDataContext CreateContext(ShardTopology topology)
        => new(new DataContextBuilder()
            .UseSharding(topology)
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));

    private static (string Tenant, int Bucket) FindTenant(ShardTopology topology, string desiredShard)
    {
        var router = new ShardRouter(topology);
        for (var index = 0; index < 10_000; index++)
        {
            var tenant = $"tenant-{index}";
            var scope = router.ResolveShard(typeof(ShardedRow), [tenant]);
            var bucket = int.Parse(scope.Key!, System.Globalization.CultureInfo.InvariantCulture);
            if (router.GetShard(bucket) == desiredShard)
                return (tenant, bucket);
        }

        throw new InvalidOperationException($"No tenant mapped to shard '{desiredShard}'.");
    }

    private static string PrepareSelect(SqliteDataContext context, string tenant)
        => PrepareSelect(context, context.From<ShardedRow>().Where(row => row.Tenant == tenant).Select(row => row.Id));

    private static string PrepareSelect(SqliteDataContext context, QueryCommand<int> query)
    {
        var prepared = BoundQueryInvocation<int>.GetPlan(context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None))!;
        return prepared.DbCommand.CommandText;
    }

    [ShardKey(nameof(Tenant))]
    private sealed class ShardedRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PlainRow
    {
        public int Id { get; set; }
    }
}
