using System.Collections;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// One evaluated entry of a captured collection indexed inside a query (<c>dict[column]</c>): the key
/// compared against the query value and the value the lookup yields, in the order they are rendered.
/// </summary>
internal readonly struct LookupEntry
{
    /// <summary>The dictionary key (or the zero-based index for a list/array).</summary>
    public readonly object? Key;
    /// <summary>The value stored under <see cref="Key"/>.</summary>
    public readonly object? Value;

    /// <summary>Creates an entry.</summary>
    /// <param name="key">The key or index.</param>
    /// <param name="value">The stored value.</param>
    public LookupEntry(object? key, object? value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// Build-time handling of a captured collection indexed by an expression (<c>dict[column]</c>,
/// <c>list[column]</c>, an array index). The collection is evaluated on the client and the lookup is
/// rendered as a portable <c>CASE WHEN key = @k THEN @v … END</c>, so no provider-specific construct
/// is needed. The entry count changes the SQL/parameter shape without changing the expression tree, so
/// it is folded into the plan key through <see cref="InValues.ComputeShapeHash"/>, exactly like a
/// value-list.
/// </summary>
internal static class DictionaryLookup
{
    /// <summary>
    /// Matches an indexer over a captured collection that nextorm can evaluate on the client. The C#
    /// compiler lowers an indexer differently per collection: a dictionary/list indexer becomes a
    /// <c>get_Item</c> call, an array index an <see cref="ExpressionType.ArrayIndex"/> binary node and a
    /// dynamic/COM index an <see cref="IndexExpression"/>; all three shapes are accepted.
    /// </summary>
    /// <param name="node">The expression to inspect.</param>
    /// <param name="collectionExp">The captured collection being indexed.</param>
    /// <param name="keyExp">The index/key expression (may reference a query column).</param>
    /// <returns><see langword="true"/> when the node is a translatable captured-collection lookup.</returns>
    internal static bool TryGetLookup(Expression node, out Expression collectionExp, out Expression keyExp)
    {
        collectionExp = null!;
        keyExp = null!;

        switch (node)
        {
            case IndexExpression { Object: { } indexObject, Arguments: [var indexKey] }:
                collectionExp = indexObject;
                keyExp = indexKey;
                break;
            case MethodCallExpression { Object: { } callObject, Arguments: [var callKey] } call when call.Method.Name == "get_Item":
                collectionExp = callObject;
                keyExp = callKey;
                break;
            case BinaryExpression { NodeType: ExpressionType.ArrayIndex, Left: var array, Right: var arrayIndex }:
                collectionExp = array;
                keyExp = arrayIndex;
                break;
            default:
                return false;
        }

        var collectionType = collectionExp.Type;
        if (collectionType == typeof(string) || !IsLookupCollection(collectionType))
            return false;

        // Only a captured collection (a closure member/constant) can be read on the client; a
        // collection reached through a query parameter is a server-side value and is not ours.
        if (collectionExp.Has<ParameterExpression>())
        {
            collectionExp = null!;
            keyExp = null!;
            return false;
        }

        return true;
    }

    /// <summary>Whether the type is a non-generic <see cref="IDictionary"/> or <see cref="IList"/> (arrays included).</summary>
    /// <param name="type">The collection type.</param>
    /// <returns><see langword="true"/> when the collection can be indexed.</returns>
    internal static bool IsLookupCollection(Type type)
        => typeof(IDictionary).IsAssignableFrom(type) || typeof(IList).IsAssignableFrom(type);

    /// <summary>
    /// Evaluates the captured collection and returns its entries. A dictionary keeps its enumeration
    /// order (stable for the captured instance); a list/array is enumerated by ascending index.
    /// </summary>
    /// <param name="collectionExp">The captured collection expression.</param>
    /// <param name="queryProvider">The query registry used to evaluate and cache the closure access.</param>
    /// <returns>The evaluated key/value entries.</returns>
    /// <exception cref="NotSupportedException">The evaluated collection is null or not indexable.</exception>
    internal static List<LookupEntry> Evaluate(Expression collectionExp, IQueryRegistry queryProvider)
        => ToEntries(InValuesEvaluator.Evaluate(collectionExp, queryProvider));

    private static List<LookupEntry> ToEntries(object? value)
    {
        switch (value)
        {
            case IDictionary dictionary:
            {
                var entries = new List<LookupEntry>(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                    entries.Add(new LookupEntry(entry.Key, entry.Value));

                return entries;
            }
            case IList list:
            {
                var entries = new List<LookupEntry>(list.Count);
                for (var i = 0; i < list.Count; i++)
                    entries.Add(new LookupEntry(i, list[i]));

                return entries;
            }
            default:
                throw new NotSupportedException(
                    "The captured collection indexed inside the query evaluated to null or to a value that cannot be indexed; " +
                    "a Dictionary, List or array is required.");
        }
    }
}
