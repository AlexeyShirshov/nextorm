using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent builder, passed to <c>From&lt;T&gt;(...)</c>, that declares the table name and column
/// mappings of an entity type.
/// </summary>
public class EntityMetadataBuilder<T>
{
    private readonly IList<EntityPropertyBuilder<T>> _props = new List<EntityPropertyBuilder<T>>();
    private string? _tableName;

    /// <summary>
    /// Builds the metadata from the mappings declared on this builder, auto-deriving the table name
    /// and, when no properties were declared, the property mappings from the CLR type.
    /// </summary>
    /// <returns>The resolved entity metadata.</returns>
    public IEntityMetadata Build()
    {
        var (tableName, isTableNameAuto) = string.IsNullOrEmpty(_tableName)
            ? AutoBuildTableName()
            : (_tableName, false);

        return new EntityMetadata(tableName, _props.Count == 0
            ? AutoBuildProperties()
            : _props.Select(pb => pb.Build()).ToArray(), isTableNameAuto);
    }
    /// <summary>
    /// Builds the metadata entirely by reflecting over the CLR type, ignoring any table or property
    /// mappings declared on this builder.
    /// </summary>
    /// <returns>The auto-derived entity metadata.</returns>
    public IEntityMetadata AutoBuild()
    {
        var propsMeta = AutoBuildProperties();

        var (tableName, isTableNameAuto) = AutoBuildTableName();

        return new EntityMetadata(tableName, propsMeta, isTableNameAuto);
    }

    private static List<IPropertyMetadata> AutoBuildProperties()
    {
        var propsMeta = new List<IPropertyMetadata>();

        var entityType = typeof(T);

        var props = entityType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanWrite).ToArray();
        for (var (idx, cnt) = (0, props.Length); idx < cnt; idx++)
        {
            var prop = props[idx];
            if (prop is null) continue;

            // Attributes are declared on the interface for interface-mapped entities, so a property
            // without a direct mapping falls back to its matching interface property (mirrors the
            // former ColumnAttribute-only lookup).
            var intProp = FindInterfaceProperty(entityType, prop);
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true) ?? intProp?.GetCustomAttribute<ColumnAttribute>(true);
            var keyAttr = prop.GetCustomAttribute<KeyAttribute>(true) ?? intProp?.GetCustomAttribute<KeyAttribute>(true);
            var generatedAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>(true) ?? intProp?.GetCustomAttribute<DatabaseGeneratedAttribute>(true);
            var durationAttr = prop.GetCustomAttribute<DurationAttribute>(true) ?? intProp?.GetCustomAttribute<DurationAttribute>(true);
            var collationAttr = prop.GetCustomAttribute<CollationAttribute>(true) ?? intProp?.GetCustomAttribute<CollationAttribute>(true);
            var valueConverterAttr = prop.GetCustomAttribute<ValueConverterAttribute>(true) ?? intProp?.GetCustomAttribute<ValueConverterAttribute>(true);
            var jsonColumnAttr = prop.GetCustomAttribute<JsonColumnAttribute>(true) ?? intProp?.GetCustomAttribute<JsonColumnAttribute>(true);
            var rangeColumnsAttr = prop.GetCustomAttribute<RangeColumnsAttribute>(true) ?? intProp?.GetCustomAttribute<RangeColumnsAttribute>(true);
            var generated = generatedAttr?.DatabaseGeneratedOption ?? DatabaseGeneratedOption.None;

