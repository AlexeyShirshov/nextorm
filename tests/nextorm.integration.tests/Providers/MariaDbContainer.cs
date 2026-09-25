using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace NextORM.Integration.Tests;

/// <summary>
/// Owns the MariaDB instance the MySQL/MariaDB-specific integration tests run against. A connection
/// string provided through <see cref="ConnectionStringVariable"/> always wins (pointing the suite at an
/// already running server); otherwise a reusable Testcontainers instance is started once per test
/// process and kept between runs.
/// <para>
/// A raw <see cref="ContainerBuilder"/> is used instead of the Testcontainers MySQL module: MariaDB
/// ships <c>mariadb-admin</c> rather than <c>mysqladmin</c>, so the module's <c>mysqladmin ping</c>
/// wait strategy never succeeds; the official <c>healthcheck.sh</c> is used instead.
/// </para>
/// </summary>
internal static class MariaDbContainer
{
    public const string ConnectionStringVariable = "NEXTORM_MARIADB_CONNECTION";

    private const string DefaultImage = "mariadb:11.4";

    private const string ReuseLabel = "reuse-id";

    private const int ContainerPort = 3306;

    private static readonly object Gate = new();

    private static volatile bool _resolved;
    private static IContainer? _container;
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

    /// <summary>The reason MariaDB is unavailable, or <c>null</c> when it is available.</summary>
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
                ?? throw new InvalidOperationException(_failure ?? "MariaDB is not available.");
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

                var container = new ContainerBuilder(DefaultImage)
                    .WithEnvironment("MARIADB_DATABASE", "nextorm")
                    .WithEnvironment("MARIADB_USER", "nextorm")
                    .WithEnvironment("MARIADB_PASSWORD", "nextorm")
                    .WithEnvironment("MARIADB_ROOT_PASSWORD", "nextorm")
                    .WithPortBinding(ContainerPort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("healthcheck.sh", "--connect", "--innodb_initialized"))
                    .WithReuse(true)
                    .WithLabel(ReuseLabel, "nextorm-mariadb")
                    .Build();

                container.StartAsync().GetAwaiter().GetResult();

                _container = container;
                _connectionString =
                    $"Server={container.Hostname};Port={container.GetMappedPublicPort(ContainerPort)};Database=nextorm;User ID=nextorm;Password=nextorm";
            }
            catch (Exception exception)
            {
                _failure =
                    $"Could not start a MariaDB test container ({exception.Message}). " +
                    $"Set {ConnectionStringVariable} to use an already running server.";
            }
            finally
            {
                _resolved = true;
            }
        }
    }

    /// <summary>Releases the container at the end of the test run (with reuse enabled it is retained).</summary>
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
