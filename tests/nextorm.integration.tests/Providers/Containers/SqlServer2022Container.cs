using Testcontainers.MsSql;

namespace NextORM.Integration.Tests;

/// <summary>
/// Owns a SQL Server 2022 (major 16) instance for the T348 U1 live arm. It mirrors
/// <see cref="SqlServerContainer"/> — same lazy, reused, best-effort-disposal lifecycle — but with a
/// distinct image, reuse label and connection-string override variable, so it is never the same
/// container as the 2025 one. The 2022 arm runs as a separate serial invocation, so only one SQL
/// Server container is ever alive at a time.
/// </summary>
internal static class SqlServer2022Container
{
    public const string ConnectionStringVariable = "NEXTORM_SQLSERVER2022_CONNECTION";

    private const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private const string ReuseLabel = "reuse-id";

    private static readonly object Gate = new();

    private static volatile bool _resolved;
    private static MsSqlContainer? _container;
    private static string? _connectionString;
    private static string? _failure;

    /// <summary>True when the suite was pointed at an existing 2022 server through the environment.</summary>
    public static bool IsExternallyConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));

    /// <summary>True when either the external server or a Testcontainers instance is usable.</summary>
    public static bool IsAvailable
    {
        get
        {
            Resolve();
            return _connectionString is not null;
        }
    }

    /// <summary>The reason SQL Server 2022 is unavailable, or <c>null</c> when it is available.</summary>
    public static string? Failure
    {
        get
        {
            Resolve();
            return _failure;
        }
    }

    public static string ConnectionString
    {
        get
        {
            Resolve();
            return _connectionString
                ?? throw new InvalidOperationException(_failure ?? "SQL Server 2022 is not available.");
        }
    }

    private static void Resolve()
    {
        if (_resolved)
            return;

        lock (Gate)
        {
            if (_resolved)
                return;

            try
            {
                var external = Environment.GetEnvironmentVariable(ConnectionStringVariable);
                if (!string.IsNullOrWhiteSpace(external))
                {
                    _connectionString = external;
                    return;
                }

                var container = new MsSqlBuilder(DefaultImage)
                    .WithReuse(true)
                    .WithLabel(ReuseLabel, "nextorm-sqlserver-2022")
                    .Build();

                container.StartAsync().GetAwaiter().GetResult();

                _container = container;
                _connectionString = container.GetConnectionString();
            }
            catch (Exception exception)
            {
                _failure =
                    $"Could not start a SQL Server 2022 test container ({exception.Message}). " +
                    $"Set {ConnectionStringVariable} to use an already running server.";
            }
            finally
            {
                _resolved = true;
            }
        }
    }

    /// <summary>
    /// Stops the container at the end of the 2022 invocation. With reuse enabled the container is kept
    /// on disk and started again on the next run; disposal happens from the test class rather than the
    /// process-exit handler for the reason documented on <see cref="SqlServerContainer"/>.
    /// </summary>
    internal static async ValueTask DisposeContainerAsync()
    {
        var container = Interlocked.Exchange(ref _container, null);
        if (container is null)
            return;

        try
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort; Testcontainers' resource reaper remains the primary cleanup mechanism.
        }
    }
}
