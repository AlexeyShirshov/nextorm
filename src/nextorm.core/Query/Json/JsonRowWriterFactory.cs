using System.Data;
using System.Data.Common;
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
    private static readonly MethodInfo GetFieldValueByteArrayMI = typeof(DbDataReader)
        .GetMethod(nameof(DbDataReader.GetFieldValue))!.MakeGenericMethod(typeof(byte[]));

    private static readonly MethodInfo WriteStartObjectMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStartObject), Type.EmptyTypes)!;
    private static readonly MethodInfo WriteEndObjectMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteEndObject), Type.EmptyTypes)!;
    private static readonly MethodInfo WriteNullValueMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNullValue), Type.EmptyTypes)!;
    private static readonly MethodInfo WritePropertyNameMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WritePropertyName), [typeof(string)])!;
    private static readonly MethodInfo WriteBooleanValueMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteBooleanValue), [typeof(bool)])!;
    private static readonly MethodInfo WriteStringValueStringMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), [typeof(string)])!;
    private static readonly MethodInfo WriteStringValueGuidMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), [typeof(Guid)])!;
    private static readonly MethodInfo WriteStringValueDateTimeMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), [typeof(DateTime)])!;
    private static readonly MethodInfo WriteBase64MI = typeof(JsonRowWriterFactory).GetMethod(nameof(WriteBase64), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo WriteStartArrayMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStartArray), Type.EmptyTypes)!;
    private static readonly MethodInfo WriteEndArrayMI = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteEndArray), Type.EmptyTypes)!;
    private static readonly PropertyInfo ArrayLengthPI = typeof(Array).GetProperty(nameof(Array.Length))!;
    private static readonly MethodInfo ArrayGetValueMI = typeof(Array).GetMethod(nameof(Array.GetValue), [typeof(int)])!;
    private static readonly MethodInfo ToArrayMI = typeof(JsonRowWriterFactory).GetMethod(nameof(ToArray), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConstructorInfo NotSupportedExceptionCtor = typeof(NotSupportedException).GetConstructor([typeof(string)])!;

    // Matches System.Text.Json's default maximum depth; a deeper recursive shape is rejected while the
    // writer is compiled (during preparation), never with a partial unsupported-shape fallback.
    private const int MaxShapeDepth = 64;

    // Writes one recursively-typed array value (the whole-column runtime value or a jagged element).
    private delegate void JsonShapeArrayWriter(object? value, Utf8JsonWriter writer);

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
        // A captured recursive shape (nested construction, array member or expanded entity item) carries
        // its own descriptor, scoped name validation and per-object presence rule; compile the recursive
        // writer from it. The flat phase-1 column plan is not built for such a shape (see JsonShapePlan).
        if (plan.Shape is not null)
            return BuildRecursive(plan.Shape, plan.Options);

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

    /// <summary>
    /// Compiles the recursive row writer for a captured <see cref="JsonShapeNode"/> descriptor. Scalar
    /// leaves reuse the phase-1 typed accessors through their explicit prepared ordinals; object nodes
    /// write <c>{...}</c> with per-scope validated names; array nodes recurse over the declared
    /// rank-one array type (<c>byte[]</c> Base64 is tested before general array handling). Presence is
    /// decided from the raw <c>IsDBNull</c> inputs before any <c>DefaultOnNull</c> substitution.
    /// </summary>
    private static JsonRowWriter BuildRecursive(JsonShapeNode shape, JsonStreamOptions options)
    {
        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var writer = Expression.Parameter(typeof(Utf8JsonWriter), "writer");
        var body = BuildRootValue(shape, options, record, writer, depth: 0);
        return Expression.Lambda<JsonRowWriter>(body, record, writer).Compile();
    }

    private static Expression BuildRootValue(JsonShapeNode node, JsonStreamOptions options, Expression record, Expression writer, int depth)
    {
        EnsureDepth(depth, node.Name);

        return node.Kind switch
        {
            JsonShapeNodeKind.Scalar => BuildScalarValue(node, record, writer),
            JsonShapeNodeKind.Array => BuildRootArrayValue(node, record, writer, depth),
            JsonShapeNodeKind.Object => BuildRootObjectValue(node, options, record, writer, depth),
            _ => throw new NotSupportedException($"Unsupported JSON shape node kind {node.Kind}."),
        };
    }

    private static Expression BuildScalarValue(JsonShapeNode node, Expression record, Expression writer)
    {
        if (node.Binding is not { } binding)
            throw new NotSupportedException($"The JSON scalar leaf '{node.Name}' has no prepared ordinal binding.");

        var (valueType, kind) = JsonShapePlan.Classify(node.DeclaredType, node.Name);
        var column = new JsonShapeColumn(binding.Ordinal, node.Name ?? string.Empty, valueType, kind, binding.Nullable, binding.DefaultOnNull);
        var isNull = Expression.Call(record, IsDBNullMI, Expression.Constant(binding.Ordinal));
        var present = WriteValue(writer, column, ReadValue(record, column));
        var writeNull = binding.DefaultOnNull
            ? WriteValue(writer, column, Expression.Default(valueType))
            : Expression.Call(writer, WriteNullValueMI);
        return Expression.IfThenElse(isNull, writeNull, present);
    }

    private static Expression BuildRootArrayValue(JsonShapeNode node, Expression record, Expression writer, int depth)
    {
        if (node.Binding is not { } binding)
            throw new NotSupportedException($"The JSON array '{node.Name}' has no prepared ordinal binding.");

        var arrayWriter = BuildArrayWriter(node.DeclaredType, node.Name, depth + 1);
        var isNull = Expression.Call(record, IsDBNullMI, Expression.Constant(binding.Ordinal));
        var present = Expression.Invoke(
            Expression.Constant(arrayWriter),
            Expression.Call(record, GetValueMI, Expression.Constant(binding.Ordinal)),
            writer);
        return Expression.IfThenElse(isNull, Expression.Call(writer, WriteNullValueMI), present);
    }

    private static Expression BuildRootObjectValue(JsonShapeNode node, JsonStreamOptions options, Expression record, Expression writer, int depth)
    {
        var body = BuildObjectBody(node, options, record, writer, depth);

        if (node.Presence.Kind == JsonShapePresenceKind.Always)
            return body;

        var anyNotNull = BuildAnyColumnNotNull(node.Presence, record);
        return Expression.IfThenElse(anyNotNull, body, Expression.Call(writer, WriteNullValueMI));
    }

    private static Expression BuildObjectBody(JsonShapeNode node, JsonStreamOptions options, Expression record, Expression writer, int depth)
    {
        EnsureDepth(depth, node.Name);

        var members = new List<Expression>(node.Members.Length + 2)
        {
            Expression.Call(writer, WriteStartObjectMI),
        };
        members.AddRange(BuildMemberWriters(node, options, record, writer, depth + 1));
        members.Add(Expression.Call(writer, WriteEndObjectMI));
        return Expression.Block(members);
    }

    private static List<Expression> BuildMemberWriters(JsonShapeNode node, JsonStreamOptions options, Expression record, Expression writer, int depth)
    {
        // Scoped-name validation: unique effective names within this object only; the same name in a
        // sibling/different scope is accepted (replaces the flat path's single global name set).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var expressions = new List<Expression>(node.Members.Length);

        foreach (var member in node.Members)
        {
            var name = ResolveMemberName(member.Name, options);
            if (!seen.Add(name))
                throw new NotSupportedException(
                    $"Duplicate JSON property name '{name}' within one object scope; JSON object members must be unique per object.");

            expressions.Add(BuildMember(member, name, options, record, writer, depth));
        }

        return expressions;
    }

    private static Expression BuildMember(JsonShapeNode node, string name, JsonStreamOptions options, Expression record, Expression writer, int depth)
        => node.Kind switch
        {
            JsonShapeNodeKind.Scalar => BuildScalarMember(node, name, options, record, writer),
            JsonShapeNodeKind.Array => BuildArrayMember(node, name, options, record, writer, depth),
            JsonShapeNodeKind.Object => BuildObjectMember(node, name, options, record, writer, depth),
            _ => throw new NotSupportedException($"Unsupported JSON shape node kind {node.Kind}."),
        };

    private static Expression BuildScalarMember(JsonShapeNode node, string name, JsonStreamOptions options, Expression record, Expression writer)
    {
        if (node.Binding is not { } binding)
            throw new NotSupportedException($"The JSON scalar member '{node.Name}' has no prepared ordinal binding.");

        var (valueType, kind) = JsonShapePlan.Classify(node.DeclaredType, node.Name);
        var column = new JsonShapeColumn(binding.Ordinal, name, valueType, kind, binding.Nullable, binding.DefaultOnNull);
        var isNull = Expression.Call(record, IsDBNullMI, Expression.Constant(binding.Ordinal));
        var present = Expression.Block(
            Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
            WriteValue(writer, column, ReadValue(record, column)));

        Expression writeNull;
        if (binding.DefaultOnNull)
        {
            writeNull = Expression.Block(
                Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
                WriteValue(writer, column, Expression.Default(valueType)));
        }
        else
        {
            writeNull = options.IgnoreNull
                ? Expression.Empty()
                : Expression.Block(
                    Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
                    Expression.Call(writer, WriteNullValueMI));
        }

        return Expression.IfThenElse(isNull, writeNull, present);
    }

    private static Expression BuildArrayMember(JsonShapeNode node, string name, JsonStreamOptions options, Expression record, Expression writer, int depth)
    {
        if (node.Binding is not { } binding)
            throw new NotSupportedException($"The JSON array member '{node.Name}' has no prepared ordinal binding.");

        var arrayWriter = BuildArrayWriter(node.DeclaredType, node.Name, depth + 1);
        var isNull = Expression.Call(record, IsDBNullMI, Expression.Constant(binding.Ordinal));
        var present = Expression.Block(
            Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
            Expression.Invoke(
                Expression.Constant(arrayWriter),
                Expression.Call(record, GetValueMI, Expression.Constant(binding.Ordinal)),
                writer));
        var writeNull = options.IgnoreNull
            ? (Expression)Expression.Empty()
            : Expression.Block(
                Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
                Expression.Call(writer, WriteNullValueMI));
        return Expression.IfThenElse(isNull, writeNull, present);
    }

    private static Expression BuildObjectMember(JsonShapeNode node, string name, JsonStreamOptions options, Expression record, Expression writer, int depth)
    {
        var body = BuildObjectBody(node, options, record, writer, depth);

        if (node.Presence.Kind == JsonShapePresenceKind.Always)
        {
            return Expression.Block(
                Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
                body);
        }

        var anyNotNull = BuildAnyColumnNotNull(node.Presence, record);
        var present = Expression.Block(
            Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
            body);
        var writeNull = options.IgnoreNull
            ? (Expression)Expression.Empty()
            : Expression.Block(
                Expression.Call(writer, WritePropertyNameMI, Expression.Constant(name)),
                Expression.Call(writer, WriteNullValueMI));
        return Expression.IfThenElse(anyNotNull, present, writeNull);
    }

    private static Expression BuildAnyColumnNotNull(JsonShapePresence presence, Expression record)
    {
        if (presence.Kind != JsonShapePresenceKind.AnyColumnNotNull)
            throw new NotSupportedException($"Unexpected JSON presence kind {presence.Kind} for an any-column predicate.");

        if (presence.Ordinals.Length == 0)
            throw new NotSupportedException("A JSON entity slot has no mapped columns and cannot decide presence.");

        Expression? anyNotNull = null;
        foreach (var ordinal in presence.Ordinals)
        {
            var notNull = Expression.Not(Expression.Call(record, IsDBNullMI, Expression.Constant(ordinal)));
            anyNotNull = anyNotNull is null ? notNull : Expression.OrElse(anyNotNull, notNull);
        }

        return anyNotNull!;
    }

    private static JsonShapeArrayWriter BuildArrayWriter(Type arrayType, string? name, int depth)
    {
        EnsureDepth(depth, name);
        ValidateArrayType(arrayType, name);

        var elementType = arrayType.GetElementType()!;
        var elementValueType = Nullable.GetUnderlyingType(elementType) ?? elementType;

        var value = Expression.Parameter(typeof(object), "value");
        var writer = Expression.Parameter(typeof(Utf8JsonWriter), "writer");
        var array = Expression.Variable(typeof(Array), "array");
        var index = Expression.Variable(typeof(int), "index");
        var item = Expression.Variable(typeof(object), "item");
        var breakLabel = Expression.Label("break");

        var body = Expression.Block(
            new[] { array, index, item },
            Expression.Assign(array, Expression.Call(ToArrayMI, value)),
            Expression.Call(writer, WriteStartArrayMI),
            Expression.Assign(index, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(index, Expression.Property(array, ArrayLengthPI)),
                    Expression.Block(
                        Expression.Assign(item, Expression.Call(array, ArrayGetValueMI, index)),
                        BuildElementWrite(elementType, elementValueType, name, item, writer, depth),
                        Expression.PostIncrementAssign(index)),
                    Expression.Break(breakLabel)),
                breakLabel),
            Expression.Call(writer, WriteEndArrayMI));

        return Expression.Lambda<JsonShapeArrayWriter>(body, value, writer).Compile();
    }

    private static Expression BuildElementWrite(Type elementType, Type elementValueType, string? name, Expression item, Expression writer, int depth)
    {
        Expression writeItem;

        // byte[] Base64 takes precedence over general array handling, including as a jagged element.
        if (elementValueType != typeof(byte[]) && elementValueType.IsArray)
        {
            var nested = BuildArrayWriter(elementValueType, name, depth + 1);
            writeItem = Expression.Invoke(Expression.Constant(nested), item, writer);
        }
        else
        {
            var (valueType, kind) = JsonShapePlan.Classify(elementType, name);
            var column = new JsonShapeColumn(0, name ?? string.Empty, valueType, kind, nullable: true, defaultOnNull: false);
            writeItem = WriteValue(writer, column, Expression.Convert(item, valueType));
        }

        // A provider-native array element may surface a SQL NULL as CLR null or as boxed DBNull.Value
        // depending on the driver; both mean "no value" and must emit null rather than fail the
        // element conversion.
        var isNull = Expression.OrElse(
            Expression.ReferenceEqual(item, Expression.Constant(null, typeof(object))),
            Expression.ReferenceEqual(item, Expression.Constant(DBNull.Value, typeof(object))));
        return Expression.IfThenElse(
            isNull,
            Expression.Call(writer, WriteNullValueMI),
            writeItem);
    }

    private static string ResolveMemberName(string? rawName, JsonStreamOptions options)
    {
        if (string.IsNullOrEmpty(rawName))
            throw new NotSupportedException("A nested JSON object member has no name; only the root object is unnamed.");

        if (options.PropertyNamingPolicy is null)
            return rawName;

        var name = options.PropertyNamingPolicy.ConvertName(rawName);
        if (name is null)
            throw new NotSupportedException(
                $"The property naming policy returned null for member '{rawName}'; JSON object member names cannot be null.");
        return name;
    }

    private static void ValidateArrayType(Type arrayType, string? name)
    {
        if (!arrayType.IsArray || arrayType.GetArrayRank() != 1)
            throw new NotSupportedException(
                $"Array '{name}' has declared type {arrayType} which is not a supported rank-one JSON array; multidimensional and non-vector arrays are not supported by JSON streaming.");
    }

    private static void EnsureDepth(int depth, string? name)
    {
        if (depth > MaxShapeDepth)
            throw new NotSupportedException(
                $"The JSON projection shape around '{name}' exceeds the maximum supported nesting depth of {MaxShapeDepth}.");
    }

    private static Array ToArray(object? value) => value as Array
        ?? throw new NotSupportedException(
            $"A JSON array column returned a provider value of type {value?.GetType().ToString() ?? "null"} which is not a rank-one array; only provider-native arrays are supported.");

    private static Expression ReadValue(Expression record, in JsonShapeColumn column)
        => column.Kind switch
        {
            JsonWriteKind.Number => BuildNumericRead(record, column),
            JsonWriteKind.Boolean => Expression.Call(record, GetBooleanMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.String => Expression.Call(record, GetStringMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.Guid => Expression.Call(record, GetGuidMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.DateTime => Expression.Call(record, GetDateTimeMI, Expression.Constant(column.Ordinal)),
            JsonWriteKind.Base64 => BuildByteArrayRead(record, column),
            _ => throw new NotSupportedException($"Unsupported JSON write kind {column.Kind}."),
        };

    private static Expression BuildByteArrayRead(Expression record, in JsonShapeColumn column)
    {
        // Prefer the driver's typed GetFieldValue<byte[]> (the ordinary materializer's accessor): a
        // provider may expose a byte[] column as a driver value the boxed GetValue cannot cast directly
        // (for example ClickHouse renders FixedString, and a bare IDataRecord used by the direct-writer
        // tests has no typed accessor), so fall back to the boxed GetValue cast only when the record is
        // not a DbDataReader.
        var byteReader = Expression.Variable(typeof(DbDataReader), "byteReader");
        var viaValue = Expression.Convert(
            Expression.Call(record, GetValueMI, Expression.Constant(column.Ordinal)),
            typeof(byte[]));
        var viaField = Expression.Call(byteReader, GetFieldValueByteArrayMI, Expression.Constant(column.Ordinal));
        return Expression.Block(
            [byteReader],
            Expression.Assign(byteReader, Expression.TypeAs(record, typeof(DbDataReader))),
            Expression.Condition(
                Expression.ReferenceEqual(byteReader, Expression.Constant(null, typeof(DbDataReader))),
                viaValue,
                viaField));
    }

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
