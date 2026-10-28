using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// The navigation shape an additive, metadata-driven <c>LoadWith</c> call selected: the collection
/// element form or the single-reference form. A collection navigation must never be bound through the
/// reference route and vice versa.
/// </summary>
internal enum MetadataEagerLoadRoute
{
    /// <summary>A collection navigation (<see cref="RelationshipKind.OneToMany"/> or <see cref="RelationshipKind.ManyToMany"/>).</summary>
    Collection,

    /// <summary>A reference navigation (<see cref="RelationshipKind.ManyToOne"/> or <see cref="RelationshipKind.OneToOne"/>).</summary>
    Reference,
}

/// <summary>
/// One positional join-key component pair of a declared relationship: the component read from the
/// entity being loaded from (the parent side) and the component read from the related entity (the child
/// side). Position is the contract: component <c>i</c> pairs with component <c>i</c> of the other side,
/// and only the mapped CLR types are compared, never the property names.
/// </summary>
internal sealed class MetadataEagerLoadKeyComponent
{
    /// <summary>Initializes a positional component pair.</summary>
    /// <param name="parent">The parent-side component.</param>
    /// <param name="child">The child-side component at the same position.</param>
    public MetadataEagerLoadKeyComponent(PropertyInfo parent, PropertyInfo child)
    {
        Parent = parent;
        Child = child;
    }

    /// <summary>The component read from the entity being loaded from.</summary>
    public PropertyInfo Parent { get; }

    /// <summary>The component read from the related entity, paired with <see cref="Parent"/> by position.</summary>
    public PropertyInfo Child { get; }

    /// <summary>The <c>parent-&gt;child</c> rendering used in diagnostics and the structural shape.</summary>
    public override string ToString() => Parent.Name + "->" + Child.Name;
}

/// <summary>
/// Marker over the metadata-driven declaration, so the preparation pipeline can distinguish the SQL-only
/// additive <c>LoadWith</c> form from the legacy explicit-key specification without reflecting over the
/// generic type. No members: the capability decision only needs the declaration kind.
/// </summary>
internal interface IMetadataEagerLoadSpec;

/// <summary>
/// The canonical plan identity of one metadata relationship: the declaring/parent type, the related/child
/// type, the navigation member, the declared cardinality, the collection/reference route and the ordered
/// parent-&gt;child component list (with each component's declaring type, so inherited members and
/// shadowed names stay distinct). It is the value that makes two semantically identical declarations —
/// the inferred-metadata form and an explicit-key form selecting the same declared relationship — key the
/// same plan entry, and makes any result-affecting difference (component order, component set, navigation,
/// cardinality) a different entry. It carries no runtime value, captured context, parameter or command.
/// </summary>
internal static class MetadataEagerLoadPlanIdentity
{
    /// <summary>Builds the canonical identity string for one validated relationship declaration.</summary>
    /// <param name="parentType">The entity the navigation is declared on (the loading parent).</param>
    /// <param name="childType">The related entity type.</param>
    /// <param name="navigation">The selected navigation member.</param>
    /// <param name="kind">The declared relationship kind.</param>
    /// <param name="route">The collection/reference route the declaration took.</param>
    /// <param name="components">The ordered parent-to-child component pairs.</param>
    /// <returns>The canonical identity string.</returns>
    public static string Build(
        Type parentType,
        Type childType,
        PropertyInfo navigation,
        RelationshipKind kind,
        MetadataEagerLoadRoute route,
        IReadOnlyList<MetadataEagerLoadKeyComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var parentComponents = new PropertyInfo[components.Count];
        var childComponents = new PropertyInfo[components.Count];
        for (var i = 0; i < components.Count; i++)
        {
            parentComponents[i] = components[i].Parent;
            childComponents[i] = components[i].Child;
        }

        return Build(parentType, childType, navigation, kind, route, parentComponents, childComponents);
    }

    /// <summary>
    /// Builds the canonical identity string from the two independent key legs. The legs are positional
    /// within themselves; a <see cref="RelationshipKind.OneToMany"/> or reference relationship pairs them
    /// one-to-one (and renders the pair form, identical to the component-pair overload), while a
    /// <see cref="RelationshipKind.ManyToMany"/> relationship maps each leg independently onto the junction
    /// and the two legs need not have the same arity.
    /// </summary>
    /// <param name="parentType">The entity the navigation is declared on (the loading parent).</param>
    /// <param name="childType">The related entity type.</param>
    /// <param name="navigation">The selected navigation member.</param>
    /// <param name="kind">The declared relationship kind.</param>
    /// <param name="route">The collection/reference route the declaration took.</param>
    /// <param name="parentComponents">The ordered parent-side key components.</param>
    /// <param name="childComponents">The ordered child-side key components.</param>
    /// <returns>The canonical identity string.</returns>
    public static string Build(
        Type parentType,
        Type childType,
        PropertyInfo navigation,
        RelationshipKind kind,
        MetadataEagerLoadRoute route,
        IReadOnlyList<PropertyInfo> parentComponents,
        IReadOnlyList<PropertyInfo> childComponents)
    {
        ArgumentNullException.ThrowIfNull(parentType);
        ArgumentNullException.ThrowIfNull(childType);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(parentComponents);
        ArgumentNullException.ThrowIfNull(childComponents);

        var identity = new System.Text.StringBuilder(160);
        identity.Append("P=").Append(parentType.FullName ?? parentType.Name);
        identity.Append(";C=").Append(childType.FullName ?? childType.Name);
        identity.Append(";M=").Append(navigation.DeclaringType?.FullName ?? parentType.FullName).Append('.').Append(navigation.Name);
        identity.Append(";R=").Append(kind);
        identity.Append(";RT=").Append(route);
        identity.Append(";KY=");

        if (parentComponents.Count == childComponents.Count)
        {
            for (var i = 0; i < parentComponents.Count; i++)
            {
                if (i > 0)
                    identity.Append(',');

                AppendComponent(identity, parentComponents[i], parentType);
                identity.Append("->");
                AppendComponent(identity, childComponents[i], childType);
            }
        }
        else
        {
            // Independent legs (many-to-many): the parent leg is not paired with the child leg, so each
            // ordered list is rendered on its own side of the arrow.
            for (var i = 0; i < parentComponents.Count; i++)
            {
                if (i > 0)
                    identity.Append(',');

                AppendComponent(identity, parentComponents[i], parentType);
            }

            identity.Append("->");

            for (var i = 0; i < childComponents.Count; i++)
            {
                if (i > 0)
                    identity.Append(',');

                AppendComponent(identity, childComponents[i], childType);
            }
        }

        return identity.ToString();
    }

    private static void AppendComponent(System.Text.StringBuilder identity, PropertyInfo component, Type entityType)
        => identity.Append(component.DeclaringType?.FullName ?? entityType.FullName)
            .Append('.').Append(component.Name);
}

/// <summary>
/// A fully resolved, validated metadata binding of one additive <c>LoadWith</c> declaration: the declared
/// relationship, the navigation it was selected by, the cardinality route and the ordered key component
/// pairs. Construction is the demand-time validation point (<see cref="RelationshipResolver.ValidateRelationship"/>),
/// so a malformed declaration fails before any SQL is built and before a plan is cached.
/// </summary>
/// <typeparam name="TEntity">The entity being loaded from.</typeparam>
/// <typeparam name="TChild">The related entity type.</typeparam>
internal sealed class MetadataEagerLoadBinding<TEntity, TChild>
{
    /// <summary>Initializes the binding from an already validated relationship.</summary>
    /// <param name="relationship">The declared relationship.</param>
    /// <param name="navigation">The selected navigation property.</param>
    /// <param name="route">The cardinality route the caller selected.</param>
    /// <param name="parentComponents">The ordered parent-side key components.</param>
    /// <param name="childComponents">The ordered child-side key components.</param>
    /// <param name="requestedMode">The requested eager-load mode.</param>
    /// <param name="form">The exact overload form, used in diagnostics.</param>
    public MetadataEagerLoadBinding(
        IRelationshipMetadata relationship,
        PropertyInfo navigation,
        MetadataEagerLoadRoute route,
        IReadOnlyList<PropertyInfo> parentComponents,
        IReadOnlyList<PropertyInfo> childComponents,
        EagerLoadMode requestedMode,
        string form)
    {
        Relationship = relationship;
        Navigation = navigation;
        Route = route;
        ParentComponents = parentComponents;
        ChildComponents = childComponents;
        KeyComponents = BuildComponentPairs(parentComponents, childComponents);
        ParentKeyExpression = JoinIntoSpecHelpers.BuildKeySelector<TEntity>(parentComponents);
        ChildKeyExpression = JoinIntoSpecHelpers.BuildKeySelector<TChild>(childComponents);
        RequestedMode = requestedMode;
        Form = form;
    }

    private static IReadOnlyList<MetadataEagerLoadKeyComponent> BuildComponentPairs(
        IReadOnlyList<PropertyInfo> parentComponents,
        IReadOnlyList<PropertyInfo> childComponents)
    {
        if (parentComponents.Count != childComponents.Count)
            return [];

        var pairs = new MetadataEagerLoadKeyComponent[parentComponents.Count];
        for (var i = 0; i < pairs.Length; i++)
            pairs[i] = new MetadataEagerLoadKeyComponent(parentComponents[i], childComponents[i]);

        return pairs;
    }

    /// <summary>The declared relationship supplying the cardinality and the ordered keys.</summary>
    public IRelationshipMetadata Relationship { get; }

    /// <summary>The navigation property the caller selected.</summary>
    public PropertyInfo Navigation { get; }

