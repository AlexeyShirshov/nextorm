using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using MySqlConnector;
using NextORM.Core;

namespace NextORM.MySql;

/// <summary>
/// Data context for MySQL. Wraps the <c>MySqlConnector</c> driver for connections and parameters and
/// renders SQL through <c>MySqlDialect</c>.
/// </summary>
/// <remarks>
/// The bulk-insert executor is re-implemented here (the base hook cannot carry the whole-invocation
/// budget or the explicit option presence) so <see cref="BulkInsertMode"/> is resolved by the provider,
/// the native <c>MySqlBulkCopy</c> prerequisites are checked read-only before a row is read, and
/// MariaDB (its derived context) can add its scoped session controls.
/// </remarks>
public class MySqlDataContext : DataContext, IBulkInsertExecutor, IBulkInsertNativeOptions, IBulkInsertPortableScope
{
    /// <summary>
    /// Creates a MySQL context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>MySqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MySqlDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a MySQL context over a caller-supplied connection, which the context does not dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MySqlDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a context with an optional connection string or a caller-supplied connection and
    /// snapshots the provider server <paramref name="serverVersion"/> once. Derived providers (MariaDB)
    /// forward their version here so the base <see cref="DataContext.ServerVersion"/> snapshot agrees
    /// with the dialect they build.
    /// </summary>
    /// <param name="connectionString">Connection string used to create a context-owned connection, or <see langword="null"/>.</param>
    /// <param name="providedConnection">An already-created connection owned by the caller, or <see langword="null"/>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <param name="serverVersion">The provider server version to snapshot, or <see langword="null"/> for unset.</param>
    protected MySqlDataContext(string? connectionString, DbConnection? providedConnection, DataContextBuilder optionsBuilder, Version? serverVersion)
        : base(connectionString, providedConnection, optionsBuilder, serverVersion)
    {
    }

    /// <summary>Creates a new <c>MySqlConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The MySQL connection string.</param>
    /// <returns>A new, unopened MySQL connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new MySqlConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => MySqlDialect.Instance;

    /// <summary>
    /// Creates a <c>MySqlConnector</c> parameter, mapping a <see langword="null"/> value to
    /// <c>DBNull.Value</c> because <c>MySqlConnector</c> rejects an unset parameter value. This is the
    /// public 2-arg factory; the execution paths use the command-aware overloads below so a command
    /// from the active connection's driver mints the parameter.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new MySQL parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
        => new MySqlParameter(name, value ?? DBNull.Value);

    /// <summary>
    /// Mints the parameter through the executing command so it belongs to the connection's actual
    /// driver: <c>MySql.Data</c> (the driver of Oracle's EF Core provider) rejects a
    /// <c>MySqlConnector.MySqlParameter</c> in its parameter collection, and vice versa. No throwaway
    /// command is created: the active command owns the parameter and the execution layer adds it.
    /// </summary>
    /// <param name="command">The executing command the parameter will be added to.</param>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new MySQL parameter of the active connection's driver.</returns>
    protected internal override DbParameter CreateParam(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    /// <summary>
    /// Creates a <c>MySqlParameter</c> for a descriptor. A <see cref="TableParameterValue"/> is emulated
    /// as a JSON text parameter (<c>JSON_TABLE(@p, '$[*]' COLUMNS(...))</c> on MySQL 8.0+/MariaDB
    /// 10.6+). A <see cref="ProcedureParameter.TypeName"/> is SQL Server only and is rejected.
    /// </summary>
    /// <remarks>
    /// Kept for compatibility with callers that hold no executing command; the raw/procedure execution
    /// paths use the command-aware overload below, which mints the parameter through the command so it
    /// belongs to the connection's actual driver. This command-unaware overload falls back to the 2-arg
    /// <see cref="CreateParam(string, object?)"/> and is therefore not safe when the connection is
    /// driven by <c>MySql.Data</c>.
    /// </remarks>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.Name"/> is blank, <see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected internal override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
        => CreateProcedureParameterCore(null, parameter);

    /// <summary>
    /// Command-aware variant used by the raw/procedure execution paths: mints the parameter through
    /// <paramref name="command"/> so it belongs to the connection's actual driver (<c>MySql.Data</c> vs
    /// <c>MySqlConnector</c>), which is required when Oracle's EF Core provider owns the connection.
    /// </summary>
    /// <param name="command">The executing command the parameter will be added to.</param>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter of the active connection's driver.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.Name"/> is blank, <see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected internal override DbParameter CreateProcedureParameter(DbCommand command, ProcedureParameter parameter)
        => CreateProcedureParameterCore(command, parameter);

    /// <summary>
    /// Single implementation behind both procedure-parameter overloads: validates the descriptor, then
    /// mints the provider parameter either through <paramref name="command"/> (raw/procedure execution;
    /// driver identity) or through the command-unaware 2-arg factory when no command is available. The
    /// name check is the first statement so a blank name fails before any provider work.
    /// </summary>
    /// <param name="command">The executing command, or <see langword="null"/> for the compatibility path.</param>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter configured from <paramref name="parameter"/>.</returns>
    private DbParameter CreateProcedureParameterCore(DbCommand? command, ProcedureParameter parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Name);

        if (parameter.Value is TableParameterValue tableValue)
        {
            RejectTypeName(parameter);
            RejectNonInputTable(parameter);
            return command is null
                ? CreateParam(parameter.Name, tableValue.WriteJson(this))
                : CreateParam(command, parameter.Name, tableValue.WriteJson(this));
        }

        RejectTypeName(parameter);

        var dbParameter = command is null
            ? CreateParam(parameter.Name, parameter.Value)
            : CreateParam(command, parameter.Name, parameter.Value);

        return ApplyProcedureParameterMetadata(dbParameter, parameter);
    }

