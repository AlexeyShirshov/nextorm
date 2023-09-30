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

    class TypeExpressionVisitor<T> : ExpressionVisitor
        where T : Expression
    {
        private bool _has;

        public bool Has { get => _has; }

        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
        {
            if (node is T)
            {
                _has = true;
                return node;                
            }
            
            return base.Visit(node);
        }
    }
}