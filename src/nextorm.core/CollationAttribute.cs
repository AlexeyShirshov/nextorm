namespace NextORM.Core;

/// <summary>
/// Declares the database collation of a mapped string property. The collation is applied to the
/// column in collation-sensitive operations of generated queries (comparison, <c>LIKE</c>,
/// <c>ORDER BY</c>, <c>GROUP BY</c>), so the column follows the declared collation instead of the
/// database default. Requires a provider that supports a per-expression <c>COLLATE</c> clause (see
/// <see cref="ISqlDialect.SupportsCollation"/>); ClickHouse has none and rejects a collated column.
/// </summary>
/// <remarks>
/// nextorm does not generate DDL, so the attribute declares an existing column's collation rather
/// than creating it (the equivalent of <c>UseCollation</c> in EF Core). The attribute is read by
/// <see cref="IPropertyMetadata"/> (<see cref="IPropertyMetadata.Collation"/>); the equivalent fluent
/// mapping is <see cref="EntityPropertyBuilder{T}.Collation(string)"/>. The in-memory provider has no
/// collations and ignores the value (its comparisons are already ordinal).
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CollationAttribute : Attribute
{
    /// <summary>
    /// Creates an attribute declaring <paramref name="name"/> as the column's collation.
    /// </summary>
    /// <param name="name">The provider-native collation name.</param>
    public CollationAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The provider-native collation name, for example <c>"C"</c> on PostgreSQL or
    /// <c>"Latin1_General_100_BIN2"</c> on SQL Server.
    /// </summary>
    public string Name { get; set; }
}
