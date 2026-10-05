using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>One projected CSV column: its header and the compiled delegate that appends its formatted value to a row buffer.</summary>
internal sealed class CsvColumn
{
    public CsvColumn(string header, Action<IDataRecord, CsvRowBuffer> write)
    {
        Header = header;
        Write = write;
    }

    /// <summary>The header cell, derived from the projection's property name.</summary>
    public string Header { get; }

    /// <summary>Appends this column's value for the current row.</summary>
    public Action<IDataRecord, CsvRowBuffer> Write { get; }
}

/// <summary>The compiled column plan of one CSV terminal call.</summary>
internal sealed class CsvPlan
{
    public CsvPlan(CsvColumn[] columns) => Columns = columns;

    /// <summary>The projection columns, in reader-ordinal order.</summary>
    public CsvColumn[] Columns { get; }
}

/// <summary>
/// Compiles a query projection into a per-column CSV plan and streams the rows to a caller-provided
/// stream. Columns are read through the provider's typed column hook
/// (<see cref="DataContext.MapTypedColumnExpression(SelectExpression, Expression, Type)"/> on a real
/// context) with the reader's storage type, so provider-specific reader accessors and value converters
/// are honoured without an <c>object</c> round-trip. A column whose actual mapped accessor reads through
/// <see cref="IDataRecord.GetValue"/>/<c>Convert.ChangeType(object)</c> for a value type, or applies a
/// value converter through the object-based <see cref="IPropertyValueConverter.ConvertFromProvider"/>
/// bridge, is rejected while the plan is built — after the reader is open but before the header —
/// because such an accessor boxes on every row. Formatting is invariant-culture, RFC 4180, UTF-8 (no
/// BOM) and CRLF-terminated.
/// </summary>
internal static class CsvStreamWriter
{
    private static readonly IReadOnlyDictionary<Type, MethodInfo> WriteMethods = new Dictionary<Type, MethodInfo>
    {
        // sbyte/ushort are intentionally absent: the rest of nextorm cannot read them either
        // (SelectExpression.GetDataRecordMethod rejects them), so the terminal rejects them up front
        // instead of advertising support it cannot honour.

        [typeof(bool)] = GetWriteMethod(nameof(CsvValueFormatter.WriteBoolean)),
        [typeof(byte)] = GetWriteMethod(nameof(CsvValueFormatter.WriteByte)),
        [typeof(short)] = GetWriteMethod(nameof(CsvValueFormatter.WriteInt16)),
        [typeof(int)] = GetWriteMethod(nameof(CsvValueFormatter.WriteInt32)),
        [typeof(uint)] = GetWriteMethod(nameof(CsvValueFormatter.WriteUInt32)),
        [typeof(long)] = GetWriteMethod(nameof(CsvValueFormatter.WriteInt64)),
        [typeof(ulong)] = GetWriteMethod(nameof(CsvValueFormatter.WriteUInt64)),
        [typeof(float)] = GetWriteMethod(nameof(CsvValueFormatter.WriteSingle)),
        [typeof(double)] = GetWriteMethod(nameof(CsvValueFormatter.WriteDouble)),
        [typeof(decimal)] = GetWriteMethod(nameof(CsvValueFormatter.WriteDecimal)),
        [typeof(Guid)] = GetWriteMethod(nameof(CsvValueFormatter.WriteGuid)),
        [typeof(DateTime)] = GetWriteMethod(nameof(CsvValueFormatter.WriteDateTime)),
        [typeof(DateTimeOffset)] = GetWriteMethod(nameof(CsvValueFormatter.WriteDateTimeOffset)),
        [typeof(TimeSpan)] = GetWriteMethod(nameof(CsvValueFormatter.WriteTimeSpan)),
        [typeof(string)] = GetWriteMethod(nameof(CsvValueFormatter.WriteString)),
        [typeof(byte[])] = GetWriteMethod(nameof(CsvValueFormatter.WriteBytes)),
    };

    private static readonly MethodInfo ConverterBridgeMethod =
        typeof(IPropertyValueConverter).GetMethod(nameof(IPropertyValueConverter.ConvertFromProvider))!;

    private static readonly MethodInfo WriteNullFieldMethod =
        typeof(CsvRowBuffer).GetMethod(nameof(CsvRowBuffer.WriteNullField))!;

