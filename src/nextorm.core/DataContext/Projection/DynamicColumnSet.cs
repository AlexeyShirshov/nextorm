namespace NextORM.Core;

/// <summary>
/// The write-side seam for an entity's <see cref="DynamicColumnsAttribute"/> store: the physical column
/// names read from the store, ordinal-sorted for a deterministic column order, and the per-row values
/// aligned with them. Shared by the INSERT, UPDATE and MERGE builders and their renderers so the
/// extraction, key validation and dynamic-identifier quoting live in one place.
/// <para>
/// A dynamic key is a physical name: it is always quoted through <see cref="ISqlDialect.QuoteIdentifier"/>
/// regardless of the global identifier-quoting flag, and the naming convention is deliberately skipped.
/// No <see cref="IPropertyMetadata"/> is fabricated for these columns and no value converter/JSON mapping
/// applies; the value is bound from its runtime CLR type through the existing parameter path.
/// </para>
/// </summary>
internal sealed class DynamicColumnSet
{
    private DynamicColumnSet(string[] keys, object?[][] rows)
    {
        Keys = keys;
        Rows = rows;
    }

    /// <summary>The physical column names, sorted with <see cref="StringComparer.Ordinal"/>.</summary>
    public string[] Keys { get; }

    /// <summary>The values of every source row, each aligned by index with <see cref="Keys"/>.</summary>
    public object?[][] Rows { get; }

    /// <summary>The number of source rows in this set.</summary>
    public int RowCount => Rows.Length;

    /// <summary>
    /// Reads the dynamic-columns store of a single entity. Returns <see langword="null"/> when the entity
    /// has no store, its store is <see langword="null"/>, or the store has no key.
    /// </summary>
    /// <typeparam name="TEntity">The entity type carrying the store.</typeparam>
    /// <param name="metadata">The entity metadata, providing the store property and the mapped column names.</param>
    /// <param name="entity">The entity to read the store from.</param>
    /// <returns>The dynamic-columns set, or <see langword="null"/> when there is none.</returns>
    public static DynamicColumnSet? FromEntity<TEntity>(IEntityMetadata metadata, TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (entity is null)
            return null;

        var storeProperty = metadata.DynamicColumnsStore;
        if (storeProperty is null)
            return null;

        var store = storeProperty.PropertyInfo.GetValue(entity);
        if (store is null)
            return null;

        var keys = ReadKeys(store, MappedNames(metadata));
        if (keys.Length == 0)
            return null;

        var row = new object?[keys.Length];
        for (var i = 0; i < keys.Length; i++)
            row[i] = ReadValue(store, keys[i]);

        return new DynamicColumnSet(keys, [row]);
    }

    /// <summary>
    /// Reads the dynamic-columns stores of a batch of entities. Every store must expose the identical
    /// sorted key set; a mismatch is rejected with <see cref="InvalidOperationException"/>. A
    /// <see langword="null"/> store contributes an empty key set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type carrying the store.</typeparam>
    /// <param name="metadata">The entity metadata, providing the store property and the mapped column names.</param>
    /// <param name="entities">The entities whose stores are read, in row order.</param>
    /// <param name="operation">The statement name used in the mismatch message (<c>INSERT</c>, <c>MERGE</c>).</param>
    /// <returns>The dynamic-columns set, or <see langword="null"/> when no row has a key.</returns>
    /// <exception cref="InvalidOperationException">The rows expose different dynamic-column key sets.</exception>
    public static DynamicColumnSet? FromEntities<TEntity>(IEntityMetadata metadata, IReadOnlyList<TEntity> entities, string operation)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (entities.Count == 0)
            return null;

        var storeProperty = metadata.DynamicColumnsStore;
        if (storeProperty is null)
            return null;

        // The first row fixes the key set for the whole batch: its keys are read, validated and sorted
        // once. Every later row only has its key set compared against that one and its values gathered,
        // so the per-row ToArray/ValidateKey/Array.Sort of the naive path is not repeated.
        string[]? keys = null;
        HashSet<string>? keySet = null;
        DynamicColumns? mapped = null;
        var rows = new object?[entities.Count][];
        for (var r = 0; r < entities.Count; r++)
        {
            var store = storeProperty.PropertyInfo.GetValue(entities[r]);

            if (keys is null)
            {
                mapped ??= MappedNames(metadata);
                keys = store is null ? [] : ReadKeys(store, mapped);
                keySet = new HashSet<string>(keys, StringComparer.Ordinal);
            }
            else if (!SameKeySet(store, keys, keySet!))
            {
                throw new InvalidOperationException(
                    $"Every row of a multi-row {operation} must expose the same dynamic-column keys; row {r} exposes a different set.");
            }

            var row = new object?[keys.Length];
            if (store is not null)
            {
                for (var i = 0; i < keys.Length; i++)
                    row[i] = ReadValue(store, keys[i]);
            }

            rows[r] = row;
        }

