namespace NextORM.Core;

/// <summary>
/// Context defaults that affect native rendering or the resulting command.
/// </summary>
/// <remarks>
/// A native plan is keyed by the expression graphs plus the entity/result/context types, so it carries
/// no rendered text. Two contexts of the same provider class can still render different SQL (quoting,
/// naming convention, keyword case), and the normalized plan key distinguishes them only through the
/// SQL it hashes. The stamp restores that dimension for the value-agnostic route without reading any
/// rendered text.
/// </remarks>
internal readonly record struct NativeContextStamp(
    bool QuoteIdentifiers, object? NamingIdentity, KeywordCase KeywordCase, int? CommandTimeout,
    PlanMappingIdentity Mapping = default, object? Dialect = null)
{
    /// <summary>Computes the stamp of <paramref name="context"/>.</summary>
    /// <param name="context">The context the plan will execute on.</param>
    /// <returns>A stable value for one combination of rendering options.</returns>
    internal static NativeContextStamp For(IDataContext? context)
    {
        if (context is not DataContext sql)
            return default;

        return new NativeContextStamp(sql.QuoteIdentifiers, PlanObjectIdentity.ForNamingConvention(sql.NamingConvention),
            sql.KeywordCase, sql.CommandTimeout,
            PlanMappingIdentity.For(sql), PlanDialectIdentity.For(sql));
    }
}