    /// <summary>
    /// Validates the prepared projection before the query is executed, using only static information.
    /// An unsupported CLR type, an object-bridged <see cref="IPropertyValueConverter"/> and a
    /// value-type column whose untyped provider mapping (<paramref name="mapColumn"/>) boxes through
    /// <see cref="IDataRecord.GetValue"/>/<see cref="Convert.ChangeType(object, Type)"/> are rejected
    /// outright, unless <paramref name="canMapTyped"/> declares that the provider's typed hook
    /// (<see cref="DataContext.MapTypedColumnExpression(SelectExpression, Expression, Type)"/>) can
    /// bind the column box-free once the reader's storage type is known (SQL Server numeric widening).
    /// Running this before the reader is opened guarantees an unsupported projection executes no SQL
    /// and writes no byte.
    /// </summary>
    /// <param name="selectList">The prepared projection, or <see langword="null"/> when none is available.</param>
    /// <param name="mapColumn">The provider's untyped column mapper (<see cref="DataContext.MapColumnExpression"/>).</param>
    /// <param name="canMapTyped">The provider capability predicate (<see cref="DataContext.SupportsTypedColumn"/>).</param>
    public static void ValidateProjection(
        SelectExpression[]? selectList,
        Func<SelectExpression, Expression, Expression> mapColumn,
        Func<SelectExpression, bool> canMapTyped)
    {
        if (selectList is null || selectList.Length == 0)
            throw new InvalidOperationException("The query has no projected columns; there is nothing to write as CSV.");

        var record = Expression.Parameter(typeof(IDataRecord), "record");
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];
            var header = Header(column);
            _ = ResolveWriteMethod(column.PropertyType, header);

