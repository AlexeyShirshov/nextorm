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
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true);
            if (!string.IsNullOrEmpty(colAttr?.Name))
            {
                propsMeta.Add(new PropertyMetadata { ColumnName = colAttr.Name, PropertyInfo = prop, IsColumnNameAuto = false });
            }
            else
            {
                var added = false;
                foreach (var interf in entityType.GetInterfaces())
                {
                    var intMap = entityType.GetInterfaceMap(interf);

                    var implIdx = Array.IndexOf(intMap.TargetMethods, prop!.GetMethod);
                    if (implIdx >= 0)
                    {
                        var intMethod = intMap.InterfaceMethods[implIdx];

                        var intProp = interf.GetProperties().FirstOrDefault(prop => prop.GetMethod == intMethod);
                        colAttr = intProp?.GetCustomAttribute<ColumnAttribute>(true);
                        if (!string.IsNullOrEmpty(colAttr?.Name))
                        {
                            propsMeta.Add(new PropertyMetadata { ColumnName = colAttr.Name, PropertyInfo = prop, IsColumnNameAuto = false });
                            added = true;
                            break;
                        }
                    }
                }

                if (!added)
                    propsMeta.Add(new PropertyMetadata { ColumnName = prop.Name, PropertyInfo = prop, IsColumnNameAuto = true });
            }
        }

        return propsMeta;
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
