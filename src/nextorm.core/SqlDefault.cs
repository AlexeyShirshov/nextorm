namespace NextORM.Core;

/// <summary>
/// Marks a column to use the database's <c>DEFAULT</c> value in an insert. Pass it to
/// <see cref="InsertBuilder{TEntity}.Value{TValue}(System.Linq.Expressions.Expression{System.Func{TEntity, TValue}}, SqlDefault)"/>
/// or use it for a mapped member in a <c>Values(source, mapping)</c> projection.
/// <para>
/// SQLite cannot render <c>DEFAULT</c> in a <c>VALUES</c> list and rejects this marker. Omit the column
/// instead to use the same database default.
/// </para>
/// </summary>
public readonly struct SqlDefault
{
    /// <summary>The singleton marker value.</summary>
    public static SqlDefault Value => default;
}
