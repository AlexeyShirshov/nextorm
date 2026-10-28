using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Core.Tests.ClickHouseRetry;

/// <summary>
/// The measured response-reset contract (T362 AC-01/AC-04/AC-05): what <c>ClickHouse.Driver</c> 1.4.0
/// really throws for a controlled reset, and what the retrier does with it.
/// <para>
/// The classifier tests prove that the classifier agrees with a literal; these prove that the literal
/// is what the driver produces. A local <see cref="TcpListener"/> speaks enough HTTP/1.1 to answer one
/// request and then either resets the connection or completes it. Nothing here needs a database, a
/// container or a network beyond loopback, and every wait is bounded (the endpoint carries a 30 s
/// deadline and every socket call takes it), so a mistake fails the test instead of hanging CI.
/// </para>
/// </summary>
public sealed class ClickHouseResponseResetTests
{
    private static ClickHouseReadOperation Operation =>
        ClickHouseReadOperation.Audited(
            "test.identity.response-reset",
            "operation.identity.response-reset",
            ClickHouseReadCategory.PointRead);

    [Fact]
    public async Task PostResponseReset_ShouldSurfaceAsABareHttpIOExceptionCarryingResponseEnded()
    {
        // Case A of the T362 probe: a valid response head, one chunk carrying a complete row (99) so the
        // driver hands it to the reader, then a real RST with no terminating chunk.
        await using var endpoint = new ScriptedHttpEndpoint(ScriptedHttpEndpoint.PostResponseResetAfterARow(99));

        var failure = await endpoint.CaptureFailureAsync();

        failure.Should().BeOfType<HttpIOException>(
            "the post-response reset is typed by the HTTP layer, one level below the driver");
        var reset = (HttpIOException)failure;
        reset.HttpRequestError.Should().Be(HttpRequestError.ResponseEnded);
        reset.InnerException.Should().BeNull("the measured shape is bare: no wrapper and no inner chain");
        endpoint.RequestLine.Should().StartWith("POST /", "the request must actually have reached the listener");

        var classification = ClickHouseTransientClassifier.Classify(reset, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.TransportResponseReset);
        classification.ReasonCode.Should().Be("transport_response_reset");
        classification.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task PreResponseReset_ShouldBeIndistinguishableFromAGenuineEmptyBodyServerError()
    {
        // Case B of the T362 probe, contrasted with a genuine empty-body 503: if the two ADO surfaces
        // are identical, no classifier can separate them, so the pre-response reset must stay closed.
        await using var resetEndpoint = new ScriptedHttpEndpoint(ScriptedHttpEndpoint.PreResponseReset());
        var resetFailure = await resetEndpoint.CaptureFailureAsync();

        await using var serverErrorEndpoint = new ScriptedHttpEndpoint(ScriptedHttpEndpoint.EmptyBodyServiceUnavailable());
        var serverFailure = await serverErrorEndpoint.CaptureFailureAsync();

        resetFailure.Should().BeOfType<ClickHouseServerException>();
        var reset = (ClickHouseServerException)resetFailure;
        reset.ErrorCode.Should().Be(-1);
        reset.InnerException.Should().BeNull();

        serverFailure.Should().BeOfType<ClickHouseServerException>();
        var serverError = (ClickHouseServerException)serverFailure;

        serverError.ErrorCode.Should().Be(reset.ErrorCode, "the driver synthesises the same code for both surfaces");
        serverError.Message.Should().Be(reset.Message, "the driver synthesises the same message for both surfaces");

        var classification = ClickHouseTransientClassifier.Classify(reset, CancellationToken.None);
        classification.Kind.Should().Be(ClickHouseFailureKind.ServerError);
        classification.IsRetryable.Should().BeFalse("a pre-response reset carries no typed signal, so it stays fail-closed");

        ClickHouseTransientClassifier.Classify(serverError, CancellationToken.None).IsRetryable
            .Should().BeFalse("a genuine empty-body server error is never retried");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecoverFromTheMeasuredReset_AndReturnOnlyTheCompleteSecondResult()
    {
        // Attempt 1 resets after delivering row 99 to the reader; attempt 2 completes with row 1.
        await using var endpoint = new ScriptedHttpEndpoint(
            ScriptedHttpEndpoint.PostResponseResetAfterARow(99),
            ScriptedHttpEndpoint.CompleteSingleRow(1));

        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) => Task.CompletedTask);

        await using var connection = new ClickHouseConnection(endpoint.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var result = await retrier.ExecuteAsync(
            Operation,
            async cancellationToken =>
            {
                invocations++;
                return await ReadSingleColumnAsync(connection, cancellationToken);
            },
            CancellationToken.None);

        invocations.Should().Be(2, "exactly one retry is permitted");
        endpoint.AcceptedConnections.Should().Be(2, "each attempt opens its own connection to the scripted endpoint");
        result.Should().Equal(["1"], "the caller receives the successful attempt's complete result");
        result.Should().NotContain("99", "the row delivered before the reset must never reach the caller");

        records.Should().HaveCount(2);
        records[0].Attempt.Should().Be(1);
        records[0].ReasonCode.Should().Be("transport_response_reset");
        records[0].ScheduledRetry.Should().BeTrue();
        records[1].Attempt.Should().Be(2);
        records[1].Succeeded.Should().BeTrue();
        records[1].ScheduledRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNeverReturnOrMergeTheFailedAttemptsPartialResult()
    {
        // The probe showed the driver delivering a partial row before the reset surfaces, so a failed
        // attempt can have produced rows. The retrier must discard them wholesale and return only the
        // successful attempt's value — never a merge of the two.
        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) => Task.CompletedTask);

        var result = await retrier.ExecuteAsync(
            Operation,
            _ =>
            {
                invocations++;

                var rows = new List<string>();
                if (invocations == 1)
                {
                    rows.Add("partial-1");
                    rows.Add("partial-2");
                    return Task.FromException<List<string>>(
                        new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely."));
                }

                rows.Add("row-1");
                rows.Add("row-2");
                rows.Add("row-3");
                return Task.FromResult(rows);
            },
            CancellationToken.None);

        invocations.Should().Be(2);
        result.Should().Equal(["row-1", "row-2", "row-3"]);
        result.Should().NotContain("partial-1");
        result.Should().NotContain("partial-2");
        records.Should().HaveCount(2);
    }

    private static async Task<List<string>> ReadSingleColumnAsync(ClickHouseConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(Convert.ToString(reader.GetValue(0)) ?? "null");

        return rows;
    }
}

/// <summary>
/// A one-request-per-connection loopback HTTP endpoint whose behaviour is scripted per accepted
/// connection. It exists to observe the real driver's exception shapes for controlled transport faults
/// (T362) without a server, a container or a database. Boundedness is part of the contract: the whole
/// endpoint carries one deadline, every socket wait takes that token, and teardown awaits the accept
/// loop so a scripted fault can never leave a background task running into CI.
/// </summary>
internal sealed class ScriptedHttpEndpoint : IAsyncDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private readonly TcpListener _listener;
    private readonly Queue<Func<Socket, Task>> _script;
    private readonly CancellationTokenSource _deadline = new(Deadline);
    private readonly Task _server;
    private int _accepted;

