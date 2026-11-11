using System.Runtime.InteropServices;

namespace NextORM.Core;

/// <summary>
/// Extension methods that compare collections element-wise with an <see cref="IEqualityComparer{T}"/>
/// or <see cref="IValueEqualityComparer{T}"/>.
/// </summary>
public static class IEqualityComparerExtensions
{
    /// <summary>Determines whether two arrays contain the same elements, using the comparer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="comparer">The element comparer.</param>
    /// <param name="x">The first array, or <c>null</c>.</param>
    /// <param name="y">The second array, or <c>null</c>.</param>
    /// <returns><c>true</c> when both are <c>null</c>, or both have equal length and equal elements.</returns>
    public static bool Equals<T>(this IEqualityComparer<T> comparer, T[]? x, T[]? y)
        where T : class
    {
        if (x is null && y is not null) return false;
        if (x is not null)
        {
            if (y is null) return false;
            else
            {
                var xCount = x.Length;
                if (xCount != y.Length) return false;

                for (int i = 0; i < xCount; i++)
                {
                    if (x[i] == y[i]) continue;
                    if (!comparer.Equals(x[i], y[i])) return false;
                }
            }

            return true;
        }

        return true; // both nulls
    }
    /// <summary>Determines whether two lists contain the same elements, using the comparer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="comparer">The element comparer.</param>
    /// <param name="x">The first list, or <c>null</c>.</param>
    /// <param name="y">The second list, or <c>null</c>.</param>
    /// <returns><c>true</c> when both are <c>null</c>, or both have equal count and equal elements.</returns>
    public static bool Equals<T>(this IEqualityComparer<T> comparer, IReadOnlyList<T>? x, IReadOnlyList<T>? y)
    {
        if (x is null && y is not null) return false;
        if (x is not null)
        {
            if (y is null) return false;
            else
            {
                var xCount = x.Count;
                if (xCount != y.Count) return false;
                if (xCount == 0) return true;

                // var xSpan = CollectionsMarshal.AsSpan(x);
                // var ySpan = CollectionsMarshal.AsSpan(y);

                for (int i = 0; i < xCount; i++)
                {
                    if (!comparer.Equals(x[i], y[i])) return false;
                }
            }

            return true;
        }

        return true; // both nulls
    }
    /// <summary>Determines whether two arrays contain the same value elements, using the by-reference comparer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="comparer">The value comparer.</param>
    /// <param name="x">The first array, or <c>null</c>.</param>
    /// <param name="y">The second array, or <c>null</c>.</param>
    /// <returns><c>true</c> when both are <c>null</c>, or both have equal length and equal elements.</returns>
    public static bool ValueEquals<T>(this IValueEqualityComparer<T> comparer, T[]? x, T[]? y)
        where T : struct
    {
        if (x is null && y is not null) return false;
        if (x is not null)
        {
            if (y is null) return false;
            else
            {
                var xCount = x.Length;
                if (xCount != y.Length) return false;
                if (xCount == 0) return true;

                var xSpan = x.AsSpan();
                var ySpan = y.AsSpan();

                for (int i = 0; i < xCount; i++)
                {
                    if (!comparer.ValueEquals(in xSpan[i], in ySpan[i])) return false;
                }
            }

            return true;
        }

        return true; // both nulls
    }
}