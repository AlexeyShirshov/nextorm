using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Stable identity of one <c>JoinInto</c> declaration, folded into the join's plan key so two
/// declarations that render the same SQL but target different keys/collections do not share a cached
/// plan. The predicate itself is compared separately through the join condition.
/// </summary>
internal sealed class JoinIntoIdentity : IEquatable<JoinIntoIdentity>
{
    /// <summary>The parent entity type.</summary>
    public required Type ParentType { get; init; }

    /// <summary>The joined child entity type.</summary>
    public required Type ChildType { get; init; }

    /// <summary>The join kind.</summary>
    public required JoinType JoinType { get; init; }

    /// <summary>The qualified name of the parent collection member, or <see langword="null"/> when the selector is not a property access.</summary>
    public string? CollectionMember { get; init; }

    /// <summary>The foreign-key and principal-key member names that tie the two sides together.</summary>
    public required string[] KeyMembers { get; init; }

    /// <inheritdoc/>
    public bool Equals(JoinIntoIdentity? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (ParentType != other.ParentType || ChildType != other.ChildType || JoinType != other.JoinType) return false;
        if (!string.Equals(CollectionMember, other.CollectionMember, StringComparison.Ordinal)) return false;
        if (KeyMembers.Length != other.KeyMembers.Length) return false;

        for (var i = 0; i < KeyMembers.Length; i++)
        {
            if (!string.Equals(KeyMembers[i], other.KeyMembers[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as JoinIntoIdentity);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = new XxHash32();
            hash.Add(ParentType);
            hash.Add(ChildType);
            hash.Add((int)JoinType);
            if (CollectionMember is not null)
                hash.Add(CollectionMember);

            foreach (var key in KeyMembers)
                hash.Add(key);

            return hash.ToHashCode();
        }
    }
}

/// <summary>
/// A single <c>JoinInto</c> declaration stored on an <see cref="EntityBuilder{TEntity}"/>: the child
/// source, the join predicate, the parent-side collection member, the join kind and the key metadata
/// used by the list terminal to group the denormalized rows and assign the child collection.
/// </summary>
/// <typeparam name="TEntity">The parent entity type the builder projects.</typeparam>
internal interface IJoinIntoSpec<TEntity>
{
    /// <summary>The join kind; only <see cref="JoinType.Inner"/> and <see cref="JoinType.Left"/> are stored.</summary>
    JoinType JoinType { get; }

    /// <summary>The parent-side collection property selected by the <c>collection</c> argument, or <c>null</c> when it is not a property access.</summary>
    PropertyInfo? CollectionProperty { get; }

    /// <summary>The <c>ON</c> predicate over the parent and child entities.</summary>
    LambdaExpression Predicate { get; }

    /// <summary>The joined child entity type.</summary>
    Type ChildEntityType { get; }

    /// <summary>The declaration identity used by the plan cache.</summary>
    JoinIntoIdentity Identity { get; }

    /// <summary>
    /// Whether the child side ignores its own global query filters. Carried onto the synthesized join so
    /// the child's filter decision is independent of the parent's <c>IgnoreFilters()</c>.
    /// </summary>
    bool IgnoreChildFilters { get; }

    /// <summary>Reads the parent key used to deduplicate the denormalized parents.</summary>
    /// <param name="parent">The parent row.</param>
    /// <returns>The key value.</returns>
    object? GetParentKey(TEntity parent);

    /// <summary>
    /// Groups the child values of this declaration from the denormalized rows and assigns them to the
    /// matching parents, reusing the eager-load grouping/assignment semantics of <c>LoadWith</c>.
    /// </summary>
    /// <param name="parents">The deduplicated parents in first-occurrence order.</param>
    /// <param name="rows">The denormalized parent/child pairs in result order.</param>
    void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows);
}

/// <summary>
/// <see cref="IJoinIntoSpec{TEntity}"/> whose parent/child keys come from an explicitly declared
/// relationship (the overloads without key selectors). The foreign key is on the child side and the
/// principal key on the parent side.
/// </summary>
/// <typeparam name="TEntity">The parent entity type the builder projects.</typeparam>
/// <typeparam name="TChild">The child entity type.</typeparam>
internal sealed class JoinIntoSpec<TEntity, TChild> : IJoinIntoSpec<TEntity>
{
    private readonly Expression<Func<TEntity, TChild, bool>> _predicate;
    private readonly Func<TEntity, object?> _parentKey;
    private readonly Func<TChild, object?>? _childIdentity;
    private readonly EagerLoadSpec<TEntity, TChild, object> _eager;

