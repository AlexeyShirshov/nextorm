using System.Collections.Concurrent;
using System.Reflection;

namespace nextorm.core;
public static class MemberInfoExtensions
{
    // Entity metadata is registered once per type (DataContextCache.Metadata only builds on a miss),
    // so the resolved column name is stable for the lifetime of the process. Caching it turns the
    // per-member metadata lookup + linear scan into a dictionary read on the SQL-build path.
    private static readonly ConcurrentDictionary<PropertyInfo, string> _columnNames = new();

    /// <summary>
    /// Column name registered for the member in the shared entity metadata cache, or an empty string
    /// when the metadata does not declare it. Needs no context instance, so no context parameter.
    /// </summary>
    public static string GetPropertyColumnName(this MemberInfo mi)
    {
        if (mi is PropertyInfo pi)
        {
            if (_columnNames.TryGetValue(pi, out var cached))
                return cached;

            if (DataContextCache.Metadata.TryGetValue(pi.DeclaringType!, pi, out var prop))
            {
                var name = prop!.ColumnName;
                _columnNames.TryAdd(pi, name);
                return name;
            }
        }

        return string.Empty;
    }
    public static bool TryGetValue(this IDictionary<Type, IEntityMeta> dic, Type type, PropertyInfo pi, out IPropertyMeta? prop)
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