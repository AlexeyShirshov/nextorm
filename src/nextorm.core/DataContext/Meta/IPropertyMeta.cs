using System.Reflection;

namespace nextorm.core;

public interface IPropertyMeta
{
    PropertyInfo PropertyInfo { get; }
    string ColumnName { get; }
}