using System.Collections;
using System.Data;
using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// A plan-parameter descriptor: the name/value pair the planner baked into a statement, represented
/// as a real <see cref="DbParameter"/> so the guarded parameter-refresh recipe and SQL-generation
/// diagnostics keep operating on a parameter collection.
/// <para>
/// It is minted by the provider's command-unaware <c>CreateParam(string, object?)</c> hook, so it
/// carries the provider's value normalization without belonging to any command, connection or
/// transaction. Execution always mints its own parameters on the executing command; this object is
/// never added to a provider command.
/// </para>
/// </summary>
internal sealed class PlanParameter : DbParameter
{
    private string _name;
    private object? _value;

    internal PlanParameter(string name, object? value)
    {
        _name = name;
        _value = value;
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ParameterName
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    public override object? Value
    {
        get => _value;
        set => _value = value;
    }

    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    public override int Size { get; set; }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }
    public override void ResetDbType() => DbType = DbType.Object;
}

/// <summary>
/// List-backed <see cref="DbParameterCollection"/> holding a plan's parameter descriptors. It is the
/// storage behind <see cref="DbPreparedQueryCommand{TResult}.DbCommandParams"/> and the sink the
/// guarded parameter-refresh recipe binds into on a cache hit. No provider command owns it.
/// </summary>
internal sealed class PlanParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items;

    internal PlanParameterCollection(IEnumerable<DbParameter> items) => _items = new List<DbParameter>(items);

    internal void Add(DbParameter parameter) => _items.Add(parameter);

    public override int Count => _items.Count;
    public override object SyncRoot => _items;

    public override int Add(object value)
    {
        _items.Add((DbParameter)value);
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
            Add(value!);
    }

    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string? value) => _items.Exists(p => p.ParameterName == value);

    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string? parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string? parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);

    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

    protected override void SetParameter(string? parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index < 0)
            Add(value);
        else
            _items[index] = value;
    }
}

/// <summary>
/// Connection-free diagnostic <see cref="DbCommand"/> that exposes the plan's SQL text, timeout and
/// parameter descriptors. It is never executed and never attached to a connection or transaction; it
/// exists so SQL-generation diagnostics and the parameter-refresh recipe can inspect a prepared plan
/// without a provider command, which the lazy per-connection command model no longer creates eagerly.
/// </summary>
internal sealed class PlanCommand : DbCommand
{
    private readonly PlanParameterCollection _parameters;
    private string _commandText = string.Empty;
    private int _commandTimeout = 30;

    internal PlanCommand(string commandText, int? commandTimeout, IEnumerable<DbParameter> parameters)
    {
        _commandText = commandText;
        if (commandTimeout is int timeout)
            _commandTimeout = timeout;
        _parameters = new PlanParameterCollection(parameters);
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }

    public override int CommandTimeout
    {
        get => _commandTimeout;
        set => _commandTimeout = value;
    }

    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => throw new NotSupportedException();

    public override object? ExecuteScalar() => throw new NotSupportedException();

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new PlanParameter(string.Empty, null);

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
}
