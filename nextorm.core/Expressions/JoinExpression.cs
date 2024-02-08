using System.Linq.Expressions;

namespace nextorm.core;

public enum JoinType
{
    Inner = 0,
    Left = 1,
    Right = 2,
    Full = 3,
    Cross = 4,
    FullCross = 5
}

public class JoinExpression(LambdaExpression joinCondition, JoinType joinType = JoinType.Inner)
{
    public JoinType JoinType { get; } = joinType;
    public LambdaExpression JoinCondition { get; } = joinCondition;
    public required FromExpression From { get; init; }
    internal JoinExpression CloneForCache()
    {
        var newFrom = From.CloneForCache();

        if (newFrom == From) return this;

        return new JoinExpression(JoinCondition, JoinType) { From = From };
    }
    // public override int GetHashCode()
    // {
    //     unchecked
    //     {
    //         var hash = new HashCode();

    //         hash.Add(JoinType);

    //         hash.Add(JoinCondition, ExpressionEqualityComparer.Instance);

    //         hash.Add(Query?.GetHashCode());

    //         return hash.ToHashCode();
    //     }
    // }
    // public override bool Equals(object? obj)
    // {
    //     return Equals(obj as JoinExpression);
    // }
    // public bool Equals(JoinExpression? exp)
    // {
    //     if (exp is null) return false;

    //     if (JoinType != exp.JoinType) return false;

    //     if (!ExpressionEqualityComparer.Instance.Equals(JoinCondition, exp.JoinCondition)) return false;

    //     if (!Equals(Query,exp.Query)) return false;

    //     return true;
    // }
}