using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Routing;

/// <summary>
/// Topology routing beyond a single connection: several hosts under one topology, explicit host
/// pinning, transactions bound to a routed host, and the rule that one SQL statement never spans two
/// topologies. SQLite files stand in for the servers.
/// </summary>
public sealed class TopologyRoutingTests : IDisposable
{
    private const string PoolTopology = "pool";
    private const string CrmTopology = "crm";
    private const string BillingTopology = "billing";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-topology-" + Guid.NewGuid().ToString("N"));

    public TopologyRoutingTests()
    {
        Directory.CreateDirectory(_directory);
        SeedAll();
    }

    [Fact]
    public void RoundRobin_ShouldRotateHostsPerCommand()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b")))
            .UseResolver(new TypeNameResolver(nameof(PoolRow), PoolTopology)));

        var hosts = Enumerable.Range(0, 4)
            .Select(_ => context.From<PoolRow>().Single().Name)
            .ToArray();

        hosts.Should().Equal("a", "b", "a", "b");
    }

    [Fact]
    public void PinnedHost_ShouldOverrideTheRotation_UntilUnpinned()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b")))
            .UseResolver(new TypeNameResolver(nameof(PoolRow), PoolTopology)));

        context.PinHost(PoolTopology, "host-2");

        Enumerable.Range(0, 3).Select(_ => context.From<PoolRow>().Single().Name)
            .Should().Equal("b", "b", "b");

        context.UnpinHost(PoolTopology);

        Enumerable.Range(0, 3).Select(_ => context.From<PoolRow>().Single().Name)
            .Should().OnlyContain(name => name == "a" || name == "b")
            .And.HaveCount(3);
    }

    [Fact]
    public void PinToUnknownHost_ShouldFail()
    {
        using var context = CreateContext(routing => routing.AddTopology(
            new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b"))));

        FluentActions.Invoking(() => context.PinHost(PoolTopology, "host-9"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*no host 'host-9'*");
    }

    [Fact]
    public void TopologyThatIgnoresThePin_ShouldFailInsteadOfRoutingElsewhere()
    {
        using var context = CreateContext(routing => routing.AddTopology(new PinIgnoringTopology(PoolConnectionString("a"))));

        FluentActions.Invoking(() => context.PinHost(PoolTopology, "host-2"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return that host*");
    }

    [Fact]
    public void RoutedTransaction_ShouldRollBackOnItsHost()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b")))
            .UseResolver(new TypeNameResolver(nameof(PoolRow), PoolTopology)));

        var transaction = context.BeginTransaction(new RouteTarget(PoolTopology));
        context.CreateInsertBuilder<PoolRow>()
            .Value(row => row.Id, 99)
            .Value(row => row.Name, "rolled-back")
            .Insert();
        transaction.Rollback();
        transaction.Dispose();

        context.GetCurrentTransaction(new RouteTarget(PoolTopology)).Should().BeNull();
        ReadNames(PoolConnectionString("a")).Should().NotContain("rolled-back");
        ReadNames(PoolConnectionString("b")).Should().NotContain("rolled-back");
    }

    [Fact]
    public async Task RoutedTransactionAsync_ShouldCommitOnItsHost()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b")))
            .UseResolver(new TypeNameResolver(nameof(PoolRow), PoolTopology)));

        var transaction = await context.BeginTransactionAsync(new RouteTarget(PoolTopology), cancellationToken: TestContext.Current.CancellationToken);
        context.CreateInsertBuilder<PoolRow>()
            .Value(row => row.Id, 42)
            .Value(row => row.Name, "committed")
            .Insert();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        await transaction.DisposeAsync();

        var names = ReadNames(PoolConnectionString("a")).Concat(ReadNames(PoolConnectionString("b")));
        names.Should().Contain("committed");
    }

    [Fact]
    public void RoutedTransaction_ShouldRejectACommandOnAnotherConnection()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b")))
            .UseResolver(new TypeNameResolver(nameof(PoolRow), PoolTopology)));

        using var transaction = context.BeginTransaction(new RouteTarget(PoolTopology));

        FluentActions.Invoking(() => context.From<PlainRow>().ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*transaction is open on topology 'pool'*");
    }

    [Fact]
    public void TwoTypesMappedToTheSameTable_ShouldNotShareAPlanAcrossTopologies()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString()))
            .UseResolver(new TypeNameResolver(nameof(SameTableRoutedRow), CrmTopology)));

        // Identical SQL and identical result type (same table, constant projection), different routing:
        // a shared plan would send one of the two commands to the other type's host.
        var hosted = context.From<SameTableRoutedRow>().Select(row => 1).ToList();
        var local = context.From<SameTablePlainRow>().Select(row => 1).ToList();

        hosted.Should().ContainSingle();
        local.Should().HaveCount(2);
    }

    [Fact]
    public void PinningAwayFromAnOpenRoutedTransaction_ShouldFail()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new RoundRobinTopology(PoolTopology, PoolConnectionString("a"), PoolConnectionString("b")))
            .UseResolver(new TypeNameResolver(nameof(PoolRow), PoolTopology)));

        using var transaction = context.BeginTransaction(new RouteTarget(PoolTopology));

        FluentActions.Invoking(() => context.PinHost(PoolTopology, "host-2"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*transaction is open on topology 'pool'*pinned to host 'host-2'*");
    }

    [Fact]
    public void StatementSpanningTwoTopologies_ShouldFail()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString()))
            .AddTopology(new FileTopology(BillingTopology, BillingConnectionString()))
            .UseResolver(new TypeNameResolver(nameof(BillingRow), BillingTopology)));

        FluentActions.Invoking(() => context.From<CrmRow>()
                .Join<BillingRow>((crm, billing) => crm.Id == billing.Id)
                .Select(pair => new { Crm = pair.Item1.Name, Billing = pair.Item2!.Name })
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage($"*routed to topology '{CrmTopology}'*routed to topology '{BillingTopology}'*");
    }

    [Fact]
    public void SubquerySpanningTwoTopologies_ShouldFail()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString()))
            .AddTopology(new FileTopology(BillingTopology, BillingConnectionString()))
            .UseResolver(new TypeNameResolver(nameof(BillingRow), BillingTopology)));

        var billing = context.From<BillingRow>();

        FluentActions.Invoking(() => context.From<CrmRow>()
                .Where(crm => SqlFunctions.Sql.exists(billing.Where(row => row.Id == crm.Id)))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage($"*routed to topology '{BillingTopology}'*");
    }

    [Fact]
    public void RoutedSourceJoinedWithAnUnroutedOne_ShouldRunOnTheRoutedHost()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new FileTopology(CrmTopology, CrmConnectionString())));

        var rows = context.From<CrmRow>()
            .Join<PlainRow>((crm, plain) => crm.Id == plain.Id)
            .Select(pair => new { Crm = pair.Item1.Name, Plain = pair.Item2!.Name })
            .ToList();

        rows.Should().ContainSingle().Which.Plain.Should().Be("routed-plain");
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
        var builder = new DataContextBuilder().UseDataRouting(configure);
        return new SqliteDataContext(PoolConnectionString("a"), builder);
    }

    private void Seed(string connectionString, string sql)
    {
        using var context = new SqliteDataContext(connectionString, new DataContextBuilder());
        context.EnsureConnectionOpen();
        context.CreateCommand(sql).ExecuteNonQuery();
    }

    private List<string> ReadNames(string connectionString)
    {
        using var context = new SqliteDataContext(connectionString, new DataContextBuilder());
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand($"SELECT Name FROM {nameof(PoolRow)}");
        using var reader = command.ExecuteReader();

        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));

        return names;
    }

    private string PoolConnectionString(string file) => $"Data Source={Path.Combine(_directory, file + ".db")}";

    private string CrmConnectionString() => $"Data Source={Path.Combine(_directory, "crm.db")}";

    private string BillingConnectionString() => $"Data Source={Path.Combine(_directory, "billing.db")}";

    /// <summary>Seeds the routed files with the rows the checks read.</summary>
    private void SeedAll()
    {
        foreach (var file in new[] { "a", "b" })
            Seed(PoolConnectionString(file), Table(nameof(PoolRow)) + Insert(nameof(PoolRow), 1, file));

        Seed(CrmConnectionString(),
            Table(nameof(CrmRow)) + Insert(nameof(CrmRow), 1, "routed") +
            Table(nameof(PlainRow)) + Insert(nameof(PlainRow), 1, "routed-plain") +
            Table("SharedItems") + Insert("SharedItems", 1, "crm"));

        Seed(PoolConnectionString("a"),
            Table("SharedItems") + Insert("SharedItems", 1, "default") + Insert("SharedItems", 2, "default"));

        Seed(BillingConnectionString(), Table(nameof(BillingRow)) + Insert(nameof(BillingRow), 1, "billing"));
    }

    private static string Table(string name)
        => $"DROP TABLE IF EXISTS {name}; CREATE TABLE {name} (Id INTEGER PRIMARY KEY, Name TEXT); ";

    private static string Insert(string table, int id, string name)
        => $"INSERT INTO {table} (Id, Name) VALUES ({id}, '{name}'); ";

    private sealed class FileTopology(string name, string connectionString) : IHostTopology
    {
        public string Name => name;

        public bool TryGetHost(in TopologyContext context, out HostTarget host)
        {
            host = new HostTarget("only", connectionString);
            return true;
        }
    }

    private sealed class PinIgnoringTopology(string connectionString) : IHostTopology
    {
        public string Name => PoolTopology;

        public bool TryGetHost(in TopologyContext context, out HostTarget host)
        {
            host = new HostTarget("ignored-pin", connectionString);
            return true;
        }
    }

    private sealed class TypeNameResolver(string typeName, string topology) : IEntityRouteResolver
    {
        public bool TryResolve(Type entityType, in global::NextORM.Core.MappingScope scope, out RouteTarget target)
        {
            if (entityType.Name == typeName)
            {
                target = new RouteTarget(topology);
                return true;
            }

            target = default;
            return false;
        }
    }

    [SqlTable("SharedItems")]
    public sealed class SameTableRoutedRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [SqlTable("SharedItems")]
    public sealed class SameTablePlainRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [RouteTo(CrmTopology)]
    public sealed class CrmRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class BillingRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class PoolRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class PlainRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
