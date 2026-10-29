using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>The nested-object model used by the T325 control table's SQL-NULL column.</summary>
public sealed class T325ControlDoc
{
    public string? Name { get; set; }

    public List<int> Values { get; set; } = [];

    public T325ControlDoc? Inner { get; set; }
}

/// <summary>
/// T325 control table: one <c>longtext</c> text column and one <c>json</c>-declared column, both mapped
/// through the default <see cref="JsonColumnStorage.Auto"/> storage, plus a nullable text column for the
/// SQL-NULL case. The physical types are declared by the test, so the fixture cannot masquerade as a
/// text-only table.
/// </summary>
[SqlTable("t325_text_storage_control")]
public interface IT325ControlRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("doc_text")]
    [JsonColumn]
    JsonElement DocText { get; set; }

    [Column("doc_json")]
    [JsonColumn]
    JsonElement DocJson { get; set; }

    [Column("optional_doc")]
    [JsonColumn]
    T325ControlDoc? OptionalDoc { get; set; }
}

/// <summary>Concrete implementation of the control row.</summary>
public sealed class T325ControlRow : IT325ControlRow
{
    public int Id { get; set; }

    public JsonElement DocText { get; set; }

    public JsonElement DocJson { get; set; }

    public T325ControlDoc? OptionalDoc { get; set; }
}

/// <summary>
/// T325 (#325) — the storage control. Both arms run the <b>same</b> file against the baseline commit and
/// against the r=2 tree: an <c>[JsonColumn]</c> mapping in its default <see cref="JsonColumnStorage.Auto"/>
/// storage must keep using the string transport on a real <c>json</c>-declared column exactly as it does on
/// a <c>longtext</c> column, with the schema (physical type), the emitted SQL, the driver value family and
/// the decoded values all observable. The MySQL arm and the MariaDB arm are separate facts: MariaDB reports
/// the column as <c>longtext</c> and validates with <c>json_valid</c>, MySQL reports <c>json</c>.
/// </summary>
public sealed class T325TextStorageControlTests
{
    private const string Table = "t325_text_storage_control";

    private static readonly (string Raw, string Label)[] Corpus =
    [
        ("""{"name":"Привет","values":[1,2,3],"inner":{"name":"nested","values":[9]}}""", "nested-object-unicode"),
        ("[1,2,3]", "array-root"),
        ("\"Привет\"", "string-root"),
        ("42", "number-root"),
        ("true", "bool-root"),
        ("null", "json-null"),
    ];

