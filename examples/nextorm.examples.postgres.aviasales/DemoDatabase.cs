using System.Net.Http;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// Provisions the PostgreSQL Pro demo database ("Авиаперевозки", schema <c>bookings</c>):
/// either an already running server (the <c>AVIASALES_CONNECTION</c> environment variable or
/// <c>--connection</c>) or a Testcontainers instance whose data directory is a named volume seeded
/// from the official dump (downloaded once and cached on disk); later runs reuse the loaded volume.
/// </summary>
public sealed class DemoDatabase : IAsyncDisposable
{
    public const string ConnectionVariable = "AVIASALES_CONNECTION";
    public const string DumpUrlVariable = "AVIASALES_DUMP_URL";

    private const string FallbackConnectionVariable = "NEXTORM_DEMODB_POSTGRES_CONNECTION";
    private const string DefaultDumpUrl = "https://edu.postgrespro.ru/demo-20250901-3m.sql.gz";
    private const string DefaultImage = "postgres:17-alpine";
    private const string DumpFile = "demo-20250901-3m.sql.gz";
    private const string VolumeName = "nextorm-examples-aviasales-data";
    private const string DataDirectory = "/var/lib/postgresql/data";
    private const string ReloadVariable = "NEXTORM_EXAMPLES_RELOAD";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "nextorm", "aviasales");

    private readonly PostgreSqlContainer? _container;

    private DemoDatabase(string connectionString, PostgreSqlContainer? container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    public string ConnectionString { get; }

    public static async Task<DemoDatabase> Start(string? explicitConnectionString, CancellationToken ct)
    {
        var external = explicitConnectionString
            ?? Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? Environment.GetEnvironmentVariable(FallbackConnectionVariable);

        if (!string.IsNullOrWhiteSpace(external))
        {
            Console.WriteLine("[aviasales] using the provided PostgreSQL connection string");
            return new DemoDatabase(external, null);
        }

        var dump = await EnsureDump(ct).ConfigureAwait(false);

        if (IsReloadRequested())
            await DeleteVolume(ct).ConfigureAwait(false);

        // The official dump starts with a plain "DROP DATABASE demo;" and is meant to be replayed
        // with psql's default ON_ERROR_STOP off, so it cannot be used as a /docker-entrypoint-initdb.d
        // script (the entrypoint runs scripts with ON_ERROR_STOP on and would abort).
        // The cluster lives in a named volume: the examples are read-only, so the loaded database is
        // reused across runs and the multi-minute load only happens on a cold volume.
        var container = new PostgreSqlBuilder(DefaultImage)
            .WithVolumeMount(VolumeName, DataDirectory)
            .Build();

        Console.WriteLine($"[aviasales] starting {DefaultImage} ...");
        await container.StartAsync(ct).ConfigureAwait(false);

        var connectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = "demo",
            // The demo queries aggregate over the full demo dataset; the default 30s is too short.
            CommandTimeout = 600
        }.ConnectionString;

        if (await IsLoaded(connectionString, ct).ConfigureAwait(false))
        {
            Console.WriteLine($"[aviasales] reusing the loaded database from volume '{VolumeName}'");
        }
        else
        {
            Console.WriteLine($"[aviasales] loading {Path.GetFileName(dump)} (first run, this takes several minutes) ...");
            await container.CopyAsync(dump, "/tmp/", ct: ct).ConfigureAwait(false);
            var result = await container.ExecAsync(
                new[] { "sh", "-c", $"gunzip -c /tmp/{DumpFile} | psql -U postgres -q" }, ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Loading the demo dump failed (exit {result.ExitCode}).{Environment.NewLine}{result.Stderr}");
        }

        await WaitForDemo(connectionString, ct).ConfigureAwait(false);
        return new DemoDatabase(connectionString, container);
    }

    private static async Task<string> EnsureDump(CancellationToken ct)
    {
        Directory.CreateDirectory(CacheDirectory);
        var target = Path.Combine(CacheDirectory, DumpFile);
        if (File.Exists(target) && new FileInfo(target).Length > 0)
            return target;

        var url = Environment.GetEnvironmentVariable(DumpUrlVariable) ?? DefaultDumpUrl;
        Console.WriteLine($"[aviasales] downloading {url} -> {target} (large, cached afterwards) ...");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var temporary = target + ".part";
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var destination = File.Create(temporary))
        {
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);
        }

        File.Move(temporary, target, overwrite: true);
        return target;
    }

    private static bool IsReloadRequested() =>
        bool.TryParse(Environment.GetEnvironmentVariable(ReloadVariable), out var reload) && reload;

    private static async Task DeleteVolume(CancellationToken ct)
    {
        var volume = new VolumeBuilder().WithName(VolumeName).Build();
        await volume.CreateAsync(ct).ConfigureAwait(false);
        await volume.DeleteAsync(ct).ConfigureAwait(false);
    }

    private static async Task<bool> IsLoaded(string connectionString, CancellationToken ct)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "select count(*) from bookings.flights";
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
        }
        catch (Exception exception) when (exception is NpgsqlException or IOException)
        {
            return false;
        }
    }

    private static async Task WaitForDemo(string connectionString, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(30);
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = "select count(*) from bookings.flights";
                var count = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
                if (count > 0)
                {
                    Console.WriteLine($"[aviasales] demo database ready: bookings.flights has {count} row(s)");
                    return;
                }
            }
            catch (Exception exception) when (exception is NpgsqlException or IOException)
            {
                // The init script is still running; retry below.
            }

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The Авиаперевозки demo dump did not finish loading within 30 minutes.");

            await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
