using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextORM.Core;
using Npgsql;
using NpgsqlTypes;

namespace NextORM.Postgres;

/// <summary>
/// Data context for PostgreSQL. Wraps the <c>Npgsql</c> driver for connections and parameters and
/// renders SQL through <c>PostgresDialect</c>.
/// </summary>
public class PostgresDataContext : DataContext
{
    private static readonly MethodInfo GetFieldValueMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
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
    protected override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
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

        return base.MapColumnExpression(column, param);
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
    /// Writes <paramref name="rows"/> through <c>COPY &lt;table&gt; (&lt;cols&gt;) FROM STDIN (FORMAT
    /// BINARY)</c> with <c>NpgsqlBinaryImporter</c>, streaming each row without buffering the set.
    /// </summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The command timeout in seconds; rejected because <c>COPY</c> has none.</param>
    /// <param name="maxBatchSize">Ignored: the binary importer streams one row at a time.</param>
    /// <param name="progress">Called with the cumulative written-row count every <paramref name="notifyEvery"/> rows, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller; <c>COPY</c> cannot express them.</param>
    /// <returns>The number of rows written.</returns>
    protected override int BulkInsertRows(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy)
    {
        EnsureCopyTimeoutSupported(commandTimeoutSeconds);
        bulkCopy.ThrowIfRequested("the PostgreSQL COPY path");
        EnsureConnectionOpen();
        var connection = (NpgsqlConnection)GetConnection();

        using var enumerator = rows.GetEnumerator();
        if (!enumerator.MoveNext())
            return 0;

        using var writer = connection.BeginBinaryImport(BuildCopyCommand(tableName, columnNames));
        var written = 0;

        do
        {
            writer.WriteRow(enumerator.Current);
            written++;
            if (progress is not null && written % notifyEvery == 0)
                progress(written);
        }
        while (enumerator.MoveNext());

        return (int)writer.Complete();
    }

    /// <summary>Asynchronously writes <paramref name="rows"/> through a binary <c>COPY</c>.</summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The command timeout in seconds; rejected because <c>COPY</c> has none.</param>
    /// <param name="maxBatchSize">Ignored: the binary importer streams one row at a time.</param>
    /// <param name="progress">Called with the cumulative written-row count every <paramref name="notifyEvery"/> rows, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller; <c>COPY</c> cannot express them.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    protected override async Task<int> BulkInsertRowsAsync(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IAsyncEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy, CancellationToken cancellationToken)
    {
        EnsureCopyTimeoutSupported(commandTimeoutSeconds);
        bulkCopy.ThrowIfRequested("the PostgreSQL COPY path");
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = (NpgsqlConnection)GetConnection();

        await using var enumerator = rows.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            return 0;

        using var writer = await connection.BeginBinaryImportAsync(BuildCopyCommand(tableName, columnNames), cancellationToken).ConfigureAwait(false);
        var written = 0;

        do
        {
            await writer.WriteRowAsync(cancellationToken, enumerator.Current).ConfigureAwait(false);
            written++;
            if (progress is not null && written % notifyEvery == 0)
                progress(written);
        }
        while (await enumerator.MoveNextAsync().ConfigureAwait(false));

        return (int)await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureCopyTimeoutSupported(int? commandTimeoutSeconds)
    {
        if (commandTimeoutSeconds is not null)
            throw new NotSupportedException("A bulk-insert Timeout is not supported by the PostgreSQL COPY path; omit Timeout or use a provider whose native bulk API supports it.");
    }

    private string BuildCopyCommand(string tableName, IReadOnlyList<string> columnNames)
    {
        var quotedColumns = string.Join(", ", columnNames.Select(Dialect.QuoteIdentifier));
        return $"COPY {tableName} ({quotedColumns}) FROM STDIN (FORMAT BINARY)";
    }
}
