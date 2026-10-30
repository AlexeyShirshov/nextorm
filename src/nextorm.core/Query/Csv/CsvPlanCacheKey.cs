using System.Data;
using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// Builds the structural cache key of a compiled <see cref="CsvPlan"/>. The key describes exactly what
/// the compiled column delegates depend on: the concrete context type (because
/// <see cref="DataContext.MapTypedColumnExpression"/> is a <c>protected virtual</c> hook), the projection
/// signature, the resolved reader storage types, the applied <see cref="CommandBehavior.SequentialAccess"/>
/// mode, and the effective CSV option values that affect compilation or binary admission.
/// <para>
/// A non-null <see cref="CsvStreamOptions.ValueTransform"/> always makes the configuration
/// unrepresentable, so <see cref="Build"/> returns <see langword="null"/> and the caller bypasses the
/// cache (lookup and publication). Reference identity of <see cref="CsvStreamOptions"/> is never used.
/// </para>
/// </summary>
internal static class CsvPlanCacheKey
{
    /// <summary>
    /// Builds the cache key, or <see langword="null"/> when the configuration is unrepresentable and the
    /// caller must compile without publishing an entry.
    /// </summary>
    internal static string? Build(Type contextType, SelectExpression[]? selectList, IDataRecord? schema, bool sequentialAccess, CsvStreamOptions? options)
    {
        if (selectList is null || selectList.Length == 0)
            return null;

        // ValueTransform always bypasses the cache, including an identity transform.
        if (options?.ValueTransform is not null)
            return null;

        var builder = new StringBuilder(256);
        builder.Append("csv;");
        AppendType(builder, contextType);
        AppendBool(builder, sequentialAccess);

        // Effective option values that affect compilation or binary admission.
        var delimiter = options?.Delimiter ?? ',';
        var nullMarker = options?.NullMarker ?? CsvStreamOptions.DefaultNullMarker;
        var excelMode = options?.ExcelMode ?? false;
        builder.Append(delimiter).Append(';');
        AppendString(builder, nullMarker);
        AppendBool(builder, excelMode);

        // Projection signature.
        AppendInt(builder, selectList.Length);
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];
            AppendType(builder, column.PropertyType);
            AppendString(builder, column.PropertyName);
            AppendInt(builder, column.Index);
            AppendString(builder, column.PhysicalColumnName);
            AppendTypeOrNull(builder, column.ProviderType);
            AppendBool(builder, column.IsLobStreaming);
            AppendString(builder, column.Converter?.GetType().AssemblyQualifiedName);
            AppendString(builder, Header(column));
        }

        // Resolved reader storage types.
        for (var i = 0; i < selectList.Length; i++)
        {
            Type storageType;
            try
            {
                storageType = schema is null ? typeof(object) : schema.GetFieldType(selectList[i].Index);
            }
            catch
            {
                // A schema that cannot report a field type is not representable; bypass conservatively.
                return null;
            }

            AppendType(builder, storageType);
        }

        return builder.ToString();
    }

    private static string Header(SelectExpression column)
        => string.IsNullOrEmpty(column.PropertyName) ? $"Column{column.Index + 1}" : column.PropertyName;

    private static void AppendInt(StringBuilder builder, int value)
        => builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');

    private static void AppendBool(StringBuilder builder, bool value)
        => builder.Append(value ? '1' : '0').Append(';');

    private static void AppendString(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1;");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
    }

    private static void AppendType(StringBuilder builder, Type type)
    {
        var name = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        builder.Append(name.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(name).Append(';');
    }

    private static void AppendTypeOrNull(StringBuilder builder, Type? type)
    {
        if (type is null)
        {
            builder.Append("-1;");
            return;
        }

        AppendType(builder, type);
    }
}