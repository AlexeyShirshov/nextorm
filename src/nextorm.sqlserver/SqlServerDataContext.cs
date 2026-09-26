using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// Data context for Microsoft SQL Server. Wraps the <c>Microsoft.Data.SqlClient</c> driver for
/// connections and parameters and renders SQL through <c>SqlServerDialect</c>.
/// </summary>
public class SqlServerDataContext : DataContext
{
    private static readonly MethodInfo GetValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo ChangeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;

    /// <summary>
    /// Creates a SQL Server context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>SqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqlServerDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a SQL Server context over a caller-supplied connection, which the context does not
    /// dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqlServerDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>Creates a new <c>SqlConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>A new, unopened SQL Server connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new SqlConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => SqlServerDialect.Instance;

    /// <summary>
    /// Creates a <c>SqlParameter</c>, mapping a <see langword="null"/> value to <c>DBNull.Value</c>
    /// because SqlClient otherwise omits the parameter entirely.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new SQL Server parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // A null value would leave SqlParameter unset, and SqlClient then sends no value at all
        // ("expects the parameter ... which was not supplied"), so nulls must be passed as DBNull.
        return new SqlParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Creates a <c>SqlParameter</c> from a descriptor, additionally applying a structured parameter
    /// type name. When <see cref="ProcedureParameter.TypeName"/> is set the parameter is marked
    /// <see cref="SqlDbType.Structured"/> (SqlClient rejects a type name on a non-structured parameter)
    /// — a structured parameter is <b>input only</b>, so any other <see cref="ParameterDirection"/> is
    /// rejected. <see cref="SqlDbType.Structured"/> overrides <see cref="ProcedureParameter.DbType"/>
    /// for that parameter; the legacy value must be a <c>DataTable</c>/<c>IEnumerable&lt;SqlDataRecord&gt;</c>.
    /// <para>
    /// When <see cref="ProcedureParameter.Value"/> is a <see cref="TableParameterValue"/> (built with
    /// <see cref="ProcedureParameter.Table{T}(string, string, IEnumerable{T})"/>) the rows are streamed
    /// as <c>SqlDataRecord</c>s and <see cref="ProcedureParameter.TypeName"/> is <b>required</b>.
    /// </para>
    /// <para>
    /// The CLR type of each bound column picks a fixed T-SQL type: <c>bool</c>→<c>bit</c>,
    /// <c>char</c>→<c>nchar(1)</c>, <c>sbyte</c>/<c>short</c>→<c>smallint</c>,
    /// <c>byte</c>→<c>tinyint</c>, <c>ushort</c>/<c>int</c>→<c>int</c>, <c>uint</c>/<c>long</c>→<c>bigint</c>,
    /// <c>ulong</c>→<c>decimal(20,0)</c>, <c>float</c>→<c>real</c>, <c>double</c>→<c>float</c>,
    /// <c>decimal</c>→<c>decimal(38,18)</c> (or the property's declared precision/scale), <c>string</c>→<c>nvarchar(max)</c>,
    /// <c>Guid</c>→<c>uniqueidentifier</c>, <c>DateTime</c>→<c>datetime2</c>,
    /// <c>DateTimeOffset</c>→<c>datetimeoffset</c>, <c>DateOnly</c>→<c>date</c>, <c>TimeOnly</c>→<c>time</c>,
    /// <c>TimeSpan</c>→<c>bigint</c> (the mapped duration unit, or ticks) and <c>byte[]</c>→<c>varbinary(max)</c>.
    /// A <c>decimal</c> column uses the precision/scale declared with
    /// <see cref="DecimalPrecisionAttribute"/> or the fluent mapping, defaulting to <c>decimal(38,18)</c>;
    /// for a different precision/scale on any other type declare the user-defined table type explicitly
    /// and pass a legacy <c>DataTable</c> with <see cref="ProcedureParameter.TypeName"/>.
    /// </para>
    /// </summary>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new SQL Server parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="parameter"/> is a table-valued parameter without a type name, or a structured parameter whose <see cref="ParameterDirection"/> is not <see cref="ParameterDirection.Input"/>.</exception>
    protected override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
    {
        if (parameter.Value is TableParameterValue tableValue)
            return CreateTableValuedParameter(parameter, tableValue);

        // A table-valued parameter is input-only: reject an output/return direction before any
        // parameter is allocated so the per-call command can be disposed by the leak-safe path.
        if (parameter.TypeName is not null && parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A structured (table-valued) parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}. "
                + "Declare it with ParameterDirection.Input and return the procedure's values through result sets or Output parameters.",
                nameof(parameter));

