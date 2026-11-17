using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// An expression for a window function before its <c>OVER</c> specification is supplied. Window-function
/// methods on <see cref="CommonFunctions"/> return this wrapper; call <see cref="Over(Expression{Func{object?}}?, Expression{Func{object?}}?, WindowFrame?)"/>
/// to add partition, ordering, and frame clauses and get the value type read from the result set.
/// <para>
/// The wrapper builds an expression tree; its members are not executed. The nested
/// <c>partitionBy</c>/<c>orderBy</c> lambdas close over the outer query parameter. For example:
/// <c>SqlFunctions.Sql.row_number().Over(partitionBy: () =&gt; e.Int, orderBy: () =&gt; e.Id)</c>.
/// </para>
/// </summary>
public sealed class WindowFunction<T>
{
    /// <summary>
    /// Completes the window function with an optional partition expression, ascending order expression,
    /// and frame. Calling <c>Over()</c> renders an empty specification (<c>OVER ()</c>).
    /// <para>
    /// To specify ordering without a partition, use an overload that accepts <see cref="WindowOrder"/>.
    /// C# expression trees do not allow you to skip the preceding <c>partitionBy</c> argument while
    /// naming <c>orderBy</c>.
    /// </para>
    /// </summary>
    public T Over(
        Expression<Func<object?>>? partitionBy = null,
        Expression<Func<object?>>? orderBy = null,
        WindowFrame? frame = null) => default!;

    /// <summary>
    /// Completes the window function with a single partition expression and a single ordered key
    /// (<c>asc</c>/<c>desc</c>), plus an optional frame. This is the scalar counterpart of the array
    /// overload below for callers that do not build arrays.
    /// <para>
    /// This overload has lower resolution priority so an untyped <c>default</c> or <c>null</c> for
    /// <c>orderBy</c> continues to bind to the existing
    /// expression-based overload instead of becoming ambiguous.
    /// </para>
    /// </summary>
    /// <param name="partitionBy">The partition key expression (a lambda closing over the outer query parameter), or <c>null</c> for no <c>PARTITION BY</c>.</param>
    /// <param name="orderBy">The single ordered key of the window.</param>
    /// <param name="frame">An optional <c>ROWS</c>/<c>RANGE</c>/<c>GROUPS</c> frame.</param>
    [OverloadResolutionPriority(-1)]
    public T Over(
        Expression<Func<object?>>? partitionBy,
        WindowOrder orderBy,
        WindowFrame? frame = null) => default!;

    /// <summary>Completes the window function with a single ordered key and an optional frame.</summary>
    public T Over(WindowOrder orderBy, WindowFrame? frame = null) => default!;

    /// <summary>
    /// Completes the window function by referencing a named window declared on the query with
    /// <c>EntityBuilder.Window(...)</c> (rendered as <c>OVER windowName</c>). Requires a dialect that
    /// supports the <c>WINDOW</c> clause (see <see cref="ISqlDialect.SupportsNamedWindows"/>).
    /// </summary>
    /// <param name="windowName">The name of a window declared on the same query.</param>
    public T Over(string windowName) => default!;

    /// <summary>
    /// Completes the window function with multiple ordered keys. Use this for mixed <c>asc</c>/<c>desc</c>
    /// ordering or more than one key. To supply partition keys as well, use
    /// <see cref="Over(Expression{Func{object?}}[]?, WindowOrder[]?, WindowFrame?)"/>.
    /// </summary>
    public T Over(WindowOrder[] orderBy, WindowFrame? frame = null) => default!;

    /// <summary>
    /// Completes the window function with multiple partition expressions and/or ordered keys. Use
    /// <see cref="CommonFunctions.asc(Expression{Func{object?}})"/> and
    /// <see cref="CommonFunctions.desc(Expression{Func{object?}})"/> to mix ordering directions.
    /// <paramref name="partitionBy"/> is required here so <c>Over()</c> remains unambiguous; pass
    /// <see langword="null"/> when no partition is needed.
    /// </summary>
    public T Over(
        Expression<Func<object?>>[]? partitionBy,
        WindowOrder[]? orderBy = null,
        WindowFrame? frame = null) => default!;
}

/// <summary>An <c>ORDER BY</c> expression of a window specification together with its direction.</summary>
public sealed class WindowOrder
{
    internal WindowOrder(Expression<Func<object?>> expression, OrderDirection direction)
    {
        Expression = expression;
        Direction = direction;
    }

    /// <summary>The order key expression (a lambda closing over the outer query parameter).</summary>
    public Expression<Func<object?>> Expression { get; }

    /// <summary>Ascending or descending.</summary>
    public OrderDirection Direction { get; }
}

/// <summary>Which SQL frame unit a <see cref="WindowFrame"/> uses.</summary>
public enum WindowFrameType
{
    /// <summary>The <c>ROWS</c> unit: offsets count physical rows.</summary>
    Rows,
    /// <summary>The <c>RANGE</c> unit: offsets count ordering values rather than physical rows.</summary>
    Range,
    /// <summary>
    /// The <c>GROUPS</c> unit: an offset counts whole peer groups (rows equal on the window's
    /// <c>ORDER BY</c> key) instead of physical rows (<c>ROWS</c>) or ordering values (<c>RANGE</c>).
    /// </summary>
    Groups
}

/// <summary>
/// A frame exclusion (<c>EXCLUDE ...</c>) that removes rows around the current row from the frame even
/// when they fall inside the frame boundaries.
/// </summary>
public enum WindowFrameExclusion
{
    /// <summary>Keep the current row and its peers (<c>EXCLUDE NO OTHERS</c>), the default.</summary>
    NoOthers,
    /// <summary>Remove the current row from the frame (<c>EXCLUDE CURRENT ROW</c>).</summary>
    CurrentRow,
    /// <summary>Remove the current row and its ordering peers (<c>EXCLUDE GROUP</c>).</summary>
    Group,
    /// <summary>Remove the current row's ordering peers but keep the current row (<c>EXCLUDE TIES</c>).</summary>
    Ties
}

