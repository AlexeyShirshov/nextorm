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
/// T286b D7 (F07 Map): the ClickHouse-only map members. The tests pin every rendered arm, the
/// declared <c>Map(K, V)</c> normalisation cast, the captured-dictionary and captured-array operand
/// paths reused from the existing map helpers, the inline <c>map(...)</c> constructor path, the
/// key/value type and NULL guards, and the fail-closed refusal of every other provider. They never
/// open a connection.
/// </summary>
public class T286bF07MapFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f07_entity")]
    internal interface IT286bMapEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("a_map")]
        IDictionary<string, int> AMap { get; set; }

        [Column("b_map")]
        IDictionary<string, int> BMap { get; set; }

        [Column("num_map")]
        IDictionary<int, int> NumMap { get; set; }

        [Column("data")]
        string Data { get; set; }

        [Column("keys")]
        string[] Keys { get; set; }

        [Column("values")]
        int[] Values { get; set; }
    }

    private static readonly string[] MemberNames =
    [
        "map_subtract", "map_update", "map_reverse_sort", "map_partial_sort",
        "map_from_arrays", "map_populate_series", "extract_key_value_pairs"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_subtract(x.AMap, x.BMap) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapSubtract(a_map, b_map)), mapValues(mapSubtract(a_map, b_map))) as Array(Tuple(String, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_update(x.AMap, x.BMap) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapUpdate(a_map, b_map)), mapValues(mapUpdate(a_map, b_map))) as Array(Tuple(String, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_reverse_sort(x.AMap) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapReverseSort(a_map)), mapValues(mapReverseSort(a_map))) as Array(Tuple(String, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_partial_sort(2L, x.AMap) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapPartialSort(2, a_map)), mapValues(mapPartialSort(2, a_map))) as Array(Tuple(String, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_from_arrays(x.Keys, x.Values) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapFromArrays(keys, values)), mapValues(mapFromArrays(keys, values))) as Array(Tuple(String, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_populate_series(x.NumMap) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapPopulateSeries(num_map)), mapValues(mapPopulateSeries(num_map))) as Array(Tuple(Int32, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_populate_series(x.NumMap, 10L) }))
                .Should().Contain("cast(arrayZip(mapKeys(mapPopulateSeries(num_map, 10)), mapValues(mapPopulateSeries(num_map, 10))) as Array(Tuple(Int32, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.extract_key_value_pairs(x.Data) }))
                .Should().Contain("cast(arrayZip(mapKeys(extractKeyValuePairs(data)), mapValues(extractKeyValuePairs(data))) as Array(Tuple(String, String))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.extract_key_value_pairs(x.Data, "=", ",") }))
                .Should().Contain("cast(arrayZip(mapKeys(extractKeyValuePairs(data, '=', ',')), mapValues(extractKeyValuePairs(data, '=', ','))) as Array(Tuple(String, String))) as `V`");
        }
    }

    [Fact]
    public void CapturedDictionaries_ShouldBindAsOneMapParameterEach()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        var left = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var right = new Dictionary<string, int> { ["b"] = 9 };
        var command = Prepare(ctx, e.Select(x => new
        {
            Subtract = SqlFunctions.ClickHouse.map_subtract(left, right),
            Update = SqlFunctions.ClickHouse.map_update(left, right),
            Reverse = SqlFunctions.ClickHouse.map_reverse_sort(left),
            Partial = SqlFunctions.ClickHouse.map_partial_sort(1L, left)
        }));

        var sql = Normalize(command.DbCommand.CommandText);
        using (new AssertionScope())
        {
            sql.Should().Contain("cast(arrayZip(mapKeys(mapSubtract(@p0, @p1)), mapValues(mapSubtract(@p0, @p1))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapUpdate(@p2, @p3)), mapValues(mapUpdate(@p2, @p3))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapReverseSort(@p4)), mapValues(mapReverseSort(@p4))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapPartialSort(1, @p5)), mapValues(mapPartialSort(1, @p5))) as Array(Tuple(String, Int32)))");
            command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().HaveCount(6,
                "each captured dictionary is bound exactly once");
        }
    }

    [Fact]
    public void CapturedArrays_ShouldBindAsOneParameterEach()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        string[] keys = ["a", "b"];
        int[] values = [1, 2];
        var command = Prepare(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.map_from_arrays(keys, values)
        }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("cast(arrayZip(mapKeys(mapFromArrays(@p0, @p1)), mapValues(mapFromArrays(@p0, @p1))) as Array(Tuple(String, Int32))) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().HaveCount(2);
    }

    [Fact]
    public void InlineMaps_ShouldRenderThroughTheMapConstructor()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.map_subtract(
                SqlFunctions.ClickHouse.map(Tuple.Create("a", 1), Tuple.Create("b", 2)),
                SqlFunctions.ClickHouse.map(Tuple.Create("b", 9)))
        }));

        sql.Should().Contain(
            "cast(arrayZip(mapKeys(mapSubtract(map('a', 1, 'b', 2), map('b', 9))), mapValues(mapSubtract(map('a', 1, 'b', 2), map('b', 9)))) as Array(Tuple(String, Int32))) as `V`");
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        (string Member, Type[] Generic, Type[] Parameters, Type Result)[] rows =
        [
            ("map_subtract", [typeof(string), typeof(int)],
                [typeof(IDictionary<string, int>), typeof(IDictionary<string, int>)], typeof(Tuple<string, int>[])),
            ("map_update", [typeof(string), typeof(int)],
                [typeof(IDictionary<string, int>), typeof(IDictionary<string, int>)], typeof(Tuple<string, int>[])),
            ("map_reverse_sort", [typeof(string), typeof(int)],
                [typeof(IDictionary<string, int>)], typeof(Tuple<string, int>[])),
            ("map_partial_sort", [typeof(string), typeof(int)],
                [typeof(long), typeof(IDictionary<string, int>)], typeof(Tuple<string, int>[])),
            ("map_from_arrays", [typeof(string), typeof(int)],
                [typeof(string[]), typeof(int[])], typeof(Tuple<string, int>[])),
            ("map_populate_series", [typeof(int), typeof(int)],
                [typeof(IDictionary<int, int>)], typeof(Tuple<int, int>[])),
            ("map_populate_series", [typeof(int), typeof(int)],
                [typeof(IDictionary<int, int>), typeof(long)], typeof(Tuple<int, int>[])),
            ("extract_key_value_pairs", [],
                [typeof(string)], typeof(Tuple<string, string>[])),
            ("extract_key_value_pairs", [],
                [typeof(string), typeof(string), typeof(string)], typeof(Tuple<string, string>[]))
        ];

        using (new AssertionScope())
        {
            foreach (var row in rows)
            {
                Resolve(row.Member, row.Generic, row.Parameters).ReturnType.Should().Be(
                    row.Result, "'{0}' declares its ClickHouse result shape", row.Member);
            }
        }

        static MethodInfo Resolve(string member, Type[] generic, Type[] parameters)
        {
            var candidates = typeof(ClickHouseFunctions).GetMethods()
                .Where(method => method.Name == member
                    && method.GetGenericArguments().Length == generic.Length
                    && method.GetParameters().Length == parameters.Length)
                .Where(method => generic.Length == 0 ? !method.IsGenericMethodDefinition : method.IsGenericMethodDefinition)
                .Select(method => generic.Length == 0 ? method : method.MakeGenericMethod(generic))
                .Where(method => method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters));

            return candidates.Single();
        }
    }

    [Fact]
    public void NullableComponents_ShouldBeRefused()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.map_subtract((IDictionary<string, int?>?)null, null)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_subtract*refused for the value type*Nullable*non-nullable operand*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.map_reverse_sort((IDictionary<int?, int>?)null)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_reverse_sort*refused for the key type*Nullable*key in a Map*");
        }
    }

    [Fact]
    public void MapPopulateSeries_ShouldRefuseNonIntegralKeys()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        Dictionary<string, int> stringKeys = [];

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.map_populate_series(stringKeys) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*map_populate_series*refused for the key type*String*integer key*");
    }

    [Fact]
    public void NullOperands_ShouldBeRefusedBeforeSql()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.map_update((IDictionary<string, int>?)null, x.AMap)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_update*refused for a null operand*not NULL-able*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.map_from_arrays((string[]?)null, x.Values)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_from_arrays*refused for a null operand*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.extract_key_value_pairs((string?)null)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*extract_key_value_pairs*refused for a null operand*");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Subtract = SqlFunctions.ClickHouse.map_subtract(x.AMap, x.BMap),
            Update = SqlFunctions.ClickHouse.map_update(x.AMap, x.BMap),
            Reverse = SqlFunctions.ClickHouse.map_reverse_sort(x.AMap),
            Partial = SqlFunctions.ClickHouse.map_partial_sort(2L, x.AMap),
            FromArrays = SqlFunctions.ClickHouse.map_from_arrays(x.Keys, x.Values),
            Populate = SqlFunctions.ClickHouse.map_populate_series(x.NumMap),
            Extract = SqlFunctions.ClickHouse.extract_key_value_pairs(x.Data)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("cast(arrayZip(mapKeys(mapSubtract(a_map, b_map)), mapValues(mapSubtract(a_map, b_map))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapUpdate(a_map, b_map)), mapValues(mapUpdate(a_map, b_map))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapReverseSort(a_map)), mapValues(mapReverseSort(a_map))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapPartialSort(2, a_map)), mapValues(mapPartialSort(2, a_map))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapFromArrays(keys, values)), mapValues(mapFromArrays(keys, values))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapPopulateSeries(num_map)), mapValues(mapPopulateSeries(num_map))) as Array(Tuple(Int32, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(extractKeyValuePairs(data)), mapValues(extractKeyValuePairs(data))) as Array(Tuple(String, String)))");
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bMapEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.map_from_arrays(x.Keys, x.Values).Length > 0
                && SqlFunctions.ClickHouse.map_reverse_sort(x.AMap).Length >= 1)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapFromArrays(keys, values)), mapValues(mapFromArrays(keys, values))) as Array(Tuple(String, Int32)))");
            sql.Should().Contain("cast(arrayZip(mapKeys(mapReverseSort(a_map)), mapValues(mapReverseSort(a_map))) as Array(Tuple(String, Int32)))");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286bMapEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_reverse_sort(x.AMap) }));

        sql.Should().Contain("cast(arrayZip(mapKeys(mapReverseSort(`a_map`)), mapValues(mapReverseSort(`a_map`))) as Array(Tuple(String, Int32))) as `V`");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryMember()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "map_subtract" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.map_subtract(x.AMap, x.BMap) })),
                "map_update" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.map_update(x.AMap, x.BMap) })),
                "map_reverse_sort" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.map_reverse_sort(x.AMap) })),
                "map_partial_sort" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.map_partial_sort(2L, x.AMap) })),
                "map_from_arrays" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.map_from_arrays(x.Keys, x.Values) })),
                "map_populate_series" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.map_populate_series(x.NumMap) })),
                "extract_key_value_pairs" => () => SqlOf(ctx, ctx.From<IT286bMapEntity>().Select(x => new { V = SqlFunctions.ClickHouse.extract_key_value_pairs(x.Data) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider", name);
        }
    }
}
