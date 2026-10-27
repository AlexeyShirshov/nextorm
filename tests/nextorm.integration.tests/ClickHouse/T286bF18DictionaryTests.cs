using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;
using NextORM.Core;
using Testcontainers.ClickHouse;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D18 (F18 Dictionary): live execution of the ClickHouse-only dictionary and embedded-geobase
/// region members against the pinned <c>25.8-alpine</c> (25.8.33.6) server.
///
/// <para>
/// The dictionary fixture is a real server object read through a local <c>CLICKHOUSE</c> source: a
/// hashed hierarchical dictionary for <c>dictGetOrNull</c>/<c>dictGetHierarchy</c>/<c>dictGetChildren</c>/
/// <c>dictGetDescendants</c>, and a <c>REGEXP_TREE</c> dictionary for <c>dictGetAll</c>. Every fact
/// creates the objects, asserts the pinned vendor values, and drops them again; the class also drops
/// them in <see cref="DisposeAsync"/> so a failing assertion cannot leave fixtures behind.
/// </para>
///
/// <para>
/// The region functions need the embedded geobase dictionaries, which ClickHouse loads only from the
/// server configuration at startup. The dedicated container therefore receives
/// <c>config.d/geobase.xml</c> and the <c>regions_hierarchy.txt</c>/<c>regions_names_{en,ru}.txt</c>
/// files through <c>WithResourceMapping</c>
/// before it starts; the tests pin <c>regionToName</c> (default and explicit language, missing region),
/// the nearest-ancestor accessors, population, hierarchy and the reflexive <c>regionIn</c> relation.
/// </para>
/// </summary>
public sealed class T286bF18DictionaryTests : ProviderTestSuite, IClassFixture<T286bF18RegionFixture>, IAsyncLifetime
{
    private const string HierarchyDictionary = "t286b_f18_h_dict";
    private const string RegexpDictionary = "t286b_f18_rx_dict";

    private readonly T286bF18RegionFixture _region;
    private IDataContext? _regionContext;

    public T286bF18DictionaryTests(T286bF18RegionFixture region)
    {
        _region = region;
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private IDataContext RegionContext =>
        _regionContext ??= new ClickHouseDataContext(_region.ConnectionString, new DataContextBuilder());

    [SqlTable("t286b_f18_marker")]
    internal interface IT286bF18Marker
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
    }