    internal ScriptedHttpEndpoint(params Func<Socket, Task>[] script)
    {
        _script = new Queue<Func<Socket, Task>>(script);
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _server = Task.Run(AcceptAsync);
    }

    internal int Port { get; }

    internal int AcceptedConnections => Volatile.Read(ref _accepted);

    internal string RequestLine { get; private set; } = string.Empty;

    internal string ConnectionString =>
        $"Host=127.0.0.1;Port={Port};Protocol=http;Database=default;Username=default;Password=";

    /// <summary>Writes a complete response head and one chunk holding a single <c>UInt8</c> row, then
    /// resets the connection with an RST (<c>SO_LINGER {on, 0}</c>) and no terminating chunk.</summary>
    internal static Func<Socket, Task> PostResponseResetAfterARow(byte rowValue) => async socket =>
    {
        await socket.SendAsync(Head("HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nTransfer-Encoding: chunked\r\n\r\n"), SocketFlags.None);

        var payload = RowBinarySingleUInt8Row(rowValue);
        await socket.SendAsync(Chunk(payload), SocketFlags.None);

        // One bounded pause so the client consumes the partial row before the RST discards the receive
        // queue; this is sequencing, not polling.
        await Task.Delay(TimeSpan.FromMilliseconds(150), CancellationToken.None);
        Reset(socket);
    };

