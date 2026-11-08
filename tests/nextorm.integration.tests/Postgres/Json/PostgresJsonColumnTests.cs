using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL-only regression coverage for <see cref="JsonColumnAttribute"/> against a physical
/// <c>json</c> column and a physical <c>jsonb</c> column (the shared suite picks one physical type
/// from the dialect and therefore exercises only <c>jsonb</c> here). Backed by a Testcontainers
/// instance unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
public sealed class PostgresJsonColumnTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    public sealed class Nested
    {
        public string? Inner { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    public sealed class Shape
    {
        public string? Name { get; set; }
        public int Count { get; set; }
        public bool Flag { get; set; }
        public Nested? Nested { get; set; }
    }

    [SqlTable("pg_json_col_probe_jsonb")]
    public interface IJsonbProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        [JsonColumn]
        Shape Data { get; set; }

        [Column("data_null")]
        [JsonColumn]
        Shape? DataNull { get; set; }
    }

    public sealed class JsonbProbe : IJsonbProbe
    {
        public long Id { get; set; }
        public Shape Data { get; set; } = new();
        public Shape? DataNull { get; set; }
    }

    [SqlTable("pg_json_col_probe_json")]
    public interface IJsonProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        [JsonColumn]
        Shape Data { get; set; }

        [Column("data_null")]
        [JsonColumn]
        Shape? DataNull { get; set; }
    }

    public sealed class JsonProbe : IJsonProbe
    {
        public long Id { get; set; }
        public Shape Data { get; set; } = new();
        public Shape? DataNull { get; set; }
    }

    // Issue #197: a bare System.Text.Json.Nodes.JsonNode property (no [JsonColumn]) maps a native
    // json/jsonb column directly. The declared JsonNode/JsonNode? determines the CLR shape; the
    // physical column type is the axis under test (jsonb vs json).
    [SqlTable("pg_json_node_probe_jsonb")]
    public interface IJsonNodeJsonbProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        JsonNode Data { get; set; }

        [Column("data_null")]
        JsonNode? DataNull { get; set; }
    }

    public sealed class JsonNodeJsonbProbe : IJsonNodeJsonbProbe
    {
        public long Id { get; set; }
        public JsonNode Data { get; set; } = null!;
        public JsonNode? DataNull { get; set; }
    }

    [SqlTable("pg_json_node_probe_json")]
    public interface IJsonNodeJsonProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        JsonNode Data { get; set; }

        [Column("data_null")]
        JsonNode? DataNull { get; set; }
    }

    public sealed class JsonNodeJsonProbe : IJsonNodeJsonProbe
    {
        public long Id { get; set; }
        public JsonNode Data { get; set; } = null!;
        public JsonNode? DataNull { get; set; }
    }

    // Control: JsonDocument/JsonElement keep their native reader accessors next to the JsonNode one.
    [SqlTable("pg_json_read_control")]
    public interface IJsonReadControlProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("doc")]
        JsonDocument Doc { get; set; }

        [Column("el")]
        JsonElement El { get; set; }

        [Column("node")]
        JsonNode Node { get; set; }
    }

    public sealed class JsonReadControlProbe : IJsonReadControlProbe
    {
        public long Id { get; set; }
        public JsonDocument Doc { get; set; } = null!;
        public JsonElement El { get; set; }
        public JsonNode Node { get; set; } = null!;
    }

    [Fact]
    public void JsonColumn_PhysicalJsonb_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_col_probe_jsonb";

        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data jsonb, data_null jsonb)");

        try
        {
            // Count/Flag stay at their CLR defaults (0/false); Name carries Unicode; Nested is a
            // separate object with a nested Unicode string and a list.
            var payload = new Shape
            {
                Name = "\u00dcn\u00efc\u00f8d\u00e9 \u2014 \u65e5\u672c\u8a9e \ud83c\udf8c",
                Count = 0,
                Flag = false,
                Nested = new Nested { Inner = "nested-\u00fcn\u00efc\u00f8de", Tags = ["\u03b1", "\u03b2"] }
            };
            var second = new Shape { Name = "second", Count = 3, Flag = true, Nested = null };

            ctx.CreateInsertBuilder<IJsonbProbe>()
                .Values(new JsonbProbe { Id = 1, Data = payload, DataNull = null })
                .Insert();
            ctx.CreateInsertBuilder<IJsonbProbe>()
                .Values(new JsonbProbe { Id = 2, Data = second, DataNull = second })
                .Insert();

            // insert -> read
            var first = ctx.From<JsonbProbe>().Where(x => x.Id == 1).ToList().Single();
            first.Data.Should().BeEquivalentTo(payload);
            first.Data.Name.Should().Contain("\u65e5\u672c\u8a9e");
            first.DataNull.Should().BeNull();

            var roundTripped = ctx.From<JsonbProbe>().Where(x => x.Id == 2).ToList().Single();
            roundTripped.Data.Should().BeEquivalentTo(second);
            roundTripped.DataNull.Should().BeEquivalentTo(second);

            // update -> read (a semantically-equal but distinct object)
            var updated = new Shape
            {
                Name = "updated-\u00fc",
                Count = 9,
                Flag = true,
                Nested = new Nested { Inner = "u2", Tags = ["\u65e5"] }
            };
            ctx.CreateUpdateBuilder<IJsonbProbe>()
                .Set(x => x.Data, updated)
                .Where(x => x.Id == 1)
                .Update();

            var reread = ctx.From<JsonbProbe>().Where(x => x.Id == 1).ToList().Single();
            reread.Data.Should().BeEquivalentTo(updated);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    public void JsonColumn_PhysicalJson_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_col_probe_json";

        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data json, data_null json)");

        try
        {
            var payload = new Shape
            {
                Name = "\u00dcn\u00efc\u00f8d\u00e9 \u2014 \u65e5\u672c\u8a9e \ud83c\udf8c",
                Count = 0,
                Flag = false,
                Nested = new Nested { Inner = "nested-\u00fcn\u00efc\u00f8de", Tags = ["\u03b1", "\u03b2"] }
            };
            var second = new Shape { Name = "second", Count = 3, Flag = true, Nested = null };

            ctx.CreateInsertBuilder<IJsonProbe>()
                .Values(new JsonProbe { Id = 1, Data = payload, DataNull = null })
                .Insert();
            ctx.CreateInsertBuilder<IJsonProbe>()
                .Values(new JsonProbe { Id = 2, Data = second, DataNull = second })
                .Insert();

            var first = ctx.From<JsonProbe>().Where(x => x.Id == 1).ToList().Single();
            first.Data.Should().BeEquivalentTo(payload);
            first.Data.Name.Should().Contain("\u65e5\u672c\u8a9e");
            first.DataNull.Should().BeNull();

            var roundTripped = ctx.From<JsonProbe>().Where(x => x.Id == 2).ToList().Single();
            roundTripped.Data.Should().BeEquivalentTo(second);
            roundTripped.DataNull.Should().BeEquivalentTo(second);

            var updated = new Shape
            {
                Name = "updated-\u00fc",
                Count = 9,
                Flag = true,
                Nested = new Nested { Inner = "u2", Tags = ["\u65e5"] }
            };
            ctx.CreateUpdateBuilder<IJsonProbe>()
                .Set(x => x.Data, updated)
                .Where(x => x.Id == 1)
                .Update();

            var reread = ctx.From<JsonProbe>().Where(x => x.Id == 1).ToList().Single();
            reread.Data.Should().BeEquivalentTo(updated);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    public void BareJsonNode_PhysicalJsonb_CompositeProjection_ShouldReadEveryRoot()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_node_probe_jsonb";

        PrepareBareJsonNodeTable(ctx, table, "jsonb");
        try
        {
            var rows = ctx.From<JsonNodeJsonbProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Data, x.DataNull })
                .ToList()
                .Select(x => (x.Id, (JsonNode?)x.Data, (JsonNode?)x.DataNull))
                .ToList();

            AssertBareJsonNodeRoots(rows);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    public void BareJsonNode_PhysicalJson_CompositeProjection_ShouldReadEveryRoot()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_node_probe_json";

        PrepareBareJsonNodeTable(ctx, table, "json");
        try
        {
            var rows = ctx.From<JsonNodeJsonProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Data, x.DataNull })
                .ToList()
                .Select(x => (x.Id, (JsonNode?)x.Data, (JsonNode?)x.DataNull))
                .ToList();

            AssertBareJsonNodeRoots(rows);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    public void BareJsonNode_ScalarProjection_ShouldMaterialize()
    {
        var ctx = _sut.DataProvider;
        const string jsonb = "pg_json_node_probe_jsonb";
        const string json = "pg_json_node_probe_json";

        PrepareBareJsonNodeTable(ctx, jsonb, "jsonb");
        PrepareBareJsonNodeTable(ctx, json, "json");

        // data_null is SQL NULL for ids 1..7 in the shared fixture; mirror data into it so the scalar
        // JsonNode? axis covers every root as well (JSON literal null stays literal, SQL NULL stays SQL NULL).
        Execute(ctx, $"update {jsonb} set data_null = data");
        Execute(ctx, $"update {json} set data_null = data");

        try
        {
            // Physical jsonb x declared JsonNode / JsonNode?.
            AssertScalarJsonNodeRoots(
                ctx.From<JsonNodeJsonbProbe>().OrderBy(x => x.Id).Select(x => x.Data).ToList()
                    .Select(n => (JsonNode?)n).ToList());
            AssertScalarJsonNodeRoots(
                ctx.From<JsonNodeJsonbProbe>().OrderBy(x => x.Id).Select(x => x.DataNull).ToList());

            // Physical json x declared JsonNode / JsonNode?.
            AssertScalarJsonNodeRoots(
                ctx.From<JsonNodeJsonProbe>().OrderBy(x => x.Id).Select(x => x.Data).ToList()
                    .Select(n => (JsonNode?)n).ToList());
            AssertScalarJsonNodeRoots(
                ctx.From<JsonNodeJsonProbe>().OrderBy(x => x.Id).Select(x => x.DataNull).ToList());
        }
        finally
        {
            Execute(ctx, $"drop table if exists {jsonb}");
            Execute(ctx, $"drop table if exists {json}");
        }
    }

    [Fact]
    public void BareJsonNode_Parameter_ShouldBindAsJsonbAndRoundTrip()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_node_probe_jsonb";

        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data jsonb, data_null jsonb)");
        try
        {
            var payload = new JsonObject
            {
                ["name"] = "carol",
                ["age"] = 41,
                ["nested"] = new JsonObject { ["x"] = 9 },
            };

            // The parameter is bound with NpgsqlDbType.Jsonb, so pg_typeof reports jsonb without a cast.
            using (var probe = ctx.ExecuteRaw(
                "select pg_typeof(@data)::text as type",
                [new ProcedureParameter("data", payload)]))
            {
                probe.Read<string>().Should().Equal("jsonb");
            }

            using (ctx.ExecuteRaw(
                $"insert into {table} (id, data) values (@id, @data)",
                [new ProcedureParameter("id", 100L), new ProcedureParameter("data", payload)]))
            {
            }

            var read = ctx.From<JsonNodeJsonbProbe>()
                .Where(x => x.Id == 100)
                .Select(x => new { x.Data })
                .ToList()
                .Single()
                .Data;

            JsonNode.DeepEquals(read, payload).Should().BeTrue();
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    public void JsonDocument_And_JsonElement_ShouldStillReadNative()
    {
        var ctx = _sut.DataProvider;
        const string table = "pg_json_read_control";

        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, doc jsonb, el jsonb, node jsonb)");
        try
        {
            Execute(ctx, "insert into " + table + " (id, doc, el, node) values (1, '{\"a\":1}', '{\"b\":2}', '{\"c\":3}')");

            var row = ctx.From<JsonReadControlProbe>().Where(x => x.Id == 1).ToList().Single();

            row.Doc.RootElement.GetProperty("a").GetInt32().Should().Be(1);
            row.El.GetProperty("b").GetInt32().Should().Be(2);
            JsonNode.DeepEquals(row.Node, JsonNode.Parse("""{"c":3}""")).Should().BeTrue();

            row.Doc.Dispose();
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    // Every root kind plus both null flavours, in the order the ids are inserted.
    private static readonly (long Id, string Json)[] RootPayloads =
    [
        (1, """{"name":"bob","age":25,"nested":{"x":2}}"""),
        (2, "[1,2,3]"),
        (3, "42"),
        (4, "\"hello\""),
        (5, "true"),
    ];

    private static void PrepareBareJsonNodeTable(IDataContext ctx, string table, string jsonType)
    {
        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data {jsonType}, data_null {jsonType})");

        // Literal inserts: each RootPayloads.Json text is wrapped in a single-quoted SQL literal whose
        // unknown type Postgres coerces to the physical json/jsonb column. Npgsql cannot write a scalar
        // JsonValue as a JsonNode parameter (only JsonObject/JsonArray bind), so the scalar roots must
        // not go through a parameter; the JsonObject parameter binding is covered separately below.
        var values = string.Join(", ", RootPayloads.Select(p => $"({p.Id}, '{p.Json}')"));
        Execute(ctx, $"insert into {table} (id, data) values {values}, (6, 'null'), (7, null)");

        // A populated nullable property alongside a populated non-nullable one.
        Execute(ctx, "insert into " + table + " (id, data, data_null) values (8, '{\"n\":\"x\"}', '{\"n\":\"y\"}')");
    }

    private static void AssertBareJsonNodeRoots(IReadOnlyList<(long Id, JsonNode? Data, JsonNode? DataNull)> rows)
    {
        rows.Should().HaveCount(8);

        foreach (var (id, json) in RootPayloads)
        {
            var row = rows.Single(r => r.Id == id);
            JsonNode.DeepEquals(row.Data, JsonNode.Parse(json)).Should()
                .BeTrue($"row {id} ({json}) should parse to a semantically equal node");
            row.DataNull.Should().BeNull();
        }

        // JSON literal null -> CLR null (not a JsonValue wrapping null).
        rows.Single(r => r.Id == 6).Data.Should().BeNull();
        // SQL NULL -> CLR null.
        rows.Single(r => r.Id == 7).Data.Should().BeNull();

        var both = rows.Single(r => r.Id == 8);
        JsonNode.DeepEquals(both.Data, JsonNode.Parse("""{"n":"x"}""")).Should().BeTrue();
        JsonNode.DeepEquals(both.DataNull, JsonNode.Parse("""{"n":"y"}""")).Should().BeTrue();
    }

    // Scalar counterpart of AssertBareJsonNodeRoots: rows are read in Id order (1..8) through a
    // single-column Select, so roots are compared positionally and semantically.
    private static void AssertScalarJsonNodeRoots(IReadOnlyList<JsonNode?> nodes)
    {
        nodes.Should().HaveCount(8);

        for (var i = 0; i < RootPayloads.Length; i++)
        {
            var (id, json) = RootPayloads[i];
            JsonNode.DeepEquals(nodes[i], JsonNode.Parse(json)).Should()
                .BeTrue($"scalar row {id} ({json}) should parse to a semantically equal node");
        }

        // JSON literal null -> CLR null (not a JsonValue wrapping null).
        nodes[5].Should().BeNull();
        // SQL NULL -> CLR null.
        nodes[6].Should().BeNull();

        JsonNode.DeepEquals(nodes[7], JsonNode.Parse("""{"n":"x"}""")).Should().BeTrue();
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
