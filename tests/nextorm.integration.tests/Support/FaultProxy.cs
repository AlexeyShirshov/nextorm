using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace NextORM.Integration.Tests;

/// <summary>
/// A test-owned TCP fault injector for the ClickHouse acceptance probes (T328 D4a / EV06). It listens
/// on <c>127.0.0.1:0</c>, connects to the live ClickHouse endpoint supplied by the caller and pumps
/// bytes in both directions, applying one deterministic fault mode per instance.
/// <para>
/// The proxy never prints anything and never reveals the connection string it was given: the caller
/// gets a rewritten string back from <see cref="ConnectionStringFor"/> and the only observable state
/// is the counter read-back below. The counters are how a probe proves endpoint reachability without
/// ever touching a payload: <see cref="ServerRespondedBeforeFault"/> is true only when bytes came back
/// from the live server through this proxy before the connection was faulted, and a pre-response
/// reset proves reachability with <see cref="TargetConnected"/> plus <see cref="ClientToServerBytes"/>,
/// which show that a TCP connection was established and the request was forwarded.
/// </para>
/// <para>
/// Disposal is robust by construction: every pump swallows transport teardown, every connection task
/// is observed, the listener is stopped and every socket is aborted, so no unobserved task exception
/// survives a probe.
/// </para>
/// </summary>
internal sealed class FaultProxy : IAsyncDisposable
{
    /// <summary>The driver's default HTTP port, used when the connection string carries no <c>Port</c>.</summary>
    private const int DefaultPort = 8123;

    private const int BufferSize = 16 * 1024;

    /// <summary>Upper bound on waiting for the live endpoint to respond, so a stuck server cannot hang a probe.</summary>
    private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(15);

    /// <summary>Reset-stage marker: no fault was staged on this proxy.</summary>
    internal const string ResetStageNone = "none";

    /// <summary>Reset-stage marker: the client's request bytes were forwarded to the target.</summary>
    internal const string ResetStageRequestForwarded = "request_forwarded";

    /// <summary>Reset-stage marker: no request byte could be forwarded before the wait expired.</summary>
    internal const string ResetStageRequestTimeout = "request_timeout";

    /// <summary>Reset-stage marker: the configured response-byte threshold was never reached.</summary>
    internal const string ResetStageResponseTimeout = "response_timeout";

    /// <summary>Reset-stage marker: exactly the configured response bytes were forwarded, then reset.</summary>
    internal const string ResetStageResponseBytesForwarded = "response_bytes_forwarded";

    /// <summary>Reset-stage marker: the configured pre-request delay elapsed, then reset.</summary>
    internal const string ResetStageDelayElapsed = "delay_elapsed";

    private readonly TcpListener _listener;
    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly FaultMode _mode;
    private readonly TimeSpan _delay;
    private readonly int _responseBytesBeforeReset;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentDictionary<long, Socket> _sockets = new();
    private readonly List<Task> _connections = [];
    private readonly object _connectionsGate = new();
    private readonly Task _acceptLoop;

    private long _nextSocketId;
    private int _acceptedConnections;
    private long _clientToServerBytes;
    private long _serverToClientBytes;
    private int _faultedConnections;
    private int _serverRespondedBeforeFault;
    private int _targetConnected;
    private int _connectionsObserved;
    private string _resetStage = ResetStageNone;

