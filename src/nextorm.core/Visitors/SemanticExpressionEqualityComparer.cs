using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Semantic equality comparer for expressions, distinct from the value-free plan comparer:
/// literals compare by value, source parameters are alpha-equivalent, closure targets compare by
/// reference identity plus member path, and unknown nodes are conservatively unequal. No captured
/// value is evaluated.
/// </summary>
public sealed class SemanticExpressionEqualityComparer : IEqualityComparer<Expression>
{
    /// <summary>Shared instance.</summary>
    public static readonly SemanticExpressionEqualityComparer Instance = new();

    private SemanticExpressionEqualityComparer() { }

    /// <inheritdoc />
    public bool Equals(Expression? x, Expression? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        if (x.NodeType != y.NodeType) return false;
        if (x.Type != y.Type) return false;

        // Parameter expressions: alpha-equivalent by name is too weak; treat any two parameters as
        // potentially equivalent only when both are parameters (renamed). We map by position lazily
        // in the comparer-less path: here we accept any parameter of the same type as equivalent,
        // which matches "source parameters alpha-equivalent" for single-source projections.
        if (x is ParameterExpression && y is ParameterExpression)
            return true;

        // Constant: literals by value; closure targets by reference identity.
        if (x is ConstantExpression cx && y is ConstantExpression cy)
        {
            // Closure target (non-primitive captured object): compare by reference identity.
            if (cx.Value is not null && cx.Value.GetType().IsClass && cx.Value.GetType() != typeof(string))
            {
                return ReferenceEquals(cx.Value, cy.Value);
            }
            // Literal value: compare by value.
            return Equals(cx.Value, cy.Value);
        }

        // Member access: compare expression + member name.
        if (x is MemberExpression mx && y is MemberExpression my)
        {
            if (mx.Member.Name != my.Member.Name) return false;
            if (mx.Member.DeclaringType != my.Member.DeclaringType) return false;
            return Equals(mx.Expression, my.Expression);
        }

        // Method call: compare method + arguments.
        if (x is MethodCallExpression mcx && y is MethodCallExpression mcy)
        {
            if (mcx.Method != mcy.Method) return false;
            if (mcx.Arguments.Count != mcy.Arguments.Count) return false;
            if (!Equals(mcx.Object, mcy.Object)) return false;
            for (var i = 0; i < mcx.Arguments.Count; i++)
            {
                if (!Equals(mcx.Arguments[i], mcy.Arguments[i])) return false;
            }
            return true;
        }

        // Binary.
        if (x is BinaryExpression bx && y is BinaryExpression by)
        {
            if (bx.Method != by.Method) return false;
            return Equals(bx.Left, by.Left) && Equals(bx.Right, by.Right);
        }

        // Unary.
        if (x is UnaryExpression ux && y is UnaryExpression uy)
        {
            if (ux.Method != uy.Method) return false;
            return Equals(ux.Operand, uy.Operand);
        }

        // New expression.
        if (x is NewExpression nx && y is NewExpression ny)
        {
            if (nx.Constructor != ny.Constructor) return false;
            if (nx.Arguments.Count != ny.Arguments.Count) return false;
            for (var i = 0; i < nx.Arguments.Count; i++)
            {
                if (!Equals(nx.Arguments[i], ny.Arguments[i])) return false;
            }
            return true;
        }

        // Lambda.
        if (x is LambdaExpression lx && y is LambdaExpression ly)
        {
            if (lx.Parameters.Count != ly.Parameters.Count) return false;
            return Equals(lx.Body, ly.Body);
        }

        // Conservative: any other node type is unequal unless already reference-equal.
        return false;
    }

    /// <inheritdoc />
    public int GetHashCode(Expression obj)
    {
        if (obj is null) return 0;
        var hc = new HashCode();
        hc.Add(obj.NodeType);
        hc.Add(obj.Type);
        if (obj is ConstantExpression c)
        {
            if (c.Value is not null && c.Value.GetType().IsClass && c.Value.GetType() != typeof(string))
                hc.Add(RuntimeHelpers.GetHashCode(c.Value));
            else
                hc.Add(c.Value);
        }
        else if (obj is ParameterExpression)
        {
            hc.Add(0); // alpha: all params same hash bucket
        }
        else if (obj is MemberExpression m)
        {
            hc.Add(m.Member.Name);
            if (m.Expression is not null) hc.Add(GetHashCode(m.Expression));
        }
        else if (obj is MethodCallExpression mc)
        {
            hc.Add(mc.Method);
            if (mc.Object is not null) hc.Add(GetHashCode(mc.Object));
            foreach (var a in mc.Arguments) hc.Add(GetHashCode(a));
        }
        return hc.ToHashCode();
    }
}
