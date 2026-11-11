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
/// T286b D9 (F09 JSON): the ClickHouse-only JSON members. The tests pin every rendered arm
/// (<c>JSONMergePatch</c>, <c>JSONArrayLength</c>, the native-JSON <c>JSONDynamicPaths</c>/
/// <c>JSONSharedDataPaths</c> pairs with their <c>WithTypes</c> siblings, <c>JSONKey</c>,
/// <c>JSONExtractKeysAndValuesRaw</c>, <c>JSONExtractUInt</c>, <c>isValidJSON</c> and the
/// <c>simpleJSONExtract*</c> fast path), the declared CLR result shapes and their nullability, and
/// the deterministic refusals of the two members absent from the pinned 25.8 server
/// (<c>prettyPrintJSON</c>, <c>JSONExtractUIntOrDefault</c>). They never open a connection.
/// </summary>
public class T286bF09JsonFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f09_entity")]
    internal interface IT286bJsonEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("doc")]
        string? Doc { get; set; }

        [Column("ndoc")]
        string? NativeDoc { get; set; }

        [Column("path")]
        string? Path { get; set; }

        [Column("field")]
        string? Field { get; set; }

        [Column("idx")]
        int Index { get; set; }
    }

    /// <summary>The delivered catalog names, one per non-refused member.</summary>
    private static readonly string[] DeliveredNames =
    [
        "json_merge_patch", "json_array_length", "json_dynamic_paths",
        "json_dynamic_paths_with_types", "json_shared_data_paths",
        "json_shared_data_paths_with_types", "json_key", "json_extract_keys_and_values_raw",
        "json_extract_uint", "is_valid_json", "simple_json_extract_raw",
        "simple_json_extract_string", "simple_json_extract_int", "simple_json_extract_uint",
        "simple_json_extract_float", "simple_json_extract_bool"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch("{\"a\":1}", "{\"b\":2}") }))
                .Should().Contain("JSONMergePatch('{\"a\":1}', '{\"b\":2}') as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch(x.Doc, "{\"b\":2}", "{\"c\":3}") }))
                .Should().Contain("JSONMergePatch(doc, '{\"b\":2}', '{\"c\":3}') as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_array_length(x.Doc) }))
                .Should().Contain("toInt64(JSONArrayLength(doc)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc) }))
                .Should().Contain("JSONDynamicPaths(ndoc) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc) }))
                .Should().Contain("JSONDynamicPathsWithTypes(ndoc) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc) }))
                .Should().Contain("JSONSharedDataPaths(ndoc) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc) }))
                .Should().Contain("JSONSharedDataPathsWithTypes(ndoc) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc) }))
                .Should().Contain("toNullable(JSONKey(doc)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc, "a") }))
                .Should().Contain("toNullable(JSONKey(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc, 1) }))
                .Should().Contain("toNullable(JSONKey(doc, 1)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.Doc) }))
                .Should().Contain("JSONExtractKeysAndValuesRaw(doc) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.Doc, "o") }))
                .Should().Contain("JSONExtractKeysAndValuesRaw(doc, 'o') as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a") }))
                .Should().Contain("toNullable(JSONExtractUInt(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.is_valid_json(x.Doc) }))
                .Should().Contain("toNullable(isValidJSON(doc)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a") }))
                .Should().Contain("toNullable(simpleJSONExtractRaw(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a") }))
                .Should().Contain("toNullable(simpleJSONExtractString(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_int(x.Doc, "a") }))
                .Should().Contain("toNullable(simpleJSONExtractInt(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_uint(x.Doc, "a") }))
                .Should().Contain("toNullable(simpleJSONExtractUInt(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_float(x.Doc, "a") }))
                .Should().Contain("toNullable(simpleJSONExtractFloat(doc, 'a')) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_bool(x.Doc, "a") }))
                .Should().Contain("toNullable(simpleJSONExtractBool(doc, 'a')) as `V`");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            ReturnType("json_merge_patch", [typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("json_merge_patch", [typeof(string), typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("json_array_length", [typeof(string)]).Should().Be(typeof(long?));
            ReturnType("json_dynamic_paths", [typeof(string)]).Should().Be(typeof(string[]));
            ReturnType("json_dynamic_paths_with_types", [typeof(string)]).Should().Be(typeof(Dictionary<string, string>));
            ReturnType("json_shared_data_paths", [typeof(string)]).Should().Be(typeof(string[]));
            ReturnType("json_shared_data_paths_with_types", [typeof(string)]).Should().Be(typeof(Dictionary<string, string>));
            ReturnType("json_key", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("json_key", [typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("json_key", [typeof(string), typeof(int)]).Should().Be(typeof(string));
            ReturnType("json_extract_keys_and_values_raw", [typeof(string)]).Should().Be(typeof(Tuple<string, string>[]));
            ReturnType("json_extract_keys_and_values_raw", [typeof(string), typeof(string)]).Should().Be(typeof(Tuple<string, string>[]));
            ReturnType("json_extract_uint", [typeof(string), typeof(string)]).Should().Be(typeof(ulong?));
            ReturnType("is_valid_json", [typeof(string)]).Should().Be(typeof(bool?));
            ReturnType("simple_json_extract_raw", [typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("simple_json_extract_string", [typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("simple_json_extract_int", [typeof(string), typeof(string)]).Should().Be(typeof(long?));
            ReturnType("simple_json_extract_uint", [typeof(string), typeof(string)]).Should().Be(typeof(ulong?));
            ReturnType("simple_json_extract_float", [typeof(string), typeof(string)]).Should().Be(typeof(double?));
            ReturnType("simple_json_extract_bool", [typeof(string), typeof(string)]).Should().Be(typeof(bool?));
        }

        static Type ReturnType(string name, Type[] arguments)
            => typeof(ClickHouseFunctions)
                .GetMethod(name, BindingFlags.Public | BindingFlags.Instance, arguments)!
                .ReturnType;
    }

    [Fact]
    public void MergePatch_ShouldBindCapturedOperandsAndKeepTheVendorStringResult()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        var patch = "{\"b\":2}";
        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch(x.Doc, patch) }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("JSONMergePatch(doc, @patch) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle()
            .Which.Value.Should().Be(patch);
    }

    [Fact]
    public void NativeJsonPathMembers_ShouldUseTheColumnAndTheWithTypesSiblings()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            D = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc),
            DT = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc),
            S = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc),
            ST = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("JSONDynamicPaths(ndoc) as `D`");
            sql.Should().Contain("JSONDynamicPathsWithTypes(ndoc) as `DT`");
            sql.Should().Contain("JSONSharedDataPaths(ndoc) as `S`");
            sql.Should().Contain("JSONSharedDataPathsWithTypes(ndoc) as `ST`");
            sql.Should().NotContain("JSONDynamicPathsWithTypes(ndoc) as `D`");
        }
    }

    [Fact]
    public void JsonArrayLength_ShouldKeepTheNullableInt64CastAndAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        SqlOf(ctx, e.Select(x => new { Length = SqlFunctions.ClickHouse.json_array_length(x.Doc) }))
            .Should().Contain("toInt64(JSONArrayLength(doc)) as `Length`");
    }

    [Fact]
    public void JsonKey_ShouldRenderTheOneKeyAndIndexForms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc) }))
                .Should().Contain("toNullable(JSONKey(doc)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc, x.Path) }))
                .Should().Contain("toNullable(JSONKey(doc, path)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc, x.Index) }))
                .Should().Contain("toNullable(JSONKey(doc, idx)) as `V`");
        }
    }

    [Fact]
    public void SimpleJsonFastPath_ShouldRenderEveryNullableVendorArm()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a"),
            S = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a"),
            I = SqlFunctions.ClickHouse.simple_json_extract_int(x.Doc, "a"),
            U = SqlFunctions.ClickHouse.simple_json_extract_uint(x.Doc, "a"),
            F = SqlFunctions.ClickHouse.simple_json_extract_float(x.Doc, "a"),
            B = SqlFunctions.ClickHouse.simple_json_extract_bool(x.Doc, "a")
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toNullable(simpleJSONExtractRaw(doc, 'a')) as `R`");
            sql.Should().Contain("toNullable(simpleJSONExtractString(doc, 'a')) as `S`");
            sql.Should().Contain("toNullable(simpleJSONExtractInt(doc, 'a')) as `I`");
            sql.Should().Contain("toNullable(simpleJSONExtractUInt(doc, 'a')) as `U`");
            sql.Should().Contain("toNullable(simpleJSONExtractFloat(doc, 'a')) as `F`");
            sql.Should().Contain("toNullable(simpleJSONExtractBool(doc, 'a')) as `B`");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMemberInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.ClickHouse.json_merge_patch(x.Doc, "{\"b\":2}"),
            AL = SqlFunctions.ClickHouse.json_array_length(x.Doc),
            DP = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc),
            DPT = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc),
            SP = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc),
            SPT = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc),
            K = SqlFunctions.ClickHouse.json_key(x.Doc, 1),
            KV = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.Doc),
            UI = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a"),
            V = SqlFunctions.ClickHouse.is_valid_json(x.Doc),
            SR = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a"),
            SS = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a"),
            SI = SqlFunctions.ClickHouse.simple_json_extract_int(x.Doc, "a"),
            SU = SqlFunctions.ClickHouse.simple_json_extract_uint(x.Doc, "a"),
            SF = SqlFunctions.ClickHouse.simple_json_extract_float(x.Doc, "a"),
            SB = SqlFunctions.ClickHouse.simple_json_extract_bool(x.Doc, "a")
        }));

        foreach (var name in new[] { "M", "AL", "DP", "DPT", "SP", "SPT", "K", "KV", "UI", "V", "SR", "SS", "SI", "SU", "SF", "SB" })
            sql.Should().Contain($"as `{name}`");
    }

    [Fact]
    public void Predicate_ShouldKeepTheJsonValidationInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.ClickHouse.is_valid_json(x.Doc) == true).Select(x => new { x.Id }))
            .Should().Contain("where toNullable(isValidJSON(doc)) = true");
    }

    [Fact]
    public void AlreadyCoveredSurface_ShouldKeepTheExistingJsonArms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bJsonEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.json_extract_string(x.Doc, "b"),
            I = SqlFunctions.ClickHouse.json_extract_int(x.Doc, "a"),
            F = SqlFunctions.ClickHouse.json_extract_float(x.Doc, "f"),
            B = SqlFunctions.ClickHouse.json_extract_bool(x.Doc, "t"),
            R = SqlFunctions.ClickHouse.json_extract_raw(x.Doc, "d"),
            A = SqlFunctions.ClickHouse.json_extract_array_raw(x.Doc, "c"),
            K = SqlFunctions.ClickHouse.json_extract_keys(x.Doc),
            KV = SqlFunctions.ClickHouse.json_extract_keys_and_values<int>(x.Doc),
            H = SqlFunctions.ClickHouse.json_has(x.Doc, "a"),
            L = SqlFunctions.ClickHouse.json_length(x.Doc, "c"),
            T = SqlFunctions.ClickHouse.json_type(x.Doc, "b"),
            V = SqlFunctions.ClickHouse.visit_param_extract_string(x.Doc, "b"),
            P = SqlFunctions.ClickHouse.json_all_paths(x.NativeDoc),
            PT = SqlFunctions.ClickHouse.json_all_paths_with_types(x.NativeDoc),
            J = SqlFunctions.ClickHouse.to_json_string(x.NativeDoc)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("JSONExtractString(doc, 'b') as `S`");
            sql.Should().Contain("JSONExtractInt(doc, 'a') as `I`");
            sql.Should().Contain("JSONExtractFloat(doc, 'f') as `F`");
            sql.Should().Contain("JSONExtractBool(doc, 't') as `B`");
            sql.Should().Contain("JSONExtractRaw(doc, 'd') as `R`");
            sql.Should().Contain("JSONExtractArrayRaw(doc, 'c') as `A`");
            sql.Should().Contain("JSONExtractKeys(doc) as `K`");
            sql.Should().Contain("JSONExtractKeysAndValues(doc, 'Int32') as `KV`");
            sql.Should().Contain("JSONHas(doc, 'a') as `H`");
            sql.Should().Contain("toInt64(JSONLength(doc, 'c')) as `L`");
            sql.Should().Contain("JSONType(doc, 'b') as `T`");
            sql.Should().Contain("visitParamExtractString(doc, 'b') as `V`");
            sql.Should().Contain("JSONAllPaths(ndoc) as `P`");
            sql.Should().Contain("JSONAllPathsWithTypes(ndoc) as `PT`");
            sql.Should().Contain("toJSONString(ndoc) as `J`");
        }
    }

    [Fact]
    public void RefusedMembers_ShouldFailClosedWithThePreciseReasonOnEveryProvider()
    {
        using var clickhouse = ClickHouseTestContext.Create();
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var ctx in new IDataContext[] { clickhouse, postgres, sqlServer })
            {
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bJsonEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.pretty_print_json(x.Doc) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("*prettyPrintJSON*26.4*25.8.33.6*UNKNOWN_FUNCTION*");

                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bJsonEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.json_extract_uint_or_default(x.Doc, "a", 7UL) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("*JSONExtractUIntOrDefault*25.8.33.6*UNKNOWN_FUNCTION*invent a semantic*");
            }
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
                AssertRejected(postgres, name, "*not supported by this provider*");
                AssertRejected(sqlServer, name, "*not supported by this provider*");
            }

            // The text-JSON and native-JSON arms keep distinct, precise messages.
            AssertRejected(postgres, "json_extract_uint", "*JSONExtract*JSON-as-text*not supported*");
            AssertRejected(postgres, "json_shared_data_paths", "*native-JSON*not supported*");
        }

        static void AssertRejected(IDataContext ctx, string name, string message)
        {
            Func<string> query = name switch
            {
                "json_merge_patch" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch(x.Doc, "{\"b\":2}") })),
                "json_array_length" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_array_length(x.Doc) })),
                "json_dynamic_paths" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc) })),
                "json_dynamic_paths_with_types" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc) })),
                "json_shared_data_paths" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc) })),
                "json_shared_data_paths_with_types" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc) })),
                "json_key" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc, "a") })),
                "json_extract_keys_and_values_raw" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.Doc) })),
                "json_extract_uint" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a") })),
                "is_valid_json" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.is_valid_json(x.Doc) })),
                "simple_json_extract_raw" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a") })),
                "simple_json_extract_string" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a") })),
                "simple_json_extract_int" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_int(x.Doc, "a") })),
                "simple_json_extract_uint" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_uint(x.Doc, "a") })),
                "simple_json_extract_float" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_float(x.Doc, "a") })),
                "simple_json_extract_bool" => () => SqlOf(ctx, ctx.From<IT286bJsonEntity>().Select(x => new { V = SqlFunctions.ClickHouse.simple_json_extract_bool(x.Doc, "a") })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider: {1}", name, message)
                .WithMessage(message);
        }
    }
}
