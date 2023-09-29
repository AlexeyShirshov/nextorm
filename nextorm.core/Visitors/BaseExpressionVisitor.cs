using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace nextorm.core;
public class BaseExpressionVisitor : ExpressionVisitor, ICloneable
{
    private readonly Type _entityType;
    private readonly SqlDataProvider _dataProvider;
    private readonly FromExpression _from;
    protected readonly StringBuilder _builder = new();
    private readonly List<Param> _params = new (); 
    private bool _needAlias;
    public BaseExpressionVisitor(Type entityType, SqlDataProvider dataProvider, FromExpression from)
    {
        _entityType = entityType;
        _dataProvider = dataProvider;
        _from = from;
    }
    public bool NeedAlias => _needAlias;
    public List<Param> Params => _params;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Object?.Type == typeof(TableAlias))
        {
            // switch (node.Method.Name)
            // {
            //     case "Long":
            //         if (node.Arguments[0] is ConstantExpression constExp)
            //             _builder.Append(constExp.Value?.ToString());

            //         break;
            // }

            _builder.Append(node switch
            {
                {
                    Method.Name:
                        nameof(TableAlias.Long)
                        or nameof(TableAlias.Int)
                        or nameof(TableAlias.Boolean)
                        or nameof(TableAlias.Byte)
                        or nameof(TableAlias.DateTime)
                        or nameof(TableAlias.Decimal)
                        or nameof(TableAlias.Double)
                        or nameof(TableAlias.Float)
                        or nameof(TableAlias.Guid)
                        or nameof(TableAlias.NullableLong)
                        or nameof(TableAlias.NullableInt)
                        or nameof(TableAlias.NullableBoolean)
                        or nameof(TableAlias.NullableByte)
                        or nameof(TableAlias.NullableDateTime)
                        or nameof(TableAlias.NullableDecimal)
                        or nameof(TableAlias.NullableDouble)
                        or nameof(TableAlias.NullableFloat)
                        or nameof(TableAlias.NullableGuid),
                    Arguments: [ConstantExpression constExp]
                } => constExp.Value?.ToString(),
                _ => throw new NotSupportedException(node.Method.Name)
            });

            return node;
        }

        return base.VisitMethodCall(node);
    }
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (!EmitValue(node.Type, node.Value))
        {
            // if (node.Type.IsClosure())
            // {
            //     var o = GetFirstProp(node.Value);

            //     EmitValue(o.Item1, o.Item2);
            // }
        }

        return node;

        // return base.VisitConstant(node);
        // static (Type, object?) GetFirstProp(object value)
        // {
        //     var field = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).First();
        //     return (field.FieldType, field.GetValue(value));
        // }
        bool EmitValue(Type t, object? v)
        {
            if (v is null)
                _builder.Append("null");
            else if (t == typeof(string) || t == typeof(Guid))
            {
                _builder.Append('\'').Append(v?.ToString()).Append('\'');
            }
            else if (t.IsPrimitive)
                _builder.Append(v?.ToString());
            else
                return false;

            return true;
        }
    }
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression?.Type == _entityType)
        {
            var colAttr = node.Member.GetCustomAttribute<ColumnAttribute>();
            if (colAttr is not null)
                _builder.Append(colAttr.Name);
            else if (_from.Table.IsT1)
            {
                var innerCol = _from.Table.AsT1.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name) ?? throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");
                var col = _dataProvider.MakeColumn(innerCol, _from.Table.AsT1.EntityType!, _from.Table.AsT1.From!);
                if (col.NeedAlias)
                    _builder.Append(innerCol.PropertyName);
                else
                    _builder.Append(col.Column);
            }
            else
                _builder.Append(_dataProvider.GetColumnName(node.Member));
            //}
            return node;
        }
        else 
        {
            //if (node.NodeType == ExpressionType.MemberAccess && node.Expression is ConstantExpression constExp)
            {
                // var valueVisitor = new ValueVisitor(constExp);
                // valueVisitor.Visit(constExp);
                // valueVisitor.Value.Get
                var value = Expression.Lambda(node).Compile().DynamicInvoke();
                _params.Add(new Param(node.Member.Name, value));
                _builder.Append(_dataProvider.MakeParam(node.Member.Name));

                return node;
            }
        }

        //return base.VisitMember(node);
    }
    protected override Expression VisitBinary(BinaryExpression node)
    {
        _needAlias = true;

        switch (node.NodeType)
        {
            case ExpressionType.Coalesce:
                var leftVisitor = Clone();
                leftVisitor.Visit(node.Left);
                Params.AddRange(leftVisitor.Params);

                var rightVisitor = Clone();
                rightVisitor.Visit(node.Right);
                Params.AddRange(rightVisitor.Params);

                _builder.Append(_dataProvider.MakeCoalesce(
                    leftVisitor.ToString(),
                    rightVisitor.ToString()
                ));

                return node;
            case ExpressionType.Conditional:
                throw new NotImplementedException();
            case ExpressionType.Switch:
                throw new NotImplementedException();
        }

        _builder.Append('(');
        Visit(node.Left);
        switch (node.NodeType)
        {
            case ExpressionType.Add:
                if (node.Type == typeof(string))
                    _builder.Append(_dataProvider.ConcatStringOperator);
                else
                    _builder.Append(" + ");

                break;
            case ExpressionType.And:
                _builder.Append(" & "); break;
            case ExpressionType.AndAlso:
                _builder.Append(" and "); break;
            case ExpressionType.Decrement:
                _builder.Append(" -1 "); break;
            case ExpressionType.Divide:
                _builder.Append(" / "); break;
            case ExpressionType.GreaterThan:
                _builder.Append(" > "); break;
            case ExpressionType.GreaterThanOrEqual:
                _builder.Append(" >= "); break;
            case ExpressionType.Increment:
                _builder.Append(" + 1"); break;
            case ExpressionType.LeftShift:
                _builder.Append(" << "); break;
            case ExpressionType.LessThan:
                _builder.Append(" < "); break;
            case ExpressionType.LessThanOrEqual:
                _builder.Append(" <= "); break;
            case ExpressionType.Modulo:
                _builder.Append(" % "); break;
            case ExpressionType.Multiply:
                _builder.Append(" * "); break;
            case ExpressionType.Negate:
                _builder.Append(" - "); break;
            case ExpressionType.Not:
                _builder.Append(" ~ "); break;
            case ExpressionType.NotEqual:
                _builder.Append(" != "); break;
            case ExpressionType.Or:
                _builder.Append(" | "); break;
            case ExpressionType.OrElse:
                _builder.Append(" or "); break;
            case ExpressionType.Power:
                _builder.Append(" ^ "); break;
            case ExpressionType.RightShift:
                _builder.Append(" >> "); break;
            case ExpressionType.Subtract:
                _builder.Append(" - "); break;
            default:
                throw new NotSupportedException(node.NodeType.ToString());
        }
        Visit(node.Right);
        _builder.Append(')');

        return node;
    }
    public override string ToString()
    {
        return _builder.ToString();
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    public virtual BaseExpressionVisitor Clone()
    {
        return new BaseExpressionVisitor(_entityType, _dataProvider, _from);
    }
}