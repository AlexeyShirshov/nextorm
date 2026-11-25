using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Routing;

/// <summary>
/// A batch is one SQL session and one round trip, so it runs on one host: the topology and the mapping
/// scope of its statements must agree, exactly like the sources of one statement. Covers the routed
/// batch (it executes on the topology host, not on the context connection), the two fail-closed cases
/// (two topologies, two scopes), a step without a rule riding along on the chosen host, a lazy temporary
/// table materialised on the shard it is read from, and a batch inside a routed transaction.
/// SQLite files stand in for the servers, as in <see cref="TopologyRoutingTests"/>.
/// </summary>
public sealed class BatchRoutingTests : IDisposable
{
    private const string DefaultTopology = "main";
    private const string CrmTopology = "crm";
    private const string BillingTopology = "billing";
    private const string ShardTopologyName = "sharding";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-batch-routing-" + Guid.NewGuid().ToString("N"));

    public BatchRoutingTests()
    {
        Directory.CreateDirectory(_directory);
        SeedAll();
    }

    [Fact]
    public void BatchOnARoutedEntity_ShouldRunOnTheRoutedHost()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        var names = context.CreateBatchBuilder()
            .Update(context.CreateUpdateBuilder<CrmRow>().Set(row => row.Name, "batched").Where(row => row.Id == 1))
            .Query(context.From<CrmRow>().Where(row => row.Id == 1).Select(row => row.Name))
            .ToList();