    private FaultProxy(string connectionString, FaultMode mode, TimeSpan delay, int responseBytesBeforeReset)
    {
        (_targetHost, _targetPort) = ParseEndpoint(connectionString);
        _mode = mode;
        _delay = delay;
        _responseBytesBeforeReset = responseBytesBeforeReset;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        ProxyPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>How one accepted client connection is treated.</summary>
    internal enum FaultMode
    {
        /// <summary>Forward both directions for the lifetime of the connection; never fault.</summary>
        PassThrough,

        /// <summary>
        /// Read the client's first request but do not forward it for the configured delay, then close
        /// both sockets abruptly (RST). A client whose configured timeout is shorter than the delay
        /// observes its own client-side timeout; later connections pass through.
        /// </summary>
        DelayFirstRequest,

        /// <summary>
        /// Forward the request and reset both sockets as soon as it has been forwarded, before a
        /// single response byte reaches the client. Only the first connection is faulted; later
        /// connections pass through.
        /// </summary>
        ResetBeforeResponse,

        /// <summary>
        /// Forward the request, let at most the configured number of response bytes come back from
        /// the live server through this proxy, then reset both sockets. Never forwards the remainder
        /// of that response. Only the first connection is faulted; later connections pass through.
        /// </summary>
        ResetAfterFirstResponseBytes,

        /// <summary>
        /// <see cref="ResetBeforeResponse"/> semantics on every accepted connection: the request is
        /// forwarded, then both sockets are reset before any response byte reaches the client.
        /// </summary>
        ResetAlways,
    }

    /// <summary>The loopback port the proxy listens on.</summary>
    internal int ProxyPort { get; }

    /// <summary>The number of client connections accepted by the proxy.</summary>
    internal int AcceptedConnections => Volatile.Read(ref _acceptedConnections);

    /// <summary>Bytes observed travelling from a client into the proxy.</summary>
    internal long ClientToServerBytes => Interlocked.Read(ref _clientToServerBytes);

    /// <summary>Bytes observed coming back from the live endpoint into the proxy.</summary>
    internal long ServerToClientBytes => Interlocked.Read(ref _serverToClientBytes);

    /// <summary>The number of connections the proxy faulted (abrupt reset).</summary>
    internal int FaultedConnections => Volatile.Read(ref _faultedConnections);

    /// <summary>
    /// True when at least one response byte came back from the live endpoint through this proxy
    /// before the fault. This is the endpoint-reachability proof: the request really reached the
    /// server, without any payload being printed.
    /// </summary>
    internal bool ServerRespondedBeforeFault => Volatile.Read(ref _serverRespondedBeforeFault) != 0;

    /// <summary>
    /// True when the proxy established a TCP connection to the live endpoint. A pre-response reset
    /// carries no response bytes, so this counter (together with <see cref="ClientToServerBytes"/>)
    /// is the endpoint proof for <see cref="FaultMode.ResetBeforeResponse"/>.
    /// </summary>
    internal bool TargetConnected => Volatile.Read(ref _targetConnected) != 0;

    /// <summary>The stage at which the proxy stopped forwarding, as a short symbolic marker.</summary>
    internal string ResetStage => Volatile.Read(ref _resetStage);

    /// <summary>Creates a proxy that forwards both directions and never faults.</summary>
    internal static FaultProxy PassThrough(string connectionString) =>
        new(connectionString, FaultMode.PassThrough, TimeSpan.Zero, 0);

    /// <summary>Creates a proxy that withholds the first request for <paramref name="delay"/>, then resets.</summary>
    internal static FaultProxy DelayFirstRequest(string connectionString, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "The delay must be positive.");

        return new FaultProxy(connectionString, FaultMode.DelayFirstRequest, delay, 0);
    }

    /// <summary>Creates a proxy that resets after <paramref name="responseBytes"/> bytes came back from the server.</summary>
    internal static FaultProxy ResetAfterFirstResponseBytes(string connectionString, int responseBytes)
    {
        if (responseBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(responseBytes), "At least one response byte must be forwarded before the reset.");

        return new FaultProxy(connectionString, FaultMode.ResetAfterFirstResponseBytes, TimeSpan.Zero, responseBytes);
    }

    /// <summary>
    /// Creates a proxy that forwards the first connection's request to the live endpoint and then
    /// resets it before any response byte is delivered; later connections pass through.
    /// </summary>
    internal static FaultProxy ResetBeforeResponse(string connectionString) =>
        new(connectionString, FaultMode.ResetBeforeResponse, TimeSpan.Zero, 0);

