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

namespace NextORM.Integration.Tests;

/// <summary>The reference model used for the nullable JSON column.</summary>
public sealed class MariaDbJsonStorageDoc
{
    public string? Name { get; set; }

    public List<int> Values { get; set; } = [];

    public MariaDbJsonStorageDoc? Inner { get; set; }
}

/// <summary>
/// T325 (#325) — MariaDB's <c>JSON</c> type is an alias for <c>LONGTEXT CHARACTER SET utf8mb4 COLLATE
/// utf8mb4_bin</c> plus a column-level <c>CHECK (json_valid(col))</c>. The columns are declared by this
/// test, so the alias and its validation rule are the subject, not an inference from MySQL.
/// </summary>
[SqlTable("t325_mariadb_json_storage")]
public interface IMariaDbJsonStorageRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("alias_json")]
    [JsonColumn]
    JsonElement AliasJson { get; set; }

    [Column("text_alias")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    JsonElement TextAlias { get; set; }

    [Column("alias_longtext")]
    [JsonColumn]
    JsonElement AliasLongText { get; set; }

    [Column("nullable_alias")]
    [JsonColumn]
    MariaDbJsonStorageDoc? NullableAlias { get; set; }
}

/// <summary>Concrete row for <see cref="IMariaDbJsonStorageRow"/>.</summary>
public sealed class MariaDbJsonStorageRow : IMariaDbJsonStorageRow
{
    public int Id { get; set; }

    public JsonElement AliasJson { get; set; }

    public JsonElement TextAlias { get; set; }

    public JsonElement AliasLongText { get; set; }

    public MariaDbJsonStorageDoc? NullableAlias { get; set; }
}

/// <summary>An explicit <see cref="JsonColumnStorage.Native"/> mapping: must be refused.</summary>
[SqlTable("t325_mariadb_json_storage")]
public interface IMariaDbJsonNativeRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("alias_json")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    MariaDbJsonStorageDoc Doc { get; set; }
}

/// <summary>Concrete row for <see cref="IMariaDbJsonNativeRow"/>.</summary>
public sealed class MariaDbJsonNativeRow : IMariaDbJsonNativeRow
{
    public int Id { get; set; }

    public MariaDbJsonStorageDoc Doc { get; set; } = new();
}

/// <summary>
/// T325 (#325) — MariaDB live storage facts, collected on MariaDB itself (own provider, own container),
/// never inferred from MySQL. A declared <c>JSON</c> column reports <c>DATA_TYPE = longtext</c>, carries
/// a <c>json_valid</c> CHECK constraint, stores the caller's text verbatim (no normalisation) and is used
/// through the text transport; an explicit <see cref="JsonColumnStorage.Native"/> request is refused
/// before any command runs.
/// </summary>
public sealed class MariaDbJsonColumnStorageTests : IDisposable
{
    private const string Table = "t325_mariadb_json_storage";

    private readonly MariaDbDataContext _context;
    private readonly SqlTap _tap = new();

    public MariaDbJsonColumnStorageTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _context = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Execute($"drop table if exists {Table}");
        Execute(
            $"create table {Table} (id int not null primary key, alias_json json not null, "
            + "text_alias json not null, alias_longtext longtext not null, nullable_alias json null)");
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
    public void DeclaredJsonAlias_ShouldReportLongtextAndCarryAJsonValidCheck()
    {
        var ddl = ReadSingle($"show create table {Table}", 1);
        Report($"mariadb ddl={ddl}");

        var columnTypes = ReadColumnTypes();
        Report("mariadb DATA_TYPE " + string.Join(", ", columnTypes.Select(pair => $"{pair.Key}={pair.Value}")));

        // Every column is longtext: the JSON spelling never reaches the metadata as a distinct type.
        columnTypes["alias_json"].Should().Be("longtext");
        columnTypes["text_alias"].Should().Be("longtext");
        columnTypes["alias_longtext"].Should().Be("longtext");
        columnTypes["nullable_alias"].Should().Be("longtext");

        // Only the JSON-declared columns carry the validation rule.
        ddl.Should().Contain("json_valid(`alias_json`)");
        ddl.Should().Contain("json_valid(`text_alias`)");
        ddl.Should().NotContain("json_valid(`alias_longtext`)");
        ddl.Should().Contain("utf8mb4_bin");

        var checks = ReadCheckConstraints();
        checks.Should().HaveCount(3);
        checks.Should().OnlyContain(item => item.Contains("json_valid", StringComparison.OrdinalIgnoreCase));
        foreach (var check in checks)
            Report($"mariadb CHECK {check}");
    }

