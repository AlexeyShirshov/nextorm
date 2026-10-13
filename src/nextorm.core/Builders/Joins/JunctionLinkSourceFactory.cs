using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Builds and caches the synthetic <see cref="JoinIntoLink{TParentKey,TChildKey}"/> source a many-to-many
/// <c>JoinInto</c> projects between the parent and the child, and registers its keyless mapping so the
/// denormalized pair command can expand the derived junction link exactly like any other entity-typed
/// projection item (see <c>QueryPreparer.TryExpandEntityItem</c>).
/// </summary>
/// <remarks>
/// The link type is not part of the user model: it is a closed generic built on demand for the two key
/// types of the relationship and mapped by convention (the auto-built mapping carries the three columns
/// <c>ParentKey</c>, <c>ChildKey</c> and <c>Occurrence</c>, and declares no key). Both the
/// <c>MakeGenericType</c> call and the built mapping are cached, so the same key-type pair never rebuilds
/// them per query. The mapping is (re)published into <see cref="DataContextCache.Metadata"/> on every
/// request, so a <c>DataContextCache.Clear()</c> that drops the configured cache is transparently healed
/// without rebuilding the metadata. The derived junction <c>FROM</c> join itself (the
/// <c>row_number() over (partition by fkP, fkC order by fkC)</c> subquery) is built by the many-to-many
/// <c>JoinInto</c> spec on top of this source.
/// </remarks>
internal static class JunctionLinkSourceFactory
{
    private static readonly ConcurrentDictionary<(Type ParentKey, Type ChildKey), Type> LinkTypeCache = new();
    private static readonly ConcurrentDictionary<(Type ParentKey, Type ChildKey), IEntityMetadata> MetadataCache = new();

    /// <summary>
    /// Returns the closed <see cref="JoinIntoLink{TParentKey,TChildKey}"/> type for the two junction key
    /// types, creating it once per pair and reusing it afterwards.
    /// </summary>
    /// <param name="parentKeyType">The CLR type of the parent-side key / junction parent foreign key.</param>
    /// <param name="childKeyType">The CLR type of the child-side key / junction child foreign key.</param>
    /// <returns>The closed link type.</returns>
    internal static Type GetLinkType(Type parentKeyType, Type childKeyType)
    {
        ArgumentNullException.ThrowIfNull(parentKeyType);
        ArgumentNullException.ThrowIfNull(childKeyType);

        return LinkTypeCache.GetOrAdd(
            (parentKeyType, childKeyType),
            static key => typeof(JoinIntoLink<,>).MakeGenericType(key.ParentKey, key.ChildKey));
    }

    /// <summary>
    /// Returns the keyless mapping of the closed <see cref="JoinIntoLink{TParentKey,TChildKey}"/> type,
    /// building it on first use and (re)publishing it into <see cref="DataContextCache.Metadata"/> so the
    /// projection expander resolves it.
    /// </summary>
    /// <param name="parentKeyType">The CLR type of the parent-side key / junction parent foreign key.</param>
    /// <param name="childKeyType">The CLR type of the child-side key / junction child foreign key.</param>
    /// <returns>The link type's entity metadata.</returns>
    internal static IEntityMetadata GetMetadata(Type parentKeyType, Type childKeyType)
    {
        ArgumentNullException.ThrowIfNull(parentKeyType);
        ArgumentNullException.ThrowIfNull(childKeyType);

        var linkType = GetLinkType(parentKeyType, childKeyType);
        var metadata = MetadataCache.GetOrAdd(
            (parentKeyType, childKeyType),
            static key => BuildMetadata(GetLinkType(key.ParentKey, key.ChildKey)));

        // Republish when the process-wide cache was cleared (or never had the entry): the cached
        // instance is reused, only the registration is redone. The check and the write run under the
        // shared registration gate, so a concurrent bridged import for the same key cannot be lost.
        if (!DataContextCache.Metadata.TryGetValue(linkType, out var registered) || !ReferenceEquals(registered, metadata))
        {
            lock (DataContextCache.MetadataRegistrationGate)
            {
                if (!DataContextCache.Metadata.TryGetValue(linkType, out registered) || !ReferenceEquals(registered, metadata))
                    DataContextCache.Metadata[linkType] = metadata;
            }
        }

        return metadata;
    }