        if (keys is null || keys.Length == 0)
            return null;

        return new DynamicColumnSet(keys, rows);
    }

    /// <summary>
    /// Renders the physical keys, each quoted with the dialect's identifier quoting regardless of the
    /// global identifier-quoting flag.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <returns>The quoted column names in <see cref="Keys"/> order.</returns>
    public string[] RenderKeys(ISqlDialect dialect)
    {
        var rendered = new string[Keys.Length];
        for (var i = 0; i < rendered.Length; i++)
            rendered[i] = dialect.QuoteIdentifier(Keys[i]);

        return rendered;
    }

    /// <summary>
    /// Binds the values of one row as parameters through the existing parameter path with a
    /// <see langword="null"/> property (no converter, no JSON, no duration unit), mirroring the read side.
    /// </summary>
    /// <param name="row">The row index, aligned with <see cref="Rows"/>.</param>
    /// <param name="parameters">The statement's parameter accumulator.</param>
    /// <param name="parameterProvider">The provider that names the parameters.</param>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <returns>The generated parameter names, aligned with <see cref="Keys"/>.</returns>
    public string[] AddValueParameters(int row, List<Parameter> parameters, IParameterProvider parameterProvider, ISqlDialect dialect)
    {
        var values = Rows[row];
        var names = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var name = parameterProvider.GetParamName();
            parameters.Add(new Parameter(name, DurationStorage.ToParameterValue(values[i], null, dialect)));
            names[i] = name;
        }

        return names;
    }

    private static string[] ReadKeys(object store, DynamicColumns mapped)
    {
        var keys = GetKeys(store).ToArray();

        for (var i = 0; i < keys.Length; i++)
        {
            ValidateKey(keys[i]);
            if (mapped.IsMapped(keys[i]))
                throw new InvalidOperationException(
                    $"A dynamic-column key must not collide with a mapped column of the entity; the key is \"{keys[i]}\".");
        }

        Array.Sort(keys, StringComparer.Ordinal);
        return keys;
    }

    /// <summary>
    /// Builds the same mapped-name set the read side uses to strip mapped columns, from the entity
    /// metadata: each mapped property contributes its CLR name and its physical column name (both range
    /// columns for a range property).
    /// </summary>
    private static DynamicColumns MappedNames(IEntityMetadata metadata)
    {
        var names = new List<string>(metadata.Properties.Count * 2);
        foreach (var property in metadata.Properties)
        {
            if (property.RangeColumns is { } range)
            {
                // Mirror the read side (EntitySelectListBuilder/RowMaterializerBuilder): a range property
                // contributes only its two physical column names, never its CLR name. Adding the CLR name
                // here would reject a store key that the read side happily stores.
                names.Add(range.LowerColumn);
                names.Add(range.UpperColumn);
                continue;
            }

            names.Add(property.PropertyInfo.Name);
            names.Add(property.ColumnName);
        }

        return new DynamicColumns(names.ToArray());
    }

    private static IEnumerable<string> GetKeys(object store)
        => store switch
        {
            IReadOnlyDictionary<string, object?> readOnly => readOnly.Keys,
            IDictionary<string, object?> dictionary => dictionary.Keys,
            _ => throw new BuildSqlCommandException(
                $"The dynamic-columns store of type {store.GetType()} is not a string-keyed dictionary of object values."),
        };

    private static object? ReadValue(object store, string key)
        => store switch
        {
            IReadOnlyDictionary<string, object?> readOnly => readOnly[key],
            IDictionary<string, object?> dictionary => dictionary[key],
            _ => null,
        };

    /// <summary>
    /// Compares a later row's key set with the established one by membership (both dictionaries have a
    /// unique-key guarantee, so an equal size plus full containment is set equality), without sorting or
    /// copying the row's keys.
    /// </summary>
    private static bool SameKeySet(object? store, string[] keys, HashSet<string> keySet)
    {
        if (store is null)
            return keys.Length == 0;

        var count = 0;
        foreach (var key in GetKeys(store))
        {
            count++;
            if (!keySet.Contains(key))
                return false;
        }

        return count == keys.Length;
    }

    /// <summary>
    /// Rejects a key that cannot be a physical column name: an empty key or a key containing a NUL.
    /// </summary>
    /// <param name="key">The dynamic-column key to validate.</param>
    /// <exception cref="ArgumentException">The key is empty or contains a NUL character.</exception>
    public static void ValidateKey(string key)
    {
        if (key.Length == 0)
            throw new ArgumentException("A dynamic-column key must not be empty; the key is \"\".");

        if (key.Contains('\0'))
            throw new ArgumentException($"A dynamic-column key must not contain a NUL character; the key is \"{key}\".");
    }
}
