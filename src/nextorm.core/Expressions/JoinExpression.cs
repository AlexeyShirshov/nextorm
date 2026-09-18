using System.Linq.Expressions;

namespace nextorm.core;

public enum JoinType
{
    Inner = 0,
    Left = 1,
    Right = 2,
    Full = 3,
    Cross = 4,
    FullCross = 5,
    /// <summary>
    /// <c>CROSS APPLY</c> / <c>CROSS JOIN LATERAL</c>: the right-hand source is evaluated per
    /// left-hand row and has no <c>ON</c> condition.
    /// </summary>
    CrossApply = 6,
    /// <summary>
    /// <c>OUTER APPLY</c> / <c>LEFT JOIN LATERAL ... ON true</c>: like <see cref="CrossApply"/> but
    /// left-hand rows with an empty right-hand source are preserved with nulls.
    /// </summary>
    OuterApply = 7
}

public class JoinExpression(LambdaExpression? joinCondition, JoinType joinType = JoinType.Inner)
{
    public JoinType JoinType { get; } = joinType;
    public LambdaExpression? JoinCondition { get; } = joinCondition;
    /// <summary>
    /// Joined source type. Only needed when <see cref="JoinCondition"/> is absent (a cross join has no
    /// condition parameter to read the right-hand type from), so the alias of the joined table can be
    /// resolved during SQL generation and in the in-memory provider.
    /// </summary>
    public Type? EntityType { get; init; }
    public required FromExpression From { get; init; }
    internal JoinExpression CloneForCache()
    {
        var newFrom = From.CloneForCache();

        if (newFrom == From) return this;

        return new JoinExpression(JoinCondition, JoinType) { From = From, EntityType = EntityType };
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