using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>Resolves the SQL table alias for a lambda parameter or a projection occurrence.</summary>
internal static class AliasResolver
{
    internal static string? GetAliasFromParam(BaseExpressionVisitor visitor, ParameterExpression lambdaParameter, bool fromProjection)
    {
        var idx = visitor.ColumnsProvider!.FindAlias(lambdaParameter, fromProjection, includeOuterScopes: false, includeNestedSources: visitor.IncludeNestedSources);

        if (!idx.HasValue) return null;

        return visitor.AliasProvider!.FindAlias(idx.Value);
    }

    /// <summary>
    /// Resolves the alias of an outer-reference parameter. Unlike <see cref="GetAliasFromParam(BaseExpressionVisitor, ParameterExpression, bool)"/>
    /// this deliberately looks past the current subquery's source scope: the parameter belongs to an
    /// enclosing command, whose source entry was added before the current scope began.
    /// </summary>
    internal static string? GetOuterAliasFromParam(BaseExpressionVisitor visitor, ParameterExpression lambdaParameter, bool fromProjection)
    {
        var idx = visitor.ColumnsProvider!.FindAlias(lambdaParameter, fromProjection, includeOuterScopes: true, includeNestedSources: visitor.IncludeNestedSources);

        if (!idx.HasValue) return null;

        return visitor.AliasProvider!.FindAlias(idx.Value);
    }

    internal static string? GetAliasFromParam(BaseExpressionVisitor visitor, Type entityType, int? paramIdx, bool fromProjection)
    {
        var idx = visitor.ColumnsProvider!.FindAlias(entityType, paramIdx, fromProjection, visitor.IncludeNestedSources) ?? throw new InvalidOperationException();

        return visitor.AliasProvider!.FindAlias(idx);
    }
}
