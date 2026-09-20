using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the session/information functions of <see cref="CommonFunctions"/>: <c>current_user</c>,
/// <c>session_user</c>, <c>current_schema</c>, <c>current_database</c> and <c>version</c>.
/// <para>
/// The family is guarded by <see cref="ISqlDialect.SupportsSessionInfoFunctions"/> and each function by
/// <see cref="ISqlDialect.SupportsSessionInfoFunction(string)"/>; a provider that cannot express a
/// function fails with a clear message instead of emitting invalid SQL.
/// </para>
/// </summary>
internal static class SessionInfoFunctionTranslator
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        nameof(CommonFunctions.current_user), nameof(CommonFunctions.session_user),
        nameof(CommonFunctions.current_schema), nameof(CommonFunctions.current_database),
        nameof(CommonFunctions.version)
    };

    /// <summary>Translates a session/information call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name;

        if (!Names.Contains(name))
            return false;

        var dialect = visitor.Dialect;

        if (!dialect.SupportsSessionInfoFunctions)
            throw new NotSupportedException("The session information functions are not supported by this provider.");

        if (!dialect.SupportsSessionInfoFunction(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(dialect.MakeSessionInfoFunction(name));
        }

        return true;
    }
}
