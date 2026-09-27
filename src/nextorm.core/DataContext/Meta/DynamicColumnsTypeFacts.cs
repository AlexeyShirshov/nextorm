namespace NextORM.Core;

/// <summary>
/// Type rules for a dynamic-columns store property (see <see cref="DynamicColumnsAttribute"/>).
/// </summary>
internal static class DynamicColumnsTypeFacts
{
    /// <summary>
    /// Whether <paramref name="type"/> is a usable dynamic-columns store: a string-keyed
    /// dictionary/read-only-dictionary of <see cref="object"/> values. Accepted shapes are
    /// <see cref="IReadOnlyDictionary{TKey,TValue}"/> with <see cref="string"/> keys and
    /// <see cref="object"/> values and any type assignable to
    /// <see cref="IDictionary{TKey,TValue}"/> with the same key/value types (for example
    /// <c>Dictionary&lt;string, object?&gt;</c> or <c>IDictionary&lt;string, object?&gt;</c>).
    /// <see cref="object"/> and the non-generic dictionary/enumerable shapes are rejected.
    /// </summary>
    /// <param name="type">The property type to test.</param>
    /// <returns><see langword="true"/> when the type can store the dynamic-columns dictionary.</returns>
    public static bool IsStoreType(Type type)
    {
        if (type == typeof(IReadOnlyDictionary<string, object?>))
            return true;

        return typeof(IDictionary<string, object?>).IsAssignableFrom(type);
    }
}
