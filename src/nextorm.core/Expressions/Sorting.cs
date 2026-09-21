using System.Linq.Expressions;
namespace NextORM.Core;

/// <summary>
/// A single <c>ORDER BY</c> key: an expression or a resolved column index together with its direction.
/// </summary>
/// <remarks>
/// Was a struct with public mutable fields; <see cref="Direction"/> and <see cref="PreparedExpression"/>
/// are now properties (with private backing fields). See <c>docs/specs/design/API-NAMING-REVIEW.md</c>
/// findings P1-17 and P2-19.
/// </remarks>
public struct Sorting
{
    private readonly Expression? _expression;
    private OrderDirection _direction;
    private Expression? _preparedExpression;
    private readonly int? _columnIdx;

    public Sorting(Expression expression)
    {
        _expression = expression;
    }

    public Sorting(int columnIdx)
    {
        _columnIdx = columnIdx;
    }

    /// <summary>Ascending or descending.</summary>
    public OrderDirection Direction
    {
        readonly get => _direction;
        set => _direction = value;
    }

    /// <summary>
    /// The <c>ORDER BY</c> key after translation (column index, parameter, ...). Populated by the
    /// query preparer; <c>null</c> until then.
    /// </summary>
    public Expression? PreparedExpression
    {
        readonly get => _preparedExpression;
        set => _preparedExpression = value;
    }

    public readonly Expression? SortExpression => _expression;
    public readonly int? ColumnIndex => _columnIdx;
}
