using System.Collections;
using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// The command-owned canonical parameter set of a prepared command. It is deliberately a separate
/// collection from the driver's live <see cref="DbCommand.Parameters"/>: an execution may lower some
/// text bindings into literals and remove exactly those parameters from the driver collection, while the
/// canonical set has to stay complete so the next checkout restores it and the value-refresh paths can
/// keep indexing it by canonical ordinal.
/// </summary>
/// <remarks>
/// The collection stores the same <see cref="DbParameter"/> objects that belong to the command, never
/// copies, so writing a value here (the captured/runtime refresh) updates the object the driver would
/// bind. It is mutable only to accept a parameter that an execution minted on first use; the prepared
/// command never removes from it.
/// </remarks>
internal sealed class CanonicalParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items;

    /// <summary>Takes a snapshot of the already-created driver parameters, preserving their order.</summary>
    /// <param name="source">The command's parameter collection at construction time.</param>
    internal CanonicalParameterCollection(DbParameterCollection source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _items = new List<DbParameter>(source.Count);
        for (var i = 0; i < source.Count; i++)
            _items.Add(source[i]);
    }

    /// <inheritdoc/>
    public override int Count => _items.Count;

    /// <inheritdoc/>
    public override object SyncRoot => this;

    /// <inheritdoc/>
    public override int Add(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _items.Add((DbParameter)value);
        return _items.Count - 1;
    }

    /// <inheritdoc/>
    public override void AddRange(Array values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
            Add(value!);
    }

    /// <inheritdoc/>
    public override void Clear() => _items.Clear();

    /// <inheritdoc/>
    public override bool Contains(object value) => value is DbParameter parameter && _items.Contains(parameter);

    /// <inheritdoc/>
    public override bool Contains(string value) => IndexOf(value) >= 0;

    /// <inheritdoc/>
    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    /// <inheritdoc/>
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public override int IndexOf(object value) => value is DbParameter parameter ? _items.IndexOf(parameter) : -1;

    /// <inheritdoc/>
    public override int IndexOf(string parameterName)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i].ParameterName, parameterName, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <inheritdoc/>
    public override void Insert(int index, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _items.Insert(index, (DbParameter)value);
    }

    /// <inheritdoc/>
    public override void Remove(object value)
    {
        if (value is DbParameter parameter)
            _items.Remove(parameter);
    }

    /// <inheritdoc/>
    public override void RemoveAt(int index) => _items.RemoveAt(index);

    /// <inheritdoc/>
    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
            _items.RemoveAt(index);
    }

    /// <inheritdoc/>
    protected override DbParameter GetParameter(int index) => _items[index];

    /// <inheritdoc/>
    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

    /// <inheritdoc/>
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

    /// <inheritdoc/>
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index < 0)
            _items.Add(value);
        else
            _items[index] = value;
    }
}
