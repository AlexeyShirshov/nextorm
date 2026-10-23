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
    /// The canonical parameter collection of the statement, sorted by canonical binding ordinal. It
    /// stores the same <see cref="DbParameter"/> objects the command binds, but it is never shortened,
    /// so a checkout can restore the complete driver collection after an execution lowered some text
    /// bindings into literals.
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
    /// The immutable guarded parameter-refresh recipe for this plan, or <see langword="null"/> when the
    /// plan does not support the scalar subset and the original <c>ExtractParams</c> refresh path must be
    /// used. The recipe is immutable and retains neither the miss-time closure nor a mutable command or
    /// enumerator.
    /// </summary>
    internal ParamRefreshRecipe? ParamRecipe { get; private set; }

    /// <summary>
    /// The immutable parameter-occurrence template of the statement the command was prepared with, or
    /// <see langword="null"/> when the command carries no planner-owned emission (caller-owned raw SQL)
    /// or when its render could not be recorded reliably. Created once by the planner at plan
    /// construction; it is never mutated, so every execution of the cached command composes from the
    /// same original statement.
    /// </summary>
    internal SqlParameterTemplate? ParameterTemplate { get; private set; }

    /// <summary>
    /// The string-literal policy the statement was planned with, or <see langword="null"/> when the
    /// statement has no template. Execution-time specialization is enabled only for the MySQL-family
    /// policy; every other policy keeps the prepared statement byte-identical.
    /// </summary>
    private SqlStringLiteralPolicy? _stringLiteralPolicy;

    /// <summary>
    /// Whether the driver-visible command still carries a previous execution's specialization. The next
    /// checkout restores the canonical text and parameter collection before refreshing values.
    /// </summary>
    private bool _specialized;

    /// <summary>
    /// Whether a failed restoration left the command in a state that must never be returned again.
    /// </summary>
    private bool _stateBroken;

    /// <summary>
    /// Sets the immutable parameter-refresh recipe. Called exactly once, by the planner at plan
    /// construction; the characterization tests use the same seam to install an intentionally
    /// incompatible recipe and prove the mismatch-safe fallback. The recipe itself is never mutated.
    /// </summary>
    /// <param name="recipe">The recipe, or <see langword="null"/> to keep the original extraction path.</param>
    internal void SetParamRecipe(ParamRefreshRecipe? recipe) => ParamRecipe = recipe;
    /// <summary>
    /// Sets the immutable parameter-occurrence template and the string-literal policy the statement was
    /// planned with. Called exactly once, by the planner at plan construction, for a statement the ORM
    /// rendered itself. Neither value is ever mutated.
    /// </summary>
    /// <param name="template">The statement template, or <see langword="null"/> when none could be recorded.</param>
    /// <param name="policy">The policy resolved for the planning dialect, or <see langword="null"/>.</param>
    internal void SetParameterTemplate(SqlParameterTemplate? template, SqlStringLiteralPolicy? policy)
    {
        ParameterTemplate = template;
        _stringLiteralPolicy = policy;
    }
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
        DbCommandParams = new CanonicalParameterCollection(dbCommand.Parameters);
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

        if (_stateBroken)
        {
            throw new InvalidOperationException(
                "The prepared command's canonical parameter state could not be restored after a previous execution; " +
                "the plan must be rebuilt instead of executing a partially restored command.");
        }

        // Restore first: a previous execution may have lowered text bindings into literals and removed
        // them from the driver collection. The canonical text and the complete canonical parameter set
        // must be back in place before any value refresh runs, so every index-based access is valid.
        RestoreCanonicalCommand();

        var cmd = DbCommand;
        var parameters = cmd.Parameters;

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
                        parameters[idx].Value = newValue;
                    }
                }
                else
                {
                    var created = aware is not null
                        ? aware(cmd, paramName!, @params[i])
                        : legacy!(paramName!, @params[i]);
                    ParamMap[i] = DbCommandParams.Count;
                    parameters.Add(created);
                    // The runtime minted a parameter the planning render did not produce; the canonical
                    // set has to know it too, or the next restoration would drop it.
                    DbCommandParams.Add(created);
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

        if (ParameterTemplate is { } template && ReferenceEquals(_stringLiteralPolicy, SqlStringLiteralPolicy.MySql))
            SpecializeMySql(template);

        return cmd;
    }

    /// <summary>
    /// Restores the canonical statement text and the complete canonical parameter set when a previous
    /// execution specialized the driver-visible command. A no-op otherwise.
    /// </summary>
    private void RestoreCanonicalCommand(bool force = false)
    {
        if (!_specialized && !force)
            return;

        try
        {
            if (ParameterTemplate is { } template)
                DbCommand.CommandText = template.StatementText;

            var driver = DbCommand.Parameters;
            var canonical = DbCommandParams;
            if (driver.Count != canonical.Count)
            {
                driver.Clear();
                for (var i = 0; i < canonical.Count; i++)
                    driver.Add(canonical[i]);
            }

            _specialized = false;
        }
        catch
        {
            // A partial restoration must never be handed to an execution: the command cannot be used
            // again, so the next checkout fails instead of binding values against a broken collection.
            _stateBroken = true;
            throw;
        }
    }

    /// <summary>
    /// Specializes the MySQL-family statement for this execution: every planner-owned value occurrence
    /// whose final bound value is text becomes a charset-introduced UTF-8 hex literal, and a binding is
    /// removed from the driver collection only when every one of its occurrences was lowered. The
    /// composition and the replacement set are computed before the command is mutated, so a failure
    /// restores the canonical state instead of leaving a half-specialized command.
    /// </summary>
    /// <param name="template">The immutable statement template of this prepared command.</param>
    private void SpecializeMySql(SqlParameterTemplate template)
    {
        var occurrences = template.Occurrences;
        if (occurrences.Length == 0)
            return;

        var replacements = new string?[occurrences.Length];
        var occurrenceCounts = new int[template.BindingCount];
        var loweredCounts = new int[template.BindingCount];
        var anyLowered = false;

        for (var i = 0; i < occurrences.Length; i++)
        {
            var occurrence = occurrences[i];
            occurrenceCounts[occurrence.BindingId]++;

            // Grammar and pattern roles keep the original placeholder: D6 owns their spelling.
            if (occurrence.Role != SqlStringLiteralRole.Value)
                continue;

            var parameter = DbCommandParams[occurrence.BindingId];
            switch (parameter.Value)
            {
                case string text:
                    replacements[i] = SqlStringLiteralPolicy.MySql.Render(text);
                    loweredCounts[occurrence.BindingId]++;
                    anyLowered = true;
                    break;
                case char value:
                    replacements[i] = SqlStringLiteralPolicy.MySql.Render(value.ToString());
                    loweredCounts[occurrence.BindingId]++;
                    anyLowered = true;
                    break;
            }
        }

        if (!anyLowered)
            return;

        var composed = template.Compose(replacements);

        try
        {
            DbCommand.CommandText = composed;

            var driver = DbCommand.Parameters;
            for (var bindingId = 0; bindingId < template.BindingCount; bindingId++)
            {
                if (occurrenceCounts[bindingId] == 0 || loweredCounts[bindingId] != occurrenceCounts[bindingId])
                    continue;

                var index = IndexOfReference(driver, DbCommandParams[bindingId]);
                if (index >= 0)
                    driver.RemoveAt(index);
            }

            _specialized = true;
        }
        catch
        {
            // Installation is transactional with respect to the cached command: restore the canonical
            // text and complete parameter collection before propagating the failure. There is never a
            // text-binding fallback.
            RestoreCanonicalCommand(force: true);
            throw;
        }
    }

    private static int IndexOfReference(DbParameterCollection collection, DbParameter parameter)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            if (ReferenceEquals(collection[i], parameter))
                return i;
        }

        return -1;
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
