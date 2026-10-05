using System.Data;
using System.Text.Json;

namespace NextORM.Core;

/// <summary>
/// Streams result rows as JSON into a caller-owned <see cref="Stream"/>. Owns the bounded buffer and
/// the <see cref="Utf8JsonWriter"/>; never closes the destination. The container framing lives here
/// (<c>[</c>/<c>]</c>, an optional <c>Root</c> wrapper, NDJSON newlines), while the per-row value is
/// written by the compiled <see cref="JsonRowWriter"/>.
/// </summary>
internal sealed class JsonStreamWriter : IDisposable
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private readonly PooledStreamBufferWriter _sink;
    private readonly Utf8JsonWriter _writer;
    private readonly JsonRowWriter _rowWriter;
    private readonly JsonStreamOptions _options;
    private int _rowsWritten;
    private bool _completed;
    private bool _disposed;

    /// <summary>Initializes the writer and emits the container prologue.</summary>
    /// <param name="output">The caller-owned destination stream; it is never closed.</param>
    /// <param name="rowWriter">The compiled per-row writer.</param>
    /// <param name="options">The validated container options.</param>
    public JsonStreamWriter(Stream output, JsonRowWriter rowWriter, JsonStreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(rowWriter);
        ArgumentNullException.ThrowIfNull(options);

        _rowWriter = rowWriter;
        _options = options;
        _sink = new PooledStreamBufferWriter(output);
        _writer = new Utf8JsonWriter(_sink, new JsonWriterOptions { Indented = options.WriteIndented });

        WritePrologue();
    }

    /// <summary>The number of rows written so far.</summary>
    public int RowsWritten => _rowsWritten;

    /// <summary>Writes one result row synchronously.</summary>
    /// <param name="record">The current result-set row.</param>
    public void WriteRow(IDataRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureWritable();

        _sink.DeferFlush = false;
        _rowWriter(record, _writer);
        _writer.Flush();
        if (_options.Mode == JsonStreamMode.NdJson)
        {
            _writer.Reset();
            _sink.Write(NewLine);
        }

        _sink.Flush();
        _rowsWritten++;
    }

    /// <summary>Writes one result row and flushes it to the destination asynchronously.</summary>
    /// <param name="record">The current result-set row.</param>
    /// <param name="cancellationToken">A token observed while flushing.</param>
    public async ValueTask WriteRowAsync(IDataRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureWritable();

        // Defer the buffer-full rollover to the awaited FlushAsync below: no synchronous destination
        // write and the cancellation token is observed.
        _sink.DeferFlush = true;
        _rowWriter(record, _writer);
        _writer.Flush();
        if (_options.Mode == JsonStreamMode.NdJson)
        {
            _writer.Reset();
            _sink.Write(NewLine);
        }

        await _sink.FlushAsync(cancellationToken).ConfigureAwait(false);
        _rowsWritten++;
    }

    /// <summary>Closes the JSON container and flushes the remaining bytes synchronously.</summary>
    public void Complete()
    {
        if (_completed)
            return;

        EnsureNotDisposed();
        _completed = true;
        _sink.DeferFlush = false;
        WriteEpilogue();
        _writer.Flush();
        _sink.Flush();
    }

    /// <summary>Closes the JSON container and flushes the remaining bytes asynchronously.</summary>
    /// <param name="cancellationToken">A token observed while flushing.</param>
    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
            return;

        EnsureNotDisposed();
        _completed = true;
        _sink.DeferFlush = true;
        WriteEpilogue();
        _writer.Flush();
        await _sink.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _writer.Dispose();
        _sink.Dispose();
    }

    private void WritePrologue()
    {
        if (_options.Mode != JsonStreamMode.Array)
            return;

        if (_options.Root is not null)
        {
            _writer.WriteStartObject();
            _writer.WritePropertyName(_options.Root);
        }

        _writer.WriteStartArray();
    }

    private void WriteEpilogue()
    {
        if (_options.Mode != JsonStreamMode.Array)
            return;

        _writer.WriteEndArray();
        if (_options.Root is not null)
            _writer.WriteEndObject();
    }

    private void EnsureWritable()
    {
        EnsureNotDisposed();
        if (_completed)
            throw new InvalidOperationException("The JSON stream has already been completed; rows cannot be written after Complete.");
    }

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
