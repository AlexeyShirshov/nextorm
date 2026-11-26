using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Carries a manually supplied SQL body and an optional parameter object for a query command,
/// overriding the generated SQL. It is an implementation detail of <c>WithSql</c>/<c>PrepareFromSql</c>.
/// </summary>
internal sealed class RawSqlOverride
{
    private readonly object? _parameters;
    private readonly PropertyInfo[] _properties;

    internal RawSqlOverride(string sql, object? parameters)
    {
        ManualSql = sql;
        _parameters = parameters;
        _properties = parameters?.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public) ?? [];
        ParameterNames = Array.ConvertAll(_properties, static property => property.Name);
    }

    internal string ManualSql { get; }

    // Only these ordered names travel into the plan key; neither reflection metadata nor the bag does.
    internal ReadOnlyMemory<string> ParameterNames { get; }

    internal List<Parameter> MakeParams()
    {
        var parameters = new List<Parameter>(_properties.Length);
        foreach (var property in _properties)
        {
            parameters.Add(new Parameter(property.Name, property.GetValue(_parameters)));
        }
        return parameters;
    }

    internal object?[]? ReadValues()
    {
        if (_properties.Length == 0)
        {
            return null;
        }

        var values = new object?[_properties.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = _properties[i].GetValue(_parameters) ?? DBNull.Value;
        }
        return values;
    }
}
