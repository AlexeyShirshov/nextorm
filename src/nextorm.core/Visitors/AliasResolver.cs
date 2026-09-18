using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>Resolves the SQL table alias for a lambda parameter or a projection occurrence.</summary>
internal static class AliasResolver
{
    internal static string? GetAliasFromParam(BaseExpressionVisitor visitor, ParameterExpression lambdaParameter, bool fromProjection)
    {
        var idx = visitor.ColumnsProvider!.FindAlias(lambdaParameter, fromProjection);

        if (!idx.HasValue) return null;

        return visitor.AliasProvider!.FindAlias(idx.Value);
    }

    internal static string? GetAliasFromParam(BaseExpressionVisitor visitor, Type entityType, int? paramIdx, bool fromProjection)
    {
        var idx = visitor.ColumnsProvider!.FindAlias(entityType, paramIdx, fromProjection) ?? throw new InvalidOperationException();

        return visitor.AliasProvider!.FindAlias(idx);
    }
}
