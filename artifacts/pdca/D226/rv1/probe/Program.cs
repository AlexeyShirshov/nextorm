// D226 / rv1 — D1 real-driver probe.
//
// Measures, on real ADO.NET drivers, the DbDataReader.RecordsAffected value:
//   * before the reader is released (the pre-close snapshot),
//   * after Close / Dispose / DisposeAsync (the post-release value, or the exact exception type),
// for DML with a known total, on the sync path, the async path, the early-Dispose path and a
// partially-read reader, plus the two multi-statement batch forms (joined command, native DbBatch).
//
// The probe deliberately does NOT reference nextorm: it measures the raw driver contract that the
// chosen algorithm ("snapshot pre-close, release the reader, prefer the post-release value else the
// pre-close value else -1") has to reproduce without a provider-specific workaround.
//
// Output: one JSON document on stdout. Exit code 1 when a known-total DML probe on
// sqlite/postgres/sqlserver/mysql fails to reproduce the expected total; 0 otherwise.

using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using ClickHouse.Driver.ADO;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Testcontainers.ClickHouse;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;

const int KnownTotal = 3;

// Optional argv[0]: write the JSON document to this path so the container-runtime transcript on
// stdout cannot pollute the machine-readable evidence.
var outputPath = args.Length > 0 ? args[0] : null;

var results = new List<ProbeResult>();
var notes = new List<ProbeNote>();
var providers = (Environment.GetEnvironmentVariable("NEXTORM_PROBE_PROVIDERS")
        ?? "sqlite,postgres,sqlserver,mysql,clickhouse")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

foreach (var provider in providers)
{
    try
    {
        await RunProviderAsync(provider, results, notes);
    }
    catch (Exception exception)
    {
        notes.Add(new ProbeNote(provider, "provider-failed", exception.GetType().Name + ": " + exception.Message));
    }
}

var oracleProviders = new[] { "sqlite", "postgres", "sqlserver", "mysql" };
var failed = results
    .Where(r => r.IsDml && oracleProviders.Contains(r.Provider) && !r.ChosenMatchesExpected)
    .ToList();

var json = JsonSerializer.Serialize(
    new
    {
        probe = "D226-D1-records-affected-metadata",
        known_total = KnownTotal,
        generated_at_utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        providers,
        notes,
        results,
    },
    new JsonSerializerOptions { WriteIndented = true });

if (outputPath is not null)
{
    await File.WriteAllTextAsync(outputPath, json);
}

Console.WriteLine(json);

return failed.Count == 0 ? 0 : 1;

async Task RunProviderAsync(string provider, List<ProbeResult> results, List<ProbeNote> notes)
{
    switch (provider)
    {
        case "sqlite":
        {
            var path = Path.Combine(Path.GetTempPath(), $"d226-probe-{Guid.NewGuid():N}.db");
            try
            {
                await using var connection = new SqliteConnection($"Data Source={path}");
                await connection.OpenAsync();
                await ProbeRelationalAsync(provider, connection, notes, results);
            }
            finally
            {
                File.Delete(path);
            }

            break;
        }

        case "postgres":
        {
            var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await container.StartAsync();
            try
            {
                await using var connection = new NpgsqlConnection(container.GetConnectionString());
                await connection.OpenAsync();
                await ProbeRelationalAsync(provider, connection, notes, results);
            }
            finally
            {
                await container.DisposeAsync();
            }

            break;
        }

        case "sqlserver":
        {
            var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
            await container.StartAsync();
            try
            {
                await using var connection = new SqlConnection(container.GetConnectionString());
                await connection.OpenAsync();
                await ProbeRelationalAsync(provider, connection, notes, results);
            }
            finally
            {
                await container.DisposeAsync();
            }

            break;
        }

        case "mysql":
        {
            var container = new MySqlBuilder("mysql:8.4").Build();
            await container.StartAsync();
            try
            {
                await using var connection = new MySqlConnection(container.GetConnectionString());
                await connection.OpenAsync();
                await ProbeRelationalAsync(provider, connection, notes, results);
            }
            finally
            {
                await container.DisposeAsync();
            }

            break;
        }

        case "clickhouse":
        {
            var container = new ClickHouseBuilder("clickhouse/clickhouse-server:25.8-alpine").Build();
            await container.StartAsync();
            try
            {
                await using var connection = new ClickHouseConnection(container.GetConnectionString());
                await connection.OpenAsync();
                await ProbeClickHouseAsync(connection, notes, results);
            }
            finally
            {
                await container.DisposeAsync();
            }

            break;
        }

        default:
            notes.Add(new ProbeNote(provider, "unknown-provider", "no probe defined"));
            break;
    }
}

