using System.Collections.Frozen;
using System.Linq.Expressions;
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
    private static readonly FrozenSet<string> s_streamingAccessors = BuildStreamingAccessors();
    private static readonly FrozenSet<string> s_streamingMembers = BuildStreamingMembers();

    internal static bool IsAccessor(string methodName) => s_accessors.Contains(methodName);

    /// <summary>
    /// True when the <see cref="TableAlias"/> accessor selects a streaming LOB column
    /// (<see cref="TableAlias.GetStream"/> or <see cref="TableAlias.GetTextReader"/>).
    /// </summary>
    internal static bool IsStreamingAccessor(string methodName) => s_streamingAccessors.Contains(methodName);

    /// <summary>
    /// True when <paramref name="expression"/> selects a streaming LOB column — either a
    /// <see cref="TableAlias"/> method call (<c>GetStream</c>/<c>GetTextReader</c>) or a
    /// <see cref="TableColumn"/> streaming member (<c>AsStream</c>/<c>AsTextReader</c>). Used by the
    /// projection builder to mark the resulting <see cref="SelectExpression"/>, and by the row mapper
    /// to reject buffered materialization of a streaming column.
    /// </summary>
    internal static bool IsStreaming(Expression expression)
    {
        switch (TypeFacts.UnwrapConvert(expression))
        {
            case MethodCallExpression call:
                return call.Method.DeclaringType == typeof(TableAlias) && IsStreamingAccessor(call.Method.Name);
            case MemberExpression member:
                return member.Member.DeclaringType == typeof(TableColumn) && s_streamingMembers.Contains(member.Member.Name);
            default:
                return false;
        }
    }

    private static FrozenSet<string> BuildStreamingAccessors()
        => new[] { nameof(TableAlias.GetStream), nameof(TableAlias.GetTextReader) }.ToFrozenSet(StringComparer.Ordinal);

    private static FrozenSet<string> BuildStreamingMembers()
        => new[] { nameof(TableColumn.AsStream), nameof(TableColumn.AsTextReader) }.ToFrozenSet(StringComparer.Ordinal);

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
