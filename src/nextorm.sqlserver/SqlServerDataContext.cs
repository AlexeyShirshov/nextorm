using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient;
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