    [SqlTable("t286b_f18_region_marker")]
    internal interface IT286bF18RegionMarker
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
    }

    public ValueTask InitializeAsync()
    {
        try
        {
            SeedDictionaryFixtures();
        }
        catch
        {
            CleanupDictionaryFixtures();
            throw;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        CleanupDictionaryFixtures();
        _regionContext?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ---- fixture lifecycle ----------------------------------------------------------------

    private static void SeedDictionaryFixtures()
    {
        var source = DictionarySource();
        var statements = new List<string>
        {
            "drop dictionary if exists " + HierarchyDictionary,
            "drop dictionary if exists " + RegexpDictionary,
            "drop table if exists t286b_f18_h_src",
            "drop table if exists t286b_f18_rx_src",
            "drop table if exists t286b_f18_marker",
            "create table t286b_f18_marker (id Int32) engine = Memory",
            "insert into t286b_f18_marker values (1)",
            """
            create table t286b_f18_h_src
            (
                id UInt64,
                parent UInt64,
                name String,
                pop UInt64
            )
            engine = MergeTree order by id
            """,
            """
            insert into t286b_f18_h_src (id, parent, name, pop) values
                (1, 0, 'root', 100),
                (2, 1, 'child', 50),
                (3, 2, 'leaf', 10),
                (4, 1, 'other', 25),
                (5, 4, 'otherleaf', 5)
            """,
            $"""
            create dictionary {HierarchyDictionary}
            (
                id UInt64,
                parent UInt64 DEFAULT 0 HIERARCHICAL,
                name String DEFAULT '',
                pop UInt64 DEFAULT 0
            )
            primary key id
            source(clickhouse({source} table 't286b_f18_h_src'))
            layout(hashed())
            lifetime(0)
            """,
            """
            create table t286b_f18_rx_src
            (
                id UInt64,
                parent_id UInt64,
                regexp String,
                keys Array(String),
                values Array(String)
            )
            engine = Memory
            """,
            """
            insert into t286b_f18_rx_src (id, parent_id, regexp, keys, values) values
                (1, 0, 'Linux', ['os_replacement'], ['Linux']),
                (2, 1, 'Android', ['os_replacement'], ['Android']),
                (3, 2, 'Android 1', ['os_replacement'], ['Android']),
                (4, 3, 'Android 12', ['os_replacement'], ['Android'])
            """,
            $"""
            create dictionary {RegexpDictionary}
            (
                regexp String,
                os_replacement String DEFAULT 'Other'
            )
            primary key regexp
            source(clickhouse({source} table 't286b_f18_rx_src'))
            layout(regexp_tree)
            lifetime(0)
            """,
            "system reload dictionaries"
        };

        Execute(statements);
    }

    private static void CleanupDictionaryFixtures() =>
        Execute(
        [
            "drop dictionary if exists " + HierarchyDictionary,
            "drop dictionary if exists " + RegexpDictionary,
            "drop table if exists t286b_f18_h_src",
            "drop table if exists t286b_f18_rx_src",
            "drop table if exists t286b_f18_marker"
        ]);

    private static void Execute(IEnumerable<string> statements)
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in statements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    private static string DictionarySource()
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = ClickHouseContainer.ConnectionString };
        var user = Setting(builder, "Username", "User ID", "User");
        var password = Setting(builder, "Password", "Pwd");
        var database = Setting(builder, "Database");
        return $"host 'localhost' port 9000 user '{Literal(user)}' password '{Literal(password)}' db '{Literal(database)}'";
    }

    private static string Setting(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value))
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return string.Empty;
    }

    private static string Literal(string value) => value.Replace("'", "''");

    private static long Count(string table, string name)
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"select count() from system.{table} where name = '{name}'";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    // ---- dictionary members ---------------------------------------------------------------

    [Fact]
    public void Fixtures_ShouldBeCreatedAndLoaded()
    {
        Count("dictionaries", HierarchyDictionary).Should().Be(1);
        Count("dictionaries", RegexpDictionary).Should().Be(1);
        Count("tables", "t286b_f18_h_src").Should().Be(1);
        Count("tables", "t286b_f18_rx_src").Should().Be(1);
    }

    [Fact]
    public void DictGet_ShouldReturnTheStoredValueAndTheVendorDefaultForAMissingKey()
    {
        var row = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Present = SqlFunctions.ClickHouse.dict_get<string, ulong>(HierarchyDictionary, "name", 2ul),
                Missing = SqlFunctions.ClickHouse.dict_get<string, ulong>(HierarchyDictionary, "name", 99ul)
            })
            .Single();

        row.Present.Should().Be("child");
        row.Missing.Should().BeEmpty("dictGet returns the attribute's configured default, not NULL");
    }

    [Fact]
    public void DictGetOrDefault_ShouldReturnTheStoredValueOrTheExplicitDefault()
    {
        var row = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Present = SqlFunctions.ClickHouse.dict_get_or_default<string, ulong>(HierarchyDictionary, "name", 3ul, "n/a"),
                Missing = SqlFunctions.ClickHouse.dict_get_or_default<string, ulong>(HierarchyDictionary, "name", 99ul, "n/a")
            })
            .Single();

        row.Present.Should().Be("leaf");
        row.Missing.Should().Be("n/a");
    }

    [Fact]
    public void DictGetOrNull_ShouldReturnTheStoredValueOrNull()
    {
        var row = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Present = SqlFunctions.ClickHouse.dict_get_or_null<string, ulong>(HierarchyDictionary, "name", 2ul),
                Missing = SqlFunctions.ClickHouse.dict_get_or_null<string, ulong>(HierarchyDictionary, "name", 99ul),
                PresentPop = SqlFunctions.ClickHouse.dict_get_or_null<ulong?, ulong>(HierarchyDictionary, "pop", 2ul),
                MissingPop = SqlFunctions.ClickHouse.dict_get_or_null<ulong?, ulong>(HierarchyDictionary, "pop", 99ul)
            })
            .Single();

        row.Present.Should().Be("child");
        row.Missing.Should().BeNull("dictGetOrNull returns NULL for an absent key");
        row.PresentPop.Should().Be(50UL);
        row.MissingPop.Should().BeNull();
    }

    [Fact]
    public void DictGetAll_ShouldReturnEveryMatchInVendorOrder()
    {
        var values = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.dict_get_all<string, string>(
                RegexpDictionary,
                "os_replacement",
                "Mozilla/5.0 (Linux; Android 12; SM-G998B) Mobile Safari/537.36"))
            .Single();

        values.Should().Equal("Android", "Android", "Android", "Linux");
    }

    [Fact]
    public void DictGetAll_WithLimit_ShouldTruncateWithChildPrecedence()
    {
        var row = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Limited = SqlFunctions.ClickHouse.dict_get_all<string, string>(
                    RegexpDictionary,
                    "os_replacement",
                    "Mozilla/5.0 (Linux; Android 12; SM-G998B) Mobile Safari/537.36",
                    2UL),
                NoMatch = SqlFunctions.ClickHouse.dict_get_all<string, string>(RegexpDictionary, "os_replacement", "NoMatch"),
                ZeroLimit = SqlFunctions.ClickHouse.dict_get_all<string, string>(
                    RegexpDictionary,
                    "os_replacement",
                    "Linux",
                    0UL)
            })
            .Single();

        row.Limited.Should().Equal("Android", "Android");
        row.NoMatch.Should().BeEmpty("an unmatched regexp-tree lookup returns an empty array, not a default");
        row.ZeroLimit.Should().BeEmpty("the vendor limit 0 returns an empty array");
    }

    [Fact]
    public void DictGetDescendants_ShouldReturnEveryLevelAndTheLevelLimit()
    {
        var row = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                All = SqlFunctions.ClickHouse.dict_get_descendants<ulong>(HierarchyDictionary, 1ul),
                FirstLevel = SqlFunctions.ClickHouse.dict_get_descendants<ulong>(HierarchyDictionary, 1ul, 1),
                ZeroLevel = SqlFunctions.ClickHouse.dict_get_descendants<ulong>(HierarchyDictionary, 1ul, 0),
                Leaf = SqlFunctions.ClickHouse.dict_get_descendants<ulong>(HierarchyDictionary, 3ul)
            })
            .Single();

        row.All.Should().Equal(4UL, 2UL, 3UL, 5UL);
        row.FirstLevel.Should().Equal(4UL, 2UL);
        row.ZeroLevel.Should().Equal(4UL, 2UL, 3UL, 5UL);
        row.Leaf.Should().BeEmpty();
    }

    [Fact]
    public void DictGetHierarchyAndChildren_ShouldReturnThePinnedChains()
    {
        var row = Context.From<IT286bF18Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Hierarchy = SqlFunctions.ClickHouse.dict_get_hierarchy<ulong>(HierarchyDictionary, 3ul),
                Children = SqlFunctions.ClickHouse.dict_get_children<ulong>(HierarchyDictionary, 1ul),
                LeafChildren = SqlFunctions.ClickHouse.dict_get_children<ulong>(HierarchyDictionary, 3ul)
            })
            .Single();

        row.Hierarchy.Should().Equal(3UL, 2UL, 1UL);
        row.Children.Should().Equal(4UL, 2UL);
        row.LeafChildren.Should().BeEmpty();
    }

    [Fact]
    public void DictGetOrNull_OnRegexpTree_ShouldBeRejectedByTheServer()
    {
        FluentActions.Invoking(() => Context.From<IT286bF18Marker>()
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.dict_get_or_null<string, string>(RegexpDictionary, "os_replacement", "Linux"))
                .Single())
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*does not support method*hasKeys*");
    }

    // ---- region members -------------------------------------------------------------------

    [Fact]
    public void RegionToName_ShouldUseTheRequestedLanguageAndReturnEmptyForAMissingRegion()
    {
        var row = RegionContext.From<IT286bF18RegionMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Default = SqlFunctions.ClickHouse.region_to_name(2u),
                English = SqlFunctions.ClickHouse.region_to_name(2u, "en"),
                Russian = SqlFunctions.ClickHouse.region_to_name(2u, "ru"),
                Missing = SqlFunctions.ClickHouse.region_to_name(99u, "en")
            })
            .Single();

        row.Default.Should().Be("США", "the default geobase language is ru");
        row.English.Should().Be("USA");
        row.Russian.Should().Be("США");
        row.Missing.Should().BeEmpty("a missing region has no name, as the vendor defines");
    }

    [Fact]
    public void RegionAccessors_ShouldReturnThePinnedAncestorIdsAndPopulation()
    {
        var row = RegionContext.From<IT286bF18RegionMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                City = SqlFunctions.ClickHouse.region_to_city(5u),
                Area = SqlFunctions.ClickHouse.region_to_area(3u),
                District = SqlFunctions.ClickHouse.region_to_district(3u),
                Country = SqlFunctions.ClickHouse.region_to_country(3u),
                Continent = SqlFunctions.ClickHouse.region_to_continent(2u),
                TopContinent = SqlFunctions.ClickHouse.region_to_top_continent(2u),
                Population = SqlFunctions.ClickHouse.region_to_population(2u)
            })
            .Single();

        row.City.Should().Be(5u);
        row.Area.Should().Be(3u);
        row.District.Should().Be(0u, "the fixture chain has no federal-district node");
        row.Country.Should().Be(2u);
        row.Continent.Should().Be(6u);
        row.TopContinent.Should().Be(7u);
        row.Population.Should().Be(330000000u);
    }

    [Fact]
    public void RegionAccessors_ShouldReportAnUnknownRegionAsZero()
    {
        var row = RegionContext.From<IT286bF18RegionMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                City = SqlFunctions.ClickHouse.region_to_city(99u),
                Country = SqlFunctions.ClickHouse.region_to_country(99u),
                Continent = SqlFunctions.ClickHouse.region_to_continent(99u),
                Hierarchy = SqlFunctions.ClickHouse.region_hierarchy(99u)
            })
            .Single();

        row.City.Should().Be(0u);
        row.Country.Should().Be(0u);
        row.Continent.Should().Be(0u);
        row.Hierarchy.Should().Equal(99u);
    }

    [Fact]
    public void RegionHierarchy_ShouldReturnTheRegionAndEveryParent()
    {
        var chain = RegionContext.From<IT286bF18RegionMarker>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.region_hierarchy(3u))
            .Single();

        chain.Should().Equal(3u, 2u, 6u, 7u, 1u);
    }

    [Fact]
    public void RegionIn_ShouldBeReflexiveAndRejectANonAncestor()
    {
        var row = RegionContext.From<IT286bF18RegionMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ancestor = SqlFunctions.ClickHouse.region_in(3u, 1u),
                Self = SqlFunctions.ClickHouse.region_in(3u, 3u),
                NonAncestor = SqlFunctions.ClickHouse.region_in(3u, 4u),
                Unknown = SqlFunctions.ClickHouse.region_in(99u, 1u)
            })
            .Single();

        row.Ancestor.Should().BeTrue();
        row.Self.Should().BeTrue("the vendor relation is reflexive");
        row.NonAncestor.Should().BeFalse("Boulder County is not an ancestor of Colorado");
        row.Unknown.Should().BeFalse();
    }

    // ---- refusals and cleanup -------------------------------------------------------------

    [Fact]
    public void VersionAbsentMembers_ShouldFailClosedBeforeAnySql()
    {
        using (new AssertionScope())
        {
            FluentActions.Invoking(() => Context.From<IT286bF18Marker>()
                    .Select(x => SqlFunctions.ClickHouse.dict_get_keys<ulong, string>(HierarchyDictionary, "name", "child")).First())
                .Should().Throw<NotSupportedException>().WithMessage("*dictGetKeys was introduced in ClickHouse 25.12*");
            FluentActions.Invoking(() => Context.From<IT286bF18Marker>()
                    .Select(x => SqlFunctions.ClickHouse.dict_get_root<ulong>(HierarchyDictionary, 3ul)).First())
                .Should().Throw<NotSupportedException>().WithMessage("*dictGetRoot was introduced in ClickHouse 26.7*");
            FluentActions.Invoking(() => RegionContext.From<IT286bF18RegionMarker>()
                    .Select(x => SqlFunctions.ClickHouse.region_to_parent(3u)).First())
                .Should().Throw<NotSupportedException>().WithMessage("*has no regionToParent function*");
            FluentActions.Invoking(() => RegionContext.From<IT286bF18RegionMarker>()
                    .Select(x => SqlFunctions.ClickHouse.region_to_parents(3u)).First())
                .Should().Throw<NotSupportedException>().WithMessage("*has no regionToParents function*");
        }
    }

    [Fact]
    public void Cleanup_ShouldRemoveATemporaryDictionaryAndItsSourceTable()
    {
        Execute(
        [
            "drop dictionary if exists t286b_f18_cleanup_dict",
            "drop table if exists t286b_f18_cleanup_src",
            "create table t286b_f18_cleanup_src (id UInt64, name String) engine = Memory",
            "insert into t286b_f18_cleanup_src values (1, 'x')",
            $"""
            create dictionary t286b_f18_cleanup_dict (id UInt64, name String DEFAULT '')
            primary key id
            source(clickhouse({DictionarySource()} table 't286b_f18_cleanup_src'))
            layout(hashed()) lifetime(0)
            """,
            "system reload dictionaries"
        ]);

        Count("dictionaries", "t286b_f18_cleanup_dict").Should().Be(1);

        Execute(
        [
            "drop dictionary if exists t286b_f18_cleanup_dict",
            "drop table if exists t286b_f18_cleanup_src"
        ]);

        Count("dictionaries", "t286b_f18_cleanup_dict").Should().Be(0);
        Count("tables", "t286b_f18_cleanup_src").Should().Be(0);
    }
}

