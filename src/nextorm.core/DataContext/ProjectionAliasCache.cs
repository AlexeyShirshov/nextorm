using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Caches the per-position occurrence of a projection item's type, keyed by the projection type.
/// <para>
/// Resolving the alias of a projection member (<c>p.Item3</c>) needs to know which occurrence of that
/// entity type the member means when the same type is joined more than once (for example
/// <c>SimpleEntity, ComplexEntity, SimpleEntity, ComplexEntity</c>). That is a pure function of the
/// projection type's generic arguments and the member position, so it is computed once per projection
/// shape instead of once per projected column on every SQL build.
/// </para>
/// </summary>
internal static class ProjectionAliasCache
{
    // -1 encodes "the projection has no repeated type, so no occurrence hint is needed" (the caller
    // passes a null paramIdx, which makes the columns provider return the first match).
    private static readonly ConcurrentDictionary<Type, int[]> _occurrences = new();

    // Resolved slot per member. Reflection over the custom attribute is the expensive step, and the
    // same MemberInfo instance is revisited on every translation of a cached expression, so the answer
    // is memoized. ConditionalWeakTable keeps the map from rooting the member's declaring assembly.
    private static readonly ConditionalWeakTable<MemberInfo, StrongBox<int>> _memberPositions = new();

    /// <summary>
    /// Returns the zero-based projection slot a member addresses. Engine projections keep addressing
    /// their position through the trailing digits of <c>ItemN</c>, so that cheap parse runs first; only
    /// a member that is not an <c>ItemN</c> shape is probed for <see cref="JoinSlotAttribute"/> (the
    /// generated lexical aliases such as <c>Buyer</c> carry no digits). A member with neither yields
    /// <c>-1</c>, which <see cref="GetOccurrence"/> turns into no occurrence hint.
    /// </summary>
    public static int GetMemberPosition(MemberInfo member)
    {
        if (_memberPositions.TryGetValue(member, out var cached))
            return cached.Value;

        return _memberPositions.GetValue(member, static m => new StrongBox<int>(ResolveMemberPosition(m))).Value;
    }

    private static int ResolveMemberPosition(MemberInfo member)
    {
        if (TryParseItemPosition(member.Name, out var position))
            return position - 1;

        if (Attribute.GetCustomAttribute(member, typeof(JoinSlotAttribute)) is JoinSlotAttribute slot)
            return slot.Position - 1;

        // Neither: no occurrence hint. This is a real, stable result for the member, so it is cached.
        return -1;
    }

    /// <summary>
    /// Parses the 1-based position out of an engine projection member name (<c>Item1</c>..<c>Item8</c>):
    /// <c>Item</c> followed by one or more digits. Returns <see langword="false"/> for any other name, so
    /// a generated lexical alias that happens to end in a digit (for example <c>Buyer2</c>) is not
    /// misread as a positional slot before its <see cref="JoinSlotAttribute"/> is consulted.
    /// </summary>
    internal static bool TryParseItemPosition(string name, out int position)
    {
        position = 0;
        if (name.Length <= 4 || !name.StartsWith("Item", StringComparison.Ordinal))
            return false;

        var value = 0;
        for (var i = 4; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsAsciiDigit(c))
                return false;
            value = value * 10 + (c - '0');
        }

        position = value;
        return true;
    }

    /// <summary>
    /// Returns the occurrence index (among earlier items sharing the position's type) to use as the
    /// <c>paramIdx</c> when resolving the alias, or <c>null</c> when the projection has no repeated
    /// types. <paramref name="position"/> is the zero-based projection member index (Item1 = 0).
    /// </summary>
    public static int? GetOccurrence(Type projectionType, int position)
    {
        var map = _occurrences.GetOrAdd(projectionType, BuildMap);
        if ((uint)position >= (uint)map.Length) return null;

        var value = map[position];
        return value < 0 ? null : value;
    }

    private static int[] BuildMap(Type projectionType)
    {
        var args = projectionType.GetGenericArguments();
        var result = new int[args.Length];

        var hasDuplicates = false;
        for (var i = 0; i < args.Length && !hasDuplicates; i++)
        {
            for (var j = i + 1; j < args.Length; j++)
            {
                if (args[i] == args[j])
                {
                    hasDuplicates = true;
                    break;
                }
            }
        }

        if (!hasDuplicates)
        {
            Array.Fill(result, -1);
            return result;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var occurrence = 0;
            for (var j = 0; j < i; j++)
            {
                if (args[j] == args[i]) occurrence++;
            }

            result[i] = occurrence;
        }

        return result;
    }

    public static void Clear() => _occurrences.Clear();
}
