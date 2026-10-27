using System.Runtime.CompilerServices;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using NextORM.Core;

// The adapter and the request semantics are internal (the public surface stays exactly the typed
// option group, the two client/data-source constructors and the two UseClickHouse overloads). The unit
// tests exercise the bounded adapter and the portable-equivalence rule directly, and the D6 counter
// measurement reads RowsConsumed/CompletedDriverCalls, so both test assemblies see the internals.
[assembly: InternalsVisibleTo("nextorm.clickhouse.tests")]
[assembly: InternalsVisibleTo("nextorm.integration.tests")]

namespace NextORM.ClickHouse.Bulk;

/// <summary>
/// The resolved ClickHouse request of one native <c>InsertBinaryAsync</c> write: the effective
/// parallelism and session, and the refusals that make a contradictory request fail by name before a
/// single row is read. A group with every value unset preserves the configured session and the driver's
/// single-stream default, so a write with no ClickHouse options behaves exactly like a plain insert.
/// </summary>
internal sealed class ClickHouseInsertRequest
{
    private readonly bool? _useSession;

    private ClickHouseInsertRequest(bool? useSession, string? sessionId, int maxDegreeOfParallelism)
    {
        _useSession = useSession;
        SessionId = sessionId;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    /// <summary>The degree of parallelism handed to the driver (<c>1</c> when not requested).</summary>
    public int MaxDegreeOfParallelism { get; }

    /// <summary>The actual session id the write runs in, or <see langword="null"/> for a sessionless write.</summary>
    public string? SessionId { get; }

    /// <summary>Whether the write runs in an actual ClickHouse session.</summary>
    public bool UsesSession => SessionId is not null;

    /// <summary>
    /// Resolves the native request against the settings of the client that will run it. A configured
    /// session id is read once from the client settings, never mutated: the caller's settings object and
    /// the connection's own settings stay untouched.
    /// </summary>
    /// <param name="command">The bulk-insert command carrying the requested option group.</param>
    /// <param name="settings">The resolved client's settings (a copy of the connection's settings).</param>
    /// <returns>The resolved request.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><c>MaxDegreeOfParallelism</c> is not positive.</exception>
    /// <exception cref="NotSupportedException">
    /// <c>UseSession=true</c> has no actual session id, a session is combined with a parallelism above one,
    /// or a local temporary target is requested on a sessionless write.
    /// </exception>
    public static ClickHouseInsertRequest Resolve(BulkInsertCommand command, ClickHouseClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(settings);

        var requested = command.ClickHouse;
        var parallelism = requested?.MaxDegreeOfParallelism ?? 1;
        ThrowIfInvalidParallelism(parallelism);

        var configuredSessionId = settings.SessionId;
        var configuredSessionOn = settings.UseSession && !string.IsNullOrEmpty(configuredSessionId);

        var useSession = requested?.UseSession;

        // A flag without an id cannot be honoured by inference: the driver would silently send no
        // session at all, so the request is refused by name instead.
        if (useSession is true && !configuredSessionOn)
        {
            throw new NotSupportedException(
                "ClickHouseBulkInsertOptions.UseSession=true requires an actual ClickHouse session id, but the resolved client settings have none "
                + "(UseSession is false or SessionId is empty). Configure the connection or client with UseSession=true and a session id, "
                + "or leave UseSession unset to preserve the configured session.");
        }

        var sessionId = useSession switch
        {
            true => configuredSessionId,
            false => null,
            _ => configuredSessionOn ? configuredSessionId : null,
        };

        if (sessionId is not null && parallelism > 1)
        {
            throw new NotSupportedException(
                $"ClickHouseBulkInsertOptions.MaxDegreeOfParallelism={parallelism} cannot be combined with a ClickHouse session: "
                + "ClickHouse allows one concurrent query per session. Use MaxDegreeOfParallelism=1, or request a sessionless insert (UseSession=false).");
        }

        if (command.TargetKind == BulkInsertTargetKind.LocalTemporary && sessionId is null)
        {
            throw new NotSupportedException(
                "BulkInsertOptions.TargetKind.LocalTemporary requires an actual ClickHouse session: a temporary table is visible only inside the "
                + "session that created it, and this request is sessionless (UseSession=false, or a sessionless client). "
                + "Configure UseSession=true, or write to a regular table.");
        }

        return new ClickHouseInsertRequest(useSession, sessionId, parallelism);
    }

    /// <summary>
    /// Validates the same request for the portable <c>INSERT ... VALUES</c> path, which is a single stream
    /// over the context connection and therefore equivalent only when the requested stream and session
    /// match it exactly: a parallelism above one cannot be expressed, a required session must exist on the
    /// connection, and a session cannot be turned off for the connection.
    /// </summary>
    /// <param name="options">The bulk-insert options carrying the requested option group.</param>
    /// <param name="connectionUsesSession">Whether the context connection has an actual ClickHouse session id.</param>
    /// <exception cref="ArgumentOutOfRangeException"><c>MaxDegreeOfParallelism</c> is not positive.</exception>
    /// <exception cref="NotSupportedException">The portable path cannot express the request.</exception>
    public static void ValidatePortableFallback(BulkInsertOptions options, bool connectionUsesSession)
    {
        ArgumentNullException.ThrowIfNull(options);

        var requested = options.ClickHouse;
        if (requested is null)
            return;

        var parallelism = requested.MaxDegreeOfParallelism ?? 1;
        ThrowIfInvalidParallelism(parallelism);

        if (parallelism > 1)
        {
            throw new NotSupportedException(
                $"ClickHouseBulkInsertOptions.MaxDegreeOfParallelism={parallelism} requires the ClickHouse native InsertBinaryAsync path: "
                + "the portable INSERT ... VALUES path writes a single stream. Use MaxDegreeOfParallelism=1, or run the write with Mode.Auto "
                + "on a context that has a ClickHouse client.");
        }

        switch (requested.UseSession)
        {
            case true when !connectionUsesSession:
                throw new NotSupportedException(
                    "ClickHouseBulkInsertOptions.UseSession=true cannot be honoured by the portable INSERT ... VALUES path: the context connection has no "
                    + "actual ClickHouse session id, so the portable write would run sessionless. Configure UseSession=true on the connection, "
                    + "or run the write with Mode.Native on a client that has a session.");

            case false when connectionUsesSession:
                throw new NotSupportedException(
                    "ClickHouseBulkInsertOptions.UseSession=false cannot be honoured by the portable INSERT ... VALUES path: the portable write runs on "
                    + "the context connection, whose ClickHouse session cannot be turned off. Use Mode.Native with a sessionless client, "
                    + "or leave UseSession unset.");
        }
    }

    /// <summary>Builds the driver options for one chunk of this request.</summary>
    /// <returns>A fresh <c>InsertOptions</c> instance, so no driver-side mutation leaks between chunks.</returns>
    public InsertOptions ToInsertOptions() => new()
    {
        // null preserves the configured session, true is paired with the resolved id, false is explicit.
        UseSession = _useSession,
        SessionId = SessionId,
        MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        UseSchemaCache = true,
    };

    private static void ThrowIfInvalidParallelism(int parallelism)
    {
        if (parallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(ClickHouseBulkInsertOptions.MaxDegreeOfParallelism), parallelism, "ClickHouseBulkInsertOptions.MaxDegreeOfParallelism must be positive.");
    }
}

/// <summary>
/// The bounded chunk adapter of the ClickHouse native bulk path. It pulls at most one chunk of rows from
/// the source, hands exactly that chunk to the driver, awaits the completed call, and only then pulls the
/// next one: the source is never buffered whole, and no chunk is sent before the previous call returned.
/// </summary>
/// <remarks>
/// The counters are the measurement surface of the acceptance criterion: <see cref="RowsConsumed"/> grows
/// as the source is pulled, <see cref="CompletedDriverCalls"/> grows only after a driver call returned
/// successfully, and <see cref="RowsWritten"/> is the driver-reported row count. Retention is therefore
/// bounded by <see cref="ChunkSize"/> row payloads plus constant bookkeeping. Progress is reported only
/// after a completed driver call, never on a timer and never from an invented callback.
/// </remarks>
internal sealed class ClickHouseBulkInsertAdapter
{
    /// <summary>Rows buffered per native call when <see cref="BulkInsertOptions.MaxBatchSize"/> is not set.</summary>
    internal const int DefaultChunkRows = 10_000;

