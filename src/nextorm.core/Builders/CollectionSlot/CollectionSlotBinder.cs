using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace NextORM.Core;

/// <summary>
/// A collection-valued projection carrier (#249 / T353): the public command keeps the projected result type,
/// while execution runs the full entity query through the existing eager-loading preparation and terminals and
/// projects the materialized graph in memory. No SQL column is added for a slot and no correlation column is
/// appended; the projection never escapes before the graph is materialized.
/// </summary>
internal abstract class CollectionSlotCarrier
{
    /// <summary>The refusal used when a terminal the carrier cannot honour is reached (before any SQL).</summary>
    /// <returns>The deterministic refusal message.</returns>
    internal abstract string UnsupportedTerminal();
}

/// <summary>The typed half of <see cref="CollectionSlotCarrier"/>: one buffered list of the projected shape.</summary>
/// <typeparam name="TResult">The projected shape the public command exposes.</typeparam>
internal abstract class CollectionSlotCarrier<TResult> : CollectionSlotCarrier
{
    /// <summary>Executes the carrier and returns the buffered projection.</summary>
    /// <param name="params">Positional parameter values forwarded to the entity query.</param>
    /// <returns>The projected rows.</returns>
    internal abstract List<TResult> ToList(ReadOnlySpan<object?> @params);
}

/// <summary>
/// Binds the recognized collection slots of a projection to an entity carrier (#249 / T353): each slot becomes an
/// eager-load declaration on an internal entity query (so split/single, ordering, paging and the assignment
/// contract are the existing ones), while the in-memory pipeline applies the adapter operations and converts the
/// result into the slot's declared CLR shape.
/// </summary>
internal static class CollectionSlotBinder
{
    /// <summary>
    /// Recognizes and binds the slots of <paramref name="projection"/>, or leaves the command untouched when it
    /// carries none. Every refusal is raised here, before any SQL is built and before any plan entry exists.
    /// </summary>
    /// <typeparam name="TEntity">The entity being projected.</typeparam>
    /// <typeparam name="TResult">The projected shape.</typeparam>
    /// <param name="builder">The builder the projection was declared on.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="command">The command the projection produced.</param>
    /// <param name="groupingComposed">Whether the query also composes an explicit grouping.</param>
    internal static void TryBind<TEntity, TResult>(
        EntityBuilder<TEntity> builder,
        Expression<Func<TEntity, TResult>> projection,
        QueryCommand<TResult> command,
        bool groupingComposed)
    {
        var isNavigation = (Func<PropertyInfo, bool>)(property =>
            MetadataEagerLoadResolver.HasDeclaredRelationship(builder.DataProvider, typeof(TEntity), property));

        var recognition = CollectionSlotRecognizer.Recognize(projection, isNavigation);
        if (recognition is null)
            return;

        if (groupingComposed)
        {
            throw new NotSupportedException(
                "A collection-valued projection slot cannot be combined with grouping: SelectGroup (or an explicit " +
                "GroupBy) aggregates one row per key, so a per-parent collection has no owner. Project the parent " +
                "entities first and group a scalar projection.");
        }

        if (!builder.DataProvider.NeedMapping)
        {
            throw new NotSupportedException(
                "A collection-valued projection slot requires a SQL provider: the in-memory provider has no " +
                "eager-loading mapping, so the per-parent collections cannot be materialized without silently " +
                "falling back to a client-side join.");
        }

        var bindSlot = typeof(CollectionSlotBinder).GetMethod(nameof(BindSlot), BindingFlags.NonPublic | BindingFlags.Static)!;
        var buildReader = typeof(CollectionSlotBinder).GetMethod(nameof(BuildReader), BindingFlags.NonPublic | BindingFlags.Static)!;
        var readers = new Func<TEntity, object?>[recognition.Slots.Count];
        var entityBuilder = builder;
        var boundPaths = new Dictionary<string, EntityBuilder<TEntity>>(StringComparer.Ordinal);

        for (var i = 0; i < recognition.Slots.Count; i++)
        {
            var slot = recognition.Slots[i];

            // One eager-load declaration per navigation: the same collection projected into two different CLR
            // shapes shares the materialized collection and only differs in the conversion.
            if (boundPaths.TryGetValue(slot.Path, out var alreadyBound))
            {
                entityBuilder = alreadyBound;
                readers[i] = (Func<TEntity, object?>)InvokeAfterBinding(
                    buildReader, [typeof(TEntity), slot.ChildType], [slot]);
                continue;
            }

            var bound = (BoundSlot<TEntity>)InvokeAfterBinding(
                bindSlot, [typeof(TEntity), slot.ChildType], [entityBuilder, slot]);

            entityBuilder = bound.Builder;
            boundPaths[slot.Path] = bound.Builder;
            readers[i] = bound.Reader;
        }

        var projector = CollectionSlotProjector.Build(projection, recognition.Nodes);
        command.CollectionSlotCarrier = new CollectionSlotCarrier<TEntity, TResult>(entityBuilder, readers, projector);
    }

