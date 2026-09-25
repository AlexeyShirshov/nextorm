using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Parses a <c>Returning(projection)</c> selector into the mapped columns a mutation returns through
/// its <c>RETURNING</c>/<c>OUTPUT</c> form and the projection shape used to materialize them. Shared by
/// the insert and delete returning builders so the two keep the same supported shapes.
/// </summary>
internal static class ReturningProjection
{
    /// <summary>
    /// Parses a returning selector. Only member references are supported: the identity, a single member,
    /// an anonymous type, a positional constructor or a member-init.
    /// </summary>
    /// <param name="projection">The selector expression.</param>
    /// <param name="allProperties">Every mapped property of the entity, used by the identity form.</param>
    /// <param name="find">Resolves a reflected property to its mapping.</param>
    /// <returns>The returned columns, the select list of the row mapper, and whether it is a single scalar column.</returns>
    internal static (IReadOnlyList<IPropertyMetadata> Columns, SelectExpression[] SelectList, bool OneColumn) Parse(
        LambdaExpression projection,
        IReadOnlyList<IPropertyMetadata> allProperties,
        Func<PropertyInfo, IPropertyMetadata?> find)
    {
        var body = UnwrapConvert(projection.Body);

        if (body is ParameterExpression)
        {
            var entityTargets = new PropertyInfo[allProperties.Count];
            for (var i = 0; i < entityTargets.Length; i++)
                entityTargets[i] = allProperties[i].PropertyInfo;

            return (allProperties, BuildSelectList(allProperties, entityTargets), false);
        }

        if (body is MemberExpression { Member: PropertyInfo singleMember })
        {
            var property = Resolve(singleMember, find);
            return ([property], BuildSelectList([property], [singleMember]), true);
        }

        var members = new List<IPropertyMetadata>();
        var targets = new List<PropertyInfo>();
        if (body is NewExpression { Arguments.Count: > 0 } newExpression)
        {
            foreach (var argument in newExpression.Arguments)
            {
                if (argument is not MemberExpression { Member: PropertyInfo argumentMember })
                    throw new NotSupportedException("A Returning projection may only reference mapped properties; use ReturningIdentity or ReturningKey for a generated key.");

                members.Add(Resolve(argumentMember, find));
                targets.Add(argumentMember);
            }
        }
        else if (body is MemberInitExpression { Bindings.Count: > 0 } memberInit)
        {
            foreach (var binding in memberInit.Bindings)
            {
                if (binding is not MemberAssignment { Member: PropertyInfo targetMember, Expression: MemberExpression { Member: PropertyInfo bindingMember } })
                    throw new NotSupportedException("A Returning projection may only reference mapped properties; use ReturningIdentity or ReturningKey for a generated key.");

                members.Add(Resolve(bindingMember, find));
                targets.Add(targetMember);
            }
        }
        else
        {
            throw new NotSupportedException("A Returning projection must be the identity, a mapped property, an anonymous type or a member-init.");
        }

        return (members, BuildSelectList(members, targets), false);
    }

    /// <summary>Builds the row-mapper select list for <paramref name="columns"/> projected onto <paramref name="targets"/>.</summary>
    /// <param name="columns">The returned mapped columns, in result-set order.</param>
    /// <param name="targets">The CLR members each column is projected onto.</param>
    /// <returns>The select list.</returns>
    internal static SelectExpression[] BuildSelectList(IReadOnlyList<IPropertyMetadata> columns, IReadOnlyList<PropertyInfo> targets)
    {
        var selectList = new SelectExpression[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var target = targets[i];
            selectList[i] = new SelectExpression(column.PropertyInfo.PropertyType)
            {
                Index = i,
                PropertyName = target.Name,
                PropertyInfo = target,
                DurationUnit = column.DurationUnit,
                DurationPrecision = column.DurationPrecision,
                ProviderType = column.Converter?.ProviderType,
                Converter = column.Converter,
            };
        }

        return selectList;
    }

    private static IPropertyMetadata Resolve(PropertyInfo property, Func<PropertyInfo, IPropertyMetadata?> find)
        => find(property)
           ?? throw new BuildSqlCommandException($"Property {property.Name} is not mapped.");

    private static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? UnwrapConvert(unary.Operand)
            : expression;
}
