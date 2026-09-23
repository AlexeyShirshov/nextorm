using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

public sealed partial class MergeBuilder<TEntity>
{
    private MergeBuilder<TEntity> AddBranch(MergeMatchKind match, MergeActionKind action, LambdaExpression? columns, LambdaExpression? condition)
    {
        if (_columns.Count == 0 && _source is null)
            throw new InvalidOperationException("Call Using(...) before adding a merge branch.");

        IReadOnlyList<IPropertyMetadata> selected;
        if (columns is null)
        {
            var list = new List<IPropertyMetadata>();
            foreach (var property in _metadata.Properties)
            {
                if (property.IsIdentity || property.IsComputed)
                    continue;

                if (action == MergeActionKind.Update && property.IsKey)
                    continue;

                list.Add(property);
            }

            selected = list;
        }
        else
        {
            selected = ParseColumns(columns);
            ValidateExplicitColumns(selected, action);
        }

        if (action is MergeActionKind.Update or MergeActionKind.Insert && selected.Count == 0)
            throw new NotSupportedException($"Entity {typeof(TEntity)} has no column to {action.ToString().ToLowerInvariant()}.");

        _branches.Add(new MergeBranch(match, action, selected, condition));
        return this;
    }

    private IReadOnlyList<IPropertyMetadata> ParseColumns(LambdaExpression selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        var members = new List<PropertyInfo>();
        switch (body)
        {
            case NewExpression @new:
                foreach (var argument in @new.Arguments)
                    AddSelectedMember(argument, members);
                break;
            case MemberInitExpression init:
                foreach (var binding in init.Bindings)
                {
                    if (binding is MemberAssignment assignment)
                        AddSelectedMember(assignment.Expression, members);
                }
                break;
            default:
                AddSelectedMember(body, members);
                break;
        }

        if (members.Count == 0)
            throw new ArgumentException("The selector does not name any mapped column.", nameof(selector));

        var result = new List<IPropertyMetadata>(members.Count);
        foreach (var member in members)
        {
            var property = FindWritable(member)
                ?? throw new BuildSqlCommandException($"Property {member.Name} of {typeof(TEntity).Name} is not a mapped, writable column.");
            result.Add(property);
        }

        return result;
    }

    private static void AddSelectedMember(Expression expression, List<PropertyInfo> members)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;

        if (expression is MemberExpression { Member: PropertyInfo property })
            members.Add(property);
        else
            throw new BuildSqlCommandException("A merge column selector may only name mapped columns.");
    }

    private IPropertyMetadata? FindWritable(PropertyInfo property)
    {
        foreach (var candidate in _metadata.Properties)
        {
            if (candidate.PropertyInfo == property)
            {
                if (candidate.IsComputed)
                    throw new NotSupportedException($"Property {property.Name} of {typeof(TEntity).Name} is computed and cannot be written.");

                return candidate;
            }
        }

        return null;
    }

    // An explicit selector bypasses the automatic key/identity filtering of a selector-less branch, so it
    // is validated here: a database-generated column can never be written, and a matched branch must not
    // rewrite the column used to match.
    private static void ValidateExplicitColumns(IReadOnlyList<IPropertyMetadata> columns, MergeActionKind action)
    {
        foreach (var column in columns)
        {
            if (column.IsIdentity)
                throw new NotSupportedException($"Property {column.PropertyInfo.Name} is database-generated and cannot be written by a merge branch.");

            if (action == MergeActionKind.Update && column.IsKey)
                throw new NotSupportedException($"Property {column.PropertyInfo.Name} is the merge match key and cannot be written by a matched branch.");
        }
    }

    private IPropertyMetadata? FindMapped(PropertyInfo property)
    {
        foreach (var candidate in _metadata.Properties)
        {
            if (candidate.PropertyInfo == property)
                return candidate;
        }

        return null;
    }
}
