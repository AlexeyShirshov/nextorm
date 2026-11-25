using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Routing;

/// <summary>
/// A materialisation writes a literal target name and reads its source query, so it belongs to the host
/// (and the scope) of that query, not to the projected row type: <c>ToTable</c>/<c>ToTempTable</c> on a
/// routed entity, or on a bucket-scoped one, creates — and with <c>DropExisting</c> first drops — the
/// table on the routed host. SQLite files stand in for the servers, as in
/// <see cref="TopologyRoutingTests"/>.
/// </summary>
public sealed class MaterializationRoutingTests : IDisposable
{
    private const string DefaultTopology = "main";
    private const string CrmTopology = "crm";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-materialization-routing-" + Guid.NewGuid().ToString("N"));

    public MaterializationRoutingTests()
    {
        Directory.CreateDirectory(_directory);
        SeedAll();
    }

    [Fact]
    public void ToTableOnARoutedEntity_ShouldCreateOnTheRoutedHost()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        context.From<CrmRow>().Select(row => new { row.Id, row.Name }).ToTable("crm_copy");

        TableExists(CrmConnectionString(), "crm_copy").Should().BeTrue();
        TableExists(DefaultConnectionString(), "crm_copy").Should().BeFalse();
        ReadNames(CrmConnectionString(), "crm_copy").Should().Equal("routed");
    }

    [Fact]
    public void ToTableWithDropExisting_ShouldDropAndCreateOnTheSameHost()
    {
        // A decoy of the same name on the default host must survive: the drop follows the create, and
        // both belong to the source's host.
        Seed(DefaultConnectionString(), "CREATE TABLE crm_copy (Id INTEGER PRIMARY KEY, Name TEXT); " +
                                       "INSERT INTO crm_copy (Id, Name) VALUES (7, 'decoy');");
        Seed(CrmConnectionString(), "CREATE TABLE crm_copy (Id INTEGER PRIMARY KEY, Name TEXT); " +
                                    "INSERT INTO crm_copy (Id, Name) VALUES (8, 'stale');");

        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        context.From<CrmRow>()
            .Select(row => new { row.Id, row.Name })
            .ToTable("crm_copy", new CreateTableOptions { DropExisting = true });

        ReadNames(CrmConnectionString(), "crm_copy").Should().Equal("routed");
        ReadNames(DefaultConnectionString(), "crm_copy").Should().Equal("decoy");
    }

    [Fact]
    public void ToTableOnABucketScopedSource_ShouldCreateOnTheShard()
    {
        using var context = CreateShardedContext();
        var scope = new global::NextORM.Core.MappingVariant("3");

        context.From<ShardRow>(scope)
            .Select(row => new { row.Id, row.Name })
            .ToTable("bucket_copy");

        TableExists(ShardConnectionString(), "bucket_copy").Should().BeTrue();
        TableExists(DefaultConnectionString(), "bucket_copy").Should().BeFalse();
        ReadNames(ShardConnectionString(), "bucket_copy").Should().Equal("shard-a", "shard-b");
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

    private string DefaultConnectionString() => $"Data Source={Path.Combine(_directory, "main.db")}";

    private string CrmConnectionString() => $"Data Source={Path.Combine(_directory, "crm.db")}";

    private string ShardConnectionString() => $"Data Source={Path.Combine(_directory, "shard.db")}";

    private void SeedAll()
    {
        Seed(CrmConnectionString(),
            "CREATE TABLE CrmRow (Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO CrmRow (Id, Name) VALUES (1, 'routed');");

        Seed(DefaultConnectionString(), "CREATE TABLE PlainRow (Id INTEGER PRIMARY KEY, Name TEXT);");

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

    [ShardKey(nameof(Tenant))]
    public sealed class ShardRow
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
