using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// The many-to-many slice of a <see cref="IJoinIntoSpec{TEntity}"/> the execution paths need but a
/// one-to-many declaration has no notion of: the junction entity, the parent/child keys that tie the
/// junction to either side, and the uniformly-addressable link accessors. Kept off
/// <see cref="IJoinIntoSpec{TEntity}"/> so the direct specs stay unchanged; the stitcher discovers it
/// with a type test.
/// </summary>
internal interface IJoinIntoManyToManySpec
{
    /// <summary>The junction entity type the in-memory path joins directly in place of the derived link.</summary>
    Type JunctionEntityType { get; }

    /// <summary>The parent-side principal key members the junction's parent foreign key matches.</summary>
    IReadOnlyList<PropertyInfo> ParentPrincipalKeys { get; }

    /// <summary>The child-side key members the junction's child foreign key matches.</summary>
    IReadOnlyList<PropertyInfo> ChildKeys { get; }

    /// <summary>The junction members that reference the parent key, in component order.</summary>
    IReadOnlyList<PropertyInfo> JunctionParentForeignKeys { get; }

    /// <summary>The junction members that reference the child key, in component order.</summary>
    IReadOnlyList<PropertyInfo> JunctionChildForeignKeys { get; }

    /// <summary>
    /// The number of flattened projection items one many-to-many declaration occupies in a single-query
    /// result row: the link item followed by the child item. <see cref="GetItemTypes(bool)"/> always returns
    /// exactly this many types (only the link item's identity -- the derived <c>JoinIntoLink</c> on SQL, the
    /// junction entity in memory -- is path-dependent), so the row layout and the stitcher's slice stride
    /// cannot disagree.
    /// </summary>
    const int ItemCount = 2;

    /// <summary>
    /// The projection item types the declaration contributes for the given provider path: the derived
    /// <see cref="JoinIntoLink{TParentKey,TChildKey}"/> followed by the child on a SQL provider; the
    /// junction entity followed by the child on the in-memory provider.
    /// </summary>
    /// <param name="isSql">Whether the provider maps SQL and executes the derived link source.</param>
    /// <returns>The link item type followed by the child item type.</returns>
    IReadOnlyList<Type> GetItemTypes(bool isSql);

    /// <summary>Reads the parent key from a link projection item, whatever shape the path produced.</summary>
    Func<object, object?> LinkParentKey { get; }

    /// <summary>Reads the child key from a link projection item.</summary>
    Func<object, object?> LinkChildKey { get; }

    /// <summary>
    /// The occurrence token that distinguishes duplicate <c>(parent, child)</c> junction rows:
    /// the <c>row_number()</c> value on SQL, the junction instance reference in memory.
    /// </summary>
    Func<object, object?> LinkOccurrence { get; }

    /// <summary>
    /// Groups and assigns the many-to-many child collections from rows that carry both the link item
    /// and the child. Unlike the direct declaration it preserves duplicate <c>(parent, child)</c>
    /// junction rows as distinct elements and drops a row whose child (or link) item is null.
    /// </summary>
    /// <typeparam name="TParent">The parent entity type.</typeparam>
    /// <param name="parents">The deduplicated parents in first-occurrence order.</param>
    /// <param name="rows">The denormalized parent/link/child rows in result order.</param>
    void AssignChildrenFromLinks<TParent>(
        IReadOnlyList<TParent> parents,
        IReadOnlyList<JoinIntoManyToManyRow<TParent>> rows);
}

