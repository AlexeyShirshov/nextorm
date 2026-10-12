using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Non-generic view over one eager-load declaration, so the shared graph walker can traverse a nested
/// declaration without knowing the parent/child types at compile time. Implemented by
/// <see cref="IEagerLoadSpec{TEntity}"/>; carries no public surface.
/// </summary>
internal interface IEagerLoadNodeSpec
{
    /// <summary>
    /// Canonical structural fingerprint of this declaration (parent/child types, collection member and
    /// key identity). It is the per-level segment of the node's ancestor-composite identity, so two
    /// declarations with the same fingerprint are the same graph node.
    /// </summary>
    string ShapeKey { get; }

    /// <summary>
    /// The parent-side collection member this declaration targets, or <c>null</c> when the collection
    /// expression is not a direct field/property.
    /// </summary>
    MemberInfo? CollectionMember { get; }

    /// <summary>
    /// The eager-load strategy requested for this declaration (the <c>LoadWith</c> argument, so
    /// <see cref="EagerLoadMode.Default"/> means "inherit the graph-wide mode").
    /// </summary>
    EagerLoadMode RequestedMode { get; }

    /// <summary>
    /// Resolves the child query builder for <paramref name="context"/> (the child-query factory is invoked
    /// at most once per <c>(declaration, context)</c>), so the graph walker can inspect the child's own
    /// nested declarations. It builds no command, writes no cache and never touches
    /// <see cref="QueryCommand.Cache"/>.
    /// </summary>
    /// <param name="context">The data context the child query is built from.</param>
    /// <returns>The resolved child builder, viewed as an eager-load node source.</returns>
    IEagerLoadNodeSource ResolveChildNodeSource(IDataContext context);

    /// <summary>
    /// The number of flattened projection items this declaration's subtree contributes to a single
    /// denormalized result set: this node plus every descendant. It lays out the nested columns of the
    /// single-query join chain and fails closed before a graph exceeds the projection arity. Resolving the
    /// child factories is memoized per <c>(declaration, context)</c>, so no factory is invoked twice.
    /// </summary>
    /// <param name="context">The data context used to resolve child query builders.</param>
    /// <returns>The subtree item count (at least one).</returns>
    int SubtreeItemCount(IDataContext context);
}

/// <summary>
/// Non-generic view over an entity builder's ordered eager-load declarations, so the graph walker can
/// recurse across the declared child types without reflection or reference identity. Implemented by
/// <see cref="EntityBuilder{TEntity}"/>; carries no public surface.
/// </summary>
internal interface IEagerLoadNodeSource
{
    /// <summary>
    /// The declarations carried by this builder, in declaration order, or <c>null</c> when it carries
    /// none.
    /// </summary>
    IReadOnlyList<IEagerLoadNodeSpec>? EagerLoadNodeSpecs { get; }
}

/// <summary>
/// A single split-query eager-load specification stored on an <see cref="EntityBuilder{TEntity}"/>:
/// the parent-side collection member, the factory that builds the child query, and the two key
/// selectors used to join the two result sets in memory.
/// </summary>
/// <typeparam name="TEntity">The parent entity type the builder projects.</typeparam>
internal interface IEagerLoadSpec<TEntity> : IEagerLoadNodeSpec
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
    /// Builds the single-query (<c>EagerLoadMode.SingleQuery</c>) equivalent of this split-query specification: an
    /// explicit-key <c>JoinInto</c> declaration whose <c>ON</c> predicate equates the two key selectors
    /// and folds in the child query's own <c>Where</c> condition, re-rooted onto the child join
    /// parameter, so a <c>LEFT JOIN</c> keeps childless parents while filtering the children.
    /// </summary>
    /// <param name="context">The data context used to build the child query.</param>
    /// <param name="parentScope">The parent command's global-query-filter scope, folded into a per-parent paging child.</param>
    /// <param name="childSource">Receives the resolved <c>FROM</c> source of the joined child.</param>
    /// <returns>The join specification consumed by the pair command and the stitcher.</returns>
    IJoinIntoSpec<TEntity> ToJoinIntoSpec(IDataContext context, QueryFilterScope parentScope, out FromExpression childSource);

    /// <summary>
    /// Whether this load's child query requests a per-parent top-N (a child <c>Limit</c>/<c>Offset</c>/
    /// <c>Page</c>). Such a requirement is lowered by the split loader and the single-query join to a
    /// <c>row_number() over (partition by &lt;child key&gt;)</c> source whose page bounds filter each
    /// partition, so the child page is applied independently to every parent. Building the child here is
    /// what makes the paging visible to preparation; the built child is reused by execution, so the
    /// child-query factory is invoked at most once per load.
    /// </summary>
    /// <param name="context">The data context used to build the child query.</param>
    /// <returns><see langword="true"/> when the child query pages per parent.</returns>
    bool RequiresPerParentTopN(IDataContext context);

    /// <summary>
    /// Validates a per-parent paging child (if any) against the shapes the ranked child source can
    /// lower, throwing before any SQL is built or executed. The split loader and the single-query join
    /// call this through this shared seam so a child the ranked lowering cannot render fails closed
    /// instead of being silently dropped. A non-paged child is a no-op.
    /// </summary>
    /// <param name="context">The data context used to build the child query.</param>
    /// <exception cref="NotSupportedException">
    /// The child pages per parent and carries a shape the ranked source cannot fold in, or pages per
    /// parent without a window ordering.
    /// </exception>
    void ValidatePerParentPaging(IDataContext context);

    /// <summary>
    /// Builds the single-query plan of this declaration: its own <c>JoinInto</c> clause (folded with the
    /// child's own <c>Where</c>) followed, recursively, by one join per declared descendant, together with
    /// the flattened projection item types of the whole subtree. A child that carries a nested graph is
    /// planned as a nested graph; an unexpressible child shape or a nested per-parent page fails closed
    /// here, before any SQL is built or executed and without silently dropping the nested metadata.
    /// </summary>
    /// <param name="context">The data context used to build the child query.</param>
    /// <param name="parentScope">The parent command's global-query-filter scope.</param>
    /// <returns>The single-query plan of this declaration's subtree.</returns>
    /// <exception cref="NotSupportedException">
    /// The child carries a shape the single-query fold cannot render, or a nested graph requests a
    /// per-parent top-N the ranked source cannot lower.
    /// </exception>
    EagerLoadSingleQueryPlan<TEntity> PlanSingleQuery(IDataContext context, QueryFilterScope parentScope);
}

