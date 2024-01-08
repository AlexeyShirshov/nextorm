using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
namespace nextorm.core;

public sealed class SortingExpressionPlanEqualityComparer(IQueryProvider queryProvider) : IEqualityComparer<Sorting>
{
    private readonly IQueryProvider _queryProvider = queryProvider;

    public bool Equals(Sorting x, Sorting y)
    {
        if (x.Direction != y.Direction) return false;

        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.PreparedExpression, y.PreparedExpression)) return false;

        return true;
    }
    public int GetHashCode(Sorting obj)
    {
        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.Direction);

            hash.Add(obj.PreparedExpression, _queryProvider.GetExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
    public int GetHashCodeRef(in Sorting obj)
    {
        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.Direction);

            hash.Add(obj.PreparedExpression, _queryProvider.GetExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}