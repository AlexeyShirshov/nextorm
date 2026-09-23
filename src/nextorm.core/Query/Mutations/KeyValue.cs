namespace NextORM.Core;

/// <summary>
/// One declared key column and the value read from an entity, shared by the key forms of
/// <c>Delete(entity)</c> and <c>Update(entity)</c>. The value is bound as a parameter, never inlined.
/// </summary>
internal sealed class KeyValue
{
    /// <summary>Creates a key binding.</summary>
    /// <param name="property">The key column.</param>
    /// <param name="value">The value read from the entity.</param>
    public KeyValue(IPropertyMetadata property, object? value)
    {
        Property = property;
        Value = value;
    }

    /// <summary>The key column.</summary>
    public IPropertyMetadata Property { get; }

    /// <summary>The value read from the entity, bound as a parameter.</summary>
    public object? Value { get; }
}