/// <summary>
/// Non-generic-per-level view of a single-query eager-load node that can assemble itself and its
/// descendants from one flattened denormalized result set, reusing the section-7 assignment contract at
/// every level. Implemented by <see cref="EagerLoadSpec{TEntity,TChild,TKey}"/>; carries no public surface.
/// </summary>
/// <typeparam name="TEntity">The parent entity type of the node.</typeparam>
internal interface IEagerSingleQueryStitch<TEntity>
{
    /// <summary>
    /// Assigns this node's children from the flattened <paramref name="rows"/> and, recursively, does the
    /// same for every descendant with the children it just assigned as the next level's parents. A node
    /// without descendants is level-one behavior. A null key never matches and never crashes.
    /// </summary>
    /// <param name="parents">The canonical parents of this level, in first-occurrence order.</param>
    /// <param name="rows">The flattened denormalized rows, in result order.</param>
    /// <param name="columns">The <c>Item1..ItemN</c> accessors of the row projection.</param>
    /// <param name="parentColumn">The zero-based column holding this node's parent item.</param>
    /// <param name="childColumn">The zero-based column holding this node's own item.</param>
    /// <param name="context">The data context used to resolve descendant builders.</param>
    void StitchSingleQuery(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context);
}

/// <summary>
/// The single-query plan of one top-level eager-load declaration: the stitching specification the stitcher
/// consumes (whose <see cref="IJoinIntoSpec{TEntity}.ItemTypes"/> is the flattened subtree), every join
/// clause of the subtree (the declaration's own join followed by its descendants', pre-order), and the
/// flattened projection item types.
/// </summary>
/// <typeparam name="TEntity">The root entity type.</typeparam>
internal sealed class EagerLoadSingleQueryPlan<TEntity>
{
    /// <summary>Initializes the plan.</summary>
    /// <param name="spec">The top-level stitching specification.</param>
    /// <param name="joins">The join clauses, pre-order.</param>
    /// <param name="itemTypes">The flattened projection item types.</param>
    public EagerLoadSingleQueryPlan(IJoinIntoSpec<TEntity> spec, IReadOnlyList<JoinExpression> joins, IReadOnlyList<Type> itemTypes)
    {
        Spec = spec;
        Joins = joins;
        ItemTypes = itemTypes;
    }

    /// <summary>The top-level stitching specification.</summary>
    public IJoinIntoSpec<TEntity> Spec { get; }

    /// <summary>The join clauses of this declaration and its descendants, pre-order.</summary>
    public IReadOnlyList<JoinExpression> Joins { get; }

    /// <summary>The flattened projection item types of this declaration's subtree.</summary>
    public IReadOnlyList<Type> ItemTypes { get; }
}

