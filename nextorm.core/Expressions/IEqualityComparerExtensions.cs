using System.Runtime.InteropServices;

namespace nextorm.core;

public static class IEqualityComparerExtensions
{
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
    // public static bool Equals<T>(this IEqualityComparer<T> comparer, List<T>? x, List<T>? y)
    // {
    //     if (x is null && y is not null) return false;
    //     if (x is not null)
    //     {
    //         if (y is null) return false;
    //         else
    //         {
    //             var xCount = x.Count;
    //             if (xCount != y.Count) return false;
    //             if (xCount == 0) return true;

    //             var xSpan = CollectionsMarshal.AsSpan(x);
    //             var ySpan = CollectionsMarshal.AsSpan(y);

    //             for (int i = 0; i < xCount; i++)
    //             {
    //                 if (!comparer.Equals(xSpan[i], ySpan[i])) return false;
    //             }
    //         }

    //         return true;
    //     }

    //     return true; // both nulls
    // }
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