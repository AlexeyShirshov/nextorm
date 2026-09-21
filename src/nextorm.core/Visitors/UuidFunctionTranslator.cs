using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the UUID generator functions of <see cref="CommonFunctions"/>: <c>gen_random_uuid</c>
/// (random v4) and <c>uuidv7</c>.
/// <para>
/// The family is guarded by <see cref="ISqlDialect.UuidGenerators"/> and each generator by
/// <see cref="IUuidGenerators.Supports"/>; a provider that cannot express a generator
/// fails with a clear message instead of emitting invalid SQL.
/// </para>
/// </summary>
internal static class UuidFunctionTranslator
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        nameof(CommonFunctions.gen_random_uuid), nameof(CommonFunctions.uuidv7)
    };

    /// <summary>Translates a UUID generator call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name;

        if (!Names.Contains(name))
            return false;

        var dialect = visitor.Dialect;

        if (dialect.UuidGenerators is not { } uuidGenerators)
            throw new NotSupportedException("The UUID generator functions are not supported by this provider.");

        if (!uuidGenerators.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(uuidGenerators.Render(name));
        }

        return true;
    }
}
