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
/// The declared-Azure mapping over the existing <c>nvarchar(max)</c> column: the storage form is explicit
/// <see cref="JsonColumnStorage.Native"/>, yet SQL Server transports it as text, so no schema change is
/// needed for an existing text column.
/// </summary>
[SqlTable(T345JsonEditionRegressionTests.TextTable)]
public interface IT345NativeTextStorageRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    JsonElement? Data { get; set; }
}

/// <summary>Concrete row for <see cref="IT345NativeTextStorageRow"/>.</summary>
public sealed class T345NativeTextStorageRow : IT345NativeTextStorageRow
{
    public long Id { get; set; }

    public JsonElement? Data { get; set; }
}

/// <summary>The same mapping over a real <c>json</c> column (2025 only).</summary>
[SqlTable(T345JsonEditionRegressionTests.NativeTable)]
public interface IT345NativeJsonStorageRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    JsonElement? Data { get; set; }
}

/// <summary>Concrete row for <see cref="IT345NativeJsonStorageRow"/>.</summary>
public sealed class T345NativeJsonStorageRow : IT345NativeJsonStorageRow
{
    public long Id { get; set; }

    public JsonElement? Data { get; set; }
}

/// <summary>The declared-Azure context: version 12 and engine edition 5, the boxed Azure SQL shape.</summary>
internal sealed class T345DeclaredAzureLiveContext(string connectionString)
    : SqlServerDataContext(new Version(12, 0), SqlServerEngineEdition.SqlDatabase, connectionString);

/// <summary>The unversioned control over the same server: no version, no edition.</summary>
internal sealed class T345UnversionedLiveContext(string connectionString)
    : SqlServerDataContext(connectionString, new DataContextBuilder());

/// <summary>
/// T345 (#345) — the live regression for the edition-derived native <c>json</c> capability. One class runs
/// on both containers (SQL Server 2022, actual major 16, and SQL Server 2025, actual major 17): the
/// declaration-derived capability is asserted on both, the <c>json</c>-column halves return an evidence line
/// on the server that cannot store the type, and the text transport is asserted unchanged everywhere. Every
/// line
/// records the actual server facts, the declared pair, the emitted SQL, the bound parameter metadata, the
/// physical column type, the row result and the executed-command count; nothing here is presented as live
/// Azure/Fabric proof.
/// </summary>
public sealed class T345JsonEditionRegressionTests : IDisposable
{
    internal const string TextTable = "t345_json_text";
    internal const string NativeTable = "t345_json_native";

    private readonly int _major;
    private readonly int _engineEdition;
    private readonly string _tag;

    public T345JsonEditionRegressionTests()
    {
        if (!SqlServerContainer.IsAvailable)
        {
            if (ProviderSkipPolicy.IsOptedOut("sqlserver"))
                Assert.Skip($"Provider 'sqlserver' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}.");

            SqlServerContainer.IsAvailable.Should().BeTrue(
                "provider 'sqlserver' is unavailable ({0}); the edition regression test fails closed rather than skipping silently",
                SqlServerContainer.Failure);
        }

        using var probe = new T345UnversionedLiveContext(SqlServerContainer.ConnectionString);
        (_major, _engineEdition) = ReadServerFacts(probe);
        _major.Should().BeGreaterThan(0, "the server must report a product major version");
        _tag = _major >= 17 ? "L25" : "L22";
    }

    public void Dispose()
    {
        using var context = new T345UnversionedLiveContext(SqlServerContainer.ConnectionString);
        Execute(context, $"drop table if exists {NativeTable}");
        Execute(context, $"drop table if exists {TextTable}");
    }

    [Fact]
    public void DeclaredAzureCapability_ShouldBeAFunctionOfTheDeclaration()
    {
        using var declared = new T345DeclaredAzureLiveContext(SqlServerContainer.ConnectionString);
        using var unversioned = new T345UnversionedLiveContext(SqlServerContainer.ConnectionString);

        declared.Dialect.SupportsJson.Should().BeTrue(
            "the declared Azure edition opens the capability without any server query");
        unversioned.Dialect.SupportsJson.Should().BeFalse(
            "the same server through an unversioned context keeps the historical text mapping");

        Report(
            $"{_tag}-DECLARED actual_major={_major} actual_engine_edition={_engineEdition} "
            + "declared=[version: 12.0, engine edition: SqlDatabase (5)] "
            + $"declared_supports_json={declared.Dialect.SupportsJson} "
            + $"unversioned_supports_json={unversioned.Dialect.SupportsJson} outcome=declaration_only");
    }