/// <summary>
/// One denormalized many-to-many row: the parent, the path-dependent link item (the derived
/// <see cref="JoinIntoLink{TParentKey,TChildKey}"/> on SQL, the junction entity in memory) and the
/// joined child. Kept as a private-shaped type so the common <c>IJoinIntoSpec</c> row contract stays
/// untouched.
/// </summary>
/// <typeparam name="TParent">The parent entity type.</typeparam>
internal readonly struct JoinIntoManyToManyRow<TParent>
{
    /// <summary>Initializes the row.</summary>
    /// <param name="parent">The parent item.</param>
    /// <param name="link">The link item.</param>
    /// <param name="child">The child item.</param>
    public JoinIntoManyToManyRow(TParent parent, object? link, object? child)
    {
        Parent = parent;
        Link = link;
        Child = child;
    }

    /// <summary>The parent item.</summary>
    public TParent Parent { get; }

    /// <summary>The link item, or <c>null</c> for a non-matching outer join.</summary>
    public object? Link { get; }

    /// <summary>The child item, or <c>null</c> for a non-matching outer join.</summary>
    public object? Child { get; }
}

/// <summary>
/// <see cref="IJoinIntoSpec{TEntity}"/> for a many-to-many navigation. Unlike a one-to-many declaration
/// it contributes <b>two</b> projection items and two joins: a synthetic
/// <see cref="JoinIntoLink{TParentKey,TChildKey}"/> item produced by a derived junction source, followed
/// by the child. The link join connects the parent to the junction; the child join connects the link to
/// the child. Both edges use the declaration's <see cref="JoinType"/>.
/// </summary>
/// <typeparam name="TEntity">The parent entity type the builder projects.</typeparam>
/// <typeparam name="TChild">The child entity type.</typeparam>
internal sealed class JoinIntoManyToManySpec<TEntity, TChild> : IJoinIntoSpec<TEntity>, IJoinIntoManyToManySpec
{
    private readonly JoinIntoSpec<TEntity, TChild> _assignment;
    private readonly EagerLoadSpec<TEntity, TChild, object> _eager;
    private readonly bool _occurrenceByReference;
    private readonly Expression<Func<TEntity, TChild, bool>> _predicate;
    private readonly Func<object, object?> _parentKeyOfAny;

