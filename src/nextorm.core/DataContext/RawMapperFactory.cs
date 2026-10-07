using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Builds (and caches) the row mapper for a raw command's result set. Three shapes are supported,
/// matching the scope of <c>ExecuteRaw</c>:
/// <list type="bullet">
/// <item><description>a scalar result type (primitive, string, decimal,
/// <see cref="DateTime"/>/<see cref="DateTimeOffset"/>/<see cref="TimeSpan"/>,
/// <see cref="DateOnly"/>/<see cref="TimeOnly"/>, <see cref="Guid"/>, <c>byte[]</c>, enum or a nullable
/// of these) reads column 0 of the current result set;</description></item>
/// <item><description>a single record column (a PostgreSQL anonymous <c>ROW(...)</c> materialized into
/// the matching <see cref="Tuple"/> family, or a caller-registered named composite) is read from the
/// driver's record value; whole-record NULL maps to CLR <see langword="null"/> and per-field
/// NULL/conversion follows the R194-NULL/R194-ERROR contract (see <see cref="RawRowValue"/>);</description></item>
/// <item><description>a mapped entity result type is materialized by <b>column name</b>: the entity's
/// projection is reordered/filtered to the reader's columns (case-insensitive), properties absent from
/// the reader are skipped, and unmatched reader columns are ignored. If no column matches, the read fails
/// with a clear <see cref="InvalidOperationException"/>.</description></item>
/// </list>
/// Name matching is done in two passes: first against the mapped column name (naming convention applied
/// for auto names), then against the CLR property name for properties still unmatched. Each reader
/// column binds at most one property, and each property/projection slot binds at most once. A duplicated
/// reader column name binds its first occurrence and is ignored afterwards.
/// </summary>
/// <remarks>
/// An entity result type must be a <b>concrete class with a public parameterless constructor</b>: mapping
/// binds by column name through member-init, and a constructor with parameters would bind the projected
/// columns positionally (<c>EnsureNameMappable</c> rejects it).
/// <para>
/// The per-type raw select-list cache (<c>RawSelectListCache</c>) and the mapper key
/// (<see cref="RowMapperFactory.GetOrBuildRaw{TResult}"/>, keyed by provider type, result type, column
/// shape and <see cref="INamingConvention"/> type) assume the registered entity metadata is
/// <b>immutable</b> and the naming convention is <b>stateless</b> — they are keyed by type, not
/// snapshotted. Do not mutate a mapping or use a stateful convention while raw mappers are cached.
/// </para>
/// </remarks>
internal static class RawMapperFactory
{
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo GetValueMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo ConvertMI = typeof(RawValueConverter).GetMethod(nameof(RawValueConverter.Convert))!;
    private static readonly MethodInfo ConvertFieldMI = typeof(RawRowValue).GetMethod(nameof(RawRowValue.ConvertField))!;
    private static readonly MethodInfo ValidateRecordMI = typeof(RawRowValue).GetMethod(nameof(RawRowValue.ValidateRecord))!;
    private static readonly MethodInfo ReadNamedCompositeMI = typeof(RawRowValue).GetMethod(nameof(RawRowValue.ReadNamedComposite))!;

    // Raw projections start from the entity metadata, not from the shared DataContextCache.SelectListCache:
    // that cache is reserved for LINQ/plan-cached projections, whose entries carry a PlanHashCode. Writing
    // a raw entry there would change a later LINQ query's ColumnsPlanHash (the entry would contribute zero),
    // so raw keeps its own Type-keyed copy. Reading the shared cache is intentionally not done either, so
    // the raw and LINQ shapes can never influence each other.
    private static readonly ConcurrentDictionary<Type, SelectExpression[]> RawSelectListCache = new();

    /// <summary>Clears the raw per-type projection cache; called by <see cref="DataContextCache.Clear"/>.</summary>
    internal static void Clear() => RawSelectListCache.Clear();

