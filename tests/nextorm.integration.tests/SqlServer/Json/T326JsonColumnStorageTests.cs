using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// T326 (#326) — the live SQL Server matrix through the ORM. The columns are declared by the tests
/// (<c>j json null</c> and <c>n nvarchar(max) null</c>) so the server-reported type, the emitted SQL, the
/// bound parameter metadata and the stored bytes are all observed facts, not inferences. The native arm
/// needs SQL Server 2025+ (major 17) and skips on older servers; the text control arm and the gate tests
/// run everywhere.
/// </summary>
[SqlTable(T326JsonColumnStorageTests.Table)]
public interface IT326JsonStorageRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("j")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    JsonElement? J { get; set; }

    [Column("n")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    JsonElement? N { get; set; }
}

/// <summary>Concrete row for <see cref="IT326JsonStorageRow"/>.</summary>
public sealed class T326JsonStorageRow : IT326JsonStorageRow
{
    public long Id { get; set; }

    public JsonElement? J { get; set; }

    public JsonElement? N { get; set; }
}

/// <summary>The text-only mapping of the same table, used for the scalar roots the native column rejects.</summary>
[SqlTable(T326JsonColumnStorageTests.Table)]
public interface IT326JsonTextOnlyRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("n")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    JsonElement? N { get; set; }
}

/// <summary>Concrete row for <see cref="IT326JsonTextOnlyRow"/>.</summary>
public sealed class T326JsonTextOnlyRow : IT326JsonTextOnlyRow
{
    public long Id { get; set; }

    public JsonElement? N { get; set; }
}

/// <summary>
/// The <see cref="JsonColumnStorage.Auto"/> mapping of the text column: on a gate-open dialect Auto must
/// still store the identical bytes, because a native representation exists and resolves to the same text
/// transport.
/// </summary>
[SqlTable(T326JsonColumnStorageTests.Table)]
public interface IT326JsonAutoRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("n")]
    [JsonColumn]
    JsonElement? N { get; set; }
}

/// <summary>Concrete row for <see cref="IT326JsonAutoRow"/>.</summary>
public sealed class T326JsonAutoRow : IT326JsonAutoRow
{
    public long Id { get; set; }

    public JsonElement? N { get; set; }
}

/// <summary>A version-17 (SQL Server 2025) context over the live container.</summary>
internal sealed class T326V17LiveContext(string connectionString)
    : SqlServerDataContext(new Version(17, 0), connectionString);

/// <summary>
/// Writes the structured evidence lines to the console and, when the run provides a path, to a file, so
/// the matrix survives a passing run whose console output is not echoed by the test platform.
/// </summary>
internal static class T326Evidence
{
    internal const string Variable = "NEXTORM_T326_EVIDENCE";

    public static void Write(string message)
    {
        Console.WriteLine("[T326-sqlserver] " + message);

        var path = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrEmpty(path))
            File.AppendAllText(path, message + Environment.NewLine);
    }
}

public sealed class T326JsonColumnStorageTests : IDisposable
{
    internal const string Table = "t326_json_storage";

    private readonly T326V17LiveContext _context;
    private readonly SqlTap _tap = new();
    private readonly int _major;