            if (valueConverterAttr is not null && jsonColumnAttr is not null)
                throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} cannot be mapped with both {nameof(ValueConverterAttribute)} and {nameof(JsonColumnAttribute)}.");

            if (rangeColumnsAttr is not null)
            {
                if (valueConverterAttr is not null || jsonColumnAttr is not null)
                    throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} cannot be mapped with both {nameof(RangeColumnsAttribute)} and a value/JSON converter.");

                if (!RangeTypeFacts.IsRange(prop.PropertyType))
                    throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} is mapped with {nameof(RangeColumnsAttribute)} but its type {prop.PropertyType} is not Range<T>.");

                if (!string.IsNullOrEmpty(colAttr?.Name))
                    throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} cannot be mapped with both {nameof(ColumnAttribute)} and {nameof(RangeColumnsAttribute)}; the range pair declares its own column names.");
            }

            var columnName = rangeColumnsAttr is not null
                ? rangeColumnsAttr.LowerColumn
                : !string.IsNullOrEmpty(colAttr?.Name) ? colAttr!.Name! : prop.Name;
            propsMeta.Add(new PropertyMetadata
            {
                ColumnName = columnName,
                PropertyInfo = prop,
                IsColumnNameAuto = rangeColumnsAttr is null && string.IsNullOrEmpty(colAttr?.Name),
                IsKey = keyAttr is not null,
                IsIdentity = generated == DatabaseGeneratedOption.Identity,
                IsComputed = generated == DatabaseGeneratedOption.Computed,
                DurationUnit = durationAttr?.Unit,
                DurationPrecision = durationAttr?.Precision ?? 0,
                Collation = collationAttr?.Name,
                Converter = jsonColumnAttr is not null
                    ? JsonColumnConverterFactory.Create(prop.PropertyType, jsonColumnAttr.Storage)
                    : valueConverterAttr?.Create(prop.PropertyType),
                RangeColumns = rangeColumnsAttr?.ToMetadata(),
            });
        }

        InferKeyIfMissing(propsMeta, entityType.Name);

        return propsMeta;
    }

    private static PropertyInfo? FindInterfaceProperty(Type entityType, PropertyInfo prop)
    {
        if (entityType.IsInterface || prop.GetMethod is null)
            return null;

        foreach (var interf in entityType.GetInterfaces())
        {
            var intMap = entityType.GetInterfaceMap(interf);

            var implIdx = Array.IndexOf(intMap.TargetMethods, prop.GetMethod);
            if (implIdx >= 0)
            {
                var intMethod = intMap.InterfaceMethods[implIdx];
                var intProp = interf.GetProperties().FirstOrDefault(p => p.GetMethod == intMethod);
                if (intProp is not null)
                    return intProp;
            }
        }

        return null;
    }

    // Key convention when no property is declared with [Key]/.Key(): the property named "Id" or
    // "<TypeName>Id". Insert does not depend on the key, but update/delete (todo_update/todo_delete)
    // address a row through it.
    private static void InferKeyIfMissing(List<IPropertyMetadata> props, string typeName)
    {
        foreach (var p in props)
        {
            if (p.IsKey)
                return;
        }

        var idName = typeName + "Id";
        foreach (var candidate in new[] { "Id", idName })
        {
            foreach (var p in props)
            {
                if (string.Equals(p.PropertyInfo.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    ((PropertyMetadata)p).IsKey = true;
                    return;
                }
            }
        }
    }

    private static (string? TableName, bool IsAuto) AutoBuildTableName()
    {
        var entityType = typeof(T);
        string? tableName = entityType.Name;
        var isAuto = true;

        var sqlTableAttr = entityType.GetCustomAttribute<SqlTableAttribute>(true);

        if (sqlTableAttr is not null)
        {
            tableName = sqlTableAttr.Name;
            isAuto = false;
        }
        else
        {
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>(true);

            if (tableAttr is not null)
            {
                tableName = tableAttr.Name;
                isAuto = false;
            }
        }

        foreach (var interf in entityType.GetInterfaces())
        {
            sqlTableAttr = interf.GetCustomAttribute<SqlTableAttribute>(true);

            if (sqlTableAttr is not null)
            {
                tableName = sqlTableAttr.Name;
                isAuto = false;
            }
            else
            {
                var tableAttr = interf.GetCustomAttribute<TableAttribute>(true);

                if (tableAttr is not null)
                {
                    tableName = tableAttr.Name;
                    isAuto = false;
                }
            }
        }

        return (tableName, isAuto);
    }

    /// <summary>
    /// Declares a fluent mapping for the property selected by <paramref name="propertySelector"/>.
    /// </summary>
    /// <param name="propertySelector">Selects the property to map; the expression must produce a <see cref="PropertyInfo"/>.</param>
    /// <returns>A builder for the selected property's column mapping.</returns>
    public EntityPropertyBuilder<T> Property(Expression<Func<T, object>> propertySelector)
    {
        var pb = new EntityPropertyBuilder<T>(propertySelector);
        _props.Add(pb);
        return pb;
    }
    /// <summary>
    /// Overrides the table name for the entity instead of deriving it from the CLR type or attributes.
    /// </summary>
    /// <param name="tableName">The table name to map the entity to.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> Table(string tableName)
    {
        _tableName = tableName;
        return this;
    }
}