        names.Should().Equal("batched");
        // The write landed on the routed file, and the default one never saw the table.
        ReadNames(CrmConnectionString(), nameof(CrmRow)).Should().Equal("batched");
        TableExists(DefaultConnectionString(), nameof(CrmRow)).Should().BeFalse();
    }

    [Fact]
    public void BatchStepWithoutARoute_ShouldRideAlongOnTheChosenHost()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        var result = context.CreateBatchBuilder()
            .AddQuery(context.From<CrmRow>().Where(row => row.Id == 1).Select(row => row.Name))
            .AddQuery(context.From<PlainRow>().Where(row => row.Id == 1).Select(row => row.Name))
            .Execute();

        // The unrouted second query follows the routed first one to crm and reads the row that lives
        // there instead of the one in the default file.
        result.Read<string>().Should().Equal("routed");
        result.Read<string>().Should().Equal("routed-plain");
    }

    [Fact]
    public void BatchWithoutARoutedStep_ShouldStayOnTheContextConnection()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        var names = context.CreateBatchBuilder()
            .Query(context.From<PlainRow>().Where(row => row.Id == 1).Select(row => row.Name))
            .ToList();

        // No step is routed, so nothing selects the crm host: the batch runs on the context connection.
        names.Should().Equal("home");
    }

    [Fact]
    public void BatchRoutedElsewhereInsideAContextTransaction_ShouldFail()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        using var transaction = ((ITransactionManager)context).BeginTransaction();

        FluentActions.Invoking(() => context.CreateBatchBuilder()
                .Update(context.CreateUpdateBuilder<CrmRow>().Set(row => row.Name, "x").Where(row => row.Id == 1))
                .Query(context.From<CrmRow>().Select(row => row.Name))
                .ToSql())
            .Should().NotThrow();

        FluentActions.Invoking(() => context.CreateBatchBuilder()
                .Update(context.CreateUpdateBuilder<CrmRow>().Set(row => row.Name, "x").Where(row => row.Id == 1))
                .Query(context.From<CrmRow>().Select(row => row.Name))
                .ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*routes this command to another host, but a transaction is open on the context connection*");
    }

    [Fact]
    public void BatchSpanningTwoTopologies_ShouldFail()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString()))
            .AddTopology(new FileTopology(BillingTopology, BillingConnectionString())));

        FluentActions.Invoking(() => context.CreateBatchBuilder()
                .Update(context.CreateUpdateBuilder<BillingRow>().Set(row => row.Name, "x").Where(row => row.Id == 1))
                .Query(context.From<CrmRow>().Select(row => row.Name))
                .ToSql())
            .Should().Throw<NotSupportedException>()
            .WithMessage($"*span topologies '{BillingTopology}' and '{CrmTopology}'*one SQL session*");
    }

    [Fact]
    public void BatchSpanningTwoScopes_ShouldFail()
    {
        using var context = CreateShardedContext();

        FluentActions.Invoking(() => context.CreateBatchBuilder()
                .AddQuery(context.From<ShardRow>(Scope(3)).Select(row => row.Id))
                .AddQuery(context.From<ShardRow>(Scope(5)).Select(row => row.Id))
                .ToSql())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*span the mapping variants '3' and '5'*one scope*");
    }

    [Fact]
    public void BatchInsideARoutedTransaction_ShouldRunOnTheTransactionHostAndRollBack()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        using (var transaction = context.BeginTransaction(new RouteTarget(CrmTopology)))
        {
            context.CreateBatchBuilder()
                .Update(context.CreateUpdateBuilder<CrmRow>().Set(row => row.Name, "in-transaction").Where(row => row.Id == 1))
                .Query(context.From<CrmRow>().Where(row => row.Id == 1).Select(row => row.Name))
                .ToList()
                .Should().Equal("in-transaction");

            transaction.Rollback();
        }

        ReadNames(CrmConnectionString(), nameof(CrmRow)).Should().Equal("routed");
    }

    [Fact]
    public void TemporaryTableRead_ShouldMaterialiseAndReadOnTheRoutedShard()
    {
        using var context = CreateShardedContext();
        var scope = Scope(3);

        // The temporary table is a batch (drop, create as select, read) that runs on one session: the
        // shard the bucket belongs to, where the source rows live.
        var source = context.From<ShardRow>(scope)
            .Select(row => new { row.Id, row.Name })
            .AsTempTable();

        var rows = context.From(source, scope)
            .OrderBy(row => row.GetInt32("Id"))
            .Select(row => row.GetString("Name"))
            .ToList();

        rows.Should().Equal("shard-a", "shard-b");
    }

    [Fact]
    public void TemporaryTableRead_ShouldRejectASourceOfAnotherBucket()
    {
        using var context = CreateShardedContext();
        var otherTenant = TenantForBucket(5);

        // A sharded source resolves its own bucket from the predicate; materialising it for bucket 5 and
        // reading it as bucket 3 would put one statement of the batch on the other shard's placement.
        var source = context.From<ShardRow>()
            .Where(row => row.Tenant == otherTenant)
            .Select(row => new { row.Id, row.Name })
            .AsTempTable();

        FluentActions.Invoking(() => context.From(source, Scope(3))
                .Select(row => row.GetInt32("Id"))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*mapping variants '5' and '3'*source query*");
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

    private SqliteDataContext CreateContext(Action<DataRoutingBuilder> configure)
    {
        var builder = new DataContextBuilder().UseDataRouting(routing =>
        {
            routing.UseDefaultTopology(DefaultTopology, DefaultConnectionString());
            configure(routing);
        });

        return new SqliteDataContext(builder);
    }

    private SqliteDataContext CreateShardedContext()
    {
        var topology = new ShardTopology(bucketCount: 8, BucketPlacementMode.TableSuffix);
        topology.AddShard("only", ShardConnectionString());

        return new SqliteDataContext(new DataContextBuilder()
            .UseSharding(topology)
            .UseDataRouting(routing => routing.UseDefaultTopology(DefaultTopology, DefaultConnectionString())));
    }

    private static global::NextORM.Core.MappingVariant Scope(int bucket)
        => new(bucket.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private string DefaultConnectionString() => $"Data Source={Path.Combine(_directory, "main.db")}";

    private string CrmConnectionString() => $"Data Source={Path.Combine(_directory, "crm.db")}";

    private string BillingConnectionString() => $"Data Source={Path.Combine(_directory, "billing.db")}";

    private string ShardConnectionString() => $"Data Source={Path.Combine(_directory, "shard.db")}";

    /// <summary>Finds a tenant whose shard-key hash lands on <paramref name="bucket"/>.</summary>
    private static string TenantForBucket(int bucket)
    {
        var topology = new ShardTopology(bucketCount: 8, BucketPlacementMode.TableSuffix);
        topology.AddShard("only", "Data Source=:memory:");
        var router = new ShardRouter(topology);

        for (var index = 0; index < 10_000; index++)
        {
            var tenant = $"tenant-{index}";
            var resolved = int.Parse(
                router.ResolveShard(typeof(ShardRow), [tenant]).Key!,
                System.Globalization.CultureInfo.InvariantCulture);
            if (resolved == bucket)
                return tenant;
        }

        throw new InvalidOperationException($"No tenant mapped to bucket {bucket}.");
    }

    private void SeedAll()
    {
        Seed(CrmConnectionString(),
            Table(nameof(CrmRow)) + Insert(nameof(CrmRow), 1, "routed") +
            Table(nameof(PlainRow)) + Insert(nameof(PlainRow), 1, "routed-plain"));

        Seed(BillingConnectionString(), Table(nameof(BillingRow)) + Insert(nameof(BillingRow), 1, "billing"));

        // The default file exists but deliberately has no CrmRow table.
        Seed(DefaultConnectionString(), Table(nameof(PlainRow)) + Insert(nameof(PlainRow), 1, "home"));

        // Only bucket 3 exists on the shard: the temporary-table read is routed by the bucket scope.
        Seed(ShardConnectionString(),
            "CREATE TABLE ShardRow_3 (Id INTEGER PRIMARY KEY, Tenant TEXT NOT NULL, Name TEXT NOT NULL); " +
            "INSERT INTO ShardRow_3 (Id, Tenant, Name) VALUES (1, 'tenant-1', 'shard-a'), (2, 'tenant-2', 'shard-b');");
    }

    private static void Seed(string connectionString, string sql)
    {
        using var context = new SqliteDataContext(connectionString, new DataContextBuilder());
        context.EnsureConnectionOpen();
        context.CreateCommand(sql).ExecuteNonQuery();
    }

    private static bool TableExists(string connectionString, string table)
    {
        using var context = new SqliteDataContext(connectionString, new DataContextBuilder());
        context.EnsureConnectionOpen();

        using var command = context.CreateCommand(
            $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'");
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static List<string> ReadNames(string connectionString, string table)
    {
        using var context = new SqliteDataContext(connectionString, new DataContextBuilder());
        context.EnsureConnectionOpen();

        using var command = context.CreateCommand($"SELECT Name FROM {table} ORDER BY Id");
        using var reader = command.ExecuteReader();

        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));

        return names;
    }

    private static string Table(string name)
        => $"DROP TABLE IF EXISTS {name}; CREATE TABLE {name} (Id INTEGER PRIMARY KEY, Name TEXT); ";

    private static string Insert(string table, int id, string name)
        => $"INSERT INTO {table} (Id, Name) VALUES ({id}, '{name}'); ";

    /// <summary>One host, always selected: a single-server topology.</summary>
    private sealed class FileTopology(string name, string connectionString) : IHostTopology
    {
        public string Name => name;

        public bool TryGetHost(in TopologyContext context, out HostTarget host)
        {
            host = new HostTarget("only", connectionString);
            return true;
        }
    }

    [RouteTo(CrmTopology)]
    public sealed class CrmRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [RouteTo(BillingTopology)]
    public sealed class BillingRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class PlainRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [ShardKey(nameof(Tenant))]
    public sealed class ShardRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
