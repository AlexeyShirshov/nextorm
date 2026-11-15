using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Postgres.Tests.Sharding;

/// <summary>
/// Proves that shard routing renders the shard-specific table and schema on every supported dialect,
/// and that an unrouted sharded access fails before SQL on every dialect.
/// </summary>
public sealed class ShardingDialectTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void PredicateRoute_ShouldRenderTheShardTableOnEveryDialect(string provider)
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "east");
        using var context = CreateContext(provider, new DataContextBuilder().UseSharding(topology));

        var sql = PrepareSelect(context, tenant);

        sql.Should().Contain($"ShardedRow_{bucket}");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void ExplicitShardKey_ShouldRenderTheShardTableOnEveryDialect(string provider)
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "west");
        using var context = CreateContext(provider, new DataContextBuilder().UseSharding(topology));

        var sql = PrepareSelect(context, context.From<ShardedRow>().WithShardKey(tenant).Select(row => row.Id));

        sql.Should().Contain($"ShardedRow_{bucket}");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void UnroutedSelect_ShouldFailBeforeSqlOnEveryDialect(string provider)
    {
        using var context = CreateContext(provider, new DataContextBuilder().UseSharding(CreateTopology()));

        FluentActions.Invoking(() => PrepareSelect(context, context.From<ShardedRow>().Where(row => row.Id > 1).Select(row => row.Id)))
            .Should().Throw<InvalidOperationException>().WithMessage("*sharded*");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void InsertInstance_ShouldRenderTheShardTableOnEveryDialect(string provider)
    {
        var topology = CreateTopology();
        var (tenant, bucket) = FindTenant(topology, "east");
        using var context = CreateContext(provider, new DataContextBuilder().UseSharding(topology));

        var sql = context.CreateInsertBuilder<ShardedRow>()
            .Values(new ShardedRow { Id = 1, Tenant = tenant, Name = "row" })
            .ToSql();

        sql.Should().Contain($"ShardedRow_{bucket}");
    }

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(4096);
        topology.AddShard("east", "Server=east");
        topology.AddShard("west", "Server=west");
        return topology;
    }

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

    private static string PrepareSelect(DataContext context, string tenant)
        => PrepareSelect(context, context.From<ShardedRow>().Where(row => row.Tenant == tenant).Select(row => row.Id));

    private static string PrepareSelect(DataContext context, QueryCommand<int> query)
    {
        var prepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None);
        return prepared.DbCommand.CommandText;
    }

    private static DataContext CreateContext(string provider, DataContextBuilder builder)
        => provider switch
        {
            "sqlite" => new SqliteDataContext("Data Source=:memory:", builder),
            "postgres" => new PostgresDataContext("Host=localhost;Database=test;Username=test;Password=test", builder),
            "sqlserver" => new SqlServerDataContext("Server=localhost;Database=test;User Id=test;Password=test;TrustServerCertificate=True", builder),
            "mysql" => new MySqlDataContext("Server=localhost;Database=test;User=test;Password=test", builder),
            "clickhouse" => new ClickHouseDataContext("Host=localhost;Database=test;Username=test;Password=test", builder),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };

    [ShardKey(nameof(Tenant))]
    private sealed class ShardedRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
