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
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>The reference model used for the nullable JSON column.</summary>
public sealed class MySqlJsonStorageDoc
{
    public string? Name { get; set; }

    public List<int> Values { get; set; } = [];

    public MySqlJsonStorageDoc? Inner { get; set; }
}

/// <summary>
/// T325 (#325) — a real MySQL <c>json</c> column and a <c>longtext</c> control, mapped through the
/// default <see cref="JsonColumnStorage.Auto"/> storage and through an explicit
/// <see cref="JsonColumnStorage.Text"/> storage. Nothing here asks for
/// <see cref="JsonColumnStorage.Native"/>: the provider has no bindable native representation.
/// </summary>
[SqlTable("t325_mysql_json_storage")]
public interface IMySqlJsonStorageRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("auto_json")]
    [JsonColumn]
    JsonElement AutoJson { get; set; }

    [Column("text_json")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    JsonElement TextJson { get; set; }

    [Column("auto_text")]
    [JsonColumn]
    JsonElement AutoText { get; set; }

    [Column("text_text")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    JsonElement TextText { get; set; }

    [Column("nullable_json")]
    [JsonColumn]
    MySqlJsonStorageDoc? NullableJson { get; set; }
}

/// <summary>Concrete row for <see cref="IMySqlJsonStorageRow"/>.</summary>
public sealed class MySqlJsonStorageRow : IMySqlJsonStorageRow
{
    public int Id { get; set; }

    public JsonElement AutoJson { get; set; }

    public JsonElement TextJson { get; set; }

    public JsonElement AutoText { get; set; }

    public JsonElement TextText { get; set; }

    public MySqlJsonStorageDoc? NullableJson { get; set; }
}

/// <summary>An explicit <see cref="JsonColumnStorage.Native"/> mapping: must be refused.</summary>
[SqlTable("t325_mysql_json_storage")]
public interface IMySqlJsonNativeRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("auto_json")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    MySqlJsonStorageDoc Doc { get; set; }
}

/// <summary>Concrete row for <see cref="IMySqlJsonNativeRow"/>.</summary>
public sealed class MySqlJsonNativeRow : IMySqlJsonNativeRow
{
    public int Id { get; set; }

    public MySqlJsonStorageDoc Doc { get; set; } = new();
}

/// <summary>
/// T325 (#325) — MySQL live storage facts. The server's <c>json</c> type is genuine
/// (<c>information_schema.COLUMNS.DATA_TYPE = json</c>, the server validates and re-serialises the
/// document), but MySqlConnector has no JSON CLR binding: a <c>json</c> column always reads back as
/// <see cref="string"/> and no DOM value can be bound as a parameter. So the supported transport for a
/// MySQL <c>json</c> column is text, through <see cref="JsonColumnStorage.Auto"/> and
/// <see cref="JsonColumnStorage.Text"/> alike, and an explicit <see cref="JsonColumnStorage.Native"/>
/// request is refused before any command runs. The table is test-owned and disposable; the container is
/// the shared MySQL instance (or <c>NEXTORM_MYSQL_CONNECTION</c>). A MySQL run never stands in for
/// MariaDB evidence.
/// </summary>
public sealed class MySqlJsonColumnStorageTests : IDisposable
{
    private const string Table = "t325_mysql_json_storage";

    private readonly MySqlDataContext _context;
    private readonly SqlTap _tap = new();

