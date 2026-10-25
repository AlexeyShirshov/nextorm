using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Immutable recipe that mints the ADO.NET command a prepared query executes on a given connection.
/// It is the context-free extraction of the planner's former eager command-construction block: the
/// same SQL text, the same optional command timeout and the same ordered parameter seeds, bound with
/// the executing context's command-aware parameter factory.
/// <para>
/// It stores value data only — never a connection, a context, an interceptor or a command — so a
/// prepared command can keep it for the lifetime of a cached plan without rooting either the planning
/// context or its connection.
/// </para>
/// </summary>
internal readonly struct PreparedCommandFactory
{
    private readonly Parameter[] _seeds;

    internal PreparedCommandFactory(string commandText, int? commandTimeout, Parameter[] seeds)
    {
        CommandText = commandText;
        CommandTimeout = commandTimeout;
        _seeds = seeds;
    }

    /// <summary>The statement text the command is created with.</summary>
    internal string CommandText { get; }

    /// <summary>The resolved timeout, or <see langword="null"/> to keep the provider default.</summary>
    internal int? CommandTimeout { get; }

    /// <summary>The plan-time parameter seeds, in placeholder order.</summary>
    internal IReadOnlyList<Parameter> Seeds => _seeds;

    /// <summary>
    /// Creates a command on <paramref name="connection"/> and seeds its parameters. The caller owns the
    /// returned command and must dispose it when it is not transferred to a state or a reader owner.
    /// </summary>
    internal DbCommand Create(DbConnection connection, Func<DbCommand, string, object?, DbParameter> createParam)
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
                command.Parameters.Add(createParam(command, seed.Name, seed.Value));
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
            parameters[i] = createParam(seed.Name, seed.Value);
        }

        return parameters;
    }
}
