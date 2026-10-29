using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Prepared query command backed by an ADO.NET <see cref="System.Data.IDataRecord"/> reader.
/// <para>
/// Immutable preparation data (mapper, reader behavior, SQL text, parameter flags, refresh recipe and
/// the planned batch) lives on this object. Everything that depends on the connection an execution
/// runs on lives in a <see cref="CommandState"/> held by a weak table keyed on that
/// connection, so one prepared command can be executed concurrently through contexts on distinct
/// connections while each connection keeps its own command, parameter positions and execution lease.
/// Nothing here holds a connection, a transaction, a context or a reader.
/// </para>
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
    /// The SQL text the command was prepared with, when known.
    /// </summary>
    public readonly string? SqlStmt;
    /// <summary>
    /// Whether the command has no parameters, allowing parameter binding to be skipped.
    /// </summary>
    public readonly bool NoParams;
    /// <summary>
    /// Whether parameter values must be refreshed before each execution rather than assuming the
    /// values baked into the plan are still current.
    /// </summary>
    public readonly bool NeedsParamRefresh;

    private readonly PreparedCommandFactory _factory;
    private readonly PreparedCommandCache _cachePolicy;
    private readonly ConditionalWeakTable<DbConnection, CommandState> _states = new();
    private readonly ConditionalWeakTable<DbConnection, CommandState>.CreateValueCallback _createState;
    private readonly DbCommand? _diagnosticCommand;
    private readonly PlanCommand _planCommand;
    private Memo? _memo;
    private int[]? _planParamMap;
    private CanonicalParameterCollection? _canonicalParams;
    private int _createdCommandCount;
    private int _disposedCommandCount;

    /// <summary>
    /// The plan's command-shaped diagnostic view: SQL text, timeout and connection-free parameter
    /// descriptors. It is never executed and never attached to a connection; SQL-generation tests and
    /// the guarded refresh recipe read it. Execution always runs a per-connection command.
    /// </summary>
    internal DbCommand DbCommand => _diagnosticCommand ?? _planCommand;

    /// <summary>
    /// The canonical parameter set of the statement, in binding order: the same
    /// <see cref="DbParameter"/> objects the plan (or the diagnostic command) carries, but never
    /// shortened. An execution may lower text bindings into literals and remove exactly those from the
    /// driver collection, so the complete set has to survive for the next checkout and for every
    /// index-by-binding access. Values are read and written through the same objects, so a refresh is
    /// visible here without copying.
    /// </summary>
    internal DbParameterCollection DbCommandParams =>
        LazyInitializer.EnsureInitialized(ref _canonicalParams, () => new CanonicalParameterCollection(DbCommand.Parameters));

    /// <summary>The statement text every per-connection command is created with.</summary>
    internal string CommandText => _factory.CommandText;

    /// <summary>The plan-time parameter seeds, in placeholder order.</summary>
    internal IReadOnlyList<Parameter> ParamSeeds => _factory.Seeds;

    /// <summary>
    /// The batch that must run before this command's rows can be read (a lazy temporary-table
    /// materialisation). When set, the execution terminals route through the batch runner instead of
    /// this command; the batch's result-bearing query is this command, so
    /// <see cref="PreparedQueryCommand{TResult, IDataRecord}.MapDelegate"/> maps its rows. Immutable
    /// planning information: it is written once, by the planner/context, before publication.
    /// </summary>
    internal BatchPlan? PendingBatch { get; set; }

    /// <summary>
    /// The immutable guarded parameter-refresh recipe for this plan, or <see langword="null"/> when the
    /// plan does not support the scalar subset and the original <c>ExtractParams</c> refresh path must be
    /// used.
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
    /// The string-literal policy the statement was planned with, or <see langword="null"/> only for a
    /// command constructed outside the planner. Execution-time specialization is enabled only for the
    /// MySQL-family policy; every other policy keeps the prepared statement byte-identical. The
    /// PostgreSQL policy enables the final-value NUL guard on every checkout.
    /// </summary>
    private SqlStringLiteralPolicy? _stringLiteralPolicy;

    /// <summary>
    /// Per-command bookkeeping of the diagnostic checkout path only (the parameter-factory contract and
    /// the SQL-generation tests bind onto the plan's diagnostic command). Execution never uses it: every
    /// execution binds onto its connection's own command and keeps that command's restore bookkeeping in
    /// its <see cref="CommandState"/>.
    /// </summary>
    private CommandState? _diagnosticState;

    /// <summary>
    /// The command-reuse policy this prepared command was created with, as supplied to
    /// <see cref="PreparedCommandOptions.Cache"/>. Read-only: the policy is never re-derived at execution.
    /// </summary>
    internal PreparedCommandCache CachePolicy => _cachePolicy;

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
    /// Whether this prepared command may produce streaming enumerators. Set when preparation requested
    /// an enumerator; a cached plan first created for a buffered terminal is promoted on the first
    /// streaming request (the plan is reused, not rebuilt).
    /// </summary>
    internal bool StreamingEnabled { get; private set; }

    /// <summary>
    /// Creates a fresh execution-local enumerator. The factory captures this prepared command and the
    /// logging switches only — never the planner, the context or a connection.
    /// </summary>
    internal Func<ResultSetEnumerator<TResult>>? EnumeratorFactory { get; private set; }

    /// <summary>Number of execution commands this prepared command has created. Diagnostic only.</summary>
    internal int CreatedCommandCount => Volatile.Read(ref _createdCommandCount);

    /// <summary>Number of execution commands this prepared command has disposed. Diagnostic only.</summary>
    internal int DisposedCommandCount => Volatile.Read(ref _disposedCommandCount);

    /// <summary>
    /// Number of live per-connection states, read from the weak table itself so an entry reclaimed by
    /// the garbage collector is no longer counted. Diagnostic only.
    /// </summary>
    internal int LiveStateCount => System.Linq.Enumerable.Count(_states);

    /// <summary>
    /// The command currently published for <paramref name="connection"/>, or <see langword="null"/> when
    /// the state is idle or does not exist. Test/diagnostic seam — never used by execution.
    /// </summary>
    internal DbCommand? PeekCommand(DbConnection connection)
        => _states.TryGetValue(connection, out var state) ? state.Command : null;

    /// <summary>The lease token currently owning <paramref name="connection"/>'s state, or <c>0</c>. Test seam.</summary>
    internal int GetLeaseOwnerForTest(DbConnection connection)
        => _states.TryGetValue(connection, out var state) ? Volatile.Read(ref state.LeaseOwner) : 0;

    /// <summary>
    /// Wraps an already prepared command and its execution options.
    /// </summary>
    /// <param name="factory">Creates an equivalent command per execution connection.</param>
    /// <param name="planParameters">The connection-free plan parameter descriptors.</param>
    /// <param name="mapDelegate">Maps each <see cref="IDataRecord"/> to a result, or <see langword="null"/> when no projection is needed.</param>
    /// <param name="options">Prepared-command options such as the SQL text and parameter flags.</param>
    internal DbPreparedQueryCommand(
        PreparedCommandFactory factory,
        DbParameter[] planParameters,
        Func<IDataRecord, TResult>? mapDelegate,
        PreparedCommandOptions options)
        : base(mapDelegate)
    {
        _factory = factory;
        _cachePolicy = options.Cache;
        _createState = static _ => new CommandState();
        _planCommand = new PlanCommand(factory.CommandText, factory.CommandTimeout, planParameters);

        if (options.SingleRow)
            Behavior = CommandBehavior.SingleRow;
        if (options.SequentialAccess)
            Behavior |= CommandBehavior.SequentialAccess;

        SqlStmt = options.Sql;
        NoParams = options.NoParams;
        NeedsParamRefresh = options.NeedsParamRefresh;
    }

    /// <summary>
    /// Test/diagnostic seam: wraps an already-minted command as the plan's diagnostic command so the
    /// parameter-factory contract can be exercised without a provider connection. Production always
    /// uses the factory constructor above.
    /// </summary>
    internal DbPreparedQueryCommand(DbCommand diagnosticCommand, Func<IDataRecord, TResult>? mapDelegate, PreparedCommandOptions options)
        : this(new PreparedCommandFactory(diagnosticCommand.CommandText, null, []), [], mapDelegate, options)
    {
        _diagnosticCommand = diagnosticCommand;
    }

    /// <summary>
    /// Sets the immutable parameter-refresh recipe. Called exactly once, by the planner at plan
    /// construction; the characterization tests use the same seam to install an intentionally
    /// incompatible recipe and prove the mismatch-safe fallback.
    /// </summary>
    internal void SetParamRecipe(ParamRefreshRecipe? recipe) => ParamRecipe = recipe;

    /// <summary>Enables streaming enumerators on this prepared command. Called once, by the planner.</summary>
    internal void EnableStreaming(Func<ResultSetEnumerator<TResult>> factory)
    {
        EnumeratorFactory = factory;
        StreamingEnabled = true;
    }

    /// <summary>
    /// Acquires this connection's execution lease and returns the bound command for exactly one
    /// execution. The lease is acquired <b>before</b> any command state is mutated; overlapping
    /// executions of this prepared command on the same connection fail promptly, while different
    /// connections proceed independently (there is no prepared-object-wide lock).
    /// </summary>
    /// <param name="connection">The connection the execution runs on; it keys the per-connection state.</param>
    /// <param name="createParam">The executing context's command-aware parameter factory.</param>
    /// <param name="params">Positional runtime parameter values, or <see langword="null"/> for none.</param>
    /// <param name="transaction">The transaction active for this execution, or <see langword="null"/>.</param>
    /// <param name="onCommandCreated">Raises the command-initialized event when a command is minted.</param>
    /// <returns>The lease; the caller must dispose (success) or fail (pre-transfer failure) it exactly once.</returns>
    /// <exception cref="InvalidOperationException">This prepared command is already executing on the same connection.</exception>
    internal CommandLease Acquire(
        DbConnection connection,
        Func<DbCommand, string, object?, DbParameter> createParam,
        object[]? @params,
        DbTransaction? transaction,
        Action<DbCommand>? onCommandCreated)
        => Acquire(connection, createParam, @params is null ? ReadOnlySpan<object?>.Empty : @params, transaction, onCommandCreated);

    /// <summary>Span overload of the array-parameter <c>Acquire</c>.</summary>
    internal CommandLease Acquire(
        DbConnection connection,
        Func<DbCommand, string, object?, DbParameter> createParam,
        ReadOnlySpan<object?> @params,
        DbTransaction? transaction,
        Action<DbCommand>? onCommandCreated)
    {
        var state = GetOrCreateState(connection);
        var token = NextLeaseToken(state);
        if (Interlocked.CompareExchange(ref state.LeaseOwner, token, 0) != 0)
        {
            throw new InvalidOperationException(
                "This prepared command is already executing on the same connection. One prepared command allows a "
                + "single concurrent operation per connection: complete or dispose the active operation first, or "
                + "execute through a different connection (executions on distinct connections run concurrently).");
        }

        var nonePolicy = _cachePolicy == PreparedCommandCache.None;
        var lease = new CommandLease(this, connection, state, token, nonePolicy);
        try
        {
            DbCommand command;
            var created = false;

            if (nonePolicy)
            {
                command = _factory.Create(connection, createParam);
                created = true;
                // The command is execution-local: the cached positional map belongs to the previous
                // command on this connection and its collection indices no longer apply.
                state.ParamMap = null;
            }
            else if (state.Command is { } existing)
            {
                command = existing;
            }
            else
            {
                command = _factory.Create(connection, createParam);
                state.Command = command;
                state.Canonical ??= new CanonicalParameterCollection(command.Parameters);
                created = true;
            }

            lease.SetCommand(command, created);
            if (created)
            {
                Interlocked.Increment(ref _createdCommandCount);
                onCommandCreated?.Invoke(command);
            }

            Bind(command, state, createParam, @params, connection, transaction);
            return lease;
        }
        catch
        {
            lease.Fail();
            throw;
        }
    }

    /// <summary>
    /// Binds the plan's refreshed captured values and the positional runtime parameters to
    /// <paramref name="command"/>, validates the transaction belongs to the executing connection, and
    /// verifies the command is attached to it.
    /// </summary>
    private void Bind(
        DbCommand command,
        CommandState state,
        Func<DbCommand, string, object?, DbParameter> createParam,
        ReadOnlySpan<object?> @params,
        DbConnection connection,
        DbTransaction? transaction)
    {
        // A command-owned by the connection state carries restore bookkeeping; an execution-local
        // (None policy) command is canonical by construction and has nothing to restore.
        var bookkeeping = ReferenceEquals(state.Command, command) ? state : null;

        if (bookkeeping is { Broken: true })
        {
            throw new InvalidOperationException(
                "The prepared command's canonical parameter state could not be restored after a previous execution; " +
                "the plan must be rebuilt instead of executing a partially restored command.");
        }

        // Restore first: the previous execution on this connection may have lowered text bindings into
        // literals and removed them from the driver collection. The canonical text and the complete
        // canonical parameter set must be back in place before any value refresh runs, so every
        // index-based access is valid.
        RestoreCanonicalCommand(command, bookkeeping);

        // A cached plan's captured values are refreshed on the plan descriptors before execution; copy
        // them onto this connection's own command.
        if (NeedsParamRefresh)
            ApplyPlanValues(command, state);

        if (!@params.IsEmpty)
            BindRuntimeParameters(command, state, bookkeeping, createParam, @params);

        if (!ReferenceEquals(command.Connection, connection))
            command.Connection = connection;

        if (transaction is null)
        {
            if (command.Transaction is not null)
                command.Transaction = null;
        }
        else
        {
            // Reading Connection also validates a disposed/changed transaction: a stale transaction must
            // fail this execution instead of being silently attached to another connection.
            if (!ReferenceEquals(transaction.Connection, connection))
            {
                throw new InvalidOperationException(
                    "The transaction supplied to the execution does not belong to the executing connection; a prepared "
                    + "command never reuses a transaction bound to a different connection.");
            }

            if (!ReferenceEquals(command.Transaction, transaction))
                command.Transaction = transaction;
        }

        // The final values are now in the driver collection (cold bind or cache refresh), so PostgreSQL's
        // NUL rejection runs here, before the command can be executed. It runs on every checkout: a second
        // execution that receives a NUL-bearing value throws instead of reaching the server. PostgreSQL
        // never specializes, so this guard is independent of the MySQL branch below.
        PostgresParameterGuard.Validate(_stringLiteralPolicy, command.Parameters);

        if (ParameterTemplate is { } template && ReferenceEquals(_stringLiteralPolicy, SqlStringLiteralPolicy.MySql))
            SpecializeMySql(template, command, bookkeeping);
    }

    private void ApplyPlanValues(DbCommand command, CommandState state)
    {
        var planParameters = DbCommandParams;
        if (planParameters.Count == 0)
            return;

        var parameters = command.Parameters;
        var map = state.PlanParamMap;
        if (map is null || map.Length != planParameters.Count)
        {
            map = new int[planParameters.Count];
            for (var i = 0; i < map.Length; i++)
                map[i] = -1;
            state.PlanParamMap = map;
        }

        for (var i = 0; i < planParameters.Count; i++)
        {
            var name = planParameters[i].ParameterName;
            var idx = map[i];
            if (idx < 0 || idx >= parameters.Count || !string.Equals(parameters[idx].ParameterName, name, StringComparison.Ordinal))
            {
                idx = parameters.IndexOf(name);
                map[i] = idx;
            }

            if (idx >= 0)
                parameters[idx].Value = planParameters[i].Value ?? DBNull.Value;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "Both branches are reachable: @params may be empty, so the loop is not dead code; the analyzer cannot model the span conversion.")]
    private static void BindRuntimeParameters(
        DbCommand command,
        CommandState state,
        CommandState? bookkeeping,
        Func<DbCommand, string, object?, DbParameter> createParam,
        ReadOnlySpan<object?> @params)
    {
        var parameters = command.Parameters;
        var pLength = @params.Length;
        var map = state.ParamMap;
        if (map is null || map.Length < pLength)
        {
            var next = new int[pLength];
            if (map is not null)
                Array.Copy(map, next, map.Length);
            for (var i = map?.Length ?? 0; i < pLength; i++)
                next[i] = -1;
            map = next;
            state.ParamMap = map;
        }

        for (var i = 0; i < pLength; i++)
        {
            var idx = map[i];

            string? paramName = null;
            if (idx < 0)
            {
                paramName = NormParam.GetName(i);
                // parameters.IndexOf is a provider-side linear scan, but the map caches the result
                // below, so it runs at most once per parameter per connection.
                idx = parameters.IndexOf(paramName);

                if (idx >= 0)
                    map[i] = idx;
            }

            if (idx >= 0)
            {
                // Normalize null to DBNull so providers that reject null parameter values
                // (Microsoft.Data.Sqlite) work and the equality check stays stable.
                var newValue = @params[i] ?? DBNull.Value;
                if (parameters[idx].Value != newValue)
                    parameters[idx].Value = newValue;
            }
            else
            {
                map[i] = parameters.Count;
                var created = createParam(command, paramName!, @params[i]);
                parameters.Add(created);
                // The runtime minted a parameter the planning render did not produce; the canonical set
                // has to know it too, or a later restoration would drop it and the cached positional map
                // would point past the end of the collection.
                bookkeeping?.Canonical?.Add(created);
            }
        }
    }

    private CommandState GetOrCreateState(DbConnection connection)
    {
        var memo = Volatile.Read(ref _memo);
        if (memo is not null && memo.TryGet(connection, out var cached))
            return cached!;

        var state = _states.GetValue(connection, _createState);
        Volatile.Write(ref _memo, new Memo(connection, state));
        return state;
    }

    private static int NextLeaseToken(CommandState state)
    {
        var token = Interlocked.Increment(ref state.NextLeaseToken);
        // 0 means "no owner"; skip it on overflow.
        return token == 0 ? Interlocked.Increment(ref state.NextLeaseToken) : token;
    }

    /// <summary>
    /// Records the disposal of an execution-owned (<c>None</c>) command. Called by a lease, or by a
    /// streaming enumerator that took the command over from its lease, so the creation/disposal counters
    /// stay balanced for both buffered and streaming executions.
    /// </summary>
    internal void OnLeaseCommandDisposed() => Interlocked.Increment(ref _disposedCommandCount);

    /// <summary>
    /// Evicts the state for <paramref name="connection"/> and disposes its command. Used when the
    /// command must never be reused — a failed execution poisoned it (a partial read, a rejected
    /// transaction). The command is disposed here because its connection is still the one that owns it.
    /// </summary>
    internal void Evict(DbConnection connection)
    {
        CommandState? state = null;
        if (_states.TryGetValue(connection, out var found))
        {
            state = found;
            _states.Remove(connection);
        }

        if (state?.Command is { } command)
        {
            state.Command = null;
            command.Connection = null;
            command.Transaction = null;
            command.Dispose();
            Interlocked.Increment(ref _disposedCommandCount);
        }

        ClearMemo(connection);
    }

    private void ClearMemo(DbConnection connection)
    {
        var memo = Volatile.Read(ref _memo);
        if (memo is not null && memo.Targets(connection))
            Volatile.Write(ref _memo, null);
    }

    /// <summary>
    /// Drops this connection's execution state without touching its command. Called when the connection
    /// itself goes away (closed or disposed): a command attached to a connection belongs to that
    /// connection — Microsoft.Data.Sqlite disposes the commands attached to it in <c>Close</c> — and a
    /// provider-disposed command must never be touched again (Npgsql throws
    /// <see cref="ObjectDisposedException"/> from <c>set_DbConnection</c>).
    /// </summary>
    void IDbCommandHolder.ResetConnection(DbConnection conn, IDataContext dbContext) => Forget(conn);

    /// <summary>
    /// Drops the state and memo of <paramref name="connection"/> without touching the command it held.
    /// </summary>
    private void Forget(DbConnection connection)
    {
        _states.Remove(connection);
        ClearMemo(connection);
    }

    /// <inheritdoc cref="IDbCommandHolder.EvictConnection"/>
    void IDbCommandHolder.EvictConnection(DbConnection conn) => Evict(conn);

    /// <inheritdoc cref="IDbCommandHolder.OnCommandDisposed"/>
    void IDbCommandHolder.OnCommandDisposed() => OnLeaseCommandDisposed();

    /// <summary>
    /// Diagnostic-only binding used by the parameter-allocation benchmark and by the parameter-factory
    /// contract tests: applies positional parameters to the plan's diagnostic command. Execution never
    /// uses this path — it binds onto a per-connection command inside <c>Acquire</c>.
    /// </summary>
    internal DbCommand GetDbCommand(ReadOnlySpan<object?> @params, Func<string, object?, DbParameter> createParam, DbConnection conn)
        => GetDbCommandCore(@params, null, createParam, conn, null);

    /// <inheritdoc cref="GetDbCommand(ReadOnlySpan{object?}, Func{string, object?, DbParameter}, DbConnection)"/>
    internal DbCommand GetDbCommand(object[]? @params, Func<string, object?, DbParameter> createParam, DbConnection conn)
        => GetDbCommandCore(@params is null ? ReadOnlySpan<object?>.Empty : @params, null, createParam, conn, null);

    /// <inheritdoc cref="GetDbCommand(ReadOnlySpan{object?}, Func{string, object?, DbParameter}, DbConnection)"/>
    internal DbCommand GetDbCommand(ReadOnlySpan<object?> @params, Func<string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params, null, createParam, conn, transaction);

    /// <inheritdoc cref="GetDbCommand(ReadOnlySpan{object?}, Func{string, object?, DbParameter}, DbConnection)"/>
    internal DbCommand GetDbCommand(object[]? @params, Func<string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params is null ? ReadOnlySpan<object?>.Empty : @params, null, createParam, conn, transaction);

    /// <summary>
    /// Diagnostic command-aware binding: <paramref name="createParam"/> receives the diagnostic command
    /// the parameter is added to.
    /// </summary>
    internal DbCommand GetDbCommand(ReadOnlySpan<object?> @params, Func<DbCommand, string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params, createParam, null, conn, transaction);

    /// <inheritdoc cref="GetDbCommand(ReadOnlySpan{object?}, Func{DbCommand, string, object?, DbParameter}, DbConnection, DbTransaction?)"/>
    internal DbCommand GetDbCommand(object[]? @params, Func<DbCommand, string, object?, DbParameter> createParam, DbConnection conn, DbTransaction? transaction)
        => GetDbCommandCore(@params is null ? ReadOnlySpan<object?>.Empty : @params, createParam, null, conn, transaction);

    /// <summary>
    /// Shared diagnostic binding core. Exactly one parameter factory must be supplied; the connection
    /// and transaction arguments are accepted for source compatibility but never attached, because this
    /// path must not root a connection in the prepared plan.
    /// </summary>
    internal DbCommand GetDbCommandCore(
        ReadOnlySpan<object?> @params,
        Func<DbCommand, string, object?, DbParameter>? aware,
        Func<string, object?, DbParameter>? legacy,
        DbConnection conn,
        DbTransaction? transaction)
    {
        if ((aware is null) == (legacy is null))
        {
            throw new ArgumentException(
                "Exactly one of the command-aware and legacy parameter factories must be supplied.",
                aware is null ? nameof(legacy) : nameof(aware));
        }

        // The diagnostic checkout keeps its own restore bookkeeping: it mutates the plan's diagnostic
        // command exactly like an execution mutates a per-connection one, so the next checkout has to see
        // the canonical text and the complete parameter set again.
        var diagnosticState = _diagnosticState ??= new CommandState();

        if (diagnosticState.Broken)
        {
            throw new InvalidOperationException(
                "The prepared command's canonical parameter state could not be restored after a previous execution; " +
                "the plan must be rebuilt instead of executing a partially restored command.");
        }

        var cmd = DbCommand;
        diagnosticState.Canonical ??= new CanonicalParameterCollection(cmd.Parameters);

        // Restore first: a previous checkout may have lowered text bindings into literals and removed
        // them from the driver collection. The canonical text and the complete canonical parameter set
        // must be back in place before any value refresh runs, so every index-based access is valid.
        RestoreCanonicalCommand(cmd, diagnosticState);

        var parameters = cmd.Parameters;

        var pLength = @params.Length;
        if (_planParamMap is null)
        {
            _planParamMap = new int[pLength];
            for (var i = 0; i < pLength; i++) _planParamMap[i] = -1;
        }

        for (var i = 0; i < pLength; i++)
        {
            var idx = _planParamMap[i];

            string? paramName = null;
            if (idx < 0)
            {
                paramName = NormParam.GetName(i);
                idx = parameters.IndexOf(paramName);

                if (idx >= 0)
                    _planParamMap[i] = idx;
            }

            if (idx >= 0)
            {
                var newValue = @params[i] ?? DBNull.Value;
                if (parameters[idx].Value != newValue)
                    parameters[idx].Value = newValue;
            }
            else
            {
                _planParamMap[i] = parameters.Count;
                var created = aware is not null
                    ? aware(cmd, paramName!, @params[i])
                    : legacy!(paramName!, @params[i]);
                parameters.Add(created);
                // The runtime minted a parameter the planning render did not produce; the canonical set
                // has to know it too, or the next restoration would drop it.
                diagnosticState.Canonical!.Add(created);
            }
        }

        // The final values are now in the driver collection (cold bind or cache refresh), so PostgreSQL's
        // NUL rejection runs here, before the command can be executed. It runs on every checkout: a second
        // execution that receives a NUL-bearing value throws instead of reaching the server. PostgreSQL
        // never specializes, so this guard is independent of the MySQL branch below.
        PostgresParameterGuard.Validate(_stringLiteralPolicy, parameters);

        if (ParameterTemplate is { } template && ReferenceEquals(_stringLiteralPolicy, SqlStringLiteralPolicy.MySql))
            SpecializeMySql(template, cmd, diagnosticState);

        return cmd;
    }

    /// <summary>
    /// Restores the canonical statement text and the complete canonical parameter set of one command when
    /// a previous execution specialized it. A no-op when the command carries no specialization.
    /// </summary>
    /// <param name="command">The driver-visible command being checked out.</param>
    /// <param name="state">
    /// The command's restore bookkeeping, or <see langword="null"/> for an execution-local command that is
    /// never reused and therefore has nothing to restore.
    /// </param>
    /// <param name="force">Restores even when the command is not marked as specialized.</param>
    private void RestoreCanonicalCommand(DbCommand command, CommandState? state, bool force = false)
    {
        if (state is null || (!state.Specialized && !force))
            return;

        try
        {
            if (ParameterTemplate is { } template)
                command.CommandText = template.StatementText;

            var canonical = state.Canonical;
            var driver = command.Parameters;
            if (canonical is not null && driver.Count != canonical.Count)
            {
                driver.Clear();
                for (var i = 0; i < canonical.Count; i++)
                    driver.Add(canonical[i]);
            }

            state.Specialized = false;
        }
        catch
        {
            // A partial restoration must never be handed to an execution: the command cannot be used
            // again, so the next checkout fails instead of binding values against a broken collection.
            state.Broken = true;
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
    /// <param name="command">The driver-visible command being checked out.</param>
    /// <param name="state">
    /// The command's restore bookkeeping, or <see langword="null"/> for an execution-local command; the
    /// canonical binding set is snapshotted from the command in that case.
    /// </param>
    private void SpecializeMySql(SqlParameterTemplate template, DbCommand command, CommandState? state)
    {
        var occurrences = template.Occurrences;
        if (occurrences.Length == 0)
            return;

        var canonical = state?.Canonical ?? new CanonicalParameterCollection(command.Parameters);
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

            if (occurrence.BindingId >= DbCommandParams.Count)
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
            command.CommandText = composed;

            var driver = command.Parameters;
            for (var bindingId = 0; bindingId < template.BindingCount; bindingId++)
            {
                if (occurrenceCounts[bindingId] == 0 || loweredCounts[bindingId] != occurrenceCounts[bindingId])
                    continue;

                if (bindingId >= canonical.Count)
                    continue;

                var index = IndexOfReference(driver, canonical[bindingId]);
                if (index >= 0)
                    driver.RemoveAt(index);
            }

            if (state is not null)
                state.Specialized = true;
        }
        catch
        {
            // Installation is transactional with respect to the checked-out command: restore the
            // canonical text and complete parameter collection before propagating the failure. There is
            // never a text-binding fallback.
            RestoreCanonicalCommand(command, state, force: true);
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
    /// Weak last-pair memo: a hint that lets the hot path skip the weak-table lookup for a repeated
    /// connection. Both references are weak, so the memo can never keep a connection (or its state)
    /// alive; a read confirms the identity of both targets before using the state.
    /// </summary>
    private sealed class Memo
    {
        private readonly WeakReference<DbConnection> _connection;
        private readonly WeakReference<CommandState> _state;

        internal Memo(DbConnection connection, CommandState state)
        {
            _connection = new WeakReference<DbConnection>(connection);
            _state = new WeakReference<CommandState>(state);
        }

        internal bool TryGet(DbConnection connection, out CommandState? state)
        {
            state = null;
            if (!_connection.TryGetTarget(out var memoConnection) || !ReferenceEquals(memoConnection, connection))
                return false;

            if (!_state.TryGetTarget(out var memoState))
                return false;

            state = memoState;
            return true;
        }

        internal bool Targets(DbConnection connection)
            => _connection.TryGetTarget(out var memoConnection) && ReferenceEquals(memoConnection, connection);
    }
}
