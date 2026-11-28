using System.Runtime.CompilerServices;

namespace NextORM.Core;

internal static class PlanDialectIdentity
{
    private static readonly ConditionalWeakTable<ISqlDialect, object> Identities = new();

    // Built-in dialect constructors register an immutable description of the effective configuration.
    internal static void Register(ISqlDialect dialect, object configuration) => Identities.Add(dialect, configuration);

    // An extension dialect has no structural compatibility contract. Its token must not keep it alive.
    internal static object? For(IDataContext? context)
    {
        if (context is not DataContext sql)
            return null;

        return Identities.GetValue(sql.Dialect, static _ => new object());
    }
}
