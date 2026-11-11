using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;
using Npgsql;
using NpgsqlTypes;

namespace NextORM.Postgres.Tests;

/// <summary>
/// A probe entity carrying one native <c>json</c> column (<c>doc_json</c>) and one native
/// <c>jsonb</c> column (<c>doc_jsonb</c>). SQL generation only sees the column reference, so the two
/// rows prove the same SQL obligation (no document cast, typed right operand) for both physical
/// types; the physical DDL and the evaluated result types are the live-test half of R11-R14/R23-R26.
/// </summary>
[SqlTable("json_access_probe")]
public interface IJsonAccessProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>A native SQL <c>json</c> column.</summary>
    [Column("doc_json")]
    JsonDocument DocJson { get; set; }

    /// <summary>A native SQL <c>jsonb</c> column.</summary>
    [Column("doc_jsonb")]
    JsonElement DocJsonb { get; set; }
}

public sealed class JsonAccessProbe : IJsonAccessProbe
{
    public int Id { get; set; }
    public JsonDocument DocJson { get; set; } = JsonDocument.Parse("{}");
    public JsonElement DocJsonb { get; set; }
}

/// <summary>
/// The SQL-shape matrix for the PostgreSQL JSON accessors. PostgreSQL cannot resolve the access
/// operator when either operand is <c>unknown</c> (SQLSTATE 42725), so the translation types both
/// sides: a raw client value (a literal, a captured value or a bound parameter) becomes
/// <c>cast(... as jsonb)</c>, a mapped native <c>json</c>/<c>jsonb</c> column or a server-side JSON
/// expression stays uncast, and the key/index/path operand is always cast to <c>text</c>,
/// <c>integer</c> or <c>text[]</c>. Every cell asserts the exact frozen shape, not merely the
/// presence of an operator. No database connection is used.
/// </summary>
public class JsonAccessSqlGenerationTests
{
    /// <summary>A raw literal document; compiled into an expression tree as a <c>ConstantExpression</c>.</summary>
    private const string Flat = "{\"a\":1}";

    /// <summary>A nested JSONB fixture document.</summary>
    private const string Nested = "{\"a\":{\"b\":[1,2]}}";

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText.Replace("\r\n", "\n");

    /// <summary>The frozen document-cast fragment of a raw literal JSON value.</summary>
    private static string LiteralDocument(string json)
        => $"cast(E'{json.Replace("\\", "\\\\").Replace("'", "\\'")}' as jsonb)";

    // - R03-R06: literal document, four right-operand kinds -

