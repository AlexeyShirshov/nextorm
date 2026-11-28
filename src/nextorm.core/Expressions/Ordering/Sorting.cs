using System.Linq.Expressions;
namespace NextORM.Core;

/// <summary>
/// A single <c>ORDER BY</c> key: an expression or a resolved column index together with its direction.
/// </summary>
public struct Sorting
{
    private readonly Expression? _expression;
    private OrderDirection _direction;
    private Expression? _preparedExpression;
    private readonly int? _columnIdx;
    private NullsPosition _nullsPosition;

    /// <summary>Initializes a sort key from an expression that is translated during preparation.</summary>
    /// <param name="expression">The <c>ORDER BY</c> key expression.</param>
    public Sorting(Expression expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Initializes a sort key from an expression that is translated during preparation, with an explicit
    /// NULL placement.
    /// </summary>
    /// <param name="expression">The <c>ORDER BY</c> key expression.</param>
    /// <param name="nullsPosition">Where SQL <c>NULL</c> values are placed for this key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nullsPosition"/> is not a defined <see cref="NullsPosition"/> member.</exception>
    public Sorting(Expression expression, NullsPosition nullsPosition)
    {
        _expression = expression;
        _nullsPosition = NullsOrderingRenderer.Validate(nullsPosition);
    }

    /// <summary>Initializes a sort key from an already-resolved result-set column index.</summary>
    /// <param name="columnIdx">The zero-based column index.</param>
    public Sorting(int columnIdx)
    {
        _columnIdx = columnIdx;
    }

    /// <summary>
    /// Initializes a sort key from an already-resolved result-set column index, with an explicit NULL
    /// placement.
    /// </summary>
    /// <param name="columnIdx">The zero-based column index.</param>
    /// <param name="nullsPosition">Where SQL <c>NULL</c> values are placed for this key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nullsPosition"/> is not a defined <see cref="NullsPosition"/> member.</exception>
    public Sorting(int columnIdx, NullsPosition nullsPosition)
    {
        _columnIdx = columnIdx;
        _nullsPosition = NullsOrderingRenderer.Validate(nullsPosition);
    }

    internal Sorting(in Sorting original, Expression? sortExpression, Expression? preparedExpression)
    {
        _expression = sortExpression;
        _preparedExpression = preparedExpression;
        _columnIdx = original._columnIdx;
        _direction = original._direction;
        _nullsPosition = original._nullsPosition;
    }

    /// <summary>Ascending or descending.</summary>
    public OrderDirection Direction
    {
        readonly get => _direction;
        set => _direction = value;
    }

    /// <summary>
    /// Where SQL <c>NULL</c> values are placed for this key. <see cref="NullsPosition.Default"/> emits no
    /// clause and keeps the provider default; it is the value produced by the constructor overloads that
    /// do not take a placement.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is not a defined <see cref="NullsPosition"/> member.</exception>
    public NullsPosition NullsPosition
    {
        readonly get => _nullsPosition;
        set => _nullsPosition = NullsOrderingRenderer.Validate(value);
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

    /// <summary>The original <c>ORDER BY</c> expression, or <c>null</c> when the key is a column index.</summary>
    public readonly Expression? SortExpression => _expression;
    /// <summary>The resolved result-set column index, or <c>null</c> when the key is an expression.</summary>
    public readonly int? ColumnIndex => _columnIdx;
}
