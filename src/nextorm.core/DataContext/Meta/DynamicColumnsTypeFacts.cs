namespace NextORM.Core;

/// <summary>
/// Type rules for a dynamic-columns store property (see <see cref="DynamicColumnsAttribute"/>).
/// </summary>
internal static class DynamicColumnsTypeFacts
{
    /// <summary>
    /// Whether <paramref name="type"/> can hold the dictionary produced when a row is read: it must be
    /// assignable from <see cref="Dictionary{TKey,TValue}"/> with <see cref="string"/> keys and
    /// <see cref="object"/> values (for example <c>Dictionary&lt;string, object?&gt;</c>,
    /// <c>IDictionary&lt;string, object?&gt;</c> or <c>IReadOnlyDictionary&lt;string, object?&gt;</c>).
    /// </summary>
    /// <param name="type">The property type to test.</param>
    /// <returns><see langword="true"/> when the type can store the dynamic-columns dictionary.</returns>
    public static bool IsStoreType(Type type) => type.IsAssignableFrom(typeof(Dictionary<string, object?>));
}
