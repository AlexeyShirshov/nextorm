using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Parses a <c>Returning(projection)</c> selector of a multi-table mutation. The selector runs over the
/// positional join projection (<c>Projection&lt;T1, ...&gt;</c>), so a selected member may belong to any
/// joined entity; the parser resolves each reflected member against the metadata of every entity the
/// selector actually reads and returns the same column/select-list shape the single-table
/// <see cref="ReturningProjection"/> produces. Only the explicit projection form is accepted: the
/// whole-join-projection (identity) form cannot address the projection's item slots in the CTE read shape.
/// </summary>
internal static class JoinedReturningProjection
{
    /// <summary>
    /// Parses a joined returning selector: a member/constructor/member-init form returns the selected
    /// mapped columns.
    /// </summary>
    /// <param name="projection">The selector expression over the join projection.</param>
    /// <returns>The returned columns, the row-mapper select list and whether it is a single scalar column.</returns>
    /// <exception cref="QueryPreparationException">A referenced join item cannot be resolved at all: it is neither a registered entity nor a readable shape.</exception>
    internal static (IReadOnlyList<IPropertyMetadata> Columns, SelectExpression[] SelectList, bool OneColumn) Parse(LambdaExpression projection)
    {
        var projectionType = projection.Parameters[0].Type;

        // The whole-projection (identity) form cannot address the projection's item slots in the CTE read
        // shape: it would reach EnumerateReturningMembers' default and emit a bare RETURNING with no
        // columns. Reject it up front instead.
        if (TypeFacts.UnwrapConvert(projection.Body) is ParameterExpression || projection.ReturnType == projectionType)
            throw new NotSupportedException(
                "The whole-projection (identity) RETURNING form is not supported for a multi-table mutation; provide an explicit projection, for example p => new { p.Item1.Id, p.Item2.Name }.");

        var entityTypes = projectionType.GetGenericArguments();
        var referencedSlots = ReferencedSlots(projection);
        var allProperties = new List<IPropertyMetadata>();

        // Resolve only the item slots the selector actually reads. A joined read CTE (or any derived-table
        // shape) carries no entity metadata, and an unreferenced slot must not force one; requiring it for
        // every slot rejected a projection that referenced only the real-entity item.
        for (var i = 0; i < entityTypes.Length; i++)
        {
            if (!referencedSlots.Contains(i))
                continue;

            if (DataContextCache.Metadata.TryGetValue(entityTypes[i], out var metadata))
            {
                allProperties.AddRange(metadata.Properties);
                continue;
            }

            // A referenced slot with no registered entity metadata is a read CTE / projection shape: its
            // CLR surface supplies the readable source columns. Fail closed when even that yields nothing,
            // so a genuinely unresolvable referenced member still surfaces as a preparation error.
            var shapeColumns = ShapeColumns(entityTypes[i]);
            if (shapeColumns.Count == 0)
                throw new QueryPreparationException(
                    $"No metadata is registered for {entityTypes[i].Name} and it exposes no readable columns, so the multi-table RETURNING projection {projectionType.Name} cannot resolve its referenced members. Register every joined entity with From<T>/Join<T> on the same data context.");

            allProperties.AddRange(shapeColumns);
        }

        IPropertyMetadata? Find(PropertyInfo property)
        {
            for (var i = 0; i < allProperties.Count; i++)
            {
                if (allProperties[i].PropertyInfo == property)
                    return allProperties[i];
            }

            return null;
        }

        return ReturningProjection.Parse(projection, allProperties, Find);
    }

    /// <summary>
    /// Returns the 0-based item slots the selector reads, by finding every <c>p.ItemN</c> access in the
    /// expression tree. The identity form reads every slot.
    /// </summary>
    private static HashSet<int> ReferencedSlots(LambdaExpression projection)
    {
        var parameter = projection.Parameters[0];
        var slots = new HashSet<int>();

        if (TypeFacts.UnwrapConvert(projection.Body) is ParameterExpression)
        {
            var count = parameter.Type.GetGenericArguments().Length;
            for (var i = 0; i < count; i++)
                slots.Add(i);
            return slots;
        }

        Walk(projection.Body);
        return slots;

        void Walk(Expression? node)
        {
            switch (node)
            {
                case null:
                    return;
                case MemberExpression member:
                    if (IsItemAccess(member, parameter, out var index))
                        slots.Add(index);
                    Walk(member.Expression);
                    return;
                case IndexExpression indexExpression:
                    // An indexed member access (array element or indexer) reads its object; record the slot
                    // so a referenced indexed member is never silently dropped from the resolved metadata.
                    Walk(indexExpression.Object);
                    foreach (var argument in indexExpression.Arguments)
                        Walk(argument);
                    return;
                case UnaryExpression unary:
                    Walk(unary.Operand);
                    return;
                case BinaryExpression binary:
                    Walk(binary.Left);
                    Walk(binary.Right);
                    Walk(binary.Conversion);
                    return;
                case ConditionalExpression conditional:
                    Walk(conditional.Test);
                    Walk(conditional.IfTrue);
                    Walk(conditional.IfFalse);
                    return;
                case MethodCallExpression call:
                    Walk(call.Object);
                    foreach (var argument in call.Arguments)
                        Walk(argument);
                    return;
                case NewExpression @new:
                    foreach (var argument in @new.Arguments)
                        Walk(argument);
                    return;
                case MemberInitExpression init:
                    Walk(init.NewExpression);
                    foreach (var binding in init.Bindings)
                    {
                        if (binding is MemberAssignment assignment)
                            Walk(assignment.Expression);
                    }
                    return;
                case NewArrayExpression array:
                    foreach (var element in array.Expressions)
                        Walk(element);
                    return;
                case InvocationExpression invocation:
                    Walk(invocation.Expression);
                    foreach (var argument in invocation.Arguments)
                        Walk(argument);
                    return;
                case LambdaExpression lambda:
                    Walk(lambda.Body);
                    return;
                default:
                    return;
            }
        }
    }

    /// <summary>True when <paramref name="member"/> is a direct <c>ItemN</c> access on the selector parameter; yields the 0-based slot.</summary>
    private static bool IsItemAccess(MemberExpression member, ParameterExpression parameter, out int index)
    {
        index = -1;
        if (member.Member is not PropertyInfo property)
            return false;

        Expression? target = member.Expression;
        while (target is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            target = unary.Operand;

        if (!ReferenceEquals(target, parameter))
            return false;

        var name = property.Name;
        if (name.Length <= 4 || !name.StartsWith("Item", StringComparison.Ordinal))
            return false;

        if (!int.TryParse(name.AsSpan(4), out var ordinal) || ordinal < 1)
            return false;

        index = ordinal - 1;
        return true;
    }

    /// <summary>
    /// Treats the public instance properties of a non-entity slot type as its readable source columns
    /// (the projection/CTE read shape), preserving the ordered surface the selector references.
    /// </summary>
    private static IReadOnlyList<IPropertyMetadata> ShapeColumns(Type shape)
    {
        var properties = shape.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columns = new List<IPropertyMetadata>(properties.Length);
        foreach (var property in properties)
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
                continue;

            columns.Add(new PropertyMetadata
            {
                PropertyInfo = property,
                ColumnName = property.Name,
                IsColumnNameAuto = false,
            });
        }

        return columns;
    }
}
