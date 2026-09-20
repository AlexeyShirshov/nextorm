using System.Net.Http;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// Provisions the PostgreSQL Pro demo database ("Авиаперевозки", schema <c>bookings</c>):
/// either an already running server (the <c>AVIASALES_CONNECTION</c> environment variable or
/// <c>--connection</c>) or a throwaway Testcontainers instance seeded from the official dump
/// (downloaded once and cached on disk).
/// </summary>
public sealed class DemoDatabase : IAsyncDisposable
{
    public const string ConnectionVariable = "AVIASALES_CONNECTION";
    public const string DumpUrlVariable = "AVIASALES_DUMP_URL";

    private const string FallbackConnectionVariable = "NEXTORM_DEMODB_POSTGRES_CONNECTION";
    private const string DefaultDumpUrl = "https://edu.postgrespro.ru/demo-20250901-3m.sql.gz";
    private const string DefaultImage = "postgres:17-alpine";
    private const string DumpFile = "demo-20250901-3m.sql.gz";

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

        // The official dump starts with a plain "DROP DATABASE demo;" and is meant to be replayed
        // with psql's default ON_ERROR_STOP off, so it cannot be used as a /docker-entrypoint-initdb.d
        // script (the entrypoint runs scripts with ON_ERROR_STOP on and would abort).
        var container = new PostgreSqlBuilder(DefaultImage)
            .WithResourceMapping(dump, "/tmp/")
            .Build();

        Console.WriteLine($"[aviasales] starting {DefaultImage} ...");
        await container.StartAsync(ct).ConfigureAwait(false);

        Console.WriteLine($"[aviasales] loading {Path.GetFileName(dump)} (this takes several minutes) ...");
        var result = await container.ExecAsync(
            new[] { "sh", "-c", $"gunzip -c /tmp/{DumpFile} | psql -U postgres -q" }, ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Loading the demo dump failed (exit {result.ExitCode}).{Environment.NewLine}{result.Stderr}");

        var connectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = "demo",
            // The demo queries aggregate over the full demo dataset; the default 30s is too short.
            CommandTimeout = 600
        }.ConnectionString;

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