    /// <summary>
    /// Returns the compiled mapper for the current result set of <paramref name="reader"/>. Mappers are
    /// cached by result shape (reader column names + type), never by SQL text, so arbitrary raw statements
    /// do not grow the cache. See <see cref="RowMapperFactory.GetOrBuildRaw{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The scalar or mapped-entity result type.</typeparam>
    /// <param name="context">The context supplying the provider type, naming convention and column mapping.</param>
    /// <param name="reader">The reader positioned on the result set whose columns drive the shape.</param>
    /// <returns>A compiled mapper from a data record to <typeparamref name="T"/>.</returns>
    internal static Func<IDataRecord, T> GetOrBuild<T>(DataContext context, DbDataReader reader)
    {
        var resultType = typeof(T);
        var providerType = context.GetType();

        if (IsScalarType(resultType))
        {
            return RowMapperFactory.GetOrBuildRaw<T>(
                providerType,
                resultType,
                oneColumn: true,
                columns: string.Empty,
                namingConventionType: null,
                buildSelectList: () => [new SelectExpression(resultType) { Index = 0 }],
                mapColumn: MapScalarColumn);
        }

        if (context.RawRowColumnsSupported
            && TryGetRawRowShape(context, reader, resultType, out var rawRowKind, out var guardReason))
        {
            if (guardReason is not null)
                throw UnsupportedRawRow(guardReason);

            return rawRowKind switch
            {
                RawRowKind.AnonymousTuple => RowMapperFactory.GetOrBuildRaw<T>(
                    providerType,
                    resultType,
                    oneColumn: true,
                    columns: RawRowColumnsKey,
                    namingConventionType: null,
                    recordKind: rawRowKind,
                    recordSignature: BuildRecordSignature(resultType),
                    buildSelectList: () => [new SelectExpression(resultType) { Index = 0 }],
                    mapColumn: MapAnonymousTupleColumn),
                RawRowKind.NamedComposite => RowMapperFactory.GetOrBuildRaw<T>(
                    providerType,
                    resultType,
                    oneColumn: true,
                    columns: RawRowColumnsKey,
                    namingConventionType: null,
                    recordKind: rawRowKind,
                    recordSignature: null,
                    buildSelectList: () => [new SelectExpression(resultType) { Index = 0 }],
                    mapColumn: MapNamedCompositeColumn),
                _ => throw UnsupportedRawRow("unknown raw-row shape"),
            };
        }

        var entityMeta = DataContextCache.Metadata.TryGetValue(resultType, out var existing)
            ? existing
            : DataContextExtensions.ResolveMetadata<T>(context, null);

        if (entityMeta.Properties.Count == 0)
            throw Unsupported(resultType);

        return RowMapperFactory.GetOrBuildRaw<T>(
            providerType,
            resultType,
            oneColumn: false,
            columns: BuildColumnsKey(reader),
            namingConventionType: context.NamingConvention?.GetType(),
            buildSelectList: () => BuildEntitySelectList(context, reader, resultType, entityMeta),
            mapColumn: context.MapColumnExpression);
    }

    // The single-record-column key discriminator; the record shape itself is carried by ResultType
    // (tuple) / RecordSignature, so a record mapper can never alias an ordinary raw mapper.
    private const string RawRowColumnsKey = "\u0001rawrow";

    /// <summary>
    /// Detects a single-record-column raw result (a PostgreSQL anonymous <c>ROW(...)</c> or a
    /// caller-registered named composite) and reports the branch to take. Returns <see langword="true"/>
    /// when the caller must act: either <paramref name="kind"/> is a supported record kind, or
    /// <paramref name="guardReason"/> names an unsupported shape that must surface as the R194-ERROR
    /// <see cref="NotSupportedException"/>. Returns <see langword="false"/> for every ordinary
    /// scalar/entity result set, whose behaviour is unchanged.
    /// </summary>
    private static bool TryGetRawRowShape(DataContext context, DbDataReader reader, Type resultType, out RawRowKind kind, out string? guardReason)
    {
        kind = RawRowKind.None;
        guardReason = null;

        if (reader.FieldCount == 0)
            return false;

        // Only PostgreSQL surfaces an anonymous record column under the data type name "record"; a
        // ClickHouse Tuple(...) column keeps its own data type name and is left to the existing route.
        var recordColumns = 0;
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (IsAnonymousRecordColumn(reader, i))
                recordColumns++;
        }

        if (recordColumns > 0 && (recordColumns != 1 || reader.FieldCount != 1))
        {
            guardReason = "multiple record columns or a record column mixed with scalar columns are not supported";
            return true;
        }

        var isTuple = TypeFacts.IsTupleType(resultType);
        var isValueTuple = TypeFacts.IsValueTupleType(resultType);
        var isTupleBeyondSeven = IsTupleBeyondSeven(resultType);
        // A driver reports a registered struct composite's CLR type (not its Nullable<> wrapper), so a
        // declared Read<PtStruct?> must still match fieldType == PtStruct. Without this the named-composite
        // branch is unreachable for a nullable struct composite (MapNamedCompositeColumn already unwraps
        // Nullable when building the accessor).
        var effectiveResultType = Nullable.GetUnderlyingType(resultType) ?? resultType;