    /// <summary>Initializes the specification from a declared relationship.</summary>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate.</param>
    /// <param name="collection">The parent-side collection selector.</param>
    /// <param name="relationship">The declared relationship that supplies the parent/child keys.</param>
    /// <param name="joinType">The join kind.</param>
    public JoinIntoSpec(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        IRelationshipMetadata relationship,
        JoinType joinType)
    {
        Child = child;
        _predicate = predicate;
        CollectionProperty = JoinIntoSpecHelpers.TryResolveCollectionProperty(collection);
        Relationship = relationship;
        JoinType = joinType;

        var principalKey = relationship.PrincipalKey[0].PropertyInfo;
        var foreignKey = relationship.ForeignKey[0].PropertyInfo;

        var parentKeyExpression = JoinIntoSpecHelpers.BuildKeySelector<TEntity>(principalKey);
        var childKeyExpression = JoinIntoSpecHelpers.BuildKeySelector<TChild>(foreignKey);

        _parentKey = parentKeyExpression.Compile();
        _childIdentity = JoinIntoSpecHelpers.BuildIdentitySelector<TChild>();
        _eager = new EagerLoadSpec<TEntity, TChild, object>(
            collection,
            static _ => throw new NotSupportedException("JoinInto does not run the split eager-load query."),
            parentKeyExpression,
            childKeyExpression);

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
        };
    }

    /// <summary>The child source, typed for the list terminal that builds the pair command.</summary>
    public EntityBuilder<TChild> Child { get; }

    /// <summary>The relationship supplying the parent/child key properties.</summary>
    public IRelationshipMetadata Relationship { get; }

    /// <inheritdoc/>
    public PropertyInfo? CollectionProperty { get; }

    /// <inheritdoc/>
    public JoinType JoinType { get; }

    /// <inheritdoc/>
    public LambdaExpression Predicate => _predicate;

    /// <inheritdoc/>
    public Type ChildEntityType => typeof(TChild);

    /// <inheritdoc/>
    public JoinIntoIdentity Identity { get; }

    /// <inheritdoc/>
    public bool IgnoreChildFilters => Child.IgnoresFilters;

    /// <inheritdoc/>
    public object? GetParentKey(TEntity parent) => _parentKey(parent);

    /// <inheritdoc/>
    public void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
    {
        var grouped = new Dictionary<object, List<TChild>>();
        var seen = new HashSet<(object? ParentKey, object? ChildIdentity)>();

        foreach (var (parent, childValue) in rows)
        {
            if (childValue is not TChild child)
                continue;

            // Deduplicate within each parent (identified by the same key the eager assignment groups
            // by), so a child identity shared by two parents is kept for both, while a cartesian repeat
            // of one parent's child collapses. The identity is the child's key, or its reference when it
            // has none.
            var identity = _childIdentity is not null ? _childIdentity(child) : child;
            if (!seen.Add((_parentKey(parent), identity)))
                continue;

            _eager.AddChildren(grouped, [child]);
        }

        _eager.Assign(parents, grouped);
    }
}