    private static void RejectTypeName(ProcedureParameter parameter)
    {
        if (parameter.TypeName is not null)
            throw new ArgumentException(
                $"TypeName is SQL Server only, but '{parameter.Name}' has TypeName = '{parameter.TypeName}'. "
                + "On MySQL/MariaDB pass the rows to ProcedureParameter.Table(name, rows) and read them with JSON_TABLE.",
                nameof(parameter));
    }

    private static void RejectNonInputTable(ProcedureParameter parameter)
    {
        if (parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A table-valued parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}.",
                nameof(parameter));
    }

    // ---------------------------------------------------------------------------------------------
    // Bulk insert (IBulkInsertExecutor). The provider owns the path decision because it owns the native
    // prerequisites: Mode.Portable never enters here, Mode.Native refuses by name when a prerequisite is
    // missing, and Mode.Auto writes through the portable INSERT ... VALUES path instead of replaying a
    // failed load. A runtime LOAD failure is never caught and replayed.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Rows buffered per native call when <see cref="BulkInsertOptions.MaxBatchSize"/> is not set. The
    /// connector's writer consumes a synchronous <see cref="IDataReader"/>, so the adapter pulls the
    /// source in bounded chunks instead of buffering the whole set.
    /// </summary>
    private const int DefaultNativeChunkRows = 10_000;

    private const string ZeroIdentitySqlMode = "NO_AUTO_VALUE_ON_ZERO";

    private const string ServerLocalInfileQuery = "select @@global.local_infile";

    /// <summary>
    /// The native <c>MySqlBulkCopy</c> load writes explicit identity columns (with the zero-mode guard) and
    /// maps <c>IgnoreDuplicates</c> to <c>MySqlBulkLoaderConflictOption.Ignore</c>, so both options stay on
    /// the native path; the selection itself is made once in the builder.
    /// </summary>
    bool IBulkInsertNativeOptions.SupportsNativeKeepIdentity => true;

    /// <inheritdoc cref="IBulkInsertNativeOptions.SupportsNativeIgnoreDuplicates"/>
    bool IBulkInsertNativeOptions.SupportsNativeIgnoreDuplicates => true;

    /// <summary>
    /// Runs the provider-scoped contract on the portable path: the same <c>KeepNulls=false</c> shape guard
    /// as the native load, the identity zero-mode for explicit identity values, and (on MariaDB) the
    /// scoped constraint controls. The scope is entered before the first row is read and restores every
    /// session control on disposal, including the error path.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The scope to dispose after the write.</returns>
    IDisposable IBulkInsertPortableScope.BeginPortableBulkInsertScope(BulkInsertCommand command)
    {
        EnsureKeepNullsIsDefaultEquivalent(command);

        var scopedConstraintChecks = SupportsScopedConstraintChecks && command.Options.CheckConstraints is false;
        if (!command.KeepIdentity && !scopedConstraintChecks)
            return NoSessionScope.Instance;

        return MySqlSessionScope.Apply(command, RequireMySqlConnection(), ResolveTransaction(), scopedConstraintChecks);
    }

    int IBulkInsertExecutor.BulkInsert(BulkInsertCommand command)
    {
        var rows = command.SyncRows
            ?? throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");

        if (!TryUseNative(command))
            return new PortableBulkInsertExecutor(this).BulkInsert(command);

        using var deadline = BulkInsertDeadline.Start(command.Timeout, CancellationToken.None);
        return WriteNativeRows(command, rows, deadline);
    }

    async Task<int> IBulkInsertExecutor.BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        var rows = command.AsyncRows ?? new SyncToAsyncEnumerable<object?[]>(command.SyncRows!);

        if (!await TryUseNativeAsync(command, cancellationToken).ConfigureAwait(false))
            return await new PortableBulkInsertExecutor(this).BulkInsertAsync(command, cancellationToken).ConfigureAwait(false);

        using var deadline = BulkInsertDeadline.Start(command.Timeout, cancellationToken);
        try
        {
            return await WriteNativeRowsAsync(command, rows, deadline).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation)
        {
            throw deadline.Translate(cancellation);
        }
    }

    private bool TryUseNative(BulkInsertCommand command)
    {
        if (command.Mode == BulkInsertMode.Portable)
            return false;

        EnsureNativeRequestSupported(command);

        var missing = ClientLocalInfilePrerequisite() ?? ServerLocalInfilePrerequisite();
        return ResolveMissingPrerequisite(command, missing);
    }

    private async Task<bool> TryUseNativeAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        if (command.Mode == BulkInsertMode.Portable)
            return false;

        EnsureNativeRequestSupported(command);

        var missing = ClientLocalInfilePrerequisite()
            ?? await ServerLocalInfilePrerequisiteAsync(cancellationToken).ConfigureAwait(false);

        return ResolveMissingPrerequisite(command, missing);
    }

    private static bool ResolveMissingPrerequisite(BulkInsertCommand command, string? missing)
    {
        if (missing is null)
            return true;

        if (command.Mode == BulkInsertMode.Native)
            throw new NotSupportedException(
                $"Bulk insert Mode.Native cannot run the MySQL/MariaDB native MySqlBulkCopy path: {missing}. "
                + "nextorm reads this prerequisite without changing any client or server setting; enable it, "
                + "or use Mode.Auto to let nextorm write through the portable INSERT ... VALUES path.");

        return false;
    }

    private void EnsureNativeRequestSupported(BulkInsertCommand command)
    {
        if (command.BulkCopy.TableLock || command.BulkCopy.FireTriggers)
            throw new NotSupportedException(
                "TableLock=true and FireTriggers=true are only supported by the SQL Server native SqlBulkCopy path; "
                + "the MySQL/MariaDB native MySqlBulkCopy path cannot express them.");

        if (command.Options.CheckConstraints is false && !SupportsScopedConstraintChecks)
            throw new NotSupportedException(
                "BulkInsertOptions.CheckConstraints=false cannot be honoured by the MySQL native MySqlBulkCopy path: "
                + "MySQL always checks CHECK and FOREIGN KEY constraints. Only MariaDB has scoped session controls for it.");

        EnsureKeepNullsIsDefaultEquivalent(command);
    }

    /// <summary>
    /// Whether this context is a MariaDB one, resolved from the dialect assembly rather than a new
    /// protected member (the public surface must stay unchanged). MariaDB alone can turn CHECK constraint
    /// checking off on its own session.
    /// </summary>
    private bool SupportsScopedConstraintChecks
        => string.Equals(Dialect.GetType().Assembly.GetName().Name, "nextorm.mariadb", StringComparison.Ordinal);

    private string? ClientLocalInfilePrerequisite()
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(ConnectionString);
            return builder.AllowLoadLocalInfile
                ? null
                : "the client connection option AllowLoadLocalInfile=true is required (MySqlBulkCopy loads through LOAD DATA LOCAL INFILE)";
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return $"a valid MySQL connection string is required (the current one cannot be parsed: {exception.Message})";
        }
    }

    private string? ServerLocalInfilePrerequisite()
    {
        var connection = GetConnection();
        if (connection.State != ConnectionState.Open)
            ((IConnectionManager)this).EnsureConnectionOpen();

        using var command = connection.CreateCommand();
        command.CommandText = ServerLocalInfileQuery;
        command.Transaction = ((ITransactionManager)this).CurrentTransaction;
        return ValidateServerLocalInfile(command.ExecuteScalar());
    }

    private async Task<string?> ServerLocalInfilePrerequisiteAsync(CancellationToken cancellationToken)
    {
        var connection = GetConnection();
        if (connection.State != ConnectionState.Open)
            await ((IConnectionManager)this).EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = ServerLocalInfileQuery;
        command.Transaction = ((ITransactionManager)this).CurrentTransaction;
        return ValidateServerLocalInfile(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static string? ValidateServerLocalInfile(object? value)
    {
        if (value is null)
            return "the server variable @@GLOBAL.local_infile=1 is required (its value could not be read)";

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture) == 1
                ? null
                : "the server variable @@GLOBAL.local_infile=1 is required (the server reports it disabled)";
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return $"the server variable @@GLOBAL.local_infile=1 is required (unexpected value '{value}')";
        }
    }

    // Binary LOAD always writes the supplied value, so KeepNulls=false can only be honoured when writing
    // NULL is indistinguishable from letting the destination DEFAULT apply: every written column must be
    // nullable with a NULL/absent SQL default. The SQL-default half is certified against the live schema
    // by the integration suite; a non-nullable input column is refused here, before any row is read.
    private static void EnsureKeepNullsIsDefaultEquivalent(BulkInsertCommand command)
    {
        if (command.Options.KeepNulls is not false)
            return;

        var nullability = new NullabilityInfoContext();
        var nonNullable = new List<string>(command.Columns.Count);
        foreach (var column in command.Columns)
        {
            if (nullability.Create(column.PropertyInfo).WriteState != NullabilityState.Nullable)
                nonNullable.Add(column.ColumnName);
        }

        if (nonNullable.Count == 0)
            return;

        throw new NotSupportedException(
            "BulkInsertOptions.KeepNulls=false cannot be honoured by the MySQL/MariaDB bulk paths: the write always sends the supplied value and a column DEFAULT applies only when the column is omitted. "
            + "The request is accepted only for a default-equivalent shape, where every written column is nullable with a NULL/absent SQL default; "
            + $"the non-nullable input column(s) {string.Join(", ", nonNullable.Select(name => $"'{name}'"))} break that shape. Use KeepNulls=true, or write the DEFAULT explicitly.");
    }

    private int WriteNativeRows(BulkInsertCommand command, IEnumerable<object?[]> rows, BulkInsertDeadline deadline)
    {
        var connection = RequireMySqlConnection();
        var transaction = ResolveTransaction();
        var (tableName, columnNames) = RenderTarget(command);
        using var session = MySqlSessionScope.Apply(command, connection, transaction, SupportsScopedConstraintChecks);

        var chunkSize = ResolveChunkSize(command);
        var buffer = new List<object?[]>(chunkSize);
        var total = 0;

        foreach (var row in rows)
        {
            deadline.ThrowIfCancelled();
            buffer.Add(row);

            if (buffer.Count >= chunkSize)
            {
                total += WriteNativeChunk(command, connection, transaction, tableName, columnNames, buffer, total, deadline);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
            total += WriteNativeChunk(command, connection, transaction, tableName, columnNames, buffer, total, deadline);

        return total;
    }

    private async Task<int> WriteNativeRowsAsync(BulkInsertCommand command, IAsyncEnumerable<object?[]> rows, BulkInsertDeadline deadline)
    {
        var connection = await RequireMySqlConnectionAsync(deadline.Token).ConfigureAwait(false);
        var transaction = ResolveTransaction();
        var (tableName, columnNames) = RenderTarget(command);
        using var session = MySqlSessionScope.Apply(command, connection, transaction, SupportsScopedConstraintChecks);

        var chunkSize = ResolveChunkSize(command);
        var buffer = new List<object?[]>(chunkSize);
        var total = 0;

        await foreach (var row in rows.WithCancellation(deadline.Token).ConfigureAwait(false))
        {
            deadline.ThrowIfCancelled();
            buffer.Add(row);

            if (buffer.Count >= chunkSize)
            {
                total += await WriteNativeChunkAsync(command, connection, transaction, tableName, columnNames, buffer, total, deadline).ConfigureAwait(false);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
            total += await WriteNativeChunkAsync(command, connection, transaction, tableName, columnNames, buffer, total, deadline).ConfigureAwait(false);

        return total;
    }

    private int WriteNativeChunk(
        BulkInsertCommand command,
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string tableName,
        IReadOnlyList<string> columnNames,
        List<object?[]> buffer,
        int offset,
        BulkInsertDeadline deadline)
    {
        var bulk = CreateBulkCopy(command, connection, transaction, tableName, columnNames, deadline);
        var progress = command.Progress;
        var reported = offset;

        if (progress is not null)
        {
            bulk.NotifyAfter = Math.Max(1, command.NotifyEvery);
            bulk.MySqlRowsCopied += (_, args) =>
            {
                var cumulative = offset + (int)args.RowsCopied;
                if (cumulative <= reported)
                    return;

                reported = cumulative;
                progress(cumulative);
            };
        }

        using var reader = new BulkRowReader(buffer, columnNames);
        var result = bulk.WriteToServer(reader);

        var written = (int)result.RowsInserted;
        if (progress is not null && offset + written > reported)
            progress(offset + written);

        return written;
    }

    private async Task<int> WriteNativeChunkAsync(
        BulkInsertCommand command,
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string tableName,
        IReadOnlyList<string> columnNames,
        List<object?[]> buffer,
        int offset,
        BulkInsertDeadline deadline)
    {
        var bulk = CreateBulkCopy(command, connection, transaction, tableName, columnNames, deadline);
        var progress = command.Progress;
        var reported = offset;

        if (progress is not null)
        {
            bulk.NotifyAfter = Math.Max(1, command.NotifyEvery);
            bulk.MySqlRowsCopied += (_, args) =>
            {
                var cumulative = offset + (int)args.RowsCopied;
                if (cumulative <= reported)
                    return;

                reported = cumulative;
                progress(cumulative);
            };
        }

        using var reader = new BulkRowReader(buffer, columnNames);
        var result = await bulk.WriteToServerAsync(reader, deadline.Token).ConfigureAwait(false);

        var written = (int)result.RowsInserted;
        if (progress is not null && offset + written > reported)
            progress(offset + written);

        return written;
    }

    private static MySqlBulkCopy CreateBulkCopy(
        BulkInsertCommand command,
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string tableName,
        IReadOnlyList<string> columnNames,
        BulkInsertDeadline deadline)
    {
        var bulk = new MySqlBulkCopy(connection, transaction)
        {
            DestinationTableName = tableName,
            BulkCopyTimeout = ResolveBulkCopyTimeout(deadline),
            ConflictOption = command.IgnoreConflicts ? MySqlBulkLoaderConflictOption.Ignore : MySqlBulkLoaderConflictOption.None,
        };

        for (var i = 0; i < columnNames.Count; i++)
            bulk.ColumnMappings.Add(new MySqlBulkCopyColumnMapping { SourceOrdinal = i, DestinationColumn = columnNames[i] });

        return bulk;
    }

    private static int ResolveBulkCopyTimeout(BulkInsertDeadline deadline)
    {
        if (deadline.Remaining is not { } remaining)
            return 30;

        var seconds = Math.Ceiling(remaining.TotalSeconds);
        return seconds >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)seconds);
    }

    private static int ResolveChunkSize(BulkInsertCommand command)
    {
        var configured = command.Batch?.MaxBatchSize;
        return configured is int rows && rows > 0 ? rows : DefaultNativeChunkRows;
    }

    private (string TableName, List<string> ColumnNames) RenderTarget(BulkInsertCommand command)
    {
        var tableName = SqlMutationBuilder.RenderBulkTarget(Dialect, QuoteIdentifiers, command, NamingConvention);
        var columnNames = new List<string>(command.Columns.Count);
        foreach (var column in command.Columns)
            columnNames.Add(SqlMutationBuilder.ResolveColumnName(column, NamingConvention));

        return (tableName, columnNames);
    }

    private MySqlConnection RequireMySqlConnection()
    {
        var connection = GetConnection();
        if (connection is not MySqlConnection mySqlConnection)
            throw new NotSupportedException(
                $"The native MySqlBulkCopy path requires a MySqlConnector {nameof(MySqlConnection)}; the current connection is {connection.GetType().Name}. "
                + "Use Mode.Portable to write through INSERT ... VALUES on this connection.");

        if (mySqlConnection.State != ConnectionState.Open)
            ((IConnectionManager)this).EnsureConnectionOpen();

        return mySqlConnection;
    }

    private async Task<MySqlConnection> RequireMySqlConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = GetConnection();
        if (connection is not MySqlConnection mySqlConnection)
            throw new NotSupportedException(
                $"The native MySqlBulkCopy path requires a MySqlConnector {nameof(MySqlConnection)}; the current connection is {connection.GetType().Name}. "
                + "Use Mode.Portable to write through INSERT ... VALUES on this connection.");

        if (mySqlConnection.State != ConnectionState.Open)
            await ((IConnectionManager)this).EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        return mySqlConnection;
    }

    private MySqlTransaction? ResolveTransaction()
    {
        var transaction = ((ITransactionManager)this).CurrentTransaction;
        return transaction switch
        {
            null => null,
            MySqlTransaction mySqlTransaction => mySqlTransaction,
            _ => throw new NotSupportedException(
                $"The native MySqlBulkCopy path requires a MySqlConnector transaction; the current transaction is {transaction.GetType().Name}."),
        };
    }

    /// <summary>
    /// The scope returned when a portable write requests neither explicit identity values nor scoped
    /// constraint checks: nothing to snapshot or restore, and deliberately no connection is opened.
    /// </summary>
    private sealed class NoSessionScope : IDisposable
    {
        internal static readonly NoSessionScope Instance = new();

        private NoSessionScope()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Scoped session controls around one native write: the identity zero-mode (<c>NO_AUTO_VALUE_ON_ZERO</c>,
    /// set before the load so an explicit <c>0</c> is not replaced by a generated value) and, on MariaDB,
    /// the constraint checks requested off. Every control is snapshotted and restored on the same session
    /// even when the load fails.
    /// </summary>
    private sealed class MySqlSessionScope : IDisposable
    {
        private readonly MySqlConnection _connection;
        private readonly MySqlTransaction? _transaction;
        private readonly List<string> _restoreStatements = [];
        private bool _disposed;

        private MySqlSessionScope(MySqlConnection connection, MySqlTransaction? transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public static MySqlSessionScope Apply(BulkInsertCommand command, MySqlConnection connection, MySqlTransaction? transaction, bool scopedConstraintChecks)
        {
            var scope = new MySqlSessionScope(connection, transaction);
            try
            {
                if (command.KeepIdentity)
                    scope.RequireSqlMode(ZeroIdentitySqlMode);

                if (scopedConstraintChecks && command.Options.CheckConstraints is false)
                {
                    scope.DisableFlag("check_constraint_checks");
                    scope.DisableFlag("foreign_key_checks");
                }

                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            for (var i = _restoreStatements.Count - 1; i >= 0; i--)
                NonQuery(_restoreStatements[i]);
        }

        private void RequireSqlMode(string mode)
        {
            var current = Scalar("select @@session.sql_mode") ?? string.Empty;
            if (current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(existing => string.Equals(existing, mode, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            NonQuery($"set session sql_mode = concat_ws(',', @@session.sql_mode, '{mode}')");
            _restoreStatements.Add($"set session sql_mode = {QuoteSqlMode(current)}");
        }

        private void DisableFlag(string variable)
        {
            var current = Scalar($"select @@session.{variable}");
            if (current is null || !long.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
                throw new NotSupportedException($"The bulk-insert session control requires the server variable @@SESSION.{variable}, which could not be read.");

            if (numeric == 0)
                return;

            NonQuery($"set session {variable} = 0");
            _restoreStatements.Add($"set session {variable} = {numeric.ToString(CultureInfo.InvariantCulture)}");
        }

        private string? Scalar(string sql)
        {
            try
            {
                using var command = CreateCommand(sql);
                return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            catch (MySqlException exception)
            {
                throw new NotSupportedException(
                    $"The bulk-insert session control '{sql}' could not be read, so the requested option cannot be honoured: {exception.Message}",
                    exception);
            }
        }

        private void NonQuery(string sql)
        {
            try
            {
                using var command = CreateCommand(sql);
                command.ExecuteNonQuery();
            }
            catch (MySqlException exception)
            {
                throw new NotSupportedException(
                    $"The bulk-insert session control '{sql}' could not be applied, so the requested option cannot be honoured: {exception.Message}",
                    exception);
            }
        }

        private MySqlCommand CreateCommand(string sql)
        {
            var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _transaction;
            return command;
        }

        private static string QuoteSqlMode(string mode)
        {
            foreach (var character in mode)
            {
                if (!char.IsLetterOrDigit(character) && character is not ('_' or ','))
                    throw new NotSupportedException(
                        $"The current session sql_mode '{mode}' contains a character nextorm cannot restore safely.");
            }

            return $"'{mode}'";
        }
    }

    /// <summary>
    /// Minimal forward-only <see cref="IDataReader"/> over one bounded chunk of projected rows. The
    /// connector's writer is synchronous, so the adapter feeds it a chunk at a time instead of buffering
    /// the whole source.
    /// </summary>
    private sealed class BulkRowReader(IReadOnlyList<object?[]> rows, IReadOnlyList<string> columnNames) : IDataReader
    {
        private int _index = -1;

        public int Depth => 0;

        public bool IsClosed => false;

        public int RecordsAffected => -1;

        public int FieldCount => columnNames.Count;

        public object this[int i] => GetValue(i);

        public object this[string name] => GetValue(GetOrdinal(name));

        public void Close()
        {
        }

        public void Dispose()
        {
        }

        public bool Read() => ++_index < rows.Count;

        public bool NextResult() => false;

        public DataTable GetSchemaTable()
            => throw new NotSupportedException("The bulk source has no schema table; explicit column mappings are supplied.");

        public string GetName(int i) => columnNames[i];

        public int GetOrdinal(string name)
        {
            for (var i = 0; i < columnNames.Count; i++)
            {
                if (string.Equals(columnNames[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            throw new IndexOutOfRangeException($"Column '{name}' is not part of the bulk-insert source.");
        }

        public object GetValue(int i) => rows[_index][i] ?? DBNull.Value;

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);

            return count;
        }

        public bool IsDBNull(int i) => rows[_index][i] is null;

        public Type GetFieldType(int i)
        {
            for (var row = 0; row < rows.Count; row++)
            {
                if (rows[row][i] is { } value)
                    return value.GetType();
            }

            return typeof(object);
        }

        public string GetDataTypeName(int i) => GetFieldType(i).Name;

        public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i), CultureInfo.InvariantCulture);

        public byte GetByte(int i) => Convert.ToByte(GetValue(i), CultureInfo.InvariantCulture);

        public char GetChar(int i) => Convert.ToChar(GetValue(i), CultureInfo.InvariantCulture);

        public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i), CultureInfo.InvariantCulture);

        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i), CultureInfo.InvariantCulture);

        public double GetDouble(int i) => Convert.ToDouble(GetValue(i), CultureInfo.InvariantCulture);

        public float GetFloat(int i) => Convert.ToSingle(GetValue(i), CultureInfo.InvariantCulture);

        public Guid GetGuid(int i) => (Guid)GetValue(i);

        public short GetInt16(int i) => Convert.ToInt16(GetValue(i), CultureInfo.InvariantCulture);

        public int GetInt32(int i) => Convert.ToInt32(GetValue(i), CultureInfo.InvariantCulture);

        public long GetInt64(int i) => Convert.ToInt64(GetValue(i), CultureInfo.InvariantCulture);

        public string GetString(int i) => Convert.ToString(GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();
    }
}
