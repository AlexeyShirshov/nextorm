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
    private static readonly MethodInfo GetByteMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte))!;
    private static readonly MethodInfo GetInt16MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16))!;
    private static readonly MethodInfo GetInt32MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32))!;
    private static readonly MethodInfo GetInt64MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))!;
    private static readonly MethodInfo GetFloatMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat))!;
    private static readonly MethodInfo GetDoubleMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble))!;
    private static readonly MethodInfo GetDecimalMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal))!;

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

    /// <summary>
    /// CSV-terminal variant of <see cref="MapColumnExpression"/>: for an unconverted numeric column it
    /// reads the value with the typed getter of the reader's actual storage type (<paramref name="storageType"/>)
    /// and converts it with the matching static <c>Convert.To&lt;Target&gt;(storage)</c> overload. That
    /// keeps the per-row path free of <see cref="IDataRecord.GetValue"/> boxing while the shared buffered
    /// mapper (<see cref="MapColumnExpression"/>) stays on its object-based widening.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="record">The data-reader expression the accessor is built from.</param>
    /// <param name="storageType">The reader's CLR field type for the column's ordinal.</param>
    /// <returns>An expression that reads and converts the column value without boxing.</returns>
    protected override Expression MapTypedColumnExpression(SelectExpression column, Expression record, Type storageType)
    {
        // A converted column owns its reader type; the numeric widening below must not bypass the converter.
        if (column.Converter is not null)
            return base.MapTypedColumnExpression(column, record, storageType);

        var target = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;

        if (!IsNumeric(target))
            return base.MapTypedColumnExpression(column, record, storageType);

        var storage = Nullable.GetUnderlyingType(storageType) ?? storageType;
        var index = Expression.Constant(column.Index);

        var storageGetter = GetNumericGetter(storage);
        if (storageGetter is null || !IsNumeric(storage))
            throw new NotSupportedException(
                $"The CSV terminal cannot read the column '{column.PropertyName}' without boxing: SQL Server reported the storage type '{storageType.Name}' for a numeric projection of '{target.Name}', and there is no typed getter for it. Project a column whose storage type is one of byte, short, int, long, float, double or decimal, or materialise the query and format the value yourself.");

        Expression value;
        if (storage == target)
        {
            value = Expression.Call(record, storageGetter, index);
        }
        else
        {
            var conversion = GetTypedConversion(target, storage);
            if (conversion is null)
                throw new NotSupportedException(
                    $"The CSV terminal cannot read the column '{column.PropertyName}' without boxing: there is no typed Convert.To{target.Name}({storage.Name}) overload to convert the storage type '{storageType.Name}' to the projected type '{target.Name}'. Project a supported numeric type, or materialise the query and format the value yourself.");

            value = Expression.Call(conversion, Expression.Call(record, storageGetter, index));
        }

        if (value.Type != column.PropertyType)
            value = Expression.Convert(value, column.PropertyType);

        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(record, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            // A non-nullable *OrDefault scalar: SQL NULL means no row, so return default(T).
            return Expression.Condition(
                Expression.Call(record, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                value);
        }

        return value;
    }

    /// <summary>
    /// The typed CSV hook reads an unconverted numeric column with the storage-typed getter of the
    /// reader's actual field type, so a numeric column that <see cref="MapColumnExpression"/> would
    /// widen through <c>GetValue</c> must not be rejected before the reader reports its storage type.
    /// A converted column keeps its converter and a non-numeric column is not handled by the typed
    /// hook, so both keep the default rejection.
    /// </summary>
    protected override bool SupportsTypedColumnMapping(SelectExpression column)
    {
        if (column.Converter is not null)
            return false;

        var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
        return IsNumeric(type);
    }

    private static MethodInfo? GetNumericGetter(Type type) => type switch
    {
        _ when type == typeof(byte) => GetByteMI,
        _ when type == typeof(short) => GetInt16MI,
        _ when type == typeof(int) => GetInt32MI,
        _ when type == typeof(long) => GetInt64MI,
        _ when type == typeof(float) => GetFloatMI,
        _ when type == typeof(double) => GetDoubleMI,
        _ when type == typeof(decimal) => GetDecimalMI,
        _ => null,
    };

    private static MethodInfo? GetTypedConversion(Type target, Type storage)
    {
        var name = target == typeof(byte) ? nameof(Convert.ToByte)
            : target == typeof(short) ? nameof(Convert.ToInt16)
            : target == typeof(int) ? nameof(Convert.ToInt32)
            : target == typeof(long) ? nameof(Convert.ToInt64)
            : target == typeof(float) ? nameof(Convert.ToSingle)
            : target == typeof(double) ? nameof(Convert.ToDouble)
            : target == typeof(decimal) ? nameof(Convert.ToDecimal)
            : null;

        return name is null ? null : typeof(Convert).GetMethod(name, [storage]);
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
