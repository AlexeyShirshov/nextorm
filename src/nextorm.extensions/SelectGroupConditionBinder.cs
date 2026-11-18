using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

/// <summary>
/// Rebinds a <c>SelectGroupHaving</c> condition typed on the projection result onto the source entity:
/// every member of the result is replaced by the projection expression that produces it, so the
/// prepared <c>HAVING</c> is written over the source exactly like an equivalent entity-typed predicate
/// and the generated SQL (and the plan key) is unchanged.
/// </summary>
internal static class SelectGroupConditionBinder
{
    /// <summary>
    /// Rebinds <paramref name="condition"/> (typed on the projection result) to the entity parameter of
    /// <paramref name="projection"/>.
    /// </summary>
    /// <param name="projection">The projection whose shape names the result members.</param>
    /// <param name="condition">The condition typed on the projection result.</param>
    /// <returns>A one-parameter lambda over the projection's source entity.</returns>
    /// <exception cref="QueryPreparationException">
    /// The condition references the result as a whole, or a member the projection does not bind.
    /// </exception>
    internal static LambdaExpression Bind<TEntity>(LambdaExpression projection, LambdaExpression condition)
    {
        var body = AggregateClassification.UnwrapConvert(projection.Body);
        var members = BuildMemberMap(body);
        var rebound = new MemberSubstitution(condition.Parameters[0], body, members).Visit(condition.Body)!;
        // The entity type is the component's compile-time TEntity: closing the delegate type here keeps the
        // run-time generic Expression<Func<TEntity, bool>> out of a trimmed/native image (the non-generic
        // Expression.Lambda overload infers and closes it at run time, which NativeAOT cannot compile).
        return Expression.Lambda<Func<TEntity, bool>>(rebound, projection.Parameters[0]);
    }

    /// <summary>
    /// Builds the member-name map of the projection shape: an anonymous-type constructor maps every
    /// <c>new { A = expr, … }</c> member to its argument, a member initializer maps every bound member
    /// to its value. A scalar projection returns <see langword="null"/>: the whole body stands for the
    /// condition parameter.
    /// </summary>
    private static IReadOnlyDictionary<string, Expression>? BuildMemberMap(Expression projectionBody)
    {
        if (projectionBody is NewExpression { Members: not null } ctor)
        {
            // A single-column projection type (a wrapped scalar) is the parameter itself, not a member set.
            if (AggregateClassification.IsSingleColumnProjection(ctor.Type))
                return null;

            var map = new Dictionary<string, Expression>(ctor.Members.Count, StringComparer.Ordinal);
            for (var i = 0; i < ctor.Members.Count; i++)
                map[ctor.Members[i].Name] = ctor.Arguments[i];

            return map;
        }

        if (projectionBody is MemberInitExpression init)
        {
            var map = new Dictionary<string, Expression>(init.Bindings.Count, StringComparer.Ordinal);
            for (var i = 0; i < init.Bindings.Count; i++)
            {
                if (init.Bindings[i] is not MemberAssignment assignment)
                {
                    throw new QueryPreparationException(
                        "SelectGroupHaving requires a projection with scalar member-init bindings; a list/member binding is not supported.");
                }

                map[assignment.Member.Name] = assignment.Expression;
            }

            return map;
        }

        return null;
    }

    /// <summary>Replaces every access to the condition parameter with its projection expression.</summary>
    private sealed class MemberSubstitution(
        ParameterExpression parameter,
        Expression projectionBody,
        IReadOnlyDictionary<string, Expression>? members) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsParameter(node.Expression))
            {
                if (members is null)
                {
                    var receiver = projectionBody;
                    return node.Expression!.Type == receiver.Type
                        ? Expression.MakeMemberAccess(receiver, node.Member)
                        : Expression.MakeMemberAccess(Expression.Convert(receiver, node.Expression.Type), node.Member);
                }

                if (!members.TryGetValue(node.Member.Name, out var bound))
                {
                    throw new QueryPreparationException(
                        $"SelectGroupHaving condition references '{node.Member.Name}', which is not part of the projection.");
                }

                return bound;
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!ReferenceEquals(node, parameter))
                return node;

            if (members is null)
                return projectionBody;

            throw new QueryPreparationException(
                "SelectGroupHaving condition must reference a member of the projection, not the projection result as a whole.");
        }

        private bool IsParameter(Expression? expression)
        {
            var stripped = expression;
            while (stripped is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
                stripped = unary.Operand;

            return ReferenceEquals(stripped, parameter);
        }
    }
}