async Task ProbeRelationalAsync(string provider, DbConnection connection, List<ProbeNote> notes, List<ProbeResult> results)
{
    // Bare DML with a known total (no result set at all).
    await ProbeDmlAsync(provider, connection, "dml-sync-dispose-no-read", Release.SyncDispose, readRows: false, results);
    await ProbeDmlAsync(provider, connection, "dml-sync-close-no-read", Release.SyncClose, readRows: false, results);
    await ProbeDmlAsync(provider, connection, "dml-async-dispose-no-read", Release.AsyncDispose, readRows: false, results);

    // The shape every nextorm batch ends with: a DML statement then a result-bearing SELECT, read to
    // the end and released (full read) and released early without advancing (partial read).
    await ProbeDmlThenSelectAsync(provider, connection, "dml-then-select-sync-dispose", Release.SyncDispose, readRows: true, results);
    await ProbeDmlThenSelectAsync(provider, connection, "dml-then-select-async-dispose", Release.AsyncDispose, readRows: true, results);
    await ProbeDmlThenSelectAsync(provider, connection, "dml-then-select-partial-sync-dispose", Release.SyncDispose, readRows: false, results);

    // Inapplicable metadata: a pure SELECT keeps the sentinel.
    await ProbeSelectAsync(provider, connection, "select-sync-dispose", results);

    if (connection.CanCreateBatch)
    {
        await ProbeNativeBatchAsync(provider, connection, results);
    }
    else
    {
        notes.Add(new ProbeNote(provider, "native-batch", "CanCreateBatch=false; only the joined-command path exists"));
    }
}

async Task ProbeDmlAsync(string provider, DbConnection connection, string scenario, Release release, bool readRows, List<ProbeResult> results)
{
    var table = UniqueTable();
    await SetupAsync(connection, table);
    var sql = $"update {table} set id = id + 100 where id <= {KnownTotal}";
    await MeasureAsync(provider, connection, scenario, "dml", sql, readRows, release, KnownTotal, isDml: true, results);
    await DropQuietAsync(connection, table);
}

async Task ProbeDmlThenSelectAsync(string provider, DbConnection connection, string scenario, Release release, bool readRows, List<ProbeResult> results)
{
    var table = UniqueTable();
    await SetupAsync(connection, table);
    var sql = $"update {table} set id = id + 100 where id <= {KnownTotal}; select count(*) as c from {table}";
    await MeasureAsync(provider, connection, scenario, "dml+select", sql, readRows, release, KnownTotal, isDml: true, results);
    await DropQuietAsync(connection, table);
}

async Task ProbeSelectAsync(string provider, DbConnection connection, string scenario, List<ProbeResult> results)
{
    var table = UniqueTable();
    await SetupAsync(connection, table);
    await MeasureAsync(provider, connection, scenario, "select", $"select id from {table}", readRows: true, Release.SyncDispose, -1, isDml: false, results);
    await DropQuietAsync(connection, table);
}

async Task ProbeNativeBatchAsync(string provider, DbConnection connection, List<ProbeResult> results)
{
    var table = UniqueTable();
    await SetupAsync(connection, table);

    await using var batch = connection.CreateBatch();
    var update = batch.CreateBatchCommand();
    update.CommandText = $"update {table} set id = id + 100 where id <= {KnownTotal}";
    batch.BatchCommands.Add(update);
    var select = batch.CreateBatchCommand();
    select.CommandText = $"select count(*) as c from {table}";
    batch.BatchCommands.Add(select);

    await using var reader = await batch.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
    }

    var before = SafeRead(reader);
    await reader.DisposeAsync();
    var after = SafeRead(reader);

    results.Add(new ProbeResult(
        provider,
        "native-batch-async-dispose",
        "batch",
        "update; select",
        KnownTotal,
        before.Value,
        before.Error,
        after.Value,
        after.Error,
        Chosen(before, after),
        true,
        true,
        true));

    await DropQuietAsync(connection, table);
}

