using Testcontainers.MsSql;

namespace nextorm.integration.tests;

/// <summary>
/// Owns the SQL Server instance the integration suite runs against. A connection string provided
/// through <see cref="ConnectionStringVariable"/> always wins (pointing the suite at an already
/// running server); otherwise a throwaway <see cref="MsSqlContainer"/> is started once per test
/// process. The container runtime is discovered the standard Testcontainers way (the
/// <c>DOCKER_HOST</c> environment variable or the default Docker socket). Resolution is lazy and
/// cached, so a container is only started when a SQL Server test actually runs.
/// </summary>
internal static class SqlServerContainer
{
    public const string ConnectionStringVariable = "NEXTORM_SQLSERVER_CONNECTION";

    private const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private static readonly object Gate = new();

    // Volatile and only ever set to true once resolution has completed, so the lock-free fast
    // path can never observe a half-initialized state (tests run on many threads in parallel).
    private static volatile bool _resolved;
    private static MsSqlContainer? _container;
    private static string? _connectionString;
    private static string? _failure;

    /// <summary>True when the suite was pointed at an existing server through the environment.</summary>
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

    /// <summary>The reason SQL Server is unavailable, or <c>null</c> when it is available.</summary>
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
                ?? throw new InvalidOperationException(_failure ?? "SQL Server is not available.");
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
                    .Build();

                container.StartAsync().GetAwaiter().GetResult();

                _container = container;
                _connectionString = container.GetConnectionString();
            }
            catch (Exception exception)
            {
                _failure =
                    $"Could not start a SQL Server test container ({exception.Message}). " +
                    $"Set {ConnectionStringVariable} to use an already running server.";
            }
            finally
            {
                // Publish only once the state above is fully initialized; readers use this as
                // their fast path, so it must not be set before _connectionString/_failure are.
                _resolved = true;
            }
        }
    }

    /// <summary>
    /// Releases the container at the end of the test run. Disposal happens through
    /// <see cref="DatabaseContainers"/> rather than a ProcessExit handler: the container modules
    /// leave background threads behind, and blocking on them while the process exits makes the
    /// test host report leftover foreground threads (a non-zero exit code).
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