        // The base throws on TypeName (no provider-neutral structured type); strip it, then apply the
        // SQL Server-specific structured handling after the common Direction/DbType/Size processing.
        var dbParameter = base.CreateProcedureParameter(parameter with { TypeName = null });
        var sqlParameter = (SqlParameter)dbParameter;

        if (parameter.TypeName is not null)
        {
            sqlParameter.TypeName = parameter.TypeName;
            // SqlClient requires Structured for a table-valued parameter; TypeName alone is rejected.
            // Structured replaces any DbType the descriptor requested (they describe the same type slot).
            sqlParameter.SqlDbType = SqlDbType.Structured;
        }

        return sqlParameter;
    }

    private SqlParameter CreateTableValuedParameter(ProcedureParameter parameter, TableParameterValue value)
    {
        if (parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A table-valued parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}.",
                nameof(parameter));

        if (parameter.TypeName is null)
            throw new ArgumentException(
                $"SQL Server table-valued parameter '{parameter.Name}' requires the user-defined table type name. "
                + "Use ProcedureParameter.Table(name, typeName, rows).",
                nameof(parameter));

        var sqlParameter = new SqlParameter(parameter.Name, DBNull.Value)
        {
            TypeName = parameter.TypeName,
            SqlDbType = SqlDbType.Structured,
        };

        // An empty sequence cannot be streamed as records: SqlClient rejects DBNull for a structured
        // parameter ("Table-valued parameters cannot be DBNull"), so an empty table-valued parameter is
        // sent as an unset (null) value. A non-empty sequence is streamed lazily (one reused
        // SqlDataRecord), so a large set is never buffered in memory.
        sqlParameter.Value = BuildRecords(value);
        return sqlParameter;
    }

    private IEnumerable<SqlDataRecord>? BuildRecords(TableParameterValue value)
    {
        var columns = value.GetColumns(this);
        var metadata = new SqlMetaData[columns.Count];
        for (var i = 0; i < columns.Count; i++)
            metadata[i] = BuildMetaData(columns[i]);

        // An empty set is bound as an unset (null) value: SqlClient rejects DBNull for a structured
        // parameter and also rejects an empty record sequence ("Enumerable doesn't contain any records").
        if (value.Count == 0)
            return null;

        // A known non-empty collection can always be re-enumerated from the source.
        if (value.Count is not null)
            return EnumerateRecords(value.Rows, null, null, columns, metadata);

        // An unknown/lazy source is peeked once at bind time with a dedicated enumerator: no first row
        // means the set is empty (bind null); otherwise the already-started enumerator is handed to the
        // first enumeration (so a one-shot source still works) while later enumerations restart from the
        // source (so a re-enumerable source works repeatedly).
        var enumerator = value.Rows.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            enumerator.Dispose();
            return null;
        }

        return new PeekedRecordSequence(value.Rows, enumerator, enumerator.Current, columns, metadata);
    }

    // The returned sequence is re-enumerable when the source is: every GetEnumerator that does not own
    // the peeked enumerator starts a fresh enumeration of the source and allocates its own
    // SqlDataRecord, so executing the same descriptor twice yields the full set each time.
    private static IEnumerable<SqlDataRecord> EnumerateRecords(
        IEnumerable<object> rows,
        IEnumerator<object>? started,
        object? first,
        IReadOnlyList<TableParameterColumn> columns,
        SqlMetaData[] metadata)
    {
        var record = new SqlDataRecord(metadata);

        if (started is not null)
        {
            try
            {
                yield return Project(record, first!, columns);

                while (started.MoveNext())
                    yield return Project(record, started.Current, columns);
            }
            finally
            {
                started.Dispose();
            }

            yield break;
        }

        foreach (var row in rows)
            yield return Project(record, row, columns);
    }

    private static SqlDataRecord Project(SqlDataRecord record, object row, IReadOnlyList<TableParameterColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
            record.SetValue(i, ToSqlValue(columns[i].GetValue(row), columns[i].ClrType) ?? DBNull.Value);

        return record;
    }

    /// <summary>
    /// Wraps a source whose emptiness was checked by peeking one row. The first enumeration consumes the
    /// already-started enumerator (including the peeked row) and disposes it in its finally; later
    /// enumerations restart from the source. If the parameter is never enumerated (the command is never
    /// executed), the peeked enumerator is left to the GC: an ordinary LINQ/iterator enumerator holds no
    /// unmanaged resource, so pass a materialized collection when the source owns one.
    /// </summary>
    private sealed class PeekedRecordSequence : IEnumerable<SqlDataRecord>
    {
        private readonly IEnumerable<object> _rows;
        private readonly IReadOnlyList<TableParameterColumn> _columns;
        private readonly SqlMetaData[] _metadata;
        private IEnumerator<object>? _peeked;
        private object? _first;

        internal PeekedRecordSequence(
            IEnumerable<object> rows,
            IEnumerator<object> peeked,
            object first,
            IReadOnlyList<TableParameterColumn> columns,
            SqlMetaData[] metadata)
        {
            _rows = rows;
            _peeked = peeked;
            _first = first;
            _columns = columns;
            _metadata = metadata;
        }

        public IEnumerator<SqlDataRecord> GetEnumerator()
        {
            var peeked = _peeked;
            var first = _first;
            _peeked = null;
            _first = null;

            return peeked is null
                ? EnumerateRecords(_rows, null, null, _columns, _metadata).GetEnumerator()
                : EnumerateRecords(_rows, peeked, first, _columns, _metadata).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // The metadata type is chosen from the column's CLR type, so a value that needs a wider/coerced
    // provider representation is converted to the matching CLR type before SetValue.
    private static object? ToSqlValue(object? value, Type clrType)
    {
        if (value is null)
            return null;

        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (type.IsEnum)
            return Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
        if (type == typeof(char))
            return value.ToString();
        if (type == typeof(sbyte))
            return (short)(sbyte)value;
        if (type == typeof(ushort))
            return (int)(ushort)value;
        if (type == typeof(uint))
            return (long)(uint)value;
        if (type == typeof(ulong))
            return (decimal)(ulong)value;

        return value;
    }

    // The fixed-width scalar mappings: the CLR type decides the SQL type without any extra
    // length/precision/scale. Types that need those are handled by BuildSizedMetaData.
    private static readonly Dictionary<Type, SqlDbType> ScalarSqlDbTypes = new()
    {
        [typeof(bool)] = SqlDbType.Bit,
        [typeof(sbyte)] = SqlDbType.SmallInt,
        [typeof(byte)] = SqlDbType.TinyInt,
        [typeof(short)] = SqlDbType.SmallInt,
        [typeof(ushort)] = SqlDbType.Int,
        [typeof(int)] = SqlDbType.Int,
        [typeof(uint)] = SqlDbType.BigInt,
        [typeof(long)] = SqlDbType.BigInt,
        [typeof(float)] = SqlDbType.Real,
        [typeof(double)] = SqlDbType.Float,
        [typeof(Guid)] = SqlDbType.UniqueIdentifier,
        [typeof(DateTime)] = SqlDbType.DateTime2,
        [typeof(DateTimeOffset)] = SqlDbType.DateTimeOffset,
        [typeof(DateOnly)] = SqlDbType.Date,
        [typeof(TimeOnly)] = SqlDbType.Time,
    };

    private static SqlMetaData BuildMetaData(TableParameterColumn column)
    {
        var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;

        // SQL Server has no native duration type: a TimeSpan column is written as a bigint in ticks
        // (the same rule as the bulk-copy path).
        if (type == typeof(TimeSpan))
            type = typeof(long);

        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);

        if (ScalarSqlDbTypes.TryGetValue(type, out var dbType))
            return new SqlMetaData(column.Name, dbType);

        return BuildSizedMetaData(column, type);
    }

    // The mappings that carry a max length / precision / scale, kept explicit because SqlMetaData has
    // no single constructor for them.
    private static SqlMetaData BuildSizedMetaData(TableParameterColumn column, Type type)
    {
        if (type == typeof(char))
            return new SqlMetaData(column.Name, SqlDbType.NChar, 1);
        if (type == typeof(string))
            return new SqlMetaData(column.Name, SqlDbType.NVarChar, -1);
        if (type == typeof(byte[]))
            return new SqlMetaData(column.Name, SqlDbType.VarBinary, -1);
        if (type == typeof(ulong))
            return new SqlMetaData(column.Name, SqlDbType.Decimal, precision: 20, scale: 0);

        // A decimal column uses the declared precision/scale when the property maps one; otherwise
        // SQL Server's historical TVP default (38, 18) is kept.
        if (type == typeof(decimal))
        {
            if (column.DecimalPrecision is { } precision)
                return new SqlMetaData(column.Name, SqlDbType.Decimal, precision: (byte)precision, scale: (byte)(column.DecimalScale ?? 0));

            return new SqlMetaData(column.Name, SqlDbType.Decimal, precision: 38, scale: 18);
        }

        throw new NotSupportedException(
            $"SQL Server table-valued parameters do not support the CLR type {type.Name} (column '{column.Name}').");
    }

    /// <summary>
    /// Maps projected numeric columns by reading the raw value and converting it to the projected CLR
    /// type, because SqlClient typed getters throw when the field type does not match.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <returns>An expression that reads and converts the column value.</returns>
    public override Expression MapColumnExpression(SelectExpression column, Expression param)
    {
        // A converted column owns its reader type; the numeric widening below must not bypass the
        // converter.
        if (column.Converter is not null)
            return base.MapColumnExpression(column, param);

        var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;

        if (!IsNumeric(type))
            return base.MapColumnExpression(column, param);

        // SqlClient typed getters are strict: reading an int column through GetInt64 throws, and a
        // computed numeric expression can be wider than the projected CLR type. Read the value and
        // convert it instead of relying on the reader getter to widen the type.
        var index = Expression.Constant(column.Index);
        var value = Expression.Convert(
            Expression.Call(ChangeTypeMethod, Expression.Call(param, GetValueMethod, index), Expression.Constant(type)),
            column.PropertyType);

        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            // A non-nullable *OrDefault scalar: SQL NULL means no row, so return default(T) rather than
            // letting ChangeType(DBNull) throw.
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                value);
        }

        return value;
    }
    private static bool IsNumeric(Type type) => type == typeof(byte) || type == typeof(short)
        || type == typeof(int) || type == typeof(long) || type == typeof(float)
        || type == typeof(double) || type == typeof(decimal);

    /// <summary>
    /// Writes <paramref name="rows"/> through <c>SqlBulkCopy</c>. The rows are buffered in a
    /// <see cref="DataTable"/> because <c>SqlBulkCopy</c> needs column type metadata; the whole set is
    /// held in memory before the copy starts.
    /// </summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The <c>SqlBulkCopy</c> timeout in seconds, or <see langword="null"/> for the 30s default.</param>
    /// <param name="maxBatchSize">The <c>SqlBulkCopy.BatchSize</c> in rows, or <see langword="null"/> for one batch.</param>
    /// <param name="progress">Called with the cumulative written-row count on each native progress tick, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller.</param>
    /// <returns>The number of rows written.</returns>
    protected override int BulkInsertRows(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy)
    {
        EnsureConnectionOpen();
        var table = BuildTable(columnNames, columns);
        var count = 0;

        foreach (var row in rows)
        {
            table.Rows.Add(row);
            count++;
        }

        using (table)
        {
            using var bulk = CreateBulkCopy(tableName, columnNames, commandTimeoutSeconds, maxBatchSize, progress, notifyEvery, bulkCopy);

            try
            {
                bulk.WriteToServer(table);
            }
            catch (Microsoft.Data.OperationAbortedException ex) when (ex.InnerException is OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        return count;
    }

    /// <summary>Asynchronously writes <paramref name="rows"/> through <c>SqlBulkCopy</c>.</summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The <c>SqlBulkCopy</c> timeout in seconds, or <see langword="null"/> for the 30s default.</param>
    /// <param name="maxBatchSize">The <c>SqlBulkCopy.BatchSize</c> in rows, or <see langword="null"/> for one batch.</param>
    /// <param name="progress">Called with the cumulative written-row count on each native progress tick, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    protected override async Task<int> BulkInsertRowsAsync(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IAsyncEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy, CancellationToken cancellationToken)
    {
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var table = BuildTable(columnNames, columns);
        var count = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            table.Rows.Add(row);
            count++;
        }

        using (table)
        {
            using var bulk = CreateBulkCopy(tableName, columnNames, commandTimeoutSeconds, maxBatchSize, progress, notifyEvery, bulkCopy);

            try
            {
                await bulk.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
            }
            catch (Microsoft.Data.OperationAbortedException ex) when (ex.InnerException is OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        return count;
    }

    private SqlBulkCopy CreateBulkCopy(string tableName, IReadOnlyList<string> columnNames, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy)
    {
        var connection = (SqlConnection)GetConnection();
        var transaction = (this as ITransactionManager)?.CurrentTransaction as SqlTransaction;

        var bulk = new SqlBulkCopy(connection, MapBulkCopyOptions(bulkCopy.CheckConstraints, bulkCopy.TableLock, bulkCopy.KeepNulls, bulkCopy.FireTriggers), transaction)
        {
            DestinationTableName = tableName,
            BulkCopyTimeout = commandTimeoutSeconds ?? 30,
            BatchSize = maxBatchSize ?? 0
        };

        for (var i = 0; i < columnNames.Count; i++)
            bulk.ColumnMappings.Add(columnNames[i], columnNames[i]);

        if (progress is not null)
        {
            bulk.NotifyAfter = notifyEvery;
            bulk.SqlRowsCopied += (_, e) => progress((int)e.RowsCopied);
        }

        return bulk;
    }

    internal static SqlBulkCopyOptions MapBulkCopyOptions(bool checkConstraints, bool tableLock, bool keepNulls, bool fireTriggers)
    {
        var options = SqlBulkCopyOptions.Default;
        if (checkConstraints)
            options |= SqlBulkCopyOptions.CheckConstraints;
        if (tableLock)
            options |= SqlBulkCopyOptions.TableLock;
        if (keepNulls)
            options |= SqlBulkCopyOptions.KeepNulls;
        if (fireTriggers)
            options |= SqlBulkCopyOptions.FireTriggers;

        return options;
    }

    private DataTable BuildTable(IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns)
    {
        var table = new DataTable();
        for (var i = 0; i < columnNames.Count; i++)
        {
            var type = Nullable.GetUnderlyingType(columns[i].PropertyInfo.PropertyType) ?? columns[i].PropertyInfo.PropertyType;
            // SQL Server has no native duration type, so a TimeSpan column is written as a bigint in
            // the declared unit; the value array (and therefore the DataTable column) holds the long.
            if (type == typeof(TimeSpan) && !Dialect.SupportsNativeDuration)
                type = typeof(long);

            table.Columns.Add(columnNames[i], type);
        }

        return table;
    }
}
