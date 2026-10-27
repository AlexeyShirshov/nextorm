using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Parses a relationship key selector into its ordered mapped-property components. Every producer
/// (the legacy single-column overloads, the composite overloads and the attribute scan) resolves its
/// selector through this one parser so the accepted syntax and the rejection diagnostics are identical.
/// </summary>
/// <remarks>
/// The accepted forms are a direct scalar property access (<c>x =&gt; x.ParentId</c>) and a flat
/// anonymous object of direct scalar property accesses (<c>x =&gt; new { x.TenantId, x.ParentId }</c>).
/// The returned order is exactly the selector order; anonymous-member aliases are ignored. Boxing and
/// nullable conversions around a component are unwrapped. Fields, nested paths, computations, method
/// calls, indexers, array and collection initialisers, tuple literals and constructors, DTO and nested
/// object construction, empty anonymous objects and duplicate members are rejected.
/// </remarks>
internal static class RelationshipKeySelectorParser
{
    /// <summary>
    /// Parses <paramref name="selector"/> into the ordered components it selects.
    /// </summary>
    /// <param name="selector">The key selector, for example <c>x =&gt; x.ParentId</c> or <c>x =&gt; new { x.TenantId, x.ParentId }</c>.</param>
    /// <param name="entityType">The entity type the selector reads from, used in diagnostics.</param>
    /// <param name="selectorName">The selector's role (for example <c>foreignKey</c>), used in diagnostics.</param>
    /// <param name="relationshipName">The relationship being declared (declaring type and navigation), used in diagnostics.</param>
    /// <returns>The selected properties in selector order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> or its body is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The selector is not a scalar property access or a flat anonymous object of scalar property accesses.</exception>
    internal static IReadOnlyList<PropertyInfo> Parse(
        LambdaExpression? selector,
        Type entityType,
        string selectorName,
        string relationshipName)
    {
        ArgumentNullException.ThrowIfNull(selector, selectorName);
        ArgumentNullException.ThrowIfNull(entityType);

        var body = selector.Body
            ?? throw new ArgumentNullException(selectorName, "The key selector has no body.");
        body = UnwrapConversions(body);

        var subject = $"'{selectorName}' selector of the relationship '{relationshipName}' on entity '{entityType.Name}'";

        if (body is NewExpression creation)
            return ParseCreation(creation, subject);

        if (body is MemberInitExpression)
            throw Unsupported(subject, body, "an object initializer");

        if (body is MemberExpression { Member: PropertyInfo property } member && member.Expression is ParameterExpression)
            return new[] { property };

        throw Unsupported(subject, body, Describe(body));
    }

    private static IReadOnlyList<PropertyInfo> ParseCreation(NewExpression creation, string subject)
    {
        if (IsTupleType(creation.Type))
            throw Unsupported(subject, creation, "a tuple literal; select the component properties directly");

        if (!IsAnonymousType(creation.Type))
            throw Unsupported(subject, creation, $"an object construction ('new {creation.Type.Name}')");

        if (creation.Arguments.Count == 0)
            throw new NotSupportedException(
                $"The {subject} must select at least one property, but it is an empty anonymous object.");

        var components = new PropertyInfo[creation.Arguments.Count];
        var seen = new HashSet<PropertyInfo>(creation.Arguments.Count);
        for (var i = 0; i < creation.Arguments.Count; i++)
        {
            var argument = UnwrapConversions(creation.Arguments[i]);
            if (argument is not MemberExpression { Member: PropertyInfo property } member || member.Expression is not ParameterExpression)
            {
                throw new NotSupportedException(
                    $"The {subject} is not supported: component {i + 1} of {creation.Arguments.Count} ('{argument}') is {Describe(argument)}.");
            }

            if (!seen.Add(property))
            {
                throw new NotSupportedException(
                    $"The {subject} is not supported: component {i + 1} of {creation.Arguments.Count} repeats the property '{property.Name}'.");
            }

            components[i] = property;
        }

        return components;
    }

    private static string Describe(Expression expression)
        => expression switch
        {
            MemberExpression { Member: FieldInfo } => "a field access; only mapped properties are supported",
            MemberExpression { Expression: MemberExpression } => "a nested member path; only a direct property of the entity is supported",
            MemberExpression { Expression: not ParameterExpression } => "a member access on a computed value",
            MethodCallExpression { Method.Name: "get_Item" } => "an index access",
            MethodCallExpression => "a method call",
            BinaryExpression { NodeType: ExpressionType.ArrayIndex } => "an index access",
            IndexExpression => "an index access",
            BinaryExpression => "a computation",
            UnaryExpression => "a computation",
            NewArrayExpression => "an array initializer",
            ListInitExpression => "a collection initializer",
            ConditionalExpression => "a conditional expression",
            ConstantExpression => "a constant",
            ParameterExpression => "the entity itself rather than a property",
            NewExpression { Type: var type } when IsTupleType(type) => "a tuple literal; select the component properties directly",
            NewExpression { Type: var type } when IsAnonymousType(type) => "a nested anonymous object",
            NewExpression { Type: var type } => $"an object construction ('new {type.Name}')",
            MemberInitExpression => "an object initializer",
            _ => $"an unsupported expression ('{expression}')",
        };

    private static NotSupportedException Unsupported(string subject, Expression body, string reason)
        => new($"The {subject} must be a scalar property access or a flat anonymous object of scalar property accesses, but the selector '{body}' is {reason}.");

    private static Expression UnwrapConversions(Expression expression)
    {
        var current = expression;
        while (current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            current = conversion.Operand;

        return current;
    }

    private static bool IsAnonymousType(Type type)
        => type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
           && type.Name.Contains("AnonymousType", StringComparison.Ordinal)
           && (type.Name.StartsWith("<>", StringComparison.Ordinal) || type.Name.StartsWith("VB$", StringComparison.Ordinal));

    private static bool IsTupleType(Type type)
        => type.IsGenericType
           && (type.GetGenericTypeDefinition() == typeof(ValueTuple<>) || type.GetGenericTypeDefinition() == typeof(Tuple<>)
               || type.FullName is { } name && (name.StartsWith("System.ValueTuple`", StringComparison.Ordinal) || name.StartsWith("System.Tuple`", StringComparison.Ordinal)));
}