    /// <summary>Accepts the connection, reads the request, then resets without writing a single byte.</summary>
    internal static Func<Socket, Task> PreResponseReset() => socket =>
    {
        Reset(socket);
        return Task.CompletedTask;
    };

    /// <summary>A complete, well-formed chunked response carrying one <c>UInt8</c> row.</summary>
    internal static Func<Socket, Task> CompleteSingleRow(byte rowValue) => async socket =>
    {
        await socket.SendAsync(Head("HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nTransfer-Encoding: chunked\r\n\r\n"), SocketFlags.None);

        var payload = RowBinarySingleUInt8Row(rowValue);
        var body = Encoding.ASCII.GetBytes($"{payload.Length:x}\r\n")
            .Concat(payload)
            .Concat(Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"))
            .ToArray();
        await socket.SendAsync(body, SocketFlags.None);
        socket.Shutdown(SocketShutdown.Send);
    };

    /// <summary>A genuine server error: HTTP 503 with a <c>Content-Length: 0</c> body and a graceful close.</summary>
    internal static Func<Socket, Task> EmptyBodyServiceUnavailable() => async socket =>
    {
        await socket.SendAsync(Head("HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n"), SocketFlags.None);
        socket.Shutdown(SocketShutdown.Send);
    };

    /// <summary>Runs one <c>SELECT 1</c> against the endpoint and returns the exception the driver
    /// produced. A run that does not fault is a failed contract case, not a pass.</summary>
    internal async Task<Exception> CaptureFailureAsync()
    {
        Exception? captured = null;

        try
        {
            await using var connection = new ClickHouseConnection(ConnectionString);
            await connection.OpenAsync(_deadline.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await using var reader = await command.ExecuteReaderAsync(_deadline.Token);
            while (await reader.ReadAsync(_deadline.Token))
            {
            }
        }
        catch (Exception failure)
        {
            captured = failure;
        }

        return captured
            ?? throw new InvalidOperationException("The scripted endpoint produced no failure; the contract case was not exercised.");
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        await _deadline.CancelAsync();

        try
        {
            await _server.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _deadline.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_deadline.IsCancellationRequested)
        {
            Socket socket;
            try
            {
                socket = await _listener.AcceptSocketAsync(_deadline.Token);
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

            Interlocked.Increment(ref _accepted);

            using (socket)
            {
                await DrainRequestAsync(socket);
                var behaviour = _script.Count > 0 ? _script.Dequeue() : CompleteSingleRow(1);
                await behaviour(socket);
            }
        }
    }

    private async Task DrainRequestAsync(Socket socket)
    {
        var buffer = new byte[8192];
        var received = new StringBuilder();

        while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await socket.ReceiveAsync(buffer, SocketFlags.None, _deadline.Token);
            if (read <= 0)
                break;

            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        RequestLine = received.ToString().Split("\r\n")[0];
    }

    private static byte[] Head(string head) => Encoding.ASCII.GetBytes(head);

    private static byte[] Chunk(byte[] payload) =>
        Encoding.ASCII.GetBytes($"{payload.Length:x}\r\n").Concat(payload).Concat(Encoding.ASCII.GetBytes("\r\n")).ToArray();

    private static void Reset(Socket socket)
    {
        socket.LingerState = new LingerOption(true, 0);
        socket.Close();
    }

    private static byte[] RowBinarySingleUInt8Row(byte rowValue)
    {
        // RowBinaryWithNamesAndTypes: column count, then names, then types, then the row values.
        var bytes = new List<byte> { 0x01, 0x01, (byte)'1' };
        var type = Encoding.ASCII.GetBytes("UInt8");
        bytes.Add((byte)type.Length);
        bytes.AddRange(type);
        bytes.Add(rowValue);
        return bytes.ToArray();
    }
}
