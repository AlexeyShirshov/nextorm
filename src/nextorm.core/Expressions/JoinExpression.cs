using System.Linq.Expressions;

namespace NextORM.Core;

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
    OuterApply = 7,
    /// <summary>
    /// ClickHouse <c>LEFT SEMI JOIN</c>: keeps only the left-hand columns, for left rows that have at
    /// least one match on the right. Modelled as a join type rather than a
    /// <see cref="JoinStrictness"/> modifier because it changes the result column set.
    /// </summary>
    Semi = 8,
    /// <summary>
    /// ClickHouse <c>LEFT ANTI JOIN</c>: keeps only the left-hand columns, for left rows with no match
    /// on the right; the complement of <see cref="Semi"/>.
    /// </summary>
    Anti = 9,
    /// <summary>
    /// ClickHouse <c>PASTE JOIN</c>: joins the two sources by row position with no <c>ON</c>
    /// condition. The result carries the left and right columns side by side and as many rows as the
    /// shorter side.
    /// </summary>
    Paste = 10
}

/// <summary>
/// Optional join modifier that selects which matching right-hand row survives. Only ClickHouse
/// understands these; every other dialect supports just <see cref="Default"/>. Rendered by
/// <see cref="ISqlDialect.MakeJoinKeyword"/> and gated by <see cref="ISqlDialect.SupportsJoinStrictness"/>.
/// </summary>
public enum JoinStrictness
{
    /// <summary>A plain join with no modifier.</summary>
    Default = 0,
    /// <summary><c>ANY</c>: keep the first matching right-hand row.</summary>
    Any = 1,
    /// <summary><c>ALL</c>: keep every matching right-hand row.</summary>
    All = 2,
    /// <summary><c>ASOF</c>: join on a closest-match inequality (time-series lookup).</summary>
    Asof = 3
}

public class JoinExpression(LambdaExpression? joinCondition, JoinType joinType = JoinType.Inner)
{
    public JoinType JoinType { get; } = joinType;
    public LambdaExpression? JoinCondition { get; } = joinCondition;
    /// <summary>
    /// Join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>). Set through the fluent
    /// <c>WithStrictness</c> modifier, which copies the join rather than mutating it; defaults to
    /// <see cref="JoinStrictness.Default"/>.
    /// </summary>
    public JoinStrictness Strictness { get; internal init; }
    /// <summary>
    /// Whether the join is the ClickHouse <c>GLOBAL</c> variant (the right-hand side is resolved once
    /// and broadcast, for distributed queries). Set through the fluent <c>Global</c> modifier, which
    /// copies the join rather than mutating it. Rendered by
    /// <see cref="ISqlDialect.MakeJoinKeyword"/> and gated by <see cref="ISqlDialect.SupportsGlobalJoin"/>.
    /// </summary>
    public bool IsGlobal { get; internal init; }
    /// <summary>
    /// Joined source type. Only needed when <see cref="JoinCondition"/> is absent (a cross join has no
    /// condition parameter to read the right-hand type from), so the alias of the joined table can be
    /// resolved during SQL generation and in the in-memory provider.
    /// </summary>
    public Type? EntityType { get; init; }
    private FromExpression _from = null!;
    public required FromExpression From { get => _from; init => _from = value; }
    /// <summary>
    /// Set for a correlated <c>CROSS/OUTER APPLY</c> source: a lambda whose parameter is the
    /// left-hand row and whose body builds the applied query. The concrete derived query is built
    /// from it during preparation (<c>QueryCommand.QueryPreparer.PrepareJoin</c>) and installed as
    /// <see cref="From"/>; it is not part of the public surface.
    /// </summary>
    internal LambdaExpression? ApplySource { get; init; }
    /// <summary>Installs the derived query built from <see cref="ApplySource"/> during preparation.</summary>
    internal void SetFrom(FromExpression from) => _from = from;
    internal JoinExpression CloneForCache()
    {
        var newFrom = From.CloneForCache();

        if (newFrom == From) return this;

        return new JoinExpression(JoinCondition, JoinType) { From = newFrom!, EntityType = EntityType, Strictness = Strictness, IsGlobal = IsGlobal, ApplySource = ApplySource };
    }
    // public override int GetHashCode()
    // {
    //     unchecked
    //     {
    //         var hash = new XxHash32();

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