    public T326JsonColumnStorageTests()
    {
        // Fail closed exactly like ProviderTestSuite: an unavailable SQL Server is an error, and only an
        // explicit NEXTORM_SKIP_PROVIDERS opt-out may skip. A version-first context is constructed here,
        // because the shared provider context is deliberately unversioned.
        if (!SqlServerContainer.IsAvailable)
        {
            if (ProviderSkipPolicy.IsOptedOut("sqlserver"))
                Assert.Skip($"Provider 'sqlserver' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}.");

            SqlServerContainer.IsAvailable.Should().BeTrue(
                "provider 'sqlserver' is unavailable ({0}). Integration tests fail closed so a missing database "
                + "cannot look like a pass: start the container runtime and set DOCKER_HOST, or opt this provider "
                + "out explicitly with NEXTORM_SKIP_PROVIDERS=sqlserver",
                SqlServerContainer.Failure);
        }

        _context = new T326V17LiveContext(SqlServerContainer.ConnectionString);
        _major = ReadMajor(_context);

        // Only the native arm is version-bound: the text control and the gate tests keep running on 2022.
        Assert.SkipWhen(
            _major < 17,
            $"SQL Server major version {_major} has no native json type; the native arm needs 17 (2025+).");

        Execute($"if object_id('{Table}') is null create table {Table} (id bigint not null primary key, j json null, n nvarchar(max) null)");
        Execute($"delete from {Table}");
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

    [Fact]
    public void DeclaredColumns_ShouldReportJsonAndNvarcharFromTheServer()
    {
        var types = ReadColumnTypes();
        Report($"T326META table={Table} columns={string.Join(",", types.Select(pair => $"{pair.Key}={pair.Value}"))}");

        types["j"].Should().Be("json");
        types["n"].Should().Be("nvarchar");
        Report($"T326META declared j=json n=nvarchar(max) server_reported j={types["j"]} n={types["n"]} major={_major}");
    }

    // C3: the native column round-trips object roots and preserves SQL NULL.
    [Fact]
    public void NativeObjectRootsAndSqlNull_ShouldRoundTrip()
    {
        _context.AddInterceptor(_tap);

        var cases = new (long Id, string Label, string Json)[]
        {
            (1, "C3_native_nested_object", "{\"name\":\"alpha\",\"inner\":{\"k\":1,\"flag\":true}}"),
            (2, "C3_native_array_root", "[1,2,3]"),
            (3, "C3_native_unicode_object", "{\"name\":\"Привет, мир — ünïcode\",\"emoji\":\"🙂\"}"),
        };

        foreach (var (id, label, json) in cases)
        {
            var element = Parse(json);
            var before = _tap.Executions.Count;
            _context.CreateInsertBuilder<IT326JsonStorageRow>()
                .Values(new T326JsonStorageRow { Id = id, J = element, N = element })
                .Insert();

            ReportCase(label, "j", before, "ok");
            ReportRead(label, "j", id, Parse(json));
            ReportRead(label, "n", id, Parse(json));
        }

        // SQL NULL on the native column (and on the text control).
        var nullIndex = _tap.Executions.Count;
        _context.CreateInsertBuilder<IT326JsonStorageRow>()
            .Values(new T326JsonStorageRow { Id = 4, J = null, N = null })
            .Insert();
        ReportCase("C3_native_sql_null", "j", nullIndex, "ok");

        var nullRead = ReadBack("j", 4);
        Report($"T326READ case=C3_native_sql_null column=j clr={nullRead.FieldType} value={nullRead.Value} isdbnull={nullRead.IsDbNull} datalength={nullRead.DataLength} isnull={nullRead.IsNull}");
        nullRead.IsNull.Should().BeTrue();

        var textNullRead = ReadBack("n", 4);
        Report($"T326READ case=C3_text_sql_null column=n clr={textNullRead.FieldType} value={textNullRead.Value} isdbnull={textNullRead.IsDbNull} datalength={textNullRead.DataLength} isnull={textNullRead.IsNull}");
        textNullRead.IsNull.Should().BeTrue();

        var rows = _context.From<T326JsonStorageRow>().OrderBy(x => x.Id).ToList();
        rows.Should().HaveCount(4);
        foreach (var (id, label, json) in cases)
        {
            var row = rows.Single(x => x.Id == id);
            row.J.Should().NotBeNull();
            JsonNode.DeepEquals(JsonNode.Parse(row.J!.Value.GetRawText()), JsonNode.Parse(json))
                .Should().BeTrue($"case {label}: the native column must round-trip the document");
            JsonNode.DeepEquals(JsonNode.Parse(row.N!.Value.GetRawText()), JsonNode.Parse(json))
                .Should().BeTrue($"case {label}: the text control must round-trip the document");
        }

        rows.Single(x => x.Id == 4).J.Should().BeNull("SQL NULL on the native json column stays SQL NULL");
        rows.Single(x => x.Id == 4).N.Should().BeNull();
    }

    // C4: the native column rejects every scalar root with the server's own error; the text column accepts them.
    [Fact]
    public void NativeScalarRoots_ShouldBeRejectedByTheServer_WhileTheTextColumnAcceptsThem()
    {
        _context.AddInterceptor(_tap);

        var scalars = new (long Id, string Label, string Json)[]
        {
            (5, "C4_native_scalar_string", "\"text\""),
            (6, "C4_native_scalar_number", "42.5"),
            (7, "C4_native_scalar_bool", "true"),
            (8, "C4_native_scalar_json_null", "null"),
        };

        foreach (var (id, label, json) in scalars)
        {
            var element = Parse(json);
            var before = _tap.Executions.Count;

            var rejection = FluentActions.Invoking(() => _context.CreateInsertBuilder<IT326JsonStorageRow>()
                .Values(new T326JsonStorageRow { Id = id, J = element, N = element })
                .Insert()).Should().Throw<SqlException>();

            ReportCase(label, "j", before, $"server_error number={rejection.Which.Number}");
            rejection.Which.Number.Should().Be(13609, "the server, not the library, limits a json column to object/array roots");

            // The very same document is accepted by the nvarchar(max) control column.
            var textIndex = _tap.Executions.Count;
            _context.CreateInsertBuilder<IT326JsonTextOnlyRow>()
                .Values(new T326JsonTextOnlyRow { Id = id, N = element })
                .Insert();
            ReportCase(label.Replace("_native_", "_text_", StringComparison.Ordinal), "n", textIndex, "ok");
            ReportRead(label, "n", id, Parse(json));
        }

        var stored = _context.From<T326JsonTextOnlyRow>().OrderBy(x => x.Id).ToList();
        stored.Should().HaveCount(4);
        foreach (var (id, label, json) in scalars)
        {
            var expected = JsonNode.Parse(json);
            var row = stored.Single(x => x.Id == id);
            if (expected is null)
            {
                // The JSON null document deserializes to the model's default value, so the CLR side cannot
                // tell it apart from SQL NULL; C5 pins the distinction where it exists — in the column
                // (DATALENGTH 8, IS NULL = 0). Nothing else is claimed here.
                row.N.Should().BeNull("a JSON null document deserializes to the default value");
                continue;
            }

            row.N.Should().NotBeNull();
            JsonNode.DeepEquals(JsonNode.Parse(row.N!.Value.GetRawText()), expected)
                .Should().BeTrue($"case {label}: the text column stores the scalar document");
        }

        // Nothing reached the native column: the four rejected rows do not exist.
        Scalar("select count(*) from " + Table + " where id between 5 and 8").Should().Be("4");
        Scalar("select count(*) from " + Table + " where id between 5 and 8 and j is not null").Should().Be("0");
    }

    // C5: JSON null and SQL NULL are different states on both column kinds.
    [Fact]
    public void JsonNullAndSqlNull_ShouldStayDistinctOnBothColumnKinds()
    {
        _context.AddInterceptor(_tap);

        var jsonNullIndex = _tap.Executions.Count;
        _context.CreateInsertBuilder<T326JsonTextOnlyRow>()
            .Values(new T326JsonTextOnlyRow { Id = 30, N = Parse("null") })
            .Insert();
        ReportCase("C5_text_json_null", "n", jsonNullIndex, "ok");

        var sqlNullIndex = _tap.Executions.Count;
        _context.CreateInsertBuilder<IT326JsonStorageRow>()
            .Values(new T326JsonStorageRow { Id = 31, J = null, N = null })
            .Insert();
        ReportCase("C5_sql_null", "j", sqlNullIndex, "ok");

        var jsonNull = ReadBack("n", 30);
        Report($"T326READ case=C5_text_json_null column=n clr={jsonNull.FieldType} value={jsonNull.Value} isdbnull={jsonNull.IsDbNull} datalength={jsonNull.DataLength} isnull={jsonNull.IsNull}");
        jsonNull.Value.Should().Be("null");
        jsonNull.Value!.Length.Should().Be(4, "JSON null is the four-character document text, not SQL NULL");
        jsonNull.DataLength.Should().Be("8", "nvarchar stores 2 bytes per character");
        jsonNull.IsNull.Should().BeFalse();

        var sqlNull = ReadBack("n", 31);
        Report($"T326READ case=C5_text_sql_null column=n clr={sqlNull.FieldType} value={sqlNull.Value} isdbnull={sqlNull.IsDbNull} datalength={sqlNull.DataLength} isnull={sqlNull.IsNull}");
        sqlNull.IsNull.Should().BeTrue();
        sqlNull.DataLength.Should().Be("NULL");

        // On the native column the same JSON null document is a server limitation (13609, C4), while SQL
        // NULL is a real state (C3) — the library adds no client-side rule on top of the server's.
        var nativeSqlNull = ReadBack("j", 31);
        Report($"T326READ case=C5_native_sql_null column=j clr={nativeSqlNull.FieldType} value={nativeSqlNull.Value} isdbnull={nativeSqlNull.IsDbNull} datalength={nativeSqlNull.DataLength} isnull={nativeSqlNull.IsNull}");
        nativeSqlNull.IsNull.Should().BeTrue();
    }

    // C2 on the gate-open dialect: Auto and Text store the identical bytes on the text column.
    [Fact]
    public void AutoAndText_ShouldStoreTheSameDocumentOnTheGateOpenDialect()
    {
        _context.AddInterceptor(_tap);

        const string json = "{\"name\":\"alpha\",\"inner\":{\"k\":1,\"flag\":true}}";
        var element = Parse(json);

        var autoIndex = _tap.Executions.Count;
        _context.CreateInsertBuilder<IT326JsonAutoRow>()
            .Values(new T326JsonAutoRow { Id = 20, N = element })
            .Insert();
        ReportCase("C2_auto_object", "n", autoIndex, "ok");

        var textIndex = _tap.Executions.Count;
        _context.CreateInsertBuilder<IT326JsonTextOnlyRow>()
            .Values(new T326JsonTextOnlyRow { Id = 21, N = element })
            .Insert();
        ReportCase("C2_text_object", "n", textIndex, "ok");

        var auto = ReadBack("n", 20);
        var text = ReadBack("n", 21);
        Report($"T326READ case=C2_auto_object column=n clr={auto.FieldType} value={auto.Value} isdbnull={auto.IsDbNull} datalength={auto.DataLength} isnull={auto.IsNull}");
        Report($"T326READ case=C2_text_object column=n clr={text.FieldType} value={text.Value} isdbnull={text.IsDbNull} datalength={text.DataLength} isnull={text.IsNull}");

        auto.Value.Should().Be(text.Value, "Auto on a gate-open SQL Server must resolve to the same text transport");
        auto.DataLength.Should().Be(text.DataLength);
        auto.Value.Should().Be(JsonSerializer.Serialize(Parse(json)));

        var autoRow = _context.From<T326JsonAutoRow>().ToList().Single(x => x.Id == 20);
        JsonNode.DeepEquals(JsonNode.Parse(autoRow.N!.Value.GetRawText()), JsonNode.Parse(json)).Should().BeTrue();
    }

    private void ReportCase(string label, string column, int executionIndex, string outcome)
    {
        var commands = _tap.Executions;
        var command = executionIndex < commands.Count ? commands[executionIndex] : null;
        var sql = command is null ? "sql=<none>" : "sql=" + command.Sql.ReplaceLineEndings(" ");
        var parameters = command is null
            ? "params=[]"
            : "params=[" + string.Join("; ", command.Parameters.Select(Describe)) + "]";

        Report($"T326CASE class=storage major={_major} case={label} column={column} {sql} {parameters} outcome={outcome}");
    }

    private static string Describe(CapturedParameter parameter)
        => $"{parameter.Name}:clr={parameter.Type}:dbtype={parameter.DbType}:sqltype={parameter.SqlDbType}:size={parameter.Size}:value={parameter.Payload}";

    private void ReportRead(string label, string column, long id, JsonElement expected)
    {
        var read = ReadBack(column, id);
        var equal = !read.IsDbNull && JsonNode.DeepEquals(JsonNode.Parse(read.Value!), JsonNode.Parse(expected.GetRawText()));
        Report($"T326READ case={label} column={column} clr={read.FieldType} value={read.Value} isdbnull={read.IsDbNull} datalength={read.DataLength} isnull={read.IsNull} json_equal={equal}");
        equal.Should().BeTrue($"case {label} on column {column} must read back the same document");
    }

    private SqlValue ReadBack(string column, long id)
    {
        using var command = _context.CreateCommand(
            $"select {column}, datalength({column}), case when {column} is null then 1 else 0 end from {Table} where id = {id.ToString(CultureInfo.InvariantCulture)}");
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();

        return new SqlValue(
            reader.GetFieldType(0).Name,
            reader.IsDBNull(0) ? "NULL" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
            reader.IsDBNull(0),
            reader.IsDBNull(1) ? "NULL" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "NULL",
            reader.GetInt32(2) == 1);
    }

    private Dictionary<string, string> ReadColumnTypes()
    {
        using var command = _context.CreateCommand(
            "select column_name, data_type from information_schema.columns "
            + $"where table_name = '{Table}' order by ordinal_position");
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    private static int ReadMajor(IDataContext context)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand("select cast(serverproperty('ProductMajorVersion') as int)");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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

    private static void Report(string message) => T326Evidence.Write(message);

    private sealed record SqlValue(string FieldType, string Value, bool IsDbNull, string DataLength, bool IsNull);

    private sealed record Captured(string Sql, List<CapturedParameter> Parameters);

    private sealed record CapturedParameter(string Name, string Type, string DbType, string SqlDbType, int Size, string Payload);

    /// <summary>Records every executed command with its ordered bound parameters.</summary>
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
                    DBNull => "NULL",
                    null => "NULL",
                    _ => Convert.ToString(parameter.Value, CultureInfo.InvariantCulture) ?? "NULL",
                };
                parameters.Add(new CapturedParameter(
                    parameter.ParameterName,
                    parameter.Value is DBNull or null ? "DBNull" : parameter.Value.GetType().FullName ?? "null",
                    parameter.DbType.ToString(),
                    (parameter as SqlParameter)?.SqlDbType.ToString() ?? "n/a",
                    parameter.Size,
                    payload));
            }

            Executions.Add(new Captured(command.CommandText, parameters));
        }
    }
}
