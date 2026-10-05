namespace NextORM.Core;

/// <summary>
/// Marks a generated alias projection property with the 1-based slot of the joined table it exposes.
/// <para>
/// A projection produced by <c>Join</c> exposes its items as <c>Item1</c>..<c>Item8</c>; a generated
/// alias projection additionally exposes lexical aliases (for example <c>Buyer</c>/<c>Approver</c>)
/// whose types may repeat. The trailing digits of <c>ItemN</c> carry the slot position; this attribute
/// carries the same information for a member whose name does not. The translator reads it to resolve
/// the right table alias when the joined type occurs more than once.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class JoinSlotAttribute : Attribute
{
    /// <summary>Creates the marker for a projection member occupying <paramref name="position"/>.</summary>
    /// <param name="position">The 1-based slot among all projection items (the base entity is slot 1).</param>
    public JoinSlotAttribute(int position) => Position = position;

    /// <summary>The 1-based slot among all projection items (the base entity is slot 1).</summary>
    public int Position { get; }
}