            var readType = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
            // The converter bridge is rejected for every column; a value-type column whose static
            // mapping boxes is rejected too, unless the provider declares a typed hook for it.
            var allowObjectRead = readType.IsValueType && canMapTyped(column);
            EnsureBoxFree(column, mapColumn(column, record), header, allowObjectRead);
        }
    }

    /// <summary>
    /// Validates the dialect and compiles the plan with a provider's typed column hook. Must be called
    /// after the reader is open, so the reader's storage types are known; each column's
    /// <c>GetFieldType</c> is read once, when that column's accessor is bound. Throws before any row (or
    /// header byte) when the delimiter is not representable or a column type has no box-free formatter.
    /// </summary>
    /// <param name="selectList">The prepared projection, or <see langword="null"/> when none is available.</param>
    /// <param name="schema">The open reader, used to read each column's storage type, or <see langword="null"/> for a schema-less plan (tests).</param>
    /// <param name="mapTypedColumn">The provider's typed column mapper (<see cref="DataContext.MapTypedColumnExpression"/>).</param>
    /// <returns>The compiled column plan.</returns>
    public static CsvPlan Build(SelectExpression[]? selectList, IDataRecord? schema, Func<SelectExpression, Expression, Type, Expression> mapTypedColumn)
    {
        if (selectList is null || selectList.Length == 0)
            throw new InvalidOperationException("The query has no projected columns; there is nothing to write as CSV.");

        var columns = new CsvColumn[selectList.Length];
        for (var i = 0; i < selectList.Length; i++)
            columns[i] = BuildColumn(selectList[i], schema, mapTypedColumn);

        return new CsvPlan(columns);
    }

    /// <summary>
    /// Compiles a plan from an untyped column mapper, ignoring the reader's storage type. Kept as the
    /// seam for tests that drive the writer over an arbitrary accessor shape.
    /// </summary>
    /// <param name="selectList">The prepared projection, or <see langword="null"/> when none is available.</param>
    /// <param name="mapColumn">The provider's untyped column mapper.</param>
    /// <returns>The compiled column plan.</returns>
    public static CsvPlan Build(SelectExpression[]? selectList, Func<SelectExpression, Expression, Expression> mapColumn)
        => Build(selectList, schema: null, (column, record, _) => mapColumn(column, record));

    /// <summary>Validates a delimiter that cannot be represented by the RFC 4180 escaping rules.</summary>
    /// <param name="options">The caller's options.</param>
    public static void ValidateOptions(CsvStreamOptions options)
    {
        if (options.Delimiter is '"' or '\r' or '\n')
            throw new ArgumentException(
                "The CSV delimiter cannot be a double quote, CR or LF; these characters are reserved by the RFC 4180 escaping rules.",
                nameof(options));

        // A lone delimiter char is always an unpaired UTF-16 surrogate (a valid pair needs two chars),
        // and encoding it would silently produce U+FFFD, so reject it instead of corrupting the output.
        if (char.IsSurrogate(options.Delimiter))
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Delimiter,
                "The CSV delimiter cannot be a lone surrogate; it cannot be encoded as valid UTF-8 and would be written as U+FFFD.");

        // The NULL marker is written verbatim, so it must be a single unescaped field that cannot be
        // confused with an empty field; an empty marker cannot represent NULL at all.
        if (string.IsNullOrEmpty(options.NullMarker))
            throw new ArgumentException(
                "The CSV NULL marker cannot be null or empty; an empty marker cannot be distinguished from an empty field.",
                nameof(options));

        // A lone surrogate in the marker has no valid UTF-8 form: Encoding.UTF8 would silently write
        // U+FFFD. A valid surrogate pair is accepted.
        if (ContainsLoneSurrogate(options.NullMarker))
            throw new ArgumentException(
                "The CSV NULL marker cannot contain a lone surrogate; it cannot be encoded as valid UTF-8 and would be written as U+FFFD.",
                nameof(options));

        foreach (var value in options.NullMarker)
        {
            if (value is '"' or '\r' or '\n' || value == options.Delimiter)
                throw new ArgumentException(
                    "The CSV NULL marker cannot contain a double quote, CR, LF or the delimiter; it is written verbatim so it must not need escaping.",
                    nameof(options));
        }
    }

    private static bool ContainsLoneSurrogate(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsSurrogate(value[i]))
                continue;

            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Synchronously writes the header (when requested) and every row to <paramref name="destination"/>.</summary>
    public static void Write(DbDataReader reader, Stream destination, CsvPlan plan, CsvStreamOptions options, CancellationToken cancellationToken)
    {
        using var buffer = new CsvRowBuffer(new CsvDialect(options.Delimiter), CreatePolicy(options));

        if (options.IncludeHeader)
        {
            WriteHeader(plan, buffer);
            destination.Write(buffer.Written);
            buffer.Reset();
        }

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteRow(plan, reader, buffer);
            destination.Write(buffer.Written);
            buffer.Reset();
        }
    }

    /// <summary>Asynchronously writes the header (when requested) and every row to <paramref name="destination"/>.</summary>
    public static async Task WriteAsync(DbDataReader reader, Stream destination, CsvPlan plan, CsvStreamOptions options, CancellationToken cancellationToken)
    {
        using var buffer = new CsvRowBuffer(new CsvDialect(options.Delimiter), CreatePolicy(options));

        if (options.IncludeHeader)
        {
            WriteHeader(plan, buffer);
            await destination.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            buffer.Reset();
        }

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            WriteRow(plan, reader, buffer);
            await destination.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            buffer.Reset();
        }
    }

    private static CsvColumn BuildColumn(SelectExpression column, IDataRecord? schema, Func<SelectExpression, Expression, Type, Expression> mapTypedColumn)
    {
        var propertyType = column.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        var header = Header(column);
        var writeMethod = ResolveWriteMethod(propertyType, header);

        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var buffer = Expression.Parameter(typeof(CsvRowBuffer), "buffer");

        // The storage type is read from the open reader once per column, before the header is written.
        var storageType = schema is null ? typeof(object) : schema.GetFieldType(column.Index);
        var value = mapTypedColumn(column, record, storageType);

        EnsureBoxFree(column, value, header);

        if (value.Type != propertyType)
            value = Expression.Convert(value, propertyType);

        Expression body;
        if (underlyingType is not null)
        {
            // The mapper already folds DBNull to null; NULL is written as the configured marker, a present
            // value through the typed formatter. A per-value transform is handled inside the formatter.
            var local = Expression.Variable(propertyType, "value");
            body = Expression.Block(
                [local],
                Expression.Assign(local, value),
                Expression.IfThenElse(
                    Expression.Property(local, nameof(Nullable<int>.HasValue)),
                    Expression.Call(writeMethod, buffer, Expression.Property(local, nameof(Nullable<int>.Value))),
                    Expression.Call(buffer, WriteNullFieldMethod)));
        }
        else
        {
            body = Expression.Call(writeMethod, buffer, value);
        }

        var lambda = Expression.Lambda<Action<IDataRecord, CsvRowBuffer>>(body, record, buffer);
        return new CsvColumn(header, lambda.Compile());
    }

    private static string Header(SelectExpression column)
        => string.IsNullOrEmpty(column.PropertyName) ? $"Column{column.Index + 1}" : column.PropertyName;

    private static MethodInfo ResolveWriteMethod(Type propertyType, string header)
    {
        var readType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!WriteMethods.TryGetValue(readType, out var writeMethod))
            throw new NotSupportedException(
                $"The CSV terminal cannot format the column '{header}' of type '{propertyType}'. Supported types are bool, byte, short, int, uint, long, ulong, float, double, decimal, Guid, DateTime, DateTimeOffset, TimeSpan, string and byte[]; sbyte/ushort, arrays other than byte[], tuples, dictionaries and arbitrary object projections are not supported.");

        return writeMethod;
    }

    /// <summary>
    /// Rejects a column whose provider mapping boxes on every row. The mapped accessor is inspected
    /// directly: a value read through <see cref="IDataRecord.GetValue"/> (for example a
    /// <see cref="TimeSpan"/> kept in an integer column on a provider without a native duration type),
    /// a widening through <see cref="Convert.ChangeType(object, Type)"/> (the SQL Server buffered
    /// numeric mapping), or a converter applied through the object-based
    /// <see cref="IPropertyValueConverter.ConvertFromProvider"/> bridge all box. <c>byte[]</c> is read
    /// through the typed <c>GetFieldValue&lt;byte[]&gt;</c> instead and so is box-free (written as
    /// Base64); the SQL Server CSV path is box-free through
    /// <see cref="DataContext.MapTypedColumnExpression(SelectExpression, Expression, Type)"/>.
    /// </summary>
    /// <param name="column">The projected column whose accessor is inspected.</param>
    /// <param name="mappedValue">The mapped reader accessor expression.</param>
    /// <param name="header">The column's CSV header, used in the error message.</param>
    /// <param name="allowObjectRead">
    /// When <see langword="true"/>, an object-based <c>GetValue</c>/<c>ChangeType</c> read is tolerated
    /// because the provider's typed hook binds the column box-free after the reader is open (SQL Server
    /// numeric widening). The object-bridged converter is always rejected.
    /// </param>
    private static void EnsureBoxFree(SelectExpression column, Expression mappedValue, string header, bool allowObjectRead = false)
    {
        var detector = new ObjectReaderDetector();
        detector.Visit(mappedValue);

        if (detector.UsesConverterBridge)
            throw new NotSupportedException(
                $"The CSV terminal cannot write the column '{header}' of type '{column.PropertyType}' without boxing: its value converter is applied through the object-based IPropertyValueConverter.ConvertFromProvider(object) bridge. Derive the converter from ValueConverter<TModel, TProvider> so it is invoked through its typed method, or materialise the query and format the value yourself.");

        if (!allowObjectRead && (detector.UsesGetValue || detector.UsesObjectConversion))
            throw new NotSupportedException(
                $"The CSV terminal cannot write the column '{header}' of type '{column.PropertyType}' without boxing: its provider mapping reads the value through IDataRecord.GetValue(object)/Convert.ChangeType(object) instead of a typed getter. Project a column that is read through a typed accessor, or materialise the query and format the value yourself.");
    }

    /// <summary>Finds the box-per-row accessors in a mapped column expression.</summary>
    private sealed class ObjectReaderDetector : ExpressionVisitor
    {
        /// <summary>True when the expression reads a column through <see cref="IDataRecord.GetValue"/>.</summary>
        public bool UsesGetValue { get; private set; }

        /// <summary>True when the expression applies a converter through the object-based bridge.</summary>
        public bool UsesConverterBridge { get; private set; }

        /// <summary>True when the expression widens a value through <see cref="Convert.ChangeType(object, Type)"/>.</summary>
        public bool UsesObjectConversion { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method == ConverterBridgeMethod)
                UsesConverterBridge = true;
            else if (node.Method.Name == nameof(IDataRecord.GetValue)
                && node.Method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(int))
                UsesGetValue = true;
            else if (node.Method.Name == nameof(Convert.ChangeType)
                && node.Method.DeclaringType == typeof(Convert)
                && node.Method.GetParameters().Length == 2)
                UsesObjectConversion = true;

            return base.VisitMethodCall(node);
        }
    }

    internal static void WriteHeader(CsvPlan plan, CsvRowBuffer buffer)
    {
        for (var i = 0; i < plan.Columns.Length; i++)
        {
            if (i > 0)
                buffer.WriteDelimiter();

            buffer.WriteField(plan.Columns[i].Header);
        }

        buffer.WriteRowTerminator();
    }

    internal static void WriteRow(CsvPlan plan, IDataRecord record, CsvRowBuffer buffer)
    {
        for (var i = 0; i < plan.Columns.Length; i++)
        {
            if (i > 0)
                buffer.WriteDelimiter();

            plan.Columns[i].Write(record, buffer);
        }

        buffer.WriteRowTerminator();
    }

    private static CsvFieldPolicy CreatePolicy(CsvStreamOptions options)
        => new(options.NullMarker, options.ExcelMode, options.ValueTransform);

    private static MethodInfo GetWriteMethod(string name)
        => typeof(CsvValueFormatter).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
}
