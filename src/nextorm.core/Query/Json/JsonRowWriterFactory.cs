using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace NextORM.Core;

/// <summary>
/// Writes one result row as JSON using only typed reader accessors: the scalar value for a scalar
/// projection, or a flat object for a multi-column projection. It never materializes the projected
/// <c>TResult</c>.
/// </summary>
/// <param name="record">The current result-set row.</param>
/// <param name="writer">The destination JSON writer.</param>
internal delegate void JsonRowWriter(IDataRecord record, Utf8JsonWriter writer);

/// <summary>
/// Compiles a <see cref="JsonRowWriter"/> from a <see cref="JsonShapePlan"/>. Numeric columns are
/// read through the typed getter that matches the provider's <c>GetFieldType</c> and converted to the
/// projection's CLR type with a typed <see cref="Convert"/> overload; for provider field types that
/// have no typed <see cref="IDataRecord"/> getter (<c>sbyte</c>/<c>ushort</c>/<c>uint</c>/<c>ulong</c>,
/// e.g. MySQL/MariaDB) the boxed value is read with <c>GetValue</c> and converted with the typed
/// <c>Convert.ToXxx(object)</c> overload. Boolean, string, Guid and DateTime use their exact typed
/// getter; <c>byte[]</c> is read via <c>GetValue</c> and cast (accepted: it does not box
/// <c>TResult</c>). Unsupported runtime shapes throw instead of falling back to managed serialization.
/// </summary>
internal static class JsonRowWriterFactory
{
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo GetFieldTypeMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFieldType))!;
    private static readonly MethodInfo GetValueMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo GetByteMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte))!;
    private static readonly MethodInfo GetInt16MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16))!;
    private static readonly MethodInfo GetInt32MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32))!;
    private static readonly MethodInfo GetInt64MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))!;
    private static readonly MethodInfo GetFloatMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat))!;
    private static readonly MethodInfo GetDoubleMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble))!;
    private static readonly MethodInfo GetDecimalMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal))!;
    private static readonly MethodInfo GetBooleanMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetBoolean))!;
    private static readonly MethodInfo GetStringMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetString))!;
    private static readonly MethodInfo GetGuidMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetGuid))!;
    private static readonly MethodInfo GetDateTimeMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDateTime))!;

    private static readonly MethodInfo WriteStartObjectMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStartObject), Type.EmptyTypes)!;
    private static readonly MethodInfo WriteEndObjectMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteEndObject), Type.EmptyTypes)!;
    private static readonly MethodInfo WriteNullValueMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNullValue), Type.EmptyTypes)!;
    private static readonly MethodInfo WritePropertyNameMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WritePropertyName), [typeof(string)])!;
    private static readonly MethodInfo WriteBooleanValueMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteBooleanValue), [typeof(bool)])!;
    private static readonly MethodInfo WriteStringValueStringMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), [typeof(string)])!;
    private static readonly MethodInfo WriteStringValueGuidMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), [typeof(Guid)])!;
    private static readonly MethodInfo WriteStringValueDateTimeMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), [typeof(DateTime)])!;
    private static readonly MethodInfo WriteBase64MI = typeof(JsonRowWriterFactory).GetMethod(nameof(WriteBase64), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConstructorInfo NotSupportedExceptionCtor = typeof(NotSupportedException).GetConstructor([typeof(string)])!;

    // Every numeric provider field type a supported provider can report. sbyte/ushort/uint/ulong have
    // no typed IDataRecord getter (MySQL/MariaDB) and are read via GetValue in BuildNumericRead.
    private static readonly Type[] NumericFieldTypes =
    [
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
    ];

    // The ClickHouse driver reports every Decimal column as ClickHouse.Driver.Numerics.ClickHouseDecimal
    // (its UseBigDecimal setting defaults to true), which is not a CLR numeric. The driver's GetDecimal
    // narrows it to System.Decimal without boxing, so it is read exactly like a decimal field — the same
    // accessor the buffered row mapper uses. NextORM.Core cannot reference the provider assembly, so the
    // type is resolved by name; when ClickHouse is not in play the branch is simply omitted.
    private static readonly Type? ClickHouseDecimalFieldType =
        Type.GetType("ClickHouse.Driver.Numerics.ClickHouseDecimal, ClickHouse.Driver", throwOnError: false);

    /// <summary>Compiles the row writer for a frozen shape plan.</summary>
    /// <param name="plan">The validated shape.</param>
    /// <returns>A delegate that writes one row value per call.</returns>
    public static JsonRowWriter Build(JsonShapePlan plan)
    {
        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var writer = Expression.Parameter(typeof(Utf8JsonWriter), "writer");
        var body = new List<Expression>(plan.Columns.Length + 2);

        if (!plan.IsScalar)
            body.Add(Expression.Call(writer, WriteStartObjectMI));

        foreach (var column in plan.Columns)
        {
            var isNull = Expression.Call(record, IsDBNullMI, Expression.Constant(column.Ordinal));
            var value = ReadValue(record, column);

            Expression writePresent;
            Expression writeNull;
            if (plan.IsScalar)
            {
                writePresent = WriteValue(writer, column, value);
                writeNull = column.DefaultOnNull
                    ? WriteValue(writer, column, Expression.Default(column.ValueType))
                    : Expression.Call(writer, WriteNullValueMI);
            }
            else
            {
                var name = Expression.Constant(column.Name);
                writePresent = Expression.Block(
                    Expression.Call(writer, WritePropertyNameMI, name),
                    WriteValue(writer, column, value));
                if (column.DefaultOnNull)
                {
                    // A non-nullable *OrDefault scalar means SQL NULL is "no row": emit default(T), the
                    // same substitution RowMapperFactory performs (and IgnoreNull must not drop it).
                    writeNull = Expression.Block(
                        Expression.Call(writer, WritePropertyNameMI, name),
                        WriteValue(writer, column, Expression.Default(column.ValueType)));
                }
                else
                {
                    writeNull = plan.Options.IgnoreNull
                        ? Expression.Empty()
                        : Expression.Block(
                            Expression.Call(writer, WritePropertyNameMI, name),
                            Expression.Call(writer, WriteNullValueMI));
                }
            }

            body.Add(Expression.IfThenElse(isNull, writeNull, writePresent));
        }

        if (!plan.IsScalar)
            body.Add(Expression.Call(writer, WriteEndObjectMI));

        return Expression.Lambda<JsonRowWriter>(Expression.Block(body), record, writer).Compile();
    }

    private static Expression ReadValue(Expression record, in JsonShapeColumn column)
        => column.Kind switch
        {
            JsonWriteKind.Number => BuildNumericRead(record, column),
            JsonWriteKind.Boolean => Expression.Call(record, GetBooleanMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.String => Expression.Call(record, GetStringMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.Guid => Expression.Call(record, GetGuidMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.DateTime => Expression.Call(record, GetDateTimeMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.Base64 => Expression.Convert(
                Expression.Call(record, GetValueMI, Expression.Constant(column.Ordinal)),
                typeof(byte[])),
            _ => throw new NotSupportedException($"Unsupported JSON write kind {column.Kind}."),
        };

    private static Expression WriteValue(Expression writer, in JsonShapeColumn column, Expression value)
    {
        switch (column.Kind)
        {
            case JsonWriteKind.Number:
                // Utf8JsonWriter has no byte/short overload; widen to int (an implicit widening, no box).
                var writeType = column.ValueType == typeof(byte) || column.ValueType == typeof(short)
                    ? typeof(int)
                    : column.ValueType;
                if (writeType != column.ValueType)
                    value = Expression.Convert(value, writeType);
                return Expression.Call(writer, NumberMethod(writeType), value);

            case JsonWriteKind.Boolean:
                return Expression.Call(writer, WriteBooleanValueMI, value);

            case JsonWriteKind.String:
                return Expression.Call(writer, WriteStringValueStringMI, value);

            case JsonWriteKind.Guid:
                return Expression.Call(writer, WriteStringValueGuidMI, value);

            case JsonWriteKind.DateTime:
                return Expression.Call(writer, WriteStringValueDateTimeMI, value);

            case JsonWriteKind.Base64:
                return Expression.Call(WriteBase64MI, writer, value);

            default:
                throw new NotSupportedException($"Unsupported JSON write kind {column.Kind}.");
        }
    }

    /// <summary>
    /// Builds a numeric read that inspects the provider field type at runtime, reads it with the
    /// matching typed getter and converts it with the typed <see cref="Convert"/> overload for the
    /// projected CLR type. This is the boxing-free equivalent of SQL Server's
    /// <c>GetValue</c>+<c>Convert.ChangeType</c> path and works for providers that return a narrower or
    /// wider numeric field type than the projection.
    /// </summary>
    private static Expression BuildNumericRead(Expression record, in JsonShapeColumn column)
    {
        var fieldType = Expression.Variable(typeof(Type), "fieldType");
        var assign = Expression.Assign(fieldType, Expression.Call(record, GetFieldTypeMI, Expression.Constant(column.Ordinal)));

        Expression result = Expression.Throw(
            Expression.New(
                NotSupportedExceptionCtor,
                Expression.Constant(
                    $"JSON streaming cannot read the numeric column at ordinal {column.Ordinal} (target {column.ValueType.Name}): its provider field type is not one of sbyte, byte, short, ushort, int, uint, long, ulong, float, double, decimal or ClickHouseDecimal.")),
            column.ValueType);

        // ClickHouse's arbitrary-precision decimal representation, read through the driver's GetDecimal.
        // Wrapped around the whitelist conditions so it is tried first when the provider reports it.
        if (ClickHouseDecimalFieldType is not null)
        {
            var read = Expression.Call(record, GetDecimalMI, Expression.Constant(column.Ordinal));
            if (column.ValueType != typeof(decimal))
                read = Expression.Call(ConvertMethod(column.ValueType, typeof(decimal)), read);

            result = Expression.Condition(
                Expression.ReferenceEqual(fieldType, Expression.Constant(ClickHouseDecimalFieldType, typeof(Type))),
                read,
                result);
        }

        for (var i = NumericFieldTypes.Length - 1; i >= 0; i--)
        {
            var fieldTypeConstant = NumericFieldTypes[i];
            var (read, convertSource) = ReadNumericField(record, column.Ordinal, fieldTypeConstant);
            if (convertSource != column.ValueType)
                read = Expression.Call(ConvertMethod(column.ValueType, convertSource), read);

            result = Expression.Condition(
                Expression.ReferenceEqual(fieldType, Expression.Constant(fieldTypeConstant, typeof(Type))),
                read,
                result);
        }

        return Expression.Block([fieldType], assign, result);
    }

    // Reads one typed numeric field. Types with an IDataRecord getter are read directly; the narrow
    // unsigned/signed types MySQL/MariaDB report (sbyte/ushort/uint/ulong) have no getter and are read
    // as their boxed value, then converted by the typed Convert.ToXxx(object) overload. The box is
    // created by the provider, not by our code, and TResult is never boxed.
    private static (Expression Read, Type ConvertSource) ReadNumericField(Expression record, int ordinal, Type fieldType)
    {
        var getter = FieldGetter(fieldType);
        if (getter is null)
            return (Expression.Call(record, GetValueMI, Expression.Constant(ordinal)), typeof(object));

        return (Expression.Call(record, getter, Expression.Constant(ordinal)), fieldType);
    }

    private static MethodInfo? FieldGetter(Type fieldType)
    {
        if (fieldType == typeof(byte)) return GetByteMI;
        if (fieldType == typeof(short)) return GetInt16MI;
        if (fieldType == typeof(int)) return GetInt32MI;
        if (fieldType == typeof(long)) return GetInt64MI;
        if (fieldType == typeof(float)) return GetFloatMI;
        if (fieldType == typeof(double)) return GetDoubleMI;
        if (fieldType == typeof(decimal)) return GetDecimalMI;

        // sbyte/ushort/uint/ulong: no typed getter on IDataRecord; the caller reads via GetValue.
        return null;
    }

    private static MethodInfo NumberMethod(Type type)
    {
        if (type == typeof(int)) return typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), [typeof(int)])!;
        if (type == typeof(long)) return typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), [typeof(long)])!;
        if (type == typeof(float)) return typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), [typeof(float)])!;
        if (type == typeof(double)) return typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), [typeof(double)])!;
        if (type == typeof(decimal)) return typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), [typeof(decimal)])!;

        throw new NotSupportedException($"No JSON number writer for CLR type {type}.");
    }

    private static MethodInfo ConvertMethod(Type target, Type field)
    {
        var name = target switch
        {
            _ when target == typeof(byte) => nameof(Convert.ToByte),
            _ when target == typeof(short) => nameof(Convert.ToInt16),
            _ when target == typeof(int) => nameof(Convert.ToInt32),
            _ when target == typeof(long) => nameof(Convert.ToInt64),
            _ when target == typeof(float) => nameof(Convert.ToSingle),
            _ when target == typeof(double) => nameof(Convert.ToDouble),
            _ when target == typeof(decimal) => nameof(Convert.ToDecimal),
            _ => throw new NotSupportedException($"No typed Convert overload for JSON numeric target {target}."),
        };

        return typeof(Convert).GetMethod(name, [field])!;
    }

    private static void WriteBase64(Utf8JsonWriter writer, byte[] value) => writer.WriteBase64StringValue(value);
}
