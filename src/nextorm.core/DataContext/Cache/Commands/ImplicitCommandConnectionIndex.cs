using System.Data.Common;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

// Connection cleanup follows the exact connection, not the current thread or L1 generation.
// Weak keys in both directions let either the connection or an evicted plan die independently.
internal static class ImplicitCommandConnectionIndex
{
    private static readonly object Registered = new();
    private static readonly ConditionalWeakTable<DbConnection, ConditionalWeakTable<IDbCommandHolder, object>> Holders = new();

    internal static void Register(DbConnection connection, IDbCommandHolder holder)
        => Holders.GetValue(connection, static _ => new()).TryAdd(holder, Registered);

    internal static void Forget(DbConnection connection, IDataContext owner)
    {
        if (!Holders.TryGetValue(connection, out var holders))
        {
            return;
        }

        // Releasing a connection must not overlap an execution on that connection.
        Holders.Remove(connection);
        foreach (var entry in holders)
        {
            entry.Key.ResetConnection(connection, owner);
        }
    }
}
