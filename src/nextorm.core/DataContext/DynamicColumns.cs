using System.Data;

namespace NextORM.Core;

/// <summary>
/// Reads the columns of a result row that are not mapped to a declared entity member into the
/// dynamic-columns dictionary of the entity (see <see cref="DynamicColumnsAttribute"/>). One instance
/// is built per materializer from the mapped column names, so the name normalisation is not repeated
/// per row.
/// </summary>
internal sealed class DynamicColumns
{
    private readonly string[] _mappedNames;
    private readonly string[] _normalizedMappedNames;

    /// <summary>
    /// Creates the reader for the given mapped column names.
    /// </summary>
    /// <param name="mappedNames">
    /// The names of the mapped columns to skip. A result field is matched against them verbatim
    /// (ordinal, case-insensitive) and after dropping underscores and lower-casing, so a snake-case
    /// physical name matches its PascalCase property name.
    /// </param>
    public DynamicColumns(string[] mappedNames)
    {
        _mappedNames = mappedNames;
        _normalizedMappedNames = new string[mappedNames.Length];
        for (var i = 0; i < mappedNames.Length; i++)
            _normalizedMappedNames[i] = Normalize(mappedNames[i]);
    }

    /// <summary>
    /// Builds the dynamic-columns dictionary from the fields of <paramref name="record"/> starting at
    /// <paramref name="startIndex"/> (the first field produced by the source's <c>*</c>), skipping the
    /// fields whose name matches a mapped column.
    /// </summary>
    /// <param name="record">The result-set row to read.</param>
    /// <param name="startIndex">The ordinal of the first unmapped field.</param>
    /// <returns>The unmapped columns, keyed by their result-set name.</returns>
    public Dictionary<string, object?> Read(IDataRecord record, int startIndex)
    {
        var fieldCount = record.FieldCount;
        var result = new Dictionary<string, object?>(fieldCount > startIndex ? fieldCount - startIndex : 0, StringComparer.Ordinal);
        for (var i = startIndex; i < fieldCount; i++)
        {
            var name = record.GetName(i);
            if (IsMapped(name))
                continue;

            result[name] = record.IsDBNull(i) ? null : record.GetValue(i);
        }

        return result;
    }

    private bool IsMapped(string name)
    {
        var mappedNames = _mappedNames;
        for (var i = 0; i < mappedNames.Length; i++)
        {
            if (string.Equals(name, mappedNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var normalizedName = Normalize(name);
        var normalized = _normalizedMappedNames;
        for (var i = 0; i < normalized.Length; i++)
        {
            if (string.Equals(normalizedName, normalized[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string Normalize(string name)
    {
        var buffer = name.Length <= 128 ? stackalloc char[name.Length] : new char[name.Length];
        var length = 0;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
                continue;

            buffer[length++] = char.ToLowerInvariant(c);
        }

        return new string(buffer[..length]);
    }
}
