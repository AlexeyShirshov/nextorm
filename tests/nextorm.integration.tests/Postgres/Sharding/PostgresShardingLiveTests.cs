using System.Globalization;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Npgsql;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live PostgreSQL sharding: one container, two databases (<c>nextorm_shard_a</c>,
/// <c>nextorm_shard_b</c>) standing in for two servers. Every bucket table exists in both databases, so a
/// row present in only one of them proves that the statement ran on the connection of the shard that owns
/// the bucket. The scratch databases are dropped again, because every other class of the shared container
/// expects the catalog of the container as it was created.
/// </summary>
[Collection("Postgres")]
public sealed class PostgresShardingLiveTests : ProviderTestSuite
{
    private const int BucketCount = 8;
    private const string ShardADatabase = "nextorm_shard_a";
    private const string ShardBDatabase = "nextorm_shard_b";

    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void RoutedWrites_ShouldLandInTheOwningShardDatabase()
    {
        try
        {
            var topology = CreateTopology();
            using var context = CreateContext(topology);
            Seed(topology);

            var (tenantA, bucketA) = FindTenant(topology, "shard-a");
            var (tenantB, bucketB) = FindTenant(topology, "shard-b");

            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = 1, Tenant = tenantA, Name = "on-a" }).Insert().Should().Be(1);
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = 2, Tenant = tenantB, Name = "on-b" }).Insert().Should().Be(1);

            CountIn(ShardADatabase, bucketA).Should().Be(1L);
            CountIn(ShardADatabase, bucketB).Should().Be(0L);
            CountIn(ShardBDatabase, bucketB).Should().Be(1L);
            CountIn(ShardBDatabase, bucketA).Should().Be(0L);

            // A routed point read resolves the shard from the shard key and reads that database only.
            context.From<ShardedRow>().Where(row => row.Tenant == tenantA).Select(row => row.Name).ToList()
                .Should().Equal("on-a");
            context.From<ShardedRow>().Where(row => row.Tenant == tenantB).Select(row => row.Name).ToList()
                .Should().Equal("on-b");
        }
        finally
        {
            DropShardDatabases();
        }
    }

    [Fact]
    public void AcrossBuckets_ShouldReturnOneVectorPerBucket()
    {
        try
        {
            var topology = CreateTopology();
            using var context = CreateContext(topology);
            Seed(topology);

            var (tenantA, bucketA) = FindTenant(topology, "shard-a");
            var (tenantB, bucketB) = FindTenant(topology, "shard-b");
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = 1, Tenant = tenantA, Name = "row-a" }).Insert();
            context.CreateInsertBuilder<ShardedRow>()
                .Values(new ShardedRow { Id = 2, Tenant = tenantB, Name = "row-b" }).Insert();

            var vectors = context.From<ShardedRow>().AcrossBuckets().Select(row => row.Name).ToList();

            vectors.Should().HaveCount(BucketCount);
            vectors[bucketA].Should().Equal("row-a");
            vectors[bucketB].Should().Equal("row-b");
            vectors.Where((_, bucket) => bucket != bucketA && bucket != bucketB)
                .Should().OnlyContain(vector => vector.Count == 0);

            var counts = context.From<ShardedRow>().AcrossBuckets().Count();
            var any = context.From<ShardedRow>().AcrossBuckets().Any();

            counts.Should().HaveCount(BucketCount);
            counts.Sum().Should().Be(2);
            counts[bucketA].Should().Be(1);
            counts[bucketB].Should().Be(1);
            any[bucketA].Should().BeTrue();
            any[bucketB].Should().BeTrue();
            any.Count(static value => value).Should().Be(2);
        }
        finally
        {
            DropShardDatabases();
        }
    }

    private static PostgresDataContext CreateContext(ShardTopology topology)
    {
        var context = new PostgresDataContext(
            PostgresContainer.ConnectionString, new DataContextBuilder().UseSharding(topology));
        context.EnsureConnectionOpen();
        return context;
    }

    /// <summary>Two shards, one container: each shard is a database of the same server.</summary>
    private static ShardTopology CreateTopology()
    {
        EnsureDatabase(ShardADatabase);
        EnsureDatabase(ShardBDatabase);

        var topology = new ShardTopology(BucketCount);
        topology.AddShard("shard-a", WithDatabase(ShardADatabase));
        topology.AddShard("shard-b", WithDatabase(ShardBDatabase));
        return topology;
    }

    /// <summary>Creates <paramref name="database"/> on the container server when it does not exist yet.</summary>
    private static void EnsureDatabase(string database)
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();

        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
            probe.Parameters.AddWithValue("name", database);
            if (probe.ExecuteScalar() is not null)
                return;
        }

        using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{database}\"";
        try
        {
            create.ExecuteNonQuery();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // A concurrently scheduled class created it first; the database is what this test needs.
        }
    }

    /// <summary>
    /// Removes the two scratch databases so the shared container keeps the catalog state every other class
    /// starts from. Pooled handles are dropped first, and <c>WITH (FORCE)</c> ends anything still connected.
    /// </summary>
    private static void DropShardDatabases()
    {
        NpgsqlConnection.ClearAllPools();
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        foreach (var database in new[] { ShardADatabase, ShardBDatabase })
        {
            using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
            drop.ExecuteNonQuery();
        }
    }

    private static string WithDatabase(string database)
        => new NpgsqlConnectionStringBuilder(PostgresContainer.ConnectionString) { Database = database }.ConnectionString;

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

    /// <summary>Creates every bucket table in both databases, so a missing row is a routing fact.</summary>
    private static void Seed(ShardTopology topology)
    {
        foreach (var database in new[] { ShardADatabase, ShardBDatabase })
        {
            using var context = new PostgresDataContext(
                WithDatabase(database), new DataContextBuilder());
            context.EnsureConnectionOpen();

            for (var bucket = 0; bucket < BucketCount; bucket++)
                context.CreateCommand($"DROP TABLE IF EXISTS ShardedRow_{bucket}").ExecuteNonQuery();

            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                using var create = context.CreateCommand(
                    $"CREATE TABLE ShardedRow_{bucket} (Id INTEGER PRIMARY KEY, Tenant TEXT NOT NULL, Name TEXT NOT NULL)");
                create.ExecuteNonQuery();
            }
        }
    }

    private static object? CountIn(string database, int bucket)
    {
        using var context = new PostgresDataContext(WithDatabase(database), new DataContextBuilder());
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand($"SELECT COUNT(*) FROM ShardedRow_{bucket}");
        return command.ExecuteScalar();
    }
}
