using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// The kind of join connecting a left and right source. Numeric values are part of the plan hash and
/// must stay stable.
/// </summary>
public enum JoinType
{
    /// <summary><c>INNER JOIN</c>: keeps only rows that match on both sides.</summary>
    Inner = 0,
    /// <summary><c>LEFT [OUTER] JOIN</c>: keeps every left row, padding unmatched right columns with nulls.</summary>
    Left = 1,
    /// <summary><c>RIGHT [OUTER] JOIN</c>: keeps every right row, padding unmatched left columns with nulls.</summary>
    Right = 2,
    /// <summary><c>FULL [OUTER] JOIN</c>: keeps every row from both sides, padding where there is no match.</summary>
    Full = 3,
    /// <summary><c>CROSS JOIN</c>: the Cartesian product of both sources; has no <c>ON</c> condition.</summary>
    Cross = 4,
    /// <summary>
    /// A conditionless cross join variant. Renders and evaluates like <see cref="Cross"/>; kept as a
    /// distinct value for callers that need to distinguish it.
    /// </summary>
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
/// <see cref="ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness, bool, KeywordCase)"/> and gated by <see cref="ISqlDialect.SupportsJoinStrictness"/>.
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

/// <summary>
/// A join between two sources: the join kind, the optional <c>ON</c> condition, the joined
/// <see cref="From"/> source and the optional ClickHouse strictness and global modifiers.
/// </summary>
/// <param name="joinCondition">The <c>ON</c> condition, or <c>null</c> for a conditionless join.</param>
/// <param name="joinType">The kind of join to perform.</param>
public class JoinExpression(LambdaExpression? joinCondition, JoinType joinType = JoinType.Inner)
{
    /// <summary>The kind of join.</summary>
    public JoinType JoinType { get; } = joinType;
    /// <summary>The <c>ON</c> condition, or <c>null</c> when the join has none (cross and apply joins).</summary>
    public LambdaExpression? JoinCondition { get; private set; } = joinCondition;
    private LambdaExpression? _originalJoinCondition = joinCondition;
    internal LambdaExpression? OriginalJoinCondition
    {
        get => _originalJoinCondition;
        init => _originalJoinCondition = value;
    }
    /// <summary>
    /// Replaces the <c>ON</c> condition during preparation, after the joined entity's global query
    /// filters have been combined into it.
    /// </summary>
    /// <param name="condition">The condition to install.</param>
    internal void SetJoinCondition(LambdaExpression condition) => JoinCondition = condition;
    /// <summary>
    /// Join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>). Set at construction from the join's
    /// <see cref="JoinOptions"/> (<c>WithStrictness</c>); defaults to
    /// <see cref="JoinStrictness.Default"/>.
    /// </summary>
    public JoinStrictness Strictness { get; internal init; }
    /// <summary>
    /// Whether the join is the ClickHouse <c>GLOBAL</c> variant (the right-hand side is resolved once
    /// and broadcast, for distributed queries). Set at construction from the join's
    /// <see cref="JoinOptions"/> (<c>Global</c>). Rendered by
    /// <see cref="ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness, bool, KeywordCase)"/> and gated by <see cref="ISqlDialect.SupportsGlobalJoin"/>.
    /// </summary>
    public bool IsGlobal { get; internal init; }
    /// <summary>
    /// Optional provider-specific hint attached to this join, or <c>null</c> when the join has none.
    /// Set at construction from the join's <see cref="JoinOptions"/> (<c>WithJoinHint</c>).
    /// A dialect that renders join hints places it inside the join clause (SQL Server
    /// <c>inner loop join</c>); a dialect with inline hint comments folds it into the statement-level
    /// <c>/*+ ... */</c>. A dialect that supports neither rejects the command.
    /// </summary>
    public string? JoinHint { get; internal init; }
    /// <summary>
    /// Optional table-level hints attached to this join's right-hand physical table, or <c>null</c>
    /// when the join has none. Set at construction from the join's <see cref="JoinOptions"/>
    /// (<c>WithJoinTableHint</c>). On a dialect with structural table hints (SQL Server) they
    /// render as a <c>WITH (...)</c> clause after the joined table name; a dialect that does not
    /// support table hints rejects the command.
    /// </summary>
    internal IReadOnlyList<string>? TableHints { get; init; }
    /// <summary>
    /// Joined source type. Only needed when <see cref="JoinCondition"/> is absent (a cross join has no
    /// condition parameter to read the right-hand type from), so the alias of the joined table can be
    /// resolved during SQL generation and in the in-memory provider.
    /// </summary>
    public Type? EntityType { get; init; }
    /// <summary>
    /// The lambda parameter that denotes this join's right-hand source when it was injected by an
    /// implicit reference-navigation expansion (#148-B R2.2). Registered with the columns provider so
    /// alias resolution binds by occurrence identity, keeping two same-typed joined sources distinct.
    /// <c>null</c> for an ordinary user join.
    /// </summary>
    internal ParameterExpression? SourceParameter { get; init; }
    private FromExpression _from = null!;
    /// <summary>The right-hand source being joined.</summary>
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
    /// <summary>
    /// Whether this join was added by <c>JoinInto</c> (as opposed to an explicit <c>Join</c>). The flag
    /// lets the non-list terminals build a parent-only command that excludes the stitching join.
    /// </summary>
    internal bool IsJoinInto { get; set; }
    /// <summary>
    /// Whether the <c>JoinInto</c> declaration that added this join asked to suppress the
    /// <c>JoinInto.MultipleCollections</c> diagnostic (see <see cref="JoinOptions.SuppressCartesianWarning"/>).
    /// Diagnostic-only: it never participates in the rendered SQL or the plan key.
    /// </summary>
    internal bool SuppressCartesianWarning { get; init; }
    /// <summary>
    /// The identity of the <c>JoinInto</c> declaration that added this join, or <see langword="null"/>
    /// for a regular join. It is folded into the join's plan hash and equality so two distinct
    /// <c>JoinInto</c> declarations (different child/keys/collection) never share a cached plan.
    /// </summary>
    internal JoinIntoIdentity? JoinIntoIdentity { get; set; }
    /// <summary>
    /// The right-hand (child) side's selective global-query-filter scope, or <see langword="null"/> to
    /// inherit the command's <see cref="QueryCommand.FilterScope"/>. <see langword="null"/> and an empty
    /// scope are equivalent (both inherit): the effective child scope is always the <b>union</b> of this
    /// scope and the command's, and <see cref="QueryFilterScope.AllFilters"/> absorbs that union, so a
    /// parent <c>IgnoreFilters()</c> still disables every child filter and a child
    /// <c>IgnoreFilters(keys)</c> adds to the parent's selective scope. Set for the single-query
    /// (<c>EagerLoadMode.SingleQuery</c>) child joins and for a <c>JoinInto</c> child that disabled filters; a plain
    /// join leaves it <see langword="null"/>.
    /// </summary>
    internal QueryFilterScope? FilterScope { get; set; }
    /// <summary>
    /// Returns the command's own copy-on-write clone used by preparation (see
    /// <c>QueryPreparer.PrepareJoin</c>). Builders share <see cref="JoinExpression"/> elements by
    /// reference across clones, so preparation must mutate only this command's copy. The copy bases its
    /// mutable <see cref="JoinCondition"/> on the pristine <see cref="OriginalJoinCondition"/> (a sibling
    /// may already have injected its filters into the shared condition) and shares <see cref="From"/>
    /// rather than deep-cloning it: a derived join's <c>From.SubQuery</c> must stay bound to its data
    /// context, and preparation only replaces the whole source (<see cref="SetFrom"/>), never mutates it.
    /// </summary>
    internal JoinExpression CloneForPreparation()
        => new(_originalJoinCondition, JoinType)
        {
            From = _from,
            EntityType = EntityType,
            SourceParameter = SourceParameter,
            Strictness = Strictness,
            IsGlobal = IsGlobal,
            JoinHint = JoinHint,
            TableHints = TableHints,
            ApplySource = ApplySource,
            OriginalJoinCondition = _originalJoinCondition,
            IsJoinInto = IsJoinInto,
            JoinIntoIdentity = JoinIntoIdentity,
            SuppressCartesianWarning = SuppressCartesianWarning,
            FilterScope = FilterScope,
        };
    internal JoinExpression CloneForCache()
    {
        var newFrom = From.CloneForCache();

        return new JoinExpression(JoinCondition, JoinType)
        {
            From = newFrom!,
            EntityType = EntityType,
            SourceParameter = SourceParameter,
            Strictness = Strictness,
            IsGlobal = IsGlobal,
            JoinHint = JoinHint,
            TableHints = TableHints,
            ApplySource = ApplySource,
            OriginalJoinCondition = _originalJoinCondition,
            IsJoinInto = IsJoinInto,
            JoinIntoIdentity = JoinIntoIdentity,
            SuppressCartesianWarning = SuppressCartesianWarning,
            FilterScope = FilterScope,
        };
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