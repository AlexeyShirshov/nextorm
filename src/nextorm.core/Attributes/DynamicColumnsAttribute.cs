namespace NextORM.Core;

/// <summary>
/// Marks a writable property of an entity as the store that receives the row's columns which are not
/// mapped to a declared member. The property must be assignable from
/// <see cref="Dictionary{TKey,TValue}"/> with <see cref="string"/> keys and <see cref="object"/> values.
/// </summary>
/// <remarks>
/// Read side only: when the entity is queried as a whole (for example
/// <c>context.From&lt;TEntity&gt;()</c>) the generated <c>SELECT</c> appends the source's <c>*</c> after the
/// mapped columns and the store is materialised with every returned column whose name is not one of the
/// mapped columns. Writing the keys back as columns is not implemented; see
/// <c>docs/advanced/limitations.md</c>. The column names are taken from the result set (the database
/// schema), so the store is only populated for a single physical source — a query with joins rejects the
/// store with <see cref="System.NotSupportedException"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DynamicColumnsAttribute : Attribute
{
}
