using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T304 (#304): a declared reference navigation used *inside* a correlated subquery must become a
/// properly registered outer reference of the nested command, so the joined alias that navigation
/// expansion injected on the outer command stays correlated instead of leaking as a free
/// <c>navigation{N}</c> parameter. The emitted shape is portable: <c>LEFT JOIN</c> on the outer command
/// plus a correlated <c>EXISTS</c> / scalar subquery over the inner one, never <c>APPLY</c>.
///
/// <para>
/// The matrix rows are S (scalar reference comparison inside the correlated body), E (bare reference
/// presence there) and P (reference scalar projected by the correlated subquery - explicitly not
/// whole-object eager loading). Each row covers hop 1, hop 2+ and a nullable foreign key. Which rows a
/// populated versus a missing (dangling or null) reference yields is data-dependent, so those
/// outcomes are pinned by the live suites; here the SQL shape they all share is pinned. No database is
/// opened: the provider context carries a placeholder connection and only preparation runs.
/// </para>
/// </summary>
public class CorrelatedReferenceHopSqlTests
{
    /// <summary>
    /// Strips every identifier quote flavour (SQLite single quotes, PostgreSQL double quotes,
    /// SQL Server brackets, MySQL/MariaDB/ClickHouse backticks) and lowercases the text, so one shape
    /// assertion holds on every provider. Only structure is compared, never literal values.
    /// </summary>
    private static string Shape(string sql)
    {
        var lowered = sql.Replace("\r\n", "\n").ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        foreach (var c in lowered)
        {
            if (c is '[' or ']' or '`' or '"' or '\'') continue;
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Shape(Prepare(ctx, cmd).DbCommand.CommandText);

    private static IDataContext CreateContext()
    {
        var ctx = PostgresTestContext.Create();
        ctx.From<T304Root>();
        ctx.From<T304Group>(b => b.HasOne(g => g.Root, g => g.RootId));
        ctx.From<T304Parent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<T304NullableParent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        return ctx;
    }

    /// <summary>The same command shape built twice, so the captured parameter carries two distinct
    /// identities of one CLR type under one captured name.</summary>
    private static QueryCommand<int> ParameterCommand(IDataContext ctx, string value)
    {
        var inner = ctx.From<T304Child>();
        return ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code && p.Group.Code == value)))
            .Select(p => p.Id);
    }

    /// <summary>
    /// S row. The historical red arm (#304) is the first hop: it used to throw
    /// <c>InvalidOperationException: variable 'navigation0' ... is not defined</c> at preparation. The
    /// repaired shape joins the reference on the outer command and correlates the inner one on the
    /// joined alias; hop 2+, the nullable foreign key and their combinations go through the same door.
    /// </summary>
    [Fact]
    public void ScalarExists_HopAndNullMatrix()
    {
        using var ctx = CreateContext();
        var inner = ctx.From<T304Child>();

        var hop1 = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id));

        hop1.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        hop1.Should().Contain("exists(select *");
        hop1.Should().Contain("t3.parent_id = t1.id");
        hop1.Should().Contain("t3.group_code = t2.code");
        hop1.Should().NotContain("apply");

        // Hop 2+: the same body reads through a two-level chain. Both joins belong to the outer command
        // and the deepest alias is the correlated scalar - a hop depth is not a special case.
        var hop2 = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id));

        hop2.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        hop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        hop2.Should().Contain("t4.parent_id = t1.id");
        hop2.Should().Contain("t4.group_code = t3.label");
        hop2.Should().NotContain("apply");

        // Nullable foreign key: the join pair is (int?, int), so only the narrower operand is lifted
        // before the key equality is built; the portable join + correlated shape is unchanged.
        var nullableHop1 = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id));

        nullableHop1.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        nullableHop1.Should().Contain("t3.group_code = t2.code");
        nullableHop1.Should().NotContain("apply");

        var nullableHop2 = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id));

        nullableHop2.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        nullableHop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        nullableHop2.Should().Contain("t4.group_code = t3.label");
        nullableHop2.Should().NotContain("apply");
    }

    /// <summary>
    /// E row: a bare reference used as a presence test expands to the joined-key null test, so the
    /// joined alias has to stay correlated exactly like a member read - never a free parameter. The
    /// negated form is the missing-reference compensation and must translate the same way.
    /// </summary>
    [Fact]
    public void Presence_HopAndNullMatrix()
    {
        using var ctx = CreateContext();
        var inner = ctx.From<T304Child>();

        var presentHop1 = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group != null)))
            .Select(p => p.Id));

        presentHop1.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        presentHop1.Should().Contain("exists(select *");
        presentHop1.Should().Contain("t3.parent_id = t1.id");
        presentHop1.Should().Contain("t2.id is not null");
        presentHop1.Should().NotContain("apply");

        var presentHop2 = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group!.Root != null)))
            .Select(p => p.Id));

        presentHop2.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        presentHop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        presentHop2.Should().Contain("t4.parent_id = t1.id");
        presentHop2.Should().Contain("t3.id is not null");
        presentHop2.Should().NotContain("apply");

        var missingHop1 = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group == null)))
            .Select(p => p.Id));

        missingHop1.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        missingHop1.Should().Contain("t2.id is null");
        missingHop1.Should().NotContain("apply");

        // Nullable foreign key: a null key never matches the joined principal, so the same null test
        // expresses both "absent row" and "absent key" - with no extra join form.
        var nullablePresent = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group != null)))
            .Select(p => p.Id));

        nullablePresent.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        nullablePresent.Should().Contain("t2.id is not null");
        nullablePresent.Should().NotContain("apply");

        var nullableMissing = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group == null)))
            .Select(p => p.Id));

        nullableMissing.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        nullableMissing.Should().Contain("t2.id is null");
        nullableMissing.Should().NotContain("apply");

        var nullableHop2 = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group!.Root != null)))
            .Select(p => p.Id));

        nullableHop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        nullableHop2.Should().Contain("t3.id is not null");
        nullableHop2.Should().NotContain("apply");

        // Missing reference at hop 2+: the deepest joined key is NULL, so the presence test
        // flips to the null test without changing the join chain.
        var missingHop2 = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group!.Root == null)))
            .Select(p => p.Id));

        missingHop2.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        missingHop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        missingHop2.Should().Contain("t3.id is null");
        missingHop2.Should().NotContain("apply");

        var nullableMissingHop2 = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group!.Root == null)))
            .Select(p => p.Id));

        nullableMissingHop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        nullableMissingHop2.Should().Contain("t3.id is null");
        nullableMissingHop2.Should().NotContain("apply");
    }

    /// <summary>
    /// P row: the reference scalar is projected by the correlated subquery itself (the inner select
    /// list reads the outer joined alias). This is the scalar scope only - whole-object eager loading
    /// of a reference is a different, deferred form - and it must correlate without APPLY at both hops.
    /// </summary>
    [Fact]
    public void ScalarProjection_HopAndNullMatrix()
    {
        using var ctx = CreateContext();

        var hop1 = SqlOf(ctx, ctx.From<T304Parent>()
            .Select(p => new
            {
                p.Id,
                Code = ctx.From<T304Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => p.Group!.Code)
                    .FirstOrDefault()
            }));

        hop1.Should().Contain("(select t2.code");
        hop1.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        hop1.Should().Contain("from t304_child as t3");
        hop1.Should().Contain("t3.parent_id = t1.id");
        hop1.Should().NotContain("apply");

        var hop2 = SqlOf(ctx, ctx.From<T304Parent>()
            .Select(p => new
            {
                p.Id,
                Label = ctx.From<T304Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => p.Group!.Root!.Label)
                    .FirstOrDefault()
            }));

        hop2.Should().Contain("(select t3.label");
        hop2.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        hop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        hop2.Should().Contain("from t304_child as t4");
        hop2.Should().Contain("t4.parent_id = t1.id");
        hop2.Should().NotContain("apply");

        var nullableHop1 = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Select(p => new
            {
                p.Id,
                Code = ctx.From<T304Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => p.Group!.Code)
                    .FirstOrDefault()
            }));

        nullableHop1.Should().Contain("(select t2.code");
        nullableHop1.Should().Contain("from t304_nullable_parent as t1");
        nullableHop1.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        nullableHop1.Should().NotContain("apply");

        // Nullable foreign key at hop 2+: the same correlated projection two levels up.
        var nullableHop2 = SqlOf(ctx, ctx.From<T304NullableParent>()
            .Select(p => new
            {
                p.Id,
                Label = ctx.From<T304Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => p.Group!.Root!.Label)
                    .FirstOrDefault()
            }));

        nullableHop2.Should().Contain("(select t3.label");
        nullableHop2.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        nullableHop2.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        nullableHop2.Should().Contain("from t304_child as t4");
        nullableHop2.Should().Contain("t4.parent_id = t1.id");
        nullableHop2.Should().NotContain("apply");
    }

    /// <summary>
    /// A5: two commands of one shape, each capturing its own <c>value</c> - the same CLR type under the
    /// same captured name, hence the same parameter name - must keep their parameter identities
    /// isolated. Preparing the sibling must not refresh this plan's parameter, and the reference hop
    /// itself must stay the joined alias rather than become a parameter.
    /// </summary>
    [Fact]
    public void ParameterIdentity_Isolated()
    {
        using var ctx = CreateContext();

        var first = Prepare(ctx, ParameterCommand(ctx, "alpha"));
        var second = Prepare(ctx, ParameterCommand(ctx, "beta"));

        first.Should().NotBeSameAs(second, "each command owns its prepared plan");

        var firstParameters = first.DbCommandParams.Cast<DbParameter>().ToArray();
        var secondParameters = second.DbCommandParams.Cast<DbParameter>().ToArray();

        firstParameters.Should().ContainSingle();
        secondParameters.Should().ContainSingle();
        firstParameters[0].ParameterName.Should().Be(secondParameters[0].ParameterName,
            "both captured locals are named the same and have the same CLR type");
        firstParameters[0].Value.Should().Be("alpha", "preparing the sibling plan must not refresh this one");
        secondParameters[0].Value.Should().Be("beta");

        Shape(first.DbCommand.CommandText).Should().Contain("t2.code",
            "the reference hop renders as the joined outer alias, not as a parameter");
        firstParameters.Select(p => p.ParameterName)
            .Should().NotContain(name => name.Contains("navigation", StringComparison.OrdinalIgnoreCase),
                "no free navigation alias may leak as a parameter");
    }

    /// <summary>
    /// A5: the same reference path used by two correlated predicates of one command must reuse one
    /// joined parameter - one LEFT JOIN for the path, both EXISTS bodies reading the same alias.
    /// </summary>
    [Fact]
    public void RepeatedPath_ReusesJoin()
    {
        using var ctx = CreateContext();
        var inner = ctx.From<T304Child>();

        var sql = SqlOf(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code))
                     && SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id));

        Occurrences(sql, "left join t304_group").Should().Be(1,
            "one command keeps one joined parameter per reference path");
        Occurrences(sql, "t2.code").Should().Be(2, "both correlated bodies read the same joined alias");
        Occurrences(sql, "exists(select *").Should().Be(2);
        sql.Should().NotContain("apply");
    }

    /// <summary>
    /// A2/A6: the portable shape has no unbound alias - the joined parameter is registered as an outer
    /// reference, so neither the text nor the parameter list carries a <c>navigation</c> placeholder.
    /// </summary>
    [Fact]
    public void PortableShape_HasNoUnboundAlias()
    {
        using var ctx = CreateContext();
        var inner = ctx.From<T304Child>();

        var prepared = Prepare(ctx, ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id));

        var sql = Shape(prepared.DbCommand.CommandText);

        sql.Should().NotContain("navigation",
            "the joined reference is registered as an outer reference, never a free navigation parameter");
        sql.Should().Contain("left join t304_group as t2 on t1.group_id = t2.id");
        sql.Should().Contain("left join t304_root as t3 on t2.root_id = t3.id");
        sql.Should().Contain("exists(select *");
        Occurrences(sql, "exists(select *").Should().Be(1);

        prepared.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().NotContain(name => name.Contains("navigation", StringComparison.OrdinalIgnoreCase),
                "an unbound alias must not be rebound as a parameter");
    }

}

[SqlTable("t304_root")]
public sealed class T304Root
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t304_group")]
public sealed class T304Group
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    [Column("root_id")]
    public int RootId { get; set; }

    public T304Root? Root { get; set; }
}

[SqlTable("t304_parent")]
public sealed class T304Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    public T304Group? Group { get; set; }
}

[SqlTable("t304_nullable_parent")]
public sealed class T304NullableParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int? GroupId { get; set; }

    public T304Group? Group { get; set; }
}

[SqlTable("t304_child")]
public sealed class T304Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("group_code")]
    public string? GroupCode { get; set; }

    public T304Parent? Parent { get; set; }
}
