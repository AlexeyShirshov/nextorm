using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A window-function call that has not been given its <c>OVER</c> clause yet. The <c>row_number</c>,
/// <c>rank</c>, <c>lag</c>, <c>sum_over</c>, ... methods of <see cref="CommonFunctions"/> return this
/// wrapper so that <see cref="Over(Expression{Func{object?}}?, Expression{Func{object?}}?, WindowFrame?)"/>
/// can be chained and returns the value type that is actually read from the result set.
/// <para>
/// The wrapper is only ever produced by the compiler while building an expression tree; none of its
/// members is executed. The nested <c>partitionBy</c>/<c>orderBy</c> lambdas close over the outer
/// query parameter, e.g. <c>SqlFunctions.Sql.row_number().Over(partitionBy: () =&gt; e.Int, orderBy: () =&gt; e.Id)</c>.
/// </para>
/// </summary>
public sealed class WindowFunction<T>
{
    /// <summary>
    /// Completes the window function with a partition and/or an ascending <c>ORDER BY</c> plus an
    /// optional frame. <c>Over()</c> renders an empty specification (<c>over ()</c>); trailing
    /// arguments may be omitted.
    /// <para>
    /// Because C# expression trees reject named arguments that skip a preceding defaulted parameter,
    /// an order-only specification must use the <see cref="WindowOrder"/> overloads
    /// (<c>Over(SqlFunctions.Sql.asc(() =&gt; e.Id))</c>) rather than <c>Over(orderBy: ...)</c>.
    /// </para>
    /// </summary>
    public T Over(
        Expression<Func<object?>>? partitionBy = null,
        Expression<Func<object?>>? orderBy = null,
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
    /// Completes the window function with multiple ordered keys. Use this when a mixed
    /// <c>asc</c>/<c>desc</c> ordering or more than one order key is required; partition keys for
    /// this shape are supplied through the array overload (see the <c>Over</c> overload below).
    /// </summary>
    public T Over(WindowOrder[] orderBy, WindowFrame? frame = null) => default!;

    /// <summary>
    /// Completes the window function with multiple partition and/or ordered expressions, allowing a
    /// mixed <c>asc</c>/<c>desc</c> ordering built with <see cref="CommonFunctions.asc(Expression{Func{object?}})"/>
    /// and <see cref="CommonFunctions.desc(Expression{Func{object?}})"/>. <paramref name="partitionBy"/> is
    /// required here so that <c>Over()</c> still binds unambiguously to the single-expression overload.
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
    Rows,
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
    UnboundedPreceding,
    Preceding,
    CurrentRow,
    Following,
    UnboundedFollowing
}

/// <summary>A single boundary of a <see cref="WindowFrame"/> (e.g. <c>1 preceding</c>).</summary>
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

    public static WindowFrameBound UnboundedPreceding => new(WindowFrameBoundKind.UnboundedPreceding, 0);
    public static WindowFrameBound CurrentRow => new(WindowFrameBoundKind.CurrentRow, 0);
    public static WindowFrameBound UnboundedFollowing => new(WindowFrameBoundKind.UnboundedFollowing, 0);

    /// <summary>A boundary <paramref name="rows"/> rows before the current row.</summary>
    public static WindowFrameBound Preceding(int rows) => new(WindowFrameBoundKind.Preceding, rows);

    /// <summary>A boundary <paramref name="rows"/> rows after the current row.</summary>
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

    /// <summary>A <c>rows between</c> frame with explicit boundaries.</summary>
    public static WindowFrame Rows(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Rows, start, end, null);

    /// <summary>A <c>range between</c> frame with explicit boundaries.</summary>
    public static WindowFrame Range(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Range, start, end, null);

    /// <summary>A <c>groups between</c> frame with explicit peer-group boundaries.</summary>
    public static WindowFrame Groups(WindowFrameBound start, WindowFrameBound end) => new(WindowFrameType.Groups, start, end, null);

    /// <summary><c>groups between &lt;preceding&gt; preceding and &lt;following&gt; following</c>.</summary>
    public static WindowFrame Groups(int preceding, int following)
        => new(WindowFrameType.Groups, WindowFrameBound.Preceding(preceding), WindowFrameBound.Following(following), null);

    /// <summary>
    /// Returns a copy of this frame carrying <paramref name="exclusion"/>. Passing
    /// <see cref="WindowFrameExclusion.NoOthers"/> requests the explicit <c>EXCLUDE NO OTHERS</c>.
    /// </summary>
    public WindowFrame WithExclusion(WindowFrameExclusion exclusion) => new(Type, Start, End, exclusion);

    /// <summary><c>rows between &lt;preceding&gt; preceding and &lt;following&gt; following</c>.</summary>
    public static WindowFrame Rows(int preceding, int following)
        => new(WindowFrameType.Rows, WindowFrameBound.Preceding(preceding), WindowFrameBound.Following(following), null);

    /// <summary><c>rows between unbounded preceding and current row</c>, the default running aggregate frame.</summary>
    public static WindowFrame RowsUnboundedPrecedingToCurrentRow
        => new(WindowFrameType.Rows, WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow, null);

    /// <summary><c>range between unbounded preceding and current row</c>.</summary>
    public static WindowFrame RangeUnboundedPrecedingToCurrentRow
        => new(WindowFrameType.Range, WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow, null);
}

