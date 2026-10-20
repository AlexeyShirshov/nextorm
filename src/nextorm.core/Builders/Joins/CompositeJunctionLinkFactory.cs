using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace NextORM.Core;

/// <summary>
/// The internal, arity-general link carrier for a composite-junction many-to-many <c>JoinInto</c>
/// (#223). Unlike the scalar <see cref="JoinIntoLink{TParentKey,TChildKey}"/> (two key members), a
/// composite junction needs one scalar member per declared junction foreign-key component plus the
/// occurrence. The shape is emitted at runtime so the carrier is arity-general (no fixed
/// two-component maximum) and stays a plain flat entity-item: every member is an individually mapped
/// scalar column, so the existing entity-item projection/materialization route applies and no
/// nested/derived projection (the T213 failure mode) is used.
/// </summary>
/// <remarks>
/// The facility is internal, keyless and provider-agnostic: it only generates the carrier shape and
/// (re)publishes its metadata. The emitted type is cached per junction foreign-key component-type
/// shape, so the same shape never rebuilds its type or metadata; component order and count are carried
/// by the <see cref="JoinIntoManyToManySpec{TEntity,TChild}"/> accessors, not by the shape. Emission and
/// publication are atomic per shape: the cache stores a <see cref="Lazy{T}"/> whose single execution
/// emits the type and (re)publishes the metadata, so concurrent first use of one shape cannot emit
/// duplicate dynamic types or hand callers split metadata.
/// </remarks>
internal static class CompositeJunctionLinkFactory
{
    /// <summary>
    /// The emitted flat carrier for one junction foreign-key shape: the closed link type, the parent- and
    /// child-component property lists (in declared order) and the occurrence property.
    /// </summary>
    internal sealed class CarrierInfo
    {
        internal CarrierInfo(Type linkType, PropertyInfo[] parentKeys, PropertyInfo[] childKeys, PropertyInfo occurrence)
        {
            LinkType = linkType;
            ParentKeys = parentKeys;
            ChildKeys = childKeys;
            Occurrence = occurrence;
        }

        /// <summary>The closed flat carrier type.</summary>
        internal Type LinkType { get; }

        /// <summary>The carrier's parent-component properties, in declared junction order.</summary>
        internal PropertyInfo[] ParentKeys { get; }

        /// <summary>The carrier's child-component properties, in declared junction order.</summary>
        internal PropertyInfo[] ChildKeys { get; }

        /// <summary>The carrier's occurrence property.</summary>
        internal PropertyInfo Occurrence { get; }
    }

    private const string OccurrencePropertyName = "Occurrence";

    private static readonly ConcurrentDictionary<string, Lazy<CarrierInfo>> CarrierCache = new();
    private static readonly ConcurrentDictionary<Type, CarrierInfo> CarrierByType = new();
    private static readonly object EmitGate = new();
    private static readonly ModuleBuilder Module = CreateModule();
    private static int _emittedTypeCounter;

    private static ModuleBuilder CreateModule()
    {
        var assemblyName = new AssemblyName("NextORM.Core.CompositeJunctionLink");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule("Main");
    }

    /// <summary>
    /// Resolves (emitting on first use) the flat carrier for the given junction foreign-key members and
    /// (re)publishes its metadata into <see cref="DataContextCache.Metadata"/> so both the derived link
    /// subquery and the outer pair command expand it. The check and write run under the shared
    /// registration gate, mirroring <see cref="JunctionLinkSourceFactory"/>'s scalar link publication.
    /// </summary>
    /// <param name="parentForeignKeys">The junction foreign-key members referencing the parent, in order.</param>
    /// <param name="childForeignKeys">The junction foreign-key members referencing the child, in order.</param>
    /// <returns>The carrier information for the shape.</returns>
    internal static CarrierInfo Ensure(IReadOnlyList<PropertyInfo> parentForeignKeys, IReadOnlyList<PropertyInfo> childForeignKeys)
    {
        ArgumentNullException.ThrowIfNull(parentForeignKeys);
        ArgumentNullException.ThrowIfNull(childForeignKeys);
        if (parentForeignKeys.Count == 0 || childForeignKeys.Count == 0)
            throw new NotSupportedException("A composite-junction link needs at least one parent and one child foreign-key component.");

        foreach (var property in parentForeignKeys)
            ValidateComponent(property);
        foreach (var property in childForeignKeys)
            ValidateComponent(property);

        var carrier = CarrierCache.GetOrAdd(
            BuildKey(parentForeignKeys, childForeignKeys),
            _ => new Lazy<CarrierInfo>(
                () => Emit(parentForeignKeys, childForeignKeys),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        CarrierByType[carrier.LinkType] = carrier;
        Publish(carrier);
        return carrier;
    }

    /// <summary>
    /// Rejects a junction foreign-key component whose property type the flat carrier cannot emit or map
    /// (a null property, a pointer/by-ref/by-ref-like or open-generic type), fail-closed at preparation
    /// rather than during runtime emission.
    /// </summary>
    private static void ValidateComponent(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var type = property.PropertyType;
        if (type == typeof(void) || type.IsByRef || type.IsPointer || type.IsByRefLike || type.ContainsGenericParameters)
            throw new NotSupportedException(
                $"The composite-junction link component '{property.Name}' has the unsupported type '{type}'; " +
                "a flat link carrier cannot map it.");
    }

    /// <summary>True when <paramref name="itemType"/> is a carrier emitted by this factory.</summary>
    internal static bool TryGet(Type itemType, out CarrierInfo carrier)
        => CarrierByType.TryGetValue(itemType, out carrier!);

    /// <summary>(Re)publishes the carrier metadata, e.g. after a <see cref="DataContextCache.Clear"/>.</summary>
    internal static void Publish(CarrierInfo carrier)
    {
        if (!DataContextCache.Metadata.TryGetValue(carrier.LinkType, out var registered) || registered is null)
        {
            lock (DataContextCache.MetadataRegistrationGate)
            {
                if (!DataContextCache.Metadata.ContainsKey(carrier.LinkType))
                    DataContextCache.Metadata[carrier.LinkType] = BuildMetadata(carrier.LinkType);
            }
        }
    }

    private static string BuildKey(IReadOnlyList<PropertyInfo> parentForeignKeys, IReadOnlyList<PropertyInfo> childForeignKeys)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var property in parentForeignKeys)
            builder.Append(property.PropertyType.FullName).Append(';');
        builder.Append('#');
        foreach (var property in childForeignKeys)
            builder.Append(property.PropertyType.FullName).Append(';');
        return builder.ToString();
    }

