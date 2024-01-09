using System.Runtime.InteropServices;

namespace nextorm.core;

public static class IEqualityComparerExtensions
{
    public static bool Equals<T>(this IEqualityComparer<T> comparer, List<T>? x, List<T>? y)
    {
        if (x is null && y is not null) return false;
        if (x is not null)
        {
            if (y is null) return false;
            else
            {
                if (x.Count != y.Count) return false;
                var xSpan = CollectionsMarshal.AsSpan(x);
                var ySpan = CollectionsMarshal.AsSpan(y);

                for (int i = 0; i < x.Count; i++)
                {
                    if (!comparer.Equals(xSpan[i], ySpan[i])) return false;
                }
            }

            return true;
        }

        return true; // both nulls
    }
    public static bool ValueListEquals<T>(this IValueEqualityComparer<T> comparer, List<T>? x, List<T>? y)
        where T : struct
    {
        if (x is null && y is not null) return false;
        if (x is not null)
        {
            if (y is null) return false;
            else
            {
                if (x.Count != y.Count) return false;
                var xSpan = CollectionsMarshal.AsSpan(x);
                var ySpan = CollectionsMarshal.AsSpan(y);

                for (int i = 0; i < x.Count; i++)
                {
                    if (!comparer.ValueEquals(in xSpan[i], in ySpan[i])) return false;
                }
            }

            return true;
        }

        return true; // both nulls
    }
}