using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace nextorm.core;
public class StringExpressionVisitor : ExpressionVisitor
{
    private readonly Type _entityType;
    private readonly SqlClient _sqlClient;

    public StringExpressionVisitor(Type entityType, SqlClient sqlClient)
    {
        _entityType = entityType;
        _sqlClient = sqlClient;
    }

    private readonly StringBuilder _builder = new();
    protected override Expression VisitConstant(ConstantExpression node)
    {
        _builder.Append(node.Value?.ToString());
        return base.VisitConstant(node);
    }
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression?.Type == _entityType)
        {
            var col = node.Member.GetCustomAttribute<ColumnAttribute>();
            if (col is not null)
                _builder.Append(col.Name);
            else
                _builder.Append(_sqlClient.GetColumnName(node.Member));
        }

        return base.VisitMember(node);
    }
    protected override Expression VisitBinary(BinaryExpression node)
    {
        switch(node.NodeType)
        {
            case ExpressionType.Coalesce:
                throw new NotImplementedException();
            case ExpressionType.Conditional:
                throw new NotImplementedException();
            case ExpressionType.Switch:
                throw new NotImplementedException();
        }

        Visit(node.Left);
        switch(node.NodeType)
        {
            case ExpressionType.Add:
                _builder.Append('+');break;
            case ExpressionType.And:
                _builder.Append('&');break;
            case ExpressionType.AndAlso:
                _builder.Append(" and ");break;
            case ExpressionType.Decrement:
                _builder.Append("-1");break;
            case ExpressionType.Divide:
                _builder.Append('/');break;
            case ExpressionType.GreaterThan:
                _builder.Append('>');break;
            case ExpressionType.GreaterThanOrEqual:
                _builder.Append(">=");break;
            case ExpressionType.Increment:
                _builder.Append("+1");break;
            case ExpressionType.LeftShift:
                _builder.Append("<<");break;
            case ExpressionType.LessThan:
                _builder.Append('<');break;
            case ExpressionType.LessThanOrEqual:
                _builder.Append("<=");break;
            case ExpressionType.Modulo:
                _builder.Append('%');break;
            case ExpressionType.Multiply:
                _builder.Append('*');break;
            case ExpressionType.Negate:
                _builder.Append('-');break;
            case ExpressionType.Not:
                _builder.Append('~');break;
            case ExpressionType.NotEqual:
                _builder.Append("!=");break;
            case ExpressionType.Or:
                _builder.Append('|');break;
            case ExpressionType.OrElse:
                _builder.Append(" or ");break;
            case ExpressionType.Power:
                _builder.Append('^');break;
            case ExpressionType.RightShift:
                _builder.Append(">>");break;
            case ExpressionType.Subtract:
                _builder.Append('-');break;
            default:
                throw new NotSupportedException();
        }
        Visit(node.Right);
        return node;
    }
    public override string ToString()
    {
        return _builder.ToString();
    }
}