    /// <summary>The cardinality route the caller selected.</summary>
    public MetadataEagerLoadRoute Route { get; }

    /// <summary>Whether the selected route is the collection route.</summary>
    public bool IsCollection => Route == MetadataEagerLoadRoute.Collection;

    /// <summary>Whether the selected route is the reference route.</summary>
    public bool IsReference => Route == MetadataEagerLoadRoute.Reference;

    /// <summary>The related entity type the navigation points to (validated against <typeparamref name="TChild"/>).</summary>
    public Type RelatedType => Relationship.RelatedType;

    /// <summary>
    /// The ordered parent-to-child key component pairs, in declaration/selector order. Empty for a
    /// many-to-many relationship whose two key legs have different arities: those legs are independent and
    /// are carried by <see cref="ParentComponents"/> and <see cref="ChildComponents"/>.
    /// </summary>
    public IReadOnlyList<MetadataEagerLoadKeyComponent> KeyComponents { get; }

    /// <summary>The ordered parent-side key components, in declaration/selector order.</summary>
    public IReadOnlyList<PropertyInfo> ParentComponents { get; }

    /// <summary>
    /// The ordered child-side key components, in declaration/selector order. For a many-to-many
    /// relationship this leg is independent of <see cref="ParentComponents"/> and is matched against the
    /// junction's child foreign key; for every other kind it pairs one-to-one with the parent leg.
    /// </summary>
    public IReadOnlyList<PropertyInfo> ChildComponents { get; }

    /// <summary>The boxed composite parent-key selector built from <see cref="ParentComponents"/>.</summary>
    public Expression<Func<TEntity, object>> ParentKeyExpression { get; }

    /// <summary>The boxed composite child-key selector built from <see cref="ChildComponents"/>.</summary>
    public Expression<Func<TChild, object>> ChildKeyExpression { get; }

    /// <summary>The requested eager-load mode.</summary>
    public EagerLoadMode RequestedMode { get; }

    /// <summary>The exact overload form, used in diagnostics.</summary>
    public string Form { get; }

    /// <summary>
    /// The structural fingerprint of this declaration: parent/child types, the navigation member, the
    /// declared kind/cardinality route and every ordered component pair (with its declaring type). Two
    /// declarations that differ in cardinality, route, component order/set or navigation therefore cannot
    /// share a cached eager graph; semantically identical ones (the explicit-key form and the
    /// inferred-metadata form of the same relationship) produce the identical value.
    /// </summary>
    public string ShapeKey =>
        MetadataEagerLoadPlanIdentity.Build(
            typeof(TEntity), typeof(TChild), Navigation, Relationship.Kind, Route, ParentComponents, ChildComponents);
}

/// <summary>
/// Resolves the declared relationship behind an additive, metadata-driven <c>LoadWith</c> navigation and
/// builds the validated binding. The consumer never infers a relationship: an undeclared navigation, a
/// cardinality mismatch, a related-type mismatch or a malformed key list fails closed here, before any
/// SQL is built and before the plan cache is touched.
/// </summary>
internal static class MetadataEagerLoadResolver
{
    /// <summary>
    /// Resolves and validates the binding for <paramref name="navigation"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity being loaded from.</typeparam>
    /// <typeparam name="TChild">The related entity type selected by the navigation expression.</typeparam>
    /// <param name="dataProvider">The context that owns the parent query.</param>
    /// <param name="navigation">The navigation-selecting lambda.</param>
    /// <param name="route">The cardinality route the caller selected.</param>
    /// <param name="requestedMode">The requested eager-load mode.</param>
    /// <param name="form">The exact overload form, used in diagnostics.</param>
    /// <returns>The validated binding.</returns>
    /// <exception cref="NotSupportedException">
    /// The selector is not a mapping navigation, no relationship is declared for it, the declared
    /// relationship's cardinality or related type does not match the chosen overload, or its key lists
    /// are structurally invalid.
    /// </exception>
    public static MetadataEagerLoadBinding<TEntity, TChild> Resolve<TEntity, TChild>(
        IDataContext dataProvider,
        LambdaExpression navigation,
        MetadataEagerLoadRoute route,
        EagerLoadMode requestedMode,
        string form)
    {
        ArgumentNullException.ThrowIfNull(dataProvider);
        ArgumentNullException.ThrowIfNull(navigation);

        var property = StripConvert(navigation.Body) as MemberExpression is { Member: PropertyInfo member }
            ? member
            : throw new NotSupportedException(
                $"{form} requires the navigation selector to be a property access so the declared relationship can be resolved.");

        ValidateNavigationShape<TChild>(property, route, form);

        var relationship = FindDeclaredRelationship(dataProvider, typeof(TEntity), property)
            ?? throw new NotSupportedException(
                $"{form} cannot load '{typeof(TEntity).Name}.{property.Name}': no relationship is declared for that navigation. " +
                "Declare it with HasMany/HasOne/HasOneToOne/HasManyThrough or [Relationship]; LoadWith does not infer relationships from conventions.");

        // Cardinality is validated before the related-type check so a collection navigation selected
        // through the reference route (or the reverse) reports the route mistake, not a type mismatch.
        ValidateCardinality(relationship, route, form);

        if (relationship.RelatedType != typeof(TChild))
            throw new NotSupportedException(
                $"{form} loads '{typeof(TEntity).Name}.{property.Name}' as '{typeof(TChild).Name}', but the declared relationship " +
                $"'{Describe(relationship)}' points to '{relationship.RelatedType.Name}'.");

        // Demand-time structural validation, before either key list is consumed: a hand-written
        // IRelationshipMetadata has no lazy guard of its own, and a built-in one validates on first read.
        // Nothing is cached before this returns, so a rejected mapping can never reach SQL or a plan.
        RelationshipResolver.ValidateRelationship(relationship);

        var (parentComponents, childComponents) = MapComponents(relationship);
        if (parentComponents.Count == 0 || childComponents.Count == 0)
            throw new NotSupportedException(
                $"{form} cannot load '{Describe(relationship)}': the relationship declares no key components.");

        // A many-to-many relationship maps each leg independently onto the junction (parent key -> junction
        // parent foreign key, child key -> junction child foreign key), so the two legs are free to differ in
        // arity. Every other kind pairs the principal and foreign key components one-to-one, and
        // RelationshipResolver.ValidateRelationship already enforces that; keep the guard for hand-written
        // metadata, which has no lazy validation of its own.
        if (relationship.Kind != RelationshipKind.ManyToMany && parentComponents.Count != childComponents.Count)
            throw new NotSupportedException(
                $"{form} cannot load '{Describe(relationship)}': the relationship declares {parentComponents.Count} " +
                $"parent-side component(s) but {childComponents.Count} child-side component(s); the arities must match.");

        return new MetadataEagerLoadBinding<TEntity, TChild>(
            relationship,
            property,
            route,
            parentComponents,
            childComponents,
            requestedMode,
            form);
    }

    /// <summary>
    /// Resolves the plan identity of an explicit-key <c>LoadWith</c> declaration that is semantically the
    /// inferred-metadata declaration of a declared relationship. The legacy overload must never gain a
    /// metadata failure mode, so a missing declaration, a different related type/cardinality, a composite
    /// or non-member key, or malformed metadata keeps the legacy identity: this probe never throws and
    /// returns <see langword="null"/> instead, leaving the explicit-key shape and behaviour untouched.
    /// </summary>
    /// <typeparam name="TEntity">The entity being loaded from.</typeparam>
    /// <typeparam name="TChild">The related entity type.</typeparam>
    /// <typeparam name="TKey">The explicit key type shared by both selectors.</typeparam>
    /// <param name="dataProvider">The context that owns the parent query.</param>
    /// <param name="collection">The parent-side collection member.</param>
    /// <param name="parentKey">The explicit parent key selector.</param>
    /// <param name="childKey">The explicit child key selector.</param>
    /// <returns>
    /// The canonical metadata plan identity when the declaration selects exactly the declared OneToMany
    /// relationship of that navigation, otherwise <see langword="null"/>.
    /// </returns>
    internal static string? TryResolveEquivalentPlanShape<TEntity, TChild, TKey>(
        IDataContext dataProvider,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey)
    {
        try
        {
            if (StripConvert(collection.Body) is not MemberExpression { Member: PropertyInfo navigation })
                return null;

            var relationship = FindDeclaredRelationship(dataProvider, typeof(TEntity), navigation);
            if (relationship is null
                || relationship.Kind != RelationshipKind.OneToMany
                || relationship.RelatedType != typeof(TChild))
                return null;

            RelationshipResolver.ValidateRelationship(relationship);

            var (parentComponents, childComponents) = MapComponents(relationship);

            // The legacy overload carries one typed key value, so only a one-component relationship can be
            // expressed by it. A composite declaration stays on the legacy identity (the explicit API gains
            // no composite promise); a selector that is not exactly the mapped member is not equivalent.
            if (parentComponents.Count != 1 || childComponents.Count != 1)
                return null;

            if (typeof(TKey) != parentComponents[0].PropertyType || typeof(TKey) != childComponents[0].PropertyType)
                return null;

            if (!SelectsMember(parentKey.Body, parentComponents[0]) || !SelectsMember(childKey.Body, childComponents[0]))
                return null;

            MetadataEagerLoadKeyComponent[] components =
                [new MetadataEagerLoadKeyComponent(parentComponents[0], childComponents[0])];

            return MetadataEagerLoadPlanIdentity.Build(
                typeof(TEntity), typeof(TChild), navigation, relationship.Kind, MetadataEagerLoadRoute.Collection, components);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            // The explicit-key path is the compatibility control: an optional metadata probe can never turn
            // a previously valid legacy declaration into a failure. It simply keeps the legacy identity.
            return null;
        }
    }