        if (reader.FieldCount == 1 && recordColumns == 1)
        {
            if (isTuple)
            {
                kind = RawRowKind.AnonymousTuple;
                return true;
            }

            if (isValueTuple)
            {
                guardReason = "ValueTuple result types are not supported";
                return true;
            }

            if (isTupleBeyondSeven)
            {
                guardReason = "a System.Tuple with more than 7 elements (System.Tuple<...,TRest>) is not supported";
                return true;
            }

            if (!IsScalarType(resultType))
            {
                guardReason = "an anonymous ROW must be read as System.Tuple<...>";
                return true;
            }

            return false;
        }

        if (reader.FieldCount != 1)
        {
            // A named composite column is not reported under the anonymous "record" data type, so it is
            // not counted above. A composite mixed with scalar columns (S12) must guard, not fall through
            // to the entity path with a misleading "no mapped property" error. Matching the driver field
            // type against the declared result type keeps ordinary multi-column entity reads (whose
            // columns are scalar or provider structs such as ranges) from being affected.
            if (!IsScalarType(resultType))
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (TryGetFieldType(reader, i, out var columnType) && columnType == effectiveResultType)
                    {
                        guardReason = "multiple record columns or a record column mixed with scalar columns are not supported";
                        return true;
                    }
                }
            }

            return false;
        }

        // A single non-record column: a caller-registered named composite resolves its CLR field type
        // through the driver, so the declared result type can be matched exactly — but only when the
        // provider's authoritative predicate reports a genuine composite. A CLR-type match alone is not
        // enough: array covariance lets a text[]/int[] column satisfy a declared array type, and a
        // provider struct/dictionary can share the declared type, so those would be routed to the typed
        // composite accessor and silently accepted instead of taking the ordinary scalar/entity path.
        if (TryGetFieldType(reader, 0, out var fieldType))
        {
            if (fieldType == effectiveResultType && IsGenuineNamedComposite(context, reader, 0, effectiveResultType))
            {
                kind = RawRowKind.NamedComposite;
                return true;
            }

            if ((isTuple || isValueTuple || isTupleBeyondSeven) && !IsScalarType(fieldType) && fieldType != typeof(object[]))
            {
                guardReason = "a named composite type mapped to System.Tuple is not supported";
                return true;
            }

            return false;
        }

        // The driver cannot resolve the single column type. Only the provider's authoritative predicate
        // may identify it as an unregistered named composite (S6); the metadata-probe exception shape and
        // a dotted data type name are not classifiers, so an unresolvable non-composite keeps the ordinary
        // entity path and its "None of the result-set columns" error.
        if (!IsScalarType(resultType) && context.IsGenuineCompositeRawRowColumn(reader, 0))
        {
            guardReason = "a named composite type must be registered with MapComposite<T> before it can be read";
            return true;
        }

        return false;
    }

    private static bool IsAnonymousRecordColumn(DbDataReader reader, int index)
    {
        try
        {
            return string.Equals(reader.GetDataTypeName(index), "record", StringComparison.Ordinal);
        }
        catch (Exception ex) when (IsMetadataRejection(ex))
        {
            // A provider whose reader rejects the metadata probe is not a record-column producer. A real
            // driver/connection error (DbException), cancellation or a disposed reader is not a metadata
            // verdict and is not swallowed here.
            return false;
        }
    }

    private static bool TryGetFieldType(DbDataReader reader, int index, out Type fieldType)
    {
        try
        {
            fieldType = reader.GetFieldType(index);
            return true;
        }
        catch (Exception ex) when (IsMetadataRejection(ex))
        {
            // The probe could not resolve a CLR type; the caller falls back to the ordinary path and,
            // where classification is needed, consults the provider's authoritative predicate. The
            // rejection exception type is deliberately not surfaced: an exception shape is not a
            // composite signal. A real driver/connection error (DbException), cancellation or a disposed
            // reader propagates instead of being masked.
            fieldType = typeof(object);
            return false;
        }
    }

    /// <summary>
    /// Classifies an exception thrown by a provider's metadata probe (<c>GetDataTypeName</c>/
    /// <c>GetFieldType</c>) as a rejection verdict rather than a real fault. Providers signal an
    /// unsupported column with <see cref="InvalidCastException"/> or <see cref="NotSupportedException"/>;
    /// those only mean "no verdict on this column", so the caller falls back to the ordinary path. A
    /// composite verdict is never derived from the rejection shape: where classification is needed, the
    /// caller consults the provider's <c>IsGenuineCompositeRawRowColumn</c> predicate instead. Every
    /// other exception — a driver/connection error, cancellation, or a disconnected/disposed reader
    /// (<see cref="ObjectDisposedException"/> is itself an <see cref="InvalidOperationException"/>) — is a
    /// real fault and propagates. In particular a plain <see cref="InvalidOperationException"/> is no
    /// longer a metadata verdict.
    /// </summary>
    private static bool IsMetadataRejection(Exception ex)
        => ex is InvalidCastException or NotSupportedException;

    /// <summary>
    /// Reports whether the single reader column at <paramref name="index"/> is a genuine named composite
    /// that may be read through the driver's typed composite accessor. The CLR field type must match the
    /// declared result type (checked by the caller) and the provider's authoritative predicate must report
    /// a composite; the declared result type must not itself be an array or a dictionary (which array
    /// covariance / a provider dictionary type would otherwise let match). Only arrays and
    /// <see cref="System.Collections.IDictionary"/> are excluded: a caller-registered composite that merely
    /// implements <see cref="System.Collections.IEnumerable"/> is a legitimate composite and must not be
    /// rejected by a blanket collection test.
    /// </summary>
    private static bool IsGenuineNamedComposite(DataContext context, DbDataReader reader, int index, Type resultType)
    {
        if (resultType.IsArray || typeof(System.Collections.IDictionary).IsAssignableFrom(resultType))
            return false;

        return context.IsGenuineCompositeRawRowColumn(reader, index);
    }

    /// <summary>
    /// Reports whether a multi-column result set contains a column the driver cannot resolve and that the
    /// provider's authoritative predicate identifies as an (unregistered) named composite mixed with
    /// scalar columns (S6/S12). Resolvable columns are skipped, and a metadata-rejection exception shape
    /// (<see cref="InvalidCastException"/>/<see cref="NotSupportedException"/>) is <b>not</b> a classifier:
    /// an unresolvable non-composite (for example <c>ltree</c> or <c>hstore</c>) keeps the ordinary
    /// no-match behaviour. The data type name is used only for the diagnostic. Only consulted when the
    /// entity path already matched no column, so a resolvable-but-unmapped column is still ignored.
    /// </summary>
    private static bool TryGetUnresolvableCompositeColumn(DataContext context, DbDataReader reader, out string? dataTypeName)
    {
        dataTypeName = null;
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (TryGetFieldType(reader, i, out _))
                continue;

            // ONLY the provider's authoritative predicate identifies a composite. The metadata-probe
            // exception type is not a classifier.
            if (!context.IsGenuineCompositeRawRowColumn(reader, i))
                continue;

            string name;
            try
            {
                name = reader.GetDataTypeName(i);
            }
            catch (Exception ex) when (IsMetadataRejection(ex))
            {
                continue;
            }

            if (!string.Equals(name, "record", StringComparison.Ordinal))
            {
                dataTypeName = name;
                return true;
            }
        }

        return false;
    }

    private static bool IsTupleBeyondSeven(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Tuple<,,,,,,,>);

    private static string BuildRecordSignature(Type resultType)
    {
        var args = resultType.GetGenericArguments();
        var parts = new string[args.Length];
        for (var i = 0; i < parts.Length; i++)
            parts[i] = args[i].AssemblyQualifiedName ?? args[i].FullName ?? args[i].Name;

        return string.Join(",", parts);
    }

    /// <summary>
    /// Builds the accessor for a caller-registered named composite (#194, S5): the driver resolves the
    /// single column to the registered CLR type, so it is read through the typed generic accessor. This
    /// accessor lives in the raw path (not in the provider's <c>MapColumnExpression</c>) so an ordinary
    /// LINQ class column — which may carry a value converter — is never hijacked. The driver read is
    /// wrapped (<see cref="RawRowValue.ReadNamedComposite{T}"/>) so a read failure or a SQL NULL into a
    /// non-nullable struct composite surfaces the R194-ERROR contract instead of a bare driver exception.
    /// Whole-record <c>DBNull</c> is still taken first via <c>IsDBNull</c> for a nullable target.
    /// </summary>
    private static Expression MapNamedCompositeColumn(SelectExpression column, Expression param)
    {
        var realType = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
        var index = Expression.Constant(column.Index);
        Expression getter = Expression.Call(
            ReadNamedCompositeMI.MakeGenericMethod(realType),
            Expression.Convert(param, typeof(DbDataReader)),
            index);

        if (!column.Nullable)
            return getter;

        // The getter returns the unwrapped composite type; for a declared non-nullable struct composite
        // that is the property type, but for a declared Nullable<struct> it must be lifted so the two
        // branches of the whole-record-NULL condition share a type.
        if (getter.Type != column.PropertyType)
            getter = Expression.Convert(getter, column.PropertyType);

        return Expression.Condition(
            Expression.Call(param, IsDBNullMI, index),
            Expression.Constant(null, column.PropertyType),
            getter);
    }

    /// <summary>
    /// Builds the accessor for an anonymous PostgreSQL <c>ROW(...)</c>: the driver already produced a
    /// null-preserving <c>object[]</c> for the single record column, so the mapper validates the declared
    /// tuple shape and constructs <see cref="Tuple"/> from it. A whole-record SQL NULL becomes a CLR
    /// <see langword="null"/>; per-field NULL/error semantics live in <see cref="RawRowValue"/>.
    /// </summary>
    private static Expression MapAnonymousTupleColumn(SelectExpression column, Expression record)
    {
        var tupleType = column.PropertyType;
        var args = tupleType.GetGenericArguments();
        foreach (var arg in args)
        {
            if (!IsScalarType(arg))
                throw UnsupportedRawRow("a record field type is outside the supported scalar allow-list, or a nested ROW was declared");
        }

        var index = Expression.Constant(column.Index);
        // Validate the driver's actual record at runtime: a non-array value (tuple materialization enabled)
        // or a ROW arity that differs from the declared tuple must not silently drop fields or throw a bare
        // IndexOutOfRangeException (R194-ERROR).
        var values = Expression.Call(
            ValidateRecordMI,
            Expression.Call(record, GetValueMI, index),
            Expression.Constant(args.Length));

        var ctor = tupleType.GetConstructor(args)
            ?? throw UnsupportedRawRow($"{tupleType.Name} has no public constructor matching its arity");

        var items = new Expression[args.Length];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Expression.Call(
                ConvertFieldMI.MakeGenericMethod(args[i]),
                Expression.ArrayIndex(values, Expression.Constant(i)),
                Expression.Constant(i));
        }

        return Expression.Condition(
            Expression.Call(record, IsDBNullMI, index),
            Expression.Constant(null, tupleType),
            Expression.New(ctor, items));
    }

    internal static NotSupportedException UnsupportedRawRow(string reason)
        => new($"PostgreSQL raw-row materialization is not supported: {reason}.");

    internal static InvalidOperationException FailedRawRow(string reason, Exception? inner = null)
        => inner is null
            ? new($"PostgreSQL raw-row materialization failed: {reason}.")
            : new($"PostgreSQL raw-row materialization failed: {reason}.", inner);


    private static NotSupportedException Unsupported(Type resultType)
        => new(
            $"ExecuteRaw supports mapped entity types and scalar types; {resultType.Name} is neither. "
            + $"Register a mapping with From<{resultType.Name}>() first, or read the result as a scalar.");

    /// <summary>
    /// Raw entity mapping binds by column name, which is expressed as member-init. A constructor with
    /// parameters would make <see cref="RowMaterializerBuilder"/> bind positionally (its longest
    /// constructor is used when its parameter count matches the projection), which can silently
    /// mis-map under a reader-ordered projection; reject it with an actionable message instead.
    /// </summary>
    private static void EnsureNameMappable(Type resultType)
    {
        var ctor = resultType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor is null || ctor.GetParameters().Length != 0)
            throw new NotSupportedException(
                $"Raw command mapping is by column name and requires {resultType.Name} to have a public parameterless constructor; "
                + "a constructor with parameters would bind the projected columns positionally, which is not supported for raw result sets.");
    }

    private static Expression MapScalarColumn(SelectExpression column, Expression record)
    {
        var index = Expression.Constant(column.Index);
        var isDbNull = Expression.Call(record, IsDBNullMI, index);
        var value = Expression.Call(ConvertMI.MakeGenericMethod(column.PropertyType), Expression.Call(record, GetValueMI, index));

        return column.PropertyType.IsValueType
            ? Expression.Condition(isDbNull, Expression.Default(column.PropertyType), value)
            : Expression.Condition(isDbNull, Expression.Constant(null, column.PropertyType), value);
    }

    private static SelectExpression[] BuildEntitySelectList(DataContext context, DbDataReader reader, Type resultType, IEntityMetadata entityMeta)
    {
        var baseList = GetBaseSelectList(resultType, entityMeta);
        var properties = new Dictionary<PropertyInfo, IPropertyMetadata>();
        foreach (var prop in entityMeta.Properties)
            properties[prop.PropertyInfo] = prop;

        // matched[c] is the clone bound to base projection slot c (null when unmatched); usedReader
        // ensures a reader column binds at most one slot, matched[c] ensures a slot binds at most once.
        var matched = new SelectExpression?[baseList.Length];
        var usedReader = new bool[reader.FieldCount];

        // Pass 1: the mapped column name (naming convention applied). This is what makes a property X
        // mapped to column "y" win over a property named Y also present in the reader.
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (usedReader[i])
                continue;

            var readerName = reader.GetName(i);
            for (var c = 0; c < baseList.Length; c++)
            {
                if (matched[c] is not null || !MatchesMappedColumn(baseList[c], properties, context.NamingConvention, readerName))
                    continue;

                matched[c] = CloneWithIndex(baseList[c], i);
                usedReader[i] = true;
                break;
            }
        }

        // Pass 2: the CLR property name, only for slots still unmatched. Range slots are excluded (they
        // bind through their declared lower/upper column names in pass 1).
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (usedReader[i])
                continue;

            var readerName = reader.GetName(i);
            for (var c = 0; c < baseList.Length; c++)
            {
                if (matched[c] is not null || !MatchesPropertyName(baseList[c], readerName))
                    continue;

                matched[c] = CloneWithIndex(baseList[c], i);
                usedReader[i] = true;
                break;
            }
        }

        // Range<T> materialization (RowMaterializerBuilder) expects a lower slot immediately followed
        // by its upper slot. Emit matched slots in metadata order, pairing them, and drop an incomplete
        // pair (only one of the two columns present in the reader).
        var result = new List<SelectExpression>(baseList.Length);
        for (var c = 0; c < baseList.Length; c++)
        {
            var column = matched[c];
            if (column is null || column.RangeColumnRole == RangeColumnRole.Upper)
                continue;

            if (column.RangeColumnRole == RangeColumnRole.Lower)
            {
                var upper = FindMatchingUpper(matched, baseList, column);
                if (upper is null)
                    continue;

                result.Add(column);
                result.Add(upper);
                continue;
            }

            result.Add(column);
        }

        if (result.Count == 0)
        {
            // The read was going to fail anyway. For a provider that surfaces PostgreSQL record/composite
            // columns, an unresolvable genuine composite column mixed with scalars is an (unregistered)
            // named composite: report the R194-ERROR guard *before* the name-mapping validation below, so
            // a positional-record composite surfaces the contracted guard rather than the generic
            // "... requires a public parameterless constructor" message. A resolvable unmapped column is
            // still ignored above, so ordinary entity reads are unaffected.
            if (context.RawRowColumnsSupported && TryGetUnresolvableCompositeColumn(context, reader, out var compositeName))
                throw UnsupportedRawRow($"a named composite column ({compositeName}) mixed with scalar columns is not supported");

            EnsureNameMappable(resultType);

            throw new InvalidOperationException(
                $"None of the result-set columns ({DescribeColumns(reader)}) matches a mapped property of {resultType.Name}.");
        }

        EnsureNameMappable(resultType);

        return result.ToArray();
    }

    private static SelectExpression? FindMatchingUpper(SelectExpression?[] matched, SelectExpression[] baseList, SelectExpression lower)
    {
        for (var c = 0; c < baseList.Length; c++)
        {
            if (baseList[c].RangeColumnRole == RangeColumnRole.Upper
                && ReferenceEquals(baseList[c].PropertyInfo, lower.PropertyInfo))
                return matched[c];
        }

        return null;
    }

    private static bool MatchesMappedColumn(SelectExpression column, Dictionary<PropertyInfo, IPropertyMetadata> properties, INamingConvention? namingConvention, string readerName)
    {
        if (column.RangeColumnRole == RangeColumnRole.Lower && column.RangeColumns is { } lower)
            return NameEquals(lower.LowerColumn, readerName);
        if (column.RangeColumnRole == RangeColumnRole.Upper && column.RangeColumns is { } upper)
            return NameEquals(upper.UpperColumn, readerName);

        if (column.PropertyInfo is { } propertyInfo && properties.TryGetValue(propertyInfo, out var prop))
            return NameEquals(SqlMutationBuilder.ResolveColumnName(prop, namingConvention), readerName);

        return false;
    }

    private static bool MatchesPropertyName(SelectExpression column, string readerName)
    {
        if (column.RangeColumnRole != RangeColumnRole.None)
            return false;

        return column.PropertyInfo is { } propertyInfo
            ? NameEquals(propertyInfo.Name, readerName)
            : NameEquals(column.PropertyName, readerName);
    }

    private static SelectExpression[] GetBaseSelectList(Type resultType, IEntityMetadata entityMeta)
        => RawSelectListCache.GetOrAdd(resultType, _ => EntitySelectListBuilder.Build(resultType, entityMeta, CancellationToken.None).Columns);

    // A clone is mandatory: the base projection is shared per type, so mutating its Index to point at a
    // reader ordinal would corrupt any other raw read of the same type.
    private static SelectExpression CloneWithIndex(SelectExpression source, int index) => new(source.PropertyType)
    {
        Index = index,
        PropertyName = source.PropertyName,
        Expression = source.Expression,
        PropertyInfo = source.PropertyInfo,
        DurationUnit = source.DurationUnit,
        DurationPrecision = source.DurationPrecision,
        ProviderType = source.ProviderType,
        Converter = source.Converter,
        DefaultOnNull = source.DefaultOnNull,
        RangeColumnRole = source.RangeColumnRole,
        RangeColumns = source.RangeColumns,
    };

    private static string BuildColumnsKey(DbDataReader reader)
    {
        if (reader.FieldCount == 0)
            return string.Empty;

        // The shape key is the exact ordered reader column names; it is cheap to build and lets a
        // cached mapper be reused for the same shape without touching the metadata/property dictionary.
        var names = new string[reader.FieldCount];
        for (var i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return string.Join("\u001f", names);
    }

    private static bool NameEquals(string? a, string? b)
        => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string DescribeColumns(DbDataReader reader)
    {
        if (reader.FieldCount == 0)
            return string.Empty;

        var names = new string[reader.FieldCount];
        for (var i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return string.Join(", ", names);
    }

    internal static bool IsScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(Guid)
            || type == typeof(byte[]);
    }
}

