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
    class TypeExpressionVisitor<T> : ExpressionVisitor
        where T : Expression
    {
        private bool _has;
        private T? _target;
        public bool Has { get => _has; }
        public T? Target {get=>_target;}
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
}