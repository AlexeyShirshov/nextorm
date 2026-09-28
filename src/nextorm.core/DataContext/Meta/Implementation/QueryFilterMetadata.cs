using System.Linq.Expressions;

namespace NextORM.Core;

internal sealed class QueryFilterMetadata(string? key, LambdaExpression? lambda, Delegate? func = null) : IQueryFilterMetadata
{
    public string Key { get; } = string.IsNullOrEmpty(key) ? QueryFilters.AnonymousKey : key;

    public LambdaExpression? Lambda { get; } = lambda;

    public Delegate? Func { get; } = func;
}