    private static bool SelectsMember(Expression body, PropertyInfo expected)
        => StripConvert(body) is MemberExpression { Member: PropertyInfo selected } && SameMember(selected, expected);

    /// <summary>
    /// Rejects a selected member that cannot be a navigation of the chosen route before any metadata is
    /// consulted: a value type or <see cref="string"/> is never a reference navigation, and a collection
    /// route member must be a collection of <typeparamref name="TChild"/>.
    /// </summary>
    private static void ValidateNavigationShape<TChild>(PropertyInfo property, MetadataEagerLoadRoute route, string form)
    {
        if (route == MetadataEagerLoadRoute.Collection)
        {
            if (!typeof(ICollection<TChild>).IsAssignableFrom(property.PropertyType))
                throw new NotSupportedException(
                    $"{form} requires the selector to be a collection of '{typeof(TChild).Name}', but '{property.Name}' is of type " +
                    $"'{property.PropertyType.Name}'. Use the reference overload for a single navigation.");
            return;
        }

        if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
            throw new NotSupportedException(
                $"{form} requires a reference navigation, but '{property.Name}' is of type '{property.PropertyType.Name}', " +
                "which is not a reference navigation.");

        // A nullable annotation does not change the runtime type; an inherited/interface navigation may
        // be typed as a base of the related entity, so assignability (not equality) is the right test.
        if (!typeof(TChild).IsAssignableFrom(property.PropertyType) && !property.PropertyType.IsAssignableFrom(typeof(TChild)))
            throw new NotSupportedException(
                $"{form} loads '{property.Name}' as '{typeof(TChild).Name}', but the navigation property is of type " +
                $"'{property.PropertyType.Name}'.");
    }

    /// <summary>
    /// Whether a relationship is declared for the navigation member (#249 / T353). A projection slot may only be
    /// recognised on a declared navigation: a scalar collection column (<c>byte[]</c>, an array or range-returning
    /// SQL function) is an ordinary projected value and must never be taken for, or refused as, a slot.
    /// </summary>
    /// <param name="dataProvider">The context that owns the projection.</param>
    /// <param name="entityType">The projected entity type.</param>
    /// <param name="property">The projected member.</param>
    /// <returns><see langword="true"/> when the member is a declared navigation.</returns>
    internal static bool HasDeclaredRelationship(IDataContext dataProvider, Type entityType, PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(dataProvider);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(property);

        return FindDeclaredRelationship(dataProvider, entityType, property) is not null;
    }

    /// <summary>
    /// Returns the declared relationship kind of <paramref name="property"/> on <paramref name="entityType"/>,
    /// or <see langword="null"/> when no relationship is declared for it. Used by projection-slot recognition,
    /// which must tell a collection navigation from a reference one before binding either.
    /// </summary>
    /// <param name="dataProvider">The context that owns the query.</param>
    /// <param name="entityType">The entity that declares the navigation.</param>
    /// <param name="property">The navigation property.</param>
    /// <returns>The declared relationship kind, or <see langword="null"/>.</returns>
    internal static RelationshipKind? DeclaredRelationshipKind(IDataContext dataProvider, Type entityType, PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(dataProvider);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(property);

        return FindDeclaredRelationship(dataProvider, entityType, property)?.Kind;
    }

    /// <summary>
    /// Finds the relationship whose declared navigation is <paramref name="property"/>. The entity's own
    /// metadata is searched first; a navigation inherited from a base type then falls back to the already
    /// registered declaring-type metadata (an auto-built entry is never published by the fallback, so a
    /// later configured registration of the base type is not shadowed). Matching is by declared
    /// navigation only, so two navigations to the same related type (buyer/seller) stay distinct.
    /// </summary>
    private static IRelationshipMetadata? FindDeclaredRelationship(IDataContext dataProvider, Type entityType, PropertyInfo property)
    {
        var metadata = DataContextExtensions.ResolveMetadata(dataProvider, entityType);
        var match = Match(metadata, property);
        if (match is not null)
            return match;

        if (property.DeclaringType is not { } declaringType
            || declaringType == entityType
            || !declaringType.IsAssignableFrom(entityType))
            return null;

        return DataContextCache.Metadata.TryGetValue(declaringType, out var declaringMetadata)
               && !string.IsNullOrEmpty(declaringMetadata.TableName)
            ? Match(declaringMetadata, property)
            : null;
    }

    private static IRelationshipMetadata? Match(IEntityMetadata metadata, PropertyInfo property)
    {
        for (var i = 0; i < metadata.Relationships.Count; i++)
        {
            var relationship = metadata.Relationships[i];
            if (relationship.Navigation is { } navigation && SameMember(navigation, property))
                return relationship;
        }

        return null;
    }

    /// <summary>
    /// Validates that the declared cardinality agrees with itself and with the chosen overload: the
    /// collection route accepts only a declared collection (OneToMany/ManyToMany), the reference route
    /// only a declared reference (ManyToOne/OneToOne).
    /// </summary>
    private static void ValidateCardinality(IRelationshipMetadata relationship, MetadataEagerLoadRoute route, string form)
    {
        var declaredCollection = relationship.Kind is RelationshipKind.OneToMany or RelationshipKind.ManyToMany;
        if (relationship.IsCollection != declaredCollection)
            throw new NotSupportedException(
                $"{form} cannot load '{Describe(relationship)}': the metadata is inconsistent — kind {relationship.Kind} " +
                $"reports IsCollection={relationship.IsCollection}, but the kind is {(declaredCollection ? "a collection" : "a reference")} relationship.");

        if ((route == MetadataEagerLoadRoute.Collection) != declaredCollection)
        {
            throw new NotSupportedException(
                route == MetadataEagerLoadRoute.Collection
                    ? $"{form} requires a collection navigation, but the relationship '{Describe(relationship)}' is {relationship.Kind}; " +
                      "use the reference overload for a single navigation."
                    : $"{form} requires a reference navigation, but the relationship '{Describe(relationship)}' is {relationship.Kind}; " +
                      "use the collection overload for a collection navigation.");
        }
    }

    /// <summary>
    /// Maps the relationship's ordered key lists onto parent/child entity roles. For a many-to-one
    /// navigation the declaring entity is the dependent, so its foreign key is the parent side; for
    /// every other kind the declaring entity is the principal/owning side and the related entity carries
    /// the foreign key. Component <c>i</c> always pairs with component <c>i</c> — except for a
    /// many-to-many relationship, where the parent-side principal key and the child-side key are two
    /// independent legs matched through the junction and therefore need not have the same arity.
    /// </summary>
    private static (IReadOnlyList<PropertyInfo> Parent, IReadOnlyList<PropertyInfo> Child) MapComponents(IRelationshipMetadata relationship)
    {
        var foreign = relationship.ForeignKey;
        var principal = relationship.PrincipalKey;

        var parent = new PropertyInfo[principal.Count];
        var child = new PropertyInfo[foreign.Count];

        if (relationship.Kind == RelationshipKind.ManyToOne)
        {
            // Declaring = dependent (foreign key), related = principal key.
            for (var i = 0; i < foreign.Count; i++)
                parent[i] = foreign[i].PropertyInfo;
            for (var i = 0; i < principal.Count; i++)
                child[i] = principal[i].PropertyInfo;
        }
        else
        {
            // Declaring = principal/owning side, related = dependent (foreign key). Many-to-many exposes
            // the parent-side key as PrincipalKey and the child-side key as ForeignKey.
            for (var i = 0; i < principal.Count; i++)
                parent[i] = principal[i].PropertyInfo;
            for (var i = 0; i < foreign.Count; i++)
                child[i] = foreign[i].PropertyInfo;
        }

        return (parent, child);
    }

    private static string Describe(IRelationshipMetadata relationship)
        => relationship.Navigation is { } navigation
            ? $"{relationship.DeclaringType.Name}.{navigation.Name}"
            : $"{relationship.DeclaringType.Name} -> {relationship.RelatedType.Name}";

    private static bool SameMember(PropertyInfo left, PropertyInfo right)
        => left.Equals(right) || (left.Name == right.Name && left.DeclaringType == right.DeclaringType);

    internal static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;

        return expression;
    }
}

/// <summary>
/// The metadata-driven eager-load specification: the validated binding (ordered component pairs and
/// cardinality) plus the child-query source. The collection route reuses the proven eager machinery
/// through boxed composite selectors, with the per-component split predicate and the parameter-aware
/// chunk policy supplied by <see cref="MetadataEagerLoadKeyPlan{TEntity,TChild}"/>; the reference route
/// buffers the child rows and enforces the full-identity cardinality rule; the many-to-many route reads
/// the junction and the children in two batched commands and preserves every valid link occurrence. The
/// child factory is resolved at most once per context through this declaration's memo.
/// </summary>
/// <typeparam name="TEntity">The entity being loaded from.</typeparam>
/// <typeparam name="TChild">The related entity type.</typeparam>
internal sealed class MetadataEagerLoadSpec<TEntity, TChild> : IEagerLoadSpec<TEntity>, IEagerSingleQueryStitch<TEntity>, IMetadataEagerLoadSpec
{
    private readonly Func<IDataContext, EntityBuilder<TChild>> _childQuery;
    private readonly ConditionalWeakTable<IDataContext, Lazy<EntityBuilder<TChild>>> _resolvedChildren = new();
    // The many-to-many single-query layout (link readers plus assignment) built while planning, so the
    // stitcher can reuse the flat declaration's link-aware assignment contract instead of rebuilding it.
    private readonly ConditionalWeakTable<IDataContext, IJoinIntoManyToManySpec> _manyToManyLayouts = new();
    private readonly MetadataEagerLoadKeyPlan<TEntity, TChild> _keyPlan;
    private readonly Expression<Func<TEntity, ICollection<TChild>>>? _collectionExpression;
    private readonly EagerLoadSpec<TEntity, TChild, object>? _collection;
    private readonly Func<TEntity, TChild?>? _referenceGetter;
    private readonly Action<TEntity, TChild?>? _referenceSetter;