    [Fact]
    public void AutoAndText_ShouldRoundTripEveryCorpusRootAndStoreTheTextVerbatim()
    {
        _context.AddInterceptor(_tap);

        for (var index = 0; index < Corpus.Length; index++)
        {
            var (raw, label) = Corpus[index];
            var element = JsonDocument.Parse(raw).RootElement.Clone();
            _context.CreateInsertBuilder<IMariaDbJsonStorageRow>()
                .Values(new MariaDbJsonStorageRow
                {
                    Id = index + 1,
                    AliasJson = element,
                    TextAlias = element,
                    AliasLongText = element,
                    NullableAlias = new MariaDbJsonStorageDoc { Name = label, Values = [index] },
                })
                .Insert();
        }

        _context.CreateInsertBuilder<IMariaDbJsonStorageRow>()
            .Values(new MariaDbJsonStorageRow
            {
                Id = Corpus.Length + 1,
                AliasJson = JsonDocument.Parse("{}").RootElement.Clone(),
                TextAlias = JsonDocument.Parse("{}").RootElement.Clone(),
                AliasLongText = JsonDocument.Parse("{}").RootElement.Clone(),
                NullableAlias = null,
            })
            .Insert();

        var insert = _tap.Executions.First(item => item.Sql.Contains("insert into", StringComparison.OrdinalIgnoreCase));
        Report($"mariadb insert sql={insert.Sql}");
        foreach (var parameter in insert.Parameters)
            Report($"mariadb   bound {parameter.Name} type={parameter.Type?.FullName ?? "<null>"} dbType={parameter.DbType} payload={parameter.Payload}");

        insert.Parameters.Should().OnlyContain(parameter => parameter.Type == typeof(int));
        var literals = HexLiterals(insert.Sql);
        literals.Should().HaveCount(4);
        literals.Take(3).Should().OnlyContain(literal =>
            JsonNode.DeepEquals(JsonNode.Parse(literal), JsonNode.Parse(Corpus[0].Raw)));

        _tap.Executions.Clear();

        var rows = _context.From<MariaDbJsonStorageRow>().ToList();
        rows.Should().HaveCount(Corpus.Length + 1);

        var select = _tap.Executions.Should()
            .ContainSingle(item => item.Sql.Contains("select", StringComparison.OrdinalIgnoreCase)).Subject;
        Report($"mariadb select sql={select.Sql}");
        select.Sql.Should().Contain($"from {Table}");

        for (var index = 0; index < Corpus.Length; index++)
        {
            var (raw, label) = Corpus[index];
            var id = index + 1;
            var expected = JsonNode.Parse(raw);
            var row = rows.Single(item => item.Id == id);
            var serialized = JsonSerializer.Serialize(JsonDocument.Parse(raw).RootElement);

            JsonNode.DeepEquals(JsonNode.Parse(row.AliasJson.GetRawText()), expected)
                .Should().BeTrue($"mariadb/{label}: Auto on the JSON alias");
            JsonNode.DeepEquals(JsonNode.Parse(row.TextAlias.GetRawText()), expected)
                .Should().BeTrue($"mariadb/{label}: Text on the JSON alias");
            JsonNode.DeepEquals(JsonNode.Parse(row.AliasLongText.GetRawText()), expected)
                .Should().BeTrue($"mariadb/{label}: Auto on the longtext control");

            // MariaDB stores the longtext alias verbatim: no re-serialisation, no key reordering.
            Scalar($"select alias_json from {Table} where id = {id}").Should().Be(serialized);
            Scalar($"select text_alias from {Table} where id = {id}").Should().Be(serialized);
            Scalar($"select alias_longtext from {Table} where id = {id}").Should().Be(serialized);

            Report($"mariadb id={id} label={label} input={raw} stored={serialized}");
        }

        // The driver returns text for the JSON alias, exactly as for a plain longtext column.
        using (var command = _context.CreateCommand($"select alias_json, alias_longtext from {Table} where id = 1"))
        using (var reader = command.ExecuteReader())
        {
            reader.Read().Should().BeTrue();
            reader.GetFieldType(0).Should().Be(typeof(string));
            reader.GetFieldType(1).Should().Be(typeof(string));
            Report($"mariadb reader field types={reader.GetFieldType(0).Name}, {reader.GetFieldType(1).Name}");
        }

        // JSON null is a real document in the alias; the nullable column's null model is SQL NULL.
        var jsonNullRowId = Array.FindIndex(Corpus, item => item.Label == "json-null") + 1;
        var jsonNullValid = Scalar($"select json_valid(alias_json) from {Table} where id = {jsonNullRowId}");
        var jsonNullIsNull = Scalar($"select alias_json is null from {Table} where id = {jsonNullRowId}");
        var jsonNullType = Scalar($"select json_type(alias_json) from {Table} where id = {jsonNullRowId}");
        Report($"mariadb id={jsonNullRowId} label=json-null json_valid={jsonNullValid} json_is_null={jsonNullIsNull} json_type={jsonNullType}");
        jsonNullValid.Should().Be("1");
        jsonNullIsNull.Should().Be("0");
        jsonNullType.Should().Be("NULL");

        var sqlNullRowId = Corpus.Length + 1;
        var sqlNullIsNull = Scalar($"select nullable_alias is null from {Table} where id = {sqlNullRowId}");
        Report($"mariadb id={sqlNullRowId} label=sql-null nullable_alias_is_null={sqlNullIsNull}");
        sqlNullIsNull.Should().Be("1");
        rows.Single(item => item.Id == sqlNullRowId).NullableAlias.Should().BeNull();
        rows.Single(item => item.Id == 1).NullableAlias!.Name.Should().Be("nested-object-unicode");

        Report($"mariadb corpus rows={Corpus.Length + 1} result=OK");
    }