    [Fact]
    public void T263_R03_LiteralDocument_LiteralKey_ShouldCastBothOperands()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, "a") }))
            .Should().Contain($"{LiteralDocument(Flat)} -> cast(E'a' as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(Flat, "a") }))
            .Should().Contain($"{LiteralDocument(Flat)} ->> cast(E'a' as text)");
    }

    [Fact]
    public void T263_R04_LiteralDocument_CapturedKey_ShouldCastKeyAsTextParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "a";

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, key) }))
            .Should().Contain($"{LiteralDocument(Flat)} -> cast(@key as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(Flat, key) }))
            .Should().Contain($"{LiteralDocument(Flat)} ->> cast(@key as text)");
    }

    [Fact]
    public void T263_R05_LiteralDocument_IntegerIndex_ShouldCastIndexAsInteger()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, 1) }));
        sql.Should().Contain($"{LiteralDocument(Flat)} -> cast(1 as integer)");
        sql.Should().NotContain("cast(1 as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(Flat, 0) }))
            .Should().Contain($"{LiteralDocument(Flat)} ->> cast(0 as integer)");
    }

    [Fact]
    public void T263_R06_LiteralDocument_PathOverloads_ShouldCastPathAsTextArray()
    {
        using var ctx = PostgresTestContext.Create();
        string?[] path = ["a", "b"];
        string?[] empty = [];

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(Flat, path) }))
            .Should().Contain($"{LiteralDocument(Flat)} #> cast(@p0 as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(Flat, path) }))
            .Should().Contain($"{LiteralDocument(Flat)} #>> cast(@p0 as text[])");

        // An empty captured path is still a bound text[] parameter, and a literal path is inlined.
        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(Flat, empty) }))
            .Should().Contain($"{LiteralDocument(Flat)} #> cast(@p0 as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(Flat, new[] { "a", "b" }) }))
            .Should().Contain($"{LiteralDocument(Flat)} #>> cast(array[E'a', E'b'] as text[])");
    }

    // - R07-R10: parameter raw document, four right-operand kinds -

    [Fact]
    public void T263_R07_ParameterDocument_LiteralKey_ShouldCastDocumentAndKey()
    {
        using var ctx = PostgresTestContext.Create();
        var document = Flat;

        // A captured string and an explicit positional parameter both bind as a client value.
        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(document, "a") }))
            .Should().Contain("cast(@document as jsonb) -> cast(E'a' as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<string>(0), "a") }))
            .Should().Contain("cast(@norm_p0 as jsonb) ->> cast(E'a' as text)");
    }

    [Fact]
    public void T263_R08_ParameterDocument_ParameterKey_ShouldTypeDocumentAndKeyIndependently()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "a";

        var sql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<string>(0), key) }));

        sql.Should().Contain("cast(@norm_p0 as jsonb) -> cast(@key as text)");
        sql.Should().NotContain("cast(@norm_p0 as text)");
    }

    [Fact]
    public void T263_R09_ParameterDocument_IntegerIndex_ShouldCastIndexAsInteger()
    {
        using var ctx = PostgresTestContext.Create();
        var document = Flat;

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(document, 1) }))
            .Should().Contain("cast(@document as jsonb) -> cast(1 as integer)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<string>(0), 0) }))
            .Should().Contain("cast(@norm_p0 as jsonb) ->> cast(0 as integer)");
    }

    [Fact]
    public void T263_R10_ParameterDocument_Path_ShouldCastPathAsTextArray()
    {
        using var ctx = PostgresTestContext.Create();
        var document = Flat;
        string?[] path = ["a", "b"];

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(document, path) }))
            .Should().Contain("cast(@document as jsonb) #> cast(@p0 as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(SqlFunctions.Parameter<string>(0), path) }))
            .Should().Contain("cast(@norm_p0 as jsonb) #>> cast(@p0 as text[])");
    }

    // - R11-R14: native json column, no document cast -

    [Fact]
    public void T263_R11_NativeJsonColumn_LiteralKey_ShouldNotCastDocument()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IJsonAccessProbe>();

        var sql = SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(x.DocJson, "a") }));
        sql.Should().Contain("doc_json -> cast(E'a' as text)");
        sql.Should().NotContain("cast(doc_json");

        SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(x.DocJson, "a") }))
            .Should().Contain("doc_json ->> cast(E'a' as text)");
    }

    [Fact]
    public void T263_R12_NativeJsonColumn_ParameterKey_ShouldCastOnlyTheKey()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "a";

        var sql = SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(x.DocJson, key) }));

        sql.Should().Contain("doc_json -> cast(@key as text)");
        sql.Should().NotContain("cast(doc_json");
    }

    [Fact]
    public void T263_R13_NativeJsonColumn_IntegerIndex_ShouldCastOnlyTheIndex()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(x.DocJson, 0) }));

        sql.Should().Contain("doc_json -> cast(0 as integer)");
        sql.Should().NotContain("cast(doc_json");
    }

    [Fact]
    public void T263_R14_NativeJsonColumn_Path_ShouldCastOnlyThePath()
    {
        using var ctx = PostgresTestContext.Create();
        string?[] path = ["a", "b"];

        SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(x.DocJson, path) }))
            .Should().Contain("doc_json #> cast(@p0 as text[])");

        var sql = SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(x.DocJson, new[] { "a", "b" }) }));
        sql.Should().Contain("doc_json #>> cast(array[E'a', E'b'] as text[])");
        sql.Should().NotContain("cast(doc_json");
    }

    // - R15-R18: jsonb fixture raw literal, four right-operand kinds -

    [Fact]
    public void T263_R15_JsonbFixtureLiteral_LiteralKey_ShouldCastBothOperands()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(Nested, "a") }))
            .Should().Contain($"{LiteralDocument(Nested)} -> cast(E'a' as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(Nested, "b") }))
            .Should().Contain($"{LiteralDocument(Nested)} ->> cast(E'b' as text)");
    }

    [Fact]
    public void T263_R16_JsonbFixtureLiteral_ParameterKey_ShouldCastKeyAsTextParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "a";

        var sql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(Nested, key) }));

        sql.Should().Contain($"{LiteralDocument(Nested)} -> cast(@key as text)");
        sql.Should().Contain("cast(E'{\"a\":{\"b\":[1,2]}}' as jsonb)");
    }

    [Fact]
    public void T263_R17_JsonbFixtureLiteral_IntegerIndex_ShouldCastIndexAsInteger()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(Nested, 0) }))
            .Should().Contain($"{LiteralDocument(Nested)} -> cast(0 as integer)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(Nested, 1) }))
            .Should().Contain($"{LiteralDocument(Nested)} ->> cast(1 as integer)");
    }

    [Fact]
    public void T263_R18_JsonbFixtureLiteral_Path_ShouldCastPathAsTextArray()
    {
        using var ctx = PostgresTestContext.Create();
        string?[] path = ["a", "b"];
        string?[] empty = [];

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(Nested, path) }))
            .Should().Contain($"{LiteralDocument(Nested)} #> cast(@p0 as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(Nested, empty) }))
            .Should().Contain($"{LiteralDocument(Nested)} #>> cast(@p0 as text[])");
    }

    // - R19-R22: JSONB/raw parameter, four right-operand kinds -

    [Fact]
    public void T263_R19_JsonParameter_LiteralKey_ShouldCastDocument()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonDocument>(0), "a") }))
            .Should().Contain("cast(@norm_p0 as jsonb) -> cast(E'a' as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), "a") }))
            .Should().Contain("cast(@norm_p0 as jsonb) ->> cast(E'a' as text)");
    }

    [Fact]
    public void T263_R20_JsonParameter_ParameterKey_ShouldTypeBothIndependently()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "a";

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonDocument>(0), key) }))
            .Should().Contain("cast(@norm_p0 as jsonb) -> cast(@key as text)");
    }

    [Fact]
    public void T263_R21_JsonParameter_IntegerIndex_ShouldCastInteger()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), 0) }))
            .Should().Contain("cast(@norm_p0 as jsonb) ->> cast(0 as integer)");
    }

    [Fact]
    public void T263_R22_JsonParameter_Path_ShouldCastTextArray()
    {
        using var ctx = PostgresTestContext.Create();
        string?[] path = ["a", "b"];

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(SqlFunctions.Parameter<JsonDocument>(0), path) }))
            .Should().Contain("cast(@norm_p0 as jsonb) #> cast(@p0 as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(SqlFunctions.Parameter<JsonDocument>(0), new[] { "a" }) }))
            .Should().Contain("cast(@norm_p0 as jsonb) #>> cast(array[E'a'] as text[])");
    }

    // - R23-R26: native jsonb column, no document cast -

    [Fact]
    public void T263_R23_NativeJsonbColumn_LiteralKey_ShouldNotCastDocument()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(x.DocJsonb, "a") }));
        sql.Should().Contain("doc_jsonb -> cast(E'a' as text)");
        sql.Should().NotContain("cast(doc_jsonb");

        SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(x.DocJsonb, "a") }))
            .Should().Contain("doc_jsonb ->> cast(E'a' as text)");
    }

    [Fact]
    public void T263_R24_NativeJsonbColumn_ParameterKey_ShouldCastOnlyTheKey()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "a";

        var sql = SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(x.DocJsonb, key) }));

        sql.Should().Contain("doc_jsonb -> cast(@key as text)");
        sql.Should().NotContain("cast(doc_jsonb");
    }

    [Fact]
    public void T263_R25_NativeJsonbColumn_IntegerIndex_ShouldCastOnlyTheIndex()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(x.DocJsonb, 1) }))
            .Should().Contain("doc_jsonb -> cast(1 as integer)");
    }

    [Fact]
    public void T263_R26_NativeJsonbColumn_Path_ShouldCastOnlyThePath()
    {
        using var ctx = PostgresTestContext.Create();
        string?[] path = ["a", "b"];

        var sql = SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text(x.DocJsonb, path) }));
        sql.Should().Contain("doc_jsonb #>> cast(@p0 as text[])");
        sql.Should().NotContain("cast(doc_jsonb");

        SqlOf(ctx, ctx.From<IJsonAccessProbe>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(x.DocJsonb, new[] { "a" }) }))
            .Should().Contain("doc_jsonb #> cast(array[E'a'] as text[])");
    }

    // - R27: null documents stay typed -

    [Fact]
    public void T263_R27_NullDocument_ShouldEmitTypedNullInsteadOfBareNull()
    {
        using var ctx = PostgresTestContext.Create();
        var captured = (string?)null;

        var literal = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get((string?)null, "a") }));
        literal.Should().Contain("cast(null as jsonb) -> cast(E'a' as text)");
        literal.Should().NotContain("(null ->");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(captured, "a") }))
            .Should().Contain("cast(@captured as jsonb) -> cast(E'a' as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<string>(0), "a") }))
            .Should().Contain("cast(@norm_p0 as jsonb) ->> cast(E'a' as text)");
    }

    // - R28: null key/path/element and both operands null -

    [Fact]
    public void T263_R28_NullKeyAndPath_ShouldEmitTypedNulls()
    {
        using var ctx = PostgresTestContext.Create();
        var capturedPath = (string?[])null!;

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, (string?)null) }))
            .Should().Contain($"{LiteralDocument(Flat)} -> cast(null as text)");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(Flat, default(string?[])!) }))
            .Should().Contain($"{LiteralDocument(Flat)} #> cast(null as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(Flat, capturedPath) }))
            .Should().Contain($"{LiteralDocument(Flat)} #> cast(@p0 as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get_path(Flat, new string?[] { "a", null }) }))
            .Should().Contain($"{LiteralDocument(Flat)} #> cast(array[E'a', null] as text[])");

        SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_get((string?)null, (string?)null) }))
            .Should().Contain("cast(null as jsonb) -> cast(null as text)");
    }

    // - R29: key "0" versus index 0, negative index -

    [Fact]
    public void T263_R29_StringKeyZero_And_IntegerIndexZero_ShouldProduceDistinctSql()
    {
        using var ctx = PostgresTestContext.Create();

        var keySql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, "0") }));
        var indexSql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, 0) }));
        var negativeSql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, -7) }));

        keySql.Should().Contain($"{LiteralDocument(Flat)} -> cast(E'0' as text)");
        indexSql.Should().Contain($"{LiteralDocument(Flat)} -> cast(0 as integer)");
        negativeSql.Should().Contain($"{LiteralDocument(Flat)} -> cast(-7 as integer)");
        keySql.Should().NotBe(indexSql);
    }

    // - R31: apostrophe safety -

    [Fact]
    public void T263_R31_ApostropheInDocumentAndKey_ShouldDoubleQuotes()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get("{\"a\":\"it's\"}", "it's") }));

        sql.Should().Contain("cast(E'{\"a\":\"it\\'s\"}' as jsonb) -> cast(E'it\\'s' as text)");
        sql.Should().NotContain("it's");
    }

    // - R32: JsonDocument/JsonNode/JsonElement inputs are bound as jsonb -

    [Fact]
    public void T263_R32_StructuredJsonInputs_ShouldCastDocumentAndBindAsJsonb()
    {
        using var ctx = PostgresTestContext.Create();
        var document = JsonDocument.Parse(Flat);
        var node = JsonNode.Parse(Flat)!;
        var element = JsonDocument.Parse(Flat).RootElement;

        var documentCommand = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(document, "a") }));
        documentCommand.DbCommand.CommandText.Should().Contain("cast(@document as jsonb) -> cast(E'a' as text)");

        var nodeCommand = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(node, "a") }));
        nodeCommand.DbCommand.CommandText.Should().Contain("cast(@node as jsonb) -> cast(E'a' as text)");

        var elementCommand = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(element, "a") }));
        elementCommand.DbCommand.CommandText.Should().Contain("cast(@element as jsonb) -> cast(E'a' as text)");

        // CreateParam binds every structured JSON CLR input as jsonb, so the cast above matches the
        // bound provider type; no typed-json parameter API is invented.
        Param(documentCommand, "document").NpgsqlDbType.Should().Be(NpgsqlDbType.Jsonb);
        Param(nodeCommand, "node").NpgsqlDbType.Should().Be(NpgsqlDbType.Jsonb);
        Param(elementCommand, "element").NpgsqlDbType.Should().Be(NpgsqlDbType.Jsonb);
    }

    // - R34: server JSON expressions stay native -

    [Fact]
    public void T263_R34_ServerJsonExpression_ShouldNotCastDocument()
    {
        using var ctx = PostgresTestContext.Create();

        var json = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(SqlFunctions.Postgres.json_build_object("a", x.Id), "a") }));
        json.Should().Contain("json_build_object(E'a', id) -> cast(E'a' as text)");
        json.Should().NotContain("cast(json_build_object");

        var jsonb = SqlOf(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_text(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), 0) }));
        jsonb.Should().Contain("jsonb_build_object(E'a', id) ->> cast(0 as integer)");
        jsonb.Should().NotContain("cast(jsonb_build_object");
    }

    // - Provider guard: the frozen rejection message on a non-PostgreSQL dialect -

    [Fact]
    public void T263_NonPostgresDialect_ShouldThrowFrozenGuardMessage()
    {
        // A real non-PostgreSQL SQL dialect (SQL Server) is used because the guard lives in the SQL
        // renderer; the executable SQLite form of the same rejection is R33/AC6.
        using var ctx = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(Flat, "a") }));

        act.Should().Throw<NotSupportedException>().WithMessage(
            "JSON is not supported by this provider: the json/jsonb functions and operators require PostgreSQL.");
    }

    private static NpgsqlParameter Param<T>(DbPreparedQueryCommand<T> command, string name)
        => command.DbCommandParams.Cast<NpgsqlParameter>().Single(p => p.ParameterName == name);
}
