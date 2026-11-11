using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The recognized collection-valued slots of one projection (#249 / T353): the descriptors, the exact
/// expression node each one replaces, and nothing else.
/// </summary>
internal sealed class CollectionSlotRecognition
{
    internal required List<CollectionSlotDescriptor> Slots { get; init; }

    internal required List<Expression> Nodes { get; init; }
}

/// <summary>
/// Recognition of the approved collection-valued projection slots (#249 / T353): a direct member of a mapped
/// collection navigation, or an adapter chain (<c>nav.AsEntityBuilder&lt;T&gt;()</c> followed by
/// <c>Where</c>/<c>OrderBy</c>/<c>ThenBy</c>/<c>Limit</c>/<c>Select</c>) terminated by a parameterless
/// <c>ToList()</c>/<c>ToArray()</c>. Recognition is structural and side-effect free: every unsupported shape is
/// rejected here, before any SQL is built and before any plan entry exists.
/// </summary>
internal static class CollectionSlotRecognizer
{
    private static readonly HashSet<string> OrderingMethods = new(StringComparer.Ordinal)
    {
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
    };

    /// <summary>Recognizes the slots of a projection, or returns <see langword="null"/> when it carries none.</summary>
    /// <typeparam name="TEntity">The entity being projected.</typeparam>
    /// <typeparam name="TResult">The projected shape.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="dataProvider">
    /// The context that owns the query; the declared relationship kind of a member navigation is resolved
    /// through its metadata. Everything an ordinary scalar projection needs is allocation-free: the ordinal
    /// accumulation lists are created only when a slot is actually recognized.
    /// </param>
    /// <returns>The recognition, or <see langword="null"/> for an ordinary scalar projection.</returns>
    internal static CollectionSlotRecognition? Recognize<TEntity, TResult>(
        Expression<Func<TEntity, TResult>> projection,
        IDataContext dataProvider)
    {
        var root = projection.Parameters[0];
        var state = default(RecognitionState);

        VisitProjection(projection.Body, root, typeof(TEntity), dataProvider, ref state);

        var slots = state.Slots;
        if (slots is null || !slots.Exists(static slot => !slot.IsReference))
            return null;

        // A reference-only projection is an ordinary entity projection: the implicit-navigation path owns it and
        // must not change. The carrier and its cardinality guard are projection-scoped to projections that
        // actually ask for a per-parent collection.
        return new CollectionSlotRecognition { Slots = slots, Nodes = state.Nodes! };
    }

    /// <summary>
    /// Accumulates recognized slots (and the node each one replaces) behind nullable lists, so a projection
    /// that carries no slot — the common case — allocates nothing at all.
    /// </summary>
    private struct RecognitionState
    {
        internal List<CollectionSlotDescriptor>? Slots;
        internal List<Expression>? Nodes;

        internal readonly int Count => Slots?.Count ?? 0;

        internal void Add(CollectionSlotDescriptor slot, Expression node)
        {
            (Slots ??= new List<CollectionSlotDescriptor>(2)).Add(slot);
            (Nodes ??= new List<Expression>(2)).Add(node);
        }
    }

    private static RelationshipKind? RelationshipKindOf(IDataContext dataProvider, Type parentType, PropertyInfo property)
        => MetadataEagerLoadResolver.DeclaredRelationshipKind(dataProvider, parentType, property);

    private static void VisitProjection(
        Expression body,
        ParameterExpression root,
        Type parentType,
        IDataContext dataProvider,
        ref RecognitionState state)
    {
        switch (body)
        {
            case MemberInitExpression init:
                VisitProjection(init.NewExpression, root, parentType, dataProvider, ref state);
                foreach (var binding in init.Bindings)
                {
                    if (binding is not MemberAssignment assignment)
                        continue;

                    AddCandidate(assignment.Expression, assignment.Member, -1, MemberType(assignment.Member), root, parentType, dataProvider, ref state);
                }

                break;

            case NewExpression creation:
                var parameters = creation.Constructor?.GetParameters();
                for (var i = 0; i < creation.Arguments.Count; i++)
                {
                    var parameterType = parameters?[i].ParameterType ?? creation.Arguments[i].Type;
                    AddCandidate(creation.Arguments[i], null, i, parameterType, root, parentType, dataProvider, ref state);
                }

                break;
        }
    }

    private static void AddCandidate(
        Expression expression,
        MemberInfo? member,
        int argumentIndex,
        Type destinationType,
        ParameterExpression root,
        Type parentType,
        IDataContext dataProvider,
        ref RecognitionState state)
    {
        if (TryRecognize(expression, root, destinationType, parentType, dataProvider, out var slot))
        {
            slot.Member = member;
            slot.ArgumentIndex = argumentIndex;
            state.Add(slot, expression);
            return;
        }

        if (expression is not (MemberInitExpression or NewExpression))
        {
            // Only an expression that carries a *declared navigation* can be a malformed slot; anything else
            // (a scalar array column, a collection-valued SQL function) is an ordinary projected value.
            if (TryGetRootedNavigation(expression, root) is { } navigation && RelationshipKindOf(dataProvider, parentType, navigation) is not null && TryGetCollectionElement(expression.Type, out _))
            {
                throw new NotSupportedException(
                    $"The collection projection slot '{Describe(member)}' is expressed as '{expression}', which is not a " +
                    "supported collection slot: declare it as a direct navigation member or as a navigation adapter " +
                    "(nav.AsEntityBuilder<T>() ... .ToList()/.ToArray()).");
            }

            return;
        }

        // A child projection constructed inline (not through an adapter Select) that itself carries a slot: the
        // slot's navigation is still rooted at this level, but the destination DTO is built in memory, so the
        // outer projector cannot address it. Refuse by name instead of silently projecting the child shape.
        var nested = default(RecognitionState);
        VisitProjection(expression, root, parentType, dataProvider, ref nested);
        if (nested.Count > 0)
        {
            throw new NotSupportedException(
                "The projection carries a collection slot inside an inline child projection: a nested slot must be " +
                "declared inside an adapter Select(...) over the navigation element, so that it is rooted at the " +
                "child entity. Project the child shape without a collection slot.");
        }
    }

    private static bool TryRecognize(
        Expression expression,
        ParameterExpression root,
        Type destinationType,
        Type parentType,
        IDataContext dataProvider,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        return TryRecognizeAdapter(expression, root, destinationType, parentType, dataProvider, out slot)
            || TryRecognizeDirect(expression, root, destinationType, parentType, dataProvider, out slot)
            || TryRecognizeReference(expression, root, parentType, dataProvider, out slot);
    }

    private static bool TryRecognizeDirect(
        Expression expression,
        ParameterExpression root,
        Type destinationType,
        Type parentType,
        IDataContext dataProvider,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        if (expression is not MemberExpression member || !IsRootedAt(member, root))
            return false;

        if (member.Member is not PropertyInfo property || RelationshipKindOf(dataProvider, parentType, property) is not (RelationshipKind.OneToMany or RelationshipKind.ManyToMany))
            return false;

        if (!TryGetCollectionElement(member.Type, out var elementType))
            return false;

        EnsureAssignableNavigation(property);

        if (!CollectionSlotShapes.IsSupported(destinationType, elementType))
            throw UnsupportedShape(Describe(member.Member), destinationType);

        slot = new CollectionSlotDescriptor
        {
            SlotType = destinationType,
            ElementType = elementType,
            ChildType = elementType,
            ParentType = parentType,
            Navigation = member,
            Root = root,
            Path = Describe(member.Member),
        };
        return true;
    }

    /// <summary>
    /// Recognizes a projected <em>reference</em> navigation (N:1 or 1:1): the projection reads one related
    /// entity per parent, so the carrier has to materialize it through the eager-loading reference route, whose
    /// raw-row identity resolution is the projection-scoped cardinality guard.
    /// </summary>
    private static bool TryRecognizeReference(
        Expression expression,
        ParameterExpression root,
        Type parentType,
        IDataContext dataProvider,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        if (expression is not MemberExpression member || member.Expression != root)
            return false;

        if (member.Member is not PropertyInfo property || RelationshipKindOf(dataProvider, parentType, property) is not (RelationshipKind.ManyToOne or RelationshipKind.OneToOne))
            return false;

        slot = new CollectionSlotDescriptor
        {
            IsReference = true,
            SlotType = member.Type,
            ElementType = member.Type,
            ChildType = member.Type,
            ParentType = parentType,
            Navigation = member,
            Root = root,
            Path = Describe(member.Member),
        };
        return true;
    }

    private static bool TryRecognizeAdapter(
        Expression expression,
        ParameterExpression root,
        Type destinationType,
        Type parentType,
        IDataContext dataProvider,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        if (expression is not MethodCallExpression terminal || !TryGetTerminalOperand(terminal, out var first))
            return false;

        var operations = new List<CollectionSlotOperation>();
        Expression current = first;
        MemberExpression? navigation = null;

        while (current is MethodCallExpression call)
        {
            if (call.Method.DeclaringType == typeof(EntityBuilderExtensions) && call.Method.Name == "AsEntityBuilder")
            {
                navigation = call.Arguments[0] as MemberExpression;
                break;
            }

            if (!IsEntityBuilderMethod(call.Method))
                throw UnsupportedAdapterMethod(call.Method.Name);

            if (call.Method.Name == "Where")
            {
                operations.Insert(0, new CollectionSlotWhere(UnwrapLambda(call.Arguments[0], "Where")));
            }
            else if (OrderingMethods.Contains(call.Method.Name))
            {
                var descending = call.Method.Name is "OrderByDescending" or "ThenByDescending";
                var thenBy = call.Method.Name is "ThenBy" or "ThenByDescending";
                operations.Insert(0, new CollectionSlotOrder(UnwrapLambda(call.Arguments[0], call.Method.Name), descending, thenBy));
            }
            else if (call.Method.Name == "Limit")
            {
                operations.Insert(0, new CollectionSlotLimit(call.Arguments[0]));
            }
            else if (call.Method.Name == "Select")
            {
                var projection = UnwrapLambda(call.Arguments[0], "Select");
                operations.Insert(0, new CollectionSlotSelect(projection, projection.ReturnType));
            }
            else
            {
                throw UnsupportedAdapterMethod(call.Method.Name);
            }

            if (call.Object is null && call.Arguments.Count == 0)
                return false;

            current = (call.Object ?? call.Arguments[0])!;
        }

        if (navigation is null
            || navigation.Member is not PropertyInfo navigationProperty
            || RelationshipKindOf(dataProvider, parentType, navigationProperty) is not (RelationshipKind.OneToMany or RelationshipKind.ManyToMany)
            || !IsRootedAt(navigation, root)
            || !TryGetCollectionElement(navigation.Type, out var childType))
            return false;

        EnsureAssignableNavigation(navigationProperty);

        var elementType = operations.OfType<CollectionSlotSelect>().LastOrDefault()?.ResultType ?? childType;

        if (!CollectionSlotShapes.IsSupported(destinationType, elementType))
            throw UnsupportedShape(Describe(navigation.Member), destinationType);

        // A slot declared inside the adapter's Select(...) belongs to the child level: its navigation is rooted at
        // that lambda's parameter, and the binder declares it on the child builder. The recursion is structural, so
        // any depth is handled by the same code path.
        var nested = default(RecognitionState);
        if (operations.OfType<CollectionSlotSelect>().LastOrDefault() is { } select)
            VisitProjection(select.Projection.Body, select.Projection.Parameters[0], childType, dataProvider, ref nested);

        slot = new CollectionSlotDescriptor
        {
            SlotType = destinationType,
            ElementType = elementType,
            ChildType = childType,
            ParentType = parentType,
            Navigation = navigation,
            Root = root,
            Path = Describe(navigation.Member),
            Operations = operations,
            Nested = nested.Slots ?? new List<CollectionSlotDescriptor>(0),
            NestedNodes = nested.Nodes ?? new List<Expression>(0),
        };
        return true;
    }

    private static bool IsEntityBuilderMethod(MethodInfo method)
    {
        var declaring = method.DeclaringType;
        return declaring is { IsGenericType: true } && declaring.GetGenericTypeDefinition() == typeof(EntityBuilder<>);
    }

    /// <summary>
    /// Resolves the operand an approved terminal consumes. Two terminals are equivalent: the parameterless
    /// <c>EntityBuilderExtensions.ToList()</c>/<c>ToArray()</c> over a bare <c>AsEntityBuilder&lt;T&gt;()</c> chain, and the
    /// parameterless <c>QueryCommand&lt;TResult&gt;.ToList()</c>/<c>ToArray()</c> a chain yields after its <c>Select(...)</c>.
    /// </summary>
    private static bool TryGetTerminalOperand(MethodCallExpression terminal, out Expression operand)
    {
        operand = null!;

        var method = terminal.Method;

        if (method.DeclaringType == typeof(EntityBuilderExtensions)
            && method.Name is "ToList" or "ToArray"
            && terminal.Arguments.Count == 1)
        {
            operand = terminal.Arguments[0];
            return true;
        }

        if (terminal.Object is not null
            && method.DeclaringType is { IsGenericType: true } declaring
            && declaring.GetGenericTypeDefinition() == typeof(QueryCommand<>)
            && method.Name is "ToList" or "ToArray"
            && terminal.Arguments.Count == 0)
        {
            operand = terminal.Object;
            return true;
        }

        return false;
    }

    private static Type MemberType(MemberInfo member)
        => member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new NotSupportedException(
                $"The projection binds the collection slot to '{member.Name}', which is neither a property nor a field."),
        };

    private static LambdaExpression UnwrapLambda(Expression expression, string operation)
        => expression switch
        {
            UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression quoted } => quoted,
            LambdaExpression lambda => lambda,
            _ => throw new NotSupportedException(
                $"The collection projection slot applies {operation}(...) with an argument that is not a lambda " +
                "expression; only expression lambdas can be projected in memory."),
        };

    private static PropertyInfo? TryGetRootedNavigation(Expression expression, ParameterExpression root)
    {
        // A plain member chain is the common shape and resolves without allocating a visitor. The fast
        // path is used only when the whole chain is members ending at the root, which makes the visitor's
        // depth-first answer exactly the first property reached from the outside.
        if (expression is MemberExpression top)
        {
            Expression? tail = top;
            while (tail is MemberExpression memberTail)
                tail = memberTail.Expression;

            if (tail == root)
            {
                Expression? current = top;
                while (current is MemberExpression member)
                {
                    if (member.Member is PropertyInfo property)
                        return property;
                    current = member.Expression;
                }

                return null;
            }
        }

        var finder = new RootedNavigationFinder(root);
        finder.Visit(expression);
        return finder.Navigation;
    }

    private sealed class RootedNavigationFinder(ParameterExpression root) : ExpressionVisitor
    {
        internal PropertyInfo? Navigation { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Navigation is not null || node is null)
                return node;

            if (node is MemberExpression { Member: PropertyInfo property } member && IsRootedAt(member, root))
            {
                Navigation = property;
                return node;
            }

            return base.Visit(node);
        }
    }

    private static bool IsRootedAt(Expression expression, ParameterExpression root)
    {
        while (expression is MemberExpression member)
        {
            if (member.Expression == root)
                return true;

            expression = member.Expression!;
        }

        return false;
    }

    private static bool TryGetCollectionElement(Type type, out Type elementType)
    {
        elementType = null!;

        if (type == typeof(string))
            return false;

        if (type.IsArray)
        {
            if (type.GetArrayRank() != 1)
                return false;

            elementType = type.GetElementType()!;
            return true;
        }

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : FindEnumerableInterface(type);

        if (enumerable is null)
            return false;

        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }

    /// <summary>
    /// Returns the type's <see cref="IEnumerable{T}"/> interface, or <c>null</c>. A plain loop over
    /// <see cref="Type.GetInterfaces"/> keeps the projection hot path free of the LINQ enumerator and
    /// delegate allocations a <c>FirstOrDefault</c> would add for every projected member.
    /// </summary>
    private static Type? FindEnumerableInterface(Type type)
    {
        var interfaces = type.GetInterfaces();
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidate = interfaces[i];
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return candidate;
        }

        return null;
    }

    internal static string Describe(MemberInfo? member)
        => member is null ? "a constructor argument" : $"{member.DeclaringType?.Name}.{member.Name}";

    /// <summary>
    /// Refuses a navigation member whose declared type the eager-loading carrier cannot fill, before any SQL.
    /// The carrier assigns through a mutable <see cref="ICollection{T}"/> (it clears the current value and adds
    /// the loaded children), so a fixed-size array-typed navigation member would fail inside the loader after the
    /// query had already run. The array <em>destination</em> stays supported through the adapter's
    /// <c>ToArray()</c>; only the navigation declaration itself must be a mutable collection.
    /// </summary>
    /// <param name="navigation">The navigation property the slot reads.</param>
    /// <exception cref="NotSupportedException">The navigation is declared as an array.</exception>
    private static void EnsureAssignableNavigation(PropertyInfo navigation)
    {
        if (!navigation.PropertyType.IsArray)
            return;

        throw new NotSupportedException(
            $"The collection projection slot navigation '{Describe(navigation)}' is declared as " +
            $"'{navigation.PropertyType}': the eager-loading carrier fills a mutable collection and cannot assign " +
            "an array-typed navigation member. Declare the navigation as List<T> and project an array destination " +
            "with the adapter's ToArray().");
    }

    internal static NotSupportedException UnsupportedShape(string path, Type slotType)
        => new(
            $"The collection projection slot '{path}' is declared as '{slotType}': only " +
            $"{CollectionSlotShapes.SupportedList} are supported, and no other shape is substituted.");

    internal static NotSupportedException UnsupportedAdapterMethod(string method)
        => new(
            $"The collection projection slot applies {method}(...) inside its adapter chain, which is not supported: " +
            "the adapter may apply Where, OrderBy/OrderByDescending/ThenBy/ThenByDescending, Limit and Select, " +
            "terminated by a parameterless ToList() or ToArray().");
}
