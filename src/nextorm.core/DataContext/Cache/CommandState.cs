using System.Data.Common;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Per-connection mutable execution state of one prepared command. The conditional weak table that
/// owns these instances is keyed by the connection, so a state never needs to hold the connection,
/// the transaction, the context or a reader: doing so would root the key through the value and keep a
/// disposed connection alive for the lifetime of a cached plan.
/// <para>
/// <see cref="Command"/> is <see langword="null"/> under the <see cref="PreparedCommandCache.None"/>
/// policy, which creates a fresh command per execution. Under
/// <see cref="PreparedCommandCache.Connection"/> it is the one command this connection reuses and it
/// stays attached to its own connection between executions: the table is a <i>dependent handle</i>, so
/// a value that references its key does not keep that key alive (AC11 proves the connection is still
/// collected), and detaching would throw away the provider's prepared statement on every execution.
/// </para>
/// <para>
/// The state is deliberately non-generic: nothing in it depends on the projected result type, and a
/// non-generic type lets <see cref="CommandLease"/> stay a non-generic struct (no boxing, no
/// per-execution allocation on the buffered path).
/// </para>
/// </summary>
internal sealed class CommandState
{
    /// <summary>The command reused on this connection, or <see langword="null"/> under <c>None</c>.</summary>
    internal DbCommand? Command;

    /// <summary>Positional runtime-parameter index to collection index, or <see langword="null"/> before the first bind.</summary>
    internal int[]? ParamMap;

    /// <summary>Plan-parameter index to collection index, or <see langword="null"/> before the first refresh.</summary>
    internal int[]? PlanParamMap;

    /// <summary>The lease token currently owning this state, or <c>0</c> when no execution owns it.</summary>
    internal int LeaseOwner;

    /// <summary>
    /// Capacity hint for the next buffered materialization on this connection: the number of rows the
    /// previous <c>ToList</c> read returned. It is a <b>hint only</b> — never a row-count contract, never
    /// exposed on any public surface, never read by a caller and never used for correctness (a stale
    /// value merely chooses a different initial <see cref="List{T}"/> capacity; the list still grows to
    /// fit). It exists because the legacy buffered path pre-sized its list from the previous row count
    /// and the default <see cref="List{T}"/> capacity of four costs one redundant array enlargement for
    /// small result sets.
    /// </summary>
    internal int RowCapacityHint;

    /// <summary>Monotonic lease token source; tokens are never <c>0</c> so <c>0</c> can mean "free".</summary>
    internal int NextLeaseToken;

    /// <summary>Releases the lease when <paramref name="token"/> is still the owner (a stale release is a no-op).</summary>
    internal void Release(int token) => Interlocked.CompareExchange(ref LeaseOwner, 0, token);
}
