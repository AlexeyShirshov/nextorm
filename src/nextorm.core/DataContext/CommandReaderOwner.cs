using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Owns the per-call <see cref="DbCommand"/> and <see cref="DbDataReader"/> of a raw command. Unlike a
/// prepared query, a raw command is never reused, so this object owns a fresh command and disposes it
/// together with the reader. The underlying connection stays owned by the context and is never closed
/// here.
/// </summary>
/// <remarks>
/// The reader can be closed early (to make ADO.NET output parameters available) while the command is
/// kept alive: <see cref="CloseReader"/> disposes only the reader. <see cref="Dispose"/> and
/// <see cref="DisposeAsync"/> release both, reader first.
/// </remarks>
internal sealed class CommandReaderOwner : IDisposable, IAsyncDisposable
{
    private DbCommand? _command;
    private DbDataReader? _reader;

    internal CommandReaderOwner(DbCommand command, DbDataReader reader)
    {
        _command = command;
        _reader = reader;
    }

    /// <summary>The command this owner created; available until the owner is disposed.</summary>
    internal DbCommand Command => _command ?? throw new ObjectDisposedException(nameof(CommandReaderOwner));

    /// <summary>The reader this owner created; available until the reader is closed or the owner is disposed.</summary>
    internal DbDataReader Reader => _reader ?? throw new ObjectDisposedException(nameof(CommandReaderOwner));

    /// <summary>Whether the reader has already been released (by <see cref="CloseReader"/> or disposal).</summary>
    internal bool IsReaderClosed => _reader is null;

    /// <summary>
    /// Releases the reader but keeps the command, so a provider can populate output/return-value
    /// parameters. The remaining result sets are discarded.
    /// </summary>
    internal void CloseReader()
    {
        var reader = _reader;
        _reader = null;
        reader?.Dispose();
    }

    /// <summary>Asynchronously releases the reader but keeps the command.</summary>
    /// <returns>A value task that completes once the reader has been disposed.</returns>
    internal async ValueTask CloseReaderAsync()
    {
        var reader = _reader;
        _reader = null;
        if (reader is not null)
            await reader.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Disposes the reader (when still open) and then the command. Safe to call more than once.</summary>
    public void Dispose()
    {
        var reader = _reader;
        _reader = null;
        reader?.Dispose();

        var command = _command;
        _command = null;
        command?.Dispose();
    }

    /// <summary>Asynchronously disposes the reader (when still open) and then the command. Safe to call more than once.</summary>
    /// <returns>A value task that completes once both have been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        var reader = _reader;
        _reader = null;
        if (reader is not null)
            await reader.DisposeAsync().ConfigureAwait(false);

        var command = _command;
        _command = null;
        if (command is not null)
            await command.DisposeAsync().ConfigureAwait(false);
    }
}
