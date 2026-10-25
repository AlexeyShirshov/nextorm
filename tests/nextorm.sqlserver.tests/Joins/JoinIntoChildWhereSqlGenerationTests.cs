using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// #309 SQL generation: a fluent <c>Where</c> declared on the <c>JoinInto</c> child source must reach the
/// join's <c>ON</c> condition — never the outer <c>WHERE</c>, which would break LEFT-join parent
/// preservation — and must compose with the relationship keys, the caller predicate and any injected
/// global filter exactly once. Unsupported child modifiers are rejected at declaration, so they never
/// render SQL at all; those rows assert the throw instead of a statement.
/// </summary>
public class JoinIntoChildWhereSqlGenerationTests
{
    private const string Skip = "skip";

    private static string Canon(string sql) => Regex.Replace(
        sql.Replace("\r\n", " ").Replace("\"", string.Empty).Replace("`", string.Empty)
            .Replace("[", string.Empty).Replace("]", string.Empty),
        @"\s+",
        " ").Trim().ToLowerInvariant();

    private static int CountOccurrences(string text, string needle)
        => Regex.Matches(text, Regex.Escape(needle)).Count;

    /// <summary>The <c>ON</c> clause of the last join (the child join for the many-to-many form).</summary>
    private static string LastOnClause(string sql)
    {
        var canon = Canon(sql);
        var start = canon.LastIndexOf(" on ", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the child source must render a join with an ON clause");

        var tail = canon[(start + 4)..];
        var end = tail.IndexOf(" where ", StringComparison.Ordinal);
        if (end < 0)
            end = tail.IndexOf(" settings ", StringComparison.Ordinal);

        return end < 0 ? tail : tail[..end];
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static EntityBuilder<CwSqlParent> Parents(IDataContext ctx)
        => ctx.From<CwSqlParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.PrimaryProfile, p => p.Id, c => c.ParentId));

    private static EntityBuilder<CwSqlM2MParent> ManyParents(IDataContext ctx)
        => ctx.From<CwSqlM2MParent>(b => b.HasManyThrough<CwSqlM2MChild, CwSqlM2MLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

    private static EntityBuilder<CwSqlChild> Children(IDataContext ctx) => ctx.From<CwSqlChild>();

    [Fact]
    public void ChildWhere_CollectionLeft_ShouldRenderInTheJoinOnAndNotInTheOuterWhere()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(Children(ctx).Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToCommand());

        sql.Should().Contain("left join cw_sql_child");
        sql.Should().NotContain(" where ", "a child predicate in the outer WHERE would drop LEFT parents");

        var on = LastOnClause(sql);
        on.Should().Contain("t1.id = t2.parent_id", "the relationship keys stay part of the ON")
            .And.Contain("t2.tag !=", "the child Where reaches the ON");
        CountOccurrences(on, " and ").Should().Be(1, "keys and child predicate compose exactly once");
    }

    [Fact]
    public void ChildWhere_CollectionInner_ShouldRenderAnInnerJoinWithThePredicateInTheOn()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(Children(ctx).Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToCommand());

        sql.Should().Contain("join cw_sql_child");
        sql.Should().NotContain("left join", "an Inner JoinInto renders an inner join");
        sql.Should().NotContain(" where ");