async Task MeasureAsync(
    string provider,
    DbConnection connection,
    string scenario,
    string shape,
    string sql,
    bool readRows,
    Release release,
    int expected,
    bool isDml,
    List<ProbeResult> results)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;

    var asyncRelease = release == Release.AsyncDispose;
    await using var reader = asyncRelease
        ? await command.ExecuteReaderAsync()
        : command.ExecuteReader();

    if (readRows)
    {
        while (asyncRelease ? await reader.ReadAsync() : reader.Read())
        {
        }
    }

    var before = SafeRead(reader);

    switch (release)
    {
        case Release.SyncClose:
            reader.Close();
            break;
        case Release.AsyncDispose:
            await reader.DisposeAsync();
            break;
        default:
            reader.Dispose();
            break;
    }

    var after = SafeRead(reader);

    results.Add(new ProbeResult(
        provider,
        scenario,
        shape,
        sql,
        expected,
        before.Value,
        before.Error,
        after.Value,
        after.Error,
        Chosen(before, after),
        isDml,
        readRows,
        readRows));
}

async Task ProbeClickHouseAsync(DbConnection connection, List<ProbeNote> notes, List<ProbeResult> results)
{
    var table = "probe_" + Guid.NewGuid().ToString("N")[..8];
    try
    {
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"create table {table} (id Int32) engine = Memory";
            await create.ExecuteNonQueryAsync();
        }

        for (var i = 1; i <= 5; i++)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = $"insert into {table} (id) values ({i})";
            await insert.ExecuteNonQueryAsync();
        }

        // ClickHouse has no per-statement affected-row oracle: record the INSERT and SELECT metadata
        // as observations. A lightweight DELETE is attempted and its exact refusal is recorded.
        await ObserveClickHouseAsync(
            connection,
            notes,
            results,
            "insert-async-dispose",
            "dml",
            $"insert into {table} (id) values (99)",
            0,
            isDml: true);

        await ObserveClickHouseAsync(
            connection,
            notes,
            results,
            "lightweight-delete-async-dispose",
            "dml",
            $"delete from {table} where id = 1",
            0,
            isDml: true);

        await ObserveClickHouseAsync(
            connection,
            notes,
            results,
            "select-async-dispose",
            "select",
            $"select count(*) as c from {table}",
            -1,
            isDml: false);
    }
    finally
    {
        notes.Add(new ProbeNote("clickhouse", "oracle", "ClickHouse has no DML affected-row oracle; values are recorded as observations only"));
        await DropQuietAsync(connection, table);
    }
}

async Task ObserveClickHouseAsync(
    DbConnection connection,
    List<ProbeNote> notes,
    List<ProbeResult> results,
    string scenario,
    string shape,
    string sql,
    int expected,
    bool isDml)
{
    try
    {
        await MeasureAsync("clickhouse", connection, scenario, shape, sql, readRows: true, Release.AsyncDispose, expected, isDml, results);
    }
    catch (Exception exception)
    {
        notes.Add(new ProbeNote("clickhouse", scenario, exception.GetType().Name + ": " + exception.Message));
    }
}

async Task SetupAsync(DbConnection connection, string table)
{
    await using var command = connection.CreateCommand();
    command.CommandText =
        $"create table {table} (id int); " +
        $"insert into {table} (id) values (1),(2),(3),(4),(5)";
    await command.ExecuteNonQueryAsync();
}

async Task DropQuietAsync(DbConnection connection, string table)
{
    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"drop table {table}";
        await command.ExecuteNonQueryAsync();
    }
    catch
    {
        // Best effort: the container or database file is discarded with the probe.
    }
}

string UniqueTable() => "probe_" + Guid.NewGuid().ToString("N")[..8];

(int Value, string? Error) SafeRead(DbDataReader reader)
{
    try
    {
        return (reader.RecordsAffected, null);
    }
    catch (Exception exception)
    {
        return (-1, exception.GetType().Name);
    }
}

int Chosen((int Value, string? Error) before, (int Value, string? Error) after)
    => after.Value != -1 ? after.Value : before.Value;

internal enum Release
{
    SyncClose,
    SyncDispose,
    AsyncDispose,
}

internal sealed record ProbeResult(
    string Provider,
    string Scenario,
    string Shape,
    string Sql,
    int Expected,
    int Before,
    string? BeforeError,
    int After,
    string? AfterError,
    int Chosen,
    bool IsDml,
    bool ReadRows,
    bool FullRead)
{
    public bool ChosenMatchesExpected => Chosen == Expected;
}

internal sealed record ProbeNote(string Provider, string Topic, string Note);
