using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace NextORM.Core;

/// <summary>
/// Prepared query command backed by an ADO.NET <see cref="System.Data.IDataRecord"/> reader.
/// </summary>
public sealed class DbPreparedQueryCommand<TResult> : PreparedQueryCommand<TResult, IDataRecord>, IDbCommandHolder
{
    /// <summary>
    /// The <see cref="CommandBehavior"/> requested from the underlying reader. A buffered command uses
    /// <see cref="CommandBehavior.SingleRow"/> or <c>0</c>; a streaming LOB command additionally sets
    /// <see cref="CommandBehavior.SequentialAccess"/>.
    /// </summary>
    public readonly CommandBehavior Behavior = 0;
    /// <summary>
    /// The underlying ADO.NET command. Its parameter values and connection are mutated in place and
    /// reused across executions of the compiled query.
    /// </summary>
    public readonly DbCommand DbCommand;
    private DbConnection? DbCommandConnection;
    private DbTransaction? DbCommandTransaction;
    /// <summary>
    /// The parameter collection of <see cref="DbCommand"/>, cached so parameter lookups do not touch
    /// the command property on every execution.
    /// </summary>
    public readonly DbParameterCollection DbCommandParams;
    /// <summary>
    /// The enumerator currently reading this command, tracked so it can be detached from its context
    /// when the connection is reset.
    /// </summary>
    public ResultSetEnumerator<TResult>? Enumerator;
    /// <summary>
    /// The batch that must run before this command's rows can be read (a lazy temporary-table
    /// materialisation). When set, the execution terminals route through the batch runner instead of
    /// this command's <see cref="DbCommand"/>; the batch's result-bearing query is this command, so
    /// <see cref="PreparedQueryCommand{TResult, IDataRecord}.MapDelegate"/> maps its rows.
    /// </summary>
    internal BatchPlan? PendingBatch { get; set; }
    /// <summary>
    /// Number of rows returned by the most recent execution of the compiled query.
    /// </summary>
    public int LastRowCount;
    /// <summary>
    /// Maps each positional parameter index to its index in <see cref="DbCommandParams"/>, or <c>-1</c>
    /// when the mapping has not been resolved yet. Built lazily on first execution and reused afterwards.
    /// </summary>
    public int[]? ParamMap;
    /// <summary>
    /// The SQL text the command was prepared with, when known.
    /// </summary>
    public readonly string? SqlStmt;
    /// <summary>
    /// Whether the command has no parameters, allowing parameter binding to be skipped.
    /// </summary>
    public readonly bool NoParams;
    /// <summary>
    /// Whether parameter values must be refreshed before each execution rather than assuming the
    /// cached values on <see cref="DbCommand"/> are still current.
    /// </summary>
    public readonly bool NeedsParamRefresh;
    /// <summary>
    /// Wraps an already prepared <see cref="DbCommand"/> and its execution options.
    /// </summary>
    /// <param name="dbCommand">The command to execute; its connection is captured for later resets.</param>
    /// <param name="mapDelegate">Maps each <see cref="IDataRecord"/> to a result, or <see langword="null"/> when no projection is needed.</param>
    /// <param name="options">Prepared-command options such as the SQL text and parameter flags.</param>
    public DbPreparedQueryCommand(DbCommand dbCommand, Func<IDataRecord, TResult>? mapDelegate, PreparedCommandOptions options)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        DbCommandConnection = dbCommand.Connection;
        DbCommandParams = dbCommand.Parameters;
        if (options.SingleRow)
            Behavior = CommandBehavior.SingleRow;
        if (options.SequentialAccess)
            Behavior |= CommandBehavior.SequentialAccess;