    // A chunk never grows beyond the configured limit; the initial capacity is capped so an unusually
    // large MaxBatchSize does not allocate a huge empty buffer up front.
    private const int MaxInitialCapacity = 1024;

    private readonly Func<IReadOnlyList<object?[]>, CancellationToken, Task<int>> _send;
    private readonly int _chunkSize;

    /// <summary>Creates an adapter over a driver call.</summary>
    /// <param name="send">The awaited driver call: one chunk in, the written row count out.</param>
    /// <param name="chunkSize">The maximum rows handed to one driver call.</param>
    internal ClickHouseBulkInsertAdapter(Func<IReadOnlyList<object?[]>, CancellationToken, Task<int>> send, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(send);

        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "A native chunk must hold at least one row.");

        _send = send;
        _chunkSize = chunkSize;
    }

    /// <summary>The maximum rows handed to one driver call.</summary>
    public int ChunkSize => _chunkSize;

    /// <summary>The rows pulled from the source so far.</summary>
    public int RowsConsumed { get; private set; }

    /// <summary>The driver calls that returned successfully so far.</summary>
    public int CompletedDriverCalls { get; private set; }

    /// <summary>The rows the driver reported as written so far.</summary>
    public int RowsWritten { get; private set; }

    /// <summary>
    /// Creates an adapter over a ClickHouse client's <c>InsertBinaryAsync</c>: one awaited binary insert
    /// per chunk, with the resolved table reference, column list and request options.
    /// </summary>
    /// <param name="client">The client that runs the inserts.</param>
    /// <param name="table">The rendered, quoted destination the driver inserts into.</param>
    /// <param name="columns">The written column names, in row order.</param>
    /// <param name="request">The resolved session/parallelism request.</param>
    /// <param name="chunkSize">The maximum rows per driver call.</param>
    /// <returns>The adapter.</returns>
    internal static ClickHouseBulkInsertAdapter ForClient(
        IClickHouseClient client,
        string table,
        IReadOnlyList<string> columns,
        ClickHouseInsertRequest request,
        int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(request);

        return new ClickHouseBulkInsertAdapter(
            async (chunk, cancellationToken) =>
            {
                // The driver reports the written row count as a long; a bulk chunk never exceeds int range,
                // so the count is clamped rather than truncated.
                var written = await client.InsertBinaryAsync(table, columns, chunk, request.ToInsertOptions(), cancellationToken).ConfigureAwait(false);
                return written > int.MaxValue ? int.MaxValue : (int)written;
            },
            chunkSize);
    }

