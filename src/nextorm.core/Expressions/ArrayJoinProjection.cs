namespace NextORM.Core;

/// <summary>
/// Marker for the projection produced by <c>ArrayJoinElement</c>/<c>LeftArrayJoinElement</c>. It
/// deliberately does not implement <see cref="IProjection"/>: unlike the join projections it is not
/// accumulated per source table (code dispatched by <see cref="IProjection"/> must not treat it as a
/// joined source), and it wraps the original entity (<c>Item1</c>) together with the value expanded by
/// the bound <c>ARRAY JOIN</c> (<c>Element</c>).
/// </summary>
public interface IArrayJoinProjection
{
}

/// <summary>
/// Projection exposed by <c>ArrayJoinElement</c>/<c>LeftArrayJoinElement</c>: <see cref="Item1"/> is
/// the original query entity and <see cref="Element"/> the value expanded by the bound
/// <c>ARRAY JOIN</c>. Use it to reference both in <c>Select</c>/<c>Where</c>, for example
/// <c>From&lt;IArrayEntity&gt;().ArrayJoinElement(e =&gt; e.Tags).Select(p =&gt; new { p.Item1.Id, Tag = p.Element })</c>.
/// </summary>
/// <typeparam name="TEntity">The query's entity type.</typeparam>
/// <typeparam name="TElement">The element type of the joined array.</typeparam>
public class ArrayJoinProjection<TEntity, TElement> : IArrayJoinProjection
{
    /// <summary>The original query entity.</summary>
    public TEntity Item1 { get; init; } = default!;

    /// <summary>The value expanded by the bound <c>ARRAY JOIN</c> for the current row.</summary>
    public TElement Element { get; init; } = default!;
}

/// <summary>
/// Names shared between the builder/renderer (which emit the element alias) and the expression
/// translator (which references it). Kept internal so the generated SQL identifiers are an
/// implementation detail.
/// </summary>
internal static class ArrayJoinNames
{
    /// <summary>The SQL alias of the bound <c>ARRAY JOIN</c> element.</summary>
    internal const string ElementAlias = "__nextorm_aj_element";

    /// <summary>The CLR member name of <see cref="ArrayJoinProjection{TEntity, TElement}.Element"/>.</summary>
    internal const string ElementMember = "Element";
}
