using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>Extracts evaluable equality values for a declared shard key from a predicate.</summary>
public static class ShardKeyPredicateExtractor
{
    /// <summary>Attempts to extract every shard-key value from equality terms joined with AND.</summary>
    /// <typeparam name="T">The entity type used by the predicate.</typeparam>
    /// <param name="predicate">The predicate to inspect.</param>
    /// <param name="shardKey">The ordered shard-key properties.</param>
    /// <param name="values">Receives the values in shard-key order on success, or an empty array on failure.</param>
    /// <returns><see langword="true"/> when every shard-key property has an evaluable equality and the predicate contains no OR.</returns>
    public static bool TryGetValues<T>(
        Expression<Func<T, bool>> predicate,
        IReadOnlyList<PropertyInfo> shardKey,
        out object?[] values)
    {
        values = [];
        if (predicate is null || shardKey is null || shardKey.Count == 0 || ContainsOr(predicate.Body))
            return false;

        var extracted = new object?[shardKey.Count];
        var found = new bool[shardKey.Count];
        if (!CollectAndEqualities(predicate.Body, predicate.Parameters[0], shardKey, extracted, found))
            return false;

        for (var index = 0; index < found.Length; index++)
        {
            if (!found[index])
                return false;
        }

        values = extracted;
        return true;
    }

    private static bool ContainsOr(Expression expression)
    {
        if (expression is BinaryExpression binary)
        {
            if (binary.NodeType is ExpressionType.Or or ExpressionType.OrElse)
                return true;
            return ContainsOr(binary.Left) || ContainsOr(binary.Right);
        }

        return expression is UnaryExpression unary && ContainsOr(unary.Operand);
    }

    private static bool CollectAndEqualities(
        Expression expression,
        ParameterExpression parameter,
        IReadOnlyList<PropertyInfo> shardKey,
        object?[] values,
        bool[] found)
    {
        expression = StripConvert(expression);
        if (expression is BinaryExpression andAlso
            && andAlso.NodeType is ExpressionType.AndAlso or ExpressionType.And)
        {
            return CollectAndEqualities(andAlso.Left, parameter, shardKey, values, found)
                && CollectAndEqualities(andAlso.Right, parameter, shardKey, values, found);
        }

        if (expression is not BinaryExpression equality || equality.NodeType != ExpressionType.Equal)
            return true;

        if (TryGetEntityProperty(equality.Left, parameter, out var leftProperty)
            && TryFindKeyIndex(shardKey, leftProperty, out var leftIndex))
        {
            return TrySetValue(equality.Right, leftIndex, shardKey, values, found);
        }

        if (TryGetEntityProperty(equality.Right, parameter, out var rightProperty)
            && TryFindKeyIndex(shardKey, rightProperty, out var rightIndex))
        {
            return TrySetValue(equality.Left, rightIndex, shardKey, values, found);
        }

        return true;
    }

    private static bool TrySetValue(
        Expression expression,
        int index,
        IReadOnlyList<PropertyInfo> shardKey,
        object?[] values,
        bool[] found)
    {
        if (!TryEvaluate(expression, out var value))
            return false;

        if (found[index] && !Equals(values[index], value))
            return false;

        found[index] = true;
        values[index] = value;
        return true;
    }

    private static bool TryGetEntityProperty(Expression expression, ParameterExpression parameter, out PropertyInfo property)
    {
        expression = StripConvert(expression);
        if (expression is MemberExpression { Member: PropertyInfo candidate, Expression: ParameterExpression root }
            && ReferenceEquals(root, parameter))
        {
            property = candidate;
            return true;
        }

        property = null!;
        return false;
    }

    private static bool TryFindKeyIndex(IReadOnlyList<PropertyInfo> shardKey, PropertyInfo property, out int index)
    {
        for (var candidate = 0; candidate < shardKey.Count; candidate++)
        {
            if (shardKey[candidate] == property)
            {
                index = candidate;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private static bool TryEvaluate(Expression expression, out object? value)
    {
        expression = StripConvert(expression);
        try
        {
            switch (expression)
            {
                case ConstantExpression constant:
                    value = constant.Value;
                    return true;
                case MemberExpression member when member.Member is FieldInfo field:
                    if (member.Expression is null)
                    {
                        value = field.GetValue(null);
                        return true;
                    }
                    if (TryEvaluate(member.Expression, out var fieldOwner))
                    {
                        value = field.GetValue(fieldOwner);
                        return true;
                    }
                    break;
                case MemberExpression member when member.Member is PropertyInfo property
                    && property.GetIndexParameters().Length == 0:
                    if (member.Expression is null)
                    {
                        value = property.GetValue(null);
                        return true;
                    }
                    if (TryEvaluate(member.Expression, out var propertyOwner))
                    {
                        value = property.GetValue(propertyOwner);
                        return true;
                    }
                    break;
            }
        }
        catch (Exception)
        {
            // Unsupported or throwing closure/member access is not routable at build time.
        }

        value = null;
        return false;
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.Quote } unary)
            expression = unary.Operand;
        return expression;
    }
}