    /// <summary>Initializes the specification from a validated binding.</summary>
    /// <param name="binding">The validated metadata binding.</param>
    /// <param name="collection">The collection selector for the collection route; <see langword="null"/> for a reference navigation.</param>
    /// <param name="childQuery">Builds the child query from a data context.</param>
    public MetadataEagerLoadSpec(
        MetadataEagerLoadBinding<TEntity, TChild> binding,
        Expression<Func<TEntity, ICollection<TChild>>>? collection,
        Func<IDataContext, EntityBuilder<TChild>> childQuery)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(childQuery);
        if (binding.IsCollection && collection is null)
            throw new ArgumentNullException(nameof(collection), "A collection navigation requires a collection selector.");

        Binding = binding;
        _childQuery = childQuery;
        _collectionExpression = collection;
        _keyPlan = new MetadataEagerLoadKeyPlan<TEntity, TChild>(binding.ParentComponents, binding.ChildComponents);

        if (binding.IsCollection)
        {
            // The boxed key selectors carry one component for an arity-1 key and a structurally equal
            // JoinIntoKey for a composite, so grouping/assignment consume every component in declaration
            // order. The metadata key plan supplies the component predicate and the parameter-bounded
            // chunks; the legacy explicit-key path never installs it and keeps its typed single key.
            _collection = new EagerLoadSpec<TEntity, TChild, object>(
                collection!,
                ResolveChild,
                binding.ParentKeyExpression,
                binding.ChildKeyExpression,
                binding.RequestedMode,
                _keyPlan);
        }
        else
        {
            var parameter = Expression.Parameter(typeof(TEntity), "p");
            _referenceGetter = Expression.Lambda<Func<TEntity, TChild?>>(
                Expression.Property(parameter, binding.Navigation),
                parameter).Compile();

            if (binding.Navigation.CanWrite)
            {
                var value = Expression.Parameter(typeof(TChild), "value");
                _referenceSetter = Expression.Lambda<Action<TEntity, TChild?>>(
                    Expression.Assign(Expression.Property(parameter, binding.Navigation), value),
                    parameter,
                    value).Compile();
            }
        }
    }

    /// <summary>The validated binding carried by this declaration.</summary>
    public MetadataEagerLoadBinding<TEntity, TChild> Binding { get; }

    /// <summary>The split key plan (component getters, chunk policy) carried by this declaration.</summary>
    internal MetadataEagerLoadKeyPlan<TEntity, TChild> KeyPlan => _keyPlan;

    /// <summary>The collection specification, or <see langword="null"/> for a reference navigation.</summary>
    internal EagerLoadSpec<TEntity, TChild, object>? CollectionLoadSpec => _collection;

    /// <inheritdoc/>
    public MemberInfo? CollectionMember => Binding.Navigation;

    /// <inheritdoc/>
    public EagerLoadMode RequestedMode => Binding.RequestedMode;

    /// <summary>
    /// One child item for a one-to-many or reference declaration; the link item followed by the child item
    /// for a many-to-many declaration (<see cref="IJoinIntoManyToManySpec.ItemCount"/>), because the
    /// single-query row projects the derived link (SQL) or the junction entity (in memory) first.
    /// </summary>
    public int OwnItemCount => Binding.Relationship.Kind == RelationshipKind.ManyToMany
        ? IJoinIntoManyToManySpec.ItemCount
        : 1;

    /// <inheritdoc/>
    public string ShapeKey => Binding.ShapeKey;

    /// <summary>
    /// The plan-cache identity of this declaration, which the command's <c>EagerLoadShape</c> is built
    /// from. For a metadata declaration it is the same canonical relationship identity as
    /// <see cref="ShapeKey"/>; the explicit-key form produces the identical value for an equivalent
    /// declaration through <see cref="MetadataEagerLoadPlanIdentity"/>, so the two forms share one entry.
    /// </summary>
    public string PlanShapeKey => Binding.ShapeKey;

    /// <inheritdoc/>
    IEagerLoadNodeSource IEagerLoadNodeSpec.ResolveChildNodeSource(IDataContext context) => ResolveChild(context);

    /// <inheritdoc/>
    bool IEagerLoadSpec<TEntity>.RequiresPerParentTopN(IDataContext context)
        => EntityBuilderEagerLoading.HasPerParentTopN(ResolveChild(context));

    /// <inheritdoc/>
    void IEagerLoadSpec<TEntity>.ValidatePerParentPaging(IDataContext context)
        => EntityBuilderEagerLoading.ValidatePerParentPagingChild(ResolveChild(context));

    /// <inheritdoc/>
    int IEagerLoadNodeSpec.SubtreeItemCount(IDataContext context)
        => EntityBuilderEagerLoading.SubtreeItemCount(this, context);

    /// <inheritdoc/>
    void IEagerLoadSpec<TEntity>.Execute(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents)
    {
        if (parents.Count == 0)
            return;

        if (Binding.IsReference)
        {
            ExecuteReference(context, parentScope, parents);
            return;
        }

        if (Binding.Relationship.Kind == RelationshipKind.ManyToMany)
        {
            ExecuteManyToMany(context, parentScope, parents);
            return;
        }

        ((IEagerLoadSpec<TEntity>)CollectionSpec()).Execute(context, parentScope, parents);
    }

    /// <inheritdoc/>
    async Task IEagerLoadSpec<TEntity>.ExecuteAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        if (parents.Count == 0)
            return;

        if (Binding.IsReference)
        {
            await ExecuteReferenceAsync(context, parentScope, parents, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (Binding.Relationship.Kind == RelationshipKind.ManyToMany)
        {
            await ExecuteManyToManyAsync(context, parentScope, parents, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ((IEagerLoadSpec<TEntity>)CollectionSpec()).ExecuteAsync(context, parentScope, parents, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    IJoinIntoSpec<TEntity> IEagerLoadSpec<TEntity>.ToJoinIntoSpec(IDataContext context, QueryFilterScope parentScope, out FromExpression childSource)
    {
        var built = BuildSingleQuery(context, parentScope, baseItemNumber: 1, parentItemNumber: 1);
        childSource = built.Joins[built.Joins.Count - 1].From
            ?? throw new InvalidOperationException(
                $"The single-query join for '{typeof(TEntity).Name}.{Binding.Navigation.Name}' has no child source.");
        return built.Spec;
    }

    /// <inheritdoc/>
    EagerLoadSingleQueryPlan<TEntity> IEagerLoadSpec<TEntity>.PlanSingleQuery(IDataContext context, QueryFilterScope parentScope, int baseItemNumber, int parentItemNumber)
    {
        ArgumentNullException.ThrowIfNull(context);

        var built = BuildSingleQuery(context, parentScope, baseItemNumber, parentItemNumber);
        if (built.ItemTypes.Count > JoinIntoProjectionFactory.MaxChildCount)
            throw new NotSupportedException(
                $"LoadWith with EagerLoadMode.SingleQuery supports at most {JoinIntoProjectionFactory.MaxChildCount} " +
                $"eager-loaded child items per query, but the graph below '{typeof(TEntity).Name}.{Binding.Navigation.Name}' " +
                $"declares {built.ItemTypes.Count}. Declare a shallower graph or use split-query loading.");

        return new EagerLoadSingleQueryPlan<TEntity>(built.Spec, built.Joins, built.ItemTypes);
    }

    /// <inheritdoc/>
    void IEagerSingleQueryStitch<TEntity>.StitchSingleQuery(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
        => StitchSingleQueryCore(parents, rows, columns, parentColumn, childColumn, context);

    /// <summary>
    /// Builds the single-query join(s) of this declaration: one left join for a one-to-many collection or
    /// a reference navigation, two joins (derived link then child) for a many-to-many collection. Every
    /// route preserves the parents (left join) and pairs component <c>i</c> of the parent key with
    /// component <c>i</c> of the child key, never by member name and never only the first component.
    /// </summary>
    private SingleQueryBuild BuildSingleQuery(IDataContext context, QueryFilterScope parentScope, int baseItemNumber, int parentItemNumber)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parentScope);

        if (Binding.IsReference)
            return BuildReferenceSingleQuery(context, parentItemNumber);

        if (Binding.Relationship.Kind == RelationshipKind.ManyToMany)
            return BuildManyToManySingleQuery(context, parentItemNumber);

        return BuildCollectionSingleQuery(context, parentScope, baseItemNumber, parentItemNumber);
    }

    // ---------------------------------------------------------------------------------------------
    // Single-query collection route (1:N): one left join, the child's own Where folded into the ON
    // predicate, an optional per-parent page lowered to a ranked derived source, and recursive joins
    // for a nested graph.
    // ---------------------------------------------------------------------------------------------

    private SingleQueryBuild BuildCollectionSingleQuery(IDataContext context, QueryFilterScope parentScope, int baseItemNumber, int parentItemNumber)
    {
        var child = ResolveChild(context);
        var nestedSpecs = ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs;
        var paged = context.NeedMapping && EntityBuilderEagerLoading.HasPerParentTopN(child);

        // A composite key cannot be a single window partition expression: this release renders only a
        // single-component per-parent partition, so the composite + page combination fails closed here
        // instead of silently partitioning by one component or paging globally.
        if (paged && _keyPlan.ParentProperties.Count > 1)
            throw CompositePagingNotSupported();

        if (nestedSpecs is { Count: > 0 })
        {
            // The ranked derived source has no per-level rendering: a nested graph and a per-parent page
            // cannot share one join chain. Same rejection the explicit-key path reports.
            if (EntityBuilderEagerLoading.HasPerParentTopN(child))
                throw EntityBuilderEagerLoading.PerParentTopNNotSupported();

            RejectUnsupportedChildShapes(child);

            var level1Source = child.ResolveSource() ?? context.GetFrom(typeof(TChild), null)
                ?? throw new BuildSqlCommandException(
                    $"The single-query child source for type {typeof(TChild)} could not be resolved.");

            var level1 = new MetadataEagerLoadJoinIntoSpec<TEntity, TChild>(
                this, child.Condition, child.FilterScope, [typeof(TChild)], isCollection: true);
            var level1Join = EntityBuilderEagerLoading.BuildSingleQueryJoin(level1, level1Source, child.Sorting);
            level1Join.EagerParentItemNumber = parentItemNumber;

            var joins = new List<JoinExpression> { level1Join };
            var itemTypes = new List<Type> { typeof(TChild) };
            for (var i = 0; i < nestedSpecs.Count; i++)
            {
                // Each descendant is planned through the same seam, so a deeper level recurses. Its
                // subtree starts right after the items already laid out, and its first join's direct
                // parent is this node's own item, so a repeated CLR type resolves to the correct slot.
                var descendantBase = baseItemNumber + itemTypes.Count;
                var descendant = ((IEagerLoadSpec<TChild>)nestedSpecs[i]).PlanSingleQuery(
                    context, child.FilterScope, descendantBase, baseItemNumber);
                joins.AddRange(descendant.Joins);
                itemTypes.AddRange(descendant.ItemTypes);
            }

            var nestedSpec = new MetadataEagerLoadJoinIntoSpec<TEntity, TChild>(
                this, child.Condition, child.FilterScope, itemTypes, isCollection: true);
            return new SingleQueryBuild(nestedSpec, joins, itemTypes);
        }

        FromExpression childSource;
        Expression<Func<TChild, bool>>? childCondition;
        QueryFilterScope childScope;
        List<Sorting>? childOrdering;

        if (paged)
        {
            var scoped = BuildChild(context, parentScope);
            EntityBuilderEagerLoading.ValidatePerParentPagingChild(scoped);

            var command = scoped.ToCommand();
            command.From ??= context.GetFrom(typeof(TChild), null);
            command.PerParentPaging = true;
            command.PerParentPagingEmbedded = true;
            command.PerParentPartition = _keyPlan.BuildPartitionLambda();
            childSource = new FromExpression(command);
            // The child's Where/order/page are consumed by the derived source, so the join predicate is
            // the key equality alone and the rendering metadata is the embedded source's own order.
            childCondition = null;
            childScope = QueryFilterScope.None;
            childOrdering = null;
        }
        else
        {
            RejectUnsupportedChildShapes(child);
            childSource = child.ResolveSource() ?? context.GetFrom(typeof(TChild), null)
                ?? throw new BuildSqlCommandException(
                    $"The single-query child source for type {typeof(TChild)} could not be resolved.");
            childCondition = child.Condition;
            childScope = child.FilterScope;
            childOrdering = child.Sorting;
        }

        var spec = new MetadataEagerLoadJoinIntoSpec<TEntity, TChild>(
            this, childCondition, childScope, [typeof(TChild)], isCollection: true);
        var join = EntityBuilderEagerLoading.BuildSingleQueryJoin(spec, childSource, childOrdering);
        join.EagerParentItemNumber = parentItemNumber;
        return new SingleQueryBuild(spec, [join], [typeof(TChild)]);
    }

    // ---------------------------------------------------------------------------------------------
    // Single-query reference route (N:1 and 1:1): one left join, at most one distinct full identity per
    // parent, and a null navigation when nothing matches.
    // ---------------------------------------------------------------------------------------------

    private SingleQueryBuild BuildReferenceSingleQuery(IDataContext context, int parentItemNumber)
    {
        var child = ResolveChild(context);
        EnsureReferenceChildIsSupported(child);

        var nested = ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs;
        if (nested is { Count: > 0 })
            throw new NotSupportedException(
                $"LoadWith with EagerLoadMode.SingleQuery cannot fold a nested graph declared below the reference " +
                $"navigation '{typeof(TEntity).Name}.{Binding.Navigation.Name}' yet: one reference row per parent " +
                "cannot carry a child graph in the same result set. Load the reference with split-query loading " +
                "(omit EagerLoadMode.SingleQuery), or declare the nested graph on the collection side.");

        RejectUnsupportedChildShapes(child);

        var childSource = child.ResolveSource() ?? context.GetFrom(typeof(TChild), null)
            ?? throw new BuildSqlCommandException(
                $"The single-query child source for type {typeof(TChild)} could not be resolved.");

        var spec = new MetadataEagerLoadJoinIntoSpec<TEntity, TChild>(
            this, child.Condition, child.FilterScope, [typeof(TChild)], isCollection: false);
        var join = EntityBuilderEagerLoading.BuildSingleQueryJoin(spec, childSource, child.Sorting);
        join.EagerParentItemNumber = parentItemNumber;
        return new SingleQueryBuild(spec, [join], [typeof(TChild)]);
    }

    // ---------------------------------------------------------------------------------------------
    // Single-query many-to-many route: the proven derived junction link + child edges, keeping every
    // valid link occurrence and never multiplying sibling collections.
    // ---------------------------------------------------------------------------------------------

    private SingleQueryBuild BuildManyToManySingleQuery(IDataContext context, int parentItemNumber)
    {
        var child = ResolveChild(context);
        var paged = EntityBuilderEagerLoading.HasPerParentTopN(child);
        ValidateManyToManyPaging(child);

        var nested = ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs;
        if (nested is { Count: > 0 })
            throw new NotSupportedException(
                $"LoadWith with EagerLoadMode.SingleQuery cannot fold a nested graph declared below the many-to-many " +
                $"collection '{typeof(TEntity).Name}.{Binding.Navigation.Name}' yet: the derived junction link " +
                "occupies the projection item a nested graph would need. Use split-query loading (omit " +
                "EagerLoadMode.SingleQuery), which recurses through the link table.");

        // A paged many-to-many child is not folded at all: its page is applied to the stitched surviving
        // occurrences (JoinIntoManyToManySpec.AssignChildrenFromLinks), so only the page's own shape
        // contract applies — ValidateManyToManyPaging has already rejected every other child shape. A
        // non-paged child keeps the strict fold inventory, like the 1:N single-query route.
        if (!paged)
            RejectUnsupportedChildShapes(child);

        var collection = _collectionExpression
            ?? throw new InvalidOperationException(
                $"The many-to-many declaration '{typeof(TEntity).Name}.{Binding.Navigation.Name}' has no collection selector.");

        var predicate = BuildManyToManyPredicate(child.Condition);
        var (manyToMany, linkJoin, childJoin) = JoinIntoManyToManySpec<TEntity, TChild>.Create(
            child, predicate, collection, Binding.Relationship, JoinType.Left, new JoinOptions(), context,
            allowChildOrderingAndPaging: paged);

        linkJoin.EagerParentItemNumber = parentItemNumber;
        var joins = new List<JoinExpression>
        {
            WithEagerOrdering(linkJoin, null, null),
            WithEagerOrdering(childJoin, child.Sorting, child.FilterScope),
        };

        _manyToManyLayouts.AddOrUpdate(context, manyToMany);
        return new SingleQueryBuild(manyToMany, joins, manyToMany.GetItemTypes(context.NeedMapping));
    }

    /// <summary>
    /// Single-result-set assembly of a many-to-many declaration: the row carries the parent item, the link
    /// item and the child item, in that order (the declaration's own slice starts at the link). Assignment
    /// reuses the flat many-to-many declaration's link-aware contract, so a repeated junction row keeps its
    /// occurrence, a dangling link contributes nothing, and a child never lands under a parent it was not
    /// linked to. A non-many-to-many declaration never reaches this method.
    /// </summary>
    private void StitchManyToManyCore(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
    {
        if (!_manyToManyLayouts.TryGetValue(context, out var layout))
            throw new InvalidOperationException(
                $"The many-to-many declaration '{typeof(TEntity).Name}.{Binding.Navigation.Name}' was stitched " +
                "without its single-query link layout: the query must be prepared before its rows are assembled.");

        var linkRows = new List<JoinIntoManyToManyRow<TEntity>>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row is null)
                continue;

            if (columns[parentColumn].GetValue(row) is not TEntity parent)
                continue;

            linkRows.Add(new JoinIntoManyToManyRow<TEntity>(
                parent,
                columns[childColumn].GetValue(row),
                columns[childColumn + 1].GetValue(row)));
        }

        layout.AssignChildrenFromLinks(parents, linkRows);
    }

    private static Expression<Func<TEntity, TChild, bool>> BuildManyToManyPredicate(Expression<Func<TChild, bool>>? condition)
    {
        var parent = Expression.Parameter(typeof(TEntity), "p");
        var child = Expression.Parameter(typeof(TChild), "c");
        Expression body = condition is null
            ? Expression.Constant(true)
            : JoinIntoSpecHelpers.ReplaceParameter(condition.Body, condition.Parameters[0], child);

        return Expression.Lambda<Func<TEntity, TChild, bool>>(body, parent, child);
    }

    /// <summary>
    /// Copies a stored join with this declaration's own rendering metadata: the child query's ordering
    /// (so stitched children keep the child query's order where the provider's outer ordering supports
    /// it) and its selective global-filter scope. Every other field is preserved.
    /// </summary>
    private static JoinExpression WithEagerOrdering(JoinExpression source, List<Sorting>? ordering, QueryFilterScope? childScope)
        => new(source.JoinCondition, source.JoinType)
        {
            From = source.From,
            EntityType = source.EntityType,
            Strictness = source.Strictness,
            IsGlobal = source.IsGlobal,
            JoinHint = source.JoinHint,
            TableHints = source.TableHints,
            ApplySource = source.ApplySource,
            OriginalJoinCondition = source.OriginalJoinCondition,
            IsJoinInto = source.IsJoinInto,
            JoinIntoIdentity = source.JoinIntoIdentity,
            SuppressCartesianWarning = source.SuppressCartesianWarning,
            EagerParentItemNumber = source.EagerParentItemNumber,
            FilterScope = childScope is { IsEmpty: false } scope ? scope : source.FilterScope,
            EagerPerParentOrdering = ordering is { Count: > 0 } ? [.. ordering] : source.EagerPerParentOrdering,
        };

    /// <summary>
    /// Rejects a child query shape the single-query fold cannot render. Unlike the explicit-key path this
    /// declaration carries the child's own <c>OrderBy</c> as rendering metadata, so ordering is accepted;
    /// every other unsupported shape fails closed with the same wording.
    /// </summary>
    private void RejectUnsupportedChildShapes(EntityBuilder<TChild> child)
    {
        var unsupported = child.UnsupportedSingleQueryChildShapes();
        if (unsupported.Count == 0)
            return;

        List<string>? rejected = null;
        for (var i = 0; i < unsupported.Count; i++)
        {
            if (string.Equals(unsupported[i], "OrderBy", StringComparison.Ordinal))
                continue;

            (rejected ??= []).Add(unsupported[i]);
        }

        if (rejected is null)
            return;

        throw new NotSupportedException(
            $"LoadWith with EagerLoadMode.SingleQuery cannot fold the child query's {string.Join(", ", rejected)} into the join " +
            "predicate: the metadata-driven fold supports only the child's own Where/OrderBy (and a single-component " +
            "per-parent page). Remove the modifier or use split-query loading (omit the SingleQuery mode), which honors " +
            "every child-query shape.");
    }

    private NotSupportedException CompositePagingNotSupported()
        => new(
            $"LoadWith with EagerLoadMode.SingleQuery cannot apply Limit/Offset/Page to the metadata-driven child query " +
            $"of the composite-key relationship '{typeof(TEntity).Name}.{Binding.Navigation.Name}' " +
            $"({string.Join(", ", Binding.KeyComponents)}): the per-parent partition needs a single key component, and " +
            "a composite partition is not rendered by this release. Remove the child paging or use an explicit join.");

    /// <summary>
    /// Reads and assigns this node (and, recursively, its descendants) from the flattened projection. The
    /// collection route reuses the split loader's section-7 assignment contract, so both modes share one
    /// stitching rule; the reference route resolves at most one distinct full identity per parent and
    /// leaves a parent with no match <see langword="null"/>.
    /// </summary>
    internal void StitchSingleQueryCore(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
    {
        if (Binding.IsReference)
        {
            var pairs = new List<(TEntity Parent, object? Child)>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row is null)
                    continue;

                if (columns[parentColumn].GetValue(row) is not TEntity parent)
                    continue;

                pairs.Add((parent, columns[childColumn].GetValue(row)));
            }

            AssignReferencesFromRows(parents, pairs);
            return;
        }

        // A many-to-many declaration projects its link item before the child, so the general collection
        // route (which groups by the child's own parent foreign key) cannot assign it: the link carries the
        // pairing. Route it through the link-aware contract the flat declaration uses.
        if (Binding.Relationship.Kind == RelationshipKind.ManyToMany)
        {
            StitchManyToManyCore(parents, rows, columns, parentColumn, childColumn, context);
            return;
        }

        // Reuses the split loader's own recursive assembly (the per-context child builder memo and the
        // declared descendants), so a collection→collection graph stitches exactly like the split path.
        CollectionSpec().StitchSingleQueryCore(parents, rows, columns, parentColumn, childColumn, context);
    }

    /// <summary>Groups the flattened rows by the full parent key and assigns a collection navigation.</summary>
    internal void AssignChildrenCore(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
    {
        if (Binding.IsReference)
        {
            AssignReferencesFromRows(parents, rows);
            return;
        }

        CollectionSpec().AssignChildrenFromRows(parents, rows);
    }

    /// <summary>
    /// Resolves the reference navigation from the flattened rows: candidates are grouped by the parent's
    /// full key, a parent with no candidate stays <see langword="null"/>, repeated rows of one identity
    /// collapse and two distinct identities fail before any parent is mutated.
    /// </summary>
    private void AssignReferencesFromRows(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
    {
        var grouped = new Dictionary<object, List<TChild>>();
        for (var i = 0; i < rows.Count; i++)
        {
            var (parent, childValue) = rows[i];
            if (childValue is not TChild child || !_keyPlan.IsMatchable(parent))
                continue;

            var key = _keyPlan.ParentKey(parent);
            if (!grouped.TryGetValue(key, out var candidates))
                grouped[key] = candidates = [];

            candidates.Add(child);
        }

        var resolved = ResolveReferences(parents, grouped);
        MetadataReferenceAssignment.Assign(
            parents,
            resolved,
            _referenceGetter!,
            _referenceSetter,
            typeof(TEntity).Name,
            Binding.Navigation.Name);
    }

    /// <summary>The boxed full parent key used to deduplicate the denormalized parents.</summary>
    internal object ParentKeyOf(TEntity parent) => _keyPlan.ParentKey(parent);

    /// <summary>One built declaration: its stitching specification, its joins (pre-order) and items.</summary>
    private sealed class SingleQueryBuild(
        IJoinIntoSpec<TEntity> spec,
        List<JoinExpression> joins,
        IReadOnlyList<Type> itemTypes)
    {
        public IJoinIntoSpec<TEntity> Spec { get; } = spec;

        public List<JoinExpression> Joins { get; } = joins;

        public IReadOnlyList<Type> ItemTypes { get; } = itemTypes;
    }

    /// <summary>
    /// Resolves the child builder for <paramref name="context"/> once and returns it on every later call,
    /// so graph inspection and execution share one builder and the user factory is invoked at most once
    /// per context. The supplied builder's entity type is validated here, before any SQL is built.
    /// </summary>
    /// <param name="context">The context that owns the parent query.</param>
    /// <returns>The reusable child builder.</returns>
    internal EntityBuilder<TChild> ResolveChild(IDataContext context)
        => _resolvedChildren.GetValue(
            context,
            ctx => new Lazy<EntityBuilder<TChild>>(() => ValidateChild(ctx), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private EntityBuilder<TChild> ValidateChild(IDataContext context)
    {
        var child = _childQuery(context)
            ?? throw new NotSupportedException(
                $"{Binding.Form} child query returned no builder for '{typeof(TChild).Name}'.");

        var sourceType = child.SourceEntityType ?? typeof(TChild);
        if (sourceType != Binding.RelatedType)
            throw new NotSupportedException(
                $"{Binding.Form} supplied a child builder for '{sourceType.Name}', but the declared relationship " +
                $"'{typeof(TEntity).Name}.{Binding.Navigation.Name}' points to '{Binding.RelatedType.Name}'.");

        return child;
    }

    private EntityBuilder<TChild> BuildChild(IDataContext context, QueryFilterScope parentScope)
        => ResolveChild(context).WithFilterScope(parentScope);

    private EagerLoadSpec<TEntity, TChild, object> CollectionSpec()
        => _collection
           ?? throw new InvalidOperationException(
               $"'{typeof(TEntity).Name}.{Binding.Navigation.Name}' is a reference navigation and cannot run the collection loader.");

    // ---------------------------------------------------------------------------------------------
    // Reference route: buffered full-identity resolution and assignment.
    // ---------------------------------------------------------------------------------------------

    private void ExecuteReference(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents)
    {
        var child = BuildChild(context, parentScope);
        EnsureReferenceChildIsSupported(child);

        var grouped = _keyPlan.Load(child, parents, loaded: null);
        var resolved = ResolveReferences(parents, grouped);
        MetadataReferenceAssignment.Assign(
            parents,
            resolved,
            _referenceGetter!,
            _referenceSetter,
            typeof(TEntity).Name,
            Binding.Navigation.Name);

        var assigned = CollectAssignedReferences(resolved);
        if (assigned.Count > 0 && ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs is { Count: > 0 })
            EntityBuilderEagerLoading.ExecuteLoadedChildren(child, assigned);
    }

    private async Task ExecuteReferenceAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        var child = BuildChild(context, parentScope);
        EnsureReferenceChildIsSupported(child);

        var grouped = await _keyPlan.LoadAsync(child, parents, loaded: null, cancellationToken).ConfigureAwait(false);
        var resolved = ResolveReferences(parents, grouped);
        MetadataReferenceAssignment.Assign(
            parents,
            resolved,
            _referenceGetter!,
            _referenceSetter,
            typeof(TEntity).Name,
            Binding.Navigation.Name);

        var assigned = CollectAssignedReferences(resolved);
        if (assigned.Count > 0 && ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs is { Count: > 0 })
            await EntityBuilderEagerLoading.ExecuteLoadedChildrenAsync(child, assigned, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureReferenceChildIsSupported(EntityBuilder<TChild> child)
    {
        if (EntityBuilderEagerLoading.HasPerParentTopN(child))
            throw new NotSupportedException(
                "LoadWith cannot apply Limit/Offset/Page to a reference child query: a per-parent top-N " +
                "applies to a collection navigation. Remove the child paging or use the collection overload.");
    }

    private TChild?[] ResolveReferences(IReadOnlyList<TEntity> parents, Dictionary<object, List<TChild>> grouped)
    {
        var identity = JoinIntoSpecHelpers.BuildIdentitySelector<TChild>();
        var message =
            $"The reference navigation '{typeof(TEntity).Name}.{Binding.Navigation.Name}' matched more than one " +
            $"distinct '{typeof(TChild).Name}' for a single parent; a reference navigation requires at most one " +
            "related row per parent.";

        var resolved = new TChild?[parents.Count];
        for (var i = 0; i < parents.Count; i++)
        {
            var parent = parents[i];
            if (!_keyPlan.IsMatchable(parent) || !grouped.TryGetValue(_keyPlan.ParentKey(parent), out var candidates))
                continue;

            resolved[i] = MetadataReferenceAssignment.Resolve(candidates, identity, message);
        }

        return resolved;
    }

    private static IReadOnlyList<TChild> CollectAssignedReferences(IReadOnlyList<TChild?> resolved)
    {
        var seen = new HashSet<TChild>();
        var assigned = new List<TChild>();
        for (var i = 0; i < resolved.Count; i++)
        {
            if (resolved[i] is { } child && seen.Add(child))
                assigned.Add(child);
        }

        return assigned;
    }

    // ---------------------------------------------------------------------------------------------
    // Many-to-many route: one batched junction read and one batched child read per bounded chunk.
    // ---------------------------------------------------------------------------------------------

    private void ExecuteManyToMany(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents)
    {
        var child = BuildChild(context, parentScope);
        ValidateManyToManyPaging(child);

        var loaded = new List<TChild>();
        var grouped = LoadManyToMany(context, child, parents, loaded);
        CollectionSpec().Assign(parents, grouped);

        if (((IEagerLoadNodeSource)child).EagerLoadNodeSpecs is { Count: > 0 })
            EntityBuilderEagerLoading.ExecuteLoadedChildren(child, loaded);
    }

    private async Task ExecuteManyToManyAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        var child = BuildChild(context, parentScope);
        ValidateManyToManyPaging(child);

        var loaded = new List<TChild>();
        var grouped = await LoadManyToManyAsync(context, child, parents, loaded, cancellationToken).ConfigureAwait(false);
        CollectionSpec().Assign(parents, grouped);

        if (((IEagerLoadNodeSource)child).EagerLoadNodeSpecs is { Count: > 0 })
            await EntityBuilderEagerLoading.ExecuteLoadedChildrenAsync(child, loaded, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates a many-to-many child's own <c>Limit</c>/<c>Offset</c>/<c>Page</c>. A per-parent page over a
    /// junction has no single-child partition to rank in SQL, so the page is applied to the surviving
    /// occurrence list after loading (the junction read, the child read and the single-query stitching all
    /// preserve occurrence order). The same shape contract as the ranked 1:N loader applies: a
    /// non-empty deterministic ordering, no <c>WithTies</c> and no child shape the eager load cannot fold.
    /// A non-paged child is a no-op.
    /// </summary>
    private static void ValidateManyToManyPaging(EntityBuilder<TChild> child)
    {
        if (EntityBuilderEagerLoading.HasPerParentTopN(child))
            EntityBuilderEagerLoading.ValidatePerParentPagingChild(child);
    }

    private Dictionary<object, List<TChild>> LoadManyToMany(
        IDataContext context,
        EntityBuilder<TChild> child,
        IReadOnlyList<TEntity> parents,
        List<TChild> loaded)
    {
        var (parentLinkProperties, childLinkProperties) = ResolveJunctionComponents();
        var links = new List<(object ParentKey, object ChildKey)>();
        var distinctChildren = new List<(object Key, object?[] Values)>();
        var seenChildKeys = new HashSet<object>();

        foreach (var chunk in _keyPlan.Chunks(parents))
        {
            var parentPredicate = EagerLoadInPredicate.Build(
                JunctionType(),
                parentLinkProperties,
                _keyPlan.ParentValues(chunk));

            var linkRows = LoadJunctionRows(context, JunctionType(), parentPredicate);
            AppendLinks(linkRows, parentLinkProperties, childLinkProperties, links, distinctChildren, seenChildKeys);
        }

        LoadLinkedChildren(child, distinctChildren, loaded);
        return ApplyPerParentPage(MetadataManyToManyAssignment.Group(links, loaded, MatchableChildKey), child.Paging);
    }

    private async Task<Dictionary<object, List<TChild>>> LoadManyToManyAsync(
        IDataContext context,
        EntityBuilder<TChild> child,
        IReadOnlyList<TEntity> parents,
        List<TChild> loaded,
        CancellationToken cancellationToken)
    {
        var (parentLinkProperties, childLinkProperties) = ResolveJunctionComponents();
        var links = new List<(object ParentKey, object ChildKey)>();
        var distinctChildren = new List<(object Key, object?[] Values)>();
        var seenChildKeys = new HashSet<object>();

        foreach (var chunk in _keyPlan.Chunks(parents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parentPredicate = EagerLoadInPredicate.Build(
                JunctionType(),
                parentLinkProperties,
                _keyPlan.ParentValues(chunk));

            var linkRows = await LoadJunctionRowsAsync(context, JunctionType(), parentPredicate, cancellationToken).ConfigureAwait(false);
            AppendLinks(linkRows, parentLinkProperties, childLinkProperties, links, distinctChildren, seenChildKeys);
        }

        await LoadLinkedChildrenAsync(child, distinctChildren, loaded, cancellationToken).ConfigureAwait(false);
        return ApplyPerParentPage(
            MetadataManyToManyAssignment.Group(links, loaded, MatchableChildKey),
            child.Paging);
    }

    /// <summary>
    /// Applies the child query's <c>Limit</c>/<c>Offset</c>/<c>Page</c> to every parent's surviving
    /// occurrence list. The grouping already holds exactly the occurrences the link table contributes — a
    /// repeated junction row repeats its child, a dangling link is absent — and each list is in the child
    /// query's own order, so trimming a prefix is the per-parent page the 1:N ranked lowering renders
    /// in SQL. The page never reaches a statement as a global <c>LIMIT</c>/<c>TOP</c>: a global page would
    /// truncate the rows of every parent after the first.
    /// </summary>
    private static Dictionary<object, List<TChild>> ApplyPerParentPage(
        Dictionary<object, List<TChild>> grouped,
        Paging paging)
    {
        MetadataManyToManyAssignment.ApplyPage(grouped, paging);
        return grouped;
    }

    private (IReadOnlyList<PropertyInfo> Parent, IReadOnlyList<PropertyInfo> Child) ResolveJunctionComponents()
    {
        var junction = Binding.Relationship.Junction
            ?? throw new NotSupportedException(
                $"The many-to-many relationship '{typeof(TEntity).Name}.{Binding.Navigation.Name}' has no junction metadata.");
        JunctionLinkSourceFactory.EnsureJunctionRegistered(junction.JunctionType);

        var parentCount = _keyPlan.ParentProperties.Count;
        var childCount = _keyPlan.ChildProperties.Count;
        if (junction.JunctionParentForeignKey.Count != parentCount || junction.JunctionChildForeignKey.Count != childCount)
            throw new NotSupportedException(
                $"The many-to-many relationship '{typeof(TEntity).Name}.{Binding.Navigation.Name}' has a junction " +
                "whose foreign keys do not cover every relationship key component.");

        var parent = new PropertyInfo[parentCount];
        var child = new PropertyInfo[childCount];
        for (var i = 0; i < parentCount; i++)
            parent[i] = junction.JunctionParentForeignKey[i].PropertyInfo;
        for (var i = 0; i < childCount; i++)
            child[i] = junction.JunctionChildForeignKey[i].PropertyInfo;

        return (parent, child);
    }

    private Type JunctionType()
        => Binding.Relationship.Junction?.JunctionType
           ?? throw new NotSupportedException(
               $"The many-to-many relationship '{typeof(TEntity).Name}.{Binding.Navigation.Name}' has no junction metadata.");

    private void AppendLinks(
        IReadOnlyList<object> linkRows,
        IReadOnlyList<PropertyInfo> parentLinkProperties,
        IReadOnlyList<PropertyInfo> childLinkProperties,
        List<(object ParentKey, object ChildKey)> links,
        List<(object Key, object?[] Values)> distinctChildren,
        HashSet<object> seenChildKeys)
    {
        for (var i = 0; i < linkRows.Count; i++)
        {
            var row = linkRows[i];
            var parentValues = ReadComponents(row, parentLinkProperties, out var parentComplete);
            var childValues = ReadComponents(row, childLinkProperties, out var childComplete);
            if (!parentComplete || !childComplete)
                continue;

            var parentKey = BoxKey(parentValues);
            var childKey = BoxKey(childValues);
            links.Add((parentKey, childKey));

            if (seenChildKeys.Add(childKey))
                distinctChildren.Add((childKey, childValues));
        }
    }

    private void LoadLinkedChildren(
        EntityBuilder<TChild> child,
        IReadOnlyList<(object Key, object?[] Values)> distinctChildren,
        List<TChild> loaded)
    {
        for (var offset = 0; offset < distinctChildren.Count; offset += _keyPlan.ChunkSize)
        {
            var predicate = BuildChildPredicate(distinctChildren, offset);
            var children = BuildLinkedChildCommand(child, predicate).ToList();
            loaded.AddRange(children);
        }
    }

    private async Task LoadLinkedChildrenAsync(
        EntityBuilder<TChild> child,
        IReadOnlyList<(object Key, object?[] Values)> distinctChildren,
        List<TChild> loaded,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < distinctChildren.Count; offset += _keyPlan.ChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var predicate = BuildChildPredicate(distinctChildren, offset);
            var children = await BuildLinkedChildCommand(child, predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
            loaded.AddRange(children);
        }
    }

    /// <summary>
    /// Builds one batched child command of the junction-driven load. The child query's own page is cleared
    /// here: the per-parent page is applied afterwards to each parent's surviving occurrence list
    /// (<see cref="ApplyPerParentPage"/>), while a page left on this statement would be a global
    /// <c>LIMIT</c>/<c>TOP</c> over the whole chunk. The command is a fresh instance, so nothing on the child
    /// builder — and therefore no shared sticky cache state — is touched.
    /// </summary>
    private static QueryCommand<TChild> BuildLinkedChildCommand(
        EntityBuilder<TChild> child,
        Expression<Func<TChild, bool>> predicate)
    {
        var command = child.Where(predicate).ToCommand();
        command.Paging = default;
        return command;
    }

    private Expression<Func<TChild, bool>> BuildChildPredicate(
        IReadOnlyList<(object Key, object?[] Values)> distinctChildren,
        int offset)
    {
        var count = Math.Min(_keyPlan.ChunkSize, distinctChildren.Count - offset);
        var values = new List<object?>[_keyPlan.ChildProperties.Count];
        for (var component = 0; component < values.Length; component++)
            values[component] = new List<object?>(count);

        for (var i = 0; i < count; i++)
        {
            var entry = distinctChildren[offset + i];
            for (var component = 0; component < values.Length; component++)
                values[component].Add(entry.Values[component]);
        }

        return EagerLoadInPredicate.Build<TChild>(_keyPlan.ChildProperties, values);
    }

    private object? MatchableChildKey(TChild child)
        => _keyPlan.IsMatchableChild(child) ? _keyPlan.ChildKey(child) : null;

    private static object?[] ReadComponents(object row, IReadOnlyList<PropertyInfo> properties, out bool complete)
    {
        var values = new object?[properties.Count];
        complete = true;
        for (var i = 0; i < properties.Count; i++)
        {
            values[i] = properties[i].GetValue(row);
            if (values[i] is null)
                complete = false;
        }

        return values;
    }

    private static object BoxKey(object?[] values)
        => values.Length == 1 ? values[0]! : new JoinIntoKey(values);

    private static readonly MethodInfo JunctionLoadMethod =
        typeof(MetadataEagerLoadSpec<TEntity, TChild>).GetMethod(
            nameof(LoadJunctionRowsCore),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo JunctionLoadAsyncMethod =
        typeof(MetadataEagerLoadSpec<TEntity, TChild>).GetMethod(
            nameof(LoadJunctionRowsAsyncCore),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static IReadOnlyList<object> LoadJunctionRows(IDataContext context, Type junctionType, LambdaExpression predicate)
        => (IReadOnlyList<object>)JunctionLoadMethod.MakeGenericMethod(junctionType).Invoke(null, [context, predicate])!;

    private static Task<IReadOnlyList<object>> LoadJunctionRowsAsync(
        IDataContext context,
        Type junctionType,
        LambdaExpression predicate,
        CancellationToken cancellationToken)
        => (Task<IReadOnlyList<object>>)JunctionLoadAsyncMethod
            .MakeGenericMethod(junctionType)
            .Invoke(null, [context, predicate, cancellationToken])!;

    private static IReadOnlyList<object> LoadJunctionRowsCore<TJunction>(IDataContext context, LambdaExpression predicate)
    {
        var typed = (Expression<Func<TJunction, bool>>)predicate;
        var rows = context.From<TJunction>().Where(typed).ToList();
        var result = new object[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            result[i] = rows[i]!;

        return result;
    }

    private static async Task<IReadOnlyList<object>> LoadJunctionRowsAsyncCore<TJunction>(
        IDataContext context,
        LambdaExpression predicate,
        CancellationToken cancellationToken)
    {
        var typed = (Expression<Func<TJunction, bool>>)predicate;
        var rows = await context.From<TJunction>().Where(typed).ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new object[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            result[i] = rows[i]!;

        return result;
    }
}

/// <summary>
/// The single-query join specification of one metadata-driven declaration: a <b>left</b> join whose
/// <c>ON</c> predicate pairs component <c>i</c> of the parent key with component <c>i</c> of the child key
/// (positionally, never by name or only the first component), with the child's own <c>Where</c> folded in
/// so it filters the children without filtering the parents. The stitching and parent-key identity are
/// delegated to the owning <see cref="MetadataEagerLoadSpec{TEntity,TChild}"/>, so single and split modes
/// share one assignment contract and the same full composite key.
/// </summary>
/// <typeparam name="TEntity">The parent entity type.</typeparam>
/// <typeparam name="TChild">The related entity type.</typeparam>
internal sealed class MetadataEagerLoadJoinIntoSpec<TEntity, TChild> : IJoinIntoSpec<TEntity>, IEagerSingleQueryStitch<TEntity>
{
    private readonly MetadataEagerLoadSpec<TEntity, TChild> _owner;
    private readonly Expression<Func<TEntity, TChild, bool>> _predicate;
    private readonly QueryFilterScope _childFilterScope;
    private readonly IReadOnlyList<Type> _itemTypes;
    private readonly bool _isCollection;

    /// <summary>Initializes the specification.</summary>
    /// <param name="owner">The metadata declaration that supplies the keys and the stitching contract.</param>
    /// <param name="childCondition">The child query's own <c>Where</c>, or <see langword="null"/> when it has none.</param>
    /// <param name="childFilterScope">The child query's selective global-query-filter scope.</param>
    /// <param name="itemTypes">The flattened projection item types of this declaration's subtree.</param>
    /// <param name="isCollection">Whether the navigation is a collection (a reference assigns at most one child).</param>
    public MetadataEagerLoadJoinIntoSpec(
        MetadataEagerLoadSpec<TEntity, TChild> owner,
        Expression<Func<TChild, bool>>? childCondition,
        QueryFilterScope childFilterScope,
        IReadOnlyList<Type> itemTypes,
        bool isCollection)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(childFilterScope);
        ArgumentNullException.ThrowIfNull(itemTypes);

        _owner = owner;
        _childFilterScope = childFilterScope;
        _itemTypes = itemTypes;
        _isCollection = isCollection;

        var parent = Expression.Parameter(typeof(TEntity), "p");
        var child = Expression.Parameter(typeof(TChild), "c");
        Expression? body = null;
        foreach (var component in owner.Binding.KeyComponents)
        {
            var equality = JoinIntoSpecHelpers.BuildKeyEquality(
                Expression.Property(parent, component.Parent),
                Expression.Property(child, component.Child),
                component.Parent.PropertyType,
                "EagerLoadMode.SingleQuery");
            body = body is null ? equality : Expression.AndAlso(body, equality);
        }

        if (childCondition is not null)
        {
            var condition = JoinIntoSpecHelpers.ReplaceParameter(
                childCondition.Body, childCondition.Parameters[0], child);
            body = Expression.AndAlso(body!, condition);
        }

        _predicate = Expression.Lambda<Func<TEntity, TChild, bool>>(body!, parent, child);

        var navigation = owner.Binding.Navigation;
        Identity = new JoinIntoIdentity
        {
            ParentType = typeof(TEntity),
            ChildType = typeof(TChild),
            JoinType = JoinType.Left,
            CollectionMember = $"{navigation.DeclaringType?.FullName}.{navigation.Name}",
            KeyMembers =
            [
                .. owner.Binding.KeyComponents.Select(static component => component.Child.Name),
                .. owner.Binding.KeyComponents.Select(static component => component.Parent.Name),
            ],
        };
    }

    /// <inheritdoc/>
    public JoinType JoinType => JoinType.Left;

    /// <inheritdoc/>
    public PropertyInfo? CollectionProperty => _owner.Binding.Navigation;

    /// <inheritdoc/>
    public LambdaExpression Predicate => _predicate;

    /// <inheritdoc/>
    public Type ChildEntityType => typeof(TChild);

    /// <inheritdoc/>
    public IReadOnlyList<Type> ItemTypes => _itemTypes;

    /// <inheritdoc/>
    public bool IsCollection => _isCollection;

    /// <inheritdoc/>
    public JoinIntoIdentity Identity { get; }

    /// <inheritdoc/>
    public QueryFilterScope ChildFilterScope => _childFilterScope;

    /// <inheritdoc/>
    public object? GetParentKey(TEntity parent) => _owner.ParentKeyOf(parent);

    /// <inheritdoc/>
    public void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
        => _owner.AssignChildrenCore(parents, rows);

    /// <inheritdoc/>
    public void StitchSingleQuery(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
        => _owner.StitchSingleQueryCore(parents, rows, columns, parentColumn, childColumn, context);
}