/// <summary>The kind of a single frame boundary.</summary>
public enum WindowFrameBoundKind
{
    /// <summary>From the start of the partition, regardless of offset.</summary>
    UnboundedPreceding,
    /// <summary>An offset before the current row, measured in the frame's unit.</summary>
    Preceding,
    /// <summary>The current row.</summary>
    CurrentRow,
    /// <summary>An offset after the current row, measured in the frame's unit.</summary>
    Following,
    /// <summary>To the end of the partition, regardless of offset.</summary>
    UnboundedFollowing
}

/// <summary>A frame boundary, such as <c>1 PRECEDING</c>.</summary>
public sealed class WindowFrameBound
{
    private WindowFrameBound(WindowFrameBoundKind kind, int offset)
    {
        Kind = kind;
        Offset = offset;
    }

    /// <summary>Boundary kind.</summary>
    public WindowFrameBoundKind Kind { get; }

    /// <summary>The offset of a <c>preceding</c>/<c>following</c> boundary; zero otherwise.</summary>
    public int Offset { get; }

    /// <summary>A boundary at the start of the partition.</summary>
    public static WindowFrameBound UnboundedPreceding => new(WindowFrameBoundKind.UnboundedPreceding, 0);
    /// <summary>A boundary at the current row.</summary>
    public static WindowFrameBound CurrentRow => new(WindowFrameBoundKind.CurrentRow, 0);
    /// <summary>A boundary at the end of the partition.</summary>
    public static WindowFrameBound UnboundedFollowing => new(WindowFrameBoundKind.UnboundedFollowing, 0);

    /// <summary>
    /// Creates a boundary <paramref name="rows"/> offset units before the current row. The frame type determines
    /// whether the units are rows, ordering values, or peer groups.
    /// </summary>
    public static WindowFrameBound Preceding(int rows) => new(WindowFrameBoundKind.Preceding, rows);

    /// <summary>
    /// Creates a boundary <paramref name="rows"/> offset units after the current row. The frame type determines
    /// whether the units are rows, ordering values, or peer groups.
    /// </summary>
    public static WindowFrameBound Following(int rows) => new(WindowFrameBoundKind.Following, rows);
}

/// <summary>
/// The optional <c>ROWS</c>/<c>RANGE</c>/<c>GROUPS BETWEEN ... AND ...</c> frame of a window
/// specification, with an optional <c>EXCLUDE</c>. Not every provider can express every unit or an
/// exclusion; the translator rejects an unsupported combination through the dialect capability flags.
/// </summary>
public sealed class WindowFrame
{
    private WindowFrame(WindowFrameType type, WindowFrameBound start, WindowFrameBound end, WindowFrameExclusion? exclusion)
    {
        Type = type;
        Start = start;
        End = end;
        Exclusion = exclusion;
    }

    /// <summary>Frame unit.</summary>
    public WindowFrameType Type { get; }

    /// <summary>Lower (or, for a reversed frame, first written) boundary.</summary>
    public WindowFrameBound Start { get; }

    /// <summary>Upper (or, for a reversed frame, last written) boundary.</summary>
    public WindowFrameBound End { get; }

    /// <summary>
    /// The frame exclusion, or <c>null</c> when none was requested (the default
    /// <c>EXCLUDE NO OTHERS</c>). A dialect that cannot render an exclusion rejects a frame that sets it
    /// (see <see cref="ISqlDialect.SupportsWindowFrameExclusion"/>).
    /// </summary>
    public WindowFrameExclusion? Exclusion { get; }

    /// <summary>Creates a <c>ROWS BETWEEN</c> frame with explicit boundaries.</summary>
    public static WindowFrame Rows(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Rows, start, end, null);

    /// <summary>Creates a <c>RANGE BETWEEN</c> frame with explicit boundaries.</summary>
    public static WindowFrame Range(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Range, start, end, null);

    /// <summary>Creates a <c>GROUPS BETWEEN</c> frame with explicit peer-group boundaries.</summary>
    public static WindowFrame Groups(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Groups, start, end, null);

    /// <summary>Creates a <c>GROUPS BETWEEN</c> frame with the given preceding and following offsets.</summary>
    public static WindowFrame Groups(int preceding, int following)
        => new(WindowFrameType.Groups, WindowFrameBound.Preceding(preceding), WindowFrameBound.Following(following), null);

    /// <summary>
    /// Returns a copy of this frame carrying <paramref name="exclusion"/>. Passing
    /// <see cref="WindowFrameExclusion.NoOthers"/> requests the explicit <c>EXCLUDE NO OTHERS</c>.
    /// </summary>
    public WindowFrame WithExclusion(WindowFrameExclusion exclusion) => new(Type, Start, End, exclusion);

    /// <summary>Creates a <c>ROWS BETWEEN</c> frame with the given preceding and following offsets.</summary>
    public static WindowFrame Rows(int preceding, int following)
        => new(WindowFrameType.Rows, WindowFrameBound.Preceding(preceding), WindowFrameBound.Following(following), null);

    /// <summary>A <c>ROWS</c> frame from unbounded preceding through the current row.</summary>
    public static WindowFrame RowsUnboundedPrecedingToCurrentRow
        => new(WindowFrameType.Rows, WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow, null);

    /// <summary>A <c>RANGE</c> frame from unbounded preceding through the current row.</summary>
    public static WindowFrame RangeUnboundedPrecedingToCurrentRow
        => new(WindowFrameType.Range, WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow, null);
}

