using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace nextorm.core;

public static class ExpressionExtensions
{
    public static bool Has<T>(this Expression exp)
        where T : Expression
    {
        var visitor = new TypeExpressionVisitor<T>();
        visitor.Visit(exp);
        return visitor.Has;
    }
    public static bool Has<T>(this Expression exp, out T? param)
        where T : Expression
    {
        var visitor = new TypeExpressionVisitor<T>();
        visitor.Visit(exp);
        param = visitor.Target;
        return visitor.Has;
    }
}
public class TypeExpressionVisitor<T> : ExpressionVisitor
    where T : Expression
{
    private bool _has;
    private T? _target;
    public bool Has => _has;
    public T? Target => _target;
    [return: NotNullIfNotNull("node")]
    public override Expression? Visit(Expression? node)
    {
        if (node is T t)
        {
            _has = true;
            _target = t;
            return node;
        }

        return base.Visit(node);
    }
}
public class TwoTypeExpressionVisitor<T1, T2> : ExpressionVisitor
    where T1 : Expression
    where T2 : Expression
{
    private bool _has1;
    private T1? _target1;
    public bool Has1 => _has1;
    public T1? Target1 => _target1;
    private bool _has2;
    private T2? _target2;
    public bool Has2 => _has2;
    public T2? Target2 => _target2;
    [return: NotNullIfNotNull("node")]
    public override Expression? Visit(Expression? node)
    {
        if (node is T1 t1)
        {
            _has1 = true;
            _target1 = t1;
            return node;
        }
        else if (node is T2 t2)
        {
            _has2 = true;
            _target2 = t2;
            return node;
        }
        return base.Visit(node);
    }
}