    /// <summary>Creates a proxy that resets every connection before the response reaches the client.</summary>
    internal static FaultProxy ResetAlways(string connectionString) =>
        new(connectionString, FaultMode.ResetAlways, TimeSpan.Zero, 0);

    /// <summary>
    /// Rewrites the <c>Host</c>/<c>Port</c> pair of <paramref name="baseConnectionString"/> to this
    /// proxy while preserving every other key (credentials included). The returned string is the only
    /// outbound artefact; neither it nor its keys are ever printed by this class.
    /// </summary>
    internal string ConnectionStringFor(string baseConnectionString)
    {
        ArgumentNullException.ThrowIfNull(baseConnectionString);

        var builder = BuildBuilder(baseConnectionString, nameof(baseConnectionString));
        builder["Host"] = IPAddress.Loopback.ToString();
        builder["Port"] = ProxyPort.ToString(CultureInfo.InvariantCulture);
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Already disposed: nothing left to cancel.
        }

        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
            // The listener was already torn down.
        }

        foreach (var socket in _sockets.Values)
            Abort(socket);

        Task[] connections;
        lock (_connectionsGate)
            connections = [.. _connections];

        await ObserveAsync(connections).ConfigureAwait(false);
        await ObserveAsync([_acceptLoop]).ConfigureAwait(false);

        _cancellation.Dispose();
    }

    private static (string Host, int Port) ParseEndpoint(string connectionString)
    {
        var builder = BuildBuilder(connectionString, nameof(connectionString));

        var host = ReadKey(builder, "Host") ?? ReadKey(builder, "Server");
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("The ClickHouse connection string carries no Host key.", nameof(connectionString));

        var portText = ReadKey(builder, "Port");
        if (portText is null)
            return (host, DefaultPort);

        if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            throw new ArgumentException(
                "The ClickHouse connection string carries a Port value that is not a valid TCP port.",
                nameof(connectionString));
        }

        return (host, port);
    }

    private static DbConnectionStringBuilder BuildBuilder(string connectionString, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        try
        {
            return new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "The ClickHouse connection string is not a valid ADO.NET connection string.",
                parameterName,
                exception);
        }
    }

    private static string? ReadKey(DbConnectionStringBuilder builder, string key) =>
        builder.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private async Task AcceptLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await _listener.AcceptSocketAsync(_cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                // A transient accept failure must not kill the proxy; the probe's counters expose it.
                continue;
            }

            var connectionId = Interlocked.Increment(ref _acceptedConnections);
            var connection = Task.Run(() => HandleConnectionAsync(connectionId, client), CancellationToken.None);
            lock (_connectionsGate)
                _connections.Add(connection);
        }
    }

    private async Task HandleConnectionAsync(int connectionId, Socket client)
    {
        Socket? server = null;

        try
        {
            Track(client);

            var endpoint = await ResolveEndpointAsync(_cancellation.Token).ConfigureAwait(false);
            server = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            Track(server);
            await server.ConnectAsync(endpoint, _cancellation.Token).ConfigureAwait(false);
            Volatile.Write(ref _targetConnected, 1);

            client.NoDelay = true;

            var firstConnection = Interlocked.Increment(ref _connectionsObserved) == 1;
            switch (_mode)
            {
                case FaultMode.DelayFirstRequest when firstConnection:
                    await FaultAfterDelayAsync(client, server).ConfigureAwait(false);
                    break;

                case FaultMode.ResetBeforeResponse when firstConnection:
                    await ResetBeforeResponseAsync(client, server).ConfigureAwait(false);
                    break;

                case FaultMode.ResetAfterFirstResponseBytes when firstConnection:
                    await FaultAfterResponseBytesAsync(client, server).ConfigureAwait(false);
                    break;

                case FaultMode.ResetAlways:
                    await ResetBeforeResponseAsync(client, server).ConfigureAwait(false);
                    break;

                default:
                    // PassThrough, and every later connection of a one-shot fault mode.
                    await PumpBothWaysAsync(client, server).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception)
        {
            // Transport teardown: the client aborted, the listener stopped, or the endpoint was
            // unreachable. Nothing may escape an observed connection task; the counters, not an
            // exception, are this proxy's evidence.
        }
        finally
        {
            Abort(client);
            Abort(server);
        }
    }

    private async Task<IPEndPoint> ResolveEndpointAsync(CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(_targetHost, cancellationToken).ConfigureAwait(false);
        var address = Array.Find(addresses, static candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
            ?? (addresses.Length > 0
                ? addresses[0]
                : throw new InvalidOperationException("The ClickHouse endpoint host did not resolve to any address."));

        return new IPEndPoint(address, _targetPort);
    }

    private async Task PumpBothWaysAsync(Socket client, Socket server)
    {
        var token = _cancellation.Token;
        await Task.WhenAll(
                PumpAsync(client, server, countsRequestBytes: true, token),
                PumpAsync(server, client, countsRequestBytes: false, token))
            .ConfigureAwait(false);
    }

    private async Task PumpAsync(Socket source, Socket destination, bool countsRequestBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await source.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return;

                Count(countsRequestBytes, read);
                await destination.SendAsync(buffer.AsMemory(0, read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposal or a caller abort: the connection is being torn down.
        }
        catch (SocketException)
        {
            // Peer reset or shutdown.
        }
        catch (IOException)
        {
            // Transport closed underneath the pump.
        }
        catch (ObjectDisposedException)
        {
            // The socket was aborted by another path (disposal or Fault).
        }
    }

    /// <summary>
    /// Mode <see cref="FaultMode.DelayFirstRequest"/>: consume the client's first request without
    /// forwarding it, hold it for the configured delay and then reset both sockets.
    /// </summary>
    private async Task FaultAfterDelayAsync(Socket client, Socket server)
    {
        var buffer = new byte[BufferSize];

        try
        {
            var read = await client.ReceiveAsync(buffer, SocketFlags.None, _cancellation.Token).ConfigureAwait(false);
            if (read == 0)
                return;

            Count(countsRequestBytes: true, read);
            await Task.Delay(_delay, _cancellation.Token).ConfigureAwait(false);
            Volatile.Write(ref _resetStage, ResetStageDelayElapsed);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (SocketException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        Fault(client, server);
    }

    /// <summary>
    /// Mode <see cref="FaultMode.ResetAfterFirstResponseBytes"/>: forward the request, forward at
    /// most the configured number of response bytes from the live endpoint, then reset both sockets.
    /// The remainder of that response is never forwarded.
    /// </summary>
    private async Task FaultAfterResponseBytesAsync(Socket client, Socket server)
    {
        var token = _cancellation.Token;
        var thresholdReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = PumpAsync(client, server, countsRequestBytes: true, token);
        var response = PumpUntilThresholdAsync(server, client, thresholdReached, token);

        try
        {
            await thresholdReached.Task.WaitAsync(ResponseWait, token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Volatile.Write(ref _resetStage, ResetStageResponseTimeout);
            await ObserveAsync([request, response]).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException)
        {
            await ObserveAsync([request, response]).ConfigureAwait(false);
            return;
        }

        Volatile.Write(ref _resetStage, ResetStageResponseBytesForwarded);
        Fault(client, server);
        await ObserveAsync([request, response]).ConfigureAwait(false);
    }

    private async Task PumpUntilThresholdAsync(
        Socket source,
        Socket destination,
        TaskCompletionSource thresholdReached,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        var forwarded = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && forwarded < _responseBytesBeforeReset)
            {
                var read = await source.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return;

                // Any byte read back from the endpoint proves reachability, even when it is not
                // delivered to the client.
                Volatile.Write(ref _serverRespondedBeforeFault, 1);

                // Forward at most the remaining threshold: the rest of this chunk is dropped, so the
                // client can never receive a complete response before the reset.
                var toForward = Math.Min(read, _responseBytesBeforeReset - forwarded);
                await destination.SendAsync(buffer.AsMemory(0, toForward), SocketFlags.None, cancellationToken).ConfigureAwait(false);
                Count(countsRequestBytes: false, toForward);
                forwarded += toForward;
            }

            if (forwarded >= _responseBytesBeforeReset)
                thresholdReached.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            // Teardown.
        }
        catch (SocketException)
        {
            // Peer reset or shutdown.
        }
        catch (IOException)
        {
            // Transport closed underneath the pump.
        }
        catch (ObjectDisposedException)
        {
            // Aborted by another path.
        }
    }

    /// <summary>
    /// Modes <see cref="FaultMode.ResetBeforeResponse"/> and <see cref="FaultMode.ResetAlways"/>:
    /// accept the client, connect to the live endpoint, forward the client's request bytes and then
    /// reset the client's TCP connection before a single response byte is forwarded. The target's
    /// response is never read, so it can never reach the client; the target socket is reset too, so
    /// the server drops the in-flight query.
    /// </summary>
    private async Task ResetBeforeResponseAsync(Socket client, Socket server)
    {
        var token = _cancellation.Token;
        var requestForwarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = PumpFirstRequestChunkAsync(client, server, requestForwarded, token);

        try
        {
            await requestForwarded.Task.WaitAsync(ResponseWait, token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Volatile.Write(ref _resetStage, ResetStageRequestTimeout);
            Fault(client, server);
            await ObserveAsync([request]).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException)
        {
            await ObserveAsync([request]).ConfigureAwait(false);
            return;
        }

        Volatile.Write(ref _resetStage, ResetStageRequestForwarded);

        // The RST is sent on the client socket with LingerState(true, 0), and only after the request
        // left the client: the client is mid-request/response and observes a transport reset instead
        // of a response (a server exception can therefore never be manufactured here).
        Fault(client, server);
        await ObserveAsync([request]).ConfigureAwait(false);
    }

    /// <summary>
    /// Forwards the client's request chunks to the target and signals after the first chunk has been
    /// delivered. No response byte is ever read or forwarded by this pump.
    /// </summary>
    private async Task PumpFirstRequestChunkAsync(
        Socket source,
        Socket destination,
        TaskCompletionSource requestForwarded,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await source.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return;

                Count(countsRequestBytes: true, read);
                await destination.SendAsync(buffer.AsMemory(0, read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
                requestForwarded.TrySetResult();
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Teardown.
        }
        catch (SocketException)
        {
            // Peer reset or shutdown.
        }
        catch (IOException)
        {
            // Transport closed underneath the pump.
        }
        catch (ObjectDisposedException)
        {
            // Aborted by another path.
        }
    }

    private long Count(bool countsRequestBytes, int byteCount) =>
        countsRequestBytes
            ? Interlocked.Add(ref _clientToServerBytes, byteCount)
            : Interlocked.Add(ref _serverToClientBytes, byteCount);

    private void Fault(Socket client, Socket server)
    {
        Interlocked.Increment(ref _faultedConnections);
        Abort(client);
        Abort(server);
    }

    private void Track(Socket socket) => _sockets[Interlocked.Increment(ref _nextSocketId)] = socket;

    /// <summary>
    /// Closes a socket with <c>LingerState(true, 0)</c>, which makes the TCP stack send RST instead of
    /// an orderly FIN: the peer observes a transport reset rather than a clean end of stream.
    /// </summary>
    private static void Abort(Socket? socket)
    {
        if (socket is null)
            return;

        try
        {
            socket.LingerState = new LingerOption(enable: true, seconds: 0);
        }
        catch (SocketException)
        {
            // Already disconnected; the close below still releases the handle.
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            socket.Close();
        }
        catch (SocketException)
        {
            // Best effort teardown.
        }
        catch (ObjectDisposedException)
        {
            // Already closed.
        }
    }

    /// <summary>Observes every task and swallows its outcome, so no fault is left unobserved.</summary>
    private static async Task ObserveAsync(IEnumerable<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The pumps already handle transport teardown; this guard only keeps the disposal path
            // and every observed connection task exception-free.
        }
    }
}