    /// <summary>Initializes the specification from a declared many-to-many relationship.</summary>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate over the parent and child.</param>
    /// <param name="collection">The parent-side collection selector.</param>
    /// <param name="relationship">The declared many-to-many relationship that supplies the junction.</param>
    /// <param name="joinType">The join kind.</param>
    /// <param name="isSql">
    /// Whether the bound context maps SQL. It fixes the shape of the link projection item and therefore
    /// the link accessors: the derived <see cref="JoinIntoLink{TParentKey,TChildKey}"/> on SQL, the
    /// junction entity in memory.
    /// </param>
    public JoinIntoManyToManySpec(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        IRelationshipMetadata relationship,
        JoinType joinType,
        bool isSql)
    {
        Child = child;
        _predicate = predicate;
        Relationship = relationship;
        JoinType = joinType;
        CollectionProperty = JoinIntoSpecHelpers.TryResolveCollectionProperty(collection);

        // The grouping/assignment contract is the same as a one-to-many declaration: the exposed foreign
        // key of a many-to-many relationship is the child-side key and the principal key the parent-side
        // key (see RelationshipMetadata.ResolveKeys), so the child values group by parent exactly like the
        // direct case. Multiplicity (duplicate junction rows) is resolved by the stitcher using the link.
        _assignment = new JoinIntoSpec<TEntity, TChild>(child, predicate, collection, relationship, joinType);
        _occurrenceByReference = !isSql;

        // The many-to-many assignment groups children by the relationship keys exactly like the direct
        // path; it only differs in the multiplicity rule, so it uses its own eager spec rather than the
        // deduplicating one hidden inside the direct assignment.
        var principalKey = relationship.PrincipalKey.Select(static p => p.PropertyInfo).ToArray();
        var foreignKey = relationship.ForeignKey.Select(static p => p.PropertyInfo).ToArray();
        _eager = new EagerLoadSpec<TEntity, TChild, object>(
            collection,
            static _ => throw new NotSupportedException("JoinInto does not run the split eager-load query."),
            JoinIntoSpecHelpers.BuildKeySelector<TEntity>(principalKey),
            JoinIntoSpecHelpers.BuildKeySelector<TChild>(foreignKey));

        var junction = relationship.Junction
            ?? throw new NotSupportedException(
                $"The many-to-many relationship declared on '{typeof(TEntity).Name}' has no junction metadata.");

        JunctionEntityType = junction.JunctionType;
        ParentPrincipalKeys = principalKey;
        ChildKeys = foreignKey;
        JunctionParentForeignKeys = junction.JunctionParentForeignKey.Select(static p => p.PropertyInfo).ToArray();
        JunctionChildForeignKeys = junction.JunctionChildForeignKey.Select(static p => p.PropertyInfo).ToArray();
        _parentKeyOfAny = JoinIntoSpecHelpers.BuildKeyGetter(ParentPrincipalKeys);

        // The accessors are bound once from the path that will materialize the link item: the SQL link's
        // parent/child/occurrence members, or the junction's parent/child foreign-key members with the
        // junction instance itself as the occurrence token (reference identity). A composite junction on
        // SQL uses the flat arity-general carrier (one scalar member per component plus occurrence); the
        // scalar junction keeps the two-member JoinIntoLink shape.
        if (isSql && (JunctionParentForeignKeys.Count != 1 || JunctionChildForeignKeys.Count != 1))
        {
            var carrier = CompositeJunctionLinkFactory.Ensure(
                junction.JunctionParentForeignKey.Select(static p => p.PropertyInfo).ToArray(),
                junction.JunctionChildForeignKey.Select(static p => p.PropertyInfo).ToArray());
            LinkEntityType = carrier.LinkType;
            LinkParentKeyProperties = carrier.ParentKeys;
            LinkChildKeyProperties = carrier.ChildKeys;
            LinkParentKey = JoinIntoSpecHelpers.BuildKeyGetter(carrier.ParentKeys);
            LinkChildKey = JoinIntoSpecHelpers.BuildKeyGetter(carrier.ChildKeys);
            LinkOccurrence = BuildGetter(carrier.Occurrence);
        }
        else
        {
            LinkEntityType = JunctionLinkSourceFactory.GetLinkType(
                JunctionParentForeignKeys[0].PropertyType,
                JunctionChildForeignKeys[0].PropertyType);
            LinkParentKeyProperties = [LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.ParentKey))!];
            LinkChildKeyProperties = [LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.ChildKey))!];

            if (isSql)
            {
                LinkParentKey = BuildGetter(LinkParentKeyProperties[0]);
                LinkChildKey = BuildGetter(LinkChildKeyProperties[0]);
                LinkOccurrence = BuildGetter(LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.Occurrence))!);
            }
            else
            {
                LinkParentKey = JoinIntoSpecHelpers.BuildKeyGetter(JunctionParentForeignKeys);
                LinkChildKey = JoinIntoSpecHelpers.BuildKeyGetter(JunctionChildForeignKeys);
                LinkOccurrence = static junctionItem => junctionItem;
            }
        }

        // The join component correspondence is resolved from the junction metadata's own declared
        // key↔foreign-key pairing (junction.ParentKey[i] ↔ junction.JunctionParentForeignKey[i] and the
        // child analog), never from a blind index over two independently-ordered lists. A declaration
        // whose relationship key order contradicts the junction's declared key order, or whose paired
        // component types do not agree, fails closed here — during preparation, before any connection —
        // instead of silently emitting a self-consistent but cross-joining predicate. The on-SQL list
        // carries one pair per relationship key component (the carrier has one member per junction
        // foreign-key component, so pair i reads carrier member i); the in-memory list stays the single
        // placeholder slot the engine later rebuilds from ParentPrincipalKeys/JunctionParentForeignKeys.
        if (isSql)
        {
            ParentJoinPairs = BuildJoinPairs(relationship.PrincipalKey, junction.ParentKey, junction.JunctionParentForeignKey, "parent");
            ChildJoinPairs = BuildJoinPairs(relationship.ForeignKey, junction.ChildKey, junction.JunctionChildForeignKey, "child");
        }
        else
        {
            ParentJoinPairs = BuildInMemoryPairs(relationship.PrincipalKey, junction.ParentKey, junction.JunctionParentForeignKey, "parent");
            ChildJoinPairs = BuildInMemoryPairs(relationship.ForeignKey, junction.ChildKey, junction.JunctionChildForeignKey, "child");
        }

        // The occurrence token partitions by the junction's complete parent/child key. For a scalar
        // junction this is exactly the two former single member names, so existing plan keys are stable;
        // a composite junction lists every component.
        var occurrenceScope = string.Join(",", JunctionParentForeignKeys.Select(static p => p.Name))
            + "," + string.Join(",", JunctionChildForeignKeys.Select(static p => p.Name));

        Identity = new JoinIntoIdentity
        {
            ParentType = typeof(TEntity),
            ChildType = typeof(TChild),
            JoinType = joinType,
            CollectionMember = CollectionProperty is { } property ? $"{property.DeclaringType?.FullName}.{property.Name}" : null,
            KeyMembers =
            [
                .. relationship.ForeignKey.Select(p => p.PropertyInfo.Name),
                .. relationship.PrincipalKey.Select(p => p.PropertyInfo.Name),
            ],
            JunctionType = junction.JunctionType,
            JunctionKeyMembers =
            [
                junction.JunctionType.FullName ?? junction.JunctionType.Name,
                .. junction.ParentKey.Select(static p => p.PropertyInfo.Name),
                .. junction.ChildKey.Select(static p => p.PropertyInfo.Name),
                .. junction.JunctionParentForeignKey.Select(static p => p.PropertyInfo.Name),
                .. junction.JunctionChildForeignKey.Select(static p => p.PropertyInfo.Name),
            ],
            OccurrenceScope = occurrenceScope,
        };
    }

    /// <summary>The child source, typed for the list terminal that builds the pair command.</summary>
    public EntityBuilder<TChild> Child { get; }

    /// <summary>The relationship supplying the parent/child keys and the junction descriptor.</summary>
    public IRelationshipMetadata Relationship { get; }

    /// <summary>The closed synthetic link type this declaration projects before the child on SQL.</summary>
    public Type LinkEntityType { get; }

    /// <summary>
    /// The link item's parent-component properties, in declared junction foreign-key order. A scalar
    /// junction carries one (<c>ParentKey</c>); a composite junction carries one per component.
    /// </summary>
    internal IReadOnlyList<PropertyInfo> LinkParentKeyProperties { get; }

    /// <summary>
    /// The link item's child-component properties, in declared junction foreign-key order. A scalar
    /// junction carries one (<c>ChildKey</c>); a composite junction carries one per component.
    /// </summary>
    internal IReadOnlyList<PropertyInfo> LinkChildKeyProperties { get; }

    /// <summary>
    /// The parent join component pairs, in declared component order: each relationship principal-key
    /// component with the junction parent foreign-key component the junction metadata declares it
    /// corresponds to. Built and validated during preparation so an inconsistent or reversed declaration
    /// fails closed instead of cross-joining.
    /// </summary>
    internal IReadOnlyList<JoinComponentPair> ParentJoinPairs { get; }

    /// <summary>The child join component pairs, in declared component order.</summary>
    internal IReadOnlyList<JoinComponentPair> ChildJoinPairs { get; }

    /// <summary>
    /// One validated component join pair: a relationship key member and the junction foreign-key member
    /// the relationship metadata declares it corresponds to.
    /// </summary>
    internal readonly struct JoinComponentPair
    {
        /// <summary>Initializes the pair.</summary>
        /// <param name="key">The relationship key member on the entity side.</param>
        /// <param name="junctionForeignKey">The paired junction foreign-key member.</param>
        internal JoinComponentPair(PropertyInfo key, PropertyInfo junctionForeignKey)
        {
            Key = key;
            JunctionForeignKey = junctionForeignKey;
        }

        /// <summary>The relationship key member (principal key for the parent side, foreign key for the child side).</summary>
        internal PropertyInfo Key { get; }

        /// <summary>The junction foreign-key member declared for <see cref="Key"/>.</summary>
        internal PropertyInfo JunctionForeignKey { get; }
    }

    /// <inheritdoc/>
    public Type JunctionEntityType { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PropertyInfo> ParentPrincipalKeys { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PropertyInfo> ChildKeys { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PropertyInfo> JunctionParentForeignKeys { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PropertyInfo> JunctionChildForeignKeys { get; }

    /// <inheritdoc/>
    public Func<object, object?> LinkParentKey { get; }

    /// <inheritdoc/>
    public Func<object, object?> LinkChildKey { get; }

    /// <inheritdoc/>
    public Func<object, object?> LinkOccurrence { get; }

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetItemTypes(bool isSql)
        => isSql ? [LinkEntityType, typeof(TChild)] : [JunctionEntityType, typeof(TChild)];

    /// <summary>
    /// Builds the on-SQL component correspondence from the junction metadata's declared key↔foreign-key
    /// pairing. Each relationship key component must be the same member the junction declares at the same
    /// index, and its nullable-normalized type must match the paired junction foreign key; otherwise the
    /// correspondence cannot be established and the declaration fails closed during preparation.
    /// </summary>
    private static IReadOnlyList<JoinComponentPair> BuildJoinPairs(
        IReadOnlyList<IPropertyMetadata> relationshipKeys,
        IReadOnlyList<IPropertyMetadata> declaredKeys,
        IReadOnlyList<IPropertyMetadata> junctionForeignKeys,
        string side)
        => BuildPairs(relationshipKeys, declaredKeys, junctionForeignKeys, side);

    /// <summary>
    /// Builds the single placeholder pair the in-memory engine stores and later rebuilds from the
    /// junction members; the full declared correspondence is still validated so a malformed declaration
    /// fails closed on both paths.
    /// </summary>
    private static IReadOnlyList<JoinComponentPair> BuildInMemoryPairs(
        IReadOnlyList<IPropertyMetadata> relationshipKeys,
        IReadOnlyList<IPropertyMetadata> declaredKeys,
        IReadOnlyList<IPropertyMetadata> junctionForeignKeys,
        string side)
    {
        var pairs = BuildPairs(relationshipKeys, declaredKeys, junctionForeignKeys, side);
        return [pairs[0]];
    }

    private static IReadOnlyList<JoinComponentPair> BuildPairs(
        IReadOnlyList<IPropertyMetadata> relationshipKeys,
        IReadOnlyList<IPropertyMetadata> declaredKeys,
        IReadOnlyList<IPropertyMetadata> junctionForeignKeys,
        string side)
    {
        if (relationshipKeys.Count == 0 || relationshipKeys.Count != declaredKeys.Count || junctionForeignKeys.Count < relationshipKeys.Count)
            throw new NotSupportedException(
                $"The many-to-many relationship on '{typeof(TEntity).Name}' has a junction whose {side} foreign keys do not cover every composite key component.");

        var pairs = new JoinComponentPair[relationshipKeys.Count];
        for (var i = 0; i < relationshipKeys.Count; i++)
        {
            var relationshipKey = relationshipKeys[i].PropertyInfo;
            var declaredKey = declaredKeys[i].PropertyInfo;
            var junctionForeignKey = junctionForeignKeys[i].PropertyInfo;

            if (!SameMember(relationshipKey, declaredKey))
                throw new NotSupportedException(
                    $"The many-to-many relationship on '{typeof(TEntity).Name}' declares its {side} key components in an order that contradicts the junction's declared {side} key order, so the component correspondence cannot be established (#223).");

            var relationshipType = Nullable.GetUnderlyingType(relationshipKey.PropertyType) ?? relationshipKey.PropertyType;
            var junctionType = Nullable.GetUnderlyingType(junctionForeignKey.PropertyType) ?? junctionForeignKey.PropertyType;
            if (relationshipType != junctionType)
                throw new NotSupportedException(
                    $"The {side} key component '{relationshipKey.Name}' of type {relationshipKey.PropertyType} does not match the junction foreign-key component '{junctionForeignKey.Name}' of type {junctionForeignKey.PropertyType} declared for it, so the component correspondence cannot be established (#223).");

            pairs[i] = new JoinComponentPair(relationshipKey, junctionForeignKey);
        }

        return pairs;
    }

    /// <summary>Compares two mapped members by declaring type, name and type (reflection instances may differ).</summary>
    private static bool SameMember(PropertyInfo left, PropertyInfo right)
        => left == right
            || (left.Name == right.Name
                && left.DeclaringType == right.DeclaringType
                && left.PropertyType == right.PropertyType);

    /// <summary>Builds a boxing getter for a mapped property, used to read a link item uniformly.</summary>
    private static Func<object, object?> BuildGetter(PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(object), "item");
        return Expression.Lambda<Func<object, object?>>(
            Expression.Convert(
                Expression.Property(Expression.Convert(parameter, property.DeclaringType!), property),
                typeof(object)),
            parameter).Compile();
    }

    /// <inheritdoc/>
    public PropertyInfo? CollectionProperty { get; }

    /// <inheritdoc/>
    public JoinType JoinType { get; }

    /// <inheritdoc/>
    public LambdaExpression Predicate => _predicate;

    /// <inheritdoc/>
    public Type ChildEntityType => typeof(TChild);

    /// <inheritdoc/>
    public IReadOnlyList<Type> ItemTypes => [LinkEntityType, typeof(TChild)];

    /// <inheritdoc/>
    public bool IsManyToMany => true;

    /// <inheritdoc/>
    public JoinIntoIdentity Identity { get; }

    /// <inheritdoc/>
    public QueryFilterScope ChildFilterScope => Child.FilterScope;

    /// <inheritdoc/>
    public object? GetParentKey(TEntity parent) => _assignment.GetParentKey(parent);

    /// <inheritdoc/>
    public void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
        => _assignment.AssignChildren(parents, rows);

    /// <inheritdoc/>
    public void AssignChildrenFromLinks<TParent>(
        IReadOnlyList<TParent> parents,
        IReadOnlyList<JoinIntoManyToManyRow<TParent>> rows)
    {
        var grouped = new Dictionary<object, List<TChild>>();
        var seen = new HashSet<ManyToManyLinkKey>();

        foreach (var row in rows)
        {
            if (row.Child is not TChild child)
                continue;

            if (row.Link is null)
                continue;

            // Each distinct junction row contributes one child entry, keyed by the parent, the child
            // foreign key and the occurrence token. Duplicate (parent, child) rows from distinct
            // junctions survive; a cartesian repeat of one junction row collapses.
            var key = new ManyToManyLinkKey(
                LinkParentKey(row.Link),
                LinkChildKey(row.Link),
                LinkOccurrence(row.Link),
                _occurrenceByReference);

            if (!seen.Add(key))
                continue;

            // The eager assignment groups children by the parent key. A many-to-many child carries no
            // parent foreign key of its own (the junction does), so it is added under the parent's key
            // read from the row rather than through AddChildren's child-key selector.
            var parentKey = _parentKeyOfAny(row.Parent!);
            if (parentKey is null)
                continue;

            if (!grouped.TryGetValue(parentKey, out var children))
                grouped[parentKey] = children = [];

            children.Add(child);
        }

        _eager.Assign((IReadOnlyList<TEntity>)(object)parents, grouped);
    }

    /// <summary>
    /// Identity of one matched junction row: the parent key, the junction's child key and the
    /// occurrence token. The occurrence is compared by reference on the in-memory path (the junction
    /// instance is the token) and by value on the SQL path (the boxed <c>row_number()</c>).
    /// </summary>
    private readonly struct ManyToManyLinkKey : IEquatable<ManyToManyLinkKey>
    {
        private readonly object? _parentKey;
        private readonly object? _childKey;
        private readonly object? _occurrence;
        private readonly bool _occurrenceByReference;

        public ManyToManyLinkKey(object? parentKey, object? childKey, object? occurrence, bool occurrenceByReference)
        {
            _parentKey = parentKey;
            _childKey = childKey;
            _occurrence = occurrence;
            _occurrenceByReference = occurrenceByReference;
        }

        public bool Equals(ManyToManyLinkKey other)
            => EqualityComparer<object?>.Default.Equals(_parentKey, other._parentKey)
                && EqualityComparer<object?>.Default.Equals(_childKey, other._childKey)
                && OccurrenceEquals(_occurrence, other._occurrence, _occurrenceByReference);

        public override bool Equals(object? obj) => obj is ManyToManyLinkKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = new HashCode();
                hash.Add(_parentKey);
                hash.Add(_childKey);
                hash.Add(_occurrenceByReference
                    ? RuntimeHelpers.GetHashCode(_occurrence)
                    : _occurrence?.GetHashCode() ?? 0);

                return hash.ToHashCode();
            }
        }

        private static bool OccurrenceEquals(object? x, object? y, bool byReference)
        {
            if (x is null || y is null)
                return x is null && y is null;

            return byReference ? ReferenceEquals(x, y) : x.Equals(y);
        }
    }

    /// <summary>
    /// Builds the specification and its two joins: the derived junction link joined to the parent, then
    /// the child joined to the link. Both joins use the declaration's join kind so a <c>LEFT</c>
    /// declaration keeps childless parents.
    /// </summary>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The user predicate over the parent and child, folded into the child join.</param>
    /// <param name="collection">The parent-side collection selector.</param>
    /// <param name="relationship">The declared many-to-many relationship.</param>
    /// <param name="joinType">The join kind.</param>
    /// <param name="options">The per-join options applied to both edges.</param>
    /// <param name="dataContext">The context the derived junction source is bound to.</param>
    /// <returns>The specification, the parent-to-link join and the link-to-child join.</returns>
    internal static (JoinIntoManyToManySpec<TEntity, TChild> Spec, JoinExpression LinkJoin, JoinExpression ChildJoin) Create(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        IRelationshipMetadata relationship,
        JoinType joinType,
        JoinOptions options,
        IDataContext dataContext)
    {
        // Fold the child's own Where into the caller predicate exactly once, before the specification
        // captures it: the composed predicate then reaches spec.Predicate (and the direct assignment
        // inside it), the link-to-child join condition and its original condition. The child edge is
        // rebuilt from spec.Predicate on the in-memory path, so normalizing here — never again at the
        // edge construction below — keeps every representation on the same single fold.
        predicate = EntityBuilder<TEntity>.FoldJoinIntoChildPredicate(child, predicate);

        var isSql = dataContext.NeedMapping;
        var spec = new JoinIntoManyToManySpec<TEntity, TChild>(child, predicate, collection, relationship, joinType, isSql);
        var junction = relationship.Junction!;

        // parent-to-link: (p, l) => p.<principal key k> = l.<parent component k>, componentwise. The
        // pairing comes from the validated declared correspondence (spec.ParentJoinPairs), not from a
        // blind index: each pair is a relationship principal-key component and the junction foreign-key
        // component the metadata declares it corresponds to, read through the matching link-item member.
        // On SQL a composite junction joins on every principal-key component the junction's parent foreign
        // key covers (extra junction foreign-key columns are occurrence-only). On the in-memory path the
        // list is the single placeholder pair: the link item there is the junction entity itself and
        // BuildInMemoryJoinIntos rebuilds both edges from ParentPrincipalKeys/JunctionParentForeignKeys, so
        // the placeholder keeps the historic single-component shape and never indexes beyond it.
        var parentParameter = Expression.Parameter(typeof(TEntity), "p");
        var linkParameter = Expression.Parameter(spec.LinkEntityType, "l");
        Expression? parentEquality = null;
        for (var i = 0; i < spec.ParentJoinPairs.Count; i++)
        {
            var pair = spec.ParentJoinPairs[i];
            var equality = JoinIntoSpecHelpers.BuildKeyEquality(
                Expression.Property(parentParameter, pair.Key),
                Expression.Property(linkParameter, spec.LinkParentKeyProperties[i]),
                pair.Key.PropertyType,
                "JoinInto");
            parentEquality = parentEquality is null ? equality : Expression.AndAlso(parentEquality, equality);
        }

        var linkCondition = Expression.Lambda(parentEquality!, parentParameter, linkParameter);

        // link-to-child: (p, c) => c.<child key> = <link>.ChildKey && <user predicate>
        //
        // The parent is kept as the bare parent parameter (its dimension is 1), so the SQL builder
        // qualifies it with the parent table alias exactly like the direct one-to-many path. Only the
        // link item is read through the projection slot; the link parameter stays free (it is not the
        // child join's left parameter) and the SQL builder resolves it by projection occurrence. Binding
        // the parent through left.Item1 instead would make the builder treat it as the child join's own
        // joined entity and emit it unqualified ("t3.name = name").
        var projectionType = typeof(Projection<,>).MakeGenericType(typeof(TEntity), spec.LinkEntityType);
        var linkItemParameter = Expression.Parameter(projectionType, "l");
        var linkItem = Expression.Property(linkItemParameter, nameof(Projection<object, object>.Item2));
        var childParameter = predicate.Parameters[1];
        Expression? childEquality = null;
        for (var i = 0; i < spec.ChildJoinPairs.Count; i++)
        {
            var pair = spec.ChildJoinPairs[i];
            var linkChildKeyValue = Expression.Property(linkItem, spec.LinkChildKeyProperties[i]);
            var childKeyValue = Expression.Property(childParameter, pair.Key);
            var equality = JoinIntoSpecHelpers.BuildKeyEquality(childKeyValue, linkChildKeyValue, pair.Key.PropertyType, "JoinInto");
            childEquality = childEquality is null ? equality : Expression.AndAlso(childEquality, equality);
        }

        var childCondition = Expression.Lambda(
            Expression.AndAlso(childEquality!, predicate.Body),
            predicate.Parameters[0],
            childParameter);

        var linkJoin = new JoinExpression(linkCondition, joinType)
        {
            // The derived row_number() link source is SQL-only; the in-memory engine joins the junction
            // entity directly and rebuilds both edges in BuildInMemoryJoinIntos, so its stored From is a
            // placeholder that still republishes the junction mapping the declaration depends on.
            From = dataContext.NeedMapping
                ? JunctionLinkSourceFactory.BuildSource(dataContext, relationship)
                : JunctionLinkSourceFactory.GetInMemorySource(relationship),
            Strictness = options.Strictness ?? JoinStrictness.Default,
            IsGlobal = options.IsGlobal,
            JoinHint = options.JoinHint,
            TableHints = options.TableHints,
            SuppressCartesianWarning = options.CartesianWarningSuppressed,
        };

        var childJoin = new JoinExpression(childCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(child.DataProvider, child),
            Strictness = options.Strictness ?? JoinStrictness.Default,
            IsGlobal = options.IsGlobal,
            JoinHint = options.JoinHint,
            TableHints = options.TableHints,
            SuppressCartesianWarning = options.CartesianWarningSuppressed,
        };

        // A JoinOptions source lock targets the user-visible child table (the derived junction-link join is
        // not a physical source a lock can address).
        if (options.SourceLock is not null)
            childJoin.SetFrom(childJoin.From.WithSourceLock(options.SourceLock));

        return (spec, linkJoin, childJoin);
    }
}
