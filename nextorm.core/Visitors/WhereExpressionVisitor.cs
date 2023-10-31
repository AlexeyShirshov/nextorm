using System.Linq.Expressions;

namespace nextorm.core;

public class WhereExpressionVisitor : BaseExpressionVisitor
{
    public WhereExpressionVisitor(Type entityType, SqlDataProvider dataProvider, FromExpression from) : base(entityType, dataProvider, from)
    {
    }
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.Equal)
        {
            using var leftVisitor = Clone();
            leftVisitor.Visit(node.Left);
            Params.AddRange(leftVisitor.Params);

            using var rightVisitor = Clone();
            rightVisitor.Visit(node.Right);
            Params.AddRange(rightVisitor.Params);

            var left = leftVisitor.ToString();
            var right = rightVisitor.ToString();

            var hasNull  = left == "null" || right == "null";

            _builder.Append(left).Append(hasNull 
                ? " is "
                : " = ");
            _builder.Append(right);
            
            return node;
        }

        return base.VisitBinary(node);
    }
    // public override BaseExpressionVisitor Clone()
    // {
    //     return new WhereExpressionVisitor(_entityType, _sqlClient, _from);
    // }
}