using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Routing;

/// <summary>
/// Structural routing: an entity type is sent to a topology by an attribute, a namespace rule, an
/// assembly rule or a custom resolver, and the command executes on that topology's host. Two SQLite files
/// stand in for two servers; the default connection and the routed host hold different rows.
/// </summary>
public sealed class DataRoutingTests : IDisposable
{
    private const string RoutedTopology = "crm";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-routing-" + Guid.NewGuid().ToString("N"));

    public DataRoutingTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void AttributeRule_ShouldRouteEntityToItsTopology()
    {
        using var context = CreateContext(routing => routing.AddTopology(CrmTopology()));

        var rows = context.From<CrmRow>().ToList();

        rows.Should().ContainSingle().Which.Name.Should().Be("routed");
    }

    [Fact]
    public void NamespaceRule_ShouldRouteEveryTypeUnderIt()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(CrmTopology())
            .RouteNamespace(typeof(NamespaceRow).Namespace!, RoutedTopology));

        var rows = context.From<NamespaceRow>().ToList();

        rows.Should().ContainSingle().Which.Name.Should().Be("routed");
    }

    [Fact]
    public void AssemblyRule_ShouldRouteTypesOfThatAssembly()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(CrmTopology())
            .RouteAssembly(typeof(AssemblyRow).Assembly, RoutedTopology));

        var rows = context.From<AssemblyRow>().ToList();

        rows.Should().ContainSingle().Which.Name.Should().Be("routed");
    }

    [Fact]
    public void CustomResolver_ShouldRouteWithoutAnyStructuralRule()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(CrmTopology())
            .UseResolver(new TypeNameResolver(nameof(CustomRow), RoutedTopology)));

        var rows = context.From<CustomRow>().ToList();

        rows.Should().ContainSingle().Which.Name.Should().Be("routed");
    }

    [Fact]
    public void UnroutedEntity_ShouldStayOnTheContextConnection()
    {
        using var context = CreateContext(routing => routing.AddTopology(CrmTopology()));

        var rows = context.From<PlainRow>().ToList();

        rows.Should().ContainSingle().Which.Name.Should().Be("default");
    }

    [Fact]
    public void ConflictingRules_ShouldFailWithBothTopologies()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(CrmTopology())
            .AddTopology(new FileTopology("other", RoutedConnectionString()))
            .RouteNamespace(typeof(CrmRow).Namespace!, "other"));

        FluentActions.Invoking(() => context.From<CrmRow>().ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*routed to both*");
    }

    [Fact]
    public void RuleForUnregisteredTopology_ShouldFailBeforeAnyQuery()
    {
        FluentActions.Invoking(() => CreateContext(routing => routing
                .RouteNamespace(typeof(NamespaceRow).Namespace!, RoutedTopology)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{RoutedTopology}'*not registered*");
    }

    [Fact]
    public void RoutedMutation_ShouldRunOnTheRoutedHost()
    {
        using var context = CreateContext(routing => routing.AddTopology(CrmTopology()));

        var inserted = context.CreateInsertBuilder<CrmRow>()
            .Value(row => row.Id, 2)
            .Value(row => row.Name, "inserted")
            .Insert();
        inserted.Should().Be(1);

        using var routed = new SqliteDataContext(new DataContextBuilder().UseDataRouting(routing => routing
            .UseDefaultTopology("main", RoutedConnectionString())
            .AddTopology(CrmTopology())));
        routed.From<CrmRow>().ToList().Should().ContainSingle(row => row.Name == "inserted");
        context.From<CrmRow>().ToList().Should().HaveCount(2);
    }

    [Fact]
    public void RoutedCommandInsideATransaction_ShouldFail()
    {
        using var context = CreateContext(routing => routing.AddTopology(CrmTopology()));

        using var transaction = ((ITransactionManager)context).BeginTransaction();

        FluentActions.Invoking(() => context.From<CrmRow>().ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*transaction is open*");
    }

    [Fact]
    public void TopologyThatDeclines_ShouldLeaveTheCommandOnTheContextConnection()
    {
        using var context = CreateContext(routing => routing
            .AddTopology(new DecliningTopology())
            .UseResolver(new TypeNameResolver(nameof(PlainRow), "declining")));

        var rows = context.From<PlainRow>().ToList();

        rows.Should().ContainSingle().Which.Name.Should().Be("default");
    }

    [Fact]
    public void DuplicateTopologyName_ShouldFail()
    {
        var builder = new DataContextBuilder();
        builder.AddTopology(CrmTopology());

        FluentActions.Invoking(() => builder.AddTopology(CrmTopology()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*already registered*");
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
        using (var context = new SqliteDataContext(DefaultConnectionString(), new DataContextBuilder()))
        {
            context.EnsureConnectionOpen();
            foreach (var table in new[] { nameof(CrmRow), nameof(PlainRow) })
            {
                context.CreateCommand($"DROP TABLE IF EXISTS {table}; CREATE TABLE {table} (Id INTEGER PRIMARY KEY, Name TEXT);")
                    .ExecuteNonQuery();
                context.CreateCommand($"INSERT INTO {table} (Id, Name) VALUES (1, 'default');").ExecuteNonQuery();
            }
        }

        using (var routed = new SqliteDataContext(RoutedConnectionString(), new DataContextBuilder()))
        {
            routed.EnsureConnectionOpen();
            foreach (var table in new[] { nameof(CrmRow), nameof(NamespaceRow), nameof(AssemblyRow), nameof(CustomRow) })
            {
                routed.CreateCommand($"DROP TABLE IF EXISTS {table}; CREATE TABLE {table} (Id INTEGER PRIMARY KEY, Name TEXT);")
                    .ExecuteNonQuery();
                routed.CreateCommand($"INSERT INTO {table} (Id, Name) VALUES (1, 'routed');").ExecuteNonQuery();
            }
        }

        // A routed context is created from the builder alone: the default topology supplies its own
        // connection, and unrouted entities, raw SQL and transactions run there.
        var contextBuilder = new DataContextBuilder().UseDataRouting(routing =>
        {
            routing.UseDefaultTopology("main", DefaultConnectionString());
            configure(routing);
        });
        return new SqliteDataContext(contextBuilder);
    }

    private FileTopology CrmTopology() => new(RoutedTopology, RoutedConnectionString());

    private string DefaultConnectionString() => $"Data Source={Path.Combine(_directory, "default.db")}";

    private string RoutedConnectionString() => $"Data Source={Path.Combine(_directory, "routed.db")}";

    private sealed class FileTopology(string name, string connectionString) : IHostTopology
    {
        public string Name => name;

        public bool TryGetHost(in TopologyContext context, out HostTarget host)
        {
            host = new HostTarget("only", connectionString);
            return true;
        }
    }

    private sealed class DecliningTopology : IHostTopology
    {
        public string Name => "declining";

        public bool TryGetHost(in TopologyContext context, out HostTarget host)
        {
            host = default;
            return false;
        }
    }

    private sealed class TypeNameResolver(string typeName, string topology) : IEntityRouteResolver
    {
        public bool TryResolve(Type entityType, in global::NextORM.Core.MappingVariant variant, out RouteTarget target)
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

    [RouteTo(RoutedTopology)]
    private sealed class CrmRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NamespaceRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class AssemblyRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CustomRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PlainRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