        SqlStmt = options.Sql;
        NoParams = options.NoParams;
        NeedsParamRefresh = options.NeedsParamRefresh;
    }
    // private readonly string CommandText;
    //public DbParameterCollection DbCommandParams;
    /// <summary>
    /// Binds <paramref name="params"/> to the command and attaches it to <paramref name="conn"/>,
    /// treating a <see langword="null"/> array as no parameters.
    /// </summary>
    /// <param name="params">The positional parameter values to bind, or <see langword="null"/> for none.</param>
    /// <param name="createParam">Creates a provider parameter when the command has no matching one yet.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "Both branches are reachable: @params may be null at any call site, so the ReadOnlySpan conversion is not dead code; the analyzer cannot model the span conversion.")]
    public DbCommand GetDbCommand(object[]? @params, Func<string, object?, DbParameter> createParam, DbConnection conn)
        => GetDbCommand(@params is null ? ReadOnlySpan<object?>.Empty : @params, createParam, conn);

    /// <summary>
    /// Binds <paramref name="params"/> to the command, caching each parameter's position in
    /// <see cref="ParamMap"/> so subsequent executions skip the provider's parameter lookup, and
    /// attaches the command to <paramref name="conn"/>.
    /// </summary>
    /// <param name="params">The positional parameter values to bind; an empty span binds nothing.</param>
    /// <param name="createParam">Creates a provider parameter from its name and value; the executing command is not passed, preserving the command-unaware contract.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    public DbCommand GetDbCommand(ReadOnlySpan<object?> @params, Func<string, object?, DbParameter> createParam, DbConnection conn)
        => GetDbCommand(@params, createParam, conn, null);

    /// <summary>
    /// Binds <paramref name="params"/> to the command, attaches it to <paramref name="conn"/> and binds
    /// <paramref name="transaction"/> (or clears it when <see langword="null"/>).
    /// </summary>
    /// <param name="params">The positional parameter values to bind, or <see langword="null"/> for none.</param>
    /// <param name="createParam">Creates a provider parameter from its name and value; the executing command is not passed, preserving the command-unaware contract.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <param name="transaction">The transaction to bind, or <see langword="null"/> to clear it.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    public DbCommand GetDbCommand(object[]? @params, Func<string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommand(@params is null ? ReadOnlySpan<object?>.Empty : @params, createParam, conn, transaction);

    /// <summary>
    /// Binds <paramref name="params"/> to the command, caching each parameter's position in
    /// <see cref="ParamMap"/> so subsequent executions skip the provider's parameter lookup, and
    /// attaches the command to <paramref name="conn"/> and <paramref name="transaction"/>.
    /// </summary>
    /// <remarks>
    /// Public compatibility overload: the command-unaware <paramref name="createParam"/> is forwarded to
    /// the shared core as its legacy factory; the core execution paths instead call the internal
    /// command-aware overloads below. Forwarding the delegate directly means this public path allocates
    /// no capturing adapter per call.
    /// </remarks>
    /// <param name="params">The positional parameter values to bind; an empty span binds nothing.</param>
    /// <param name="createParam">Creates a provider parameter from its name and value; the executing command is not passed, preserving the command-unaware contract.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <param name="transaction">The transaction to bind, or <see langword="null"/> to clear it.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    public DbCommand GetDbCommand(ReadOnlySpan<object?> @params, Func<string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params, null, createParam, conn, transaction);

    /// <summary>
    /// Command-aware variant used by the core execution paths: <paramref name="createParam"/> receives
    /// the command the parameter is added to, so a connection whose ADO driver rejects foreign
    /// parameters can mint one through <c>command.CreateParameter()</c>.
    /// </summary>
    /// <param name="params">The positional parameter values to bind; an empty span binds nothing.</param>
    /// <param name="createParam">Creates a provider parameter bound to the executing command.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <param name="transaction">The transaction to bind, or <see langword="null"/> to clear it.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    internal DbCommand GetDbCommand(ReadOnlySpan<object?> @params, Func<DbCommand, string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params, createParam, null, conn, transaction);

    /// <summary>
    /// Command-aware variant used by the core execution paths; see
    /// <see cref="GetDbCommand(ReadOnlySpan{object?}, Func{DbCommand, string, object?, DbParameter}, DbConnection, DbTransaction?)"/>.
    /// </summary>
    /// <param name="params">The positional parameter values to bind, or <see langword="null"/> for none.</param>
    /// <param name="createParam">Creates a provider parameter bound to the executing command.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <param name="transaction">The transaction to bind, or <see langword="null"/> to clear it.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    internal DbCommand GetDbCommand(object[]? @params, Func<DbCommand, string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params is null ? ReadOnlySpan<object?>.Empty : @params, createParam, null, conn, transaction);

    /// <summary>
    /// Shared binding core for both the command-unaware public overloads and the command-aware
    /// execution-path overloads. Exactly one of <paramref name="aware"/> and <paramref name="legacy"/>
    /// is non-null: <paramref name="legacy"/> is the public 2-arg factory, invoked directly so the
    /// public path allocates no capturing adapter; <paramref name="aware"/> receives the executing
    /// command so a connection whose ADO driver rejects foreign parameters can mint one through
    /// <c>command.CreateParameter()</c>.
    /// </summary>
    /// <param name="params">The positional parameter values to bind; an empty span binds nothing.</param>
    /// <param name="aware">The command-aware factory, or <see langword="null"/> on the public path.</param>
    /// <param name="legacy">The command-unaware factory, or <see langword="null"/> on the execution path.</param>
    /// <param name="conn">The connection the returned command must be attached to.</param>
    /// <param name="transaction">The transaction to bind, or <see langword="null"/> to clear it.</param>
    /// <returns>The prepared command, ready to execute on <paramref name="conn"/>.</returns>
    internal DbCommand GetDbCommandCore(ReadOnlySpan<object?> @params, Func<DbCommand, string, object?, DbParameter>? aware, Func<string, object?, DbParameter>? legacy, DbConnection conn, DbTransaction? transaction)
    {
        // Real runtime guard, not a Debug.Assert: the invariant must also hold in Release, where a
        // malformed call must fail loudly instead of silently dereferencing the wrong (or a null)
        // factory for every parameter.
        if ((aware is null) == (legacy is null))
        {
            throw new ArgumentException(
                "Exactly one of the command-aware and legacy parameter factories must be supplied.",
                aware is null ? nameof(legacy) : nameof(aware));
        }

        var cmd = DbCommand;
        var parameters = DbCommandParams;//cmd.Parameters;

        if (!@params.IsEmpty)
        {
            var pLength = @params.Length;
            if (ParamMap is null)
            {
                ParamMap = new int[pLength];
                for (var i = 0; i < pLength; i++) ParamMap[i] = -1;
            }
            Debug.Assert(pLength == ParamMap.Length, "Arrays must be equal size", "{0} and {1} found", pLength, ParamMap.Length);

            for (var i = 0; i < pLength; i++)
            {
                var idx = ParamMap[i];

                string? paramName = null;
                if (idx < 0)
                {
                    paramName = NormParam.GetName(i);
                    // parameters.IndexOf is a provider-side linear scan, but ParamMap caches the
                    // result below, so it runs at most once per parameter per compiled command.
                    idx = parameters.IndexOf(paramName);

                    if (idx >= 0)
                    {
                        ParamMap[i] = idx;
                    }
                }

                if (idx >= 0)
                {
                    // Normalize null to DBNull so providers that reject null parameter values
                    // (Microsoft.Data.Sqlite) work and the equality check stays stable.
                    var newValue = @params[i] ?? DBNull.Value;
                    if (parameters[idx].Value != newValue)
                    {
                        // if (cmd == DbCommand)
                        // {
                        // cmd = conn.CreateCommand();
                        // cmd.CommandText = CommandText;
                        // foreach (var p in parameters) cmd.Parameters.Add(p);
                        // parameters = cmd.Parameters;
                        //}
                        parameters[idx].Value = newValue;
                    }
                }
                else
                {
                    // if (cmd == DbCommand)
                    // {
                    //     cmd = conn.CreateCommand();
                    //     cmd.CommandText = CommandText;
                    //     foreach (var p in parameters) cmd.Parameters.Add(p);
                    //     parameters = cmd.Parameters;
                    // }

                    ParamMap[i] = parameters.Count;
                    parameters.Add(aware is not null
                        ? aware(cmd, paramName!, @params[i])
                        : legacy!(paramName!, @params[i]));
                }
            }
        }

        if (DbCommandConnection != conn)
        {
            DbCommandConnection = conn;
            cmd.Connection = conn;
        }

        if (DbCommandTransaction != transaction)
        {
            DbCommandTransaction = transaction;
            cmd.Transaction = transaction;
        }

        return cmd;
    }
    /// <summary>
    /// Detaches the command from its connection and any active enumerator from its context so the
    /// compiled command can be rebound to another connection on a later execution.
    /// </summary>
    /// <param name="conn">The connection being reset; detached only when it is the command's current one.</param>
    /// <param name="dbContext">The context the active enumerator is reading from.</param>
    public void ResetConnection(DbConnection conn, IDataContext dbContext)
    {
        if (DbCommand?.Connection == conn)
        {
            DbCommand.Connection = null;
            DbCommand.Transaction = null;
            DbCommandTransaction = null;
        }

        Enumerator?.DetachFrom(dbContext);

        DbCommandConnection = null;
    }
}