    [Fact]
    public void DeclaredAzureNative_ShouldPrepareWithoutExecuting_AndRoundTripThroughNvarchar()
    {
        using var context = new T345DeclaredAzureLiveContext(SqlServerContainer.ConnectionString);
        var tap = new CommandTap();
        context.AddInterceptor(tap);

        // Configuration-level acceptance only: preparation resolves the text converter and must not execute.
        var prepared = context.GetPreparedQueryCommand(
            context.From<IT345NativeTextStorageRow>().Where(x => x.Id == 1).Select(x => x.Data),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);
        prepared.Should().NotBeNull();
        tap.Executions.Should().BeEmpty("preparation alone executes nothing");

        Execute(context, $"drop table if exists {TextTable}");
        Execute(context, $"create table {TextTable} (id bigint not null primary key, data nvarchar(max) null)");

        const string document = "{\"name\":\"alpha\",\"inner\":{\"k\":1,\"flag\":true}}";
        context.CreateInsertBuilder<IT345NativeTextStorageRow>()
            .Values(new T345NativeTextStorageRow { Id = 1, Data = Parse(document) })
            .Insert();

        var insert = tap.Executions[^1];
        insert.Sql.Should().StartWith("insert", "the mapping writes through the ordinary text transport");
        insert.Parameters.Should().NotContain(
            parameter => parameter.SqlDbType == "Json",
            "a declared native mapping must not retag the parameter as the native json type");
        insert.Parameters.Should().NotContain(
            parameter => parameter.ClrType == typeof(JsonElement).FullName,
            "the JSON column is transported as a string, never as a DOM parameter");

        var rows = context.From<IT345NativeTextStorageRow>().ToList();
        rows.Should().HaveCount(1);
        JsonNode.DeepEquals(JsonNode.Parse(rows[0].Data!.Value.GetRawText()), JsonNode.Parse(document))
            .Should().BeTrue("the text transport round-trips the document unchanged");

        var columnTypes = ReadColumnTypes(context, TextTable);
        columnTypes["data"].Should().Be("nvarchar", "declaring the capability is not a schema migration");

        Report(
            $"{_tag}-TEXT-ROUNDTRIP actual_major={_major} actual_engine_edition={_engineEdition} "
            + "declared=[version: 12.0, engine edition: SqlDatabase (5)] storage=Native-over-nvarchar "
            + $"sql={insert.Sql.ReplaceLineEndings(" ")} "
            + $"params=[{string.Join("; ", insert.Parameters.Select(Describe))}] "
            + $"physical_column={columnTypes["data"]} rows={rows.Count} executed_commands={tap.Executions.Count}");
    }

    [Fact]
    public void OnMajorSeventeen_JsonColumn_ShouldRoundTripThroughTheDeclaredAzureNativeMapping()
    {
        if (_major < 17)
        {
            Report(
                $"{_tag}-JSON-ROUNDTRIP-NOT-APPLICABLE actual_major={_major} actual_engine_edition={_engineEdition} "
                + "declared=[version: 12.0, engine edition: SqlDatabase (5)] "
                + $"reason=no_native_json_type_below_major_17 needs_major=17 outcome=not_applicable");
            return;
        }

        using var context = new T345DeclaredAzureLiveContext(SqlServerContainer.ConnectionString);
        var tap = new CommandTap();
        context.AddInterceptor(tap);

        Execute(context, $"drop table if exists {NativeTable}");
        Execute(context, $"create table {NativeTable} (id bigint not null primary key, data json null)");

        const string document = "{\"name\":\"alpha\",\"inner\":{\"k\":1,\"flag\":true}}";
        context.CreateInsertBuilder<IT345NativeJsonStorageRow>()
            .Values(new T345NativeJsonStorageRow { Id = 1, Data = Parse(document) })
            .Insert();

        var insert = tap.Executions[^1];
        var rows = context.From<IT345NativeJsonStorageRow>().ToList();
        rows.Should().HaveCount(1);
        JsonNode.DeepEquals(JsonNode.Parse(rows[0].Data!.Value.GetRawText()), JsonNode.Parse(document))
            .Should().BeTrue("the declared native mapping round-trips the document on a server that has the type");

        var columnTypes = ReadColumnTypes(context, NativeTable);
        columnTypes["data"].Should().Be("json");

        Report(
            $"{_tag}-JSON-ROUNDTRIP actual_major={_major} actual_engine_edition={_engineEdition} "
            + "declared=[version: 12.0, engine edition: SqlDatabase (5)] "
            + $"sql={insert.Sql.ReplaceLineEndings(" ")} "
            + $"params=[{string.Join("; ", insert.Parameters.Select(Describe))}] "
            + $"physical_column={columnTypes["data"]} rows={rows.Count} executed_commands={tap.Executions.Count}");
    }

