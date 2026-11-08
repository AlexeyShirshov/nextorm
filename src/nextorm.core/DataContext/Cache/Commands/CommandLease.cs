using System.Data.Common;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// One execution's exclusive hold on a <see cref="CommandState"/> and on the command that
/// execution is allowed to use.
/// <para>
/// A lease is a <b>struct</b> owned by value: acquiring it on the buffered execution paths (and on the
/// streaming ones, where the enumerator and the reader already allocate) adds no allocation and no
/// boxing. Ownership transfer is a value copy — the reader owner / enumerator receives that single copy
/// and disposes it exactly once; the acquiring frame drops its own copy without touching it.
/// </para>
/// <para>
/// Acquisition is CAS-only — there is no monitor, lock or semaphore anywhere on the prepared object —
/// so two connections never serialize and overlapping executions on the <i>same</i> connection fail
/// promptly instead of interleaving on one command.
/// </para>
/// <para>
/// Ownership of the command is policy-driven: under <see cref="PreparedCommandCache.None"/> the lease
/// owns the fresh command and disposes it on release; under
/// <see cref="PreparedCommandCache.Connection"/> the command belongs to the per-connection state and
/// the lease only clears its transaction before releasing the state. The command keeps its
/// <see cref="DbCommand.Connection"/> between executions: clearing it makes a provider
/// (Microsoft.Data.Sqlite) dispose its prepared statement, so every execution would re-prepare the
/// SQL. The no-root guarantee does not depend on the detach — the state is reachable only through the
/// dependent-handle table keyed by that same connection and through a weak-only memo.
/// </para>
/// </summary>
internal struct CommandLease
{
    private readonly IDbCommandHolder _owner;
    private readonly DbConnection _connection;
    private readonly CommandState _state;
    private readonly int _token;
    private readonly bool _ownsCommand;
    private DbCommand? _command;
    private bool _commandOwnedByLease;
    private int _done;

    internal CommandLease(
        IDbCommandHolder owner,
        DbConnection connection,
        CommandState state,
        int token,
        bool ownsCommand)
    {
        _owner = owner;
        _connection = connection;
        _state = state;
        _token = token;
        _ownsCommand = ownsCommand;
        _commandOwnedByLease = ownsCommand;
        _command = null;
        _done = 0;
        CreatedCommand = false;
    }

    /// <summary>The command this execution may use; valid until the lease is disposed.</summary>
    internal readonly DbCommand Command => _command ?? throw new ObjectDisposedException(nameof(CommandLease));

    /// <summary>Whether the command is owned by this execution (<c>None</c>) rather than by the connection state.</summary>
    internal readonly bool OwnsCommand => _ownsCommand;

    /// <summary>Whether this execution created the command (the caller raises the initialized event once).</summary>
    internal bool CreatedCommand { get; private set; }

    /// <summary>
    /// Capacity hint for the buffered list this execution is about to materialize, taken from the
    /// connection state's previous row count. Hint only: it never constrains how many rows are read.
    /// </summary>
    internal readonly int RowCapacityHint => _state.RowCapacityHint;

    /// <summary>
    /// Records the row count of a completed buffered materialization as the next capacity hint. Callers
    /// pass a positive count only: an empty result keeps the previous hint, so a single empty read does
    /// not reset the following materialization to the default capacity.
    /// </summary>
    /// <param name="count">Rows the completed read returned; positive.</param>
    internal void SetRowCapacityHint(int count) => _state.RowCapacityHint = count;

    /// <summary>Attaches the command this execution uses.</summary>
    internal void SetCommand(DbCommand command, bool created)
    {
        _command = command;
        CreatedCommand = created;
    }

    /// <summary>
    /// Hands command disposal to the caller (a <c>None</c> streaming enumerator): the lease keeps only
    /// the state release, so the command is never disposed twice. A struct method mutates this copy, so
    /// a holder must write the mutated copy back (see <c>ResultSetEnumerator.OwnCommand</c>).
    /// </summary>
    internal void ReleaseCommandOwnership() => _commandOwnedByLease = false;

    /// <summary>
    /// Ends a failed execution before ownership transfer: a <c>None</c> command is disposed, a
    /// <c>Connection</c> command is disposed and evicted from the state so a poisoned command can
    /// never be published for reuse. The lease is released exactly once either way.
    /// </summary>
    internal void Fail()
    {
        if (Interlocked.Exchange(ref _done, 1) != 0)
            return;

        try
        {
            if (!_ownsCommand)
            {
                _owner.EvictConnection(_connection);
            }
            else if (_commandOwnedByLease)
            {
                var command = _command;
                _command = null;
                if (command is not null)
                {
                    command.Connection = null;
                    command.Transaction = null;
                    command.Dispose();
                    _owner.OnCommandDisposed();
                }
            }
            // Otherwise the command's ownership was handed to a streaming enumerator
            // (ReleaseCommandOwnership): that owner disposes it, and the lease must not touch it again.
        }
        finally
        {
            _command = null;
            _state.Release(_token);
        }
    }

    /// <summary>Completes a successful execution: disposes an owned command or detaches a reused one, then releases the state.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _done, 1) != 0)
            return;

        try
        {
            var command = _command;
            _command = null;

            if (command is null)
                return;

            if (!_ownsCommand)
            {
                // The command stays attached to its own connection between executions: the state holds
                // it, the table is keyed by that connection (dependent handle — a value referring to its
                // key roots nothing) and the memo is weak-only. Clearing Connection here would call the
                // provider's Connection setter, which disposes the prepared statement and forces a full
                // re-prepare on the next execution (~3 us/query on SQLite).
                if (command.Transaction is not null)
                    command.Transaction = null;
            }
            else if (_commandOwnedByLease)
            {
                command.Connection = null;
                command.Transaction = null;
                command.Dispose();
                _owner.OnCommandDisposed();
            }
            // Otherwise a streaming enumerator owns the command (ReleaseCommandOwnership): it disposes
            // it, and a disposed provider command must never be touched again — Npgsql throws
            // ObjectDisposedException from set_DbConnection, so this branch has no body by design.
        }
        finally
        {
            _state.Release(_token);
        }
    }
}