    private static BoundSlot<TEntity> BindSlot<TEntity, TChild>(EntityBuilder<TEntity> builder, CollectionSlotDescriptor slot)
        where TChild : class
    {
        var collection = Expression.Lambda<Func<TEntity, ICollection<TChild>>>(slot.Navigation, slot.Root);
        var binding = MetadataEagerLoadResolver.Resolve<TEntity, TChild>(
            builder.DataProvider, collection, MetadataEagerLoadRoute.Collection, EagerLoadMode.Default, "a collection projection slot");

        if (binding.Relationship.Kind == RelationshipKind.ManyToMany && slot.Operations.OfType<CollectionSlotLimit>().Any())
        {
            throw new NotSupportedException(
                $"The collection projection slot '{slot.Path}' applies Limit(...) to the many-to-many relationship " +
                $"'{typeof(TEntity).Name}.{binding.Navigation.Name}': a per-parent page across a junction has no " +
                "parent partition in this release (M:N + Limit). Remove the Limit, or load the relationship with the " +
                "split many-to-many loader and page the materialized collection yourself.");
        }

        var bound = slot.IsAdapter
            ? builder.LoadWith<TChild>(collection, static context => context.From<TChild>())
            : builder.LoadWith<TChild>(collection);

        return new BoundSlot<TEntity>
        {
            Builder = bound,
            Reader = CollectionSlotReaderFactory.Build<TEntity, TChild>(slot),
        };
    }

    private static Func<TEntity, object?> BuildReader<TEntity, TChild>(CollectionSlotDescriptor slot)
        where TChild : class
        => CollectionSlotReaderFactory.Build<TEntity, TChild>(slot);

    /// <summary>
    /// Binds the open generic over the slot's child type. The generic dispatch goes through reflection because the
    /// child type is only known at runtime, so a refusal raised inside must keep its own type and message instead
    /// of arriving wrapped in a <see cref="TargetInvocationException"/>.
    /// </summary>
    private static object InvokeAfterBinding(MethodInfo method, Type[] typeArguments, object?[] arguments)
    {
        try
        {
            return method.MakeGenericMethod(typeArguments).Invoke(null, arguments)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private readonly struct BoundSlot<TEntity>
    {
        internal required EntityBuilder<TEntity> Builder { get; init; }

        internal required Func<TEntity, object?> Reader { get; init; }
    }
}

/// <summary>
/// The carrier implementation (#249 / T353): materialize the entity graph through the existing eager-loading
/// terminals, then project every parent in memory. The projection runs strictly after materialization, so no
/// partial result can escape and no extra SQL column carries correlation keys.
/// </summary>
internal sealed class CollectionSlotCarrier<TEntity, TResult> : CollectionSlotCarrier<TResult>
{
    private readonly EntityBuilder<TEntity> _entityBuilder;
    private readonly Func<TEntity, object?>[] _readers;
    private readonly Func<TEntity, object?[], TResult> _projector;

    internal CollectionSlotCarrier(
        EntityBuilder<TEntity> entityBuilder,
        Func<TEntity, object?>[] readers,
        Func<TEntity, object?[], TResult> projector)
    {
        _entityBuilder = entityBuilder;
        _readers = readers;
        _projector = projector;
    }

    internal override List<TResult> ToList(ReadOnlySpan<object?> @params)
    {
        var entities = EntityBuilderExtensions.ToList(_entityBuilder, @params);
        var values = new object?[_readers.Length];
        var projected = new List<TResult>(entities.Count);

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            for (var j = 0; j < _readers.Length; j++)
                values[j] = _readers[j](entity);

            projected.Add(_projector(entity, values));
        }

        return projected;
    }

