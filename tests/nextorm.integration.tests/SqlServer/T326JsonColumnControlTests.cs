using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T326 (#326) — the pre-change control for a <c>[JsonColumn]</c> (<see cref="JsonColumnStorage.Auto"/>)
/// mapped to an <c>nvarchar(max)</c> column declared by the test. The test is deliberately version
/// agnostic: it builds the context the way the suite always has (no server version), so the emitted SQL,
/// the parameter metadata and the stored bytes are the compatibility baseline that the version-gated
/// native path of T326 must not move. It stays in the suite as a regression control.
/// </summary>
[SqlTable(T326JsonColumnControlTests.Table)]
public interface IT326JsonControlRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn]
    JsonElement Data { get; set; }

    [Column("data_null")]
    [JsonColumn]
    JsonElement? DataNull { get; set; }
}

/// <summary>Concrete row for <see cref="IT326JsonControlRow"/>.</summary>
public sealed class T326JsonControlRow : IT326JsonControlRow
{
    public long Id { get; set; }

    public JsonElement Data { get; set; }

    public JsonElement? DataNull { get; set; }
}

/// <summary>
/// The control corpus: nested object, array root, unicode object, string/number/bool scalars, the JSON
/// <c>null</c> text and a SQL NULL (the last one through the nullable column, so JSON null and SQL NULL
/// stay distinguishable at the column level).
/// </summary>
public sealed class T326JsonColumnControlTests : ProviderTestSuite
{
    internal const string Table = "t326_json_column_control";

    private const string DumpVariable = "NEXTORM_T326_CONTROL_DUMP";

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private static readonly (long Id, string Label, string Json)[] Corpus =
    [
        (1, "nested_object", "{\"name\":\"alpha\",\"inner\":{\"k\":1,\"flag\":true}}"),
        (2, "array_root", "[1,2,3]"),
        (3, "unicode_object", "{\"name\":\"Привет, мир — ünïcode\",\"emoji\":\"🙂\"}"),
        (4, "string_scalar", "\"text\""),
        (5, "number_scalar", "42.5"),
        (6, "bool_scalar", "true"),
        (7, "json_null_text", "null"),
    ];

