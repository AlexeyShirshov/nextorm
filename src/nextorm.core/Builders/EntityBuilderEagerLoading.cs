using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// A single split-query eager-load specification stored on an <see cref="EntityBuilder{TEntity}"/>:
/// the parent-side collection member, the factory that builds the child query, and the two key
/// selectors used to join the two result sets in memory.
/// </summary>
/// <typeparam name="TEntity">The parent entity type the builder projects.</typeparam>
internal interface IEagerLoadSpec<TEntity>
{
    /// <summary>
    /// Loads and assigns the child collection for every parent using two sequential round trips
    /// (no N+1).
    /// </summary>
    /// <param name="context">The data context the child query is built from.</param>
    /// <param name="parentScope">The parent command's global-query-filter scope, inherited by the child side.</param>
    /// <param name="parents">The already materialized parents, in the order the parent query returned them.</param>
    void Execute(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents);

    /// <summary>
    /// Asynchronously loads and assigns the child collection for every parent using two sequential
    /// round trips (no N+1).
    /// </summary>
    /// <param name="context">The data context the child query is built from.</param>
    /// <param name="parentScope">The parent command's global-query-filter scope, inherited by the child side.</param>
    /// <param name="parents">The already materialized parents, in the order the parent query returned them.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the children have been loaded and assigned.</returns>
    Task ExecuteAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken);

    /// <summary>
    /// The parent collection member the specification targets, or <c>null</c> when the collection
    /// expression is not a direct field/property. Two specifications sharing a member are duplicates.
    /// </summary>
    MemberInfo? CollectionMember { get; }

    /// <summary>
    /// Builds the single-query (<c>EagerLoadMode.SingleQuery</c>) equivalent of this split-query specification: an
    /// explicit-key <c>JoinInto</c> declaration whose <c>ON</c> predicate equates the two key selectors
    /// and folds in the child query's own <c>Where</c> condition, re-rooted onto the child join
    /// parameter, so a <c>LEFT JOIN</c> keeps childless parents while filtering the children.
    /// </summary>
    /// <param name="context">The data context used to build the child query.</param>
    /// <param name="childSource">Receives the resolved <c>FROM</c> source of the joined child.</param>
    /// <returns>The join specification consumed by the pair command and the stitcher.</returns>
    IJoinIntoSpec<TEntity> ToJoinIntoSpec(IDataContext context, out FromExpression childSource);
}

