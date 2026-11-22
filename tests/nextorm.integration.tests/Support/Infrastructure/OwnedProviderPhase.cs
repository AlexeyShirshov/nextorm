using System.Net.Sockets;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;

namespace NextORM.Integration.Tests;

/// <summary>Runs one owned provider phase. The runner must hold the exclusive backend lease.</summary>
internal static class OwnedProviderPhase
{
    private static bool WslcEnabled => Environment.GetEnvironmentVariable("NEXTORM_SERIAL_WSLC") == "1";

    internal static bool Enabled => WslcEnabled || Environment.GetEnvironmentVariable("NEXTORM_SERIAL_OWNED") == "1";

    internal static void RequireCompatible(bool hasExternalProvider)
    {
        if (hasExternalProvider)
            throw new InvalidOperationException("An owned provider phase cannot use or stop an external provider.");
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProviderSkipPolicy.EnvironmentVariable)))
            throw new InvalidOperationException("Owned provider phases do not allow provider opt-outs.");
    }

    internal static Task RunEndpoint<T>(string provider, Func<T> create, Func<T, string> endpoint,
        Action<string> use, CancellationToken cancellationToken)
        where T : IContainer => WslcEnabled
        ? RunWslc(provider, use, cancellationToken)
        : Run(create, container => use(endpoint(container)), cancellationToken);

    // The launcher retains the exclusive lock and owns cleanup even if this client disconnects.
    // The private socket is per worker. There is no fallback and an endpoint is never logged.
    private static async Task RunWslc(string provider, Action<string> use, CancellationToken cancellationToken)
    {
        var path = Environment.GetEnvironmentVariable("NEXTORM_WSLC_OWNED_SOCKET");
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("The explicit wslc mode requires a private lifecycle socket.");

        using var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startTimeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), startTimeout.Token);
        using var stream = new NetworkStream(socket);
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream) { AutoFlush = true };
        await writer.WriteLineAsync(provider.AsMemory(), startTimeout.Token);
        var endpoint = await reader.ReadLineAsync(startTimeout.Token);
        if (endpoint is null || !endpoint.StartsWith("ready ", StringComparison.Ordinal))
            throw new InvalidOperationException("The owned wslc provider did not establish readiness.");

        try
        {
            var connectionString = endpoint[6..];
            if (provider == "sqlserver")
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(startTimeout.Token);
                using var command = connection.CreateCommand();
                command.CommandText = "select cast(serverproperty('ProductMajorVersion') as int)";
                if (await command.ExecuteScalarAsync(startTimeout.Token) is not 17)
                    throw new InvalidOperationException("The owned SQL Server endpoint is not the required major version 17.");
            }
            use(connectionString);
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await writer.WriteLineAsync("stop".AsMemory(), stopTimeout.Token);
            if (await reader.ReadLineAsync(stopTimeout.Token) != "removed")
                throw new InvalidOperationException("The previous owned wslc provider was not verified removed.");
        }
    }

    internal static async Task Run<T>(Func<T> create, Action<T> use, CancellationToken cancellationToken)
        where T : IContainer
    {
        await using var container = create();
        await container.StartAsync(cancellationToken);
        var id = container.Id;
        try
        {
            use(container);
        }
        finally
        {
            // Cleanup must still run if the test is canceled. Testcontainers 4.16 refreshes
            // its inspection snapshot for this ID inside StopAsync before returning.
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await container.StopAsync(stopTimeout.Token);
            if (container.State != TestcontainersStates.Exited || !string.Equals(container.Id, id, StringComparison.Ordinal))
                throw new InvalidOperationException("The previous owned provider's stopped state could not be confirmed.");
        }
        // The container and its connection string provider are local to this call. Nothing
        // is published to a shared endpoint cache; disposal must also finish before returning.
    }
}