    [Fact]
    public void JsonColumnAuto_OnNvarcharMax_ShouldRoundTripCorpusAndKeepCommandShape()
    {
        var ctx = _sut.DataProvider;
        var dataContext = (DataContext)ctx;
        var recorder = new CommandRecorder();
        dataContext.AddInterceptor(recorder);

        Execute(ctx, $"drop table if exists {Table}");
        Execute(ctx, $"create table {Table} (id bigint not null primary key, data nvarchar(max) null, data_null nvarchar(max) null)");

        try
        {
            foreach (var (id, _, json) in Corpus)
            {
                ctx.CreateInsertBuilder<IT326JsonControlRow>()
                    .Values(new T326JsonControlRow { Id = id, Data = Parse(json), DataNull = null })
                    .Insert();
            }

            // The SQL NULL case: no JSON text at all in the nullable column.
            ctx.CreateInsertBuilder<IT326JsonControlRow>()
                .Values(new T326JsonControlRow { Id = 8, Data = Parse("{}"), DataNull = null })
                .Insert();

            // Stored bytes: the document is written verbatim (a second serialization would store a quoted
            // JSON string, and a DOM binding would not reach the column as text at all).
            var stored = ReadStored(ctx);
            stored.Should().HaveCount(8);
            foreach (var (id, _, json) in Corpus)
            {
                var row = stored.Single(r => r.Id == id);
                row.Data.Should().Be(
                    Canonical(json),
                    "the document is stored as its serialized form, never as a JSON string wrapping it");
                row.DataNull.Should().BeNull("only the SQL NULL case uses the nullable column");
            }

            stored.Single(r => r.Id == 8).DataNull.Should().BeNull("case 8 stores SQL NULL, not JSON null");

            // ORM round trip: every JSON root materializes back to the same document.
            var materialized = ctx.From<T326JsonControlRow>().OrderBy(x => x.Id).ToList();
            materialized.Should().HaveCount(8);
            foreach (var (id, _, json) in Corpus)
            {
                var row = materialized.Single(r => r.Id == id);
                JsonElement.DeepEquals(row.Data, Parse(json)).Should().BeTrue(
                    "case {0} materializes back to the same document", row.Id);
                row.DataNull.Should().BeNull();
            }

            var sqlNull = materialized.Single(r => r.Id == 8);
            sqlNull.DataNull.Should().BeNull("SQL NULL materializes as a null value, unlike the JSON null text");
            JsonElement.DeepEquals(sqlNull.Data, Parse("{}")).Should().BeTrue();

            // The commands the context actually executed: emitted SQL plus the bound parameter metadata.
            var dump = Describe(recorder.Records);
            var inserts = recorder.Records.Where(r => r.Sql.StartsWith("insert", StringComparison.OrdinalIgnoreCase)).ToList();
            inserts.Should().HaveCount(8);

            foreach (var insert in inserts)
            {
                insert.Parameters.Should().NotContain(
                    p => p.SqlDbType == "Json",
                    "a string-typed JSON column must not retag the parameter as the native json type");
                insert.Parameters.Should().OnlyContain(
                    p => p.ClrType != typeof(JsonElement).FullName
                        && p.ClrType != typeof(System.Text.Json.Nodes.JsonObject).FullName
                        && p.ClrType != typeof(JsonDocument).FullName,
                    "the JSON column is transported as text, never as a DOM parameter");

                var jsonParameters = insert.Parameters.Where(p => p.SqlDbType == "NVarChar").ToList();
                jsonParameters.Should().NotBeEmpty();

                var present = jsonParameters.Where(p => p.Value != "NULL").ToList();
                present.Should().NotBeEmpty();
                present.Should().OnlyContain(
                    p => p.DbType == "String" && p.ClrType == typeof(string).FullName,
                    "parameters stay inferred nvarchar strings");

                jsonParameters.Where(p => p.Value == "NULL").Should().OnlyContain(
                    p => p.DbType == "String" && p.ClrType == "DBNull",
                    "a SQL NULL is bound as DBNull on the same nvarchar slot");

                // The nullable column is bound as SQL NULL, never as the JSON null text.
                insert.Parameters.Should().Contain(
                    p => p.SqlDbType == "NVarChar" && p.Value == "NULL",
                    "the data_null value is a SQL NULL parameter");
            }

            TestContext.Current.TestOutputHelper?.WriteLine("T326_CONTROL " + dump);

            var dumpPath = Environment.GetEnvironmentVariable(DumpVariable);
            if (!string.IsNullOrEmpty(dumpPath))
                File.AppendAllText(dumpPath, dump + Environment.NewLine);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {Table}");
        }
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    // The exact text the text transport must store: the document serialized once with the default
    // options. Anything else (a quoted JSON string, an empty object, DBNull) is a different document.
    private static string Canonical(string json) => JsonSerializer.Serialize(Parse(json));

    private static List<StoredRow> ReadStored(IDataContext ctx)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand($"select id, data, data_null from {Table} order by id");
        using var reader = command.ExecuteReader();

        var rows = new List<StoredRow>();
        while (reader.Read())
        {
            rows.Add(new StoredRow(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    // One canonical line per executed command: the SQL it ran and every bound parameter with its CLR
    // type, value (or NULL), DbType, provider SqlDbType and size. The line is the before/after identity
    // proof, so it must stay free of timings and other volatile data.
    private static string Describe(IEnumerable<CommandRecord> records)
    {
        var builder = new StringBuilder();
        foreach (var record in records)
        {
            builder.Append("SQL ").Append(record.Sql.ReplaceLineEndings(" "));
            foreach (var parameter in record.Parameters)
            {
                builder.Append(" || PARAM name=").Append(parameter.Name)
                    .Append(" clr=").Append(parameter.ClrType)
                    .Append(" value=").Append(parameter.Value)
                    .Append(" dbtype=").Append(parameter.DbType)
                    .Append(" sqltype=").Append(parameter.SqlDbType)
                    .Append(" size=").Append(parameter.Size.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(" || ");
        }

        return builder.ToString();
    }

    private sealed record StoredRow(long Id, string? Data, string? DataNull);

    private sealed record ParameterRecord(string Name, string ClrType, string Value, string DbType, string SqlDbType, int Size);

    private sealed record CommandRecord(string Sql, IReadOnlyList<ParameterRecord> Parameters);

    /// <summary>
    /// Records the commands the context executes with their bound parameter metadata. Uses the shipped
    /// <see cref="IQueryInterceptor"/> seam, so the captured values are the ones the provider receives.
    /// </summary>
    private sealed class CommandRecorder : IQueryInterceptor
    {
        public List<CommandRecord> Records { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            var parameters = command.Parameters
                .Cast<DbParameter>()
                .Select(static p => new ParameterRecord(
                    p.ParameterName,
                    p.Value is DBNull or null ? "DBNull" : p.Value.GetType().FullName ?? "null",
                    p.Value is DBNull || p.Value is null ? "NULL" : Convert.ToString(p.Value, CultureInfo.InvariantCulture) ?? "null",
                    p.DbType.ToString(),
                    (p as SqlParameter)?.SqlDbType.ToString() ?? "n/a",
                    p.Size))
                .ToList();

            Records.Add(new CommandRecord(command.CommandText, parameters));
        }
    }
}
