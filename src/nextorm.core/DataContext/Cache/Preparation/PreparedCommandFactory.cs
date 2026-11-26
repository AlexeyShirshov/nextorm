using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Immutable recipe that mints the ADO.NET command a prepared query executes on a given connection.
/// It is the context-free extraction of the planner's former eager command-construction block: the
/// same SQL text, optional command timeout, and ordered parameter layout, bound with the executing
/// context's command-aware parameter factory. An implicit plan gets its captured values from the
/// invocation, not from this recipe.
/// <para>
/// It stores value data only — never a connection, a context, an interceptor or a command — so a
/// prepared command can keep it for the lifetime of a cached plan without rooting either the planning
/// context or its connection.
/// </para>
/// </summary>
internal readonly struct PreparedCommandFactory
{
    private readonly Seed[] _seeds;

    internal PreparedCommandFactory(string commandText, int? commandTimeout, IReadOnlyList<Parameter>? parameters, bool requiresInvocationValues = false)
    {
        CommandText = commandText;
        CommandTimeout = commandTimeout;
        RequiresInvocationValues = requiresInvocationValues;
        _seeds = parameters is { Count: > 0 } ? new Seed[parameters.Count] : [];
        for (var i = 0; i < _seeds.Length; i++)
        {
            var parameter = parameters![i];
            // CapturedKey belongs to the render: it can reach the live expression and query registry.
            var value = requiresInvocationValues ? null : parameter.Value;
            _seeds[i] = new Seed(parameter.Name, value, parameter.DbType, parameter.Precision, parameter.Scale);
        }
    }

    /// <summary>The statement text the command is created with.</summary>
    internal string CommandText { get; }

    /// <summary>The resolved timeout, or <see langword="null"/> to keep the provider default.</summary>
    internal int? CommandTimeout { get; }

    internal bool RequiresInvocationValues { get; }

    internal int ParameterCount => _seeds.Length;

    /// <summary>The plan-time parameter seeds, in placeholder order.</summary>
    internal IReadOnlyList<Seed> Seeds => _seeds;

    internal readonly record struct Seed(string Name, object? Value, System.Data.DbType? DbType, byte? Precision, byte? Scale);

    /// <summary>
    /// Creates a command on <paramref name="connection"/> and seeds its parameters. The caller owns the
    /// returned command and must dispose it when it is not transferred to a state or a reader owner.
    /// </summary>
    internal DbCommand Create(DbConnection connection, Func<DbCommand, string, object?, DbParameter> createParam, object?[]? values)
    {
        var command = connection.CreateCommand();
        try
        {
            command.CommandText = CommandText;
            if (CommandTimeout is int timeout)
                command.CommandTimeout = timeout;

            for (var i = 0; i < _seeds.Length; i++)
            {
                var seed = _seeds[i];
                var dbParameter = createParam(command, seed.Name, values is null ? seed.Value : values[i]);
                ApplyParameterType(dbParameter, seed);
                command.Parameters.Add(dbParameter);
            }

            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Mints the connection-free plan parameter descriptors exposed by
    /// <see cref="DbPreparedQueryCommand{TResult}.DbCommandParams"/> for refresh and diagnostics. They
    /// never belong to an executing command.
    /// </summary>
    internal DbParameter[] CreatePlanParameters(Func<string, object?, DbParameter> createParam)
    {
        var parameters = new DbParameter[_seeds.Length];
        for (var i = 0; i < _seeds.Length; i++)
        {
            var seed = _seeds[i];
            // The provider's command-unaware factory applies its value normalization (converter
            // output, JSON, range) exactly as the former plan-time command did; the bare parameter
            // object belongs to no command and therefore roots nothing.
            var parameter = createParam(seed.Name, seed.Value);
            ApplyParameterType(parameter, seed);
            parameters[i] = parameter;
        }

        return parameters;
    }

    /// <summary>
    /// Applies the expression-derived provider type recorded on a captured parameter (see
    /// <see cref="ParameterTypeInfo"/>), so a cold captured <c>NULL</c> and every later value bind
    /// with the compared column's type instead of the provider's <see cref="DBNull"/> inference. A
    /// parameter without the metadata keeps the provider's value-based inference (previous behaviour).
    /// Every command is minted per connection from the same seeds, so each one carries the same
    /// provider type.
    /// </summary>
    private static void ApplyParameterType(DbParameter dbParameter, Seed parameter)
    {
        if (parameter.DbType is { } dbType)
            dbParameter.DbType = dbType;

        if (parameter.Precision is { } precision)
            dbParameter.Precision = precision;

        if (parameter.Scale is { } scale)
            dbParameter.Scale = scale;
    }
}