/// <summary>
/// <see cref="IJoinIntoSpec{TEntity}"/> whose parent/child keys are the explicit selectors of the fallback
/// overload, for an entity pair without declared relationship metadata.
/// </summary>
/// <typeparam name="TEntity">The parent entity type the builder projects.</typeparam>
/// <typeparam name="TChild">The child entity type.</typeparam>
/// <typeparam name="TKey">The non-nullable key type shared by the two selectors.</typeparam>
internal sealed class JoinIntoSpec<TEntity, TChild, TKey> : IJoinIntoSpec<TEntity>
    where TKey : notnull
{
    private readonly Expression<Func<TEntity, TChild, bool>> _predicate;
    private readonly Func<TEntity, object?> _parentKey;
    private readonly Func<TChild, object?>? _childIdentity;
    private readonly EagerLoadSpec<TEntity, TChild, TKey> _eager;

    /// <summary>Initializes the specification from explicit key selectors.</summary>
    /// <param name="child">The child source.</param>
    /// <param name="predicate">The join predicate.</param>
    /// <param name="collection">The parent-side collection selector.</param>
    /// <param name="parentKey">Selects the parent key.</param>
    /// <param name="childKey">Selects the child key.</param>
    /// <param name="joinType">The join kind.</param>
    public JoinIntoSpec(
        EntityBuilder<TChild> child,
        Expression<Func<TEntity, TChild, bool>> predicate,
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey,
        JoinType joinType)
    {
        Child = child;
        _predicate = predicate;
        CollectionProperty = JoinIntoSpecHelpers.TryResolveCollectionProperty(collection);
        ParentKey = parentKey;
        ChildKey = childKey;
        JoinType = joinType;

        var compiledParentKey = parentKey.Compile();
        _parentKey = parent => compiledParentKey(parent);
        _childIdentity = JoinIntoSpecHelpers.BuildIdentitySelector<TChild>();
        _eager = new EagerLoadSpec<TEntity, TChild, TKey>(
            collection,
            static _ => throw new NotSupportedException("JoinInto does not run the split eager-load query."),
            parentKey,
            childKey);

        Identity = new JoinIntoIdentity
        {
            ParentType = typeof(TEntity),
            ChildType = typeof(TChild),
            JoinType = joinType,
            CollectionMember = CollectionProperty is { } property ? $"{property.DeclaringType?.FullName}.{property.Name}" : null,
            KeyMembers =
            [
                JoinIntoSpecHelpers.RequireMemberName(childKey),
                JoinIntoSpecHelpers.RequireMemberName(parentKey),
            ],
        };
    }

    /// <summary>The child source, typed for the list terminal that builds the pair command.</summary>
    public EntityBuilder<TChild> Child { get; }

    /// <summary>Selects the parent key used to group the rows.</summary>
    public Expression<Func<TEntity, TKey>> ParentKey { get; }

    /// <summary>Selects the child key used to group the rows.</summary>
    public Expression<Func<TChild, TKey>> ChildKey { get; }

    /// <inheritdoc/>
    public PropertyInfo? CollectionProperty { get; }

    /// <inheritdoc/>
    public JoinType JoinType { get; }

    /// <inheritdoc/>
    public LambdaExpression Predicate => _predicate;

    /// <inheritdoc/>
    public Type ChildEntityType => typeof(TChild);

    /// <inheritdoc/>
    public JoinIntoIdentity Identity { get; }

    /// <inheritdoc/>
    public bool IgnoreChildFilters => Child.IgnoresFilters;

    /// <inheritdoc/>
    public object? GetParentKey(TEntity parent) => _parentKey(parent);

    /// <inheritdoc/>
    public void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        var seen = new HashSet<(object? ParentKey, object? ChildIdentity)>();

        foreach (var (parent, childValue) in rows)
        {
            if (childValue is not TChild child)
                continue;

            // Deduplicate within each parent (identified by the same key the eager assignment groups
            // by), so a child identity shared by two parents is kept for both, while a cartesian repeat
            // of one parent's child collapses. The identity is the child's key, or its reference when it
            // has none.
            var identity = _childIdentity is not null ? _childIdentity(child) : child;
            if (!seen.Add((_parentKey(parent), identity)))
                continue;

            _eager.AddChildren(grouped, [child]);
        }

        _eager.Assign(parents, grouped);
    }
}

