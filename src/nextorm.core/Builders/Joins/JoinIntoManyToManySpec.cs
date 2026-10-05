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

    /// <summary>The parent-side principal key the junction's parent foreign key matches.</summary>
    PropertyInfo ParentPrincipalKey { get; }

    /// <summary>The child-side foreign key the junction's child foreign key matches.</summary>
    PropertyInfo ChildKey { get; }

    /// <summary>The junction member that references the parent key.</summary>
    PropertyInfo JunctionParentForeignKey { get; }

    /// <summary>The junction member that references the child key.</summary>
    PropertyInfo JunctionChildForeignKey { get; }

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
        var principalKey = relationship.PrincipalKey[0].PropertyInfo;
        var foreignKey = relationship.ForeignKey[0].PropertyInfo;
        _eager = new EagerLoadSpec<TEntity, TChild, object>(
            collection,
            static _ => throw new NotSupportedException("JoinInto does not run the split eager-load query."),
            JoinIntoSpecHelpers.BuildKeySelector<TEntity>(principalKey),
            JoinIntoSpecHelpers.BuildKeySelector<TChild>(foreignKey));

        var junction = relationship.Junction
            ?? throw new NotSupportedException(
                $"The many-to-many relationship declared on '{typeof(TEntity).Name}' has no junction metadata.");

        JunctionEntityType = junction.JunctionType;
        ParentPrincipalKey = relationship.PrincipalKey[0].PropertyInfo;
        ChildKey = relationship.ForeignKey[0].PropertyInfo;
        JunctionParentForeignKey = junction.JunctionParentForeignKey[0].PropertyInfo;
        JunctionChildForeignKey = junction.JunctionChildForeignKey[0].PropertyInfo;
        _parentKeyOfAny = BuildGetter(ParentPrincipalKey);

        LinkEntityType = JunctionLinkSourceFactory.GetLinkType(
            JunctionParentForeignKey.PropertyType,
            JunctionChildForeignKey.PropertyType);

        // The accessors are bound once from the path that will materialize the link item: the SQL link's
        // ParentKey/ChildKey/Occurrence properties, or the junction's parent/child foreign-key members
        // with the junction instance itself as the occurrence token (reference identity).
        if (isSql)
        {
            LinkParentKey = BuildGetter(LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.ParentKey))!);
            LinkChildKey = BuildGetter(LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.ChildKey))!);
            LinkOccurrence = BuildGetter(LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.Occurrence))!);
        }
        else
        {
            LinkParentKey = BuildGetter(JunctionParentForeignKey);
            LinkChildKey = BuildGetter(JunctionChildForeignKey);
            LinkOccurrence = static junctionItem => junctionItem;
        }

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
            OccurrenceScope =
                $"{junction.JunctionParentForeignKey[0].PropertyInfo.Name},{junction.JunctionChildForeignKey[0].PropertyInfo.Name}",
        };
    }

    /// <summary>The child source, typed for the list terminal that builds the pair command.</summary>
    public EntityBuilder<TChild> Child { get; }

    /// <summary>The relationship supplying the parent/child keys and the junction descriptor.</summary>
    public IRelationshipMetadata Relationship { get; }

    /// <summary>The closed synthetic link type this declaration projects before the child on SQL.</summary>
    public Type LinkEntityType { get; }

    /// <inheritdoc/>
    public Type JunctionEntityType { get; }

    /// <inheritdoc/>
    public PropertyInfo ParentPrincipalKey { get; }

    /// <inheritdoc/>
    public PropertyInfo ChildKey { get; }

    /// <inheritdoc/>
    public PropertyInfo JunctionParentForeignKey { get; }

    /// <inheritdoc/>
    public PropertyInfo JunctionChildForeignKey { get; }

    /// <inheritdoc/>
    public Func<object, object?> LinkParentKey { get; }

    /// <inheritdoc/>
    public Func<object, object?> LinkChildKey { get; }

    /// <inheritdoc/>
    public Func<object, object?> LinkOccurrence { get; }

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetItemTypes(bool isSql)
        => isSql ? [LinkEntityType, typeof(TChild)] : [JunctionEntityType, typeof(TChild)];

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
        var spec = new JoinIntoManyToManySpec<TEntity, TChild>(child, predicate, collection, relationship, joinType, dataContext.NeedMapping);
        var junction = relationship.Junction!;

        var parentKey = relationship.PrincipalKey[0].PropertyInfo;
        var childKey = relationship.ForeignKey[0].PropertyInfo;
        var linkParentKey = spec.LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.ParentKey))!;
        var linkChildKey = spec.LinkEntityType.GetProperty(nameof(JoinIntoLink<object, object>.ChildKey))!;

        // parent-to-link: (p, l) => p.<principal key> = l.ParentKey
        var parentParameter = Expression.Parameter(typeof(TEntity), "p");
        var linkParameter = Expression.Parameter(spec.LinkEntityType, "l");
        var linkCondition = Expression.Lambda(
            JoinIntoSpecHelpers.BuildKeyEquality(
                Expression.Property(parentParameter, parentKey),
                Expression.Property(linkParameter, linkParentKey),
                parentKey.PropertyType,
                "JoinInto"),
            parentParameter,
            linkParameter);

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
        var childParameter = predicate.Parameters[1];
        var linkChildKeyValue = Expression.Property(Expression.Property(linkItemParameter, nameof(Projection<object, object>.Item2)), linkChildKey);
        var childKeyValue = Expression.Property(childParameter, childKey);
        var linkEquality = JoinIntoSpecHelpers.BuildKeyEquality(childKeyValue, linkChildKeyValue, childKey.PropertyType, "JoinInto");
        var childCondition = Expression.Lambda(
            Expression.AndAlso(linkEquality, predicate.Body),
            predicate.Parameters[0],
            childParameter);

        var linkJoin = new JoinExpression(linkCondition, joinType)
        {
            From = JunctionLinkSourceFactory.BuildSource(dataContext, relationship),
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

        return (spec, linkJoin, childJoin);
    }
}
