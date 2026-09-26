using System.Collections.Concurrent;

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
