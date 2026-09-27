using System.Linq.Expressions;

namespace NextORM.Core;

internal sealed class QueryFilterMetadata(string? key, LambdaExpression lambda) : IQueryFilterMetadata
{
    public string? Key { get; } = key;

    public LambdaExpression Lambda { get; } = lambda;
}