    private static CarrierInfo Emit(IReadOnlyList<PropertyInfo> parentForeignKeys, IReadOnlyList<PropertyInfo> childForeignKeys)
    {
        lock (EmitGate)
        {
            var typeBuilder = Module.DefineType(
                $"CompositeJunctionLink_{parentForeignKeys.Count}_{childForeignKeys.Count}_{Interlocked.Increment(ref _emittedTypeCounter)}",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit);

            var parentKeys = new PropertyInfo[parentForeignKeys.Count];
            var childKeys = new PropertyInfo[childForeignKeys.Count];

            for (var i = 0; i < parentForeignKeys.Count; i++)
                parentKeys[i] = DefineProperty(typeBuilder, $"ParentKey{i + 1}", parentForeignKeys[i].PropertyType);

            for (var i = 0; i < childForeignKeys.Count; i++)
                childKeys[i] = DefineProperty(typeBuilder, $"ChildKey{i + 1}", childForeignKeys[i].PropertyType);

            var occurrence = DefineProperty(typeBuilder, OccurrencePropertyName, typeof(int));

            var linkType = typeBuilder.CreateType()!;

            // Re-read the created properties so their declaring type is the closed emitted type.
            var resolvedParents = new PropertyInfo[parentKeys.Length];
            for (var i = 0; i < parentKeys.Length; i++)
                resolvedParents[i] = linkType.GetProperty(parentKeys[i].Name)!;

            var resolvedChildren = new PropertyInfo[childKeys.Length];
            for (var i = 0; i < childKeys.Length; i++)
                resolvedChildren[i] = linkType.GetProperty(childKeys[i].Name)!;

            var resolvedOccurrence = linkType.GetProperty(OccurrencePropertyName)!;
            return new CarrierInfo(linkType, resolvedParents, resolvedChildren, resolvedOccurrence);
        }
    }

    private static PropertyInfo DefineProperty(TypeBuilder typeBuilder, string name, Type propertyType)
    {
        var field = typeBuilder.DefineField($"_{char.ToLowerInvariant(name[0])}{name[1..]}", propertyType, FieldAttributes.Private);
        var property = typeBuilder.DefineProperty(name, PropertyAttributes.None, propertyType, null);

        var getter = typeBuilder.DefineMethod(
            $"get_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType,
            Type.EmptyTypes);
        var getIl = getter.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, field);
        getIl.Emit(OpCodes.Ret);

        var setter = typeBuilder.DefineMethod(
            $"set_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(void),
            [propertyType]);
        var setIl = setter.GetILGenerator();
        setIl.Emit(OpCodes.Ldarg_0);
        setIl.Emit(OpCodes.Ldarg_1);
        setIl.Emit(OpCodes.Stfld, field);
        setIl.Emit(OpCodes.Ret);

        property.SetGetMethod(getter);
        property.SetSetMethod(setter);
        return property;
    }

    private static IEntityMetadata BuildMetadata(Type linkType)
    {
        var builderType = typeof(EntityMetadataBuilder<>).MakeGenericType(linkType);
        var builder = Activator.CreateInstance(builderType)
            ?? throw new InvalidOperationException($"Cannot create the metadata builder for the composite link type '{linkType.Name}'.");
        var build = builderType.GetMethod(nameof(EntityMetadataBuilder<object>.Build), Type.EmptyTypes)
            ?? throw new InvalidOperationException($"The metadata builder for the composite link type '{linkType.Name}' has no parameterless Build method.");

        return build.Invoke(builder, BindingFlags.DoNotWrapExceptions, null, null, null) as IEntityMetadata
            ?? throw new InvalidOperationException($"Building the metadata for the composite link type '{linkType.Name}' returned no metadata.");
    }
}
