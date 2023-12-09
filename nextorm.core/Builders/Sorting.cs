using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
namespace nextorm.core;
public struct Sorting(Expression expression)
{
    private readonly Expression _expression = expression;
    public OrderDirection Direction;
    public Expression? PreparedExpression;
    public readonly Expression SortExpression => _expression;
}