    internal override string UnsupportedTerminal()
        => "A collection-valued projection slot supports only ToList() and ToArray(): this terminal would have to " +
           "stream or aggregate a per-parent projection that is computed after materialization, so it is refused " +
           "before any SQL is executed.";
}

/// <summary>
/// The in-memory half of a collection slot (#249 / T353): reads the materialized navigation, applies the recorded
/// adapter operations per parent (<c>Where</c>, the ordering chain, <c>Limit</c>, <c>Select</c>) and converts the
/// result into the destination's CLR shape. The adapter's lambdas keep their reference to the projection parameter,
/// so a correlated predicate such as <c>i =&gt; i.ParentId == o.Id</c> sees the current parent without recompiling.
/// </summary>
internal static class CollectionSlotReaderFactory
{
    internal static Func<TEntity, object?> Build<TEntity, TChild>(CollectionSlotDescriptor slot)
        where TChild : class
    {
        var root = slot.Root;
        var child = Expression.Parameter(typeof(TChild), "slotChild");
        var navigation = Expression.Lambda<Func<TEntity, IEnumerable<TChild>?>>(slot.Navigation, root).Compile();

        Func<TChild, TEntity, bool>? where = null;
        var orderings = new List<(Func<TChild, TEntity, object?> Selector, bool Descending, bool ThenBy)>();
        Func<TEntity, int>? limit = null;
        Func<TChild, TEntity, object?>? select = null;

        foreach (var operation in slot.Operations)
        {
            switch (operation)
            {
                case CollectionSlotWhere predicate:
                    where = Compile<TChild, TEntity, bool>(predicate.Predicate, child, root);
                    break;

                case CollectionSlotOrder ordering:
                    orderings.Add((Compile<TChild, TEntity, object?>(ordering.Selector, child, root), ordering.Descending, ordering.ThenBy));
                    break;

                case CollectionSlotLimit take:
                    limit = Expression.Lambda<Func<TEntity, int>>(Expression.Convert(take.Count, typeof(int)), root).Compile();
                    break;

                case CollectionSlotSelect projection:
                    select = Compile<TChild, TEntity, object?>(projection.Projection, child, root);
                    break;
            }
        }

        var elementType = slot.ElementType;
        var slotType = slot.SlotType;

        return entity =>
        {
            IEnumerable<TChild> items = navigation(entity) ?? [];
            if (where is not null)
                items = items.Where(item => where(item, entity));

            IOrderedEnumerable<TChild>? ordered = null;
            for (var i = 0; i < orderings.Count; i++)
            {
                var (selector, descending, thenBy) = orderings[i];
                if (!thenBy || ordered is null)
                {
                    ordered = descending
                        ? items.OrderByDescending(item => selector(item, entity), SlotComparer.Instance)
                        : items.OrderBy(item => selector(item, entity), SlotComparer.Instance);
                }
                else
                {
                    ordered = descending
                        ? ordered.ThenByDescending(item => selector(item, entity), SlotComparer.Instance)
                        : ordered.ThenBy(item => selector(item, entity), SlotComparer.Instance);
                }
            }

            if (ordered is not null)
                items = ordered;

            if (limit is not null)
                items = items.Take(Math.Max(0, limit(entity)));

            var values = new List<object?>();
            foreach (var item in items)
                values.Add(select is null ? item : select(item, entity));

            return CollectionSlotShapes.Convert(values, elementType, slotType);
        };
    }

    private static Func<TChild, TEntity, TOut> Compile<TChild, TEntity, TOut>(LambdaExpression lambda, ParameterExpression child, ParameterExpression root)
    {
        var body = new ParameterReplacer(lambda.Parameters[0], child).Visit(lambda.Body)!;
        if (body.Type != typeof(TOut))
            body = Expression.Convert(body, typeof(TOut));

        return Expression.Lambda<Func<TChild, TEntity, TOut>>(body, child, root).Compile();
    }

    private sealed class SlotComparer : IComparer<object?>
    {
        internal static readonly SlotComparer Instance = new();

        public int Compare(object? x, object? y) => Comparer<object?>.Default.Compare(x, y);
    }
}

/// <summary>
/// Builds the buffered projector (#249 / T353): the original projection with every slot node replaced by the value
/// the carrier computed, compiled once per command. The compiled delegate carries the projection shape only — the
/// values travel in the execution-time array, never in the key or the delegate.
/// </summary>
internal static class CollectionSlotProjector
{
    internal static Func<TEntity, object?[], TResult> Build<TEntity, TResult>(
        Expression<Func<TEntity, TResult>> projection,
        IReadOnlyList<Expression> nodes)
    {
        var values = Expression.Parameter(typeof(object?[]), "slotValues");
        var indices = new Dictionary<Expression, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < nodes.Count; i++)
            indices[nodes[i]] = i;

        var body = new SlotReplacementVisitor(indices, values).Visit(projection.Body)!;
        return Expression.Lambda<Func<TEntity, object?[], TResult>>(body, projection.Parameters[0], values).Compile();
    }

    private sealed class SlotReplacementVisitor(Dictionary<Expression, int> indices, ParameterExpression values) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            if (node is not null && indices.TryGetValue(node, out var index))
                return Expression.Convert(Expression.ArrayIndex(values, Expression.Constant(index)), node.Type);

            return base.Visit(node);
        }
    }
}

/// <summary>Replaces one parameter expression with another throughout a lambda body.</summary>
internal sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
}