        var on = LastOnClause(sql);
        on.Should().Contain("t2.tag !=");
        CountOccurrences(on, " and ").Should().Be(1);
    }

    [Fact]
    public void ChildWhere_WithCallerPredicate_ShouldComposeKeysCallerAndChildInTheOn()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(Children(ctx).Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId && p.Id != 9502, p => p.Children)
            .ToCommand());

        sql.Should().NotContain(" where ");

        var on = LastOnClause(sql);
        on.Should().Contain("t1.id = t2.parent_id")
            .And.Contain("t1.id != 9502", "the caller predicate stays in the ON")
            .And.Contain("t2.tag !=", "the child Where is added, not substituted");
        CountOccurrences(on, " and ").Should().Be(2, "keys + caller predicate + child predicate compose once each");
    }

    [Fact]
    public void ChildWhere_Repeated_ShouldAndComposeBothPredicatesExactlyOnce()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(Children(ctx).Where(c => c.Tag != Skip).Where(c => c.Id != 9512), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToCommand());

        var on = LastOnClause(sql);
        on.Should().Contain("t2.tag !=", "the first Where reaches the ON")
            .And.Contain("t2.id != 9512", "the second Where reaches the ON too");
        CountOccurrences(on, " and ").Should().Be(2, "the keys plus one AND-composition of the two predicates");
    }

    [Fact]
    public void ChildWhere_Reference_ShouldRenderInTheJoinOn()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<CwSqlProfile>().Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.PrimaryProfile)
            .ToCommand());

        sql.Should().Contain("left join cw_sql_profile").And.NotContain(" where ");

        var on = LastOnClause(sql);
        on.Should().Contain("t1.id = t2.parent_id").And.Contain("t2.tag !=");
        CountOccurrences(on, " and ").Should().Be(1);
    }

    [Fact]
    public void ChildWhere_ExplicitKeys_ShouldRenderInTheJoinOn()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(Children(ctx).Where(c => c.Tag != Skip), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToCommand());

        sql.Should().Contain("left join cw_sql_child").And.NotContain(" where ");

        var on = LastOnClause(sql);
        on.Should().Contain("t1.id = t2.parent_id").And.Contain("t2.tag !=");
        CountOccurrences(on, " and ").Should().Be(1);
    }

    [Fact]
    public void ChildWhere_ManyToMany_ShouldRenderInTheChildJoinOn()
    {
        using var ctx = SqlServerTestContext.Create();

        string sql = SqlOf(ctx, (dynamic)ManyParents(ctx)
            .JoinInto(ctx.From<CwSqlM2MChild>().Where(c => c.Tag != Skip), (p, c) => c.Id > 0, p => p.Children)
            .CreateJoinIntoPairCommand());

        sql.Should().Contain("left join cw_sql_m_child").And.NotContain(" where ");

        var on = LastOnClause(sql);
        on.Should().Contain("t3.id = t2.", "the junction key links the child")
            .And.Contain("t3.id > 0", "the caller predicate stays in the child ON")
            .And.Contain("t3.tag !=", "the child Where reaches the child ON");
        CountOccurrences(on, " and ").Should().Be(2);
    }

    [Fact]
    public void ChildWhere_WithGlobalFilter_ShouldBeInjectedOnceInTheJoinOn()
    {
        using var ctx = SqlServerTestContext.Create();

        // The filter must be registered before the type's metadata is first built: the first
        // registration wins process-wide. This child type is used by no other test.
        ctx.From<CwSqlFilteredChild>(b => b.HasQueryFilter((c, _) => c.ParentId != 9502));

        var sql = SqlOf(ctx, ctx.From<CwSqlFilteredParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
            .JoinInto(
                ctx.From<CwSqlFilteredChild>().Where(c => c.Tag != Skip),
                (p, c) => p.Id == c.ParentId,
                p => p.Children)
            .ToCommand());

        sql.Should().NotContain(" where ", "the injected global filter joins the ON, not the outer WHERE");

        var on = LastOnClause(sql);
        on.Should().Contain("t2.tag !=", "the fluent child Where is present");
        CountOccurrences(on, "parent_id != 9502").Should().Be(1, "the global filter must not be injected twice");
        CountOccurrences(on, " and ").Should().Be(2, "keys + child Where + global filter compose once each");
    }

    /// <summary>
    /// The guard rejects every other child modifier at declaration, so no SQL is rendered for those
    /// shapes; the assertion is the pre-execution throw that names the shape.
    /// </summary>
    /// <param name="shape">The single unsupported child modifier under test.</param>
    [Theory]
    [InlineData(CwSqlChildShape.OrderBy)]
    [InlineData(CwSqlChildShape.Limit)]
    [InlineData(CwSqlChildShape.Distinct)]
    [InlineData(CwSqlChildShape.GroupBy)]
    public void UnsupportedChildModifier_ShouldThrowAtDeclarationBeforeAnySqlRenders(CwSqlChildShape shape)
    {
        using var ctx = SqlServerTestContext.Create();
        var parents = Parents(ctx);
        var child = ChildWithShape(ctx, shape);

        Action act = () => parents.JoinInto(child, (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*JoinInto cannot fold*only the child's own Where is supported*");
    }

    private static EntityBuilder<CwSqlChild> ChildWithShape(IDataContext ctx, CwSqlChildShape shape)
    {
        var child = Children(ctx);

        return shape switch
        {
            CwSqlChildShape.OrderBy => child.OrderBy(c => c.Id),
            CwSqlChildShape.Limit => child.Limit(1),
            CwSqlChildShape.Distinct => child.Distinct(),
            CwSqlChildShape.GroupBy => child.GroupBy(c => c.ParentId),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unsupported child-shape row"),
        };
    }

    /// <summary>The unsupported child modifiers exercised at the SQL boundary, one row per modifier.</summary>
    public enum CwSqlChildShape
    {
        OrderBy,
        Limit,
        Distinct,
        GroupBy,
    }
}

[SqlTable("cw_sql_parent")]
public sealed class CwSqlParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<CwSqlChild> Children { get; } = new List<CwSqlChild>();

    public CwSqlProfile? PrimaryProfile { get; set; }
}

[SqlTable("cw_sql_child")]
public sealed class CwSqlChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("cw_sql_profile")]
public sealed class CwSqlProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("cw_sql_filtered_parent")]
public sealed class CwSqlFilteredParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<CwSqlFilteredChild> Children { get; } = new List<CwSqlFilteredChild>();
}

[SqlTable("cw_sql_filtered_child")]
public sealed class CwSqlFilteredChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("cw_sql_m_parent")]
public sealed class CwSqlM2MParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<CwSqlM2MChild> Children { get; } = new List<CwSqlM2MChild>();
}

[SqlTable("cw_sql_m_child")]
public sealed class CwSqlM2MChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tag")]
    public string? Tag { get; set; }
}

[SqlTable("cw_sql_m_link")]
public sealed class CwSqlM2MLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
