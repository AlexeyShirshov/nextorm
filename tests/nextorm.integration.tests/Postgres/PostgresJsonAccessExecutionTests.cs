using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;
using Npgsql;

namespace NextORM.Integration.Tests;

/// <summary>
/// A probe entity carrying one native SQL <c>json</c> column and one native SQL <c>jsonb</c> column.
/// The live rows prove the physical result family of the access operators on each column and that
/// the document operand itself is never coerced to the other type.
/// </summary>
[SqlTable("t263_json_access_probe")]
public interface IT263JsonAccessProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("doc_json")]
    JsonNode DocJson { get; set; }

    [Column("doc_jsonb")]
    JsonNode DocJsonb { get; set; }
}

public sealed class T263JsonAccessProbe : IT263JsonAccessProbe
{
    public int Id { get; set; }
    public JsonNode DocJson { get; set; } = null!;
    public JsonNode DocJsonb { get; set; } = null!;
}

/// <summary>
/// PostgreSQL-only live coverage for the JSON accessors (<c>json -&gt; key</c> and friends). The
/// baseline translation leaves both operands untyped, so PostgreSQL cannot choose between the
/// <c>json</c> and <c>jsonb</c> operator families and rejects the statement with SQLSTATE 42725
/// (<c>operator is not unique: unknown -&gt; unknown</c>). The tests assert that the operator
/// resolves on a real server, keeps its documented result shape and preserves a native document
/// operand instead of coercing it.
/// </summary>
[Collection("Postgres")]
public sealed class PostgresJsonAccessExecutionTests : ProviderTestSuite
{
    private const string FlatObject = """{"a":1,"s":"x"}""";
    private const string NativeTable = "t263_json_access_probe";

    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    /// <summary>
    /// AC1: a constant raw JSON document and a constant key must resolve <c>json -&gt; key</c>
    /// instead of failing with <c>42725</c>. The accessor returns the JSON element for the key; the
    /// numeric element <c>1</c> arrives as the text <c>1</c>.
    /// </summary>
    [Fact]
    public void T263_ConstantDocument_JsonGet_ReturnsOne()
    {
        var (serverVersionNum, major) = ReadServerVersion();
        major.Should().Be(17,
            $"the T263 evidence requires the live PostgreSQL 17 server (server_version_num={serverVersionNum})");

        var sql = GeneratedSql();
        Console.WriteLine($"[T263] AC1 generated SQL: {sql}");

        string? value = null;
        Exception? failure = null;
        try
        {
            value = _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.Postgres.json_get("{\"a\":1}", "a"))
                .First();
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        failure.Should().BeNull(
            "the raw literal document and key must be typed so PostgreSQL resolves the json access operator; "
            + "generated SQL: {0}; server_version_num={1}",
            sql,
            serverVersionNum);

        value.Should().Be("1", "json_get returns the JSON element for key 'a' as text; generated SQL: {0}", sql);

        using var document = JsonDocument.Parse(value!);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Number);
        document.RootElement.GetInt32().Should().Be(1);
    }

    // - R03-R06: raw literal document, four right-operand kinds -

    [Fact]
    public void T263_R03_LiteralDocument_LiteralKey_ShouldResolveJsonAndText()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(FlatObject, "a"),
                Text = SqlFunctions.Postgres.json_get_text(FlatObject, "a"),
                Quoted = SqlFunctions.Postgres.json_get(FlatObject, "s"),
                Unquoted = SqlFunctions.Postgres.json_get_text(FlatObject, "s")
            })
            .First();

        row.Json.Should().Be("1");
        row.Text.Should().Be("1");
        row.Quoted.Should().Be("\"x\"", "the -> result is a JSON string value, quotes included");
        row.Unquoted.Should().Be("x", "the ->> result is plain text");
    }

    [Fact]
    public void T263_R04_LiteralDocument_CapturedKey_ShouldResolve()
    {
        var key = "s";

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(FlatObject, key),
                Text = SqlFunctions.Postgres.json_get_text(FlatObject, key)
            })
            .First();

        row.Json.Should().Be("\"x\"");
        row.Text.Should().Be("x");
    }

    [Fact]
    public void T263_R05_LiteralDocument_IntegerIndex_ShouldResolveArrayElements()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                First = SqlFunctions.Postgres.json_get("""["zero","one"]""", 0),
                LastText = SqlFunctions.Postgres.json_get_text("""["zero","one"]""", 1),
                Negative = SqlFunctions.Postgres.json_get("""["zero","one"]""", -1)
            })
            .First();

        row.First.Should().Be("\"zero\"");
        row.LastText.Should().Be("one");
        row.Negative.Should().Be("\"one\"", "a negative index counts from the end of the array");
    }

    [Fact]
    public void T263_R06_LiteralDocument_CapturedPath_ShouldTraverseAndEmptyPathReturnsDocument()
    {
        string?[] path = ["a", "b"];
        string?[] empty = [];

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Path = SqlFunctions.Postgres.json_get_path("""{"a":{"b":[1,2]}}""", path),
                PathText = SqlFunctions.Postgres.json_get_path_text("""{"a":{"b":[1,2]}}""", path),
                Empty = SqlFunctions.Postgres.json_get_path("""{"a":{"b":[1,2]}}""", empty)
            })
            .First();

        JsonNode.DeepEquals(JsonNode.Parse(row.Path!), JsonNode.Parse("[1,2]")).Should().BeTrue();
        row.PathText.Should().Be("[1, 2]", "the element is JSONB, so #>> renders its normalized JSON text");
        JsonNode.DeepEquals(JsonNode.Parse(row.Empty!), JsonNode.Parse("""{"a":{"b":[1,2]}}""")).Should()
            .BeTrue("an empty path returns the input document");
    }

    // - R07-R10: raw captured/parameter document, four right-operand kinds -

    [Fact]
    public void T263_R07_CapturedStringDocument_LiteralKey_ShouldCastDocument()
    {
        var document = FlatObject;

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(document, "a"),
                Text = SqlFunctions.Postgres.json_get_text(document, "a")
            })
            .First();

        row.Json.Should().Be("1");
        row.Text.Should().Be("1");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<string>(0), "a"))
            .First(document);

        explicitValue.Should().Be("1", "the explicit placeholder carries the same raw document");
    }

    [Fact]
    public void T263_R07_BindingPredicate_PlainStringIsText_StructuredIsJsonb()
    {
        // The document cast is what makes a captured raw string work: the driver leaves a plain
        // string as text, while a JSON document/element/node is bound as jsonb.
        BindPredicate("""{"a":1}""").Should().Be("text");
        BindPredicate(JsonNode.Parse(FlatObject)!).Should().Be("jsonb");
        BindPredicate(JsonDocument.Parse(FlatObject)).Should().Be("jsonb");
    }

    [Fact]
    public void T263_R08_CapturedStringDocument_ParameterKey_ShouldTypeBothOperands()
    {
        var document = FlatObject;
        var key = "s";

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(document, key),
                Text = SqlFunctions.Postgres.json_get_text(document, key)
            })
            .First();

        row.Json.Should().Be("\"x\"");
        row.Text.Should().Be("x");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<string>(0), key))
            .First(document);

        explicitValue.Should().Be("\"x\"", "the explicit placeholder document and the captured key are typed independently");
    }

    [Fact]
    public void T263_R09_CapturedStringDocument_IntegerIndex_ShouldResolve()
    {
        var document = """["zero","one"]""";

        var captured = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get_text(document, 1))
            .First();

        captured.Should().Be("one");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<string>(0), 0))
            .First(document);

        explicitValue.Should().Be("\"zero\"");
    }

    [Fact]
    public void T263_R10_CapturedStringDocument_Path_ShouldResolve()
    {
        var document = """{"a":{"b":[1,2]}}""";
        string?[] path = ["a", "b"];
        string?[] empty = [];

        var captured = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                PathText = SqlFunctions.Postgres.json_get_path_text(document, path),
                Empty = SqlFunctions.Postgres.json_get_path(document, empty)
            })
            .First();

        captured.PathText.Should().Be("[1, 2]");
        JsonNode.DeepEquals(JsonNode.Parse(captured.Empty!), JsonNode.Parse(document)).Should().BeTrue();

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get_path_text(SqlFunctions.Parameter<string>(0), path))
            .First(document);

        explicitValue.Should().Be("[1, 2]");
    }

    // - R11-R14: native json column, no document cast -

    [Fact]
    public void T263_R11_NativeJsonColumn_LiteralKey_ShouldKeepJsonFamily()
    {
        PrepareNativeTable();
        try
        {
            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Type = SqlFunctions.Postgres.json_typeof(x.DocJson),
                    Quoted = SqlFunctions.Postgres.json_get(x.DocJson, "s"),
                    Plain = SqlFunctions.Postgres.json_get_text(x.DocJson, "s"),
                    Object = SqlFunctions.Postgres.json_get(x.DocJson, "obj")
                })
                .First();

            // json_typeof only exists for json; calling it on a coerced jsonb operand would fail.
            row.Type.Should().Be("object", "the native json column must not be coerced to jsonb");
            row.Quoted.Should().Be("\"x\"", "-> on a json column returns a json value");
            row.Plain.Should().Be("x", "->> on a json column returns text");
            row.Object.Should().Be("""{"b":1}""",
                "a json result preserves the stored formatting; a jsonb coercion would render {\"b\": 1}");
        }
        finally
        {
            DropNativeTable();
        }
    }

    [Fact]
    public void T263_R12_NativeJsonColumn_ParameterKey_ShouldKeepJsonFamily()
    {
        PrepareNativeTable();
        try
        {
            var key = "s";

            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Quoted = SqlFunctions.Postgres.json_get(x.DocJson, key),
                    Plain = SqlFunctions.Postgres.json_get_text(x.DocJson, key)
                })
                .First();

            row.Quoted.Should().Be("\"x\"");
            row.Plain.Should().Be("x");
        }
        finally
        {
            DropNativeTable();
        }
    }

    [Fact]
    public void T263_R13_NativeJsonColumn_IntegerIndex_ShouldKeepJsonFamily()
    {
        PrepareNativeTable();
        try
        {
            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 2)
                .Select(x => new
                {
                    First = SqlFunctions.Postgres.json_get(x.DocJson, 0),
                    LastText = SqlFunctions.Postgres.json_get_text(x.DocJson, -1)
                })
                .First();

            row.First.Should().Be("10");
            row.LastText.Should().Be("20");
        }
        finally
        {
            DropNativeTable();
        }
    }

    [Fact]
    public void T263_R14_NativeJsonColumn_Path_ShouldKeepJsonFamily()
    {
        PrepareNativeTable();
        try
        {
            string?[] path = ["a", "b"];

            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 3)
                .Select(x => new
                {
                    Path = SqlFunctions.Postgres.json_get_path(x.DocJson, path),
                    PathText = SqlFunctions.Postgres.json_get_path_text(x.DocJson, path)
                })
                .First();

            row.Path.Should().Be("[1,2]", "a json result preserves the stored formatting");
            row.PathText.Should().Be("[1,2]");
        }
        finally
        {
            DropNativeTable();
        }
    }

    // - R15-R18: raw jsonb fixture, four right-operand kinds -

    [Fact]
    public void T263_R15_JsonbFixture_LiteralKey_ShouldResolve()
    {
        const string document = """{"j":{"k":"v"},"n":2}""";

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(document, "n"),
                Text = SqlFunctions.Postgres.json_get_text(document, "n")
            })
            .First();

        row.Json.Should().Be("2");
        row.Text.Should().Be("2");
    }

    [Fact]
    public void T263_R16_JsonbFixture_ParameterKey_ShouldResolve()
    {
        const string document = """{"j":{"k":"v"},"n":2}""";
        var key = "j";

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(document, key),
                Text = SqlFunctions.Postgres.json_get_text(document, key)
            })
            .First();

        JsonNode.DeepEquals(JsonNode.Parse(row.Json!), JsonNode.Parse("""{"k":"v"}""")).Should().BeTrue();
        row.Text.Should().Be("{\"k\": \"v\"}", "a jsonb value renders with normalized spacing");
    }

    [Fact]
    public void T263_R17_JsonbFixture_IntegerIndex_ShouldResolve()
    {
        const string document = """[5,6]""";

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                First = SqlFunctions.Postgres.json_get(document, 0),
                SecondText = SqlFunctions.Postgres.json_get_text(document, 1)
            })
            .First();

        row.First.Should().Be("5");
        row.SecondText.Should().Be("6");
    }

    [Fact]
    public void T263_R18_JsonbFixture_Path_ShouldResolve()
    {
        const string document = """{"j":{"k":"v"},"n":2}""";
        string?[] path = ["j", "k"];

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get_path(document, path),
                Text = SqlFunctions.Postgres.json_get_path_text(document, path)
            })
            .First();

        row.Json.Should().Be("\"v\"");
        row.Text.Should().Be("v");
    }

    // - R19-R22: JSONB/raw parameter (captured + explicit), four right-operand kinds -

    [Fact]
    public void T263_R19_JsonbParameter_LiteralKey_ShouldResolve()
    {
        using var document = JsonDocument.Parse("""{"a":9,"arr":[3,4]}""");

        var captured = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(document, "a"))
            .First();

        captured.Should().Be("9");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonDocument>(0), "a"))
            .First(document);

        explicitValue.Should().Be("9");
    }

    [Fact]
    public void T263_R20_JsonbParameter_ParameterKey_ShouldTypeBothOperands()
    {
        using var document = JsonDocument.Parse("""{"a":9,"arr":[3,4]}""");
        var key = "a";

        var captured = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(document, key))
            .First();

        captured.Should().Be("9");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonDocument>(0), key))
            .First(document);

        explicitValue.Should().Be("9");
    }

    [Fact]
    public void T263_R21_JsonbParameter_IntegerIndex_ShouldResolve()
    {
        using var document = JsonDocument.Parse("""{"a":9,"arr":[3,4]}""");

        var captured = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { Value = SqlFunctions.Postgres.json_get(document, 0) })
            .First();

        captured.Value.Should().BeNull("an object has no element at index 0");

        using var array = JsonDocument.Parse("""[3,4]""");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), 1))
            .First(array);

        explicitValue.Should().Be("4");
    }

    [Fact]
    public void T263_R22_JsonbParameter_Path_ShouldResolve()
    {
        using var document = JsonDocument.Parse("""{"n":{"k":"v"},"n2":2}""");
        string?[] path = ["n", "k"];

        var captured = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get_path_text(document, path))
            .First();

        captured.Should().Be("v");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get_path_text(SqlFunctions.Parameter<JsonDocument>(0), path))
            .First(document);

        explicitValue.Should().Be("v");
    }

    // - R23-R26: native jsonb column, four right-operand kinds -

    [Fact]
    public void T263_R23_NativeJsonbColumn_LiteralKey_ShouldKeepJsonbFamily()
    {
        PrepareNativeTable();
        try
        {
            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Type = SqlFunctions.Postgres.jsonb_typeof(x.DocJsonb),
                    Quoted = SqlFunctions.Postgres.json_get(x.DocJsonb, "s"),
                    Plain = SqlFunctions.Postgres.json_get_text(x.DocJsonb, "s"),
                    Object = SqlFunctions.Postgres.json_get(x.DocJsonb, "obj")
                })
                .First();

            // jsonb_typeof only exists for jsonb; a coercion to json would fail on this call.
            row.Type.Should().Be("object", "the native jsonb column must not be coerced to json");
            row.Quoted.Should().Be("\"x\"");
            row.Plain.Should().Be("x");
            row.Object.Should().Be("{\"b\": 1}", "a jsonb result renders with normalized spacing");
        }
        finally
        {
            DropNativeTable();
        }
    }

    [Fact]
    public void T263_R24_NativeJsonbColumn_ParameterKey_ShouldKeepJsonbFamily()
    {
        PrepareNativeTable();
        try
        {
            var key = "s";

            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Quoted = SqlFunctions.Postgres.json_get(x.DocJsonb, key),
                    Plain = SqlFunctions.Postgres.json_get_text(x.DocJsonb, key)
                })
                .First();

            row.Quoted.Should().Be("\"x\"");
            row.Plain.Should().Be("x");
        }
        finally
        {
            DropNativeTable();
        }
    }

    [Fact]
    public void T263_R25_NativeJsonbColumn_IntegerIndex_ShouldKeepJsonbFamily()
    {
        PrepareNativeTable();
        try
        {
            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 2)
                .Select(x => new
                {
                    First = SqlFunctions.Postgres.json_get(x.DocJsonb, 0),
                    LastText = SqlFunctions.Postgres.json_get_text(x.DocJsonb, -1)
                })
                .First();

            row.First.Should().Be("10");
            row.LastText.Should().Be("20");
        }
        finally
        {
            DropNativeTable();
        }
    }

    [Fact]
    public void T263_R26_NativeJsonbColumn_Path_ShouldKeepJsonbFamily()
    {
        PrepareNativeTable();
        try
        {
            string?[] path = ["a", "b"];

            var row = _sut.DataProvider.From<T263JsonAccessProbe>()
                .Where(x => x.Id == 3)
                .Select(x => new
                {
                    Path = SqlFunctions.Postgres.json_get_path(x.DocJsonb, path),
                    PathText = SqlFunctions.Postgres.json_get_path_text(x.DocJsonb, path)
                })
                .First();

            row.Path.Should().Be("[1, 2]");
            row.PathText.Should().Be("[1, 2]");
        }
        finally
        {
            DropNativeTable();
        }
    }

    // - R27-R32: nulls, absent shapes, failure and quoted literals -

    [Fact]
    public void T263_R27_NullDocuments_ShouldYieldSqlNullNotText()
    {
        string? capturedNull = null;

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Literal = SqlFunctions.Postgres.json_get((object?)null, "a"),
                Captured = SqlFunctions.Postgres.json_get(capturedNull, "a"),
                CapturedText = SqlFunctions.Postgres.json_get_text(capturedNull, "a")
            })
            .First();

        row.Literal.Should().BeNull();
        row.Captured.Should().BeNull();
        row.CapturedText.Should().BeNull();
        row.Captured.Should().NotBe("null", "SQL NULL is not the JSON string \"null\"");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { Value = SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<string>(0), "a") })
            .First((string?)null);

        explicitValue.Value.Should().BeNull();
    }

    [Fact]
    public void T263_R28_NullKeyAndPath_ShouldPropagateNull()
    {
        string? nullKey = null;
        string?[]? nullPath = null;
        string?[] pathWithNull = ["a", null];
        string? capturedNull = null;

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                LiteralKey = SqlFunctions.Postgres.json_get(FlatObject, (string?)null),
                CapturedKey = SqlFunctions.Postgres.json_get(FlatObject, nullKey),
                NullPath = SqlFunctions.Postgres.json_get_path(FlatObject, nullPath!),
                NullElement = SqlFunctions.Postgres.json_get_path("""{"a":{"b":1}}""", pathWithNull),
                BothNull = SqlFunctions.Postgres.json_get(capturedNull, nullKey)
            })
            .First();

        row.LiteralKey.Should().BeNull();
        row.CapturedKey.Should().BeNull();
        row.NullPath.Should().BeNull();
        row.NullElement.Should().BeNull("a NULL path element makes the extraction return SQL NULL");
        row.BothNull.Should().BeNull();
    }

    [Fact]
    public void T263_R29_MissingOutOfRangeAndKeyVersusIndex_ShouldFollowPgSemantics()
    {
        const string document = """{"a":{"b":1},"arr":[10,20]}""";
        string?[] path = ["a", "missing"];

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                MissingKey = SqlFunctions.Postgres.json_get(document, "missing"),
                AbsentPath = SqlFunctions.Postgres.json_get_path(document, path),
                OutOfRange = SqlFunctions.Postgres.json_get("""[10,20]""", 5),
                Negative = SqlFunctions.Postgres.json_get("""[10,20]""", -1),
                KeyZero = SqlFunctions.Postgres.json_get("""[10,20]""", "0"),
                IndexZero = SqlFunctions.Postgres.json_get("""[10,20]""", 0)
            })
            .First();

        row.MissingKey.Should().BeNull();
        row.AbsentPath.Should().BeNull();
        row.OutOfRange.Should().BeNull();
        row.Negative.Should().Be("20");
        row.KeyZero.Should().BeNull("json -> text only extracts object fields, so key \"0\" is absent on an array");
        row.IndexZero.Should().Be("10", "json -> integer extracts the array element at index 0");
    }

    [Fact]
    public void T263_R30_MalformedDocument_ShouldFailWithInvalidTextRepresentation()
    {
        Exception? failure = null;
        try
        {
            _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.Postgres.json_get("{bad", "a"))
                .First();
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        var pg = failure.Should().BeOfType<PostgresException>(
            "a malformed raw document must be rejected by the server, not normalized").Subject;
        pg.SqlState.Should().Be("22P02", "PostgreSQL reports invalid_text_representation");
    }

    [Fact]
    public void T263_R31_Apostrophes_InValueAndKey_ShouldRoundTrip()
    {
        const string document = """{"k'ey":"it's"}""";

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(document, "k'ey"),
                Text = SqlFunctions.Postgres.json_get_text(document, "k'ey")
            })
            .First();

        row.Json.Should().Be("\"it's\"");
        row.Text.Should().Be("it's");

        var sql = GeneratedSql(_sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(document, "k'ey")));

        sql.Should().Contain("E'", "the inlined literal and key must be explicit E-strings")
            .And.Contain("it\\'s")
            .And.Contain("k\\'ey");
    }

    [Fact]
    public void T263_R32_JsonReferenceAndValueParameters_ShouldBindAsDocuments()
    {
        using var document = JsonDocument.Parse("""{"a":7}""");
        var node = JsonNode.Parse("""{"a":8}""");
        var element = document.RootElement;

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Document = SqlFunctions.Postgres.json_get(document, "a"),
                Node = SqlFunctions.Postgres.json_get(node, "a"),
                Element = SqlFunctions.Postgres.json_get(element, "a")
            })
            .First();

        row.Document.Should().Be("7");
        row.Node.Should().Be("8");
        row.Element.Should().Be("7");

        var explicitValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonElement>(0), "a"))
            .First(element);

        explicitValue.Should().Be("7");
    }

    [Fact]
    public void T263_R32_DefaultJsonElement_ShouldFailClosed()
    {
        JsonElement undefined = default;

        Exception? failure = null;
        string? value = null;
        try
        {
            value = _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.Postgres.json_get(undefined, "a"))
                .First();
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        failure.Should().BeOfType<InvalidOperationException>(
            "default(JsonElement) has ValueKind Undefined and must fail closed instead of becoming an empty document");
        value.Should().BeNull();
    }

    // - R34: server JSON expressions stay native -

    [Fact]
    public void T263_R34_ServerJsonExpressions_ShouldNotBeCast()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Json = SqlFunctions.Postgres.json_get(SqlFunctions.Postgres.json_build_object("a", 1), "a"),
                Jsonb = SqlFunctions.Postgres.json_get(SqlFunctions.Postgres.jsonb_build_object("a", 1), "a"),
                JsonType = SqlFunctions.Postgres.json_typeof(SqlFunctions.Postgres.json_build_object("a", 1)),
                JsonbType = SqlFunctions.Postgres.jsonb_typeof(SqlFunctions.Postgres.jsonb_build_object("a", 1))
            })
            .First();

        row.Json.Should().Be("1");
        row.Jsonb.Should().Be("1");
        row.JsonType.Should().Be("object");
        row.JsonbType.Should().Be("object");

        var jsonSql = GeneratedSql(_sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Postgres.json_build_object("a", 1), "a")));

        jsonSql.Should().NotContain("cast(json_build_object",
            "a server-side JSON expression must keep its native type");

        var jsonbSql = GeneratedSql(_sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get(SqlFunctions.Postgres.jsonb_build_object("a", 1), "a")));

        jsonbSql.Should().NotContain("cast(jsonb_build_object",
            "a server-side JSONB expression must keep its native type");
    }

    /// <summary>Reads <c>server_version_num</c> from a live connection to the suite's server.</summary>
    private (string Raw, int Major) ReadServerVersion()
    {
        using var ctx = Provider.CreateContext();
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();

        using var command = dataContext.CreateCommand("select current_setting('server_version_num')");
        var raw = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;

        return (raw, int.Parse(raw, CultureInfo.InvariantCulture) / 10000);
    }

    /// <summary>
    /// Renders the AC1 statement without executing it, so a failing run still reports the exact SQL
    /// that PostgreSQL rejected.
    /// </summary>
    private string GeneratedSql()
        => GeneratedSql(_sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get("{\"a\":1}", "a")));

    private string GeneratedSql<T>(QueryCommand<T> command)
    {
        var prepared = (DbPreparedQueryCommand<T>)_sut.DataProvider
            .GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        return prepared.DbCommand.CommandText;
    }

    /// <summary>Reports the server type the provider binds a client value to.</summary>
    private string BindPredicate(object? value)
    {
        using var probe = _sut.DataProvider.ExecuteRaw(
            "select pg_typeof(@p)::text as type",
            [new ProcedureParameter("p", value)]);

        return probe.Read<string>().Single();
    }

    private void PrepareNativeTable()
    {
        var ctx = _sut.DataProvider;
        Exec(ctx, $"drop table if exists {NativeTable}");
        Exec(ctx, $"create table {NativeTable} (id int primary key, doc_json json, doc_jsonb jsonb)");
        Exec(ctx, $$$"""
            insert into {{{NativeTable}}} (id, doc_json, doc_jsonb) values
              (1, '{"a":1,"s":"x","obj":{"b":1},"arr":[10,20]}', '{"a":1,"s":"x","obj":{"b":1},"arr":[10,20]}'),
              (2, '[10,20]', '[10,20]'),
              (3, '{"a":{"b":[1,2]}}', '{"a":{"b":[1,2]}}')
            """);
    }

    private void DropNativeTable() => Exec(_sut.DataProvider, $"drop table if exists {NativeTable}");

    private static void Exec(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