/// <summary>
/// Expression and reflection helpers shared by the <c>JoinInto</c> specs and the builder validation.
/// </summary>
internal static class JoinIntoSpecHelpers
{
    /// <summary>
    /// Resolves the property selected by a collection selector, unwrapping a boxing/conversion node; a
    /// field or computed expression yields <c>null</c>.
    /// </summary>
    internal static PropertyInfo? TryResolveCollectionProperty<TEntity, TChild>(
        Expression<Func<TEntity, ICollection<TChild>>> collection)
        => StripConvert(collection.Body) is MemberExpression { Member: PropertyInfo property } ? property : null;

    /// <summary>
    /// Builds the key-equality predicate <c>parentKey == childKey</c> for the single-query join. The
    /// split path compares keys with the default equality comparer (<c>Contains</c>), which works for
    /// any type; the joined predicate needs a translatable <c>Equal</c> node, so a key type without an
    /// equality operator (for example a struct with no <c>==</c> overload, or a reference type whose
    /// <c>==</c> would degrade to reference equality) is reported instead of silently producing a wrong
    /// or crashing query.
    /// </summary>
    /// <param name="parentKey">The re-rooted parent key expression.</param>
    /// <param name="childKey">The re-rooted child key expression.</param>
    /// <param name="keyType">The key type selected by both sides.</param>
    /// <param name="modeName">The mode name to mention in the rejection.</param>
    /// <returns>The equality predicate.</returns>
    /// <exception cref="NotSupportedException">The key type does not support value equality.</exception>
    internal static Expression BuildKeyEquality(Expression parentKey, Expression childKey, Type keyType, string modeName)
    {
        Expression equality;
        try
        {
            equality = Expression.Equal(parentKey, childKey);
        }
        catch (InvalidOperationException ex)
        {
            throw new NotSupportedException(
                $"{modeName} cannot compare the key type '{keyType.Name}' because it does not define an " +
                $"equality operator, so no join predicate can be built. Use split-query loading (omit {modeName}), " +
                "which compares keys with the default equality comparer.", ex);
        }

        // For a reference type without a user-defined equality operator Expression.Equal produces
        // reference equality, which would compare child/parent instances instead of key values and match
        // nothing over a join; reject it rather than silently emitting the wrong predicate.
        if (equality is BinaryExpression { Method: null } && !keyType.IsValueType)
            throw new NotSupportedException(
                $"{modeName} cannot compare the reference key type '{keyType.Name}' by value because it does " +
                $"not define an equality operator, so the join predicate would compare references. Use split-query " +
                $"loading (omit {modeName}), which compares keys with the default equality comparer.");

        return equality;
    }

    /// <summary>
    /// Resolves the property type selected by a key selector, unwrapping any conversion node, so the
    /// explicit-key overload can reject a mismatch; a non-member key expression yields <c>null</c>.
    /// </summary>
    internal static Type? TryResolveKeyType(LambdaExpression key)
        => StripConvert(key.Body) is MemberExpression member ? Unwrap(member.Type) : null;

    /// <summary>Resolves the member name selected by a key selector, or <c>null</c> when it is not a member access.</summary>
    internal static string? TryResolveMemberName(LambdaExpression key)
        => StripConvert(key.Body) is MemberExpression member ? member.Member.Name : null;

    /// <summary>
    /// Resolves the member name selected by a key selector, rejecting a computed selector: the name is
    /// part of the <c>JoinInto</c> plan identity, and a non-member expression has no unambiguous stable
    /// representation (two different computed keys would otherwise share a cached plan).
    /// </summary>
    internal static string RequireMemberName(LambdaExpression key)
        => TryResolveMemberName(key) ?? throw new NotSupportedException(
            "JoinInto explicit key selectors must be simple property or field accesses; " +
            "a computed key cannot be part of the plan identity.");

    /// <summary>
    /// Builds a boxing key selector <c>x =&gt; (object)x.Member</c> for a mapped property, used to reuse
    /// the eager-load grouping over a runtime-known key type.
    /// </summary>
    internal static Expression<Func<T, object>> BuildKeySelector<T>(PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, object>>(
            Expression.Convert(Expression.Property(parameter, property), typeof(object)),
            parameter);
    }

