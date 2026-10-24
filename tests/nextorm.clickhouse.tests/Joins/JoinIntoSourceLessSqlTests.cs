using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// D4 (#330): SQL parity of the five source-less <c>JoinInto</c> forms. Every cell renders the statement
/// twice — once from the source-less call (child source resolved from the data context) and once from the
/// explicit bare <c>ctx.From&lt;TChild&gt;()</c> twin — and the two statements must be identical after
/// <see cref="Normalize"/>. Equality alone cannot prove the join kind, so each cell also pins the join
/// fragment in the provider's own alias/quoting idiom, checks that the child table occurs exactly once and
/// that the parent projection is unchanged. These tests never open a connection.
/// <para>
/// The INNER cells assert the implicit-inner spelling the emitter actually produces (<c>join</c>, not
/// <c>inner join</c>) plus the absence of <c>left join</c>: that pair pins the kind exactly as tightly as a
/// literal <c>inner join</c> would.
/// </para>
/// </summary>
public class JoinIntoSourceLessSqlTests
{
    private const string ChildTable = "join_into_sl_child";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<JoinIntoSlParent> Parents(IDataContext ctx) =>
        ctx.From<JoinIntoSlParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId));

    private static string SelectList(string sql)
    {
        var from = sql.IndexOf("\n from ", StringComparison.Ordinal);
        return from < 0 ? sql : sql[..from];
    }

    /// <summary>
    /// Asserts exact statement parity with the explicit twin, that the expected join fragment is present
    /// (so the join kind is visible, not merely implied by equality), that the child table occurs exactly
    /// once (no duplicated FROM/JOIN) and that the parent projection is unchanged.
    /// </summary>
    private static void AssertParity(string sourceLessSql, string explicitSql, string joinFragment)
    {
        sourceLessSql.Should().Be(explicitSql, "the source-less form must render exactly the explicit bare-source statement");
        sourceLessSql.Should().Contain(joinFragment);
        Regex.Matches(sourceLessSql, Regex.Escape(ChildTable)).Count.Should()
            .Be(1, "the child table must appear exactly once: no duplicated FROM/JOIN");
        SelectList(sourceLessSql).Should().Be(SelectList(explicitSql), "the parent projection must not change");
        SelectList(sourceLessSql).Should().StartWith("select t1.id, t1.name", "the parent select list stays the parent's");
    }

    // --- collection form: default LEFT, explicit Left, explicit Inner ---------------------------------

    [Fact]
    public void SourceLess_CollectionForm_DefaultAndExplicitKinds_ShouldMatchTheExplicitBareSource()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sourceLessDefault = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .ToCommand());
        var explicitDefault = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToCommand());
        AssertParity(sourceLessDefault, explicitDefault, "left join join_into_sl_child as `t2` on t1.id = t2.parent_id");

        var sourceLessLeft = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Left)
            .ToCommand());
        var explicitLeft = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Left)
            .ToCommand());
        AssertParity(sourceLessLeft, explicitLeft, "left join join_into_sl_child as `t2` on t1.id = t2.parent_id");

        var sourceLessInner = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToCommand());
        var explicitInner = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToCommand());
        AssertParity(sourceLessInner, explicitInner, " join join_into_sl_child as `t2` on t1.id = t2.parent_id");
        sourceLessInner.Should().NotContain("left join", "an Inner JoinInto must not render a left join");
    }

    // --- reference form: default LEFT, explicit Left, explicit Inner ----------------------------------

    [Fact]
    public void SourceLess_ReferenceForm_DefaultAndExplicitKinds_ShouldMatchTheExplicitBareSource()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sourceLessDefault = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild>((p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToCommand());
        var explicitDefault = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToCommand());
        AssertParity(sourceLessDefault, explicitDefault, "left join join_into_sl_child as `t2` on t1.id = t2.parent_id");

        var sourceLessLeft = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild>((p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Left)
            .ToCommand());
        var explicitLeft = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Left)
            .ToCommand());
        AssertParity(sourceLessLeft, explicitLeft, "left join join_into_sl_child as `t2` on t1.id = t2.parent_id");

        var sourceLessInner = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild>((p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Inner)
            .ToCommand());
        var explicitInner = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Inner)
            .ToCommand());
        AssertParity(sourceLessInner, explicitInner, " join join_into_sl_child as `t2` on t1.id = t2.parent_id");
        sourceLessInner.Should().NotContain("left join", "an Inner JoinInto must not render a left join");
    }

    // --- key form: the legacy signature exposes only LEFT (no JoinType parameter) ----------------------

    [Fact]
    public void SourceLess_KeyForm_LeftOnly_ShouldMatchTheExplicitBareSource()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sourceLess = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToCommand());
        var explicitBare = SqlOf(ctx, Parents(ctx)
            .JoinInto<JoinIntoSlChild, int>(ctx.From<JoinIntoSlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToCommand());

        AssertParity(sourceLess, explicitBare, "left join join_into_sl_child as `t2`");
    }
}

[SqlTable("join_into_sl_parent")]
public sealed class JoinIntoSlParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JoinIntoSlChild> Children { get; set; } = new List<JoinIntoSlChild>();

    public JoinIntoSlChild? Primary { get; set; }
}

[SqlTable("join_into_sl_child")]
public sealed class JoinIntoSlChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