    /// <summary>
    /// Returns the <c>FROM</c> source the in-memory engine uses for a many-to-many declaration: the
    /// junction entity itself (the derived <c>row_number()</c> link source is SQL-only; the in-memory
    /// join builders replace both edge conditions and From). The junction mapping is still (re)published,
    /// so declaring the declaration keeps the side effect the SQL path would have produced.
    /// </summary>
    /// <param name="relationship">The many-to-many relationship carrying the junction metadata.</param>
    /// <returns>The junction <c>FROM</c> source.</returns>
    internal static FromExpression GetInMemorySource(IRelationshipMetadata relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        var junction = relationship.Junction
            ?? throw new NotSupportedException(
                $"The relationship on '{relationship.DeclaringType.Name}' has no junction metadata; a many-to-many JoinInto needs it.");
        EnsureJunctionRegistered(junction.JunctionType);
        return new FromExpression(junction.JunctionType);
    }

    private static readonly MethodInfo RowNumberMethod =
        typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.row_number), Type.EmptyTypes)!;

    private static readonly MethodInfo OverMethod =
        typeof(WindowFunction<int>).GetMethod(
            nameof(WindowFunction<int>.Over),
            [typeof(Expression<Func<object?>>), typeof(Expression<Func<object?>>), typeof(WindowFrame)])!;

    private static readonly MethodInfo ArrayOverMethod =
        typeof(WindowFunction<int>).GetMethod(
            nameof(WindowFunction<int>.Over),
            [typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame)])!;

    private static readonly MethodInfo AscMethod =
        typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.asc), [typeof(Expression<Func<object?>>)])!;

    /// <summary>
    /// Builds the derived <c>row_number() over (partition by all junction foreign keys order by child
    /// foreign keys)</c> link source for a composite junction, projected onto the flat arity-general
    /// carrier (one scalar member per component plus occurrence).
    /// </summary>
    private static FromExpression BuildCompositeSource(IDataContext dataContext, RelationshipJunctionMetadata junction)
    {
        var parentForeignKeys = junction.JunctionParentForeignKey.Select(static p => p.PropertyInfo).ToArray();
        var childForeignKeys = junction.JunctionChildForeignKey.Select(static p => p.PropertyInfo).ToArray();
        var carrier = CompositeJunctionLinkFactory.Ensure(parentForeignKeys, childForeignKeys);

        var junctionType = junction.JunctionType;
        var junctionParameter = Expression.Parameter(junctionType, "j");

        var rowNumber = Expression.Call(Expression.Default(typeof(CommonFunctions)), RowNumberMethod);

        var partitionElements = new Expression[parentForeignKeys.Length + childForeignKeys.Length];
        var partitionIndex = 0;
        foreach (var foreignKey in parentForeignKeys)
            partitionElements[partitionIndex++] = Expression.Quote(BuildKeyLambda(junctionParameter, foreignKey));
        foreach (var foreignKey in childForeignKeys)
            partitionElements[partitionIndex++] = Expression.Quote(BuildKeyLambda(junctionParameter, foreignKey));

        var partition = Expression.NewArrayInit(typeof(Expression<Func<object?>>), partitionElements);

        var orderElements = new Expression[childForeignKeys.Length];
        for (var i = 0; i < childForeignKeys.Length; i++)
        {
            orderElements[i] = Expression.Call(
                Expression.Default(typeof(CommonFunctions)),
                AscMethod,
                Expression.Quote(BuildKeyLambda(junctionParameter, childForeignKeys[i])));
        }

        var order = Expression.NewArrayInit(typeof(WindowOrder), orderElements);

        var occurrence = Expression.Call(
            rowNumber,
            ArrayOverMethod,
            partition,
            order,
            Expression.Constant(null, typeof(WindowFrame)));

        var bindings = new List<MemberBinding>(parentForeignKeys.Length + childForeignKeys.Length + 1);
        for (var i = 0; i < parentForeignKeys.Length; i++)
            bindings.Add(Expression.Bind(carrier.ParentKeys[i], Expression.Property(junctionParameter, parentForeignKeys[i])));
        for (var i = 0; i < childForeignKeys.Length; i++)
            bindings.Add(Expression.Bind(carrier.ChildKeys[i], Expression.Property(junctionParameter, childForeignKeys[i])));
        bindings.Add(Expression.Bind(carrier.Occurrence, occurrence));

        var body = Expression.MemberInit(Expression.New(carrier.LinkType), bindings);

        var selector = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(junctionType, carrier.LinkType),
            body,
            junctionParameter);

        var command = dataContext.CreateCommand(carrier.LinkType, new QueryDefinition
        {
            Exp = selector,
            SrcType = junctionType,
            ProjectionType = carrier.LinkType,
        });
        command.From = ResolveJunctionSource(junctionType);

        return new FromExpression(command);
    }

    /// <summary>
    /// Builds the derived junction source a many-to-many <c>JoinInto</c> joins the parent to:
    /// <c>(SELECT fkP AS ParentKey, fkC AS ChildKey, row_number() over (partition by fkP, fkC order by fkC)
    /// AS Occurrence FROM &lt;junction&gt;)</c>. The subquery is a normal <see cref="QueryCommand"/> over the
    /// junction entity projected onto the synthetic <see cref="JoinIntoLink{TParentKey,TChildKey}"/> shape,
    /// so the outer pair command expands the link item from the link metadata exactly like any other
    /// entity item.
    /// </summary>
    /// <param name="dataContext">The context that executes the outer command (and the derived one).</param>
    /// <param name="relationship">The many-to-many relationship carrying the junction metadata.</param>
    /// <returns>The derived-table source of the link join.</returns>
    /// <exception cref="NotSupportedException">The relationship carries no junction metadata.</exception>
    internal static FromExpression BuildSource(IDataContext dataContext, IRelationshipMetadata relationship)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(relationship);

        var junction = relationship.Junction
            ?? throw new NotSupportedException(
                $"The relationship on '{relationship.DeclaringType.Name}' has no junction metadata; a many-to-many JoinInto needs it.");

        // A junction whose parent and/or child foreign key declares more than one component builds the
        // same derived row_number() link source over the flat, arity-general composite carrier (one scalar
        // member per component plus occurrence); a scalar junction keeps the two-member JoinIntoLink shape.
        if (junction.JunctionParentForeignKey.Count != 1 || junction.JunctionChildForeignKey.Count != 1)
            return BuildCompositeSource(dataContext, junction);

        var junctionType = junction.JunctionType;
        var parentForeignKey = junction.JunctionParentForeignKey[0].PropertyInfo;
        var childForeignKey = junction.JunctionChildForeignKey[0].PropertyInfo;
        var parentKeyType = parentForeignKey.PropertyType;
        var childKeyType = childForeignKey.PropertyType;

        var linkType = GetLinkType(parentKeyType, childKeyType);
        GetMetadata(parentKeyType, childKeyType);

        var junctionParameter = Expression.Parameter(junctionType, "j");
        var linkParentKey = linkType.GetProperty(nameof(JoinIntoLink<object, object>.ParentKey))!;
        var linkChildKey = linkType.GetProperty(nameof(JoinIntoLink<object, object>.ChildKey))!;
        var linkOccurrence = linkType.GetProperty(nameof(JoinIntoLink<object, object>.Occurrence))!;

        var rowNumber = Expression.Call(Expression.Default(typeof(CommonFunctions)), RowNumberMethod);
        var occurrence = Expression.Call(
            rowNumber,
            OverMethod,
            Expression.Quote(BuildKeyLambda(junctionParameter, parentForeignKey)),
            Expression.Quote(BuildKeyLambda(junctionParameter, childForeignKey)),
            Expression.Constant(null, typeof(WindowFrame)));

        var body = Expression.MemberInit(
            Expression.New(linkType),
            Expression.Bind(linkParentKey, Expression.Property(junctionParameter, parentForeignKey)),
            Expression.Bind(linkChildKey, Expression.Property(junctionParameter, childForeignKey)),
            Expression.Bind(linkOccurrence, occurrence));

        var selector = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(junctionType, linkType),
            body,
            junctionParameter);

        var junctionSource = ResolveJunctionSource(junctionType);

        var command = dataContext.CreateCommand(linkType, new QueryDefinition
        {
            Exp = selector,
            SrcType = junctionType,
            ProjectionType = linkType,
        });
        command.From = junctionSource;

        return new FromExpression(command);
    }

    /// <summary>
    /// Resolves the <c>FROM</c> source of the junction table. The relationship resolver maps the
    /// junction through its auto-build path, which keeps the result in the separate TVP cache, so the
    /// mapping is republished into <see cref="DataContextCache.Metadata"/> (the cache the SQL builder
    /// reads for the derived command's columns) when it is not there yet. A configured junction mapping
    /// already present always wins; this makes a many-to-many <c>JoinInto</c> work without a separate
    /// explicit <c>From&lt;TJunction&gt;()</c> registration.
    /// </summary>
    /// <param name="junctionType">The junction entity type.</param>
    /// <returns>The junction <c>FROM</c> source, mapped to its table when one is known.</returns>
    private static FromExpression ResolveJunctionSource(Type junctionType)
    {
        if (!DataContextCache.Metadata.TryGetValue(junctionType, out var metadata) || string.IsNullOrEmpty(metadata.TableName))
        {
            metadata = DataContextExtensions.ResolveMetadata(null, junctionType);

            lock (DataContextCache.MetadataRegistrationGate)
            {
                // Revalidate under the shared registration gate: a concurrent writer (including the EF
                // Core bridge) may have published a non-empty mapping while the auto mapping was built.
                // A published entry wins and is not marked auto, so a later configured registration
                // still rebuilds it.
                if (DataContextCache.Metadata.TryGetValue(junctionType, out var current) && !string.IsNullOrEmpty(current.TableName))
                {
                    metadata = current;
                }
                else
                {
                    DataContextCache.Metadata[junctionType] = metadata;

                    // The entry came from the auto path (a configured non-empty mapping would have
                    // been found above); mark it so a later From<TJunction>(cfg) can rebuild it from
                    // the configuration.
                    DataContextCache.AutoPublishedJunctionMetadata[junctionType] = 0;
                }
            }
        }

        return !string.IsNullOrEmpty(metadata.TableName)
            ? new FromExpression(metadata.TableName!, metadata.IsTableNameAuto, junctionType.IsInterface)
            : new FromExpression(junctionType);
    }

    /// <summary>
    /// Re-publishes the auto-built mapping of a junction entity before the outer pair command is
    /// prepared, so a <see cref="DataContextCache.Clear"/> between declaration and execution cannot
    /// leave the derived link subquery without its junction metadata. Mirrors
    /// <see cref="EnsureRegistered"/> for the junction type (which the link is derived from).
    /// </summary>
    /// <param name="junctionType">The junction entity type of a many-to-many declaration.</param>
    internal static void EnsureJunctionRegistered(Type junctionType)
    {
        ArgumentNullException.ThrowIfNull(junctionType);
        _ = ResolveJunctionSource(junctionType);
    }

    /// <summary>
    /// Re-publishes the keyless mapping of a synthetic link item type before the outer pair command is
    /// prepared, so a <see cref="DataContextCache.Clear"/> between declaration and execution cannot leave
    /// the link unregistered. Non-link item types are ignored.
    /// </summary>
    /// <param name="itemType">A projection item type of the pair command.</param>
    internal static void EnsureRegistered(Type itemType)
    {
        if (CompositeJunctionLinkFactory.TryGet(itemType, out var carrier))
        {
            CompositeJunctionLinkFactory.Publish(carrier);
            return;
        }

        if (!itemType.IsGenericType || itemType.GetGenericTypeDefinition() != typeof(JoinIntoLink<,>))
            return;

        var arguments = itemType.GetGenericArguments();
        GetMetadata(arguments[0], arguments[1]);
    }

    private static LambdaExpression BuildKeyLambda(ParameterExpression junction, PropertyInfo property)
        => Expression.Lambda<Func<object?>>(
            Expression.Convert(Expression.Property(junction, property), typeof(object)));

    private static IEntityMetadata BuildMetadata(Type linkType)
    {
        var builderType = typeof(EntityMetadataBuilder<>).MakeGenericType(linkType);
        var builder = Activator.CreateInstance(builderType)
            ?? throw new InvalidOperationException(
                $"Cannot create the metadata builder for the synthetic junction link type '{linkType.Name}'.");
        var build = builderType.GetMethod(nameof(EntityMetadataBuilder<object>.Build), Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"The metadata builder for the synthetic junction link type '{linkType.Name}' has no parameterless Build method.");

        // DoNotWrapExceptions surfaces a mapping error as its own type, matching the strongly typed path.
        return build.Invoke(builder, BindingFlags.DoNotWrapExceptions, null, null, null) as IEntityMetadata
            ?? throw new InvalidOperationException(
                $"Building the metadata for the synthetic junction link type '{linkType.Name}' returned no metadata.");
    }
}