    /// <summary>
    /// Builds a selector over the mapped key of <typeparamref name="T"/>, used to deduplicate the child
    /// rows a cartesian (multi-collection) join repeats. A single-column key selects the boxed value; a
    /// composite key selects a structurally-equal <see cref="JoinIntoKey"/>. Returns <c>null</c> when the
    /// type declares no key property, in which case the caller falls back to reference identity. The
    /// compiled selector is cached per type; a <see langword="null"/> result is not cached so a type can
    /// be re-probed after its metadata is registered.
    /// </summary>
    internal static Func<T, object?>? BuildIdentitySelector<T>()
    {
        if (IdentitySelectorCache.TryGetValue(typeof(T), out var cached))
            return (Func<T, object?>?)cached;

        var selector = BuildIdentitySelectorCore<T>();
        if (selector is not null)
            IdentitySelectorCache[typeof(T)] = selector;

        return selector;
    }

    private static readonly ConcurrentDictionary<Type, Delegate> IdentitySelectorCache = new();

    private static Func<T, object?>? BuildIdentitySelectorCore<T>()
    {
        if (!DataContextCache.Metadata.TryGetValue(typeof(T), out var metadata))
            return null;

        var keys = new List<PropertyInfo>();
        foreach (var property in metadata.Properties)
        {
            if (property.IsKey)
                keys.Add(property.PropertyInfo);
        }

        if (keys.Count == 0)
            return null;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression body;
        if (keys.Count == 1)
        {
            body = Expression.Convert(Expression.Property(parameter, keys[0]), typeof(object));
        }
        else
        {
            var values = new Expression[keys.Count];
            for (var i = 0; i < keys.Count; i++)
                values[i] = Expression.Convert(Expression.Property(parameter, keys[i]), typeof(object));

            var constructor = typeof(JoinIntoKey).GetConstructor([typeof(object[])])!;
            body = Expression.Convert(
                Expression.New(constructor, Expression.NewArrayInit(typeof(object), values)),
                typeof(object));
        }

        return Expression.Lambda<Func<T, object?>>(body, parameter).Compile();
    }

    /// <summary>Unwraps a <see cref="Nullable{T}"/> type to its underlying type.</summary>
    internal static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// Re-roots <paramref name="body"/> onto <paramref name="replacement"/> by replacing exactly the
    /// parameter <paramref name="source"/> (matched by reference), leaving same-typed parameters that
    /// belong to a nested lambda untouched.
    /// </summary>
    /// <param name="body">The expression body to rewrite.</param>
    /// <param name="source">The parameter to replace.</param>
    /// <param name="replacement">The expression that takes its place.</param>
    /// <returns>The rewritten expression body.</returns>
    internal static Expression ReplaceParameter(Expression body, ParameterExpression source, Expression replacement)
        => new TargetParameterReplacer(source, replacement).Visit(body);

    private sealed class TargetParameterReplacer(ParameterExpression target, Expression replacement) : ExpressionVisitor
    {
        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
            => ReferenceEquals(node, target) ? replacement : base.VisitParameter(node);
    }

    private static Expression StripConvert(Expression body)
    {
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        return body;
    }
}

/// <summary>
/// Structural identity of an entity's composite key, used to deduplicate the child rows a cartesian
/// join repeats when the entity declares more than one key property.
/// </summary>
internal readonly struct JoinIntoKey : IEquatable<JoinIntoKey>
{
    private readonly object?[] _values;

    /// <summary>Initializes the identity from the key values in metadata order.</summary>
    /// <param name="values">The key values.</param>
    public JoinIntoKey(object?[] values) => _values = values;

    /// <inheritdoc/>
    public bool Equals(JoinIntoKey other)
    {
        if (_values.Length != other._values.Length)
            return false;

        for (var i = 0; i < _values.Length; i++)
        {
            if (!EqualityComparer<object?>.Default.Equals(_values[i], other._values[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is JoinIntoKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _values)
            hash.Add(value);

        return hash.ToHashCode();
    }
}