/// <summary>
/// The default <see cref="IEagerLoadSpec{TEntity}"/>: materializes the children of one parent type in
/// key chunks and stitches them onto the parents in memory. Created by the builder's
/// <c>LoadWith</c> modifier.
/// </summary>
/// <typeparam name="TEntity">The parent entity type.</typeparam>
/// <typeparam name="TChild">The child entity type.</typeparam>
/// <typeparam name="TKey">The key type shared by the two key selectors.</typeparam>
internal sealed class EagerLoadSpec<TEntity, TChild, TKey> : IEagerLoadSpec<TEntity>, IEagerSingleQueryStitch<TEntity>
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

    // The child query is built at most once per data context: preparation asks for it to detect a
    // per-parent top-N and execution reuses the same builder (a test-only counter on the user's child
    // factory must therefore observe a single build). The memo is thread-safe so concurrent executions of
    // the same shareable builder observe one build and the specification carries no unsynchronized mutable
    // cache; it is keyed by the data context, so a different context resolves its own builder and no
    // builder is leaked from one context into another.
    private readonly ConditionalWeakTable<IDataContext, Lazy<EntityBuilder<TChild>>> _resolvedChildren = new();

    /// <summary>
    /// Initializes the specification, compiling the collection accessor, the two key selectors and the
    /// optional collection setter once.
    /// </summary>
    /// <param name="collection">The parent-side collection member to fill.</param>
    /// <param name="childQuery">The factory that builds the child query from a data context.</param>
    /// <param name="parentKey">The parent-side key selector.</param>
    /// <param name="childKey">The child-side key selector.</param>
    /// <param name="requestedMode">The <c>LoadWith</c> strategy argument; <see cref="EagerLoadMode.Default"/> means inherit.</param>
    public EagerLoadSpec(
        Expression<Func<TEntity, ICollection<TChild>>> collection,
        Func<IDataContext, EntityBuilder<TChild>> childQuery,
        Expression<Func<TEntity, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childKey,
        EagerLoadMode requestedMode = EagerLoadMode.Default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(childQuery);
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(childKey);

        RequestedMode = requestedMode;
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

    /// <inheritdoc/>
    public EagerLoadMode RequestedMode { get; }

    /// <inheritdoc/>
    public string ShapeKey =>
        "P=" + typeof(TEntity).FullName +
        ";C=" + typeof(TChild).FullName +
        ";M=" + (_member is null ? "-" : _member.DeclaringType?.FullName + "." + _member.Name) +
        ";K=" + typeof(TKey).FullName +
        ";PK=" + (JoinIntoSpecHelpers.TryResolveMemberName(_parentKeyExpression) ?? _parentKeyExpression.ToString()) +
        ";CK=" + (JoinIntoSpecHelpers.TryResolveMemberName(_childKeyExpression) ?? _childKeyExpression.ToString());

    /// <inheritdoc/>
    IEagerLoadNodeSource IEagerLoadNodeSpec.ResolveChildNodeSource(IDataContext context)
        => ResolveChild(context);

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
    void IEagerSingleQueryStitch<TEntity>.StitchSingleQuery(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
        => StitchSingleQueryCore(parents, rows, columns, parentColumn, childColumn, context);

    /// <inheritdoc/>
    EagerLoadSingleQueryPlan<TEntity> IEagerLoadSpec<TEntity>.PlanSingleQuery(IDataContext context, QueryFilterScope parentScope)
    {
        ArgumentNullException.ThrowIfNull(context);

        var child = ResolveChild(context);
        var nestedSpecs = ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs;

        // A nested graph cannot be combined with a per-parent top-N child at this level: the ranked
        // child source has no per-level rendering. Fail closed instead of silently dropping the page or
        // the nested graph (#211).
        if (nestedSpecs is { Count: > 0 } && EntityBuilderEagerLoading.HasPerParentTopN(child))
            throw EntityBuilderEagerLoading.PerParentTopNNotSupported();

        if (nestedSpecs is not { Count: > 0 })
        {
            // Level one stays on the exact existing fold: one synthesized join, the child's own Where
            // folded in, and the per-parent paging derived source when the provider supports it.
            var flat = ((IEagerLoadSpec<TEntity>)this).ToJoinIntoSpec(context, parentScope, out var childSource);
            return new EagerLoadSingleQueryPlan<TEntity>(
                flat,
                [EntityBuilderEagerLoading.BuildSingleQueryJoin(flat, childSource)],
                [typeof(TChild)]);
        }

        // Nested: the child's own nested declarations are supported, but any other child-query shape the
        // fold cannot render is still rejected here, before any SQL is built or executed.
        var unsupported = child.UnsupportedSingleQueryChildShapes();
        if (unsupported.Count > 0)
            throw new NotSupportedException(
                $"LoadWith with EagerLoadMode.SingleQuery cannot fold the child query's {string.Join(", ", unsupported)} into the join " +
                "predicate: only the child's own Where is supported. Remove the modifier or use split-query " +
                "loading (omit the SingleQuery mode), which honors every child-query shape.");

        var level1Source = child.ResolveSource() ?? context.GetFrom(typeof(TChild), null)
            ?? throw new BuildSqlCommandException(
                $"The single-query child source for type {typeof(TChild)} could not be resolved.");

        var level1 = new EagerLoadJoinIntoSpec<TEntity, TChild, TKey>(this, child.Condition, child.FilterScope);

        var joins = new List<JoinExpression> { EntityBuilderEagerLoading.BuildSingleQueryJoin(level1, level1Source) };
        var itemTypes = new List<Type> { typeof(TChild) };
        for (var i = 0; i < nestedSpecs.Count; i++)
        {
            // Each descendant is itself planned through the same seam, so a deeper level recurses and an
            // unexpressible descendant fails closed with the same rejection.
            var descendant = ((IEagerLoadSpec<TChild>)nestedSpecs[i]).PlanSingleQuery(context, child.FilterScope);
            joins.AddRange(descendant.Joins);
            itemTypes.AddRange(descendant.ItemTypes);
        }

        if (itemTypes.Count > JoinIntoProjectionFactory.MaxChildCount)
            throw new NotSupportedException(
                $"LoadWith with EagerLoadMode.SingleQuery supports at most {JoinIntoProjectionFactory.MaxChildCount} " +
                $"eager-loaded child collections per query, but the nested graph declares {itemTypes.Count}. " +
                "Declare a shallower graph or use split-query loading.");

        var nestedSpec = new EagerLoadNestedJoinIntoSpec<TEntity, TChild, TKey>(level1, this, itemTypes);
        return new EagerLoadSingleQueryPlan<TEntity>(nestedSpec, joins, itemTypes);
    }

    /// <summary>
    /// Builds the child query for <paramref name="context"/> once and returns it on every later call, so
    /// preparation (which detects a per-parent top-N) and execution share one built child builder. The
    /// per-context <see cref="Lazy{T}"/> memo is published thread-safely, so a concurrent execution of the
    /// same shareable builder never invokes the child factory twice and never observes a partially built
    /// child.
    /// </summary>
    /// <param name="context">The data context the child query is built from.</param>
    /// <returns>The reusable child builder.</returns>
    private EntityBuilder<TChild> ResolveChild(IDataContext context)
        => _resolvedChildren.GetValue(
            context,
            ctx => new Lazy<EntityBuilder<TChild>>(
                () => _childQuery(ctx),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

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

    /// <summary>
    /// Single-result-set assembly of this node (and, recursively, its descendants): reads this node's
    /// parent/child items out of the flattened projection, assigns the children with the section-7
    /// contract, then recurses into the child builder's own declarations with the children just
    /// materialized as the next level's parents (ancestor-composite identity by key). A null child item
    /// (an outer-join miss) or a null key never matches and never crashes.
    /// </summary>
    /// <param name="parents">The canonical parents of this level.</param>
    /// <param name="rows">The flattened denormalized rows.</param>
    /// <param name="columns">The <c>Item1..ItemN</c> accessors of the row projection.</param>
    /// <param name="parentColumn">The zero-based column holding this node's parent item.</param>
    /// <param name="childColumn">The zero-based column holding this node's own item.</param>
    /// <param name="context">The data context used to resolve descendant builders.</param>
    internal void StitchSingleQueryCore(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
    {
        var children = AssignFromProjectionCollecting(parents, rows, columns, parentColumn, childColumn);

        var childSpecs = ((IEagerLoadNodeSource)ResolveChild(context)).EagerLoadNodeSpecs;
        if (childSpecs is not { Count: > 0 })
            return;

        var column = childColumn + 1;
        for (var i = 0; i < childSpecs.Count; i++)
        {
            var node = childSpecs[i];
            if (node is not IEagerSingleQueryStitch<TChild> descendant)
                throw new InvalidOperationException(
                    $"The declared eager-load node '{node.ShapeKey}' cannot be stitched from a single result set.");

            descendant.StitchSingleQuery(children, rows, columns, childColumn, column, context);
            column += node.SubtreeItemCount(context);
        }
    }

    /// <summary>
    /// Reads this node's <c>(parent, child)</c> pairs out of the flattened projection, assigns them, and
    /// returns the distinct children (by their mapped identity, or reference when keyless) in
    /// first-occurrence order, so the caller can recurse into this node's own descendants.
    /// </summary>
    private IReadOnlyList<TChild> AssignFromProjectionCollecting(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn)
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

        AssignChildrenFromRows(parents, pairs);
        return CollectAssignedChildren(parents);
    }

    /// <summary>
    /// Returns the distinct children this node assigned to <paramref name="parents"/> (by reference for the
    /// usual reference-typed entities, or value equality otherwise), in assignment order, so the caller can
    /// recurse into this node's own descendants with exactly the instances now linked to the ancestors. A
    /// child key repeated under two different ancestors yields two instances and both are returned, so
    /// every ancestor's child receives its own descendants.
    /// </summary>
    private IReadOnlyList<TChild> CollectAssignedChildren(IReadOnlyList<TEntity> parents)
    {
        var seen = new HashSet<TChild>();
        var distinct = new List<TChild>();
        for (var i = 0; i < parents.Count; i++)
        {
            var collection = _collectionGetter(parents[i]);
            if (collection is null)
                continue;

            foreach (var child in collection)
            {
                if (seen.Add(child))
                    distinct.Add(child);
            }
        }

        return distinct;
    }

    /// <inheritdoc/>
    IJoinIntoSpec<TEntity> IEagerLoadSpec<TEntity>.ToJoinIntoSpec(IDataContext context, QueryFilterScope parentScope, out FromExpression childSource)
    {
        ArgumentNullException.ThrowIfNull(context);

        var child = ResolveChild(context);

        // Per-parent top-N on a SQL provider: the child's own Where, ordering and page bounds are
        // honored inside a ranked derived source the single-query join selects from, so the bounds apply
        // within each partition of the child key instead of globally. The in-memory provider has no SQL
        // window and keeps its existing rejection (the UnsupportedSingleQueryChildShapes path below),
        // matching the plan's "retain the existing in-memory rejection" rule.
        if (context.NeedMapping && EntityBuilderEagerLoading.HasPerParentTopN(child))
            return ToPerParentPagingJoinIntoSpec(context, parentScope, out childSource);

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

    /// <summary>
    /// Builds the single-query join for a child that pages per parent. The child's own <c>Where</c>,
    /// ordering and <c>Limit</c>/<c>Offset</c>/<c>Page</c> are consumed by a ranked derived source
    /// (<c>row_number() over (partition by &lt;child key&gt; order by &lt;child order&gt;)</c> with the page
    /// bounds applied to each partition), reused through the same lowering the split loader uses. The
    /// derived command is marked embedded so its rendering drops the child's output <c>ORDER BY</c>,
    /// which a nested derived table cannot carry. The parent's global-query-filter scope is folded in
    /// exactly as the split loader does, so the two modes agree on filter semantics.
    /// </summary>
    /// <param name="context">The SQL data context used to build the child query.</param>
    /// <param name="parentScope">The parent command's global-query-filter scope.</param>
    /// <param name="childSource">Receives the ranked derived-table source of the joined child.</param>
    /// <returns>The join specification whose predicate is the key equality alone.</returns>
    private IJoinIntoSpec<TEntity> ToPerParentPagingJoinIntoSpec(IDataContext context, QueryFilterScope parentScope, out FromExpression childSource)
    {
        var child = BuildChild(context, parentScope);

        // The same fail-closed shape/ordering guard the split loader applies, so both eager modes reject
        // a child the ranked source cannot lower before any SQL is built.
        EntityBuilderEagerLoading.ValidatePerParentPagingChild(child);

        var command = child.ToCommand();
        command.From ??= context.GetFrom(typeof(TChild), null);
        command.PerParentPaging = true;
        command.PerParentPagingEmbedded = true;
        command.PerParentPartition = _childKeyExpression;
        childSource = new FromExpression(command);

        // The child's Where/order/paging and its global-filter scope are folded into the derived source,
        // so the join predicate is the key equality alone. A SubQuery join source never receives the
        // join-level filter injection (InjectJoinFilters returns early for it), so no child scope is
        // needed here.
        return new EagerLoadJoinIntoSpec<TEntity, TChild, TKey>(this, null, QueryFilterScope.None);
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

        // Resolve the scoped child builder once: the child query is built from it for every key chunk and
        // its own nested declarations are then executed against exactly the children this level loaded.
        var child = BuildChild(context, parentScope);
        var nested = ((IEagerLoadNodeSource)child).EagerLoadNodeSpecs is { Count: > 0 };
        List<TChild>? loaded = nested ? [] : null;
        Assign(parents, Load(child, parents, loaded));

        // Descendant orchestration: recurse into the child builder's own eager-load declarations with the
        // children just materialized as the next level's parents. A child with no nested declaration
        // allocates no child list and returns immediately, so level-one behavior is allocation-neutral.
        if (loaded is not null)
            EntityBuilderEagerLoading.ExecuteLoadedChildren(child, loaded);
    }

    async Task IEagerLoadSpec<TEntity>.ExecuteAsync(IDataContext context, QueryFilterScope parentScope, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        if (parents.Count == 0)
            return;

        var child = BuildChild(context, parentScope);
        var loaded = new List<TChild>();
        Assign(parents, await LoadAsync(child, parents, loaded, cancellationToken).ConfigureAwait(false));

        await EntityBuilderEagerLoading.ExecuteLoadedChildrenAsync(child, loaded, cancellationToken).ConfigureAwait(false);
    }

    private EntityBuilder<TChild> BuildChild(IDataContext context, QueryFilterScope parentScope)
        => ResolveChild(context).WithFilterScope(parentScope);

    /// <summary>
    /// Builds the child command for one key chunk. When the child query's own <c>Limit</c>/<c>Offset</c>/
    /// <c>Page</c> is present it is applied per parent: the command is marked
    /// <see cref="QueryCommand.PerParentPaging"/> and its child-key selector becomes the window
    /// partition, so the SQL builder lowers it to a ranked derived table whose page bounds apply inside
    /// each parent partition. The child builder is never mutated.
    /// </summary>
    private QueryCommand<TChild> BuildChildCommand(EntityBuilder<TChild> child, List<TKey> chunk)
    {
        var command = child.Where(BuildPredicate(chunk)).ToCommand();

        if (EntityBuilderEagerLoading.HasPerParentTopN(child))
        {
            // The in-memory provider has no SQL window and cannot apply a per-parent top-N; retain the
            // existing fail-closed rejection (this also covers the JoinInto+LoadWith path, which does not
            // mark the parent command for the preparation-time guard).
            if (!child.DataProvider.NeedMapping)
                throw EntityBuilderEagerLoading.PerParentTopNNotSupported();

            // The split loader shares the single-query path's fail-closed shape/ordering guard: the
            // ranked lowering renders only the child's own Where, ordering and page bounds, so any other
            // child shape (or an unordered page) must be rejected rather than silently dropped.
            EntityBuilderEagerLoading.ValidatePerParentPagingChild(child);

            command.PerParentPaging = true;
            command.PerParentPartition = _childKeyExpression;
        }

        return command;
    }

    /// <summary>
    /// Runs one child query per key chunk and returns the children grouped by their key, keeping the
    /// order the child query returned them in. Every loaded child is also appended to
    /// <paramref name="loaded"/>, so the caller can recurse into the child builder's own nested
    /// declarations with exactly this level's materialized children as the next level's parents.
    /// </summary>
    private Dictionary<TKey, List<TChild>> Load(EntityBuilder<TChild> child, IReadOnlyList<TEntity> parents, List<TChild>? loaded)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        foreach (var chunk in KeyChunks(parents))
        {
            var children = BuildChildCommand(child, chunk).ToList();
            loaded?.AddRange(children);
            AddChildren(grouped, children);
        }

        return grouped;
    }

    /// <summary>Asynchronously runs one child query per key chunk and groups the children by their key.</summary>
    private async Task<Dictionary<TKey, List<TChild>>> LoadAsync(EntityBuilder<TChild> child, IReadOnlyList<TEntity> parents, List<TChild>? loaded, CancellationToken cancellationToken)
    {
        var grouped = new Dictionary<TKey, List<TChild>>();
        foreach (var chunk in KeyChunks(parents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var children = await BuildChildCommand(child, chunk).ToListAsync(cancellationToken).ConfigureAwait(false);
            loaded?.AddRange(children);
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
    /// <summary>
    /// The number of flattened projection items a declared subtree contributes to the single-query result
    /// set (the node itself plus every descendant). It lays out the nested columns and fails closed before
    /// a graph exceeds the projection arity. Child factories resolve through the same per-context memo the
    /// loaders use, so no factory is invoked twice and no shared command is mutated.
    /// </summary>
    /// <param name="spec">The subtree root declaration.</param>
    /// <param name="context">The data context used to resolve child query builders.</param>
    /// <returns>The subtree item count (at least one).</returns>
    internal static int SubtreeItemCount(IEagerLoadNodeSpec spec, IDataContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        var count = 1;
        var children = spec.ResolveChildNodeSource(context).EagerLoadNodeSpecs;
        if (children is { Count: > 0 })
        {
            for (var i = 0; i < children.Count; i++)
                count += SubtreeItemCount(children[i], context);
        }

        return count;
    }

    /// <summary>
    /// Builds the single-query <c>JoinInto</c> clause for one declaration: the key predicate (with the
    /// child's own <c>Where</c> already folded in), the resolved child source, and the child's own
    /// selective filter scope. A per-parent paging child rendered as an embedded ranked derived source
    /// contributes its own ordered keys so the stitched children keep the child query's order.
    /// </summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="spec">The declaration's join specification.</param>
    /// <param name="childSource">The resolved child source.</param>
    /// <returns>The join clause.</returns>
    internal static JoinExpression BuildSingleQueryJoin<TEntity>(IJoinIntoSpec<TEntity> spec, FromExpression childSource)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(childSource);

        return new JoinExpression(spec.Predicate, spec.JoinType)
        {
            From = childSource,
            IsJoinInto = true,
            JoinIntoIdentity = spec.Identity,
            // The child query's own selective filter scope, independent of the parent's scope; an empty
            // scope is normalized to null (both mean "inherit").
            FilterScope = spec.ChildFilterScope.IsEmpty ? null : spec.ChildFilterScope,
            // A per-parent paging child is rendered as an embedded ranked derived source that cannot carry
            // its own ORDER BY; its ordered keys are appended to the pair command so the stitched children
            // keep the child query's order (resolved against this join alias).
            EagerPerParentOrdering = childSource.SubQuery is { PerParentPagingEmbedded: true } embeddedChild
                ? embeddedChild.Sorting
                : null,
        };
    }

    /// <summary>Runs every load specification on <paramref name="builder"/> for the materialized parents.</summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder that carries the specifications.</param>
    /// <param name="parents">The materialized parents, in parent-query order.</param>
    public static void Execute<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<TEntity> parents)
    {
        var specs = builder.LoadSpecs;
        if (specs is not { Count: > 0 })
            return;

        // Validate the whole declared nested graph once, at the root of the split orchestration: a cyclic
        // declaration (which would recurse without limit) or a nested declaration whose explicit mode
        // conflicts with the graph-wide mode fails closed here, before any descendant command is built.
        // Building the graph resolves the child factories through the same per-context memo the loader uses,
        // so no factory is invoked twice and no shared command is mutated.
        EagerLoadGraph.Build(builder, builder.DataProvider);

        ExecuteLoadedChildren(builder, parents);
    }

    /// <summary>
    /// Runs the specifications carried by <paramref name="builder"/> over <paramref name="parents"/>. Used
    /// both for the root level and, recursively, for each descendant level, with the children loaded at the
    /// previous level as the parents. A builder with no specification or no parent is a no-op, so
    /// level-one behavior is unchanged when no nested declaration exists.
    /// </summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder whose specifications run.</param>
    /// <param name="parents">The materialized parents for this level.</param>
    internal static void ExecuteLoadedChildren<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<TEntity> parents)
    {
        var specs = builder.LoadSpecs;
        if (specs is not { Count: > 0 })
            return;

        for (var i = 0; i < specs.Count; i++)
            specs[i].Execute(builder.DataProvider, builder.FilterScope, parents);
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

        // Same single-graph validation as the synchronous root (see <see cref="Execute{TEntity}"/>).
        EagerLoadGraph.Build(builder, builder.DataProvider);

        await ExecuteLoadedChildrenAsync(builder, parents, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Asynchronous counterpart of <see cref="ExecuteLoadedChildren{TEntity}"/>.</summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder whose specifications run.</param>
    /// <param name="parents">The materialized parents for this level.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when every specification has finished.</returns>
    internal static async Task ExecuteLoadedChildrenAsync<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<TEntity> parents, CancellationToken cancellationToken)
    {
        var specs = builder.LoadSpecs;
        if (specs is not { Count: > 0 })
            return;

        for (var i = 0; i < specs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await specs[i].ExecuteAsync(builder.DataProvider, builder.FilterScope, parents, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether <paramref name="child"/> requests a per-parent top-N (a child <c>Limit</c>/<c>Offset</c>/
    /// <c>Page</c>). Stated once and reused by the preparation-time marker
    /// (<see cref="EntityBuilderExtensions.MarkPerParentTopN"/>), the child loader and the <c>JoinInto</c>
    /// entry guard, so the three sites cannot drift apart.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="child">The resolved child builder.</param>
    /// <returns><see langword="true"/> when the child query pages per parent.</returns>
    public static bool HasPerParentTopN<TChild>(EntityBuilder<TChild> child)
        => !child.Paging.IsEmpty;

    /// <summary>
    /// The rejection for a child query that requests a per-parent top-N. Shared so the child loader and
    /// the <c>JoinInto</c> entry guard report the identical message.
    /// </summary>
    /// <returns>The per-parent top-N rejection.</returns>
    internal static NotSupportedException PerParentTopNNotSupported() =>
        new("LoadWith cannot apply Limit/Offset/Page to a child query: a per-parent top-N requires " +
            "correlated execution, which is not supported. Remove the child paging or use an explicit " +
            "child query per parent.");

    /// <summary>
    /// Validates that <paramref name="child"/>, when it requests a per-parent top-N, can be lowered by
    /// the ranked child source (<c>row_number() over (partition by ... order by ...)</c>). That lowering
    /// renders only the child's own <c>Where</c>, ordering and page bounds, so every other child shape is
    /// rejected instead of being silently dropped, and an actual page without an ordering is rejected
    /// because the window would have no valid order. A non-paged child is a no-op. Shared by the split
    /// loader and the single-query join so the two modes cannot drift.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="child">The resolved child builder.</param>
    /// <exception cref="NotSupportedException">
    /// The child pages per parent but carries an unsupported shape or no ordering.
    /// </exception>
    internal static void ValidatePerParentPagingChild<TChild>(EntityBuilder<TChild> child)
    {
        if (!HasPerParentTopN(child))
            return;

        // WITH TIES has no per-parent rendering: the ranked per-parent source keeps a fixed number of
        // rows per partition and cannot carry the tied-with-the-last-row semantics, so a child that
        // asks for both per-parent paging and WITH TIES fails closed here rather than silently
        // dropping the ties. Capability, not invalid input: NotSupportedException.
        if (child.Paging.HasWithTies)
            throw new NotSupportedException(
                "LoadWith cannot apply Limit/Offset/Page to a child query that also uses WithTies.");

        var unsupported = child.UnsupportedPerParentPagingChildShapes();
        if (unsupported.Count > 0)
            throw new NotSupportedException(
                $"LoadWith cannot apply Limit/Offset/Page to a child query that also carries " +
                $"{string.Join(", ", unsupported)}: the ranked per-parent paging source folds in only the " +
                "child's own Where and OrderBy. Remove the modifier or use an explicit child query per parent.");

        if (child.Sorting is not { Count: > 0 })
            throw new NotSupportedException(
                "LoadWith cannot apply Limit/Offset/Page to a child query without an OrderBy: a per-parent " +
                "page needs a window ordering, and a row_number() window with no OrderBy is invalid on " +
                "providers such as SQL Server. Add an OrderBy to the child query, or remove the child paging.");
    }

    /// <summary>
    /// Validates the eager-load specifications carried by a builder that is about to run its
    /// <c>JoinInto</c> path, <b>before</b> any command is built, prepared, cached or executed. An
    /// explicit <see cref="EagerLoadMode.Correlated"/> choice and a per-parent top-N child both have no
    /// implemented execution path, so they fail closed here instead of after the pair SQL ran or being
    /// silently reinterpreted. It resolves each load's child query to detect a per-parent top-N, so the
    /// child-query factory can run here (once per <c>(spec, context)</c>, memoized); it builds no command,
    /// writes no cache and never touches <see cref="QueryCommand.Cache"/>. A context-default
    /// <see cref="EagerLoadMode.Correlated"/> is
    /// ignored, because the <c>JoinInto</c> pair command is the fixed Single path and never resolves the
    /// context default.
    /// </summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder about to execute its <c>JoinInto</c> path.</param>
    /// <param name="allowPerParentTopN">
    /// When <see langword="true"/>, a child <c>Limit</c>/<c>Offset</c>/<c>Page</c> is accepted: the split
    /// list terminal applies it per parent through the ranked child loader. The non-list terminals and
    /// the single-query path keep the default <see langword="false"/> and still fail closed.
    /// </param>
    /// <exception cref="NotSupportedException">
    /// An explicit <see cref="EagerLoadMode.Correlated"/> is requested, or a load's child query pages per
    /// parent (<c>Limit</c>/<c>Offset</c>/<c>Page</c>) when <paramref name="allowPerParentTopN"/> is
    /// <see langword="false"/>.
    /// </exception>
    public static void ValidateLoadWithForJoinInto<TEntity>(EntityBuilder<TEntity> builder, bool allowPerParentTopN = false)
    {
        // Same explicit-vs-default criterion as EntityBuilder<TEntity>.SingleQuery: the builder's
        // _eagerLoadMode is set only by an explicit LoadWith choice, so an inherited context-default
        // Correlated must not be rejected (or JoinInto + Default under that default would break).
        if (builder.Mode == EagerLoadMode.Correlated)
            throw EagerLoadResolver.CorrelatedNotSupported();

        if (builder.LoadSpecs is not { Count: > 0 } specs)
            return;

        if (allowPerParentTopN)
            return;

        for (var i = 0; i < specs.Count; i++)
        {
            if (specs[i].RequiresPerParentTopN(builder.DataProvider))
                throw PerParentTopNNotSupported();
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

/// <summary>
/// Single-query <see cref="IJoinIntoSpec{TEntity}"/> for a nested (level 2+) <c>LoadWith</c> graph. It
/// exposes the flattened projection item types of the whole subtree and assembles every level from the
/// one denormalized result set by delegating the recursive section-7 assembly to the level-one
/// <see cref="EagerLoadSpec{TEntity,TChild,TKey}"/>. The predicate, identity and child filter scope stay
/// the level-one declaration's, so the rendered join and the plan identity match the flat case.
/// </summary>
/// <typeparam name="TEntity">The root entity type.</typeparam>
/// <typeparam name="TChild">The level-one child entity type.</typeparam>
/// <typeparam name="TKey">The level-one key type.</typeparam>
internal sealed class EagerLoadNestedJoinIntoSpec<TEntity, TChild, TKey> : IJoinIntoSpec<TEntity>, IEagerSingleQueryStitch<TEntity>
    where TKey : notnull
{
    private readonly EagerLoadJoinIntoSpec<TEntity, TChild, TKey> _level1;
    private readonly EagerLoadSpec<TEntity, TChild, TKey> _eager;
    private readonly IReadOnlyList<Type> _itemTypes;

    /// <summary>Initializes the specification.</summary>
    /// <param name="level1">The level-one join specification that supplies the predicate/identity.</param>
    /// <param name="eager">The level-one eager-load specification that drives the recursive assembly.</param>
    /// <param name="itemTypes">The flattened projection item types of the whole subtree.</param>
    public EagerLoadNestedJoinIntoSpec(
        EagerLoadJoinIntoSpec<TEntity, TChild, TKey> level1,
        EagerLoadSpec<TEntity, TChild, TKey> eager,
        IReadOnlyList<Type> itemTypes)
    {
        ArgumentNullException.ThrowIfNull(level1);
        ArgumentNullException.ThrowIfNull(eager);
        ArgumentNullException.ThrowIfNull(itemTypes);
        _level1 = level1;
        _eager = eager;
        _itemTypes = itemTypes;
    }

    /// <inheritdoc/>
    public JoinType JoinType => _level1.JoinType;

    /// <inheritdoc/>
    public PropertyInfo? CollectionProperty => _level1.CollectionProperty;

    /// <inheritdoc/>
    public LambdaExpression Predicate => _level1.Predicate;

    /// <inheritdoc/>
    public Type ChildEntityType => _level1.ChildEntityType;

    /// <inheritdoc/>
    public IReadOnlyList<Type> ItemTypes => _itemTypes;

    /// <inheritdoc/>
    public JoinIntoIdentity Identity => _level1.Identity;

    /// <inheritdoc/>
    public QueryFilterScope ChildFilterScope => _level1.ChildFilterScope;

    /// <inheritdoc/>
    public object? GetParentKey(TEntity parent) => _level1.GetParentKey(parent);

    /// <inheritdoc/>
    public void AssignChildren(IReadOnlyList<TEntity> parents, IReadOnlyList<(TEntity Parent, object? Child)> rows)
        => _level1.AssignChildren(parents, rows);

    /// <inheritdoc/>
    void IEagerSingleQueryStitch<TEntity>.StitchSingleQuery(
        IReadOnlyList<TEntity> parents,
        IReadOnlyList<object?> rows,
        IReadOnlyList<PropertyInfo> columns,
        int parentColumn,
        int childColumn,
        IDataContext context)
        => _eager.StitchSingleQueryCore(parents, rows, columns, parentColumn, childColumn, context);
}

/// <summary>
/// One node of the declared finite nested eager-load graph: a declaration together with its
/// ancestor-composite identity (<c>L1 key + L2 key + ...</c>). Reference identity is never used; a node's
/// per-level segment is the structural fingerprint (<see cref="IEagerLoadNodeSpec.ShapeKey"/>) of its
/// declaration, so two structurally identical declarations are the same node.
/// </summary>
internal sealed class EagerLoadNode
{
    internal EagerLoadNode(IEagerLoadNodeSpec spec, EagerLoadNode? parent)
    {
        Spec = spec;
        Parent = parent;
        Segment = spec.ShapeKey;
        Path = parent is null ? [Segment] : [.. parent.Path, Segment];
        CompositeIdentity = string.Join("/", Path);
        Depth = Path.Count;
    }

    /// <summary>The declaration this node represents.</summary>
    public IEagerLoadNodeSpec Spec { get; }

    /// <summary>The ancestor node on the path to the graph root, or <c>null</c> for a top-level node.</summary>
    public EagerLoadNode? Parent { get; }

    /// <summary>This node's structural segment: the fingerprint of its declaration.</summary>
    public string Segment { get; }

    /// <summary>The ordered structural segments from the graph root to this node (its ancestor path).</summary>
    public IReadOnlyList<string> Path { get; }

    /// <summary>
    /// The ancestor-composite identity: the path segments joined in order (<c>L1 key + L2 key + ...</c>).
    /// Two nodes with equal identity occupy the same graph position.
    /// </summary>
    public string CompositeIdentity { get; }

    /// <summary>This node's depth from the graph root (a top-level node is depth 1).</summary>
    public int Depth { get; }

    /// <summary>Whether this is a top-level declaration (it has no ancestor).</summary>
    public bool IsRoot => Parent is null;
}

/// <summary>
/// The graph-wide execution context shared by every node of one eager-load graph: the single resolved
/// eager-load strategy (one mode for the whole graph) and the root's global-query-filter scope, inherited
/// by every descendant.
/// </summary>
/// <param name="Mode">The executable strategy resolved for the whole graph.</param>
/// <param name="FilterScope">The root builder's global-query-filter scope, inherited by every descendant.</param>
internal readonly record struct EagerLoadGraphContext(EagerLoadMode Mode, QueryFilterScope FilterScope);

/// <summary>
/// The declared finite nested eager-load graph normalized to the shared contract: the root nodes, the
/// flattened nodes with their ancestor-composite identities, and the graph-wide mode/filter context.
/// Built by <see cref="Build{TEntity}"/>; building validates the declaration and fails closed on a cyclic
/// graph or a nested mode that conflicts with the graph-wide mode. It builds no command, writes no cache
/// and never touches <see cref="QueryCommand.Cache"/>.
/// </summary>
internal sealed class EagerLoadGraph
{
    /// <summary>The empty graph: a builder that declares no eager loads.</summary>
    public static readonly EagerLoadGraph Empty = new([], [], new EagerLoadGraphContext(EagerLoadMode.Default, QueryFilterScope.None));

    private EagerLoadGraph(IReadOnlyList<EagerLoadNode> roots, IReadOnlyList<EagerLoadNode> nodes, EagerLoadGraphContext context)
    {
        Roots = roots;
        Nodes = nodes;
        Context = context;
    }

    /// <summary>The top-level nodes, in declaration order.</summary>
    public IReadOnlyList<EagerLoadNode> Roots { get; }

    /// <summary>Every node of the graph, in declaration order (pre-order).</summary>
    public IReadOnlyList<EagerLoadNode> Nodes { get; }

    /// <summary>The graph-wide mode/filter context.</summary>
    public EagerLoadGraphContext Context { get; }

    /// <summary>Whether the graph declares no eager loads.</summary>
    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>The deepest node depth (0 for the empty graph; a top-level node is depth 1).</summary>
    public int MaxDepth
    {
        get
        {
            var max = 0;
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Depth > max)
                    max = Nodes[i].Depth;
            }

            return max;
        }
    }

    /// <summary>Finds the node whose ancestor-composite identity equals <paramref name="compositeIdentity"/>.</summary>
    /// <param name="compositeIdentity">The ancestor-composite identity to look up.</param>
    /// <returns>The matching node, or <c>null</c> when the graph has none.</returns>
    public EagerLoadNode? Find(string compositeIdentity)
    {
        ArgumentNullException.ThrowIfNull(compositeIdentity);
        for (var i = 0; i < Nodes.Count; i++)
        {
            if (string.Equals(Nodes[i].CompositeIdentity, compositeIdentity, StringComparison.Ordinal))
                return Nodes[i];
        }

        return null;
    }

    /// <summary>
    /// Folds the graph's ancestor-composite identities into the plan-key shape, so two otherwise identical
    /// parent commands with different nested graphs cannot share a cached plan. It is the ready seam for
    /// the split/single execution units to key nested nodes into the plan cache.
    /// </summary>
    /// <returns>The structural shape over the node identities.</returns>
    public EagerLoadShape ToShape()
    {
        if (IsEmpty)
            return EagerLoadShape.Empty;

        var parts = new string[Nodes.Count];
        for (var i = 0; i < Nodes.Count; i++)
            parts[i] = Nodes[i].CompositeIdentity;

        return new EagerLoadShape(parts);
    }

    /// <summary>
    /// Normalizes the eager-load state declared on <paramref name="builder"/> into the shared nested-graph
    /// contract for <paramref name="context"/>. It resolves the single graph-wide mode through the shared
    /// <see cref="EagerLoadResolver"/> (never per node), inherits the root's filter scope, and recursively
    /// walks the declared child factories. A declaration whose ancestor-composite identity repeats (a
    /// cycle) or whose nested explicit mode conflicts with the graph-wide mode fails closed with
    /// <see cref="NotSupportedException"/> before any command is built.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <param name="builder">The builder whose declarations are normalized.</param>
    /// <param name="context">The data context used to resolve child query builders.</param>
    /// <returns>The normalized graph, or <see cref="Empty"/> when the builder declares no eager loads.</returns>
    /// <exception cref="NotSupportedException">
    /// The declaration is cyclic, a nested declaration requests a conflicting explicit mode, or the shared
    /// resolver rejects the graph-wide mode (an explicit <see cref="EagerLoadMode.Correlated"/>).
    /// </exception>
    public static EagerLoadGraph Build<TEntity>(EntityBuilder<TEntity> builder, IDataContext context)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        var specs = ((IEagerLoadNodeSource)builder).EagerLoadNodeSpecs;
        if (specs is not { Count: > 0 })
            return Empty;

        // One mode for the whole graph: resolve it once through the shared T209 resolver, so an explicit
        // Correlated (or an undefined value) fails closed here exactly as it does during preparation.
        var mode = builder.ResolvedEagerLoadMode;
        var graphContext = new EagerLoadGraphContext(mode, builder.FilterScope);

        var roots = new List<EagerLoadNode>();
        var nodes = new List<EagerLoadNode>();
        var ancestorSegments = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < specs.Count; i++)
            AddNode(specs[i], parent: null, mode, ancestorSegments, nodes, roots, context);

        return new EagerLoadGraph(roots, nodes, graphContext);
    }

    private static void AddNode(
        IEagerLoadNodeSpec spec,
        EagerLoadNode? parent,
        EagerLoadMode graphMode,
        HashSet<string> ancestorSegments,
        List<EagerLoadNode> nodes,
        List<EagerLoadNode> roots,
        IDataContext context)
    {
        var segment = spec.ShapeKey;
        if (!ancestorSegments.Add(segment))
        {
            // A deterministic cycle guard, not a depth cap: the same structural node may not appear as its
            // own ancestor. The path identity repeats exactly when the declaration recurses into itself;
            // rejecting it here keeps the walk finite and stops an unbounded declaration before it can
            // recurse without limit.
            throw new NotSupportedException(
                "The declared eager-load graph is cyclic: the node '" + segment + "' appears again below " +
                "itself (its ancestor-composite identity repeats). Declare a finite acyclic graph; a " +
                "repeating node is rejected deterministically, not truncated by a depth limit.");
        }

        var node = new EagerLoadNode(spec, parent);
        nodes.Add(node);
        if (parent is null)
            roots.Add(node);

        var childSpecs = spec.ResolveChildNodeSource(context).EagerLoadNodeSpecs;
        if (childSpecs is { Count: > 0 })
        {
            for (var i = 0; i < childSpecs.Count; i++)
            {
                ValidateNodeMode(childSpecs[i], graphMode);
                AddNode(childSpecs[i], node, graphMode, ancestorSegments, nodes, roots, context);
            }
        }

        ancestorSegments.Remove(segment);
    }

    private static void ValidateNodeMode(IEagerLoadNodeSpec spec, EagerLoadMode graphMode)
    {
        var requested = spec.RequestedMode;
        if (requested == EagerLoadMode.Default)
            return;

        var resolved = EagerLoadResolver.Resolve(requested, graphMode);
        if (resolved != graphMode)
        {
            throw new NotSupportedException(
                "The declared eager-load graph mixes modes: the graph-wide mode is EagerLoadMode." +
                graphMode + " but a nested declaration requests EagerLoadMode." + requested +
                ", which resolves to EagerLoadMode." + resolved +
                ". One mode applies to the whole graph; remove the nested mode override.");
        }
    }
}
