using System.Linq.Expressions;
namespace nextorm.core;
public struct Sorting
{
    private readonly Expression? _expression;
    public OrderDirection Direction;
    public Expression? PreparedExpression;
    private readonly int? _columnIdx;

    public Sorting(Expression expression)
    {
        _expression = expression;
    }

    public Sorting(int columnIdx)
    {
        _columnIdx = columnIdx;
    }

    public readonly Expression? SortExpression => _expression;
    public readonly int? ColumnIndex => _columnIdx;
}