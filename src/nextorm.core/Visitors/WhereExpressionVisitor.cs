using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates a predicate expression into a SQL <c>WHERE</c> clause.
/// </summary>
public class WhereExpressionVisitor(VisitorOptions options)
    : BaseExpressionVisitor(options)
{
    /// <summary>
    /// A where clause is a condition context: providers whose dialect cannot use a boolean value
    /// as a predicate (SQL Server) have to render the expression differently.
    /// </summary>
    protected override bool AsPredicate => true;

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!_paramMode && node.Type == typeof(bool) && (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual))
        {
            // Fast path: when neither operand is a null literal the null-aware "is"/"is not"
            // rewriting cannot apply, so both sides can be rendered straight into this builder.
            // That avoids two cloned visitors (and their intermediate strings) per comparison,
            // which is the shape of every join condition and most WHERE predicates.
            if (node.Left is not ConstantExpression { Value: null } && node.Right is not ConstantExpression { Value: null })
            {
                Visit(node.Left);
                _builder!.Append(node.NodeType == ExpressionType.Equal ? " = " : " != ");
                Visit(node.Right);
                return node;
            }

            using var leftVisitor = Clone();
            leftVisitor.Visit(node.Left);

            using var rightVisitor = Clone();
            rightVisitor.Visit(node.Right);

            var left = leftVisitor.ToString();
            var right = rightVisitor.ToString();

            var hasNull = left == "null" || right == "null";

            _builder!.Append(left).Append(hasNull
                ? node.NodeType switch
                {
                    ExpressionType.Equal => " is ",
                    _ => " is not "
                }
                : node.NodeType switch
                {
                    ExpressionType.Equal => " = ",
                    _ => " != "
                });
            _builder!.Append(right);

            return node;
        }

        return base.VisitBinary(node);
    }
    // public override BaseExpressionVisitor Clone()
    // {
    //     return new WhereExpressionVisitor(_entityType, _sqlClient, _from);
    // }
}