using System.Collections.Frozen;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Describes the column accessors of <see cref="TableAlias"/> (all its public instance methods,
/// including the indexer's <c>get_Item</c>). Used by the expression visitor to recognise a table
/// alias accessor and to know whether its column-name argument may be a computed expression
/// (<see cref="TableAlias.GetColumn"/>) instead of a compile-time constant. Keeping the set here
/// removes the method-name <c>switch</c> from the visitor hot path.
/// </summary>
internal static class TableAliasAccessors
{
    private static readonly FrozenSet<string> s_accessors = BuildAccessors();
    private static readonly string s_getColumn = nameof(TableAlias.GetColumn);

    internal static bool IsAccessor(string methodName) => s_accessors.Contains(methodName);

    /// <summary>
    /// True when the accessor accepts a computed (non-constant) column expression — only
    /// <see cref="TableAlias.GetColumn"/>. Every other accessor requires a constant column name.
    /// </summary>
    internal static bool AllowsExpression(string methodName) => methodName == s_getColumn;

    private static FrozenSet<string> BuildAccessors()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in typeof(TableAlias).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            names.Add(method.Name);

        return names.ToFrozenSet(StringComparer.Ordinal);
    }
}
