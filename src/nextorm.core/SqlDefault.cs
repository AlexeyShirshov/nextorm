namespace NextORM.Core;

/// <summary>
/// Marker passed to <see cref="InsertBuilder{TEntity}.Value{TValue}(System.Linq.Expressions.Expression{System.Func{TEntity, TValue}}, SqlDefault)"/>
/// or used as a mapped member in a <c>Values(source, mapping)</c> projection to write the column's
/// database <c>DEFAULT</c> instead of a value.
/// <para>
/// A provider whose dialect cannot render the <c>DEFAULT</c> keyword in a <c>VALUES</c> list (SQLite)
/// rejects it with <see cref="NotSupportedException"/>; omit the column instead, which yields the same
/// default.
/// </para>
/// </summary>
public readonly struct SqlDefault
{
    /// <summary>The singleton marker value.</summary>
    public static SqlDefault Value => default;
}
