using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// The identifier-rendering identity of a SQL context, as it takes part in a native shape's header.
/// </summary>
/// <remarks>
/// A native plan is keyed by the expression graphs plus the entity/result/context types, so it carries
/// no rendered text. Two contexts of the same provider class can still render different SQL (quoting,
/// naming convention, keyword case), and the normalized plan key distinguishes them only through the
/// SQL it hashes. The stamp restores that dimension for the value-agnostic route without reading any
/// rendered text.
/// </remarks>
internal static class NativeContextStamp
{
    /// <summary>Computes the stamp of <paramref name="context"/>.</summary>
    /// <param name="context">The context the plan will execute on.</param>
    /// <returns>A stable value for one combination of rendering options.</returns>
    internal static int For(IDataContext? context)
    {
        if (context is not DataContext sql)
            return 0;

        var naming = sql.NamingConvention;

        unchecked
        {
            var stamp = sql.QuoteIdentifiers ? 1 : 0;
            stamp = stamp * 31 + (naming is null ? 0 : RuntimeHelpers.GetHashCode(naming));
            stamp = stamp * 31 + (int)sql.KeywordCase;
            return stamp;
        }
    }
}
