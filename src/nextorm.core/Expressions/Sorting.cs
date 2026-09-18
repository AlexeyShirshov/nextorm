using System.Linq.Expressions;
namespace nextorm.core;

/// <summary>
/// A single <c>ORDER BY</c> key: an expression or a resolved column index together with its direction.
/// </summary>
/// <remarks>
/// Exposes public mutable fields (<see cref="Direction"/>, <see cref="PreparedExpression"/>); prefer
/// validated properties. See <c>API-NAMING-REVIEW.md</c> finding P2-19.
/// </remarks>
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