    /// <summary>Writes every row, one awaited driver call per chunk.</summary>
    /// <param name="rows">The row source, pulled one chunk at a time.</param>
    /// <param name="progress">Called with the cumulative written-row count after a completed driver call, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="deadline">The whole-invocation budget shared with the caller.</param>
    /// <returns>The number of rows the driver reported as written.</returns>
    public int Write(IEnumerable<object?[]> rows, Action<int>? progress, int notifyEvery, BulkInsertDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(deadline);

        var state = new ProgressState(progress, notifyEvery);
        var buffer = new List<object?[]>(Math.Min(_chunkSize, MaxInitialCapacity));

        foreach (var row in rows)
        {
            deadline.ThrowIfCancelled();
            RowsConsumed++;
            buffer.Add(row);

            if (buffer.Count >= _chunkSize)
            {
                Drain(buffer, state, deadline);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
            Drain(buffer, state, deadline);

        state.ReportFinal(RowsWritten);
        return RowsWritten;
    }

    /// <summary>Asynchronously writes every row, one awaited driver call per chunk.</summary>
    /// <param name="rows">The asynchronous row source, pulled one chunk at a time.</param>
    /// <param name="progress">Called with the cumulative written-row count after a completed driver call, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="deadline">The whole-invocation budget shared with the caller.</param>
    /// <returns>The number of rows the driver reported as written.</returns>
    public async Task<int> WriteAsync(IAsyncEnumerable<object?[]> rows, Action<int>? progress, int notifyEvery, BulkInsertDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(deadline);

        var state = new ProgressState(progress, notifyEvery);
        var buffer = new List<object?[]>(Math.Min(_chunkSize, MaxInitialCapacity));

        await foreach (var row in rows.WithCancellation(deadline.Token).ConfigureAwait(false))
        {
            deadline.ThrowIfCancelled();
            RowsConsumed++;
            buffer.Add(row);

            if (buffer.Count >= _chunkSize)
            {
                await DrainAsync(buffer, state, deadline).ConfigureAwait(false);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
            await DrainAsync(buffer, state, deadline).ConfigureAwait(false);

        state.ReportFinal(RowsWritten);
        return RowsWritten;
    }

    private void Drain(List<object?[]> buffer, ProgressState state, BulkInsertDeadline deadline)
    {
        deadline.ThrowIfCancelled();

        // The synchronous API blocks on the same awaited call the asynchronous path uses, so a completed
        // driver call is the only point at which a chunk counts as written.
        var written = _send(buffer, deadline.Token).GetAwaiter().GetResult();
        CompleteDriverCall(written, state);
    }

    private async Task DrainAsync(List<object?[]> buffer, ProgressState state, BulkInsertDeadline deadline)
    {
        deadline.ThrowIfCancelled();

        var written = await _send(buffer, deadline.Token).ConfigureAwait(false);
        CompleteDriverCall(written, state);
    }

    private void CompleteDriverCall(int written, ProgressState state)
    {
        RowsWritten += written;
        CompletedDriverCalls++;
        state.Report(RowsWritten);
    }

    /// <summary>
    /// The progress cadence of one write: a report is emitted only after a completed driver call and only
    /// once the next <c>NotifyEvery</c> threshold was reached, plus one final report when the last partial
    /// chunk advanced the total past the last threshold.
    /// </summary>
    private sealed class ProgressState
    {
        private readonly Action<int>? _progress;
        private readonly int _notifyEvery;
        private int _nextNotify;
        private int _reported;

        public ProgressState(Action<int>? progress, int notifyEvery)
        {
            _progress = progress;
            _notifyEvery = Math.Max(1, notifyEvery);
            _nextNotify = _notifyEvery;
        }

        public void Report(int total)
        {
            if (_progress is null || total < _nextNotify)
                return;

            _progress(total);
            _reported = total;
            _nextNotify = ((total / _notifyEvery) + 1) * _notifyEvery;
        }

        public void ReportFinal(int total)
        {
            if (_progress is null || total <= _reported)
                return;

            _progress(total);
            _reported = total;
        }
    }
}