    [Fact]
    public void MalformedJson_ShouldBeRejectedByTheJsonAliasCheck_ButAcceptedByTheLongtextControl()
    {
        const string malformed = "{\"a\":";

        // The validation lives in the column: the very same text is refused by the JSON alias and
        // accepted by the plain longtext column.
        var rejection = FluentActions.Invoking(() =>
            Execute($"insert into {Table} (id, alias_json, text_alias, alias_longtext) "
                + $"values (100, '{malformed}', '{{}}', '{malformed}')"))
            .Should().Throw<DbException>();
        var error = rejection.Which;
        Report($"mariadb invalid-json error type={error.GetType().Name} number={(error as MySqlConnector.MySqlException)?.Number} message={error.Message}");
        error.Message.Should().ContainEquivalentOf("constraint");

        Execute($"insert into {Table} (id, alias_json, text_alias, alias_longtext) "
            + $"values (101, '{{}}', '{{}}', '{malformed}')");
        Scalar($"select alias_longtext from {Table} where id = 101").Should().Be(malformed);
        Scalar($"select count(*) from {Table} where id = 100").Should().Be("0");
    }

    [Fact]
    public void Native_ShouldBeRefusedBeforeAnyCommandRuns()
    {
        _context.AddInterceptor(_tap);

        var act = () => _context.From<MariaDbJsonNativeRow>().ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(MariaDbDialect)} does not support a native JSON column*")
            .WithMessage($"*{nameof(JsonColumnStorage.Text)}*");

        _tap.Executions.Should().BeEmpty();
        Scalar($"select count(*) from {Table}").Should().Be("0");
        Report("mariadb native refusal: zero commands executed");
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

    private List<string> ReadCheckConstraints()
    {
        using var command = _context.CreateCommand(
            "select check_clause from information_schema.check_constraints "
            + $"where constraint_schema = database() and table_name = '{Table}' order by constraint_name");
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
            result.Add(reader.GetString(0));
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

    private static void Report(string message) => Console.WriteLine($"[T325-mariadb] {message}");

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
