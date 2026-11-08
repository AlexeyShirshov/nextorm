using System.Text;
using Docker.DotNet.Models;
using Testcontainers.ClickHouse;

namespace NextORM.Integration.Tests;

/// <summary>
/// Owns the ClickHouse instance the integration suite runs against. A connection string provided
/// through <see cref="ConnectionStringVariable"/> always wins (pointing the suite at an already
/// running server); otherwise a reusable <see cref="ClickHouseBuilder"/> instance is started once
/// per test process and kept between runs (Testcontainers reuse), so the database initialization
/// cost is paid only on the first run. The container runtime is discovered the standard Testcontainers
/// way (the
/// <c>DOCKER_HOST</c> environment variable or the default Docker socket). Resolution is lazy and
/// cached, so a container is only started when a ClickHouse test actually runs.
/// </summary>
/// <remarks>
/// T359 (#349): every ClickHouse container the harness owns — this one and the dedicated F18 region
/// server in <c>T286bF18DictionaryTests</c> — is started through <see cref="WithResourceEnvelope"/>,
/// and every ClickHouse test class joins <see cref="ClickHouseCollection"/>. Both are part of the
/// shared-server convention, not local style; see those members for the measurements behind them.
/// </remarks>
internal static class ClickHouseContainer
{
    public const string ConnectionStringVariable = "NEXTORM_CLICKHOUSE_CONNECTION";

    private const string DefaultImage = "clickhouse/clickhouse-server:25.8-alpine";

    private const string ReuseLabel = "reuse-id";

    /// <summary>
    /// T359 (#349): the reuse identity was bumped when the resource envelope was introduced.
    /// Testcontainers' reuse hash covers the image, environment, command and labels but not the
    /// resource mappings or create-parameter modifiers, so a host that already owns the pre-envelope
    /// container would silently keep the unbounded server (measured: the envelope first ran against
    /// the old container with <c>Memory = 0</c> and a 10.5 GiB allowance). A new identity forces the
    /// bounded container to be created; the old one is left stopped and can be removed by hand.
    /// </summary>
    private const string ReuseIdentity = "nextorm-clickhouse-mem2g";

    /// <summary>
    /// T359 (#349): hard cgroup ceiling for every ClickHouse container the harness starts (2 GiB).
    /// </summary>
    /// <remarks>
    /// Without an explicit limit ClickHouse derives <c>max_server_memory_usage</c> from the memory it
    /// sees (0.9 × MemTotal). Inside this WSL2/Podman host the container sees the whole 11.7 GB
    /// machine, so the server reserved ~10.5 GiB and never self-limited while the host was swapping
    /// (measured during the T359 baseline run: 4.0 GB of a 4.1 GB swap in use, load average 31.9 on
    /// 8 vCPUs). The ceiling is sized from the measured server footprint — <c>MemoryTracking</c>
    /// peaked at 405 MB across the six barrier runs and the T359 baseline — leaving ~3.7× headroom.
    /// </remarks>
    internal const long ContainerMemoryLimitBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// T359 (#349): the server-level allowance passed as <c>--max_server_memory_usage</c> (1.5 GiB).
    /// Kept below <see cref="ContainerMemoryLimitBytes"/> so the server sheds memory by its own limit
    /// rather than being OOM-killed by the cgroup.
    /// </summary>
    internal const long ServerMemoryUsageBytes = 1536L * 1024 * 1024;

    /// <summary>
    /// Applies the T359 resource envelope to a ClickHouse builder: a 2 GiB container ceiling and a
    /// matching 1.5 GiB server allowance, so the server stays inside a bound the host can carry
    /// instead of pushing the shared WSL2 machine into swap. Both ClickHouse containers in the suite
    /// must use it; a builder that skips it is unbounded again (0.9 × the whole host).
    /// </summary>
    /// <remarks>
    /// The server allowance travels as a <c>config.d</c> file rather than a command-line argument:
    /// <c>clickhouse-server --max_server_memory_usage=…</c> is rejected with
    /// "Unknown option specified" (measured on the pinned image), while a config.d overlay is merged
    /// after <c>config.xml</c> and is the documented override path.
    /// </remarks>
    internal static ClickHouseBuilder WithResourceEnvelope(this ClickHouseBuilder builder) =>
        builder
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig ??= new HostConfig();
                parameters.HostConfig.Memory = ContainerMemoryLimitBytes;
            })
            .WithResourceMapping(ServerMemoryConfigBytes(), ServerMemoryConfigPath);

    /// <summary>Where the server-level memory allowance is mounted inside the container.</summary>
    private const string ServerMemoryConfigPath = "/etc/clickhouse-server/config.d/zz-nextorm-memory.xml";

    /// <summary>The <c>config.d</c> overlay that pins <c>max_server_memory_usage</c>.</summary>
    private static byte[] ServerMemoryConfigBytes() =>
        Encoding.UTF8.GetBytes(
            "<clickhouse>\n"
            + "    <!-- T359 (#349): keep the server inside ClickHouseContainer.ContainerMemoryLimitBytes. -->\n"
            + $"    <max_server_memory_usage>{ServerMemoryUsageBytes}</max_server_memory_usage>\n"
            + "</clickhouse>\n");

    private static readonly object Gate = new();

    // Volatile and only ever set to true once resolution has completed, so the lock-free fast
    // path can never observe a half-initialized state (tests run on many threads in parallel).
    private static volatile bool _resolved;
    private static Testcontainers.ClickHouse.ClickHouseContainer? _container;
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

    /// <summary>The reason ClickHouse is unavailable, or <c>null</c> when it is available.</summary>
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
                ?? throw new InvalidOperationException(_failure ?? "ClickHouse is not available.");
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

                // The Docker image defaults to UTC, but pinning TZ keeps the DateTime columns
                // unambiguous regardless of the host and the image tag.
                var container = new ClickHouseBuilder(DefaultImage)
                    .WithEnvironment("TZ", "UTC")
                    .WithResourceEnvelope()
                    .WithReuse(true)
                    .WithLabel(ReuseLabel, ReuseIdentity)
                    .Build();

                container.StartAsync().GetAwaiter().GetResult();

                _container = container;
                _connectionString = container.GetConnectionString();
            }
            catch (Exception exception)
            {
                _failure =
                    $"Could not start a ClickHouse test container ({exception.Message}). " +
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
    /// Releases the container at the end of the test run. With reuse enabled this only stops the
    /// container; it is retained and started again on the next run. Disposal happens through
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