/// <summary>
/// Converts a boxed provider value to the requested scalar type for <see cref="RawMapperFactory"/>.
/// Kept as a static entry point so the compiled mapper can call it without boxing the converter.
/// </summary>
internal static class RawValueConverter
{
    /// <summary>Converts <paramref name="value"/> to <typeparamref name="T"/>, unwrapping a nullable target when present.</summary>
    /// <typeparam name="T">The requested scalar type.</typeparam>
    /// <param name="value">The boxed provider value (never <see cref="DBNull"/>; the caller handles nulls).</param>
    /// <returns>The converted value.</returns>
    public static T Convert<T>(object value)
    {
        if (value is T typed)
            return typed;

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)ConvertTo(value, target)!;
    }

    private static object? ConvertTo(object? value, Type target)
    {
        if (value is null)
            return null;

        if (target.IsInstanceOfType(value))
            return value;

        if (target.IsEnum)
            return value is string text ? Enum.Parse(target, text, ignoreCase: true) : Enum.ToObject(target, value);

        if (target == typeof(Guid))
            return value is Guid guid ? guid : Guid.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!);

        if (target == typeof(DateOnly))
            return value is DateOnly dateOnly ? dateOnly : DateOnly.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

        if (target == typeof(TimeOnly))
            return value is TimeOnly timeOnly ? timeOnly : TimeOnly.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

        if (target == typeof(DateTimeOffset))
            return value is DateTimeOffset offset ? offset : new DateTimeOffset(System.Convert.ToDateTime(value, CultureInfo.InvariantCulture));

        if (target == typeof(TimeSpan))
            return value is TimeSpan timeSpan ? timeSpan : TimeSpan.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

        return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Converts one element of a raw PostgreSQL record column (the null-preserving <c>object[]</c> the
/// driver returns for an anonymous <c>ROW</c>) into a declared tuple item type, enforcing the
/// R194-NULL / R194-ERROR contract: a SQL NULL for a non-nullable value-type item fails (never
/// <c>default(T)</c>), a nested ROW is an unsupported shape, and a conversion failure preserves its
/// inner exception.
/// </summary>
internal static class RawRowValue
{
    /// <summary>
    /// Validates that the driver returned a null-preserving <see cref="object"/>[] for the record column
    /// and that its arity matches the declared tuple before any field is indexed. A non-array value (for
    /// example a driver that returns a <see cref="Tuple"/> because tuple materialization was enabled) and
    /// an arity mismatch both surface the R194-ERROR diagnostic: a wider ROW would silently drop fields
    /// and a narrower one would throw a bare <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    /// <param name="value">The value the driver returned for the record column.</param>
    /// <param name="expectedArity">The declared tuple arity.</param>
    /// <returns>The record fields.</returns>
    /// <exception cref="InvalidOperationException">The value is not an <see cref="object"/>[] or its length differs from <paramref name="expectedArity"/>.</exception>
    public static object?[] ValidateRecord(object? value, int expectedArity)
    {
        if (value is not object[] values)
        {
            var actual = value is null ? "null" : value.GetType().Name;
            throw RawMapperFactory.FailedRawRow(
                $"the driver returned a record value of type {actual} instead of System.Object[]");
        }

        if (values.Length != expectedArity)
        {
            throw RawMapperFactory.FailedRawRow(
                $"the record has {values.Length} fields but the declared tuple has {expectedArity} items");
        }

        return values;
    }

    /// <summary>
    /// Reads a whole named composite column through the driver's typed <see cref="DbDataReader.GetFieldValue{T}"/>
    /// accessor, translating a driver failure into the R194-ERROR contract. The caller takes whole-record
    /// <c>DBNull</c> first via <c>IsDBNull</c> for a nullable target; a non-nullable struct composite that
    /// receives a SQL NULL therefore reaches the driver and surfaces as a materialization failure, never a
    /// bare driver exception.
    /// </summary>
    /// <typeparam name="T">The registered composite CLR type (the nullable underlying type when declared nullable).</typeparam>
    /// <param name="reader">The reader positioned on the composite column.</param>
    /// <param name="index">The zero-based column ordinal.</param>
    /// <returns>The materialized composite value.</returns>
    /// <exception cref="InvalidOperationException">The driver rejected the read (for example a SQL NULL into a non-nullable composite); the driver exception is preserved as <see cref="Exception.InnerException"/>.</exception>
    public static T ReadNamedComposite<T>(DbDataReader reader, int index)
    {
        try
        {
            return reader.GetFieldValue<T>(index);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
            and not OutOfMemoryException
            and not ObjectDisposedException)
        {
            // Cancellation, resource-exhaustion and disposed-reader signals are real faults, not
            // materialization mismatches, and propagate unwrapped — consistent with the metadata probes
            // (ObjectDisposedException is itself an InvalidOperationException and must not be wrapped here).
            throw RawMapperFactory.FailedRawRow(
                $"named composite column {index} could not be read as {typeof(T).Name}", ex);
        }
    }

    /// <summary>Converts a record field to <typeparamref name="T"/>, distinguishing NULL from a value.</summary>
    /// <typeparam name="T">The declared tuple item type.</typeparam>
    /// <param name="value">The raw field value from the driver's record <c>object[]</c> (may be <see langword="null"/>).</param>
    /// <param name="index">The zero-based field ordinal, used in the error message.</param>
    /// <returns>The converted value, or <see langword="null"/> for a nullable/reference item and a NULL field.</returns>
    /// <exception cref="InvalidOperationException">A NULL for a non-nullable value-type item, or a value not convertible to the item type.</exception>
    /// <exception cref="NotSupportedException">A nested ROW was declared as a scalar item.</exception>
    public static T ConvertField<T>(object? value, int index)
    {
        var target = typeof(T);
        var nullable = !target.IsValueType || Nullable.GetUnderlyingType(target) is not null;

        if (value is null || value is DBNull)
        {
            if (nullable)
                return default!;

            throw RawMapperFactory.FailedRawRow(
                $"record field {index} is NULL but the declared tuple item type {target.Name} is not nullable");
        }

        // A genuine nested ROW is the driver's System.Object[]; array covariance must not make a
        // string[]/text[] field (or any other array) look like one and raise the wrong guard.
        if (value.GetType().IsSZArray && value.GetType().GetElementType() == typeof(object))
            throw RawMapperFactory.UnsupportedRawRow("nested ROW values are not supported");

        try
        {
            return RawValueConverter.Convert<T>(value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // Every conversion failure is a data mismatch and must surface as the contracted
            // InvalidOperationException with the inner exception preserved (R194-ERROR). Cancellation and
            // resource-exhaustion signals are not data mismatches and propagate unchanged. The unsupported
            // shape guards are raised before this try, so nothing here is a shape rejection.
            throw RawMapperFactory.FailedRawRow(
                $"record field {index} value of type {value.GetType().Name} cannot be converted to {target.Name}",
                ex);
        }
    }
}