    [Fact]
    public void MySqlArm_Auto_ShouldKeepTextTransportOnTextAndJsonDeclaredColumns()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        using var context = new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());
        RunArm("MySQL", context, jsonDeclaredColumnIsNative: true);
    }

    [Fact]
    public void MariaDbArm_Auto_ShouldKeepTextTransportOnTextAndJsonAliasColumns()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        using var context = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        RunArm("MariaDB", context, jsonDeclaredColumnIsNative: false);
    }

    private static void RunArm(string engine, DataContext context, bool jsonDeclaredColumnIsNative)
    {
        var tap = new SqlTap();
        context.AddInterceptor(tap);

        Execute(context, $"drop table if exists {Table}");
        Execute(
            context,
            $"create table {Table} (id int not null primary key, doc_text longtext not null, "
            + "doc_json json not null, optional_doc longtext null)");

        try
        {
            var ddl = ReadCreateTable(context);
            Report($"engine={engine} ddl={ddl}");

            var types = ReadColumnTypes(context);
            Report($"engine={engine} DATA_TYPE " + string.Join(", ", types.Select(pair => $"{pair.Key}={pair.Value}")));

            types["doc_text"].Should().Be("longtext");
            types["optional_doc"].Should().Be("longtext");
            if (jsonDeclaredColumnIsNative)
            {
                // MySQL: a genuine server type, not a text column with a validation rule.
                types["doc_json"].Should().Be("json");
                ddl.Should().NotContain("json_valid");
            }
            else
            {
                // MariaDB: the JSON alias is LONGTEXT + a json_valid() CHECK, and reports as longtext.
                types["doc_json"].Should().Be("longtext");
                ddl.Should().Contain("json_valid");
            }

            for (var index = 0; index < Corpus.Length; index++)
            {
                var (raw, label) = Corpus[index];
                var row = new T325ControlRow
                {
                    Id = index + 1,
                    DocText = JsonDocument.Parse(raw).RootElement.Clone(),
                    DocJson = JsonDocument.Parse(raw).RootElement.Clone(),
                    OptionalDoc = new T325ControlDoc { Name = label, Values = [index] },
                };

                context.CreateInsertBuilder<IT325ControlRow>().Values(row).Insert();
            }

            // SQL NULL row: a null model never reaches the converter, so the column stays NULL.
            context.CreateInsertBuilder<IT325ControlRow>()
                .Values(new T325ControlRow
                {
                    Id = Corpus.Length + 1,
                    DocText = JsonDocument.Parse("{}").RootElement.Clone(),
                    DocJson = JsonDocument.Parse("{}").RootElement.Clone(),
                    OptionalDoc = null,
                })
                .Insert();

            var insert = tap.Executions.First(item => item.Sql.Contains("insert into", StringComparison.OrdinalIgnoreCase));
            Report($"engine={engine} insert sql={insert.Sql}");
            foreach (var parameter in insert.Parameters)
                Report($"engine={engine}   bound {parameter.Name} type={parameter.Type?.FullName ?? "<null>"} dbType={parameter.DbType} payload={parameter.Payload}");

            // Transport claim: the JSON document leaves the converter as text. The MySQL family lowers a
            // text mutation value into a `_utf8mb4 X'…'` literal (MutationParameterLowering) instead of
            // binding it, so the document must appear there — and no DOM value may reach the driver.
            insert.Parameters.Should().NotContain(
                parameter => parameter.Type == typeof(JsonElement)
                    || parameter.Type == typeof(JsonDocument)
                    || parameter.Type == typeof(JsonNode),
                "a JSON column is transported as text on this path");

            var literals = HexLiterals(insert.Sql);
            literals.Should().HaveCount(3, "doc_text, doc_json and optional_doc are text values");
            JsonNode.DeepEquals(JsonNode.Parse(literals[0]), JsonNode.Parse(Corpus[0].Raw))
                .Should().BeTrue($"{engine}: the inserted document must travel as its JSON text");
            JsonNode.DeepEquals(JsonNode.Parse(literals[1]), JsonNode.Parse(Corpus[0].Raw))
                .Should().BeTrue($"{engine}: the json-declared column carries the same text");

            // The driver hands a json column back as text, whatever the server type is.
            using (var command = context.CreateCommand($"select doc_json from {Table} where id = 1"))
            using (var reader = command.ExecuteReader())
            {
                reader.Read().Should().BeTrue();
                reader.GetFieldType(0).Should().Be(typeof(string));
                Report($"engine={engine} reader field type={reader.GetFieldType(0).FullName}");
            }

            var rows = context.From<T325ControlRow>().ToList();
            rows.Should().HaveCount(Corpus.Length + 1);

            for (var index = 0; index < Corpus.Length; index++)
            {
                var (raw, label) = Corpus[index];
                var id = index + 1;
                var row = rows.Single(item => item.Id == id);
                var expected = JsonNode.Parse(raw);

                JsonNode.DeepEquals(JsonNode.Parse(row.DocJson.GetRawText()), expected)
                    .Should().BeTrue($"{engine}/{label}: the json-declared column must round trip the document");
                JsonNode.DeepEquals(JsonNode.Parse(row.DocText.GetRawText()), expected)
                    .Should().BeTrue($"{engine}/{label}: the longtext column must round trip the document");

                // The longtext control is byte-for-byte the converter's serialized text.
                var longText = Scalar(context, $"select doc_text from {Table} where id = {id}");
                longText.Should().Be(JsonSerializer.Serialize(JsonDocument.Parse(raw).RootElement));
                JsonNode.DeepEquals(JsonNode.Parse(longText), expected)
                    .Should().BeTrue($"{engine}/{label}: the longtext column stores the serialized text");

                // The json-declared column is compared semantically: the server may normalise the text.
                var jsonText = Scalar(context, $"select doc_json from {Table} where id = {id}");
                JsonNode.DeepEquals(JsonNode.Parse(jsonText), expected)
                    .Should().BeTrue($"{engine}/{label}: stored json text must be semantically equal");

                Report($"engine={engine} id={id} label={label} input={raw} longtext={longText} json={jsonText}");
            }

            var nullRow = rows.Single(item => item.Id == Corpus.Length + 1);
            nullRow.OptionalDoc.Should().BeNull();
            Scalar(context, $"select optional_doc is null from {Table} where id = {Corpus.Length + 1}")
                .Should().Be("1");

            RowCount(context).Should().Be(Corpus.Length + 1);
            Report($"engine={engine} corpus rows={Corpus.Length + 1} result=OK");
        }
        finally
        {
            Execute(context, $"drop table if exists {Table}");
        }
    }

    /// <summary>Decodes every <c>X'…'</c> hex literal of a rendered statement, in emission order.</summary>
    private static List<string> HexLiterals(string sql)
    {
        var result = new List<string>();
        foreach (Match match in Regex.Matches(sql, "X'([0-9A-Fa-f]*)'", RegexOptions.CultureInvariant))
        {
            var hex = match.Groups[1].Value;
            var bytes = new byte[hex.Length / 2];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(hex.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            result.Add(Encoding.UTF8.GetString(bytes));
        }

        return result;
    }

    private static string ReadCreateTable(DataContext context)
    {
        using var command = context.CreateCommand($"show create table {Table}");
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        return reader.GetString(1);
    }

    private static Dictionary<string, string> ReadColumnTypes(DataContext context)
    {
        using var command = context.CreateCommand(
            "select column_name, data_type from information_schema.columns "
            + $"where table_schema = database() and table_name = '{Table}' order by ordinal_position");
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    private static string Scalar(DataContext context, string sql)
    {
        using var command = context.CreateCommand(sql);
        var value = command.ExecuteScalar();
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int RowCount(DataContext context)
    {
        using var command = context.CreateCommand($"select count(*) from {Table}");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(DataContext context, string sql)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static void Report(string message) => Console.WriteLine($"[T325-control] {message}");

    private sealed class SqlTap : IQueryInterceptor
    {
        public List<Captured> Executions { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            var parameters = new List<CapturedParameter>();
            foreach (DbParameter parameter in command.Parameters)
            {
                var payload = parameter.Value switch
                {
                    JsonElement element => element.GetRawText(),
                    null => null,
                    _ => Convert.ToString(parameter.Value, CultureInfo.InvariantCulture),
                };
                parameters.Add(new CapturedParameter(parameter.ParameterName, parameter.Value?.GetType(), parameter.DbType, payload));
            }

            Executions.Add(new Captured(command.CommandText, parameters));
        }
    }

    private sealed record Captured(string Sql, List<CapturedParameter> Parameters);

    private sealed record CapturedParameter(string Name, Type? Type, System.Data.DbType DbType, string? Payload);
}