    public MySqlJsonColumnStorageTests()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        _context = new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());
        Execute($"drop table if exists {Table}");
        Execute(
            $"create table {Table} (id int not null primary key, auto_json json not null, "
            + "text_json json not null, auto_text longtext not null, text_text longtext not null, "
            + "nullable_json json null)");
    }

    public void Dispose()
    {
        try
        {
            Execute($"drop table if exists {Table}");
        }
        finally
        {
            _context.Dispose();
        }
    }

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
    public void AutoAndText_ShouldRoundTripEveryCorpusRootThroughTheServerJsonColumn()
    {
        _context.AddInterceptor(_tap);

        var ddl = ReadSingle($"show create table {Table}", 1);
        Report($"mysql ddl={ddl}");
        var columnTypes = ReadColumnTypes();
        Report("mysql DATA_TYPE " + string.Join(", ", columnTypes.Select(pair => $"{pair.Key}={pair.Value}")));

        // The physical types are the test's own declaration: a genuine json type plus a longtext control.
        columnTypes["auto_json"].Should().Be("json");
        columnTypes["text_json"].Should().Be("json");
        columnTypes["nullable_json"].Should().Be("json");
        columnTypes["auto_text"].Should().Be("longtext");
        columnTypes["text_text"].Should().Be("longtext");
        ddl.Should().NotContain("json_valid");

        for (var index = 0; index < Corpus.Length; index++)
        {
            var (raw, label) = Corpus[index];
            var element = JsonDocument.Parse(raw).RootElement.Clone();
            _context.CreateInsertBuilder<IMySqlJsonStorageRow>()
                .Values(new MySqlJsonStorageRow
                {
                    Id = index + 1,
                    AutoJson = element,
                    TextJson = element,
                    AutoText = element,
                    TextText = element,
                    NullableJson = new MySqlJsonStorageDoc { Name = label, Values = [index] },
                })
                .Insert();
        }

        // SQL NULL: a null model bypasses the converter, so the column stays SQL NULL.
        _context.CreateInsertBuilder<IMySqlJsonStorageRow>()
            .Values(new MySqlJsonStorageRow
            {
                Id = Corpus.Length + 1,
                AutoJson = JsonDocument.Parse("{}").RootElement.Clone(),
                TextJson = JsonDocument.Parse("{}").RootElement.Clone(),
                AutoText = JsonDocument.Parse("{}").RootElement.Clone(),
                TextText = JsonDocument.Parse("{}").RootElement.Clone(),
                NullableJson = null,
            })
            .Insert();

        var insert = _tap.Executions.First(item => item.Sql.Contains("insert into", StringComparison.OrdinalIgnoreCase));
        Report($"mysql insert sql={insert.Sql}");
        foreach (var parameter in insert.Parameters)
            Report($"mysql   bound {parameter.Name} type={parameter.Type?.FullName ?? "<null>"} dbType={parameter.DbType} payload={parameter.Payload}");

        // Transport: every JSON value leaves the converter as text. The MySQL family lowers a text
        // mutation value into a `_utf8mb4 X'…'` literal, so the five text columns appear as literals and
        // only the integer key stays a bound parameter — no DOM value reaches the driver.
        insert.Parameters.Should().OnlyContain(parameter => parameter.Type == typeof(int));
        var literals = HexLiterals(insert.Sql);
        literals.Should().HaveCount(5);
        literals.Take(4).Should().OnlyContain(literal =>
            JsonNode.DeepEquals(JsonNode.Parse(literal), JsonNode.Parse(Corpus[0].Raw)));

        _tap.Executions.Clear();

        var rows = _context.From<MySqlJsonStorageRow>().ToList();
        rows.Should().HaveCount(Corpus.Length + 1);

        var select = _tap.Executions.Should()
            .ContainSingle(item => item.Sql.Contains("select", StringComparison.OrdinalIgnoreCase)).Subject;
        Report($"mysql select sql={select.Sql}");
        select.Sql.Should().Contain($"from {Table}");
        select.Parameters.Should().BeEmpty();

        for (var index = 0; index < Corpus.Length; index++)
        {
            var (raw, label) = Corpus[index];
            var id = index + 1;
            var expected = JsonNode.Parse(raw);
            var row = rows.Single(item => item.Id == id);

            // Auto and Text on the server json type, and Auto/Text on the longtext control.
            JsonNode.DeepEquals(JsonNode.Parse(row.AutoJson.GetRawText()), expected)
                .Should().BeTrue($"mysql/{label}: Auto on a json column");
            JsonNode.DeepEquals(JsonNode.Parse(row.TextJson.GetRawText()), expected)
                .Should().BeTrue($"mysql/{label}: Text on a json column");
            JsonNode.DeepEquals(JsonNode.Parse(row.AutoText.GetRawText()), expected)
                .Should().BeTrue($"mysql/{label}: Auto on the longtext control");
            JsonNode.DeepEquals(JsonNode.Parse(row.TextText.GetRawText()), expected)
                .Should().BeTrue($"mysql/{label}: Text on the longtext control");

            // Stored text: exact on the longtext controls, semantic on the json columns (the server
            // normalises spacing and key order).
            var controlText = Scalar($"select auto_text from {Table} where id = {id}");
            controlText.Should().Be(JsonSerializer.Serialize(JsonDocument.Parse(raw).RootElement));
            Scalar($"select text_text from {Table} where id = {id}").Should().Be(controlText);

            var jsonText = Scalar($"select auto_json from {Table} where id = {id}");
            JsonNode.DeepEquals(JsonNode.Parse(jsonText), expected)
                .Should().BeTrue($"mysql/{label}: stored json text must be semantically equal");
            Scalar($"select text_json from {Table} where id = {id}").Should().Be(jsonText);

            Report($"mysql id={id} label={label} input={raw} longtext={controlText} json={jsonText}");
        }

        // The driver returns text for a json column, whatever the server type is.
        using (var command = _context.CreateCommand($"select auto_json, text_json, auto_text from {Table} where id = 1"))
        using (var reader = command.ExecuteReader())
        {
            reader.Read().Should().BeTrue();
            for (var ordinal = 0; ordinal < 3; ordinal++)
                reader.GetFieldType(ordinal).Should().Be(typeof(string));
            Report($"mysql reader field types={string.Join(", ", Enumerable.Range(0, 3).Select(ordinal => reader.GetFieldType(ordinal).Name))}");
        }

        // JSON null is a document, not SQL NULL: the server reports json_type = 'NULL' while the column
        // itself is not null. The nullable column's null model stays SQL NULL.
        var jsonNullRowId = Array.FindIndex(Corpus, item => item.Label == "json-null") + 1;
        var jsonNullType = Scalar($"select json_type(auto_json) from {Table} where id = {jsonNullRowId}");
        var jsonNullIsNull = Scalar($"select auto_json is null from {Table} where id = {jsonNullRowId}");
        var jsonNullValid = Scalar($"select json_valid(auto_json) from {Table} where id = {jsonNullRowId}");
        Report($"mysql id={jsonNullRowId} label=json-null json_type={jsonNullType} json_is_null={jsonNullIsNull} json_valid={jsonNullValid}");
        jsonNullType.Should().Be("NULL");
        jsonNullIsNull.Should().Be("0");
        jsonNullValid.Should().Be("1");

        var sqlNullRowId = Corpus.Length + 1;
        var sqlNullIsNull = Scalar($"select nullable_json is null from {Table} where id = {sqlNullRowId}");
        Report($"mysql id={sqlNullRowId} label=sql-null nullable_json_is_null={sqlNullIsNull}");
        sqlNullIsNull.Should().Be("1");
        rows.Single(item => item.Id == sqlNullRowId).NullableJson.Should().BeNull();
        rows.Single(item => item.Id == 1).NullableJson!.Name.Should().Be("nested-object-unicode");

        Report($"mysql corpus rows={Corpus.Length + 1} result=OK");
    }

    /// <summary>
    /// MySQL compares a <c>json</c> document as a typed value: a raw string operand is a JSON *string*,
    /// not a document, so it never equals the stored object — only a JSON-typed operand does. This is the
    /// fact behind the docs statement that nextorm promises no equality translation for such a column.
    /// </summary>
    [Fact]
    public void NativeJsonEquality_IsTyped_StringOperandNeverMatchesCastDocument()
    {
        const string equalityTable = "t325_mysql_json_equality";
        const string document = "{\"a\":1}";

        Execute($"drop table if exists {equalityTable}");
        Execute($"create table {equalityTable} (id int not null primary key, doc json not null)");
        try
        {
            Execute($"insert into {equalityTable} (id, doc) values (1, '{document}')");

            var stringOperandMatches = Scalar($"select count(*) from {equalityTable} where doc = '{document}'");
            var castDocumentMatches = Scalar($"select count(*) from {equalityTable} where doc = cast('{document}' as json)");
            Report($"mysql equality raw_string_operand_count={stringOperandMatches} cast_json_operand_count={castDocumentMatches}");

            stringOperandMatches.Should().Be("0", "a string operand is compared as a JSON string, not as the document");
            castDocumentMatches.Should().Be("1", "a JSON-typed operand matches the stored document");
        }
        finally
        {
            Execute($"drop table if exists {equalityTable}");
        }
    }

    [Fact]
    public void MalformedJson_ShouldBeRejectedByTheServerJsonColumn_ButAcceptedByTheTextControl()
    {
        const string malformed = "{\"a\":";

        // Column semantics are independent of the client serialization: a raw malformed document is the
        // server's business, and a genuine json column refuses it (MySQL error 3140, "Invalid JSON text").
        var rejection = FluentActions.Invoking(() =>
            Execute($"insert into {Table} (id, auto_json, text_json, auto_text, text_text) "
                + $"values (100, '{malformed}', '{{}}', '{malformed}', '{malformed}')"))
            .Should().Throw<MySqlConnector.MySqlException>();
        var error = rejection.Which;
        Report($"mysql invalid-json error number={error.Number} sqlstate={error.SqlState} message={error.Message}");
        error.Number.Should().BeOneOf(3140, 22032);
        error.Message.Should().Contain("Invalid JSON text");

        // The same raw text is legitimate in the longtext control — the rejection above is the column
        // type, not a client-side validation.
        Execute($"insert into {Table} (id, auto_json, text_json, auto_text, text_text) "
            + $"values (101, '{{}}', '{{}}', '{malformed}', '{malformed}')");
        Scalar("select auto_text from " + Table + " where id = 101").Should().Be(malformed);
        Scalar($"select count(*) from {Table} where id = 100").Should().Be("0");
    }

    [Fact]
    public void Native_ShouldBeRefusedBeforeAnyCommandRuns()
    {
        _context.AddInterceptor(_tap);

        var act = () => _context.From<MySqlJsonNativeRow>().ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(MySqlDialect)} does not support a native JSON column*")
            .WithMessage($"*{nameof(JsonColumnStorage.Text)}*");

        // No fallback, no driver-time binding: nothing reached the server.
        _tap.Executions.Should().BeEmpty();
        Scalar($"select count(*) from {Table}").Should().Be("0");
        Report("mysql native refusal: zero commands executed");
    }

    private Dictionary<string, string> ReadColumnTypes()
    {
        using var command = _context.CreateCommand(
            "select column_name, data_type from information_schema.columns "
            + $"where table_schema = database() and table_name = '{Table}' order by ordinal_position");
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    private string ReadSingle(string sql, int ordinal)
    {
        using var command = _context.CreateCommand(sql);
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        return reader.GetString(ordinal);
    }

    private string Scalar(string sql)
    {
        using var command = _context.CreateCommand(sql);
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void Execute(string sql)
    {
        _context.EnsureConnectionOpen();
        using var command = _context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

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

    private static void Report(string message) => Console.WriteLine($"[T325-mysql] {message}");

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
