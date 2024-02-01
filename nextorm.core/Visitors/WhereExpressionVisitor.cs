using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class WhereExpressionVisitor(Type entityType, DbContext dataProvider, IColumnsProvider tableSource, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, bool paramMode, List<Param> @params, ILogger logger)
    : BaseExpressionVisitor(entityType, dataProvider, tableSource, dim, aliasProvider, paramProvider, queryProvider, false, paramMode, @params, logger)
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!_paramMode && node.Type == typeof(bool) && (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual))
        {
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