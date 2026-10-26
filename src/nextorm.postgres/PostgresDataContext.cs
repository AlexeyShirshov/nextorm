using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextORM.Core;
using Npgsql;
using Npgsql.PostgresTypes;
using NpgsqlTypes;

namespace NextORM.Postgres;

/// <summary>
/// Data context for PostgreSQL. Wraps the <c>Npgsql</c> driver for connections and parameters and
/// renders SQL through <c>PostgresDialect</c>.
/// </summary>
public class PostgresDataContext : DataContext, IBulkInsertExecutor
{
    private static readonly MethodInfo GetFieldValueMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;
    private static readonly MethodInfo GetTimeSpanMI = GetFieldValueMI.MakeGenericMethod(typeof(TimeSpan));
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo GetDataTypeNameMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDataTypeName))!;
    private static readonly MethodInfo ReadIntervalMI = typeof(PostgresIntervalReader).GetMethod(nameof(PostgresIntervalReader.Read), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo ToRangeMI = typeof(PostgresRange).GetMethod(nameof(PostgresRange.ToRange), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo ToRangesMI = typeof(PostgresRange).GetMethod(nameof(PostgresRange.ToRanges), BindingFlags.Static | BindingFlags.Public)!;

    // One immutable server version per concrete context type, so two instances of the same type can
    // never render different SQL and silently reuse each other's cached plans (the plan-cache key is
    // the context type). Unset is a sentinel distinct from every Version.
    private static readonly object UnsetServerVersion = new();
    private static readonly ConcurrentDictionary<Type, object> ServerVersionsByContextType = new();

    private readonly ISqlDialect _dialect;

    /// <summary>
    /// Creates a PostgreSQL context that owns a connection built lazily from
    /// <paramref name="connectionString"/>. The context is unversioned: every supported form is
    /// emitted.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>NpgsqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public PostgresDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : this(connectionString, null, optionsBuilder, null)
    {
    }

    /// <summary>
    /// Creates a PostgreSQL context over a caller-supplied connection, which the context does not
    /// dispose. The context is unversioned: every supported form is emitted.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public PostgresDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : this(null, connection, optionsBuilder, null)
    {
    }

    /// <summary>
    /// Creates a version-aware PostgreSQL context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>NpgsqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <param name="serverVersion">The PostgreSQL server version, or <see langword="null"/> for unset. Immutable per context type.</param>
    /// <exception cref="InvalidOperationException">A different version was already registered for this context type.</exception>
    public PostgresDataContext(string connectionString, DataContextBuilder optionsBuilder, Version? serverVersion)
        : this(connectionString, null, optionsBuilder, serverVersion)
    {
    }

    /// <summary>
    /// Creates a version-aware PostgreSQL context over a caller-supplied connection, which the context
    /// does not dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <param name="serverVersion">The PostgreSQL server version, or <see langword="null"/> for unset. Immutable per context type.</param>
    /// <exception cref="InvalidOperationException">A different version was already registered for this context type.</exception>
    public PostgresDataContext(DbConnection connection, DataContextBuilder optionsBuilder, Version? serverVersion)
        : this(null, connection, optionsBuilder, serverVersion)
    {
    }

    private PostgresDataContext(string? connectionString, DbConnection? connection, DataContextBuilder optionsBuilder, Version? serverVersion)
        : base(connectionString, connection, optionsBuilder, serverVersion)
    {
        RegisterServerVersion(GetType(), ServerVersion);
        _dialect = ServerVersion is null ? PostgresDialect.Instance : new PostgresDialect(ServerVersion);
    }

    /// <summary>Creates a new <c>NpgsqlConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>A new, unopened PostgreSQL connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new NpgsqlConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => _dialect;

    // A context type owns one immutable server version. A second, conflicting version is a
    // configuration error that names the remedy: derive a distinct context subclass.
    private static void RegisterServerVersion(Type contextType, Version? serverVersion)
    {
        var candidate = (object?)serverVersion ?? UnsetServerVersion;
        var registered = ServerVersionsByContextType.GetOrAdd(contextType, candidate);

        if (ReferenceEquals(registered, candidate))
            return;
        if (registered is Version registeredVersion && candidate is Version candidateVersion && registeredVersion.Equals(candidateVersion))
            return;

        throw new InvalidOperationException(
            $"The PostgreSQL context type '{contextType.Name}' is already bound to server version {Describe(registered)}, " +
            $"so it cannot also be used with {Describe(candidate)}. A context type has one immutable server version because the " +
            "plan cache is keyed by the context type; derive a distinct DataContext subclass for each version.");
    }

    private static string Describe(object discriminator)
        => discriminator is Version version ? version.ToString() : "unset (null)";

    /// <summary>
    /// Creates an <c>NpgsqlParameter</c>, mapping a <see langword="null"/> value to
    /// <c>DBNull.Value</c> and binding JSON documents, elements and nodes as <c>jsonb</c> so that the
    /// JSON operators accept them without an explicit cast.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new PostgreSQL parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // A provider-agnostic Range<T> is bound as the matching Npgsql range type so that the range
        // operators and columns accept it without a cast.
        if (value is not null && value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(Range<>))
        {
            var boundType = value.GetType().GetGenericArguments()[0];
            return new NpgsqlParameter(name, PostgresRange.ToDriver(value)) { NpgsqlDbType = PostgresRangeTypes.DbTypeFor(boundType) };
        }

        // A provider-agnostic Range<T>[] is bound as the matching Npgsql multirange type.
        if (value is Array && value.GetType().GetElementType() is { } elementType
            && elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Range<>))
        {
            var boundType = elementType.GetGenericArguments()[0];
            return new NpgsqlParameter(name, PostgresRange.ToDriverMultirange(value)) { NpgsqlDbType = PostgresRangeTypes.MultirangeDbTypeFor(boundType) };
        }

        // Npgsql rejects a null parameter value, so unset/null values must be passed as DBNull.
        var parameter = new NpgsqlParameter(name, value ?? DBNull.Value);

        // A JSON document/element/node is bound as jsonb so that the json/jsonb operators and
        // functions accept them without an explicit cast. A plain string is left as text and can be
        // parsed on demand with SqlFunctions.Postgres.json_cast(...).
        if (value is JsonDocument or JsonElement or JsonNode)
            parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;

        // Npgsql cannot infer a type for a CLR uint; bind it as xid, the type PostgresDialect
        // renders for it, so the value matches the column without an explicit cast. A boxed
        // uint? with a value arrives here as uint; null (DBNull) is left untouched.
        // In Npgsql oid/cid are also uint, but nextorm binds every uint as xid by design (for xmin).
        if (value is uint)
            parameter.NpgsqlDbType = NpgsqlDbType.Xid;

        return parameter;
    }

    /// <summary>
    /// Creates an <c>NpgsqlParameter</c> for a descriptor. A <see cref="TableParameterValue"/> is
    /// emulated: a scalar row type becomes a typed array (<c>unnest(@p)</c>/<c>= ANY(@p)</c>), an
    /// entity row type becomes a <c>jsonb</c> document (<c>jsonb_to_recordset(@p)</c>). A
    /// <see cref="ProcedureParameter.TypeName"/> has no meaning on PostgreSQL and is rejected.
    /// </summary>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new PostgreSQL parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException">The descriptor carries a <see cref="ProcedureParameter.TypeName"/> (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected internal override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
    {
        if (parameter.Value is TableParameterValue tableValue)
        {
            RejectTypeName(parameter);
            RejectNonInputTable(parameter);

            if (tableValue.IsScalar)
            {
                var arrayParameter = new NpgsqlParameter(parameter.Name, tableValue.ToArray(this));
                ApplyArrayDbType(arrayParameter, tableValue.RowType);
                return arrayParameter;
            }

            return new NpgsqlParameter(parameter.Name, tableValue.WriteJson(this)) { NpgsqlDbType = NpgsqlDbType.Jsonb };
        }

        RejectTypeName(parameter);
        return base.CreateProcedureParameter(parameter);
    }

    // Npgsql's CLR-array inference maps a DateTime element to timestamp and a DateTimeOffset element to
    // timestamptz; setting the array type explicitly keeps the mapping unambiguous and matches the
    // provider's scalar mapping. Types with an exact inference are left to the driver.
    private static void ApplyArrayDbType(NpgsqlParameter parameter, Type rowType)
    {
        var element = Nullable.GetUnderlyingType(rowType) ?? rowType;

        if (element.IsEnum)
            element = Enum.GetUnderlyingType(element);

        if (element == typeof(DateTime))
            parameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Timestamp;
        else if (element == typeof(DateTimeOffset))
            parameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.TimestampTz;
    }

    private static void RejectTypeName(ProcedureParameter parameter)
    {
        if (parameter.TypeName is not null)
            throw new ArgumentException(
                $"TypeName is SQL Server only, but '{parameter.Name}' has TypeName = '{parameter.TypeName}'. "
                + "On PostgreSQL pass the rows to ProcedureParameter.Table(name, rows): a scalar set binds as an array and an entity set as jsonb.",
                nameof(parameter));
    }

    private static void RejectNonInputTable(ProcedureParameter parameter)
    {
        if (parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A table-valued parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}.",
                nameof(parameter));
    }

    /// <summary>
    /// True: PostgreSQL can surface an anonymous <c>ROW(...)</c> record column or a caller-registered
    /// named composite as a single raw result-set column (#194).
    /// </summary>
    protected override bool SupportsRawRowColumns => true;

    /// <summary>
    /// True only when <paramref name="reader"/> is an <see cref="NpgsqlDataReader"/> that reports a
    /// genuine PostgreSQL composite for <paramref name="ordinal"/> (a caller-registered named or struct
    /// composite). Any other reader, or a column whose catalog metadata is unknown (<c>hstore</c>/<c>ltree</c>
    /// or an unresolved cold composite → <see cref="UnknownBackendType"/>), returns
    /// <see langword="false"/> so classification never guesses from a data type name or an exception shape.
    /// The metadata is read from the existing reader; no second command, connection or type reload is used.
    /// </summary>
    /// <param name="reader">The reader whose current result set is being classified.</param>
    /// <param name="ordinal">The zero-based column ordinal.</param>
    /// <returns><see langword="true"/> when the driver reports a genuine composite type for the column.</returns>
    protected override bool IsGenuineCompositeColumn(DbDataReader reader, int ordinal)
        => reader is NpgsqlDataReader npgsql && npgsql.GetPostgresType(ordinal) is PostgresCompositeType;

    /// <summary>
    /// Materializes a projected <see cref="Range{T}"/> column from the driver's
    /// <c>NpgsqlRange&lt;T&gt;</c> through the typed <c>GetFieldValue</c> accessor, converting it to the
    /// provider-agnostic range. Every other column is mapped by the base implementation.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <returns>An expression that reads the column value.</returns>
    public override Expression MapColumnExpression(SelectExpression column, Expression param)
    {
        var realType = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
        if (realType.IsGenericType && realType.GetGenericTypeDefinition() == typeof(Range<>))
            return MapRangeColumn(column, param, realType);

        if (realType.IsArray && realType.GetElementType() is { } element
            && element.IsGenericType && element.GetGenericTypeDefinition() == typeof(Range<>))
            return MapMultirangeColumn(column, param, element.GetGenericArguments()[0]);

        // A mapped TimeSpan is either a real PostgreSQL interval or a time-of-day column; the
        // projection carries no SQL type, so the driver type is discriminated per value. A configured
        // converter keeps its precedence over the interval probe (it is handled by the base mapping).
        if (realType == typeof(TimeSpan) && column.Converter is null)
            return MapIntervalTimeSpanColumn(column, param);

        return base.MapColumnExpression(column, param);
    }

    /// <summary>
    /// Maps a <see cref="TimeSpan"/> column whose PostgreSQL storage type is only known from the open
    /// reader: an <c>interval</c> is read as the driver's raw interval through
    /// <see cref="PostgresIntervalReader"/> (calendar months are rejected, zero-month values convert
    /// exactly), while <c>time</c>/<c>timetz</c> keep the existing <c>GetFieldValue&lt;TimeSpan&gt;</c>
    /// accessor. Null handling wraps the discriminator, so SQL NULL never reads an interval value.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <returns>An expression that reads the column value.</returns>
    private static Expression MapIntervalTimeSpanColumn(SelectExpression column, Expression param)
    {
        var index = Expression.Constant(column.Index);
        var intervalRead = Expression.Call(ReadIntervalMI, param, index);
        var timeSpanRead = Expression.Call(
            Expression.Convert(param, typeof(DbDataReader)),
            GetTimeSpanMI,
            index);
        var getter = Expression.Condition(
            Expression.Equal(
                Expression.Call(param, GetDataTypeNameMI, index),
                Expression.Constant(PostgresIntervalReader.DataTypeName)),
            intervalRead,
            timeSpanRead);

        if (column.Nullable)
        {
            Expression value = getter.Type == column.PropertyType
                ? getter
                : Expression.Convert(getter, column.PropertyType);

            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                getter);
        }

        return getter;
    }

    private static Expression MapMultirangeColumn(SelectExpression column, Expression param, Type boundType)
    {
        var driverType = typeof(NpgsqlRange<>).MakeGenericType(boundType).MakeArrayType();
        var index = Expression.Constant(column.Index);

        var getter = Expression.Call(
            Expression.Convert(param, typeof(NpgsqlDataReader)),
            GetFieldValueMI.MakeGenericMethod(driverType),
            index);
        var converted = Expression.Call(ToRangesMI.MakeGenericMethod(boundType), getter);

        if (column.Nullable)
        {
            Expression value = converted.Type == column.PropertyType
                ? converted
                : Expression.Convert(converted, column.PropertyType);

            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                converted);
        }

        return converted;
    }

    private static Expression MapRangeColumn(SelectExpression column, Expression param, Type realType)
    {
        var boundType = realType.GetGenericArguments()[0];
        var driverType = typeof(NpgsqlRange<>).MakeGenericType(boundType);
        var index = Expression.Constant(column.Index);

        var getter = Expression.Call(
            Expression.Convert(param, typeof(NpgsqlDataReader)),
            GetFieldValueMI.MakeGenericMethod(driverType),
            index);
        var converted = Expression.Call(ToRangeMI.MakeGenericMethod(boundType), getter);

        if (column.Nullable)
        {
            Expression value = converted.Type == column.PropertyType
                ? converted
                : Expression.Convert(converted, column.PropertyType);

            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                converted);
        }

        return converted;
    }

    /// <summary>
    /// One binary <c>COPY ... FROM STDIN</c> importer, abstracted so the unit tests can observe the
    /// importer lifecycle (bounded chunks, <c>Complete</c> on success, disposal on cancellation/error)
    /// without a live server. Production code always wraps a real <c>NpgsqlBinaryImporter</c>.
    /// </summary>
    internal interface IBinaryCopySession : IDisposable, IAsyncDisposable
    {
        /// <summary>Writes one row, blocking until it is buffered.</summary>
        /// <param name="row">The row values, in written-column order.</param>
        void WriteRow(object?[] row);

        /// <summary>Writes one row asynchronously.</summary>
        /// <param name="row">The row values, in written-column order.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>A task that completes when the row is buffered.</returns>
        Task WriteRowAsync(object?[] row, CancellationToken cancellationToken);

        /// <summary>Completes the COPY and returns the imported row count.</summary>
        /// <returns>The number of rows imported by this importer.</returns>
        long Complete();

        /// <summary>Completes the COPY asynchronously.</summary>
        /// <param name="cancellationToken">Cancels the completion.</param>
        /// <returns>A task producing the number of rows imported by this importer.</returns>
        Task<long> CompleteAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Writes the rows of a bulk-insert command through the native binary <c>COPY</c> path. The base hook signature
    /// cannot carry the whole-invocation <see cref="TimeSpan"/> budget or the explicit option presence, so
    /// the provider re-implements the internal bulk executor and keeps the protected hooks as a fallback.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The number of rows written.</returns>
    int IBulkInsertExecutor.BulkInsert(BulkInsertCommand command)
    {
        var rows = command.SyncRows
            ?? throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");

        var copyCommand = PrepareNativeCopy(command, out var chunkSize);
        using var deadline = BulkInsertDeadline.Start(command.Timeout, CancellationToken.None);
        using var enumerator = rows.GetEnumerator();
        return WriteRows(copyCommand, enumerator, deadline, command.TimeoutSeconds, chunkSize, command.Progress);
    }

    /// <summary>Asynchronously writes the rows of a bulk-insert command through the native binary <c>COPY</c> path.</summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    async Task<int> IBulkInsertExecutor.BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        var rows = command.AsyncRows ?? new SyncToAsyncEnumerable<object?[]>(command.SyncRows!);
        var copyCommand = PrepareNativeCopy(command, out var chunkSize);
        using var deadline = BulkInsertDeadline.Start(command.Timeout, cancellationToken);

        try
        {
            await using var enumerator = rows.GetAsyncEnumerator(deadline.Token);
            return await WriteRowsAsync(copyCommand, enumerator, deadline, command.TimeoutSeconds, chunkSize, command.Progress).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation)
        {
            throw deadline.Translate(cancellation);
        }
    }

    /// <summary>
    /// Writes <paramref name="rows"/> through <c>COPY &lt;table&gt; (&lt;cols&gt;) FROM STDIN (FORMAT
    /// BINARY)</c> with <c>NpgsqlBinaryImporter</c>. A finite <c>MaxBatchSize</c> starts one importer per
    /// bounded chunk (each completed on success and disposed on cancellation/error); an unbounded request
    /// streams every row into one importer without buffering the set.
    /// </summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The <c>Timeout</c> budget in whole seconds, or <see langword="null"/> for the provider default.</param>
    /// <param name="maxBatchSize">The maximum rows per importer, or <see langword="null"/> to stream the whole set into one importer.</param>
    /// <param name="progress">Called with the cumulative written-row count after each completed chunk, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows; the callback wrapper applies it.</param>
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller; <c>COPY</c> cannot express them.</param>
    /// <returns>The number of rows written.</returns>
    protected override int BulkInsertRows(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy)
    {
        bulkCopy.ThrowIfRequested("the PostgreSQL COPY path");
        using var deadline = StartDeadline(commandTimeoutSeconds, CancellationToken.None);
        using var enumerator = rows.GetEnumerator();
        return WriteRows(BuildCopyCommand(tableName, columnNames), enumerator, deadline, commandTimeoutSeconds, maxBatchSize, progress);
    }

    /// <summary>Asynchronously writes <paramref name="rows"/> through a binary <c>COPY</c>.</summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The <c>Timeout</c> budget in whole seconds, or <see langword="null"/> for the provider default.</param>
    /// <param name="maxBatchSize">The maximum rows per importer, or <see langword="null"/> to stream the whole set into one importer.</param>
    /// <param name="progress">Called with the cumulative written-row count after each completed chunk, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows; the callback wrapper applies it.</param>
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller; <c>COPY</c> cannot express them.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    protected override async Task<int> BulkInsertRowsAsync(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IAsyncEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy, CancellationToken cancellationToken)
    {
        bulkCopy.ThrowIfRequested("the PostgreSQL COPY path");
        using var deadline = StartDeadline(commandTimeoutSeconds, cancellationToken);

        try
        {
            await using var enumerator = rows.GetAsyncEnumerator(deadline.Token);
            return await WriteRowsAsync(BuildCopyCommand(tableName, columnNames), enumerator, deadline, commandTimeoutSeconds, maxBatchSize, progress).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation)
        {
            throw deadline.Translate(cancellation);
        }
    }

    private static BulkInsertDeadline StartDeadline(int? commandTimeoutSeconds, CancellationToken cancellationToken)
        => BulkInsertDeadline.Start(commandTimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null, cancellationToken);

    private int WriteRows(string copyCommand, IEnumerator<object?[]> enumerator, BulkInsertDeadline deadline, int? configuredTimeoutSeconds, int? chunkSize, Action<int>? progress)
    {
        if (!enumerator.MoveNext())
            return 0;

        if (chunkSize is not { } maxRows)
            return WriteSingleImporter(copyCommand, enumerator, deadline, configuredTimeoutSeconds, progress);

        var total = 0;
        var chunk = new List<object?[]>(maxRows);
        do
        {
            deadline.ThrowIfCancelled();
            chunk.Add(enumerator.Current);
            while (chunk.Count < maxRows && enumerator.MoveNext())
            {
                deadline.ThrowIfCancelled();
                chunk.Add(enumerator.Current);
            }

            total += WriteChunk(copyCommand, chunk, deadline, configuredTimeoutSeconds);
            chunk.Clear();
            progress?.Invoke(total);
        }
        while (enumerator.MoveNext());

        return total;
    }

    private async Task<int> WriteRowsAsync(string copyCommand, IAsyncEnumerator<object?[]> enumerator, BulkInsertDeadline deadline, int? configuredTimeoutSeconds, int? chunkSize, Action<int>? progress)
    {
        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            return 0;

        if (chunkSize is not { } maxRows)
            return await WriteSingleImporterAsync(copyCommand, enumerator, deadline, configuredTimeoutSeconds, progress).ConfigureAwait(false);

        var total = 0;
        var chunk = new List<object?[]>(maxRows);
        do
        {
            deadline.ThrowIfCancelled();
            chunk.Add(enumerator.Current);
            while (chunk.Count < maxRows && await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                deadline.ThrowIfCancelled();
                chunk.Add(enumerator.Current);
            }

            total += await WriteChunkAsync(copyCommand, chunk, deadline, configuredTimeoutSeconds).ConfigureAwait(false);
            chunk.Clear();
            progress?.Invoke(total);
        }
        while (await enumerator.MoveNextAsync().ConfigureAwait(false));

        return total;
    }

    private int WriteSingleImporter(string copyCommand, IEnumerator<object?[]> enumerator, BulkInsertDeadline deadline, int? configuredTimeoutSeconds, Action<int>? progress)
    {
        var session = BeginBinaryCopy(copyCommand, ResolveCopyTimeout(deadline, configuredTimeoutSeconds));
        try
        {
            do
            {
                deadline.ThrowIfCancelled();
                session.WriteRow(enumerator.Current);
            }
            while (enumerator.MoveNext());

            var completed = (int)session.Complete();
            progress?.Invoke(completed);
            return completed;
        }
        finally
        {
            session.Dispose();
        }
    }

    private async Task<int> WriteSingleImporterAsync(string copyCommand, IAsyncEnumerator<object?[]> enumerator, BulkInsertDeadline deadline, int? configuredTimeoutSeconds, Action<int>? progress)
    {
        var session = await BeginBinaryCopyAsync(copyCommand, ResolveCopyTimeout(deadline, configuredTimeoutSeconds), deadline.Token).ConfigureAwait(false);
        try
        {
            do
            {
                deadline.ThrowIfCancelled();
                await session.WriteRowAsync(enumerator.Current, deadline.Token).ConfigureAwait(false);
            }
            while (await enumerator.MoveNextAsync().ConfigureAwait(false));

            var completed = (int)await session.CompleteAsync(deadline.Token).ConfigureAwait(false);
            progress?.Invoke(completed);
            return completed;
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private int WriteChunk(string copyCommand, List<object?[]> chunk, BulkInsertDeadline deadline, int? configuredTimeoutSeconds)
    {
        var session = BeginBinaryCopy(copyCommand, ResolveCopyTimeout(deadline, configuredTimeoutSeconds));
        try
        {
            foreach (var row in chunk)
            {
                deadline.ThrowIfCancelled();
                session.WriteRow(row);
            }

            return (int)session.Complete();
        }
        finally
        {
            session.Dispose();
        }
    }

    private async Task<int> WriteChunkAsync(string copyCommand, List<object?[]> chunk, BulkInsertDeadline deadline, int? configuredTimeoutSeconds)
    {
        var session = await BeginBinaryCopyAsync(copyCommand, ResolveCopyTimeout(deadline, configuredTimeoutSeconds), deadline.Token).ConfigureAwait(false);
        try
        {
            foreach (var row in chunk)
            {
                deadline.ThrowIfCancelled();
                await session.WriteRowAsync(row, deadline.Token).ConfigureAwait(false);
            }

            return (int)await session.CompleteAsync(deadline.Token).ConfigureAwait(false);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts one binary-COPY importer on the context's own connection. Overridden by the unit tests with
    /// an instrumented session so the chunk/Complete/dispose contract is observable without a server.
    /// </summary>
    /// <param name="copyCommand">The rendered <c>COPY</c> statement.</param>
    /// <param name="timeout">The remaining whole-invocation budget, or <see langword="null"/> for the provider default.</param>
    /// <returns>The started importer session.</returns>
    internal virtual IBinaryCopySession BeginBinaryCopy(string copyCommand, TimeSpan? timeout)
    {
        EnsureConnectionOpen();
        var connection = (NpgsqlConnection)GetConnection();
        var importer = connection.BeginBinaryImport(copyCommand);
        ApplyImporterTimeout(importer, timeout);
        return new NpgsqlBinaryCopySession(importer);
    }

    /// <summary>Asynchronously starts one binary-COPY importer on the context's own connection.</summary>
    /// <param name="copyCommand">The rendered <c>COPY</c> statement.</param>
    /// <param name="timeout">The remaining whole-invocation budget, or <see langword="null"/> for the provider default.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task producing the started importer session.</returns>
    internal virtual async Task<IBinaryCopySession> BeginBinaryCopyAsync(string copyCommand, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = (NpgsqlConnection)GetConnection();
        var importer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken).ConfigureAwait(false);
        ApplyImporterTimeout(importer, timeout);
        return new NpgsqlBinaryCopySession(importer);
    }

    private static void ApplyImporterTimeout(NpgsqlBinaryImporter importer, TimeSpan? timeout)
    {
        if (timeout is { } value)
            importer.Timeout = value;
    }

    /// <summary>
    /// The driver timeout of the next importer: the <em>remaining</em> budget of the one monotonic
    /// deadline, never the whole request re-applied per chunk. Without a nextorm budget the configured
    /// whole seconds are passed through (or the provider default when both are unset).
    /// </summary>
    /// <param name="deadline">The started whole-invocation budget.</param>
    /// <param name="configuredSeconds">The configured budget in whole seconds, or <see langword="null"/>.</param>
    /// <returns>The budget to hand to <c>NpgsqlBinaryImporter.Timeout</c>, or <see langword="null"/> to keep the provider default.</returns>
    internal static TimeSpan? ResolveCopyTimeout(BulkInsertDeadline deadline, int? configuredSeconds)
    {
        if (deadline.Remaining is not { } remaining)
            return configuredSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

        return remaining <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining;
    }

    private string PrepareNativeCopy(BulkInsertCommand command, out int? chunkSize)
    {
        command.BulkCopy.ThrowIfRequested("the PostgreSQL COPY path");
        EnsureKeepNullsIsDefaultEquivalent(command);
        chunkSize = command.Batch?.MaxBatchSize;
        return BuildCopyCommand(command);
    }

    // Binary COPY always writes the supplied value, so KeepNulls=false can only be honoured when writing
    // NULL is indistinguishable from letting the destination DEFAULT apply: every written column must be
    // nullable and have a NULL/absent SQL default. The SQL-default half is certified against the live
    // schema by the integration suite; a non-nullable input column is refused here, before any row is read.
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
            "BulkInsertOptions.KeepNulls=false cannot be honoured by the PostgreSQL binary COPY path: COPY always writes the supplied value and applies a column DEFAULT only when the column is omitted. "
            + "The request is accepted only for a default-equivalent shape, where every written column is nullable with a NULL/absent SQL default; "
            + $"the non-nullable input column(s) {string.Join(", ", nonNullable.Select(name => $"'{name}'"))} break that shape. Use KeepNulls=true, or write the DEFAULT explicitly.");
    }

    private string BuildCopyCommand(BulkInsertCommand command)
    {
        var destination = BulkInsertDestination.From(command.Options);
        ValidateDatabase(destination);

        var tableName = SqlMutationBuilder.RenderTableReference(
            Dialect,
            QuoteIdentifiers,
            command.TableName,
            destination.Schema,
            command.IsTableNameAuto,
            command.EntityType,
            NamingConvention);

        var columnNames = new string[command.Columns.Count];
        for (var i = 0; i < columnNames.Length; i++)
            columnNames[i] = SqlMutationBuilder.ResolveColumnName(command.Columns[i], NamingConvention);

        return BuildCopyCommand(tableName, columnNames);
    }

    // PostgreSQL COPY runs on the context's own connection, so only the current database is addressable:
    // naming the current database stays a no-op part, naming another one is refused before the source is read.
    private void ValidateDatabase(BulkInsertDestination destination)
    {
        if (destination.Database is null)
            return;

        var current = GetConnection() is NpgsqlConnection connection ? connection.Database : string.Empty;
        if (!string.Equals(destination.Database, current, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"BulkInsertOptions.DatabaseName '{destination.Database}' cannot be addressed by a PostgreSQL bulk insert: COPY writes on the context's own connection, so only the current database ('{current}') is addressable. Connect to the required database instead.");
    }

    private string BuildCopyCommand(string tableName, IReadOnlyList<string> columnNames)
    {
        var quotedColumns = string.Join(", ", columnNames.Select(Dialect.QuoteIdentifier));
        return $"COPY {tableName} ({quotedColumns}) FROM STDIN (FORMAT BINARY)";
    }

    private sealed class NpgsqlBinaryCopySession(NpgsqlBinaryImporter importer) : IBinaryCopySession
    {
        public void WriteRow(object?[] row) => importer.WriteRow(row);

        public Task WriteRowAsync(object?[] row, CancellationToken cancellationToken)
            => importer.WriteRowAsync(cancellationToken, row);

        public long Complete() => (long)importer.Complete();

        public async Task<long> CompleteAsync(CancellationToken cancellationToken)
            => (long)await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);

        public void Dispose() => importer.Dispose();

        public ValueTask DisposeAsync() => importer.DisposeAsync();
    }
}