/// <summary>
/// Owns the dedicated ClickHouse container used by the F18 region members. The embedded geobase
/// dictionaries are read from the server configuration only at startup, so the geobase XML and the
/// three region files are copied into the container filesystem before it starts; the fixture then
/// creates the marker table the region queries project from.
/// </summary>
public sealed class T286bF18RegionFixture : IAsyncLifetime
{
    private const string Image = "clickhouse/clickhouse-server:25.8-alpine";

    private Testcontainers.ClickHouse.ClickHouseContainer? _container;

    /// <summary>The HTTP connection string of the dedicated region server.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The failure that prevented the fixture from starting, or <c>null</c>.</summary>
    public string? Failure { get; private set; }

    /// <summary>True when the dedicated region server is running.</summary>
    public bool IsAvailable => _container is not null && Failure is null;

    public async ValueTask InitializeAsync()
    {
        try
        {
            var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "ClickHouse", "Fixtures", "F18");
            var container = new ClickHouseBuilder(Image)
                .WithEnvironment("TZ", "UTC")
                .WithResourceMapping(
                    File.ReadAllBytes(Path.Combine(fixtureDirectory, "geobase.xml")),
                    "/etc/clickhouse-server/config.d/geobase.xml")
                .WithResourceMapping(
                    File.ReadAllBytes(Path.Combine(fixtureDirectory, "regions_hierarchy.txt")),
                    "/opt/geo/regions_hierarchy.txt")
                .WithResourceMapping(
                    File.ReadAllBytes(Path.Combine(fixtureDirectory, "regions_names_en.txt")),
                    "/opt/geo/regions_names_en.txt")
                .WithResourceMapping(
                    File.ReadAllBytes(Path.Combine(fixtureDirectory, "regions_names_ru.txt")),
                    "/opt/geo/regions_names_ru.txt")
                .Build();

            await container.StartAsync();
            _container = container;
            ConnectionString = container.GetConnectionString();

            using var connection = new ClickHouseConnection(ConnectionString);
            connection.Open();
            foreach (var statement in new[]
                     {
                         "create table t286b_f18_region_marker (id Int32) engine = Memory",
                         "insert into t286b_f18_region_marker values (1)"
                     })
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
        }
        catch (Exception exception)
        {
            Failure = $"The dedicated region container could not be started ({exception.Message}).";
            if (_container is { } container)
            {
                _container = null;
                await container.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        var container = Interlocked.Exchange(ref _container, null);
        if (container is not null)
            await container.DisposeAsync();
    }
}
