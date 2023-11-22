using System.Reflection;

namespace nextorm.core;
public static class MemberInfoExtensions
{
    public static string GetPropertyColumnName(this MemberInfo mi, IDataProvider dataProvider)
    {
        if (mi is PropertyInfo pi && dataProvider.Metadata.TryGetValue(mi.DeclaringType!, pi, out var prop))
        {
            return prop!.ColumnName;
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