/// <summary>
/// The default <see cref="IEagerLoadSpec{TEntity}"/>: materializes the children of one parent type in
/// key chunks and stitches them onto the parents in memory. Created by the builder's
/// <c>LoadWith</c> modifier.
/// </summary>
/// <typeparam name="TEntity">The parent entity type.</typeparam>
/// <typeparam name="TChild">The child entity type.</typeparam>
/// <typeparam name="TKey">The key type shared by the two key selectors.</typeparam>
internal sealed class EagerLoadSpec<TEntity, TChild, TKey> : IEagerLoadSpec<TEntity>
    where TKey : notnull
{
    private const int KeyChunkSize = 1000;

    private readonly Func<TEntity, ICollection<TChild>> _collectionGetter;
    private readonly Action<TEntity, ICollection<TChild>>? _collectionSetter;
    private readonly Func<IDataContext, EntityBuilder<TChild>> _childQuery;
    private readonly Func<TEntity, TKey> _parentKey;
    private readonly Func<TChild, TKey> _childKey;
    private readonly Expression<Func<TEntity, ICollection<TChild>>> _collection;
    private readonly Expression<Func<TEntity, TKey>> _parentKeyExpression;
    private readonly Expression<Func<TChild, TKey>> _childKeyExpression;
    private readonly MethodInfo _containsMethod;
    private readonly MemberInfo? _member;

    /// <summary>
    /// Initializes the specification, compiling the collection accessor, the two key selectors and the
    /// optional collection setter once.
    /// </summary>
    /// <param name="collection">The parent-side collection member to fill.</param>
    /// <param name="childQuery">The factory that builds the child query from a data context.</param>
    /// <param name="parentKey">The parent-side key selector.</param>
    /// <param name="childKey">The child-side key selector.</param>
    public EagerLoadSpec(
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Func<IDataContext, EntityBuilder<TChild>> childQuery,
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(childQuery);
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(childKey);

        _collectionGetter = collection.Compile();
        _childQuery = childQuery;
        _parentKey = parentKey.Compile();
        _childKey = childKey.Compile();
        _collection = collection;
        _parentKeyExpression = parentKey;
        _childKeyExpression = childKey;
        _containsMethod = typeof(List<TKey>).GetMethod(nameof(List<TKey>.Contains))!;

        var body = collection.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        if (body is MemberExpression member)
        {
            _member = member.Member;
            if (CanAssign(member.Member))
            {
                var valueParameter = Expression.Parameter(typeof(ICollection<TChild>), "value");
                _collectionSetter = Expression.Lambda<Action<TEntity, ICollection<TChild>>>(
                    Expression.Assign(member, Expression.Convert(valueParameter, member.Type)),
                    collection.Parameters[0],
                    valueParameter).Compile();
            }
        }
    }

    /// <inheritdoc/>
    public MemberInfo? CollectionMember => _member;

    /// <summary>The parent-side collection selector, retained for the single-query join conversion.</summary>
    internal Expression<Func<TEntity, ICollection<TChild>>> CollectionExpression => _collection;

    /// <summary>The parent-side key selector, retained for the single-query join conversion.</summary>
    internal Expression<Func<TEntity, TKey>> ParentKeyExpression => _parentKeyExpression;

    /// <summary>The child-side key selector, retained for the single-query join conversion.</summary>
    internal Expression<Func<TChild, TKey>> ChildKeyExpression => _childKeyExpression;

    /// <summary>Returns the parent key value the grouping matches on.</summary>
    /// <param name="parent">The parent row.</param>
    /// <returns>The key value.</returns>
    internal object? ParentKeyOf(TEntity parent) => _parentKey(parent);

    /// <summary>
    /// Groups the denormalized <c>JoinInto</c> child rows by their key and assigns them onto the parents,
    /// deduplicating repeated <c>(parent, child)</c> pairs the cartesian join produces. Reuses the same
    /// grouping and assignment as the split loader, so the two modes share one stitching contract.
    /// </summary>
    /// <param name="parents">The deduplicated parents in first-occurrence order.</param>
    /// <param name="rows">The denormalized parent/child pairs in result order.</param>
    internal void AssignChildrenFromRows(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        var seen = new HashSet<(object? ParentKey, object? ChildIdentity)>();
        var identity = JoinIntoSpecHelpers.BuildIdentitySelector<TChild>();

        foreach (var (parent, childValue) in rows)
        {
            if (childValue is not TChild child)
                continue;

            var childIdentity = identity is not null ? identity(child) : child;
            if (!seen.Add((_parentKey(parent), childIdentity)))
                continue;

            AddChildren(grouped, [child]);
        }

        Assign(parents, grouped);
    }

    /// <inheritdoc/>
    IJoinIntoSpec<TEntity> IEagerLoadSpec<TEntity>.ToJoinIntoSpec(IDataContext context, out FromExpression childSource)
    {
        ArgumentNullException.ThrowIfNull(context);

        var child = _childQuery(context);

        var unsupported = child.UnsupportedSingleQueryChildShapes();
        if (unsupported.Count > 0)
            throw new NotSupportedException(
                $"LoadWith with EagerLoadMode.SingleQuery cannot fold the child query's {string.Join(", ", unsupported)} into the join " +
                "predicate: only the child's own Where is supported. Remove the modifier or use split-query " +
                "loading (omit the SingleQuery mode), which honors every child-query shape.");

        childSource = child.ResolveSource() ?? context.GetFrom(typeof(TChild), null)
            ?? throw new BuildSqlCommandException(
                $"The single-query child source for type {typeof(TChild)} could not be resolved.");

        return new EagerLoadJoinIntoSpec<TEntity, TChild, TKey>(this, child.Condition, child.FilterScope);
    }

    /// <summary>Whether the member can be assigned from a fresh list.</summary>
    private static bool CanAssign(MemberInfo member) => member switch
    {
        PropertyInfo property => property.CanWrite,
        FieldInfo field => !field.IsInitOnly,
        _ => false,
    };

    void IEagerLoadSpec<TEntity>.Execute(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents)
    {
        if (parents.Count == 0)
            return;

        Assign(parents, Load(context, parentScope, parents));
    }

    async Task IEagerLoadSpec<TEntity>.ExecuteAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        if (parents.Count == 0)
            return;

        Assign(parents, await LoadAsync(context, parentScope, parents, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Builds a fresh child builder for one query and folds the parent command's global-query-filter
    /// scope into the child's own scope, so the split path applies exactly the same union rule as the
    /// single-query (<c>EagerLoadMode.SingleQuery</c>/<c>JoinInto</c>) path: a child's selective scope combines with
    /// the parent's, <c>All</c> absorbs the union, and <c>null</c>/empty child scopes inherit the parent.
    /// <see cref="EntityBuilder{TEntity}.WithFilterScope(QueryFilterScope)"/> returns a scoped copy when
    /// the parent scope is non-empty, so the user's child builder is never mutated and one child cannot
    /// leak its selective scope to a sibling.
    /// </summary>
    private EntityBuilder<TChild> BuildChild(IDataContext context, QueryFilterScope parentScope)
        => _childQuery(context).WithFilterScope(parentScope);

    /// <summary>
    /// Runs one child query per key chunk and returns the children grouped by their key, keeping the
    /// order the child query returned them in.
    /// </summary>
    private Dictionary<TKey, List<TChild>> Load(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        foreach (var chunk in KeyChunks(parents))
            AddChildren(grouped, BuildChild(context, parentScope).Where(BuildPredicate(chunk)).ToList());

        return grouped;
    }

    /// <summary>Asynchronously runs one child query per key chunk and groups the children by their key.</summary>
    private async Task<Dictionary<TKey, List<TChild>>> LoadAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        foreach (var chunk in KeyChunks(parents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var children = await BuildChild(context, parentScope).Where(BuildPredicate(chunk)).ToListAsync(cancellationToken).ConfigureAwait(false);
            AddChildren(grouped, children);
        }

        return grouped;
    }

    /// <summary>
    /// Enumerates the distinct non-null parent keys in chunks of <see cref="KeyChunkSize"/>, preserving
    /// the parent order.
    /// </summary>
    private static IEnumerable<List<TKey>> KeyChunks(IReadOnlyList<TEntity> parents, Func<TEntity, TKey> parentKey)
    {
        var chunk = new List<TKey>();
        var seen = new HashSet<TKey>();
        foreach (var parent in parents)
        {
            var key = parentKey(parent);
            if (key is null || !seen.Add(key))
                continue;

            chunk.Add(key);
            if (chunk.Count == KeyChunkSize)
            {
                yield return chunk;
                chunk = [];
            }
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    private IEnumerable<List<TKey>> KeyChunks(IReadOnlyList<TEntity> parents) => KeyChunks(parents, _parentKey);

    /// <summary>Appends already loaded children to the group of their key; a null child key is ignored.</summary>
    internal void AddChildren(Dictionary<TKey, List<TChild>> grouped, List<TChild> children)
    {
        foreach (var child in children)
        {
            var key = _childKey(child);
            if (key is null)
                continue;

            if (!grouped.TryGetValue(key, out var items))
                grouped[key] = items = [];

            items.Add(child);
        }
    }

    /// <summary>Builds the child predicate <c>keys.Contains(childKey)</c> for one chunk.</summary>
    private Expression<Func<TChild, bool>> BuildPredicate(List<TKey> keys)
        => Expression.Lambda<Func<TChild, bool>>(
            Expression.Call(Expression.Constant(keys, typeof(List<TKey>)), _containsMethod, _childKeyExpression.Body),
            _childKeyExpression.Parameters[0]);

    /// <summary>
    /// Assigns the grouped children onto each parent: an existing (non-null) collection is cleared and
    /// refilled, otherwise a fresh list is set through the settable member. A read-only member whose
    /// value is null cannot be filled and is rejected. Every target is validated before any parent is
    /// mutated, so a read-only null member throws without leaving earlier parents partially populated.
    /// </summary>
    internal void Assign(IReadOnlyList<TEntity> parents, Dictionary<TKey, List<TChild>> grouped)
    {
        if (_collectionSetter is null)
        {
            foreach (var parent in parents)
            {
                if (_collectionGetter(parent) is null)
                    throw new NotSupportedException(
                        $"Cannot load the collection member '{_member?.Name}' on '{typeof(TEntity).Name}': " +
                        "its current value is null and the member is not settable. Expose it as a settable " +
                        "property/field or initialize it with an empty collection.");
            }
        }

        foreach (var parent in parents)
        {
            var key = _parentKey(parent);
            List<TChild>? children = null;
            if (key is not null)
                grouped.TryGetValue(key, out children);

            var target = _collectionGetter(parent);
            if (target is not null)
            {
                target.Clear();
                if (children is not null)
                {
                    foreach (var child in children)
                        target.Add(child);
                }

                continue;
            }

            _collectionSetter!(parent, children is null ? [] : new List<TChild>(children));
        }
    }
}

/// <summary>
/// Runs the eager-load specifications stored on an <see cref="EntityBuilder{TEntity}"/> after the
/// parent query has been materialized. The command terminal (<see cref="EntityBuilder{TEntity}.ToCommand"/>)
/// deliberately ignores the specifications, so the child query does not recurse into the parent's loader.
/// </summary>
internal static class EntityBuilderEagerLoading
{
    /// <summary>Runs every load specification on <paramref name="builder"/> for the materialized parents.</summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder that carries the specifications.</param>
    /// <param name="parents">The materialized parents, in parent-query order.</param>
    public static void Execute<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<TEntity> parents)
    {
        var specs = builder.LoadSpecs;
        if (specs is not { Count: > 0 })
            return;

        foreach (var spec in specs)
            spec.Execute(builder.DataProvider, builder.FilterScope, parents);
    }

    /// <summary>Asynchronously runs every load specification on <paramref name="builder"/>.</summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder that carries the specifications.</param>
    /// <param name="parents">The materialized parents, in parent-query order.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when every specification has finished.</returns>
    public static async Task ExecuteAsync<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        var specs = builder.LoadSpecs;
        if (specs is not { Count: > 0 })
            return;

        foreach (var spec in specs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await spec.ExecuteAsync(builder.DataProvider, builder.FilterScope, parents, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Adapts a split-query <see cref="EagerLoadSpec{TEntity, TChild, TKey}"/> to the single-query
/// <c>JoinInto</c> contract used by <c>EagerLoadMode.SingleQuery</c>: the <c>ON</c> predicate equates the two key
/// selectors and folds in the child query's own <c>Where</c> condition, re-rooted onto the child join
/// parameter, so a <c>LEFT JOIN</c> keeps childless parents while filtering the children. The grouping
/// and assignment delegate to the eager-load specification, so both modes share one stitching contract.
/// </summary>
/// <typeparam name="TEntity">The parent entity type.</typeparam>
/// <typeparam name="TChild">The child entity type.</typeparam>
/// <typeparam name="TKey">The key type shared by the two key selectors.</typeparam>
internal sealed class EagerLoadJoinIntoSpec<TEntity, TChild, TKey> : IJoinIntoSpec<TEntity>
    where TKey : notnull
{
    private readonly EagerLoadSpec<TEntity, TChild, TKey> _eager;
    private readonly Expression<Func<TEntity, TChild, bool>> _predicate;
    private readonly QueryFilterScope _childFilterScope;

    /// <summary>
    /// Initializes the specification, synthesizing the key-equality predicate and merging the optional
    /// child condition into it.
    /// </summary>
    /// <param name="eager">The split-query specification this single-query declaration mirrors.</param>
    /// <param name="childCondition">The child query's own <c>Where</c> condition, or <c>null</c> when it has none.</param>
    /// <param name="childFilterScope">The child query's selective global-query-filter scope.</param>
    public EagerLoadJoinIntoSpec(
        EagerLoadSpec<TEntity, TChild, TKey> eager,
        Expression<Func<TChild, bool>>? childCondition,
        QueryFilterScope childFilterScope)
    {
        ArgumentNullException.ThrowIfNull(eager);
        ArgumentNullException.ThrowIfNull(childFilterScope);
        _eager = eager;
        _childFilterScope = childFilterScope;

        var parentParameter = Expression.Parameter(typeof(TEntity), "p");
        var childParameter = Expression.Parameter(typeof(TChild), "c");

        var parentKey = JoinIntoSpecHelpers.ReplaceParameter(
            eager.ParentKeyExpression.Body, eager.ParentKeyExpression.Parameters[0], parentParameter);
        var childKey = JoinIntoSpecHelpers.ReplaceParameter(
            eager.ChildKeyExpression.Body, eager.ChildKeyExpression.Parameters[0], childParameter);

        Expression body = JoinIntoSpecHelpers.BuildKeyEquality(parentKey, childKey, typeof(TKey), "EagerLoadMode.SingleQuery");

        if (childCondition is not null)
        {
            var condition = JoinIntoSpecHelpers.ReplaceParameter(
                childCondition.Body, childCondition.Parameters[0], childParameter);
            body = Expression.AndAlso(body, condition);
        }

        _predicate = Expression.Lambda<Func<TEntity, TChild, bool>>(body, parentParameter, childParameter);
        CollectionProperty = JoinIntoSpecHelpers.TryResolveCollectionProperty(eager.CollectionExpression);
        Identity = new JoinIntoIdentity
        {
            ParentType = typeof(TEntity),
            ChildType = typeof(TChild),
            JoinType = JoinType.Left,
            CollectionMember = CollectionProperty is { } property ? $"{property.DeclaringType?.FullName}.{property.Name}" : null,
            KeyMembers =
            [
                JoinIntoSpecHelpers.TryResolveMemberName(eager.ChildKeyExpression) ?? eager.ChildKeyExpression.ToString(),
                JoinIntoSpecHelpers.TryResolveMemberName(eager.ParentKeyExpression) ?? eager.ParentKeyExpression.ToString(),
            ],
        };
    }

    /// <inheritdoc/>
    public JoinType JoinType => JoinType.Left;

    /// <inheritdoc/>
    public PropertyInfo? CollectionProperty { get; }

    /// <inheritdoc/>
    public LambdaExpression Predicate => _predicate;

    /// <inheritdoc/>
    public Type ChildEntityType => typeof(TChild);

    /// <inheritdoc/>
    public JoinIntoIdentity Identity { get; }

    /// <inheritdoc/>
    public QueryFilterScope ChildFilterScope => _childFilterScope;

    /// <inheritdoc/>
    public object? GetParentKey(TEntity parent) => _eager.ParentKeyOf(parent);

    /// <inheritdoc/>
    public void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
        => _eager.AssignChildrenFromRows(parents, rows);
}
