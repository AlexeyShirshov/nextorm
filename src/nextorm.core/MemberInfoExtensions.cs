using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;
public static class MemberInfoExtensions
{
    // Entity metadata is registered once per type (DataContextCache.Metadata only builds on a miss),
    // so the resolved column name is stable for a given naming convention for the lifetime of the
    // process. Caching it turns the per-member metadata lookup + linear scan into a dictionary read
    // on the SQL-build path.
    private static readonly ConcurrentDictionary<(PropertyInfo, INamingConvention?), string> _columnNames = new();

    /// <summary>
    /// Column name registered for the member in the shared entity metadata cache, or an empty string
    /// when the metadata does not declare it. Auto-derived names are translated through
    /// <paramref name="convention"/>; declared names are returned verbatim.
    /// </summary>
    public static string GetPropertyColumnName(this MemberInfo mi, INamingConvention? convention = null)
    {
        if (mi is PropertyInfo pi)
        {
            var key = (pi, convention);

            if (_columnNames.TryGetValue(key, out var cached))
                return cached;

            if (DataContextCache.Metadata.TryGetValue(pi.DeclaringType!, pi, out var prop))
            {
                var name = prop!.ColumnName;
                if (prop.IsColumnNameAuto && convention is not null)
                    name = convention.ColumnName(name);

                _columnNames.TryAdd(key, name);
                return name;
            }
        }

        return string.Empty;
    }
    public static bool TryGetValue(this IDictionary<Type, IEntityMetadata> dic, Type type, PropertyInfo pi, out IPropertyMetadata? prop)
    {
        if (dic.TryGetValue(type, out var entity))
        {
            foreach (var item in entity.Properties)
            {
                if (item.PropertyInfo == pi)
                {
                    prop = item;
                    return true;
                }
            }
        }
        prop = null;
        return false;
    }
}
