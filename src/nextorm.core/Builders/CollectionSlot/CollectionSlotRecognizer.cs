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
    /// <param name="relationshipKind">
    /// The declared relationship kind of a member navigation, or <see langword="null"/> when the member is not a
    /// navigation (a scalar collection column is not, and neither is an undeclared property).
    /// </param>
    /// <returns>The recognition, or <see langword="null"/> for an ordinary scalar projection.</returns>
    internal static CollectionSlotRecognition? Recognize<TEntity, TResult>(
        Expression<Func<TEntity, TResult>> projection,
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind)
    {
        var root = projection.Parameters[0];
        var slots = new List<CollectionSlotDescriptor>();
        var nodes = new List<Expression>();

        VisitProjection(projection.Body, root, typeof(TEntity), relationshipKind, slots, nodes);

        // A reference-only projection is an ordinary entity projection: the implicit-navigation path owns it and
        // must not change. The carrier and its cardinality guard are projection-scoped to projections that
        // actually ask for a per-parent collection.
        return slots.Exists(static slot => !slot.IsReference) ? new CollectionSlotRecognition { Slots = slots, Nodes = nodes } : null;
    }

    private static void VisitProjection(
        Expression body,
        ParameterExpression root,
        Type parentType,
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind,
        List<CollectionSlotDescriptor> slots,
        List<Expression> nodes)
    {
        switch (body)
        {
            case MemberInitExpression init:
                VisitProjection(init.NewExpression, root, parentType, relationshipKind, slots, nodes);
                foreach (var binding in init.Bindings)
                {
                    if (binding is not MemberAssignment assignment)
                        continue;

                    AddCandidate(assignment.Expression, assignment.Member, -1, MemberType(assignment.Member), root, parentType, relationshipKind, slots, nodes);
                }

                break;

            case NewExpression creation:
                for (var i = 0; i < creation.Arguments.Count; i++)
                {
                    var parameterType = creation.Constructor?.GetParameters()[i].ParameterType ?? creation.Arguments[i].Type;
                    AddCandidate(creation.Arguments[i], null, i, parameterType, root, parentType, relationshipKind, slots, nodes);
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
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind,
        List<CollectionSlotDescriptor> slots,
        List<Expression> nodes)
    {
        if (TryRecognize(expression, root, destinationType, parentType, relationshipKind, out var slot))
        {
            slot.Member = member;
            slot.ArgumentIndex = argumentIndex;
            slots.Add(slot);
            nodes.Add(expression);
            return;
        }

        if (expression is not (MemberInitExpression or NewExpression))
        {
            // Only an expression that carries a *declared navigation* can be a malformed slot; anything else
            // (a scalar array column, a collection-valued SQL function) is an ordinary projected value.
            if (TryGetRootedNavigation(expression, root) is { } navigation && relationshipKind(parentType, navigation) is not null && TryGetCollectionElement(expression.Type, out _))
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
        var nestedSlots = new List<CollectionSlotDescriptor>();
        VisitProjection(expression, root, parentType, relationshipKind, nestedSlots, []);
        if (nestedSlots.Count > 0)
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
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        return TryRecognizeAdapter(expression, root, destinationType, parentType, relationshipKind, out slot)
            || TryRecognizeDirect(expression, root, destinationType, parentType, relationshipKind, out slot)
            || TryRecognizeReference(expression, root, parentType, relationshipKind, out slot);
    }

    private static bool TryRecognizeDirect(
        Expression expression,
        ParameterExpression root,
        Type destinationType,
        Type parentType,
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        if (expression is not MemberExpression member || !IsRootedAt(member, root))
            return false;

        if (member.Member is not PropertyInfo property || relationshipKind(parentType, property) is not (RelationshipKind.OneToMany or RelationshipKind.ManyToMany))
            return false;

        if (!TryGetCollectionElement(member.Type, out var elementType))
            return false;

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
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind,
        out CollectionSlotDescriptor slot)
    {
        slot = null!;

        if (expression is not MemberExpression member || member.Expression != root)
            return false;

        if (member.Member is not PropertyInfo property || relationshipKind(parentType, property) is not (RelationshipKind.ManyToOne or RelationshipKind.OneToOne))
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
        Func<Type, PropertyInfo, RelationshipKind?> relationshipKind,
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
            || relationshipKind(parentType, navigationProperty) is not (RelationshipKind.OneToMany or RelationshipKind.ManyToMany)
            || !IsRootedAt(navigation, root)
            || !TryGetCollectionElement(navigation.Type, out var childType))
            return false;

        var elementType = operations.OfType<CollectionSlotSelect>().LastOrDefault()?.ResultType ?? childType;

        if (!CollectionSlotShapes.IsSupported(destinationType, elementType))
            throw UnsupportedShape(Describe(navigation.Member), destinationType);

        // A slot declared inside the adapter's Select(...) belongs to the child level: its navigation is rooted at
        // that lambda's parameter, and the binder declares it on the child builder. The recursion is structural, so
        // any depth is handled by the same code path.
        var nested = new List<CollectionSlotDescriptor>();
        var nestedNodes = new List<Expression>();
        if (operations.OfType<CollectionSlotSelect>().LastOrDefault() is { } select)
            VisitProjection(select.Projection.Body, select.Projection.Parameters[0], childType, relationshipKind, nested, nestedNodes);

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
            Nested = nested,
            NestedNodes = nestedNodes,
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
            : type.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable is null)
            return false;

        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }

    internal static string Describe(MemberInfo? member)
        => member is null ? "a constructor argument" : $"{member.DeclaringType?.Name}.{member.Name}";

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
