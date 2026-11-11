using System.Data.Common;

namespace NextORM.Core;

internal interface IDbCommandHolder
{
    void ResetConnection(DbConnection conn, IDataContext dbContext);

    /// <summary>
    /// Evicts this connection's execution state after a failed execution. The context-scoped
    /// <see cref="ResetConnection"/> needs a context only for its lifecycle subscription; a failed
    /// lease has none, so it evicts through this member.
    /// </summary>
    void EvictConnection(DbConnection conn);

    /// <summary>Records the disposal of an execution-owned (<c>None</c>) command (diagnostic counters).</summary>
    void OnCommandDisposed();
}
