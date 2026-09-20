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

    public IEntityMetadata Build()
    {
        return new EntityMetadata(string.IsNullOrEmpty(_tableName)
            ? AutoBuildTableName()
            : _tableName, _props.Count == 0
                ? AutoBuildProperties()
                : _props.Select(pb => pb.Build()).ToArray());
    }
    public IEntityMetadata AutoBuild()
    {
        var propsMeta = AutoBuildProperties();

        var tableName = AutoBuildTableName();

        return new EntityMetadata(tableName, propsMeta);
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
                propsMeta.Add(new PropertyMetadata { ColumnName = colAttr.Name, PropertyInfo = prop });
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
                            propsMeta.Add(new PropertyMetadata { ColumnName = colAttr.Name, PropertyInfo = prop });
                            added = true;
                            break;
                        }
                    }
                }

                if (!added)
                    propsMeta.Add(new PropertyMetadata { ColumnName = prop.Name, PropertyInfo = prop });
            }
        }

        return propsMeta;
    }

    private static string? AutoBuildTableName()
    {
        var entityType = typeof(T);
        string? tableName = entityType.Name;

        var sqlTableAttr = entityType.GetCustomAttribute<SqlTableAttribute>(true);

        if (sqlTableAttr is not null)
            tableName = sqlTableAttr.Name;
        else
        {
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>(true);

            if (tableAttr is not null)
                tableName = tableAttr.Name;
        }

        foreach (var interf in entityType.GetInterfaces())
        {
            sqlTableAttr = interf.GetCustomAttribute<SqlTableAttribute>(true);

            if (sqlTableAttr is not null)
                tableName = sqlTableAttr.Name;
            else
            {
                var tableAttr = interf.GetCustomAttribute<TableAttribute>(true);

                if (tableAttr is not null)
                    tableName = tableAttr.Name;
            }
        }

        return tableName;
    }

    public EntityPropertyBuilder<T> Property(Expression<Func<T, object>> propertySelector)
    {
        var pb = new EntityPropertyBuilder<T>(propertySelector);
        _props.Add(pb);
        return pb;
    }
    public EntityMetadataBuilder<T> Table(string tableName)
    {
        _tableName = tableName;
        return this;
    }
}