    [Fact]
    public void OnMajorSixteen_TheServer_ShouldRejectAJsonColumn()
    {
        if (_major >= 17)
        {
            Report(
                $"{_tag}-JSON-REJECTED-NOT-APPLICABLE actual_major={_major} actual_engine_edition={_engineEdition} "
                + "declared=[version: 12.0, engine edition: SqlDatabase (5)] "
                + $"reason=native_json_type_present_from_major_17 needs_major=16 outcome=not_applicable");
            return;
        }

        using var context = new T345DeclaredAzureLiveContext(SqlServerContainer.ConnectionString);
        Execute(context, $"drop table if exists {NativeTable}");

        var act = () => Execute(
            context,
            $"create table {NativeTable} (id bigint not null primary key, data json null)");
        var error = act.Should().Throw<SqlException>().Which;

        Report(
            $"{_tag}-JSON-REJECTED actual_major={_major} actual_engine_edition={_engineEdition} "
            + "declared=[version: 12.0, engine edition: SqlDatabase (5)] "
            + $"server_error_number={error.Number} outcome=server_rejects_json_column");

        // The declaration cannot create a type the server lacks: the same mapping still writes nvarchar only.
        var tap = new CommandTap();
        context.AddInterceptor(tap);
        Execute(context, $"create table {TextTable} (id bigint not null primary key, data nvarchar(max) null)");
        context.CreateInsertBuilder<IT345NativeTextStorageRow>()
            .Values(new T345NativeTextStorageRow { Id = 1, Data = Parse("{\"name\":\"beta\"}") })
            .Insert();

        var insert = tap.Executions[^1];
        insert.Parameters.Should().NotContain(parameter => parameter.SqlDbType == "Json");

        var columnTypes = ReadColumnTypes(context, TextTable);
        columnTypes["data"].Should().Be("nvarchar", "the declaration cannot manufacture the missing type");

        Report(
            $"{_tag}-TEXT-ONLY actual_major={_major} actual_engine_edition={_engineEdition} "
            + "declared=[version: 12.0, engine edition: SqlDatabase (5)] "
            + $"sql={insert.Sql.ReplaceLineEndings(" ")} "
            + $"params=[{string.Join("; ", insert.Parameters.Select(Describe))}] "
            + $"physical_column={columnTypes["data"]} executed_commands={tap.Executions.Count}");
    }

    private static (int Major, int EngineEdition) ReadServerFacts(IDataContext context)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(
            "select cast(serverproperty('ProductMajorVersion') as int), cast(serverproperty('EngineEdition') as int)");
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue("serverproperty always returns one row");

        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static Dictionary<string, string> ReadColumnTypes(IDataContext context, string table)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(
            "select column_name, data_type from information_schema.columns "
            + $"where table_name = '{table}' order by ordinal_position");
        using var reader = command.ExecuteReader();

        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            types[reader.GetString(0)] = reader.GetString(1);

        return types;
    }

    private static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Describe(CapturedParameter parameter)
        => $"{parameter.Name}:clr={parameter.ClrType}:dbtype={parameter.DbType}:sqltype={parameter.SqlDbType}:size={parameter.Size}:value={parameter.Payload}";

    private static void Report(string message) => Console.WriteLine(message);

    private sealed record CapturedParameter(
        string Name,
        string ClrType,
        string DbType,
        string SqlDbType,
        int Size,
        string Payload);

    private sealed record Captured(string Sql, IReadOnlyList<CapturedParameter> Parameters);

    /// <summary>Records every executed command with its ordered bound parameters.</summary>
    private sealed class CommandTap : IQueryInterceptor
    {
        public List<Captured> Executions { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            var parameters = command.Parameters
                .Cast<DbParameter>()
                .Select(static parameter => new CapturedParameter(
                    parameter.ParameterName,
                    parameter.Value is DBNull or null ? "DBNull" : parameter.Value.GetType().FullName ?? "null",
                    parameter.DbType.ToString(),
                    (parameter as SqlParameter)?.SqlDbType.ToString() ?? "n/a",
                    parameter.Size,
                    parameter.Value switch
                    {
                        JsonElement element => element.GetRawText(),
                        DBNull => "NULL",
                        null => "NULL",
                        _ => Convert.ToString(parameter.Value, CultureInfo.InvariantCulture) ?? "NULL",
                    }))
                .ToList();

            Executions.Add(new Captured(command.CommandText, parameters));
        }
    }
}
