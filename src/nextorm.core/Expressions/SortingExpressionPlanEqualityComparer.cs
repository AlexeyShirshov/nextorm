namespace NextORM.Core;

/// <summary>
/// Compares and hashes <see cref="Sorting"/> values for query-plan caching, delegating the
/// comparison of prepared expressions to the registry's plan comparer.
/// </summary>
/// <param name="queryProvider">The registry that supplies the expression plan comparer.</param>
public sealed class SortingExpressionPlanEqualityComparer(IQueryRegistry queryProvider) : IEqualityComparer<Sorting>, IValueEqualityComparer<Sorting>
{
    private readonly IQueryRegistry _queryProvider = queryProvider;

    /// <summary>Determines whether two sort keys are equal for plan-cache purposes.</summary>
    /// <param name="x">The first sort key.</param>
    /// <param name="y">The second sort key.</param>
    /// <returns><c>true</c> when the direction, column index and prepared expression all match.</returns>
    public bool Equals(Sorting x, Sorting y)
    {
        if (x.Direction != y.Direction) return false;

        if (x.ColumnIndex != y.ColumnIndex) return false;

        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.PreparedExpression, y.PreparedExpression)) return false;

        return true;
    }
    /// <inheritdoc cref="Equals(Sorting, Sorting)"/>
    public bool ValueEquals(in Sorting x, in Sorting y)
    {
        if (x.Direction != y.Direction) return false;

        if (x.ColumnIndex != y.ColumnIndex) return false;

        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.PreparedExpression, y.PreparedExpression)) return false;

        return true;
    }
    /// <summary>Returns a hash code for a sort key consistent with <see cref="Equals(Sorting, Sorting)"/>.</summary>
    /// <param name="obj">The sort key to hash.</param>
    /// <returns>The hash code.</returns>
    public int GetHashCode(Sorting obj)
    {
        unchecked
        {
            var hash = new XxHash32();

            hash.Add(obj.Direction);

            hash.Add(obj.ColumnIndex);

            hash.Add(obj.PreparedExpression, _queryProvider.GetExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
    /// <inheritdoc cref="GetHashCode(Sorting)"/>
    public int GetHashCodeRef(in Sorting obj)
    {
        unchecked
        {
            var hash = new XxHash32();

            hash.Add(obj.Direction);

            hash.Add(obj.ColumnIndex);

            hash.Add(obj.PreparedExpression, _queryProvider.GetExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}

/// <summary>
/// An equality comparer for value types that takes its operands by reference to avoid boxing.
/// </summary>
/// <typeparam name="T">The value type being compared.</typeparam>
public interface IValueEqualityComparer<T>
    where T : struct
{
    /// <summary>Determines whether two values are equal.</summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns><c>true</c> when the values are equal; otherwise <c>false</c>.</returns>
    bool ValueEquals(in T x, in T y);
}