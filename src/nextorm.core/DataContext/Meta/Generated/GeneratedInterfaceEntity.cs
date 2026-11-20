using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NextORM.Core;

/// <summary>Compiler-generated registration for interface entities; not a consumer construction API.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedInterfaceEntity
{
    private const DynamicallyAccessedMemberTypes RequiredMembers =
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties;

    private static readonly ConcurrentDictionary<Type, Registration> Shapes = new();

    /// <summary>Registers an implementation and its declared interface members from generated code.</summary>
    /// <typeparam name="TEntity">The interface used in the query.</typeparam>
    /// <param name="implementationType">The generated class with a parameterless constructor and indexed storage properties.</param>
    /// <param name="members">Returns the mapped properties of the original interface, in storage-slot order.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<TEntity>(
        [DynamicallyAccessedMembers(RequiredMembers)] Type implementationType,
        Func<PropertyInfo[]> members)
    {
        // Materialization keeps the declared PropertyInfos, so SQL and metadata never see the
        // generated storage names. Root only the generated constructor and writable slots.
        var factory = new ShapeFactory(implementationType, members);
        Shapes.TryAdd(typeof(TEntity), new Registration(
            new Lazy<InterfaceEntityShape>(factory.Create),
            static () => new EntityMetadataBuilder<TEntity>().AutoBuild()));
    }

    internal static InterfaceEntityShape? Find(Type entityType) =>
        Shapes.TryGetValue(entityType, out var registration) ? registration.Shape.Value : null;

    internal static Func<IEntityMetadata>? FindMetadata(Type entityType) =>
        Shapes.TryGetValue(entityType, out var registration) ? registration.BuildMetadata : null;

    private sealed record Registration(Lazy<InterfaceEntityShape> Shape, Func<IEntityMetadata> BuildMetadata);

    private sealed class ShapeFactory(
        [DynamicallyAccessedMembers(RequiredMembers)] Type implementationType, Func<PropertyInfo[]> members)
    {
        [DynamicallyAccessedMembers(RequiredMembers)]
        private readonly Type _implementationType = implementationType;

        internal InterfaceEntityShape Create()
        {
            var declared = members();
            var slots = new Dictionary<string, PropertyInfo>(declared.Length, StringComparer.Ordinal);
            for (var i = 0; i < declared.Length; i++)
            {
                slots.Add(declared[i].Name, _implementationType.GetProperty(
                    "Slot" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))!);
            }

            return new InterfaceEntityShape(_implementationType.GetConstructor(Type.EmptyTypes)!, declared, slots);
        }
    }
}
