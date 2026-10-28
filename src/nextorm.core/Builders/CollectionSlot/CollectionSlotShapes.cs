using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The seven CLR shapes a collection-valued projection slot may declare (#249 / T353), and the conversion from
/// a materialized in-memory collection into that shape. Every other concrete or custom shape fails closed with
/// no substitution: a slot typed <c>HashSet&lt;T&gt;</c> or a domain collection must not silently arrive as a
/// <c>List&lt;T&gt;</c>.
/// </summary>
internal static class CollectionSlotShapes
{
    /// <summary>The supported shapes, used verbatim in the refusal message.</summary>
    internal const string SupportedList =
        "List<T>, T[], IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T>";

    /// <summary>
    /// Whether <paramref name="slotType"/> is one of the seven supported shapes over <paramref name="elementType"/>.
    /// </summary>
    /// <param name="slotType">The destination CLR type.</param>
    /// <param name="elementType">The element type the slot produces.</param>
    /// <returns><see langword="true"/> for a supported shape.</returns>
    internal static bool IsSupported(Type slotType, Type elementType)
    {
        if (slotType.IsArray)
            return slotType.GetArrayRank() == 1 && slotType.GetElementType() == elementType;

        if (!slotType.IsGenericType)
            return false;

        if (slotType.GetGenericArguments()[0] != elementType)
            return false;

        var definition = slotType.GetGenericTypeDefinition();
        return definition == typeof(List<>)
            || definition == typeof(IEnumerable<>)
            || definition == typeof(ICollection<>)
            || definition == typeof(IList<>)
            || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(IReadOnlyList<>);
    }

    /// <summary>
    /// Materializes <paramref name="items"/> into <paramref name="slotType"/>, preserving order and duplicates.
    /// </summary>
    /// <param name="items">The materialized items, in pipeline order.</param>
    /// <param name="elementType">The element type the slot carries.</param>
    /// <param name="slotType">The destination CLR type (already validated by <see cref="IsSupported"/>).</param>
    /// <returns>The slot value.</returns>
    internal static object Convert(IReadOnlyList<object?> items, Type elementType, Type slotType)
    {
        if (slotType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
                array.SetValue(items[i], i);

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        for (var i = 0; i < items.Count; i++)
            list.Add(items[i]);

        return list;
    }
}
