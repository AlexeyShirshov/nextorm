using System.Collections.ObjectModel;

namespace NextORM.Core;

/// <summary>Declares the entity properties whose values select a shard.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
public sealed class ShardKeyAttribute : Attribute
{
    private readonly ReadOnlyCollection<string> _propertyNames;

    /// <summary>Initializes a shard key from one or more entity property names.</summary>
    /// <param name="propertyNames">The ordered property names that compose the shard key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The list is empty or contains an empty or whitespace name.</exception>
    public ShardKeyAttribute(params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);
        if (propertyNames.Length == 0 || propertyNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty shard-key property name is required.", nameof(propertyNames));

        _propertyNames = Array.AsReadOnly((string[])propertyNames.Clone());
    }

    /// <summary>Gets the ordered property names that compose the shard key.</summary>
    public IReadOnlyList<string> PropertyNames => _propertyNames;
}
