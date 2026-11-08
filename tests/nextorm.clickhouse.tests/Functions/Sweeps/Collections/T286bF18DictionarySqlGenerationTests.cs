using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D18 (F18 Dictionary): the ClickHouse-only dictionary and embedded-geobase region surface.
/// The tests pin every rendered arm (<c>dictGetOrNull</c>, <c>dictGetAll</c> with and without the
/// limit, <c>dictGetDescendants</c> with and without the level, and every <c>regionTo*</c> /
/// <c>regionHierarchy</c> / <c>regionIn</c> form), the declared CLR shapes, the captured-parameter
/// path, the fail-closed refusal of every other provider, and the four deterministic version-gap
/// refusals (<c>dictGetKeys</c>, <c>dictGetRoot</c>, <c>regionToParent</c>, <c>regionToParents</c>).
/// They never open a connection.
/// </summary>
public class T286bF18DictionarySqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f18_entity")]
    internal interface IT286bDictionaryEntity
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("value")]
        string? Value { get; set; }
    }

    private static readonly string[] DeliveredNames =
    [
        "dict_get_or_null", "dict_get_all", "dict_get_descendants",
        "region_to_name", "region_to_city", "region_to_area", "region_to_district",
        "region_to_country", "region_to_continent", "region_to_top_continent",
        "region_to_population", "region_hierarchy", "region_in"
    ];

    private static readonly string[] RefusedNames =
    [
        "dict_get_keys", "dict_get_root", "region_to_parent", "region_to_parents"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDictionaryEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_or_null<string, long>("dict", "name", x.Id) }))
                .Should().Contain("dictGetOrNull('dict', 'name', id)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_all<string, string>("regexp", "os", "Linux") }))
                .Should().Contain("dictGetAll('regexp', 'os', 'Linux')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_all<string, string>("regexp", "os", "Linux", 2UL) }))
                .Should().Contain("dictGetAll('regexp', 'os', 'Linux', 2)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_descendants<long>("hierarchy", x.Id) }))
                .Should().Contain("dictGetDescendants('hierarchy', id)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_descendants<long>("hierarchy", x.Id, 1) }))
                .Should().Contain("dictGetDescendants('hierarchy', id, 1)");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_name(2U) }))
                .Should().Contain("regionToName(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_name(2U, "en") }))
                .Should().Contain("regionToName(toUInt32(2), 'en')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_city(2U) }))
                .Should().Contain("regionToCity(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_city(2U, "ua") }))
                .Should().Contain("regionToCity(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_area(2U) }))
                .Should().Contain("regionToArea(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_area(2U, "ua") }))
                .Should().Contain("regionToArea(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_district(2U) }))
                .Should().Contain("regionToDistrict(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_district(2U, "ua") }))
                .Should().Contain("regionToDistrict(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_country(2U) }))
                .Should().Contain("regionToCountry(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_country(2U, "ua") }))
                .Should().Contain("regionToCountry(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_continent(2U) }))
                .Should().Contain("regionToContinent(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_continent(2U, "ua") }))
                .Should().Contain("regionToContinent(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_top_continent(2U) }))
                .Should().Contain("regionToTopContinent(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_top_continent(2U, "ua") }))
                .Should().Contain("regionToTopContinent(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_population(2U) }))
                .Should().Contain("regionToPopulation(toUInt32(2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_to_population(2U, "ua") }))
                .Should().Contain("regionToPopulation(toUInt32(2), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_hierarchy(3U) }))
                .Should().Contain("regionHierarchy(toUInt32(3))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_hierarchy(3U, "ua") }))
                .Should().Contain("regionHierarchy(toUInt32(3), 'ua')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_in(3U, 1U) }))
                .Should().Contain("regionIn(toUInt32(3), toUInt32(1))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.region_in(3U, 1U, "ua") }))
                .Should().Contain("regionIn(toUInt32(3), toUInt32(1), 'ua')");
        }
    }

    [Fact]
    public void DeliveredMembers_ShouldDeclareTheTypedClrShapes()
    {
        using (new AssertionScope())
        {
            ReturnTypeOf("dict_get_or_null", 3, typeof(string), typeof(long)).Should().Be(typeof(string));
            ReturnTypeOf("dict_get_or_null", 3, typeof(ulong?), typeof(long)).Should().Be(typeof(ulong?));
            ReturnTypeOf("dict_get_all", 3, typeof(int), typeof(long)).Should().Be(typeof(int[]));
            ReturnTypeOf("dict_get_all", 4, typeof(int), typeof(long)).Should().Be(typeof(int[]));
            ReturnTypeOf("dict_get_descendants", 2, typeof(long)).Should().Be(typeof(ulong[]));
            ReturnTypeOf("dict_get_descendants", 3, typeof(long)).Should().Be(typeof(ulong[]));
            ReturnTypeOf("region_to_name", 1).Should().Be(typeof(string));
            ReturnTypeOf("region_to_name", 2).Should().Be(typeof(string));
            ReturnTypeOf("region_to_city", 1).Should().Be(typeof(uint));
            ReturnTypeOf("region_to_city", 2).Should().Be(typeof(uint));
            ReturnTypeOf("region_to_population", 1).Should().Be(typeof(uint));
            ReturnTypeOf("region_to_population", 2).Should().Be(typeof(uint));
            ReturnTypeOf("region_hierarchy", 1).Should().Be(typeof(uint[]));
            ReturnTypeOf("region_hierarchy", 2).Should().Be(typeof(uint[]));
            ReturnTypeOf("region_in", 2).Should().Be(typeof(bool));
            ReturnTypeOf("region_in", 3).Should().Be(typeof(bool));
        }

        static Type ReturnTypeOf(string name, int parameterCount, params Type[] genericArguments)
        {
            var method = typeof(ClickHouseFunctions)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);
            return method.IsGenericMethodDefinition
                ? method.MakeGenericMethod(genericArguments).ReturnType
                : method.ReturnType;
        }
    }

    [Fact]
    public void CapturedDictionaryAndLanguage_ShouldBindAsParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDictionaryEntity>();
        var dict = "dict";
        var lang = "en";

        var command = Prepare(ctx, e.Select(x => new
        {
            Lookup = SqlFunctions.ClickHouse.dict_get_or_null<string, long>(dict, "name", x.Id),
            Name = SqlFunctions.ClickHouse.region_to_name(2U, lang)
        }));

        var sql = Normalize(command.DbCommand.CommandText);
        using (new AssertionScope())
        {
            sql.Should().Contain("dictGetOrNull(@");
            sql.Should().Contain("regionToName(toUInt32(2), @");
            command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Count().Should().BeGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryDeliveredMember()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in DeliveredNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "dict_get_or_null" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.dict_get_or_null<string, long>("d", "a", x.Id) })),
                "dict_get_all" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.dict_get_all<string, string>("d", "a", "v") })),
                "dict_get_descendants" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.dict_get_descendants<long>("d", x.Id) })),
                "region_to_name" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_name(2U) })),
                "region_to_city" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_city(2U) })),
                "region_to_area" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_area(2U) })),
                "region_to_district" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_district(2U) })),
                "region_to_country" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_country(2U) })),
                "region_to_continent" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_continent(2U) })),
                "region_to_top_continent" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_top_continent(2U) })),
                "region_to_population" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_population(2U) })),
                "region_hierarchy" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_hierarchy(3U) })),
                "region_in" => () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_in(3U, 1U) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                    "'{0}' is ClickHouse-only and must be refused by every other provider", name)
                .WithMessage("*dictionary*");
        }
    }

    [Fact]
    public void VersionAbsentMembers_ShouldFailClosedOnEveryProviderWithThePreciseReason()
    {
        using var clickHouse = ClickHouseTestContext.Create();
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var ctx in new IDataContext[] { clickHouse, postgres })
            {
                AssertRefusal(ctx, "dictGetKeys was introduced in ClickHouse 25.12",
                    () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.dict_get_keys<long, string>("d", "a", "v") })));
                AssertRefusal(ctx, "dictGetRoot was introduced in ClickHouse 26.7",
                    () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.dict_get_root<long>("d", x.Id) })));
                AssertRefusal(ctx, "has no regionToParent function",
                    () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_parent(2U) })));
                AssertRefusal(ctx, "has no regionToParents function",
                    () => SqlOf(ctx, ctx.From<IT286bDictionaryEntity>().Select(x => new { V = SqlFunctions.ClickHouse.region_to_parents(2U) })));
            }
        }

        static void AssertRefusal(IDataContext ctx, string reason, Func<string> query)
            => FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage($"*{reason}*");
    }

    [Fact]
    public void DialectTable_ShouldCarryEveryNewVendorSpelling()
    {
        var dialect = new NextORM.ClickHouse.ClickHouseDialect();

        using (new AssertionScope())
        {
            dialect.MakeDictionaryFunction("dict_get_or_null", ["'d'", "'a'", "id"]).Should().Be("dictGetOrNull('d', 'a', id)");
            dialect.MakeDictionaryFunction("dict_get_all", ["'d'", "'a'", "v", "2"]).Should().Be("dictGetAll('d', 'a', v, 2)");
            dialect.MakeDictionaryFunction("dict_get_descendants", ["'d'", "id", "1"]).Should().Be("dictGetDescendants('d', id, 1)");
            dialect.MakeDictionaryFunction("region_to_name", ["2"]).Should().Be("regionToName(2)");
            dialect.MakeDictionaryFunction("region_to_name", ["2", "'en'"]).Should().Be("regionToName(2, 'en')");
            dialect.MakeDictionaryFunction("region_to_city", ["2"]).Should().Be("regionToCity(2)");
            dialect.MakeDictionaryFunction("region_to_area", ["2"]).Should().Be("regionToArea(2)");
            dialect.MakeDictionaryFunction("region_to_district", ["2"]).Should().Be("regionToDistrict(2)");
            dialect.MakeDictionaryFunction("region_to_country", ["2"]).Should().Be("regionToCountry(2)");
            dialect.MakeDictionaryFunction("region_to_continent", ["2"]).Should().Be("regionToContinent(2)");
            dialect.MakeDictionaryFunction("region_to_top_continent", ["2"]).Should().Be("regionToTopContinent(2)");
            dialect.MakeDictionaryFunction("region_to_population", ["2"]).Should().Be("regionToPopulation(2)");
            dialect.MakeDictionaryFunction("region_hierarchy", ["3"]).Should().Be("regionHierarchy(3)");
            dialect.MakeDictionaryFunction("region_in", ["3", "1"]).Should().Be("regionIn(3, 1)");
        }
    }

    [Fact]
    public void RefusedNames_ShouldCoverTheVersionGapMembersOnly()
    {
        RefusedNames.Should().Equal(
            "dict_get_keys", "dict_get_root", "region_to_parent", "region_to_parents");
        DeliveredNames.Should().NotIntersectWith(RefusedNames);
    }
}
