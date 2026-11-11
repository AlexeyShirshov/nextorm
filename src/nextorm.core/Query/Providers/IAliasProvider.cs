using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Allocates and resolves the table aliases (<c>t1</c>, <c>t2</c>, ...) of the sources in a query.
/// </summary>
public interface IAliasProvider
{
    /// <summary>Returns the alias registered at the zero-based <paramref name="idx"/>, or <c>null</c> when there is none.</summary>
    /// <param name="idx">The zero-based source index.</param>
    string? FindAlias(int idx);
    /// <summary>Allocates the next alias for <paramref name="from"/> and records the source.</summary>
    /// <param name="from">The FROM source to register.</param>
    string GetNextAlias(FromExpression from);
    /// <summary>Allocates the next alias for the nested <paramref name="queryCommand"/> and records the source.</summary>
    /// <param name="queryCommand">The nested query to register.</param>
    string GetNextAlias(QueryCommand queryCommand);
}
