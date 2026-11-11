using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Owns the <see cref="DbCommand"/> and <see cref="DbDataReader"/> of one execution. A raw command is
/// never reused, so the owner disposes its fresh command; a prepared command carries a
/// <see cref="CommandLease"/> instead, which disposes a <c>None</c> command or detaches a
/// <c>Connection</c> command and releases the per-connection state. The underlying connection stays
/// owned by the context and is never closed here.
/// </summary>
/// <remarks>
/// The reader can be closed early (to make ADO.NET output parameters available) while the command is
/// kept alive: <see cref="CloseReader"/> disposes only the reader. <see cref="Dispose"/> and
/// <see cref="DisposeAsync"/> release the reader, then the command/lease, in a <c>finally</c> so a
/// throwing reader disposal can never leak the lease.
/// </remarks>
internal sealed class CommandReaderOwner : IDisposable, IAsyncDisposable
{
    private DbCommand? _command;
    private DbDataReader? _reader;
    private CommandLease? _lease;
    private int _recordsAffected = -1;
    private bool _recordsAffectedCaptured;

    internal CommandReaderOwner(DbCommand command, DbDataReader reader, CommandLease? lease)
    {
        _command = command;
        _reader = reader;
        _lease = lease;
    }

    /// <summary>The command this owner created; available until the owner is disposed.</summary>
    internal DbCommand Command => _command ?? throw new ObjectDisposedException(nameof(CommandReaderOwner));

    /// <summary>The reader this owner created; available until the reader is closed or the owner is disposed.</summary>
    internal DbDataReader Reader => _reader ?? throw new ObjectDisposedException(nameof(CommandReaderOwner));

    /// <summary>Whether the reader has already been released (by <see cref="CloseReader"/> or disposal).</summary>
    internal bool IsReaderClosed => _reader is null;

    /// <summary>
    /// The number of rows the command's DML statements affected, as reported by the provider, or
    /// <c>-1</c> while the reader is still open or when the command has no affected-row metadata
    /// (a query). Captured once, when the reader is released (by <see cref="CloseReader"/>,
    /// <see cref="Dispose"/> or <see cref="DisposeAsync"/>): the pre-release value is published first
    /// and the post-release value is preferred when the provider only finalizes it on Close. The
    /// published value is never reset by a repeated release, and reading this property never touches a
    /// released reader.
    /// </summary>
    internal int RecordsAffected => _recordsAffected;

    /// <summary>
    /// Reads a provider's affected-row total, treating metadata that is not usable in the reader's
    /// current state (<see cref="NotSupportedException"/>, <see cref="InvalidOperationException"/> or
    /// <see cref="ObjectDisposedException"/>) as unavailable (<c>-1</c>). A cleanup error is never
    /// swallowed: this helper only reads metadata and never releases a resource. Shared with the batch
    /// runner so both capture paths read the driver the same way.
    /// </summary>
    /// <param name="reader">The reader whose affected-row total is read.</param>
    /// <returns>The reported total, or <c>-1</c> when the metadata is unavailable.</returns>
    internal static int ReadRecordsAffected(DbDataReader reader)
    {
        try
        {
            return reader.RecordsAffected;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ObjectDisposedException)
        {
            return -1;
        }
    }

    // The pre-release snapshot is published first, so a provider whose cleanup throws cannot lose the
    // value; the post-release value then upgrades it once, because some providers only finalize the
    // total on Close. Publishing is guarded, so a repeated release (which has no reader left) cannot
    // reset a value a caller has already observed.
    private void PublishBeforeRelease(DbDataReader reader)
    {
        if (_recordsAffectedCaptured)
            return;

        _recordsAffectedCaptured = true;
        _recordsAffected = ReadRecordsAffected(reader);
    }

    private void PublishAfterRelease(DbDataReader reader)
    {
        if (!_recordsAffectedCaptured)
            return;

        var afterRelease = ReadRecordsAffected(reader);
        if (afterRelease != -1)
            _recordsAffected = afterRelease;
    }

    /// <summary>
    /// Releases the reader but keeps the command, so a provider can populate output/return-value
    /// parameters. The remaining result sets are discarded.
    /// </summary>
    internal void CloseReader()
    {
        var reader = _reader;
        _reader = null;
        if (reader is null)
            return;

        PublishBeforeRelease(reader);
        reader.Dispose();
        PublishAfterRelease(reader);
    }

    /// <summary>Disposes the reader (when still open), then the command or the lease. Safe to call more than once.</summary>
    public void Dispose()
    {
        var reader = _reader;
        _reader = null;
        try
        {
            if (reader is not null)
            {
                PublishBeforeRelease(reader);
                reader.Dispose();
                PublishAfterRelease(reader);
            }
        }
        finally
        {
            var command = _command;
            _command = null;
            var lease = _lease;
            _lease = null;
            try
            {
                // A raw command has no lease and is owned here; a prepared command is owned by its lease.
                if (lease is null)
                    command?.Dispose();
            }
            finally
            {
                lease?.Dispose();
            }
        }
    }

    /// <summary>Asynchronously disposes the reader (when still open), then the command or the lease. Safe to call more than once.</summary>
    /// <returns>A value task that completes once the reader and the command/lease have been released.</returns>
    public async ValueTask DisposeAsync()
    {
        var reader = _reader;
        _reader = null;
        try
        {
            if (reader is not null)
            {
                PublishBeforeRelease(reader);
                await reader.DisposeAsync().ConfigureAwait(false);
                PublishAfterRelease(reader);
            }
        }
        finally
        {
            var command = _command;
            _command = null;
            var lease = _lease;
            _lease = null;
            try
            {
                if (lease is null && command is not null)
                    await command.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                lease?.Dispose();
            }
        }
    }
}
