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
    /// <param name="parents">The already materialized parents, in the order the parent query returned them.</param>
    void Execute(IDataContext context, IReadOnlyList<TEntity> parents);

    /// <summary>
    /// Asynchronously loads and assigns the child collection for every parent using two sequential
    /// round trips (no N+1).
    /// </summary>
    /// <param name="context">The data context the child query is built from.</param>
    /// <param name="parents">The already materialized parents, in the order the parent query returned them.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the children have been loaded and assigned.</returns>
    Task ExecuteAsync(IDataContext context, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken);

    /// <summary>
    /// The parent collection member the specification targets, or <c>null</c> when the collection
    /// expression is not a direct field/property. Two specifications sharing a member are duplicates.
    /// </summary>
    MemberInfo? CollectionMember { get; }
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

    /// <summary>Whether the member can be assigned from a fresh list.</summary>
    private static bool CanAssign(MemberInfo member) => member switch
    {
        PropertyInfo property => property.CanWrite,
        FieldInfo field => !field.IsInitOnly,
        _ => false,
    };

    void IEagerLoadSpec<TEntity>.Execute(IDataContext context, IReadOnlyList<TEntity> parents)
    {
        if (parents.Count == 0)
            return;

        Assign(parents, Load(context, parents));
    }

    async Task IEagerLoadSpec<TEntity>.ExecuteAsync(IDataContext context, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        if (parents.Count == 0)
            return;

        Assign(parents, await LoadAsync(context, parents, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Runs one child query per key chunk and returns the children grouped by their key, keeping the
    /// order the child query returned them in.
    /// </summary>
    private Dictionary<TKey, List<TChild>> Load(IDataContext context, IReadOnlyList<TEntity> parents)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        foreach (var chunk in KeyChunks(parents))
            AddChildren(grouped, _childQuery(context).Where(BuildPredicate(chunk)).ToList());

        return grouped;
    }

    /// <summary>Asynchronously runs one child query per key chunk and groups the children by their key.</summary>
    private async Task<Dictionary<TKey, List<TChild>>> LoadAsync(IDataContext context, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        foreach (var chunk in KeyChunks(parents))
        {
            var children = await _childQuery(context).Where(BuildPredicate(chunk)).ToListAsync(cancellationToken).ConfigureAwait(false);
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
    private void Assign(IReadOnlyList<TEntity> parents, Dictionary<TKey, List<TChild>> grouped)
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
            spec.Execute(builder.DataProvider, parents);
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
            await spec.ExecuteAsync(builder.DataProvider, parents, cancellationToken).ConfigureAwait(false);
    }
}
