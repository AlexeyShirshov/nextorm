namespace NextORM.Core;

public sealed partial class MergeBuilder<TEntity>
{
    // In-memory key upsert: match each source row against the registered sequence on the declared key(s),
    // mutate the matched instance's non-key columns or add the source row otherwise. Only the key-upsert
    // form is supported; the full-MERGE branches and a query source have no in-memory counterpart.
    private int MergeInMemory(InMemoryDataContext inMemory)
    {
        if (_source is not null)
            throw new NotSupportedException("The in-memory provider does not support a query source; pass an entity or a batch.");
        if (_branches.Count > 0)
            throw new NotSupportedException("The in-memory provider does not support the full-MERGE branch form; use WhenMatchedUpdate()/WhenNotMatchedInsert().");
        if (_sourceEntities is not { Count: > 0 })
            throw new InvalidOperationException("No source rows were specified; call Using first.");
        if (_keys is not { Count: > 0 })
            throw new InvalidOperationException("No match key was specified; call OnKeys first.");
        if (!_whenMatchedUpdate || !_whenNotMatchedInsert)
            throw new InvalidOperationException("A key upsert requires both WhenMatchedUpdate() and WhenNotMatchedInsert().");

        var list = inMemory.Data[typeof(TEntity)] switch
        {
            IList<TEntity> existing => existing,
            IEnumerable<TEntity> enumerable => [.. enumerable],
            _ => new List<TEntity>(),
        };
        inMemory.Data[typeof(TEntity)] = list;

        var updateColumns = new List<IPropertyMetadata>();
        foreach (var property in _metadata.Properties)
        {
            if (property.IsKey || property.IsIdentity || property.IsComputed)
                continue;

            updateColumns.Add(property);
        }

        foreach (var row in _sourceEntities)
        {
            TEntity? match = default;
            foreach (var candidate in list)
            {
                if (KeysEqual(candidate, row))
                {
                    match = candidate;
                    break;
                }
            }

            if (match is null)
            {
                list.Add(row);
            }
            else
            {
                foreach (var column in updateColumns)
                    column.PropertyInfo.SetValue(match, column.PropertyInfo.GetValue(row));
            }
        }

        return _sourceEntities.Count;
    }

    private bool KeysEqual(TEntity left, TEntity right)
    {
        foreach (var key in _keys!)
        {
            if (!Equals(key.PropertyInfo.GetValue(left), key.PropertyInfo.GetValue(right)))
                return false;
        }

        return true;
    }
}
