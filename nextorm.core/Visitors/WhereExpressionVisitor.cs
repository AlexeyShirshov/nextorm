using System.Linq.Expressions;

namespace nextorm.core;

public class WhereExpressionVisitor(Type entityType, DbContext dataProvider, ISourceProvider tableSource, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, bool paramMode)
    : BaseExpressionVisitor(entityType, dataProvider, tableSource, dim, aliasProvider, paramProvider, queryProvider, false, paramMode)
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!_paramMode && node.Type == typeof(bool) && (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual))
        {
            using var leftVisitor = Clone();
            leftVisitor.Visit(node.Left);
            Params.AddRange(leftVisitor.Params);

            using var rightVisitor = Clone();
            rightVisitor.Visit(node.Right);
            Params.AddRange(rightVisitor.Params);

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