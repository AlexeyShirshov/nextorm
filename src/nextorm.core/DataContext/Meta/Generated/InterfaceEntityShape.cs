using System.Reflection;

namespace NextORM.Core;

internal sealed class InterfaceEntityShape(
    ConstructorInfo constructor, PropertyInfo[] members, Dictionary<string, PropertyInfo> storage)
{
    internal ConstructorInfo Constructor { get; } = constructor;

    internal PropertyInfo[] Members { get; } = members;

    internal PropertyInfo Bind(SelectExpression column, Type entityType)
    {
        var name = column.BoundMember?.Name ?? column.PropertyName;
        if (name is not null && storage.TryGetValue(name, out var property))
        {
            return property;
        }

        throw new QueryPreparationException($"Column '{name}' does not bind to a property on interface entity '{entityType}'.");
    }
}
