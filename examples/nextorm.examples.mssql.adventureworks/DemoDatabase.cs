using System.Net.Http;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// Provisions the AdventureWorks 2022 database: either an already running server (the
/// <c>ADVENTUREWORKS_CONNECTION</c> environment variable or <c>--connection</c>) or a throwaway
/// Testcontainers instance restored from the official <c>.bak</c> (downloaded once and cached).
/// </summary>
public sealed class DemoDatabase : IAsyncDisposable
{
    public const string ConnectionVariable = "ADVENTUREWORKS_CONNECTION";
    public const string BackupUrlVariable = "ADVENTUREWORKS_BAK_URL";

    private const string FallbackConnectionVariable = "NEXTORM_DEMODB_MSSQL_CONNECTION";
    private const string DefaultBackupUrl =
        "https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks/AdventureWorks2022.bak";
    private const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string BackupFile = "AdventureWorks2022.bak";
    private const string DatabaseName = "AdventureWorks";
    private const string SqlCmd = "/opt/mssql-tools18/bin/sqlcmd";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "nextorm", "adventureworks");

    private readonly MsSqlContainer? _container;

    private DemoDatabase(string connectionString, MsSqlContainer? container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    public string ConnectionString { get; }

    public static async Task<DemoDatabase> StartAsync(string? explicitConnectionString, CancellationToken ct)
    {
        var external = explicitConnectionString
            ?? Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? Environment.GetEnvironmentVariable(FallbackConnectionVariable);

        if (!string.IsNullOrWhiteSpace(external))
        {
            Console.WriteLine("[adventureworks] using the provided SQL Server connection string");
            return new DemoDatabase(external, null);
        }

        var backup = await EnsureBackupAsync(ct).ConfigureAwait(false);
        var container = new MsSqlBuilder(DefaultImage).Build();

        Console.WriteLine($"[adventureworks] starting {DefaultImage} ...");
        await container.StartAsync(ct).ConfigureAwait(false);

        var password = new SqlConnectionStringBuilder(container.GetConnectionString()).Password;
        await container.CopyAsync(backup, "/tmp/", ct: ct).ConfigureAwait(false);
        await RestoreAsync(container, password, ct).ConfigureAwait(false);

        var connectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;

        await WaitForReadyAsync(connectionString, ct).ConfigureAwait(false);
        return new DemoDatabase(connectionString, container);
    }

    private static async Task<string> EnsureBackupAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(CacheDirectory);
        var target = Path.Combine(CacheDirectory, BackupFile);
        if (File.Exists(target) && new FileInfo(target).Length > 0)
            return target;

        var url = Environment.GetEnvironmentVariable(BackupUrlVariable) ?? DefaultBackupUrl;
        Console.WriteLine($"[adventureworks] downloading {url} -> {target} (large, cached afterwards) ...");

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

    private static async Task RestoreAsync(MsSqlContainer container, string password, CancellationToken ct)
    {
        Console.WriteLine("[adventureworks] restoring the database (this takes several minutes) ...");

        var query =
            $"RESTORE DATABASE {DatabaseName} FROM DISK = '/tmp/{BackupFile}' " +
            $"WITH MOVE 'AdventureWorks2022' TO '/var/opt/mssql/data/AdventureWorks.mdf', " +
            $"MOVE 'AdventureWorks2022_log' TO '/var/opt/mssql/data/AdventureWorks_log.ldf', STATS = 5";

        var result = await container.ExecAsync(
            new[] { SqlCmd, "-S", "localhost", "-U", "sa", "-P", password, "-C", "-b", "-Q", query }, ct)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"AdventureWorks restore failed (exit {result.ExitCode}).{Environment.NewLine}{result.Stdout}{result.Stderr}");
    }

    private static async Task WaitForReadyAsync(string connectionString, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = "select count(*) from Sales.SalesOrderHeader";
                var count = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
                if (count > 0)
                {
                    Console.WriteLine($"[adventureworks] database ready: Sales.SalesOrderHeader has {count} row(s)");
                    return;
                }
            }
            catch (Exception exception) when (exception is SqlException or IOException)
            {
                // The restore is still settling; retry below.
            }

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("AdventureWorks did not become ready within 10 minutes.");

            await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
